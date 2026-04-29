using Core.Multiplayer;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[CustomEditor(typeof(MultiplayerRuntimeConfig))]
public sealed class MultiplayerRuntimeConfigEditor : Editor
{
    private SerializedProperty _playerAvatarPrefabProperty;
    private SerializedProperty _hostPlayerAvatarPrefabProperty;
    private SerializedProperty _clientPlayerAvatarPrefabProperty;

    private void OnEnable()
    {
        _playerAvatarPrefabProperty = serializedObject.FindProperty("_playerAvatarPrefab");
        _hostPlayerAvatarPrefabProperty = serializedObject.FindProperty("_hostPlayerAvatarPrefab");
        _clientPlayerAvatarPrefabProperty = serializedObject.FindProperty("_clientPlayerAvatarPrefab");
    }

    public override void OnInspectorGUI()
    {
        MultiplayerRuntimeConfig runtimeConfig = (MultiplayerRuntimeConfig)target;

        serializedObject.Update();

        MessageType messageType = runtimeConfig.HasResolvedPlayerAvatarPrefabs ? MessageType.Info : MessageType.Error;
        EditorGUILayout.HelpBox(runtimeConfig.BuildValidationMessage(), messageType);

        EditorGUILayout.PropertyField(_playerAvatarPrefabProperty);
        EditorGUILayout.PropertyField(_hostPlayerAvatarPrefabProperty);
        EditorGUILayout.PropertyField(_clientPlayerAvatarPrefabProperty);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Resolved Player Prefab", runtimeConfig.PlayerAvatarPrefab, typeof(GameObject), false);
            EditorGUILayout.ObjectField("Resolved Host Prefab", runtimeConfig.HostPlayerAvatarPrefab, typeof(GameObject), false);
            EditorGUILayout.ObjectField("Resolved Client Prefab", runtimeConfig.ClientPlayerAvatarPrefab, typeof(GameObject), false);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

[InitializeOnLoad]
public sealed class MultiplayerRuntimeConfigValidator : IPreprocessBuildWithReport
{
    static MultiplayerRuntimeConfigValidator()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    public int callbackOrder => 0;

    [MenuItem("Tools/Multiplayer/Select Runtime Config")]
    private static void SelectRuntimeConfig()
    {
        MultiplayerRuntimeConfig runtimeConfig = LoadRuntimeConfigAsset();
        if (runtimeConfig == null)
        {
            Debug.LogError($"Multiplayer runtime config asset is missing. Create or restore {MultiplayerRuntimeConfig.AssetPath}.");
            return;
        }

        Selection.activeObject = runtimeConfig;
        EditorGUIUtility.PingObject(runtimeConfig);
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (TryGetValidationError(out string validationError))
        {
            throw new BuildFailedException(validationError);
        }
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
        {
            return;
        }

        if (!TryGetValidationError(out string validationError))
        {
            return;
        }

        Debug.LogError(validationError);
        EditorApplication.isPlaying = false;
    }

    private static bool TryGetValidationError(out string validationError)
    {
        MultiplayerRuntimeConfig runtimeConfig = LoadRuntimeConfigAsset();
        if (runtimeConfig == null)
        {
            validationError = $"Multiplayer runtime config asset is missing. Create or restore {MultiplayerRuntimeConfig.AssetPath}.";
            return true;
        }

        if (runtimeConfig.HasResolvedPlayerAvatarPrefabs)
        {
            validationError = null;
            return false;
        }

        validationError = runtimeConfig.BuildValidationMessage();
        return true;
    }

    private static MultiplayerRuntimeConfig LoadRuntimeConfigAsset()
    {
        return AssetDatabase.LoadAssetAtPath<MultiplayerRuntimeConfig>(MultiplayerRuntimeConfig.AssetPath);
    }
}
