using System.Collections.Generic;
using UnityEngine;
using Haare.Client.Routine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Haare.Util.Logger;
using VContainer;

public class BuildingData
{
    // 건물이 차지하는 타일 중 최소 좌표(좌하단) 앵커 — UnitGenerate.IsAreaClear 등 기존 footprint
    // 관례(position=최소 좌표, +x/+y 방향으로 확장)와 동일하게 맞췄다.
    public Vector3Int Position;

    // 건물이 차지하는 타일 크기 — 유닛 생산 건물 3x3 / 자원 생산 건물 2x2. 더미 건물(디버그용)은
    // 항상 (1,1).
    public Vector2Int Footprint = Vector2Int.one;

    // 렌더링용 GameObject — 바닥 타일 자체를 Tilemap.SetTile로 갈아치우면 스프라이트 투명 영역이
    // 타일맵 뒤(카메라 배경색)를 드러낸다. 바닥은 그대로 두고 이 오브젝트를 그 위에 얹는다
    // (GameSession.SpawnObject와 동일 관례).
    public GameObject VisualObject;

    // true면 자원 생산 건물(V키) — 유닛 생산 큐와 무관하게 시간기반으로 Wood/Stone만 증가시킨다.
    // false면 유닛 생산 건물(B키) — 아래 AvailableRules/Queue를 사용한다.
    public bool IsResourceBuilding;
    public float ResourceTickTimer;

    // 디버그용 더미 건물 — 생산/자원 로직 전부 건너뛰고 타일 점유(건물 판정)만 한다.
    public bool IsDummy;

    // 유닛 생산 건물이 생산 가능한 규칙 목록(문서 6장 "건축물마다 서로 다른 생산 규칙을 지정할 수
    // 있어야 함") — BuildingControlPanel이 이 목록을 버튼으로 나열한다.
    public List<ProductionRule> AvailableRules;
    public Queue<ProductionRule> Queue = new Queue<ProductionRule>();
    public float ProductionProgress;
    public bool IsProducing;

    // 방 인구수 초과로 배출을 보류 중인지 — 대기 진입/해제 시 한 번씩만 로그를 남기기 위한 상태 플래그.
    public bool WaitingForRoomSpace;
}

public class BuildingManager : NativeRoutine
{
    // 자원 생산 건물 1개당 틱 주기/증가량 — 처치 보상(KillRewardWood/Stone=30)의 1/6로 잡은 자리표시자.
    public const float ResourceTickInterval = 5f;
    private const int ResourceTickAmount = 10;

    // 유닛 생산 건물은 3x3, 자원 생산 건물은 2x2, 더미 건물(디버그용)은 항상 1x1.
    public static readonly Vector2Int UnitBuildingFootprint = new Vector2Int(3, 3);
    public static readonly Vector2Int ResourceBuildingFootprint = new Vector2Int(2, 2);
    private static readonly Vector2Int DummyBuildingFootprint = Vector2Int.one;

    // footprint가 차지하는 모든 타일 좌표를 나열한다 — CanInstallAt/InstallBuildingInternal/
    // UpdateMapDataObstacle 3곳이 각자 쓰던 같은 dx/dy 이중 루프를 통합한 것.
    private static IEnumerable<Vector3Int> FootprintTiles(Vector3Int origin, Vector2Int footprint)
    {
        for (int dx = 0; dx < footprint.x; dx++)
        {
            for (int dy = 0; dy < footprint.y; dy++)
            {
                yield return new Vector3Int(origin.x + dx, origin.y + dy, origin.z);
            }
        }
    }

    // footprint 크기의 건물을 감싸는 바로 바깥쪽 테두리 한 칸 오프셋 전체를 만든다(원점=Position
    // 기준). footprint(1,1)이면 (0,1)/(0,-1)/(-1,0)/(1,0) 상하좌우 4칸 그대로다.
    private static List<Vector2Int> GetBuildingBorderOffsets(Vector2Int footprint)
    {
        var offsets = new List<Vector2Int>();
        for (int x = -1; x <= footprint.x; x++)
        {
            offsets.Add(new Vector2Int(x, -1));
            offsets.Add(new Vector2Int(x, footprint.y));
        }
        for (int y = 0; y < footprint.y; y++)
        {
            offsets.Add(new Vector2Int(-1, y));
            offsets.Add(new Vector2Int(footprint.x, y));
        }
        return offsets;
    }

    private Dictionary<Vector3Int, BuildingData> buildingGrid = new Dictionary<Vector3Int, BuildingData>();

    private MapManager mapManager;
    private ResourceManager resourceManager;
    private CreateMap createMap;
    private UnitGenerate unitGenerate;

    // GameSession도 초기 자원 건물 배치를 위해 BuildingManager를 주입받아 VContainer 순환 의존성이
    // 발생하므로, OffenseProcessor.UpdateProcess와 동일하게 DI 대신 GameSession.Instance를 직접 참조한다.
    [Inject]
    public void Construct(MapManager mapManager, ResourceManager resourceManager, CreateMap createMap, UnitGenerate unitGenerate)
    {
        this.mapManager = mapManager;
        this.resourceManager = resourceManager;
        this.createMap = createMap;
        this.unitGenerate = unitGenerate;
    }

    public override async UniTask Initialize(CancellationToken cts)
    {
        await base.Initialize(cts);
        // NativeRoutine들의 Initialize() 실행 순서는 보장되지 않는다 — GameSession.SpawnInitialBuildings()가
        // buildingGrid에 항목을 넣은 뒤 여기서 다시 Clear()하면 지워지므로, 필드 초기화로 이미 빈
        // Dictionary인 buildingGrid를 여기서 비우지 않는다.
        LogHelper.Log(LogHelper.GAME, "BuildingManager Initialized");
    }

    public override async UniTask Finalize()
    {
        buildingGrid.Clear();
        LogHelper.Log(LogHelper.GAME, "BuildingManager Disposed");
        await base.Finalize();
    }

    // 1타일 1오브젝트 규칙 — footprint가 차지할 모든 타일이 각각 설치 가능해야 전체 설치가 가능하다.
    // requireOwnership=false면 방 점령(소유) 여부를 무시한다(디버그용 더미 건물 전용, 실제 생산
    // 건물은 항상 true).
    public bool CanInstallAt(Vector3Int pos, Vector2Int footprint, bool requireOwnership = true)
    {
        foreach (Vector3Int tile in FootprintTiles(pos, footprint))
        {
            if (!CanInstallSingleTile(tile, requireOwnership))
                return false;
        }
        return true;
    }

    private bool CanInstallSingleTile(Vector3Int pos, bool requireOwnership = true)
    {
        // 좌표가 음수면 맵 밖이므로 바로 false 반환 (IndexOutOfRangeException 방지)
        if (pos.x < 0 || pos.y < 0 || pos.z < 0) return false;

        // 1. 이미 건물이 있는지 확인 (1타일 1오브젝트)
        if (buildingGrid.ContainsKey(pos)) return false;

        // 1-1. 게이트(문) 타일 최우선 예약 — 문이 파괴된 뒤에도 재설치 가능성을 지형으로 영구 보장하려고 IsDoorTile 대신 IsRepairableDoorTile로 확인한다.
        if (GameSession.Instance != null && GameSession.Instance.IsRepairableDoorTile(pos)) return false;

        // 1-2. 자기 소유(PlayerControlled) 방에만 건물을 지을 수 있다. requireOwnership=false(더미 건물)면 건너뛴다.
        if (requireOwnership && createMap != null && !createMap.IsPositionPlayerOwned(pos.z, new Vector2Int(pos.x, pos.y))) return false;

        // 2. 맵의 타일 장애물 정보 확인
        if (createMap != null && createMap.map.floors != null)
        {
            if (pos.z >= 0 && pos.z < createMap.map.floors.Length)
            {
                Floor floor = createMap.map.floors[pos.z];
                if (floor.chunks != null)
                {
                    int cs = floor.config.chunkSize;
                    int cx = pos.x / cs;
                    int cy = pos.y / cs;
                    int tx = pos.x % cs;
                    int ty = pos.y % cs;
                    if (cx >= 0 && cx < floor.config.width && cy >= 0 && cy < floor.config.height)
                    {
                        var chunk = floor.chunks[cx, cy];
                        if (chunk.chunk != null)
                        {
                            var tile = chunk.chunk[tx, ty];
                            // 기본 타일맵 상 지나갈수 없는 곳인지 체크 (벽 등)
                            if (tile.name == "Wall") return false;
                            // 이미 다른 구조물이 있는지 체크
                            if (tile.isStructureExist) return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    public BuildingData GetBuildingAt(Vector3Int pos)
    {
        buildingGrid.TryGetValue(pos, out BuildingData data);
        return data;
    }

    // 건물 조작 UI용 조회 — 자원 생산 건물 개수와 그로 인한 초당 생산량을 계산한다.
    public int ResourceBuildingCount
    {
        get
        {
            // 다중 타일 건물(2026-08-25)은 footprint 칸 수만큼 buildingGrid에 같은 BuildingData가
            // 여러 키로 중복 등록되므로, 앵커 타일(kvp.Key == b.Position)일 때만 세어 중복 집계를 막는다.
            int count = 0;
            foreach (var kvp in buildingGrid)
                if (kvp.Key == kvp.Value.Position && kvp.Value.IsResourceBuilding) count++;
            return count;
        }
    }

    public float CurrentResourceProductionPerSecond => ResourceBuildingCount * (ResourceTickAmount / ResourceTickInterval);

    public override void UpdateProcess()
    {
        float dt = Time.deltaTime;

        foreach (var kvp in buildingGrid)
        {
            BuildingData b = kvp.Value;

            // 다중 타일 건물은 footprint 칸 수만큼 같은 BuildingData가 여러 키로 등록되므로, 앵커
            // 타일에서만 처리해 생산/자원 틱 중복 실행을 막는다.
            if (kvp.Key != b.Position) continue;

            if (b.IsDummy) continue;

            if (b.IsResourceBuilding)
            {
                b.ResourceTickTimer += dt;
                if (b.ResourceTickTimer >= ResourceTickInterval)
                {
                    b.ResourceTickTimer -= ResourceTickInterval;
                    resourceManager?.AddResource(ResourceType.Wood, ResourceTickAmount);
                    resourceManager?.AddResource(ResourceType.Stone, ResourceTickAmount);
                }
                continue;
            }

            // 대기열에 항목이 있고 지금 아무것도 안 만들고 있으면 다음 항목 생산 시작
            if (!b.IsProducing && b.Queue.Count > 0)
            {
                b.IsProducing = true;
                b.ProductionProgress = 0f;
            }

            if (b.IsProducing)
            {
                // 방 인구수 제한 — 생산 완료 시점이 아니라 진행 자체를 게이팅한다. 방이 꽉 차 있는
                // 동안은 진행도를 그대로 정지시켜 자리가 나기 전까지 타이머가 흐르지 않는다.
                if (!HasRoomForProduction(b))
                {
                    if (!b.WaitingForRoomSpace)
                    {
                        b.WaitingForRoomSpace = true;
                        LogHelper.Warning(LogHelper.GAME, $"방 인구수 초과로 생산을 중지합니다: {b.Position}");
                        NoticeCenter.Instance?.PushMomentary($"방 인구수 초과로 생산이 중지됐습니다. ({b.Position})", NoticeCenter.WarningColor);
                    }
                    continue;
                }
                b.WaitingForRoomSpace = false;

                b.ProductionProgress += dt;
                ProductionRule current = b.Queue.Peek();
                if (b.ProductionProgress >= current.productionTime)
                {
                    b.Queue.Dequeue();
                    b.IsProducing = false;
                    b.ProductionProgress = 0f;
                    SpawnUnitFromBuilding(b, current);
                }
            }
        }
    }

    // 건물이 속한 방에 생산된 유닛 하나가 더 들어갈 자리가 있는지 확인한다. 현재 생산 가능한 유닛은
    // MeleeTank뿐이라 populationCost=1로 단순화했다 — 종류가 늘어나면 실제 UnitType 조회로 바꿔야 한다.
    private bool HasRoomForProduction(BuildingData b)
    {
        GameSession gameSession = GameSession.Instance;
        if (gameSession == null) return true;
        if (!gameSession.roomGrid.TryGetValue(b.Position, out Room room)) return true;

        const int meleeTankPopulationCost = 1;
        return room.CurrentPopulation + meleeTankPopulationCost <= room.MaxPopulation;
    }

    // 9단계: 기존 GameSession 파이프라인 연계
    private void SpawnUnitFromBuilding(BuildingData b, ProductionRule rule)
    {
        LogHelper.Log(LogHelper.GAME, $"Production Complete! Spawning {rule.targetUnitTypeName} near {b.Position}");

        GameSession gameSession = GameSession.Instance;
        if (unitGenerate == null || gameSession == null) return;

        // 플레이어(몬스터 진영) 생산 건물이라 안전하게 쓸 수 있는 건 Monster 계열뿐이다(Human 기본값은
        // 적 진영이라 여기서 뽑으면 적 유닛이 나온다). 현재 플레이어용 Monster UnitType은 MeleeTank뿐이다.
        UnitType t = null;
        if (rule.targetUnitTypeName == "MeleeTank") t = new MeleeTank();

        if (t == null)
        {
            LogHelper.Warning(LogHelper.GAME, $"알 수 없거나 플레이어 진영에 부적합한 유닛 타입: {rule.targetUnitTypeName}");
            return;
        }

        Vector2Int pos = GetSpawnPosAroundBuilding(b, t.footprint);

        Monster m = unitGenerate.GenerateUnitAtPos<Monster>(t, pos, b.Position.z);
        // 플레이어 진영 몬스터는 해당 방 안에서만 돌아다니고 플레이어 명령으로만 다른 방으로 이동 가능하다.
        m.MovementAlgorithm = new RoomConfinedMovement();
        gameSession.units.Add(m);
        gameSession.RegisterUnitPos(m, m.position);
    }

    // 건물 테두리 칸들을 랜덤 순서로 섞어서 첫 번째로 비어있는 칸을 반환한다. 테두리가 다 막혀
    // 있으면 방 안에서 재시도한다(플레이어 진영 몬스터는 해당 방 안에서만 스폰돼야 하므로 층 전체
    // 폴백은 최후 수단으로만 쓴다).
    private Vector2Int GetSpawnPosAroundBuilding(BuildingData b, Vector2 footprint)
    {
        Vector3Int buildingPos = b.Position;
        Vector2Int origin = new Vector2Int(buildingPos.x, buildingPos.y);
        var offsets = GetBuildingBorderOffsets(b.Footprint);
        for (int i = offsets.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (offsets[i], offsets[j]) = (offsets[j], offsets[i]);
        }

        foreach (var offset in offsets)
        {
            Vector2Int candidate = origin + offset;
            if (unitGenerate.IsAreaClear(candidate, footprint, buildingPos.z))
                return candidate;
        }

        if (GameSession.Instance != null && GameSession.Instance.roomGrid.TryGetValue(buildingPos, out Room room))
        {
            const int maxAttempts = 20;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector2Int candidate = room.GetRandomPosInRoom();
                if (unitGenerate.IsAreaClear(candidate, footprint, buildingPos.z))
                    return candidate;
            }
        }

        LogHelper.Warning(LogHelper.GAME, $"GetSpawnPosAroundBuilding: 건물({buildingPos})이 속한 방 안에서 스폰 자리를 못 찾아 층 전체에서 대신 찾습니다 — 방 제한 규칙에 예외가 생깁니다.");
        return unitGenerate.GetRandomFloorPos(footprint, buildingPos.z);
    }

    // 설치(Install) 로직 — 자원 생산 건물(V키)과 유닛 생산 건물(B키)은 얇은 래퍼로 분리한다(공통
    // 로직은 InstallBuildingInternal).
    public bool InstallResourceBuilding(Vector3Int pos, Sprite buildingSprite)
    {
        return InstallBuildingInternal(pos, ResourceBuildingFootprint, isResourceBuilding: true, isDummy: false, availableRules: null, buildingSprite, "자원 생산 시설");
    }

    public bool InstallProductionBuilding(Vector3Int pos, List<ProductionRule> availableRules, Sprite buildingSprite)
    {
        return InstallBuildingInternal(pos, UnitBuildingFootprint, isResourceBuilding: false, isDummy: false, availableRules, buildingSprite, "유닛 생산 시설");
    }

    // 디버그용 더미 건물 — 생산/자원 로직 없이 1타일 점유만 한다. requireOwnership: false — 방 소유/점령 여부와 무관하게 설치 가능하다.
    public bool InstallDummyBuilding(Vector3Int pos, Sprite buildingSprite, string displayName)
    {
        return InstallBuildingInternal(pos, DummyBuildingFootprint, isResourceBuilding: false, isDummy: true, availableRules: null, buildingSprite, displayName, requireOwnership: false);
    }

    private bool InstallBuildingInternal(Vector3Int pos, Vector2Int footprint, bool isResourceBuilding, bool isDummy, List<ProductionRule> availableRules, Sprite buildingSprite, string displayNameForLog, bool requireOwnership = true)
    {
        if (!CanInstallAt(pos, footprint, requireOwnership))
        {
            LogHelper.Warning(LogHelper.GAME, $"Cannot install building at {pos} (footprint {footprint})");
            return false;
        }

        GameObject visual = CreateBuildingVisual(pos, footprint, buildingSprite, displayNameForLog);

        BuildingData newBuilding = new BuildingData
        {
            Position = pos,
            Footprint = footprint,
            IsResourceBuilding = isResourceBuilding,
            IsDummy = isDummy,
            AvailableRules = availableRules,
            VisualObject = visual,
        };

        foreach (Vector3Int tile in FootprintTiles(pos, footprint))
        {
            buildingGrid[tile] = newBuilding;
        }

        UpdateMapDataObstacle(pos, footprint, true);

        LogHelper.Log(LogHelper.GAME, $"Installed Building '{displayNameForLog}' at {pos} (footprint {footprint})");
        return true;
    }

    // Tilemap.SetTile로 바닥 타일을 통째로 갈아치우면 스프라이트 투명 영역이 타일맵 뒤(카메라 배경색)를
    // 드러내므로, 바닥은 그대로 두고 이 GameObject를 그 위에 얹는다(GameSession.SpawnObject와 동일 관례).
    private GameObject CreateBuildingVisual(Vector3Int pos, Vector2Int footprint, Sprite buildingSprite, string objectName)
    {
        GameObject visual = new GameObject($"Building_{objectName}_{pos.x}_{pos.y}_{pos.z}");
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = buildingSprite;
        // 건물이 계단/오브젝트(다른 sortingOrder들이 모두 5)보다 항상 위에 그려지도록 6으로 올린다.
        sr.sortingOrder = 6;

        Vector3 floorOffset = unitGenerate != null ? unitGenerate.GetFloorOffset(pos.z) : Vector3.zero;
        visual.transform.position = new Vector3(pos.x + footprint.x / 2f, pos.y + footprint.y / 2f, 0f) + floorOffset;

        Vector2 spriteWorldSize = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        float scaleX = spriteWorldSize.x > 0f ? footprint.x / spriteWorldSize.x : footprint.x;
        float scaleY = spriteWorldSize.y > 0f ? footprint.y / spriteWorldSize.y : footprint.y;
        visual.transform.localScale = new Vector3(scaleX, scaleY, 1f);

        // 건물은 "Buildings" 하위 그룹으로 계층 정리.
        Transform buildingGroup = GameSession.Instance?.GetFloorCategoryGroup(pos.z, "Buildings");
        if (buildingGroup != null) visual.transform.SetParent(buildingGroup, true);

        return visual;
    }

    private void UpdateMapDataObstacle(Vector3Int pos, Vector2Int footprint, bool isObstacle)
    {
        foreach (Vector3Int tile in FootprintTiles(pos, footprint))
        {
            UpdateMapDataObstacleSingleTile(tile, isObstacle);
        }
    }

    private void UpdateMapDataObstacleSingleTile(Vector3Int pos, bool isObstacle)
    {
        if (pos.x < 0 || pos.y < 0 || pos.z < 0) return;
        if (createMap == null || createMap.map.floors == null) return;
        if (pos.z >= createMap.map.floors.Length) return;

        // 1. 세이브/로드 및 기반 데이터를 위한 MapData 갱신
        Floor floor = createMap.map.floors[pos.z];
        int cs = floor.config.chunkSize;
        int cx = pos.x / cs;
        int cy = pos.y / cs;
        int tx = pos.x % cs;
        int ty = pos.y % cs;

        if (cx >= 0 && cx < floor.config.width && cy >= 0 && cy < floor.config.height)
        {
            var chunk = floor.chunks[cx, cy];
            if (chunk.chunk != null)
            {
                // 배열 내 구조체 값을 직접 변경
                chunk.chunk[tx, ty].isStructureExist = isObstacle;
            }
        }

        // 2. 실제 유닛들의 이동 및 길찾기(AStar) 판단 기준이 되는 discoveredMap 동기화
        // 2=벽(이동불가), 1=바닥(이동가능)
        int mapValue = isObstacle ? 2 : 1;

        if (Unit.humanFactionData != null && Unit.humanFactionData.discoveredMap != null)
        {
            if (pos.z >= 0 && pos.z < Unit.humanFactionData.discoveredMap.Length)
            {
                Unit.humanFactionData.discoveredMap[pos.z][pos.x, pos.y] = mapValue;
            }
        }

        if (Unit.monsterFactionData != null && Unit.monsterFactionData.discoveredMap != null)
        {
            if (pos.z >= 0 && pos.z < Unit.monsterFactionData.discoveredMap.Length)
            {
                Unit.monsterFactionData.discoveredMap[pos.z][pos.x, pos.y] = mapValue;
            }
        }
    }
}
