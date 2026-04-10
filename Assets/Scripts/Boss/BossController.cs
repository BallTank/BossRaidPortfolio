using Core.Boss.AoE;
using Core.Boss.Attacks;
using Core.Boss.Projectiles;
using Core.Combat;
using Core.Common;
using Core.Common.Attributes;
using Core.Common.Patterns;
using Core.Multiplayer;
using Core.Player;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Core.Common.Attributes
{
    /// <summary>
    /// Vector2를 최소/최대 슬라이더로 그리기 위한 인스펙터 속성.
    /// </summary>
    public sealed class MinMaxRangeAttribute : PropertyAttribute
    {
        public float Min { get; }
        public float Max { get; }
        public bool ShowFields { get; }

        public MinMaxRangeAttribute(float min, float max, bool showFields = true)
        {
            Min = min;
            Max = max;
            ShowFields = showFields;
        }
    }
}

namespace Core.Boss
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class BossController : MonoBehaviour
    {
        public enum BossPhase
        {
            Phase1,
            Phase2
        }

        [Header("참조 (References)")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private BossVisual animator;
        [SerializeField] private BlinkWhiteEffect damageBlinkEffect;
        [SerializeField, Tooltip("Basic 공격 사거리 기준점 (미할당 시 Boss Root 사용)")]
        private Transform basicAttackRangeOrigin;

        [Header("스탯 (Stats)")]
        [SerializeField] private float moveSpeed = 3.5f; // 애니메이션 Walk 임계값과 동기화
        [SerializeField] private float searchingMoveSpeed = 2.0f;
        [SerializeField] private float rotationSpeed = 5.0f;

        [Header("페이즈 설정 (Phase Settings)")]
        [SerializeField, Range(0.05f, 1f)] private float phaseTwoHealthThreshold = 0.5f;

        [Header("감지 설정 (Detection Settings)")]
        [SerializeField, Tooltip("두 플레이어가 모두 이 범위 안에 있으면 aggro time 종료 후 피해 기여도로 어그로를 비교한다.")]
        private float aggroPriorityRange = 6.0f;
        [SerializeField] private float detectionRange = 10.0f;
        [FormerlySerializedAs("attackRange")]
        [SerializeField, Tooltip("Basic 공격 반경. 시각 경고와 실제 판정에 함께 사용된다.")]
        private float basicAttackRange = 2.5f;
        [SerializeField] private float lungeAttackRange = 4.5f;
        [FormerlySerializedAs("projectileAttackRange")]
        [FormerlySerializedAs("aoeAttackRange")]
        [SerializeField] private float sharedRangedAttackRange = 6.0f;
        [SerializeField, Tooltip("공격 사거리 경계 지터 완화를 위한 추적 재진입 여유 거리")]
        private float chaseReengageBuffer = 1.0f;
        [SerializeField] private float searchDuration = 5.0f;

        [Header("어그로 설정 (Aggro Settings)")]
        [FormerlySerializedAs("aggroDamageWindow")]
        [SerializeField, Tooltip("첫 피격 후 피해 기여도를 확정하기 전까지 누적할 시간(초)")]
        private float aggroTime = 3.0f;

        [Header("공격 설정 (Attack Settings)")]
        [SerializeField] private int attackDamage = 20;
        [SerializeField] private float attackDuration = 1.0f;
        [SerializeField] private float attackCooldown = 2.0f;

        [Header("Basic Attack Settings")]
        [SerializeField] private BasicAttackSettings basicAttackSettings;

        [Header("Legacy DamageCaster References")]
        [Tooltip("레거시 Head DamageCaster 참조 (Basic/Lunge는 더 이상 사용하지 않음)")]
        [SerializeField] private DamageCaster _headDamageCaster;
        [Tooltip("레거시 Lunge DamageCaster 참조 (Basic/Lunge는 더 이상 사용하지 않음)")]
        [FormerlySerializedAs("_clawDamageCaster")]
        [SerializeField] private DamageCaster _lungeDamageCaster;

        [Header("Lunge Attack Settings")]
        [FormerlySerializedAs("clawAttackSettings")]
        [SerializeField] private LungeAttackSettings lungeAttackSettings;

        [Header("Projectile Attack Settings")]
        [SerializeField] private ProjectileAttackSettings projectileAttackSettings;
        [SerializeField] private BossProjectilePool projectilePool;
        [SerializeField] private Transform projectileSpawnPoint;

        [Header("AoE Attack Settings")]
        [SerializeField] private AoEAttackSettings aoeAttackSettings;

        [Header("디버그 설정 (Debug Settings)")]
        [SerializeField] private bool enableChase = true;
        [SerializeField] private bool enableRotation = true;
        [SerializeField] private bool enableBasicAttack = true;
        [FormerlySerializedAs("enableClawAttack")]
        [SerializeField] private bool enableLungeAttack = true;
        [SerializeField] private bool enableProjectileAttack = true;
        [SerializeField] private bool enableAoEAttack = true;
        [SerializeField, Tooltip("보스 CharacterController와 플레이어 콜라이더 충돌을 무시한다.")]
        private bool ignorePlayerCollision = true;
        [SerializeField] private bool showPhaseDebugLabel = true;

        // FSM (제네릭 StateMachine 사용)
        private StateMachine<BossBaseState> _stateMachine;
        public StateMachine<BossBaseState> StateMachine => _stateMachine;

        // States
        public BossIdleState IdleState { get; private set; }
        public BossCombatState CombatState { get; private set; }
        public BossSearchingState SearchingState { get; private set; }
        public BossAttackState AttackState { get; private set; }
        public BossHitState HitState { get; private set; }
        public BossDeadState DeadState { get; private set; }

        // Attack Patterns
        public BasicAttackPattern BasicAttackPattern { get; private set; }
        public LungeAttackPattern LungeAttackPattern { get; private set; }
        public ProjectileAttackPattern ProjectileAttackPattern { get; private set; }
        public AoEAttackPattern AoEAttackPattern { get; private set; }

        // Components
        private CharacterController _characterController;
        private Health _health;
        private AttackWarningController _basicAttackTelegraph;
        private AttackWarningController _lungeAttackTelegraph;
        private float _nextAttackTime;
        private BossAuthoritativeAttackId _currentAuthoritativeAttackId;
        private float _currentAttackStartTime = -1f;
        private readonly Queue<BossReplicatedEffectEvent> _pendingReplicatedEffectEvents = new Queue<BossReplicatedEffectEvent>(8);
        private int _nextReplicatedEffectSequenceId;

        // Phase Flow
        private BossPhase _currentPhase = BossPhase.Phase1;
        private bool _phaseOneIntroCompleted;
        private bool _phaseTwoIntroCompleted;
        private bool _phaseTwoTriggered;
        private bool _phaseIntroPlaying;
        private float _phaseIntroEndTime;
        private bool _suppressLocomotionVisual;
        private Vector3 _lungeTravelDirection = Vector3.forward;
        private bool _isLungeTravelDirectionLocked;
        private bool _isLungeTravelActive;
        private float _remainingLungeTravelDistance;
        private float _lungeTravelSpeed;
        private bool _hasAppliedPlayerCollisionIgnore;
        private int _ignoredPlayerRootInstanceId;
        private const float LungeRootMotionMinStep = 0.0001f;
        private const float ClosestLiveTargetRefreshInterval = 0.1f;
        private const int MaxAggroCandidateCount = 8;
        private float _nextClosestLiveTargetRefreshTime;
        private readonly Transform[] _aggroCandidateBuffer = new Transform[MaxAggroCandidateCount];
        private readonly Transform[] _aggroContributorBuffer = new Transform[MaxAggroCandidateCount];
        private readonly int[] _aggroContributorDamageBuffer = new int[MaxAggroCandidateCount];
        private int _aggroContributorCount;
        private bool _isAggroTimerRunning;
        private float _aggroTimerRemaining;
        private Transform _lockedAggroTarget;

        /// <summary>
        /// 어그로 타겟 재평가에 필요한 스캔 결과를 한 번에 묶어 전달한다.
        /// </summary>
        private readonly struct AggroTargetScanResult
        {
            public AggroTargetScanResult(Transform closestTarget, int aggroCandidateCount)
            {
                ClosestTarget = closestTarget;
                AggroCandidateCount = aggroCandidateCount;
            }

            public Transform ClosestTarget { get; }
            public int AggroCandidateCount { get; }
        }

        /// <summary>
        /// Unity 에디터에서 스크립트가 로드되거나 인스펙터의 값이 변경될 때 호출되어 데이터의 유효성을 검사합니다.
        /// </summary>
        private void OnValidate()
        {
            // 이동 속도는 음수가 되지 않도록 보정
            if (moveSpeed < 0) moveSpeed = 0f;
            if (searchingMoveSpeed < 0) searchingMoveSpeed = 0f;
            phaseTwoHealthThreshold = Mathf.Clamp01(phaseTwoHealthThreshold);
            if (detectionRange < 0f) detectionRange = 0f;
            aggroPriorityRange = Mathf.Clamp(aggroPriorityRange, 0f, detectionRange);
            if (aggroTime < 0f) aggroTime = 0f;
            if (basicAttackRange < 0f) basicAttackRange = 0f;
            if (lungeAttackRange < 0f) lungeAttackRange = 0f;
            if (sharedRangedAttackRange < 0f) sharedRangedAttackRange = 0f;
            if (chaseReengageBuffer < 0f) chaseReengageBuffer = 0f;

            if (basicAttackSettings == null)
            {
                basicAttackSettings = new BasicAttackSettings();
            }
            else
            {
                basicAttackSettings.ClampValues();
            }

            if (lungeAttackSettings != null)
            {
                lungeAttackSettings.ClampValues();
            }

            if (projectileAttackSettings != null)
            {
                if (projectileAttackSettings.volleyCount < 1) projectileAttackSettings.volleyCount = 1;
                if (projectileAttackSettings.volleyInterval < 0f) projectileAttackSettings.volleyInterval = 0f;
                if (projectileAttackSettings.postFireRecoveryDuration < 0f) projectileAttackSettings.postFireRecoveryDuration = 0f;
                projectileAttackSettings.exitNormalizedTime =
                    Mathf.Clamp(projectileAttackSettings.exitNormalizedTime, 0.5f, 1.2f);
            }

        }

        // Public Properties for States
        public Transform Target => playerTransform;
        public BossVisual Visual => animator;
        public float MoveSpeed => moveSpeed;
        public float SearchingMoveSpeed => searchingMoveSpeed;
        public float DetectionRange => detectionRange;
        public float AggroPriorityRange => Mathf.Min(aggroPriorityRange, detectionRange);
        public float AggroTime => aggroTime;
        public float BasicAttackRange => Mathf.Max(0f, basicAttackRange);
        public float LungeAttackRange => ResolveConfiguredLungeTravelDistance();
        public float SharedRangedAttackRange => sharedRangedAttackRange;
        public float ProjectileAttackRange => sharedRangedAttackRange;
        public float AoEAttackRange => sharedRangedAttackRange;
        public float ChaseReengageBuffer => chaseReengageBuffer;
        public float SearchDuration => searchDuration;
        public int AttackDamage => attackDamage;
        public float AttackDuration => attackDuration;
        public BasicAttackSettings BasicAttackConfig => basicAttackSettings;
        public bool CanAttack => Time.time >= _nextAttackTime;
        public bool EnableChase => enableChase;
        public bool EnableBasicAttack => enableBasicAttack;
        public bool EnableLungeAttack => enableLungeAttack;
        public bool EnableProjectileAttack => enableProjectileAttack;
        public bool EnableAoEAttack => enableAoEAttack;
        public BossProjectilePool ProjectilePool => projectilePool;
        public Transform ProjectileSpawnPoint => projectileSpawnPoint;
        public BossPhase CurrentPhase => _currentPhase;
        public bool IsPhaseIntroPlaying => _phaseIntroPlaying;
        public bool IsPhaseOneAttackWindow => _currentPhase == BossPhase.Phase1 && _phaseOneIntroCompleted && !_phaseIntroPlaying;
        public bool IsPhaseTwoAttackWindow => _currentPhase == BossPhase.Phase2 && _phaseTwoIntroCompleted && !_phaseIntroPlaying;
        public bool IsLocomotionVisualSuppressed => _suppressLocomotionVisual;

        /// <summary>
        /// Host authority가 현재 boss truth를 dedicated DTO로 캡처한다.
        /// </summary>
        public BossAuthoritativeState CaptureAuthoritativeState(int currentServerTick, float networkFixedDeltaTime)
        {
            EnsureRuntimeReferences();

            BossAuthoritativeState state = default;
            state.Position = transform.position;
            state.Rotation = transform.rotation;
            state.LocomotionState = ResolveAuthoritativeLocomotionState();
            state.CurrentAttackId = _currentAuthoritativeAttackId;
            state.AttackVisualState = ResolveAuthoritativeAttackVisualState();
            state.AttackStartServerTick = ResolveAuthoritativeAttackStartServerTick(
                currentServerTick,
                networkFixedDeltaTime);
            state.AttackNormalizedTime = ResolveAuthoritativeAttackNormalizedTime(
                currentServerTick,
                networkFixedDeltaTime);
            state.AttackPlaybackSpeed = ResolveAuthoritativeAttackPlaybackSpeed();
            state.CurrentHealth = _health != null ? _health.CurrentHealth : 0;
            state.MaxHealth = _health != null ? _health.MaxHealth : 0;
            state.Phase = ResolveAuthoritativePhase();
            state.IsDead = (_health != null && _health.IsDead)
                           || (_stateMachine != null && _stateMachine.CurrentState == DeadState);
            return state;
        }

        /// <summary>
        /// 공격 진입 시 attack id와 시작 시각을 한 번만 기록한다.
        /// </summary>
        public void BeginAuthoritativeAttack(IBossAttackPattern pattern)
        {
            _currentAuthoritativeAttackId = ResolveAuthoritativeAttackId(pattern);
            _currentAttackStartTime = Time.time;
        }

        /// <summary>
        /// 공격 종료 또는 사망 시 active attack bookkeeping을 비운다.
        /// </summary>
        public void EndAuthoritativeAttack()
        {
            _currentAuthoritativeAttackId = BossAuthoritativeAttackId.None;
            _currentAttackStartTime = -1f;
        }

        /// <summary>
        /// Host가 공격 3 투사체 표시 이벤트를 remote client용으로 큐에 적재한다.
        /// </summary>
        public void EnqueueReplicatedProjectileShot(
            Vector3 startPosition,
            Vector3 direction,
            float speed,
            float lifetime,
            Transform target,
            float homingStrength,
            float homingDuration,
            float verticalFollowSpeed)
        {
            if (!ShouldQueueReplicatedEffect())
            {
                return;
            }

            BossReplicatedEffectEvent effect = default;
            effect.EffectKind = BossReplicatedEffectKind.ProjectileShot;
            effect.SequenceId = ++_nextReplicatedEffectSequenceId;
            effect.StartPosition = startPosition;
            effect.Direction = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : transform.forward;
            effect.Speed = Mathf.Max(0f, speed);
            effect.Lifetime = Mathf.Max(0f, lifetime);
            effect.TargetNetworkObjectId = ResolveTargetNetworkObjectId(target);
            effect.HomingStrength = Mathf.Clamp01(homingStrength);
            effect.HomingDuration = Mathf.Max(0f, homingDuration);
            effect.VerticalFollowSpeed = Mathf.Max(0f, verticalFollowSpeed);
            _pendingReplicatedEffectEvents.Enqueue(effect);
        }

        /// <summary>
        /// Host가 공격 4 장판/낙하 표시 이벤트를 remote client용으로 큐에 적재한다.
        /// </summary>
        public void EnqueueReplicatedAoESpawn(
            Vector3 projectileStartPosition,
            Vector3 impactPosition,
            float radius,
            float warningDuration,
            float activeDuration)
        {
            if (!ShouldQueueReplicatedEffect())
            {
                return;
            }

            BossReplicatedEffectEvent effect = default;
            effect.EffectKind = BossReplicatedEffectKind.AoESpawn;
            effect.SequenceId = ++_nextReplicatedEffectSequenceId;
            effect.StartPosition = projectileStartPosition;
            effect.ImpactPosition = impactPosition;
            effect.Radius = Mathf.Max(0f, radius);
            effect.WarningDuration = Mathf.Max(0f, warningDuration);
            effect.ActiveDuration = Mathf.Max(0f, activeDuration);
            _pendingReplicatedEffectEvents.Enqueue(effect);
        }

        /// <summary>
        /// Host가 Basic/Lunge 경고 표시 이벤트를 remote client용으로 큐에 적재한다.
        /// </summary>
        public void EnqueueReplicatedAttackWarningShow(
            BossReplicatedWarningChannel warningChannel,
            BossReplicatedWarningShape warningShape,
            Vector3 startPosition,
            Vector3 forwardDirection,
            float warningDuration,
            float activeDuration,
            float radius,
            float length,
            float width,
            float sectorAngle)
        {
            if (!ShouldQueueReplicatedEffect())
            {
                return;
            }

            BossReplicatedEffectEvent effect = default;
            effect.EffectKind = BossReplicatedEffectKind.AttackWarningShow;
            effect.SequenceId = ++_nextReplicatedEffectSequenceId;
            effect.WarningChannel = warningChannel;
            effect.WarningShape = warningShape;
            effect.StartPosition = startPosition;
            effect.Direction = forwardDirection.sqrMagnitude > 0.0001f
                ? forwardDirection.normalized
                : transform.forward;
            effect.WarningDuration = Mathf.Max(0f, warningDuration);
            effect.ActiveDuration = Mathf.Max(0f, activeDuration);
            effect.Radius = Mathf.Max(0f, radius);
            effect.Length = Mathf.Max(0f, length);
            effect.Width = Mathf.Max(0f, width);
            effect.SectorAngle = Mathf.Clamp(sectorAngle, 0f, AttackWarningController.FullSectorAngle);
            _pendingReplicatedEffectEvents.Enqueue(effect);
        }

        /// <summary>
        /// Host가 Basic/Lunge 경고 종료 이벤트를 remote client용으로 큐에 적재한다.
        /// </summary>
        public void EnqueueReplicatedAttackWarningHide(BossReplicatedWarningChannel warningChannel)
        {
            if (!ShouldQueueReplicatedEffect())
            {
                return;
            }

            BossReplicatedEffectEvent effect = default;
            effect.EffectKind = BossReplicatedEffectKind.AttackWarningHide;
            effect.SequenceId = ++_nextReplicatedEffectSequenceId;
            effect.WarningChannel = warningChannel;
            _pendingReplicatedEffectEvents.Enqueue(effect);
        }

        /// <summary>
        /// Bridge가 Host에서 적재된 이펙트 이벤트를 순서대로 꺼낸다.
        /// </summary>
        public bool TryDequeueReplicatedEffectEvent(out BossReplicatedEffectEvent effect)
        {
            if (_pendingReplicatedEffectEvents.Count > 0)
            {
                effect = _pendingReplicatedEffectEvents.Dequeue();
                return true;
            }

            effect = default;
            return false;
        }

        /// <summary>
        /// remote receiver가 없을 때 쌓인 표시 이벤트를 비운다.
        /// </summary>
        public void ClearPendingReplicatedEffectEvents()
        {
            _pendingReplicatedEffectEvents.Clear();
        }

        private static bool ShouldQueueReplicatedEffect()
        {
            return NetworkManager.Singleton != null
                   && NetworkManager.Singleton.IsServer
                   && MultiplayerSessionService.HasInstance
                   && MultiplayerSessionService.Instance.HasActiveSession;
        }

        /// <summary>
        /// 런타임에서 보스 추적 타겟을 다시 바꿀 때 사용한다.
        /// 멀티플레이 씬에서는 legacy player 제거 후 network avatar로 재바인딩한다.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            playerTransform = newTarget;
            _hasAppliedPlayerCollisionIgnore = false;
            _ignoredPlayerRootInstanceId = 0;
            TryApplyPlayerCollisionIgnore();
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            ResolveDamageBlinkEffect();
            if (basicAttackSettings == null) basicAttackSettings = new BasicAttackSettings();
            if (projectileAttackSettings == null) projectileAttackSettings = new ProjectileAttackSettings();
            if (lungeAttackSettings == null) lungeAttackSettings = new LungeAttackSettings();
            if (aoeAttackSettings == null) aoeAttackSettings = new AoEAttackSettings();

            // 플레이어가 할당되지 않았다면 자동으로 찾음
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }

            TryApplyPlayerCollisionIgnore();

            // FSM 초기화 (제네릭 StateMachine)
            _stateMachine = new StateMachine<BossBaseState>();
            IdleState = new BossIdleState(this);
            CombatState = new BossCombatState(this);
            AttackState = new BossAttackState(this);
            SearchingState = new BossSearchingState(this);
            HitState = new BossHitState(this);
            DeadState = new BossDeadState(this);

            // Attack Patterns 초기화
            BasicAttackPattern = new BasicAttackPattern();
            LungeAttackPattern = new LungeAttackPattern(lungeAttackSettings);
            ProjectileAttackPattern = new ProjectileAttackPattern(projectileAttackSettings);
            AoEAttackPattern = new AoEAttackPattern(aoeAttackSettings);
            AoEAttackPattern.PrepareDisplayPool();

            if (_health != null)
            {
                _health.OnDamageTaken += HandleDamage;
                _health.OnDeath += HandleDeath;
            }

            HitTraceLogger.Log(
                $"[HitTrace][BOOT][BossController][Awake] object={name} attackDamage={attackDamage} " +
                $"basicRange={basicAttackRange:F2} lungeRange={lungeAttackRange:F2}");
        }

        private void OnDestroy()
        {
            HideBasicAttackTelegraph("OnDestroy.InitialCleanup", true);
            HideLungeAttackTelegraph("OnDestroy.InitialCleanup", true);
            if (_basicAttackTelegraph != null)
            {
                Destroy(_basicAttackTelegraph.gameObject);
                _basicAttackTelegraph = null;
            }
            if (_lungeAttackTelegraph != null)
            {
                Destroy(_lungeAttackTelegraph.gameObject);
                _lungeAttackTelegraph = null;
            }

            if (_health != null)
            {
                _health.OnDamageTaken -= HandleDamage;
                _health.OnDeath -= HandleDeath;
            }
        }

        private void Start()
        {
            _headDamageCaster?.ForceDisableHitbox();
            _lungeDamageCaster?.ForceDisableHitbox();

            TryApplyPlayerCollisionIgnore();
            damageBlinkEffect?.StopBlink();
            _stateMachine.ChangeState(IdleState);
            HitTraceLogger.Log($"[HitTrace][BOOT][BossController][Start] object={name} state=Idle");
        }

        private void Update()
        {
            TryApplyPlayerCollisionIgnore();
            ApplyGravity();
            UpdatePhaseFlow();

            // Controller에서 직접 Update 호출
            _stateMachine.CurrentState?.Update();
            UpdateAggroTimer();
        }

        private void HandleDamage(int damage)
        {
            // 이미 죽었으면 반응 안 함
            if (_health.IsDead) return;

            // 피격 시각 효과는 상태와 무관하게 항상 재생한다.
            damageBlinkEffect?.PlaySingleBlink();

            // 공격 준비/실행 중에는 피격 모션을 무시한다.
            if (ShouldIgnoreHitMotion()) return;

            // FSM을 통해 Hit 상태로 전환
            _stateMachine.ChangeState(HitState);
        }

        private void HandleDeath()
        {
            damageBlinkEffect?.StopBlink();
            ResetAggroState();
            EndAuthoritativeAttack();
            _stateMachine.ChangeState(DeadState);
        }

        private bool ShouldIgnoreHitMotion()
        {
            if (_stateMachine == null) return false;

            BossBaseState currentState = _stateMachine.CurrentState;
            if (currentState == null) return false;

            if (currentState == AttackState) return true;
            if (_phaseIntroPlaying) return true;

            return false;
        }

        private void ResolveDamageBlinkEffect()
        {
            if (damageBlinkEffect != null) return;

            if (animator != null)
            {
                damageBlinkEffect = animator.GetComponent<BlinkWhiteEffect>();
                if (damageBlinkEffect == null)
                {
                    damageBlinkEffect = animator.GetComponentInChildren<BlinkWhiteEffect>(true);
                }
            }

            if (damageBlinkEffect == null)
            {
                damageBlinkEffect = GetComponent<BlinkWhiteEffect>();
            }
        }

        private void EnsureRuntimeReferences()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_health == null)
            {
                _health = GetComponent<Health>();
            }
        }

        private bool TryResolveBasicAttackTelegraph(out AttackWarningController telegraph)
        {
            return TryResolveAttackWarningController(ref _basicAttackTelegraph, out telegraph);
        }

        private bool TryResolveLungeAttackTelegraph(out AttackWarningController telegraph)
        {
            return TryResolveAttackWarningController(ref _lungeAttackTelegraph, out telegraph);
        }

        private bool TryResolveAttackWarningController(
            ref AttackWarningController cachedTelegraph,
            out AttackWarningController telegraph)
        {
            telegraph = cachedTelegraph;
            if (telegraph != null)
            {
                return true;
            }

            if (aoeAttackSettings == null || aoeAttackSettings.circlePrefab == null)
            {
                return false;
            }

            GameObject telegraphObject = aoeAttackSettings.circleRoot != null
                ? Instantiate(aoeAttackSettings.circlePrefab.gameObject, aoeAttackSettings.circleRoot)
                : Instantiate(aoeAttackSettings.circlePrefab.gameObject);
            telegraph = telegraphObject.GetComponent<AttackWarningController>();
            if (telegraph == null)
            {
                AoECircleController circleController = telegraphObject.GetComponent<AoECircleController>();
                telegraph = circleController != null ? circleController.WarningController : null;
            }

            if (telegraph == null)
            {
                Destroy(telegraphObject);
                return false;
            }

            telegraph.gameObject.SetActive(false);
            cachedTelegraph = telegraph;
            return true;
        }

        private static ulong ResolveTargetNetworkObjectId(Transform target)
        {
            if (target == null)
            {
                return 0;
            }

            NetworkObject networkObject = target.GetComponentInParent<NetworkObject>();
            return networkObject != null ? networkObject.NetworkObjectId : 0;
        }

        private BossAuthoritativeLocomotionState ResolveAuthoritativeLocomotionState()
        {
            if (_health != null && _health.IsDead)
            {
                return BossAuthoritativeLocomotionState.Dead;
            }

            if (_stateMachine == null || _stateMachine.CurrentState == null)
            {
                return BossAuthoritativeLocomotionState.Unknown;
            }

            if (_phaseIntroPlaying)
            {
                return BossAuthoritativeLocomotionState.PhaseIntro;
            }

            BossBaseState currentState = _stateMachine.CurrentState;
            if (currentState == DeadState)
            {
                return BossAuthoritativeLocomotionState.Dead;
            }

            if (currentState == HitState)
            {
                return BossAuthoritativeLocomotionState.Hit;
            }

            if (currentState == AttackState)
            {
                return BossAuthoritativeLocomotionState.Attack;
            }

            if (currentState == SearchingState)
            {
                return BossAuthoritativeLocomotionState.Search;
            }

            float planarSpeed = ResolvePlanarSpeed();
            if (planarSpeed > 0.01f)
            {
                return BossAuthoritativeLocomotionState.Move;
            }

            return BossAuthoritativeLocomotionState.Idle;
        }

        private float ResolvePlanarSpeed()
        {
            if (_characterController == null)
            {
                return 0f;
            }

            Vector3 velocity = _characterController.velocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }

        private BossAuthoritativePhase ResolveAuthoritativePhase()
        {
            return _currentPhase switch
            {
                BossPhase.Phase1 => BossAuthoritativePhase.Phase1,
                BossPhase.Phase2 => BossAuthoritativePhase.Phase2,
                _ => BossAuthoritativePhase.None
            };
        }

        private int ResolveAuthoritativeAttackStartServerTick(int currentServerTick, float networkFixedDeltaTime)
        {
            if (_currentAuthoritativeAttackId == BossAuthoritativeAttackId.None
                || _currentAttackStartTime < 0f
                || networkFixedDeltaTime <= 0f)
            {
                return 0;
            }

            // 공격 시작 프레임 직후 캡처되더라도 미래 tick으로 밀리지 않게 약간 보수적으로 환산한다.
            float elapsedSeconds = Mathf.Max(0f, Time.time - _currentAttackStartTime);
            int elapsedTicks = Mathf.CeilToInt(elapsedSeconds / networkFixedDeltaTime);
            return Mathf.Max(0, currentServerTick - elapsedTicks);
        }

        private float ResolveAuthoritativeAttackNormalizedTime(int currentServerTick, float networkFixedDeltaTime)
        {
            if (_currentAuthoritativeAttackId == BossAuthoritativeAttackId.None)
            {
                return -1f;
            }

            if (TryResolveCurrentAttackAnimatorNormalizedTime(out float normalizedTime))
            {
                return normalizedTime;
            }

            float clipLength = ResolveAuthoritativeAttackClipLengthOrDefault();
            if (_currentAttackStartTime < 0f || clipLength <= 0.0001f)
            {
                return -1f;
            }

            int attackStartServerTick = ResolveAuthoritativeAttackStartServerTick(
                currentServerTick,
                networkFixedDeltaTime);
            if (attackStartServerTick <= 0 || networkFixedDeltaTime <= 0f)
            {
                float elapsedSeconds = Mathf.Max(0f, Time.time - _currentAttackStartTime);
                return Mathf.Clamp01(elapsedSeconds / clipLength);
            }

            float elapsedByTick = Mathf.Max(0f, (currentServerTick - attackStartServerTick) * networkFixedDeltaTime);
            return Mathf.Clamp01(elapsedByTick / clipLength);
        }

        private float ResolveAuthoritativeAttackPlaybackSpeed()
        {
            if (_currentAuthoritativeAttackId == BossAuthoritativeAttackId.None
                || animator == null
                || animator.Animator == null)
            {
                return 1f;
            }

            return Mathf.Max(0.01f, animator.Animator.speed);
        }

        private bool TryResolveCurrentAttackAnimatorNormalizedTime(out float normalizedTime)
        {
            normalizedTime = -1f;
            if (animator == null || animator.Animator == null)
            {
                return false;
            }

            AnimatorStateInfo stateInfo = animator.Animator.GetCurrentAnimatorStateInfo(0);
            if (!TryResolveCurrentAttackVisualState(_currentAuthoritativeAttackId, stateInfo, out _))
            {
                return false;
            }

            normalizedTime = Mathf.Clamp01(stateInfo.normalizedTime);
            return true;
        }

        private BossAuthoritativeAttackVisualState ResolveAuthoritativeAttackVisualState()
        {
            if (_currentAuthoritativeAttackId == BossAuthoritativeAttackId.None)
            {
                return BossAuthoritativeAttackVisualState.None;
            }

            if (animator != null && animator.Animator != null)
            {
                AnimatorStateInfo stateInfo = animator.Animator.GetCurrentAnimatorStateInfo(0);
                if (TryResolveCurrentAttackVisualState(_currentAuthoritativeAttackId, stateInfo, out BossAuthoritativeAttackVisualState visualState))
                {
                    return visualState;
                }
            }

            return ResolveFallbackAuthoritativeAttackVisualState(_currentAuthoritativeAttackId);
        }

        private float ResolveAuthoritativeAttackClipLengthOrDefault()
        {
            if (animator == null)
            {
                return Mathf.Max(attackDuration, 0.1f);
            }

            return _currentAuthoritativeAttackId switch
            {
                BossAuthoritativeAttackId.Basic => animator.GetBasicAttackClipLengthOrDefault(attackDuration),
                BossAuthoritativeAttackId.Lunge => animator.GetLungeAttackClipLengthOrDefault(attackDuration),
                BossAuthoritativeAttackId.Projectile => Mathf.Max(attackDuration, 1f),
                BossAuthoritativeAttackId.AoE => Mathf.Max(attackDuration, 1.2f),
                _ => Mathf.Max(attackDuration, 0.1f)
            };
        }

        private static bool TryResolveCurrentAttackVisualState(
            BossAuthoritativeAttackId attackId,
            AnimatorStateInfo stateInfo,
            out BossAuthoritativeAttackVisualState visualState)
        {
            visualState = attackId switch
            {
                BossAuthoritativeAttackId.Basic when stateInfo.IsName("Basic Attack")
                    => BossAuthoritativeAttackVisualState.Basic,
                BossAuthoritativeAttackId.Lunge when stateInfo.IsName("Lunge Attack")
                                                   || stateInfo.IsName("Claw Attack")
                    => BossAuthoritativeAttackVisualState.Lunge,
                BossAuthoritativeAttackId.Projectile when stateInfo.IsName("Flame Attack")
                                                        || stateInfo.IsName("Fireball Shoot")
                                                        || stateInfo.IsName("Basic Attack")
                    => BossAuthoritativeAttackVisualState.Projectile,
                BossAuthoritativeAttackId.AoE when stateInfo.IsName("takeOff")
                                                 || stateInfo.IsName("TakeOff")
                    => BossAuthoritativeAttackVisualState.AoETakeOff,
                BossAuthoritativeAttackId.AoE when stateInfo.IsName("FlyForward")
                                                 || stateInfo.IsName("Fly Forward")
                    => BossAuthoritativeAttackVisualState.AoEFlyForward,
                BossAuthoritativeAttackId.AoE when stateInfo.IsName("FlyIdle")
                                                 || stateInfo.IsName("Fly Idle")
                    => BossAuthoritativeAttackVisualState.AoEFlyIdle,
                BossAuthoritativeAttackId.AoE when stateInfo.IsName("Land")
                    => BossAuthoritativeAttackVisualState.AoELand,
                _ => BossAuthoritativeAttackVisualState.None
            };

            return visualState != BossAuthoritativeAttackVisualState.None;
        }

        private static BossAuthoritativeAttackVisualState ResolveFallbackAuthoritativeAttackVisualState(
            BossAuthoritativeAttackId attackId)
        {
            return attackId switch
            {
                BossAuthoritativeAttackId.Basic => BossAuthoritativeAttackVisualState.Basic,
                BossAuthoritativeAttackId.Lunge => BossAuthoritativeAttackVisualState.Lunge,
                BossAuthoritativeAttackId.Projectile => BossAuthoritativeAttackVisualState.Projectile,
                BossAuthoritativeAttackId.AoE => BossAuthoritativeAttackVisualState.AoETakeOff,
                _ => BossAuthoritativeAttackVisualState.None
            };
        }

        private static BossAuthoritativeAttackId ResolveAuthoritativeAttackId(IBossAttackPattern pattern)
        {
            return pattern switch
            {
                Core.Boss.Attacks.BasicAttackPattern => BossAuthoritativeAttackId.Basic,
                Core.Boss.Attacks.LungeAttackPattern => BossAuthoritativeAttackId.Lunge,
                Core.Boss.Attacks.ProjectileAttackPattern => BossAuthoritativeAttackId.Projectile,
                Core.Boss.Attacks.AoEAttackPattern => BossAuthoritativeAttackId.AoE,
                _ => BossAuthoritativeAttackId.None
            };
        }

        #region Phase Methods

        public void EnsurePhaseIntroForCurrentPhase()
        {
            if (_health != null && _health.IsDead) return;

            if (_currentPhase == BossPhase.Phase1 && !_phaseOneIntroCompleted && !_phaseIntroPlaying)
            {
                BeginPhaseIntro(BossPhase.Phase1);
                return;
            }

            if (_currentPhase == BossPhase.Phase2 && !_phaseTwoIntroCompleted && !_phaseIntroPlaying)
            {
                BeginPhaseIntro(BossPhase.Phase2);
            }
        }

        private void UpdatePhaseFlow()
        {
            if (_health == null || _health.IsDead) return;

            if (!_phaseTwoTriggered && _health.HealthRatio <= phaseTwoHealthThreshold)
            {
                TriggerPhaseTwo();
            }

            if (_phaseIntroPlaying && Time.time >= _phaseIntroEndTime)
            {
                _phaseIntroPlaying = false;
                if (_currentPhase == BossPhase.Phase1)
                {
                    _phaseOneIntroCompleted = true;
                }
                else
                {
                    _phaseTwoIntroCompleted = true;
                }
            }
        }

        private void TriggerPhaseTwo()
        {
            if (_phaseTwoTriggered) return;

            _phaseTwoTriggered = true;
            _currentPhase = BossPhase.Phase2;
            _phaseTwoIntroCompleted = false;

            BeginPhaseIntro(BossPhase.Phase2);

            if (_stateMachine.CurrentState != DeadState && _stateMachine.CurrentState != CombatState)
            {
                _stateMachine.ChangeState(CombatState);
            }
        }

        private void BeginPhaseIntro(BossPhase phase)
        {
            _currentPhase = phase;
            StopMoving();

            float introDuration = animator != null ? animator.PlayScream() : 1.2f;
            _phaseIntroPlaying = true;
            _phaseIntroEndTime = Time.time + Mathf.Max(0.1f, introDuration);
        }

        #endregion

        private void OnGUI()
        {
            if (!showPhaseDebugLabel) return;

            float healthRatio = _health != null ? _health.HealthRatio : 0f;
            string targetLabel = playerTransform != null ? playerTransform.name : "None";
            string lockedTargetLabel = _lockedAggroTarget != null ? _lockedAggroTarget.name : "None";
            string aggroMode = _isAggroTimerRunning
                ? "TimerHold"
                : _lockedAggroTarget != null ? "DamageLock" : IsCurrentTargetWithinAggroPriorityRange() ? "TargetHold" : "Distance";
            int currentTargetCycleDamage = ResolveAggroContributionDamage(playerTransform);
            string debugText =
                $"Boss Phase: {_currentPhase}\n" +
                $"HP: {healthRatio * 100f:0.#}%\n" +
                $"Intro Playing: {_phaseIntroPlaying}\n" +
                $"Phase2 Triggered: {_phaseTwoTriggered}\n" +
                $"Target: {targetLabel}\n" +
                $"Aggro Mode: {aggroMode}\n" +
                $"Aggro Circle: {AggroPriorityRange:0.#}\n" +
                $"Aggro Time: {aggroTime:0.#}s\n" +
                $"Aggro Timer: {(_isAggroTimerRunning ? _aggroTimerRemaining : 0f):0.##}s\n" +
                $"Locked Target: {lockedTargetLabel}\n" +
                $"Target Cycle Damage: {currentTargetCycleDamage}";

            Rect rect = new Rect(16f, 16f, 300f, 185f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, debugText);
        }

        #region Public Helper Methods for States

        /// <summary>
        /// 보스와 타겟 간 수평(XZ) 거리만 계산한다.
        /// </summary>
        public float GetPlanarDistanceToTarget()
        {
            if (playerTransform == null) return float.PositiveInfinity;
            return GetPlanarDistance(transform.position, playerTransform.position);
        }

        /// <summary>
        /// Basic 공격 사거리 판정을 위한 수평(XZ) 거리 계산.
        /// 기준점은 basicAttackRangeOrigin을 우선 사용하고, 미할당 시 Boss Root를 사용한다.
        /// </summary>
        public float GetPlanarDistanceFromBasicAttackOriginToTarget()
        {
            if (playerTransform == null) return float.PositiveInfinity;

            Vector3 origin;
            if (!TryResolveBasicAttackTelegraphPose(out origin, out _, Application.isPlaying))
            {
                origin = basicAttackRangeOrigin != null
                    ? basicAttackRangeOrigin.position
                    : transform.position;
            }

            return GetPlanarDistance(origin, playerTransform.position);
        }

        /// <summary>
        /// Basic bite가 현재 타겟을 입 전방 반구 안에서 맞출 수 있는지 판정한다.
        /// </summary>
        public bool IsTargetInsideBasicAttackArc()
        {
            if (playerTransform == null) return false;

            Vector3 attackOrigin;
            Vector3 attackForward;
            if (!TryResolveBasicAttackTelegraphPose(out attackOrigin, out attackForward, Application.isPlaying))
            {
                Transform forwardSource = ResolveBasicAttackSourceAnchor();
                attackOrigin = forwardSource.position;
                attackForward = forwardSource.forward;
            }

            float hitHalfAngle = basicAttackSettings != null
                ? basicAttackSettings.hitHalfAngle
                : BasicAttackSettings.DefaultHitHalfAngle;

            return IsInsideForwardArc(
                attackOrigin,
                attackForward,
                playerTransform.position,
                hitHalfAngle);
        }

        private static bool IsInsideForwardArc(
            Vector3 origin,
            Vector3 forward,
            Vector3 targetPosition,
            float halfAngle)
        {
            Vector3 toTarget = targetPosition - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            float minDot = Mathf.Cos(Mathf.Clamp(halfAngle, 0f, 180f) * Mathf.Deg2Rad);
            return Vector3.Dot(forward.normalized, toTarget.normalized) >= minDot;
        }

        public void ShowBasicAttackTelegraph(float warningDuration)
        {
            HitTraceLogger.Log(
                $"[HitTrace][BOOT][BossController][ShowBasicAttackTelegraph][ENTER] object={name} warning={warningDuration:F3} range={BasicAttackRange:F2}");
            if (BasicAttackRange <= 0f)
            {
                HitTraceLogger.Log("[HitTrace][BOOT][BossController][ShowBasicAttackTelegraph][FAIL] reason=BasicRangeNonPositive");
                HideBasicAttackTelegraph("ShowBasicAttackTelegraph.BasicRangeNonPositive", true);
                return;
            }

            if (!TryResolveBasicAttackTelegraph(out AttackWarningController telegraph))
            {
                HitTraceLogger.Log("[HitTrace][BOOT][BossController][ShowBasicAttackTelegraph][FAIL] reason=TelegraphResolveFailed");
                return;
            }

            if (!TryResolveBasicAttackTelegraphPose(out Vector3 telegraphPosition, out Vector3 telegraphForward))
            {
                HitTraceLogger.Log("[HitTrace][BOOT][BossController][ShowBasicAttackTelegraph][FAIL] reason=TelegraphPoseResolveFailed");
                return;
            }

            float sectorAngle = basicAttackSettings != null
                ? Mathf.Clamp(basicAttackSettings.hitHalfAngle * 2f, 0.1f, 360f)
                : 180f;
            telegraph.StartDamageSector(
                telegraphPosition,
                BasicAttackRange,
                Mathf.Max(0f, warningDuration),
                0f,
                sectorAngle,
                telegraphForward,
                CreateAttackWarningDamageSettings(attackDamage, BossAttackHitType.Attack1),
                AttackWarningController.DamageMode.OnceOnActivePhaseStart);
            HitTraceLogger.Log(
                $"[HitTrace][BOOT][BossController][ShowBasicAttackTelegraph][PASS] pos={telegraphPosition} forward={telegraphForward} " +
                $"sectorAngle={sectorAngle:F1}");
            EnqueueReplicatedAttackWarningShow(
                BossReplicatedWarningChannel.BasicAttack,
                BossReplicatedWarningShape.Sector,
                telegraphPosition,
                telegraphForward,
                warningDuration,
                0f,
                BasicAttackRange,
                0f,
                0f,
                sectorAngle);
        }

        public bool HideBasicAttackTelegraph(string reason = "Unspecified", bool forceImmediate = false)
        {
            if (_basicAttackTelegraph == null)
            {
                HitTraceLogger.Log(
                    $"[HitTrace][BOOT][BossController][HideBasicAttackTelegraph][SKIP] reason={reason} telegraph=null");
                EnqueueReplicatedAttackWarningHide(BossReplicatedWarningChannel.BasicAttack);
                return false;
            }

            if (!forceImmediate && _basicAttackTelegraph.IsRunning && !_basicAttackTelegraph.IsActivePhase)
            {
                HitTraceLogger.Log(
                    $"[HitTrace][BOOT][BossController][HideBasicAttackTelegraph][DEFER_PREACTIVE] reason={reason} " +
                    $"running={_basicAttackTelegraph.IsRunning} active={_basicAttackTelegraph.IsActivePhase}");
                return false;
            }

            HitTraceLogger.Log(
                $"[HitTrace][BOOT][BossController][HideBasicAttackTelegraph][CALL] reason={reason} " +
                $"running={_basicAttackTelegraph.IsRunning} active={_basicAttackTelegraph.IsActivePhase} force={forceImmediate}");
            _basicAttackTelegraph.ForceEnd($"BossController.Basic:{reason}");
            EnqueueReplicatedAttackWarningHide(BossReplicatedWarningChannel.BasicAttack);
            return true;
        }

        public bool TryEnterBasicAttackTelegraphActiveNow(string reason = "Unspecified")
        {
            if (_basicAttackTelegraph == null)
            {
                HitTraceLogger.Log(
                    $"[HitTrace][BOOT][BossController][TryEnterBasicAttackTelegraphActiveNow][SKIP] reason={reason} telegraph=null");
                return false;
            }

            bool entered = _basicAttackTelegraph.TryEnterActivePhaseNow($"BossController.Basic:{reason}");
            HitTraceLogger.Log(
                $"[HitTrace][BOOT][BossController][TryEnterBasicAttackTelegraphActiveNow][{(entered ? "PASS" : "SKIP")}] reason={reason} " +
                $"running={_basicAttackTelegraph.IsRunning} active={_basicAttackTelegraph.IsActivePhase}");
            return entered;
        }

        public void ShowLungeAttackTelegraph(float warningDuration, float activeDuration, int damage)
        {
            HitTraceLogger.Log(
                $"[HitTrace][BOOT][BossController][ShowLungeAttackTelegraph][ENTER] object={name} warning={warningDuration:F3} " +
                $"active={activeDuration:F3} damage={damage}");
            if (activeDuration <= 0f)
            {
                HitTraceLogger.Log("[HitTrace][BOOT][BossController][ShowLungeAttackTelegraph][FAIL] reason=ActiveDurationNonPositive");
                HideLungeAttackTelegraph("ShowLungeAttackTelegraph.ActiveDurationNonPositive", true);
                return;
            }

            if (!TryResolveLungeAttackTelegraph(out AttackWarningController telegraph))
            {
                HitTraceLogger.Log("[HitTrace][BOOT][BossController][ShowLungeAttackTelegraph][FAIL] reason=TelegraphResolveFailed");
                return;
            }

            Vector3 telegraphStart = ProjectPointToGround(transform.position);
            Vector3 telegraphForward = ResolveCurrentLungeForward();

            telegraph.StartDamageStrip(
                telegraphStart,
                ResolveConfiguredLungeTravelDistance(),
                ResolveConfiguredLungePathWidth(),
                Mathf.Max(0f, warningDuration),
                Mathf.Max(0f, activeDuration),
                telegraphForward,
                CreateAttackWarningDamageSettings(damage, BossAttackHitType.Attack2),
                AttackWarningController.DamageMode.ContinuousWhileActive);
            HitTraceLogger.Log(
                $"[HitTrace][BOOT][BossController][ShowLungeAttackTelegraph][PASS] start={telegraphStart} forward={telegraphForward} " +
                $"length={ResolveConfiguredLungeTravelDistance():F2} width={ResolveConfiguredLungePathWidth():F2}");
            EnqueueReplicatedAttackWarningShow(
                BossReplicatedWarningChannel.LungeAttack,
                BossReplicatedWarningShape.Strip,
                telegraphStart,
                telegraphForward,
                warningDuration,
                activeDuration,
                0f,
                ResolveConfiguredLungeTravelDistance(),
                ResolveConfiguredLungePathWidth(),
                0f);
        }

        public bool HideLungeAttackTelegraph(string reason = "Unspecified", bool forceImmediate = false)
        {
            if (_lungeAttackTelegraph == null)
            {
                HitTraceLogger.Log(
                    $"[HitTrace][BOOT][BossController][HideLungeAttackTelegraph][SKIP] reason={reason} telegraph=null");
                EnqueueReplicatedAttackWarningHide(BossReplicatedWarningChannel.LungeAttack);
                return false;
            }

            if (!forceImmediate && _lungeAttackTelegraph.IsRunning && !_lungeAttackTelegraph.IsActivePhase)
            {
                HitTraceLogger.Log(
                    $"[HitTrace][BOOT][BossController][HideLungeAttackTelegraph][DEFER_PREACTIVE] reason={reason} " +
                    $"running={_lungeAttackTelegraph.IsRunning} active={_lungeAttackTelegraph.IsActivePhase}");
                return false;
            }

            HitTraceLogger.Log(
                $"[HitTrace][BOOT][BossController][HideLungeAttackTelegraph][CALL] reason={reason} " +
                $"running={_lungeAttackTelegraph.IsRunning} active={_lungeAttackTelegraph.IsActivePhase} force={forceImmediate}");
            _lungeAttackTelegraph.ForceEnd($"BossController.Lunge:{reason}");
            EnqueueReplicatedAttackWarningHide(BossReplicatedWarningChannel.LungeAttack);
            return true;
        }

        public void PlayReplicatedAttackWarning(
            BossReplicatedWarningChannel warningChannel,
            BossReplicatedWarningShape warningShape,
            Vector3 startPosition,
            Vector3 forwardDirection,
            float warningDuration,
            float activeDuration,
            float radius,
            float length,
            float width,
            float sectorAngle)
        {
            if (!TryResolveReplicatedAttackWarningController(warningChannel, out AttackWarningController telegraph))
            {
                return;
            }

            switch (warningShape)
            {
                case BossReplicatedWarningShape.Strip:
                    telegraph.StartDamageStrip(
                        startPosition,
                        Mathf.Max(0.1f, length),
                        Mathf.Max(0.1f, width),
                        Mathf.Max(0f, warningDuration),
                        Mathf.Max(0f, activeDuration),
                        forwardDirection,
                        default,
                        AttackWarningController.DamageMode.None);
                    break;

                case BossReplicatedWarningShape.Sector:
                    telegraph.StartWarningSector(
                        startPosition,
                        Mathf.Max(0.1f, radius),
                        Mathf.Max(0f, warningDuration),
                        Mathf.Max(0f, activeDuration),
                        Mathf.Clamp(sectorAngle, 0.1f, AttackWarningController.FullSectorAngle),
                        forwardDirection,
                        false);
                    break;
            }
        }

        public void HideReplicatedAttackWarning(BossReplicatedWarningChannel warningChannel)
        {
            AttackWarningController telegraph = warningChannel switch
            {
                BossReplicatedWarningChannel.BasicAttack => _basicAttackTelegraph,
                BossReplicatedWarningChannel.LungeAttack => _lungeAttackTelegraph,
                _ => null
            };

            if (telegraph == null)
            {
                return;
            }

            telegraph.ForceEnd($"HideReplicatedAttackWarning:{warningChannel}");
        }

        public void HideReplicatedAttackWarnings()
        {
            HideReplicatedAttackWarning(BossReplicatedWarningChannel.BasicAttack);
            HideReplicatedAttackWarning(BossReplicatedWarningChannel.LungeAttack);
        }

        public void BeginConfiguredLungeTravel(float activeDuration)
        {
            float travelDistance = ResolveConfiguredLungeTravelDistance();
            if (activeDuration <= 0f || travelDistance <= 0f)
            {
                StopConfiguredLungeTravel();
                return;
            }

            _remainingLungeTravelDistance = travelDistance;
            _lungeTravelSpeed = travelDistance / activeDuration;
            _isLungeTravelActive = true;
        }

        public void UpdateConfiguredLungeTravel()
        {
            if (!_isLungeTravelActive || _characterController == null)
            {
                return;
            }

            float stepDistance = _lungeTravelSpeed * Time.deltaTime;
            if (stepDistance <= 0f)
            {
                return;
            }

            stepDistance = Mathf.Min(stepDistance, _remainingLungeTravelDistance);
            _remainingLungeTravelDistance -= stepDistance;
            _characterController.Move(ResolveCurrentLungeForward() * stepDistance);

            if (_remainingLungeTravelDistance <= 0.0001f)
            {
                StopConfiguredLungeTravel();
            }
        }

        public void StopConfiguredLungeTravel()
        {
            _isLungeTravelActive = false;
            _remainingLungeTravelDistance = 0f;
            _lungeTravelSpeed = 0f;
        }

        private bool TryResolveReplicatedAttackWarningController(
            BossReplicatedWarningChannel warningChannel,
            out AttackWarningController telegraph)
        {
            switch (warningChannel)
            {
                case BossReplicatedWarningChannel.BasicAttack:
                    return TryResolveBasicAttackTelegraph(out telegraph);

                case BossReplicatedWarningChannel.LungeAttack:
                    return TryResolveLungeAttackTelegraph(out telegraph);

                default:
                    telegraph = null;
                    return false;
            }
        }

        private bool TryResolveBasicAttackTelegraphPose(
            out Vector3 telegraphPosition,
            out Vector3 telegraphForward,
            bool preferSampledEndPose = true)
        {
            Transform sourceAnchor = ResolveBasicAttackSourceAnchor();

            telegraphPosition = sourceAnchor.position;
            telegraphForward = sourceAnchor.forward;

            if (preferSampledEndPose
                && animator != null
                && animator.TryGetBasicAttackEndPose(sourceAnchor, out Vector3 sampledPosition, out Vector3 sampledForward))
            {
                telegraphPosition = sampledPosition;
                telegraphForward = sampledForward;
            }

            telegraphPosition = ProjectPointToGround(telegraphPosition);
            telegraphForward.y = 0f;
            if (telegraphForward.sqrMagnitude <= 0.0001f)
            {
                telegraphForward = transform.forward;
                telegraphForward.y = 0f;
            }

            if (telegraphForward.sqrMagnitude <= 0.0001f)
            {
                telegraphForward = Vector3.forward;
            }
            else
            {
                telegraphForward.Normalize();
            }

            return true;
        }

        private Transform ResolveBasicAttackSourceAnchor()
        {
            if (basicAttackRangeOrigin != null)
            {
                return basicAttackRangeOrigin;
            }

            if (_headDamageCaster != null && _headDamageCaster.CastCenter != null)
            {
                return _headDamageCaster.CastCenter;
            }

            return transform;
        }

        private Vector3 ProjectPointToGround(Vector3 worldPosition)
        {
            float fallbackOffset = aoeAttackSettings != null ? aoeAttackSettings.groundOffset : 0f;
            if (aoeAttackSettings == null)
            {
                worldPosition.y = transform.position.y + fallbackOffset;
                return worldPosition;
            }

            Vector3 rayOrigin = worldPosition + Vector3.up * Mathf.Max(0.1f, aoeAttackSettings.groundRayHeight);
            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    Mathf.Max(0.1f, aoeAttackSettings.groundRayDistance),
                    aoeAttackSettings.groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                worldPosition = hit.point;
                worldPosition.y += aoeAttackSettings.groundOffset;
                return worldPosition;
            }

            worldPosition.y = transform.position.y + fallbackOffset;
            return worldPosition;
        }

        private AttackWarningController.DamageSettings CreateAttackWarningDamageSettings(
            int damage,
            BossAttackHitType hitType)
        {
            AttackWarningController.DamageSettings settings = default;
            settings.damage = Mathf.Max(0, damage);
            settings.targetMask = ~0;
            settings.ownerInstanceId = gameObject.GetInstanceID();
            settings.bossAttackHitType = hitType;
            settings.maxTargets = 16;
            settings.queryHeight = 4f;
            return settings;
        }

        private float ResolveConfiguredLungeTravelDistance()
        {
            if (lungeAttackSettings == null || lungeAttackSettings.travelDistance <= 0f)
            {
                return Mathf.Max(0.1f, lungeAttackRange);
            }

            return Mathf.Max(0.1f, lungeAttackSettings.travelDistance);
        }

        private float ResolveConfiguredLungePathWidth()
        {
            if (lungeAttackSettings == null || lungeAttackSettings.pathWidth <= 0f)
            {
                return 1f;
            }

            return Mathf.Max(0.1f, lungeAttackSettings.pathWidth);
        }

        private Vector3 ResolveCurrentLungeForward()
        {
            Vector3 forward = _isLungeTravelDirectionLocked ? _lungeTravelDirection : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            return forward.normalized;
        }

        /// <summary>
        /// 타겟이 감지 반경 안에 있는지 수평(XZ) 거리 기준으로 판정한다.
        /// </summary>
        public bool IsTargetInDetectionRange()
        {
            if (playerTransform == null) return false;
            return GetPlanarDistanceToTarget() <= detectionRange;
        }

        /// <summary>
        /// 보스 피격 시 현재 aggro cycle의 피해 기여도를 기록한다.
        /// 첫 피격이 들어오면 aggro time을 시작하고, 타이머가 끝날 때까지 누적 피해를 모은다.
        /// </summary>
        public void RegisterAggroContribution(GameObject dealer, int damage)
        {
            if (dealer == null || damage <= 0) return;
            if (_health == null || _health.IsDead || !_health.HasRuntimeWriteAuthority) return;

            PlayerController dealerController = dealer.GetComponent<PlayerController>();
            if (dealerController == null)
            {
                dealerController = dealer.GetComponentInParent<PlayerController>();
            }

            if (dealerController == null) return;

            if (!_isAggroTimerRunning)
            {
                BeginAggroTimer();
            }

            RegisterAggroDamage(dealerController.transform, damage);
        }

        /// <summary>
        /// 현재 씬의 살아있는 플레이어를 기준으로 타겟을 다시 선택한다.
        /// 기본 규칙은 가장 가까운 플레이어 우선이고, aggro time 종료 후에는 피해 기여도 winner를 잠깐 고정한다.
        /// </summary>
        public void RefreshClosestLiveTarget(bool force = false)
        {
            if (!ShouldRefreshClosestLiveTarget(force))
            {
                return;
            }

            PlayerController[] playerControllers = FindObjectsByType<PlayerController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            AggroTargetScanResult targetScan = ScanLiveTargets(playerControllers);
            Transform bestTarget = ResolveTargetByAggro(targetScan);

            ApplyTargetIfChanged(bestTarget);
        }

        private bool ShouldRefreshClosestLiveTarget(bool force)
        {
            if (!force && Time.time < _nextClosestLiveTargetRefreshTime)
            {
                return false;
            }

            _nextClosestLiveTargetRefreshTime = Time.time + ClosestLiveTargetRefreshInterval;
            return true;
        }

        private Transform ResolveTargetByAggro(AggroTargetScanResult targetScan)
        {
            Transform lockedTarget = TryGetValidLockedAggroTarget();
            if (lockedTarget != null)
            {
                return lockedTarget;
            }

            Transform heldTarget = TryGetHeldCurrentAggroTarget();
            if (heldTarget != null)
            {
                return heldTarget;
            }

            return targetScan.ClosestTarget;
        }

        private Transform TryGetValidLockedAggroTarget()
        {
            if (_lockedAggroTarget == null)
            {
                return null;
            }

            if (IsLiveTarget(_lockedAggroTarget) && IsTargetWithinAggroPriorityRange(_lockedAggroTarget))
            {
                return _lockedAggroTarget;
            }

            _lockedAggroTarget = null;
            return null;
        }

        private Transform TryGetHeldCurrentAggroTarget()
        {
            // 어그로 타이머가 돌고 있거나 공격 직후라도, 현재 타겟이 우선 원 안에 있으면 그 타겟을 유지한다.
            return IsCurrentTargetWithinAggroPriorityRange() ? playerTransform : null;
        }

        private void ApplyTargetIfChanged(Transform nextTarget)
        {
            if (nextTarget != playerTransform)
            {
                SetTarget(nextTarget);
            }
        }

        private AggroTargetScanResult ScanLiveTargets(PlayerController[] playerControllers)
        {
            Transform closestTarget = null;
            float closestDistanceSqr = float.PositiveInfinity;
            int aggroCandidateCount = 0;
            float aggroPriorityRangeSqr = AggroPriorityRange * AggroPriorityRange;

            for (int i = 0; i < playerControllers.Length; i++)
            {
                PlayerController playerController = playerControllers[i];
                if (playerController == null)
                {
                    continue;
                }

                Health playerHealth = playerController.GetComponent<Health>();
                if (playerHealth != null && playerHealth.IsDead)
                {
                    continue;
                }

                Transform candidate = playerController.transform;
                float planarDistanceSqr = GetPlanarDistanceSqr(transform.position, candidate.position);
                if (planarDistanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = planarDistanceSqr;
                    closestTarget = candidate;
                }

                if (aggroCandidateCount < MaxAggroCandidateCount
                    && planarDistanceSqr <= aggroPriorityRangeSqr)
                {
                    _aggroCandidateBuffer[aggroCandidateCount] = candidate;
                    aggroCandidateCount++;
                }
            }

            return new AggroTargetScanResult(closestTarget, aggroCandidateCount);
        }

        private void UpdateAggroTimer()
        {
            if (!_isAggroTimerRunning)
            {
                return;
            }

            // 공격/페이즈 전환 연출 중에는 타이머를 멈춰 타겟 고정 변경이 애니메이션을 끊지 않게 한다.
            if (ShouldPauseAggroTimer())
            {
                return;
            }

            _aggroTimerRemaining -= Time.deltaTime;
            if (_aggroTimerRemaining > 0f)
            {
                return;
            }

            LockAggroTargetFromCurrentCycle();
        }

        private bool ShouldPauseAggroTimer()
        {
            if (_phaseIntroPlaying)
            {
                return true;
            }

            if (_stateMachine == null)
            {
                return false;
            }

            return _stateMachine.CurrentState == AttackState;
        }

        private void BeginAggroTimer()
        {
            ClearAggroContributions();
            _lockedAggroTarget = null;
            _isAggroTimerRunning = true;
            _aggroTimerRemaining = Mathf.Max(0f, aggroTime);
        }

        private void LockAggroTargetFromCurrentCycle()
        {
            _isAggroTimerRunning = false;
            _aggroTimerRemaining = 0f;
            _lockedAggroTarget = ResolveAggroDamageWinner();
            ClearAggroContributions();

            // 타이머 종료 시 clear winner가 있으면 다음 refresh를 기다리지 않고 즉시 타겟을 넘긴다.
            if (_lockedAggroTarget != null && _lockedAggroTarget != playerTransform)
            {
                SetTarget(_lockedAggroTarget);
            }
        }

        private Transform ResolveAggroDamageWinner()
        {
            PlayerController[] playerControllers = FindObjectsByType<PlayerController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            AggroTargetScanResult targetScan = ScanLiveTargets(playerControllers);
            if (targetScan.AggroCandidateCount < 2)
            {
                return null;
            }

            int aggroCandidateCount = targetScan.AggroCandidateCount;
            Transform bestDealer = null;
            int bestDamage = 0;
            bool hasTie = false;

            for (int i = 0; i < aggroCandidateCount; i++)
            {
                Transform candidate = _aggroCandidateBuffer[i];
                int dealtDamage = ResolveAggroContributionDamage(candidate);
                if (dealtDamage > bestDamage)
                {
                    bestDamage = dealtDamage;
                    bestDealer = candidate;
                    hasTie = false;
                }
                else if (dealtDamage == bestDamage && dealtDamage > 0)
                {
                    hasTie = true;
                }
            }

            if (bestDamage <= 0 || hasTie)
            {
                return null;
            }

            return bestDealer;
        }

        private void RegisterAggroDamage(Transform dealer, int damage)
        {
            int contributorIndex = FindAggroContributorIndex(dealer);
            if (contributorIndex >= 0)
            {
                _aggroContributorDamageBuffer[contributorIndex] += damage;
                return;
            }

            if (_aggroContributorCount >= MaxAggroCandidateCount)
            {
                return;
            }

            _aggroContributorBuffer[_aggroContributorCount] = dealer;
            _aggroContributorDamageBuffer[_aggroContributorCount] = damage;
            _aggroContributorCount++;
        }

        private int FindAggroContributorIndex(Transform dealer)
        {
            if (dealer == null)
            {
                return -1;
            }

            for (int i = 0; i < _aggroContributorCount; i++)
            {
                if (_aggroContributorBuffer[i] == dealer)
                {
                    return i;
                }
            }

            return -1;
        }

        private int ResolveAggroContributionDamage(Transform target)
        {
            int contributorIndex = FindAggroContributorIndex(target);
            if (contributorIndex < 0)
            {
                return 0;
            }

            return _aggroContributorDamageBuffer[contributorIndex];
        }

        private void ClearAggroContributions()
        {
            for (int i = 0; i < _aggroContributorCount; i++)
            {
                _aggroContributorBuffer[i] = null;
                _aggroContributorDamageBuffer[i] = 0;
            }

            _aggroContributorCount = 0;
        }

        private void ResetAggroState()
        {
            _isAggroTimerRunning = false;
            _aggroTimerRemaining = 0f;
            _lockedAggroTarget = null;
            ClearAggroContributions();
        }

        private bool IsCurrentTargetWithinAggroPriorityRange()
        {
            return IsTargetWithinAggroPriorityRange(playerTransform);
        }

        private bool IsTargetWithinAggroPriorityRange(Transform target)
        {
            if (!IsLiveTarget(target))
            {
                return false;
            }

            float aggroPriorityRangeSqr = AggroPriorityRange * AggroPriorityRange;
            return GetPlanarDistanceSqr(transform.position, target.position) <= aggroPriorityRangeSqr;
        }

        private static bool IsLiveTarget(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            PlayerController playerController = target.GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = target.GetComponentInParent<PlayerController>();
            }

            if (playerController == null)
            {
                return false;
            }

            Health playerHealth = playerController.GetComponent<Health>();
            return playerHealth == null || !playerHealth.IsDead;
        }

        /// <summary>
        /// Y축을 제외한 수평 거리 계산 유틸리티.
        /// </summary>
        public static float GetPlanarDistance(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }

        public void MoveTo(Vector3 targetPosition, float speed)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                _characterController.Move(direction * speed * Time.deltaTime);

                if (enableRotation)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }

                if (animator)
                {
                    if (_suppressLocomotionVisual)
                    {
                        // 공중 연출 중에는 Locomotion 진입을 막고 속도 파라미터만 정지 상태로 유지한다.
                        animator.SetSpeed(0f);
                    }
                    else
                    {
                        animator.PlayMove();
                        animator.SetSpeed(speed);
                    }
                }
            }
        }

        public void RotateTowards(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0;
            if (enableRotation && direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// 타겟을 향해 즉시 회전한다. (공격 시작 프레임 정렬용)
        /// </summary>
        public void RotateTowardsImmediate(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (!enableRotation || direction.sqrMagnitude <= 0.000001f) return;
            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        /// <summary>
        /// Lunge 시작 시 이동 방향을 고정한다.
        /// </summary>
        public void BeginLungeTravelDirectionLock(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            SetLungeTravelDirectionLock(direction);
        }

        /// <summary>
        /// 현재 보스가 바라보는 방향으로 Lunge 이동 방향을 고정한다.
        /// </summary>
        public void BeginLungeTravelDirectionLockFromCurrentForward()
        {
            SetLungeTravelDirectionLock(transform.forward);
        }

        private void SetLungeTravelDirectionLock(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = transform.forward;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.000001f)
            {
                _isLungeTravelDirectionLocked = false;
                return;
            }

            _lungeTravelDirection = direction.normalized;
            _isLungeTravelDirectionLocked = true;
        }

        /// <summary>
        /// Lunge 고정 이동 방향을 해제한다.
        /// </summary>
        public void EndLungeTravelDirectionLock()
        {
            _isLungeTravelDirectionLocked = false;
        }

        /// <summary>
        /// 애니메이션 변경 없이 물리 이동만 수행합니다.
        /// 공격 패턴 등 자체 애니메이션이 있는 상태에서 사용합니다.
        /// </summary>
        public void MoveRaw(Vector3 direction, float speed)
        {
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                _characterController.Move(direction.normalized * speed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Lunge 루트 모션 델타를 보스 루트(CharacterController)에 적용한다.
        /// </summary>
        public void ApplyLungeRootMotion(Vector3 deltaPosition)
        {
            if (_characterController == null) return;

            deltaPosition.y = 0f;
            float deltaMagnitude = deltaPosition.magnitude;
            if (deltaMagnitude <= LungeRootMotionMinStep) return;

            if (_isLungeTravelDirectionLocked)
            {
                _characterController.Move(_lungeTravelDirection * deltaMagnitude);
                return;
            }

            _characterController.Move(deltaPosition);
        }

        public void StopMoving()
        {
            if (animator)
            {
                animator.SetSpeed(0f);
                if (!_suppressLocomotionVisual)
                {
                    animator.PlayIdle();
                }
            }
        }

        /// <summary>
        /// 공중 공격 연출 중 지상 이동 애니메이션(Locomotion) 오염을 방지한다.
        /// </summary>
        public void SetLocomotionVisualSuppressed(bool suppressed)
        {
            _suppressLocomotionVisual = suppressed;
            if (animator && suppressed)
            {
                animator.SetSpeed(0f);
            }
        }

        public void StartAttackCooldown()
        {
            _nextAttackTime = Time.time + attackCooldown;
        }

        private void TryApplyPlayerCollisionIgnore()
        {
            if (!ignorePlayerCollision) return;
            if (_characterController == null) return;
            if (playerTransform == null) return;

            int playerId = playerTransform.gameObject.GetInstanceID();
            if (_hasAppliedPlayerCollisionIgnore && _ignoredPlayerRootInstanceId == playerId) return;

            Collider[] playerColliders = playerTransform.GetComponentsInChildren<Collider>(true);
            bool applied = false;
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider == null) continue;
                Physics.IgnoreCollision(_characterController, playerCollider, true);
                applied = true;
            }

            _hasAppliedPlayerCollisionIgnore = applied;
            _ignoredPlayerRootInstanceId = playerId;
        }

        #endregion

        [Header("Physics Settings")]
        private float _verticalVelocity;

        private void ApplyGravity()
        {
            if (_characterController.isGrounded && _verticalVelocity < 0)
            {
                _verticalVelocity = -2f; // 지면에 붙어있게 하는 힘
            }
            else
            {
                // 중력 가속도 적용 (Physics.gravity.y 사용)
                _verticalVelocity += Physics.gravity.y * Time.deltaTime;
            }

            Vector3 gravityMove = Vector3.up * _verticalVelocity * Time.deltaTime;
            _characterController.Move(gravityMove);
        }

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 basicOrigin;
            Vector3 basicForward;
            if (!TryResolveBasicAttackTelegraphPose(out basicOrigin, out basicForward, Application.isPlaying))
            {
                Transform sourceAnchor = ResolveBasicAttackSourceAnchor();
                basicOrigin = sourceAnchor.position;
                basicForward = sourceAnchor.forward;
            }

            DrawWireSectorGizmo(
                basicOrigin,
                basicForward,
                BasicAttackRange,
                basicAttackSettings != null ? basicAttackSettings.hitHalfAngle : BasicAttackSettings.DefaultHitHalfAngle);

            Gizmos.color = new Color(1f, 0.55f, 0f);
            DrawWireStripGizmo(
                transform.position,
                ResolveCurrentLungeForward(),
                ResolveConfiguredLungeTravelDistance(),
                ResolveConfiguredLungePathWidth());

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, sharedRangedAttackRange);

            Gizmos.color = new Color(1f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, AggroPriorityRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }

        private static void DrawWireSectorGizmo(Vector3 origin, Vector3 forward, float radius, float halfAngle)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f || radius <= 0f)
            {
                return;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            int segmentCount = 24;
            float startAngle = -Mathf.Clamp(halfAngle, 0f, 180f);
            float endAngle = Mathf.Clamp(halfAngle, 0f, 180f);
            Vector3 previousPoint = origin + ResolvePlanarDirection(forward, right, startAngle) * radius;
            Gizmos.DrawLine(origin, previousPoint);

            for (int i = 1; i <= segmentCount; i++)
            {
                float t = i / (float)segmentCount;
                float angle = Mathf.Lerp(startAngle, endAngle, t);
                Vector3 nextPoint = origin + ResolvePlanarDirection(forward, right, angle) * radius;
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }

            Gizmos.DrawLine(origin, previousPoint);
        }

        private static Vector3 ResolvePlanarDirection(Vector3 forward, Vector3 right, float angleDegrees)
        {
            float angle = angleDegrees * Mathf.Deg2Rad;
            return (forward * Mathf.Cos(angle)) + (right * Mathf.Sin(angle));
        }

        private static void DrawWireStripGizmo(Vector3 start, Vector3 forward, float length, float width)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f || length <= 0f || width <= 0f)
            {
                return;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized * (width * 0.5f);
            Vector3 end = start + (forward * length);

            Vector3 a = start - right;
            Vector3 b = start + right;
            Vector3 c = end + right;
            Vector3 d = end - right;

            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }

        #endregion

        [System.Serializable]
        public class BasicAttackSettings
        {
            public const float DefaultHitHalfAngle = 90f;
            public const float DefaultTelegraphHideNormalizedTime = 0.75f;

            [Tooltip("How long the selected ready slice should take in seconds")]
            public float readyDuration = 0.2f;

            [MinMaxRange(0f, 1f)]
            [Tooltip("Attack1 ready slice in normalized time (x = start, y = end)")]
            public Vector2 readyNormalizedWindow = new Vector2(0.15f, 0.45f);

            [Range(0f, 180f)]
            [Tooltip("Attack1 front hit arc half-angle in degrees (90 = 180-degree bite)")]
            public float hitHalfAngle = DefaultHitHalfAngle;

            [Range(0f, 1f)]
            [Tooltip("Attack1 warning half-circle hide timing in normalized time")]
            public float telegraphHideNormalizedTime = DefaultTelegraphHideNormalizedTime;

            public void ClampValues()
            {
                if (readyDuration < 0f) readyDuration = 0f;

                readyNormalizedWindow.x = Mathf.Clamp01(readyNormalizedWindow.x);
                readyNormalizedWindow.y = Mathf.Clamp(readyNormalizedWindow.y, readyNormalizedWindow.x, 1f);
                hitHalfAngle = Mathf.Clamp(hitHalfAngle, 0f, 180f);
                telegraphHideNormalizedTime = Mathf.Clamp01(telegraphHideNormalizedTime);
            }
        }

        [System.Serializable]
        public class LungeAttackSettings
        {
            [Tooltip("기본 공격력 대비 배수")]
            public float damageMultiplier = 1.5f;

            [MinMaxRange(0f, 1f)]
            [Tooltip("Attack2 판정 활성 normalized window (x = start, y = end)")]
            public Vector2 damageCastNormalizedWindow = new Vector2(0.15f, 0.8f);

            [Min(0.1f)]
            [Tooltip("Lunge 고정 이동 거리")]
            public float travelDistance = 4.5f;

            [Min(0.1f)]
            [Tooltip("Lunge 직선 경고/판정의 전체 너비")]
            public float pathWidth = 2.2f;

            public void ClampValues()
            {
                if (damageMultiplier < 0f) damageMultiplier = 0f;

                damageCastNormalizedWindow.x = Mathf.Clamp01(damageCastNormalizedWindow.x);
                damageCastNormalizedWindow.y = Mathf.Clamp(damageCastNormalizedWindow.y, damageCastNormalizedWindow.x, 1f);
                travelDistance = Mathf.Max(0.1f, travelDistance);
                pathWidth = Mathf.Max(0.1f, pathWidth);
            }
        }

        [System.Serializable]
        public class ProjectileAttackSettings
        {
            [FormerlySerializedAs("telegraphDuration")]
            [Tooltip("경고 시간(초)")]
            public float warningDuration = 0.3f;
            [Tooltip("투사체 데미지")]
            public int damage = 12;
            [Tooltip("투사체 속도")]
            public float speed = 12f;
            [Tooltip("투사체 수명(초)")]
            public float lifetime = 3f;
            [Tooltip("한 번의 패턴에서 발사할 개수")]
            public int volleyCount = 3;
            [Tooltip("발사 간격(초)")]
            public float volleyInterval = 0.08f;
            [Tooltip("유도 강도 (0 = 직진, 1 = 강한 유도)")]
            [Range(0f, 1f)]
            public float homingStrength = 0.25f;
            [Tooltip("유도 지속 시간(초). 0이면 유도 비활성화")]
            public float homingDuration = 1.2f;
            [Tooltip("Y축 추적 속도 (0이면 발사 높이 유지)")]
            public float verticalFollowSpeed = 4f;
            [Tooltip("발사 종료 후 상태 복귀 전 최소 대기 시간(초)")]
            public float postFireRecoveryDuration = 0.12f;
            [Tooltip("공격 애니메이션 종료 판정 normalizedTime")]
            [Range(0.5f, 1.2f)]
            public float exitNormalizedTime = 0.9f;
        }

        [System.Serializable]
        public class AoEAttackSettings
        {
            [Header("Runtime References")]
            [Tooltip("장판 프리팹 (AoECircleController 포함)")]
            public AoECircleController circlePrefab;
            [Tooltip("장판 인스턴스가 생성될 부모 Transform")]
            public Transform circleRoot;

            [Header("Pattern Timing")]
            [Tooltip("이륙 연출 시간")]
            public float takeOffDuration = 0.35f;
            [Tooltip("전진 비행 연출 시간")]
            public float flyForwardDuration = 0.35f;
            [Tooltip("전진 비행 중 추적 속도")]
            public float flyForwardSpeed = 6.0f;
            [Tooltip("이 거리 이하로 들어오면 FlyIdle 캐스팅 시작")]
            public float castRange = 3.0f;
            [Tooltip("착지 연출 시간")]
            public float landDuration = 0.4f;
            [Tooltip("장판 생성 간격")]
            public float spawnInterval = 0.1f;

            [Header("AoE Damage")]
            public int damage = 10;
            [FormerlySerializedAs("telegraphDuration")]
            [Tooltip("경고 시간 (fire 착지/장판 발동 동기화 시간)")]
            public float warningDuration = 0.9f;
            [Tooltip("장판 활성 유지 시간")]
            public float activeDuration = 0.9f;
            [Tooltip("틱 데미지 간격")]
            public float tickInterval = 0.25f;
            [Tooltip("AoE 데미지 대상 레이어")]
            public LayerMask targetMask = ~0;

            [Header("AoE Spawn Area")]
            [Tooltip("한 번의 패턴에서 생성할 장판 개수")]
            public int circleCount = 3;
            [Tooltip("장판 최대 동시 인스턴스 수")]
            public int maxCircleInstances = 12;
            [Tooltip("장판 반경")]
            public float radius = 2.5f;
            [Tooltip("타겟 주변 랜덤 생성 반경")]
            public float spawnSpreadRadius = 4.5f;
            [Tooltip("타겟 진행 방향 예측 시간(초)")]
            public float headingLeadTime = 0.35f;
            [Tooltip("예측 오프셋 최대 거리")]
            public float maxHeadingLeadDistance = 6f;
            [Tooltip("진행 방향 전방 확산 반경")]
            public float forwardSpreadRadius = 6f;
            [Tooltip("진행 방향 측면 확산 반경")]
            public float sideSpreadRadius = 3.5f;
            [Tooltip("전방 편향 강도 (0 = 균등, 1 = 전방 집중)")]
            [Range(0f, 1f)]
            public float headingBias = 0.7f;
            [Tooltip("예측 적용 최소 속도")]
            public float headingMinSpeed = 0.1f;
            [Tooltip("지면 투영 Ray 시작 높이")]
            public float groundRayHeight = 15f;
            [Tooltip("지면 투영 Ray 최대 거리")]
            public float groundRayDistance = 40f;
            [Tooltip("장판 Y 오프셋")]
            public float groundOffset = 0.05f;
            [Tooltip("지면 판정 레이어")]
            public LayerMask groundMask = ~0;

            [Header("Projectile Sync")]
            [Tooltip("SpawnPoint 미할당/저지대일 때 보정할 발사 높이")]
            public float fallbackProjectileHeight = 6f;
        }
    }
}
