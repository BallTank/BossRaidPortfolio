using Core.Combat;
using Core.Player;
using UnityEngine;

namespace Core.Multiplayer
{
    public sealed class HostPlayerReactionResolver
    {
        private const int RawHitLogCapacity = 16;

        public readonly struct RawHitLogEntry
        {
            public readonly ulong DealerClientId;
            public readonly int ActionSequence;
            public readonly InputFlag ActionFlag;
            public readonly int DamageAmount;
            public readonly int ServerTick;

            public RawHitLogEntry(ulong dealerClientId, int actionSequence, InputFlag actionFlag, int damageAmount, int serverTick)
            {
                DealerClientId = dealerClientId;
                ActionSequence = actionSequence;
                ActionFlag = actionFlag;
                DamageAmount = damageAmount;
                ServerTick = serverTick;
            }
        }

        private readonly RawHitLogEntry[] _rawHitLogEntries = new RawHitLogEntry[RawHitLogCapacity];
        private int _nextReactionSequence = 1;
        private int _rawHitLogWriteIndex;
        private int _rawHitLogCount;

        public void Reset()
        {
            _nextReactionSequence = 1;
            _rawHitLogWriteIndex = 0;
            _rawHitLogCount = 0;

            for (int i = 0; i < _rawHitLogEntries.Length; i++)
            {
                _rawHitLogEntries[i] = default;
            }
        }

        public void SeedFromRuntime(PlayerController controller, Health health, int serverTick, ref HostPlayerState hostState)
        {
            hostState = HostPlayerState.Create(controller, health, serverTick);
            _nextReactionSequence = Mathf.Max(1, hostState.LastConsumedReactionSequence + 1);
        }

        public void SyncRuntimeState(PlayerController controller, Health health, int serverTick, ref HostPlayerState hostState)
        {
            if (!hostState.IsInitialized)
            {
                SeedFromRuntime(controller, health, serverTick, ref hostState);
                return;
            }

            hostState.SyncFromRuntime(controller, health, serverTick);
        }

        public bool TryResolveBossHit(
            in BossAttackHitData hitData,
            BossAttackHitResolution resolution,
            PlayerController controller,
            Health health,
            int serverTick,
            ref HostPlayerState hostState,
            out HostToClientPlayerReactionSnapshot snapshot)
        {
            snapshot = default;

            if (resolution == BossAttackHitResolution.Ignored)
            {
                SyncRuntimeState(controller, health, serverTick, ref hostState);
                return false;
            }

            int previousHealth = hostState.CurrentHealth;
            bool wasStunned = hostState.IsStunned;
            bool wasDead = hostState.IsDead;
            InputFlag interruptedAction = hostState.ActiveActionFlag;

            SyncRuntimeState(controller, health, serverTick, ref hostState);

            int damageAmount = previousHealth > hostState.CurrentHealth
                ? previousHealth - hostState.CurrentHealth
                : 0;

            HostPlayerReactionFlags reactionFlags = HostPlayerReactionFlags.None;
            if (resolution == BossAttackHitResolution.Damaged || damageAmount > 0)
            {
                reactionFlags |= HostPlayerReactionFlags.Hit;
            }

            if (hostState.IsStunned && !wasStunned)
            {
                reactionFlags |= HostPlayerReactionFlags.Stun;
            }

            if (hostState.IsDead && !wasDead)
            {
                reactionFlags |= HostPlayerReactionFlags.Death;
            }

            if (interruptedAction != 0 && (reactionFlags & (HostPlayerReactionFlags.Hit | HostPlayerReactionFlags.Stun | HostPlayerReactionFlags.Death)) != 0)
            {
                reactionFlags |= HostPlayerReactionFlags.InterruptedAction;
            }
            else
            {
                interruptedAction = 0;
            }

            if (reactionFlags == HostPlayerReactionFlags.None)
            {
                return false;
            }

            snapshot = HostToClientPlayerReactionSnapshot.Create(
                _nextReactionSequence++,
                serverTick,
                reactionFlags,
                damageAmount,
                hostState.CurrentHealth,
                hostState.MaxHealth,
                interruptedAction,
                hitData.HitType);

            hostState.LastConsumedReactionSequence = snapshot.ReactionSequence;
            hostState.LastProcessedServerTick = serverTick;
            return true;
        }

        public bool TryRecordDamageContribution(ulong dealerClientId, in HostPlayerState hostState, int damageAmount, int serverTick, out RawHitLogEntry entry)
        {
            entry = default;

            if (damageAmount <= 0 || hostState.LastAcceptedActionSequence <= 0)
            {
                return false;
            }

            InputFlag actionFlag = hostState.LastAcceptedActionFlag != 0
                ? hostState.LastAcceptedActionFlag
                : InputFlag.Attack;

            entry = new RawHitLogEntry(
                dealerClientId,
                hostState.LastAcceptedActionSequence,
                actionFlag,
                damageAmount,
                serverTick);

            _rawHitLogEntries[_rawHitLogWriteIndex] = entry;
            _rawHitLogWriteIndex = (_rawHitLogWriteIndex + 1) % RawHitLogCapacity;
            _rawHitLogCount = Mathf.Min(_rawHitLogCount + 1, RawHitLogCapacity);
            return true;
        }
    }
}
