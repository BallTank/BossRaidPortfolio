using Core.Combat;
using Core.Boss;
using Core.GameFlow;
using System;
using Core.Player;
using Core.UI;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Core.Multiplayer
{
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
        private const float DisconnectProfileMoveThresholdSqr = 0.0001f;
        private static readonly int BossAnimatorSpeedParam = Animator.StringToHash("Speed");
        private readonly NetworkVariable<int> _replicatedHudCurrentHealth = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _replicatedHudMaxHealth = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        [Header("Action Authority Debug")]
        [SerializeField] private bool _enableActionAuthorityTrace = true;

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

        private struct BossPresentationSnapshot : INetworkSerializable
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public int StateHash;
            public float NormalizedTime;
            public float SpeedParameter;
            public float PlaybackSpeed;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Position);
                serializer.SerializeValue(ref Rotation);
                serializer.SerializeValue(ref StateHash);
                serializer.SerializeValue(ref NormalizedTime);
                serializer.SerializeValue(ref SpeedParameter);
                serializer.SerializeValue(ref PlaybackSpeed);
            }
        }

        private PlayerController _playerController;
        private LocalInputProvider _localInputProvider;
        private MultiplayerBufferedInputProvider _bufferedInputProvider;
        private CharacterController _characterController;
        private Health _health;
        private NetworkTransform _networkTransform;
        private BossController _bossController;
        private Animator _bossAnimator;
        private readonly HostPlayerActionValidator _hostPlayerActionValidator = new HostPlayerActionValidator();
        private readonly HostPlayerReactionResolver _hostPlayerReactionResolver = new HostPlayerReactionResolver();
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
        private int _nextLocalActionSequence;
        private HostPlayerState _hostPlayerState;
        private HostToClientPlayerReactionSnapshot _latestHostReactionSnapshot;
        private bool _hasLatestHostReactionSnapshot;
        private bool _hasBoundHostAuthorityHooks;
        private bool _hasReceivedInitialAuthoritativeBaseline;
        private byte _lastObservedActionButtons;
        private byte _lastBufferedActionButtons;
        private bool _disconnectProfileSawMoveInput;
        private bool _disconnectProfileSawActionButtons;
        private int _disconnectProfileLastInputSequence;
        private string _disconnectProfileLastSourceLabel = "none";
        private bool _hasLoggedDisconnectProfileBaseline;
        private string _lastLoggedDisconnectProfileLabel = string.Empty;
        private BossPresentationSnapshot _latestBossPresentationSnapshot;
        private bool _hasLatestBossPresentationSnapshot;
        private MultiplayerPlayerAvatar _hudPartnerAvatar;
        private CombatHUDController _hudController;

        private void Awake()
        {
            CacheComponents();
        }

        private void LateUpdate()
        {
            RefreshLocalMultiplayerHud();
        }

        public override void OnDestroy()
        {
            UnbindHostAuthorityHooks();
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
            ConfigureRuntimeRole();
            ConfigureHostAuthorityContracts();
            SyncReplicatedHudHealthState();
            TryLogDisconnectProfileBaseline("spawn");
        }

        public override void OnNetworkDespawn()
        {
            UnbindHostAuthorityHooks();
            UnregisterNetworkTick();
            _health?.ResetRuntimeWriteAuthority();

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

            PlayerInputPacket input = locomotionInput.ToPlayerInputPacket();
            TrackDisconnectInputProfile(input, locomotionInput.InputSequence, "server-buffer");
            ProcessBufferedActionIntentEdges(locomotionInput, input);
            _bufferedInputProvider.SetInput(locomotionInput);
            _serverLatestReceivedInputSequence = Mathf.Max(_serverLatestReceivedInputSequence, locomotionInput.InputSequence);
            StoreServerInput(locomotionInput);
        }

        [ServerRpc(Delivery = RpcDelivery.Reliable)]
        private void SubmitOwnerActionIntentServerRpc(ClientToHostPlayerActionIntent actionIntent, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            LogActionIntentRpcReceived(actionIntent, senderClientId, "client-owner");
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
            if (!_hasReceivedInitialAuthoritativeBaseline)
            {
                _playerController.ApplyLocomotionState(state);
                ApplyLocomotionAnimator(ResolveLocomotionAnimatorMagnitude(state));
                _clientPredictedState = state;
                _hasClientPredictedState = true;
                _hasReceivedInitialAuthoritativeBaseline = true;
                _clientAllowsPrediction = state.AllowsPrediction;
                _lastAppliedAuthoritativeInputSequence = Mathf.Max(_lastAppliedAuthoritativeInputSequence, state.InputSequence);
                StoreClientPredictedState(state.InputSequence, state);
                return;
            }

            _clientAllowsPrediction = state.AllowsPrediction;
            _hasClientPredictedState = true;

            if (state.AllowsPrediction && state.InputSequence <= _lastAppliedAuthoritativeInputSequence)
            {
                return;
            }

            if (!state.AllowsPrediction)
            {
                _playerController.ApplyLocomotionState(state);
                ApplyLocomotionAnimator(ResolveLocomotionAnimatorMagnitude(state));
                _clientPredictedState = state;
                _lastAppliedAuthoritativeInputSequence = Mathf.Max(_lastAppliedAuthoritativeInputSequence, state.InputSequence);
                return;
            }

            if (TryGetClientPredictedState(state.InputSequence, out MultiplayerLocomotionState predictedState)
                && IsWithinOwnerCorrectionDeadzone(predictedState, state))
            {
                _lastAppliedAuthoritativeInputSequence = Mathf.Max(_lastAppliedAuthoritativeInputSequence, state.InputSequence);
                return;
            }

            _lastAppliedAuthoritativeInputSequence = Mathf.Max(_lastAppliedAuthoritativeInputSequence, state.InputSequence);
            _playerController.ApplyLocomotionState(state);

            MultiplayerLocomotionState replayState = state;
            float replayInputMagnitude = ResolveLocomotionAnimatorMagnitude(state);
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

                replayInputMagnitude = ResolveLocomotionAnimatorMagnitude(
                    replayState,
                    inputEntry.Input.MoveDirection.magnitude);
            }

            _clientPredictedState = replayState;
            ApplyLocomotionAnimator(replayInputMagnitude);
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void PushBossPresentationSnapshotClientRpc(BossPresentationSnapshot snapshot, ClientRpcParams clientRpcParams = default)
        {
            if (!IsSpawned || IsServer || !IsOwner)
            {
                return;
            }

            _latestBossPresentationSnapshot = snapshot;
            _hasLatestBossPresentationSnapshot = true;
            ApplyBossPresentationSnapshot();
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

        private void ConfigureHostAuthorityContracts()
        {
            UnbindHostAuthorityHooks();
            _hostPlayerReactionResolver.Reset();
            _hostPlayerState = default;
            _latestHostReactionSnapshot = default;
            _hasLatestHostReactionSnapshot = false;

            if (!IsServer || _playerController == null)
            {
                return;
            }

            BindHostAuthorityHooks();
            _hostPlayerReactionResolver.SeedFromRuntime(_playerController, _health, ResolveCurrentServerTick(), ref _hostPlayerState);
        }

        private void BindHostAuthorityHooks()
        {
            if (_hasBoundHostAuthorityHooks || _playerController == null)
            {
                return;
            }

            _playerController.AttackDamageResolved += HandleAttackDamageResolved;
            _playerController.BossAttackResolved += HandleBossAttackResolved;
            _hasBoundHostAuthorityHooks = true;
        }

        private void UnbindHostAuthorityHooks()
        {
            if (!_hasBoundHostAuthorityHooks || _playerController == null)
            {
                _hasBoundHostAuthorityHooks = false;
                return;
            }

            _playerController.AttackDamageResolved -= HandleAttackDamageResolved;
            _playerController.BossAttackResolved -= HandleBossAttackResolved;
            _hasBoundHostAuthorityHooks = false;
        }

        private void ConfigureHostOwnedPlayer()
        {
            _health?.SetRuntimeWriteAuthority(true);
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
            _health?.SetRuntimeWriteAuthority(true);
            _playerController.SetInputProviderOverride(_bufferedInputProvider);
            _playerController.SetLocalPresentationEnabled(false);
            _playerController.SetLookDrivenCameraRootEnabled(false);
            SetCharacterControllerEnabled(true);
            SetOwnerTransformSyncEnabled(true);
            UpdateServerAuthorityMode(forceApply: true);
        }

        private void ConfigureClientOwnedPlayer()
        {
            _health?.SetRuntimeWriteAuthority(false);
            _localInputProvider?.SetLookAngles(transform.eulerAngles.y, _playerController.LatestLookPitch);
            _localInputProvider?.SetRuntimeInputEnabled(true);
            _playerController.SetInputProviderOverride(_localInputProvider);
            _playerController.SetSimulationMode(PlayerController.RuntimeSimulationMode.PredictedLocomotion);
            _playerController.SetLocalPresentationEnabled(true);
            _playerController.SetLookDrivenCameraRootEnabled(false);
            SetCharacterControllerEnabled(true);
            SetOwnerTransformSyncEnabled(false);
            _hasReceivedInitialAuthoritativeBaseline = false;
            _clientAllowsPrediction = false;
            _clientPredictedState = _playerController.CaptureCurrentLocomotionState(0, 0, false);
            _hasClientPredictedState = true;
            BindLocalPresentation();
        }

        private void ConfigureClientReplica()
        {
            _health?.SetRuntimeWriteAuthority(false);
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

            if (IsServer)
            {
                HandleHostAuthorityStateTick();
            }

            if (IsServer && IsOwner)
            {
                HandleHostOwnedActionIntentTick();
            }

            if (IsServer && !IsOwner)
            {
                HandleServerAuthorityTick();
                PushBossPresentationToOwnerClient();
            }

            if (IsOwner && !IsServer)
            {
                HandleClientPredictionTick();
            }
        }

        private void HandleHostAuthorityStateTick()
        {
            if (_playerController == null)
            {
                return;
            }

            _hostPlayerReactionResolver.SyncRuntimeState(_playerController, _health, ResolveCurrentServerTick(), ref _hostPlayerState);
            SyncReplicatedHudHealthState();
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
            TrackDisconnectInputProfile(input, locomotionInput.InputSequence, "client-owner");
            ObserveActionIntentEdges(input, submitToServer: true, sourceLabel: "client-owner");
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

            SubmitOwnerInputServerRpc(locomotionInput);
        }

        private void HandleHostOwnedActionIntentTick()
        {
            if (_localInputProvider == null || _playerController == null)
            {
                return;
            }

            PlayerInputPacket input = _localInputProvider.GetInput();
            TrackDisconnectInputProfile(input, ResolveCurrentServerTick(), "host-owner");
            ObserveActionIntentEdges(input, submitToServer: false, sourceLabel: "host-owner");
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
                        true);

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
                PlayerInputPacket latestInput = _bufferedInputProvider != null
                    ? _bufferedInputProvider.GetInput()
                    : default;
                int latestAuthoritativeSequence = _bufferedInputProvider != null
                    ? _bufferedInputProvider.LatestInputSequence
                    : _serverLastProcessedInputSequence;
                bool allowsOwnerPrediction = ShouldAllowFallbackPrediction(latestInput);

                _serverAuthoritativeState = _playerController.CaptureCurrentLocomotionState(
                    latestAuthoritativeSequence,
                    currentServerTick,
                    allowsOwnerPrediction);

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
            if (_playerController == null
                || !_playerController.CanRunNetworkLocomotion)
            {
                return false;
            }

            PlayerInputPacket latestInput = _bufferedInputProvider != null
                ? _bufferedInputProvider.GetInput()
                : default;

            return CanUsePredictedLocomotionButtons(latestInput.buttons);
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

            float inputMagnitude = _bufferedInputProvider != null
                ? _bufferedInputProvider.GetInput().moveDir.magnitude
                : 0f;
            ApplyLocomotionAnimator(
                ResolveLocomotionAnimatorMagnitude(_serverAuthoritativeState, inputMagnitude),
                forceLocomotionState: true);
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
                ShouldAllowFallbackPrediction(_bufferedInputProvider != null ? _bufferedInputProvider.GetInput() : default));
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

        private void ObserveActionIntentEdges(in PlayerInputPacket input, bool submitToServer, string sourceLabel)
        {
            byte currentActionButtons = (byte)(input.buttons & (byte)(InputFlag.Dash | InputFlag.Attack));
            byte pressedActionEdges = (byte)(currentActionButtons & ~_lastObservedActionButtons);
            _lastObservedActionButtons = currentActionButtons;

            if (pressedActionEdges == 0)
            {
                return;
            }

            EmitActionIntentIfPressed(pressedActionEdges, InputFlag.Dash, submitToServer, sourceLabel);
            EmitActionIntentIfPressed(pressedActionEdges, InputFlag.Attack, submitToServer, sourceLabel);
        }

        private void EmitActionIntentIfPressed(byte pressedActionEdges, InputFlag requestedFlag, bool submitToServer, string sourceLabel)
        {
            if ((pressedActionEdges & (byte)requestedFlag) == 0)
            {
                return;
            }

            ClientToHostPlayerActionIntent actionIntent = ClientToHostPlayerActionIntent.Create(
                requestedFlag,
                ++_nextLocalActionSequence,
                ResolveCurrentServerTick());

            LogObservedActionIntent(actionIntent, sourceLabel, submitToServer);

            if (submitToServer)
            {
                SubmitOwnerActionIntentServerRpc(actionIntent);
                return;
            }

            ProcessHostValidatedActionIntent(actionIntent, OwnerClientId, sourceLabel);
        }

        private void ProcessBufferedActionIntentEdges(in MultiplayerLocomotionInput locomotionInput, in PlayerInputPacket input)
        {
            byte currentActionButtons = (byte)(input.buttons & (byte)(InputFlag.Dash | InputFlag.Attack));
            byte pressedActionEdges = (byte)(currentActionButtons & ~_lastBufferedActionButtons);
            _lastBufferedActionButtons = currentActionButtons;

            if (pressedActionEdges == 0 || locomotionInput.InputSequence <= 0)
            {
                return;
            }

            ProcessBufferedActionIntentIfPressed(pressedActionEdges, InputFlag.Dash, locomotionInput);
            ProcessBufferedActionIntentIfPressed(pressedActionEdges, InputFlag.Attack, locomotionInput);
        }

        // remote action edge는 latest buffer snapshot이 아니라 Host receive 시점에서 바로 잡아야 덮어쓰기로 사라지지 않는다.
        private void ProcessBufferedActionIntentIfPressed(byte pressedActionEdges, InputFlag requestedFlag, in MultiplayerLocomotionInput locomotionInput)
        {
            if ((pressedActionEdges & (byte)requestedFlag) == 0)
            {
                return;
            }

            LogBufferedActionIntentEdge(requestedFlag, locomotionInput.InputSequence, locomotionInput.Buttons);

            ClientToHostPlayerActionIntent actionIntent = ClientToHostPlayerActionIntent.Create(
                requestedFlag,
                locomotionInput.InputSequence,
                locomotionInput.InputSequence);

            ProcessHostValidatedActionIntent(actionIntent, OwnerClientId, "server-buffer");
        }

        private void LogBufferedActionIntentEdge(InputFlag requestedFlag, int inputSequence, byte buttons)
        {
            if (!_enableActionAuthorityTrace)
            {
                return;
            }

            Debug.Log(
                $"[MultiplayerActionIntentTrace] " +
                $"phase=buffer-observe " +
                $"name={gameObject.name} " +
                $"source=server-buffer " +
                $"owner={OwnerClientId} " +
                $"objectId={NetworkObjectId} " +
                $"inputSeq={inputSequence} " +
                $"serverTick={ResolveCurrentServerTick()} " +
                $"flag={requestedFlag} " +
                $"buttons={buttons}");
        }

        private void ProcessHostValidatedActionIntent(in ClientToHostPlayerActionIntent actionIntent, ulong senderClientId, string sourceLabel)
        {
            if (!IsServer)
            {
                return;
            }

            bool isAccepted = false;
            string rejectionReason;

            if (senderClientId != OwnerClientId)
            {
                rejectionReason = "sender-owner-mismatch";
            }
            else
            {
                isAccepted = _hostPlayerActionValidator.TryValidate(actionIntent, _playerController, out rejectionReason);
            }

            LogValidatedActionIntent(actionIntent, senderClientId, sourceLabel, isAccepted, rejectionReason);

            if (!isAccepted)
            {
                return;
            }

            int currentServerTick = ResolveCurrentServerTick();
            _hostPlayerState.RecordAcceptedAction(actionIntent, _health, currentServerTick);
            LogHostAuthoritativeState(actionIntent, _hostPlayerState);
        }

        private void HandleAttackDamageResolved(int totalDamage)
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            int currentServerTick = ResolveCurrentServerTick();
            if (_hostPlayerReactionResolver.TryRecordDamageContribution(
                    OwnerClientId,
                    _hostPlayerState,
                    totalDamage,
                    currentServerTick,
                    out HostPlayerReactionResolver.RawHitLogEntry rawHitLogEntry))
            {
                LogHostDamageContribution(rawHitLogEntry);
            }
        }

        private void HandleBossAttackResolved(in BossAttackHitData hitData, BossAttackHitResolution resolution)
        {
            if (!IsSpawned || !IsServer || _playerController == null)
            {
                return;
            }

            if (_hostPlayerReactionResolver.TryResolveBossHit(
                    hitData,
                    resolution,
                    _playerController,
                    _health,
                    ResolveCurrentServerTick(),
                    ref _hostPlayerState,
                    out HostToClientPlayerReactionSnapshot snapshot))
            {
                _latestHostReactionSnapshot = snapshot;
                _hasLatestHostReactionSnapshot = snapshot.IsValid;
                LogHostReactionSnapshot(snapshot);
            }
        }

        private void PushBossPresentationToOwnerClient()
        {
            if (!TryCaptureBossPresentationSnapshot(out BossPresentationSnapshot snapshot))
            {
                return;
            }

            PushBossPresentationSnapshotClientRpc(snapshot, _authoritativeStateClientRpcParams);
        }

        private bool TryCaptureBossPresentationSnapshot(out BossPresentationSnapshot snapshot)
        {
            snapshot = default;

            if (!TryResolveBossPresentationRuntime())
            {
                return false;
            }

            snapshot.Position = _bossController.transform.position;
            snapshot.Rotation = _bossController.transform.rotation;
            snapshot.PlaybackSpeed = _bossAnimator != null ? Mathf.Max(0.01f, _bossAnimator.speed) : 1f;
            snapshot.SpeedParameter = _bossAnimator != null ? _bossAnimator.GetFloat(BossAnimatorSpeedParam) : 0f;

            if (_bossAnimator == null)
            {
                return true;
            }

            AnimatorStateInfo stateInfo = _bossAnimator.GetCurrentAnimatorStateInfo(0);
            if (_bossAnimator.IsInTransition(0))
            {
                AnimatorStateInfo nextStateInfo = _bossAnimator.GetNextAnimatorStateInfo(0);
                if (nextStateInfo.shortNameHash != 0)
                {
                    stateInfo = nextStateInfo;
                }
            }

            snapshot.StateHash = stateInfo.shortNameHash;
            snapshot.NormalizedTime = Mathf.Repeat(stateInfo.normalizedTime, 1f);
            return true;
        }

        private void ApplyBossPresentationSnapshot()
        {
            if (!_hasLatestBossPresentationSnapshot || !TryResolveBossPresentationRuntime())
            {
                return;
            }

            _bossController.transform.SetPositionAndRotation(
                _latestBossPresentationSnapshot.Position,
                _latestBossPresentationSnapshot.Rotation);

            if (_bossAnimator == null)
            {
                return;
            }

            _bossAnimator.speed = Mathf.Max(0.01f, _latestBossPresentationSnapshot.PlaybackSpeed);
            _bossAnimator.SetFloat(
                BossAnimatorSpeedParam,
                Mathf.Clamp01(_latestBossPresentationSnapshot.SpeedParameter));

            if (_latestBossPresentationSnapshot.StateHash == 0
                || !_bossAnimator.HasState(0, _latestBossPresentationSnapshot.StateHash))
            {
                return;
            }

            AnimatorStateInfo currentStateInfo = _bossAnimator.GetCurrentAnimatorStateInfo(0);
            float normalizedTimeDelta = Mathf.Abs(
                Mathf.Repeat(currentStateInfo.normalizedTime, 1f)
                - _latestBossPresentationSnapshot.NormalizedTime);

            if (currentStateInfo.shortNameHash != _latestBossPresentationSnapshot.StateHash
                || normalizedTimeDelta > 0.15f)
            {
                _bossAnimator.Play(
                    _latestBossPresentationSnapshot.StateHash,
                    0,
                    _latestBossPresentationSnapshot.NormalizedTime);
            }
        }

        private bool TryResolveBossPresentationRuntime()
        {
            if (_bossController == null)
            {
                _bossController = FindObjectOfType<BossController>();
            }

            if (_bossController == null)
            {
                _bossAnimator = null;
                return false;
            }

            if (_bossAnimator == null)
            {
                _bossAnimator = _bossController.Visual != null
                    ? _bossController.Visual.Animator
                    : null;
            }

            return true;
        }

        private void SyncReplicatedHudHealthState()
        {
            if (!IsServer || _health == null)
            {
                return;
            }

            _replicatedHudCurrentHealth.Value = Mathf.Max(0, _health.CurrentHealth);
            _replicatedHudMaxHealth.Value = Mathf.Max(0, _health.MaxHealth);
        }

        private void RefreshLocalMultiplayerHud()
        {
            if (!IsSpawned
                || !IsOwner
                || _playerController == null
                || !MultiplayerSessionService.HasInstance
                || !MultiplayerSessionService.Instance.HasActiveSession)
            {
                return;
            }

            CombatHUDController hudController = ResolveCombatHudController();
            if (hudController == null)
            {
                return;
            }

            if (TryResolveHudHealthValues(allowLocalFallback: true, out int localCurrentHealth, out int localMaxHealth))
            {
                hudController.SetPlayerHpNormalized(
                    localMaxHealth > 0 ? (float)localCurrentHealth / localMaxHealth : 0f,
                    localCurrentHealth,
                    localMaxHealth);
            }

            hudController.SetPlayerName(ResolveHudPlayerLabel(isLocalPlayer: true));

            MultiplayerPlayerAvatar partnerAvatar = ResolvePartnerAvatar();
            int partnerCurrentHealth = 0;
            int partnerMaxHealth = 0;
            bool hasPartner = partnerAvatar != null
                              && partnerAvatar.IsSpawned
                              && partnerAvatar.TryResolveHudHealthValues(
                                  allowLocalFallback: false,
                                  out partnerCurrentHealth,
                                  out partnerMaxHealth);

            hudController.SetPartnerHudVisible(hasPartner);
            if (!hasPartner)
            {
                return;
            }

            hudController.SetPartnerName(partnerAvatar.ResolveHudPlayerLabel(isLocalPlayer: false));
            hudController.SetPartnerHpNormalized(
                partnerMaxHealth > 0 ? (float)partnerCurrentHealth / partnerMaxHealth : 0f,
                partnerCurrentHealth,
                partnerMaxHealth);
        }

        private CombatHUDController ResolveCombatHudController()
        {
            if (_hudController != null)
            {
                return _hudController;
            }

            _hudController = _playerController.CombatHUD;
            if (_hudController == null)
            {
                _hudController = FindObjectOfType<CombatHUDController>();
            }

            return _hudController;
        }

        private MultiplayerPlayerAvatar ResolvePartnerAvatar()
        {
            if (_hudPartnerAvatar != null
                && _hudPartnerAvatar != this
                && _hudPartnerAvatar.IsSpawned)
            {
                return _hudPartnerAvatar;
            }

            MultiplayerPlayerAvatar[] avatars = FindObjectsByType<MultiplayerPlayerAvatar>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < avatars.Length; i++)
            {
                MultiplayerPlayerAvatar avatar = avatars[i];
                if (avatar == null || avatar == this || !avatar.IsSpawned)
                {
                    continue;
                }

                _hudPartnerAvatar = avatar;
                return _hudPartnerAvatar;
            }

            _hudPartnerAvatar = null;
            return null;
        }

        private bool TryResolveHudHealthValues(bool allowLocalFallback, out int currentHealth, out int maxHealth)
        {
            maxHealth = _replicatedHudMaxHealth.Value;
            if (maxHealth > 0)
            {
                currentHealth = Mathf.Clamp(_replicatedHudCurrentHealth.Value, 0, maxHealth);
                return true;
            }

            if (allowLocalFallback && _health != null && _health.MaxHealth > 0)
            {
                currentHealth = Mathf.Clamp(_health.CurrentHealth, 0, _health.MaxHealth);
                maxHealth = _health.MaxHealth;
                return true;
            }

            currentHealth = 0;
            maxHealth = 0;
            return false;
        }

        private string ResolveHudPlayerLabel(bool isLocalPlayer)
        {
            string baseLabel = OwnerClientId == NetworkManager.ServerClientId
                ? "Host"
                : "Client";

            return isLocalPlayer ? $"{baseLabel}(me)" : baseLabel;
        }

        private void ApplyLocomotionAnimator(float inputMagnitude, bool forceLocomotionState = false)
        {
            if (_playerController == null || _playerController.Animator == null)
            {
                return;
            }

            if (forceLocomotionState)
            {
                _playerController.Animator.CrossFade(PlayerController.ANIM_STATE_LOCOMOTION, 0.1f);
            }

            _playerController.Animator.SetFloat(PlayerController.ANIM_PARAM_SPEED, Mathf.Clamp01(inputMagnitude));
        }

        private float ResolveLocomotionAnimatorMagnitude(in MultiplayerLocomotionState state, float inputMagnitude = 0f)
        {
            if (_playerController == null)
            {
                return Mathf.Clamp01(inputMagnitude);
            }

            float normalizedPlanarSpeed = _playerController.MoveSpeed > 0.0001f
                ? state.PlanarVelocity.magnitude / _playerController.MoveSpeed
                : 0f;

            return Mathf.Clamp01(Mathf.Max(inputMagnitude, normalizedPlanarSpeed));
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
            _nextLocalActionSequence = 0;
            _lastAppliedAuthoritativeInputSequence = 0;
            _lastReceivedAuthoritativeServerTick = 0;
            _serverLatestReceivedInputSequence = 0;
            _serverLastProcessedInputSequence = 0;
            _serverNextInputSequenceToProcess = 1;
            _hasReceivedInitialAuthoritativeBaseline = false;
            _hostPlayerState = default;
            _latestHostReactionSnapshot = default;
            _hasLatestHostReactionSnapshot = false;
            _lastObservedActionButtons = 0;
            _lastBufferedActionButtons = 0;
            _disconnectProfileSawMoveInput = false;
            _disconnectProfileSawActionButtons = false;
            _disconnectProfileLastInputSequence = 0;
            _disconnectProfileLastSourceLabel = "none";
            _hasLoggedDisconnectProfileBaseline = false;
            _lastLoggedDisconnectProfileLabel = string.Empty;
            _bossController = null;
            _bossAnimator = null;
            _latestBossPresentationSnapshot = default;
            _hasLatestBossPresentationSnapshot = false;
            _hudPartnerAvatar = null;
            _hudController = null;

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

        public bool HasNoActionDisconnectProfile
        {
            get { return !_disconnectProfileSawActionButtons; }
        }

        public string BuildConnectionDebugProfile()
        {
            return
                $"name={gameObject.name} " +
                $"owner={OwnerClientId} " +
                $"objectId={NetworkObjectId} " +
                $"server={IsServer} " +
                $"ownerLocal={IsOwner} " +
                $"inputProfile={ResolveDisconnectInputProfileLabel()} " +
                $"lastInputSeq={_disconnectProfileLastInputSequence} " +
                $"lastSource={_disconnectProfileLastSourceLabel}";
        }

        private static int PositiveModulo(int value, int modulo)
        {
            int result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private void TrackDisconnectInputProfile(in PlayerInputPacket input, int inputSequence, string sourceLabel)
        {
            TryLogDisconnectProfileBaseline(sourceLabel);
            string previousProfile = ResolveDisconnectInputProfileLabel();

            if (inputSequence < _disconnectProfileLastInputSequence)
            {
                return;
            }

            _disconnectProfileLastInputSequence = inputSequence;
            _disconnectProfileLastSourceLabel = sourceLabel;

            if (input.moveDir.sqrMagnitude > DisconnectProfileMoveThresholdSqr)
            {
                _disconnectProfileSawMoveInput = true;
            }

            if ((input.buttons & (byte)(InputFlag.Dash | InputFlag.Attack | InputFlag.Jump)) != 0)
            {
                _disconnectProfileSawActionButtons = true;
            }

            TryLogDisconnectProfileTransition(previousProfile, ResolveDisconnectInputProfileLabel(), sourceLabel, inputSequence);
        }

        private string ResolveDisconnectInputProfileLabel()
        {
            if (_disconnectProfileSawActionButtons)
            {
                return "action-observed";
            }

            return _disconnectProfileSawMoveInput ? "idle-walk-only" : "idle-only";
        }

        private void TryLogDisconnectProfileBaseline(string sourceLabel)
        {
            if (_hasLoggedDisconnectProfileBaseline)
            {
                return;
            }

            string currentProfile = ResolveDisconnectInputProfileLabel();
            if (!MultiplayerSessionService.TryLogAvatarConnectionProfileBaseline(
                this,
                currentProfile,
                sourceLabel,
                _disconnectProfileLastInputSequence))
            {
                return;
            }

            _hasLoggedDisconnectProfileBaseline = true;
            _lastLoggedDisconnectProfileLabel = currentProfile;
        }

        private void TryLogDisconnectProfileTransition(
            string previousProfile,
            string currentProfile,
            string sourceLabel,
            int inputSequence)
        {
            if (string.Equals(previousProfile, currentProfile, StringComparison.Ordinal)
                || string.Equals(_lastLoggedDisconnectProfileLabel, currentProfile, StringComparison.Ordinal))
            {
                return;
            }

            if (!MultiplayerSessionService.TryLogAvatarConnectionProfileTransition(
                this,
                previousProfile,
                currentProfile,
                sourceLabel,
                inputSequence))
            {
                return;
            }

            _lastLoggedDisconnectProfileLabel = currentProfile;
        }

        private void LogRuntimeRoleConfiguration()
        {
            Debug.Log(
                $"MultiplayerPlayerAvatar: role configured name={gameObject.name} predictionPath=PredictionReconciliation server={IsServer} owner={IsOwner} mode={_playerController.SimulationMode}");
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
            if (!_clientAllowsPrediction
                || _playerController == null
                || !_playerController.CanRunNetworkLocomotion)
            {
                return false;
            }

            return CanUsePredictedLocomotionButtons(input.buttons);
        }

        private bool ShouldAllowFallbackPrediction(in PlayerInputPacket latestInput)
        {
            if (_playerController == null || _playerController.StateMachine == null)
            {
                return false;
            }

            if (_playerController.StateMachine.CurrentState == _playerController.DashState)
            {
                return true;
            }

            return _playerController.StateMachine.CurrentState == _playerController.MoveState
                   && CanUsePredictedLocomotionButtons(latestInput.buttons);
        }

        private static bool CanUsePredictedLocomotionButtons(byte buttons)
        {
            return (buttons & (byte)(InputFlag.Attack | InputFlag.Jump)) == 0;
        }

        private void LogObservedActionIntent(in ClientToHostPlayerActionIntent actionIntent, string sourceLabel, bool submittedToServer)
        {
            if (!_enableActionAuthorityTrace)
            {
                return;
            }

            Debug.Log(
                $"[MultiplayerActionIntentTrace] " +
                $"phase=observe " +
                $"name={gameObject.name} " +
                $"source={sourceLabel} " +
                $"submit={submittedToServer} " +
                $"seq={actionIntent.ActionSequence} " +
                $"tick={actionIntent.ClientTick} " +
                $"flag={actionIntent.RequestedFlag}");
        }

        private void LogActionIntentRpcReceived(in ClientToHostPlayerActionIntent actionIntent, ulong senderClientId, string sourceLabel)
        {
            if (!_enableActionAuthorityTrace)
            {
                return;
            }

            Debug.Log(
                $"[MultiplayerActionIntentTrace] " +
                $"phase=rpc-received " +
                $"name={gameObject.name} " +
                $"source={sourceLabel} " +
                $"sender={senderClientId} " +
                $"owner={OwnerClientId} " +
                $"objectId={NetworkObjectId} " +
                $"seq={actionIntent.ActionSequence} " +
                $"tick={actionIntent.ClientTick} " +
                $"flag={actionIntent.RequestedFlag}");
        }

        private void LogValidatedActionIntent(in ClientToHostPlayerActionIntent actionIntent, ulong senderClientId, string sourceLabel, bool isAccepted, string rejectionReason)
        {
            if (!_enableActionAuthorityTrace)
            {
                return;
            }

            Debug.Log(
                $"[MultiplayerActionIntentTrace] " +
                $"phase=validate " +
                $"name={gameObject.name} " +
                $"source={sourceLabel} " +
                $"sender={senderClientId} " +
                $"accepted={isAccepted} " +
                $"reason={(string.IsNullOrEmpty(rejectionReason) ? "none" : rejectionReason)} " +
                $"seq={actionIntent.ActionSequence} " +
                $"tick={actionIntent.ClientTick} " +
                $"flag={actionIntent.RequestedFlag}");
        }

        private void LogHostAuthoritativeState(in ClientToHostPlayerActionIntent actionIntent, in HostPlayerState hostPlayerState)
        {
            if (!_enableActionAuthorityTrace)
            {
                return;
            }

            Debug.Log(
                $"[MultiplayerActionIntentTrace] " +
                $"phase=host-state " +
                $"name={gameObject.name} " +
                $"seq={actionIntent.ActionSequence} " +
                $"flag={actionIntent.RequestedFlag} " +
                $"active={hostPlayerState.ActiveActionFlag} " +
                $"acceptedAction={hostPlayerState.LastAcceptedActionFlag} " +
                $"startTick={hostPlayerState.LastAcceptedActionStartTick} " +
                $"hp={hostPlayerState.CurrentHealth}/{hostPlayerState.MaxHealth}");
        }

        private void LogHostReactionSnapshot(in HostToClientPlayerReactionSnapshot snapshot)
        {
            if (!_enableActionAuthorityTrace)
            {
                return;
            }

            Debug.Log(
                $"[MultiplayerActionIntentTrace] " +
                $"phase=reaction-snapshot " +
                $"name={gameObject.name} " +
                $"reactionSeq={snapshot.ReactionSequence} " +
                $"serverTick={snapshot.ServerTick} " +
                $"flags={snapshot.ReactionFlags} " +
                $"damage={snapshot.DamageAmount} " +
                $"hp={snapshot.ResultHealth}/{snapshot.MaxHealth} " +
                $"sourceHit={snapshot.SourceHitTypeValue} " +
                $"interrupted={snapshot.InterruptedActionFlag}");
        }

        private void LogHostDamageContribution(in HostPlayerReactionResolver.RawHitLogEntry rawHitLogEntry)
        {
            if (!_enableActionAuthorityTrace)
            {
                return;
            }

            Debug.Log(
                $"[MultiplayerActionIntentTrace] " +
                $"phase=raw-hit-log " +
                $"name={gameObject.name} " +
                $"dealer={rawHitLogEntry.DealerClientId} " +
                $"action={rawHitLogEntry.ActionFlag} " +
                $"seq={rawHitLogEntry.ActionSequence} " +
                $"damage={rawHitLogEntry.DamageAmount} " +
                $"serverTick={rawHitLogEntry.ServerTick}");
        }
    }
}
