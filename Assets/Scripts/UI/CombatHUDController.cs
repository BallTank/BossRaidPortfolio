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
        private const string PartnerHudPanelName = "PartnerHUD_Panel";
        private const string PartnerHpFillName = "Image_PartnerHP_Fill";
        private const string PartnerNameTextName = "Text_PartnerName";
        private const string PartnerPortraitImageName = "Image_PartnerPortrait_3P";
        private const string PartnerLegacyPortraitImageName = "Image_PartnerPortrait_2P";
        private const string ComboRootName = "Text_Combo";
        private const string DashFillImageName = "Image_Dash_Icon_Active3";

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

        [Header("파트너 HUD")]
        [SerializeField] private GameObject _partnerHudRoot;
        [SerializeField] private Image _partnerPortraitImage;
        [SerializeField] private Image _partnerLegacyPortraitImage;
        [SerializeField] private Image _partnerHpFill;
        [SerializeField] private TMP_Text _partnerNameText;
        [SerializeField] private string _partnerNameLabel = "Partner";

        [Header("고정형 데미지 피드백")]
        [SerializeField] private TMP_Text _damageFeedbackText;
        [SerializeField] private Color _hitColor = new Color(1f, 0.88f, 0.15f, 1f);
        [SerializeField, Min(1f)] private float _hitScale = 1.15f;
        [SerializeField, Min(0.05f)] private float _feedbackDuration = 0.3f;

        [Header("콤보 UI")]
        [SerializeField] private TMP_Text _comboText;

        [Header("Dash HUD")]
        [SerializeField] private Image _dashFillImage;

        [Header("데이터 소스 (선택)")]
        [SerializeField] private Health _playerHealthSource;
        [SerializeField] private Health _bossHealthSource;

        private Health _playerHealth;
        private Health _bossHealth;
        private bool _isHealthEventsBound;
        private Coroutine _feedbackRoutine;
        private Vector3 _damageFeedbackBaseScale = Vector3.one;
        private float _damageFeedbackVisibleAlpha = 1f;
        private bool _isHudVisible = true;
        private bool _isPartnerHudVisible;
        private GameObject _comboRoot;
        private bool _isComboVisible;
        private int _currentComboStep = 1;
        private int _lastObservedPlayerHealth = int.MinValue;
        private int _lastObservedPlayerMaxHealth = int.MinValue;
        private int _lastObservedBossHealth = int.MinValue;
        private int _lastObservedBossMaxHealth = int.MinValue;
        private Sprite _hostPortraitSprite;
        private Sprite _clientPortraitSprite;
        private bool _hasCachedMultiplayerPortraitSprites;

        public Health PlayerHealth => _playerHealth;
        public Health BossHealth => _bossHealth;

        private void Awake()
        {
            ApplyNameLabels();
            ResolvePartnerHudRoot();
            ResolvePartnerHudBindings();
            ApplyPartnerHudVisibility();
            ResolveComboText();
            HideComboImmediate();

            if (_damageFeedbackText != null)
            {
                _damageFeedbackBaseScale = _damageFeedbackText.transform.localScale;
                if (_damageFeedbackBaseScale == Vector3.zero)
                {
                    _damageFeedbackBaseScale = Vector3.one;
                }

                _damageFeedbackVisibleAlpha = ResolveDamageFeedbackVisibleAlpha();
            }
        }

        private void Start()
        {
            HideDamageFeedbackImmediate();
            HideComboImmediate();

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
            HideComboImmediate();
        }

        private void LateUpdate()
        {
            RefreshHealthBarsIfSourceChanged();
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
            _lastObservedPlayerHealth = int.MinValue;
            _lastObservedPlayerMaxHealth = int.MinValue;
            _lastObservedBossHealth = int.MinValue;
            _lastObservedBossMaxHealth = int.MinValue;

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
        /// 멀티플레이 viewer 기준으로 좌측/파트너 portrait를 재배치한다.
        /// Host 화면은 Host portrait가 좌측, Client 화면은 Client portrait가 좌측이다.
        /// </summary>
        public void SetViewerRelativePortraitLayout(bool isLocalHost)
        {
            ResolvePartnerHudBindings();
            CacheMultiplayerPortraitSprites();

            Sprite mainPortrait = isLocalHost ? _hostPortraitSprite : _clientPortraitSprite;
            Sprite partnerPortrait = isLocalHost ? _clientPortraitSprite : _hostPortraitSprite;
            ApplyPortraitLayout(mainPortrait, partnerPortrait);
        }

        /// <summary>
        /// HUD portrait를 prefab 기본 배치(Host 좌측, Client partner)로 되돌린다.
        /// solo 또는 HUD 재초기화 시 기준값으로 사용한다.
        /// </summary>
        public void ResetPortraitLayoutToDefault()
        {
            ResolvePartnerHudBindings();
            CacheMultiplayerPortraitSprites();
            ApplyPortraitLayout(_hostPortraitSprite, _clientPortraitSprite);
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

        private void RefreshHealthBarsIfSourceChanged()
        {
            if (_playerHealth != null
                && (_lastObservedPlayerHealth != _playerHealth.CurrentHealth
                    || _lastObservedPlayerMaxHealth != _playerHealth.MaxHealth))
            {
                RefreshPlayerHealthBar();
            }

            if (_bossHealth != null
                && (_lastObservedBossHealth != _bossHealth.CurrentHealth
                    || _lastObservedBossMaxHealth != _bossHealth.MaxHealth))
            {
                RefreshBossHealthBar();
            }
        }

        private void RefreshPlayerHealthBar()
        {
            if (_playerHealth == null) return;
            _lastObservedPlayerHealth = _playerHealth.CurrentHealth;
            _lastObservedPlayerMaxHealth = _playerHealth.MaxHealth;
            SetPlayerHpNormalized(_playerHealth.HealthRatio, _playerHealth.CurrentHealth, _playerHealth.MaxHealth);
        }

        private void RefreshBossHealthBar()
        {
            if (_bossHealth == null) return;
            _lastObservedBossHealth = _bossHealth.CurrentHealth;
            _lastObservedBossMaxHealth = _bossHealth.MaxHealth;
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
        /// 파트너 이름 라벨을 설정한다.
        /// </summary>
        public void SetPartnerName(string partnerName)
        {
            _partnerNameLabel = string.IsNullOrWhiteSpace(partnerName) ? "Partner" : partnerName.Trim();
            ResolvePartnerHudBindings();

            if (_partnerNameText != null)
            {
                _partnerNameText.text = _partnerNameLabel;
            }
        }

        /// <summary>
        /// 파트너 체력 UI를 갱신한다.
        /// </summary>
        public void SetPartnerHpNormalized(float ratio, int current, int max)
        {
            _ = current;
            _ = max;

            ResolvePartnerHudBindings();

            float clampedRatio = Mathf.Clamp01(ratio);
            if (_partnerHpFill != null)
            {
                _partnerHpFill.fillAmount = clampedRatio;
            }
        }

        /// <summary>
        /// 대시 준비도 UI fill 값을 갱신한다.
        /// </summary>
        public void SetDashReadyNormalized(float ratio)
        {
            ResolveDashHudBindings();
            if (_dashFillImage == null)
            {
                return;
            }

            _dashFillImage.fillAmount = Mathf.Clamp01(ratio);
        }

        /// <summary>
        /// 파트너 HUD 표시 상태를 전환한다.
        /// 실질적인 데이터 바인딩은 멀티플레이 gameplay sync 단계에서 확장한다.
        /// </summary>
        public void SetPartnerHudVisible(bool visible)
        {
            _isPartnerHudVisible = visible;
            ApplyPartnerHudVisibility();
        }

        /// <summary>
        /// 현재 콤보 단계를 HUD에 표시한다.
        /// 콤보 중에만 보이고, 일반 상태에서는 숨김을 유지한다.
        /// </summary>
        public void ShowCombo(int comboStep)
        {
            ResolveComboText();
            if (_comboText == null)
            {
                return;
            }

            _currentComboStep = Mathf.Max(1, comboStep);
            _comboText.text = _currentComboStep.ToString();
            _isComboVisible = true;
            ApplyComboVisibility();
        }

        /// <summary>
        /// 콤보 표시를 즉시 숨긴다.
        /// </summary>
        public void HideCombo()
        {
            _isComboVisible = false;
            ApplyComboVisibility();
        }

        /// <summary>
        /// 고정 위치 데미지 피드백 텍스트를 표시한다.
        /// 적중 시에만 텍스트를 노출하고, 짧은 페이드 아웃을 적용한다.
        /// </summary>
        public void ShowDamageFeedback(bool isHit, int totalDamage)
        {
            if (_damageFeedbackText == null) return;

            if (!isHit || _damageFeedbackVisibleAlpha <= 0f)
            {
                // 비적중 시에는 텍스트를 표시하지 않는다.
                HideDamageFeedbackImmediate();
                return;
            }

            _damageFeedbackText.text = $"HIT {Mathf.Max(0, totalDamage)}";

            _damageFeedbackText.color = GetDamageFeedbackVisibleColor();
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
            _isHudVisible = visible;

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

            if (_dashFillImage != null)
            {
                _dashFillImage.gameObject.SetActive(visible);
            }

            ApplyPartnerHudVisibility();

            if (!visible)
            {
                HideDamageFeedbackImmediate();
                HideComboImmediate();
            }
            else
            {
                ApplyComboVisibility();
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

            ResolvePartnerHudBindings();
            if (_partnerNameText != null)
            {
                _partnerNameText.text = string.IsNullOrWhiteSpace(_partnerNameLabel) ? "Partner" : _partnerNameLabel;
            }
        }

        private void ResolvePartnerHudRoot()
        {
            if (_partnerHudRoot != null)
            {
                return;
            }

            Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < childTransforms.Length; i++)
            {
                Transform childTransform = childTransforms[i];
                if (childTransform == null || !string.Equals(childTransform.name, PartnerHudPanelName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                _partnerHudRoot = childTransform.gameObject;
                break;
            }
        }

        private void ApplyPartnerHudVisibility()
        {
            ResolvePartnerHudRoot();
            if (_partnerHudRoot == null)
            {
                return;
            }

            _partnerHudRoot.SetActive(_isHudVisible && _isPartnerHudVisible);
        }

        private void ResolvePartnerHudBindings()
        {
            ResolvePartnerHudRoot();
            if (_partnerHudRoot == null)
            {
                return;
            }

            if (_partnerHpFill == null)
            {
                Transform partnerHpFillTransform = _partnerHudRoot.transform.Find(PartnerHpFillName);
                if (partnerHpFillTransform == null)
                {
                    partnerHpFillTransform = FindChildTransformByName(_partnerHudRoot.transform, PartnerHpFillName);
                }

                if (partnerHpFillTransform != null)
                {
                    _partnerHpFill = partnerHpFillTransform.GetComponent<Image>();
                }
            }

            if (_partnerNameText == null)
            {
                Transform partnerNameTransform = _partnerHudRoot.transform.Find(PartnerNameTextName);
                if (partnerNameTransform == null)
                {
                    partnerNameTransform = FindChildTransformByName(_partnerHudRoot.transform, PartnerNameTextName);
                }

                if (partnerNameTransform != null)
                {
                    _partnerNameText = partnerNameTransform.GetComponent<TMP_Text>();
                }
            }

            if (_partnerPortraitImage == null)
            {
                Transform partnerPortraitTransform = _partnerHudRoot.transform.Find(PartnerPortraitImageName);
                if (partnerPortraitTransform == null)
                {
                    partnerPortraitTransform = FindChildTransformByNameOrPrefix(_partnerHudRoot.transform, PartnerPortraitImageName);
                }

                if (partnerPortraitTransform != null)
                {
                    _partnerPortraitImage = partnerPortraitTransform.GetComponent<Image>();
                }
            }

            if (_partnerLegacyPortraitImage == null)
            {
                Transform partnerLegacyPortraitTransform = _partnerHudRoot.transform.Find(PartnerLegacyPortraitImageName);
                if (partnerLegacyPortraitTransform == null)
                {
                    partnerLegacyPortraitTransform = FindChildTransformByNameOrPrefix(_partnerHudRoot.transform, PartnerLegacyPortraitImageName);
                }

                if (partnerLegacyPortraitTransform != null)
                {
                    _partnerLegacyPortraitImage = partnerLegacyPortraitTransform.GetComponent<Image>();
                }
            }
        }

        private void ResolveDashHudBindings()
        {
            if (_dashFillImage != null)
            {
                return;
            }

            Transform dashFillTransform = transform.Find("Panel_Dash/Dash_Icon_Active/Image_Dash_Icon_Active3");
            if (dashFillTransform == null)
            {
                dashFillTransform = FindChildTransformByName(transform, DashFillImageName);
            }

            if (dashFillTransform != null)
            {
                _dashFillImage = dashFillTransform.GetComponent<Image>();
            }
        }

        private void CacheMultiplayerPortraitSprites()
        {
            if (_hostPortraitSprite == null && _playerTorsoImage != null)
            {
                _hostPortraitSprite = _playerTorsoImage.sprite;
            }

            if (_clientPortraitSprite == null)
            {
                Image partnerPortraitSource = ResolvePartnerPortraitSlot();
                if (partnerPortraitSource != null)
                {
                    _clientPortraitSprite = partnerPortraitSource.sprite;
                }
            }

            _hasCachedMultiplayerPortraitSprites = _hostPortraitSprite != null || _clientPortraitSprite != null;
        }

        private void ApplyPortraitLayout(Sprite mainPortrait, Sprite partnerPortrait)
        {
            if (_playerTorsoImage != null)
            {
                _playerTorsoImage.sprite = mainPortrait;
                _playerTorsoImage.enabled = mainPortrait != null;
            }

            Image activePartnerPortraitSlot = ResolvePartnerPortraitSlot();
            if (activePartnerPortraitSlot != null)
            {
                activePartnerPortraitSlot.gameObject.SetActive(true);
                activePartnerPortraitSlot.sprite = partnerPortrait;
                activePartnerPortraitSlot.enabled = partnerPortrait != null;
            }

            if (_partnerPortraitImage != null
                && _partnerLegacyPortraitImage != null
                && _partnerLegacyPortraitImage != _partnerPortraitImage)
            {
                _partnerLegacyPortraitImage.gameObject.SetActive(false);
            }
        }

        private Image ResolvePartnerPortraitSlot()
        {
            if (_partnerPortraitImage != null)
            {
                return _partnerPortraitImage;
            }

            return _partnerLegacyPortraitImage;
        }

        private static Transform FindChildTransformByNameOrPrefix(Transform root, string childName)
        {
            Transform exactMatch = FindChildTransformByName(root, childName);
            if (exactMatch != null)
            {
                return exactMatch;
            }

            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            Transform[] childTransforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < childTransforms.Length; i++)
            {
                Transform childTransform = childTransforms[i];
                if (childTransform == null
                    || !childTransform.name.StartsWith(childName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                return childTransform;
            }

            return null;
        }

        private static Transform FindChildTransformByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            Transform[] childTransforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < childTransforms.Length; i++)
            {
                Transform childTransform = childTransforms[i];
                if (childTransform == null
                    || !string.Equals(childTransform.name, childName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                return childTransform;
            }

            return null;
        }

        private void ResolveComboText()
        {
            if (_comboText != null)
            {
                _comboRoot = _comboText.gameObject;
                return;
            }

            Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < childTransforms.Length; i++)
            {
                Transform childTransform = childTransforms[i];
                if (childTransform == null || !string.Equals(childTransform.name, ComboRootName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                _comboText = childTransform.GetComponent<TMP_Text>();
                _comboRoot = childTransform.gameObject;
                break;
            }
        }

        private void ApplyComboVisibility()
        {
            ResolveComboText();
            if (_comboRoot == null)
            {
                return;
            }

            if (_comboText != null && _isComboVisible)
            {
                _comboText.text = _currentComboStep.ToString();
            }

            _comboRoot.SetActive(_isHudVisible && _isComboVisible);
        }

        private IEnumerator PlayDamageFeedbackRoutine()
        {
            float elapsed = 0f;
            Vector3 startScale = _damageFeedbackBaseScale * _hitScale;
            Vector3 endScale = _damageFeedbackBaseScale;
            Color baseColor = GetDamageFeedbackVisibleColor();

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
                frameColor.a = Mathf.Lerp(_damageFeedbackVisibleAlpha, 0f, t);
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
            _damageFeedbackText.color = GetDamageFeedbackVisibleColor();
        }

        private void HideComboImmediate()
        {
            _isComboVisible = false;
            ResolveComboText();
            if (_comboRoot == null)
            {
                return;
            }

            _comboRoot.SetActive(false);
        }

        private float ResolveDamageFeedbackVisibleAlpha()
        {
            if (_damageFeedbackText == null)
            {
                return 0f;
            }

            return Mathf.Min(
                Mathf.Clamp01(_damageFeedbackText.color.a),
                Mathf.Clamp01(_hitColor.a));
        }

        private Color GetDamageFeedbackVisibleColor()
        {
            Color color = _hitColor;
            color.a = _damageFeedbackVisibleAlpha;
            return color;
        }
    }
}
