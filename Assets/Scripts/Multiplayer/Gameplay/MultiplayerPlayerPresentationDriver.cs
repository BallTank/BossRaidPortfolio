using Core.Player;
using Unity.Netcode;
using UnityEngine;

namespace Core.Multiplayer
{
    /// <summary>
    /// 멀티플레이 로컬 owner 화면의 visual child와 camera follow surface만 다룬다.
    /// gameplay truth, FSM authority, HP/result apply는 이 클래스 책임이 아니다.
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
        private bool _hasPresentationWorldPosition;
        private Vector3 _predictedPresentationPreviousTargetPosition;
        private Vector3 _predictedPresentationCurrentTargetPosition;
        private float _predictedPresentationTargetSetTime;
        private bool _hasPredictedPresentationTargets;
        private Vector2 _lastPredictedPresentationMoveInput;
        private bool _hasLastPredictedPresentationMoveInput;
        private bool _wasDashPresentationActive;
        private float _nextMovementPresentationDebugLogTime;
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
            if (!ShouldUseVisualOnlyPredictedPresentation())
            {
                ResetPresentationRotationToRoot();
                return;
            }

            Transform presentationTransform = ResolvePresentationTransform();
            if (presentationTransform == null)
            {
                return;
            }

            bool isDashActive = _controller.IsDashStateActive;
            bool dashStartedThisFrame = isDashActive && !_wasDashPresentationActive;
            bool shouldSnapPredictedTransition = dashStartedThisFrame
                                                || ShouldSnapPredictedPresentationTransition(input.moveDir);
            UpdatePredictedPresentationPosition(presentationTransform, shouldSnapPredictedTransition);

            if (TryBeginMovementPresentationDebugLog())
            {
                LogPredictedPresentationDebug(input, isDashActive, dashStartedThisFrame, shouldSnapPredictedTransition, presentationTransform);
            }

            _wasDashPresentationActive = isDashActive;

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
            if (ShouldUseVisualOnlyPredictedPresentation() && _hasPresentationWorldPosition)
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

        private bool ShouldUseVisualOnlyPredictedPresentation()
        {
            return _controller.SimulationMode == PlayerController.RuntimeSimulationMode.PredictedLocomotion
                   && _controller.CurrentActionAuthorityMode == PlayerController.ActionAuthorityMode.ClientOwnerProxy
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
            if (!ShouldUseVisualOnlyPredictedPresentation())
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

                _presentationWorldPosition = renderPosition;
            }
            else
            {
                _presentationWorldPosition = targetPosition;
                _predictedPresentationPreviousTargetPosition = targetPosition;
                _predictedPresentationCurrentTargetPosition = targetPosition;
                _predictedPresentationTargetSetTime = Time.time;
                _hasPredictedPresentationTargets = true;
            }

            presentationTransform.position = _presentationWorldPosition;
        }

        private bool TryBeginMovementPresentationDebugLog()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_controller == null || !_controller.EnableMovementDebugLog)
            {
                return false;
            }

            if (Time.time < _nextMovementPresentationDebugLogTime)
            {
                return false;
            }

            _nextMovementPresentationDebugLogTime = Time.time + _controller.MovementDebugLogInterval;
            return true;
#else
            return false;
#endif
        }

        private void LogPredictedPresentationDebug(
            in PlayerInputPacket input,
            bool isDashActive,
            bool dashStartedThisFrame,
            bool shouldSnapPredictedTransition,
            Transform presentationTransform)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_controller == null)
            {
                return;
            }

            Vector3 rootPosition = _controller.transform.position;
            float rootYaw = _controller.transform.eulerAngles.y;
            Vector3 proxyPosition = presentationTransform.position;
            Vector3 targetPosition = ResolvePresentationTargetPosition();
            float animSpeed = _controller.Animator != null
                ? _controller.Animator.GetFloat(PlayerController.ANIM_PARAM_SPEED)
                : 0f;
            float proxyRootDelta = (proxyPosition - rootPosition).magnitude;
            float proxyTargetDelta = (proxyPosition - targetPosition).magnitude;
            float targetAge = Mathf.Max(0f, Time.time - _predictedPresentationTargetSetTime);
            float tickInterval = ResolvePredictedPresentationTickInterval();
            float interpolationWindow = tickInterval > 0f
                ? Mathf.Min(_controller.MultiplayerPredictedRenderSmoothTime, tickInterval)
                : _controller.MultiplayerPredictedRenderSmoothTime;
            if (interpolationWindow <= 0f)
            {
                interpolationWindow = tickInterval > 0f ? tickInterval : 1f / 60f;
            }

            float linearInterpolationAlpha = EvaluatePredictedPresentationLinearAlpha(targetAge, interpolationWindow);
            float interpolationAlpha = EvaluatePredictedPresentationInterpolationAlpha(linearInterpolationAlpha);

            Debug.Log(
                $"[MoveDebug][Proxy] " +
                $"input=({input.moveDir.x:F3},{input.moveDir.y:F3}) " +
                $"dash={isDashActive} " +
                $"dashStart={dashStartedThisFrame} " +
                $"snap={shouldSnapPredictedTransition} " +
                $"rootPos=({rootPosition.x:F3},{rootPosition.y:F3},{rootPosition.z:F3}) " +
                $"rootYaw={rootYaw:F3} " +
                $"proxyPos=({proxyPosition.x:F3},{proxyPosition.y:F3},{proxyPosition.z:F3}) " +
                $"targetPos=({targetPosition.x:F3},{targetPosition.y:F3},{targetPosition.z:F3}) " +
                $"prevTarget=({_predictedPresentationPreviousTargetPosition.x:F3},{_predictedPresentationPreviousTargetPosition.y:F3},{_predictedPresentationPreviousTargetPosition.z:F3}) " +
                $"currentTarget=({_predictedPresentationCurrentTargetPosition.x:F3},{_predictedPresentationCurrentTargetPosition.y:F3},{_predictedPresentationCurrentTargetPosition.z:F3}) " +
                $"proxyRootDelta={proxyRootDelta:F3} " +
                $"proxyTargetDelta={proxyTargetDelta:F3} " +
                $"targetAge={targetAge:F3} " +
                $"tickInterval={tickInterval:F3} " +
                $"interpWindow={interpolationWindow:F3} " +
                $"interpAlpha={interpolationAlpha:F3} " +
                $"smoothTime={_controller.MultiplayerPredictedRenderSmoothTime:F3} " +
                $"snapDistance={_controller.MultiplayerPredictedRenderSnapDistance:F3} " +
                $"animSpeed={animSpeed:F3} " +
                $"state={_controller.StateMachine?.CurrentState?.GetType().Name ?? "None"}");
#endif
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
            _hasPresentationWorldPosition = true;
            _predictedPresentationPreviousTargetPosition = _presentationWorldPosition;
            _predictedPresentationCurrentTargetPosition = _presentationWorldPosition;
            _predictedPresentationTargetSetTime = Time.time;
            _hasPredictedPresentationTargets = true;
            _lastPredictedPresentationMoveInput = Vector2.zero;
            _hasLastPredictedPresentationMoveInput = false;
            _wasDashPresentationActive = false;
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
