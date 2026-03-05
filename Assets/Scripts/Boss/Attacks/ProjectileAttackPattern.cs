using Core.Boss.Projectiles;
using Core.Combat;
using UnityEngine;

namespace Core.Boss.Attacks
{
    /// <summary>
    /// 보스 투사체 공격 패턴.
    /// telegraph -> 3연발(좌/중앙/우) -> 종료 순서로 동작한다.
    /// </summary>
    public class ProjectileAttackPattern : IBossAttackPattern
    {
        private const string AnimFlameAttack = "Flame Attack";
        private const string AnimFireballShoot = "Fireball Shoot";
        private const string AnimBasicAttack = "Basic Attack";

        private readonly BossController.ProjectileAttackSettings _settings;

        private float _telegraphTimer;
        private float _volleyTimer;
        private float _postFireRecoveryTimer;
        private int _shotsFired;
        private bool _isFiringPhase;

        public ProjectileAttackPattern(BossController.ProjectileAttackSettings settings)
        {
            _settings = settings;
        }

        public void Enter(BossController controller)
        {
            controller.StopMoving();

            if (controller.Target != null)
            {
                controller.RotateTowards(controller.Target.position);
            }

            controller.Visual?.PlayProjectileAttack();

            _telegraphTimer = _settings.telegraphDuration;
            _volleyTimer = 0f;
            _postFireRecoveryTimer = Mathf.Max(0f, _settings.postFireRecoveryDuration);
            _shotsFired = 0;
            _isFiringPhase = false;
        }

        public bool Update(BossController controller)
        {
            // 1) 예고(telegraph) 구간
            if (!_isFiringPhase)
            {
                _telegraphTimer -= Time.deltaTime;
                if (_telegraphTimer > 0f) return false;
                _isFiringPhase = true;
                _volleyTimer = 0f;
            }

            // 2) 발사 간격에 맞춰 연속 발사
            if (_shotsFired < _settings.volleyCount)
            {
                _volleyTimer -= Time.deltaTime;
                if (_volleyTimer <= 0f)
                {
                    FireShot(controller, _shotsFired);
                    _shotsFired++;
                    _volleyTimer = _settings.volleyInterval;
                }

                return false;
            }

            // 3) 발사 완료 후 애니메이션 마무리 시점까지 대기
            return IsRecoveryComplete(controller);
        }

        public void Exit(BossController controller)
        {
            // 투사체는 독립 수명으로 동작하므로 상태 종료 시 별도 정리 없음
        }

        private void FireShot(BossController controller, int shotIndex)
        {
            if (controller.ProjectilePool == null) return;

            BossProjectile projectile = controller.ProjectilePool.TryGetProjectile();
            if (projectile == null) return;

            Vector3 origin = controller.ProjectileSpawnPoint != null
                ? controller.ProjectileSpawnPoint.position
                : controller.transform.position + Vector3.up * 1.2f;

            Vector3 baseDirection;
            if (controller.Target != null)
            {
                Vector3 toTarget = controller.Target.position - origin;
                toTarget.y = 0f;
                baseDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : controller.transform.forward;
            }
            else
            {
                baseDirection = controller.transform.forward;
            }

            float spreadAngle = GetSpreadAngle(shotIndex);
            Quaternion spreadRot = Quaternion.AngleAxis(spreadAngle, Vector3.up);
            Vector3 shotDirection = spreadRot * baseDirection;

            projectile.gameObject.SetActive(true);
            projectile.Initialize(
                origin,
                shotDirection,
                _settings.speed,
                _settings.damage,
                _settings.lifetime,
                controller.gameObject.GetInstanceID(),
                controller.Target,
                _settings.homingStrength,
                _settings.homingDuration,
                _settings.verticalFollowSpeed,
                BossAttackHitType.Attack3Projectile);
        }

        private float GetSpreadAngle(int shotIndex)
        {
            // 계획 고정: 3발 기준 -8, 0, +8
            if (shotIndex == 0) return -8f;
            if (shotIndex == 1) return 0f;
            if (shotIndex == 2) return 8f;
            return 0f;
        }

        private bool IsRecoveryComplete(BossController controller)
        {
            // 최소 대기 시간 보장(발사 직후 즉시 복귀 방지)
            if (_postFireRecoveryTimer > 0f)
            {
                _postFireRecoveryTimer -= Time.deltaTime;
                if (_postFireRecoveryTimer > 0f) return false;
            }

            Animator animator = controller.Visual?.Animator;
            if (animator == null) return true;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool isProjectileAnim =
                stateInfo.IsName(AnimFlameAttack) ||
                stateInfo.IsName(AnimFireballShoot) ||
                stateInfo.IsName(AnimBasicAttack);

            // 이미 다른 상태로 전환된 경우에는 복귀를 허용한다.
            if (!isProjectileAnim) return true;

            return stateInfo.normalizedTime >= _settings.exitNormalizedTime;
        }
    }
}
