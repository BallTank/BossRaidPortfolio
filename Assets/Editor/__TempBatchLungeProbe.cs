using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TempBatchLungeProbe
{
    private const string CommandFlag = "-runLungeProbe";
    private const string ScenePath = "Assets/Scenes/GamePlayScene_TestResult.unity";
    private const string ResultPath = "Temp/lunge-probe-result.txt";
    private const double ProbeDurationSeconds = 12.0;
    private const double HardTimeoutSeconds = 90.0;
    private const float PositiveDeltaThreshold = 0.08f;
    private const float NearPlanarThreshold = 1.2f;

    private static readonly string[] Args;

    private static bool _active;
    private static bool _startedPlayMode;
    private static bool _finished;
    private static double _startTime;
    private static double _playStartTime;

    private static GameObject _bossObject;
    private static GameObject _playerObject;
    private static CharacterController _bossController;
    private static CharacterController _playerController;
    private static bool _playerRepositioned;
    private static bool _headStandDetected;
    private static float _maxBottomMinusPlayerTop = float.NegativeInfinity;
    private static float _minPlanarAtPositiveDelta = float.PositiveInfinity;
    private static string _error = "-";

    static TempBatchLungeProbe()
    {
        Args = Environment.GetCommandLineArgs();
        if (!Application.isBatchMode) return;
        if (!HasFlag(CommandFlag)) return;

        _active = true;
        _startTime = EditorApplication.timeSinceStartup;
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ResultPath, "status=starting\n");

        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.delayCall += Begin;
    }

    private static bool HasFlag(string flag)
    {
        for (int i = 0; i < Args.Length; i++)
        {
            if (string.Equals(Args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void Begin()
    {
        if (!_active || _finished) return;

        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            _startedPlayMode = true;
            EditorApplication.isPlaying = true;
        }
        catch (Exception ex)
        {
            _error = ex.ToString().Replace('\n', ' ').Replace('\r', ' ');
            FinishAndExit();
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!_active || _finished) return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _playStartTime = EditorApplication.timeSinceStartup;
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode && _startedPlayMode)
        {
            FinishAndExit();
        }
    }

    private static void OnEditorUpdate()
    {
        if (!_active || _finished) return;

        double totalElapsed = EditorApplication.timeSinceStartup - _startTime;
        if (totalElapsed > HardTimeoutSeconds)
        {
            _error = "timeout";
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }

            FinishAndExit();
            return;
        }

        if (!EditorApplication.isPlaying) return;

        try
        {
            ResolveReferences();
            if (_bossObject == null || _playerObject == null || _bossController == null || _playerController == null)
            {
                return;
            }

            if (!_playerRepositioned)
            {
                Vector3 forward = _bossObject.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude <= 0.000001f)
                {
                    forward = Vector3.forward;
                }

                forward.Normalize();
                Vector3 playerStart = _bossObject.transform.position + forward * 15f;
                playerStart.y = 1f;
                _playerObject.transform.position = playerStart;
                _playerRepositioned = true;
            }

            Vector3 bossPos = _bossObject.transform.position;
            Vector3 playerPos = _playerObject.transform.position;
            Vector3 planarDelta = playerPos - bossPos;
            planarDelta.y = 0f;
            float planarDistance = planarDelta.magnitude;

            float bossBottom = bossPos.y + _bossController.center.y - (_bossController.height * 0.5f);
            float playerTop = playerPos.y + _playerController.center.y + (_playerController.height * 0.5f);
            float delta = bossBottom - playerTop;

            if (delta > _maxBottomMinusPlayerTop)
            {
                _maxBottomMinusPlayerTop = delta;
            }

            if (delta > 0f && planarDistance < _minPlanarAtPositiveDelta)
            {
                _minPlanarAtPositiveDelta = planarDistance;
            }

            if (delta > PositiveDeltaThreshold && planarDistance <= NearPlanarThreshold)
            {
                _headStandDetected = true;
            }

            double playElapsed = EditorApplication.timeSinceStartup - _playStartTime;
            if (playElapsed >= ProbeDurationSeconds)
            {
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception ex)
        {
            _error = ex.ToString().Replace('\n', ' ').Replace('\r', ' ');
            EditorApplication.isPlaying = false;
        }
    }

    private static void ResolveReferences()
    {
        if (_bossObject == null)
        {
            _bossObject = GameObject.Find("Boss");
            if (_bossObject != null)
            {
                _bossController = _bossObject.GetComponent<CharacterController>();
            }
        }

        if (_playerObject == null)
        {
            _playerObject = GameObject.Find("Player");
            if (_playerObject != null)
            {
                _playerController = _playerObject.GetComponent<CharacterController>();
            }
        }
    }

    private static void FinishAndExit()
    {
        if (_finished) return;
        _finished = true;

        string minPlanarText = float.IsPositiveInfinity(_minPlanarAtPositiveDelta)
            ? "inf"
            : _minPlanarAtPositiveDelta.ToString("F4");

        string output =
            $"status=completed\n" +
            $"head_stand_detected={_headStandDetected}\n" +
            $"max_bottom_minus_player_top={_maxBottomMinusPlayerTop:F4}\n" +
            $"min_planar_at_positive_delta={minPlanarText}\n" +
            $"error={_error}\n";

        File.WriteAllText(ResultPath, output);

        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.Exit(0);
    }
}
