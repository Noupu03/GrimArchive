using UnityEngine;

public interface IMapColorizer
{
    void ChangeRoomColor(Room room, Color color);

    // 점령 순간 흩어진 VFX 대신, 방 테두리(소유 진영을 표현하는 윤곽선) 자체를 잠깐 웅웅거리게
    // 한다. OffenseProcessor.ApplyRoomOwnership(방 소유권이 실제로 바뀐 순간)이 호출한다.
    void PulseRoomOutline(Room room);

    // 인류 거점을 짓는 동안 방 테두리를 진행도(0~1)만큼 fillColor로 채워 보여 준다. 나머지 부분은 baseColor(소유 진영 색) 그대로다.
    // 다른 갱신(소유 진영 변경·펄스)이 테두리를 다시 그려도 Clear 전까지 유지된다.
    void SetRoomBuildProgress(Room room, float progress01, Color baseColor, Color fillColor);

    // 테두리 채움을 걷고 소유 진영 색 한 가지로 되돌린다.
    void ClearRoomBuildProgress(Room room);
}
