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
    /// GameSession과 VContainer DI 시스템에 맞춰 웨이브 몬스터(+동행 인류 파티)를 스폰하는 스크립트.
    /// CreateMap의 방(Room) 데이터와 연동하여 지정된 위치 또는 방에 몬스터를 스폰합니다.
    /// </summary>
    public class WaveSpawner : Haare.Client.Routine.NativeRoutine
    {
        [Tooltip("소환할 웨이브 데이터")]
        public WaveData waveData;

        public SpawnMode spawnMode = SpawnMode.ByRoomRole;

        [Tooltip("몇 층(Floor)에 소환할 것인가?")]
        public int targetFloor = 1;

        [Tooltip("소환 중심점 (예: F1 청크 계단).")]
        public Vector3 spawnCenter = Vector3.zero;
        [Tooltip("소환 중심점으로부터 그리드 타일 단위 최대 반경")]
        public int spawnTileRadius = 3;

        [Tooltip("ByRoomRole 선택 시 지정할 방 역할 (예: StartRoom, BossRoom 등)")]
        public RoomRole targetRoomRole = RoomRole.NormalRoom;

        [Tooltip("ByRoomId 선택 시 지정할 방 번호")]
        public int targetRoomId = 0;

        public override async Cysharp.Threading.Tasks.UniTask Initialize(System.Threading.CancellationToken cts)
        {
            await base.Initialize(cts);

            // 런타임에 WaveData 자동 로드 시도
            if (waveData == null)
            {
                waveData = Resources.Load<WaveData>("WaveData");
                if (waveData == null)
                {
                    Haare.Util.Logger.LogHelper.Warning(Haare.Util.Logger.LogHelper.GAME, "WaveSpawner: Resources/WaveData 를 찾지 못했습니다. 웨이브를 소환하려면 데이터를 주입해야 합니다.");
                }
            }
        }

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

            List<Monster> spawnedMonsters = new List<Monster>();
            List<Party> spawnedParties = new List<Party>();

            if (waveData.parties != null)
            {
                foreach (var config in waveData.parties)
                {
                    if (config == null || config.units == null) continue;

                    if (config.faction == PartyFaction.Monster)
                    {
                        // 몬스터 그룹 — 별도 Party 객체 없이 스쿼드로만 스폰. 스폰 위치는 인스펙터의
                        // spawnMode/targetRoomRole/targetRoomId 설정을 그대로 따른다.
                        foreach (var group in config.units)
                        {
                            if (string.IsNullOrEmpty(group.unitTypeName)) continue;
                            for (int i = 0; i < group.count; i++)
                            {
                                Monster monster = InstantiateMonster(group.unitTypeName);
                                if (monster != null) spawnedMonsters.Add(monster);
                            }
                        }
                    }
                    else // PartyFaction.Human
                    {
                        // 인류 파티 — 연산공식 문서 6장/13장/23장이 전제하는 "파티" 단위로 실제
                        // Party 객체를 만든다. 파티원은 몬스터 스폰 위치와 무관하게 항상 시작방
                        // (StartRoom)에서 등장한다 — 몬스터 그룹의 스폰 모드를 그대로 재사용하면
                        // 보스방/일반방 한복판에 파티가 떨어지는 상황이 생길 수 있어서다.
                        List<Human> members = new List<Human>();
                        foreach (var group in config.units)
                        {
                            if (string.IsNullOrEmpty(group.unitTypeName)) continue;
                            for (int i = 0; i < group.count; i++)
                            {
                                Human human = InstantiateHuman(group.unitTypeName);
                                if (human != null) members.Add(human);
                            }
                        }

                        if (members.Count == 0) continue;

                        string partyName = string.IsNullOrEmpty(config.partyName) ? "Party" : config.partyName;
                        Party party = GameSession.Instance.CreateParty(partyName, members);
                        spawnedParties.Add(party);
                    }
                }
            }

            // 이 웨이브에서 스폰된 몬스터 전체를, 같은 웨이브의 모든 인류 파티가 공통으로 상대한다
            // — 한 파티가 전멸해도 다른 파티는 계속 진행하고, 그 몬스터들이 전부 죽으면 아직 살아있는
            // 파티마다 각자 독립적으로 웨이브 클리어(생존자 전역 반영)가 트리거된다.
            foreach (var party in spawnedParties)
                party.WaveMonsters = spawnedMonsters;

            Debug.Log($"[WaveSpawner] 몬스터 {spawnedMonsters.Count}마리, 파티 {spawnedParties.Count}개 소환 완료(모드: {spawnMode}).");
        }

        // 공용 타입 탐색: C# 클래스명("Knight") 또는 UnitType.typeName 한글 이름("기사형")으로
        // UnitType 서브클래스를 찾는다. InstantiateMonster/InstantiateHuman이 공유한다.
        private Type ResolveUnitType(string typeName)
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

            return t;
        }

        private UnitType CreateUnitTypeInstance(string typeName)
        {
            Type t = ResolveUnitType(typeName);
            if (t == null)
            {
                Debug.LogWarning($"[WaveSpawner] '{typeName}' 클래스 또는 타입 이름을 찾을 수 없습니다. UnitTypes.cs에 정의되어 있는지 확인하세요.");
                return null;
            }

            try
            {
                return (UnitType)Activator.CreateInstance(t);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WaveSpawner] '{typeName}' 인스턴스 생성 실패: {e.Message}");
                return null;
            }
        }

        private Monster InstantiateMonster(string typeName)
        {
            UnitType unitTypeInstance = CreateUnitTypeInstance(typeName);
            if (unitTypeInstance == null) return null;

            Vector2Int validPos = FindValidSpawnPosition(unitTypeInstance.footprint);

            Monster monster = GameSession.Instance.unitGenerate.GenerateUnitAtPos<Monster>(unitTypeInstance, validPos, targetFloor);
            if (monster != null)
            {
                GameSession.Instance.units.Add(monster);
                GameSession.Instance.RegisterUnitPos(monster, validPos);
            }

            return monster;
        }

        // 파티원(인류)은 항상 시작방(StartRoom)에서 소환한다 — 몬스터의 spawnMode/targetRoomRole
        // 설정과는 독립적이다.
        private Human InstantiateHuman(string typeName)
        {
            UnitType unitTypeInstance = CreateUnitTypeInstance(typeName);
            if (unitTypeInstance == null) return null;

            if (!TryFindPosByRoomRole(RoomRole.StartRoom, unitTypeInstance.footprint, out Vector2Int validPos))
                validPos = GameSession.Instance.unitGenerate.GetRandomFloorPos(unitTypeInstance.footprint, targetFloor);

            Human human = GameSession.Instance.unitGenerate.GenerateUnitAtPos<Human>(unitTypeInstance, validPos, targetFloor);
            if (human != null)
            {
                GameSession.Instance.units.Add(human);
                GameSession.Instance.RegisterUnitPos(human, validPos);
            }

            return human;
        }

        private Vector2Int FindValidSpawnPosition(Vector2 footprint)
        {
            UnitGenerate generator = GameSession.Instance.unitGenerate;

            if (spawnMode == SpawnMode.AroundTransform)
            {
                Vector2Int centerGridPos = new Vector2Int(Mathf.RoundToInt(spawnCenter.x), Mathf.RoundToInt(spawnCenter.y));
                for (int i = 0; i < 30; i++)
                {
                    int offsetX = UnityEngine.Random.Range(-spawnTileRadius, spawnTileRadius + 1);
                    int offsetY = UnityEngine.Random.Range(-spawnTileRadius, spawnTileRadius + 1);
                    Vector2Int cand = centerGridPos + new Vector2Int(offsetX, offsetY);

                    if (generator.IsAreaClear(cand, footprint, targetFloor))
                        return cand;
                }
            }
            else if (spawnMode == SpawnMode.ByRoomRole)
            {
                if (TryFindPosByRoomRole(targetRoomRole, footprint, out Vector2Int pos)) return pos;
            }
            else if (spawnMode == SpawnMode.ByRoomId)
            {
                if (TryFindPosByRoomId(targetRoomId, footprint, out Vector2Int pos)) return pos;
            }

            // 조건에 맞는 공간을 못 찾았을 경우 맵 전체에서 랜덤 빈 타일 반환 (안전망)
            Debug.Log("[WaveSpawner] 유효한 방 또는 위치를 찾지 못하여 맵 전체의 랜덤한 위치에 소환합니다.");
            return generator.GetRandomFloorPos(footprint, targetFloor);
        }

        // ByRoomRole 스폰 로직 — targetRoomRole 인스펙터 설정과 무관하게 임의의 RoomRole로 찾을 수
        // 있도록 파라미터화(인류 파티는 항상 StartRoom을 넘겨 호출한다). (0,0)도 유효한 타일좌표일
        // 수 있어 실패를 값으로 구분하지 않고 bool 반환값으로 구분한다.
        private bool TryFindPosByRoomRole(RoomRole role, Vector2 footprint, out Vector2Int pos)
        {
            List<Vector2Int> chunks = CollectRoomChunks(c => c.roomRole == role);
            if (chunks.Count == 0)
            {
                Debug.LogWarning($"[WaveSpawner] 조건에 맞는 방({role})을 찾지 못했습니다.");
                pos = default;
                return false;
            }
            return TryPickTileInChunks(chunks, footprint, out pos);
        }

        private bool TryFindPosByRoomId(int roomId, Vector2 footprint, out Vector2Int pos)
        {
            List<Vector2Int> chunks = CollectRoomChunks(c => c.roomId == roomId);
            if (chunks.Count == 0)
            {
                Debug.LogWarning($"[WaveSpawner] 조건에 맞는 방({roomId})을 찾지 못했습니다.");
                pos = default;
                return false;
            }
            return TryPickTileInChunks(chunks, footprint, out pos);
        }

        private List<Vector2Int> CollectRoomChunks(Func<Chunks, bool> match)
        {
            var result = new List<Vector2Int>();

            CreateMap cmap = GameSession.Instance.cmap;
            if (cmap == null || cmap.map.floors == null || targetFloor < 0 || targetFloor >= cmap.map.floors.Length)
                return result;

            Floor floor = cmap.map.floors[targetFloor];
            if (floor.chunks == null) return result;

            int chunkW = floor.config.width;
            int chunkH = floor.config.height;
            for (int cx = 0; cx < chunkW; cx++)
            {
                for (int cy = 0; cy < chunkH; cy++)
                {
                    Chunks c = floor.chunks[cx, cy];
                    if (c.chunk == null) continue;
                    if (match(c)) result.Add(new Vector2Int(cx, cy));
                }
            }

            return result;
        }

        private bool TryPickTileInChunks(List<Vector2Int> validRoomChunks, Vector2 footprint, out Vector2Int pos)
        {
            UnitGenerate generator = GameSession.Instance.unitGenerate;

            for (int i = 0; i < 50; i++) // 50번 시도
            {
                Vector2Int chunkPos = validRoomChunks[UnityEngine.Random.Range(0, validRoomChunks.Count)];
                int tx = UnityEngine.Random.Range(0, 8); // 청크 내부 타일 좌표 (0~7)
                int ty = UnityEngine.Random.Range(0, 8);

                Vector2Int globalPos = new Vector2Int(chunkPos.x * 8 + tx, chunkPos.y * 8 + ty);
                if (generator.IsAreaClear(globalPos, footprint, targetFloor))
                {
                    pos = globalPos;
                    return true;
                }
            }

            pos = default;
            return false;
        }
    }
}
