using System.Collections;
using Core.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Core.UI
{
    /// <summary>
    /// 전투 HUD의 기본 골격을 담당하는 컨트롤러.
    /// 플레이어/보스 체력 표시와 고정형 데미지 텍스트 앵커를 한곳에서 관리한다.
    /// </summary>
    public class CombatHUDController : MonoBehaviour
    {
        [Header("플레이어 HUD")]
        [SerializeField] private Image _playerTorsoImage;
        [SerializeField] private Image _playerHpFill;
        [FormerlySerializedAs("_playerHpText")]
        [SerializeField] private TMP_Text _playerNameText;
        [SerializeField] private string _playerNameLabel = "Player";

        [Header("보스 HUD")]
        [SerializeField] private Image _bossHpFill;
        [FormerlySerializedAs("_bossHpText")]
        [SerializeField] private TMP_Text _bossNameText;
        [SerializeField] private string _bossNameLabel = "Dragon";

        [Header("고정형 데미지 피드백")]
        [SerializeField] private TMP_Text _damageFeedbackText;
        [SerializeField] private Color _hitColor = new Color(1f, 0.88f, 0.15f, 1f);
        [SerializeField, Min(1f)] private float _hitScale = 1.15f;
        [SerializeField, Min(0.05f)] private float _feedbackDuration = 0.3f;

        [Header("데이터 소스 (선택)")]
        [SerializeField] private Health _playerHealthSource;
        [SerializeField] private Health _bossHealthSource;

        private Health _playerHealth;
        private Health _bossHealth;
        private bool _isHealthEventsBound;
        private Coroutine _feedbackRoutine;
        private Vector3 _damageFeedbackBaseScale = Vector3.one;

        public Health PlayerHealth => _playerHealth;
        public Health BossHealth => _bossHealth;

        private void Awake()
        {
            ApplyNameLabels();

            if (_damageFeedbackText != null)
            {
                _damageFeedbackBaseScale = _damageFeedbackText.transform.localScale;
                if (_damageFeedbackBaseScale == Vector3.zero)
                {
                    _damageFeedbackBaseScale = Vector3.one;
                }
            }
        }

        private void Start()
        {
            HideDamageFeedbackImmediate();

            // 인스펙터로 체력 참조를 미리 연결한 경우 시작 시 자동 바인딩한다.
            if (_playerHealthSource != null || _bossHealthSource != null)
            {
                Initialize(_playerHealthSource, _bossHealthSource);
            }
        }

        private void OnDestroy()
        {
            UnbindHealthEvents();
            HideDamageFeedbackImmediate();
        }

        /// <summary>
        /// 외부에서 체력 참조를 주입한다.
        /// 실제 이벤트 구독은 다음 단계에서 연결한다.
        /// </summary>
        public void Initialize(Health playerHealth, Health bossHealth)
        {
            UnbindHealthEvents();

            _playerHealth = playerHealth;
            _bossHealth = bossHealth;

            BindHealthEvents();
            RefreshAllHealthBars();
        }

        /// <summary>
        /// 플레이어 토르소 이미지를 설정한다. 스프라이트가 없으면 이미지 슬롯을 숨긴다.
        /// </summary>
        public void SetPlayerTorso(Sprite torsoSprite)
        {
            if (_playerTorsoImage == null) return;

            _playerTorsoImage.sprite = torsoSprite;
            _playerTorsoImage.enabled = torsoSprite != null;
        }

        /// <summary>
        /// 플레이어 체력 UI를 갱신한다.
        /// 이름 라벨은 별도 필드로 관리한다.
        /// </summary>
        public void SetPlayerHpNormalized(float ratio, int current, int max)
        {
            _ = current;
            _ = max;

            float clampedRatio = Mathf.Clamp01(ratio);

            if (_playerHpFill != null)
            {
                _playerHpFill.fillAmount = clampedRatio;
            }
        }

        /// <summary>
        /// 보스 체력 UI를 갱신한다.
        /// 이름 라벨은 별도 필드로 관리한다.
        /// </summary>
        public void SetBossHpNormalized(float ratio, int current, int max)
        {
            _ = current;
            _ = max;

            float clampedRatio = Mathf.Clamp01(ratio);

            if (_bossHpFill != null)
            {
                _bossHpFill.fillAmount = clampedRatio;
            }
        }

        private void BindHealthEvents()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnDamageTaken += HandlePlayerDamaged;
                _playerHealth.OnDeath += HandlePlayerDied;
            }

            if (_bossHealth != null)
            {
                _bossHealth.OnDamageTaken += HandleBossDamaged;
                _bossHealth.OnDeath += HandleBossDied;
            }

            _isHealthEventsBound = true;
        }

        private void UnbindHealthEvents()
        {
            if (!_isHealthEventsBound) return;

            if (_playerHealth != null)
            {
                _playerHealth.OnDamageTaken -= HandlePlayerDamaged;
                _playerHealth.OnDeath -= HandlePlayerDied;
            }

            if (_bossHealth != null)
            {
                _bossHealth.OnDamageTaken -= HandleBossDamaged;
                _bossHealth.OnDeath -= HandleBossDied;
            }

            _isHealthEventsBound = false;
        }

        private void HandlePlayerDamaged(int damage)
        {
            _ = damage;
            RefreshPlayerHealthBar();
        }

        private void HandlePlayerDied()
        {
            RefreshPlayerHealthBar();
        }

        private void HandleBossDamaged(int damage)
        {
            _ = damage;
            RefreshBossHealthBar();
        }

        private void HandleBossDied()
        {
            RefreshBossHealthBar();
        }

        private void RefreshAllHealthBars()
        {
            RefreshPlayerHealthBar();
            RefreshBossHealthBar();
        }

        private void RefreshPlayerHealthBar()
        {
            if (_playerHealth == null) return;
            SetPlayerHpNormalized(_playerHealth.HealthRatio, _playerHealth.CurrentHealth, _playerHealth.MaxHealth);
        }

        private void RefreshBossHealthBar()
        {
            if (_bossHealth == null) return;
            SetBossHpNormalized(_bossHealth.HealthRatio, _bossHealth.CurrentHealth, _bossHealth.MaxHealth);
        }

        /// <summary>
        /// 플레이어 이름 라벨을 설정한다.
        /// </summary>
        public void SetPlayerName(string playerName)
        {
            _playerNameLabel = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();

            if (_playerNameText != null)
            {
                _playerNameText.text = _playerNameLabel;
            }
        }

        /// <summary>
        /// 보스 이름 라벨을 설정한다.
        /// </summary>
        public void SetBossName(string bossName)
        {
            _bossNameLabel = string.IsNullOrWhiteSpace(bossName) ? "Dragon" : bossName.Trim();

            if (_bossNameText != null)
            {
                _bossNameText.text = _bossNameLabel;
            }
        }

        /// <summary>
        /// 고정 위치 데미지 피드백 텍스트를 표시한다.
        /// 적중 시에만 텍스트를 노출하고, 짧은 페이드 아웃을 적용한다.
        /// </summary>
        public void ShowDamageFeedback(bool isHit, int totalDamage)
        {
            if (_damageFeedbackText == null) return;

            if (!isHit)
            {
                // 비적중 시에는 텍스트를 표시하지 않는다.
                HideDamageFeedbackImmediate();
                return;
            }

            _damageFeedbackText.text = $"HIT {Mathf.Max(0, totalDamage)}";

            Color feedbackColor = _hitColor;
            feedbackColor.a = 1f;
            _damageFeedbackText.color = feedbackColor;
            _damageFeedbackText.transform.localScale = _damageFeedbackBaseScale * _hitScale;
            _damageFeedbackText.gameObject.SetActive(true);

            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
            }

            _feedbackRoutine = StartCoroutine(PlayDamageFeedbackRoutine());
        }

        /// <summary>
        /// HUD 전체 표시 상태를 전환한다.
        /// </summary>
        public void ShowHud(bool visible)
        {
            if (_playerTorsoImage != null)
            {
                _playerTorsoImage.gameObject.SetActive(visible);
            }

            if (_playerHpFill != null)
            {
                _playerHpFill.gameObject.SetActive(visible);
            }

            if (_playerNameText != null)
            {
                _playerNameText.gameObject.SetActive(visible);
            }

            if (_bossHpFill != null)
            {
                _bossHpFill.gameObject.SetActive(visible);
            }

            if (_bossNameText != null)
            {
                _bossNameText.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                HideDamageFeedbackImmediate();
            }
        }

        private void ApplyNameLabels()
        {
            if (_playerNameText != null)
            {
                _playerNameText.text = string.IsNullOrWhiteSpace(_playerNameLabel) ? "Player" : _playerNameLabel;
            }

            if (_bossNameText != null)
            {
                _bossNameText.text = string.IsNullOrWhiteSpace(_bossNameLabel) ? "Dragon" : _bossNameLabel;
            }
        }

        private IEnumerator PlayDamageFeedbackRoutine()
        {
            float elapsed = 0f;
            Vector3 startScale = _damageFeedbackBaseScale * _hitScale;
            Vector3 endScale = _damageFeedbackBaseScale;
            Color baseColor = _hitColor;

            while (elapsed < _feedbackDuration)
            {
                if (_damageFeedbackText == null)
                {
                    _feedbackRoutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _feedbackDuration);

                Color frameColor = baseColor;
                frameColor.a = 1f - t;
                _damageFeedbackText.color = frameColor;
                _damageFeedbackText.transform.localScale = Vector3.Lerp(startScale, endScale, t);

                yield return null;
            }

            HideDamageFeedbackImmediate();
        }

        private void HideDamageFeedbackImmediate()
        {
            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
                _feedbackRoutine = null;
            }

            if (_damageFeedbackText == null) return;

            _damageFeedbackText.gameObject.SetActive(false);
            _damageFeedbackText.transform.localScale = _damageFeedbackBaseScale;

            Color resetColor = _hitColor;
            resetColor.a = 1f;
            _damageFeedbackText.color = resetColor;
        }
    }
}
