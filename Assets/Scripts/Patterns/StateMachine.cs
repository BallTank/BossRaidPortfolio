namespace BossRaid.Patterns
{
    public class StateMachine
    {
        private BaseState _currentState;

        public void ChangeState(BaseState newState)
        {
            _currentState?.Exit();
            _currentState = newState;
            _currentState?.Enter();
        }

        public void Update(PlayerInputPacket input)
        {
            _currentState?.Update(input);
        }
    }
}
