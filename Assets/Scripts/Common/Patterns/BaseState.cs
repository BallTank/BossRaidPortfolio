namespace Core.Common.Patterns
{
    /// <summary>
    /// 제네릭 기반 상태 머신의 기본 상태 클래스.
    /// IState 인터페이스 구현하여 StateMachine과 호환.
    /// </summary>
    public abstract class BaseState<TController> : IState
    {
        protected TController Controller;

        public BaseState(TController controller)
        {
            Controller = controller;
        }

        public abstract void Enter();
        public abstract void Exit();
    }
}
