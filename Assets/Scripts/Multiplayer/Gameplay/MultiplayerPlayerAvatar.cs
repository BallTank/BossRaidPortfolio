using Core.Combat;
using Core.GameFlow;
using Core.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Core.Multiplayer
{
    public enum LocomotionRuntimePath
    {
        HostOnlyCharacterController,
        PredictionReconciliation
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(LocalInputProvider))]
    [RequireComponent(typeof(MultiplayerBufferedInputProvider))]
    public sealed class MultiplayerPlayerAvatar : NetworkBehaviour
    {
        private const int PredictionBufferSize = 256;
        private const int MaxServerInputsPerTick = 8;
        private const float FallbackFixedDeltaTime = 1f / 30f;
        private const float OwnerPositionCorrectionDeadzone = 0.03f;
        private const float OwnerYawCorrectionDeadzone = 1.25f;

        [Header("Locomotion Runtime Path")]
        [SerializeField] private LocomotionRuntimePath _locomotionRuntimePath = LocomotionRuntimePath.HostOnlyCharacterController;

        [Header("Prediction Debug")]
        [SerializeField] private bool _enableClientPredictionTrace = true;
        [SerializeField] private bool _tracePredictionTicks = true;
        [SerializeField] private bool _traceDeadzonePackets;
        [SerializeField] private bool _traceDuplicatePackets;
        [SerializeField, Min(0.02f)] private float _clientPredictionTraceLogInterval = 0.08f;
        [SerializeField, Min(0f)] private float _clientPredictionTraceErrorThreshold = 0.05f;

        private struct ClientInputHistoryEntry
        {
            public int Sequence;
            public MultiplayerLocomotionInput Input;
            public bool WasPredicted;
        }

        private struct ClientPredictedStateHistoryEntry
        {
            public int Sequence;
            public MultiplayerLocomotionState State;
        }

        private struct ServerInputBufferEntry
        {
            public int Sequence;
            public MultiplayerLocomotionInput Input;
            public bool IsSet;
        }

        private PlayerController _playerController;
        private LocalInputProvider _localInputProvider;
        private MultiplayerBufferedInputProvider _bufferedInputProvider;
        private CharacterController _characterController;
        private Health _health;
        private NetworkTransform _networkTransform;
        private readonly ClientInputHistoryEntry[] _clientInputHistory = new ClientInputHistoryEntry[PredictionBufferSize];
        private readonly ClientPredictedStateHistoryEntry[] _clientPredictedStateHistory = new ClientPredictedStateHistoryEntry[PredictionBufferSize];
        private readonly ServerInputBufferEntry[] _serverInputBuffer = new ServerInputBufferEntry[PredictionBufferSize];
        private readonly ulong[] _authoritativeStateTargetClientIds = new ulong[1];
        private ClientRpcParams _authoritativeStateClientRpcParams;
        private MultiplayerLocomotionState _clientPredictedState;
        private MultiplayerLocomotionState _serverAuthoritativeState;
        private bool _hasClientPredictedState;
        private bool _hasServerAuthoritativeState;
        private bool _clientAllowsPrediction = true;
        private bool _serverUsingLocomotionAuthority;
        private bool _isTickRegistered;
        private int _nextLocalInputSequence;
        private int _lastAppliedAuthoritativeInputSequence;
        private int _lastReceivedAuthoritativeServerTick;
        private int _serverLatestReceivedInputSequence;
        private int _serverLastProcessedInputSequence;
        private int _serverNextInputSequenceToProcess = 1;
        private bool _hasReceivedInitialAuthoritativeBaseline;
        private bool _hasLoggedPredictionPathFallback;
        private float _nextClientPredictionTraceLogTime;
        private float _nextClientAuthoritativeTraceLogTime;

        private void Awake()
        {
            CacheComponents();
        }

        public override void OnDestroy()
        {
            UnregisterNetworkTick();
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            CacheComponents();
            _authoritativeStateTargetClientIds[0] = OwnerClientId;
            _authoritativeStateClientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = _authoritativeStateTargetClientIds
                }
            };

            RegisterNetworkTick();
            ResetRuntimeState();
            MultiplayerGameplaySceneCoordinator.EnsureCurrentGameplayScenePrepared();
            RefreshAvatarDebugName();
            LogPredictionPathFallbackIfNeeded();
            ConfigureRuntimeRole();
        }

        public override void OnNetworkDespawn()
        {
            UnregisterNetworkTick();

            if (_localInputProvider != null)
            {
                _localInputProvider.SetRuntimeInputEnabled(false);
            }

            if (MultiplayerLocalPlayerRegistry.LocalPlayer == _playerController)
            {
                MultiplayerLocalPlayerRegistry.Clear();
            }

            SetOwnerTransformSyncEnabled(true);
        }

        // 입력 시퀀스는 손실되면 재생이 멈추므로 owner->host 경로는 순서 보장을 유지한다.
        [ServerRpc(Delivery = RpcDelivery.Reliable)]
        private void SubmitOwnerInputServerRpc(MultiplayerLocomotionInput locomotionInput)
        {
            if (_bufferedInputProvider == null)
            {
                return;
            }

            _bufferedInputProvider.SetInput(locomotionInput);
            _serverLatestReceivedInputSequence = Mathf.Max(_serverLatestReceivedInputSequence, locomotionInput.InputSequence);
            StoreServerInput(locomotionInput);
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void PushAuthoritativeLocomotionStateClientRpc(MultiplayerLocomotionState state, ClientRpcParams clientRpcParams = default)
        {
            if (!IsSpawned || !IsOwner || IsServer || _playerController == null)
            {
                return;
            }

            if (state.ServerTick < _lastReceivedAuthoritativeServerTick)
            {
                return;
            }

            _lastReceivedAuthoritativeServerTick = state.ServerTick;
            if (ShouldActivatePredictionRuntimePath() && !_hasReceivedInitialAuthoritativeBaseline)
            {
                _playerController.ApplyLocomotionState(state);
                ApplyLocomotionAnimator(state.PlanarVelocity.magnitude);
                _clientPredictedState = state;
                _hasClientPredictedState = true;
                _hasReceivedInitialAuthoritativeBaseline = true;
                _clientAllowsPrediction = state.AllowsPrediction;
                _lastAppliedAuthoritativeInputSequence = Mathf.Max(_lastAppliedAuthoritativeInputSequence, state.InputSequence);
                StoreClientPredictedState(state.InputSequence, state);
                LogClientAuthoritativeTrace("baseline", state, state, 0f, 0f, true, 0);
                return;
            }

            _clientAllowsPrediction = state.AllowsPrediction;
            _hasClientPredictedState = true;

            bool hasPredictedComparison = TryGetClientPredictedState(state.InputSequence, out MultiplayerLocomotionState predictedStateForLog);
            float positionErrorForLog = hasPredictedComparison
                ? Vector3.Distance(predictedStateForLog.Position, state.Position)
                : 0f;
            float yawErrorForLog = hasPredictedComparison
                ? Mathf.Abs(Mathf.DeltaAngle(predictedStateForLog.Yaw, state.Yaw))
                : 0f;

            if (state.AllowsPrediction && state.InputSequence <= _lastAppliedAuthoritativeInputSequence)
            {
                LogClientAuthoritativeTrace(
                    "duplicate",
                    state,
                    hasPredictedComparison ? predictedStateForLog : _clientPredictedState,
                    positionErrorForLog,
                    yawErrorForLog,
                    false,
                    0);
                return;
            }

            if (!state.AllowsPrediction)
            {
                _playerController.ApplyLocomotionState(state);
                ApplyLocomotionAnimator(state.PlanarVelocity.magnitude);
                _clientPredictedState = state;
                _lastAppliedAuthoritativeInputSequence = Mathf.Max(_lastAppliedAuthoritativeInputSequence, state.InputSequence);
                LogClientAuthoritativeTrace(
                    "fallback",
                    state,
                    hasPredictedComparison ? predictedStateForLog : _clientPredictedState,
                    positionErrorForLog,
                    yawErrorForLog,
                    true,
                    0);
                return;
            }

            if (TryGetClientPredictedState(state.InputSequence, out MultiplayerLocomotionState predictedState)
                && IsWithinOwnerCorrectionDeadzone(predictedState, state))
            {
                _lastAppliedAuthoritativeInputSequence = Mathf.Max(_lastAppliedAuthoritativeInputSequence, state.InputSequence);
                LogClientAuthoritativeTrace(
                    "deadzone",
                    state,
                    predictedState,
                    Vector3.Distance(predictedState.Position, state.Position),
                    Mathf.Abs(Mathf.DeltaAngle(predictedState.Yaw, state.Yaw)),
                    false,
                    0);
                return;
            }

            _lastAppliedAuthoritativeInputSequence = Mathf.Max(_lastAppliedAuthoritativeInputSequence, state.InputSequence);
            _playerController.ApplyLocomotionState(state);

            MultiplayerLocomotionState replayState = state;
            float replayInputMagnitude = state.PlanarVelocity.magnitude / Mathf.Max(_playerController.MoveSpeed, 0.0001f);
            int replayCount = 0;
            for (int sequence = state.InputSequence + 1; sequence <= _nextLocalInputSequence; sequence++)
            {
                if (!TryGetClientInput(sequence, out ClientInputHistoryEntry inputEntry) || !inputEntry.WasPredicted)
                {
                    break;
                }

                replayState = SimulateNetworkLocomotionTick(
                    replayState,
                    inputEntry.Input.ToPlayerInputPacket(),
                    ResolveFixedDeltaTime(),
                    sequence,
                    state.ServerTick,
                    true,
                    true);

                replayInputMagnitude = inputEntry.Input.MoveDirection.magnitude;
                replayCount++;
            }

            _clientPredictedState = replayState;
            ApplyLocomotionAnimator(replayInputMagnitude);
            LogClientAuthoritativeTrace(
                "reconcile",
                state,
                hasPredictedComparison ? predictedStateForLog : _clientPredictedState,
                positionErrorForLog,
                yawErrorForLog,
                true,
                replayCount);
        }

        private void CacheComponents()
        {
            _playerController = GetComponent<PlayerController>();
            _localInputProvider = GetComponent<LocalInputProvider>();
            _bufferedInputProvider = GetComponent<MultiplayerBufferedInputProvider>();
            _characterController = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            _networkTransform = GetComponent<NetworkTransform>();
        }

        private void ConfigureRuntimeRole()
        {
            if (_playerController == null)
            {
                return;
            }

            ResetRuntimeState();
            _bufferedInputProvider?.Clear();
            _localInputProvider?.SetRuntimeInputEnabled(false);
            _playerController.SetInputProviderOverride(null);
            _playerController.SetSimulationMode(PlayerController.RuntimeSimulationMode.Disabled);
            _playerController.SetLocalPresentationEnabled(false);
            _playerController.SetLookDrivenCameraRootEnabled(false);

            if (IsServer && IsOwner)
            {
                ConfigureHostOwnedPlayer();
                LogRuntimeRoleConfiguration();
                return;
            }

            if (IsServer)
            {
                ConfigureHostAuthorityReplica();
                LogRuntimeRoleConfiguration();
                return;
            }

            if (IsOwner)
            {
                ConfigureClientOwnedPlayer();
                LogRuntimeRoleConfiguration();
                return;
            }

            ConfigureClientReplica();
            LogRuntimeRoleConfiguration();
        }

        private void ConfigureHostOwnedPlayer()
        {
            _localInputProvider?.SetLookAngles(transform.eulerAngles.y, _playerController.LatestLookPitch);
            _localInputProvider?.SetRuntimeInputEnabled(true);
            _playerController.SetInputProviderOverride(_localInputProvider);
            _playerController.SetSimulationMode(PlayerController.RuntimeSimulationMode.Full);
            _playerController.SetLocalPresentationEnabled(true);
            _playerController.SetLookDrivenCameraRootEnabled(false);
            SetCharacterControllerEnabled(true);
            SetOwnerTransformSyncEnabled(true);
            BindLocalPresentation();
        }

        private void ConfigureHostAuthorityReplica()
        {
            _playerController.SetInputProviderOverride(_bufferedInputProvider);
            _playerController.SetLocalPresentationEnabled(false);
            _playerController.SetLookDrivenCameraRootEnabled(false);
            SetCharacterControllerEnabled(true);
            SetOwnerTransformSyncEnabled(true);
            UpdateServerAuthorityMode(forceApply: true);
        }

        private void ConfigureClientOwnedPlayer()
        {
            bool usePredictionRuntimePath = ShouldActivatePredictionRuntimePath();
            _localInputProvider?.SetLookAngles(transform.eulerAngles.y, _playerController.LatestLookPitch);
            _localInputProvider?.SetRuntimeInputEnabled(true);
            _playerController.SetInputProviderOverride(_localInputProvider);
            _playerController.SetSimulationMode(usePredictionRuntimePath
                ? PlayerController.RuntimeSimulationMode.PredictedLocomotion
                : PlayerController.RuntimeSimulationMode.LookOnly);
            _playerController.SetLocalPresentationEnabled(true);
            _playerController.SetLookDrivenCameraRootEnabled(false);
            SetCharacterControllerEnabled(usePredictionRuntimePath);
            SetOwnerTransformSyncEnabled(false);
            _hasReceivedInitialAuthoritativeBaseline = !usePredictionRuntimePath;
            _clientAllowsPrediction = !usePredictionRuntimePath;
            _clientPredictedState = _playerController.CaptureCurrentLocomotionState(0, 0, false);
            _hasClientPredictedState = true;
            BindLocalPresentation();
        }

        private void ConfigureClientReplica()
        {
            _playerController.SetInputProviderOverride(null);
            _playerController.SetSimulationMode(PlayerController.RuntimeSimulationMode.Disabled);
            _playerController.SetLocalPresentationEnabled(false);
            _playerController.SetLookDrivenCameraRootEnabled(false);
            SetCharacterControllerEnabled(false);
            SetOwnerTransformSyncEnabled(true);
        }

        private void BindLocalPresentation()
        {
            MultiplayerLocalPlayerRegistry.SetLocalPlayer(_playerController);
            _playerController.RefreshLocalPresentationBindings();

            if (_health == null)
            {
                return;
            }

            GameManager gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null)
            {
                gameManager.SetPlayerHealth(_health);
            }
        }

        private void RegisterNetworkTick()
        {
            if (_isTickRegistered || NetworkManager == null || NetworkManager.NetworkTickSystem == null)
            {
                return;
            }

            NetworkManager.NetworkTickSystem.Tick += HandleNetworkTick;
            _isTickRegistered = true;
        }

        private void UnregisterNetworkTick()
        {
            if (!_isTickRegistered || NetworkManager == null || NetworkManager.NetworkTickSystem == null)
            {
                _isTickRegistered = false;
                return;
            }

            NetworkManager.NetworkTickSystem.Tick -= HandleNetworkTick;
            _isTickRegistered = false;
        }

        private void HandleNetworkTick()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer && !IsOwner)
            {
                HandleServerAuthorityTick();
            }

            if (IsOwner && !IsServer)
            {
                HandleClientPredictionTick();
            }
        }

        private void HandleClientPredictionTick()
        {
            if (_localInputProvider == null || _playerController == null)
            {
                return;
            }

            if (!_hasClientPredictedState)
            {
                _clientPredictedState = _playerController.CaptureCurrentLocomotionState(0, 0, _clientAllowsPrediction);
                _hasClientPredictedState = true;
            }

            PlayerInputPacket input = _localInputProvider.GetInput();
            MultiplayerLocomotionInput locomotionInput = MultiplayerLocomotionInput.FromPlayerInputPacket(input, ++_nextLocalInputSequence);
            bool canPredictThisTick = _hasReceivedInitialAuthoritativeBaseline && ShouldPredictLocomotionThisTick(input);

            StoreClientInput(locomotionInput, canPredictThisTick);

            if (canPredictThisTick)
            {
                if (!_clientPredictedState.AllowsPrediction)
                {
                    _clientPredictedState = _playerController.CaptureCurrentLocomotionState(
                        _lastAppliedAuthoritativeInputSequence,
                        _lastReceivedAuthoritativeServerTick,
                        true);
                }

                _clientPredictedState = SimulateNetworkLocomotionTick(
                    _clientPredictedState,
                    input,
                    ResolveFixedDeltaTime(),
                    locomotionInput.InputSequence,
                    _lastReceivedAuthoritativeServerTick,
                    true,
                    true);
            }
            else
            {
                _clientPredictedState = _playerController.CaptureCurrentLocomotionState(
                    locomotionInput.InputSequence,
                    _lastReceivedAuthoritativeServerTick,
                    false);
            }

            StoreClientPredictedState(locomotionInput.InputSequence, _clientPredictedState);
            LogClientPredictionTrace(locomotionInput, _clientPredictedState, canPredictThisTick);

            SubmitOwnerInputServerRpc(locomotionInput);
        }

        private void HandleServerAuthorityTick()
        {
            if (_playerController == null)
            {
                return;
            }

            UpdateServerAuthorityMode(forceApply: false);

            int currentServerTick = ResolveCurrentServerTick();
            if (_serverUsingLocomotionAuthority)
            {
                int processedInputCount = 0;
                while (processedInputCount < MaxServerInputsPerTick
                       && TryConsumeServerInput(_serverNextInputSequenceToProcess, out MultiplayerLocomotionInput locomotionInput))
                {
                    if (!_hasServerAuthoritativeState)
                    {
                        _serverAuthoritativeState = _playerController.CaptureCurrentLocomotionState(
                            _serverLastProcessedInputSequence,
                            currentServerTick,
                            true);
                        _hasServerAuthoritativeState = true;
                    }

                    _serverAuthoritativeState = SimulateNetworkLocomotionTick(
                        _serverAuthoritativeState,
                        locomotionInput.ToPlayerInputPacket(),
                        ResolveFixedDeltaTime(),
                        _serverNextInputSequenceToProcess,
                        currentServerTick,
                        true,
                        false);

                    _serverLastProcessedInputSequence = _serverNextInputSequenceToProcess;
                    _serverNextInputSequenceToProcess++;
                    processedInputCount++;
                }

                if (!_hasServerAuthoritativeState)
                {
                    _serverAuthoritativeState = _playerController.CaptureCurrentLocomotionState(
                        _serverLastProcessedInputSequence,
                        currentServerTick,
                        true);
                    _hasServerAuthoritativeState = true;
                }
                else
                {
                    _serverAuthoritativeState.InputSequence = _serverLastProcessedInputSequence;
                    _serverAuthoritativeState.ServerTick = currentServerTick;
                    _serverAuthoritativeState.Position = transform.position;
                    _serverAuthoritativeState.Yaw = transform.eulerAngles.y;
                    _serverAuthoritativeState.IsGrounded = _characterController != null && _characterController.isGrounded;
                    _serverAuthoritativeState.AllowsPrediction = true;
                }
            }
            else
            {
                int latestAuthoritativeSequence = _bufferedInputProvider != null
                    ? _bufferedInputProvider.LatestInputSequence
                    : _serverLastProcessedInputSequence;

                _serverAuthoritativeState = _playerController.CaptureCurrentLocomotionState(
                    latestAuthoritativeSequence,
                    currentServerTick,
                    false);

                _serverLastProcessedInputSequence = Mathf.Max(_serverLastProcessedInputSequence, latestAuthoritativeSequence);
                _serverNextInputSequenceToProcess = _serverLastProcessedInputSequence + 1;
                _hasServerAuthoritativeState = true;
            }

            PushAuthoritativeStateToOwner(_serverAuthoritativeState);
        }

        private void UpdateServerAuthorityMode(bool forceApply)
        {
            bool shouldUseLocomotionAuthority = ShouldUseAuthoritativeLocomotion();
            if (!forceApply && shouldUseLocomotionAuthority == _serverUsingLocomotionAuthority)
            {
                return;
            }

            if (shouldUseLocomotionAuthority)
            {
                EnterAuthoritativeLocomotionMode();
                return;
            }

            EnterFullAuthoritativeFallbackMode();
        }

        private bool ShouldUseAuthoritativeLocomotion()
        {
            if (!ShouldActivatePredictionRuntimePath()
                || _playerController == null
                || !_playerController.CanRunNetworkLocomotion)
            {
                return false;
            }

            PlayerInputPacket latestInput = _bufferedInputProvider != null
                ? _bufferedInputProvider.GetInput()
                : default;

            return latestInput.buttons == 0;
        }

        private void EnterAuthoritativeLocomotionMode()
        {
            _serverUsingLocomotionAuthority = true;
            _playerController.SetInputProviderOverride(_bufferedInputProvider);
            _playerController.SetSimulationMode(PlayerController.RuntimeSimulationMode.AuthoritativeLocomotion);
            _playerController.SetLocalPresentationEnabled(false);
            _playerController.SetLookDrivenCameraRootEnabled(false);
            SetCharacterControllerEnabled(true);
            SetOwnerTransformSyncEnabled(true);

            _serverLastProcessedInputSequence = Mathf.Max(
                _serverLastProcessedInputSequence,
                _bufferedInputProvider != null ? _bufferedInputProvider.LatestInputSequence : 0);
            _serverNextInputSequenceToProcess = _serverLastProcessedInputSequence + 1;
            ClearServerInputBuffer();
            _serverAuthoritativeState = _playerController.CaptureCurrentLocomotionState(
                _serverLastProcessedInputSequence,
                ResolveCurrentServerTick(),
                true);
            _hasServerAuthoritativeState = true;
        }

        private void EnterFullAuthoritativeFallbackMode()
        {
            _serverUsingLocomotionAuthority = false;
            _playerController.SetInputProviderOverride(_bufferedInputProvider);
            _playerController.SetSimulationMode(PlayerController.RuntimeSimulationMode.Full);
            _playerController.SetLocalPresentationEnabled(false);
            _playerController.SetLookDrivenCameraRootEnabled(true);
            SetCharacterControllerEnabled(true);
            SetOwnerTransformSyncEnabled(true);

            _serverLastProcessedInputSequence = Mathf.Max(
                _serverLastProcessedInputSequence,
                _bufferedInputProvider != null ? _bufferedInputProvider.LatestInputSequence : _serverLastProcessedInputSequence);
            _serverNextInputSequenceToProcess = _serverLastProcessedInputSequence + 1;
            ClearServerInputBuffer();
            _serverAuthoritativeState = _playerController.CaptureCurrentLocomotionState(
                _serverLastProcessedInputSequence,
                ResolveCurrentServerTick(),
                false);
            _hasServerAuthoritativeState = true;
        }

        private void StoreClientInput(in MultiplayerLocomotionInput input, bool wasPredicted)
        {
            int index = PositiveModulo(input.InputSequence, PredictionBufferSize);
            _clientInputHistory[index] = new ClientInputHistoryEntry
            {
                Sequence = input.InputSequence,
                Input = input,
                WasPredicted = wasPredicted
            };
        }

        private bool TryGetClientInput(int sequence, out ClientInputHistoryEntry entry)
        {
            int index = PositiveModulo(sequence, PredictionBufferSize);
            entry = _clientInputHistory[index];
            return entry.Sequence == sequence;
        }

        private void StoreClientPredictedState(int sequence, in MultiplayerLocomotionState state)
        {
            int index = PositiveModulo(sequence, PredictionBufferSize);
            _clientPredictedStateHistory[index] = new ClientPredictedStateHistoryEntry
            {
                Sequence = sequence,
                State = state
            };
        }

        private bool TryGetClientPredictedState(int sequence, out MultiplayerLocomotionState state)
        {
            int index = PositiveModulo(sequence, PredictionBufferSize);
            ClientPredictedStateHistoryEntry entry = _clientPredictedStateHistory[index];
            if (entry.Sequence != sequence)
            {
                state = default;
                return false;
            }

            state = entry.State;
            return true;
        }

        private void StoreServerInput(in MultiplayerLocomotionInput input)
        {
            int index = PositiveModulo(input.InputSequence, PredictionBufferSize);
            _serverInputBuffer[index] = new ServerInputBufferEntry
            {
                Sequence = input.InputSequence,
                Input = input,
                IsSet = true
            };
        }

        private bool TryConsumeServerInput(int sequence, out MultiplayerLocomotionInput input)
        {
            int index = PositiveModulo(sequence, PredictionBufferSize);
            ServerInputBufferEntry entry = _serverInputBuffer[index];
            if (!entry.IsSet || entry.Sequence != sequence)
            {
                input = default;
                return false;
            }

            _serverInputBuffer[index] = default;
            input = entry.Input;
            return true;
        }

        private void ClearServerInputBuffer()
        {
            for (int i = 0; i < _serverInputBuffer.Length; i++)
            {
                _serverInputBuffer[i] = default;
            }
        }

        private void PushAuthoritativeStateToOwner(in MultiplayerLocomotionState state)
        {
            PushAuthoritativeLocomotionStateClientRpc(state, _authoritativeStateClientRpcParams);
        }

        private void ApplyLocomotionAnimator(float inputMagnitude)
        {
            if (_playerController == null || _playerController.Animator == null)
            {
                return;
            }

            _playerController.Animator.SetFloat(PlayerController.ANIM_PARAM_SPEED, Mathf.Clamp01(inputMagnitude));
        }

        private MultiplayerLocomotionState SimulateNetworkLocomotionTick(
            in MultiplayerLocomotionState currentState,
            in PlayerInputPacket input,
            float deltaTime,
            int inputSequence,
            int serverTick,
            bool allowsPrediction,
            bool updateAnimator)
        {
            return _playerController.SimulateLocomotionTickFromCurrent(
                currentState,
                input,
                deltaTime,
                inputSequence,
                serverTick,
                allowsPrediction,
                updateAnimator);
        }

        private void ResetRuntimeState()
        {
            _clientPredictedState = default;
            _serverAuthoritativeState = default;
            _hasClientPredictedState = false;
            _hasServerAuthoritativeState = false;
            _clientAllowsPrediction = true;
            _serverUsingLocomotionAuthority = false;
            _nextLocalInputSequence = 0;
            _lastAppliedAuthoritativeInputSequence = 0;
            _lastReceivedAuthoritativeServerTick = 0;
            _serverLatestReceivedInputSequence = 0;
            _serverLastProcessedInputSequence = 0;
            _serverNextInputSequenceToProcess = 1;
            _hasReceivedInitialAuthoritativeBaseline = false;
            _hasLoggedPredictionPathFallback = false;
            _nextClientPredictionTraceLogTime = 0f;
            _nextClientAuthoritativeTraceLogTime = 0f;

            for (int i = 0; i < _clientInputHistory.Length; i++)
            {
                _clientInputHistory[i] = default;
            }

            for (int i = 0; i < _clientPredictedStateHistory.Length; i++)
            {
                _clientPredictedStateHistory[i] = default;
            }

            ClearServerInputBuffer();
        }

        private float ResolveFixedDeltaTime()
        {
            if (NetworkManager != null && NetworkManager.NetworkTickSystem != null)
            {
                return NetworkManager.NetworkTickSystem.LocalTime.FixedDeltaTime;
            }

            return FallbackFixedDeltaTime;
        }

        private int ResolveCurrentServerTick()
        {
            if (NetworkManager != null && NetworkManager.NetworkTickSystem != null)
            {
                return NetworkManager.NetworkTickSystem.LocalTime.Tick;
            }

            return 0;
        }

        private void SetCharacterControllerEnabled(bool enabled)
        {
            if (_characterController == null)
            {
                return;
            }

            _characterController.enabled = enabled;
        }

        private void SetOwnerTransformSyncEnabled(bool enabled)
        {
            if (_networkTransform == null)
            {
                return;
            }

            _networkTransform.enabled = enabled;
        }

        private void RefreshAvatarDebugName()
        {
            if (NetworkManager == null)
            {
                return;
            }

            gameObject.name = OwnerClientId == NetworkManager.ServerClientId
                ? "hostPlayer"
                : "clientPlayer";
        }

        private static int PositiveModulo(int value, int modulo)
        {
            int result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private bool ShouldActivatePredictionRuntimePath()
        {
            return _locomotionRuntimePath == LocomotionRuntimePath.PredictionReconciliation
                   && IsPredictionRuntimePathReady();
        }

        private static bool IsPredictionRuntimePathReady()
        {
            return true;
        }

        private void LogPredictionPathFallbackIfNeeded()
        {
            if (_hasLoggedPredictionPathFallback
                || _locomotionRuntimePath != LocomotionRuntimePath.PredictionReconciliation
                || IsPredictionRuntimePathReady())
            {
                return;
            }

            _hasLoggedPredictionPathFallback = true;
            Debug.Log("MultiplayerPlayerAvatar: PredictionReconciliation path is selected but not active yet. Falling back to HostOnlyCharacterController for Phase 0.");
        }

        private void LogRuntimeRoleConfiguration()
        {
            Debug.Log(
                $"MultiplayerPlayerAvatar: role configured name={gameObject.name} selectedPath={_locomotionRuntimePath} activePrediction={ShouldActivatePredictionRuntimePath()} server={IsServer} owner={IsOwner} mode={_playerController.SimulationMode}");
        }

        private void LogClientPredictionTrace(in MultiplayerLocomotionInput input, in MultiplayerLocomotionState predictedState, bool canPredictThisTick)
        {
            if (!_enableClientPredictionTrace
                || !_tracePredictionTicks
                || !IsOwner
                || IsServer
                || !ShouldActivatePredictionRuntimePath()
                || Time.time < _nextClientPredictionTraceLogTime)
            {
                return;
            }

            bool isMoveActive = input.MoveDirection.sqrMagnitude > 0.0001f;
            if (!isMoveActive && input.Buttons == 0)
            {
                return;
            }

            _nextClientPredictionTraceLogTime = Time.time + _clientPredictionTraceLogInterval;
            Debug.Log(
                $"[MultiplayerClientMoveTrace] " +
                $"phase=predict " +
                $"name={gameObject.name} " +
                $"seq={input.InputSequence} " +
                $"allowsPrediction={canPredictThisTick} " +
                $"mode={_playerController.SimulationMode} " +
                $"inputMag={input.MoveDirection.magnitude:F3} " +
                $"input=({input.MoveDirection.x:F3},{input.MoveDirection.y:F3}) " +
                $"root=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}) " +
                $"predicted=({predictedState.Position.x:F3},{predictedState.Position.y:F3},{predictedState.Position.z:F3}) " +
                $"yaw={predictedState.Yaw:F1} " +
                $"planarSpeed={predictedState.PlanarVelocity.magnitude:F3} " +
                $"vY={predictedState.VerticalVelocity:F3} " +
                $"grounded={predictedState.IsGrounded} " +
                $"buttons={input.Buttons}");
        }

        private void LogClientAuthoritativeTrace(
            string phase,
            in MultiplayerLocomotionState authoritativeState,
            in MultiplayerLocomotionState comparedState,
            float positionError,
            float yawError,
            bool corrected,
            int replayCount)
        {
            if (!_enableClientPredictionTrace
                || !IsOwner
                || IsServer
                || !ShouldActivatePredictionRuntimePath())
            {
                return;
            }

            if ((phase == "deadzone" && !_traceDeadzonePackets)
                || (phase == "duplicate" && !_traceDuplicatePackets))
            {
                return;
            }

            bool shouldTrace = corrected
                               || authoritativeState.PlanarVelocity.sqrMagnitude > 0.0001f
                               || positionError >= _clientPredictionTraceErrorThreshold
                               || yawError >= OwnerYawCorrectionDeadzone;
            if (!shouldTrace || Time.time < _nextClientAuthoritativeTraceLogTime)
            {
                return;
            }

            _nextClientAuthoritativeTraceLogTime = Time.time + _clientPredictionTraceLogInterval;
            Debug.Log(
                $"[MultiplayerClientMoveTrace] " +
                $"phase={phase} " +
                $"name={gameObject.name} " +
                $"seq={authoritativeState.InputSequence} " +
                $"serverTick={authoritativeState.ServerTick} " +
                $"allowsPrediction={authoritativeState.AllowsPrediction} " +
                $"corrected={corrected} " +
                $"replayCount={replayCount} " +
                $"root=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}) " +
                $"authoritative=({authoritativeState.Position.x:F3},{authoritativeState.Position.y:F3},{authoritativeState.Position.z:F3}) " +
                $"compared=({comparedState.Position.x:F3},{comparedState.Position.y:F3},{comparedState.Position.z:F3}) " +
                $"posError={positionError:F3} " +
                $"yawError={yawError:F2} " +
                $"authYaw={authoritativeState.Yaw:F1} " +
                $"predYaw={comparedState.Yaw:F1} " +
                $"authPlanarSpeed={authoritativeState.PlanarVelocity.magnitude:F3} " +
                $"predPlanarSpeed={comparedState.PlanarVelocity.magnitude:F3}");
        }

        private static bool IsWithinOwnerCorrectionDeadzone(in MultiplayerLocomotionState predictedState, in MultiplayerLocomotionState authoritativeState)
        {
            float positionError = (authoritativeState.Position - predictedState.Position).sqrMagnitude;
            float yawError = Mathf.Abs(Mathf.DeltaAngle(predictedState.Yaw, authoritativeState.Yaw));
            return positionError <= OwnerPositionCorrectionDeadzone * OwnerPositionCorrectionDeadzone
                   && yawError <= OwnerYawCorrectionDeadzone;
        }

        private bool ShouldPredictLocomotionThisTick(in PlayerInputPacket input)
        {
            if (!ShouldActivatePredictionRuntimePath()
                || !_clientAllowsPrediction
                || _playerController == null
                || !_playerController.CanRunNetworkLocomotion)
            {
                return false;
            }

            return input.buttons == 0;
        }
    }
}
