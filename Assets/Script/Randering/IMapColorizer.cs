using UnityEngine;

public interface IMapColorizer
{
    void ChangeRoomColor(Room room, Color color);

    // 점령 순간 흩어진 VFX 대신, 방 테두리(소유 진영을 표현하는 윤곽선) 자체를 잠깐 웅웅거리게
    // 한다. OffenseProcessor.ApplyRoomOwnership(방 소유권이 실제로 바뀐 순간)이 호출한다.
    void PulseRoomOutline(Room room);
}
