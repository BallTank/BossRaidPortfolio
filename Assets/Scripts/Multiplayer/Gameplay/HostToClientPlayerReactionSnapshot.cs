using Core.Combat;
using Core.Player;
using Unity.Netcode;

namespace Core.Multiplayer
{
    [System.Flags]
    public enum HostPlayerReactionFlags : byte
    {
        None = 0,
        Hit = 1 << 0,
        Stun = 1 << 1,
        Death = 1 << 2,
        InterruptedAction = 1 << 3
    }

    public struct HostToClientPlayerReactionSnapshot : INetworkSerializable
    {
        public int ReactionSequence;
        public int ServerTick;
        public int DamageAmount;
        public int ResultHealth;
        public int MaxHealth;
        public byte ReactionBits;
        public byte InterruptedAction;
        public byte SourceHitType;

        public HostPlayerReactionFlags ReactionFlags => (HostPlayerReactionFlags)ReactionBits;
        public InputFlag InterruptedActionFlag => (InputFlag)InterruptedAction;
        public BossAttackHitType SourceHitTypeValue => (BossAttackHitType)SourceHitType;
        public bool IsValid => ReactionSequence > 0 && ReactionBits != 0;

        public static HostToClientPlayerReactionSnapshot Create(
            int reactionSequence,
            int serverTick,
            HostPlayerReactionFlags reactionFlags,
            int damageAmount,
            int resultHealth,
            int maxHealth,
            InputFlag interruptedAction,
            BossAttackHitType sourceHitType)
        {
            return new HostToClientPlayerReactionSnapshot
            {
                ReactionSequence = reactionSequence,
                ServerTick = serverTick,
                DamageAmount = damageAmount,
                ResultHealth = resultHealth,
                MaxHealth = maxHealth,
                ReactionBits = (byte)reactionFlags,
                InterruptedAction = (byte)interruptedAction,
                SourceHitType = (byte)sourceHitType
            };
        }

        public bool HasFlag(HostPlayerReactionFlags reactionFlags)
        {
            return (ReactionFlags & reactionFlags) == reactionFlags;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ReactionSequence);
            serializer.SerializeValue(ref ServerTick);
            serializer.SerializeValue(ref DamageAmount);
            serializer.SerializeValue(ref ResultHealth);
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref ReactionBits);
            serializer.SerializeValue(ref InterruptedAction);
            serializer.SerializeValue(ref SourceHitType);
        }
    }
}
