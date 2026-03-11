using System;
using System.Collections.Generic;
using Core.Interfaces;
using UnityEngine;

namespace Core.Combat
{
    /// <summary>
    /// 무기(검)에 부착되어 실제 데미지 판정을 수행하는 클래스.
    /// Physics.OverlapSphereNonAlloc을 사용하여 GC 없는 최적화된 충돌 감지를 수행합니다.
    /// </summary>
    [ExecuteAlways]
    public class DamageCaster : MonoBehaviour
    {
        /// <summary>
        /// 한 번의 공격 윈도우가 닫힐 때 호출되는 결과 이벤트.
        /// bool: 적중 여부, int: 누적 피해량
        /// </summary>
        public event Action<bool, int> OnAttackWindowResolved;

        [Header("Settings")]
        [SerializeField] private float _radius = 1.0f;
        [SerializeField] private LayerMask _targetLayer;
        [SerializeField] private int _maxTargets = 10;
        [SerializeField] private Transform _castCenter; // 판정 중심점 (입력 안하면 자신의 위치)
        [SerializeField] private BossAttackHitType _bossAttackHitType = BossAttackHitType.Unknown;

        [Header("Debug")]
        [SerializeField] private bool _showGizmos = true;
        [SerializeField] private Color _gizmoColor = Color.red;

        private Collider[] _hitResults;
        private bool _isCasting = false;
        private int _damagePayload = 0;
        private int _attackWindowTotalDamage = 0;
        private bool _attackWindowOpen = false;

        // 한 번의 공격(Enable~Disable 기간) 동안 중복 피격을 방지하기 위한 Set
        private HashSet<int> _hitTargets = new HashSet<int>();
        private int _ownerInstanceID = 0; // 자신을 타격하지 않도록 Owner ID 저장

        private void Awake()
        {
            _hitResults = new Collider[_maxTargets];
            if (_castCenter == null)
                _castCenter = this.transform;
        }

        private void OnValidate()
        {
            // 에디터에서 참조 누락 시에도 기즈모 중심이 사라지지 않도록 보정한다.
            if (_castCenter == null)
            {
                _castCenter = transform;
            }

            if (_maxTargets < 1)
            {
                _maxTargets = 1;
            }
        }

        /// <summary>
        /// 공격 판정을 시작합니다. (Animation Event에서 호출)
        /// </summary>
        /// <param name="damage">이번 공격의 데미지</param>
        public void EnableHitbox(int damage)
        {
            // 0 이하 데미지는 유효한 타격 윈도우로 취급하지 않는다.
            if (damage <= 0)
            {
                ResetCastingState();
                return;
            }

            _isCasting = true;
            _damagePayload = damage;
            _hitTargets.Clear();

            _attackWindowTotalDamage = 0;
            _attackWindowOpen = true;
        }

        /// <summary>
        /// 공격 판정을 종료합니다. (Animation Event에서 호출)
        /// </summary>
        public void DisableHitbox()
        {
            _isCasting = false;

            if (!_attackWindowOpen) return;

            bool isHit = _attackWindowTotalDamage > 0;
            OnAttackWindowResolved?.Invoke(isHit, _attackWindowTotalDamage);

            _attackWindowOpen = false;
            _attackWindowTotalDamage = 0;
        }

        /// <summary>
        /// 상태 전환/초기화 시 잔존 공격 판정을 강제로 정리한다.
        /// </summary>
        public void ForceDisableHitbox()
        {
            ResetCastingState();
        }

        public void SetOwner(GameObject owner)
        {
            if (owner != null)
                _ownerInstanceID = owner.GetInstanceID();
        }

        /// <summary>
        /// 공격 판정 반경을 외부에서 동기화할 때 사용한다.
        /// </summary>
        public void SetRadius(float radius)
        {
            _radius = Mathf.Max(0f, radius);
        }

        public void SetBossAttackHitType(BossAttackHitType hitType)
        {
            _bossAttackHitType = hitType;
        }

        private void FixedUpdate()
        {
            if (!_isCasting) return;
            if (_damagePayload <= 0)
            {
                ResetCastingState();
                return;
            }

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
                if (target == null)
                {
                    target = col.GetComponentInParent<IDamageable>();
                }

                if (target != null)
                {
                    // 중복 타격 방지 로직 개선:
                    // BossHitBox인 경우 Owner(보스 본체)의 ID를 추적, 일반 몬스터는 자신의 ID 추적.
                    int realTargetID = 0;

                    if (target is BossHitBox bossHitBox && bossHitBox.Owner != null)
                    {
                        realTargetID = bossHitBox.Owner.gameObject.GetInstanceID();
                    }
                    else if (target is MonoBehaviour targetMono)
                    {
                        realTargetID = targetMono.gameObject.GetInstanceID();
                    }
                    else
                    {
                        realTargetID = targetID;
                    }

                    if (_hitTargets.Contains(realTargetID)) continue;
                    // Owner(자신)인 경우 공격 판정 제외
                    if (_ownerInstanceID != 0 && realTargetID == _ownerInstanceID) continue;

                    bool handledByBossReceiver = false;
                    bool didDamage = false;
                    if (_bossAttackHitType != BossAttackHitType.Unknown)
                    {
                        IBossAttackHitReceiver bossHitReceiver = col.GetComponent<IBossAttackHitReceiver>();
                        if (bossHitReceiver == null)
                        {
                            bossHitReceiver = col.GetComponentInParent<IBossAttackHitReceiver>();
                        }

                        if (bossHitReceiver != null)
                        {
                            handledByBossReceiver = true;

                            Vector3 forceDirection = col.transform.position - _castCenter.position;
                            forceDirection.y = 0f;
                            if (forceDirection.sqrMagnitude <= 0.0001f)
                            {
                                forceDirection = transform.forward;
                            }

                            BossAttackHitResolution resolution = bossHitReceiver.ReceiveBossAttackHit(
                                new BossAttackHitData(_damagePayload, _bossAttackHitType, forceDirection));
                            didDamage = resolution == BossAttackHitResolution.Damaged;
                        }
                    }

                    if (!handledByBossReceiver)
                    {
                        target.TakeDamage(_damagePayload);
                        didDamage = true;
                    }

                    if (didDamage)
                    {
                        _attackWindowTotalDamage += _damagePayload;
                    }
                    _hitTargets.Add(realTargetID); // 실제 대상 ID 등록

                    // 디버그 로그 (필요시 주석 해제)
                    // Debug.Log($"⚔️ Hit: {col.name} -> RealTarget: {realTargetID}");
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

        private void OnDrawGizmosSelected()
        {
            if (!_showGizmos) return;

            // 선택 시에는 항상 선명한 색으로 그려 중심 확인을 쉽게 한다.
            Gizmos.color = _gizmoColor;
            Vector3 center = _castCenter != null ? _castCenter.position : transform.position;
            Gizmos.DrawWireSphere(center, _radius);
        }

        private void ResetCastingState()
        {
            _isCasting = false;
            _damagePayload = 0;
            _attackWindowOpen = false;
            _attackWindowTotalDamage = 0;
            _hitTargets.Clear();
        }
    }
}
