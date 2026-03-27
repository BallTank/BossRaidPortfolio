using System;
using Core.Player;
using UnityEngine;

namespace Core.Player
{
    public class LocalInputProvider : MonoBehaviour, IInputProvider
    {
        [Header("Settings")]
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private bool _startInputEnabled = true;

        private PlayerControlInput _inputActions;
        private static LocalInputProvider _cursorOwner;

        private float _currentYaw;
        private float _currentPitch;
        private bool _runtimeInputEnabled;
        private Vector2 _cachedMoveDirection;
        private byte _cachedButtons;

        private void Awake()
        {
            _inputActions = new PlayerControlInput();
            _currentYaw = transform.eulerAngles.y;
            _currentPitch = transform.eulerAngles.x;
            _runtimeInputEnabled = false;
            _cachedMoveDirection = Vector2.zero;
            _cachedButtons = 0;
        }

        private void OnEnable()
        {
            SetRuntimeInputEnabled(_startInputEnabled);
        }

        private void OnDisable()
        {
            SetRuntimeInputEnabled(false);
        }

        private void Update()
        {
            if (!_runtimeInputEnabled)
            {
                return;
            }

            Vector2 mouseDelta = _inputActions.Player.Look.ReadValue<Vector2>();
            _currentYaw += mouseDelta.x * mouseSensitivity;

            _currentPitch -= mouseDelta.y * mouseSensitivity;
            _currentPitch = Mathf.Clamp(_currentPitch, -80f, 80f);
            RefreshCachedGameplayInput();
        }

        public PlayerInputPacket GetInput()
        {
            PlayerInputPacket packet = new PlayerInputPacket();
            if (!_runtimeInputEnabled)
            {
                packet.lookYaw = _currentYaw;
                packet.lookPitch = _currentPitch;
                return packet;
            }

            packet.moveDir = _cachedMoveDirection;
            packet.lookYaw = _currentYaw;
            packet.lookPitch = _currentPitch;
            packet.buttons = _cachedButtons;

            return packet;
        }

        public void SetLookAngles(float yaw, float pitch)
        {
            _currentYaw = yaw;
            _currentPitch = Mathf.Clamp(pitch, -80f, 80f);
        }

        public void SetRuntimeInputEnabled(bool enabled)
        {
            if (_inputActions == null)
            {
                return;
            }

            _startInputEnabled = enabled;
            _runtimeInputEnabled = enabled;

            if (enabled)
            {
                _inputActions.Player.Enable();
                RefreshCachedGameplayInput();
                _cursorOwner = this;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }

            _inputActions.Player.Disable();
            _cachedMoveDirection = Vector2.zero;
            _cachedButtons = 0;
            if (_cursorOwner == this)
            {
                _cursorOwner = null;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void RefreshCachedGameplayInput()
        {
            if (_inputActions == null)
            {
                return;
            }

            _cachedMoveDirection = _inputActions.Player.Move.ReadValue<Vector2>().normalized;
            _cachedButtons = 0;

            if (_inputActions.Player.Dash.IsPressed())
            {
                _cachedButtons |= (byte)InputFlag.Dash;
            }

            if (_inputActions.Player.Attack.IsPressed())
            {
                _cachedButtons |= (byte)InputFlag.Attack;
            }

            if (_inputActions.Player.Jump.IsPressed())
            {
                _cachedButtons |= (byte)InputFlag.Jump;
            }
        }
    }
}
