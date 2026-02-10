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
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float searchingMoveSpeed = 2.0f;
        [SerializeField] private float rotationSpeed = 5.0f;

        [Header("감지 설정 (Detection Settings)")]
        [SerializeField] private float detectionRange = 10.0f;
        [SerializeField] private float attackRange = 2.5f;
        [SerializeField] private float searchDuration = 5.0f;
        [SerializeField] private LayerMask obstacleMask;

        [Header("공격 설정 (Attack Settings)")]
        [SerializeField] private int attackDamage = 20;
        [SerializeField] private float attackDuration = 1.0f;
        [SerializeField] private float attackCooldown = 2.0f;
        [SerializeField] private DamageCaster _damageCaster;

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
        public DamageCaster DamageCaster => _damageCaster;

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

                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                if (animator) animator.SetSpeed(speed);
            }
        }

        public void RotateTowards(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        public void StopMoving()
        {
            if (animator) animator.SetSpeed(0f);
        }

        public void StartAttackCooldown()
        {
            _nextAttackTime = Time.time + attackCooldown;
        }

        #endregion

        private void ApplyGravity()
        {
            if (!_characterController.isGrounded)
            {
                _characterController.Move(Vector3.up * -9.81f * Time.deltaTime);
            }
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
    }
}
