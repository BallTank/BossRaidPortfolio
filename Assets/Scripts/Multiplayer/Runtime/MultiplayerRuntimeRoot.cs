using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Core.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerRuntimeRoot : MonoBehaviour
    {
        private const string PlayerAvatarResourcePath = "Multiplayer/MultiplayerPlayerAvatar";
        private const uint DefaultNetworkTickRate = 60;

        private static MultiplayerRuntimeRoot _instance;
        private bool _isPlayerAvatarPrefabRegistered;
        private bool _didWarnMissingPlayerAvatarPrefab;

        [Header("Network Tick")]
        [SerializeField] private uint _networkTickRate = DefaultNetworkTickRate;

        public static bool HasInstance => _instance != null;
        public static MultiplayerRuntimeRoot Instance => GetOrCreateInstance();

        public NetworkManager NetworkManager { get; private set; }
        public UnityTransport UnityTransport { get; private set; }
        public GameObject PlayerAvatarPrefab { get; private set; }
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

            PlayerAvatarPrefab = Resources.Load<GameObject>(PlayerAvatarResourcePath);
            if (PlayerAvatarPrefab == null && !_didWarnMissingPlayerAvatarPrefab)
            {
                _didWarnMissingPlayerAvatarPrefab = true;
                Debug.LogWarning($"MultiplayerRuntimeRoot: Could not load player avatar prefab at Resources/{PlayerAvatarResourcePath}.prefab");
            }
        }

        private void RegisterPlayerAvatarPrefab()
        {
            if (_isPlayerAvatarPrefabRegistered || PlayerAvatarPrefab == null || NetworkManager == null)
            {
                return;
            }

            NetworkManager.AddNetworkPrefab(PlayerAvatarPrefab);
            _isPlayerAvatarPrefabRegistered = true;
        }

    }
}
