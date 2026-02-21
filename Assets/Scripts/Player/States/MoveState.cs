using Core.Common.Patterns;
using Core.Player;
using UnityEngine;

namespace Core.Player.States
{
    public class MoveState : PlayerBaseState
    {
        private float _verticalVelocity;
        private bool _wasDashPressed;
        private bool _wasJumpPressed;

        public MoveState(PlayerController controller) : base(controller) { }

        private const float GroundedGravity = -2.0f;

        public override void Enter()
        {
            // 수직 속도 초기화 (공중에서 전이된 경우 속도 유지)
            if (Controller.CharController.isGrounded)
            {
                _verticalVelocity = GroundedGravity;
            }
            else
            {
                _verticalVelocity = Controller.CharController.velocity.y;
            }
            _wasDashPressed = false;
            _wasJumpPressed = false;

            // Animation: Locomotion Blend Tree (Speed = 0 for Idle)
            if (Controller.Animator != null)
            {
                Controller.Animator.CrossFade(PlayerController.ANIM_STATE_LOCOMOTION, 0.1f);
                Controller.Animator.SetFloat(PlayerController.ANIM_PARAM_SPEED, 0f);
            }
        }

        public override void Update(PlayerInputPacket input)
        {
            // 회전 및 이동 처리
            UpdateRotation(input);
            UpdateMovement(input);

            // Animation: Update locomotion blend
            if (Controller.Animator != null)
                Controller.Animator.SetFloat(PlayerController.ANIM_PARAM_SPEED, input.moveDir.magnitude);

            // Attack Transition (지면/공중 모두 가능)
            if (input.HasFlag(InputFlag.Attack))
            {
                Controller.StateMachine.ChangeState(Controller.AttackState);
                return;
            }

            // [DISABLED] Jump Transition - 현재 게임 디자인에서 점프 비활성화 (입력 키는 F10으로 보존)
            // bool jumpPressed = input.HasFlag(InputFlag.Jump);
            // if (jumpPressed && !_wasJumpPressed && Controller.CharController.isGrounded)
            // {
            //     Controller.StateMachine.ChangeState(Controller.JumpState);
            //     return;
            // }
            // _wasJumpPressed = jumpPressed;

            // Dash Transition (엣지 트리거 + 쿨타임 체크)
            bool dashPressed = input.HasFlag(InputFlag.Dash);
            if (dashPressed && !_wasDashPressed && Controller.CanDash)
            {
                Controller.StateMachine.ChangeState(Controller.DashState);
            }
            _wasDashPressed = dashPressed;
        }

        public override void Exit()
        {
            // Cleanup
        }

        private void UpdateRotation(PlayerInputPacket input)
        {
            // 카메라 방향 기준 캐릭터 회전
            Vector3 moveDirection = Controller.GetMovementDirection(input.moveDir);

            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                Controller.transform.rotation = Quaternion.Slerp(Controller.transform.rotation, targetRotation, Controller.RotationSpeed * Time.deltaTime);
            }

            // Calc Velocity
            if (Controller.CharController.isGrounded && _verticalVelocity < 0)
            {
                _verticalVelocity = GroundedGravity;
            }
            _verticalVelocity += Controller.Gravity * Time.deltaTime;

            Vector3 finalVelocity = (moveDirection * Controller.MoveSpeed) + Vector3.up * _verticalVelocity;
            Controller.CharController.Move(finalVelocity * Time.deltaTime);
        }

        private void UpdateMovement(PlayerInputPacket input)
        {
            // Merged into UpdateRotation for consistency with original code flow
        }
    }
}
