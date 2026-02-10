using Core.Common;
using UnityEngine;

namespace Core.Player
{
    /// <summary>
    /// 플레이어 시각 효과 담당 (애니메이션, 이펙트, UI).
    /// 애니메이션 이벤트도 이 클래스에서 수신.
    /// </summary>
    public class PlayerVisual : BaseVisual
    {
        // Controller 참조 (Animation Event 전달용)
        private PlayerController _controller;

        public Animator Animator => _animator;

        private void Awake()
        {
            _controller = GetComponentInParent<PlayerController>();
        }

        #region Visual Methods

        public void SetSpeed(float speed)
        {
            if (_animator) _animator.SetFloat(AnimSpeed, speed);
        }

        #endregion

        #region Animation Events

        /// <summary>
        /// [Animation Event] 공격 판정 시작
        /// </summary>
        public void OnHitStart()
        {
            if (_controller != null)
            {
                _controller.OnHitStart();
            }
        }

        /// <summary>
        /// [Animation Event] 공격 판정 종료
        /// </summary>
        public void OnHitEnd()
        {
            if (_controller != null)
            {
                _controller.OnHitEnd();
            }
        }

        /// <summary>
        /// [Animation Event] 발자국 소리 재생 (선택 사항)
        /// </summary>
        public void OnFootstep()
        {
            // TODO: Sound Manager 연동
        }

        #endregion
    }
}
