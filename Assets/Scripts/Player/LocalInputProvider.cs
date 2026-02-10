using System;
using Core.Player;
using UnityEngine;

namespace Core.Player
{
    public class LocalInputProvider : MonoBehaviour, IInputProvider
    {
        [Header("Settings")]
        [SerializeField] private float mouseSensitivity = 0.1f;

        // [수정됨] PlayerControlInput으로 변경
        private PlayerControlInput _inputActions;

        private float _currentYaw;
        private float _currentPitch;

        private void Awake()
        {
            // [수정됨] 생성자도 PlayerControlInput으로 변경
            _inputActions = new PlayerControlInput();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _currentYaw = transform.eulerAngles.y;
            _currentPitch = transform.eulerAngles.x;
        }

        private void OnEnable()
        {
            // Action Map 이름이 "Player"인지 확인!
            _inputActions.Player.Enable();
        }

        private void OnDisable()
        {
            _inputActions.Player.Disable();
        }

        private void Update()
        {
            // Action 이름 "Look"
            Vector2 mouseDelta = _inputActions.Player.Look.ReadValue<Vector2>();
            _currentYaw += mouseDelta.x * mouseSensitivity;

            // 위아래(Pitch) 누적 및 제한
            // 마우스 위로 올리면(-) 각도가 줄어야 고개가돌림
            _currentPitch -= mouseDelta.y * mouseSensitivity;
            _currentPitch = Mathf.Clamp(_currentPitch, -80f, 80f); // 목꺾임 방지
        }

        public PlayerInputPacket GetInput()
        {
            PlayerInputPacket packet = new PlayerInputPacket();

            // 1. 이동 (Move)
            packet.moveDir = _inputActions.Player.Move.ReadValue<Vector2>().normalized;

            // 2. 시점
            packet.lookYaw = _currentYaw;
            packet.lookPitch = _currentPitch;

            // 3. 버튼 (Dash, Attack, Jump)
            packet.SetFlag(InputFlag.Dash, _inputActions.Player.Dash.IsPressed());
            packet.SetFlag(InputFlag.Attack, _inputActions.Player.Attack.IsPressed());
            packet.SetFlag(InputFlag.Jump, _inputActions.Player.Jump.IsPressed());

            return packet;
        }
    }
}