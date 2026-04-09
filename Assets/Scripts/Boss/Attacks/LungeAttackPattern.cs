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
        private const float FixedExitPhaseRatio = 1.0f;
        private const float DamageTrailActiveRatio = 0.2f;
        private bool _damageWindowActive;
        private bool _travelWindowActive;
        private bool _lungeStateObserved;
        private bool _telegraphStarted;
        private int _damagePayload;
        private float _warningDuration;
        private float _activeDuration;

        public LungeAttackPattern(BossController.LungeAttackSettings settings)
        {
            _settings = settings;
        }

        public void Enter(BossController controller)
        {
            controller.StopMoving();
            _damageWindowActive = false;
            _travelWindowActive = false;
            _lungeStateObserved = false;
            _telegraphStarted = false;
            _damagePayload = Mathf.RoundToInt(controller.AttackDamage * _settings.damageMultiplier);
            _warningDuration = ResolveWarningDuration(controller);
            _activeDuration = ResolveActiveDuration(controller);

            // 타겟 방향으로 즉시 회전
            if (controller.Target != null)
            {
                controller.RotateTowardsImmediate(controller.Target.position);
            }

            controller.BeginLungeTravelDirectionLockFromCurrentForward();
            controller.StopConfiguredLungeTravel();
            controller.Visual?.SetLungeRootMotionEnabled(false);
            controller.HideLungeAttackTelegraph();

            // Lunge Attack 애니메이션 재생
            controller.Visual?.PlayLungeAttack();
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
                if (_lungeStateObserved)
                {
                    CloseDamageWindow(controller);
                    CloseTravelWindow(controller);
                    return true;
                }

                return false;
            }

            if (!_lungeStateObserved)
            {
                _lungeStateObserved = true;
                StartTelegraph(controller);
            }

            float progress = stateInfo.normalizedTime;
            float hitStart = _settings.damageCastNormalizedWindow.x;
            float hitEnd = _settings.damageCastNormalizedWindow.y;
            float damageTrailEnd = ResolveDamageTrailEnd(hitStart, hitEnd);

            if (!_damageWindowActive && progress >= hitStart && progress < hitEnd)
            {
                OpenDamageWindow(controller);
            }

            if (_travelWindowActive && progress < hitEnd)
            {
                controller.UpdateConfiguredLungeTravel();
            }

            if (_damageWindowActive && progress >= damageTrailEnd)
            {
                CloseDamageWindow(controller);
            }

            if (_travelWindowActive && progress >= hitEnd)
            {
                CloseTravelWindow(controller);
            }

            if (progress >= FixedExitPhaseRatio)
            {
                CloseDamageWindow(controller);
                CloseTravelWindow(controller);
                return true;
            }

            // normalizedTime: 0.0(시작) ~ 1.0(끝). 루프 클립은 1.0 초과 가능.
            // 애니메이션 종료 판정은 클립 끝(1.0) 기준으로 수행한다.
            return false;
        }

        private void OpenDamageWindow(BossController controller)
        {
            if (_damageWindowActive) return;

            _damageWindowActive = true;
            if (!_travelWindowActive)
            {
                _travelWindowActive = true;
                controller.BeginConfiguredLungeTravel(_activeDuration);
            }
        }

        private void CloseDamageWindow(BossController controller)
        {
            controller.HideLungeAttackTelegraph();
            _damageWindowActive = false;
        }

        private void CloseTravelWindow(BossController controller)
        {
            controller.StopConfiguredLungeTravel();
            _travelWindowActive = false;
        }

        private void StartTelegraph(BossController controller)
        {
            if (_telegraphStarted) return;

            controller.ShowLungeAttackTelegraph(_warningDuration, _activeDuration, _damagePayload);
            _telegraphStarted = true;
        }

        public void Exit(BossController controller)
        {
            // 판정 종료 및 이동 정지 (사망 등 강제 전환 시에도 안전하게 정리)
            controller.EndLungeTravelDirectionLock();
            controller.Visual?.SetLungeRootMotionEnabled(false);
            CloseDamageWindow(controller);
            CloseTravelWindow(controller);
            controller.StopMoving();
            _telegraphStarted = false;
        }

        private static float ResolveDamageTrailEnd(float hitStart, float hitEnd)
        {
            float normalizedWindow = Mathf.Max(0f, hitEnd - hitStart);
            return hitStart + (normalizedWindow * DamageTrailActiveRatio);
        }

        private float ResolveWarningDuration(BossController controller)
        {
            return ResolveClipLength(controller) * Mathf.Clamp01(_settings.damageCastNormalizedWindow.x);
        }

        private float ResolveActiveDuration(BossController controller)
        {
            float normalizedWindow = Mathf.Clamp01(_settings.damageCastNormalizedWindow.y)
                - Mathf.Clamp01(_settings.damageCastNormalizedWindow.x);
            return ResolveClipLength(controller) * Mathf.Max(0f, normalizedWindow);
        }

        private static float ResolveClipLength(BossController controller)
        {
            if (controller.Visual == null)
            {
                return Mathf.Max(0.05f, controller.AttackDuration);
            }

            return controller.Visual.GetLungeAttackClipLengthOrDefault(controller.AttackDuration);
        }
    }
}
