using System.Collections.Generic;
using UnityEngine;

namespace Core.Common
{
    /// <summary>
    /// _BlinkWhite 셰이더 파라미터를 이용한 흰색 점멸 시각 효과 컴포넌트.
    /// 게임플레이 로직(무적/피해 판정)과 분리된 순수 비주얼 전용이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlinkWhiteEffect : MonoBehaviour
    {
        private const string DefaultBlinkShaderName = "Custom/Common/BlinkWhiteLit";
        private const string LegacyBlinkShaderName = "Custom/Player/BlinkWhiteLit";
        private const float MinBlinkInterval = 0.01f;
        private static readonly int BlinkWhiteId = Shader.PropertyToID("_BlinkWhite");

        private enum BlinkMode
        {
            None,
            Manual,
            TimedToggle,
            Single
        }

        [Header("Blink Shader")]
        [SerializeField] private Shader blinkShader;

        [Header("Blink Preset")]
        [SerializeField] private float defaultBlinkFrequency = 0.2f;
        [SerializeField] private float singleBlinkDuration = 0.08f;

        [Header("Renderer Scope")]
        [SerializeField] private bool includeInactiveChildren = true;
        [SerializeField] private bool excludeVfxRenderers = true;
        [SerializeField] private Renderer[] targetRenderers;

        private Renderer[] _resolvedRenderers;
        private bool[] _supportsBlinkProperty;
        private Material[][] _originalSharedMaterialsByRenderer;
        private Material[][] _blinkSharedMaterialsByRenderer;
        private Dictionary<Material, Material> _runtimeBlinkMaterialMap;
        private List<Material> _runtimeBlinkMaterials;
        private MaterialPropertyBlock _propertyBlock;

        private BlinkMode _mode;
        private float _remainingDuration;
        private float _toggleInterval;
        private float _toggleTimer;
        private bool _isWhite;
        private bool _isBlinkMaterialActive;

        private void OnValidate()
        {
            if (defaultBlinkFrequency < MinBlinkInterval) defaultBlinkFrequency = MinBlinkInterval;
            if (singleBlinkDuration < MinBlinkInterval) singleBlinkDuration = MinBlinkInterval;
        }

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            InitializeRendererData();
            StopBlink();
        }

        private void OnDisable()
        {
            StopBlink();
        }

        private void OnDestroy()
        {
            if (_runtimeBlinkMaterials == null) return;

            for (int i = 0; i < _runtimeBlinkMaterials.Count; i++)
            {
                Material runtimeMaterial = _runtimeBlinkMaterials[i];
                if (runtimeMaterial == null) continue;

                if (Application.isPlaying)
                {
                    Destroy(runtimeMaterial);
                }
                else
                {
                    DestroyImmediate(runtimeMaterial);
                }
            }

            _runtimeBlinkMaterials.Clear();
            _runtimeBlinkMaterialMap?.Clear();
        }

        private void Update()
        {
            switch (_mode)
            {
                case BlinkMode.TimedToggle:
                    UpdateTimedToggleBlink();
                    break;
                case BlinkMode.Single:
                    UpdateSingleBlink();
                    break;
            }
        }

        /// <summary>
        /// 지정 시간 동안 점멸을 재생한다. 점멸 주기는 프리셋 값을 사용한다.
        /// </summary>
        public void PlayBlink(float duration)
        {
            PlayBlink(duration, defaultBlinkFrequency);
        }

        /// <summary>
        /// 지정 시간 동안 점멸을 재생한다.
        /// </summary>
        public void PlayBlink(float duration, float frequency)
        {
            if (duration <= 0f)
            {
                StopBlink();
                return;
            }

            ActivateBlinkMaterialSet();

            _mode = BlinkMode.TimedToggle;
            _remainingDuration = duration;
            _toggleInterval = Mathf.Max(MinBlinkInterval, frequency);
            _toggleTimer = 0f;
            SetBlinkWhite(true);
        }

        /// <summary>
        /// 단일 히트 피드백용 1회 점멸을 재생한다.
        /// </summary>
        public void PlaySingleBlink()
        {
            ActivateBlinkMaterialSet();

            _mode = BlinkMode.Single;
            _remainingDuration = Mathf.Max(MinBlinkInterval, singleBlinkDuration);
            _toggleTimer = 0f;
            SetBlinkWhite(true);
        }

        /// <summary>
        /// 즉시 점멸 상태를 강제로 설정한다.
        /// </summary>
        public void SetBlink(bool enabled)
        {
            if (!enabled)
            {
                StopBlink();
                return;
            }

            ActivateBlinkMaterialSet();
            _mode = BlinkMode.Manual;
            _remainingDuration = 0f;
            _toggleTimer = 0f;
            SetBlinkWhite(true);
        }

        /// <summary>
        /// 점멸을 중지하고 원래 머티리얼로 복구한다.
        /// </summary>
        public void StopBlink()
        {
            _mode = BlinkMode.None;
            _remainingDuration = 0f;
            _toggleTimer = 0f;
            SetBlinkWhite(false);
            DeactivateBlinkMaterialSet();
        }

        private void UpdateTimedToggleBlink()
        {
            _remainingDuration -= Time.deltaTime;

            _toggleTimer += Time.deltaTime;
            while (_toggleTimer >= _toggleInterval)
            {
                _toggleTimer -= _toggleInterval;
                SetBlinkWhite(!_isWhite);
            }

            if (_remainingDuration <= 0f)
            {
                StopBlink();
            }
        }

        private void UpdateSingleBlink()
        {
            _remainingDuration -= Time.deltaTime;
            if (_remainingDuration <= 0f)
            {
                StopBlink();
            }
        }

        private void InitializeRendererData()
        {
            ResolveBlinkShader();
            ResolveTargetRenderers();
            PrepareBlinkMaterialSets();
            CacheBlinkPropertySupport();
        }

        private void ResolveBlinkShader()
        {
            if (blinkShader != null) return;
            blinkShader = Shader.Find(DefaultBlinkShaderName);
            if (blinkShader == null)
            {
                blinkShader = Shader.Find(LegacyBlinkShaderName);
            }
        }

        private void ResolveTargetRenderers()
        {
            Renderer[] sourceRenderers = targetRenderers;
            if (sourceRenderers == null || sourceRenderers.Length == 0)
            {
                sourceRenderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);
            }

            if (sourceRenderers == null || sourceRenderers.Length == 0)
            {
                _resolvedRenderers = System.Array.Empty<Renderer>();
                return;
            }

            List<Renderer> filteredRenderers = new List<Renderer>(sourceRenderers.Length);
            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                Renderer renderer = sourceRenderers[i];
                if (renderer == null) continue;
                if (excludeVfxRenderers && IsVfxRenderer(renderer)) continue;
                filteredRenderers.Add(renderer);
            }

            _resolvedRenderers = filteredRenderers.ToArray();
        }

        private static bool IsVfxRenderer(Renderer renderer)
        {
            return renderer is ParticleSystemRenderer ||
                   renderer is TrailRenderer ||
                   renderer is LineRenderer;
        }

        private void PrepareBlinkMaterialSets()
        {
            int rendererCount = _resolvedRenderers != null ? _resolvedRenderers.Length : 0;
            _originalSharedMaterialsByRenderer = new Material[rendererCount][];
            _blinkSharedMaterialsByRenderer = new Material[rendererCount][];

            _runtimeBlinkMaterialMap ??= new Dictionary<Material, Material>();
            _runtimeBlinkMaterials ??= new List<Material>();

            for (int i = 0; i < rendererCount; i++)
            {
                Renderer renderer = _resolvedRenderers[i];
                if (renderer == null) continue;

                Material[] sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials == null || sharedMaterials.Length == 0) continue;

                _originalSharedMaterialsByRenderer[i] = sharedMaterials;

                Material[] blinkMaterials = new Material[sharedMaterials.Length];
                bool hasReplacement = false;
                for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    Material sourceMaterial = sharedMaterials[materialIndex];
                    if (sourceMaterial == null)
                    {
                        blinkMaterials[materialIndex] = null;
                        continue;
                    }

                    if (sourceMaterial.HasProperty(BlinkWhiteId))
                    {
                        blinkMaterials[materialIndex] = sourceMaterial;
                        continue;
                    }

                    Material blinkMaterial = GetOrCreateBlinkMaterial(sourceMaterial);
                    if (blinkMaterial == null)
                    {
                        blinkMaterials[materialIndex] = sourceMaterial;
                        continue;
                    }

                    blinkMaterials[materialIndex] = blinkMaterial;
                    hasReplacement = true;
                }

                _blinkSharedMaterialsByRenderer[i] = hasReplacement
                    ? blinkMaterials
                    : sharedMaterials;
            }
        }

        private Material GetOrCreateBlinkMaterial(Material sourceMaterial)
        {
            if (sourceMaterial == null || blinkShader == null) return null;

            if (_runtimeBlinkMaterialMap.TryGetValue(sourceMaterial, out Material cachedMaterial) &&
                cachedMaterial != null)
            {
                return cachedMaterial;
            }

            Material blinkMaterial = new Material(sourceMaterial)
            {
                name = $"{sourceMaterial.name}_BlinkRuntime",
                shader = blinkShader
            };

            if (blinkMaterial.HasProperty(BlinkWhiteId))
            {
                blinkMaterial.SetFloat(BlinkWhiteId, 0f);
            }

            _runtimeBlinkMaterialMap[sourceMaterial] = blinkMaterial;
            _runtimeBlinkMaterials.Add(blinkMaterial);
            return blinkMaterial;
        }

        private void CacheBlinkPropertySupport()
        {
            int rendererCount = _resolvedRenderers != null ? _resolvedRenderers.Length : 0;
            _supportsBlinkProperty = new bool[rendererCount];

            for (int i = 0; i < rendererCount; i++)
            {
                Material[] materials = _blinkSharedMaterialsByRenderer != null &&
                                       i < _blinkSharedMaterialsByRenderer.Length
                    ? _blinkSharedMaterialsByRenderer[i]
                    : null;

                if (materials == null || materials.Length == 0)
                {
                    _supportsBlinkProperty[i] = false;
                    continue;
                }

                bool supportsProperty = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null) continue;
                    if (!material.HasProperty(BlinkWhiteId)) continue;

                    supportsProperty = true;
                    break;
                }

                _supportsBlinkProperty[i] = supportsProperty;
            }
        }

        private void ActivateBlinkMaterialSet()
        {
            if (_isBlinkMaterialActive) return;
            if (_resolvedRenderers == null || _resolvedRenderers.Length == 0) return;

            for (int i = 0; i < _resolvedRenderers.Length; i++)
            {
                Renderer renderer = _resolvedRenderers[i];
                if (renderer == null) continue;

                Material[] targetMaterials = _blinkSharedMaterialsByRenderer != null &&
                                             i < _blinkSharedMaterialsByRenderer.Length
                    ? _blinkSharedMaterialsByRenderer[i]
                    : null;

                if (targetMaterials == null || targetMaterials.Length == 0) continue;
                renderer.sharedMaterials = targetMaterials;
            }

            _isBlinkMaterialActive = true;
        }

        private void DeactivateBlinkMaterialSet()
        {
            if (!_isBlinkMaterialActive) return;
            if (_resolvedRenderers == null || _resolvedRenderers.Length == 0) return;

            for (int i = 0; i < _resolvedRenderers.Length; i++)
            {
                Renderer renderer = _resolvedRenderers[i];
                if (renderer == null) continue;

                Material[] originalMaterials = _originalSharedMaterialsByRenderer != null &&
                                               i < _originalSharedMaterialsByRenderer.Length
                    ? _originalSharedMaterialsByRenderer[i]
                    : null;

                if (originalMaterials == null || originalMaterials.Length == 0) continue;
                renderer.sharedMaterials = originalMaterials;
            }

            _isBlinkMaterialActive = false;
        }

        private void SetBlinkWhite(bool isWhite)
        {
            _isWhite = isWhite;

            if (!_isBlinkMaterialActive) return;
            if (_resolvedRenderers == null || _resolvedRenderers.Length == 0) return;
            if (_propertyBlock == null) return;

            float targetValue = isWhite ? 1f : 0f;
            for (int i = 0; i < _resolvedRenderers.Length; i++)
            {
                Renderer renderer = _resolvedRenderers[i];
                if (renderer == null) continue;
                if (_supportsBlinkProperty == null ||
                    i >= _supportsBlinkProperty.Length ||
                    !_supportsBlinkProperty[i])
                {
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(BlinkWhiteId, targetValue);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
