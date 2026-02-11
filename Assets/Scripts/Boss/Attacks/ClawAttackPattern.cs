using UnityEngine;

namespace Core.Boss.Attacks
{
    /// <summary>
    /// 돌진하며 할퀴는 공격 패턴 (Rush & Claw).
    /// </summary>
    public class ClawAttackPattern : IBossAttackPattern
    {
        private float _timer;
        private readonly BossController.ClawAttackSettings _settings;

        public ClawAttackPattern(BossController.ClawAttackSettings settings)
        {
            _settings = settings;
        }

        public void Enter(BossController controller)
        {
            controller.StopMoving();

            // 타겟 방향 회전
            if (controller.Target != null)
            {
                controller.RotateTowards(controller.Target.position);
            }

            // 애니메이션 재생
            controller.Visual?.PlayClawAttack();

            // DamageCaster 활성화 (공격력은 1.5배 설정 예시)
            int damage = (int)(controller.AttackDamage * _settings.damageMultiplier);
            controller.DamageCaster?.EnableHitbox(damage);

            _timer = controller.AttackDuration; // 전체 지속 시간
        }

        public bool Update(BossController controller)
        {
            _timer -= Time.deltaTime;

            // 돌진 구간 (초반 일정 시간 동안)
            if (_timer > (controller.AttackDuration - _settings.rushDuration))
            {
                // 앞쪽으로 돌진
                controller.MoveTo(controller.transform.position + controller.transform.forward, _settings.rushSpeed);
            }
            else
            {
                // 돌진 후 정지
                controller.StopMoving();
            }

            return _timer <= 0;
        }

        public void Exit(BossController controller)
        {
            controller.DamageCaster?.DisableHitbox();
            controller.StopMoving();
        }
    }
}
