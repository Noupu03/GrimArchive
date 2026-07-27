using System.Collections.Generic;
using UnityEngine;

public enum RoomType
{
    Normal,     // 야생 무리형 (거점 없음)
    Spawner     // 야생 거점형
}

public class Room
{
    public string RoomName { get; set; } = "Room";
    public FactionType RoomFaction { get; set; } = FactionType.Wild;
    public RoomType Type { get; set; } = RoomType.Normal;

    // 2026-07-27 신규 — CreateMap.Chunks.roomId/floorId 원본 참조. GameSession.BuildRoomGrid가 모든
    // 층을 순회하게 되면서(기존엔 1층 고정) 여러 층의 Room이 allRooms 하나에 섞이는데, Bounds만으로는
    // 층을 구분할 수 없어(각 층 로컬 좌표계가 비슷한 범위를 씀) 겹침 오판 버그가 생긴다 — Floor로
    // 명시적으로 구분한다(RoomConfinedMovement 참고). RoomId는 CreateMap 쪽 점령상태(occupationState)
    // 조회 등에 이 Room이 원래 어느 방이었는지 되짚어야 할 때 쓴다.
    public int RoomId { get; set; } = -1;
    public int Floor { get; set; } = -1;


    // 맵 상의 물리적 영역 (MVP 테스트용 기본값 제공)
    public RectInt Bounds { get; set; } = new RectInt(10, 10, 5, 5);
    public bool HasActiveSpawner { get; set; } = false;

    public Vector2Int GetRandomPosInRoom()
    {
        int rx = UnityEngine.Random.Range(Bounds.xMin, Bounds.xMax);
        int ry = UnityEngine.Random.Range(Bounds.yMin, Bounds.yMax);
        return new Vector2Int(rx, ry);
    }

    private List<Unit> _containedUnits = new List<Unit>();
    public IReadOnlyList<Unit> ContainedUnits => _containedUnits;

    public void AddUnit(Unit unit)
    {
        if (!_containedUnits.Contains(unit)) _containedUnits.Add(unit);
    }

    public void RemoveUnit(Unit unit)
    {
        if (_containedUnits.Contains(unit)) _containedUnits.Remove(unit);
    }
}
