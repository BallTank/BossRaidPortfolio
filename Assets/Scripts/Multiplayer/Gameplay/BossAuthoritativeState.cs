using Unity.Netcode;
using UnityEngine;

namespace Core.Multiplayer
{
    /// <summary>
    /// 보스 전용 authoritative snapshot의 이동 표현 상태.
    /// </summary>
    public enum BossAuthoritativeLocomotionState : byte
    {
        Unknown = 0,
        Idle = 1,
        Move = 2,
        Search = 3,
        Attack = 4,
        Hit = 5,
        PhaseIntro = 6,
        Dead = 7
    }

    /// <summary>
    /// 보스가 현재 재생 중인 공격 식별자.
    /// </summary>
    public enum BossAuthoritativeAttackId : byte
    {
        None = 0,
        Basic = 1,
        Lunge = 2,
        Projectile = 3,
        AoE = 4
    }

    /// <summary>
    /// 보스 페이즈의 dedicated replicated 표현.
    /// </summary>
    public enum BossAuthoritativePhase : byte
    {
        None = 0,
        Phase1 = 1,
        Phase2 = 2
    }

    /// <summary>
    /// Host가 기록하는 boss gameplay truth snapshot.
    /// </summary>
    public struct BossAuthoritativeState : INetworkSerializable
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public BossAuthoritativeLocomotionState LocomotionState;
        public BossAuthoritativeAttackId CurrentAttackId;
        public int AttackStartServerTick;
        public int CurrentHealth;
        public int MaxHealth;
        public BossAuthoritativePhase Phase;
        public bool IsDead;

        public bool HasActiveAttack => CurrentAttackId != BossAuthoritativeAttackId.None;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref LocomotionState);
            serializer.SerializeValue(ref CurrentAttackId);
            serializer.SerializeValue(ref AttackStartServerTick);
            serializer.SerializeValue(ref CurrentHealth);
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref IsDead);
        }
    }
}
