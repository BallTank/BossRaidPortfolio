using UnityEngine;

namespace Core.Player.States
{
    public class HitState : PlayerBaseState
    {
        private float _timer;
        private float _verticalVelocity;
        private const float StunDuration = 0.5f;
        private const float GroundedGravity = -2.0f;

        public HitState(PlayerController controller) : base(controller) { }

        public override void Enter()
        {
            // 비주얼 담당 클래스에 위임
            Controller.Visual?.TriggerHit();

            _timer = StunDuration;

            // 초기 수직 속도 설정
            if (Controller.CharController.isGrounded)
            {
                _verticalVelocity = GroundedGravity;
            }
            else
            {
                _verticalVelocity = 0f;
            }
        }

        public override void Update(PlayerInputPacket input)
        {
            _timer -= Time.deltaTime;

            // 중력 적용 (공중 체공 방지)
            if (Controller.CharController.isGrounded && _verticalVelocity < 0)
            {
                _verticalVelocity = GroundedGravity;
            }
            _verticalVelocity += Controller.Gravity * Time.deltaTime;

            Controller.CharController.Move(Vector3.up * _verticalVelocity * Time.deltaTime);

            if (_timer <= 0)
            {
                Controller.StateMachine.ChangeState(Controller.MoveState);
            }
        }

        public override void Exit() { }
    }
}
