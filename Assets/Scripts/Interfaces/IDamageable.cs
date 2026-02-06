using UnityEngine;

namespace BossRaid.Interfaces
{
    /// <summary>
    /// 모든 피격 가능한 오브젝트(플레이어, 몬스터, 파괴 가능한 오브젝트 등)가 구현해야 하는 인터페이스.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 데미지를 입었을 때 호출됩니다.
        /// </summary>
        /// <param name="damage">받는 데미지 양</param>
        void TakeDamage(int damage);
    }
}
