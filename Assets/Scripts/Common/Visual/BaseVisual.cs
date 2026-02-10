using System.Collections;
using UnityEngine;

namespace Core.Common
{
    /// <summary>
    /// Base class for visual effects (Player, Boss).
    /// Handles common hit feedback (Flash, Animation).
    /// </summary>
    public abstract class BaseVisual : MonoBehaviour
    {
        [Header("Base References")]
        [SerializeField] protected Animator _animator;
        [SerializeField] protected Renderer[] _renderers; // For flashing effect

        [Header("Flash Settings")]
        [SerializeField] protected Color _flashColor = Color.white;
        [SerializeField] protected float _flashDuration = 0.1f;

        // Animation Name Constants
        protected const string ANIM_SPEED = "Speed";
        protected const string ANIM_HIT = "Hit";
        protected const string ANIM_DIE = "Die";

        // Animation IDs - Shared
        protected static readonly int AnimSpeed = Animator.StringToHash(ANIM_SPEED);
        protected static readonly int AnimHit = Animator.StringToHash(ANIM_HIT);
        protected static readonly int AnimDie = Animator.StringToHash(ANIM_DIE);

        /// <summary>
        /// Triggers Hit animation and flash effect.
        /// </summary>
        public virtual void TriggerHit()
        {
            if (_animator)
            {
                _animator.CrossFade(AnimHit, 0.1f);
            }
            StartCoroutine(FlashRoutine());
        }

        public virtual void TriggerDie()
        {
            if (_animator)
            {
                _animator.CrossFade(AnimDie, 0.1f);
            }
        }

        protected virtual IEnumerator FlashRoutine()
        {
            if (_renderers == null || _renderers.Length == 0) yield break;

            // Flash
            foreach (var r in _renderers) r.material.color = _flashColor;

            yield return new WaitForSeconds(_flashDuration);

            // Restore (Assuming White/Default is the target state)
            foreach (var r in _renderers) r.material.color = Color.white;
        }
    }
}
