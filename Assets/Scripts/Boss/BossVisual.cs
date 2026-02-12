using Core.Common;
using UnityEngine;

namespace Core.Boss
{
    public class BossVisual : BaseVisual
    {
        [Header("Visual Elements")]
        [SerializeField] private GameObject _questionMarkUI;

        /// <summary>
        /// Animator 접근 프로퍼티 (ClawAttackPattern의 normalizedTime 체크 등에 사용)
        /// </summary>
        public Animator Animator => _animator;

        // Animation Name Constants (Add only new ones)
        private const string ANIM_LOCOMOTION = "Locomotion";
        private const string ANIM_BASIC_ATTACK = "Basic Attack";
        private const string ANIM_CLAW_ATTACK = "Claw Attack";

        // Animation IDs
        private static readonly int AnimLocomotion = Animator.StringToHash(ANIM_LOCOMOTION);
        private static readonly int AnimBasicAttack = Animator.StringToHash(ANIM_BASIC_ATTACK);
        private static readonly int AnimClawAttack = Animator.StringToHash(ANIM_CLAW_ATTACK);

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
        public void PlayClawAttack() => CrossFade(AnimClawAttack);

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

        public void SetSearchingUI(bool active)
        {
            if (_questionMarkUI) _questionMarkUI.SetActive(active);
        }
    }
}
