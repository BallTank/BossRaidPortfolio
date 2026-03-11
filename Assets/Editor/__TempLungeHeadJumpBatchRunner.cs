using System;
using System.IO;
using System.Reflection;
using Core.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TempBatch
{
    [InitializeOnLoad]
    public static class TempLungeHeadJumpBatchRunner
    {
        private const string ScenePath = "Assets/Scenes/GamePlayScene_TestResult.unity";
        private const string ResultPath = "Temp/lunge-head-jump-result.txt";
        private const double DurationSeconds = 12.0;
        private const double HardTimeoutSeconds = 90.0;

        private const string SessionActiveKey = "TempLungeProbe.Active";
        private const string SessionStartedKey = "TempLungeProbe.Started";
        private const string SessionFinishedKey = "TempLungeProbe.Finished";
        private const string SessionPlayStartKey = "TempLungeProbe.PlayStart";
        private const string SessionRunStartKey = "TempLungeProbe.RunStart";

        private static BossController _boss;
        private static CharacterController _bossController;
        private static Transform _player;
        private static CharacterController _playerController;
        private static bool _playerRepositioned;
        private static bool _headStandDetected;
        private static bool _collisionModeApplied;
        private static float _maxBottomMinusPlayerTop = float.NegativeInfinity;
        private static float _minPlanarDistanceAtPositiveDelta = float.PositiveInfinity;
        private static float _lastBossBottom;
        private static float _lastPlayerTop;
        private static float _lastPlanarDistance;
        private static string _error = "-";

        static TempLungeHeadJumpBatchRunner()
        {
            if (SessionState.GetBool(SessionActiveKey, false))
            {
                EnsureHooks();
            }
        }

        public static void Run()
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ResultPath, "status=starting\n");

            SessionState.SetBool(SessionActiveKey, true);
            SessionState.SetBool(SessionStartedKey, false);
            SessionState.SetBool(SessionFinishedKey, false);
            SessionState.SetFloat(SessionRunStartKey, (float)EditorApplication.timeSinceStartup);

            ResetRuntimeState();
            EnsureHooks();

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void EnsureHooks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(SessionActiveKey, false))
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(SessionStartedKey, true);
                SessionState.SetFloat(SessionPlayStartKey, (float)EditorApplication.timeSinceStartup);
                ResetRuntimeState();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(SessionFinishedKey, false))
            {
                CleanupAndExit();
            }
        }

        private static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(SessionActiveKey, false))
            {
                return;
            }

            double runElapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(SessionRunStartKey, 0f);
            if (runElapsed >= HardTimeoutSeconds)
            {
                _error = "timeout";
                WriteResultAndStop(runElapsed);
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (!SessionState.GetBool(SessionStartedKey, false))
            {
                return;
            }

            try
            {
                ResolveReferences();
                if (_boss == null || _player == null || _bossController == null || _playerController == null)
                {
                    return;
                }

                if (!_collisionModeApplied)
                {
                    ApplyCollisionModeFromArgs();
                    _collisionModeApplied = true;
                }

                if (!_playerRepositioned)
                {
                    Vector3 front = _boss.transform.forward;
                    front.y = 0f;
                    if (front.sqrMagnitude <= 0.000001f)
                    {
                        front = Vector3.forward;
                    }

                    front.Normalize();
                    Vector3 targetPosition = _boss.transform.position + front * 15f;
                    targetPosition.y = 1f;
                    _player.position = targetPosition;
                    _playerRepositioned = true;
                }

                Vector3 bossPos = _boss.transform.position;
                Vector3 playerPos = _player.position;
                _lastPlanarDistance = BossController.GetPlanarDistance(bossPos, playerPos);

                _lastBossBottom = bossPos.y + _bossController.center.y - (_bossController.height * 0.5f);
                _lastPlayerTop = playerPos.y + _playerController.center.y + (_playerController.height * 0.5f);

                float delta = _lastBossBottom - _lastPlayerTop;
                if (delta > _maxBottomMinusPlayerTop)
                {
                    _maxBottomMinusPlayerTop = delta;
                }

                if (delta > 0f && _lastPlanarDistance < _minPlanarDistanceAtPositiveDelta)
                {
                    _minPlanarDistanceAtPositiveDelta = _lastPlanarDistance;
                }

                if (delta > 0.08f && _lastPlanarDistance <= 1.2f)
                {
                    _headStandDetected = true;
                }

                double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(SessionPlayStartKey, 0f);
                if (elapsed >= DurationSeconds)
                {
                    WriteResultAndStop(elapsed);
                }
            }
            catch (Exception ex)
            {
                _error = ex.ToString().Replace('\n', ' ').Replace('\r', ' ');
                double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(SessionPlayStartKey, 0f);
                WriteResultAndStop(elapsed);
            }
        }

        private static void ApplyCollisionModeFromArgs()
        {
            if (_boss == null || _bossController == null || _player == null)
            {
                return;
            }

            bool forceIgnoreOff = HasCommandFlag("-forceIgnoreOff");
            FieldInfo ignoreField = typeof(BossController).GetField("ignorePlayerCollision", BindingFlags.NonPublic | BindingFlags.Instance);
            if (ignoreField != null)
            {
                ignoreField.SetValue(_boss, !forceIgnoreOff);
            }

            Collider[] playerColliders = _player.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider == null) continue;
                Physics.IgnoreCollision(_bossController, playerCollider, !forceIgnoreOff);
            }
        }

        private static bool HasCommandFlag(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ResolveReferences()
        {
            if (_boss == null)
            {
                _boss = UnityEngine.Object.FindObjectOfType<BossController>();
                if (_boss != null)
                {
                    _bossController = _boss.GetComponent<CharacterController>();
                }
            }

            if (_player == null)
            {
                GameObject playerObject = GameObject.Find("Player");
                if (playerObject != null)
                {
                    _player = playerObject.transform;
                    _playerController = playerObject.GetComponent<CharacterController>();
                }
            }
        }

        private static void WriteResultAndStop(double elapsed)
        {
            string minPlanarText = float.IsPositiveInfinity(_minPlanarDistanceAtPositiveDelta)
                ? "inf"
                : _minPlanarDistanceAtPositiveDelta.ToString("F4");

            string content =
                $"status=completed\n" +
                $"elapsed={elapsed:F3}\n" +
                $"head_stand_detected={_headStandDetected}\n" +
                $"max_bottom_minus_player_top={_maxBottomMinusPlayerTop:F4}\n" +
                $"min_planar_distance_positive_delta={minPlanarText}\n" +
                $"last_planar_distance={_lastPlanarDistance:F4}\n" +
                $"last_boss_bottom={_lastBossBottom:F4}\n" +
                $"last_player_top={_lastPlayerTop:F4}\n" +
                $"error={_error}\n";

            File.WriteAllText(ResultPath, content);

            SessionState.SetBool(SessionFinishedKey, true);
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
            else
            {
                CleanupAndExit();
            }
        }

        private static void CleanupAndExit()
        {
            SessionState.SetBool(SessionActiveKey, false);
            SessionState.SetBool(SessionStartedKey, false);
            SessionState.SetBool(SessionFinishedKey, false);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.Exit(0);
        }

        private static void ResetRuntimeState()
        {
            _boss = null;
            _bossController = null;
            _player = null;
            _playerController = null;
            _playerRepositioned = false;
            _headStandDetected = false;
            _collisionModeApplied = false;
            _maxBottomMinusPlayerTop = float.NegativeInfinity;
            _minPlanarDistanceAtPositiveDelta = float.PositiveInfinity;
            _lastBossBottom = 0f;
            _lastPlayerTop = 0f;
            _lastPlanarDistance = 0f;
            _error = "-";
        }
    }
}
