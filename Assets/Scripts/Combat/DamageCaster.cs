using System.Collections.Generic;
using BossRaid.Interfaces;
using UnityEngine;

namespace BossRaid.Combat
{
    /// <summary>
    /// 무기(검)에 부착되어 실제 데미지 판정을 수행하는 클래스.
    /// Physics.OverlapSphereNonAlloc을 사용하여 GC 없는 최적화된 충돌 감지를 수행합니다.
    /// </summary>
    public class DamageCaster : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _radius = 1.0f;
        [SerializeField] private LayerMask _targetLayer;
        [SerializeField] private int _maxTargets = 10;
        [SerializeField] private Transform _castCenter; // 판정 중심점 (입력 안하면 자신의 위치)

        [Header("Debug")]
        [SerializeField] private bool _showGizmos = true;
        [SerializeField] private Color _gizmoColor = Color.red;

        private Collider[] _hitResults;
        private bool _isCasting = false;
        private int _damagePayload = 0;

        // 한 번의 공격(Enable~Disable 기간) 동안 중복 피격을 방지하기 위한 Set
        private HashSet<int> _hitTargets = new HashSet<int>();

        private void Awake()
        {
            _hitResults = new Collider[_maxTargets];
            if (_castCenter == null)
                _castCenter = this.transform;
        }

        /// <summary>
        /// 공격 판정을 시작합니다. (Animation Event에서 호출)
        /// </summary>
        /// <param name="damage">이번 공격의 데미지</param>
        public void EnableHitbox(int damage)
        {
            _isCasting = true;
            _damagePayload = damage;
            _hitTargets.Clear();
        }

        /// <summary>
        /// 공격 판정을 종료합니다. (Animation Event에서 호출)
        /// </summary>
        public void DisableHitbox()
        {
            _isCasting = false;
        }

        private void FixedUpdate()
        {
            if (!_isCasting) return;

            // NonAlloc을 사용하여 가비지 컬렉션 방지
            int hitCount = Physics.OverlapSphereNonAlloc(_castCenter.position, _radius, _hitResults, _targetLayer);

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _hitResults[i];
                if (col == null) continue;

                // 이미 타격한 대상인지 확인 (InstanceID 사용)
                int targetID = col.GetInstanceID();
                if (_hitTargets.Contains(targetID)) continue;

                // IDamageable 인터페이스 확인
                IDamageable target = col.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(_damagePayload);
                    _hitTargets.Add(targetID); // 중복 타격 방지 목록에 추가
                    Debug.Log($"⚔️ Hit Logic: Dealt {_damagePayload} damage to {col.name}");
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            Gizmos.color = _isCasting ? _gizmoColor : new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 0.2f);

            Vector3 center = _castCenter != null ? _castCenter.position : transform.position;
            Gizmos.DrawWireSphere(center, _radius);
        }
    }
}
