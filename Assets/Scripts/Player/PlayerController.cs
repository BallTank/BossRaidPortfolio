using Core.Combat;
using Core.Common;
using Core.Common.Interfaces;
using Core.Common.Patterns;
using Core.Player;
using Core.Player.States;
using Core.UI;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour, IDashContext, IAttackable, IBossAttackHitReceiver
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 6.0f;
    [SerializeField] private float rotationSpeed = 10.0f;

    [Header("Camera")]
    [SerializeField] private Transform cameraRoot;

    [Header("Visual")]
    [SerializeField] private PlayerVisual playerVisual;
    [SerializeField] private BlinkWhiteEffect blinkWhiteEffect;

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

    [Header("Stun Settings")]
    [SerializeField] private float stunDuration = 0.5f;
    [FormerlySerializedAs("invincibilityDuration")]
    [SerializeField] private float postStunInvulDuration = 2.0f;
    [SerializeField] private float pushbackDuration = 0.7f;
    [SerializeField] private float projectileCountTimer = 0.5f;

    [Header("HUD Settings")]
    [SerializeField] private CombatHUDController _combatHUD;
    [SerializeField] private Health _bossHealthForHUD;
    [SerializeField] private string _playerDisplayName = "Player";
    [SerializeField] private string _bossDisplayName = "Dragon";

    // Animation Constants
    public const string ANIM_PARAM_SPEED = "Speed";
    public const string ANIM_STATE_LOCOMOTION = "Locomotion";
    public const string ANIM_STATE_DASH = "Quickshift_F";
    public const string ANIM_STATE_ATTACK1 = "Attack1";
    public const string ANIM_STATE_ATTACK2 = "Attack2";
    public const string ANIM_STATE_ATTACK3 = "Attack3";
    public const string ANIM_STATE_JUMP = "Jump";
    public const string ANIM_STATE_HIT = "Hit";
    public const string ANIM_STATE_STUN = "Stun";
    public const string ANIM_STATE_DIE = "Die";

    // FSM (제네릭 StateMachine 사용)
    private StateMachine<PlayerBaseState> _stateMachine;
    public MoveState MoveState { get; private set; }
    public DashState DashState { get; private set; }
    public JumpState JumpState { get; private set; }
    public AttackState AttackState { get; private set; }
    public HitState HitState { get; private set; }
    public StunState StunState { get; private set; }
    public DeadState DeadState { get; private set; }

    // Components
    private Health _health;
    private IInputProvider _inputProvider;
    private CharacterController _characterController;
    private float _nextDashTime;

    // Stun / Invul Runtime
    private bool _isStunned;
    private bool _isPostStunInvulnerable;
    private float _postStunInvulTimer;
    private int _projectileHitCount;
    private float _projectileCountTimerLeft;
    private bool _suppressDamageTakenReaction;

    // Public Properties for States
    public float MoveSpeed => moveSpeed;
    public float RotationSpeed => rotationSpeed;
    public float Gravity => Physics.gravity.y;
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
    public AttackComboData[] AttackCombos => attackCombos;
    public float CurrentAttackDamage { get; set; }

    private void OnValidate()
    {
        if (moveSpeed < 0f) moveSpeed = 0f;
        if (rotationSpeed < 0f) rotationSpeed = 0f;
        if (dashDuration < 0f) dashDuration = 0f;
        if (dashSpeedMultiplier < 0f) dashSpeedMultiplier = 0f;
        if (dashCooldown < 0f) dashCooldown = 0f;
        if (jumpForce < 0f) jumpForce = 0f;
        if (airControl < 0f) airControl = 0f;
        if (stunDuration < 0f) stunDuration = 0f;
        if (postStunInvulDuration < 0f) postStunInvulDuration = 0f;
        if (pushbackDuration < 0f) pushbackDuration = 0f;
        if (projectileCountTimer < 0f) projectileCountTimer = 0f;
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _inputProvider = GetComponent<IInputProvider>();
        _health = GetComponent<Health>();
        ResolveBlinkEffect();

        if (_health != null)
        {
            _health.OnDamageTaken += HandleDamage;
            _health.OnDeath += HandleDeath;
        }

        if (_damageCaster != null)
        {
            _damageCaster.SetOwner(gameObject);
            _damageCaster.ForceDisableHitbox();
            _damageCaster.OnAttackWindowResolved += HandleAttackWindowResolved;
        }

        // FSM 초기화 (제네릭 StateMachine)
        _stateMachine = new StateMachine<PlayerBaseState>();
        MoveState = new MoveState(this);
        DashState = new DashState(this, this);
        JumpState = new JumpState(this);
        AttackState = new AttackState(this);
        HitState = new HitState(this);
        StunState = new StunState(this);
        DeadState = new DeadState(this);
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnDamageTaken -= HandleDamage;
            _health.OnDeath -= HandleDeath;
        }

        if (_damageCaster != null)
        {
            _damageCaster.OnAttackWindowResolved -= HandleAttackWindowResolved;
        }
    }

    private void Start()
    {
        _stateMachine.ChangeState(MoveState);
        _damageCaster?.ForceDisableHitbox();
        blinkWhiteEffect?.StopBlink();
        UpdateHealthInvincibilityByState();
        InitializeCombatHUD();
    }

    private void Update()
    {
        UpdateProjectileHitCountTimer();
        UpdatePostStunInvulnerability();

        if (_inputProvider == null) return;

        PlayerInputPacket input = _inputProvider.GetInput();
        if (cameraRoot != null)
        {
            cameraRoot.rotation = Quaternion.Euler(input.lookPitch, input.lookYaw, 0f);
        }

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
        if (_health == null || _health.IsDead) return;
        if (_suppressDamageTakenReaction) return;
        if (_isStunned || _isPostStunInvulnerable) return;

        _stateMachine.ChangeState(HitState);
    }

    private void HandleDeath()
    {
        _isStunned = false;
        _isPostStunInvulnerable = false;
        _postStunInvulTimer = 0f;
        ResetProjectileHitCounter();

        blinkWhiteEffect?.StopBlink();
        UpdateHealthInvincibilityByState();
        _stateMachine.ChangeState(DeadState);
    }

    public BossAttackHitResolution ReceiveBossAttackHit(in BossAttackHitData hitData)
    {
        if (_health == null || _health.IsDead)
        {
            return BossAttackHitResolution.Ignored;
        }

        if (_isStunned || _isPostStunInvulnerable)
        {
            return BossAttackHitResolution.Ignored;
        }

        switch (hitData.HitType)
        {
            case BossAttackHitType.Attack1:
                return ApplyNormalDamageAndHitReaction(hitData.Damage)
                    ? BossAttackHitResolution.Damaged
                    : BossAttackHitResolution.Ignored;

            case BossAttackHitType.Attack2:
                BeginStun(hitData.ForceDirection);
                return BossAttackHitResolution.StunOnly;

            case BossAttackHitType.Attack3Projectile:
            case BossAttackHitType.Attack4Projectile:
                return HandleProjectileHit(hitData);
        }

        return ApplyNormalDamageAndHitReaction(hitData.Damage)
            ? BossAttackHitResolution.Damaged
            : BossAttackHitResolution.Ignored;
    }

    public void HandleStunFinished()
    {
        if (!_isStunned) return;

        _isStunned = false;
        _stateMachine.ChangeState(MoveState);
        StartPostStunInvulnerability();
    }

    private BossAttackHitResolution HandleProjectileHit(in BossAttackHitData hitData)
    {
        if (_projectileCountTimerLeft <= 0f)
        {
            _projectileHitCount = 1;
            _projectileCountTimerLeft = projectileCountTimer;

            return ApplyNormalDamageAndHitReaction(hitData.Damage)
                ? BossAttackHitResolution.Damaged
                : BossAttackHitResolution.Ignored;
        }

        _projectileHitCount += 1;
        if (_projectileHitCount >= 2)
        {
            BeginStun(hitData.ForceDirection);
            ResetProjectileHitCounter();
            return BossAttackHitResolution.StunOnly;
        }

        return BossAttackHitResolution.Ignored;
    }

    private bool ApplyNormalDamageAndHitReaction(int damage)
    {
        if (_health == null || _health.IsDead) return false;
        if (damage <= 0) return false;

        int previousHp = _health.CurrentHealth;
        _suppressDamageTakenReaction = true;
        _health.TakeDamage(damage);
        _suppressDamageTakenReaction = false;

        bool didDamage = _health.CurrentHealth < previousHp;
        if (didDamage && !_health.IsDead)
        {
            _stateMachine.ChangeState(HitState);
        }

        return didDamage;
    }

    private void BeginStun(Vector3 forceDirection)
    {
        _isStunned = true;
        _isPostStunInvulnerable = false;
        _postStunInvulTimer = 0f;
        blinkWhiteEffect?.StopBlink();
        UpdateHealthInvincibilityByState();
        ResetProjectileHitCounter();

        OnHitEnd();

        Vector3 planarForceDirection = forceDirection;
        planarForceDirection.y = 0f;
        if (planarForceDirection.sqrMagnitude <= 0.0001f)
        {
            planarForceDirection = -transform.forward;
        }

        float configuredPushbackDuration = Mathf.Max(0f, pushbackDuration);
        float dashSpeed = moveSpeed * dashSpeedMultiplier;
        float pushDistance = dashSpeed * configuredPushbackDuration;

        StunState.Configure(
            stunDuration,
            planarForceDirection,
            pushDistance,
            configuredPushbackDuration);
        _stateMachine.ChangeState(StunState);
    }

    private void StartPostStunInvulnerability()
    {
        _isPostStunInvulnerable = true;
        _postStunInvulTimer = postStunInvulDuration;
        blinkWhiteEffect?.PlayBlink(postStunInvulDuration);
        UpdateHealthInvincibilityByState();
    }

    private void EndPostStunInvulnerability()
    {
        _isPostStunInvulnerable = false;
        _postStunInvulTimer = 0f;
        blinkWhiteEffect?.StopBlink();
        UpdateHealthInvincibilityByState();
    }

    private void UpdatePostStunInvulnerability()
    {
        if (!_isPostStunInvulnerable) return;

        _postStunInvulTimer -= Time.deltaTime;

        if (_postStunInvulTimer <= 0f)
        {
            EndPostStunInvulnerability();
        }
    }

    private void ResolveBlinkEffect()
    {
        if (blinkWhiteEffect != null) return;

        if (playerVisual != null)
        {
            blinkWhiteEffect = playerVisual.GetComponent<BlinkWhiteEffect>();
            if (blinkWhiteEffect == null)
            {
                blinkWhiteEffect = playerVisual.GetComponentInChildren<BlinkWhiteEffect>(true);
            }
        }

        if (blinkWhiteEffect == null)
        {
            blinkWhiteEffect = GetComponent<BlinkWhiteEffect>();
        }
    }

    private void UpdateProjectileHitCountTimer()
    {
        if (_projectileCountTimerLeft <= 0f) return;

        _projectileCountTimerLeft -= Time.deltaTime;
        if (_projectileCountTimerLeft <= 0f)
        {
            ResetProjectileHitCounter();
        }
    }

    private void UpdateHealthInvincibilityByState()
    {
        if (_health == null) return;
        _health.SetInvincible(_isStunned || _isPostStunInvulnerable);
    }

    private void ResetProjectileHitCounter()
    {
        _projectileHitCount = 0;
        _projectileCountTimerLeft = 0f;
    }

    private void HandleAttackWindowResolved(bool isHit, int totalDamage)
    {
        _combatHUD?.ShowDamageFeedback(isHit, totalDamage);
    }

    private void InitializeCombatHUD()
    {
        if (_combatHUD == null)
        {
            _combatHUD = FindObjectOfType<CombatHUDController>();
            if (_combatHUD == null) return;
        }

        if (_bossHealthForHUD == null)
        {
            Core.Boss.BossController bossController = FindObjectOfType<Core.Boss.BossController>();
            if (bossController != null)
            {
                _bossHealthForHUD = bossController.GetComponent<Health>();
            }
        }

        _combatHUD.Initialize(_health, _bossHealthForHUD);
        _combatHUD.SetPlayerName(_playerDisplayName);
        _combatHUD.SetBossName(_bossDisplayName);
    }

    // Animation Event Callbacks
    public void OnHitStart()
    {
        if (_damageCaster == null) return;
        if (_stateMachine == null || _stateMachine.CurrentState != AttackState) return;

        int damage = Mathf.RoundToInt(CurrentAttackDamage);
        if (damage <= 0) return;

        _damageCaster.EnableHitbox(damage);
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
