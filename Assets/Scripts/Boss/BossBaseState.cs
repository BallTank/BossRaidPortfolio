using Core.Common.Patterns;
using UnityEngine;

namespace Core.Boss
{
    /// <summary>
    /// Boss 전용 상태 기본 클래스. BaseState를 상속받아 Update 시그니처 정의.
    /// </summary>
    public abstract class BossBaseState : BaseState<BossController>
    {
        public BossBaseState(BossController controller) : base(controller) { }

        /// <summary>
        /// 매 프레임 호출. Boss는 입력이 없으므로 파라미터 없음.
        /// </summary>
        public abstract void Update();
    }
}
