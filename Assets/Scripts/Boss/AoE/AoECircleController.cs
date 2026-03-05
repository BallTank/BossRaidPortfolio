using System.Collections.Generic;
using Core.Interfaces;
using Core.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Boss.AoE
{
    /// <summary>
    /// Manages one AoE circle lifecycle: telegraph -> active -> end.
    /// Includes a runtime fallback disc so circles remain visible even if the assigned renderer is not compatible.
    /// </summary>
    public class AoECircleController : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Renderer telegraphRenderer;
        [SerializeField] private Transform radiusVisualRoot;
        [SerializeField] private float radiusToScaleMultiplier = 2f;
        [SerializeField] private string fillPropertyName = "_Fill01";
        [SerializeField] private string colorPropertyName = "_BaseColor";
        [SerializeField] private string alternateColorPropertyName = "_Color";
        [SerializeField] private Color telegraphColor = new Color(1f, 0f, 0f, 0.25f);
        [SerializeField] private Color activeColor = new Color(1f, 0f, 0f, 0.6f);

        [Header("Fallback Visual")]
        [SerializeField] private bool forceRuntimeFallbackVisual;
        [SerializeField] private float fallbackYOffset = 0f;
        [SerializeField] private int fallbackSegments = 48;
        [SerializeField] private string fallbackShaderName = "Universal Render Pipeline/Unlit";

        [Header("Damage")]
        [SerializeField] private int maxTargets = 16;
        [SerializeField] private LayerMask targetMask = ~0;

        [Header("Debug")]
        [SerializeField] private bool showGizmos;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.25f, 0.25f, 0.8f);

        private Collider[] _hitResults;
        private HashSet<int> _hitTargetIds;
        private MaterialPropertyBlock _propertyBlock;
        private int _fillPropertyId;
        private int _colorPropertyId;
        private int _alternateColorPropertyId;

        private bool _supportsFillProperty;
        private bool _supportsColorProperty;
        private bool _useScaleFillFallback;
        private float _baseVisualScaleY = 1f;

        private Renderer _runtimeFallbackRenderer;
        private Mesh _runtimeFallbackMesh;
        private Material _runtimeFallbackMaterial;

        private bool _isRunning;
        private bool _isActivePhase;
        private float _radius;
        private float _telegraphDuration;
        private float _activeDuration;
        private float _tickInterval;
        private int _damage;
        private int _ownerInstanceID;
        private BossAttackHitType _bossAttackHitType = BossAttackHitType.Attack4Projectile;
        private float _phaseTimer;
        private float _tickTimer;

        public bool IsRunning => _isRunning;

        private void Awake()
        {
            _hitResults = new Collider[Mathf.Max(1, maxTargets)];
            _hitTargetIds = new HashSet<int>(Mathf.Max(4, maxTargets));
            _propertyBlock = new MaterialPropertyBlock();
            _fillPropertyId = Shader.PropertyToID(fillPropertyName);
            _colorPropertyId = Shader.PropertyToID(colorPropertyName);
            _alternateColorPropertyId = Shader.PropertyToID(alternateColorPropertyName);

            if (telegraphRenderer == null)
            {
                telegraphRenderer = GetComponentInChildren<Renderer>(true);
            }

            if (forceRuntimeFallbackVisual || !TryConfigureRendererCapabilities())
            {
                CreateRuntimeFallbackVisual();
                TryConfigureRendererCapabilities();
            }

            if (radiusVisualRoot == null)
            {
                radiusVisualRoot = telegraphRenderer != null ? telegraphRenderer.transform : transform;
            }

            _baseVisualScaleY = Mathf.Max(0.001f, radiusVisualRoot.localScale.y);
        }

        private void Update()
        {
            if (!_isRunning) return;

            if (!_isActivePhase)
            {
                _phaseTimer += Time.deltaTime;
                float fill = _telegraphDuration > 0f ? Mathf.Clamp01(_phaseTimer / _telegraphDuration) : 1f;
                ApplyVisual(fill, telegraphColor);

                if (_phaseTimer >= _telegraphDuration)
                {
                    EnterActivePhase();
                }
                return;
            }

            _phaseTimer += Time.deltaTime;
            _tickTimer -= Time.deltaTime;

            while (_tickTimer <= 0f)
            {
                DealDamageTick();
                _tickTimer += _tickInterval;
            }

            if (_phaseTimer >= _activeDuration)
            {
                End();
            }
        }

        public void StartTelegraph(
            Vector3 centerPosition,
            float radius,
            float telegraphDuration,
            float activeDuration,
            float tickInterval,
            int damage,
            int ownerInstanceID,
            LayerMask damageMask,
            BossAttackHitType bossAttackHitType)
        {
            transform.position = centerPosition;

            _radius = Mathf.Max(0.1f, radius);
            _telegraphDuration = Mathf.Max(0f, telegraphDuration);
            _activeDuration = Mathf.Max(0f, activeDuration);
            _tickInterval = Mathf.Max(0.01f, tickInterval);
            _damage = Mathf.Max(0, damage);
            _ownerInstanceID = ownerInstanceID;
            targetMask = damageMask;
            _bossAttackHitType = bossAttackHitType;

            _phaseTimer = 0f;
            _tickTimer = _tickInterval;
            _isActivePhase = false;
            _isRunning = true;

            ApplyRadiusScale();
            ApplyVisual(0f, telegraphColor);

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void ForceEnd()
        {
            End();
        }

        private void EnterActivePhase()
        {
            _isActivePhase = true;
            _phaseTimer = 0f;
            _tickTimer = 0f;
            ApplyVisual(1f, activeColor);
        }

        private void End()
        {
            _isRunning = false;
            _isActivePhase = false;
            ApplyVisual(0f, telegraphColor);
            gameObject.SetActive(false);
        }

        private void DealDamageTick()
        {
            if (_damage <= 0) return;

            _hitTargetIds.Clear();

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _radius, _hitResults, targetMask);
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _hitResults[i];
                if (col == null) continue;

                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    damageable = col.GetComponentInParent<IDamageable>();
                }

                if (damageable == null) continue;

                int targetId = ExtractTargetInstanceId(damageable, col);
                if (targetId == 0) continue;
                if (_ownerInstanceID != 0 && targetId == _ownerInstanceID) continue;
                if (!_hitTargetIds.Add(targetId)) continue;

                if (_bossAttackHitType != BossAttackHitType.Unknown)
                {
                    IBossAttackHitReceiver bossHitReceiver = col.GetComponent<IBossAttackHitReceiver>();
                    if (bossHitReceiver == null)
                    {
                        bossHitReceiver = col.GetComponentInParent<IBossAttackHitReceiver>();
                    }

                    if (bossHitReceiver != null)
                    {
                        Vector3 forceDirection = col.transform.position - transform.position;
                        forceDirection.y = 0f;
                        if (forceDirection.sqrMagnitude <= 0.0001f)
                        {
                            forceDirection = transform.forward;
                        }

                        bossHitReceiver.ReceiveBossAttackHit(
                            new BossAttackHitData(_damage, _bossAttackHitType, forceDirection));
                        continue;
                    }
                }

                damageable.TakeDamage(_damage);
            }
        }

        private void ApplyRadiusScale()
        {
            if (radiusVisualRoot == null) return;

            Vector3 scale = radiusVisualRoot.localScale;
            float diameterScale = _radius * radiusToScaleMultiplier;
            scale.x = diameterScale;
            scale.z = diameterScale;
            scale.y = _baseVisualScaleY;
            radiusVisualRoot.localScale = scale;
        }

        private void ApplyVisual(float fill01, Color color)
        {
            if (telegraphRenderer == null) return;

            float clampedFill = Mathf.Clamp01(fill01);

            telegraphRenderer.GetPropertyBlock(_propertyBlock);
            if (_supportsFillProperty)
            {
                _propertyBlock.SetFloat(_fillPropertyId, clampedFill);
            }
            if (_supportsColorProperty)
            {
                _propertyBlock.SetColor(_colorPropertyId, color);
                _propertyBlock.SetColor(_alternateColorPropertyId, color);
            }
            telegraphRenderer.SetPropertyBlock(_propertyBlock);

            if (_runtimeFallbackMaterial != null)
            {
                ApplyFallbackMaterialColor(_runtimeFallbackMaterial, color);
            }

            if (_useScaleFillFallback && radiusVisualRoot != null)
            {
                float diameterScale = Mathf.Max(0.001f, _radius * radiusToScaleMultiplier * clampedFill);
                Vector3 scale = radiusVisualRoot.localScale;
                scale.x = diameterScale;
                scale.z = diameterScale;
                scale.y = _baseVisualScaleY;
                radiusVisualRoot.localScale = scale;
            }
        }

        private bool TryConfigureRendererCapabilities()
        {
            _supportsFillProperty = false;
            _supportsColorProperty = false;
            _useScaleFillFallback = false;

            if (telegraphRenderer == null) return false;
            if (telegraphRenderer.GetType().Name.Contains("VFX")) return false;

            Material sharedMaterial = telegraphRenderer.sharedMaterial;
            if (sharedMaterial == null) return false;

            _supportsFillProperty = sharedMaterial.HasProperty(_fillPropertyId);
            _supportsColorProperty = sharedMaterial.HasProperty(_colorPropertyId) || sharedMaterial.HasProperty(_alternateColorPropertyId);
            _useScaleFillFallback = !_supportsFillProperty;
            return true;
        }

        private void CreateRuntimeFallbackVisual()
        {
            if (_runtimeFallbackRenderer != null)
            {
                telegraphRenderer = _runtimeFallbackRenderer;
                radiusVisualRoot = _runtimeFallbackRenderer.transform;
                return;
            }

            GameObject visual = new GameObject("AoE_RuntimeFallbackDisc");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0f, fallbackYOffset, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            MeshFilter meshFilter = visual.AddComponent<MeshFilter>();
            _runtimeFallbackMesh = BuildDiscMesh(Mathf.Clamp(fallbackSegments, 8, 128));
            meshFilter.sharedMesh = _runtimeFallbackMesh;

            MeshRenderer meshRenderer = visual.AddComponent<MeshRenderer>();
            _runtimeFallbackMaterial = CreateFallbackMaterial();
            meshRenderer.sharedMaterial = _runtimeFallbackMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            _runtimeFallbackRenderer = meshRenderer;
            telegraphRenderer = meshRenderer;
            radiusVisualRoot = visual.transform;
            _useScaleFillFallback = true;
        }

        private Material CreateFallbackMaterial()
        {
            Shader shader = Shader.Find(fallbackShaderName);
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = new Material(shader);
            material.renderQueue = (int)RenderQueue.Transparent;
            ApplyFallbackMaterialColor(material, telegraphColor);

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

        private static Mesh BuildDiscMesh(int segments)
        {
            Mesh mesh = new Mesh
            {
                name = "AoE_RuntimeDisc"
            };

            int vertexCount = segments + 2;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);

                int index = i + 1;
                vertices[index] = new Vector3(x, 0f, z);
                uvs[index] = new Vector2((x * 0.5f) + 0.5f, (z * 0.5f) + 0.5f);

                if (i < segments)
                {
                    int tri = i * 3;
                    triangles[tri] = 0;
                    // Clockwise winding for Unity front-face on XZ plane when viewed from above.
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

        private static int ExtractTargetInstanceId(IDamageable damageable, Collider hitCollider)
        {
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

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, _radius > 0f ? _radius : 0.1f);
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
