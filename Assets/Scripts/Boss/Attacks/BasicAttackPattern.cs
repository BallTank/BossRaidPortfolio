using UnityEngine;

namespace Core.Boss.Attacks
{
    /// <summary>
    /// 기본 근접 공격 패턴.
    /// 기존 BossAttackState의 로직을 이관한 것.
    /// </summary>
    public class BasicAttackPattern : IBossAttackPattern
    {
        private float _timer;

        public void Enter(BossController controller)
        {
            controller.StopMoving();

            // 타겟 방향으로 회전
            if (controller.Target != null)
            {
                controller.RotateTowards(controller.Target.position);
            }

            // 공격 애니메이션 재생
            controller.Visual?.PlayAttack();

            // DamageCaster 활성화
            controller.HeadDamageCaster?.EnableHitbox(controller.AttackDamage);

            _timer = controller.AttackDuration;
        }

        public bool Update(BossController controller)
        {
            _timer -= Time.deltaTime;
            return _timer <= 0; // true = 공격 종료
        }

        public void Exit(BossController controller)
        {
            // 히트박스 안전 정리
            controller.HeadDamageCaster?.DisableHitbox();
        }
    }
}
