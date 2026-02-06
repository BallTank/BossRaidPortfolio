namespace BossRaid.Patterns
{
    public abstract class BaseState
    {
        protected PlayerController Controller;

        public BaseState(PlayerController controller)
        {
            this.Controller = controller;
        }

        public abstract void Enter();
        public abstract void Update(PlayerInputPacket input);
        public abstract void Exit();
    }
}
