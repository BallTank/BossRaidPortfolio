using Core.Boss.Projectiles;
using Core.Combat;
using Core.Audio;
using UnityEngine;

namespace Core.Boss.Attacks
{
    /// <summary>
    /// ���� ����ü ���� ����.
    /// warning -> 3����(��/�߾�/��) -> ���� ������ �����Ѵ�.
    /// </summary>
    public class ProjectileAttackPattern : IBossAttackPattern
    {
        private const string AnimFlameAttack = "Flame Attack";
        private const string AnimFireballShoot = "Fireball Shoot";
        private const string AnimBasicAttack = "Basic Attack";

        private readonly BossController.ProjectileAttackSettings _settings;

        private float _warningTimer;
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
            SoundController.Instance?.Play(SoundId.DragonAttack3);

            _warningTimer = _settings.warningDuration;
            _volleyTimer = 0f;
            _postFireRecoveryTimer = Mathf.Max(0f, _settings.postFireRecoveryDuration);
            _shotsFired = 0;
            _isFiringPhase = false;
        }

        public bool Update(BossController controller)
        {
            // 1) ���(warning) ����
            if (!_isFiringPhase)
            {
                _warningTimer -= Time.deltaTime;
                if (_warningTimer > 0f) return false;
                _isFiringPhase = true;
                _volleyTimer = 0f;
            }

            // 2) �߻� ���ݿ� ���� ���� �߻�
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

            // 3) �߻� �Ϸ� �� �ִϸ��̼� ������ �������� ���
            return IsRecoveryComplete(controller);
        }

        public void Exit(BossController controller)
        {
            // ����ü�� ���� �������� �����ϹǷ� ���� ���� �� ���� ���� ����
        }

        /// <summary>
        /// Remote client ȭ�鿡�� ���� 3 ����ü�� ǥ�� �������� ����Ѵ�.
        /// </summary>
        public void PlayReplicatedDisplayShot(
            BossController controller,
            Vector3 origin,
            Vector3 direction,
            float speed,
            float lifetime,
            Transform target,
            float homingStrength,
            float homingDuration,
            float verticalFollowSpeed)
        {
            if (controller.ProjectilePool == null) return;

            BossProjectile projectile = controller.ProjectilePool.TryGetProjectile();
            if (projectile == null) return;

            projectile.gameObject.SetActive(true);
            projectile.InitializeDisplayOnly(
                origin,
                direction,
                speed,
                lifetime,
                target,
                homingStrength,
                homingDuration,
                verticalFollowSpeed);
        }

        private void FireShot(BossController controller, int shotIndex)
        {
            if (shotIndex == 0)
            {
                SoundController.Instance?.Play(SoundId.DragonBreath);
            }
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

            controller.EnqueueReplicatedProjectileShot(
                origin,
                shotDirection,
                _settings.speed,
                _settings.lifetime,
                controller.Target,
                _settings.homingStrength,
                _settings.homingDuration,
                _settings.verticalFollowSpeed);
        }

        private float GetSpreadAngle(int shotIndex)
        {
            // ��ȹ ����: 3�� ���� -8, 0, +8
            if (shotIndex == 0) return -8f;
            if (shotIndex == 1) return 0f;
            if (shotIndex == 2) return 8f;
            return 0f;
        }

        private bool IsRecoveryComplete(BossController controller)
        {
            // �ּ� ��� �ð� ����(�߻� ���� ��� ���� ����)
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

            // �̹� �ٸ� ���·� ��ȯ�� ��쿡�� ���͸� ����Ѵ�.
            if (!isProjectileAnim) return true;

            return stateInfo.normalizedTime >= _settings.exitNormalizedTime;
        }
    }
}

