using System;
using System.Collections.Generic;
using Core.Combat;
using Core.Common;
using Core.Interfaces;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Core.Boss
{
    public sealed class AttackWarningController : MonoBehaviour
    {
        public const float FullSectorAngle = 360f;
        private const float SectorAngleEpsilon = 0.01f;
        private const float DefaultDamageQueryHeight = 4f;

        private enum WarningShape
        {
            Sector,
            Strip
        }

        public enum DamageMode
        {
            None,
            OnceOnActivePhaseStart,
            ContinuousWhileActive
        }

        [Serializable]
        public struct VisualSettings
        {
            public Renderer warningRenderer;
            public Transform radiusVisualRoot;
            public float visualHeightOffset;
            public float radiusToScaleMultiplier;
            public float fallbackRadiusToScaleMultiplier;
            public string fillPropertyName;
            public string colorPropertyName;
            public string alternateColorPropertyName;
            public Color warningColor;
            public Color activeColor;
            public bool forceRuntimeFallbackVisual;
            public float fallbackYOffset;
            public int fallbackSegments;
            public string fallbackShaderName;
            public bool showGizmos;
            public Color gizmoColor;
        }

        public struct DamageSettings
        {
            public int damage;
            public LayerMask targetMask;
            public int ownerInstanceId;
            public BossAttackHitType bossAttackHitType;
            public int maxTargets;
            public float queryHeight;
        }

        [Header("Visual")]
        [FormerlySerializedAs("telegraphRenderer")]
        [SerializeField] private Renderer warningRenderer;
        [SerializeField] private Transform radiusVisualRoot;
        [SerializeField] private float visualHeightOffset = 0f;
        [SerializeField] private float radiusToScaleMultiplier = 2f;
        [SerializeField] private float fallbackRadiusToScaleMultiplier = 1.2f;
        [SerializeField] private string fillPropertyName = "_Fill01";
        [SerializeField] private string colorPropertyName = "_BaseColor";
        [SerializeField] private string alternateColorPropertyName = "_Color";
        [FormerlySerializedAs("telegraphColor")]
        [SerializeField] private Color warningColor = new Color(1f, 0f, 0f, 0.25f);
        [SerializeField] private Color activeColor = new Color(1f, 0f, 0f, 0.6f);

        [Header("Fallback Visual")]
        [SerializeField] private bool forceRuntimeFallbackVisual;
        [SerializeField] private float fallbackYOffset = 0f;
        [SerializeField] private int fallbackSegments = 48;
        [SerializeField] private string fallbackShaderName = "Universal Render Pipeline/Unlit";

        [Header("Debug")]
        [SerializeField] private bool showGizmos;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.25f, 0.25f, 0.8f);

        private MaterialPropertyBlock _propertyBlock;
        private int _fillPropertyId;
        private int _colorPropertyId;
        private int _alternateColorPropertyId;
        private bool _supportsFillProperty;
        private bool _supportsColorProperty;
        private bool _useScaleFillFallback;
        private float _baseVisualScaleY = 1f;
        private Transform _cachedVisualRoot;
        private Vector3 _cachedVisualRootLocalPosition;

        private Renderer _runtimeFallbackRenderer;
        private Mesh _runtimeFallbackMesh;
        private MeshFilter _runtimeFallbackMeshFilter;
        private Material _runtimeFallbackMaterial;
        private float _runtimeFallbackMeshSectorAngle = -1f;
        private int _runtimeFallbackMeshSegments;
        private WarningShape _runtimeFallbackMeshShape = WarningShape.Sector;
        private readonly List<GameObject> _suppressedSourceVisualObjects = new List<GameObject>(4);

        private bool _isInitialized;
        private bool _isRunning;
        private bool _isActivePhase;
        private float _radius;
        private float _sectorAngle = FullSectorAngle;
        private float _stripLength;
        private float _stripWidth;
        private float _warningDuration;
        private float _activeDuration;
        private float _phaseTimer;
        private WarningShape _shape = WarningShape.Sector;

        private DamageMode _damageMode;
        private int _damage;
        private LayerMask _damageTargetMask = ~0;
        private int _ownerInstanceId;
        private BossAttackHitType _bossAttackHitType = BossAttackHitType.Unknown;
        private float _damageQueryHeight = DefaultDamageQueryHeight;
        private Collider[] _hitResults = Array.Empty<Collider>();
        private readonly HashSet<int> _hitTargetIds = new HashSet<int>(16);
        private static int s_traceSequence;
        private int _traceId;
        private int _damageLoopCallCount;

        public event Action WarningCompleted;
        public event Action PlaybackCompleted;

        public bool IsRunning => _isRunning;
        public bool IsActivePhase => _isActivePhase;
        public float CurrentRadius => _radius;
        public float CurrentStripLength => _stripLength;
        public float CurrentStripWidth => _stripWidth;

        public void SetSharedVisualHeightOffset(float offset)
        {
            visualHeightOffset = Mathf.Max(0f, offset);
            ApplyVisualRootVerticalOffset();
        }

        private void Awake()
        {
            InitializeVisualRuntime();
            HitTraceLogger.Log(
                $"[HitTrace][BOOT][AttackWarningController][Awake] object={name} active={gameObject.activeInHierarchy} path={HitTraceLogger.LogPath}");
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            if (!_isActivePhase)
            {
                _phaseTimer += Time.deltaTime;
                float fill = _warningDuration > 0f ? Mathf.Clamp01(_phaseTimer / _warningDuration) : 1f;
                ApplyVisual(fill, warningColor);

                if (_phaseTimer < _warningDuration)
                {
                    return;
                }

                EnterActivePhase();
                return;
            }

            if (_damageMode == DamageMode.ContinuousWhileActive)
            {
                DealDamageForCurrentShape();
            }

            _phaseTimer += Time.deltaTime;
            if (_phaseTimer >= _activeDuration)
            {
                FinishPlayback("ActiveDurationElapsed");
            }
        }

        public void ApplySettings(in VisualSettings settings)
        {
            warningRenderer = settings.warningRenderer;
            radiusVisualRoot = settings.radiusVisualRoot;
            visualHeightOffset = Mathf.Max(0f, settings.visualHeightOffset);
            radiusToScaleMultiplier = settings.radiusToScaleMultiplier;
            fallbackRadiusToScaleMultiplier = settings.fallbackRadiusToScaleMultiplier;
            fillPropertyName = string.IsNullOrEmpty(settings.fillPropertyName) ? "_Fill01" : settings.fillPropertyName;
            colorPropertyName = string.IsNullOrEmpty(settings.colorPropertyName) ? "_BaseColor" : settings.colorPropertyName;
            alternateColorPropertyName = string.IsNullOrEmpty(settings.alternateColorPropertyName) ? "_Color" : settings.alternateColorPropertyName;
            warningColor = settings.warningColor;
            activeColor = settings.activeColor;
            forceRuntimeFallbackVisual = settings.forceRuntimeFallbackVisual;
            fallbackYOffset = settings.fallbackYOffset;
            fallbackSegments = settings.fallbackSegments;
            fallbackShaderName = string.IsNullOrEmpty(settings.fallbackShaderName)
                ? "Universal Render Pipeline/Unlit"
                : settings.fallbackShaderName;
            showGizmos = settings.showGizmos;
            gizmoColor = settings.gizmoColor;

            InitializeVisualRuntime();
        }

        public void StartWarning(
            Vector3 centerPosition,
            float radius,
            float warningDuration,
            float activeDuration)
        {
            ConfigureDamage(DamageMode.None, default);
            StartWarningInternal(
                WarningShape.Sector,
                centerPosition,
                radius,
                0f,
                0f,
                FullSectorAngle,
                transform.forward,
                warningDuration,
                activeDuration);
        }

        public void StartWarningSector(
            Vector3 centerPosition,
            float radius,
            float warningDuration,
            float activeDuration,
            float sectorAngle,
            Vector3 forwardDirection,
            bool endImmediatelyAfterWarning)
        {
            ConfigureDamage(DamageMode.None, default);
            StartWarningInternal(
                WarningShape.Sector,
                centerPosition,
                radius,
                0f,
                0f,
                sectorAngle,
                forwardDirection,
                warningDuration,
                endImmediatelyAfterWarning ? 0f : activeDuration);
        }

        public void StartDamageSector(
            Vector3 centerPosition,
            float radius,
            float warningDuration,
            float activeDuration,
            float sectorAngle,
            Vector3 forwardDirection,
            in DamageSettings damageSettings,
            DamageMode damageMode)
        {
            ConfigureDamage(damageMode, damageSettings);
            StartWarningInternal(
                WarningShape.Sector,
                centerPosition,
                radius,
                0f,
                0f,
                sectorAngle,
                forwardDirection,
                warningDuration,
                activeDuration);
        }

        public void StartDamageStrip(
            Vector3 startPosition,
            float length,
            float width,
            float warningDuration,
            float activeDuration,
            Vector3 forwardDirection,
            in DamageSettings damageSettings,
            DamageMode damageMode)
        {
            ConfigureDamage(damageMode, damageSettings);
            StartWarningInternal(
                WarningShape.Strip,
                startPosition,
                0f,
                length,
                width,
                FullSectorAngle,
                forwardDirection,
                warningDuration,
                activeDuration);
        }

        public void ForceEnd(string reason = "Unspecified")
        {
            if (!_isRunning && !gameObject.activeSelf)
            {
                HitTraceLogger.Log(
                    $"[HitTrace][BOOT][AttackWarningController][ForceEndSkip] traceId={_traceId} reason={reason} running={_isRunning} activeSelf={gameObject.activeSelf}");
                return;
            }

            HitTraceLogger.Log(
                $"[HitTrace][BOOT][AttackWarningController][ForceEnd] traceId={_traceId} reason={reason} " +
                $"shape={_shape} mode={_damageMode} isActivePhase={_isActivePhase} phaseTimer={_phaseTimer:F3} warning={_warningDuration:F3} active={_activeDuration:F3}");
            FinishPlayback($"ForceEnd:{reason}");
        }

        public bool TryEnterActivePhaseNow(string reason = "Unspecified")
        {
            if (!_isRunning)
            {
                HitTraceLogger.Log(
                    $"[HitTrace][BOOT][AttackWarningController][TryEnterActivePhaseNow][SKIP] traceId={_traceId} reason={reason} running={_isRunning}");
                return false;
            }

            if (_isActivePhase)
            {
                HitTraceLogger.Log(
                    $"[HitTrace][BOOT][AttackWarningController][TryEnterActivePhaseNow][SKIP] traceId={_traceId} reason={reason} alreadyActive=true");
                return false;
            }

            HitTraceLogger.Log(
                $"[HitTrace][BOOT][AttackWarningController][TryEnterActivePhaseNow][CALL] traceId={_traceId} reason={reason} " +
                $"phaseTimer={_phaseTimer:F3} warning={_warningDuration:F3} active={_activeDuration:F3}");
            EnterActivePhase();
            return true;
        }

        private void StartWarningInternal(
            WarningShape shape,
            Vector3 position,
            float radius,
            float stripLength,
            float stripWidth,
            float sectorAngle,
            Vector3 forwardDirection,
            float warningDuration,
            float activeDuration)
        {
            _shape = shape;
            _radius = Mathf.Max(0.1f, radius);
            _stripLength = Mathf.Max(0.1f, stripLength);
            _stripWidth = Mathf.Max(0.1f, stripWidth);
            _sectorAngle = Mathf.Clamp(sectorAngle, 0.1f, FullSectorAngle);
            _warningDuration = Mathf.Max(0f, warningDuration);
            _activeDuration = Mathf.Max(0f, activeDuration);
            _phaseTimer = 0f;
            _isActivePhase = false;
            _isRunning = true;
            _hitTargetIds.Clear();
            _traceId = ++s_traceSequence;
            _damageLoopCallCount = 0;

            transform.position = position;
            ApplySectorOrientation(forwardDirection);

            HitTraceLogger.Log(
                $"[HitTrace][BOOT][AttackWarningController][StartWarning] traceId={_traceId} shape={shape} mode={_damageMode} " +
                $"attack={ResolveAttackTypeLabel(_bossAttackHitType)} damage={_damage} warning={_warningDuration:F3} active={_activeDuration:F3}");

            PrepareVisualForCurrentShape();
            // 첫 실행 시 visual root 초기화가 transform local 값을 건드릴 수 있으므로
            // 시각 준비 직후 앵커 pose를 다시 고정해 XZ 기준점이 흔들리지 않게 한다.
            transform.position = position;
            ApplySectorOrientation(forwardDirection);
            EnsureFallbackMeshMatchesShape();
            ApplyShapeScale(1f);
            ApplyVisual(0f, warningColor);

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (_warningDuration <= 0f)
            {
                EnterActivePhase();
            }
        }

        private void ConfigureDamage(DamageMode damageMode, in DamageSettings settings)
        {
            _damageMode = damageMode;
            _damage = Mathf.Max(0, settings.damage);
            _damageTargetMask = settings.targetMask == 0 ? ~0 : settings.targetMask;
            _ownerInstanceId = settings.ownerInstanceId;
            _bossAttackHitType = settings.bossAttackHitType;
            _damageQueryHeight = settings.queryHeight > 0f ? settings.queryHeight : DefaultDamageQueryHeight;

            int maxTargets = Mathf.Max(1, settings.maxTargets);
            if (_hitResults == null || _hitResults.Length != maxTargets)
            {
                _hitResults = new Collider[maxTargets];
            }
        }

        private void EnterActivePhase()
        {
            _isActivePhase = true;
            _phaseTimer = 0f;
            ApplyVisual(1f, activeColor);
            HitTraceLogger.Log(
                $"[HitTrace][BOOT][AttackWarningController][EnterActivePhase] traceId={_traceId} shape={_shape} mode={_damageMode} " +
                $"attack={ResolveAttackTypeLabel(_bossAttackHitType)} damage={_damage}");
            WarningCompleted?.Invoke();

            if (_damageMode == DamageMode.OnceOnActivePhaseStart)
            {
                DealDamageForCurrentShape();
            }

            if (_activeDuration <= 0f)
            {
                FinishPlayback("ActiveDurationNonPositive");
            }
        }

        private void FinishPlayback(string reason = "Unspecified")
        {
            bool shouldNotify = _isRunning;
            HitTraceLogger.Log(
                $"[HitTrace][BOOT][AttackWarningController][FinishPlayback] traceId={_traceId} reason={reason} " +
                $"shape={_shape} mode={_damageMode} wasRunning={_isRunning} wasActive={_isActivePhase} " +
                $"phaseTimer={_phaseTimer:F3} warning={_warningDuration:F3} active={_activeDuration:F3}");

            _isRunning = false;
            _isActivePhase = false;
            _damageMode = DamageMode.None;
            _hitTargetIds.Clear();
            ApplyVisual(0f, warningColor);
            RestoreSuppressedSourceVisuals();

            if (shouldNotify)
            {
                PlaybackCompleted?.Invoke();
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void InitializeVisualRuntime()
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            _fillPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(fillPropertyName) ? "_Fill01" : fillPropertyName);
            _colorPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(colorPropertyName) ? "_BaseColor" : colorPropertyName);
            _alternateColorPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(alternateColorPropertyName) ? "_Color" : alternateColorPropertyName);

            if (warningRenderer == null)
            {
                warningRenderer = GetComponentInChildren<Renderer>(true);
            }

            if (radiusVisualRoot == null)
            {
                radiusVisualRoot = warningRenderer != null ? warningRenderer.transform : transform;
            }

            _baseVisualScaleY = Mathf.Max(0.001f, radiusVisualRoot.localScale.y);
            _isInitialized = true;
            CacheVisualRootLocalPosition();
            ApplyVisualRootVerticalOffset();
        }

        private void PrepareVisualForCurrentShape()
        {
            InitializeVisualRuntime();
            RestoreSuppressedSourceVisuals();

            if (_shape == WarningShape.Strip)
            {
                CreateRuntimeFallbackVisual();
                SuppressIncompatibleSourceVisualsForFallback();
                warningRenderer = _runtimeFallbackRenderer;
                radiusVisualRoot = _runtimeFallbackRenderer != null ? _runtimeFallbackRenderer.transform : transform;
                _baseVisualScaleY = Mathf.Max(0.001f, radiusVisualRoot.localScale.y);
                TryConfigureRendererCapabilities();
                _useScaleFillFallback = true;
                return;
            }

            if (forceRuntimeFallbackVisual || !TryConfigureRendererCapabilities())
            {
                CreateRuntimeFallbackVisual();
                SuppressIncompatibleSourceVisualsForFallback();
                TryConfigureRendererCapabilities();
            }

            if (radiusVisualRoot == null)
            {
                radiusVisualRoot = warningRenderer != null ? warningRenderer.transform : transform;
            }

            _baseVisualScaleY = Mathf.Max(0.001f, radiusVisualRoot.localScale.y);
            CacheVisualRootLocalPosition();
            ApplyVisualRootVerticalOffset();
        }

        private void ApplyShapeScale(float fill01)
        {
            if (!_isInitialized || radiusVisualRoot == null)
            {
                return;
            }

            float clampedFill = Mathf.Clamp01(fill01);
            Vector3 scale = radiusVisualRoot.localScale;

            switch (_shape)
            {
                case WarningShape.Strip:
                    scale.x = Mathf.Max(0.001f, _stripWidth);
                    scale.z = Mathf.Max(0.001f, _stripLength * (_useScaleFillFallback ? clampedFill : 1f));
                    break;

                default:
                    float visualScale = _radius * ResolveRadiusScaleMultiplier();
                    if (_useScaleFillFallback)
                    {
                        visualScale *= clampedFill;
                    }

                    scale.x = Mathf.Max(0.001f, visualScale);
                    scale.z = Mathf.Max(0.001f, visualScale);
                    break;
            }

            scale.y = _baseVisualScaleY;
            radiusVisualRoot.localScale = scale;
        }

        private void ApplyVisual(float fill01, Color color)
        {
            if (!_isInitialized || warningRenderer == null)
            {
                return;
            }

            float clampedFill = Mathf.Clamp01(fill01);

            warningRenderer.GetPropertyBlock(_propertyBlock);
            if (_supportsFillProperty)
            {
                _propertyBlock.SetFloat(_fillPropertyId, clampedFill);
            }
            if (_supportsColorProperty)
            {
                _propertyBlock.SetColor(_colorPropertyId, color);
                _propertyBlock.SetColor(_alternateColorPropertyId, color);
            }
            warningRenderer.SetPropertyBlock(_propertyBlock);

            if (_runtimeFallbackMaterial != null)
            {
                ApplyFallbackMaterialColor(_runtimeFallbackMaterial, color);
            }

            ApplyShapeScale(clampedFill);
        }

        private void CacheVisualRootLocalPosition(bool force = false)
        {
            if (radiusVisualRoot == null)
            {
                _cachedVisualRoot = null;
                _cachedVisualRootLocalPosition = Vector3.zero;
                return;
            }

            if (!force && _cachedVisualRoot == radiusVisualRoot)
            {
                return;
            }

            _cachedVisualRoot = radiusVisualRoot;
            _cachedVisualRootLocalPosition = radiusVisualRoot.localPosition;
        }

        private void ApplyVisualRootVerticalOffset()
        {
            if (!_isInitialized || radiusVisualRoot == null)
            {
                return;
            }

            CacheVisualRootLocalPosition();
            Vector3 localPosition = _cachedVisualRootLocalPosition;
            localPosition.y += visualHeightOffset;
            radiusVisualRoot.localPosition = localPosition;
        }

        private float ResolveRadiusScaleMultiplier()
        {
            if (_runtimeFallbackRenderer != null && radiusVisualRoot == _runtimeFallbackRenderer.transform)
            {
                return Mathf.Max(0.001f, fallbackRadiusToScaleMultiplier);
            }

            return Mathf.Max(0.001f, radiusToScaleMultiplier);
        }

        private bool TryConfigureRendererCapabilities()
        {
            _supportsFillProperty = false;
            _supportsColorProperty = false;
            _useScaleFillFallback = false;

            if (warningRenderer == null)
            {
                return false;
            }

            if (warningRenderer.GetType().Name.Contains("VFX"))
            {
                return false;
            }

            if (warningRenderer is ParticleSystemRenderer)
            {
                return false;
            }

            Material sharedMaterial = warningRenderer.sharedMaterial;
            if (sharedMaterial == null)
            {
                return false;
            }

            _supportsFillProperty = sharedMaterial.HasProperty(_fillPropertyId);
            _supportsColorProperty = sharedMaterial.HasProperty(_colorPropertyId) || sharedMaterial.HasProperty(_alternateColorPropertyId);
            _useScaleFillFallback = !_supportsFillProperty;
            return true;
        }

        private void CreateRuntimeFallbackVisual()
        {
            if (_runtimeFallbackRenderer != null)
            {
                if (_runtimeFallbackRenderer.transform != null)
                {
                    _runtimeFallbackRenderer.transform.localPosition = new Vector3(0f, fallbackYOffset, 0f);
                }

                warningRenderer = _runtimeFallbackRenderer;
                radiusVisualRoot = _runtimeFallbackRenderer.transform;
                CacheVisualRootLocalPosition(true);
                ApplyVisualRootVerticalOffset();
                EnsureFallbackMeshMatchesShape();
                return;
            }

            GameObject visual = new GameObject("AttackWarning_RuntimeFallbackMesh");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0f, fallbackYOffset, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            _runtimeFallbackMeshFilter = visual.AddComponent<MeshFilter>();

            MeshRenderer meshRenderer = visual.AddComponent<MeshRenderer>();
            _runtimeFallbackMaterial = CreateFallbackMaterial();
            meshRenderer.sharedMaterial = _runtimeFallbackMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            _runtimeFallbackRenderer = meshRenderer;
            warningRenderer = meshRenderer;
            radiusVisualRoot = visual.transform;
            CacheVisualRootLocalPosition(true);
            ApplyVisualRootVerticalOffset();
            EnsureFallbackMeshMatchesShape();
        }

        private void SuppressIncompatibleSourceVisualsForFallback()
        {
            Transform fallbackRoot = _runtimeFallbackRenderer != null ? _runtimeFallbackRenderer.transform : null;
            ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                if (fallbackRoot != null && particleSystem.transform.IsChildOf(fallbackRoot))
                {
                    continue;
                }

                TrySuppressSourceVisualObject(particleSystem.gameObject);
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer == _runtimeFallbackRenderer)
                {
                    continue;
                }

                if (fallbackRoot != null && renderer.transform.IsChildOf(fallbackRoot))
                {
                    continue;
                }

                if (!renderer.GetType().Name.Contains("VFX"))
                {
                    continue;
                }

                TrySuppressSourceVisualObject(renderer.gameObject);
            }
        }

        private void TrySuppressSourceVisualObject(GameObject targetObject)
        {
            if (targetObject == null
                || targetObject == gameObject
                || !targetObject.activeSelf
                || _suppressedSourceVisualObjects.Contains(targetObject))
            {
                return;
            }

            targetObject.SetActive(false);
            _suppressedSourceVisualObjects.Add(targetObject);
        }

        private void RestoreSuppressedSourceVisuals()
        {
            for (int i = 0; i < _suppressedSourceVisualObjects.Count; i++)
            {
                GameObject suppressedObject = _suppressedSourceVisualObjects[i];
                if (suppressedObject == null)
                {
                    continue;
                }

                suppressedObject.SetActive(true);
            }

            _suppressedSourceVisualObjects.Clear();
        }

        private Material CreateFallbackMaterial()
        {
            Shader shader = Shader.Find(fallbackShaderName);
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = new Material(shader);
            material.renderQueue = (int)RenderQueue.Transparent;
            ApplyFallbackMaterialColor(material, warningColor);

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            return material;
        }

        private void ApplyFallbackMaterialColor(Material material, Color color)
        {
            if (material == null) return;

            if (material.HasProperty(_colorPropertyId))
            {
                material.SetColor(_colorPropertyId, color);
            }
            if (material.HasProperty(_alternateColorPropertyId))
            {
                material.SetColor(_alternateColorPropertyId, color);
            }
        }

        private void EnsureFallbackMeshMatchesShape()
        {
            if (_runtimeFallbackRenderer == null)
            {
                return;
            }

            if (_runtimeFallbackMeshFilter == null)
            {
                _runtimeFallbackMeshFilter = _runtimeFallbackRenderer.GetComponent<MeshFilter>();
            }

            if (_runtimeFallbackMeshFilter == null)
            {
                return;
            }

            int segmentCount = Mathf.Clamp(fallbackSegments, 8, 128);
            bool requiresRebuild = _runtimeFallbackMesh == null || _runtimeFallbackMeshShape != _shape;

            if (_shape == WarningShape.Sector)
            {
                requiresRebuild |= Mathf.Abs(_runtimeFallbackMeshSectorAngle - _sectorAngle) > SectorAngleEpsilon;
                requiresRebuild |= _runtimeFallbackMeshSegments != segmentCount;
            }

            if (!requiresRebuild)
            {
                return;
            }

            if (_runtimeFallbackMesh != null)
            {
                Destroy(_runtimeFallbackMesh);
            }

            _runtimeFallbackMesh = _shape == WarningShape.Strip
                ? BuildStripMesh()
                : BuildDiscMesh(segmentCount, _sectorAngle);
            _runtimeFallbackMeshFilter.sharedMesh = _runtimeFallbackMesh;
            _runtimeFallbackMeshShape = _shape;
            _runtimeFallbackMeshSectorAngle = _sectorAngle;
            _runtimeFallbackMeshSegments = segmentCount;
        }

        private void ApplySectorOrientation(Vector3 forwardDirection)
        {
            Vector3 planarForward = forwardDirection;
            planarForward.y = 0f;
            if (planarForward.sqrMagnitude <= 0.0001f)
            {
                planarForward = Vector3.forward;
            }

            transform.rotation = Quaternion.LookRotation(planarForward.normalized, Vector3.up);
        }

        private void DealDamageForCurrentShape()
        {
            if (_damageMode == DamageMode.None || _damage <= 0)
            {
                HitTraceLogger.Log(
                    $"[HitTrace][BOOT][AttackWarningController][DealDamageSkip] traceId={_traceId} mode={_damageMode} damage={_damage}");
                return;
            }

            _damageLoopCallCount++;
            HitTraceLogger.Log(
                $"[HitTrace][BOOT][AttackWarningController][DealDamageTick] traceId={_traceId} call={_damageLoopCallCount} " +
                $"shape={_shape} mode={_damageMode} attack={ResolveAttackTypeLabel(_bossAttackHitType)}");

            switch (_shape)
            {
                case WarningShape.Strip:
                    DealDamageInStrip();
                    break;

                default:
                    DealDamageInSector();
                    break;
            }
        }

        private void DealDamageInSector()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, Mathf.Max(0.1f, _radius), _hitResults, _damageTargetMask);
            HitTraceLogger.Log(
                $"[HitTrace][BOOT][AttackWarningController][SectorQuery] traceId={_traceId} hitCount={hitCount} " +
                $"radius={_radius:F2} mask={_damageTargetMask.value}");
            float halfAngle = Mathf.Clamp(_sectorAngle * 0.5f, 0f, 180f);
            float minDot = Mathf.Cos(halfAngle * Mathf.Deg2Rad);

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _hitResults[i];
                if (col == null || !PassesSectorFilter(col, minDot))
                {
                    continue;
                }

                Vector3 forceDirection = col.bounds.center - transform.position;
                forceDirection.y = 0f;
                if (forceDirection.sqrMagnitude <= 0.0001f)
                {
                    forceDirection = transform.forward;
                }

                ApplyDamageToCollider(col, forceDirection.normalized);
            }
        }

        private bool PassesSectorFilter(Collider col, float minDot)
        {
            if (_sectorAngle >= FullSectorAngle - SectorAngleEpsilon)
            {
                return true;
            }

            Vector3 toTarget = col.bounds.center - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            return Vector3.Dot(forward.normalized, toTarget.normalized) >= minDot;
        }

        private void DealDamageInStrip()
        {
            float halfWidth = Mathf.Max(0.05f, _stripWidth * 0.5f);
            float halfLength = Mathf.Max(0.05f, _stripLength * 0.5f);
            float halfHeight = Mathf.Max(0.25f, _damageQueryHeight * 0.5f);
            Vector3 center = transform.position
                + (transform.forward * halfLength)
                + (Vector3.up * halfHeight);

            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                new Vector3(halfWidth, halfHeight, halfLength),
                _hitResults,
                transform.rotation,
                _damageTargetMask,
                QueryTriggerInteraction.Ignore);
            HitTraceLogger.Log(
                $"[HitTrace][BOOT][AttackWarningController][StripQuery] traceId={_traceId} hitCount={hitCount} " +
                $"length={_stripLength:F2} width={_stripWidth:F2} mask={_damageTargetMask.value}");

            Vector3 forceDirection = transform.forward;
            forceDirection.y = 0f;
            if (forceDirection.sqrMagnitude <= 0.0001f)
            {
                forceDirection = Vector3.forward;
            }

            forceDirection.Normalize();

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _hitResults[i];
                if (col == null)
                {
                    continue;
                }

                ApplyDamageToCollider(col, forceDirection);
            }
        }

        private void ApplyDamageToCollider(Collider col, Vector3 forceDirection)
        {
            string attackLabel = ResolveAttackTypeLabel(_bossAttackHitType);
            string targetLabel = col != null ? col.transform.root.name : "null";

            IDamageable damageable = col.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = col.GetComponentInParent<IDamageable>();
            }

            if (damageable == null)
            {
                HitTraceLogger.Log($"[HitTrace][S05][{attackLabel}][FAIL] traceId={_traceId} target={targetLabel} reason=IDamageableNotFound");
                return;
            }

            HitTraceLogger.Log($"[HitTrace][S05][{attackLabel}][PASS] traceId={_traceId} target={targetLabel} damageable={damageable.GetType().Name}");

            int targetId = ExtractTargetInstanceId(damageable, col);
            if (targetId == 0)
            {
                HitTraceLogger.Log($"[HitTrace][S06][{attackLabel}][FAIL] traceId={_traceId} target={targetLabel} reason=TargetIdZero");
                return;
            }

            if (_ownerInstanceId != 0 && targetId == _ownerInstanceId)
            {
                HitTraceLogger.Log($"[HitTrace][S06][{attackLabel}][FAIL] traceId={_traceId} target={targetLabel} reason=OwnerSelfFiltered");
                return;
            }

            if (_hitTargetIds.Contains(targetId))
            {
                HitTraceLogger.Log($"[HitTrace][S06][{attackLabel}][FAIL] traceId={_traceId} target={targetLabel} reason=AlreadyHitThisPlayback targetId={targetId}");
                return;
            }

            HitTraceLogger.Log($"[HitTrace][S06][{attackLabel}][PASS] traceId={_traceId} target={targetLabel} targetId={targetId}");

            if (_bossAttackHitType != BossAttackHitType.Unknown)
            {
                IBossAttackHitReceiver bossHitReceiver = col.GetComponent<IBossAttackHitReceiver>();
                if (bossHitReceiver == null)
                {
                    bossHitReceiver = col.GetComponentInParent<IBossAttackHitReceiver>();
                }

                if (bossHitReceiver != null)
                {
                    HitTraceLogger.Log(
                        $"[HitTrace][S07][{attackLabel}][CALL] traceId={_traceId} target={targetLabel} receiver={bossHitReceiver.GetType().Name} " +
                        $"damage={_damage}");
                    BossAttackHitResolution resolution = bossHitReceiver.ReceiveBossAttackHit(
                        new BossAttackHitData(_damage, _bossAttackHitType, forceDirection));
                    if (resolution != BossAttackHitResolution.Ignored)
                    {
                        _hitTargetIds.Add(targetId);
                        HitTraceLogger.Log($"[HitTrace][S07][{attackLabel}][PASS] traceId={_traceId} target={targetLabel} resolution={resolution}");
                    }
                    else
                    {
                        HitTraceLogger.Log($"[HitTrace][S07][{attackLabel}][FAIL] traceId={_traceId} target={targetLabel} resolution=Ignored");
                        HitTraceLogger.Log($"[HitTrace][S12][{attackLabel}][FAIL] traceId={_traceId} target={targetLabel} reason=ReceiverIgnored_NoFallback");
                    }
                    return;
                }

                HitTraceLogger.Log($"[HitTrace][S07][{attackLabel}][FAIL] traceId={_traceId} target={targetLabel} reason=IBossAttackHitReceiverNotFound");
            }

            damageable.TakeDamage(_damage);
            _hitTargetIds.Add(targetId);
            HitTraceLogger.Log($"[HitTrace][S07][{attackLabel}][PASS] traceId={_traceId} target={targetLabel} path=DirectDamageableFallback damage={_damage}");
        }

        private static int ExtractTargetInstanceId(IDamageable damageable, Collider hitCollider)
        {
            if (damageable is BossHitBox bossHitBox && bossHitBox.Owner != null)
            {
                return bossHitBox.Owner.gameObject.GetInstanceID();
            }

            if (damageable is MonoBehaviour mono)
            {
                return mono.gameObject.GetInstanceID();
            }

            if (hitCollider != null && hitCollider.transform.root != null)
            {
                return hitCollider.transform.root.gameObject.GetInstanceID();
            }

            return 0;
        }

        private static string ResolveAttackTypeLabel(BossAttackHitType hitType)
        {
            return hitType switch
            {
                BossAttackHitType.Attack1 => "Basic",
                BossAttackHitType.Attack2 => "Lunge",
                BossAttackHitType.Attack3Projectile => "Projectile",
                BossAttackHitType.Attack4Projectile => "AoE",
                _ => "Unknown"
            };
        }

        private static Mesh BuildDiscMesh(int segments, float sectorAngle)
        {
            float clampedSectorAngle = Mathf.Clamp(sectorAngle, 0.1f, FullSectorAngle);
            Mesh mesh = new Mesh
            {
                name = "AttackWarning_RuntimeDisc"
            };

            int vertexCount = segments + 2;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i <= segments; i++)
            {
                float normalizedStep = i / (float)segments;
                float angleDegrees = clampedSectorAngle >= FullSectorAngle - SectorAngleEpsilon
                    ? normalizedStep * FullSectorAngle
                    : (-clampedSectorAngle * 0.5f) + (normalizedStep * clampedSectorAngle);
                float angle = angleDegrees * Mathf.Deg2Rad;

                // 부채꼴의 전방을 로컬 +Z에 맞춘다.
                float x = Mathf.Sin(angle);
                float z = Mathf.Cos(angle);

                int index = i + 1;
                vertices[index] = new Vector3(x, 0f, z);
                uvs[index] = new Vector2((x * 0.5f) + 0.5f, (z * 0.5f) + 0.5f);

                if (i < segments)
                {
                    int tri = i * 3;
                    triangles[tri] = 0;
                    triangles[tri + 1] = index + 1;
                    triangles[tri + 2] = index;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildStripMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "AttackWarning_RuntimeStrip"
            };

            Vector3[] vertices =
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.5f, 0f, 1f),
                new Vector3(0.5f, 0f, 1f)
            };

            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };

            int[] triangles =
            {
                0, 2, 1,
                2, 3, 1
            };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos)
            {
                return;
            }

            Gizmos.color = gizmoColor;

            switch (_shape)
            {
                case WarningShape.Strip:
                    Matrix4x4 previousMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(
                        transform.position + (transform.forward * (_stripLength * 0.5f)),
                        transform.rotation,
                        Vector3.one);
                    Gizmos.DrawWireCube(
                        Vector3.up * (_damageQueryHeight * 0.5f),
                        new Vector3(_stripWidth, _damageQueryHeight, _stripLength));
                    Gizmos.matrix = previousMatrix;
                    break;

                default:
                    Gizmos.DrawWireSphere(transform.position, _radius > 0f ? _radius : 0.1f);
                    break;
            }
        }

        private void OnDestroy()
        {
            if (_runtimeFallbackMesh != null)
            {
                Destroy(_runtimeFallbackMesh);
            }

            if (_runtimeFallbackMaterial != null)
            {
                Destroy(_runtimeFallbackMaterial);
            }
        }
    }
}
