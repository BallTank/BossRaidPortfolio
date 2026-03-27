using Core.Multiplayer;
using UnityEditor;

[CustomEditor(typeof(MultiplayerPlayerAvatar))]
public sealed class MultiplayerPlayerAvatarInspectorGuide : Editor
{
    private const string ComponentGuide =
        "Multiplayer quick guide\n\n" +
        "NetworkObject: network identity + spawn/owner info.\n" +
        "NetworkTransform: transform sync for remote viewers.\n" +
        "MultiplayerBufferedInputProvider: Host-side cached owner input source.\n" +
        "MultiplayerPlayerAvatar: main role/authority/prediction bridge.\n" +
        "NetworkAnimator: animator sync for remote viewers.\n\n" +
        "Current tuning pass:\n" +
        "Adjust only PlayerController > Multiplayer Predicted Render Tuning > Predicted Render Smooth Time.";

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(ComponentGuide, MessageType.Info);
        DrawDefaultInspector();
    }
}
