using UnityEngine;

public interface IMapColorizer
{
    void ChangeRoomColor(Room room, Color color);

    // 점령 순간 흩어진 VFX 대신, 방 테두리(소유 진영을 표현하는 윤곽선) 자체를 잠깐 웅웅거리게
    // 한다. 현재 호출부 없음 — 다음 점령 조건이 정해지면 그 트리거 지점에서 호출할 것.
    void PulseRoomOutline(Room room);
}
