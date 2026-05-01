using System;
using Core.Boss;
using Core.Combat;
using Core.Multiplayer;
using Core.Player;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.GameFlow
{
    public enum GameFlowState
    {
        InGame,
        GameOver
    }

    public enum GameResult
    {
        None,
        Victory,
        Defeated
    }

    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        private const int ExpectedMultiplayerPlayerCount = 2;

        [Header("Health References")]
        [SerializeField] private Health _playerHealth;
        [SerializeField] private Health _bossHealth;

        [Header("GameOver UI")]
        [SerializeField] private GameObject _gameOverRoot;
        [SerializeField] private GameObject _victoryImageRoot;
        [SerializeField] private GameObject _defeatedImageRoot;
        [SerializeField] private GameObject _resultTextRoot;
        [SerializeField] private TMP_Text _resultLabel;
        [SerializeField, TextArea(2, 4)] private string _victoryText = "Victory";
        [SerializeField, TextArea(2, 4)] private string _defeatedText = "Try Again?\n(Press Enter to Restart)";
        [SerializeField, TextArea(1, 2)] private string _multiplayerDefeatedTitle = "Defeated";
        [SerializeField] private string _multiplayerRetryPromptFormat = "Press Enter to Play ({0}/{1})";

        [Header("Animation (Optional)")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _victoryTrigger = "Victory";
        [SerializeField] private string _defeatedTrigger = "Defeated";

        [Header("Input")]
        [SerializeField] private KeyCode _restartKey = KeyCode.Return;
        [SerializeField] private float _multiplayerRetryRestartDelay = 0.35f;

        private bool _isHealthEventsBound;
        private bool _playerDead;
        private bool _bossDead;
        private bool _isGameOverResolved;
        private bool _isSceneLoading;
        private int _lastMultiplayerRetryReadyCount = -1;
        private int _lastMultiplayerRetryTotalCount = -1;
        private int _stableMultiplayerRetryReadyCount;
        private int _stableMultiplayerRetryTotalCount = ExpectedMultiplayerPlayerCount;
        private bool _hasMultiplayerRetryConsensus;
        private float _multiplayerRetryConsensusReachedTime = -1f;
        private TMP_Text[] _cachedResultTextLabels = Array.Empty<TMP_Text>();

        public GameFlowState CurrentState { get; private set; } = GameFlowState.InGame;
        public GameResult CurrentResult { get; private set; } = GameResult.None;

        private void Awake()
        {
            EnsureSoloSceneAvatarStateIfNeeded();
            ResolveHealthReferences();
            ResolveGameOverUiReferences();
            HideGameOverUI();
        }

        private void OnEnable()
        {
            BindHealthEvents();
        }

        private void OnDisable()
        {
            UnbindHealthEvents();
        }

        private void Start()
        {
            CurrentState = GameFlowState.InGame;
            CurrentResult = GameResult.None;
            _isGameOverResolved = false;
            _isSceneLoading = false;
            ResetMultiplayerRetryUiState();

            ResolveHealthReferences();
            _playerDead = _playerHealth != null && _playerHealth.IsDead;
            _bossDead = _bossHealth != null && _bossHealth.IsDead;

            Debug.Log(
                $"[SoloDebug][GamePlayScene][Start] scene={SceneManager.GetActiveScene().path} " +
                $"hasActiveSession={(MultiplayerSessionService.HasInstance && MultiplayerSessionService.Instance.HasActiveSession)} " +
                $"solo={DescribeSceneAvatar("SoloPlayerAvatar")} " +
                $"multi={DescribeSceneAvatar("MultiPlayerAvatar")}");
        }

        private void EnsureSoloSceneAvatarStateIfNeeded()
        {
            if (IsMultiplayerGameplaySessionActive())
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid()
                || !string.Equals(activeScene.path, MultiplayerScenePaths.GamePlayScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            GameObject soloAvatar = FindSceneObjectByName("SoloPlayerAvatar");
            if (soloAvatar != null && !soloAvatar.activeSelf)
            {
                soloAvatar.SetActive(true);
            }

            GameObject multiAvatar = FindSceneObjectByName("MultiPlayerAvatar");
            if (multiAvatar != null && multiAvatar.activeSelf)
            {
                multiAvatar.SetActive(false);
            }
        }

        private void Update()
        {
            if (CurrentState != GameFlowState.GameOver) return;
            if (_isSceneLoading) return;

            if (IsMultiplayerGameplayActive())
            {
                if (CurrentResult == GameResult.Defeated)
                {
                    HandleMultiplayerDefeatRestart();
                }

                return;
            }

            if (!IsRestartPressed()) return;

            RestartCurrentScene();
        }

        private void LateUpdate()
        {
            if (CurrentState != GameFlowState.InGame) return;
            if (_isGameOverResolved) return;

            if (IsMultiplayerGameplayActive())
            {
                ResolveMultiplayerGameOver();
                return;
            }

            if (!_playerDead && !_bossDead) return;

            if (_bossDead)
            {
                ResolveGameOver(GameResult.Victory);
                return;
            }

            ResolveGameOver(GameResult.Defeated);
        }

        private void HandlePlayerDeath()
        {
            _playerDead = true;
        }

        private void HandleBossDeath()
        {
            _bossDead = true;
        }

        private void ResolveMultiplayerGameOver()
        {
            if (TryGetMultiplayerBossDeadState(out bool isBossDead) && isBossDead)
            {
                ResolveGameOver(GameResult.Victory);
                return;
            }

            if (!TryGetMultiplayerPlayerDeathState(out bool areAllPlayersDead))
            {
                return;
            }

            if (areAllPlayersDead)
            {
                ResolveGameOver(GameResult.Defeated);
            }
        }

        private bool TryGetMultiplayerBossDeadState(out bool isBossDead)
        {
            isBossDead = false;
            if (!MultiplayerRuntimeRoot.HasInstance)
            {
                if (_bossHealth != null)
                {
                    isBossDead = _bossHealth.IsDead;
                    return true;
                }

                return false;
            }

            MultiplayerBossAuthorityBridge bridge = MultiplayerRuntimeRoot.Instance.BossAuthorityBridge;
            if (bridge != null && bridge.HasLatestBossState)
            {
                isBossDead = bridge.IsBossDead;
                return true;
            }

            if (MultiplayerRuntimeRoot.Instance.NetworkManager != null
                && MultiplayerRuntimeRoot.Instance.NetworkManager.IsServer
                && _bossHealth != null)
            {
                isBossDead = _bossHealth.IsDead;
                return true;
            }

            return false;
        }

        private bool TryGetMultiplayerPlayerDeathState(out bool areAllPlayersDead)
        {
            areAllPlayersDead = false;

            int avatarCount = MultiplayerPlayerAvatar.GetActiveAvatarCount();
            if (avatarCount < ExpectedMultiplayerPlayerCount)
            {
                return false;
            }

            int resolvedAvatarCount = 0;
            int deadAvatarCount = 0;

            for (int i = 0; i < avatarCount; i++)
            {
                if (!MultiplayerPlayerAvatar.TryGetActiveAvatar(i, out MultiplayerPlayerAvatar avatar) || avatar == null)
                {
                    continue;
                }

                if (!avatar.TryGetResultDeathState(out bool isDead))
                {
                    return false;
                }

                resolvedAvatarCount++;
                if (isDead)
                {
                    deadAvatarCount++;
                }
            }

            if (resolvedAvatarCount < ExpectedMultiplayerPlayerCount)
            {
                return false;
            }

            areAllPlayersDead = deadAvatarCount >= resolvedAvatarCount;
            return true;
        }

        private void HandleMultiplayerDefeatRestart()
        {
            if (IsRestartPressed() && MultiplayerPlayerAvatar.TryGetLocalAvatar(out MultiplayerPlayerAvatar localAvatar))
            {
                localAvatar.SubmitRetryReadyIfOwner();
            }

            RefreshMultiplayerRetryState();
            UpdateMultiplayerDefeatedUi(force: false);

            if (!ShouldRestartMultiplayerGameplay())
            {
                return;
            }

            RestartCurrentScene();
        }

        private bool ShouldRestartMultiplayerGameplay()
        {
            if (!MultiplayerRuntimeRoot.HasInstance || MultiplayerRuntimeRoot.Instance.NetworkManager == null)
            {
                return false;
            }

            if (!MultiplayerRuntimeRoot.Instance.NetworkManager.IsServer)
            {
                return false;
            }

            RefreshMultiplayerRetryState();
            if (!_hasMultiplayerRetryConsensus || _multiplayerRetryConsensusReachedTime < 0f)
            {
                return false;
            }

            return Time.unscaledTime - _multiplayerRetryConsensusReachedTime >= Mathf.Max(0f, _multiplayerRetryRestartDelay);
        }

        private bool TryGetMultiplayerRetryCounts(out int readyCount, out int totalCount)
        {
            readyCount = 0;
            totalCount = 0;

            int avatarCount = MultiplayerPlayerAvatar.GetActiveAvatarCount();
            for (int i = 0; i < avatarCount; i++)
            {
                if (!MultiplayerPlayerAvatar.TryGetActiveAvatar(i, out MultiplayerPlayerAvatar avatar) || avatar == null)
                {
                    continue;
                }

                totalCount++;
                if (avatar.IsRetryReady)
                {
                    readyCount++;
                }
            }

            return totalCount > 0;
        }

        private void ResolveGameOver(GameResult result)
        {
            if (_isGameOverResolved) return;

            _isGameOverResolved = true;
            CurrentState = GameFlowState.GameOver;
            CurrentResult = result;

            if (IsMultiplayerGameplayActive() && result == GameResult.Defeated)
            {
                ResetMultiplayerRetryUiState();
                UpdateMultiplayerDefeatedUi(force: true);
            }
            else
            {
                ShowGameOverUI(
                    result,
                    result == GameResult.Victory
                        ? ResolveVictoryDisplayText()
                        : ResolveSoloDefeatedDisplayText());
            }

            if (_animator != null)
            {
                bool isVictory = result == GameResult.Victory;
                _animator.SetTrigger(isVictory ? _victoryTrigger : _defeatedTrigger);
            }
        }

        private void RestartCurrentScene()
        {
            if (_isSceneLoading) return;

            _isSceneLoading = true;
            if (IsMultiplayerGameplayActive())
            {
                RestartMultiplayerGameplayAsync();
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }

        private async void RestartMultiplayerGameplayAsync()
        {
            try
            {
                if (!MultiplayerSessionService.HasInstance)
                {
                    throw new InvalidOperationException("Multiplayer session service is missing.");
                }

                await MultiplayerSessionService.Instance.RestartGameplayAsync();
            }
            catch (Exception ex)
            {
                _isSceneLoading = false;
                Debug.LogError($"GameManager: Multiplayer restart failed. {ex.Message}");
            }
        }

        private bool IsRestartPressed()
        {
            bool isRestartPressed = Input.GetKeyDown(_restartKey);
            if (_restartKey == KeyCode.Return)
            {
                isRestartPressed = isRestartPressed || Input.GetKeyDown(KeyCode.KeypadEnter);
            }

            return isRestartPressed;
        }

        private bool IsMultiplayerGameplayActive()
        {
            if (!IsMultiplayerGameplaySessionActive())
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return false;
            }

            return string.Equals(activeScene.path, MultiplayerScenePaths.GamePlayScenePath, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(activeScene.path, MultiplayerScenePaths.FullGamePlayScenePath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMultiplayerGameplaySessionActive()
        {
            return MultiplayerSessionService.HasInstance && MultiplayerSessionService.Instance.HasActiveSession;
        }

        private void UpdateMultiplayerDefeatedUi(bool force)
        {
            RefreshMultiplayerRetryState();

            int totalCount = Mathf.Max(ExpectedMultiplayerPlayerCount, _stableMultiplayerRetryTotalCount);
            int readyCount = Mathf.Clamp(_stableMultiplayerRetryReadyCount, 0, totalCount);
            if (_hasMultiplayerRetryConsensus)
            {
                readyCount = totalCount;
            }

            if (!force
                && readyCount == _lastMultiplayerRetryReadyCount
                && totalCount == _lastMultiplayerRetryTotalCount)
            {
                return;
            }

            _lastMultiplayerRetryReadyCount = readyCount;
            _lastMultiplayerRetryTotalCount = totalCount;
            ShowGameOverUI(GameResult.Defeated, BuildMultiplayerDefeatedMessage(readyCount, totalCount));
        }

        private string BuildMultiplayerDefeatedMessage(int readyCount, int totalCount)
        {
            int clampedTotalCount = Mathf.Max(ExpectedMultiplayerPlayerCount, totalCount);
            int clampedReadyCount = Mathf.Clamp(readyCount, 0, clampedTotalCount);
            string retryPrompt = string.Format(_multiplayerRetryPromptFormat, clampedReadyCount, clampedTotalCount);
            if (_defeatedImageRoot != null)
            {
                return retryPrompt;
            }

            return $"{_multiplayerDefeatedTitle}\n{retryPrompt}";
        }

        private string ResolveVictoryDisplayText()
        {
            return _victoryImageRoot != null ? string.Empty : _victoryText;
        }

        private string ResolveSoloDefeatedDisplayText()
        {
            return _defeatedText;
        }

        private void RefreshMultiplayerRetryState()
        {
            if (!TryGetMultiplayerRetryCounts(out int runtimeReadyCount, out int runtimeTotalCount))
            {
                return;
            }

            int resolvedTotalCount = Mathf.Max(ExpectedMultiplayerPlayerCount, runtimeTotalCount);
            int resolvedReadyCount = Mathf.Clamp(runtimeReadyCount, 0, resolvedTotalCount);

            _stableMultiplayerRetryTotalCount = Mathf.Max(_stableMultiplayerRetryTotalCount, resolvedTotalCount);
            _stableMultiplayerRetryReadyCount = Mathf.Max(_stableMultiplayerRetryReadyCount, resolvedReadyCount);

            if (_stableMultiplayerRetryReadyCount >= _stableMultiplayerRetryTotalCount)
            {
                _hasMultiplayerRetryConsensus = true;
                _stableMultiplayerRetryReadyCount = _stableMultiplayerRetryTotalCount;

                if (_multiplayerRetryConsensusReachedTime < 0f)
                {
                    _multiplayerRetryConsensusReachedTime = Time.unscaledTime;
                }
            }
        }

        private void ResetMultiplayerRetryUiState()
        {
            _lastMultiplayerRetryReadyCount = -1;
            _lastMultiplayerRetryTotalCount = -1;
            _stableMultiplayerRetryReadyCount = 0;
            _stableMultiplayerRetryTotalCount = ExpectedMultiplayerPlayerCount;
            _hasMultiplayerRetryConsensus = false;
            _multiplayerRetryConsensusReachedTime = -1f;
        }

        private void ResolveHealthReferences()
        {
            if (_playerHealth == null)
            {
                PlayerController playerController = FindObjectOfType<PlayerController>();
                if (playerController != null)
                {
                    _playerHealth = playerController.GetComponent<Health>();
                }
            }

            if (_bossHealth == null)
            {
                BossController bossController = FindObjectOfType<BossController>();
                if (bossController != null)
                {
                    _bossHealth = bossController.GetComponent<Health>();
                }
            }
        }

        private static string DescribeSceneAvatar(string avatarName)
        {
            GameObject avatarObject = FindSceneObjectByName(avatarName);
            if (avatarObject == null)
            {
                return $"{avatarName}(missing)";
            }

            Health health = avatarObject.GetComponent<Health>();
            PlayerController playerController = avatarObject.GetComponent<PlayerController>();
            string healthText = health != null ? $"{health.CurrentHealth}/{health.MaxHealth}" : "n/a";
            string authorityText = playerController != null ? playerController.CurrentActionAuthorityMode.ToString() : "n/a";
            string simulationText = playerController != null ? playerController.SimulationMode.ToString() : "n/a";

            return $"{avatarName}(activeSelf={avatarObject.activeSelf}, activeInHierarchy={avatarObject.activeInHierarchy}, hp={healthText}, simulation={simulationText}, authority={authorityText})";
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return null;
            }

            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                GameObject found = FindSceneChildRecursive(rootObjects[i].transform, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static GameObject FindSceneChildRecursive(Transform current, string objectName)
        {
            if (current == null)
            {
                return null;
            }

            if (string.Equals(current.name, objectName, StringComparison.Ordinal))
            {
                return current.gameObject;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                GameObject found = FindSceneChildRecursive(current.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public void SetPlayerHealth(Health playerHealth)
        {
            if (_playerHealth == playerHealth)
            {
                return;
            }

            bool shouldRebind = _isHealthEventsBound;
            if (shouldRebind)
            {
                UnbindHealthEvents();
            }

            _playerHealth = playerHealth;
            _playerDead = _playerHealth != null && _playerHealth.IsDead;

            if (shouldRebind && isActiveAndEnabled)
            {
                BindHealthEvents();
            }
        }

        private void ResolveGameOverUiReferences()
        {
            if (_gameOverRoot != null
                && _resultLabel != null
                && _victoryImageRoot != null
                && _defeatedImageRoot != null
                && _resultTextRoot != null)
            {
                return;
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null) continue;

                Transform gameOverTransform = FindChildRecursive(canvas.transform, "GameOver_Panel");
                if (gameOverTransform == null) continue;

                if (_gameOverRoot == null)
                {
                    _gameOverRoot = gameOverTransform.gameObject;
                }

                if (_victoryImageRoot == null)
                {
                    Transform victoryImageTransform = FindChildRecursive(gameOverTransform, "Image_Win");
                    _victoryImageRoot = victoryImageTransform != null ? victoryImageTransform.gameObject : null;
                }

                if (_defeatedImageRoot == null)
                {
                    Transform defeatedImageTransform = FindChildRecursive(gameOverTransform, "Image_Lose");
                    _defeatedImageRoot = defeatedImageTransform != null ? defeatedImageTransform.gameObject : null;
                }

                if (_resultLabel == null)
                {
                    _resultLabel = FindGameResultLabel(gameOverTransform);
                }

                if (_resultTextRoot == null)
                {
                    Transform resultTextTransform = FindChildRecursive(gameOverTransform, "Text_GameResult");
                    if (resultTextTransform == null && _resultLabel != null)
                    {
                        resultTextTransform = ResolveDirectChildAncestor(_resultLabel.transform, gameOverTransform);
                    }

                    _resultTextRoot = resultTextTransform != null
                        ? resultTextTransform.gameObject
                        : (_resultLabel != null ? _resultLabel.gameObject : null);
                }

                CacheResultTextLabels();

                if (_gameOverRoot != null)
                {
                    return;
                }
            }
        }

        private static Transform FindChildRecursive(Transform root, string expectedName)
        {
            if (root == null || string.IsNullOrWhiteSpace(expectedName))
            {
                return null;
            }

            string normalizedExpectedName = expectedName.Trim();
            if (root.name.Trim() == normalizedExpectedName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform match = FindChildRecursive(child, normalizedExpectedName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static TMP_Text FindGameResultLabel(Transform gameOverRoot)
        {
            if (gameOverRoot == null)
            {
                return null;
            }

            TMP_Text[] labels = gameOverRoot.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text label = labels[i];
                if (label == null) continue;

                string normalizedName = label.name.Replace(" ", string.Empty);
                if (normalizedName.Contains("GameResult"))
                {
                    return label;
                }
            }

            return labels.Length > 0 ? labels[0] : null;
        }

        private static Transform ResolveDirectChildAncestor(Transform target, Transform expectedRoot)
        {
            if (target == null || expectedRoot == null)
            {
                return null;
            }

            Transform current = target;
            while (current != null && current.parent != null)
            {
                if (current.parent == expectedRoot)
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private void CacheResultTextLabels()
        {
            if (_resultTextRoot != null)
            {
                _cachedResultTextLabels = _resultTextRoot.GetComponentsInChildren<TMP_Text>(true);
                if (_cachedResultTextLabels.Length > 0)
                {
                    return;
                }
            }

            _cachedResultTextLabels = _resultLabel != null
                ? new[] { _resultLabel }
                : Array.Empty<TMP_Text>();
        }

        private void BindHealthEvents()
        {
            if (_isHealthEventsBound) return;

            ResolveHealthReferences();

            if (_playerHealth != null)
            {
                _playerHealth.OnDeath += HandlePlayerDeath;
            }

            if (_bossHealth != null)
            {
                _bossHealth.OnDeath += HandleBossDeath;
            }

            _isHealthEventsBound = true;
        }

        private void UnbindHealthEvents()
        {
            if (!_isHealthEventsBound) return;

            if (_playerHealth != null)
            {
                _playerHealth.OnDeath -= HandlePlayerDeath;
            }

            if (_bossHealth != null)
            {
                _bossHealth.OnDeath -= HandleBossDeath;
            }

            _isHealthEventsBound = false;
        }

        private void ShowGameOverUI(GameResult result, string message)
        {
            if (_gameOverRoot != null)
            {
                _gameOverRoot.SetActive(true);
            }

            if (_victoryImageRoot != null)
            {
                _victoryImageRoot.SetActive(result == GameResult.Victory);
            }

            if (_defeatedImageRoot != null)
            {
                _defeatedImageRoot.SetActive(result == GameResult.Defeated);
            }

            // Victory는 공백을 유지하고, Defeated에서는 메시지를 표시한다.
            bool shouldShowText = true;
            const string blankResultText = " ";
            string displayText = result == GameResult.Defeated && !string.IsNullOrWhiteSpace(message)
                ? message
                : blankResultText;
            if (_resultTextRoot != null)
            {
                _resultTextRoot.SetActive(shouldShowText);
            }

            CacheResultTextLabels();
            for (int i = 0; i < _cachedResultTextLabels.Length; i++)
            {
                TMP_Text label = _cachedResultTextLabels[i];
                if (label == null)
                {
                    continue;
                }

                label.gameObject.SetActive(shouldShowText);
                label.text = displayText;
            }
        }

        private void HideGameOverUI()
        {
            if (_gameOverRoot != null)
            {
                _gameOverRoot.SetActive(false);
            }

            if (_victoryImageRoot != null)
            {
                _victoryImageRoot.SetActive(false);
            }

            if (_defeatedImageRoot != null)
            {
                _defeatedImageRoot.SetActive(false);
            }

            if (_resultTextRoot != null)
            {
                _resultTextRoot.SetActive(false);
            }

            CacheResultTextLabels();
            for (int i = 0; i < _cachedResultTextLabels.Length; i++)
            {
                TMP_Text label = _cachedResultTextLabels[i];
                if (label == null)
                {
                    continue;
                }

                label.gameObject.SetActive(false);
            }
        }
    }
}
