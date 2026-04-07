using System.Collections.Generic;
using Core.Boss;
using Core.UI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerBossAuthorityBridge : MonoBehaviour
    {
        private const string BossAuthorityMessageName = "boss-authority-state";
        private const string BossEffectMessageName = "boss-replicated-effect";
        private const float FallbackFixedDeltaTime = 1f / 30f;
        private const float MinimumPlaybackSpeed = 0.01f;
        private const string BasicAttackStateName = "Basic Attack";
        private const string LungeAttackStateName = "Lunge Attack";
        private const string LegacyClawAttackStateName = "Claw Attack";
        private const string FlameAttackStateName = "Flame Attack";
        private const string FireballShootStateName = "Fireball Shoot";
        private const string TakeOffStateName = "takeOff";
        private const string TakeOffAltStateName = "TakeOff";
        private readonly List<ulong> _remoteClientIds = new List<ulong>(2);
        private readonly List<BossReplicatedEffectEvent> _pendingOutgoingEffectBatch = new List<BossReplicatedEffectEvent>(8);
        private readonly Queue<BossReplicatedEffectEvent> _pendingReceivedEffectEvents = new Queue<BossReplicatedEffectEvent>(8);

        private struct BossAuthoritativeStateMessage : INetworkSerializable
        {
            public int ServerTick;
            public BossAuthoritativeState State;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref ServerTick);
                serializer.SerializeValue(ref State);
            }
        }

        private NetworkManager _networkManager;
        private BossController _bossController;
        private BossVisual _bossVisual;
        private Animator _bossAnimator;
        private bool _isTickRegistered;
        private bool _isMessageHandlerRegistered;
        private bool _isEffectMessageHandlerRegistered;
        private bool _hasLatestReceivedMessage;
        private BossAuthoritativeStateMessage _latestReceivedMessage;
        private bool _hasConsumedReceivedMessage;
        private int _lastConsumedServerTick;
        private int _lastQueuedEffectSequenceId;
        private int _lastConsumedEffectSequenceId;
        private bool _hasLastAppliedState;
        private BossAuthoritativeState _lastAppliedState;
        private int _lastAppliedServerTick;
        private bool _hasStablePresentationPlanarSpeed;
        private float _stablePresentationPlanarSpeed;
        private bool _searchingUiActive;
        private CombatHUDController _combatHudController;
        private bool _hasAppliedBossHudState;
        private int _lastBossHudCurrentHealth = int.MinValue;
        private int _lastBossHudMaxHealth = int.MinValue;
        private bool _hasLatestBossState;
        private BossAuthoritativeState _latestBossState;

        public bool HasLatestBossState => _hasLatestBossState;
        public bool IsBossDead => _hasLatestBossState && _latestBossState.IsDead;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
        }

        public bool TryGetLatestBossState(out BossAuthoritativeState state)
        {
            if (_hasLatestBossState)
            {
                state = _latestBossState;
                return true;
            }

            state = default;
            return false;
        }

        private void Update()
        {
            RefreshRegistrations();

            if (!ShouldProcessBossBridge())
            {
                ResetPresentationState();
                return;
            }

            if (!_networkManager.IsServer && ShouldConsumeLatestReceivedMessage())
            {
                ApplyLatestReceivedState();
            }

            if (!_networkManager.IsServer)
            {
                ApplyPendingReceivedEffects();
            }
        }

        private void OnDestroy()
        {
            UnregisterNetworkTick();
            UnregisterMessageHandler();
            UnregisterEffectMessageHandler();
        }

        private void RefreshRegistrations()
        {
            if (_networkManager == null)
            {
                _networkManager = GetComponent<NetworkManager>();
            }

            bool isNetworkActive = _networkManager != null
                                   && (_networkManager.IsServer || _networkManager.IsClient)
                                   && !_networkManager.ShutdownInProgress;

            if (!isNetworkActive)
            {
                UnregisterNetworkTick();
                UnregisterMessageHandler();
                UnregisterEffectMessageHandler();
                return;
            }

            RegisterMessageHandler();
            RegisterEffectMessageHandler();
            RegisterNetworkTick();
        }

        private void RegisterNetworkTick()
        {
            if (_isTickRegistered || _networkManager == null || _networkManager.NetworkTickSystem == null)
            {
                return;
            }

            _networkManager.NetworkTickSystem.Tick += HandleNetworkTick;
            _isTickRegistered = true;
        }

        private void UnregisterNetworkTick()
        {
            if (!_isTickRegistered)
            {
                return;
            }

            if (_networkManager != null && _networkManager.NetworkTickSystem != null)
            {
                _networkManager.NetworkTickSystem.Tick -= HandleNetworkTick;
            }

            _isTickRegistered = false;
        }

        private void RegisterMessageHandler()
        {
            if (_isMessageHandlerRegistered
                || _networkManager == null
                || _networkManager.CustomMessagingManager == null
                || !_networkManager.IsClient)
            {
                return;
            }

            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                BossAuthorityMessageName,
                HandleBossAuthoritativeStateMessage);
            _isMessageHandlerRegistered = true;
        }

        private void UnregisterMessageHandler()
        {
            if (!_isMessageHandlerRegistered)
            {
                return;
            }

            if (_networkManager != null && _networkManager.CustomMessagingManager != null)
            {
                _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(BossAuthorityMessageName);
            }

            _isMessageHandlerRegistered = false;
        }

        private void RegisterEffectMessageHandler()
        {
            if (_isEffectMessageHandlerRegistered
                || _networkManager == null
                || _networkManager.CustomMessagingManager == null
                || !_networkManager.IsClient)
            {
                return;
            }

            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                BossEffectMessageName,
                HandleBossReplicatedEffectMessage);
            _isEffectMessageHandlerRegistered = true;
        }

        private void UnregisterEffectMessageHandler()
        {
            if (!_isEffectMessageHandlerRegistered)
            {
                return;
            }

            if (_networkManager != null && _networkManager.CustomMessagingManager != null)
            {
                _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(BossEffectMessageName);
            }

            _isEffectMessageHandlerRegistered = false;
        }

        private void HandleNetworkTick()
        {
            if (!ShouldProcessBossBridge() || _networkManager == null || !_networkManager.IsServer)
            {
                return;
            }

            PushAuthoritativeBossStateToClients();
            PushReplicatedBossEffectsToClients();
        }

        private bool ShouldProcessBossBridge()
        {
            if (_networkManager == null
                || (!_networkManager.IsServer && !_networkManager.IsClient)
                || _networkManager.ShutdownInProgress
                || !MultiplayerSessionService.HasInstance
                || !MultiplayerSessionService.Instance.HasActiveSession)
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid()
                   && string.Equals(
                       activeScene.path,
                       MultiplayerScenePaths.GamePlayScenePath,
                       System.StringComparison.OrdinalIgnoreCase);
        }

        private void PushAuthoritativeBossStateToClients()
        {
            if (_networkManager.CustomMessagingManager == null || !TryResolveBossRuntime())
            {
                return;
            }

            int currentServerTick = ResolveCurrentServerTick();
            BossAuthoritativeState state = _bossController.CaptureAuthoritativeState(
                currentServerTick,
                ResolveFixedDeltaTime());
            _latestBossState = state;
            _hasLatestBossState = true;
            ApplyBossHudState(state);

            CollectRemoteClientIds();
            if (_remoteClientIds.Count == 0)
            {
                return;
            }

            BossAuthoritativeStateMessage message = new BossAuthoritativeStateMessage
            {
                ServerTick = currentServerTick,
                State = state
            };

            using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteValueSafe(message);
            _networkManager.CustomMessagingManager.SendNamedMessage(
                BossAuthorityMessageName,
                _remoteClientIds,
                writer,
                NetworkDelivery.UnreliableSequenced);
        }

        private void PushReplicatedBossEffectsToClients()
        {
            if (_networkManager.CustomMessagingManager == null || !TryResolveBossRuntime())
            {
                return;
            }

            CollectRemoteClientIds();
            if (_remoteClientIds.Count == 0)
            {
                _bossController.ClearPendingReplicatedEffectEvents();
                return;
            }

            _pendingOutgoingEffectBatch.Clear();
            while (_bossController.TryDequeueReplicatedEffectEvent(out BossReplicatedEffectEvent effect))
            {
                _pendingOutgoingEffectBatch.Add(effect);
            }

            if (_pendingOutgoingEffectBatch.Count == 0)
            {
                return;
            }

            using FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp);
            writer.WriteValueSafe(_pendingOutgoingEffectBatch.Count);
            for (int i = 0; i < _pendingOutgoingEffectBatch.Count; i++)
            {
                BossReplicatedEffectEvent effect = _pendingOutgoingEffectBatch[i];
                writer.WriteValueSafe(effect);
            }

            _networkManager.CustomMessagingManager.SendNamedMessage(
                BossEffectMessageName,
                _remoteClientIds,
                writer,
                NetworkDelivery.ReliableSequenced);
        }

        private void CollectRemoteClientIds()
        {
            _remoteClientIds.Clear();

            if (_networkManager == null || _networkManager.ConnectedClientsIds == null)
            {
                return;
            }

            for (int i = 0; i < _networkManager.ConnectedClientsIds.Count; i++)
            {
                ulong clientId = _networkManager.ConnectedClientsIds[i];
                if (clientId == NetworkManager.ServerClientId)
                {
                    continue;
                }

                _remoteClientIds.Add(clientId);
            }
        }

        private void HandleBossAuthoritativeStateMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (_networkManager == null
                || _networkManager.IsServer
                || senderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            reader.ReadValueSafe(out BossAuthoritativeStateMessage message);
            if (_hasLatestReceivedMessage && message.ServerTick < _latestReceivedMessage.ServerTick)
            {
                return;
            }

            _latestReceivedMessage = message;
            _hasLatestReceivedMessage = true;
        }

        private void HandleBossReplicatedEffectMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (_networkManager == null
                || _networkManager.IsServer
                || senderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            reader.ReadValueSafe(out int effectCount);
            for (int i = 0; i < effectCount; i++)
            {
                reader.ReadValueSafe(out BossReplicatedEffectEvent effect);
                if (effect.SequenceId <= _lastConsumedEffectSequenceId
                    || effect.SequenceId <= _lastQueuedEffectSequenceId)
                {
                    continue;
                }

                _pendingReceivedEffectEvents.Enqueue(effect);
                _lastQueuedEffectSequenceId = effect.SequenceId;
            }
        }

        private void ApplyPendingReceivedEffects()
        {
            while (_pendingReceivedEffectEvents.Count > 0)
            {
                BossReplicatedEffectEvent effect = _pendingReceivedEffectEvents.Peek();
                if (effect.SequenceId <= _lastConsumedEffectSequenceId)
                {
                    _pendingReceivedEffectEvents.Dequeue();
                    continue;
                }

                if (!TryResolveBossRuntime())
                {
                    return;
                }

                ApplyReplicatedEffect(effect);
                _pendingReceivedEffectEvents.Dequeue();
                _lastConsumedEffectSequenceId = effect.SequenceId;
            }
        }

        private void ApplyReplicatedEffect(in BossReplicatedEffectEvent effect)
        {
            if (_bossController == null)
            {
                return;
            }

            switch (effect.EffectKind)
            {
                case BossReplicatedEffectKind.ProjectileShot:
                    _bossController.ProjectileAttackPattern?.PlayReplicatedDisplayShot(
                        _bossController,
                        effect.StartPosition,
                        effect.Direction,
                        effect.Speed,
                        effect.Lifetime,
                        ResolveReplicatedEffectTarget(effect.TargetNetworkObjectId),
                        effect.HomingStrength,
                        effect.HomingDuration,
                        effect.VerticalFollowSpeed);
                    break;

                case BossReplicatedEffectKind.AoESpawn:
                    _bossController.AoEAttackPattern?.PlayReplicatedDisplayAoE(
                        _bossController,
                        effect.StartPosition,
                        effect.ImpactPosition,
                        effect.Radius,
                        effect.WarningDuration,
                        effect.ActiveDuration);
                    break;
            }
        }

        private void ApplyLatestReceivedState()
        {
            if (!_hasLatestReceivedMessage || !TryResolveBossRuntime())
            {
                return;
            }

            BossAuthoritativeState state = _latestReceivedMessage.State;
            _latestBossState = state;
            _hasLatestBossState = true;
            _bossController.transform.SetPositionAndRotation(state.Position, state.Rotation);

            float planarSpeed = ResolvePlanarSpeed(state, _latestReceivedMessage.ServerTick);
            ApplySearchingUi(state);
            ApplyDisplayAnimation(state, _latestReceivedMessage.ServerTick, planarSpeed);
            ApplyBossHudState(state);

            _lastAppliedState = state;
            _lastAppliedServerTick = _latestReceivedMessage.ServerTick;
            _hasLastAppliedState = true;
            _lastConsumedServerTick = _latestReceivedMessage.ServerTick;
            _hasConsumedReceivedMessage = true;
        }

        private bool ShouldConsumeLatestReceivedMessage()
        {
            return _hasLatestReceivedMessage
                   && (!_hasConsumedReceivedMessage || _latestReceivedMessage.ServerTick > _lastConsumedServerTick);
        }

        private float ResolvePlanarSpeed(in BossAuthoritativeState state, int serverTick)
        {
            float fallbackSpeed = ResolveFallbackLocomotionSpeed(state.LocomotionState);
            if (state.LocomotionState != BossAuthoritativeLocomotionState.Move
                && state.LocomotionState != BossAuthoritativeLocomotionState.Search)
            {
                _stablePresentationPlanarSpeed = 0f;
                _hasStablePresentationPlanarSpeed = false;
                return 0f;
            }

            float targetSpeed = fallbackSpeed;
            if (_hasLastAppliedState)
            {
                int elapsedTicks = Mathf.Max(1, serverTick - _lastAppliedServerTick);
                float elapsedTime = Mathf.Max(ResolveFixedDeltaTime() * elapsedTicks, 0.0001f);

                Vector3 delta = state.Position - _lastAppliedState.Position;
                delta.y = 0f;

                float measuredSpeed = delta.magnitude / elapsedTime;
                if (measuredSpeed > 0.05f)
                {
                    targetSpeed = measuredSpeed;
                }
            }

            if (!_hasStablePresentationPlanarSpeed)
            {
                _stablePresentationPlanarSpeed = targetSpeed;
                _hasStablePresentationPlanarSpeed = true;
                return targetSpeed;
            }

            _stablePresentationPlanarSpeed = Mathf.Lerp(_stablePresentationPlanarSpeed, targetSpeed, 0.5f);
            return _stablePresentationPlanarSpeed;
        }

        private float ResolveFallbackLocomotionSpeed(BossAuthoritativeLocomotionState locomotionState)
        {
            if (_bossController == null)
            {
                return 0f;
            }

            return locomotionState == BossAuthoritativeLocomotionState.Search
                ? _bossController.SearchingMoveSpeed
                : locomotionState == BossAuthoritativeLocomotionState.Move
                    ? _bossController.MoveSpeed
                    : 0f;
        }

        private void ApplySearchingUi(in BossAuthoritativeState state)
        {
            if (_bossVisual == null)
            {
                return;
            }

            bool shouldShowSearchingUi = state.LocomotionState == BossAuthoritativeLocomotionState.Search;
            if (_searchingUiActive == shouldShowSearchingUi)
            {
                return;
            }

            _bossVisual.SetSearchingUI(shouldShowSearchingUi);
            _searchingUiActive = shouldShowSearchingUi;
        }

        private void ApplyDisplayAnimation(in BossAuthoritativeState state, int serverTick, float planarSpeed)
        {
            if (_bossVisual == null)
            {
                return;
            }

            if (state.IsDead || state.LocomotionState == BossAuthoritativeLocomotionState.Dead)
            {
                ApplyDeathVisual(state);
                return;
            }

            if (state.LocomotionState == BossAuthoritativeLocomotionState.PhaseIntro)
            {
                ApplyPhaseIntroVisual(state);
                return;
            }

            if (state.LocomotionState == BossAuthoritativeLocomotionState.Hit)
            {
                ApplyHitVisual(state);
                return;
            }

            if (state.HasActiveAttack)
            {
                ApplyAttackVisual(state, serverTick);
                return;
            }

            ApplyLocomotionVisual(state, planarSpeed);
        }

        private void ApplyDeathVisual(in BossAuthoritativeState state)
        {
            _bossController?.SetLocomotionVisualSuppressed(true);
            _bossVisual.SetLungeRootMotionEnabled(false);
            _bossVisual.SetAnimatorPlaybackSpeed(1f);
            _bossVisual.SetSpeed(0f);

            if (_hasLastAppliedState && (_lastAppliedState.IsDead || _lastAppliedState.LocomotionState == BossAuthoritativeLocomotionState.Dead))
            {
                return;
            }

            _bossVisual.TriggerDie();
        }

        private void ApplyPhaseIntroVisual(in BossAuthoritativeState state)
        {
            _bossController?.SetLocomotionVisualSuppressed(true);
            _bossVisual.SetLungeRootMotionEnabled(false);
            _bossVisual.SetAnimatorPlaybackSpeed(1f);
            _bossVisual.SetSpeed(0f);

            bool shouldRestartIntro = !_hasLastAppliedState
                                      || _lastAppliedState.LocomotionState != BossAuthoritativeLocomotionState.PhaseIntro
                                      || _lastAppliedState.Phase != state.Phase;
            if (shouldRestartIntro)
            {
                _bossVisual.PlayScream();
            }
        }

        private void ApplyHitVisual(in BossAuthoritativeState state)
        {
            _bossController?.SetLocomotionVisualSuppressed(true);
            _bossVisual.SetLungeRootMotionEnabled(false);
            _bossVisual.SetAnimatorPlaybackSpeed(1f);
            _bossVisual.SetSpeed(0f);

            if (_hasLastAppliedState && _lastAppliedState.LocomotionState == BossAuthoritativeLocomotionState.Hit)
            {
                return;
            }

            _bossVisual.TriggerHit();
        }

        private void ApplyAttackVisual(in BossAuthoritativeState state, int serverTick)
        {
            _bossController?.SetLocomotionVisualSuppressed(true);
            _bossVisual.SetLungeRootMotionEnabled(false);
            _bossVisual.SetAnimatorPlaybackSpeed(1f);
            _bossVisual.SetSpeed(0f);

            bool isNewAttack = !_hasLastAppliedState
                               || _lastAppliedState.CurrentAttackId != state.CurrentAttackId
                               || _lastAppliedState.AttackStartServerTick != state.AttackStartServerTick;
            if (!isNewAttack)
            {
                return;
            }

            float normalizedTime = ResolveAttackNormalizedTime(state.CurrentAttackId, state.AttackStartServerTick, serverTick);
            switch (state.CurrentAttackId)
            {
                case BossAuthoritativeAttackId.Basic:
                    _bossVisual.PlayAttack();
                    TryPlayAnimatorState(BasicAttackStateName, normalizedTime);
                    break;

                case BossAuthoritativeAttackId.Lunge:
                    _bossVisual.PlayLungeAttack();
                    if (!TryPlayAnimatorState(LungeAttackStateName, normalizedTime))
                    {
                        TryPlayAnimatorState(LegacyClawAttackStateName, normalizedTime);
                    }
                    break;

                case BossAuthoritativeAttackId.Projectile:
                    _bossVisual.PlayProjectileAttack();
                    if (!TryPlayAnimatorState(FlameAttackStateName, normalizedTime))
                    {
                        TryPlayAnimatorState(FireballShootStateName, normalizedTime);
                    }
                    break;

                case BossAuthoritativeAttackId.AoE:
                    _bossVisual.PlayTakeOff();
                    if (!TryPlayAnimatorState(TakeOffStateName, normalizedTime))
                    {
                        TryPlayAnimatorState(TakeOffAltStateName, normalizedTime);
                    }
                    break;
            }
        }

        private void ApplyLocomotionVisual(in BossAuthoritativeState state, float planarSpeed)
        {
            _bossController?.SetLocomotionVisualSuppressed(false);
            _bossVisual.SetLungeRootMotionEnabled(false);
            _bossVisual.SetAnimatorPlaybackSpeed(1f);

            switch (state.LocomotionState)
            {
                case BossAuthoritativeLocomotionState.Idle:
                case BossAuthoritativeLocomotionState.Unknown:
                    _bossVisual.SetSpeed(0f);
                    _bossVisual.PlayIdle();
                    break;

                case BossAuthoritativeLocomotionState.Move:
                case BossAuthoritativeLocomotionState.Search:
                    _bossVisual.PlayMove();
                    _bossVisual.SetSpeed(planarSpeed);
                    break;

                default:
                    _bossVisual.SetSpeed(0f);
                    break;
            }
        }

        private float ResolveAttackNormalizedTime(
            BossAuthoritativeAttackId attackId,
            int attackStartServerTick,
            int currentServerTick)
        {
            if (attackStartServerTick <= 0 || currentServerTick < attackStartServerTick)
            {
                return 0f;
            }

            float clipLength = ResolveAttackClipLengthOrDefault(attackId);
            if (clipLength <= 0.0001f)
            {
                return 0f;
            }

            float elapsedSeconds = (currentServerTick - attackStartServerTick) * ResolveFixedDeltaTime();
            return Mathf.Clamp01(elapsedSeconds / clipLength);
        }

        private float ResolveAttackClipLengthOrDefault(BossAuthoritativeAttackId attackId)
        {
            return attackId switch
            {
                BossAuthoritativeAttackId.Basic => GetClipLengthOrDefault(BasicAttackStateName, 1f),
                BossAuthoritativeAttackId.Lunge => ResolveFirstAvailableClipLength(
                    1f,
                    LungeAttackStateName,
                    LegacyClawAttackStateName),
                BossAuthoritativeAttackId.Projectile => ResolveFirstAvailableClipLength(
                    1f,
                    FlameAttackStateName,
                    FireballShootStateName,
                    BasicAttackStateName),
                BossAuthoritativeAttackId.AoE => ResolveFirstAvailableClipLength(
                    1.2f,
                    TakeOffStateName,
                    TakeOffAltStateName),
                _ => 1f
            };
        }

        private float ResolveFirstAvailableClipLength(float fallback, params string[] clipNames)
        {
            for (int i = 0; i < clipNames.Length; i++)
            {
                float clipLength = GetClipLengthOrDefault(clipNames[i], -1f);
                if (clipLength > 0f)
                {
                    return clipLength;
                }
            }

            return fallback;
        }

        private float GetClipLengthOrDefault(string clipName, float fallback)
        {
            if (_bossAnimator == null || _bossAnimator.runtimeAnimatorController == null)
            {
                return fallback;
            }

            AnimationClip[] clips = _bossAnimator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null)
                {
                    continue;
                }

                if (!string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return clip.length > 0f ? clip.length : fallback;
            }

            return fallback;
        }

        private bool TryPlayAnimatorState(string stateName, float normalizedTime)
        {
            if (_bossAnimator == null || string.IsNullOrEmpty(stateName))
            {
                return false;
            }

            int stateHash = Animator.StringToHash(stateName);
            if (!_bossAnimator.HasState(0, stateHash))
            {
                return false;
            }

            _bossAnimator.speed = Mathf.Max(MinimumPlaybackSpeed, _bossAnimator.speed);
            _bossAnimator.Play(stateHash, 0, Mathf.Clamp01(normalizedTime));
            return true;
        }

        private bool TryResolveBossRuntime()
        {
            if (_bossController == null)
            {
                _bossController = FindObjectOfType<BossController>();
            }

            if (_bossController == null)
            {
                _bossVisual = null;
                _bossAnimator = null;
                return false;
            }

            if (_bossVisual == null)
            {
                _bossVisual = _bossController.Visual;
            }

            if (_bossAnimator == null && _bossVisual != null)
            {
                _bossAnimator = _bossVisual.Animator;
            }

            return true;
        }

        private CombatHUDController ResolveCombatHudController()
        {
            if (_combatHudController != null)
            {
                return _combatHudController;
            }

            _combatHudController = FindObjectOfType<CombatHUDController>();
            return _combatHudController;
        }

        private void ApplyBossHudState(in BossAuthoritativeState state)
        {
            CombatHUDController combatHudController = ResolveCombatHudController();
            if (combatHudController == null)
            {
                return;
            }

            int maxHealth = Mathf.Max(0, state.MaxHealth);
            int currentHealth = maxHealth > 0
                ? Mathf.Clamp(state.CurrentHealth, 0, maxHealth)
                : 0;

            if (_hasAppliedBossHudState
                && _lastBossHudCurrentHealth == currentHealth
                && _lastBossHudMaxHealth == maxHealth)
            {
                return;
            }

            combatHudController.SetBossHpNormalized(
                maxHealth > 0 ? (float)currentHealth / maxHealth : 0f,
                currentHealth,
                maxHealth);

            _hasAppliedBossHudState = true;
            _lastBossHudCurrentHealth = currentHealth;
            _lastBossHudMaxHealth = maxHealth;
        }

        private void ResetBossHudState()
        {
            if (_combatHudController != null && _hasAppliedBossHudState)
            {
                _combatHudController.SetBossHpNormalized(0f, 0, 0);
            }

            _combatHudController = null;
            _hasAppliedBossHudState = false;
            _lastBossHudCurrentHealth = int.MinValue;
            _lastBossHudMaxHealth = int.MinValue;
        }

        private Transform ResolveReplicatedEffectTarget(ulong targetNetworkObjectId)
        {
            if (targetNetworkObjectId == 0
                || _networkManager == null
                || _networkManager.SpawnManager == null
                || !_networkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetNetworkObject)
                || targetNetworkObject == null)
            {
                return null;
            }

            return targetNetworkObject.transform;
        }

        private int ResolveCurrentServerTick()
        {
            if (_networkManager != null && _networkManager.NetworkTickSystem != null)
            {
                return _networkManager.NetworkTickSystem.LocalTime.Tick;
            }

            return 0;
        }

        private float ResolveFixedDeltaTime()
        {
            if (_networkManager != null && _networkManager.NetworkTickSystem != null)
            {
                return _networkManager.NetworkTickSystem.LocalTime.FixedDeltaTime;
            }

            return FallbackFixedDeltaTime;
        }

        private void ResetPresentationState()
        {
            if (_bossVisual != null && _searchingUiActive)
            {
                _bossVisual.SetSearchingUI(false);
            }

            ResetBossHudState();

            _bossController = null;
            _bossVisual = null;
            _bossAnimator = null;
            _searchingUiActive = false;
            _hasLatestReceivedMessage = false;
            _latestReceivedMessage = default;
            _hasConsumedReceivedMessage = false;
            _lastConsumedServerTick = 0;
            _pendingReceivedEffectEvents.Clear();
            _pendingOutgoingEffectBatch.Clear();
            _lastQueuedEffectSequenceId = 0;
            _lastConsumedEffectSequenceId = 0;
            _hasLastAppliedState = false;
            _lastAppliedState = default;
            _lastAppliedServerTick = 0;
            _hasStablePresentationPlanarSpeed = false;
            _stablePresentationPlanarSpeed = 0f;
            _hasLatestBossState = false;
            _latestBossState = default;
        }
    }
}
