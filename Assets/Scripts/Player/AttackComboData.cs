using UnityEngine;

namespace Core.Player
{
    [System.Serializable]
    public struct AttackComboData
    {
        [Header("Basic Info")]
        public float damage;            // 데미지
        public float duration;          // 공격 동작 전체 시간 (애니메이션 길이)

        [Header("Timing (Seconds)")]
        public float comboInputWindow;  // 다음 콤보 선입력 허용 시간 (이 시간 안에 누르면 다음 타격 예약)
        public float cancelStartTime;   // 대시 등으로 캔슬 가능한 시작 시간 (공격 후딜레이 캔슬용)

        // 추후 확장:
        // public string animationTriggerName;
        // public float hitCheckTime;
        // public GameObject vfxPrefab;
    }
}
