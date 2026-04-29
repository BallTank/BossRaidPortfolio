using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System.Reflection;
using System.Linq;
using UnityEngine.SceneManagement;

namespace Core.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerRuntimeRoot : MonoBehaviour
    {
        private const uint DefaultNetworkTickRate = 60;
        private const ushort MultiplayerProtocolVersion = 0x0429;
        private const uint StablePlayerAvatarNetworkPrefabHash = 0x4D505041; // "MPPA"
        private static readonly FieldInfo NetworkObjectGlobalObjectIdHashField =
            typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);

        private static MultiplayerRuntimeRoot _instance;
        private bool _arePlayerAvatarPrefabsRegistered;
        private bool _didWarnMissingPlayerAvatarPrefab;
        private bool _didLogPlayerAvatarPrefabResolution;

        [Header("Network Tick")]
        [SerializeField] private uint _networkTickRate = DefaultNetworkTickRate;

        public static bool HasInstance => _instance != null;
        public static MultiplayerRuntimeRoot Instance => GetOrCreateInstance();

        public NetworkManager NetworkManager { get; private set; }
        public UnityTransport UnityTransport { get; private set; }
        public GameObject PlayerAvatarPrefab { get; private set; }
        public GameObject HostPlayerAvatarPrefab { get; private set; }
        public GameObject ClientPlayerAvatarPrefab { get; private set; }
        public bool HasPlayerAvatarPrefabs => PlayerAvatarPrefab != null;
        public MultiplayerBossAuthorityBridge BossAuthorityBridge { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        private static MultiplayerRuntimeRoot GetOrCreateInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject host = new GameObject("MultiplayerRuntimeRoot");
            _instance = host.AddComponent<MultiplayerRuntimeRoot>();
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

            EnsureConfigured();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void EnsureConfigured()
        {
            EnsureComponents();
            EnsurePlayerAvatarPrefabLoaded();
            ConfigureNetworkManager();
        }

        private void EnsureComponents()
        {
            UnityTransport = GetComponent<UnityTransport>();
            if (UnityTransport == null)
            {
                UnityTransport = gameObject.AddComponent<UnityTransport>();
            }

            NetworkManager = GetComponent<NetworkManager>();
            if (NetworkManager == null)
            {
                NetworkManager = gameObject.AddComponent<NetworkManager>();
            }

            BossAuthorityBridge = GetComponent<MultiplayerBossAuthorityBridge>();
            if (BossAuthorityBridge == null)
            {
                BossAuthorityBridge = gameObject.AddComponent<MultiplayerBossAuthorityBridge>();
            }
        }

        private void ConfigureNetworkManager()
        {
            if (NetworkManager.NetworkConfig == null)
            {
                NetworkManager.NetworkConfig = new NetworkConfig();
            }

            NetworkManager.NetworkConfig.NetworkTransport = UnityTransport;
            NetworkManager.NetworkConfig.EnableSceneManagement = true;
            NetworkManager.NetworkConfig.PlayerPrefab = null;
            NetworkManager.NetworkConfig.ProtocolVersion = MultiplayerProtocolVersion;
            NetworkManager.NetworkConfig.TickRate = _networkTickRate == 0 ? 1u : _networkTickRate;
            NetworkManager.RunInBackground = true;
            RegisterPlayerAvatarPrefab();
        }

        private void EnsurePlayerAvatarPrefabLoaded()
        {
            if (PlayerAvatarPrefab != null)
            {
                return;
            }

            Object rawRuntimeConfig = Resources.Load(MultiplayerRuntimeConfig.ResourcePath);
            MultiplayerRuntimeConfig runtimeConfig = rawRuntimeConfig as MultiplayerRuntimeConfig
                                                     ?? MultiplayerRuntimeConfig.LoadFromResources();
            PlayerAvatarPrefab = runtimeConfig != null ? runtimeConfig.PlayerAvatarPrefab : null;
            HostPlayerAvatarPrefab = runtimeConfig != null ? runtimeConfig.HostPlayerAvatarPrefab : null;
            ClientPlayerAvatarPrefab = runtimeConfig != null ? runtimeConfig.ClientPlayerAvatarPrefab : null;

            if (PlayerAvatarPrefab == null)
            {
                PlayerAvatarPrefab = HostPlayerAvatarPrefab != null ? HostPlayerAvatarPrefab : ClientPlayerAvatarPrefab;
            }

            if (HostPlayerAvatarPrefab == null)
            {
                HostPlayerAvatarPrefab = PlayerAvatarPrefab;
            }

            if (ClientPlayerAvatarPrefab == null)
            {
                ClientPlayerAvatarPrefab = PlayerAvatarPrefab;
            }

            LogPlayerAvatarPrefabResolution(rawRuntimeConfig, runtimeConfig);

            if (!HasPlayerAvatarPrefabs && !_didWarnMissingPlayerAvatarPrefab)
            {
                _didWarnMissingPlayerAvatarPrefab = true;
                string validationMessage = runtimeConfig != null
                    ? runtimeConfig.BuildValidationMessage()
                    : $"Multiplayer runtime config asset is missing. Create or restore {MultiplayerRuntimeConfig.AssetPath}.";
                Debug.LogError(validationMessage);
            }
        }

        public GameObject GetPlayerAvatarPrefabForClient(ulong clientId)
        {
            EnsurePlayerAvatarPrefabLoaded();
            return PlayerAvatarPrefab;
        }

        private void RegisterPlayerAvatarPrefab()
        {
            if (_arePlayerAvatarPrefabsRegistered || NetworkManager == null)
            {
                return;
            }

            _arePlayerAvatarPrefabsRegistered = TryRegisterPlayerAvatarPrefab(PlayerAvatarPrefab);
        }

        private bool TryRegisterPlayerAvatarPrefab(GameObject playerAvatarPrefab)
        {
            if (playerAvatarPrefab == null || NetworkManager == null || NetworkManager.NetworkConfig == null)
            {
                return false;
            }

            if (!playerAvatarPrefab.TryGetComponent(out NetworkObject networkObject))
            {
                Debug.LogError($"MultiplayerRuntimeRoot: Player avatar prefab '{playerAvatarPrefab.name}' is missing a NetworkObject component.");
                return false;
            }

            uint targetHash = ResolveNetworkObjectSourceHash(networkObject);
            if (targetHash == 0)
            {
                Debug.LogError($"MultiplayerRuntimeRoot: Could not resolve NetworkObject source hash for prefab '{playerAvatarPrefab.name}'.");
                return false;
            }

            NetworkPrefab stablePrefabRegistration = new NetworkPrefab
            {
                Override = NetworkPrefabOverride.Hash,
                Prefab = playerAvatarPrefab,
                SourceHashToOverride = StablePlayerAvatarNetworkPrefabHash,
                OverridingTargetPrefab = playerAvatarPrefab
            };

            if (IsNetworkPrefabAlreadyRegistered(playerAvatarPrefab, StablePlayerAvatarNetworkPrefabHash, targetHash))
            {
                Debug.Log(
                    $"[MPDiag][PrefabRegister] skip prefab={playerAvatarPrefab.name} " +
                    $"sourceHash={StablePlayerAvatarNetworkPrefabHash} targetHash={targetHash} reason=already_registered");
                return false;
            }

            NetworkManager.NetworkConfig.Prefabs.Add(stablePrefabRegistration);
            Debug.Log(
                $"[MPDiag][PrefabRegister] added prefab={playerAvatarPrefab.name} " +
                $"sourceHash={StablePlayerAvatarNetworkPrefabHash} targetHash={targetHash}");
            return true;
        }

        public void LogNetworkConfigFingerprint(string role, string phase)
        {
            if (NetworkManager == null || NetworkManager.NetworkConfig == null)
            {
                Debug.LogWarning($"[MPDiag][ConfigFingerprint] role={role} phase={phase} status=network_manager_unavailable");
                return;
            }

            uint playerPrefabSourceHash = 0;
            if (PlayerAvatarPrefab != null && PlayerAvatarPrefab.TryGetComponent(out NetworkObject networkObject))
            {
                playerPrefabSourceHash = ResolveNetworkObjectSourceHash(networkObject);
            }

            ulong configHash = NetworkManager.NetworkConfig.GetConfig(false);
            string prefabOverrideHashes = DescribePrefabOverrideHashes(NetworkManager.NetworkConfig.Prefabs);

            Debug.Log(
                $"[MPDiag][ConfigFingerprint] role={role} phase={phase} " +
                $"appVersion={Application.version} isEditor={Application.isEditor} platform={Application.platform} " +
                $"configHash={configHash} " +
                $"protocolVersion={NetworkManager.NetworkConfig.ProtocolVersion} " +
                $"tickRate={NetworkManager.NetworkConfig.TickRate} " +
                $"connectionApproval={NetworkManager.NetworkConfig.ConnectionApproval} " +
                $"forceSamePrefabs={NetworkManager.NetworkConfig.ForceSamePrefabs} " +
                $"enableSceneManagement={NetworkManager.NetworkConfig.EnableSceneManagement} " +
                $"ensureNetworkVariableLengthSafety={NetworkManager.NetworkConfig.EnsureNetworkVariableLengthSafety} " +
                $"rpcHashSize={NetworkManager.NetworkConfig.RpcHashSize} " +
                $"transport={DescribeTransport(NetworkManager.NetworkConfig.NetworkTransport)} " +
                $"playerPrefab={DescribeObject(PlayerAvatarPrefab)} " +
                $"playerPrefabSourceHash={playerPrefabSourceHash} " +
                $"stablePlayerPrefabHash={StablePlayerAvatarNetworkPrefabHash} " +
                $"prefabOverrideHashes={prefabOverrideHashes}");
        }

        public void LogNetworkManagerIdentity(string role, string phase)
        {
            NetworkManager runtimeNetworkManager = NetworkManager;
            NetworkManager singletonNetworkManager = NetworkManager.Singleton;
            NetworkManager[] networkManagers = FindObjectsOfType<NetworkManager>(true);
            string managerList = string.Join(
                ",",
                networkManagers.Select(DescribeNetworkManagerInstance));

            Debug.Log(
                $"[MPDiag][ManagerIdentity] role={role} phase={phase} " +
                $"runtimeRoot={GetInstanceID()}:{name} " +
                $"runtimeNetworkManager={DescribeNetworkManagerInstance(runtimeNetworkManager)} " +
                $"singletonNetworkManager={DescribeNetworkManagerInstance(singletonNetworkManager)} " +
                $"sameSingleton={ReferenceEquals(runtimeNetworkManager, singletonNetworkManager)} " +
                $"networkManagerCount={networkManagers.Length} " +
                $"networkManagers=[{managerList}]");
        }

        private bool IsNetworkPrefabAlreadyRegistered(GameObject playerAvatarPrefab, uint sourceHash, uint targetHash)
        {
            NetworkPrefabs networkPrefabs = NetworkManager.NetworkConfig.Prefabs;
            if (networkPrefabs == null)
            {
                return false;
            }

            if (networkPrefabs.NetworkPrefabOverrideLinks.ContainsKey(sourceHash))
            {
                return true;
            }

            var runtimePrefabs = networkPrefabs.Prefabs;
            for (int i = 0; i < runtimePrefabs.Count; i++)
            {
                NetworkPrefab networkPrefab = runtimePrefabs[i];
                if (networkPrefab == null)
                {
                    continue;
                }

                if (networkPrefab.Prefab == playerAvatarPrefab)
                {
                    return true;
                }

                if (TryResolveSourceHash(networkPrefab, out uint existingSourceHash) && existingSourceHash == sourceHash)
                {
                    return true;
                }

                if (TryResolveTargetHash(networkPrefab, out uint existingTargetHash) && existingTargetHash == targetHash)
                {
                    return true;
                }
            }

            for (int listIndex = 0; listIndex < networkPrefabs.NetworkPrefabsLists.Count; listIndex++)
            {
                NetworkPrefabsList networkPrefabsList = networkPrefabs.NetworkPrefabsLists[listIndex];
                if (networkPrefabsList == null)
                {
                    continue;
                }

                var prefabList = networkPrefabsList.PrefabList;
                for (int prefabIndex = 0; prefabIndex < prefabList.Count; prefabIndex++)
                {
                    NetworkPrefab networkPrefab = prefabList[prefabIndex];
                    if (networkPrefab == null)
                    {
                        continue;
                    }

                    if (networkPrefab.Prefab == playerAvatarPrefab)
                    {
                        return true;
                    }

                    if (TryResolveSourceHash(networkPrefab, out uint existingSourceHash) && existingSourceHash == sourceHash)
                    {
                        return true;
                    }

                    if (TryResolveTargetHash(networkPrefab, out uint existingTargetHash) && existingTargetHash == targetHash)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static uint ResolveNetworkObjectSourceHash(NetworkObject networkObject)
        {
            if (networkObject == null || NetworkObjectGlobalObjectIdHashField == null)
            {
                return 0;
            }

            object rawValue = NetworkObjectGlobalObjectIdHashField.GetValue(networkObject);
            return rawValue is uint sourceHash ? sourceHash : 0;
        }

        private static bool TryResolveSourceHash(NetworkPrefab networkPrefab, out uint sourceHash)
        {
            sourceHash = 0;
            if (networkPrefab == null)
            {
                return false;
            }

            try
            {
                sourceHash = networkPrefab.SourcePrefabGlobalObjectIdHash;
                return sourceHash != 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveTargetHash(NetworkPrefab networkPrefab, out uint targetHash)
        {
            targetHash = 0;
            if (networkPrefab == null)
            {
                return false;
            }

            try
            {
                targetHash = networkPrefab.TargetPrefabGlobalObjectIdHash;
                return targetHash != 0;
            }
            catch
            {
                return false;
            }
        }

        private void LogPlayerAvatarPrefabResolution(Object rawRuntimeConfig, MultiplayerRuntimeConfig runtimeConfig)
        {
            if (_didLogPlayerAvatarPrefabResolution)
            {
                return;
            }

            _didLogPlayerAvatarPrefabResolution = true;
            string rawType = rawRuntimeConfig != null ? rawRuntimeConfig.GetType().Name : "null";
            Debug.Log(
                $"[MPDiag][RuntimeConfig] path=Resources/{MultiplayerRuntimeConfig.ResourcePath}.asset " +
                $"rawAsset={DescribeObject(rawRuntimeConfig)} rawType={rawType} " +
                $"typedConfig={DescribeObject(runtimeConfig)} " +
                $"playerPrefab={DescribeObject(PlayerAvatarPrefab)} " +
                $"hostPrefab={DescribeObject(HostPlayerAvatarPrefab)} " +
                $"clientPrefab={DescribeObject(ClientPlayerAvatarPrefab)}");
        }

        private static string DescribeObject(Object unityObject)
        {
            if (unityObject == null)
            {
                return "null";
            }

            return $"{unityObject.name}<{unityObject.GetType().Name}>";
        }

        private static string DescribeTransport(NetworkTransport networkTransport)
        {
            return networkTransport == null ? "null" : networkTransport.GetType().Name;
        }

        private static string DescribeNetworkManagerInstance(NetworkManager networkManager)
        {
            if (networkManager == null)
            {
                return "null";
            }

            Scene scene = networkManager.gameObject.scene;
            string sceneName = scene.IsValid() ? scene.name : "NoScene";
            return $"{networkManager.GetInstanceID()}:{networkManager.name}@{sceneName}";
        }

        private static string DescribePrefabOverrideHashes(NetworkPrefabs networkPrefabs)
        {
            if (networkPrefabs == null || networkPrefabs.NetworkPrefabOverrideLinks == null)
            {
                return "none";
            }

            return string.Join(
                ",",
                networkPrefabs.NetworkPrefabOverrideLinks.Keys
                    .OrderBy(hash => hash)
                    .Select(hash => hash.ToString()));
        }

    }
}
