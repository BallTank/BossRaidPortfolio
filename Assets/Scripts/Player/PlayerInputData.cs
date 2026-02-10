using UnityEngine;

namespace Core.Player
{
    // [1] 비트 마스킹용 플래그 (2의 제곱수)
    [System.Flags] // 이 속성을 붙이면 인스펙터에서 여러 개 선택 가능해짐
    public enum InputFlag : byte
    {
        None = 0,
        Dash = 1 << 0,    // 0000 0001 (십진수 1)
        Attack = 1 << 1,  // 0000 0010 (십진수 2)
        Jump = 1 << 2,    // 0000 0100 (십진수 4)
                          // 필요하면 더 추가: Skill = 1 << 3 ...
    }

    // [2] 데이터 패킷 (스택 메모리 할당 = GC 없음)
    [System.Serializable]
    public struct PlayerInputPacket
    {
        public Vector2 moveDir; // 이동 방향 (W,A,S,D)
        public float lookYaw;   // 좌우 회전 (Y축)
        public float lookPitch; // 위아래 회전 (X축)
        public byte buttons;    // 버튼 뭉치 (비트 패킹)

        // [Helper] 버튼 켜져있는지 확인 (Unpacking)
        public bool HasFlag(InputFlag flag)
        {
            return (buttons & (byte)flag) != 0;
        }

        // [Helper] 버튼 상태 설정 (Packing)
        public void SetFlag(InputFlag flag, bool active)
        {
            if (active)
                buttons |= (byte)flag;      // 켜기 (OR)
            else
                buttons &= (byte)~flag;     // 끄기 (AND + NOT)
        }
    }

    // [3] 인터페이스 (계약서)
    public interface IInputProvider
    {
        // "누가 주든 상관없으니 패킷만 내놔라"
        PlayerInputPacket GetInput();
    }
}