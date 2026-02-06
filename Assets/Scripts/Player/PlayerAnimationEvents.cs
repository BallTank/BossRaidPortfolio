using UnityEngine;

namespace BossRaid.Player
{
    /// <summary>
    /// 플레이어 모델(Visual)에 부착되어 Animator Event를 수신하고
    /// 부모인 PlayerController에게 전달하는 역할을 합니다.
    /// </summary>
    public class PlayerAnimationEvents : MonoBehaviour
    {
        private PlayerController _controller;

        private void Awake()
        {
            _controller = GetComponentInParent<PlayerController>();
        }

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
    }
}
