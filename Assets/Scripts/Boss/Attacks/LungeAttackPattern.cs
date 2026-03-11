using UnityEngine;

namespace Core.Boss.Attacks
{
    /// <summary>
    /// 도약 돌진 공격 패턴 (Lunge).
    /// normalizedTime 기반으로 애니메이션 종료를 판단하여,
    /// 클립 길이가 변경되어도 자동으로 적응합니다.
    /// </summary>
    public class LungeAttackPattern : IBossAttackPattern
    {
        private readonly BossController.LungeAttackSettings _settings;
        private const float FixedHitboxOffPhaseRatio = 0.8f;
        private const float FixedExitPhaseRatio = 1.0f;
        private Vector3 _lungeStartPosition;
        private bool _hitboxDisabled;
        private bool _lungeStateObserved;

        public LungeAttackPattern(BossController.LungeAttackSettings settings)
        {
            _settings = settings;
        }

        public void Enter(BossController controller)
        {
            controller.StopMoving();
            controller.ResetLungeRootMotionDebugLogWindow();
            _lungeStartPosition = controller.transform.position;
            _hitboxDisabled = false;
            _lungeStateObserved = false;

            // 타겟 방향으로 즉시 회전
            if (controller.Target != null)
            {
                controller.RotateTowardsImmediate(controller.Target.position);
                controller.BeginLungeTravelDirectionLock(controller.Target.position);
            }
            else
            {
                controller.BeginLungeTravelDirectionLock(controller.transform.position + controller.transform.forward);
            }

            // 도약 애니메이션의 루트 모션을 보스 루트 이동으로 전달
            controller.Visual?.SetLungeRootMotionEnabled(true);

            // Lunge Attack 애니메이션 재생
            controller.Visual?.PlayLungeAttack();

            if (controller.EnableLungeRootMotionDebugLog)
            {
                Vector3 pos = controller.transform.position;
                Debug.Log($"[LungeDebug][Enter] pos=({pos.x:F3},{pos.y:F3},{pos.z:F3})");
            }

            // DamageCaster 활성화 (기본 공격력 × damageMultiplier)
            int damage = (int)(controller.AttackDamage * _settings.damageMultiplier);
            controller.LungeDamageCaster?.EnableHitbox(damage);
        }

        /// <summary>
        /// 매 프레임 호출. normalizedTime으로 애니메이션 진행률을 추적하여
        /// 종료 시점을 판단합니다.
        /// </summary>
        /// <returns>true: 공격 종료 -> CombatState로 복귀</returns>
        public bool Update(BossController controller)
        {
            // Visual 또는 Animator가 없으면 즉시 종료 (안전 장치)
            if (controller.Visual?.Animator == null) return true;

            // 현재 Animator Layer 0의 상태 정보 조회
            AnimatorStateInfo stateInfo = controller.Visual.Animator.GetCurrentAnimatorStateInfo(0);

            // CrossFade 중이거나 타겟 상태 진입 전이면 대기
            // 레거시 Animator를 위해 "Claw Attack" 상태명도 허용한다.
            bool isLungeState = stateInfo.IsName("Lunge Attack") || stateInfo.IsName("Claw Attack");
            if (!isLungeState)
            {
                // 이미 도약 상태를 거쳤고 판정 종료가 끝났다면,
                // 애니메이터가 다음 상태로 넘어간 프레임에서도 안전하게 공격을 종료한다.
                return _lungeStateObserved && _hitboxDisabled;
            }

            _lungeStateObserved = true;
            float progress = stateInfo.normalizedTime;

            // 판정 종료 시점(0.8)과 상태 종료 시점(클립 끝)을 분리한다.
            if (!_hitboxDisabled && progress >= FixedHitboxOffPhaseRatio)
            {
                controller.LungeDamageCaster?.DisableHitbox();
                _hitboxDisabled = true;
            }

            // normalizedTime: 0.0(시작) ~ 1.0(끝). 루프 클립은 1.0 초과 가능.
            // 애니메이션 종료 판정은 클립 끝(1.0) 기준으로 수행한다.
            return progress >= FixedExitPhaseRatio;
        }

        public void Exit(BossController controller)
        {
            // 판정 종료 및 이동 정지 (사망 등 강제 전환 시에도 안전하게 정리)
            controller.EndLungeTravelDirectionLock();
            controller.Visual?.SetLungeRootMotionEnabled(false);
            controller.LungeDamageCaster?.DisableHitbox();
            _hitboxDisabled = true;
            controller.StopMoving();

            if (controller.EnableLungeRootMotionDebugLog)
            {
                Vector3 endPos = controller.transform.position;
                Vector3 delta = endPos - _lungeStartPosition;
                Debug.Log(
                    $"[LungeDebug][Exit] pos=({endPos.x:F3},{endPos.y:F3},{endPos.z:F3}) " +
                    $"deltaXZ=({delta.x:F3},{delta.z:F3})");
            }
        }
    }
}
