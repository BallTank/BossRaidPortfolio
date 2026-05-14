using Unity.Netcode;
using UnityEngine;

namespace Core.Multiplayer
{
    /// <summary>
    /// 보스가 remote client에 표시 전용으로 재생시킬 이펙트 종류.
    /// </summary>
    public enum BossReplicatedEffectKind : byte
    {
        None = 0,
        ProjectileShot = 1,
        AoESpawn = 2,
        AttackWarningShow = 3,
        AttackWarningHide = 4,
        BasicAttackSound = 5
    }

    public enum BossReplicatedWarningChannel : byte
    {
        None = 0,
        BasicAttack = 1,
        LungeAttack = 2
    }

    public enum BossReplicatedWarningShape : byte
    {
        None = 0,
        Sector = 1,
        Strip = 2
    }

    /// <summary>
    /// Host가 기록하고 client가 display-only로 재생하는 보스 이펙트 이벤트.
    /// </summary>
    public struct BossReplicatedEffectEvent : INetworkSerializable
    {
        public BossReplicatedEffectKind EffectKind;
        public int SequenceId;
        public Vector3 StartPosition;
        public Vector3 Direction;
        public Vector3 ImpactPosition;
        public float Speed;
        public float Lifetime;
        public float WarningDuration;
        public float ActiveDuration;
        public float Radius;
        public float Length;
        public float Width;
        public float SectorAngle;
        public ulong TargetNetworkObjectId;
        public float HomingStrength;
        public float HomingDuration;
        public float VerticalFollowSpeed;
        public BossReplicatedWarningChannel WarningChannel;
        public BossReplicatedWarningShape WarningShape;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref EffectKind);
            serializer.SerializeValue(ref SequenceId);
            serializer.SerializeValue(ref StartPosition);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref ImpactPosition);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref Lifetime);
            serializer.SerializeValue(ref WarningDuration);
            serializer.SerializeValue(ref ActiveDuration);
            serializer.SerializeValue(ref Radius);
            serializer.SerializeValue(ref Length);
            serializer.SerializeValue(ref Width);
            serializer.SerializeValue(ref SectorAngle);
            serializer.SerializeValue(ref TargetNetworkObjectId);
            serializer.SerializeValue(ref HomingStrength);
            serializer.SerializeValue(ref HomingDuration);
            serializer.SerializeValue(ref VerticalFollowSpeed);
            serializer.SerializeValue(ref WarningChannel);
            serializer.SerializeValue(ref WarningShape);
        }
    }
}
