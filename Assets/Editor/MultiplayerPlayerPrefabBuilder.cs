using Core.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Multiplayer.Editor
{
    public static class MultiplayerPlayerPrefabBuilder
    {
        private const string SourcePlayerName = "Player";
        private const string ResourcesFolderPath = "Assets/Resources";
        private const string MultiplayerResourcesFolderPath = ResourcesFolderPath + "/Multiplayer";
        private const string OutputPrefabPath = MultiplayerResourcesFolderPath + "/MultiplayerPlayerAvatar.prefab";

        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(MultiplayerScenePaths.GamePlayScenePath, OpenSceneMode.Single);
            GameObject sourcePlayer = FindSourcePlayer(scene);
            if (sourcePlayer == null)
            {
                throw new System.InvalidOperationException($"Could not find source player '{SourcePlayerName}' in {MultiplayerScenePaths.GamePlayScenePath}.");
            }

            EnsureFolder(ResourcesFolderPath, "Assets", "Resources");
            EnsureFolder(MultiplayerResourcesFolderPath, ResourcesFolderPath, "Multiplayer");

            GameObject workingCopy = Object.Instantiate(sourcePlayer);
            workingCopy.name = "MultiplayerPlayerAvatar";

            try
            {
                PrepareWorkingCopy(workingCopy);
                PrefabUtility.SaveAsPrefabAsset(workingCopy, OutputPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(workingCopy);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"MultiplayerPlayerPrefabBuilder: Built {OutputPrefabPath}");
        }

        private static GameObject FindSourcePlayer(Scene scene)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                GameObject rootObject = rootObjects[i];
                if (rootObject == null)
                {
                    continue;
                }

                if (string.Equals(rootObject.name, SourcePlayerName, System.StringComparison.Ordinal))
                {
                    return rootObject;
                }
            }

            return null;
        }

        private static void PrepareWorkingCopy(GameObject workingCopy)
        {
            PlayerController playerController = workingCopy.GetComponent<PlayerController>();
            LocalInputProvider localInputProvider = workingCopy.GetComponent<LocalInputProvider>();
            if (playerController == null || localInputProvider == null)
            {
                throw new System.InvalidOperationException("Source player is missing PlayerController or LocalInputProvider.");
            }

            RemoveEmbeddedCameraObjects(workingCopy);
            EnsureComponent<NetworkObject>(workingCopy);
            EnsureComponent<NetworkTransform>(workingCopy);
            EnsureComponent<MultiplayerBufferedInputProvider>(workingCopy);
            EnsureComponent<MultiplayerPlayerAvatar>(workingCopy);

            NetworkAnimator networkAnimator = EnsureComponent<NetworkAnimator>(workingCopy);
            Animator animator = playerController.Animator != null
                ? playerController.Animator
                : workingCopy.GetComponentInChildren<Animator>(true);

            SerializedObject networkAnimatorSerializedObject = new SerializedObject(networkAnimator);
            networkAnimatorSerializedObject.FindProperty("m_Animator").objectReferenceValue = animator;
            networkAnimatorSerializedObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject playerSerializedObject = new SerializedObject(playerController);
            playerSerializedObject.FindProperty("_simulationMode").enumValueIndex = (int)PlayerController.RuntimeSimulationMode.Disabled;
            playerSerializedObject.FindProperty("_isLocalPresentationEnabled").boolValue = false;
            playerSerializedObject.FindProperty("_driveCameraRootFromLookInput").boolValue = false;
            playerSerializedObject.FindProperty("_combatHUD").objectReferenceValue = null;
            playerSerializedObject.FindProperty("_bossHealthForHUD").objectReferenceValue = null;
            playerSerializedObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject inputSerializedObject = new SerializedObject(localInputProvider);
            inputSerializedObject.FindProperty("_startInputEnabled").boolValue = false;
            inputSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private static void EnsureFolder(string targetPath, string parentPath, string folderName)
        {
            if (AssetDatabase.IsValidFolder(targetPath))
            {
                return;
            }

            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static void RemoveEmbeddedCameraObjects(GameObject workingCopy)
        {
            Camera[] cameras = workingCopy.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null)
                {
                    continue;
                }

                Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
