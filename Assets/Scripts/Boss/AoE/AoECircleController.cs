using System.Collections.Generic;
using Core.Boss;
using Core.Combat;
using Core.Interfaces;
using UnityEngine;
using UnityEngine.Serialization;

namespace Core.Boss.AoE
{
    /// <summary>
    /// AoE 장판의 데미지 로직을 담당한다.
    /// 경고 시각화는 AttackWarningController에 위임한다.
    /// </summary>
    public class AoECircleController : MonoBehaviour
    {
        [Header("Visual Template (Legacy)")]
        [FormerlySerializedAs("telegraphRenderer")]
        [SerializeField] private Renderer warningRenderer;
        [SerializeField] private Transform radiusVisualRoot;
        [SerializeField] private float radiusToScaleMultiplier = 2f;
        [SerializeField] private float fallbackRadiusToScaleMultiplier = 1.2f;
        [SerializeField] private string fillPropertyName = "_Fill01";
        [SerializeField] private string colorPropertyName = "_BaseColor";
        [SerializeField] private string alternateColorPropertyName = "_Color";
        [FormerlySerializedAs("telegraphColor")]
        [SerializeField] private Color warningColor = new Color(1f, 0f, 0f, 0.25f);
        [SerializeField] private Color activeColor = new Color(1f, 0f, 0f, 0.6f);
        [SerializeField] private bool forceRuntimeFallbackVisual;
        [SerializeField] private float fallbackYOffset = 0f;
        [SerializeField] private int fallbackSegments = 48;
        [SerializeField] private string fallbackShaderName = "Universal Render Pipeline/Unlit";
        [SerializeField] private bool showGizmos;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.25f, 0.25f, 0.8f);

        [Header("Damage")]
        [SerializeField] private int maxTargets = 16;
        [SerializeField] private LayerMask targetMask = ~0;

        private Collider[] _hitResults;
        private HashSet<int> _hitTargetIds;
        private AttackWarningController _warningController;
        private bool _isRunning;
        private bool _isActivePhase;
        private int _damage;
        private int _ownerInstanceID;
        private BossAttackHitType _bossAttackHitType = BossAttackHitType.Attack4Projectile;

        public bool IsRunning => _isRunning;

        public AttackWarningController WarningController
        {
            get
            {
                ResolveWarningController();
                return _warningController;
            }
        }

        private void Awake()
        {
            _hitResults = new Collider[Mathf.Max(1, maxTargets)];
            _hitTargetIds = new HashSet<int>(Mathf.Max(4, maxTargets));
            ResolveWarningController();
        }

        private void OnDestroy()
        {
            DetachWarningControllerEvents();
        }

        private void Update()
        {
            if (!_isRunning || !_isActivePhase)
            {
                return;
            }

            DealDamageDuringActivePhase();
        }

        public void StartWarning(
            Vector3 centerPosition,
            float radius,
            float warningDuration,
            float activeDuration,
            int damage,
            int ownerInstanceID,
            LayerMask damageMask,
            BossAttackHitType bossAttackHitType)
        {
            ResolveWarningController();
            if (_warningController == null)
            {
                return;
            }

            _damage = Mathf.Max(0, damage);
            _ownerInstanceID = ownerInstanceID;
            targetMask = damageMask;
            _bossAttackHitType = bossAttackHitType;

            _isRunning = true;
            _isActivePhase = false;
            _hitTargetIds.Clear();

            _warningController.StartWarning(
                centerPosition,
                radius,
                warningDuration,
                activeDuration);
        }

        public void ForceEnd()
        {
            _warningController?.ForceEnd();
            End();
        }

        private void ResolveWarningController()
        {
            if (_warningController == null)
            {
                _warningController = GetComponent<AttackWarningController>();
                if (_warningController == null)
                {
                    _warningController = gameObject.AddComponent<AttackWarningController>();
                }
            }

            if (_warningController == null)
            {
                return;
            }

            _warningController.ApplySettings(BuildWarningVisualSettings());
            AttachWarningControllerEvents();
        }

        private void AttachWarningControllerEvents()
        {
            if (_warningController == null)
            {
                return;
            }

            _warningController.WarningCompleted -= HandleWarningCompleted;
            _warningController.PlaybackCompleted -= HandlePlaybackCompleted;
            _warningController.WarningCompleted += HandleWarningCompleted;
            _warningController.PlaybackCompleted += HandlePlaybackCompleted;
        }

        private void DetachWarningControllerEvents()
        {
            if (_warningController == null)
            {
                return;
            }

            _warningController.WarningCompleted -= HandleWarningCompleted;
            _warningController.PlaybackCompleted -= HandlePlaybackCompleted;
        }

        private AttackWarningController.VisualSettings BuildWarningVisualSettings()
        {
            AttackWarningController.VisualSettings settings = default;
            settings.warningRenderer = warningRenderer;
            settings.radiusVisualRoot = radiusVisualRoot;
            settings.radiusToScaleMultiplier = radiusToScaleMultiplier;
            settings.fallbackRadiusToScaleMultiplier = fallbackRadiusToScaleMultiplier;
            settings.fillPropertyName = fillPropertyName;
            settings.colorPropertyName = colorPropertyName;
            settings.alternateColorPropertyName = alternateColorPropertyName;
            settings.warningColor = warningColor;
            settings.activeColor = activeColor;
            settings.forceRuntimeFallbackVisual = forceRuntimeFallbackVisual;
            settings.fallbackYOffset = fallbackYOffset;
            settings.fallbackSegments = fallbackSegments;
            settings.fallbackShaderName = fallbackShaderName;
            settings.showGizmos = showGizmos;
            settings.gizmoColor = gizmoColor;
            return settings;
        }

        private void HandleWarningCompleted()
        {
            if (!_isRunning)
            {
                return;
            }

            _isActivePhase = true;
            DealDamageDuringActivePhase();
        }

        private void HandlePlaybackCompleted()
        {
            End();
        }

        private void End()
        {
            _isRunning = false;
            _isActivePhase = false;
            _hitTargetIds.Clear();
        }

        private void DealDamageDuringActivePhase()
        {
            if (_damage <= 0)
            {
                return;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, ResolveCurrentRadius(), _hitResults, targetMask);
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _hitResults[i];
                if (col == null)
                {
                    continue;
                }

                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    damageable = col.GetComponentInParent<IDamageable>();
                }

                if (damageable == null)
                {
                    continue;
                }

                int targetId = ExtractTargetInstanceId(damageable, col);
                if (targetId == 0) continue;
                if (_ownerInstanceID != 0 && targetId == _ownerInstanceID) continue;
                if (_hitTargetIds.Contains(targetId)) continue;

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

                        BossAttackHitResolution resolution = bossHitReceiver.ReceiveBossAttackHit(
                            new BossAttackHitData(_damage, _bossAttackHitType, forceDirection));
                        if (resolution != BossAttackHitResolution.Ignored)
                        {
                            _hitTargetIds.Add(targetId);
                        }
                        continue;
                    }
                }

                damageable.TakeDamage(_damage);
                _hitTargetIds.Add(targetId);
            }
        }

        private float ResolveCurrentRadius()
        {
            if (_warningController == null)
            {
                return 0.1f;
            }

            return Mathf.Max(0.1f, _warningController.CurrentRadius);
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
    }
}
