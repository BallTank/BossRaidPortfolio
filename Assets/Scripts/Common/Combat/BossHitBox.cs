using Core.Interfaces;
using UnityEngine;

namespace Core.Combat
{
    /// <summary>
    /// 보스의 자식 콜라이더(Head, Body, Tail 등)에 부착되어 데미지를 본체(Health)로 전달하는 클래스.
    /// 잡몹은 단일 Collider를 사용하므로 이 스크립트가 필요 없음.
    /// </summary>
    public class BossHitBox : MonoBehaviour, IDamageable
    {
        [Header("Settings")]
        [SerializeField] private Health _ownerHealth;

        // 외부에서 Health 접근이 필요할 경우를 위한 프로퍼티
        public Health Owner => _ownerHealth;

        private void Awake()
        {
            // 설정되지 않았을 경우 부모에서 찾기 시도
            if (_ownerHealth == null)
            {
                _ownerHealth = GetComponentInParent<Health>();
            }

            if (_ownerHealth == null)
            {
                Debug.LogWarning($"⚠️ BossHitBox ({gameObject.name}) has no owner Health assigned!");
            }
        }

        public void TakeDamage(int damage)
        {
            if (_ownerHealth != null)
            {
                _ownerHealth.TakeDamage(damage);
            }
        }
    }
}
