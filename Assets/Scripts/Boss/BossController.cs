using Core.Boss.Attacks;
using Core.Combat;
using Core.Common.Patterns;
using UnityEngine;

namespace Core.Boss
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class BossController : MonoBehaviour
    {
        [Header("참조 (References)")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private BossVisual animator;

        [Header("스탯 (Stats)")]
        [SerializeField] private float moveSpeed = 3.5f; // 애니메이션 Walk 임계값과 동기화
        [SerializeField] private float searchingMoveSpeed = 2.0f;
        [SerializeField] private float rotationSpeed = 5.0f;

        /// <summary>
        /// Unity 에디터에서 스크립트가 로드되거나 인스펙터의 값이 변경될 때 호출되어 데이터의 유효성을 검사합니다.
        /// </summary>
        private void OnValidate()
        {
            // Ensure moveSpeed is not negative
            if (moveSpeed < 0) moveSpeed = 0f;
            if (searchingMoveSpeed < 0) searchingMoveSpeed = 0f;
        }

        [Header("감지 설정 (Detection Settings)")]
        [SerializeField] private float detectionRange = 10.0f;
        [SerializeField] private float attackRange = 2.5f;
        [SerializeField] private float searchDuration = 5.0f;
        [SerializeField] private LayerMask obstacleMask;

        [Header("공격 설정 (Attack Settings)")]
        [SerializeField] private int attackDamage = 20;
        [SerializeField] private float attackDuration = 1.0f;
        [SerializeField] private float attackCooldown = 2.0f;

        [Header("부위별 DamageCaster (Explicit per-part)")]
        [Tooltip("Basic Attack(물기) 판정용 — Head Bone에 부착")]
        [SerializeField] private DamageCaster _headDamageCaster;
        [Tooltip("Claw Attack(할퀴기) 판정용 — 앞발 Bone에 부착 (미설정 시 Head 사용)")]
        [SerializeField] private DamageCaster _clawDamageCaster;

        [Header("Claw Attack Settings")]
        [SerializeField] private ClawAttackSettings clawAttackSettings;

        [Header("디버그 설정 (Debug Settings)")]
        [SerializeField] private bool enableChase = true;
        [SerializeField] private bool enableRotation = true;
        [SerializeField] private bool enableBasicAttack = true;
        [SerializeField] private bool enableClawAttack = true;

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
        public ClawAttackPattern ClawAttackPattern { get; private set; }

        // Components
        private CharacterController _characterController;
        private Health _health;
        private float _nextAttackTime;

        // Public Properties for States
        public Transform Target => playerTransform;
        public BossVisual Visual => animator;
        public float MoveSpeed => moveSpeed;
        public float SearchingMoveSpeed => searchingMoveSpeed;
        public float DetectionRange => detectionRange;
        public float AttackRange => attackRange;
        public float SearchDuration => searchDuration;
        public int AttackDamage => attackDamage;
        public float AttackDuration => attackDuration;
        public bool CanAttack => Time.time >= _nextAttackTime;
        public DamageCaster HeadDamageCaster => _headDamageCaster;
        public DamageCaster ClawDamageCaster => _clawDamageCaster;

        public bool EnableChase => enableChase;
        public bool EnableBasicAttack => enableBasicAttack;
        public bool EnableClawAttack => enableClawAttack;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _health = GetComponent<Health>();

            // 플레이어가 할당되지 않았다면 자동으로 찾음
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }

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
            ClawAttackPattern = new ClawAttackPattern(clawAttackSettings);

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
            if (_headDamageCaster != null) _headDamageCaster.SetOwner(gameObject);
            if (_clawDamageCaster != null) _clawDamageCaster.SetOwner(gameObject);

            _stateMachine.ChangeState(IdleState);
        }

        private void Update()
        {
            ApplyGravity();
            // Controller에서 직접 Update 호출
            _stateMachine.CurrentState?.Update();
        }

        private void HandleDamage(int damage)
        {
            // 이미 죽었으면 반응 안 함
            if (_health.IsDead) return;

            // FSM을 통해 Hit 상태로 전환 (강제 인터럽트)
            _stateMachine.ChangeState(HitState);
        }

        private void HandleDeath()
        {
            _stateMachine.ChangeState(DeadState);
        }

        #region Public Helper Methods for States

        public bool CheckLineOfSight()
        {
            if (playerTransform == null) return false;

            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Vector3 target = playerTransform.position + Vector3.up * 1.0f;
            Vector3 direction = (target - origin).normalized;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, detectionRange, ~LayerMask.GetMask("Ignore Raycast")))
            {
                if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform))
                {
                    return true;
                }

                if (((1 << hit.collider.gameObject.layer) & obstacleMask) != 0)
                {
                    return false;
                }
            }
            return false;
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
                    animator.PlayMove();
                    animator.SetSpeed(speed);
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

        public void StopMoving()
        {
            if (animator)
            {
                animator.PlayIdle();
                animator.SetSpeed(0f);
            }
        }

        public void StartAttackCooldown()
        {
            _nextAttackTime = Time.time + attackCooldown;
        }

        #endregion

        [Header("Physics Settings")]
        // [SerializeField] private float gravity = -9.81f; // Removed in favor of Physics.gravity.y
        private float _verticalVelocity;

        private void ApplyGravity()
        {
            if (_characterController.isGrounded && _verticalVelocity < 0)
            {
                _verticalVelocity = -2f; // 지면에 붙어있게 하는 힘 (Ground sticking force)
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
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            if (playerTransform != null)
            {
                Vector3 origin = transform.position + Vector3.up * 1.5f;
                Vector3 target = playerTransform.position + Vector3.up * 1.0f;
                bool hasLineOfSight = !Physics.Linecast(origin, target, obstacleMask);

                Gizmos.color = hasLineOfSight ? Color.green : Color.red;
                Gizmos.DrawLine(origin, target);
            }
        }

        #endregion
        [System.Serializable]
        public class ClawAttackSettings
        {
            [Tooltip("기본 공격력 대비 배수")]
            public float damageMultiplier = 1.5f;
            [Tooltip("돌진 속도")]
            public float rushSpeed = 10.0f;
            [Tooltip("애니메이션 진행률 기준 돌진 구간 (0~1). 0.3 = 전체 애니메이션의 30% 시점까지 돌진")]
            [Range(0f, 1f)]
            public float rushPhaseRatio = 0.3f;
            [Tooltip("애니메이션 종료 시점 (0~1). 0.5 = 도약 동작만 재생하고 복귀 모션 생략")]
            [Range(0.1f, 1f)]
            public float exitPhaseRatio = 0.5f;
        }
    }
}
