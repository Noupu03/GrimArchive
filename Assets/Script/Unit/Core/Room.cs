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
