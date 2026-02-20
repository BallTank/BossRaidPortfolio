using Core.Common;
using UnityEngine;

namespace Core.Boss
{
    public class BossVisual : BaseVisual
    {
        [Header("Visual Elements")]
        [SerializeField] private GameObject _questionMarkUI;

        /// <summary>
        /// Animator 접근 프로퍼티 (LungeAttackPattern의 normalizedTime 체크 등에 사용)
        /// </summary>
        public Animator Animator => _animator;

        // Animation Name Constants (Add only new ones)
        private const string ANIM_LOCOMOTION = "Locomotion";
        private const string ANIM_BASIC_ATTACK = "Basic Attack";
        private const string ANIM_LUNGE_ATTACK = "Lunge Attack";
        private const string ANIM_FLAME_ATTACK = "Flame Attack";
        private const string ANIM_FIREBALL_SHOOT = "Fireball Shoot";
        private const string ANIM_LEGACY_CLAW_ATTACK = "Claw Attack";
        private const string ANIM_TAKE_OFF = "takeOff";
        private const string ANIM_TAKE_OFF_ALT = "TakeOff";
        private const string ANIM_FLY_FORWARD = "FlyForward";
        private const string ANIM_FLY_FORWARD_ALT = "Fly Forward";
        private const string ANIM_FLY_IDLE = "FlyIdle";
        private const string ANIM_FLY_IDLE_ALT = "Fly Idle";
        private const string ANIM_LAND = "Land";
        private const string ANIM_SCREAM = "Scream";

        // Animation IDs
        private static readonly int AnimLocomotion = Animator.StringToHash(ANIM_LOCOMOTION);
        private static readonly int AnimBasicAttack = Animator.StringToHash(ANIM_BASIC_ATTACK);
        private static readonly int AnimLungeAttack = Animator.StringToHash(ANIM_LUNGE_ATTACK);
        private static readonly int AnimFlameAttack = Animator.StringToHash(ANIM_FLAME_ATTACK);
        private static readonly int AnimFireballShoot = Animator.StringToHash(ANIM_FIREBALL_SHOOT);
        private static readonly int AnimLegacyClawAttack = Animator.StringToHash(ANIM_LEGACY_CLAW_ATTACK);
        private static readonly int AnimTakeOff = Animator.StringToHash(ANIM_TAKE_OFF);
        private static readonly int AnimTakeOffAlt = Animator.StringToHash(ANIM_TAKE_OFF_ALT);
        private static readonly int AnimFlyForward = Animator.StringToHash(ANIM_FLY_FORWARD);
        private static readonly int AnimFlyForwardAlt = Animator.StringToHash(ANIM_FLY_FORWARD_ALT);
        private static readonly int AnimFlyIdle = Animator.StringToHash(ANIM_FLY_IDLE);
        private static readonly int AnimFlyIdleAlt = Animator.StringToHash(ANIM_FLY_IDLE_ALT);
        private static readonly int AnimLand = Animator.StringToHash(ANIM_LAND);
        private static readonly int AnimScream = Animator.StringToHash(ANIM_SCREAM);
        private const float DefaultScreamDuration = 1.2f;

        private int _currentAnimState;

        public void SetSpeed(float speed)
        {
            // Blend Tree Parameter (Inherited AnimSpeed)
            if (_animator) _animator.SetFloat(AnimSpeed, speed);
        }

        public void PlayIdle()
        {
            CrossFade(AnimLocomotion);
            SetSpeed(0f);
        }

        public void PlayMove()
        {
            CrossFade(AnimLocomotion);
            // Speed is set by Controller via SetSpeed()
        }

        public void PlayAttack() => CrossFade(AnimBasicAttack);
        public void PlayLungeAttack()
        {
            if (_animator && _animator.HasState(0, AnimLungeAttack))
            {
                CrossFade(AnimLungeAttack);
                return;
            }

            // 아직 Animator 상태명이 변경되지 않은 경우 레거시 이름으로 폴백
            CrossFade(AnimLegacyClawAttack);
        }

        public void PlayProjectileAttack()
        {
            if (_animator == null) return;

            if (_animator.HasState(0, AnimFlameAttack))
            {
                CrossFade(AnimFlameAttack);
                return;
            }

            if (_animator.HasState(0, AnimFireballShoot))
            {
                CrossFade(AnimFireballShoot);
                return;
            }

            // 투사체 전용 상태가 아직 없으면 기본 공격 모션으로 폴백
            CrossFade(AnimBasicAttack);
        }

        public void PlayTakeOff()
        {
            if (TryCrossFade(AnimTakeOff)) return;
            if (TryCrossFade(AnimTakeOffAlt)) return;
            PlayIdle();
        }

        public void PlayFlyForward()
        {
            if (TryCrossFade(AnimFlyForward)) return;
            if (TryCrossFade(AnimFlyForwardAlt)) return;
            PlayMove();
        }

        public void PlayFlyIdle()
        {
            if (TryCrossFade(AnimFlyIdle)) return;
            if (TryCrossFade(AnimFlyIdleAlt)) return;
            PlayIdle();
        }

        public void PlayLand()
        {
            if (TryCrossFade(AnimLand)) return;
            PlayIdle();
        }

        public float PlayScream()
        {
            if (!TryCrossFade(AnimScream))
            {
                PlayIdle();
                return DefaultScreamDuration;
            }

            return GetClipLengthOrDefault(ANIM_SCREAM, DefaultScreamDuration);
        }

        // Override Base Methods to use CrossFade with state tracking
        public override void TriggerHit()
        {
            CrossFade(AnimHit);
            base.TriggerHit(); // Flashing effect
        }

        public override void TriggerDie()
        {
            CrossFade(AnimDie);
            // No base.TriggerDie() call needed if we handle CrossFade here
        }

        private void CrossFade(int stateHash, float duration = 0.1f)
        {
            if (_animator && _currentAnimState != stateHash)
            {
                _currentAnimState = stateHash;
                _animator.CrossFade(stateHash, duration);
            }
        }

        private bool TryCrossFade(int stateHash, float duration = 0.1f)
        {
            if (_animator == null) return false;
            if (!_animator.HasState(0, stateHash)) return false;

            CrossFade(stateHash, duration);
            return true;
        }

        private float GetClipLengthOrDefault(string clipName, float fallback)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return fallback;

            AnimationClip[] clips = _animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null) continue;
                if (!string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase)) continue;
                return clip.length > 0f ? clip.length : fallback;
            }

            return fallback;
        }

        public void SetSearchingUI(bool active)
        {
            if (_questionMarkUI) _questionMarkUI.SetActive(active);
        }
    }
}
