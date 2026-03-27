using Core.Boss;
using Core.Combat;
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
        [Header("Health References")]
        [SerializeField] private Health _playerHealth;
        [SerializeField] private Health _bossHealth;

        [Header("GameOver UI")]
        [SerializeField] private GameObject _gameOverRoot;
        [SerializeField] private TMP_Text _resultLabel;
        [SerializeField, TextArea(2, 4)] private string _victoryText = "Victory";
        [SerializeField, TextArea(2, 4)] private string _defeatedText = "Try Again?\n(Press Enter to Restart)";

        [Header("Animation (Optional)")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _victoryTrigger = "Victory";
        [SerializeField] private string _defeatedTrigger = "Defeated";

        [Header("Input")]
        [SerializeField] private KeyCode _restartKey = KeyCode.Return;

        private bool _isHealthEventsBound;
        private bool _playerDead;
        private bool _bossDead;
        private bool _isGameOverResolved;
        private bool _isSceneLoading;

        public GameFlowState CurrentState { get; private set; } = GameFlowState.InGame;
        public GameResult CurrentResult { get; private set; } = GameResult.None;

        private void Awake()
        {
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

            _playerDead = _playerHealth != null && _playerHealth.IsDead;
            _bossDead = _bossHealth != null && _bossHealth.IsDead;
        }

        private void Update()
        {
            if (CurrentState != GameFlowState.GameOver) return;
            if (_isSceneLoading) return;

            bool isRestartPressed = Input.GetKeyDown(_restartKey);
            if (_restartKey == KeyCode.Return)
            {
                isRestartPressed = isRestartPressed || Input.GetKeyDown(KeyCode.KeypadEnter);
            }

            if (!isRestartPressed) return;

            RestartCurrentScene();
        }

        private void LateUpdate()
        {
            if (CurrentState != GameFlowState.InGame) return;
            if (_isGameOverResolved) return;
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

        private void ResolveGameOver(GameResult result)
        {
            if (_isGameOverResolved) return;

            _isGameOverResolved = true;
            CurrentState = GameFlowState.GameOver;
            CurrentResult = result;

            bool isVictory = result == GameResult.Victory;
            ShowGameOverUI(isVictory ? _victoryText : _defeatedText);

            if (_animator != null)
            {
                _animator.SetTrigger(isVictory ? _victoryTrigger : _defeatedTrigger);
            }
        }

        private void RestartCurrentScene()
        {
            if (_isSceneLoading) return;

            _isSceneLoading = true;
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
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
            if (_gameOverRoot != null && _resultLabel != null)
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

                if (_resultLabel == null)
                {
                    _resultLabel = FindGameResultLabel(gameOverTransform);
                }

                if (_gameOverRoot != null && _resultLabel != null)
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

        private void ShowGameOverUI(string message)
        {
            if (_gameOverRoot != null)
            {
                _gameOverRoot.SetActive(true);
            }

            if (_resultLabel != null)
            {
                _resultLabel.text = message;
                _resultLabel.gameObject.SetActive(true);
            }
        }

        private void HideGameOverUI()
        {
            if (_gameOverRoot != null)
            {
                _gameOverRoot.SetActive(false);
            }

            if (_resultLabel != null)
            {
                _resultLabel.gameObject.SetActive(false);
            }
        }
    }
}
