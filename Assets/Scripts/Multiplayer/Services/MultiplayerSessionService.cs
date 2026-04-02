using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Multiplayer
{
    public enum MultiplayerSessionState
    {
        Idle,
        CreatingHostSession,
        JoiningClientSession,
        LobbyActive,
        StartingGameplay,
        InGameplay,
        Closing,
        Closed
    }

    public enum MultiplayerSessionFailureKind
    {
        None,
        WrongJoinCode,
        Fatal
    }

    public readonly struct MultiplayerSessionSnapshot
    {
        public MultiplayerSessionSnapshot(bool hasActiveSession, string roomTitle, string joinCode, int connectedPlayerCount, string lobbyStatusText, bool isHost, bool canStart)
        {
            HasActiveSession = hasActiveSession;
            RoomTitle = roomTitle ?? string.Empty;
            JoinCode = joinCode ?? string.Empty;
            ConnectedPlayerCount = connectedPlayerCount;
            LobbyStatusText = lobbyStatusText ?? string.Empty;
            IsHost = isHost;
            CanStart = canStart;
        }

        public bool HasActiveSession { get; }
        public string RoomTitle { get; }
        public string JoinCode { get; }
        public int ConnectedPlayerCount { get; }
        public string LobbyStatusText { get; }
        public bool IsHost { get; }
        public bool CanStart { get; }
    }

    [DisallowMultipleComponent]
    public sealed class MultiplayerSessionService : MonoBehaviour
    {
        private const string RelayConnectionType = "dtls";
        private const string RelayJoinCodeDataKey = "RelayJoinCode";
        private const string SessionStateDataKey = "SessionState";
        private const string ModeDataKey = "Mode";
        private const string WaitingStateValue = "Waiting";
        private const string ReadyStateValue = "Ready";
        private const string StartingStateValue = "Starting";
        private const string BossRaidCoopModeValue = "BossRaidCoop";
        private const string WrongKeyMessage = "Wrong key. Please type again.";
        private const string LobbyClosedMessage = "Lobby closed. Returning to title.";
        private const string DisconnectedFromLobbyMessage = "Disconnected from lobby.";
        private const string DisconnectedFromHostMessage = "Disconnected from host.";
        private const string ClientDisconnectedDuringGameplayStartMessage = "Client disconnected during gameplay start.";
        private const string ClientDisconnectedDuringGameplayMessage = "Client disconnected during gameplay.";
        private const string WaitingForOtherPlayerStatusText = "Waiting for other player...";
        private const string HostStabilizingStatusText = "2/2 connected. Waiting for stable connection...";
        private const string ClientWaitingForHostStatusText = "2/2 connected. Waiting for host...";
        private const string ReadyToStartStatusText = "Ready to start";
        private const string HostCanStartStatusText = "Host can start now";
        private const string StartingGameStatusText = "Starting game...";
        private const string InGameplayStatusText = "In gameplay";
        private const string InGameStateValue = "InGame";
        private const int MaxLobbyPlayerCount = 2;
        private const int GameplayStartTimeoutMilliseconds = 15000;
        private const float LobbyHeartbeatIntervalSeconds = 15f;
        private const float LobbyPollIntervalSeconds = 2f;
        private const float HostStartUnlockStableDurationSeconds = 2f;
        private const float ConnectionDebugPulseIntervalSeconds = 2f;

        private static MultiplayerSessionService _instance;

        private Task _currentOperationTask;
        private Task _refreshLobbyTask;
        private MultiplayerRuntimeRoot _runtimeRoot;
        private MultiplayerSessionState _state = MultiplayerSessionState.Idle;
        private MultiplayerSessionSnapshot _currentSnapshot;
        private Lobby _currentLobby;
#if UGS_BETA_LOBBY_EVENTS && UGS_LOBBY_EVENTS
        private ILobbyEvents _lobbyEvents;
#endif
        private string _localPlayerId = string.Empty;
        private string _lastErrorMessage = string.Empty;
        private MultiplayerSessionFailureKind _lastFailureKind = MultiplayerSessionFailureKind.None;
        private bool _isHost;
        private bool _heartbeatEnabled;
        private bool _heartbeatRequestInFlight;
        private bool _lobbySessionStateUpdateInFlight;
        private bool _hostStartUnlocked;
        private bool _sceneLoadCallbacksRegistered;
        private int _sessionVersion;
        private float _heartbeatTimer;
        private float _lobbyPollTimer;
        private float _hostStartStableTimer;
        private TaskCompletionSource<bool> _gameplayStartTaskSource;
        private readonly StringBuilder _connectionDebugBuilder = new StringBuilder(512);
        private float _connectionDebugPulseTimer;
        private int _connectionDebugSequence;
        private bool _hasPendingDisconnectDebugSnapshot;
        private ulong _pendingDisconnectPeerClientId;
        private MultiplayerSessionState _pendingDisconnectState = MultiplayerSessionState.Idle;
        private string _pendingDisconnectReason = string.Empty;
        private string _pendingDisconnectNgoReason = string.Empty;
        private string _pendingDisconnectSceneName = string.Empty;
        private string _pendingDisconnectAppLabel = string.Empty;
        private string _pendingDisconnectLocalClientIdLabel = "na";

        public static bool HasInstance => _instance != null;
        public static MultiplayerSessionService Instance => GetOrCreateInstance();

        public MultiplayerSessionState State => _state;
        public MultiplayerSessionSnapshot CurrentSnapshot => _currentSnapshot;
        public string LastErrorMessage => _lastErrorMessage;
        public MultiplayerSessionFailureKind LastFailureKind => _lastFailureKind;
        public bool IsBusy => _state == MultiplayerSessionState.Closing
                              || (_currentOperationTask != null && !_currentOperationTask.IsCompleted);
        public bool HasActiveSession => _currentSnapshot.HasActiveSession || IsNetworkSessionAlive();

        public event Action<MultiplayerSessionState> StateChanged;
        public event Action<MultiplayerSessionSnapshot> SnapshotChanged;
        public event Action<string> FatalErrorOccurred;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        private static MultiplayerSessionService GetOrCreateInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject host = new GameObject("MultiplayerSessionService");
            _instance = host.AddComponent<MultiplayerSessionService>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            UpdateConnectionDebugPulse(Time.unscaledDeltaTime);

            if (!IsLobbyTrackedState(_state))
            {
                return;
            }

            UpdateLobbyHeartbeat();
            UpdateLobbyPollFallback();
            UpdateHostStartUnlockGate(Time.deltaTime);
            SyncHostLobbySessionStateIfNeeded();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private sealed class WrongJoinCodeException : Exception
        {
            public WrongJoinCodeException() : base(WrongKeyMessage)
            {
            }
        }

        public Task CreateHostSessionAsync(string roomTitle)
        {
            if (IsBusy)
            {
                throw new InvalidOperationException("Multiplayer session service is busy.");
            }

            if (HasActiveSession)
            {
                throw new InvalidOperationException("A multiplayer session is already active.");
            }

            int sessionVersion = AdvanceSessionVersion();
            _currentOperationTask = CreateHostSessionInternalAsync(roomTitle, sessionVersion);
            return _currentOperationTask;
        }

        public Task JoinClientSessionAsync(string joinCode)
        {
            if (IsBusy)
            {
                throw new InvalidOperationException("Multiplayer session service is busy.");
            }

            if (HasActiveSession)
            {
                throw new InvalidOperationException("A multiplayer session is already active.");
            }

            int sessionVersion = AdvanceSessionVersion();
            _currentOperationTask = JoinClientSessionInternalAsync(joinCode, sessionVersion);
            return _currentOperationTask;
        }

        public Task ShutdownSessionAsync()
        {
            if (IsBusy)
            {
                return _currentOperationTask ?? Task.CompletedTask;
            }

            _lastErrorMessage = string.Empty;
            _lastFailureKind = MultiplayerSessionFailureKind.None;
            AdvanceSessionVersion();
            _currentOperationTask = ShutdownSessionInternalAsync();
            return _currentOperationTask;
        }

        public Task StartGameplayAsync()
        {
            if (IsBusy)
            {
                throw new InvalidOperationException("Multiplayer session service is busy.");
            }

            if (!_isHost)
            {
                throw new InvalidOperationException("Only the host can start gameplay.");
            }

            if (_state != MultiplayerSessionState.LobbyActive)
            {
                throw new InvalidOperationException("Gameplay start is only available from the lobby state.");
            }

            if (!ResolveCanStart(ResolveConnectedPlayerCount()))
            {
                throw new InvalidOperationException("Gameplay start is not ready yet.");
            }

            _lastErrorMessage = string.Empty;
            _lastFailureKind = MultiplayerSessionFailureKind.None;
            _currentOperationTask = StartGameplayInternalAsync(_sessionVersion);
            return _currentOperationTask;
        }

        private async Task CreateHostSessionInternalAsync(string roomTitle, int sessionVersion)
        {
            _lastErrorMessage = string.Empty;
            _lastFailureKind = MultiplayerSessionFailureKind.None;
            SetState(MultiplayerSessionState.CreatingHostSession);

            try
            {
                await MultiplayerServicesBootstrap.Instance.EnsureInitializedAsync();

                _localPlayerId = MultiplayerServicesBootstrap.Instance.PlayerId ?? string.Empty;
                if (string.IsNullOrEmpty(_localPlayerId))
                {
                    throw new InvalidOperationException("PlayerId is empty before Host create.");
                }

                _runtimeRoot = MultiplayerRuntimeRoot.Instance;
                _runtimeRoot.EnsureConfigured();
                EnsureNetworkManagerIsIdle(_runtimeRoot.NetworkManager);

                Debug.Log("MultiplayerSessionService: Creating Relay allocation for Host.");
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);

                Debug.Log("MultiplayerSessionService: Requesting Relay join code for Host.");
                string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                Debug.Log("MultiplayerSessionService: Creating Lobby for Host.");
                CreateLobbyOptions options = BuildHostCreateOptions(relayJoinCode);
                Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(roomTitle, 2, options);

                RelayServerEndpoint relayEndpoint = ResolveRelayEndpoint(allocation);
                _runtimeRoot.UnityTransport.SetHostRelayData(
                    relayEndpoint.Host,
                    (ushort)relayEndpoint.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData,
                    string.Equals(relayEndpoint.ConnectionType, RelayConnectionType, StringComparison.OrdinalIgnoreCase) || relayEndpoint.Secure);
                if (!_runtimeRoot.NetworkManager.StartHost())
                {
                    throw new InvalidOperationException("NGO Host failed to start.");
                }

                RegisterNetworkCallbacks();

                _isHost = true;
                _currentLobby = lobby;
                _heartbeatEnabled = true;
                _heartbeatRequestInFlight = false;
                _heartbeatTimer = 0f;
                _lobbyPollTimer = 0f;
                _hostStartStableTimer = 0f;
                _hostStartUnlocked = false;
                _lobbySessionStateUpdateInFlight = false;

                await SubscribeToLobbyEventsAsync(lobby.Id, sessionVersion);

                SetState(MultiplayerSessionState.LobbyActive);
                UpdateHostStartUnlockGate(0f);
                TryPublishCurrentSnapshot();

                Debug.Log($"MultiplayerSessionService: Host session started. LobbyId={lobby.Id}, JoinCode={relayJoinCode}");
            }
            catch (Exception ex)
            {
                _lastFailureKind = MultiplayerSessionFailureKind.Fatal;
                _lastErrorMessage = $"Host create failed: {ex.Message}";
                Debug.LogError($"MultiplayerSessionService: {_lastErrorMessage}");
                AdvanceSessionVersion();
                await ShutdownSessionInternalAsync();
                throw;
            }
            finally
            {
                _currentOperationTask = null;
            }
        }

        private async Task JoinClientSessionInternalAsync(string rawJoinCode, int sessionVersion)
        {
            _lastErrorMessage = string.Empty;
            _lastFailureKind = MultiplayerSessionFailureKind.None;
            SetState(MultiplayerSessionState.JoiningClientSession);

            try
            {
                await MultiplayerServicesBootstrap.Instance.EnsureInitializedAsync();

                _localPlayerId = MultiplayerServicesBootstrap.Instance.PlayerId ?? string.Empty;
                if (string.IsNullOrEmpty(_localPlayerId))
                {
                    throw new InvalidOperationException("PlayerId is empty before Client join.");
                }

                string joinCode = NormalizeJoinCode(rawJoinCode);
                if (!IsValidJoinCode(joinCode))
                {
                    throw new WrongJoinCodeException();
                }

                _runtimeRoot = MultiplayerRuntimeRoot.Instance;
                _runtimeRoot.EnsureConfigured();
                EnsureNetworkManagerIsIdle(_runtimeRoot.NetworkManager);

                Debug.Log($"MultiplayerSessionService: Querying Lobby for Client join. JoinCode={joinCode}");
                Lobby lobby = await QueryJoinableLobbyByJoinCodeAsync(joinCode);
                if (lobby == null)
                {
                    throw new WrongJoinCodeException();
                }

                Debug.Log($"MultiplayerSessionService: Joining Lobby {lobby.Id} as Client.");
                _currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(
                    lobby.Id,
                    new JoinLobbyByIdOptions
                    {
                        Player = new Unity.Services.Lobbies.Models.Player(id: _localPlayerId)
                    });

                Debug.Log($"MultiplayerSessionService: Joining Relay allocation as Client. JoinCode={joinCode}");
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                RelayServerEndpoint relayEndpoint = ResolveRelayEndpoint(joinAllocation);
                _runtimeRoot.UnityTransport.SetClientRelayData(
                    relayEndpoint.Host,
                    (ushort)relayEndpoint.Port,
                    joinAllocation.AllocationIdBytes,
                    joinAllocation.Key,
                    joinAllocation.ConnectionData,
                    joinAllocation.HostConnectionData,
                    string.Equals(relayEndpoint.ConnectionType, RelayConnectionType, StringComparison.OrdinalIgnoreCase) || relayEndpoint.Secure);

                RegisterNetworkCallbacks();

                if (!_runtimeRoot.NetworkManager.StartClient())
                {
                    throw new InvalidOperationException("NGO Client failed to start.");
                }

                _isHost = false;
                _heartbeatEnabled = false;
                _heartbeatRequestInFlight = false;
                _heartbeatTimer = 0f;
                _lobbyPollTimer = 0f;
                _hostStartStableTimer = 0f;
                _hostStartUnlocked = false;
                _lobbySessionStateUpdateInFlight = false;

                await SubscribeToLobbyEventsAsync(_currentLobby.Id, sessionVersion);

                SetState(MultiplayerSessionState.LobbyActive);
                UpdateHostStartUnlockGate(0f);
                TryPublishCurrentSnapshot();

                Debug.Log($"MultiplayerSessionService: Client session started. LobbyId={_currentLobby.Id}, JoinCode={joinCode}");
            }
            catch (Exception ex)
            {
                if (IsWrongJoinCodeFailure(ex))
                {
                    _lastFailureKind = MultiplayerSessionFailureKind.WrongJoinCode;
                    _lastErrorMessage = WrongKeyMessage;
                    Debug.LogWarning($"MultiplayerSessionService: Client join rejected. {ex.Message}");
                }
                else
                {
                    _lastFailureKind = MultiplayerSessionFailureKind.Fatal;
                    _lastErrorMessage = $"Client join failed: {ex.Message}";
                    Debug.LogError($"MultiplayerSessionService: {_lastErrorMessage}");
                }

                AdvanceSessionVersion();
                await ShutdownSessionInternalAsync();
                throw;
            }
            finally
            {
                _currentOperationTask = null;
            }
        }

        private async Task StartGameplayInternalAsync(int sessionVersion)
        {
            try
            {
                _runtimeRoot = MultiplayerRuntimeRoot.Instance;
                _runtimeRoot.EnsureConfigured();

                NetworkManager networkManager = _runtimeRoot.NetworkManager;
                if (networkManager == null)
                {
                    throw new InvalidOperationException("NetworkManager is missing before gameplay start.");
                }

                if (!networkManager.IsServer || !networkManager.IsListening || networkManager.ShutdownInProgress)
                {
                    throw new InvalidOperationException("NGO host is not in a valid state for gameplay start.");
                }

                if (networkManager.SceneManager == null)
                {
                    throw new InvalidOperationException("NetworkSceneManager is missing.");
                }

                _hostStartUnlocked = false;
                _hostStartStableTimer = 0f;
                SetState(MultiplayerSessionState.StartingGameplay);
                TryPublishCurrentSnapshot();

                RegisterSceneLoadCallbacks();
                _gameplayStartTaskSource = new TaskCompletionSource<bool>();
                _ = UpdateHostLobbySessionStateAsync(StartingStateValue, sessionVersion);

                SceneEventProgressStatus loadStatus = networkManager.SceneManager.LoadScene(
                    MultiplayerScenePaths.GamePlayScenePath,
                    LoadSceneMode.Single);

                if (loadStatus != SceneEventProgressStatus.Started)
                {
                    throw new InvalidOperationException($"NGO gameplay scene load failed to start. Status={loadStatus}");
                }

                Task completedTask = await Task.WhenAny(
                    _gameplayStartTaskSource.Task,
                    Task.Delay(GameplayStartTimeoutMilliseconds));

                if (completedTask != _gameplayStartTaskSource.Task)
                {
                    throw new TimeoutException("Gameplay start timed out.");
                }

                await _gameplayStartTaskSource.Task;
                MultiplayerGameplaySceneCoordinator.TrySpawnAuthoritativePlayers();
                Debug.Log("MultiplayerSessionService: Gameplay scene sync completed.");
                LogGameplaySceneSyncCompleted();
                SetState(MultiplayerSessionState.InGameplay);
                TryPublishCurrentSnapshot();
                _ = UpdateHostLobbySessionStateAsync(InGameStateValue, sessionVersion);
            }
            catch (Exception ex)
            {
                _lastFailureKind = MultiplayerSessionFailureKind.Fatal;
                _lastErrorMessage = $"Gameplay start failed: {ex.Message}";
                Debug.LogError($"MultiplayerSessionService: {_lastErrorMessage}");

                if (_state != MultiplayerSessionState.Closing && _state != MultiplayerSessionState.Closed)
                {
                    if (IsSessionVersionCurrent(sessionVersion))
                    {
                        AdvanceSessionVersion();
                    }

                    await ShutdownSessionInternalAsync();
                    ReturnToMultiplayerTitleSceneIfNeeded();
                }

                throw;
            }
            finally
            {
                UnregisterSceneLoadCallbacks();
                ClearGameplayStartTracking();
                _currentOperationTask = null;
            }
        }

        private CreateLobbyOptions BuildHostCreateOptions(string relayJoinCode)
        {
            return new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = new Unity.Services.Lobbies.Models.Player(id: _localPlayerId),
                Data = new Dictionary<string, DataObject>
                {
                    {
                        RelayJoinCodeDataKey,
                        new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode, DataObject.IndexOptions.S1)
                    },
                    {
                        SessionStateDataKey,
                        new DataObject(DataObject.VisibilityOptions.Public, WaitingStateValue)
                    },
                    {
                        ModeDataKey,
                        new DataObject(DataObject.VisibilityOptions.Public, BossRaidCoopModeValue)
                    }
                }
            };
        }

        private async Task<Lobby> QueryJoinableLobbyByJoinCodeAsync(string joinCode)
        {
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(
                new QueryLobbiesOptions
                {
                    Count = 1,
                    Filters = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.S1, joinCode, QueryFilter.OpOptions.EQ),
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                    }
                });

            return response != null && response.Results != null && response.Results.Count > 0
                ? response.Results[0]
                : null;
        }

        private static RelayServerEndpoint ResolveRelayEndpoint(Allocation allocation)
        {
            if (allocation == null || allocation.ServerEndpoints == null)
            {
                throw new InvalidOperationException("Relay allocation did not include any server endpoints.");
            }

            for (int i = 0; i < allocation.ServerEndpoints.Count; i++)
            {
                RelayServerEndpoint endpoint = allocation.ServerEndpoints[i];
                if (endpoint != null && string.Equals(endpoint.ConnectionType, RelayConnectionType, StringComparison.OrdinalIgnoreCase))
                {
                    return endpoint;
                }
            }

            throw new InvalidOperationException($"Relay allocation does not include a {RelayConnectionType} endpoint.");
        }

        private static RelayServerEndpoint ResolveRelayEndpoint(JoinAllocation allocation)
        {
            if (allocation == null || allocation.ServerEndpoints == null)
            {
                throw new InvalidOperationException("Relay join allocation did not include any server endpoints.");
            }

            for (int i = 0; i < allocation.ServerEndpoints.Count; i++)
            {
                RelayServerEndpoint endpoint = allocation.ServerEndpoints[i];
                if (endpoint != null && string.Equals(endpoint.ConnectionType, RelayConnectionType, StringComparison.OrdinalIgnoreCase))
                {
                    return endpoint;
                }
            }

            throw new InvalidOperationException($"Relay join allocation does not include a {RelayConnectionType} endpoint.");
        }

        private Task SubscribeToLobbyEventsAsync(string lobbyId, int sessionVersion)
        {
#if UGS_BETA_LOBBY_EVENTS && UGS_LOBBY_EVENTS
            return SubscribeToLobbyEventsInternalAsync(lobbyId, sessionVersion);
#else
            return Task.CompletedTask;
#endif
        }

#if UGS_BETA_LOBBY_EVENTS && UGS_LOBBY_EVENTS
        private async Task SubscribeToLobbyEventsInternalAsync(string lobbyId, int sessionVersion)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                return;
            }

            LobbyEventCallbacks callbacks = new LobbyEventCallbacks();
            callbacks.PlayerJoined += _ =>
            {
                if (IsSessionVersionCurrent(sessionVersion))
                {
                    RequestLobbyRefresh();
                }
            };
            callbacks.PlayerLeft += _ =>
            {
                if (IsSessionVersionCurrent(sessionVersion))
                {
                    RequestLobbyRefresh();
                }
            };
            callbacks.DataChanged += _ =>
            {
                if (IsSessionVersionCurrent(sessionVersion))
                {
                    RequestLobbyRefresh();
                }
            };
            callbacks.DataAdded += _ =>
            {
                if (IsSessionVersionCurrent(sessionVersion))
                {
                    RequestLobbyRefresh();
                }
            };
            callbacks.DataRemoved += _ =>
            {
                if (IsSessionVersionCurrent(sessionVersion))
                {
                    RequestLobbyRefresh();
                }
            };
            callbacks.LobbyDeleted += () =>
            {
                if (IsSessionVersionCurrent(sessionVersion))
                {
                    BeginFatalShutdown("Lobby closed. Returning to title.");
                }
            };
            callbacks.KickedFromLobby += () =>
            {
                if (IsSessionVersionCurrent(sessionVersion))
                {
                    BeginFatalShutdown("Disconnected from lobby.");
                }
            };
            callbacks.LobbyEventConnectionStateChanged += state =>
            {
                if (state == LobbyEventConnectionState.Error && IsSessionVersionCurrent(sessionVersion))
                {
                    Debug.LogWarning("MultiplayerSessionService: Lobby event connection entered Error state.");
                }
            };

            _lobbyEvents = await LobbyService.Instance.SubscribeToLobbyEventsAsync(lobbyId, callbacks);
        }
#endif

        private void RequestLobbyRefresh()
        {
            if (!IsLobbyTrackedState(_state) || string.IsNullOrEmpty(_currentLobby?.Id))
            {
                return;
            }

            if (_refreshLobbyTask != null && !_refreshLobbyTask.IsCompleted)
            {
                return;
            }

            _refreshLobbyTask = RefreshLobbyAsync(_sessionVersion);
        }

        private async Task RefreshLobbyAsync(int sessionVersion)
        {
            try
            {
                Lobby refreshedLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
                if (!IsSessionVersionCurrent(sessionVersion) || !IsLobbyTrackedState(_state))
                {
                    return;
                }

                _currentLobby = refreshedLobby;
                UpdateHostStartUnlockGate(0f);
                TryPublishCurrentSnapshot();
            }
            catch (Exception ex)
            {
                if (!IsSessionVersionCurrent(sessionVersion) || _state == MultiplayerSessionState.Closing || _state == MultiplayerSessionState.Closed)
                {
                    return;
                }

                if (!_isHost && IsLobbyMembershipFailure(ex))
                {
                    BeginFatalShutdown(DisconnectedFromLobbyMessage);
                    return;
                }

                if (IsLobbyClosedFailure(ex))
                {
                    BeginFatalShutdown(LobbyClosedMessage);
                    return;
                }

                Debug.LogWarning($"MultiplayerSessionService: Lobby refresh failed. {ex.Message}");
            }
            finally
            {
                if (IsSessionVersionCurrent(sessionVersion))
                {
                    _refreshLobbyTask = null;
                }
            }
        }

        private void RegisterNetworkCallbacks()
        {
            if (_runtimeRoot == null || _runtimeRoot.NetworkManager == null)
            {
                return;
            }

            _runtimeRoot.NetworkManager.OnClientConnectedCallback -= HandleNetworkClientConnected;
            _runtimeRoot.NetworkManager.OnClientDisconnectCallback -= HandleNetworkClientDisconnected;
            _runtimeRoot.NetworkManager.OnClientConnectedCallback += HandleNetworkClientConnected;
            _runtimeRoot.NetworkManager.OnClientDisconnectCallback += HandleNetworkClientDisconnected;
        }

        private void RegisterSceneLoadCallbacks()
        {
            if (_sceneLoadCallbacksRegistered || _runtimeRoot == null || _runtimeRoot.NetworkManager == null || _runtimeRoot.NetworkManager.SceneManager == null)
            {
                return;
            }

            _runtimeRoot.NetworkManager.SceneManager.OnLoadEventCompleted -= HandleSceneLoadEventCompleted;
            _runtimeRoot.NetworkManager.SceneManager.OnLoadEventCompleted += HandleSceneLoadEventCompleted;
            _sceneLoadCallbacksRegistered = true;
        }

        private void UnregisterNetworkCallbacks()
        {
            if (_runtimeRoot == null || _runtimeRoot.NetworkManager == null)
            {
                return;
            }

            _runtimeRoot.NetworkManager.OnClientConnectedCallback -= HandleNetworkClientConnected;
            _runtimeRoot.NetworkManager.OnClientDisconnectCallback -= HandleNetworkClientDisconnected;
        }

        private void UnregisterSceneLoadCallbacks()
        {
            if (!_sceneLoadCallbacksRegistered || _runtimeRoot == null || _runtimeRoot.NetworkManager == null || _runtimeRoot.NetworkManager.SceneManager == null)
            {
                _sceneLoadCallbacksRegistered = false;
                return;
            }

            _runtimeRoot.NetworkManager.SceneManager.OnLoadEventCompleted -= HandleSceneLoadEventCompleted;
            _sceneLoadCallbacksRegistered = false;
        }

        private void HandleNetworkClientConnected(ulong clientId)
        {
            LogNetworkPeerEvent("peer-connect", clientId, string.Empty);

            if (_state != MultiplayerSessionState.LobbyActive)
            {
                return;
            }

            UpdateHostStartUnlockGate(0f);
            TryPublishCurrentSnapshot();
            if (clientId != NetworkManager.ServerClientId)
            {
                RequestLobbyRefresh();
            }
        }

        private void HandleNetworkClientDisconnected(ulong clientId)
        {
            NetworkManager networkManager = _runtimeRoot != null ? _runtimeRoot.NetworkManager : null;
            string disconnectReason = ResolveDisconnectDebugReason(clientId, networkManager);
            LogNetworkPeerEvent("peer-disconnect", clientId, disconnectReason);

            if (!IsLobbyTrackedState(_state))
            {
                return;
            }

            if (!_isHost && networkManager != null && clientId == networkManager.LocalClientId)
            {
                BeginFatalShutdown(DisconnectedFromHostMessage);
                return;
            }

            if (_state == MultiplayerSessionState.StartingGameplay)
            {
                if (_isHost && clientId != NetworkManager.ServerClientId)
                {
                    FailPendingGameplayStart(ClientDisconnectedDuringGameplayStartMessage);
                }

                return;
            }

            if (_state == MultiplayerSessionState.InGameplay)
            {
                if (_isHost && clientId != NetworkManager.ServerClientId)
                {
                    BeginFatalShutdown(ClientDisconnectedDuringGameplayMessage);
                    return;
                }
            }

            UpdateHostStartUnlockGate(0f);
            TryPublishCurrentSnapshot();
            if (clientId != NetworkManager.ServerClientId)
            {
                RequestLobbyRefresh();
            }
        }

        private void HandleSceneLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (_state != MultiplayerSessionState.StartingGameplay || _gameplayStartTaskSource == null)
            {
                return;
            }

            if (!string.Equals(sceneName, MultiplayerScenePaths.GamePlaySceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (loadSceneMode != LoadSceneMode.Single)
            {
                return;
            }

            if (clientsTimedOut != null && clientsTimedOut.Count > 0)
            {
                FailPendingGameplayStart("Gameplay start timed out.");
                return;
            }

            int completedCount = clientsCompleted != null ? clientsCompleted.Count : 0;
            if (completedCount < MaxLobbyPlayerCount)
            {
                FailPendingGameplayStart("Gameplay start completed with missing clients.");
                return;
            }

            _gameplayStartTaskSource.TrySetResult(true);
        }

        private MultiplayerSessionSnapshot BuildCurrentSnapshot()
        {
            int connectedPlayerCount = ResolveConnectedPlayerCount();
            bool canStart = ResolveCanStart(connectedPlayerCount);
            string roomTitle = _currentLobby != null ? _currentLobby.Name : string.Empty;
            string joinCode = TryGetLobbyDataValue(RelayJoinCodeDataKey);
            string statusText = ResolveLobbyStatusText(connectedPlayerCount, canStart);

            return new MultiplayerSessionSnapshot(
                hasActiveSession: !string.IsNullOrEmpty(_currentLobby?.Id),
                roomTitle: roomTitle,
                joinCode: joinCode,
                connectedPlayerCount: connectedPlayerCount,
                lobbyStatusText: statusText,
                isHost: _isHost,
                canStart: canStart);
        }

        private int ResolveConnectedPlayerCount()
        {
            int lobbyPlayerCount = _currentLobby != null && _currentLobby.Players != null ? _currentLobby.Players.Count : 0;
            int minimumPlayerCount = !string.IsNullOrEmpty(_currentLobby?.Id) ? 1 : 0;
            NetworkManager networkManager = _runtimeRoot != null ? _runtimeRoot.NetworkManager : null;

            if (_isHost && networkManager != null && networkManager.IsServer && networkManager.IsListening && !networkManager.ShutdownInProgress)
            {
                return Mathf.Clamp(Mathf.Max(networkManager.ConnectedClientsIds.Count, minimumPlayerCount), 0, MaxLobbyPlayerCount);
            }

            return Mathf.Clamp(Mathf.Max(lobbyPlayerCount, minimumPlayerCount), 0, MaxLobbyPlayerCount);
        }

        private bool ResolveCanStart(int connectedPlayerCount)
        {
            return _isHost
                   && connectedPlayerCount == MaxLobbyPlayerCount
                   && _hostStartUnlocked
                   && IsHostNetworkConnectionStable();
        }

        private string ResolveLobbyStatusText(int connectedPlayerCount, bool canStart)
        {
            if (_state == MultiplayerSessionState.StartingGameplay)
            {
                return StartingGameStatusText;
            }

            if (_state == MultiplayerSessionState.InGameplay)
            {
                return InGameplayStatusText;
            }

            string sessionStateValue = TryGetLobbyDataValue(SessionStateDataKey);
            if (string.Equals(sessionStateValue, StartingStateValue, StringComparison.Ordinal))
            {
                return StartingGameStatusText;
            }

            if (string.Equals(sessionStateValue, InGameStateValue, StringComparison.Ordinal))
            {
                return InGameplayStatusText;
            }

            if (connectedPlayerCount < MaxLobbyPlayerCount)
            {
                return WaitingForOtherPlayerStatusText;
            }

            if (_isHost)
            {
                return canStart ? ReadyToStartStatusText : HostStabilizingStatusText;
            }

            if (string.Equals(sessionStateValue, ReadyStateValue, StringComparison.Ordinal))
            {
                return HostCanStartStatusText;
            }

            return ClientWaitingForHostStatusText;
        }

        private string TryGetLobbyDataValue(string key)
        {
            if (_currentLobby == null || _currentLobby.Data == null || string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            return _currentLobby.Data.TryGetValue(key, out DataObject value) && value != null
                ? value.Value ?? string.Empty
                : string.Empty;
        }

        private void PublishSnapshot(MultiplayerSessionSnapshot snapshot)
        {
            _currentSnapshot = snapshot;
            SnapshotChanged?.Invoke(_currentSnapshot);
        }

        private void TryPublishCurrentSnapshot()
        {
            MultiplayerSessionSnapshot snapshot = BuildCurrentSnapshot();
            if (AreSnapshotsEqual(_currentSnapshot, snapshot))
            {
                return;
            }

            PublishSnapshot(snapshot);
        }

        private void SetState(MultiplayerSessionState nextState)
        {
            if (_state == nextState)
            {
                return;
            }

            MultiplayerSessionState previousState = _state;
            _state = nextState;
            LogSessionStateTransition(previousState, nextState);
            StateChanged?.Invoke(_state);
        }

        private void BeginFatalShutdown(string message)
        {
            if (_state == MultiplayerSessionState.Closing || _state == MultiplayerSessionState.Closed)
            {
                return;
            }

            if (IsBusy)
            {
                if (_state == MultiplayerSessionState.StartingGameplay && _gameplayStartTaskSource != null)
                {
                    FailPendingGameplayStart(message);
                }

                return;
            }

            _lastFailureKind = MultiplayerSessionFailureKind.Fatal;
            _lastErrorMessage = message;
            Debug.LogError($"MultiplayerSessionService: {message}");
            AdvanceSessionVersion();
            _currentOperationTask = HandleFatalShutdownAsync(message);
        }

        internal static bool TryLogAvatarConnectionProfileBaseline(
            MultiplayerPlayerAvatar avatar,
            string currentProfile,
            string sourceLabel,
            int inputSequence)
        {
            if (!HasInstance || avatar == null)
            {
                return false;
            }

            Instance.LogAvatarConnectionProfileEvent(
                "avatar-profile-baseline",
                avatar,
                string.Empty,
                currentProfile,
                sourceLabel,
                inputSequence);
            return true;
        }

        internal static bool TryLogAvatarConnectionProfileTransition(
            MultiplayerPlayerAvatar avatar,
            string previousProfile,
            string currentProfile,
            string sourceLabel,
            int inputSequence)
        {
            if (!HasInstance || avatar == null)
            {
                return false;
            }

            Instance.LogAvatarConnectionProfileEvent(
                "avatar-profile-transition",
                avatar,
                previousProfile,
                currentProfile,
                sourceLabel,
                inputSequence);
            return true;
        }

        private void UpdateConnectionDebugPulse(float deltaTime)
        {
            if (!ShouldEmitConnectionDebugPulse())
            {
                _connectionDebugPulseTimer = 0f;
                return;
            }

            _connectionDebugPulseTimer += deltaTime;
            if (_connectionDebugPulseTimer < ConnectionDebugPulseIntervalSeconds)
            {
                return;
            }

            _connectionDebugPulseTimer = 0f;
            WriteConnectionDebugLine(BeginConnectionDebugLine("pulse"), warning: false);
        }

        private bool ShouldEmitConnectionDebugPulse()
        {
            return IsLobbyTrackedState(_state) || IsNetworkSessionAlive();
        }

        private void LogNetworkPeerEvent(string eventName, ulong peerClientId, string reason)
        {
            StringBuilder builder = BeginConnectionDebugLine(eventName);
            builder.Append(' ').Append("peerClientId=").Append(peerClientId);

            if (!string.IsNullOrEmpty(reason))
            {
                builder.Append(' ').Append("reason=").Append(reason);
            }

            bool isDisconnectEvent = string.Equals(eventName, "peer-disconnect", StringComparison.Ordinal);
            NetworkManager networkManager = _runtimeRoot != null ? _runtimeRoot.NetworkManager : null;
            if (isDisconnectEvent && networkManager != null && !string.IsNullOrEmpty(networkManager.DisconnectReason))
            {
                builder.Append(' ').Append("ngoReason=").Append(networkManager.DisconnectReason);
            }

            if (isDisconnectEvent)
            {
                AppendDisconnectInputProfiles(builder);
            }

            WriteConnectionDebugLine(builder, warning: isDisconnectEvent);
        }


        private void LogSessionStateTransition(MultiplayerSessionState previousState, MultiplayerSessionState nextState)
        {
            StringBuilder builder = BeginConnectionDebugLine("state-change");
            builder.Append(' ').Append("from=").Append(previousState);
            builder.Append(' ').Append("to=").Append(nextState);
            WriteConnectionDebugLine(builder, warning: false);
        }

        private void LogGameplaySceneSyncCompleted()
        {
            WriteConnectionDebugLine(BeginConnectionDebugLine("gameplay-sync-complete"), warning: false);
        }

        private void LogAvatarConnectionProfileEvent(
            string eventName,
            MultiplayerPlayerAvatar avatar,
            string previousProfile,
            string currentProfile,
            string sourceLabel,
            int inputSequence)
        {
            StringBuilder builder = BeginConnectionDebugLine(eventName);
            builder.Append(' ').Append("avatar=").Append(avatar.gameObject.name);
            builder.Append(' ').Append("ownerClientId=").Append(avatar.OwnerClientId);
            builder.Append(' ').Append("objectId=").Append(avatar.NetworkObjectId);
            builder.Append(' ').Append("avatarServer=").Append(avatar.IsServer);
            builder.Append(' ').Append("avatarOwnerLocal=").Append(avatar.IsOwner);
            builder.Append(' ').Append("profile=").Append(currentProfile);

            if (!string.IsNullOrEmpty(previousProfile))
            {
                builder.Append(' ').Append("previous=").Append(previousProfile);
            }

            builder.Append(' ').Append("source=").Append(sourceLabel);
            builder.Append(' ').Append("inputSeq=").Append(inputSequence);
            WriteConnectionDebugLine(builder, warning: false);
        }


        private StringBuilder BeginConnectionDebugLine(string eventName)
        {
            StringBuilder builder = _connectionDebugBuilder;
            builder.Clear();
            builder.Append("[MultiplayerConnectionDebug] ");
            builder.Append("seq=").Append(++_connectionDebugSequence).Append(' ');
            builder.Append("t=").Append(Time.realtimeSinceStartup.ToString("F2", CultureInfo.InvariantCulture)).Append(' ');
            builder.Append("app=").Append(ResolveConnectionDebugAppLabel()).Append(' ');
            builder.Append("event=").Append(eventName).Append(' ');
            builder.Append("state=").Append(_state).Append(' ');
            builder.Append("scene=").Append(SceneManager.GetActiveScene().name);
            AppendConnectionDebugNetworkView(builder);
            return builder;
        }

        private string ResolveConnectionDebugAppLabel()
        {
            return $"{(Application.isEditor ? "editor-" : "build-")}{ResolveConnectionDebugRoleLabel()}";
        }

        private string ResolveConnectionDebugRoleLabel()
        {
            if (_isHost || _state == MultiplayerSessionState.CreatingHostSession)
            {
                return "host";
            }

            if (_state == MultiplayerSessionState.JoiningClientSession
                || _state == MultiplayerSessionState.LobbyActive
                || _state == MultiplayerSessionState.StartingGameplay
                || _state == MultiplayerSessionState.InGameplay)
            {
                return "client";
            }

            NetworkManager networkManager = _runtimeRoot != null ? _runtimeRoot.NetworkManager : null;
            if (networkManager != null && networkManager.IsServer)
            {
                return "host";
            }

            return networkManager != null && networkManager.IsClient ? "client" : "unknown";
        }

        private void AppendConnectionDebugNetworkView(StringBuilder builder)
        {
            NetworkManager networkManager = _runtimeRoot != null ? _runtimeRoot.NetworkManager : null;

            builder.Append(' ').Append("localClientId=");
            if (networkManager != null && (networkManager.IsListening || networkManager.IsServer || networkManager.IsClient || networkManager.ShutdownInProgress))
            {
                builder.Append(networkManager.LocalClientId);
            }
            else
            {
                builder.Append("na");
            }

            builder.Append(' ').Append("isServer=").Append(networkManager != null && networkManager.IsServer);
            builder.Append(' ').Append("isClient=").Append(networkManager != null && networkManager.IsClient);
            builder.Append(' ').Append("isConnected=").Append(networkManager != null && networkManager.IsConnectedClient);
            builder.Append(' ').Append("isListening=").Append(networkManager != null && networkManager.IsListening);
            builder.Append(' ').Append("shutdown=").Append(networkManager != null && networkManager.ShutdownInProgress);
            builder.Append(' ').Append("hostPeerCount=");

            if (networkManager != null && networkManager.IsServer && networkManager.IsListening && !networkManager.ShutdownInProgress)
            {
                builder.Append(networkManager.ConnectedClientsIds.Count);
            }
            else
            {
                builder.Append("na");
            }

            int lobbyPlayerCount = _currentLobby != null && _currentLobby.Players != null ? _currentLobby.Players.Count : 0;
            builder.Append(' ').Append("lobbyPlayers=").Append(lobbyPlayerCount);
        }

        private void WriteConnectionDebugLine(StringBuilder builder, bool warning)
        {
            if (warning)
            {
                Debug.LogWarning(builder.ToString());
                return;
            }

            Debug.Log(builder.ToString());
        }

        private void AppendDisconnectInputProfiles(StringBuilder builder)
        {
            MultiplayerPlayerAvatar[] avatars = FindObjectsOfType<MultiplayerPlayerAvatar>();
            if (avatars == null || avatars.Length == 0)
            {
                return;
            }

            bool noActionObservedAffected = false;
            builder.Append(' ').Append("avatarProfiles=");

            for (int i = 0; i < avatars.Length; i++)
            {
                MultiplayerPlayerAvatar avatar = avatars[i];
                if (avatar == null)
                {
                    continue;
                }

                if (i > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append('[').Append(avatar.BuildConnectionDebugProfile()).Append(']');
                noActionObservedAffected |= avatar.HasNoActionDisconnectProfile;
            }

            builder.Append(' ').Append("noActionObservedAffected=").Append(noActionObservedAffected);
        }

        private string ResolveDisconnectDebugReason(ulong clientId, NetworkManager networkManager)
        {
            if (!_isHost && networkManager != null && clientId == networkManager.LocalClientId)
            {
                return DisconnectedFromHostMessage;
            }

            if (_state == MultiplayerSessionState.StartingGameplay
                && _isHost
                && clientId != NetworkManager.ServerClientId)
            {
                return ClientDisconnectedDuringGameplayStartMessage;
            }

            if (_state == MultiplayerSessionState.InGameplay
                && _isHost
                && clientId != NetworkManager.ServerClientId)
            {
                return ClientDisconnectedDuringGameplayMessage;
            }

            return string.Empty;
        }

        private async Task HandleFatalShutdownAsync(string message)
        {
            await ShutdownSessionInternalAsync();
            ReturnToMultiplayerTitleSceneIfNeeded();
            FatalErrorOccurred?.Invoke(message);
        }

        private async void SendLobbyHeartbeatAsync(int sessionVersion)
        {
            if (_heartbeatRequestInFlight
                || string.IsNullOrEmpty(_currentLobby?.Id)
                || !IsSessionVersionCurrent(sessionVersion)
                || !IsLobbyTrackedState(_state))
            {
                return;
            }

            _heartbeatRequestInFlight = true;

            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
                Debug.Log("MultiplayerSessionService: Lobby heartbeat sent.");
            }
            catch (Exception ex)
            {
                if (!IsSessionVersionCurrent(sessionVersion) || _state == MultiplayerSessionState.Closing || _state == MultiplayerSessionState.Closed)
                {
                    return;
                }

                BeginFatalShutdown($"Lobby heartbeat failed: {ex.Message}");
            }
            finally
            {
                if (IsSessionVersionCurrent(sessionVersion))
                {
                    _heartbeatRequestInFlight = false;
                }
            }
        }

        private void UpdateLobbyHeartbeat()
        {
            if (!_heartbeatEnabled || _heartbeatRequestInFlight || string.IsNullOrEmpty(_currentLobby?.Id))
            {
                return;
            }

            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer < LobbyHeartbeatIntervalSeconds)
            {
                return;
            }

            _heartbeatTimer = 0f;
            SendLobbyHeartbeatAsync(_sessionVersion);
        }

        private void UpdateLobbyPollFallback()
        {
            if (string.IsNullOrEmpty(_currentLobby?.Id))
            {
                return;
            }

            _lobbyPollTimer += Time.deltaTime;
            if (_lobbyPollTimer < LobbyPollIntervalSeconds)
            {
                return;
            }

            _lobbyPollTimer = 0f;
            RequestLobbyRefresh();
        }

        private void UpdateHostStartUnlockGate(float deltaTime)
        {
            if (!_isHost)
            {
                _hostStartStableTimer = 0f;
                _hostStartUnlocked = false;
                return;
            }

            bool shouldKeepCounting = _state == MultiplayerSessionState.LobbyActive
                                      && !string.IsNullOrEmpty(_currentLobby?.Id)
                                      && ResolveConnectedPlayerCount() == MaxLobbyPlayerCount
                                      && IsHostNetworkConnectionStable();

            if (!shouldKeepCounting)
            {
                bool wasUnlocked = _hostStartUnlocked;
                _hostStartStableTimer = 0f;
                _hostStartUnlocked = false;
                if (wasUnlocked)
                {
                    TryPublishCurrentSnapshot();
                }

                return;
            }

            if (_hostStartUnlocked)
            {
                return;
            }

            _hostStartStableTimer = Mathf.Min(HostStartUnlockStableDurationSeconds, _hostStartStableTimer + deltaTime);
            if (_hostStartStableTimer < HostStartUnlockStableDurationSeconds)
            {
                return;
            }

            _hostStartUnlocked = true;
            TryPublishCurrentSnapshot();
        }

        private void SyncHostLobbySessionStateIfNeeded()
        {
            if (!_isHost || !IsLobbyTrackedState(_state) || string.IsNullOrEmpty(_currentLobby?.Id) || _lobbySessionStateUpdateInFlight)
            {
                return;
            }

            string desiredSessionStateValue = ResolveHostLobbySessionStateValue();
            string currentSessionStateValue = TryGetLobbyDataValue(SessionStateDataKey);
            if (string.Equals(currentSessionStateValue, desiredSessionStateValue, StringComparison.Ordinal))
            {
                return;
            }

            _ = UpdateHostLobbySessionStateAsync(desiredSessionStateValue, _sessionVersion);
        }

        private async Task UpdateHostLobbySessionStateAsync(string desiredSessionStateValue, int sessionVersion)
        {
            if (_lobbySessionStateUpdateInFlight
                || !_isHost
                || !IsLobbyTrackedState(_state)
                || string.IsNullOrEmpty(_currentLobby?.Id)
                || !IsSessionVersionCurrent(sessionVersion))
            {
                return;
            }

            _lobbySessionStateUpdateInFlight = true;

            try
            {
                Dictionary<string, DataObject> data = _currentLobby.Data != null
                    ? new Dictionary<string, DataObject>(_currentLobby.Data)
                    : new Dictionary<string, DataObject>();
                data[SessionStateDataKey] = new DataObject(DataObject.VisibilityOptions.Public, desiredSessionStateValue);

                Lobby updatedLobby = await LobbyService.Instance.UpdateLobbyAsync(
                    _currentLobby.Id,
                    new UpdateLobbyOptions
                    {
                        Data = data
                    });

                if (!IsSessionVersionCurrent(sessionVersion) || !IsLobbyTrackedState(_state))
                {
                    return;
                }

                _currentLobby = updatedLobby;
                TryPublishCurrentSnapshot();
            }
            catch (Exception ex)
            {
                if (!IsSessionVersionCurrent(sessionVersion) || _state == MultiplayerSessionState.Closing || _state == MultiplayerSessionState.Closed)
                {
                    return;
                }

                if (IsLobbyClosedFailure(ex))
                {
                    BeginFatalShutdown(LobbyClosedMessage);
                    return;
                }

                Debug.LogWarning($"MultiplayerSessionService: Lobby state update failed. {ex.Message}");
            }
            finally
            {
                if (IsSessionVersionCurrent(sessionVersion))
                {
                    _lobbySessionStateUpdateInFlight = false;
                }
            }
        }

        private async Task ShutdownSessionInternalAsync()
        {
            SetState(MultiplayerSessionState.Closing);

            _heartbeatEnabled = false;
            _heartbeatTimer = 0f;
            UnregisterSceneLoadCallbacks();
            ClearGameplayStartTracking();

            await UnsubscribeLobbyEventsSafeAsync();
            await DeleteOrLeaveLobbySafeAsync();
            await ShutdownNetworkSafeAsync();

            UnregisterNetworkCallbacks();
            ClearCachedSessionState();
            PublishSnapshot(default);
            SetState(MultiplayerSessionState.Closed);

            Debug.Log("MultiplayerSessionService: Cleanup complete.");
            _currentOperationTask = null;
        }

        private Task UnsubscribeLobbyEventsSafeAsync()
        {
#if UGS_BETA_LOBBY_EVENTS && UGS_LOBBY_EVENTS
            return UnsubscribeLobbyEventsSafeInternalAsync();
#else
            return Task.CompletedTask;
#endif
        }

#if UGS_BETA_LOBBY_EVENTS && UGS_LOBBY_EVENTS
        private async Task UnsubscribeLobbyEventsSafeInternalAsync()
        {
            if (_lobbyEvents == null)
            {
                return;
            }

            try
            {
                await _lobbyEvents.UnsubscribeAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"MultiplayerSessionService: Lobby event unsubscribe failed. {ex.Message}");
            }
            finally
            {
                _lobbyEvents = null;
            }
        }
#endif

        private async Task DeleteOrLeaveLobbySafeAsync()
        {
            if (string.IsNullOrEmpty(_currentLobby?.Id))
            {
                return;
            }

            try
            {
                if (_isHost)
                {
                    Debug.Log($"MultiplayerSessionService: Deleting lobby {_currentLobby.Id}.");
                    await LobbyService.Instance.DeleteLobbyAsync(_currentLobby.Id);
                }
                else if (!string.IsNullOrEmpty(_localPlayerId))
                {
                    Debug.Log($"MultiplayerSessionService: Leaving lobby {_currentLobby.Id} as player {_localPlayerId}.");
                    await LobbyService.Instance.RemovePlayerAsync(_currentLobby.Id, _localPlayerId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"MultiplayerSessionService: Lobby cleanup failed. {ex.Message}");
            }
        }

        private async Task ShutdownNetworkSafeAsync()
        {
            if (_runtimeRoot == null || _runtimeRoot.NetworkManager == null)
            {
                return;
            }

            NetworkManager networkManager = _runtimeRoot.NetworkManager;
            if (!networkManager.IsServer && !networkManager.IsClient && !networkManager.ShutdownInProgress)
            {
                return;
            }

            networkManager.Shutdown();

            int guardFrames = 120;
            while (guardFrames-- > 0 && (networkManager.IsServer || networkManager.IsClient || networkManager.ShutdownInProgress))
            {
                await Task.Yield();
            }

            if (networkManager.IsServer || networkManager.IsClient || networkManager.ShutdownInProgress)
            {
                Debug.LogWarning("MultiplayerSessionService: NetworkManager shutdown wait timed out.");
            }
        }

        private void ClearCachedSessionState()
        {
            _currentLobby = null;
            _isHost = false;
            _localPlayerId = string.Empty;
            _refreshLobbyTask = null;
            _heartbeatEnabled = false;
            _heartbeatRequestInFlight = false;
            _lobbySessionStateUpdateInFlight = false;
            _hostStartUnlocked = false;
            _sceneLoadCallbacksRegistered = false;
            _heartbeatTimer = 0f;
            _lobbyPollTimer = 0f;
            _hostStartStableTimer = 0f;
            _gameplayStartTaskSource = null;
        }


        private int AdvanceSessionVersion()
        {
            unchecked
            {
                _sessionVersion++;
            }

            if (_sessionVersion == 0)
            {
                _sessionVersion = 1;
            }

            return _sessionVersion;
        }

        private bool IsSessionVersionCurrent(int sessionVersion)
        {
            return sessionVersion != 0 && sessionVersion == _sessionVersion;
        }

        private static string NormalizeJoinCode(string rawJoinCode)
        {
            if (string.IsNullOrWhiteSpace(rawJoinCode))
            {
                return string.Empty;
            }

            return rawJoinCode.Trim().ToUpperInvariant();
        }

        private static bool IsValidJoinCode(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode) || joinCode.Length != 6)
            {
                return false;
            }

            for (int i = 0; i < joinCode.Length; i++)
            {
                if (!char.IsLetterOrDigit(joinCode[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsWrongJoinCodeFailure(Exception exception)
        {
            if (exception is WrongJoinCodeException)
            {
                return true;
            }

            if (exception is LobbyServiceException lobbyException)
            {
                return lobbyException.Reason == LobbyExceptionReason.EntityNotFound
                       || lobbyException.Reason == LobbyExceptionReason.NoOpenLobbies;
            }

            if (exception is RelayServiceException relayException)
            {
                return relayException.Reason == RelayExceptionReason.InvalidRequest
                       || relayException.Reason == RelayExceptionReason.InvalidArgument
                       || relayException.Reason == RelayExceptionReason.AllocationNotFound
                       || relayException.Reason == RelayExceptionReason.JoinCodeNotFound
                       || relayException.Reason == RelayExceptionReason.EntityNotFound;
            }

            return false;
        }

        private static bool IsLobbyClosedFailure(Exception exception)
        {
            if (exception is not LobbyServiceException lobbyException)
            {
                return false;
            }

            return lobbyException.Reason == LobbyExceptionReason.EntityNotFound
                   || lobbyException.Reason == LobbyExceptionReason.Forbidden;
        }

        private static bool IsLobbyMembershipFailure(Exception exception)
        {
            return exception is LobbyServiceException lobbyException
                   && lobbyException.Reason == LobbyExceptionReason.PlayerNotFound;
        }

        private bool IsHostNetworkConnectionStable()
        {
            if (!_isHost || _runtimeRoot == null || _runtimeRoot.NetworkManager == null)
            {
                return false;
            }

            NetworkManager networkManager = _runtimeRoot.NetworkManager;
            return networkManager.IsServer
                   && networkManager.IsClient
                   && networkManager.IsListening
                   && !networkManager.ShutdownInProgress
                   && networkManager.ConnectedClientsIds.Count == MaxLobbyPlayerCount;
        }

        private string ResolveHostLobbySessionStateValue()
        {
            if (_state == MultiplayerSessionState.StartingGameplay)
            {
                return StartingStateValue;
            }

            if (_state == MultiplayerSessionState.InGameplay)
            {
                return InGameStateValue;
            }

            return _hostStartUnlocked ? ReadyStateValue : WaitingStateValue;
        }

        private bool IsNetworkSessionAlive()
        {
            return _runtimeRoot != null
                   && _runtimeRoot.NetworkManager != null
                   && (_runtimeRoot.NetworkManager.IsServer || _runtimeRoot.NetworkManager.IsClient || _runtimeRoot.NetworkManager.ShutdownInProgress);
        }

        private static void EnsureNetworkManagerIsIdle(NetworkManager networkManager)
        {
            if (networkManager == null)
            {
                throw new InvalidOperationException("NetworkManager is missing.");
            }

            if (networkManager.IsServer || networkManager.IsClient || networkManager.ShutdownInProgress)
            {
                throw new InvalidOperationException("NetworkManager is already running or shutting down.");
            }
        }

        private static void ReturnToMultiplayerTitleSceneIfNeeded()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!MultiplayerScenePaths.IsMultiplayerTitleScene(activeScene.path))
            {
                SceneManager.LoadScene(MultiplayerScenePaths.TitleScenePath);
            }
        }

        private static bool AreSnapshotsEqual(MultiplayerSessionSnapshot left, MultiplayerSessionSnapshot right)
        {
            return left.HasActiveSession == right.HasActiveSession
                   && left.ConnectedPlayerCount == right.ConnectedPlayerCount
                   && left.IsHost == right.IsHost
                   && left.CanStart == right.CanStart
                   && string.Equals(left.RoomTitle, right.RoomTitle, StringComparison.Ordinal)
                   && string.Equals(left.JoinCode, right.JoinCode, StringComparison.Ordinal)
                   && string.Equals(left.LobbyStatusText, right.LobbyStatusText, StringComparison.Ordinal);
        }

        private void FailPendingGameplayStart(string message)
        {
            if (_gameplayStartTaskSource != null && !_gameplayStartTaskSource.Task.IsCompleted)
            {
                _gameplayStartTaskSource.TrySetException(new InvalidOperationException(message));
                return;
            }

            BeginFatalShutdown(message);
        }

        private void ClearGameplayStartTracking()
        {
            _gameplayStartTaskSource = null;
        }

        private static bool IsLobbyTrackedState(MultiplayerSessionState state)
        {
            return state == MultiplayerSessionState.LobbyActive
                   || state == MultiplayerSessionState.StartingGameplay
                   || state == MultiplayerSessionState.InGameplay;
        }
    }
}
