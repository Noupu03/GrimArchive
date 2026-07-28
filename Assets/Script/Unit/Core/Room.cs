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

    // 유닛 배치 시스템(2026-07-27 신규) 5.1장 — 방의 최대 인구수. 문서에 수치/공식이 없어 사용자
    // 확인대로 "방 크기(청크 수) 비례" 공식(GameSession.PopulationPerChunk × 이 방의 청크 수)을
    // GameSession.BuildRoomGrid가 계산해서 채운다.
    public int MaxPopulation { get; set; } = 0;

    // 안개 시스템(2026-07-28, 사용자 요청 "0층, 시작방과 양옆 방을 제외하고는 안개가 생겨") — 이
    // 방의 안개가 걷혔는지(=플레이어 진영 몬스터가 한 번이라도 들어온 적이 있는지). 한번 true가 되면
    // 다시 false로 되돌아가지 않는다(영구 해제, "경험이 있어야지만 사라져"). GameSession.
    // InitializeFogOfWar가 0층 전체와 각 층 시작방+인접방을 생성 시점에 미리 true로 깔아 두고, 그
    // 외에는 false로 시작해서 UnitFunction.SyncRoomAffiliation이 최초 입장 시 GameSession.
    // RevealRoomFog를 통해 true로 바꾼다.
    public bool FogRevealed { get; set; } = false;

    // 5.2장 "방의 현재 인구수는 소속된 모든 유닛의 인구수 합계" — ContainedUnits 기준으로 매번
    // 계산한다(별도 캐시 없이 항상 최신값 보장, 방 하나에 보통 유닛 수가 많지 않아 비용 낮음).
    // 2026-07-27 사용자 요청(정정): 인구수는 "플레이어 진영 몬스터"만 포함한다 — 인류는 원래 제외
    // 대상이고, 야생 몬스터(WildMonsterBehavior)도 제외해야 한다. WildBaseSpawnerComponent/
    // GameSession.SpawnWildRoomGuards가 야생 몬스터를 ContainedUnits에 직접 추가하므로 단순히
    // "인류가 아니면 전부 포함"(!(u is Human))으로는 야생 몬스터까지 섞여 들어가는 버그가 있었다 —
    // IsPlayerMonsterFaction으로 정확히 플레이어 소속 몬스터만 걸러낸다.
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
