using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core.GameFlow
{
    [DisallowMultipleComponent]
    public class LoadingSceneController : MonoBehaviour
    {
        [Header("Progress UI (Optional)")]
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private string _progressTextFormat = "Loading... {0}%";

        [Header("Flow Settings")]
        [SerializeField, Min(0f)] private float _minimumDisplayDuration = 0.15f;
        [SerializeField] private GameSceneId _fallbackScene = GameSceneId.GamePlay;

        private AsyncOperation _loadOperation;
        private float _displayTimer;
        private int _lastProgressPercent = -1;
        private bool _isActivatingScene;

        private void Start()
        {
            BeginLoading();
        }

        private void Update()
        {
            if (_loadOperation == null) return;

            _displayTimer += Time.deltaTime;

            float normalizedProgress = Mathf.Clamp01(_loadOperation.progress / 0.9f);
            UpdateProgressUI(normalizedProgress);

            if (_isActivatingScene) return;
            if (_loadOperation.progress < 0.9f) return;
            if (_displayTimer < _minimumDisplayDuration) return;

            _isActivatingScene = true;
            SceneLoader.NotifyTransitionCompleted();
            _loadOperation.allowSceneActivation = true;
        }

        private void BeginLoading()
        {
            string targetSceneName;
            if (!SceneLoader.TryConsumeTargetScene(out targetSceneName))
            {
                targetSceneName = SceneLoader.GetSceneName(_fallbackScene);
            }

            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogError("LoadingSceneController: 목표 씬 이름이 비어 있습니다.");
                SceneLoader.CancelPendingTransition();
                return;
            }

            _loadOperation = SceneManager.LoadSceneAsync(targetSceneName);
            if (_loadOperation == null)
            {
                Debug.LogError($"LoadingSceneController: 씬 로드 시작 실패 - {targetSceneName}");
                SceneLoader.CancelPendingTransition();
                return;
            }

            _loadOperation.allowSceneActivation = false;
            UpdateProgressUI(0f);
        }

        private void UpdateProgressUI(float normalizedProgress)
        {
            if (_progressSlider != null)
            {
                _progressSlider.value = normalizedProgress;
            }

            if (_progressText == null) return;

            int progressPercent = Mathf.RoundToInt(normalizedProgress * 100f);
            if (progressPercent == _lastProgressPercent) return;

            _lastProgressPercent = progressPercent;
            _progressText.text = string.Format(_progressTextFormat, progressPercent);
        }
    }
}
