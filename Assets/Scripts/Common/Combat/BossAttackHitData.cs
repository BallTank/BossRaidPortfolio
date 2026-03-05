using UnityEngine;

namespace Core.Combat
{
    public enum BossAttackHitType
    {
        Unknown = 0,
        Attack1 = 1,
        Attack2 = 2,
        Attack3Projectile = 3,
        Attack4Projectile = 4
    }

    public enum BossAttackHitResolution
    {
        Ignored = 0,
        Damaged = 1,
        StunOnly = 2
    }

    public readonly struct BossAttackHitData
    {
        public readonly int Damage;
        public readonly BossAttackHitType HitType;
        public readonly Vector3 ForceDirection;

        public BossAttackHitData(int damage, BossAttackHitType hitType, Vector3 forceDirection)
        {
            Damage = damage;
            HitType = hitType;
            ForceDirection = forceDirection;
        }
    }

    public interface IBossAttackHitReceiver
    {
        BossAttackHitResolution ReceiveBossAttackHit(in BossAttackHitData hitData);
    }
}
