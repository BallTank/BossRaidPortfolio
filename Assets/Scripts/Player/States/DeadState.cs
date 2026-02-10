using UnityEngine;

namespace Core.Player.States
{
    public class DeadState : PlayerBaseState
    {
        public DeadState(PlayerController controller) : base(controller) { }

        public override void Enter()
        {
            Debug.Log("Enter DeadState");
            Controller.Animator?.CrossFade(PlayerController.ANIM_STATE_DIE, 0.1f);
            // 이동/물리 연산 충돌 방지를 위해 CharacterController 비활성화
            Controller.CharController.enabled = false;
        }

        public override void Exit()
        {
            // 일반적으로 부활(Respawn)하지 않는 한 DeadState를 나가지 않음
            Controller.CharController.enabled = true;
        }

        public override void Update(PlayerInputPacket input)
        {
            // DeadState에서는 업데이트 로직 없음
            // 입력 무시
        }
    }
}
