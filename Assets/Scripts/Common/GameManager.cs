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
