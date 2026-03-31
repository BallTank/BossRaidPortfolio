using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Core.CameraSystem
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class ThirdPersonCameraController : MonoBehaviour
    {
        public enum AutoBehindAssistMode
        {
            Off = 0,
            Soft = 1,
            Strong = 2
        }

        [Header("References")]
        [Tooltip("PlayerController to read look input. If empty, it will auto-find.")]
        [SerializeField] private PlayerController playerController;
        [Tooltip("Object to follow. Usually Player root transform.")]
        [SerializeField] private Transform followTarget;
        [Tooltip("Camera movement basis transform. If empty, runtime creates one.")]
        [SerializeField] private Transform cameraRoot;

        [SerializeField] private float positionSmoothTime = 0.01f;
        [SerializeField] private float rotationSmoothTime = 0.01f;

        [Header("Follow")]
        [Tooltip("Height offset from follow target position.")]
        [SerializeField] private float targetHeight = 1.5f;
        [Tooltip("Camera offset from target pivot. X: side, Y: up, Z: back.")]
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 2.65f, -5.8f);

        [Header("Look")]
        [Tooltip("Minimum vertical look angle (down limit).")]
        [SerializeField] private float minPitch = -40f;
        [Tooltip("Maximum vertical look angle (up limit).")]
        [SerializeField] private float maxPitch = 75f;

        [SerializeField, HideInInspector] private AutoBehindAssistMode autoBehindAssist = AutoBehindAssistMode.Off;
        [SerializeField, HideInInspector] private float assistMoveThreshold = 0.15f;
        [SerializeField, HideInInspector] private float softAssistStrength = 3.5f;
        [SerializeField, HideInInspector] private float strongAssistStrength = 7f;
        [SerializeField, HideInInspector] private float sharpTurnYawThreshold = 65f;
        [SerializeField, HideInInspector] private float sharpTurnSoftBoost = 2f;
        [SerializeField, HideInInspector] private float sharpTurnStrongBoost = 4f;

        [Header("Predicted Camera Tuning")]
        [SerializeField, HideInInspector] private bool usePredictedOwnerTightFollow = true;
        [SerializeField, HideInInspector, Range(0f, 0.05f)] private float predictedOwnerPositionSmoothTime = 0f;
        [SerializeField, HideInInspector, Range(0f, 0.05f)] private float predictedOwnerRotationSmoothTime = 0f;

        [Header("Multiplayer Camera Trace")]
        [SerializeField, HideInInspector] private bool enableMultiplayerCameraFollowTrace = true;
        [SerializeField, HideInInspector, Range(0.02f, 0.5f)] private float multiplayerCameraFollowTraceLogInterval = 0.08f;
        [SerializeField, HideInInspector] private float multiplayerCameraFollowTraceDeltaThreshold = 0.01f;
        [SerializeField, HideInInspector] private float multiplayerCameraFollowTraceStillThreshold = 0.001f;

        private Vector3 _positionVelocity;
        private float _yawVelocity;
        private float _pitchVelocity;
        private float _currentYaw;
        private float _currentPitch;
        private Vector3 _lastFollowPosition;
        private bool _hasLastFollowPosition;
        private bool _initialized;
        private Vector3 _lastTraceAnchorPosition;
        private Vector3 _lastTraceDesiredPosition;
        private Vector3 _lastTraceCameraPosition;
        private bool _hasMultiplayerCameraTraceState;
        private int _anchorStillFrameCount;
        private float _nextMultiplayerCameraFollowTraceLogTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGameplayCameraControllerRuntime()
        {
            EnsureComponentExists(markSceneDirtyInEditor: false);
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void EnsureGameplayCameraControllerEditor()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying) return;
                EnsureComponentExists(markSceneDirtyInEditor: true);
            };
        }
#endif

        private static void EnsureComponentExists(bool markSceneDirtyInEditor)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;
            if (mainCamera.GetComponent<ThirdPersonCameraController>() != null) return;

            if (!IsMultiplayerGameplayContext())
            {
                PlayerController player = FindObjectOfType<PlayerController>();
                if (player == null) return;
            }

            mainCamera.gameObject.AddComponent<ThirdPersonCameraController>();

#if UNITY_EDITOR
            if (markSceneDirtyInEditor && !Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(mainCamera.gameObject.scene);
            }
#endif
        }

        private void OnValidate()
        {
            if (positionSmoothTime < 0f) positionSmoothTime = 0f;
            if (rotationSmoothTime < 0f) rotationSmoothTime = 0f;
            if (minPitch < -89f) minPitch = -89f;
            if (maxPitch > 89f) maxPitch = 89f;
            if (maxPitch < minPitch) maxPitch = minPitch;
            if (assistMoveThreshold < 0f) assistMoveThreshold = 0f;
            if (softAssistStrength < 0f) softAssistStrength = 0f;
            if (strongAssistStrength < 0f) strongAssistStrength = 0f;
            sharpTurnYawThreshold = Mathf.Clamp(sharpTurnYawThreshold, 0f, 180f);
            if (sharpTurnSoftBoost < 0f) sharpTurnSoftBoost = 0f;
            if (sharpTurnStrongBoost < 0f) sharpTurnStrongBoost = 0f;
            if (predictedOwnerPositionSmoothTime < 0f) predictedOwnerPositionSmoothTime = 0f;
            if (predictedOwnerRotationSmoothTime < 0f) predictedOwnerRotationSmoothTime = 0f;
            if (multiplayerCameraFollowTraceLogInterval < 0.02f) multiplayerCameraFollowTraceLogInterval = 0.02f;
            if (multiplayerCameraFollowTraceDeltaThreshold < 0f) multiplayerCameraFollowTraceDeltaThreshold = 0f;
            if (multiplayerCameraFollowTraceStillThreshold < 0f) multiplayerCameraFollowTraceStillThreshold = 0f;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            InitializeRig();
        }

        private void LateUpdate()
        {
            RefreshBindingIfNeeded();

            if (!_initialized)
            {
                InitializeRig();
                if (!_initialized) return;
            }

            UpdateCamera();
        }

        /// <summary>
        /// 씬 참조 누락 시 카메라가 플레이어를 자동 탐색하도록 처리한다.
        /// </summary>
        private void ResolveReferences()
        {
            PlayerController resolvedPlayerController = ResolvePreferredPlayerController();
            if (resolvedPlayerController != playerController)
            {
                playerController = resolvedPlayerController;
                followTarget = playerController != null ? playerController.transform : null;
                _initialized = false;
                _hasLastFollowPosition = false;
            }

            if (followTarget == null && playerController != null)
            {
                followTarget = playerController.transform;
            }
        }

        /// <summary>
        /// CameraRoot 소유권을 카메라로 이동하고, 플레이어 이동 기준축으로 연결한다.
        /// </summary>
        private void InitializeRig()
        {
            if (followTarget == null) return;

            if (cameraRoot == null)
            {
                GameObject rootObject = new GameObject("CameraRoot_Runtime");
                cameraRoot = rootObject.transform;
            }

            if (cameraRoot.parent != null)
            {
                cameraRoot.SetParent(null, true);
            }

            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            float initialYaw = playerController != null ? playerController.LatestLookYaw : followTarget.eulerAngles.y;
            float initialPitch = playerController != null ? playerController.LatestLookPitch : 20f;

            _currentYaw = initialYaw;
            _currentPitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
            _positionVelocity = Vector3.zero;

            Vector3 anchor = GetAnchorPosition();
            cameraRoot.position = anchor;
            cameraRoot.rotation = Quaternion.Euler(0f, _currentYaw, 0f);

            Vector3 desiredPosition = anchor + Quaternion.Euler(_currentPitch, _currentYaw, 0f) * followOffset;
            transform.position = desiredPosition;
            Vector3 lookDirection = anchor - desiredPosition;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }

            playerController?.SetCameraRoot(cameraRoot);
            _lastFollowPosition = followTarget.position;
            _hasLastFollowPosition = true;
            ResetMultiplayerCameraTraceState();
            _initialized = true;
        }

        private void UpdateCamera()
        {
            if (followTarget == null || cameraRoot == null) return;

            float inputYaw = playerController != null ? playerController.LatestLookYaw : _currentYaw;
            float inputPitch = playerController != null ? playerController.LatestLookPitch : _currentPitch;
            float targetYaw = inputYaw;
            ApplyAutoBehindAssist(ref targetYaw);
            float targetPitch = Mathf.Clamp(inputPitch, minPitch, maxPitch);

            bool useTightPredictedFollow = ShouldUsePredictedOwnerTightFollow();
            float activeRotationSmoothTime = useTightPredictedFollow
                ? predictedOwnerRotationSmoothTime
                : rotationSmoothTime;
            float activePositionSmoothTime = useTightPredictedFollow
                ? predictedOwnerPositionSmoothTime
                : positionSmoothTime;
            float safeRotationSmoothTime = Mathf.Max(0.0001f, activeRotationSmoothTime);
            if (activeRotationSmoothTime > 0f)
            {
                _currentYaw = Mathf.SmoothDampAngle(_currentYaw, targetYaw, ref _yawVelocity, safeRotationSmoothTime);
                _currentPitch = Mathf.SmoothDampAngle(_currentPitch, targetPitch, ref _pitchVelocity, safeRotationSmoothTime);
            }
            else
            {
                _currentYaw = targetYaw;
                _currentPitch = targetPitch;
                _yawVelocity = 0f;
                _pitchVelocity = 0f;
            }

            Vector3 anchor = GetAnchorPosition();
            cameraRoot.position = anchor;
            cameraRoot.rotation = Quaternion.Euler(0f, _currentYaw, 0f);

            Quaternion orbitRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 desiredPosition = anchor + orbitRotation * followOffset;
            if (activePositionSmoothTime > 0f)
            {
                transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _positionVelocity, activePositionSmoothTime);
            }
            else
            {
                transform.position = desiredPosition;
                _positionVelocity = Vector3.zero;
            }

            Vector3 lookDirection = anchor - transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                if (activeRotationSmoothTime > 0f)
                {
                    float rotationLerp = 1f - Mathf.Exp(-Time.deltaTime / safeRotationSmoothTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationLerp);
                }
                else
                {
                    transform.rotation = desiredRotation;
                }
            }

            UpdateMultiplayerCameraFollowTrace(anchor, desiredPosition, activePositionSmoothTime, activeRotationSmoothTime);
        }

        /// <summary>
        /// 마우스 입력을 1차로 유지하면서, 필요 시 캐릭터 뒤 방향 정렬 보조를 추가한다.
        /// </summary>
        private void ApplyAutoBehindAssist(ref float targetYaw)
        {
            if (autoBehindAssist == AutoBehindAssistMode.Off || followTarget == null)
            {
                UpdateFollowPositionSnapshot();
                return;
            }

            float moveSpeed = ComputePlanarFollowSpeed();
            UpdateFollowPositionSnapshot();
            if (moveSpeed < assistMoveThreshold) return;

            float assistStrength = autoBehindAssist == AutoBehindAssistMode.Strong
                ? strongAssistStrength
                : softAssistStrength;

            float yawGap = Mathf.Abs(Mathf.DeltaAngle(_currentYaw, followTarget.eulerAngles.y));
            if (yawGap >= sharpTurnYawThreshold)
            {
                assistStrength += autoBehindAssist == AutoBehindAssistMode.Strong
                    ? sharpTurnStrongBoost
                    : sharpTurnSoftBoost;
            }

            if (assistStrength <= 0f) return;

            float assistLerp = 1f - Mathf.Exp(-assistStrength * Time.deltaTime);
            targetYaw = Mathf.LerpAngle(targetYaw, followTarget.eulerAngles.y, assistLerp);
        }

        private float ComputePlanarFollowSpeed()
        {
            if (!_hasLastFollowPosition) return 0f;

            Vector3 delta = GetResolvedFollowPosition() - _lastFollowPosition;
            delta.y = 0f;
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            return delta.magnitude / deltaTime;
        }

        private void UpdateFollowPositionSnapshot()
        {
            _lastFollowPosition = GetResolvedFollowPosition();
            _hasLastFollowPosition = true;
        }

        private Vector3 GetAnchorPosition()
        {
            Vector3 anchor = GetResolvedFollowPosition();
            anchor.y += targetHeight;
            return anchor;
        }

        private Vector3 GetResolvedFollowPosition()
        {
            if (playerController != null)
            {
                return playerController.GetPreferredCameraFollowPosition();
            }

            return followTarget.position;
        }

        private void RefreshBindingIfNeeded()
        {
            ResolveReferences();

            if (playerController == null)
            {
                _initialized = false;
                ResetMultiplayerCameraTraceState();
            }
        }

        private void UpdateMultiplayerCameraFollowTrace(Vector3 anchor, Vector3 desiredPosition, float activePositionSmoothTime, float activeRotationSmoothTime)
        {
            Vector3 cameraPosition = transform.position;
            if (!ShouldTraceMultiplayerCameraFollow())
            {
                CacheMultiplayerCameraTraceState(anchor, desiredPosition, cameraPosition);
                return;
            }

            if (!_hasMultiplayerCameraTraceState)
            {
                CacheMultiplayerCameraTraceState(anchor, desiredPosition, cameraPosition);
                return;
            }

            float anchorPlanarDelta = PlanarDistance(anchor, _lastTraceAnchorPosition);
            float desiredPlanarDelta = PlanarDistance(desiredPosition, _lastTraceDesiredPosition);
            float cameraPlanarDelta = PlanarDistance(cameraPosition, _lastTraceCameraPosition);
            float cameraToAnchorPlanar = PlanarDistance(cameraPosition, anchor);
            float cameraToDesired = Vector3.Distance(cameraPosition, desiredPosition);

            bool anchorIsStill = anchorPlanarDelta <= multiplayerCameraFollowTraceStillThreshold;
            int stillFramesBeforeSpike = 0;
            if (anchorIsStill)
            {
                _anchorStillFrameCount++;
            }
            else
            {
                stillFramesBeforeSpike = _anchorStillFrameCount;
                _anchorStillFrameCount = 0;
            }

            bool shouldTrace = !anchorIsStill
                               || cameraPlanarDelta >= multiplayerCameraFollowTraceDeltaThreshold
                               || cameraToDesired >= multiplayerCameraFollowTraceDeltaThreshold;
            if (!shouldTrace || Time.time < _nextMultiplayerCameraFollowTraceLogTime)
            {
                CacheMultiplayerCameraTraceState(anchor, desiredPosition, cameraPosition);
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            _nextMultiplayerCameraFollowTraceLogTime = Time.time + multiplayerCameraFollowTraceLogInterval;
            Debug.Log(
                $"[MultiplayerCameraFollowTrace] " +
                $"anchor=({anchor.x:F3},{anchor.y:F3},{anchor.z:F3}) " +
                $"desired=({desiredPosition.x:F3},{desiredPosition.y:F3},{desiredPosition.z:F3}) " +
                $"camera=({cameraPosition.x:F3},{cameraPosition.y:F3},{cameraPosition.z:F3}) " +
                $"anchorPlanarDelta={anchorPlanarDelta:F3} " +
                $"desiredPlanarDelta={desiredPlanarDelta:F3} " +
                $"cameraPlanarDelta={cameraPlanarDelta:F3} " +
                $"anchorStillFrames={stillFramesBeforeSpike} " +
                $"anchorPlanarSpeed={(anchorPlanarDelta / deltaTime):F3} " +
                $"cameraPlanarSpeed={(cameraPlanarDelta / deltaTime):F3} " +
                $"cameraToAnchorPlanar={cameraToAnchorPlanar:F3} " +
                $"cameraToDesired={cameraToDesired:F3} " +
                $"yaw={_currentYaw:F1} " +
                $"pitch={_currentPitch:F1} " +
                $"posSmooth={activePositionSmoothTime:F3} " +
                $"rotSmooth={activeRotationSmoothTime:F3}");

            CacheMultiplayerCameraTraceState(anchor, desiredPosition, cameraPosition);
        }

        private bool ShouldUsePredictedOwnerTightFollow()
        {
            return usePredictedOwnerTightFollow
                   && playerController != null
                   && playerController.SimulationMode == PlayerController.RuntimeSimulationMode.PredictedLocomotion;
        }

        private bool ShouldTraceMultiplayerCameraFollow()
        {
            return enableMultiplayerCameraFollowTrace
                   && playerController != null
                   && playerController.SimulationMode == PlayerController.RuntimeSimulationMode.PredictedLocomotion;
        }

        private void CacheMultiplayerCameraTraceState(Vector3 anchor, Vector3 desiredPosition, Vector3 cameraPosition)
        {
            _lastTraceAnchorPosition = anchor;
            _lastTraceDesiredPosition = desiredPosition;
            _lastTraceCameraPosition = cameraPosition;
            _hasMultiplayerCameraTraceState = true;
        }

        private void ResetMultiplayerCameraTraceState()
        {
            _lastTraceAnchorPosition = Vector3.zero;
            _lastTraceDesiredPosition = Vector3.zero;
            _lastTraceCameraPosition = Vector3.zero;
            _hasMultiplayerCameraTraceState = false;
            _anchorStillFrameCount = 0;
            _nextMultiplayerCameraFollowTraceLogTime = 0f;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private PlayerController ResolvePreferredPlayerController()
        {
            if (IsMultiplayerGameplayContext())
            {
                return Core.Multiplayer.MultiplayerLocalPlayerRegistry.LocalPlayer;
            }

            if (playerController != null)
            {
                return playerController;
            }

            return FindObjectOfType<PlayerController>();
        }

        private static bool IsMultiplayerGameplayContext()
        {
            if (!Core.Multiplayer.MultiplayerSessionService.HasInstance || !Core.Multiplayer.MultiplayerSessionService.Instance.HasActiveSession)
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            return string.Equals(activeScene.path, Core.Multiplayer.MultiplayerScenePaths.GamePlayScenePath, System.StringComparison.OrdinalIgnoreCase);
        }

    }
}
