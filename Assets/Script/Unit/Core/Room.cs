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

    // CreateMap.Chunks.roomId/floorId 원본 참조. 여러 층의 Room이 allRooms 하나에 섞이므로, Bounds
    // 만으로는 층을 구분할 수 없어(각 층 로컬 좌표계가 비슷한 범위를 씀) Floor로 명시 구분한다
    // (RoomConfinedMovement 참고). RoomId는 CreateMap 쪽 점령상태(occupationState) 조회 등에 쓴다.
    public int RoomId { get; set; } = -1;
    public int Floor { get; set; } = -1;


    // 맵 상의 물리적 영역 (MVP 테스트용 기본값 제공)
    public RectInt Bounds { get; set; } = new RectInt(10, 10, 5, 5);
    public bool HasActiveSpawner { get; set; } = false;

    // 유닛 배치 시스템(2026-07-27 신규) 5.1장 — 방의 최대 인구수. 문서에 수치/공식이 없어 사용자
    // 확인대로 "방 크기(청크 수) 비례" 공식(GameSession.PopulationPerChunk × 이 방의 청크 수)을
    // GameSession.BuildRoomGrid가 계산해서 채운다.
    public int MaxPopulation { get; set; } = 0;

    // 이 방의 안개가 걷혔는지(=플레이어 진영 몬스터가 한 번이라도 들어온 적이 있는지) — 한번 true가
    // 되면 영구히 되돌아가지 않는다. GameSession.InitializeFogOfWar가 0층 전체와 각 층 시작방+인접방을
    // 생성 시점에 미리 true로 깔아 두고, 나머지는 UnitFunction.SyncRoomAffiliation의 최초 입장 시
    // GameSession.RevealRoomFog가 true로 바꾼다.
    public bool FogRevealed { get; set; } = false;

    // 기초문서.md 피드백(2026-08-22) — 코어 전면 개편: 모든 방이 항상 코어를 하나씩 갖는다.
    // GameSession.SpawnAllRoomCores가 방 생성 직후 채워준다. CoreObjectId가 null이면(발생하면 안
    // 되지만) 코어 공격 행동(TacticalBehaviorType.CoreAttack)이 이 방을 목표로 삼지 않는다.
    public string CoreObjectId;
    public Vector3Int CorePosition;

    // 5.2장 "방의 현재 인구수는 소속된 모든 유닛의 인구수 합계" — ContainedUnits 기준으로 매번
    // 계산한다(별도 캐시 없이 항상 최신값 보장, 방 하나에 유닛 수가 많지 않아 비용 낮음). 인구수는
    // "플레이어 진영 몬스터"만 포함한다 — 단순 "인류가 아니면 전부 포함"(!(u is Human))은 야생
    // 몬스터까지 섞여 들어가는 버그가 있어, IsPlayerMonsterFaction으로 정확히 걸러낸다.
    public int CurrentPopulation
    {
        get
        {
            int total = 0;
            foreach (var u in _containedUnits)
                if (u != null && u.hp > 0 && u.IsPlayerMonsterFaction) total += u.populationCost;
            return total;
        }
    }

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
