namespace Core.Boss.Attacks
{
    /// <summary>
    /// 보스 공격 패턴 인터페이스 (Strategy Pattern).
    /// BossAttackState가 이 인터페이스를 통해 다양한 공격을 실행.
    /// </summary>
    public interface IBossAttackPattern
    {
        /// <summary>공격 시작 시 호출 (애니메이션, 히트박스 등)</summary>
        void Enter(BossController controller);

        /// <summary>매 프레임 호출. true를 반환하면 공격 종료.</summary>
        bool Update(BossController controller);

        /// <summary>공격 종료 시 호출 (히트박스 정리 등)</summary>
        void Exit(BossController controller);
    }
}
