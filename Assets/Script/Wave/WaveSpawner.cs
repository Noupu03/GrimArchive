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
    /// GameSession과 VContainer DI 시스템에 맞춰 웨이브 몬스터(진행 몬스터 엔티티)를 소환하는 스크립트.
    /// CreateMap의 방(Room) 데이터를 연동하여 지정된 위치 또는 방에 몬스터를 스폰합니다.
    /// </summary>
    public class WaveSpawner : Haare.Client.Routine.NativeRoutine
    {
        [Tooltip("소환할 웨이브 데이터")]
        public WaveData waveData;

        public override async Cysharp.Threading.Tasks.UniTask Initialize(System.Threading.CancellationToken cts)
        {
            await base.Initialize(cts);

            // 초기 시점에 WaveData 자동 로드 시도
            if (waveData == null)
            {
                waveData = Resources.Load<WaveData>("WaveData");
                if (waveData == null)
                {
                    Haare.Util.Logger.LogHelper.Warning(Haare.Util.Logger.LogHelper.GAME, "WaveSpawner: Resources/WaveData 를 찾지 못했습니다. 에디터에서 할당해주시거나 위치를 확인해주세요.");
                }
            }
        }

        public void SpawnWave()
        {
            if (waveData == null)
            {
                Debug.LogWarning("[WaveSpawner] waveData 참조가 할당되지 않았습니다.");
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
                        // 몬스터 그룹은 별도 Party 객체 없이 개별로 스폰. 스폰 위치는 몬스터용
                        // waveData.spawnMode/waveData.targetRoomRole/waveData.targetRoomId 설정에 따라 다름
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
                        // 인류 파티는 기획 문서 6.13.23에 기재된 "파티" 단위로 생성
                        // Party 객체를 만들고 파티용 몬스터 스폰 위치에 무조건 할당 (시작방)
                        // (StartRoom)에서 등장한다 고 몬스터 그룹의 스폰 모드와 무관하게 사용됨.
                        // 보스방 반대편에 파티가 떨어지는 상황을 미연에 방지한다.
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

            // 스폰된 몬스터 전체를 파티 객체에 공유
            foreach (var party in spawnedParties)
                party.WaveMonsters = spawnedMonsters;

            Debug.Log($"[WaveSpawner] 몬스터 {spawnedMonsters.Count}마리, 파티 {spawnedParties.Count}팀 소환 완료(모드: {waveData.spawnMode}).");
        }

        private Type ResolveUnitType(string typeName)
        {
            Type t = Type.GetType(typeName);

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
                Debug.LogWarning($"[WaveSpawner] '{typeName}' 클래스 또는 타입 이름을 찾을 수 없습니다.");
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

            Monster monster = GameSession.Instance.unitGenerate.GenerateUnitAtPos<Monster>(unitTypeInstance, validPos, waveData.targetFloor);
            if (monster != null)
            {
                GameSession.Instance.units.Add(monster);
                GameSession.Instance.RegisterUnitPos(monster, validPos);
            }

            return monster;
        }

        private Human InstantiateHuman(string typeName)
        {
            UnitType unitTypeInstance = CreateUnitTypeInstance(typeName);
            if (unitTypeInstance == null) return null;

            if (!TryFindPosByRoomRole(RoomRole.StartRoom, unitTypeInstance.footprint, out Vector2Int validPos))
                validPos = GameSession.Instance.unitGenerate.GetRandomFloorPos(unitTypeInstance.footprint, waveData.targetFloor);

            Human human = GameSession.Instance.unitGenerate.GenerateUnitAtPos<Human>(unitTypeInstance, validPos, waveData.targetFloor);
            if (human != null)
            {
                GameSession.Instance.units.Add(human);
                GameSession.Instance.RegisterUnitPos(human, validPos);
            }

            return human;
        }

        // 0층 사전 스폰(HumanWaveManager 웨이브 시작 전 대기 연출, 2026-07-23 사용자 요청)용 —
        // InstantiateHuman과 같은 생성 로직이지만 시작방을 찾는 대신 호출부가 직접 지정한 위치/층에
        // 놓는다(0층 로비에는 RoomRole.StartRoom 방 단위 구획이 없어 TryFindPosByRoomRole을 못 씀).
        public Human InstantiatePreSpawnHumanAt(string typeName, Vector2Int pos, int floorIdx)
        {
            UnitType unitTypeInstance = CreateUnitTypeInstance(typeName);
            if (unitTypeInstance == null) return null;

            Human human = GameSession.Instance.unitGenerate.GenerateUnitAtPos<Human>(unitTypeInstance, pos, floorIdx);
            if (human != null)
            {
                GameSession.Instance.units.Add(human);
                GameSession.Instance.RegisterUnitPos(human, pos);
            }

            return human;
        }

        private Vector2Int FindValidSpawnPosition(Vector2 footprint)
        {
            UnitGenerate generator = GameSession.Instance.unitGenerate;

            if (waveData.spawnMode == SpawnMode.AroundTransform)
            {
                Vector2Int centerGridPos = new Vector2Int(Mathf.RoundToInt(waveData.spawnCenter.x), Mathf.RoundToInt(waveData.spawnCenter.y));
                for (int i = 0; i < 30; i++)
                {
                    int offsetX = UnityEngine.Random.Range(-waveData.spawnTileRadius, waveData.spawnTileRadius + 1);
                    int offsetY = UnityEngine.Random.Range(-waveData.spawnTileRadius, waveData.spawnTileRadius + 1);
                    Vector2Int cand = centerGridPos + new Vector2Int(offsetX, offsetY);

                    if (generator.IsAreaClear(cand, footprint, waveData.targetFloor))
                        return cand;
                }
            }
            else if (waveData.spawnMode == SpawnMode.ByRoomRole)
            {
                if (TryFindPosByRoomRole(waveData.targetRoomRole, footprint, out Vector2Int pos)) return pos;
            }
            else if (waveData.spawnMode == SpawnMode.ByRoomId)
            {
                if (TryFindPosByRoomId(waveData.targetRoomId, footprint, out Vector2Int pos)) return pos;
            }

            Debug.Log("[WaveSpawner] 유효한 스폰 위치를 찾지 못하여 맵 전체에서 랜덤한 위치를 반환합니다.");
            return generator.GetRandomFloorPos(footprint, waveData.targetFloor);
        }

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
            if (cmap == null || cmap.map.floors == null || waveData.targetFloor < 0 || waveData.targetFloor >= cmap.map.floors.Length)
                return result;

            Floor floor = cmap.map.floors[waveData.targetFloor];
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

            for (int i = 0; i < 50; i++)
            {
                Vector2Int chunkPos = validRoomChunks[UnityEngine.Random.Range(0, validRoomChunks.Count)];
                int tx = UnityEngine.Random.Range(0, 8);
                int ty = UnityEngine.Random.Range(0, 8);

                Vector2Int globalPos = new Vector2Int(chunkPos.x * 8 + tx, chunkPos.y * 8 + ty);
                if (generator.IsAreaClear(globalPos, footprint, waveData.targetFloor))
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
