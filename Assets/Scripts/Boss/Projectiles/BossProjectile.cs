using Core.Interfaces;
using UnityEngine;
using UnityEngine.VFX;

namespace Core.Boss.Projectiles
{
    /// <summary>
    /// 보스 투사체 단일 인스턴스.
    /// 활성화 시 초기화 -> 전진 이동 -> 충돌/수명 만료 시 풀로 반환.
    /// </summary>
    public class BossProjectile : MonoBehaviour
    {
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private VisualEffect visualEffect;
        [SerializeField] private float hitReturnDelay = 0.35f;

        private BossProjectilePool _pool;
        private int _damage;
        private float _speed;
        private float _lifetime;
        private int _ownerInstanceID;
        private Transform _target;
        private float _homingStrength;
        private float _homingTimer;
        private float _verticalFollowSpeed;
        private Vector3 _moveDirection;
        private Collider _projectileCollider;
        private bool _isActive;
        private bool _isReturned;
        private float _pendingReturnTimer = -1f;

        private void Awake()
        {
            if (visualEffect == null)
            {
                visualEffect = GetComponentInChildren<VisualEffect>(true);
            }

            _projectileCollider = GetComponent<Collider>();
        }

        public void SetPool(BossProjectilePool pool)
        {
            _pool = pool;
        }

        public void Initialize(
            Vector3 position,
            Vector3 direction,
            float speed,
            int damage,
            float lifetime,
            int ownerInstanceID,
            Transform target,
            float homingStrength,
            float homingDuration,
            float verticalFollowSpeed)
        {
            // 발사 시점 데이터 주입
            direction.y = 0f;
            _moveDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : transform.forward;
            _moveDirection.y = 0f;
            if (_moveDirection.sqrMagnitude <= 0.0001f)
            {
                _moveDirection = Vector3.forward;
            }

            transform.position = position;
            transform.forward = _moveDirection.normalized;

            _speed = speed;
            _damage = damage;
            _lifetime = lifetime;
            _ownerInstanceID = ownerInstanceID;
            _target = target;
            _homingStrength = Mathf.Clamp01(homingStrength);
            _homingTimer = Mathf.Max(0f, homingDuration);
            _verticalFollowSpeed = Mathf.Max(0f, verticalFollowSpeed);
            _pendingReturnTimer = -1f;
            _isReturned = false;
            _isActive = true;

            if (_projectileCollider != null)
            {
                _projectileCollider.enabled = true;
            }

            RestartVfx();
        }

        private void OnEnable()
        {
            _pendingReturnTimer = -1f;
            _isReturned = false;
            _isActive = true;
        }

        private void Update()
        {
            if (_pendingReturnTimer >= 0f)
            {
                _pendingReturnTimer -= Time.deltaTime;
                if (_pendingReturnTimer <= 0f)
                {
                    ReturnToPool();
                }
                return;
            }

            if (!_isActive) return;

            ApplyHoming();

            // 프레임마다 전방 이동
            transform.position += _moveDirection * (_speed * Time.deltaTime);
            ApplyVerticalFollow();
            transform.forward = _moveDirection;

            // 수명 종료 시 즉시 반환
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f)
            {
                ReturnToPool();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryProcessHit(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null) return;
            TryProcessHit(collision.collider);
        }

        private void ApplyHoming()
        {
            if (_target == null || _homingTimer <= 0f || _homingStrength <= 0f) return;

            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                _homingTimer -= Time.deltaTime;
                return;
            }

            // 0~1 강도를 0~720도/초 회전 성능으로 매핑
            float turnSpeedRad = Mathf.Lerp(0f, Mathf.PI * 4f, _homingStrength);
            Vector3 newDir = Vector3.RotateTowards(_moveDirection, toTarget.normalized, turnSpeedRad * Time.deltaTime, 0f);
            if (newDir.sqrMagnitude > 0.0001f)
            {
                _moveDirection = newDir.normalized;
                transform.forward = _moveDirection;
            }

            _homingTimer -= Time.deltaTime;
        }

        private void ApplyVerticalFollow()
        {
            if (_target == null || _verticalFollowSpeed <= 0f) return;

            Vector3 currentPos = transform.position;
            currentPos.y = Mathf.MoveTowards(currentPos.y, _target.position.y, _verticalFollowSpeed * Time.deltaTime);
            transform.position = currentPos;
        }

        private void TryProcessHit(Collider other)
        {
            if (!_isActive || other == null) return;
            if (!IsLayerAllowed(other)) return;

            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = other.GetComponentInParent<IDamageable>();
            }

            if (damageable == null) return;

            int targetInstanceID = ExtractTargetInstanceID(damageable, other);
            if (_ownerInstanceID != 0 && targetInstanceID == _ownerInstanceID) return;

            damageable.TakeDamage(_damage);
            EnterHitPhase();
        }

        private bool IsLayerAllowed(Collider other)
        {
            int colliderLayerBit = 1 << other.gameObject.layer;
            if ((hitMask.value & colliderLayerBit) != 0) return true;

            Transform root = other.transform.root;
            if (root == null) return false;

            int rootLayerBit = 1 << root.gameObject.layer;
            return (hitMask.value & rootLayerBit) != 0;
        }

        private static int ExtractTargetInstanceID(IDamageable damageable, Collider hitCollider)
        {
            if (damageable is MonoBehaviour mono)
            {
                return mono.gameObject.GetInstanceID();
            }

            if (hitCollider != null && hitCollider.transform.root != null)
            {
                return hitCollider.transform.root.gameObject.GetInstanceID();
            }

            return 0;
        }

        private void ReturnToPool()
        {
            if (_isReturned) return;

            _isReturned = true;
            _isActive = false;
            _pendingReturnTimer = -1f;

            if (visualEffect != null)
            {
                visualEffect.Stop();
            }

            _pool?.ReturnProjectile(this);
        }

        private void RestartVfx()
        {
            if (visualEffect == null) return;

            visualEffect.Reinit();
            visualEffect.SendEvent("create");
            visualEffect.Play();
        }

        private void EnterHitPhase()
        {
            if (_isReturned) return;

            _isActive = false;

            if (_projectileCollider != null)
            {
                _projectileCollider.enabled = false;
            }

            if (visualEffect != null)
            {
                visualEffect.SendEvent("hit");
            }

            _pendingReturnTimer = Mathf.Max(0f, hitReturnDelay);
            if (_pendingReturnTimer <= 0f)
            {
                ReturnToPool();
            }
        }
    }
}
