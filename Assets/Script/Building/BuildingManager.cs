using System.Collections.Generic;
using UnityEngine;
using Haare.Client.Routine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Haare.Util.Logger;
using VContainer;

public class BuildingData
{
    public Vector3Int Position;

    // 렌더링용 GameObject(2026-07-27, 사용자 신고 "건물 스프라이트 바깥쪽이 파란색" 대응) — 예전엔
    // Tilemap.SetTile로 바닥 타일 자체를 건물 타일로 갈아치웠는데, 그러면 스프라이트의 투명 영역이
    // 원래 바닥이 아니라 타일맵 뒤(카메라 배경색, 파란색)를 그대로 보여줬다. 바닥은 그대로 두고 이
    // 오브젝트를 그 위에 별도로 얹어서(GameSession.SpawnObject와 동일 관례) 자연스럽게 겹치게 한다.
    public GameObject VisualObject;

    // true면 자원 생산 건물(V키) — 유닛 생산 큐와 무관하게 시간기반으로 Wood/Stone만 증가시킨다.
    // false면 유닛 생산 건물(B키) — 아래 AvailableRules/Queue를 사용한다.
    public bool IsResourceBuilding;
    public float ResourceTickTimer;

    // 유닛 생산 건물이 생산 가능한 규칙 목록(문서 6장 "건축물마다 서로 다른 생산 규칙을 지정할 수
    // 있어야 함") — BuildingControlPanel이 이 목록을 버튼으로 나열한다.
    public List<ProductionRule> AvailableRules;
    public Queue<ProductionRule> Queue = new Queue<ProductionRule>();
    public float ProductionProgress;
    public bool IsProducing;
}

public class BuildingManager : NativeRoutine
{
    // 건축물·자원·유닛 생산 MVP(2026-07-27) — 자원 생산 건물 1개당 틱 주기/증가량. 문서와 사용자 모두
    // 정확한 수치를 정하지 않아(문서는 "최종 수치는 범위 밖"이라 명시) 처치 보상(ResourceManager.
    // KillRewardWood/Stone = 30, 훨씬 커야 한다는 사용자 기준)의 1/6로 잡았다 — 플레이테스트 후 조정
    // 요청이 오면 이 두 상수만 바꾸면 된다.
    public const float ResourceTickInterval = 5f;
    private const int ResourceTickAmount = 5;

    // 유닛 생산 건물에서 완성된 유닛이 나오는 4방향(사용자 요청: "상하좌우에서 랜덤으로 하나").
    private static readonly Vector2Int[] AdjacentOffsets =
    {
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
    };

    private Dictionary<Vector3Int, BuildingData> buildingGrid = new Dictionary<Vector3Int, BuildingData>();

    private MapManager mapManager;
    private ResourceManager resourceManager;
    private CreateMap createMap;
    private UnitGenerate unitGenerate;

    // 순환 의존성 방지(2026-07-27) — GameSession도 초기 자원 건물 배치를 위해 BuildingManager를
    // 주입받으면서 VContainer가 "Circular dependency detected" 예외를 던졌다(GameSession↔BuildingManager
    // 상호 [Inject] 메서드 순환). OffenseProcessor.UpdateProcess와 동일한 관례를 따라 DI 주입 대신
    // GameSession.Instance를 직접 참조해 순환을 끊는다.
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
        // 사용자 신고(2026-07-27) "시작방에 자동 생성된 V키 건물이 상호작용도 안 되고 자원도 안 늘어남"
        // — 원인: 서로 다른 NativeRoutine의 Initialize()는 실행 순서가 보장되지 않는데,
        // GameSession.Initialize()가 SpawnInitialBuildings()로 이 buildingGrid에 항목을 넣은
        // *뒤에* 이 메서드가 나중에 실행되면 여기 있던 buildingGrid.Clear()가 그 항목을 지워버렸다
        // (시각 오브젝트는 이미 만들어져 남아있으니 눈엔 보이지만 클릭도, 생산 틱도 안 먹는 상태가 됨).
        // buildingGrid는 필드 초기화 시점에 이미 빈 Dictionary라 여기서 다시 비울 필요가 없다 — 그냥 제거.
        LogHelper.Log(LogHelper.GAME, "BuildingManager Initialized");
    }

    public override async UniTask Finalize()
    {
        buildingGrid.Clear();
        LogHelper.Log(LogHelper.GAME, "BuildingManager Disposed");
        await base.Finalize();
    }

    // 5단계: 1타일 1오브젝트 규칙 (캡슐화된 쿼리)
    public bool CanInstallAt(Vector3Int pos)
    {
        // 좌표가 음수면 맵 밖이므로 바로 false 반환 (IndexOutOfRangeException 방지)
        if (pos.x < 0 || pos.y < 0 || pos.z < 0) return false;

        // 1. 이미 건물이 있는지 확인 (1타일 1오브젝트)
        if (buildingGrid.ContainsKey(pos)) return false;

        // 1-1. 문 타일은 최우선 예약 — 건물이 문 위에 겹쳐 설치될 수 없다(사용자 신고 2026-07-28,
        // "시작방에 있는 건물이 문 위치와 겹쳐서"). GameSession.SpawnDoors()가 다른 오브젝트/건물보다
        // 먼저 실행돼 objectGrid를 선점하지만, 이 클래스의 buildingGrid/타일 장애물 검사만으로는 문을
        // 걸러내지 못했다(문은 Tile.isStructureExist를 세우지 않는 "통행 가능" 오브젝트라서). 순환
        // 의존성 회피를 위해 GameSession도 DI 대신 Instance를 직접 참조한다(위 Construct 주석 참고).
        if (GameSession.Instance != null && GameSession.Instance.IsDoorTile(pos)) return false;

        // 2. 맵의 타일 장애물 정보 확인
        if (createMap != null && createMap.map.floors != null)
        {
            if (pos.z >= 0 && pos.z < createMap.map.floors.Length)
            {
                Floor floor = createMap.map.floors[pos.z];
                if (floor.chunks != null)
                {
                    int cx = pos.x / 8;
                    int cy = pos.y / 8;
                    int tx = pos.x % 8;
                    int ty = pos.y % 8;
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

    // 건물 조작 UI(2026-07-27, 사용자 요청 "V키 건물 클릭 시 현재 초당 생산량 표시")용 조회 — 자원
    // 생산 건물 개수와 그로 인한 초당 생산량(건물마다 독립적으로 ResourceTickAmount/ResourceTickInterval
    // 비율로 틱)을 계산한다.
    public int ResourceBuildingCount
    {
        get
        {
            int count = 0;
            foreach (var b in buildingGrid.Values)
                if (b.IsResourceBuilding) count++;
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

    // 9단계: 기존 GameSession 파이프라인 연계
    private void SpawnUnitFromBuilding(BuildingData b, ProductionRule rule)
    {
        LogHelper.Log(LogHelper.GAME, $"Production Complete! Spawning {rule.targetUnitTypeName} near {b.Position}");

        GameSession gameSession = GameSession.Instance;
        if (unitGenerate == null || gameSession == null) return;

        // 플레이어(몬스터 진영) 생산 건물이라 실제로 안전하게 쓸 수 있는 건 PlayerMonsterBehavior가
        // 기본값인 Monster 계열뿐이다(Unit.cs — Human 기본값은 HumanFactionBehavior=적 진영이라
        // Knight/Archer를 여기서 뽑으면 적 유닛이 나오는 셈이 된다). 현재 코드베이스에 있는 플레이어용
        // Monster UnitType은 MeleeTank 하나뿐이라 우선 이것만 등록해 둔다 — AvailableRules/Queue 구조
        // 자체는 리스트/큐라 나중에 플레이어용 UnitType이 추가되면 바로 확장 가능하다.
        UnitType t = null;
        if (rule.targetUnitTypeName == "MeleeTank") t = new MeleeTank();

        if (t == null)
        {
            LogHelper.Warning(LogHelper.GAME, $"알 수 없거나 플레이어 진영에 부적합한 유닛 타입: {rule.targetUnitTypeName}");
            return;
        }

        Vector2Int pos = GetSpawnPosAroundBuilding(b.Position, t.footprint);

        Monster m = unitGenerate.GenerateUnitAtPos<Monster>(t, pos, b.Position.z);
        // 플레이어 진영 몬스터 방 제한 MVP(2026-07-27, 사용자 요청) — "해당 방 안에서만 돌아다님,
        // 오직 플레이어의 명령에 의해서만 다른 방으로 이동 가능". 야생 몬스터(SpawnWildRoomGuards)와
        // 동일한 MovementAlgorithm을 재사용 — 플레이어 명령 시 우회하는 예외는 RoomConfinedMovement
        // 쪽에 넣었다(isManualMoveCommand 체크).
        m.MovementAlgorithm = new RoomConfinedMovement();
        gameSession.units.Add(m);
        gameSession.RegisterUnitPos(m, m.position);
    }

    // 건물의 상/하/좌/우 4칸을 랜덤 순서로 섞어서 첫 번째로 비어있는 칸을 반환한다(사용자 요청).
    // 4칸이 다 막혀 있으면 건물이 속한 방 안에서 재시도한다(사용자 요청, 2026-07-27: "플레이어 진영
    // 몬스터는 해당 방 안에서만 스폰" — 층 전체에서 뽑으면 다른 방에 떨어질 수 있어 더 이상 그 폴백을
    // 쓰지 않는다). 방을 못 찾거나 방 안에도 자리가 없는 극단적인 경우에만 최후 수단으로 층 전체에서
    // 찾되, 그 경우엔 경고 로그를 남긴다.
    private Vector2Int GetSpawnPosAroundBuilding(Vector3Int buildingPos, Vector2 footprint)
    {
        Vector2Int origin = new Vector2Int(buildingPos.x, buildingPos.y);
        var offsets = new List<Vector2Int>(AdjacentOffsets);
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

    // 6단계: 설치 (Install) 로직 — 자원 생산 건물(V키)과 유닛 생산 건물(B키)은 얇은 래퍼로 분리해서
    // 호출부에서 헷갈리지 않게 한다(공통 로직은 InstallBuildingInternal).
    public bool InstallResourceBuilding(Vector3Int pos, Sprite buildingSprite)
    {
        return InstallBuildingInternal(pos, isResourceBuilding: true, availableRules: null, buildingSprite, "자원 생산 시설");
    }

    public bool InstallProductionBuilding(Vector3Int pos, List<ProductionRule> availableRules, Sprite buildingSprite)
    {
        return InstallBuildingInternal(pos, isResourceBuilding: false, availableRules, buildingSprite, "유닛 생산 시설");
    }

    private bool InstallBuildingInternal(Vector3Int pos, bool isResourceBuilding, List<ProductionRule> availableRules, Sprite buildingSprite, string displayNameForLog)
    {
        if (!CanInstallAt(pos))
        {
            LogHelper.Warning(LogHelper.GAME, $"Cannot install building at {pos}");
            return false;
        }

        GameObject visual = CreateBuildingVisual(pos, buildingSprite, displayNameForLog);

        BuildingData newBuilding = new BuildingData
        {
            Position = pos,
            IsResourceBuilding = isResourceBuilding,
            AvailableRules = availableRules,
            VisualObject = visual,
        };
        buildingGrid[pos] = newBuilding;

        UpdateMapDataObstacle(pos, true);

        LogHelper.Log(LogHelper.GAME, $"Installed Building '{displayNameForLog}' at {pos}");
        return true;
    }

    // 사용자 신고(2026-07-27) "건물 스프라이트 바깥쪽이 파란색이라 부자연스러움" 대응 — Tilemap.SetTile로
    // 바닥 타일을 통째로 갈아치우면 스프라이트의 투명 영역이 원래 바닥이 아니라 타일맵 뒤(카메라
    // 배경색)를 그대로 드러냈다. GameSession.SpawnObject(루팅/함정/시체/코어)와 동일한 관례로, 바닥은
    // 그대로 두고 이 GameObject를 그 위에 얹는 방식으로 바꿨다 — 스프라이트 크기도 sprite.bounds
    // 기준으로 역산해 항상 타일 1칸에 꽉 차도록 스케일한다(SpawnObject와 동일 원리).
    private GameObject CreateBuildingVisual(Vector3Int pos, Sprite buildingSprite, string objectName)
    {
        GameObject visual = new GameObject($"Building_{objectName}_{pos.x}_{pos.y}_{pos.z}");
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = buildingSprite;
        sr.sortingOrder = 5; // 바닥 위, 유닛/오브젝트와 동일한 순서(GameSession.SpawnObject 참고)

        Vector3 floorOffset = unitGenerate != null ? unitGenerate.GetFloorOffset(pos.z) : Vector3.zero;
        visual.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0f) + floorOffset;

        Vector2 spriteWorldSize = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        float scaleX = spriteWorldSize.x > 0f ? 1f / spriteWorldSize.x : 1f;
        float scaleY = spriteWorldSize.y > 0f ? 1f / spriteWorldSize.y : 1f;
        visual.transform.localScale = new Vector3(scaleX, scaleY, 1f);

        GameObject childTilemap = GameObject.Find($"F{pos.z}_Tilemap");
        if (childTilemap != null) visual.transform.SetParent(childTilemap.transform, true);

        return visual;
    }

    // 6단계: 철거 (Uninstall) 로직
    public void UninstallBuilding(Vector3Int pos)
    {
        if (!buildingGrid.TryGetValue(pos, out BuildingData data)) return;

        // 1. 시각 오브젝트 파괴
        if (data.VisualObject != null) UnityEngine.Object.Destroy(data.VisualObject);

        // 2. MapData 롤백
        UpdateMapDataObstacle(pos, false);

        // 3. 데이터 삭제
        buildingGrid.Remove(pos);

        LogHelper.Log(LogHelper.GAME, $"Uninstalled Building at {pos}");
    }

    private void UpdateMapDataObstacle(Vector3Int pos, bool isObstacle)
    {
        if (pos.x < 0 || pos.y < 0 || pos.z < 0) return;
        if (createMap == null || createMap.map.floors == null) return;
        if (pos.z >= createMap.map.floors.Length) return;

        // 1. 세이브/로드 및 기반 데이터를 위한 MapData 갱신
        Floor floor = createMap.map.floors[pos.z];
        int cx = pos.x / 8;
        int cy = pos.y / 8;
        int tx = pos.x % 8;
        int ty = pos.y % 8;

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
