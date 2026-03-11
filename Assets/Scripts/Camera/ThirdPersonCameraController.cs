using UnityEngine;
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

        [SerializeField, HideInInspector] private float positionSmoothTime = 0.01f;
        [SerializeField, HideInInspector] private float rotationSmoothTime = 0.01f;

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

        private Vector3 _positionVelocity;
        private float _yawVelocity;
        private float _pitchVelocity;
        private float _currentYaw;
        private float _currentPitch;
        private Vector3 _lastFollowPosition;
        private bool _hasLastFollowPosition;
        private bool _initialized;

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
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player == null) return;

            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;
            if (mainCamera.GetComponent<ThirdPersonCameraController>() != null) return;

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
            if (!_initialized)
            {
                ResolveReferences();
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
            if (playerController == null)
            {
                playerController = FindObjectOfType<PlayerController>();
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

            float safeRotationSmoothTime = Mathf.Max(0.0001f, rotationSmoothTime);
            _currentYaw = Mathf.SmoothDampAngle(_currentYaw, targetYaw, ref _yawVelocity, safeRotationSmoothTime);
            _currentPitch = Mathf.SmoothDampAngle(_currentPitch, targetPitch, ref _pitchVelocity, safeRotationSmoothTime);

            Vector3 anchor = GetAnchorPosition();
            cameraRoot.position = anchor;
            cameraRoot.rotation = Quaternion.Euler(0f, _currentYaw, 0f);

            Quaternion orbitRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 desiredPosition = anchor + orbitRotation * followOffset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _positionVelocity, positionSmoothTime);

            Vector3 lookDirection = anchor - transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                float rotationLerp = 1f - Mathf.Exp(-Time.deltaTime / safeRotationSmoothTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationLerp);
            }
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

            Vector3 delta = followTarget.position - _lastFollowPosition;
            delta.y = 0f;
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            return delta.magnitude / deltaTime;
        }

        private void UpdateFollowPositionSnapshot()
        {
            _lastFollowPosition = followTarget.position;
            _hasLastFollowPosition = true;
        }

        private Vector3 GetAnchorPosition()
        {
            Vector3 anchor = followTarget.position;
            anchor.y += targetHeight;
            return anchor;
        }

    }
}
