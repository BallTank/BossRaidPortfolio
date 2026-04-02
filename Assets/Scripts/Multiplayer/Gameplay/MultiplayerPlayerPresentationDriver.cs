using Core.Player;
using Unity.Netcode;
using UnityEngine;

namespace Core.Multiplayer
{
    /// <summary>
    /// 멀티플레이 전용 시각 보정과 디버그 추적을 PlayerController 밖으로 분리한다.
    /// </summary>
    public sealed class MultiplayerPlayerPresentationDriver
    {
        private const float PredictedPresentationTransitionSnapAngle = 35f;
        private const float PredictedPresentationTickBoundaryHeadStartFraction = 0.05f;

        private readonly PlayerController _controller;
        private Vector3 _presentationDefaultLocalPosition;
        private Quaternion _presentationDefaultLocalRotation = Quaternion.identity;
        private bool _hasPresentationDefaultTransform;
        private Transform _cachedPresentationTransform;
        private Vector3 _presentationWorldPosition;
        private Vector3 _presentationWorldVelocity;
        private bool _hasPresentationWorldPosition;
        private Vector3 _predictedPresentationPreviousTargetPosition;
        private Vector3 _predictedPresentationCurrentTargetPosition;
        private float _predictedPresentationTargetSetTime;
        private bool _hasPredictedPresentationTargets;
        private Vector2 _lastPredictedPresentationMoveInput;
        private bool _hasLastPredictedPresentationMoveInput;
        public MultiplayerPlayerPresentationDriver(PlayerController controller)
        {
            _controller = controller;
            EnsurePresentationDefaultTransformCached();
            ResetPresentationRotationToRoot();
        }

        public void RefreshBindings()
        {
            EnsurePresentationDefaultTransformCached(forceRefresh: true);
            ResetPresentationRotationToRoot();
        }

        public void HandleSimulationModeChanged(PlayerController.RuntimeSimulationMode simulationMode)
        {
            if (simulationMode != PlayerController.RuntimeSimulationMode.PredictedLocomotion)
            {
                ResetPresentationRotationToRoot();
            }
        }

        public void HandleLocalPresentationEnabledChanged(bool enabled)
        {
            if (!enabled)
            {
                ResetPresentationRotationToRoot();
            }
        }

        public void UpdatePredictedLocomotionPresentation(PlayerInputPacket input)
        {
            if (!_controller.IsLocalPresentationEnabled)
            {
                ResetPresentationRotationToRoot();
                return;
            }

            Transform presentationTransform = ResolvePresentationTransform();
            if (presentationTransform == null)
            {
                return;
            }

            bool shouldSnapPredictedTransition = _controller.IsDashStateActive
                                                || ShouldSnapPredictedPresentationTransition(input.moveDir);
            UpdatePredictedPresentationPosition(presentationTransform, shouldSnapPredictedTransition);

            if (_hasPresentationDefaultTransform)
            {
                presentationTransform.localRotation = _presentationDefaultLocalRotation;
            }
            else
            {
                presentationTransform.rotation = _controller.transform.rotation;
            }
        }

        public Vector3 GetPreferredCameraFollowPosition()
        {
            if (ShouldUsePredictedRenderSmoothingPresentation() && _hasPresentationWorldPosition)
            {
                return ResolvePresentationRootProxyPosition();
            }

            return _controller.transform.position;
        }

        public void ResetPresentationRotationToRoot()
        {
            EnsurePresentationDefaultTransformCached();

            Transform presentationTransform = ResolvePresentationTransform();
            if (presentationTransform == null)
            {
                ResetPresentationState();
                return;
            }

            if (_hasPresentationDefaultTransform)
            {
                presentationTransform.localPosition = _presentationDefaultLocalPosition;
                presentationTransform.localRotation = _presentationDefaultLocalRotation;
            }
            else
            {
                presentationTransform.position = _controller.transform.position;
                presentationTransform.rotation = _controller.transform.rotation;
            }

            ResetPresentationState();
        }

        private void EnsurePresentationDefaultTransformCached(bool forceRefresh = false)
        {
            Transform presentationTransform = ResolvePresentationTransform();
            if (presentationTransform == null)
            {
                _cachedPresentationTransform = null;
                _hasPresentationDefaultTransform = false;
                return;
            }

            if (!forceRefresh
                && _hasPresentationDefaultTransform
                && presentationTransform == _cachedPresentationTransform)
            {
                return;
            }

            _cachedPresentationTransform = presentationTransform;
            _presentationDefaultLocalPosition = presentationTransform.localPosition;
            _presentationDefaultLocalRotation = presentationTransform.localRotation;
            _hasPresentationDefaultTransform = true;
        }

        private bool ShouldUsePredictedRenderSmoothingPresentation()
        {
            return _controller.SimulationMode == PlayerController.RuntimeSimulationMode.PredictedLocomotion
                   && _controller.IsLocalPresentationEnabled;
        }

        private Transform ResolvePresentationTransform()
        {
            PlayerVisual visual = _controller.Visual;
            if (visual == null || visual.transform == _controller.transform)
            {
                return null;
            }

            return visual.transform;
        }

        private Vector3 ResolvePresentationTargetPosition()
        {
            if (_hasPresentationDefaultTransform)
            {
                return _controller.transform.TransformPoint(_presentationDefaultLocalPosition);
            }

            return _controller.transform.position;
        }

        private Vector3 ResolvePresentationRootProxyPosition()
        {
            if (!_hasPresentationDefaultTransform)
            {
                return _presentationWorldPosition;
            }

            return _presentationWorldPosition - (_controller.transform.rotation * _presentationDefaultLocalPosition);
        }

        private void UpdatePredictedPresentationPosition(Transform presentationTransform, bool shouldSnapPredictedTransition)
        {
            if (!ShouldUsePredictedRenderSmoothingPresentation())
            {
                return;
            }

            Vector3 targetPosition = ResolvePresentationTargetPosition();
            float snapDistance = _controller.MultiplayerPredictedRenderSnapDistance;
            if (shouldSnapPredictedTransition
                || !_hasPresentationWorldPosition
                || (targetPosition - _presentationWorldPosition).sqrMagnitude >= snapDistance * snapDistance)
            {
                _presentationWorldPosition = targetPosition;
                _presentationWorldVelocity = Vector3.zero;
                _hasPresentationWorldPosition = true;
                _predictedPresentationPreviousTargetPosition = targetPosition;
                _predictedPresentationCurrentTargetPosition = targetPosition;
                _predictedPresentationTargetSetTime = Time.time;
                _hasPredictedPresentationTargets = true;
            }
            else if (_controller.MultiplayerPredictedRenderSmoothTime > 0f)
            {
                if (!_hasPredictedPresentationTargets)
                {
                    _predictedPresentationPreviousTargetPosition = targetPosition;
                    _predictedPresentationCurrentTargetPosition = targetPosition;
                    _predictedPresentationTargetSetTime = Time.time;
                    _hasPredictedPresentationTargets = true;
                }
                else if ((targetPosition - _predictedPresentationCurrentTargetPosition).sqrMagnitude > 0.000001f)
                {
                    _predictedPresentationPreviousTargetPosition = _predictedPresentationCurrentTargetPosition;
                    _predictedPresentationCurrentTargetPosition = targetPosition;
                    _predictedPresentationTargetSetTime = Time.time;
                }

                float tickInterval = ResolvePredictedPresentationTickInterval();
                float interpolationWindow = tickInterval > 0f
                    ? Mathf.Min(_controller.MultiplayerPredictedRenderSmoothTime, tickInterval)
                    : _controller.MultiplayerPredictedRenderSmoothTime;
                if (interpolationWindow <= 0f)
                {
                    interpolationWindow = tickInterval > 0f ? tickInterval : 1f / 60f;
                }

                float linearInterpolationAlpha = EvaluatePredictedPresentationLinearAlpha(
                    Time.time - _predictedPresentationTargetSetTime,
                    interpolationWindow);
                float interpolationAlpha = EvaluatePredictedPresentationInterpolationAlpha(linearInterpolationAlpha);
                Vector3 renderPosition = Vector3.Lerp(
                    _predictedPresentationPreviousTargetPosition,
                    _predictedPresentationCurrentTargetPosition,
                    interpolationAlpha);

                if (Time.deltaTime > 0f)
                {
                    _presentationWorldVelocity = (renderPosition - _presentationWorldPosition) / Time.deltaTime;
                }
                else
                {
                    _presentationWorldVelocity = Vector3.zero;
                }

                _presentationWorldPosition = renderPosition;
            }
            else
            {
                _presentationWorldPosition = targetPosition;
                _presentationWorldVelocity = Vector3.zero;
                _predictedPresentationPreviousTargetPosition = targetPosition;
                _predictedPresentationCurrentTargetPosition = targetPosition;
                _predictedPresentationTargetSetTime = Time.time;
                _hasPredictedPresentationTargets = true;
            }

            presentationTransform.position = _presentationWorldPosition;
        }

        private bool ShouldSnapPredictedPresentationTransition(Vector2 currentMoveInput)
        {
            if (currentMoveInput.sqrMagnitude <= 0.0001f)
            {
                _hasLastPredictedPresentationMoveInput = false;
                _lastPredictedPresentationMoveInput = Vector2.zero;
                return false;
            }

            Vector2 normalizedCurrentInput = currentMoveInput.normalized;
            bool shouldSnap = false;
            if (_hasLastPredictedPresentationMoveInput)
            {
                float angleDelta = Vector2.Angle(_lastPredictedPresentationMoveInput, normalizedCurrentInput);
                shouldSnap = angleDelta >= PredictedPresentationTransitionSnapAngle;
            }

            _lastPredictedPresentationMoveInput = normalizedCurrentInput;
            _hasLastPredictedPresentationMoveInput = true;
            return shouldSnap;
        }

        private void ResetPresentationState()
        {
            _presentationWorldPosition = ResolvePresentationTargetPosition();
            _presentationWorldVelocity = Vector3.zero;
            _hasPresentationWorldPosition = true;
            _predictedPresentationPreviousTargetPosition = _presentationWorldPosition;
            _predictedPresentationCurrentTargetPosition = _presentationWorldPosition;
            _predictedPresentationTargetSetTime = Time.time;
            _hasPredictedPresentationTargets = true;
            _lastPredictedPresentationMoveInput = Vector2.zero;
            _hasLastPredictedPresentationMoveInput = false;
        }

        private static float ResolvePredictedPresentationTickInterval()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || networkManager.NetworkConfig == null)
            {
                return 1f / 60f;
            }

            int tickRate = (int)networkManager.NetworkConfig.TickRate;
            return tickRate > 0 ? 1f / tickRate : 1f / 60f;
        }

        private static float EvaluatePredictedPresentationLinearAlpha(float elapsedSinceTarget, float interpolationWindow)
        {
            if (interpolationWindow <= 0f)
            {
                return 1f;
            }

            elapsedSinceTarget = Mathf.Max(0f, elapsedSinceTarget);
            float minimumElapsed = interpolationWindow * PredictedPresentationTickBoundaryHeadStartFraction;
            return Mathf.Clamp01(Mathf.Max(elapsedSinceTarget, minimumElapsed) / interpolationWindow);
        }

        private static float EvaluatePredictedPresentationInterpolationAlpha(float linearInterpolationAlpha)
        {
            linearInterpolationAlpha = Mathf.Clamp01(linearInterpolationAlpha);
            float inverse = 1f - linearInterpolationAlpha;
            return 1f - (inverse * inverse * inverse);
        }
    }
}
