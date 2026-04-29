using System;

namespace Core.Multiplayer
{
    public static class MultiplayerScenePaths
    {
        public const string SceneFolderPath = "Assets/Scenes/mutiplayer";
        public const string VerifyGamePlaySceneName = "GamePlayScene_Verify";
        public const string FullGamePlaySceneName = "GamePlayScene";
        public const string TitleScenePath = SceneFolderPath + "/TitleScene.unity";
        public const string LoadingScenePath = SceneFolderPath + "/LoadingScene.unity";
        public const string VerifyGamePlayScenePath = SceneFolderPath + "/GamePlayScene_Verify.unity";
        public const string FullGamePlayScenePath = SceneFolderPath + "/GamePlayScene.unity";
        public const string GamePlaySceneName = FullGamePlaySceneName;
        public const string GamePlayScenePath = FullGamePlayScenePath;

        public static bool IsMultiplayerScene(string scenePath)
        {
            return !string.IsNullOrEmpty(scenePath)
                   && scenePath.StartsWith(SceneFolderPath, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMultiplayerTitleScene(string scenePath)
        {
            return string.Equals(scenePath, TitleScenePath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
