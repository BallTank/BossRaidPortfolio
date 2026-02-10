using UnityEngine;

namespace Core.Player.States
{
    public class HitState : PlayerBaseState
    {
        private float _timer;
        private const float StunDuration = 0.5f;

        public HitState(PlayerController controller) : base(controller) { }

        public override void Enter()
        {
            // 비주얼 담당 클래스에 위임
            Controller.Visual?.TriggerHit();

            _timer = StunDuration;
        }

        public override void Update(PlayerInputPacket input)
        {
            _timer -= Time.deltaTime;

            if (_timer <= 0)
            {
                Controller.StateMachine.ChangeState(Controller.MoveState);
            }
        }

        public override void Exit() { }
    }
}
