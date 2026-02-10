using Core.Common.Patterns;
using Core.Player;
using Core.Player.States;
using UnityEngine;

namespace Core.Common.Interfaces
{
    /// <summary>
    /// 대시 로직에 필요한 데이터와 메서드를 정의하는 계약(Contract).
    /// DashState는 이 인터페이스에만 의존하여 PlayerController와의 결합도를 낮춤.
    /// </summary>
    public interface IDashContext
    {
        // 이동 속도 (대시 속도 계산에 사용)
        float MoveSpeed { get; }

        // 대시 지속 시간
        float DashDuration { get; }

        // 대시 속도 배율
        float DashSpeedMultiplier { get; }

        // Transform (대시 방향 결정용)
        Transform transform { get; }

        // CharacterController (실제 이동 수행)
        CharacterController CharController { get; }

        // StateMachine (상태 전환용) - 제네릭 타입
        StateMachine<PlayerBaseState> StateMachine { get; }

        // MoveState (복귀용)
        MoveState MoveState { get; }

        // 쿨타임 시작
        void StartDashCooldown();
    }
}
