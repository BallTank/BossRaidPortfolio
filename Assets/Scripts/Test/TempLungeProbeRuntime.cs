using System;
using System.IO;
using UnityEngine;

public sealed class TempLungeProbeRuntime : MonoBehaviour
{
    private const string CommandFlag = "-probeLunge";
    private const string ResultPath = "lunge-runtime-probe-result.txt";
    private const float ProbeDuration = 12f;
    private const float PositiveDeltaThreshold = 0.08f;
    private const float NearPlanarThreshold = 1.2f;

    private bool _active;
    private GameObject _bossObject;
    private GameObject _playerObject;
    private CharacterController _bossController;
    private CharacterController _playerController;
    private bool _playerRepositioned;
    private bool _headStandDetected;
    private float _maxBottomMinusPlayerTop = float.NegativeInfinity;
    private float _minPlanarAtPositiveDelta = float.PositiveInfinity;
    private float _elapsed;
    private string _error = "-";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!HasFlag(CommandFlag))
        {
            return;
        }

        GameObject host = new GameObject("TempLungeProbeRuntimeHost");
        DontDestroyOnLoad(host);
        host.AddComponent<TempLungeProbeRuntime>();
    }

    private static bool HasFlag(string flag)
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

    private void Start()
    {
        _active = true;
        File.WriteAllText(ResultPath, "status=starting\n");
    }

    private void Update()
    {
        if (!_active) return;

        try
        {
            if (!ResolveReferences())
            {
                _elapsed += Time.deltaTime;
                if (_elapsed > 30f)
                {
                    _error = "reference_timeout";
                    FinishAndQuit();
                }

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

            _elapsed += Time.deltaTime;
            if (_elapsed >= ProbeDuration)
            {
                FinishAndQuit();
            }
        }
        catch (Exception ex)
        {
            _error = ex.ToString().Replace('\n', ' ').Replace('\r', ' ');
            FinishAndQuit();
        }
    }

    private bool ResolveReferences()
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

        return _bossObject != null &&
               _playerObject != null &&
               _bossController != null &&
               _playerController != null;
    }

    private void FinishAndQuit()
    {
        if (!_active) return;
        _active = false;

        string minPlanarText = float.IsPositiveInfinity(_minPlanarAtPositiveDelta)
            ? "inf"
            : _minPlanarAtPositiveDelta.ToString("F4");

        string text =
            $"status=completed\n" +
            $"head_stand_detected={_headStandDetected}\n" +
            $"max_bottom_minus_player_top={_maxBottomMinusPlayerTop:F4}\n" +
            $"min_planar_at_positive_delta={minPlanarText}\n" +
            $"error={_error}\n";

        File.WriteAllText(ResultPath, text);
        Application.Quit(0);
    }
}
