using Core.Common;
using UnityEngine;

namespace Core.Boss
{
    public class BossVisual : BaseVisual
    {
        [Header("Visual Elements")]
        [SerializeField] private GameObject _questionMarkUI;

        // Animation Name Constants
        private const string ANIM_ATTACK = "Attack";

        // Animation IDs
        private static readonly int AnimAttack = Animator.StringToHash(ANIM_ATTACK);

        public void SetSpeed(float speed)
        {
            if (_animator) _animator.SetFloat(AnimSpeed, speed);
        }

        public void TriggerAttack()
        {
            if (_animator) _animator.SetTrigger(AnimAttack);
        }

        public void SetSearchingUI(bool active)
        {
            if (_questionMarkUI) _questionMarkUI.SetActive(active);
        }
    }
}
