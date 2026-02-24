using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.GameFlow
{
    public enum GameSceneId
    {
        Title,
        Loading,
        GamePlay
    }

    public static class SceneLoader
    {
        public const string TitleSceneName = "TitleScene";
        public const string LoadingSceneName = "LoadingScene";
        public const string GamePlaySceneName = "GamePlayScene";

        private static GameSceneId _targetSceneId = GameSceneId.GamePlay;
        private static bool _hasPendingTarget;
        private static bool _isTransitionInProgress;

        public static bool HasPendingTarget => _hasPendingTarget;
        public static bool IsTransitionInProgress => _isTransitionInProgress;
        public static string TargetSceneName => _hasPendingTarget ? GetSceneName(_targetSceneId) : string.Empty;

        public static void Load(GameSceneId targetSceneId)
        {
            if (_isTransitionInProgress)
            {
                return;
            }

            if (targetSceneId == GameSceneId.Loading)
            {
                Debug.LogWarning("SceneLoader.Load: Loading 씬을 목적지로 지정할 수 없습니다.");
                return;
            }

            string loadingSceneName = GetSceneName(GameSceneId.Loading);
            string targetSceneName = GetSceneName(targetSceneId);
            if (string.IsNullOrEmpty(loadingSceneName) || string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogError("SceneLoader.Load: 씬 이름 해석에 실패했습니다.");
                return;
            }

            _targetSceneId = targetSceneId;
            _hasPendingTarget = true;
            _isTransitionInProgress = true;

            SceneManager.LoadScene(loadingSceneName);
        }

        public static bool TryConsumeTargetScene(out string sceneName)
        {
            if (!_hasPendingTarget)
            {
                sceneName = string.Empty;
                _isTransitionInProgress = false;
                return false;
            }

            sceneName = GetSceneName(_targetSceneId);
            _hasPendingTarget = false;
            return !string.IsNullOrEmpty(sceneName);
        }

        public static void NotifyTransitionCompleted()
        {
            _isTransitionInProgress = false;
        }

        public static void CancelPendingTransition()
        {
            _hasPendingTarget = false;
            _isTransitionInProgress = false;
        }

        public static string GetSceneName(GameSceneId sceneId)
        {
            switch (sceneId)
            {
                case GameSceneId.Title:
                    return TitleSceneName;
                case GameSceneId.Loading:
                    return LoadingSceneName;
                case GameSceneId.GamePlay:
                    return GamePlaySceneName;
                default:
                    return string.Empty;
            }
        }
    }
}
