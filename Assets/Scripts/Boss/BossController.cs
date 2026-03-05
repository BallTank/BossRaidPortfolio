using Core.Boss.AoE;
using Core.Boss.Attacks;
using Core.Boss.Projectiles;
using Core.Combat;
using Core.Common;
using Core.Common.Patterns;
using UnityEngine;
using UnityEngine.Serialization;

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
        [SerializeField] private float detectionRange = 10.0f;

        [Header("패턴별 공격 사거리 (Pattern Attack Ranges)")]
        [FormerlySerializedAs("attackRange")]
        [SerializeField] private float basicAttackRange = 2.5f;
        [SerializeField] private float lungeAttackRange = 4.5f;
        [SerializeField] private float projectileAttackRange = 6.0f;
        [SerializeField] private float aoeAttackRange = 6.0f;

        [SerializeField, Tooltip("공격 사거리 경계 지터 완화를 위한 추적 재진입 여유 거리")]
        private float chaseReengageBuffer = 1.0f;
        [SerializeField] private float searchDuration = 5.0f;

        [Header("공격 설정 (Attack Settings)")]
        [SerializeField] private int attackDamage = 20;
        [SerializeField] private float attackDuration = 1.0f;
        [SerializeField] private float attackCooldown = 2.0f;

        [Header("부위별 DamageCaster (Explicit per-part)")]
        [Tooltip("Basic Attack(물기) 판정용 - Head Bone에 부착")]
        [SerializeField] private DamageCaster _headDamageCaster;
        [Tooltip("Lunge Attack(도약) 판정용 - 앞발 Bone에 부착 (미설정 시 Head 사용)")]
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
        [SerializeField, Tooltip("Lunge 루트 모션 디버그 로그 출력 여부")]
        private bool enableLungeRootMotionDebugLog = false;
        [SerializeField, Range(0.01f, 0.5f), Tooltip("Lunge 루트 모션 디버그 로그 출력 간격(초)")]
        private float lungeRootMotionDebugLogInterval = 0.05f;
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
        private float _nextAttackTime;

        // Phase Flow
        private BossPhase _currentPhase = BossPhase.Phase1;
        private bool _phaseOneIntroCompleted;
        private bool _phaseTwoIntroCompleted;
        private bool _phaseTwoTriggered;
        private bool _phaseIntroPlaying;
        private float _phaseIntroEndTime;
        private bool _suppressLocomotionVisual;
        private float _nextLungeRootMotionDebugLogTime;
        private Vector3 _lungeTravelDirection = Vector3.forward;
        private bool _isLungeTravelDirectionLocked;
        private bool _hasAppliedPlayerCollisionIgnore;
        private int _ignoredPlayerRootInstanceId;
        private const float LungeRootMotionMinStep = 0.0001f;

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
            if (basicAttackRange < 0f) basicAttackRange = 0f;
            if (lungeAttackRange < 0f) lungeAttackRange = 0f;
            if (projectileAttackRange < 0f) projectileAttackRange = 0f;
            if (aoeAttackRange < 0f) aoeAttackRange = 0f;
            if (chaseReengageBuffer < 0f) chaseReengageBuffer = 0f;
            if (lungeRootMotionDebugLogInterval < 0.01f) lungeRootMotionDebugLogInterval = 0.01f;

            SyncBasicAttackRangeToHeadDamageCaster();

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
        public float BasicAttackRange => basicAttackRange;
        public float LungeAttackRange => lungeAttackRange;
        public float ProjectileAttackRange => projectileAttackRange;
        public float AoEAttackRange => aoeAttackRange;
        public float ChaseReengageBuffer => chaseReengageBuffer;
        public float SearchDuration => searchDuration;
        public int AttackDamage => attackDamage;
        public float AttackDuration => attackDuration;
        public bool CanAttack => Time.time >= _nextAttackTime;
        public DamageCaster HeadDamageCaster => _headDamageCaster;
        public DamageCaster LungeDamageCaster => _lungeDamageCaster;

        public bool EnableChase => enableChase;
        public bool EnableBasicAttack => enableBasicAttack;
        public bool EnableLungeAttack => enableLungeAttack;
        public bool EnableProjectileAttack => enableProjectileAttack;
        public bool EnableAoEAttack => enableAoEAttack;
        public bool EnableLungeRootMotionDebugLog => enableLungeRootMotionDebugLog;
        public float LungeRootMotionDebugLogInterval => Mathf.Max(0.01f, lungeRootMotionDebugLogInterval);
        public BossProjectilePool ProjectilePool => projectilePool;
        public Transform ProjectileSpawnPoint => projectileSpawnPoint;
        public BossPhase CurrentPhase => _currentPhase;
        public bool IsPhaseIntroPlaying => _phaseIntroPlaying;
        public bool IsPhaseOneAttackWindow => _currentPhase == BossPhase.Phase1 && _phaseOneIntroCompleted && !_phaseIntroPlaying;
        public bool IsPhaseTwoAttackWindow => _currentPhase == BossPhase.Phase2 && _phaseTwoIntroCompleted && !_phaseIntroPlaying;
        public bool IsLocomotionVisualSuppressed => _suppressLocomotionVisual;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            ResolveDamageBlinkEffect();
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

            if (_health != null)
            {
                _health.OnDamageTaken += HandleDamage;
                _health.OnDeath += HandleDeath;
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnDamageTaken -= HandleDamage;
                _health.OnDeath -= HandleDeath;
            }
        }

        private void Start()
        {
            // DamageCaster에 Owner 설정 (자해 방지)
            if (_headDamageCaster != null)
            {
                _headDamageCaster.SetOwner(gameObject);
                _headDamageCaster.SetBossAttackHitType(BossAttackHitType.Attack1);
            }

            if (_lungeDamageCaster != null)
            {
                _lungeDamageCaster.SetOwner(gameObject);
                _lungeDamageCaster.SetBossAttackHitType(BossAttackHitType.Attack2);
            }

            SyncBasicAttackRangeToHeadDamageCaster();
            TryApplyPlayerCollisionIgnore();
            damageBlinkEffect?.StopBlink();
            _stateMachine.ChangeState(IdleState);
        }

        private void Update()
        {
            TryApplyPlayerCollisionIgnore();
            ApplyGravity();
            UpdatePhaseFlow();

            // Controller에서 직접 Update 호출
            _stateMachine.CurrentState?.Update();
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
            string debugText =
                $"Boss Phase: {_currentPhase}\n" +
                $"HP: {healthRatio * 100f:0.#}%\n" +
                $"Intro Playing: {_phaseIntroPlaying}\n" +
                $"Phase2 Triggered: {_phaseTwoTriggered}";

            Rect rect = new Rect(16f, 16f, 260f, 90f);
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

            Vector3 origin = basicAttackRangeOrigin != null
                ? basicAttackRangeOrigin.position
                : transform.position;

            return GetPlanarDistance(origin, playerTransform.position);
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
        /// Y축을 제외한 수평 거리 계산 유틸리티.
        /// </summary>
        public static float GetPlanarDistance(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            delta.y = 0f;
            return delta.magnitude;
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

        /// <summary>
        /// Lunge 루트 모션 디버그 로그의 샘플링 타이머를 초기화한다.
        /// </summary>
        public void ResetLungeRootMotionDebugLogWindow()
        {
            _nextLungeRootMotionDebugLogTime = 0f;
        }

        /// <summary>
        /// Lunge 루트 모션 디버그 로그 출력 시점인지 판정한다.
        /// </summary>
        public bool ShouldEmitLungeRootMotionDebugLog()
        {
            if (!enableLungeRootMotionDebugLog) return false;

            float interval = Mathf.Max(0.01f, lungeRootMotionDebugLogInterval);
            if (Time.time < _nextLungeRootMotionDebugLogTime) return false;

            _nextLungeRootMotionDebugLogTime = Time.time + interval;
            return true;
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

        /// <summary>
        /// Basic 공격 사거리와 Head DamageCaster 반경을 동일하게 유지한다.
        /// </summary>
        private void SyncBasicAttackRangeToHeadDamageCaster()
        {
            if (_headDamageCaster == null) return;
            _headDamageCaster.SetRadius(basicAttackRange);
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
            Vector3 basicOrigin = basicAttackRangeOrigin != null
                ? basicAttackRangeOrigin.position
                : transform.position;
            Gizmos.DrawWireSphere(basicOrigin, basicAttackRange);

            Gizmos.color = new Color(1f, 0.55f, 0f);
            Gizmos.DrawWireSphere(transform.position, lungeAttackRange);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, projectileAttackRange);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, aoeAttackRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }

        #endregion

        [System.Serializable]
        public class LungeAttackSettings
        {
            [Tooltip("기본 공격력 대비 배수")]
            public float damageMultiplier = 1.5f;
        }

        [System.Serializable]
        public class ProjectileAttackSettings
        {
            [Tooltip("예고 시간(초)")]
            public float telegraphDuration = 0.3f;
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
            [Tooltip("fire 착지/장판 발동 동기화 시간. 0 이하면 telegraphDuration 사용")]
            public float impactSyncTime = 0f;

            [Header("AoE Damage")]
            public int damage = 10;
            [Tooltip("텔레그래프 시간 (impactSyncTime 미설정 시 사용)")]
            public float telegraphDuration = 0.9f;
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
