using System;
using System.Collections.Generic;
using Core.Boss;
using Core.Combat;
using Core.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Multiplayer
{
    public static class MultiplayerGameplaySceneCoordinator
    {
        private const float PlayerSpawnSpacing = 3f;
        private static bool _hasLegacySpawnSnapshot;
        private static Vector3 _legacySpawnPosition;
        private static Quaternion _legacySpawnRotation;
        private static Vector3 _legacySpawnRight = Vector3.right;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStaticState()
        {
            MultiplayerLocalPlayerRegistry.Clear();
            ResetLegacySpawnSnapshot();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            MultiplayerLocalPlayerRegistry.Clear();
            ResetLegacySpawnSnapshot();
            PrepareScene(scene);
        }

        public static void EnsureCurrentGameplayScenePrepared()
        {
            PrepareScene(SceneManager.GetActiveScene());
        }

        public static void TrySpawnAuthoritativePlayers()
        {
            if (!ShouldPrepareCurrentScene() || !MultiplayerRuntimeRoot.HasInstance)
            {
                return;
            }

            NetworkManager networkManager = MultiplayerRuntimeRoot.Instance.NetworkManager;
            if (networkManager == null || !networkManager.IsServer || !networkManager.IsListening)
            {
                return;
            }

            GameObject playerAvatarPrefab = MultiplayerRuntimeRoot.Instance.PlayerAvatarPrefab;
            if (playerAvatarPrefab == null)
            {
                Debug.LogWarning("MultiplayerGameplaySceneCoordinator: Player avatar prefab is missing.");
                return;
            }

            List<ulong> connectedClientIds = new List<ulong>(networkManager.ConnectedClientsIds);
            connectedClientIds.Sort();
            if (connectedClientIds.Count < 2)
            {
                Debug.LogWarning("MultiplayerGameplaySceneCoordinator: Expected two connected clients before player spawn.");
                return;
            }

            EnsureCurrentGameplayScenePrepared();
            Scene activeScene = SceneManager.GetActiveScene();
            Vector3 spawnCenter = ResolveSpawnPosition(activeScene);
            Quaternion spawnRotation = ResolveSpawnRotation(activeScene);
            Vector3 lateralAxis = ResolveSpawnRight(activeScene);
            float centerIndex = (connectedClientIds.Count - 1) * 0.5f;

            for (int i = 0; i < connectedClientIds.Count; i++)
            {
                ulong clientId = connectedClientIds[i];
                if (networkManager.SpawnManager.GetPlayerNetworkObject(clientId) != null)
                {
                    continue;
                }

                GameObject avatarInstance = UnityEngine.Object.Instantiate(playerAvatarPrefab);
                float lateralOffset = (i - centerIndex) * PlayerSpawnSpacing;
                avatarInstance.name = clientId == NetworkManager.ServerClientId
                    ? "hostPlayer"
                    : "clientPlayer";
                avatarInstance.transform.SetPositionAndRotation(
                    spawnCenter + lateralAxis * lateralOffset,
                    spawnRotation);

                NetworkObject networkObject = avatarInstance.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    UnityEngine.Object.Destroy(avatarInstance);
                    Debug.LogWarning("MultiplayerGameplaySceneCoordinator: Spawned avatar is missing NetworkObject.");
                    return;
                }

                networkObject.SpawnAsPlayerObject(clientId, true);
            }

            RebindServerBossTarget(networkManager, connectedClientIds);
        }

        private static bool ShouldPrepareCurrentScene()
        {
            return ShouldPrepareScene(SceneManager.GetActiveScene());
        }

        private static bool ShouldPrepareScene(Scene scene)
        {
            if (!scene.IsValid()
                || !MultiplayerSessionService.HasInstance
                || !MultiplayerSessionService.Instance.HasActiveSession)
            {
                return false;
            }

            return string.Equals(scene.path, MultiplayerScenePaths.GamePlayScenePath, StringComparison.OrdinalIgnoreCase);
        }

        private static void PrepareScene(Scene scene)
        {
            if (!ShouldPrepareScene(scene) || !CanPrepareSpawnRuntime())
            {
                return;
            }

            RemoveLegacyScenePlayer(scene);
            DisableClientSideBossAuthority();
        }

        private static void RemoveLegacyScenePlayer(Scene scene)
        {
            PlayerController legacyScenePlayer = FindLegacyScenePlayer(scene, includeInactive: true);
            if (legacyScenePlayer != null)
            {
                CacheLegacySpawnSnapshot(legacyScenePlayer);
                PreserveLegacyMainCamera(legacyScenePlayer);
                UnityEngine.Object.Destroy(legacyScenePlayer.gameObject);
            }
        }

        private static void DisableClientSideBossAuthority()
        {
            if (!MultiplayerRuntimeRoot.HasInstance)
            {
                return;
            }

            NetworkManager networkManager = MultiplayerRuntimeRoot.Instance.NetworkManager;
            if (networkManager == null || networkManager.IsServer)
            {
                return;
            }

            BossController bossController = UnityEngine.Object.FindObjectOfType<BossController>();
            if (bossController != null)
            {
                bossController.enabled = false;

                if (bossController.TryGetComponent(out CharacterController characterController))
                {
                    characterController.enabled = false;
                }
            }
        }

        private static void RebindServerBossTarget(NetworkManager networkManager, List<ulong> connectedClientIds)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            BossController bossController = UnityEngine.Object.FindObjectOfType<BossController>();
            if (bossController == null)
            {
                return;
            }

            Transform bestTarget = null;
            float bestDistanceSqr = float.PositiveInfinity;

            for (int i = 0; i < connectedClientIds.Count; i++)
            {
                NetworkObject playerObject = networkManager.SpawnManager.GetPlayerNetworkObject(connectedClientIds[i]);
                if (playerObject == null)
                {
                    continue;
                }

                PlayerController playerController = playerObject.GetComponent<PlayerController>();
                if (playerController == null)
                {
                    continue;
                }

                Health playerHealth = playerController.GetComponent<Health>();
                if (playerHealth != null && playerHealth.IsDead)
                {
                    continue;
                }

                Vector3 delta = playerController.transform.position - bossController.transform.position;
                delta.y = 0f;
                float planarDistanceSqr = delta.sqrMagnitude;
                if (planarDistanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = planarDistanceSqr;
                bestTarget = playerController.transform;
            }

            if (bestTarget != null)
            {
                bossController.SetTarget(bestTarget);
            }
        }

        private static PlayerController FindLegacyScenePlayer(Scene scene, bool includeInactive)
        {
            FindObjectsInactive inactiveMode = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
            PlayerController[] playerControllers = UnityEngine.Object.FindObjectsByType<PlayerController>(inactiveMode, FindObjectsSortMode.None);
            for (int i = 0; i < playerControllers.Length; i++)
            {
                PlayerController playerController = playerControllers[i];
                if (playerController == null)
                {
                    continue;
                }

                if (playerController.GetComponent<MultiplayerPlayerAvatar>() != null)
                {
                    continue;
                }

                if (playerController.gameObject.scene.handle != scene.handle)
                {
                    continue;
                }

                return playerController;
            }

            return null;
        }

        private static bool CanPrepareSpawnRuntime()
        {
            return MultiplayerRuntimeRoot.HasInstance
                   && MultiplayerRuntimeRoot.Instance.PlayerAvatarPrefab != null;
        }

        private static void CacheLegacySpawnSnapshot(PlayerController legacyScenePlayer)
        {
            if (legacyScenePlayer == null)
            {
                return;
            }

            Vector3 spawnRight = legacyScenePlayer.transform.right;
            spawnRight.y = 0f;
            if (spawnRight.sqrMagnitude <= 0.0001f)
            {
                spawnRight = Vector3.right;
            }

            _legacySpawnPosition = legacyScenePlayer.transform.position;
            _legacySpawnRotation = legacyScenePlayer.transform.rotation;
            _legacySpawnRight = spawnRight.normalized;
            _hasLegacySpawnSnapshot = true;
        }

        private static Vector3 ResolveSpawnPosition(Scene scene)
        {
            if (_hasLegacySpawnSnapshot)
            {
                return _legacySpawnPosition;
            }

            PlayerController legacyScenePlayer = FindLegacyScenePlayer(scene, includeInactive: true);
            if (legacyScenePlayer != null)
            {
                CacheLegacySpawnSnapshot(legacyScenePlayer);
                return _legacySpawnPosition;
            }

            return Vector3.zero;
        }

        private static Quaternion ResolveSpawnRotation(Scene scene)
        {
            if (_hasLegacySpawnSnapshot)
            {
                return _legacySpawnRotation;
            }

            PlayerController legacyScenePlayer = FindLegacyScenePlayer(scene, includeInactive: true);
            if (legacyScenePlayer != null)
            {
                CacheLegacySpawnSnapshot(legacyScenePlayer);
                return _legacySpawnRotation;
            }

            return Quaternion.identity;
        }

        private static Vector3 ResolveSpawnRight(Scene scene)
        {
            if (_hasLegacySpawnSnapshot)
            {
                return _legacySpawnRight;
            }

            PlayerController legacyScenePlayer = FindLegacyScenePlayer(scene, includeInactive: true);
            if (legacyScenePlayer != null)
            {
                CacheLegacySpawnSnapshot(legacyScenePlayer);
                return _legacySpawnRight;
            }

            return Vector3.right;
        }

        private static void ResetLegacySpawnSnapshot()
        {
            _hasLegacySpawnSnapshot = false;
            _legacySpawnPosition = Vector3.zero;
            _legacySpawnRotation = Quaternion.identity;
            _legacySpawnRight = Vector3.right;
        }

        private static void PreserveLegacyMainCamera(PlayerController legacyScenePlayer)
        {
            if (legacyScenePlayer == null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null || !mainCamera.transform.IsChildOf(legacyScenePlayer.transform))
            {
                return;
            }

            mainCamera.transform.SetParent(null, true);
        }
    }
}
