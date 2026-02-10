using System.Collections;
using Core.Combat;
using Core.Common.Interfaces;
using Core.Common.Patterns;
using Core.Player;
using Core.Player.States;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour, IDashContext, IAttackable
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 6.0f;
    [SerializeField] private float rotationSpeed = 10.0f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Camera")]
    [SerializeField] private Transform cameraRoot;

    [Header("Visual")]
    [SerializeField] private PlayerVisual playerVisual;

    [Header("Dash Settings")]
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashSpeedMultiplier = 3.0f;
    [SerializeField] private float dashCooldown = 1.0f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 8.0f;
    [SerializeField] private float airControl = 0.5f;

    [Header("Attack Settings")]
    [SerializeField] private Core.Player.AttackComboData[] attackCombos;
    [SerializeField] private Core.Combat.DamageCaster _damageCaster;

    [Header("Combat Settings")]
    [SerializeField] private float invincibilityDuration = 1.0f;

    // Animation Constants
    public const string ANIM_PARAM_SPEED = "Speed";
    public const string ANIM_STATE_LOCOMOTION = "Locomotion";
    public const string ANIM_STATE_DASH = "Quickshift_F";
    public const string ANIM_STATE_ATTACK1 = "Attack1";
    public const string ANIM_STATE_ATTACK2 = "Attack2";
    public const string ANIM_STATE_ATTACK3 = "Attack3";
    public const string ANIM_STATE_JUMP = "Jump";
    public const string ANIM_STATE_DIE = "Die";

    // FSM (제네릭 StateMachine 사용)
    private StateMachine<PlayerBaseState> _stateMachine;
    public MoveState MoveState { get; private set; }
    public DashState DashState { get; private set; }
    public JumpState JumpState { get; private set; }
    public AttackState AttackState { get; private set; }
    public HitState HitState { get; private set; }
    public DeadState DeadState { get; private set; }

    // Components
    private Health _health;
    private IInputProvider _inputProvider;
    private CharacterController _characterController;
    private float _nextDashTime;

    // Public Properties for States
    public float MoveSpeed => moveSpeed;
    public float RotationSpeed => rotationSpeed;
    public float Gravity => gravity;
    public Transform CameraRoot => cameraRoot;
    public IInputProvider InputProvider => _inputProvider;
    public PlayerVisual Visual => playerVisual;
    public Animator Animator => playerVisual?.Animator;
    public CharacterController CharController => _characterController;
    public StateMachine<PlayerBaseState> StateMachine => _stateMachine;

    // Dash Properties
    public float DashDuration => dashDuration;
    public float DashSpeedMultiplier => dashSpeedMultiplier;
    public bool CanDash => Time.time >= _nextDashTime;

    // Jump Properties
    public float JumpForce => jumpForce;
    public float AirControl => airControl;

    // Attack Properties
    public Core.Player.AttackComboData[] AttackCombos => attackCombos;
    public float CurrentAttackDamage { get; set; }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _inputProvider = GetComponent<IInputProvider>();
        _health = GetComponent<Health>();

        if (_health != null)
        {
            _health.OnDamageTaken += HandleDamage;
            _health.OnDeath += HandleDeath;
        }

        // FSM 초기화 (제네릭 StateMachine)
        _stateMachine = new StateMachine<PlayerBaseState>();
        MoveState = new MoveState(this);
        DashState = new DashState(this, this);
        JumpState = new JumpState(this);
        AttackState = new AttackState(this);
        HitState = new HitState(this);
        DeadState = new DeadState(this);
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
        _stateMachine.ChangeState(MoveState);
    }

    private void Update()
    {
        if (_inputProvider == null) return;

        PlayerInputPacket input = _inputProvider.GetInput();
        cameraRoot.rotation = Quaternion.Euler(input.lookPitch, input.lookYaw, 0f);

        // Controller에서 직접 Update 호출
        _stateMachine.CurrentState?.Update(input);
    }

    /// <summary>
    /// 입력 벡터를 카메라 기준으로 변환하여 이동 방향 계산
    /// </summary>
    public Vector3 GetMovementDirection(Vector2 inputDir)
    {
        Vector3 camForward = cameraRoot.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = cameraRoot.right;
        camRight.y = 0;
        camRight.Normalize();

        return (camForward * inputDir.y + camRight * inputDir.x).normalized;
    }

    private void HandleDamage(int damage)
    {
        if (_health.IsDead) return;

        // 무적 처리 (스턴 상태 중복 방지 및 후속타 무시)
        StartCoroutine(InvincibilityRoutine());

        _stateMachine.ChangeState(HitState);
    }

    private IEnumerator InvincibilityRoutine()
    {
        if (_health != null)
        {
            _health.SetInvincible(true);
            yield return new WaitForSeconds(invincibilityDuration);
            _health.SetInvincible(false);
        }
    }

    private void HandleDeath()
    {
        // 진행 중인 코루틴 정리 (InvincibilityRoutine 등)
        StopAllCoroutines();
        _stateMachine.ChangeState(DeadState);
    }

    // Animation Event Callbacks
    public void OnHitStart()
    {
        if (_damageCaster != null) _damageCaster.EnableHitbox((int)CurrentAttackDamage);
    }

    public void OnHitEnd()
    {
        if (_damageCaster != null) _damageCaster.DisableHitbox();
    }

    public void StartDashCooldown()
    {
        _nextDashTime = Time.time + dashCooldown;
    }

    public void ApplyGravity(float verticalVelocity)
    {
        Vector3 gravityMove = Vector3.up * verticalVelocity * Time.deltaTime;
        _characterController.Move(gravityMove);
    }

    private void Reset()
    {
        attackCombos = new AttackComboData[3];
        attackCombos[0] = new AttackComboData { damage = 10f, duration = 0.5f, comboInputWindow = 0.3f, cancelStartTime = 0.3f };
        attackCombos[1] = new AttackComboData { damage = 15f, duration = 0.6f, comboInputWindow = 0.4f, cancelStartTime = 0.4f };
        attackCombos[2] = new AttackComboData { damage = 30f, duration = 1.0f, comboInputWindow = 0.0f, cancelStartTime = 0.6f };
    }
}
