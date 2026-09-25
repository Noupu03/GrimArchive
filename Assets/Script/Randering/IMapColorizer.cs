using UnityEngine;

public interface IMapColorizer
{
    void ChangeRoomColor(Room room, Color color);

    // 2026-09-25 사용자 요청: 점령 순간 흩어진 VFX 대신, 방 테두리(점령 소유 진영을 표현하는 그
    // 윤곽선) 자체를 잠깐 웅웅거리게 한다 — OffenseProcessor.OnRoomOccupied가 호출한다.
    void PulseRoomOutline(Room room);
}
