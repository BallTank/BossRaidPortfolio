namespace Core.Common.Patterns
{
    /// <summary>
    /// 제네릭 상태 머신. Player/Boss 모두 사용 가능.
    /// Update 호출은 각 Controller에서 직접 처리.
    /// </summary>
    public class StateMachine<TState> where TState : class
    {
        public TState CurrentState { get; private set; }

        public void ChangeState(TState newState)
        {
            // 현재 상태 Exit
            if (CurrentState is IState exitState)
                exitState.Exit();

            CurrentState = newState;

            // 새 상태 Enter
            if (CurrentState is IState enterState)
                enterState.Enter();
        }
    }

    /// <summary>
    /// 상태 공통 인터페이스 (Enter/Exit만 정의)
    /// </summary>
    public interface IState
    {
        void Enter();
        void Exit();
    }
}
