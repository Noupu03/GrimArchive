using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;

// 문 시스템(진영 기반 개폐, GameSession 비대화 방지 목적 분리). DoorOwnerFaction(보유 진영)은
// Room.RoomFaction과 분리돼 파괴 후 재설치로만 바뀐다. 기본은 항상 닫힘, 접근 시도
// (NotifyApproachAttempt) 순간에만 시각적으로 열리지만 실제 통행 가능 여부는 항상 진영 일치로만
// 판정한다(IsBlockedByClosedDoor). GameSession을 직접 [Inject]하지 않고 IObjectResolver로 지연
// 조회하는 이유: GameSession.Construct() 시점엔 생성이 끝나지 않아 즉시 주입 시 순환 참조가 된다.
public class DoorSystem
{
    public const string DoorTag = "Object/Passable/Door";

    public const float DoorMaxHp = 300f;
    // 유닛 스탯과 무관한 고정 초당 데미지 — 채널링 중인 유닛 수만큼 자연히 합산된다.
    public const float DoorAttackDamagePerSecond = 20f;

    // 마지막 피해로부터 이 시간(초)이 지나면 회복 시작(GameSession.CoreRegenDelaySeconds와 동일 값).
    public const float DoorRegenDelaySeconds = 5f;
    public const float DoorRegenPerSecond = 10f;

    // 매 프레임 개폐 판정을 돌 대상 캐시(objectGrid 전체 스캔 방지) — SpawnDoors/RebuildDoorAt에서 추가, RemoveDoor에서 제거.
    private readonly List<Vector3Int> _doorPositions = new List<Vector3Int>();
    private readonly System.Collections.Generic.Dictionary<Vector3Int, SpriteRenderer> _doorVisuals = new System.Collections.Generic.Dictionary<Vector3Int, SpriteRenderer>();

    // UnitFunction.Move가 인접 칸에서 이 문 타일로 넘어가려는 시도를 한 그 프레임에만 채워지는 집합 — UpdateProcess가 매 프레임 끝에 비운다.
    private readonly HashSet<Vector3Int> _approachedThisFrame = new HashSet<Vector3Int>();

    private Sprite _doorOpenSprite;
    private Sprite _doorClosedSprite;

    private Sprite DoorOpenSprite => SpriteCache.GetOrLoad(ref _doorOpenSprite, "obj/door_open");
    private Sprite DoorClosedSprite => SpriteCache.GetOrLoad(ref _doorClosedSprite, "obj/door_closed");

    private IObjectResolver _resolver;
    private GameSession Session => _cachedSession ??= _resolver.Resolve<GameSession>();
    private GameSession _cachedSession;

    [Inject]
    public void Construct(IObjectResolver resolver)
    {
        _resolver = resolver;
    }

    // CreateMap이 생성 단계에서 이미 기록해 둔 Floor.gates(방 연결 통로)를 재사용해 통로 타일 좌표를 되짚는다.
    public void SpawnDoors()
    {
        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null) return;

        int doorCount = 0;

        for (int floorIdx = 0; floorIdx < cmap.map.floors.Length; floorIdx++)
        {
            Floor floor = cmap.map.floors[floorIdx];
            if (floor.gates == null) continue;

            foreach (var gate in floor.gates)
            {
                // 기본(회전 0도) 그림은 수직 통로 기준이라 수평 통로는 90도 돌려야 벽 방향이 맞는다.
                float rotation = gate.isHorizontal ? 90f : 0f;

                foreach (List<Vector2Int> gateTiles in GetGateDoorTiles(gate, floor.config.chunkSize))
                {
                    foreach (var tilePos in gateTiles)
                    {
                        Vector3Int gridPos = new Vector3Int(tilePos.x, tilePos.y, floorIdx);
                        if (Session.objectGrid.ContainsKey(gridPos)) continue;

                        // 최초 배치 시점의 방 소유 진영을 스냅샷해 고정(이후 방 점령이 바뀌어도 문 소유권은 파괴+재설치로만 변경).
                        FactionType initialOwner = Session.roomGrid.TryGetValue(gridPos, out Room initialRoom) && initialRoom != null
                            ? initialRoom.RoomFaction
                            : FactionType.Wild;
                        SpawnDoorAt(gridPos, rotation, initialOwner);
                        doorCount++;
                    }
                }
            }
        }

        LogHelper.Log(LogHelper.GAME, $"SpawnDoors: 전체 {cmap.map.floors.Length}개 층에 문 {doorCount}개 배치 완료(기본 닫힘).");
    }

    // SpawnDoors/RebuildDoorAt 공용, 항상 기본 닫힘으로 생성. ownerFaction은 호출부가 정하며 이 함수는 방 소유권을 조회하지 않는다.
    private void SpawnDoorAt(Vector3Int gridPos, float rotation, FactionType ownerFaction)
    {
        string objId = $"Door_{gridPos.z}_{gridPos.x}_{gridPos.y}";
        InteractableObject door = new InteractableObject(
            objId, gridPos, baseInterest: 0f, baseDanger: 0f,
            tags: new List<string> { DoorTag }, isFullyBlocking: true, doorHp: DoorMaxHp);
        door.DoorOwnerFaction = ownerFaction;
        Session.SpawnObject(door, Color.white, rotation);
        _doorPositions.Add(gridPos);

        // 기본 닫힘 스프라이트를 즉시 적용(첫 UpdateProcess 틱을 기다리지 않음) — DoorIsOpenVisual
        // 기본값(false)/IsFullyBlocking=true(생성자 인자)와 이미 일치한다.
        GameObject visual = Session.GetObjectVisual(gridPos);
        SpriteRenderer sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (sr != null && DoorClosedSprite != null)
        {
            sr.sprite = DoorClosedSprite;
        }
    }

    // Gate(청크 경계+폭+방향)로부터 문이 놓일 타일 좌표를 계산한다 — CreateMap.Connection.cs의
    // OpenHorizontalPassage/OpenVerticalPassage와 동일 공식으로 같은 타일을 되짚으며, A/B 청크 순서가
    // 방향에 따라 뒤바뀔 수 있어 Min으로 정렬한다. 반환값 [A쪽 문턱 줄, B쪽 문턱 줄]은 MapRandering의
    // 점령 색칠 제외 판정도 재사용하므로 public static.
    public static List<Vector2Int>[] GetGateDoorTiles(Gate gate, int chunkSize)
    {
        var tilesA = new List<Vector2Int>();
        var tilesB = new List<Vector2Int>();
        int start = (chunkSize - gate.width) / 2;

        if (gate.isHorizontal)
        {
            int leftChunkX = Mathf.Min(gate.chunkAX, gate.chunkBX);
            int doorWorldXA = leftChunkX * chunkSize + chunkSize - 1; // 왼쪽 청크의 마지막 칸
            int doorWorldXB = (leftChunkX + 1) * chunkSize; // 오른쪽(문턱 너머) 청크의 첫 칸
            int chunkY = gate.chunkAY; // 수평 게이트는 두 청크가 같은 행(chunkY == chunkBY)
            for (int i = 0; i < gate.width; i++)
            {
                tilesA.Add(new Vector2Int(doorWorldXA, chunkY * chunkSize + start + i));
                tilesB.Add(new Vector2Int(doorWorldXB, chunkY * chunkSize + start + i));
            }
        }
        else
        {
            int bottomChunkY = Mathf.Min(gate.chunkAY, gate.chunkBY);
            int doorWorldYA = bottomChunkY * chunkSize + chunkSize - 1; // 아래쪽 청크의 마지막 칸
            int doorWorldYB = (bottomChunkY + 1) * chunkSize; // 위쪽 청크의 첫 칸
            int chunkX = gate.chunkAX; // 수직 게이트는 두 청크가 같은 열(chunkX == chunkBX)
            for (int i = 0; i < gate.width; i++)
            {
                tilesA.Add(new Vector2Int(chunkX * chunkSize + start + i, doorWorldYA));
                tilesB.Add(new Vector2Int(chunkX * chunkSize + start + i, doorWorldYB));
            }
        }

        return new[] { tilesA, tilesB };
    }

    // 매 프레임 문의 시각 상태(스프라이트/시야 차단)만 갱신 — 통행 가능 여부는 IsBlockedByClosedDoor가
    // 그 순간 진영 일치로 판정하는 순수 코스메틱이다(방 바닥과 같은 진영 틴트를 쓰면 안 보여 기본 색 유지).
    public void UpdateProcess()
    {
        if (GameSession.Instance == null || _doorPositions.Count == 0) return;

        foreach (var pos in _doorPositions)
        {
            if (!Session.objectGrid.TryGetValue(pos, out InteractableObject door)) continue;

            // 공격 중이면 UnitFunction.OnUpdate가 매 프레임 TimeSinceLastDamaged를 0으로 리셋하므로 여기 도달하지 않는다.
            if (door.DoorHp < door.DoorMaxHp)
            {
                bool wasBeforeDelay = door.TimeSinceLastDamaged < DoorRegenDelaySeconds;
                door.TimeSinceLastDamaged += Time.deltaTime;
                if (door.TimeSinceLastDamaged >= DoorRegenDelaySeconds)
                {
                    door.DoorHp = Mathf.Min(door.DoorMaxHp, door.DoorHp + DoorRegenPerSecond * Time.deltaTime);
                    if (wasBeforeDelay)
                        Session.GetObjectVisual(pos)?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);
                }
            }

            if (!_doorVisuals.TryGetValue(pos, out SpriteRenderer sr) || sr == null)
            {
                GameObject visual = Session.GetObjectVisual(pos);
                sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
                if (sr != null) _doorVisuals[pos] = sr;
            }
            if (sr == null) continue;

            FactionType ownerFaction = door.DoorOwnerFaction;

            // 접근 시도(이번 프레임 NotifyApproachAttempt) 또는 문 타일 위에 이미 보유 진영 유닛이
            // 서 있으면(통과 중 정지 등) 열림으로 본다.
            bool unitOnDoor = Session.unitGrid.TryGetValue(pos, out Unit occupant) && occupant != null
                && occupant.Health.hp > 0 && OffenseProcessor.MapToRoomFaction(occupant.FactionBehavior) == ownerFaction;
            bool shouldBeOpen = _approachedThisFrame.Contains(pos) || unitOnDoor;
            if (shouldBeOpen != door.DoorIsOpenVisual)
            {
                door.DoorIsOpenVisual = shouldBeOpen;
                door.IsFullyBlocking = !shouldBeOpen; // "문이 닫혀버리면 벽과 같은 가시성" — 열림/닫힘 공통 규칙, 진영 무관.

                Sprite sprite = shouldBeOpen ? DoorOpenSprite : DoorClosedSprite;
                if (sprite != null) sr.sprite = sprite;
                else LogHelper.Warning(LogHelper.GAME, $"DoorSystem.UpdateProcess: 문 스프라이트가 null입니다 (shouldBeOpen={shouldBeOpen}).");
            }
        }

        _approachedThisFrame.Clear();
    }

    // Room.RoomFaction을 실시간 조회하지 않고 문 자신의 DoorOwnerFaction(생성/재설치 시점 고정)을 그대로 읽는다.
    public FactionType? GetDoorOwnerFaction(Vector3Int pos)
    {
        if (!Session.objectGrid.TryGetValue(pos, out InteractableObject door) || door.Tags == null || !door.Tags.Contains(DoorTag))
            return null;
        return door.DoorOwnerFaction;
    }

    // UnitFunction.Move가 인접 칸에서 이 문 타일로 넘어가려는 시도를 할 때마다(성공 여부 무관) 호출한다.
    public void NotifyApproachAttempt(Vector3Int pos, Unit unit)
    {
        if (unit == null) return;
        if (!Session.objectGrid.TryGetValue(pos, out InteractableObject obj) || obj.Tags == null || !obj.Tags.Contains(DoorTag)) return;

        FactionType? doorFaction = obj.DoorOwnerFaction;
        FactionType? myFaction = OffenseProcessor.MapToRoomFaction(unit.FactionBehavior);
        if (doorFaction == null || myFaction == null || doorFaction.Value != myFaction.Value) return;

        _approachedThisFrame.Add(pos);
    }

    // UnitFunction.CanMove/AStarMovement.IsTileWalkable이 이동 판정에 직접 호출 — 시각적 개폐와
    // 무관하게 항상 "문 보유 진영 == 유닛 진영"으로만 결정하며, 문이 없으면(파괴됨) 막지 않는다.
    public bool IsBlockedByClosedDoor(Vector3Int pos, Unit unit)
    {
        if (unit == null || !Session.objectGrid.TryGetValue(pos, out InteractableObject obj)) return false;
        if (obj.Tags == null || !obj.Tags.Contains(DoorTag)) return false;

        FactionType? doorFaction = obj.DoorOwnerFaction;
        FactionType? myFaction = OffenseProcessor.MapToRoomFaction(unit.FactionBehavior);
        return doorFaction == null || myFaction == null || doorFaction.Value != myFaction.Value;
    }

    // 이동 명령 도달성 판정 — 점령 여부와 무관하게 "그 진영이 통행 가능한 문"(IsBlockedByClosedDoor와
    // 동일 기준)만 거쳐 도달 가능한 방이면 허용한다. 타일 판정을 방 단위 그래프로 그대로 확장한 것이라
    // "명령은 허용됐는데 실제로는 막힘" 같은 불일치가 없다.
    public bool CanFactionReachRoom(FactionType faction, int floorIndex, int fromRoomId, int targetRoomId)
    {
        if (fromRoomId < 0 || targetRoomId < 0) return false;
        if (fromRoomId == targetRoomId) return true;

        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null || floorIndex < 0 || floorIndex >= cmap.map.floors.Length) return false;
        Floor floor = cmap.map.floors[floorIndex];
        if (floor.gates == null || floor.gates.Count == 0) return false;

        var visited = new HashSet<int> { fromRoomId };
        var queue = new Queue<int>();
        queue.Enqueue(fromRoomId);

        while (queue.Count > 0)
        {
            int room = queue.Dequeue();

            foreach (Gate g in floor.gates)
            {
                if (g.roomA != room && g.roomB != room) continue;
                int neighbor = g.roomA == room ? g.roomB : g.roomA;
                if (visited.Contains(neighbor) || !IsGatePassableForFaction(g, floor.config.chunkSize, floorIndex, faction))
                    continue;

                if (neighbor == targetRoomId) return true;
                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return false;
    }

    // 게이트 폭 전체가 같은 시점에 함께 스폰/파괴되므로 대표 타일 하나만 확인해도 충분하다.
    private bool IsGatePassableForFaction(Gate gate, int chunkSize, int floorIndex, FactionType faction)
    {
        var tileRows = GetGateDoorTiles(gate, chunkSize);
        if (tileRows.Length == 0 || tileRows[0].Count == 0) return true; // 문 타일 정보가 없으면 막을 이유 없음

        Vector2Int tile = tileRows[0][0];
        FactionType? owner = GetDoorOwnerFaction(new Vector3Int(tile.x, tile.y, floorIndex));
        return owner == null || owner.Value == faction; // null = 문이 없음(파괴됨) = 통과 가능
    }

    // 문이 있는 자리는 오브젝트/몬스터 배치 모두 불가능(이동만 가능) — BuildingManager.CanInstallAt/
    // UnitGenerate.IsAreaClear/RoomConfinedMovement 등이 이 메서드로 문 타일을 걸러낸다.
    public bool IsDoorTile(Vector3Int pos)
    {
        return Session.objectGrid.TryGetValue(pos, out InteractableObject obj) &&
               obj.Tags != null && obj.Tags.Contains(DoorTag);
    }

    // 문 체력이 0이 되면 UnitFunction.OnUpdate가 호출한다(TrapDestroy/코어 공격과 동일한 채널링 패턴) —
    // 재설치 전까지는 진영 판정 대상 자체가 없어 아무나 통과 가능해진다.
    public void RemoveDoor(Vector3Int pos)
    {
        _doorPositions.Remove(pos);
        _doorVisuals.Remove(pos);
        Session.CollectObject(pos); // objectGrid 제거 + 비주얼 파괴
        ClearStaleWallCache(pos);
        LogHelper.Log(LogHelper.GAME, $"RemoveDoor: {pos} 위치의 문이 파괴됐습니다 — 재설치 전까지 아무나 통과 가능.");
    }

    // 두 진영 FactionData.discoveredMap과 모든 Human personalMap에 벽(2)으로 캐시된 pos 위치를 미탐사(0)로 되돌린다.
    private void ClearStaleWallCache(Vector3Int pos)
    {
        ClearStaleWallCache(Unit.humanFactionData, pos);
        ClearStaleWallCache(Unit.monsterFactionData, pos);

        foreach (var unit in Session.units)
            if (unit is Human human && human != null)
                human.personalMap.ClearWallCache(pos);
    }

    private static void ClearStaleWallCache(FactionData data, Vector3Int pos)
    {
        if (data?.discoveredMap == null || pos.z < 0 || pos.z >= data.discoveredMap.Length) return;
        int[,] floorMap = data.discoveredMap[pos.z];
        if (floorMap == null || pos.x < 0 || pos.x >= floorMap.GetLength(0) || pos.y < 0 || pos.y >= floorMap.GetLength(1)) return;
        if (floorMap[pos.x, pos.y] == 2) floorMap[pos.x, pos.y] = 0;
    }

    // ObjectPlacementController의 문 재설치 모드 전용, SpawnDoorAt과 동일하게 기본 닫힘으로 재생성하며
    // 재설치는 오직 플레이어만 실행하므로 소유 진영은 항상 Player로 고정된다.
    public void RebuildDoorAt(Vector3Int pos)
    {
        if (Session.objectGrid.ContainsKey(pos)) return;
        if (!TryFindGateAt(pos, out Gate gate)) return;

        float rotation = gate.isHorizontal ? 90f : 0f;
        SpawnDoorAt(pos, rotation, FactionType.Player);
        LogHelper.Log(LogHelper.GAME, $"RebuildDoorAt: {pos}에 문을 재설치했습니다(기본 닫힘, 소유 진영: Player).");
    }

    // 원래 게이트(통로) 타일 좌표만 재설치를 허용한다.
    public bool IsRepairableDoorTile(Vector3Int pos) => TryFindGateAt(pos, out _);

    // IsRepairableDoorTile/RebuildDoorAt 공용 — pos가 어느 게이트의 문턱 타일인지 되짚는다.
    private bool TryFindGateAt(Vector3Int pos, out Gate gate)
    {
        gate = default;
        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null || pos.z < 0 || pos.z >= cmap.map.floors.Length) return false;
        Floor floor = cmap.map.floors[pos.z];
        if (floor.gates == null) return false;

        foreach (var g in floor.gates)
        {
            foreach (var row in GetGateDoorTiles(g, floor.config.chunkSize))
            {
                foreach (var tile in row)
                {
                    if (tile.x == pos.x && tile.y == pos.y) { gate = g; return true; }
                }
            }
        }
        return false;
    }
}
