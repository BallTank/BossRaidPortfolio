using Core.Common.Patterns;

namespace Core.Player
{
    /// <summary>
    /// Player 전용 상태 기본 클래스. BaseState를 상속받아 Update 시그니처 정의.
    /// </summary>
    public abstract class PlayerBaseState : BaseState<PlayerController>
    {
        public PlayerBaseState(PlayerController controller) : base(controller) { }

        /// <summary>
        /// 매 프레임 호출. 플레이어 입력 패킷을 받아 처리.
        /// </summary>
        public abstract void Update(PlayerInputPacket input);
    }
}
