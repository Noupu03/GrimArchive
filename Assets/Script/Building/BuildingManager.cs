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
    public ProductionRule Rule;
    public float ProductionProgress;
    public bool IsProducing;
}

public class BuildingManager : NativeRoutine
{
    private Dictionary<Vector3Int, BuildingData> buildingGrid = new Dictionary<Vector3Int, BuildingData>();

    private MapManager mapManager;
    private ResourceManager resourceManager;
    private MapRandering mapRandering;
    private CreateMap createMap;
    private UnitGenerate unitGenerate;
    private GameSession gameSession;

    [Inject]
    public void Construct(MapManager mapManager, ResourceManager resourceManager, MapRandering mapRandering, CreateMap createMap, UnitGenerate unitGenerate, GameSession gameSession)
    {
        this.mapManager = mapManager;
        this.resourceManager = resourceManager;
        this.mapRandering = mapRandering;
        this.createMap = createMap;
        this.unitGenerate = unitGenerate;
        this.gameSession = gameSession;
    }

    public override async UniTask Initialize(CancellationToken cts)
    {
        await base.Initialize(cts);
        buildingGrid.Clear();
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

    public override void UpdateProcess()
    {
        float dt = Time.deltaTime;
        
        foreach (var kvp in buildingGrid)
        {
            BuildingData b = kvp.Value;
            if (b.IsProducing)
            {
                b.ProductionProgress += dt;
                if (b.ProductionProgress >= b.Rule.productionTime)
                {
                    b.IsProducing = false;
                    b.ProductionProgress = 0f;
                    SpawnUnitFromBuilding(b);
                }
            }
        }
    }

    // 9단계: 기존 GameSession 파이프라인 연계
    private void SpawnUnitFromBuilding(BuildingData b)
    {
        LogHelper.Log(LogHelper.GAME, $"Production Complete! Spawning {b.Rule.targetUnitTypeName} at {b.Position}");

        if (unitGenerate == null || gameSession == null) return;

        UnitType t = null;
        if (b.Rule.targetUnitTypeName == "Knight") t = new Knight();
        else if (b.Rule.targetUnitTypeName == "Archer") t = new Archer();
        else if (b.Rule.targetUnitTypeName == "MeleeTank") t = new MeleeTank();

        if (t != null)
        {
            Vector2Int pos = new Vector2Int(b.Position.x, b.Position.y);
            
            // 건물이 있는 곳은 통행 불가이므로 주변 빈 자리 찾기
            if (!unitGenerate.IsAreaClear(pos, t.footprint, b.Position.z))
            {
                pos = unitGenerate.GetRandomFloorPos(t.footprint, b.Position.z);
            }

            // 단순 스폰 분기
            if (b.Rule.targetUnitTypeName == "MeleeTank")
            {
                Monster m = unitGenerate.GenerateUnitAtPos<Monster>(t, pos, b.Position.z);
                gameSession.units.Add(m);
                gameSession.RegisterUnitPos(m, m.position);
            }
            else
            {
                Human h = unitGenerate.GenerateUnitAtPos<Human>(t, pos, b.Position.z);
                gameSession.units.Add(h);
                gameSession.RegisterUnitPos(h, h.position);
            }
        }
    }

    // 6단계: 설치 (Install) 로직
    public bool InstallBuilding(Vector3Int pos, ProductionRule rule, Sprite buildingSprite)
    {
        if (!CanInstallAt(pos))
        {
            LogHelper.Warning(LogHelper.GAME, $"Cannot install building at {pos}");
            return false;
        }

        // 1. 데이터 등록
        BuildingData newBuilding = new BuildingData
        {
            Position = pos,
            Rule = rule,
            ProductionProgress = 0f,
            IsProducing = false
        };
        buildingGrid[pos] = newBuilding;

        // 2. MapData 갱신
        UpdateMapDataObstacle(pos, true);

        // 3. Tilemap 렌더링
        if (mapRandering != null && mapRandering.floorTilemaps != null)
        {
            if (pos.z >= 0 && pos.z < mapRandering.floorTilemaps.Length)
            {
                var tilemap = mapRandering.floorTilemaps[pos.z];
                UnityEngine.Tilemaps.Tile newTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                newTile.sprite = buildingSprite;
                // Z is 0 for tilemap local placement
                tilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), newTile); 
            }
        }

        LogHelper.Log(LogHelper.GAME, $"Installed Building '{rule.displayName}' at {pos}");
        return true;
    }

    // 6단계: 철거 (Uninstall) 로직
    public void UninstallBuilding(Vector3Int pos)
    {
        if (!buildingGrid.ContainsKey(pos)) return;

        // 1. 타일맵 지우기
        if (mapRandering != null && mapRandering.floorTilemaps != null)
        {
            if (pos.z >= 0 && pos.z < mapRandering.floorTilemaps.Length)
            {
                var tilemap = mapRandering.floorTilemaps[pos.z];
                tilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), null);
            }
        }

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
