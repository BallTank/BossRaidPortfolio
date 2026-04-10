using Core.Common;
using UnityEngine;

namespace Core.Boss.Attacks
{
    /// <summary>
    /// 기본 근접 공격 패턴.
    /// 기존 BossAttackState의 로직을 이관한 것.
    /// </summary>
    public class BasicAttackPattern : IBossAttackPattern
    {
        private const float FixedExitNormalizedTime = 1.0f;
        private const float MinAnimatorPlaybackSpeed = 0.01f;
        private const float MaxAnimatorPlaybackSpeed = 20f;
        private const float MinReadySliceLength = 0.0001f;
        private const float MinFallbackTotalDuration = 0.05f;

        private bool _basicAttackStateObserved;
        private bool _telegraphHidden;
        private float _fallbackElapsedTime;
        private float _fallbackDamageOpenTime;
        private float _fallbackExitTime;
        private float _telegraphElapsedTime;
        private bool _damageOpenRequested;
        private bool _hasPreviousProgressSample;
        private float _previousNormalizedProgress;

        public void Enter(BossController controller)
        {
            controller.StopMoving();

            // 타겟 방향으로 회전
            if (controller.Target != null)
            {
                controller.RotateTowards(controller.Target.position);
            }

            // 공격 애니메이션 재생
            controller.Visual?.ResetAnimatorPlaybackSpeed();
            controller.Visual?.PlayAttack();
            controller.HideBasicAttackTelegraph("Basic.Enter.PreClear", true);

            _basicAttackStateObserved = false;
            _telegraphHidden = false;
            _fallbackElapsedTime = 0f;
            _telegraphElapsedTime = 0f;
            _damageOpenRequested = false;
            _hasPreviousProgressSample = false;
            _previousNormalizedProgress = 0f;

            BossController.BasicAttackSettings settings = controller.BasicAttackConfig;
            _fallbackDamageOpenTime = settings != null ? Mathf.Max(0f, settings.readyDuration) : 0f;
            _fallbackExitTime = Mathf.Max(
                _fallbackDamageOpenTime,
                Mathf.Max(MinFallbackTotalDuration, controller.AttackDuration + _fallbackDamageOpenTime));
            controller.ShowBasicAttackTelegraph(_fallbackDamageOpenTime);
        }

        public bool Update(BossController controller)
        {
            _telegraphElapsedTime += Time.deltaTime;

            if (controller.Visual?.Animator == null)
            {
                return UpdateFallback(controller);
            }

            AnimatorStateInfo stateInfo = controller.Visual.Animator.GetCurrentAnimatorStateInfo(0);
            bool isBasicAttackState = stateInfo.IsName("Basic Attack");
            if (!isBasicAttackState)
            {
                if (_basicAttackStateObserved)
                {
                    RestorePlaybackSpeed(controller);
                    return true;
                }

                return false;
            }

            _basicAttackStateObserved = true;

            BossController.BasicAttackSettings settings = controller.BasicAttackConfig;
            float progress = stateInfo.normalizedTime;

            ApplyReadyPlaybackSpeed(controller, settings, progress);
            TryOpenDamagePhaseAtReadyWindowEnd(controller, settings, progress);
            HideTelegraphIfNeeded(controller, progress);
            _previousNormalizedProgress = progress;
            _hasPreviousProgressSample = true;

            if (progress >= FixedExitNormalizedTime)
            {
                RestorePlaybackSpeed(controller);
                return true;
            }

            return false;
        }

        public void Exit(BossController controller)
        {
            RestorePlaybackSpeed(controller);
            controller.HideBasicAttackTelegraph("Basic.Exit");
        }

        private bool UpdateFallback(BossController controller)
        {
            _fallbackElapsedTime += Time.deltaTime;
            float hideTime = _fallbackExitTime * ResolveTelegraphHideNormalizedTime(controller.BasicAttackConfig);
            float safeHideTime = Mathf.Max(_fallbackDamageOpenTime, hideTime);
            if (!_telegraphHidden && _fallbackElapsedTime >= safeHideTime)
            {
                controller.HideBasicAttackTelegraph("Basic.Fallback.HideTimeReached");
                _telegraphHidden = true;
            }

            if (_fallbackElapsedTime >= _fallbackExitTime)
            {
                return true;
            }

            return false;
        }

        private void ApplyReadyPlaybackSpeed(
            BossController controller,
            BossController.BasicAttackSettings settings,
            float progress)
        {
            if (controller.Visual == null) return;
            if (settings == null)
            {
                RestorePlaybackSpeed(controller);
                return;
            }

            float readyStart = settings.readyNormalizedWindow.x;
            float readyEnd = settings.readyNormalizedWindow.y;
            float readyDuration = settings.readyDuration;
            float readySliceLength = readyEnd - readyStart;

            if (readyDuration <= 0f || readySliceLength <= MinReadySliceLength)
            {
                RestorePlaybackSpeed(controller);
                return;
            }

            if (progress < readyStart || progress >= readyEnd)
            {
                RestorePlaybackSpeed(controller);
                return;
            }

            float clipLength = controller.Visual.GetBasicAttackClipLengthOrDefault(controller.AttackDuration);
            float baseReadyDuration = readySliceLength * Mathf.Max(MinFallbackTotalDuration, clipLength);
            float targetPlaybackSpeed = Mathf.Clamp(
                baseReadyDuration / readyDuration,
                MinAnimatorPlaybackSpeed,
                MaxAnimatorPlaybackSpeed);

            controller.Visual.SetAnimatorPlaybackSpeed(targetPlaybackSpeed);
        }

        private void HideTelegraphIfNeeded(BossController controller, float progress)
        {
            if (_telegraphHidden)
            {
                return;
            }

            if (_telegraphElapsedTime < _fallbackDamageOpenTime)
            {
                return;
            }

            if (progress < ResolveTelegraphHideNormalizedTime(controller.BasicAttackConfig))
            {
                return;
            }

            controller.HideBasicAttackTelegraph("Basic.Update.HideTelegraphIfNeeded");
            _telegraphHidden = true;
        }

        private void TryOpenDamagePhaseAtReadyWindowEnd(
            BossController controller,
            BossController.BasicAttackSettings settings,
            float progress)
        {
            if (_damageOpenRequested || settings == null)
            {
                return;
            }

            float readyWindowEnd = Mathf.Clamp01(settings.readyNormalizedWindow.y);
            bool crossedReadyWindowEnd = !_hasPreviousProgressSample
                ? progress >= readyWindowEnd
                : (_previousNormalizedProgress < readyWindowEnd && progress >= readyWindowEnd);

            if (!crossedReadyWindowEnd)
            {
                return;
            }

            bool entered = controller.TryEnterBasicAttackTelegraphActiveNow(
                $"Basic.ReadyWindowEnd progress={progress:F3} end={readyWindowEnd:F3}");
            HitTraceLogger.Log(
                $"[HitTrace][BOOT][BasicAttackPattern][ReadyWindowEndOpen] progress={progress:F3} end={readyWindowEnd:F3} " +
                $"result={(entered ? "PASS" : "SKIP")}");
            _damageOpenRequested = true;
        }

        private static float ResolveTelegraphHideNormalizedTime(BossController.BasicAttackSettings settings)
        {
            if (settings == null)
            {
                return BossController.BasicAttackSettings.DefaultTelegraphHideNormalizedTime;
            }

            return Mathf.Clamp01(settings.telegraphHideNormalizedTime);
        }

        private static void RestorePlaybackSpeed(BossController controller)
        {
            controller.Visual?.ResetAnimatorPlaybackSpeed();
        }
    }
}
