using UnityEngine;

namespace Core.Multiplayer
{
    [CreateAssetMenu(menuName = "Boss Raid/Multiplayer Runtime Config")]
    public sealed class MultiplayerRuntimeConfig : ScriptableObject
    {
        public const string ResourcePath = "MultiplayerRuntimeConfig";
        public const string AssetPath = "Assets/Resources/MultiplayerRuntimeConfig.asset";

        [SerializeField] private GameObject _playerAvatarPrefab;
        [SerializeField] private GameObject _hostPlayerAvatarPrefab;
        [SerializeField] private GameObject _clientPlayerAvatarPrefab;

        public GameObject PlayerAvatarPrefab => _playerAvatarPrefab != null
            ? _playerAvatarPrefab
            : (_hostPlayerAvatarPrefab != null ? _hostPlayerAvatarPrefab : _clientPlayerAvatarPrefab);
        public GameObject HostPlayerAvatarPrefab => _hostPlayerAvatarPrefab != null ? _hostPlayerAvatarPrefab : _playerAvatarPrefab;
        public GameObject ClientPlayerAvatarPrefab => _clientPlayerAvatarPrefab != null ? _clientPlayerAvatarPrefab : _playerAvatarPrefab;

        public bool HasResolvedPlayerAvatarPrefabs => PlayerAvatarPrefab != null;

        public bool HasDirectPlayerAvatarPrefabReference => _playerAvatarPrefab != null;
        public bool HasDirectHostPlayerAvatarPrefabReference => _hostPlayerAvatarPrefab != null;
        public bool HasDirectClientPlayerAvatarPrefabReference => _clientPlayerAvatarPrefab != null;

        public static MultiplayerRuntimeConfig LoadFromResources()
        {
            return Resources.Load<MultiplayerRuntimeConfig>(ResourcePath);
        }

        public string BuildValidationMessage()
        {
            if (HasResolvedPlayerAvatarPrefabs)
            {
                return "Multiplayer runtime config is valid. NGO will use one shared network player prefab. Host/client-specific prefab fields are optional.";
            }

            return $"Multiplayer runtime config is invalid. " +
                   $"playerPrefab={DescribeGameObject(_playerAvatarPrefab)}, " +
                   $"hostPrefab={DescribeGameObject(_hostPlayerAvatarPrefab)}, " +
                   $"clientPrefab={DescribeGameObject(_clientPlayerAvatarPrefab)}, " +
                   $"resolvedNetworkPrefab={DescribeGameObject(PlayerAvatarPrefab)}. " +
                   $"Open {AssetPath} and assign at least one valid player prefab.";
        }

        private static string DescribeGameObject(GameObject prefab)
        {
            return prefab == null ? "null" : prefab.name;
        }
    }
}
