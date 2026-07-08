using UnityEngine;
using System.Collections.Generic;
using System;

namespace GrimArchive.Wave
{
    public enum SpawnMode
    {
        AroundTransform,
        ByRoomRole,
        ByRoomId
    }

    /// <summary>
    /// GameSession과 VContainer DI 시스템에 맞춰 웨이브 몬스터를 스폰하는 스크립트.
    /// CreateMap의 방(Room) 데이터와 연동하여 지정된 위치 또는 방에 몬스터를 스폰합니다.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [Header("웨이브 설정")]
        [Tooltip("소환할 웨이브 데이터")]
        public WaveData waveData;
        
        [Header("소환 위치 방식")]
        public SpawnMode spawnMode = SpawnMode.ByRoomRole;

        [Tooltip("몇 층(Floor)에 소환할 것인가?")]
        public int targetFloor = 1;

        [Header("Transform 스폰 전용 설정 (AroundTransform)")]
        [Tooltip("소환 중심점 (예: F1 청크 계단). 비워두면 이 오브젝트의 위치를 사용합니다.")]
        public Transform spawnCenter; 
        [Tooltip("소환 중심점으로부터 그리드 타일 단위 최대 반경")]
        public int spawnTileRadius = 3;

        [Header("방(Room) 기반 스폰 전용 설정")]
        [Tooltip("ByRoomRole 선택 시 지정할 방 역할 (예: StartRoom, BossRoom 등)")]
        public RoomRole targetRoomRole = RoomRole.NormalRoom;
        
        [Tooltip("ByRoomId 선택 시 지정할 방 번호")]
        public int targetRoomId = 0;

        [ContextMenu("웨이브 소환 테스트 (Spawn Wave)")]
        public void SpawnWave()
        {
            if (waveData == null)
            {
                Debug.LogWarning("[WaveSpawner] 웨이브 데이터가 할당되지 않았습니다.");
                return;
            }
            if (GameSession.Instance == null || GameSession.Instance.unitGenerate == null)
            {
                Debug.LogError("[WaveSpawner] GameSession 또는 unitGenerate 인스턴스를 찾을 수 없습니다.");
                return;
            }

            // Transform 기반 스폰일 경우의 중심점 초기화
            if (spawnMode == SpawnMode.AroundTransform && spawnCenter == null)
            {
                spawnCenter = transform;
            }

            List<Unit> spawnedUnits = new List<Unit>();

            // 1. 미리 설정된 정해진 유닛들 소환
            foreach (var group in waveData.configuredUnits)
            {
                if (!string.IsNullOrEmpty(group.unitTypeName))
                {
                    for (int i = 0; i < group.count; i++)
                    {
                        Unit unit = InstantiateUnit(group.unitTypeName);
                        if (unit != null) spawnedUnits.Add(unit);
                    }
                }
            }

            // 2. 미리 설정되지 않은(랜덤) 유닛들 소환
            if (waveData.useRandomUnits && waveData.randomUnitPool != null && waveData.randomUnitPool.Count > 0)
            {
                for (int i = 0; i < waveData.randomUnitTotalCount; i++)
                {
                    int randomIndex = UnityEngine.Random.Range(0, waveData.randomUnitPool.Count);
                    string randomTypeName = waveData.randomUnitPool[randomIndex];
                    if (!string.IsNullOrEmpty(randomTypeName))
                    {
                        Unit unit = InstantiateUnit(randomTypeName);
                        if (unit != null) spawnedUnits.Add(unit);
                    }
                }
            }

            Debug.Log($"[WaveSpawner] 총 {spawnedUnits.Count} 마리의 몬스터가 모드({spawnMode})에 의해 소환되어 세션에 등록되었습니다.");
        }

        private Unit InstantiateUnit(string typeName)
        {
            // 1차 시도: C# 클래스 이름으로 바로 찾기 (예: "Knight", "MeleeTank")
            Type t = Type.GetType(typeName);
            
            // 2차 시도: 클래스 이름이 아니라 한글 이름("기사형") 등을 입력했을 경우 리플렉션으로 검색
            if (t == null)
            {
                foreach (Type type in System.Reflection.Assembly.GetExecutingAssembly().GetTypes())
                {
                    if (type.IsSubclassOf(typeof(UnitType)) && !type.IsAbstract)
                    {
                        try 
                        {
                            UnitType tempInstance = (UnitType)Activator.CreateInstance(type);
                            if (tempInstance.typeName == typeName)
                            {
                                t = type;
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }

            if (t == null)
            {
                Debug.LogWarning($"[WaveSpawner] '{typeName}' 클래스 또는 타입 이름을 찾을 수 없습니다. UnitTypes.cs에 정의되어 있는지 확인하세요.");
                return null;
            }

            UnitType unitTypeInstance = null;
            try
            {
                unitTypeInstance = (UnitType)Activator.CreateInstance(t);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WaveSpawner] '{typeName}' 인스턴스 생성 실패: {e.Message}");
                return null;
            }

            // 소환할 위치 찾기
            Vector2Int validPos = FindValidSpawnPosition(unitTypeInstance.footprint);
            
            // UnitGenerate를 통해 Monster 생성 및 의존성 주입
            Monster monster = GameSession.Instance.unitGenerate.GenerateUnitAtPos<Monster>(unitTypeInstance, validPos, targetFloor);
            
            if (monster != null)
            {
                GameSession.Instance.units.Add(monster);
                GameSession.Instance.RegisterUnitPos(monster, validPos);
            }

            return monster;
        }

        private Vector2Int FindValidSpawnPosition(Vector2 footprint)
        {
            UnitGenerate generator = GameSession.Instance.unitGenerate;
            
            if (spawnMode == SpawnMode.AroundTransform)
            {
                Vector2Int centerGridPos = new Vector2Int(Mathf.RoundToInt(spawnCenter.position.x), Mathf.RoundToInt(spawnCenter.position.y));
                for (int i = 0; i < 30; i++)
                {
                    int offsetX = UnityEngine.Random.Range(-spawnTileRadius, spawnTileRadius + 1);
                    int offsetY = UnityEngine.Random.Range(-spawnTileRadius, spawnTileRadius + 1);
                    Vector2Int cand = centerGridPos + new Vector2Int(offsetX, offsetY);

                    if (generator.IsAreaClear(cand, footprint, targetFloor))
                        return cand;
                }
            }
            else if (spawnMode == SpawnMode.ByRoomRole || spawnMode == SpawnMode.ByRoomId)
            {
                CreateMap cmap = GameSession.Instance.cmap;
                if (cmap != null && cmap.map.floors != null && targetFloor >= 0 && targetFloor < cmap.map.floors.Length)
                {
                    Floor floor = cmap.map.floors[targetFloor];
                    if (floor.chunks != null)
                    {
                        List<Vector2Int> validRoomChunks = new List<Vector2Int>();
                        int chunkW = floor.config.width;
                        int chunkH = floor.config.height;

                        // 1. 조건에 맞는 청크 수집
                        for (int cx = 0; cx < chunkW; cx++)
                        {
                            for (int cy = 0; cy < chunkH; cy++)
                            {
                                Chunks c = floor.chunks[cx, cy];
                                if (c.chunk == null) continue;

                                if (spawnMode == SpawnMode.ByRoomRole && c.roomRole == targetRoomRole)
                                {
                                    validRoomChunks.Add(new Vector2Int(cx, cy));
                                }
                                else if (spawnMode == SpawnMode.ByRoomId && c.roomId == targetRoomId)
                                {
                                    validRoomChunks.Add(new Vector2Int(cx, cy));
                                }
                            }
                        }

                        // 2. 수집된 청크 중에서 랜덤으로 빈 타일 찾기
                        if (validRoomChunks.Count > 0)
                        {
                            for (int i = 0; i < 50; i++) // 50번 시도
                            {
                                Vector2Int chunkPos = validRoomChunks[UnityEngine.Random.Range(0, validRoomChunks.Count)];
                                int tx = UnityEngine.Random.Range(0, 8); // 청크 내부 타일 좌표 (0~7)
                                int ty = UnityEngine.Random.Range(0, 8);
                                
                                Vector2Int globalPos = new Vector2Int(chunkPos.x * 8 + tx, chunkPos.y * 8 + ty);
                                if (generator.IsAreaClear(globalPos, footprint, targetFloor))
                                {
                                    return globalPos;
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[WaveSpawner] 조건에 맞는 방({(spawnMode == SpawnMode.ByRoomRole ? targetRoomRole.ToString() : targetRoomId.ToString())})을 찾지 못했습니다.");
                        }
                    }
                }
            }

            // 조건에 맞는 공간을 못 찾았을 경우 맵 전체에서 랜덤 빈 타일 반환 (안전망)
            Debug.Log("[WaveSpawner] 유효한 방 또는 위치를 찾지 못하여 맵 전체의 랜덤한 위치에 소환합니다.");
            return generator.GetRandomFloorPos(footprint, targetFloor);
        }
    }
}
