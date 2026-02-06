using BossRaid.Combat;
using BossRaid.Patterns;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour, IDashContext, IAttackable
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 6.0f;
    [SerializeField] private float rotationSpeed = 10.0f; // Increased from 1.0f for snappier rotation
    [SerializeField] private float gravity = -9.81f;

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    [Header("Camera")]
    [SerializeField] private Transform cameraRoot;

    [Header("Dash Settings")]
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashSpeedMultiplier = 3.0f;
    [SerializeField] private float dashCooldown = 1.0f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 8.0f;
    [SerializeField] private float airControl = 0.5f;

    [Header("Attack Settings")]
    [SerializeField] private AttackComboData[] attackCombos;
    [SerializeField] private DamageCaster _damageCaster;

    // Animation Constants
    public const string ANIM_PARAM_SPEED = "Speed";
    public const string ANIM_STATE_LOCOMOTION = "Locomotion";
    public const string ANIM_STATE_DASH = "Quickshift_F";
    public const string ANIM_STATE_ATTACK1 = "Attack1";
    public const string ANIM_STATE_ATTACK2 = "Attack2";
    public const string ANIM_STATE_ATTACK3 = "Attack3";
    public const string ANIM_STATE_JUMP = "Jump";

    // [Editor Only] 컴포넌트 추가하거나 Reset 누르면 자동 호출됨
    private void Reset()
    {
        // 더미 데이터 자동 생성
        attackCombos = new AttackComboData[3];

        // 1타: 빠르고 약함
        attackCombos[0] = new AttackComboData
        {
            damage = 10f,
            duration = 0.5f,
            comboInputWindow = 0.3f, // 0.3초 안에 누르면 다음 콤보 예약
            cancelStartTime = 0.3f   // 0.3초 후 대시 캔슬 가능
        };

        // 2타: 중간
        attackCombos[1] = new AttackComboData
        {
            damage = 15f,
            duration = 0.6f,
            comboInputWindow = 0.4f,
            cancelStartTime = 0.4f
        };

        // 3타: 느리고 강함 (피니시)
        attackCombos[2] = new AttackComboData
        {
            damage = 30f,
            duration = 1.0f,
            comboInputWindow = 0.0f, // 마지막이라 콤보 없음
            cancelStartTime = 0.6f
        };
    }

    private IInputProvider _inputProvider;
    private CharacterController _characterController;
    private float _nextDashTime;

    // FSM
    private StateMachine _stateMachine;
    public MoveState MoveState { get; private set; }
    public DashState DashState { get; private set; }
    public JumpState JumpState { get; private set; }
    public AttackState AttackState { get; private set; }

    // Public Properties for States
    public float MoveSpeed => moveSpeed;
    public float RotationSpeed => rotationSpeed;
    public float Gravity => gravity;
    public Transform CameraRoot => cameraRoot;
    public Animator Animator => _animator;
    public CharacterController CharController => _characterController;
    public StateMachine StateMachine => _stateMachine;

    // Dash Properties
    public float DashDuration => dashDuration;
    public float DashSpeedMultiplier => dashSpeedMultiplier;
    public bool CanDash => Time.time >= _nextDashTime;

    // Jump Properties
    public float JumpForce => jumpForce;
    public float AirControl => airControl;

    // Attack Properties
    public AttackComboData[] AttackCombos => attackCombos;
    public float CurrentAttackDamage { get; set; } // AttackState에서 설정함

    // Animation Event Callbacks
    public void OnHitStart()
    {
        if (_damageCaster != null)
        {
            _damageCaster.EnableHitbox((int)CurrentAttackDamage);
        }
    }

    public void OnHitEnd()
    {
        if (_damageCaster != null)
        {
            _damageCaster.DisableHitbox();
        }
    }

    public void StartDashCooldown()
    {
        _nextDashTime = Time.time + dashCooldown;
    }

    // [Helper] 중력 적용 및 이동 (State에서 호출)
    public void ApplyGravity(float verticalVelocity)
    {
        Vector3 gravityMove = Vector3.up * verticalVelocity * Time.deltaTime;
        _characterController.Move(gravityMove);
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _inputProvider = GetComponent<IInputProvider>();

        // Init FSM
        _stateMachine = new StateMachine();
        MoveState = new MoveState(this);
        DashState = new DashState(this, this);
        JumpState = new JumpState(this);
        AttackState = new AttackState(this);
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

        // FSM Update
        _stateMachine.Update(input);
    }
}
