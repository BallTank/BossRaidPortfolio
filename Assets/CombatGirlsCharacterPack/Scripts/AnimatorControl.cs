using UnityEngine;
using UnityEngine.UI;

namespace CombatGirlsCharacterPack
{
    public class AnimatorControl : MonoBehaviour
    {
        private Animator animator;
        public Toggle rootMotionToggle; // Toggle UI 컴포넌트를 연결합니다.

        private void Start()
        {
            // 캐릭터 오브젝트의 Animator 컴포넌트를 가져옵니다.
            animator = GetComponent<Animator>();

            // 토글 UI 상태가 변경될 때마다 함수를 호출합니다.
            rootMotionToggle.onValueChanged.AddListener(ToggleRootMotion);
        }

        public void ToggleRootMotion(bool enableRootMotion)
        {
            // 선택된 루트 모션 옵션을 적용합니다.
            animator.applyRootMotion = enableRootMotion;
        }
    }
}
