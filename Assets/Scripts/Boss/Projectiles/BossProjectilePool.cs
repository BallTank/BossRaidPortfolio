using System.Collections.Generic;
using UnityEngine;

namespace Core.Boss.Projectiles
{
    /// <summary>
    /// 보스 투사체 오브젝트 풀.
    /// 런타임 Instantiate/Destroy를 피하고 비활성화 재사용으로 Zero-GC를 유지한다.
    /// </summary>
    public class BossProjectilePool : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private BossProjectile projectilePrefab;
        [SerializeField] private Transform poolRoot;
        [SerializeField] private int prewarmCount = 12;
        [SerializeField] private int maxCount = 24;
        [SerializeField] private bool expand = true;

        private readonly Queue<BossProjectile> _available = new Queue<BossProjectile>();
        private readonly List<BossProjectile> _allProjectiles = new List<BossProjectile>();

        private void Awake()
        {
            if (poolRoot == null)
            {
                poolRoot = transform;
            }

            if (projectilePrefab == null)
            {
                Debug.LogError("BossProjectilePool: projectilePrefab is not assigned.");
                return;
            }

            int count = Mathf.Clamp(prewarmCount, 0, maxCount);
            for (int i = 0; i < count; i++)
            {
                CreateAndStoreProjectile();
            }
        }

        public BossProjectile TryGetProjectile()
        {
            // 1) 대기 큐에 있으면 즉시 대여
            if (_available.Count > 0)
            {
                return _available.Dequeue();
            }

            // 2) 고갈 + 확장 불가면 이번 샷은 스킵
            if (!expand || _allProjectiles.Count >= maxCount)
            {
                Debug.LogWarning("BossProjectilePool: Pool exhausted. Shot skipped.");
                return null;
            }

            // 3) 확장 가능하면 1개 생성 후 즉시 대여
            return CreateAndStoreProjectile(dequeueAfterCreate: true);
        }

        public void ReturnProjectile(BossProjectile projectile)
        {
            if (projectile == null) return;

            projectile.gameObject.SetActive(false);
            projectile.transform.SetParent(poolRoot);
            _available.Enqueue(projectile);
        }

        private BossProjectile CreateAndStoreProjectile(bool dequeueAfterCreate = false)
        {
            BossProjectile instance = Instantiate(projectilePrefab, poolRoot);
            instance.gameObject.SetActive(false);
            instance.SetPool(this);

            _allProjectiles.Add(instance);
            _available.Enqueue(instance);

            return dequeueAfterCreate ? _available.Dequeue() : instance;
        }
    }
}
