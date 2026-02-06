using UnityEngine;

public interface IAttackable
{
    // 콤보 데이터 접근
    AttackComboData[] AttackCombos { get; }
    
    // 현재 공격 중인지 확인하거나 공격 종료 등을 알릴 때 사용 (확장성)
    // void OnAttackHit();
    // void OnAttackFinished();
}
