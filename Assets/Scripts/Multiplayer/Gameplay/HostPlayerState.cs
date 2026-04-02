using Core.Combat;
using Core.Player;
using Unity.Netcode;

namespace Core.Multiplayer
{
    public struct HostPlayerState : INetworkSerializable
    {
        public bool IsInitialized;
        public int MaxHealth;
        public int CurrentHealth;
        public int LastProcessedServerTick;
        public int LastAcceptedActionSequence;
        public int LastAcceptedActionStartTick;
        public int LastConsumedReactionSequence;
        public byte ActiveAction;
        public byte LastAcceptedAction;
        public bool IsHitReacting;
        public bool IsStunned;
        public bool IsDead;

        public InputFlag ActiveActionFlag => (InputFlag)ActiveAction;
        public InputFlag LastAcceptedActionFlag => (InputFlag)LastAcceptedAction;
        public bool HasActiveAction => ActiveAction != 0;

        public static HostPlayerState Create(PlayerController controller, Health health, int serverTick)
        {
            HostPlayerState state = default;
            state.SyncFromRuntime(controller, health, serverTick);
            return state;
        }

        public void RecordAcceptedAction(in ClientToHostPlayerActionIntent actionIntent, Health health, int serverTick)
        {
            IsInitialized = true;
            ActiveAction = actionIntent.RequestedAction;
            LastAcceptedAction = actionIntent.RequestedAction;
            LastAcceptedActionSequence = actionIntent.ActionSequence;
            LastAcceptedActionStartTick = serverTick;
            LastProcessedServerTick = serverTick;

            if (health == null)
            {
                return;
            }

            MaxHealth = health.MaxHealth;
            CurrentHealth = health.CurrentHealth;
            IsDead = health.IsDead;
        }

        public void SyncFromRuntime(PlayerController controller, Health health, int serverTick)
        {
            IsInitialized = true;
            LastProcessedServerTick = serverTick;

            if (health != null)
            {
                MaxHealth = health.MaxHealth;
                CurrentHealth = health.CurrentHealth;
                IsDead = health.IsDead;
            }

            if (controller == null || controller.StateMachine == null)
            {
                if (serverTick > LastAcceptedActionStartTick)
                {
                    ActiveAction = 0;
                }

                IsHitReacting = false;
                IsStunned = false;
                return;
            }

            bool isInHit = controller.StateMachine.CurrentState == controller.HitState;
            bool isInStun = controller.StateMachine.CurrentState == controller.StunState;
            bool isDeadState = controller.StateMachine.CurrentState == controller.DeadState;
            InputFlag observedAction = DetermineObservedAction(controller);

            IsHitReacting = isInHit;
            IsStunned = isInStun;
            IsDead = IsDead || isDeadState;

            if (observedAction != 0)
            {
                ActiveAction = (byte)observedAction;
            }
            else if (serverTick > LastAcceptedActionStartTick)
            {
                ActiveAction = 0;
            }
        }

        private static InputFlag DetermineObservedAction(PlayerController controller)
        {
            if (controller == null || controller.StateMachine == null)
            {
                return 0;
            }

            if (controller.IsDashStateActive)
            {
                return InputFlag.Dash;
            }

            if (controller.StateMachine.CurrentState == controller.AttackState)
            {
                return InputFlag.Attack;
            }

            return 0;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsInitialized);
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref CurrentHealth);
            serializer.SerializeValue(ref LastProcessedServerTick);
            serializer.SerializeValue(ref LastAcceptedActionSequence);
            serializer.SerializeValue(ref LastAcceptedActionStartTick);
            serializer.SerializeValue(ref LastConsumedReactionSequence);
            serializer.SerializeValue(ref ActiveAction);
            serializer.SerializeValue(ref LastAcceptedAction);
            serializer.SerializeValue(ref IsHitReacting);
            serializer.SerializeValue(ref IsStunned);
            serializer.SerializeValue(ref IsDead);
        }
    }
}
