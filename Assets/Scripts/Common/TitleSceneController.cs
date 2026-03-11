using UnityEngine;

namespace Core.GameFlow
{
    [DisallowMultipleComponent]
    public class TitleSceneController : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private GameSceneId _nextSceneId = GameSceneId.GamePlay;

        [Header("Input Guard")]
        [SerializeField, Min(0f)] private float _inputLockDuration = 0.1f;

        private float _elapsedTime;
        private bool _requestedTransition;

        private void Awake()
        {
            SceneLoader.CancelPendingTransition();
        }

        private void Update()
        {
            if (_requestedTransition)
            {
                return;
            }

            _elapsedTime += Time.deltaTime;
            if (_elapsedTime < _inputLockDuration)
            {
                return;
            }

            if (!Input.anyKeyDown)
            {
                return;
            }

            _requestedTransition = true;
            SceneLoader.Load(_nextSceneId);
        }
    }
}
