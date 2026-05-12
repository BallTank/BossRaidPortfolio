using UnityEngine;
using Core.Audio;

namespace Core.Boss.Attacks
{
    /// <summary>
    /// ���� ���� ���� ���� (Lunge).
    /// normalizedTime ������� �ִϸ��̼� ���Ḧ �Ǵ��Ͽ�,
    /// Ŭ�� ���̰� ����Ǿ �ڵ����� �����մϴ�.
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
        private bool _damageWindowCloseApplied;
        private int _damagePayload;
        private float _warningDuration;
        private float _activeDuration;
        private float _telegraphElapsedTime;

        public LungeAttackPattern(BossController.LungeAttackSettings settings)
        {
            _settings = settings;
        }

        public void Enter(BossController controller)
        {
            controller.StopMoving();
            SoundController.Instance?.Play(SoundId.DragonAttack2);
            _damageWindowActive = false;
            _travelWindowActive = false;
            _lungeStateObserved = false;
            _telegraphStarted = false;
            _damageWindowCloseApplied = false;
            _damagePayload = Mathf.RoundToInt(controller.AttackDamage * _settings.damageMultiplier);
            _warningDuration = ResolveWarningDuration(controller);
            _activeDuration = ResolveActiveDuration(controller);
            _telegraphElapsedTime = 0f;

            // Ÿ�� �������� ��� ȸ��
            if (controller.Target != null)
            {
                controller.RotateTowardsImmediate(controller.Target.position);
            }

            controller.BeginLungeTravelDirectionLockFromCurrentForward();
            controller.StopConfiguredLungeTravel();
            controller.Visual?.SetLungeRootMotionEnabled(false);
            controller.Visual?.ResetAnimatorPlaybackSpeed();
            controller.HideLungeAttackTelegraph("Lunge.Enter.PreClear", true);

            // 첫 진입에서도 준비 구간이 잘리지 않게 즉시 0프레임에서 시작한다.
            controller.Visual?.PlayLungeAttackImmediate();
        }

        /// <summary>
        /// �� ������ ȣ��. normalizedTime���� �ִϸ��̼� ������� �����Ͽ�
        /// ���� ������ �Ǵ��մϴ�.
        /// </summary>
        /// <returns>true: ���� ���� -> CombatState�� ����</returns>
        public bool Update(BossController controller)
        {
            // Visual �Ǵ� Animator�� ������ ��� ���� (���� ��ġ)
            if (controller.Visual?.Animator == null) return true;

            // ���� Animator Layer 0�� ���� ���� ��ȸ
            AnimatorStateInfo stateInfo = controller.Visual.Animator.GetCurrentAnimatorStateInfo(0);

            // CrossFade ���̰ų� Ÿ�� ���� ���� ���̸� ���
            // ���Ž� Animator�� ���� "Claw Attack" ���¸�� ����Ѵ�.
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

            if (_telegraphStarted && !_damageWindowCloseApplied)
            {
                _telegraphElapsedTime += Time.deltaTime;
            }

            float progress = stateInfo.normalizedTime;
            float hitStart = _settings.damageCastNormalizedWindow.x;
            float hitEnd = _settings.damageCastNormalizedWindow.y;
            float damageTrailEnd = ResolveDamageTrailEnd(hitStart, hitEnd);

            if (!_damageWindowCloseApplied &&
                !_damageWindowActive &&
                progress >= hitStart &&
                progress < hitEnd &&
                _telegraphElapsedTime >= _warningDuration)
            {
                OpenDamageWindow(controller);
            }

            if (_travelWindowActive && progress < hitEnd)
            {
                controller.UpdateConfiguredLungeTravel();
            }

            if (!_damageWindowCloseApplied && _damageWindowActive && progress >= damageTrailEnd)
            {
                CloseDamageWindow(controller);
            }

            if (_travelWindowActive && progress >= hitEnd)
            {
                CloseTravelWindow(controller);
            }

            if (progress >= FixedExitPhaseRatio)
            {
                CloseDamageWindow(controller, true);
                CloseTravelWindow(controller);
                return true;
            }

            // normalizedTime: 0.0(����) ~ 1.0(��). ���� Ŭ���� 1.0 �ʰ� ����.
            // �ִϸ��̼� ���� ������ Ŭ�� ��(1.0) �������� �����Ѵ�.
            return false;
        }

        private void OpenDamageWindow(BossController controller)
        {
            if (_damageWindowActive) return;

            controller.TryEnterLungeAttackTelegraphActiveNow(
                $"Lunge.OpenDamageWindow elapsed={_telegraphElapsedTime:F3} warning={_warningDuration:F3}");
            _damageWindowActive = true;
            if (!_travelWindowActive)
            {
                _travelWindowActive = true;
                controller.BeginConfiguredLungeTravel(_activeDuration);
            }
        }

        private void CloseDamageWindow(BossController controller, bool forceImmediate = false)
        {
            if (_damageWindowCloseApplied)
            {
                return;
            }

            bool didClose = controller.HideLungeAttackTelegraph("Lunge.CloseDamageWindow", forceImmediate);
            if (!didClose)
            {
                return;
            }

            _damageWindowCloseApplied = true;
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
            _telegraphElapsedTime = 0f;
        }

        public void Exit(BossController controller)
        {
            // ���� ���� �� �̵� ���� (��� �� ���� ��ȯ �ÿ��� �����ϰ� ����)
            controller.EndLungeTravelDirectionLock();
            controller.Visual?.SetLungeRootMotionEnabled(false);
            CloseDamageWindow(controller, true);
            CloseTravelWindow(controller);
            controller.StopMoving();
            _telegraphStarted = false;
            _telegraphElapsedTime = 0f;
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

