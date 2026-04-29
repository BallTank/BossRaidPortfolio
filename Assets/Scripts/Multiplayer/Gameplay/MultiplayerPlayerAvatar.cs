using Core.Combat;
using System.Collections.Generic;
using Core.Boss;
using Core.GameFlow;
using Core.Player;
using Core.UI;
using System.Reflection;
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
        private const string ClientVisualInstanceName = "ClientVisual";
        private static readonly List<MultiplayerPlayerAvatar> _activeAvatars = new List<MultiplayerPlayerAvatar>(2);
        private readonly NetworkVariable<int> _replicatedHudCurrentHealth = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _replicatedHudMaxHealth = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _replicatedRetryReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

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
        private NetworkAnimator _networkAnimator;
        private PlayerVisual _hostVisual;
        private PlayerVisual _clientVisual;
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
        private int _lastAppliedClientAuthoritativeActionSequence;
        private int _lastAppliedClientReactionSequence;
        private int _serverLatestReceivedInputSequence;
        private int _serverLastProcessedInputSequence;
        private int _serverNextInputSequenceToProcess = 1;
        private int _nextLocalActionSequence;
        private HostPlayerState _hostPlayerState;
        private HostToClientPlayerReactionSnapshot _latestHostReactionSnapshot;
        private bool _hasLatestHostReactionSnapshot;
        private bool _hasBoundHostAuthorityHooks;
        private ClientToHostPlayerActionIntent _pendingApprovedComboActionIntent;
        private bool _hasPendingApprovedComboActionIntent;
        private bool _hasReceivedInitialAuthoritativeBaseline;
        private float _nextMovementPredictionDebugLogTime;
        private float _nextMovementCorrectionDebugLogTime;
        private byte _lastObservedActionButtons;
        private byte _lastBufferedActionButtons;
        private MultiplayerPlayerAvatar _hudPartnerAvatar;
        private CombatHUDController _hudController;
        private bool _didLogNonPlayerObjectSpawn;
        private bool _didInitializeVisualVariants;
        private bool _didWarnMissingClientVisualTemplate;

        public bool HasReplicatedHealthState => TryGetReplicatedHealth(out _, out _);
        public bool IsReplicatedDead => TryGetReplicatedHealth(out int currentHealth, out int maxHealth)
                                        && maxHealth > 0
                                        && currentHealth <= 0;
        public bool IsRetryReady => IsSpawned && _replicatedRetryReady.Value;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetActiveAvatarRegistry()
        {
            _activeAvatars.Clear();
        }

        private void Awake()
        {
            CacheComponents();
            EnsureVisualVariantsInitialized();
            DisableEmbeddedCameras();
        }

        private void LateUpdate()
        {
            EnsureRuntimeRoleConfiguration();
            RefreshLocalMultiplayerHud();
        }

        public override void OnDestroy()
        {
            UnregisterActiveAvatar(this);
            UnbindHostAuthorityHooks();
            UnregisterNetworkTick();
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            CacheComponents();
            // 런타임으로 생성된 플레이어 아바타만 활성화하고, 씬 템플릿(NetworkSceneObject)은 비활성화한다.
            if (NetworkObject == null || NetworkObject.IsSceneObject == true)
            {
                HandleNonPlayerObjectSpawn();
                return;
            }

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
            RegisterActiveAvatar(this);
            MultiplayerGameplaySceneCoordinator.EnsureCurrentGameplayScenePrepared();
            RefreshAvatarDebugName();
            ConfigureRuntimeRole();
            ConfigureHostAuthorityContracts();
            SyncReplicatedHudHealthState();
        }

        private void HandleNonPlayerObjectSpawn()
        {
            if (ShouldPreserveSceneTemplateAvatar())
            {
                LogNonPlayerObjectSpawn("preserve_visible_missing_runtime_prefabs");
                _localInputProvider?.SetRuntimeInputEnabled(false);
                _bufferedInputProvider?.Clear();
                _playerController?.SetInputProviderOverride(null);
                _playerController?.SetSimulationMode(PlayerController.RuntimeSimulationMode.Disabled);
                _playerController?.SetActionAuthorityMode(PlayerController.ActionAuthorityMode.RemoteDisplayOnly);
                _playerController?.SetLocalPresentationEnabled(false);
                _playerController?.SetLookDrivenCameraRootEnabled(false);
                SetCharacterControllerEnabled(false);
                SetOwnerTransformSyncEnabled(false);
                return;
            }

            bool shouldDespawnSceneObject = IsServer && NetworkObject != null && NetworkObject.IsSpawned;
            LogNonPlayerObjectSpawn(shouldDespawnSceneObject ? "despawn" : "set_inactive");
            _localInputProvider?.SetRuntimeInputEnabled(false);
            _bufferedInputProvider?.Clear();
            _playerController?.SetInputProviderOverride(null);
            _playerController?.SetSimulationMode(PlayerController.RuntimeSimulationMode.Disabled);
            _playerController?.SetActionAuthorityMode(PlayerController.ActionAuthorityMode.RemoteDisplayOnly);
            _playerController?.SetLocalPresentationEnabled(false);
            _playerController?.SetLookDrivenCameraRootEnabled(false);
            SetCharacterControllerEnabled(false);
            SetOwnerTransformSyncEnabled(false);

            if (shouldDespawnSceneObject)
            {
                NetworkObject.Despawn(true);
                return;
            }

            gameObject.SetActive(false);
        }

        public override void OnNetworkDespawn()
        {
            UnregisterActiveAvatar(this);
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

            _playerController?.SetActionAuthorityMode(PlayerController.ActionAuthorityMode.SoloLocal);
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
            ProcessBufferedActionIntentEdges(locomotionInput, input);
            _bufferedInputProvider.SetInput(locomotionInput);
            _serverLatestReceivedInputSequence = Mathf.Max(_serverLatestReceivedInputSequence, locomotionInput.InputSequence);
            StoreServerInput(locomotionInput);
        }

        [ServerRpc(Delivery = RpcDelivery.Reliable)]
        private void SubmitOwnerActionIntentServerRpc(ClientToHostPlayerActionIntent actionIntent, ServerRpcParams rpcParams = default)
        {
            _ = actionIntent;
            _ = rpcParams;
        }

        [ServerRpc(Delivery = RpcDelivery.Reliable)]
        private void SubmitRetryReadyServerRpc()
        {
            if (!IsSpawned)
            {
                return;
            }

            _replicatedRetryReady.Value = true;
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void PushAuthoritativeLocomotionStateClientRpc(MultiplayerLocomotionState state, ClientRpcParams clientRpcParams = default)
        {
            if (!IsSpawned || IsServer || !IsLocallyOwnedAvatar() || _playerController == null)
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
                SetClientPredictedState(state);
                _hasClientPredictedState = true;
                _hasReceivedInitialAuthoritativeBaseline = true;
                _clientAllowsPrediction = state.AllowsPrediction;
                _lastAppliedAuthoritativeInputSequence = Mathf.Max(_lastAppliedAuthoritativeInputSequence, state.InputSequence);
                StoreClientPredictedState(state.InputSequence, state);
                LogAuthoritativeMovementDebug("AuthBaseline", state, false, default, 0, ResolveLocomotionAnimatorMagnitude(state), "first authoritative snapshot");
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
                SetClientPredictedState(state);
                _lastAppliedAuthoritativeInputSequence = Mathf.Max(_lastAppliedAuthoritativeInputSequence, state.InputSequence);
                LogAuthoritativeMovementDebug("AuthDirect", state, false, default, 0, ResolveLocomotionAnimatorMagnitude(state), "prediction disabled");
                return;
            }

            bool hasPredictedState = TryGetClientPredictedState(state.InputSequence, out MultiplayerLocomotionState predictedState);
            if (hasPredictedState
                && IsWithinOwnerCorrectionDeadzone(predictedState, state))
            {
                _lastAppliedAuthoritativeInputSequence = Mathf.Max(_lastAppliedAuthoritativeInputSequence, state.InputSequence);
                LogAuthoritativeMovementDebug("AuthSkip", state, true, predictedState, 0, ResolveLocomotionAnimatorMagnitude(predictedState), "within correction deadzone");
                return;
            }

            _lastAppliedAuthoritativeInputSequence = Mathf.Max(_lastAppliedAuthoritativeInputSequence, state.InputSequence);
            _playerController.ApplyLocomotionState(state);

            MultiplayerLocomotionState replayState = state;
            float replayInputMagnitude = ResolveLocomotionAnimatorMagnitude(state);
            int replayCount = 0;
            for (int sequence = state.InputSequence + 1; sequence <= _nextLocalInputSequence; sequence++)
            {
                if (!TryGetClientInput(sequence, out ClientInputHistoryEntry inputEntry) || !inputEntry.WasPredicted)
                {
                    break;
                }

                replayCount++;
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

            SetClientPredictedState(replayState);
            ApplyLocomotionAnimator(replayInputMagnitude);
            LogAuthoritativeMovementDebug(
                "AuthReplay",
                state,
                hasPredictedState,
                predictedState,
                replayCount,
                replayInputMagnitude,
                hasPredictedState ? "correction + replay" : "prediction history miss");
        }

        [ClientRpc(Delivery = RpcDelivery.Reliable)]
        private void PushApprovedActionStartClientRpc(
            ClientToHostPlayerActionIntent actionIntent,
            HostPlayerState hostPlayerState,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsSpawned || IsServer || !IsLocallyOwnedAvatar() || _playerController == null)
            {
                return;
            }

            ApplyAuthoritativeActionStartLocally(actionIntent, hostPlayerState);
        }

        [ClientRpc(Delivery = RpcDelivery.Reliable)]
        private void PushReactionSnapshotClientRpc(
            HostToClientPlayerReactionSnapshot snapshot,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsSpawned || IsServer || !IsLocallyOwnedAvatar() || _playerController == null)
            {
                return;
            }

            ApplyAuthoritativeReactionLocally(snapshot);
        }

        [ClientRpc(Delivery = RpcDelivery.Reliable)]
        private void PushAttackHitFeedbackClientRpc(
            int totalDamage,
            int comboStep,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsSpawned || IsServer || !IsLocallyOwnedAvatar() || _playerController == null)
            {
                return;
            }

            _playerController.ApplyAuthoritativeAttackHudFeedback(totalDamage, comboStep);
        }

        private void CacheComponents()
        {
            _playerController = GetComponent<PlayerController>();
            _localInputProvider = GetComponent<LocalInputProvider>();
            _bufferedInputProvider = GetComponent<MultiplayerBufferedInputProvider>();
            _characterController = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            _networkTransform = GetComponent<NetworkTransform>();
            _networkAnimator = GetComponent<NetworkAnimator>();
        }

        private void EnsureVisualVariantsInitialized()
        {
            if (_didInitializeVisualVariants)
            {
                return;
            }

            CacheHostVisualReference();
            EnsureClientVisualVariant();
            _didInitializeVisualVariants = true;
        }

        private void CacheHostVisualReference()
        {
            if (_hostVisual != null)
            {
                return;
            }

            PlayerVisual[] visuals = GetComponentsInChildren<PlayerVisual>(true);
            for (int i = 0; i < visuals.Length; i++)
            {
                PlayerVisual candidate = visuals[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.transform == transform)
                {
                    continue;
                }

                if (candidate.name == ClientVisualInstanceName)
                {
                    continue;
                }

                _hostVisual = candidate;
                return;
            }
        }

        private void EnsureClientVisualVariant()
        {
            if (_clientVisual != null)
            {
                return;
            }

            GameObject clientVisualTemplatePrefab = ResolveClientVisualTemplatePrefab();
            if (clientVisualTemplatePrefab == null)
            {
                if (!_didWarnMissingClientVisualTemplate)
                {
                    _didWarnMissingClientVisualTemplate = true;
                    Debug.LogWarning($"MultiplayerPlayerAvatar '{name}' could not resolve a client visual template prefab.");
                }

                return;
            }

            PlayerVisual templateVisual = clientVisualTemplatePrefab.GetComponentInChildren<PlayerVisual>(true);
            if (templateVisual == null)
            {
                if (!_didWarnMissingClientVisualTemplate)
                {
                    _didWarnMissingClientVisualTemplate = true;
                    Debug.LogWarning($"MultiplayerPlayerAvatar '{name}' could not find a PlayerVisual child under '{clientVisualTemplatePrefab.name}'.");
                }

                return;
            }

            GameObject clientVisualInstance = Object.Instantiate(templateVisual.gameObject, transform, false);
            clientVisualInstance.name = ClientVisualInstanceName;
            clientVisualInstance.SetActive(false);
            _clientVisual = clientVisualInstance.GetComponent<PlayerVisual>();
            BindPresentationComponentReferences(clientVisualInstance.transform);
            DisableEmbeddedCameras(clientVisualInstance.transform);
        }

        private GameObject ResolveClientVisualTemplatePrefab()
        {
            if (MultiplayerRuntimeRoot.HasInstance && MultiplayerRuntimeRoot.Instance.ClientPlayerAvatarPrefab != null)
            {
                return MultiplayerRuntimeRoot.Instance.ClientPlayerAvatarPrefab;
            }

            MultiplayerRuntimeConfig runtimeConfig = MultiplayerRuntimeConfig.LoadFromResources();
            return runtimeConfig != null ? runtimeConfig.ClientPlayerAvatarPrefab : null;
        }

        private void BindPresentationComponentReferences(Transform visualRoot)
        {
            if (visualRoot == null || _playerController == null)
            {
                return;
            }

            MonoBehaviour[] presentationBehaviours = visualRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < presentationBehaviours.Length; i++)
            {
                MonoBehaviour behaviour = presentationBehaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                FieldInfo[] fields = behaviour.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    FieldInfo field = fields[fieldIndex];
                    if (!typeof(PlayerController).IsAssignableFrom(field.FieldType))
                    {
                        continue;
                    }

                    field.SetValue(behaviour, _playerController);
                }
            }
        }

        private void DisableEmbeddedCameras()
        {
            DisableEmbeddedCameras(transform);
        }

        private static void DisableEmbeddedCameras(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Camera[] childCameras = root.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < childCameras.Length; i++)
            {
                Camera childCamera = childCameras[i];
                if (childCamera == null)
                {
                    continue;
                }

                childCamera.gameObject.SetActive(false);
            }
        }

        private void ApplyVisualRole(bool useHostVisual)
        {
            EnsureVisualVariantsInitialized();

            if (_hostVisual != null)
            {
                _hostVisual.gameObject.SetActive(useHostVisual || _clientVisual == null);
            }

            if (_clientVisual != null)
            {
                _clientVisual.gameObject.SetActive(!useHostVisual);
            }

            _playerController?.RefreshVisualBindings();
            RebindAnimatorDrivers();
        }

        private void ApplyOwnershipVisualRole()
        {
            ApplyVisualRole(IsHostOwnedAvatar());
        }

        private void RebindAnimatorDrivers()
        {
            if (_playerController == null || _networkAnimator == null)
            {
                return;
            }

            Animator activeAnimator = _playerController.Animator;
            if (activeAnimator == null)
            {
                return;
            }

            _networkAnimator.Animator = activeAnimator;
        }

        private void LogNonPlayerObjectSpawn(string action)
        {
            if (_didLogNonPlayerObjectSpawn)
            {
                return;
            }

            _didLogNonPlayerObjectSpawn = true;
            Debug.Log(
                $"[MPDiag][SceneTemplate] name={gameObject.name} " +
                $"path={BuildHierarchyPath(transform)} " +
                $"isServer={IsServer} isClient={IsClient} " +
                $"isSpawned={(NetworkObject != null && NetworkObject.IsSpawned)} " +
                $"isSceneObject={(NetworkObject != null ? NetworkObject.IsSceneObject.ToString() : "n/a")} " +
                $"ownerClientId={(NetworkObject != null ? NetworkObject.OwnerClientId.ToString() : "n/a")} " +
                $"action={action}");
        }

        private static bool ShouldPreserveSceneTemplateAvatar()
        {
            MultiplayerRuntimeConfig runtimeConfig = MultiplayerRuntimeConfig.LoadFromResources();
            if (runtimeConfig != null && runtimeConfig.HasResolvedPlayerAvatarPrefabs)
            {
                return false;
            }

            if (runtimeConfig == null)
            {
                Debug.LogError($"Multiplayer runtime config asset is missing. Create or restore {MultiplayerRuntimeConfig.AssetPath}.");
                return true;
            }

            Debug.LogError(runtimeConfig.BuildValidationMessage());
            return true;
        }

        private static string BuildHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            string path = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }

        private void ConfigureRuntimeRole()
        {
            if (_playerController == null)
            {
                return;
            }

            bool isLocallyOwnedAvatar = IsLocallyOwnedAvatar();
            bool isHostOwnedAvatar = IsHostOwnedAvatar();

            ResetRuntimeState();
            _bufferedInputProvider?.Clear();
            _localInputProvider?.SetRuntimeInputEnabled(false);
            _playerController.SetInputProviderOverride(null);
            _playerController.SetSimulationMode(PlayerController.RuntimeSimulationMode.Disabled);
            _playerController.SetLocalPresentationEnabled(false);
            _playerController.SetLookDrivenCameraRootEnabled(false);

            if (IsServer && isHostOwnedAvatar)
            {
                ConfigureHostOwnedPlayer();
                return;
            }

            if (IsServer)
            {
                ConfigureHostAuthorityReplica();
                return;
            }

            if (isLocallyOwnedAvatar)
            {
                ConfigureClientOwnedPlayer();
                return;
            }

            ConfigureClientReplica();
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
            _playerController.AuthoritativeAttackStepStarted += HandleAuthoritativeAttackStepStarted;
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
            _playerController.AuthoritativeAttackStepStarted -= HandleAuthoritativeAttackStepStarted;
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
            _playerController.SetActionAuthorityMode(PlayerController.ActionAuthorityMode.HostAuthoritative);
            _playerController.SetLocalPresentationEnabled(true);
            _playerController.SetLookDrivenCameraRootEnabled(false);
            ApplyOwnershipVisualRole();
            SetCharacterControllerEnabled(true);
            SetOwnerTransformSyncEnabled(true);
            BindLocalPresentation();
        }

        private void ConfigureHostAuthorityReplica()
        {
            _health?.SetRuntimeWriteAuthority(true);
            _playerController.SetInputProviderOverride(_bufferedInputProvider);
            _playerController.SetActionAuthorityMode(PlayerController.ActionAuthorityMode.HostAuthoritative);
            _playerController.SetLocalPresentationEnabled(false);
            _playerController.SetLookDrivenCameraRootEnabled(false);
            ApplyOwnershipVisualRole();
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
            _playerController.SetActionAuthorityMode(PlayerController.ActionAuthorityMode.ClientOwnerProxy);
            _playerController.SetLocalPresentationEnabled(true);
            _playerController.SetLookDrivenCameraRootEnabled(false);
            ApplyOwnershipVisualRole();
            SetCharacterControllerEnabled(true);
            SetOwnerTransformSyncEnabled(false);
            _hasReceivedInitialAuthoritativeBaseline = false;
            _clientAllowsPrediction = false;
            SetClientPredictedState(_playerController.CaptureCurrentLocomotionState(0, 0, false));
            _hasClientPredictedState = true;
            BindLocalPresentation();
        }

        private void ConfigureClientReplica()
        {
            _health?.SetRuntimeWriteAuthority(false);
            _playerController.SetInputProviderOverride(null);
            _playerController.SetSimulationMode(PlayerController.RuntimeSimulationMode.Disabled);
            _playerController.SetActionAuthorityMode(PlayerController.ActionAuthorityMode.RemoteDisplayOnly);
            _playerController.SetLocalPresentationEnabled(false);
            _playerController.SetLookDrivenCameraRootEnabled(false);
            ApplyOwnershipVisualRole();
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

            EnsureRuntimeRoleConfiguration();

            bool isHostOwnedAvatar = IsHostOwnedAvatar();
            bool isLocallyOwnedAvatar = IsLocallyOwnedAvatar();

            if (IsServer)
            {
                HandleHostAuthorityStateTick();
            }

            if (IsServer && isHostOwnedAvatar)
            {
                HandleHostOwnedActionIntentTick();
            }

            if (IsServer && !isHostOwnedAvatar)
            {
                HandleServerAuthorityTick();
            }

            if (!IsServer && isLocallyOwnedAvatar)
            {
                HandleClientPredictionTick();
            }
        }

        private bool IsLocallyOwnedAvatar()
        {
            return NetworkManager != null
                   && OwnerClientId == NetworkManager.LocalClientId;
        }

        private bool IsHostOwnedAvatar()
        {
            return NetworkManager != null
                   && OwnerClientId == NetworkManager.ServerClientId;
        }

        private void EnsureRuntimeRoleConfiguration()
        {
            if (!IsSpawned || _playerController == null)
            {
                return;
            }

            if (NetworkObject == null)
            {
                return;
            }

            if (NetworkObject.IsSceneObject == true)
            {
                return;
            }

            if (RequiresRuntimeRoleRefresh())
            {
                ConfigureRuntimeRole();
                return;
            }

            if (ShouldOwnLocalPresentation()
                && MultiplayerLocalPlayerRegistry.LocalPlayer != _playerController)
            {
                BindLocalPresentation();
            }
        }

        private bool RequiresRuntimeRoleRefresh()
        {
            if (_playerController == null)
            {
                return false;
            }

            bool isHostOwnedAvatar = IsHostOwnedAvatar();
            bool isLocallyOwnedAvatar = IsLocallyOwnedAvatar();

            if (IsServer && isHostOwnedAvatar)
            {
                return _playerController.SimulationMode != PlayerController.RuntimeSimulationMode.Full
                       || _playerController.CurrentActionAuthorityMode != PlayerController.ActionAuthorityMode.HostAuthoritative
                       || !_playerController.IsLocalPresentationEnabled
                       || !ReferenceEquals(_playerController.InputProvider, _localInputProvider);
            }

            if (IsServer)
            {
                return _playerController.CurrentActionAuthorityMode != PlayerController.ActionAuthorityMode.HostAuthoritative
                       || _playerController.IsLocalPresentationEnabled
                       || !ReferenceEquals(_playerController.InputProvider, _bufferedInputProvider);
            }

            if (isLocallyOwnedAvatar)
            {
                return _playerController.SimulationMode != PlayerController.RuntimeSimulationMode.PredictedLocomotion
                       || _playerController.CurrentActionAuthorityMode != PlayerController.ActionAuthorityMode.ClientOwnerProxy
                       || !_playerController.IsLocalPresentationEnabled
                       || !ReferenceEquals(_playerController.InputProvider, _localInputProvider);
            }

            return _playerController.SimulationMode != PlayerController.RuntimeSimulationMode.Disabled
                   || _playerController.CurrentActionAuthorityMode != PlayerController.ActionAuthorityMode.RemoteDisplayOnly
                   || _playerController.IsLocalPresentationEnabled
                   || _playerController.InputProvider != null;
        }

        private bool ShouldOwnLocalPresentation()
        {
            return (IsServer && IsHostOwnedAvatar())
                   || (!IsServer && IsLocallyOwnedAvatar());
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
                SetClientPredictedState(_playerController.CaptureCurrentLocomotionState(0, 0, _clientAllowsPrediction));
                _hasClientPredictedState = true;
            }

            PlayerInputPacket input = _localInputProvider.GetInput();
            MultiplayerLocomotionInput locomotionInput = MultiplayerLocomotionInput.FromPlayerInputPacket(input, ++_nextLocalInputSequence);
            ObserveActionIntentEdges(input, submitToServer: true, sourceLabel: "client-owner");
            bool canPredictThisTick = _hasReceivedInitialAuthoritativeBaseline && ShouldPredictLocomotionThisTick(input);

            StoreClientInput(locomotionInput, canPredictThisTick);

            if (canPredictThisTick)
            {
                if (!_clientPredictedState.AllowsPrediction)
                {
                    SetClientPredictedState(_playerController.CaptureCurrentLocomotionState(
                        _lastAppliedAuthoritativeInputSequence,
                        _lastReceivedAuthoritativeServerTick,
                        true));
                }

                SetClientPredictedState(SimulateNetworkLocomotionTick(
                    _clientPredictedState,
                    input,
                    ResolveFixedDeltaTime(),
                    locomotionInput.InputSequence,
                    _lastReceivedAuthoritativeServerTick,
                    true,
                    true));
            }
            else
            {
                SetClientPredictedState(_playerController.CaptureCurrentLocomotionState(
                    locomotionInput.InputSequence,
                    _lastReceivedAuthoritativeServerTick,
                    false));
            }

            StoreClientPredictedState(locomotionInput.InputSequence, _clientPredictedState);
            LogClientPredictionDebug(input, locomotionInput.InputSequence, canPredictThisTick);

            SubmitOwnerInputServerRpc(locomotionInput);
        }

        private void HandleHostOwnedActionIntentTick()
        {
            if (_localInputProvider == null || _playerController == null)
            {
                return;
            }

            PlayerInputPacket input = _localInputProvider.GetInput();
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

            EmitActionIntentIfPressed(
                pressedActionEdges,
                ResolveActionIntentFacingYaw(input, InputFlag.Dash),
                InputFlag.Dash,
                submitToServer,
                sourceLabel);
            EmitActionIntentIfPressed(
                pressedActionEdges,
                ResolveActionIntentFacingYaw(input, InputFlag.Attack),
                InputFlag.Attack,
                submitToServer,
                sourceLabel);
        }

        private void EmitActionIntentIfPressed(byte pressedActionEdges, float facingYaw, InputFlag requestedFlag, bool submitToServer, string sourceLabel)
        {
            if ((pressedActionEdges & (byte)requestedFlag) == 0)
            {
                return;
            }

            ClientToHostPlayerActionIntent actionIntent = ClientToHostPlayerActionIntent.Create(
                requestedFlag,
                ++_nextLocalActionSequence,
                ResolveCurrentServerTick(),
                facingYaw);

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

            PlayerInputPacket input = locomotionInput.ToPlayerInputPacket();
            float facingYaw = ResolveActionIntentFacingYaw(input, requestedFlag);

            ClientToHostPlayerActionIntent actionIntent = ClientToHostPlayerActionIntent.Create(
                requestedFlag,
                locomotionInput.InputSequence,
                locomotionInput.InputSequence,
                facingYaw);

            ProcessHostValidatedActionIntent(actionIntent, OwnerClientId, "server-buffer");
        }

        private void ProcessHostValidatedActionIntent(in ClientToHostPlayerActionIntent actionIntent, ulong senderClientId, string sourceLabel)
        {
            if (!IsServer)
            {
                return;
            }

            bool isAccepted = false;
            bool isQueuedComboContinuation = false;
            bool isExecutedDashCancel = false;
            string rejectionReason;

            if (senderClientId != OwnerClientId)
            {
                rejectionReason = "sender-owner-mismatch";
            }
            else
            {
                isAccepted = _hostPlayerActionValidator.TryValidate(actionIntent, _playerController, out rejectionReason);
            }

            if (isAccepted
                && actionIntent.RequestedFlag == InputFlag.Attack)
            {
                bool isComboContinuationRequest = _playerController != null
                                                 && _playerController.StateMachine != null
                                                 && _playerController.StateMachine.CurrentState == _playerController.AttackState;

                if (isComboContinuationRequest)
                {
                    if (_playerController.TryQueueAuthoritativeAttackCombo(actionIntent.FacingYaw, out _))
                    {
                        _pendingApprovedComboActionIntent = actionIntent;
                        _hasPendingApprovedComboActionIntent = true;
                        isQueuedComboContinuation = true;
                    }
                    else
                    {
                        isAccepted = false;
                        rejectionReason = "combo-queue-failed";
                    }
                }
                else if (!_playerController.TryStartAuthoritativeAttackComboStep(0, 0f, actionIntent.FacingYaw))
                {
                    isAccepted = false;
                    rejectionReason = "attack-execute-failed";
                }
            }
            else if (isAccepted
                     && actionIntent.RequestedFlag == InputFlag.Dash
                     && _playerController != null
                     && _playerController.StateMachine != null
                     && _playerController.StateMachine.CurrentState == _playerController.AttackState)
            {
                ClearPendingApprovedComboActionIntent();
                if (!_playerController.TryStartAuthoritativeDash(0f, actionIntent.FacingYaw))
                {
                    isAccepted = false;
                    rejectionReason = "dash-execute-failed";
                }
                else
                {
                    isExecutedDashCancel = true;
                }
            }

            if (!isAccepted)
            {
                return;
            }

            if (isQueuedComboContinuation)
            {
                return;
            }

            int currentServerTick = ResolveCurrentServerTick();
            if (actionIntent.RequestedFlag == InputFlag.Attack)
            {
                ClearPendingApprovedComboActionIntent();
                _hostPlayerState.RecordAcceptedAction(actionIntent, _health, currentServerTick, 0);
            }
            else
            {
                _hostPlayerState.RecordAcceptedAction(actionIntent, _health, currentServerTick);
            }

            _hostPlayerReactionResolver.SyncRuntimeState(_playerController, _health, currentServerTick, ref _hostPlayerState);

            if (!IsHostOwnedAvatar()
                && (actionIntent.RequestedFlag == InputFlag.Attack
                    || isExecutedDashCancel))
            {
                PushApprovedActionStartClientRpc(actionIntent, _hostPlayerState, _authoritativeStateClientRpcParams);
            }
        }

        private void HandleAuthoritativeAttackStepStarted(int comboIndex)
        {
            if (!IsServer || !_hasPendingApprovedComboActionIntent || comboIndex <= 0)
            {
                return;
            }

            int currentServerTick = ResolveCurrentServerTick();
            _hostPlayerState.RecordAcceptedAction(_pendingApprovedComboActionIntent, _health, currentServerTick, comboIndex);
            _hostPlayerReactionResolver.SyncRuntimeState(_playerController, _health, currentServerTick, ref _hostPlayerState);

            if (!IsHostOwnedAvatar())
            {
                PushApprovedActionStartClientRpc(_pendingApprovedComboActionIntent, _hostPlayerState, _authoritativeStateClientRpcParams);
            }

            ClearPendingApprovedComboActionIntent();
        }

        private void HandleAttackDamageResolved(int totalDamage, int comboStep)
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
                _ = rawHitLogEntry;
            }

            if (!IsHostOwnedAvatar())
            {
                PushAttackHitFeedbackClientRpc(totalDamage, comboStep, _authoritativeStateClientRpcParams);
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

                if (snapshot.HasFlag(HostPlayerReactionFlags.InterruptedAction)
                    || snapshot.HasFlag(HostPlayerReactionFlags.Stun)
                    || snapshot.HasFlag(HostPlayerReactionFlags.Death))
                {
                    ClearPendingApprovedComboActionIntent();
                }

                if (!IsHostOwnedAvatar())
                {
                    PushReactionSnapshotClientRpc(snapshot, _authoritativeStateClientRpcParams);
                }
            }
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
                || !ShouldOwnLocalPresentation()
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

            bool isLocalHost = NetworkManager != null
                               && OwnerClientId == NetworkManager.ServerClientId;
            hudController.SetViewerRelativePortraitLayout(isLocalHost);

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

        public bool TryGetReplicatedHealth(out int currentHealth, out int maxHealth)
        {
            return TryResolveHudHealthValues(allowLocalFallback: IsServer, out currentHealth, out maxHealth);
        }

        public bool TryGetResultDeathState(out bool isDead)
        {
            if (TryGetReplicatedHealth(out int currentHealth, out int maxHealth))
            {
                isDead = maxHealth > 0 && currentHealth <= 0;
                return true;
            }

            if (_playerController != null
                && _playerController.StateMachine != null
                && _playerController.StateMachine.CurrentState == _playerController.DeadState)
            {
                isDead = true;
                return true;
            }

            if (IsServer && _health != null && _health.MaxHealth > 0)
            {
                isDead = _health.IsDead;
                return true;
            }

            isDead = false;
            return false;
        }

        public void SubmitRetryReadyIfOwner()
        {
            if (!IsSpawned || !ShouldOwnLocalPresentation() || _replicatedRetryReady.Value)
            {
                return;
            }

            if (IsServer)
            {
                _replicatedRetryReady.Value = true;
                return;
            }

            SubmitRetryReadyServerRpc();
        }

        public static int GetActiveAvatarCount()
        {
            PruneActiveAvatarRegistry();
            return _activeAvatars.Count;
        }

        public static bool TryGetActiveAvatar(int index, out MultiplayerPlayerAvatar avatar)
        {
            PruneActiveAvatarRegistry();
            if (index >= 0 && index < _activeAvatars.Count)
            {
                avatar = _activeAvatars[index];
                return avatar != null;
            }

            avatar = null;
            return false;
        }

        public static bool TryGetLocalAvatar(out MultiplayerPlayerAvatar avatar)
        {
            PruneActiveAvatarRegistry();

            for (int i = 0; i < _activeAvatars.Count; i++)
            {
                MultiplayerPlayerAvatar candidate = _activeAvatars[i];
                if (candidate == null
                    || !candidate.IsSpawned
                    || candidate.NetworkManager == null
                    || candidate.OwnerClientId != candidate.NetworkManager.LocalClientId)
                {
                    continue;
                }

                avatar = candidate;
                return true;
            }

            avatar = null;
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

            if (_playerController.ShouldUseFrameDrivenPredictedLocomotionAnimatorSpeed())
            {
                return;
            }

            _playerController.SetLocomotionAnimatorSpeed(inputMagnitude);
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

        private void SetClientPredictedState(in MultiplayerLocomotionState state)
        {
            _clientPredictedState = state;
            _playerController?.SetLatestPredictedPlanarSpeedMagnitude(state.PlanarVelocity.magnitude);
        }

        private void ClearClientPredictedState()
        {
            _clientPredictedState = default;
            _playerController?.ClearLatestPredictedPlanarSpeedMagnitude();
        }

        private void ResetRuntimeState()
        {
            ClearClientPredictedState();
            _serverAuthoritativeState = default;
            _hasClientPredictedState = false;
            _hasServerAuthoritativeState = false;
            _clientAllowsPrediction = true;
            _serverUsingLocomotionAuthority = false;
            _nextLocalInputSequence = 0;
            _nextLocalActionSequence = 0;
            _lastAppliedAuthoritativeInputSequence = 0;
            _lastReceivedAuthoritativeServerTick = 0;
            _lastAppliedClientAuthoritativeActionSequence = 0;
            _lastAppliedClientReactionSequence = 0;
            _serverLatestReceivedInputSequence = 0;
            _serverLastProcessedInputSequence = 0;
            _serverNextInputSequenceToProcess = 1;
            _hasReceivedInitialAuthoritativeBaseline = false;
            _hostPlayerState = default;
            _latestHostReactionSnapshot = default;
            _hasLatestHostReactionSnapshot = false;
            _pendingApprovedComboActionIntent = default;
            _hasPendingApprovedComboActionIntent = false;
            _lastObservedActionButtons = 0;
            _lastBufferedActionButtons = 0;
            _hudPartnerAvatar = null;
            _hudController = null;

            if (IsServer)
            {
                _replicatedRetryReady.Value = false;
            }

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

        private static void RegisterActiveAvatar(MultiplayerPlayerAvatar avatar)
        {
            if (avatar == null || _activeAvatars.Contains(avatar))
            {
                return;
            }

            _activeAvatars.Add(avatar);
        }

        private static void UnregisterActiveAvatar(MultiplayerPlayerAvatar avatar)
        {
            if (avatar == null)
            {
                return;
            }

            _activeAvatars.Remove(avatar);
        }

        private static void PruneActiveAvatarRegistry()
        {
            for (int i = _activeAvatars.Count - 1; i >= 0; i--)
            {
                MultiplayerPlayerAvatar avatar = _activeAvatars[i];
                if (avatar != null && avatar.IsSpawned)
                {
                    continue;
                }

                _activeAvatars.RemoveAt(i);
            }
        }

        private void ApplyAuthoritativeActionStartLocally(in ClientToHostPlayerActionIntent actionIntent, in HostPlayerState hostPlayerState)
        {
            if (actionIntent.ActionSequence <= _lastAppliedClientAuthoritativeActionSequence)
            {
                return;
            }

            _lastAppliedClientAuthoritativeActionSequence = actionIntent.ActionSequence;

            if (actionIntent.RequestedFlag != InputFlag.Attack)
            {
                if (actionIntent.RequestedFlag != InputFlag.Dash)
                {
                    return;
                }
            }

            float authoritativeElapsedTime = 0f;
            if (hostPlayerState.LastAcceptedActionStartTick > 0)
            {
                int elapsedTicks = Mathf.Max(0, ResolveCurrentServerTick() - hostPlayerState.LastAcceptedActionStartTick);
                authoritativeElapsedTime = elapsedTicks * ResolveFixedDeltaTime();
            }

            if (actionIntent.RequestedFlag == InputFlag.Attack)
            {
                int approvedComboIndex = hostPlayerState.LastAcceptedComboIndex >= 0
                    ? hostPlayerState.LastAcceptedComboIndex
                    : 0;
                bool started = _playerController.TryStartAuthoritativeAttackComboStep(approvedComboIndex, authoritativeElapsedTime, actionIntent.FacingYaw);
                _ = started;
                return;
            }

            bool dashStarted = _playerController.TryStartAuthoritativeDash(authoritativeElapsedTime, actionIntent.FacingYaw);
            _ = dashStarted;
        }

        private void ApplyAuthoritativeReactionLocally(in HostToClientPlayerReactionSnapshot snapshot)
        {
            if (!snapshot.IsValid || snapshot.ReactionSequence <= _lastAppliedClientReactionSequence)
            {
                return;
            }

            _lastAppliedClientReactionSequence = snapshot.ReactionSequence;
            _playerController.ApplyAuthoritativeReactionSnapshot(snapshot);
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

        private static bool IsWithinOwnerCorrectionDeadzone(in MultiplayerLocomotionState predictedState, in MultiplayerLocomotionState authoritativeState)
        {
            float positionError = (authoritativeState.Position - predictedState.Position).sqrMagnitude;
            float yawError = Mathf.Abs(Mathf.DeltaAngle(predictedState.Yaw, authoritativeState.Yaw));
            return positionError <= OwnerPositionCorrectionDeadzone * OwnerPositionCorrectionDeadzone
                   && yawError <= OwnerYawCorrectionDeadzone;
        }

        private bool TryBeginMovementPredictionDebugLog()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_playerController == null || !_playerController.EnableMovementDebugLog)
            {
                return false;
            }

            if (Time.time < _nextMovementPredictionDebugLogTime)
            {
                return false;
            }

            _nextMovementPredictionDebugLogTime = Time.time + _playerController.MovementDebugLogInterval;
            return true;
#else
            return false;
#endif
        }

        private bool TryBeginMovementCorrectionDebugLog()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_playerController == null || !_playerController.EnableMovementDebugLog)
            {
                return false;
            }

            if (Time.time < _nextMovementCorrectionDebugLogTime)
            {
                return false;
            }

            _nextMovementCorrectionDebugLogTime = Time.time + _playerController.MovementDebugLogInterval;
            return true;
#else
            return false;
#endif
        }

        private void LogClientPredictionDebug(in PlayerInputPacket input, int inputSequence, bool canPredictThisTick)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!TryBeginMovementPredictionDebugLog())
            {
                return;
            }

            Vector3 rootPosition = _playerController.transform.position;
            float rootYaw = _playerController.transform.eulerAngles.y;
            float animSpeed = _playerController.Animator != null
                ? _playerController.Animator.GetFloat(PlayerController.ANIM_PARAM_SPEED)
                : 0f;

            Debug.Log(
                $"[MoveDebug][ClientPredict] " +
                $"seq={inputSequence} " +
                $"predict={canPredictThisTick} " +
                $"input=({input.moveDir.x:F3},{input.moveDir.y:F3}) " +
                $"rootPos=({rootPosition.x:F3},{rootPosition.y:F3},{rootPosition.z:F3}) " +
                $"rootYaw={rootYaw:F3} " +
                $"predPos=({_clientPredictedState.Position.x:F3},{_clientPredictedState.Position.y:F3},{_clientPredictedState.Position.z:F3}) " +
                $"predYaw={_clientPredictedState.Yaw:F3} " +
                $"planarVel={_clientPredictedState.PlanarVelocity.magnitude:F3} " +
                $"animSpeed={animSpeed:F3} " +
                $"grounded={_clientPredictedState.IsGrounded} " +
                $"dash={_clientPredictedState.IsDashActive} " +
                $"state={_playerController.StateMachine?.CurrentState?.GetType().Name ?? "None"}");
#endif
        }

        private void LogAuthoritativeMovementDebug(
            string phase,
            in MultiplayerLocomotionState authoritativeState,
            bool hasPredictedState,
            in MultiplayerLocomotionState predictedState,
            int replayCount,
            float replayInputMagnitude,
            string reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!TryBeginMovementCorrectionDebugLog())
            {
                return;
            }

            Vector3 rootPosition = _playerController.transform.position;
            float rootYaw = _playerController.transform.eulerAngles.y;
            float animSpeed = _playerController.Animator != null
                ? _playerController.Animator.GetFloat(PlayerController.ANIM_PARAM_SPEED)
                : 0f;

            string predictedText = hasPredictedState
                ? $"predPos=({predictedState.Position.x:F3},{predictedState.Position.y:F3},{predictedState.Position.z:F3}) " +
                  $"predYaw={predictedState.Yaw:F3} " +
                  $"posDelta={(authoritativeState.Position - predictedState.Position).magnitude:F3} " +
                  $"yawDelta={Mathf.Abs(Mathf.DeltaAngle(predictedState.Yaw, authoritativeState.Yaw)):F3}"
                : "predPos=n/a predYaw=n/a posDelta=n/a yawDelta=n/a";

            Debug.Log(
                $"[MoveDebug][{phase}] " +
                $"seq={authoritativeState.InputSequence} " +
                $"tick={authoritativeState.ServerTick} " +
                $"allowPred={authoritativeState.AllowsPrediction} " +
                $"reason={reason} " +
                $"authPos=({authoritativeState.Position.x:F3},{authoritativeState.Position.y:F3},{authoritativeState.Position.z:F3}) " +
                $"authYaw={authoritativeState.Yaw:F3} " +
                $"{predictedText} " +
                $"replay={replayCount} " +
                $"replayMag={replayInputMagnitude:F3} " +
                $"rootPos=({rootPosition.x:F3},{rootPosition.y:F3},{rootPosition.z:F3}) " +
                $"rootYaw={rootYaw:F3} " +
                $"animSpeed={animSpeed:F3} " +
                $"state={_playerController.StateMachine?.CurrentState?.GetType().Name ?? "None"}");
#endif
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

        private static float ResolveActionIntentFacingYaw(in PlayerInputPacket input, InputFlag requestedFlag)
        {
            if (requestedFlag != InputFlag.Dash || input.moveDir.sqrMagnitude <= 0.0001f)
            {
                return input.lookYaw;
            }

            Quaternion lookRotation = Quaternion.Euler(0f, input.lookYaw, 0f);
            Vector3 camForward = lookRotation * Vector3.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = lookRotation * Vector3.right;
            camRight.y = 0f;
            camRight.Normalize();

            Vector3 moveDirection = (camForward * input.moveDir.y + camRight * input.moveDir.x).normalized;
            if (moveDirection.sqrMagnitude <= 0.0001f)
            {
                return input.lookYaw;
            }

            return Quaternion.LookRotation(moveDirection).eulerAngles.y;
        }

        private void ClearPendingApprovedComboActionIntent()
        {
            _pendingApprovedComboActionIntent = default;
            _hasPendingApprovedComboActionIntent = false;
        }
    }
}
