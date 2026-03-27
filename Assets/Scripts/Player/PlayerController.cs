using Core.Combat;
using Core.Common;
using Core.Common.Interfaces;
using Core.Common.Patterns;
using Core.Multiplayer;
using Core.Player;
using Core.Player.States;
using Core.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour, IDashContext, IAttackable, IBossAttackHitReceiver
{
    private const float PredictedPresentationTransitionSnapAngle = 35f;
    private const float PredictedPresentationTickBoundaryHeadStartFraction = 0.05f;

    public enum RuntimeSimulationMode
    {
        Full,
        LookOnly,
        Disabled,
        PredictedLocomotion,
        AuthoritativeLocomotion
    }

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

    [Header("Runtime Multiplayer")]
    [SerializeField] private RuntimeSimulationMode _simulationMode = RuntimeSimulationMode.Full;
    [SerializeField] private bool _isLocalPresentationEnabled = true;
    [SerializeField] private bool _driveCameraRootFromLookInput;

    [Header("Multiplayer Predicted Render Tuning")]
    [SerializeField, HideInInspector] private float _multiplayerPresentationSmoothTime = 0.06f;
    [SerializeField, HideInInspector] private float _multiplayerPresentationSnapDistance = 0.75f;
    [SerializeField, HideInInspector] private float _multiplayerPresentationMovingCatchUpDistance = 0.2f;
    [SerializeField, HideInInspector] private float _multiplayerPresentationMovingCatchUpSmoothTime = 0.025f;
    [SerializeField, HideInInspector] private float _multiplayerPredictedRenderSmoothTime = 0.0167f;
    [SerializeField, HideInInspector] private float _multiplayerPredictedRenderSnapDistance = 0.35f;
    [SerializeField, HideInInspector] private float _multiplayerCameraFollowSmoothTime = 0.05f;
    [SerializeField, HideInInspector] private float _multiplayerCameraFollowSnapDistance = 0.9f;
    [SerializeField, HideInInspector] private float _multiplayerPresentationRotationSnapAngle = 50f;

    [Header("Multiplayer Locomotion Motor")]
    [SerializeField, HideInInspector] private LayerMask _multiplayerLocomotionCollisionMask;
    [SerializeField, HideInInspector] private float _multiplayerLocomotionCollisionShell = 0.02f;
    [SerializeField, HideInInspector] private float _multiplayerLocomotionGroundSnapDistance = 0.15f;
    [SerializeField, HideInInspector] private int _multiplayerLocomotionMaxSlideIterations = 1;

    [Header("Attack2 Debug")]
    [SerializeField] private bool enableAttack2DebugLog = true;
    [SerializeField] private float attack2DebugTraceDuration = 0.8f;
    [SerializeField, Range(0.01f, 0.5f)] private float attack2DebugLogInterval = 0.05f;
    [SerializeField] private float attack2DebugNearDistance = 2.5f;

    [Header("Multiplayer Presentation Debug")]
    [SerializeField, HideInInspector] private bool enableMultiplayerPresentationTrace = false;
    [SerializeField, HideInInspector, Range(0.02f, 0.5f)] private float multiplayerPresentationTraceLogInterval = 0.08f;
    [SerializeField, HideInInspector] private float multiplayerPresentationTraceOffsetThreshold = 0.03f;

    [Header("Multiplayer Predicted Render Debug")]
    [SerializeField, HideInInspector] private bool enableMultiplayerPredictedRenderTrace = true;
    [SerializeField, HideInInspector] private bool multiplayerPredictedRenderTraceLateralOnly = true;
    [SerializeField, HideInInspector, Range(0.02f, 0.5f)] private float multiplayerPredictedRenderTraceLogInterval = 0.08f;
    [SerializeField, HideInInspector] private float multiplayerPredictedRenderTraceOffsetThreshold = 0.01f;

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
    private const float NetworkLocomotionGroundedGravity = -2.0f;

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
    private float _latestLookYaw;
    private float _latestLookPitch;
    private BossAttackHitType _activeStunSourceHitType;
    private float _attack2DebugTraceTimer;
    private float _nextAttack2DebugLogTime;
    private float _nextAttack2StunDebugLogTime;
    private bool _wasAttack2NearLastFrame;
    private Core.Boss.BossController _attack2DebugBoss;
    private int _pendingComboHudStep;
    private Vector3 _presentationDefaultLocalPosition;
    private Quaternion _presentationDefaultLocalRotation = Quaternion.identity;
    private bool _hasPresentationDefaultTransform;
    private Vector3 _presentationWorldPosition;
    private Vector3 _presentationWorldVelocity;
    private bool _hasPresentationWorldPosition;
    private Vector3 _predictedPresentationPreviousTargetPosition;
    private Vector3 _predictedPresentationCurrentTargetPosition;
    private float _predictedPresentationTargetSetTime;
    private bool _hasPredictedPresentationTargets;
    private Vector2 _lastPredictedPresentationMoveInput;
    private bool _hasLastPredictedPresentationMoveInput;
    private Vector3 _cameraFollowWorldPosition;
    private Vector3 _cameraFollowWorldVelocity;
    private bool _hasCameraFollowWorldPosition;
    private bool _wasLookOnlyMoveActive;
    private float _nextMultiplayerPresentationTraceLogTime;
    private float _nextMultiplayerPredictedRenderTraceLogTime;

    // Public Properties for States
    public float MoveSpeed => moveSpeed;
    public float RotationSpeed => rotationSpeed;
    public float Gravity => Physics.gravity.y;
    public Transform CameraRoot => cameraRoot;
    public float LatestLookYaw => _latestLookYaw;
    public float LatestLookPitch => _latestLookPitch;
    public IInputProvider InputProvider => _inputProvider;
    public PlayerVisual Visual => playerVisual;
    public Animator Animator => playerVisual?.Animator;
    public CharacterController CharController => _characterController;
    public StateMachine<PlayerBaseState> StateMachine => _stateMachine;
    public RuntimeSimulationMode SimulationMode => _simulationMode;
    public float NetworkLocomotionGroundedGravityValue => NetworkLocomotionGroundedGravity;
    public LayerMask MultiplayerLocomotionCollisionMask => ResolveMultiplayerLocomotionCollisionMask();
    public float MultiplayerLocomotionCollisionShell => _multiplayerLocomotionCollisionShell > 0f ? _multiplayerLocomotionCollisionShell : 0.02f;
    public float MultiplayerLocomotionGroundSnapDistance => _multiplayerLocomotionGroundSnapDistance > 0f ? _multiplayerLocomotionGroundSnapDistance : 0.15f;
    public int MultiplayerLocomotionMaxSlideIterations => _multiplayerLocomotionMaxSlideIterations > 0 ? _multiplayerLocomotionMaxSlideIterations : 1;
    public bool CanRunNetworkLocomotion => !_isStunned
                                           && (_health == null || !_health.IsDead)
                                           && _stateMachine != null
                                           && _stateMachine.CurrentState == MoveState;

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
        if (attack2DebugTraceDuration < 0f) attack2DebugTraceDuration = 0f;
        if (attack2DebugLogInterval < 0.01f) attack2DebugLogInterval = 0.01f;
        if (attack2DebugNearDistance < 0f) attack2DebugNearDistance = 0f;
        if (_multiplayerPresentationSmoothTime < 0f) _multiplayerPresentationSmoothTime = 0f;
        if (_multiplayerPresentationSnapDistance < 0f) _multiplayerPresentationSnapDistance = 0f;
        if (_multiplayerPresentationMovingCatchUpDistance < 0f) _multiplayerPresentationMovingCatchUpDistance = 0f;
        if (_multiplayerPresentationMovingCatchUpSmoothTime < 0f) _multiplayerPresentationMovingCatchUpSmoothTime = 0f;
        if (_multiplayerPredictedRenderSmoothTime < 0f) _multiplayerPredictedRenderSmoothTime = 0f;
        if (_multiplayerPredictedRenderSnapDistance < 0f) _multiplayerPredictedRenderSnapDistance = 0f;
        if (_multiplayerCameraFollowSmoothTime < 0f) _multiplayerCameraFollowSmoothTime = 0f;
        if (_multiplayerCameraFollowSnapDistance < 0f) _multiplayerCameraFollowSnapDistance = 0f;
        if (_multiplayerPresentationRotationSnapAngle < 0f) _multiplayerPresentationRotationSnapAngle = 0f;
        if (_multiplayerLocomotionCollisionShell < 0f) _multiplayerLocomotionCollisionShell = 0f;
        if (_multiplayerLocomotionGroundSnapDistance < 0f) _multiplayerLocomotionGroundSnapDistance = 0f;
        if (_multiplayerLocomotionMaxSlideIterations < 1) _multiplayerLocomotionMaxSlideIterations = 1;
        if (multiplayerPresentationTraceLogInterval < 0.02f) multiplayerPresentationTraceLogInterval = 0.02f;
        if (multiplayerPresentationTraceOffsetThreshold < 0f) multiplayerPresentationTraceOffsetThreshold = 0f;
        if (multiplayerPredictedRenderTraceLogInterval < 0.02f) multiplayerPredictedRenderTraceLogInterval = 0.02f;
        if (multiplayerPredictedRenderTraceOffsetThreshold < 0f) multiplayerPredictedRenderTraceOffsetThreshold = 0f;
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _inputProvider = GetComponent<IInputProvider>();
        _health = GetComponent<Health>();
        ResolveBlinkEffect();
        CachePresentationDefaultTransform();
        ResetPresentationRotationToRoot();

        if (_health != null)
        {
            _health.OnDamageTaken += HandleDamage;
            _health.OnDeath += HandleDeath;
        }

        if (_damageCaster != null)
        {
            _damageCaster.SetOwner(gameObject);
            _damageCaster.ForceDisableHitbox();
            _damageCaster.OnAttackHitConfirmed += HandleAttackHitConfirmed;
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
            _damageCaster.OnAttackHitConfirmed -= HandleAttackHitConfirmed;
            _damageCaster.OnAttackWindowResolved -= HandleAttackWindowResolved;
        }
    }

    private void Start()
    {
        _stateMachine.ChangeState(MoveState);
        _damageCaster?.ForceDisableHitbox();
        blinkWhiteEffect?.StopBlink();
        UpdateHealthInvincibilityByState();
        if (_isLocalPresentationEnabled)
        {
            InitializeCombatHUD();
        }
    }

    private void Update()
    {
        UpdateProjectileHitCountTimer();
        UpdatePostStunInvulnerability();

        if (_inputProvider == null)
        {
            UpdateAttack2DebugTrace();
            return;
        }

        PlayerInputPacket input = _inputProvider.GetInput();
        _latestLookYaw = input.lookYaw;
        _latestLookPitch = input.lookPitch;
        UpdateCameraRootFromLookInput();

        if (_simulationMode == RuntimeSimulationMode.Full)
        {
            _stateMachine.CurrentState?.Update(input);
        }
        else if (_simulationMode == RuntimeSimulationMode.LookOnly)
        {
            UpdateLookOnlyPresentation(input);
        }
        else if (_simulationMode == RuntimeSimulationMode.PredictedLocomotion)
        {
            UpdatePredictedLocomotionPresentation(input);
        }
        else
        {
            ResetPresentationRotationToRoot();
        }

        UpdateAttack2DebugTrace();
    }

    /// <summary>
    /// 입력 벡터를 카메라 기준으로 변환하여 이동 방향 계산
    /// </summary>
    public Vector3 GetMovementDirection(Vector2 inputDir)
    {
        if (cameraRoot == null)
        {
            Vector3 fallbackDirection = (transform.forward * inputDir.y + transform.right * inputDir.x).normalized;
            return fallbackDirection;
        }

        Vector3 camForward = cameraRoot.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = cameraRoot.right;
        camRight.y = 0;
        camRight.Normalize();

        return (camForward * inputDir.y + camRight * inputDir.x).normalized;
    }

    /// <summary>
    /// 카메라 시스템이 런타임 CameraRoot를 주입할 수 있도록 setter를 제공한다.
    /// </summary>
    public void SetCameraRoot(Transform newCameraRoot)
    {
        if (newCameraRoot == null) return;
        cameraRoot = newCameraRoot;
        _driveCameraRootFromLookInput = false;
    }

    public void SetInputProviderOverride(IInputProvider inputProvider)
    {
        _inputProvider = inputProvider;
    }

    public void SetSimulationMode(RuntimeSimulationMode simulationMode)
    {
        _simulationMode = simulationMode;
        if (_simulationMode != RuntimeSimulationMode.LookOnly)
        {
            ResetPresentationRotationToRoot();
        }
    }

    public void SetLocalPresentationEnabled(bool enabled)
    {
        _isLocalPresentationEnabled = enabled;
        if (!enabled)
        {
            HideComboHud();
            ResetPresentationRotationToRoot();
        }
    }

    public void SetLookDrivenCameraRootEnabled(bool enabled)
    {
        _driveCameraRootFromLookInput = enabled;
    }

    public MultiplayerLocomotionState CaptureCurrentLocomotionState(int inputSequence, int serverTick, bool allowsPrediction)
    {
        return SharedMultiplayerLocomotionCore.CaptureCurrentState(this, inputSequence, serverTick, allowsPrediction);
    }

    public void ApplyLocomotionState(in MultiplayerLocomotionState state)
    {
        SharedMultiplayerLocomotionCore.ApplyState(this, state);
    }

    public MultiplayerLocomotionState SimulateLocomotionTickFromCurrent(in MultiplayerLocomotionState currentState, in PlayerInputPacket input, float deltaTime, int inputSequence, int serverTick, bool allowsPrediction, bool updateAnimator)
    {
        return SharedMultiplayerLocomotionCore.SimulateTick(
            this,
            currentState,
            input,
            deltaTime,
            inputSequence,
            serverTick,
            allowsPrediction,
            updateAnimator);
    }

    public void RefreshLocalPresentationBindings()
    {
        if (!_isLocalPresentationEnabled)
        {
            return;
        }

        ResetPresentationRotationToRoot();
        InitializeCombatHUD();
    }

    public Vector3 GetPreferredCameraFollowPosition()
    {
        if (ShouldUseLatencyMaskingPresentation())
        {
            if (!_hasCameraFollowWorldPosition)
            {
                _cameraFollowWorldPosition = transform.position;
                _cameraFollowWorldVelocity = Vector3.zero;
                _hasCameraFollowWorldPosition = true;
            }

            return _cameraFollowWorldPosition;
        }

        return transform.position;
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
        _activeStunSourceHitType = BossAttackHitType.Unknown;
        _attack2DebugTraceTimer = 0f;
        _wasAttack2NearLastFrame = false;

        blinkWhiteEffect?.StopBlink();
        UpdateHealthInvincibilityByState();
        HideComboHud();
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
                return ApplyDamageAndHitReaction(hitData.Damage)
                    ? BossAttackHitResolution.Damaged
                    : BossAttackHitResolution.Ignored;

            case BossAttackHitType.Attack2:
                return HandleAttack2Hit(hitData);

            case BossAttackHitType.Attack3Projectile:
            case BossAttackHitType.Attack4Projectile:
                return HandleProjectileHit(hitData);
        }

        return ApplyDamageAndHitReaction(hitData.Damage)
            ? BossAttackHitResolution.Damaged
            : BossAttackHitResolution.Ignored;
    }

    public void HandleStunFinished()
    {
        if (!_isStunned) return;

        _isStunned = false;
        _activeStunSourceHitType = BossAttackHitType.Unknown;
        _stateMachine.ChangeState(MoveState);
        StartPostStunInvulnerability();
    }

    private BossAttackHitResolution HandleAttack2Hit(in BossAttackHitData hitData)
    {
        BeginAttack2DebugTrace("Hit", hitData.Damage, hitData.ForceDirection);

        bool didDamage = TryApplyDamage(hitData.Damage);
        if (!didDamage)
        {
            return BossAttackHitResolution.Ignored;
        }

        if (!_health.IsDead)
        {
            BeginStun(hitData.ForceDirection, BossAttackHitType.Attack2);
        }

        return BossAttackHitResolution.Damaged;
    }

    private BossAttackHitResolution HandleProjectileHit(in BossAttackHitData hitData)
    {
        bool didDamage = ApplyDamageAndHitReaction(hitData.Damage);
        if (!didDamage)
        {
            return BossAttackHitResolution.Ignored;
        }

        if (_projectileCountTimerLeft <= 0f)
        {
            _projectileHitCount = 1;
            _projectileCountTimerLeft = projectileCountTimer;
            return BossAttackHitResolution.Damaged;
        }

        _projectileHitCount += 1;
        if (_projectileHitCount >= 2)
        {
            BeginStun(hitData.ForceDirection, hitData.HitType);
            ResetProjectileHitCounter();
            return BossAttackHitResolution.Damaged;
        }

        return BossAttackHitResolution.Damaged;
    }

    private bool TryApplyDamage(int damage)
    {
        if (_health == null || _health.IsDead) return false;
        if (damage <= 0) return false;

        int previousHp = _health.CurrentHealth;
        _suppressDamageTakenReaction = true;
        try
        {
            _health.TakeDamage(damage);
        }
        finally
        {
            _suppressDamageTakenReaction = false;
        }

        return _health.CurrentHealth < previousHp;
    }

    private bool ApplyDamageAndHitReaction(int damage)
    {
        bool didDamage = TryApplyDamage(damage);
        if (didDamage && !_health.IsDead)
        {
            _stateMachine.ChangeState(HitState);
        }

        return didDamage;
    }

    private void BeginStun(Vector3 forceDirection, BossAttackHitType sourceHitType)
    {
        _isStunned = true;
        _activeStunSourceHitType = sourceHitType;
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

        if (sourceHitType == BossAttackHitType.Attack2)
        {
            BeginAttack2DebugTrace("StunEnter", 0, planarForceDirection);
        }
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

    public void ReportStunMovement(Vector3 movement, float verticalVelocity, float pushbackTimer, float pushbackSpeed, CollisionFlags collisionFlags)
    {
        if (!enableAttack2DebugLog) return;
        if (!_isStunned || _activeStunSourceHitType != BossAttackHitType.Attack2) return;
        if (Time.time < _nextAttack2StunDebugLogTime) return;

        _nextAttack2StunDebugLogTime = Time.time + attack2DebugLogInterval;

        float bossDistance;
        float bossNormalizedTime;
        string bossStateName;
        TryGetAttack2BossDebugData(out bossDistance, out bossStateName, out bossNormalizedTime);

        float planarMove = new Vector2(movement.x, movement.z).magnitude;
        Debug.Log(
            $"[Attack2PlayerY][StunMove] " +
            $"playerY={transform.position.y:F3} " +
            $"grounded={_characterController.isGrounded} " +
            $"ccVelY={_characterController.velocity.y:F3} " +
            $"verticalVel={verticalVelocity:F3} " +
            $"moveY={movement.y:F3} " +
            $"planarMove={planarMove:F3} " +
            $"pushTimer={pushbackTimer:F3} " +
            $"pushSpeed={pushbackSpeed:F3} " +
            $"flags={collisionFlags} " +
            $"state={GetCurrentStateName()} " +
            $"bossDist={bossDistance:F3} " +
            $"bossState={bossStateName} " +
            $"bossNTime={bossNormalizedTime:F3}");
    }

    private void ResetProjectileHitCounter()
    {
        _projectileHitCount = 0;
        _projectileCountTimerLeft = 0f;
    }

    private void UpdateCameraRootFromLookInput()
    {
        if (!_driveCameraRootFromLookInput || cameraRoot == null)
        {
            return;
        }

        cameraRoot.rotation = Quaternion.Euler(0f, _latestLookYaw, 0f);
    }

    private void UpdateLookOnlyPresentation(PlayerInputPacket input)
    {
        if (!_isLocalPresentationEnabled)
        {
            ResetPresentationRotationToRoot();
            return;
        }

        bool isMoveActive = input.moveDir.sqrMagnitude > 0.0001f;
        bool didStartMovingThisFrame = isMoveActive && !_wasLookOnlyMoveActive;
        _wasLookOnlyMoveActive = isMoveActive;
        UpdateLookOnlyAnimator(input);

        Transform presentationTransform = ResolvePresentationTransform();
        UpdateMaskedCameraFollowPosition();

        if (presentationTransform == null)
        {
            return;
        }

        UpdateMaskedPresentationPosition(presentationTransform, didStartMovingThisFrame, isMoveActive);

        Quaternion targetRotation = ResolvePresentationTargetRotation(input);
        if (targetRotation != Quaternion.identity)
        {
            if (ShouldSnapLookOnlyRotation(presentationTransform.rotation, targetRotation, didStartMovingThisFrame))
            {
                presentationTransform.rotation = targetRotation;
            }
            else
            {
                presentationTransform.rotation = Quaternion.Slerp(
                    presentationTransform.rotation,
                    targetRotation,
                    RotationSpeed * Time.deltaTime);
            }
        }

        UpdateMultiplayerPresentationTrace(presentationTransform, input, isMoveActive, didStartMovingThisFrame);
    }

    private void UpdatePredictedLocomotionPresentation(PlayerInputPacket input)
    {
        if (!_isLocalPresentationEnabled)
        {
            ResetPresentationRotationToRoot();
            return;
        }

        Transform presentationTransform = ResolvePresentationTransform();
        if (presentationTransform == null)
        {
            return;
        }

        bool shouldSnapPredictedTransition = ShouldSnapPredictedPresentationTransition(input.moveDir);
        UpdatePredictedPresentationPosition(presentationTransform, shouldSnapPredictedTransition);
        UpdatePredictedRenderTrace(presentationTransform, input);

        if (_hasPresentationDefaultTransform)
        {
            presentationTransform.localRotation = _presentationDefaultLocalRotation;
        }
        else
        {
            presentationTransform.rotation = transform.rotation;
        }
    }

    private void ResetPresentationRotationToRoot()
    {
        Transform presentationTransform = ResolvePresentationTransform();
        if (presentationTransform == null)
        {
            ResetMaskedFollowState();
            return;
        }

        if (_hasPresentationDefaultTransform)
        {
            presentationTransform.localPosition = _presentationDefaultLocalPosition;
            presentationTransform.localRotation = _presentationDefaultLocalRotation;
        }
        else
        {
            presentationTransform.position = transform.position;
            presentationTransform.rotation = transform.rotation;
        }

        ResetMaskedFollowState();
    }

    private Transform ResolvePresentationTransform()
    {
        if (playerVisual == null || playerVisual.transform == transform)
        {
            return null;
        }

        return playerVisual.transform;
    }

    private static Vector3 GetMovementDirectionFromLook(Vector2 inputDir, float lookYaw)
    {
        if (inputDir.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        Quaternion lookRotation = Quaternion.Euler(0f, lookYaw, 0f);
        Vector3 camForward = lookRotation * Vector3.forward;
        Vector3 camRight = lookRotation * Vector3.right;
        return (camForward * inputDir.y + camRight * inputDir.x).normalized;
    }

    private float ResolveCurrentVerticalVelocity()
    {
        if (_characterController == null)
        {
            return 0f;
        }

        if (_characterController.isGrounded)
        {
            return NetworkLocomotionGroundedGravity;
        }

        return _characterController.velocity.y;
    }

    private Vector3 ResolveCurrentPlanarVelocity()
    {
        if (_characterController == null)
        {
            return Vector3.zero;
        }

        Vector3 controllerVelocity = _characterController.velocity;
        controllerVelocity.y = 0f;
        return controllerVelocity;
    }

    private void CachePresentationDefaultTransform()
    {
        Transform presentationTransform = ResolvePresentationTransform();
        if (presentationTransform == null)
        {
            return;
        }

        _presentationDefaultLocalPosition = presentationTransform.localPosition;
        _presentationDefaultLocalRotation = presentationTransform.localRotation;
        _hasPresentationDefaultTransform = true;
    }

    private bool ShouldUseLatencyMaskingPresentation()
    {
        return _simulationMode == RuntimeSimulationMode.LookOnly && _isLocalPresentationEnabled;
    }

    private bool ShouldUsePredictedRenderSmoothingPresentation()
    {
        return _simulationMode == RuntimeSimulationMode.PredictedLocomotion && _isLocalPresentationEnabled;
    }

    private void UpdateMaskedPresentationPosition(Transform presentationTransform, bool shouldUseImmediateBodyResponse, bool isMoveActive)
    {
        if (!ShouldUseLatencyMaskingPresentation())
        {
            return;
        }

        Vector3 targetPosition = ResolvePresentationTargetPosition();
        float snapDistance = _multiplayerPresentationSnapDistance;
        if (shouldUseImmediateBodyResponse)
        {
            _presentationWorldPosition = targetPosition;
            _presentationWorldVelocity = Vector3.zero;
            _hasPresentationWorldPosition = true;
        }
        else if (!_hasPresentationWorldPosition || (targetPosition - _presentationWorldPosition).sqrMagnitude >= snapDistance * snapDistance)
        {
            _presentationWorldPosition = targetPosition;
            _presentationWorldVelocity = Vector3.zero;
            _hasPresentationWorldPosition = true;
        }
        else
        {
            float smoothTime = _multiplayerPresentationSmoothTime;
            if (isMoveActive && _multiplayerPresentationMovingCatchUpDistance > 0f)
            {
                float movingOffset = Vector3.Distance(targetPosition, _presentationWorldPosition);
                if (movingOffset >= _multiplayerPresentationMovingCatchUpDistance)
                {
                    smoothTime = smoothTime > 0f
                        ? Mathf.Min(smoothTime, _multiplayerPresentationMovingCatchUpSmoothTime)
                        : _multiplayerPresentationMovingCatchUpSmoothTime;
                }
            }

            if (smoothTime > 0f)
            {
                _presentationWorldPosition = Vector3.SmoothDamp(
                    _presentationWorldPosition,
                    targetPosition,
                    ref _presentationWorldVelocity,
                    smoothTime);
            }
            else
            {
                _presentationWorldPosition = targetPosition;
            }
        }

        presentationTransform.position = _presentationWorldPosition;
    }

    private void UpdatePredictedPresentationPosition(Transform presentationTransform)
    {
        UpdatePredictedPresentationPosition(presentationTransform, false);
    }

    private void UpdatePredictedPresentationPosition(
        Transform presentationTransform,
        bool shouldSnapPredictedTransition)
    {
        if (!ShouldUsePredictedRenderSmoothingPresentation())
        {
            return;
        }

        Vector3 targetPosition = ResolvePresentationTargetPosition();
        float snapDistance = _multiplayerPredictedRenderSnapDistance;
        if (shouldSnapPredictedTransition
            || !_hasPresentationWorldPosition
            || (targetPosition - _presentationWorldPosition).sqrMagnitude >= snapDistance * snapDistance)
        {
            _presentationWorldPosition = targetPosition;
            _presentationWorldVelocity = Vector3.zero;
            _hasPresentationWorldPosition = true;
            _predictedPresentationPreviousTargetPosition = targetPosition;
            _predictedPresentationCurrentTargetPosition = targetPosition;
            _predictedPresentationTargetSetTime = Time.time;
            _hasPredictedPresentationTargets = true;
        }
        else if (_multiplayerPredictedRenderSmoothTime > 0f)
        {
            if (!_hasPredictedPresentationTargets)
            {
                _predictedPresentationPreviousTargetPosition = targetPosition;
                _predictedPresentationCurrentTargetPosition = targetPosition;
                _predictedPresentationTargetSetTime = Time.time;
                _hasPredictedPresentationTargets = true;
            }
            else if ((targetPosition - _predictedPresentationCurrentTargetPosition).sqrMagnitude > 0.000001f)
            {
                _predictedPresentationPreviousTargetPosition = _predictedPresentationCurrentTargetPosition;
                _predictedPresentationCurrentTargetPosition = targetPosition;
                _predictedPresentationTargetSetTime = Time.time;
            }

            float tickInterval = ResolvePredictedPresentationTickInterval();
            float interpolationWindow = tickInterval > 0f
                ? Mathf.Min(_multiplayerPredictedRenderSmoothTime, tickInterval)
                : _multiplayerPredictedRenderSmoothTime;
            if (interpolationWindow <= 0f)
            {
                interpolationWindow = tickInterval > 0f ? tickInterval : 1f / 60f;
            }

            float linearInterpolationAlpha = EvaluatePredictedPresentationLinearAlpha(
                Time.time - _predictedPresentationTargetSetTime,
                interpolationWindow);
            float interpolationAlpha = EvaluatePredictedPresentationInterpolationAlpha(linearInterpolationAlpha);
            Vector3 renderPosition = Vector3.Lerp(
                _predictedPresentationPreviousTargetPosition,
                _predictedPresentationCurrentTargetPosition,
                interpolationAlpha);

            if (Time.deltaTime > 0f)
            {
                _presentationWorldVelocity = (renderPosition - _presentationWorldPosition) / Time.deltaTime;
            }
            else
            {
                _presentationWorldVelocity = Vector3.zero;
            }

            _presentationWorldPosition = renderPosition;
        }
        else
        {
            _presentationWorldPosition = targetPosition;
            _presentationWorldVelocity = Vector3.zero;
            _predictedPresentationPreviousTargetPosition = targetPosition;
            _predictedPresentationCurrentTargetPosition = targetPosition;
            _predictedPresentationTargetSetTime = Time.time;
            _hasPredictedPresentationTargets = true;
        }

        presentationTransform.position = _presentationWorldPosition;
    }

    private bool ShouldSnapPredictedPresentationTransition(Vector2 currentMoveInput)
    {
        if (currentMoveInput.sqrMagnitude <= 0.0001f)
        {
            _hasLastPredictedPresentationMoveInput = false;
            _lastPredictedPresentationMoveInput = Vector2.zero;
            return false;
        }

        Vector2 normalizedCurrentInput = currentMoveInput.normalized;
        bool shouldSnap = false;
        if (_hasLastPredictedPresentationMoveInput)
        {
            float angleDelta = Vector2.Angle(_lastPredictedPresentationMoveInput, normalizedCurrentInput);
            shouldSnap = angleDelta >= PredictedPresentationTransitionSnapAngle;
        }

        _lastPredictedPresentationMoveInput = normalizedCurrentInput;
        _hasLastPredictedPresentationMoveInput = true;
        return shouldSnap;
    }

    private static float ResolvePredictedPresentationTickInterval()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.NetworkConfig == null)
        {
            return 1f / 60f;
        }

        int tickRate = (int)networkManager.NetworkConfig.TickRate;
        return tickRate > 0 ? 1f / tickRate : 1f / 60f;
    }

    private void UpdateMaskedCameraFollowPosition()
    {
        Vector3 targetPosition = transform.position;
        if (!ShouldUseLatencyMaskingPresentation())
        {
            _cameraFollowWorldPosition = targetPosition;
            _cameraFollowWorldVelocity = Vector3.zero;
            _hasCameraFollowWorldPosition = true;
            return;
        }

        float snapDistance = _multiplayerCameraFollowSnapDistance;
        if (!_hasCameraFollowWorldPosition || (targetPosition - _cameraFollowWorldPosition).sqrMagnitude >= snapDistance * snapDistance)
        {
            _cameraFollowWorldPosition = targetPosition;
            _cameraFollowWorldVelocity = Vector3.zero;
            _hasCameraFollowWorldPosition = true;
            return;
        }

        if (_multiplayerCameraFollowSmoothTime > 0f)
        {
            _cameraFollowWorldPosition = Vector3.SmoothDamp(
                _cameraFollowWorldPosition,
                targetPosition,
                ref _cameraFollowWorldVelocity,
                _multiplayerCameraFollowSmoothTime);
        }
        else
        {
            _cameraFollowWorldPosition = targetPosition;
        }
    }

    private void UpdateLookOnlyAnimator(PlayerInputPacket input)
    {
        if (Animator == null)
        {
            return;
        }

        Animator.SetFloat(ANIM_PARAM_SPEED, input.moveDir.magnitude);
    }

    private void UpdateMultiplayerPresentationTrace(Transform presentationTransform, PlayerInputPacket input, bool isMoveActive, bool didStartMovingThisFrame)
    {
        if (!enableMultiplayerPresentationTrace || !ShouldUseLatencyMaskingPresentation() || presentationTransform == null)
        {
            return;
        }

        Vector3 targetPosition = ResolvePresentationTargetPosition();
        float visualToTargetOffset = Vector3.Distance(presentationTransform.position, targetPosition);
        bool shouldTrace = didStartMovingThisFrame || isMoveActive || visualToTargetOffset >= multiplayerPresentationTraceOffsetThreshold;
        if (!shouldTrace)
        {
            return;
        }

        if (Time.time < _nextMultiplayerPresentationTraceLogTime)
        {
            return;
        }

        _nextMultiplayerPresentationTraceLogTime = Time.time + multiplayerPresentationTraceLogInterval;
        Quaternion targetRotation = ResolvePresentationTargetRotation(input);
        float targetYaw = targetRotation == Quaternion.identity ? presentationTransform.eulerAngles.y : targetRotation.eulerAngles.y;
        float visualToRootOffset = Vector3.Distance(presentationTransform.position, transform.position);
        float visualToCameraFollowOffset = _hasCameraFollowWorldPosition
            ? Vector3.Distance(presentationTransform.position, _cameraFollowWorldPosition)
            : 0f;

        Debug.Log(
            $"[MultiplayerPresentationTrace] " +
            $"moveActive={isMoveActive} " +
            $"startMove={didStartMovingThisFrame} " +
            $"inputMag={input.moveDir.magnitude:F3} " +
            $"input=({input.moveDir.x:F3},{input.moveDir.y:F3}) " +
            $"root=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}) " +
            $"target=({targetPosition.x:F3},{targetPosition.y:F3},{targetPosition.z:F3}) " +
            $"visual=({presentationTransform.position.x:F3},{presentationTransform.position.y:F3},{presentationTransform.position.z:F3}) " +
            $"visualTargetOffset={visualToTargetOffset:F3} " +
            $"visualRootOffset={visualToRootOffset:F3} " +
            $"visualCameraOffset={visualToCameraFollowOffset:F3} " +
            $"bodyYaw={presentationTransform.eulerAngles.y:F1} " +
            $"targetYaw={targetYaw:F1} " +
            $"visualVelMag={_presentationWorldVelocity.magnitude:F3}");
    }

    private void UpdatePredictedRenderTrace(Transform presentationTransform, PlayerInputPacket input)
    {
        if (!enableMultiplayerPredictedRenderTrace
            || !ShouldUsePredictedRenderSmoothingPresentation()
            || presentationTransform == null)
        {
            return;
        }

        bool isMoveActive = input.moveDir.sqrMagnitude > 0.0001f;
        if (!isMoveActive)
        {
            return;
        }

        if (multiplayerPredictedRenderTraceLateralOnly
            && Mathf.Abs(input.moveDir.x) < Mathf.Abs(input.moveDir.y))
        {
            return;
        }

        Vector3 targetPosition = ResolvePresentationTargetPosition();
        float visualToTargetOffset = Vector3.Distance(presentationTransform.position, targetPosition);
        if (visualToTargetOffset < multiplayerPredictedRenderTraceOffsetThreshold
            && Time.time < _nextMultiplayerPredictedRenderTraceLogTime)
        {
            return;
        }

        if (Time.time < _nextMultiplayerPredictedRenderTraceLogTime)
        {
            return;
        }

        _nextMultiplayerPredictedRenderTraceLogTime = Time.time + multiplayerPredictedRenderTraceLogInterval;
        float visualToRootOffset = Vector3.Distance(presentationTransform.position, transform.position);
        float tickInterval = ResolvePredictedPresentationTickInterval();
        float interpolationWindow = tickInterval > 0f
            ? Mathf.Min(_multiplayerPredictedRenderSmoothTime, tickInterval)
            : _multiplayerPredictedRenderSmoothTime;
        if (interpolationWindow <= 0f)
        {
            interpolationWindow = tickInterval > 0f ? tickInterval : 1f / 60f;
        }

        float linearInterpolationAlpha = EvaluatePredictedPresentationLinearAlpha(
            Time.time - _predictedPresentationTargetSetTime,
            interpolationWindow);
        float interpolationAlpha = EvaluatePredictedPresentationInterpolationAlpha(linearInterpolationAlpha);
        float tickStepDistance = _hasPredictedPresentationTargets
            ? Vector3.Distance(_predictedPresentationPreviousTargetPosition, _predictedPresentationCurrentTargetPosition)
            : 0f;
        float behindTicks = tickStepDistance > 0.0001f
            ? visualToTargetOffset / tickStepDistance
            : 0f;
        float targetSpeed = tickInterval > 0.0001f
            ? tickStepDistance / tickInterval
            : 0f;

        Debug.Log(
            $"[MultiplayerPredictedRenderTrace] " +
            $"input=({input.moveDir.x:F3},{input.moveDir.y:F3}) " +
            $"root=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}) " +
            $"target=({targetPosition.x:F3},{targetPosition.y:F3},{targetPosition.z:F3}) " +
            $"visual=({presentationTransform.position.x:F3},{presentationTransform.position.y:F3},{presentationTransform.position.z:F3}) " +
            $"visualTargetOffset={visualToTargetOffset:F3} " +
            $"visualRootOffset={visualToRootOffset:F3} " +
            $"behindTicks={behindTicks:F3} " +
            $"tickStep={tickStepDistance:F3} " +
            $"targetSpeed={targetSpeed:F3} " +
            $"interpMode=easeOutPrevToCurrent " +
            $"smoothWindow={interpolationWindow:F4} " +
            $"alphaFloor={PredictedPresentationTickBoundaryHeadStartFraction:F3} " +
            $"linearAlpha={linearInterpolationAlpha:F3} " +
            $"interpAlpha={interpolationAlpha:F3} " +
            $"supportSmooth={_multiplayerPredictedRenderSmoothTime:F4} " +
            $"rootYaw={transform.eulerAngles.y:F1} " +
            $"visualVelMag={_presentationWorldVelocity.magnitude:F3}");
    }

    private static float EvaluatePredictedPresentationLinearAlpha(float elapsedSinceTarget, float interpolationWindow)
    {
        if (interpolationWindow <= 0f)
        {
            return 1f;
        }

        elapsedSinceTarget = Mathf.Max(0f, elapsedSinceTarget);
        float minimumElapsed = interpolationWindow * PredictedPresentationTickBoundaryHeadStartFraction;
        return Mathf.Clamp01(Mathf.Max(elapsedSinceTarget, minimumElapsed) / interpolationWindow);
    }

    private static float EvaluatePredictedPresentationInterpolationAlpha(float linearInterpolationAlpha)
    {
        linearInterpolationAlpha = Mathf.Clamp01(linearInterpolationAlpha);
        float inverse = 1f - linearInterpolationAlpha;
        return 1f - (inverse * inverse * inverse);
    }

    private Vector3 ResolvePresentationTargetPosition()
    {
        if (_hasPresentationDefaultTransform)
        {
            return transform.TransformPoint(_presentationDefaultLocalPosition);
        }

        return transform.position;
    }

    private Quaternion ResolvePresentationTargetRotation(PlayerInputPacket input)
    {
        Quaternion baseRotation = _hasPresentationDefaultTransform
            ? transform.rotation * _presentationDefaultLocalRotation
            : transform.rotation;

        if (input.moveDir.sqrMagnitude <= 0.0001f)
        {
            return baseRotation;
        }

        Vector3 moveDirection = GetMovementDirection(input.moveDir);
        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            return baseRotation;
        }

        Quaternion facingRotation = Quaternion.LookRotation(moveDirection);
        return _hasPresentationDefaultTransform
            ? facingRotation * _presentationDefaultLocalRotation
            : facingRotation;
    }

    private bool ShouldSnapLookOnlyRotation(Quaternion currentRotation, Quaternion targetRotation, bool didStartMovingThisFrame)
    {
        if (didStartMovingThisFrame)
        {
            return true;
        }

        float angleDelta = Quaternion.Angle(currentRotation, targetRotation);
        return angleDelta >= _multiplayerPresentationRotationSnapAngle;
    }

    private void ResetMaskedFollowState()
    {
        _presentationWorldPosition = ResolvePresentationTargetPosition();
        _presentationWorldVelocity = Vector3.zero;
        _hasPresentationWorldPosition = true;
        _predictedPresentationPreviousTargetPosition = _presentationWorldPosition;
        _predictedPresentationCurrentTargetPosition = _presentationWorldPosition;
        _predictedPresentationTargetSetTime = Time.time;
        _hasPredictedPresentationTargets = true;
        _lastPredictedPresentationMoveInput = Vector2.zero;
        _hasLastPredictedPresentationMoveInput = false;
        _cameraFollowWorldPosition = transform.position;
        _cameraFollowWorldVelocity = Vector3.zero;
        _hasCameraFollowWorldPosition = true;
        _wasLookOnlyMoveActive = false;
        _nextMultiplayerPresentationTraceLogTime = 0f;
        _nextMultiplayerPredictedRenderTraceLogTime = 0f;
    }

    private static class SharedMultiplayerLocomotionCore
    {
        public static MultiplayerLocomotionState CaptureCurrentState(PlayerController controller, int inputSequence, int serverTick, bool allowsPrediction)
        {
            return new MultiplayerLocomotionState
            {
                InputSequence = inputSequence,
                ServerTick = serverTick,
                Position = controller.transform.position,
                Yaw = controller.transform.eulerAngles.y,
                PlanarVelocity = controller.ResolveCurrentPlanarVelocity(),
                VerticalVelocity = controller.ResolveCurrentVerticalVelocity(),
                JumpTimer = 0f,
                AllowsPrediction = allowsPrediction,
                IsGrounded = controller._characterController != null && controller._characterController.isGrounded
            };
        }

        public static void ApplyState(PlayerController controller, in MultiplayerLocomotionState state)
        {
            bool wasCharacterControllerEnabled = controller._characterController != null && controller._characterController.enabled;
            if (wasCharacterControllerEnabled)
            {
                controller._characterController.enabled = false;
            }

            controller.transform.SetPositionAndRotation(state.Position, Quaternion.Euler(0f, state.Yaw, 0f));

            if (wasCharacterControllerEnabled)
            {
                controller._characterController.enabled = true;
            }
        }

        public static MultiplayerLocomotionState SimulateTick(
            PlayerController controller,
            in MultiplayerLocomotionState currentState,
            in PlayerInputPacket input,
            float deltaTime,
            int inputSequence,
            int serverTick,
            bool allowsPrediction,
            bool updateAnimator)
        {
            float verticalVelocity = currentState.VerticalVelocity;
            bool isGrounded = currentState.IsGrounded;
            Vector3 moveDirection = GetMovementDirectionFromLook(input.moveDir, input.lookYaw);
            float nextYaw = currentState.Yaw;
            Vector3 nextPlanarVelocity = currentState.PlanarVelocity;

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                float targetYaw = Quaternion.LookRotation(moveDirection).eulerAngles.y;
                float rotationBlend = 1f - Mathf.Exp(-controller.RotationSpeed * deltaTime);
                nextYaw = Mathf.LerpAngle(currentState.Yaw, targetYaw, rotationBlend);
            }

            Vector3 previousPosition = controller.transform.position;
            controller.transform.rotation = Quaternion.Euler(0f, nextYaw, 0f);

            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = controller.NetworkLocomotionGroundedGravityValue;
            }

            verticalVelocity += controller.Gravity * deltaTime;

            if (controller._characterController != null && controller._characterController.enabled)
            {
                Vector3 finalVelocity = (moveDirection * controller.MoveSpeed) + Vector3.up * verticalVelocity;
                controller._characterController.Move(finalVelocity * deltaTime);
                isGrounded = controller._characterController.isGrounded;
                Vector3 actualDelta = controller.transform.position - previousPosition;
                nextPlanarVelocity = new Vector3(actualDelta.x, 0f, actualDelta.z) / Mathf.Max(deltaTime, 0.0001f);
            }
            else
            {
                nextPlanarVelocity = moveDirection * controller.MoveSpeed;
            }

            if (updateAnimator && controller.Animator != null)
            {
                controller.Animator.SetFloat(ANIM_PARAM_SPEED, input.moveDir.magnitude);
            }

            return new MultiplayerLocomotionState
            {
                InputSequence = inputSequence,
                ServerTick = serverTick,
                Position = controller.transform.position,
                Yaw = nextYaw,
                PlanarVelocity = nextPlanarVelocity,
                VerticalVelocity = verticalVelocity,
                JumpTimer = currentState.JumpTimer,
                AllowsPrediction = allowsPrediction,
                IsGrounded = isGrounded
            };
        }
    }

    private LayerMask ResolveMultiplayerLocomotionCollisionMask()
    {
        if (_multiplayerLocomotionCollisionMask.value != 0)
        {
            return _multiplayerLocomotionCollisionMask;
        }

        int defaultMask = LayerMask.GetMask("Ground", "Wall", "Default");
        return defaultMask != 0 ? defaultMask : Physics.DefaultRaycastLayers;
    }

    private void HandleAttackWindowResolved(bool isHit, int totalDamage)
    {
        _combatHUD?.ShowDamageFeedback(isHit, totalDamage);
    }

    private void HandleAttackHitConfirmed()
    {
        if (_pendingComboHudStep <= 0)
        {
            return;
        }

        ShowComboHud(_pendingComboHudStep);
    }

    public void SetPendingComboHudStep(int comboStep)
    {
        _pendingComboHudStep = Mathf.Max(1, comboStep);
    }

    public void ShowComboHud(int comboStep)
    {
        _combatHUD?.ShowCombo(comboStep);
    }

    public void HideComboHud()
    {
        _pendingComboHudStep = 0;
        _combatHUD?.HideCombo();
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
        _combatHUD.SetPartnerHudVisible(ShouldShowPartnerHud());
        HideComboHud();
    }

    private static bool ShouldShowPartnerHud()
    {
        if (!Core.Multiplayer.MultiplayerSessionService.HasInstance)
        {
            return false;
        }

        return Core.Multiplayer.MultiplayerSessionService.Instance.HasActiveSession;
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

    private void UpdateAttack2DebugTrace()
    {
        if (!enableAttack2DebugLog || _characterController == null) return;

        float bossDistance;
        float bossNormalizedTime;
        string bossStateName;
        bool isAttack2Near = TryGetAttack2BossDebugData(out bossDistance, out bossStateName, out bossNormalizedTime);

        if (isAttack2Near && !_wasAttack2NearLastFrame)
        {
            BeginAttack2DebugTrace("NearEnter", 0, Vector3.zero);
            LogAttack2DebugSnapshot("NearEnter", bossDistance, bossStateName, bossNormalizedTime);
        }
        else if (!isAttack2Near && _wasAttack2NearLastFrame)
        {
            LogAttack2DebugSnapshot("NearExit", bossDistance, bossStateName, bossNormalizedTime);
        }

        _wasAttack2NearLastFrame = isAttack2Near;

        if (_attack2DebugTraceTimer <= 0f) return;

        _attack2DebugTraceTimer -= Time.deltaTime;
        if (Time.time < _nextAttack2DebugLogTime) return;

        _nextAttack2DebugLogTime = Time.time + attack2DebugLogInterval;
        LogAttack2DebugSnapshot("Trace", bossDistance, bossStateName, bossNormalizedTime);
    }

    private void BeginAttack2DebugTrace(string phase, int damage, Vector3 forceDirection)
    {
        if (!enableAttack2DebugLog) return;

        _attack2DebugTraceTimer = Mathf.Max(_attack2DebugTraceTimer, attack2DebugTraceDuration);
        _nextAttack2DebugLogTime = 0f;
        _nextAttack2StunDebugLogTime = 0f;

        float bossDistance;
        float bossNormalizedTime;
        string bossStateName;
        TryGetAttack2BossDebugData(out bossDistance, out bossStateName, out bossNormalizedTime);

        Debug.Log(
            $"[Attack2PlayerY][{phase}] " +
            $"damage={damage} " +
            $"force=({forceDirection.x:F3},{forceDirection.y:F3},{forceDirection.z:F3}) " +
            $"playerY={transform.position.y:F3} " +
            $"grounded={_characterController.isGrounded} " +
            $"ccVelY={_characterController.velocity.y:F3} " +
            $"state={GetCurrentStateName()} " +
            $"bossDist={bossDistance:F3} " +
            $"bossState={bossStateName} " +
            $"bossNTime={bossNormalizedTime:F3}");
    }

    private void LogAttack2DebugSnapshot(string phase, float bossDistance, string bossStateName, float bossNormalizedTime)
    {
        float playerBottomY = transform.position.y + _characterController.center.y - (_characterController.height * 0.5f);
        float playerTopY = transform.position.y + _characterController.center.y + (_characterController.height * 0.5f);

        Debug.Log(
            $"[Attack2PlayerY][{phase}] " +
            $"playerY={transform.position.y:F3} " +
            $"bottomY={playerBottomY:F3} " +
            $"topY={playerTopY:F3} " +
            $"grounded={_characterController.isGrounded} " +
            $"ccVelY={_characterController.velocity.y:F3} " +
            $"state={GetCurrentStateName()} " +
            $"stunSource={_activeStunSourceHitType} " +
            $"bossDist={bossDistance:F3} " +
            $"bossState={bossStateName} " +
            $"bossNTime={bossNormalizedTime:F3}");
    }

    private bool TryGetAttack2BossDebugData(out float bossDistance, out string bossStateName, out float bossNormalizedTime)
    {
        bossDistance = -1f;
        bossNormalizedTime = 0f;
        bossStateName = "None";

        if (_attack2DebugBoss == null)
        {
            _attack2DebugBoss = FindObjectOfType<Core.Boss.BossController>();
        }

        if (_attack2DebugBoss == null)
        {
            return false;
        }

        bossDistance = Core.Boss.BossController.GetPlanarDistance(_attack2DebugBoss.transform.position, transform.position);

        Animator bossAnimator = _attack2DebugBoss.Visual?.Animator;
        if (bossAnimator == null)
        {
            return false;
        }

        AnimatorStateInfo stateInfo = bossAnimator.GetCurrentAnimatorStateInfo(0);
        bool isLungeAttack = stateInfo.IsName("Lunge Attack");
        bool isLegacyClawAttack = stateInfo.IsName("Claw Attack");
        if (isLungeAttack)
        {
            bossStateName = "Lunge Attack";
        }
        else if (isLegacyClawAttack)
        {
            bossStateName = "Claw Attack";
        }
        else
        {
            bossStateName = "Other";
        }

        bossNormalizedTime = stateInfo.normalizedTime;
        return (isLungeAttack || isLegacyClawAttack) && bossDistance <= attack2DebugNearDistance;
    }

    private string GetCurrentStateName()
    {
        return _stateMachine?.CurrentState != null
            ? _stateMachine.CurrentState.GetType().Name
            : "None";
    }

    private void Reset()
    {
        attackCombos = new AttackComboData[3];
        attackCombos[0] = new AttackComboData { damage = 10f, duration = 0.5f, comboInputWindow = 0.3f, cancelStartTime = 0.3f };
        attackCombos[1] = new AttackComboData { damage = 15f, duration = 0.6f, comboInputWindow = 0.4f, cancelStartTime = 0.4f };
        attackCombos[2] = new AttackComboData { damage = 30f, duration = 1.0f, comboInputWindow = 0.0f, cancelStartTime = 0.6f };
    }
}
