using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks;
using Haare.Util.Logger;

// 점령 파티가 한 방에 세우는 기초 시설 한 벌의 자리 — 방 쪽 문 묶음 하나와 그 안쪽의 목책 자리(DoorSystem.CollectRoomGates).
public struct RoomGate
{
    public Vector3Int Anchor;            // 문 묶음 대표 타일(GetRoomDoorAnchors와 같다) — 이 칸으로 문 오브젝트를 조회한다
    public List<Vector3Int> DoorTiles;   // 문 묶음의 모든 칸 — (x, y) 오름차순
    public float Rotation;               // 문 스프라이트 회전(수평 게이트 90도)
    public List<Vector3Int> FenceTiles;  // 지금 목책을 세울 수 있는 칸(문 줄을 방 안쪽으로 한 칸 민 자리 중 바닥이고 비어 있는 것) — 비어 있으면 세울 목책이 없다
    public List<Vector3Int> FenceSites;  // 목책이 서는 기하학적 자리 전부(문 줄을 방 안쪽으로 한 칸 민 칸 — 이미 목책이 섰든 막혔든 무관) — 방 안쪽을 정할 수 없으면 비어 있다. 문·목책 곁에 서는 일꾼의 자리 기준
    public bool HasDoor;                // 이 자리에 문이 지금 서 있다
    public bool HasFence;                // 이 자리에 목책이 지금 서 있다
}

// 문 시스템(진영 기반 개폐, GameSession 비대화 방지로 분리). 문은 '어느 방에 붙어 있는가'로 묶은 방 쪽 줄(폭 2 → 1×2) 하나가 오브젝트 하나라 게이트당 두 개이고, 체력·개폐·소유 진영·파괴·재설치가 묶음 단위로 처리된다(타일 키 조회는 별칭 등록으로 어느 칸에서나 같은 오브젝트). DoorOwnerFaction은 Room.RoomFaction과 분리돼 파괴 후 재설치로만 바뀐다.
// 기본은 항상 닫힘이고 접근 시도(NotifyApproachAttempt) 순간에만 시각적으로 열리지만 실제 통행 여부는 진영 일치로만 판정한다(IsBlockedByClosedDoor) — 단 보유 진영(플레이어)이 문 정보 패널에서 "열어 둔"(DoorManualOpen) 문은 누구나 지나간다. GameSession은 IObjectResolver로 지연 조회한다 — Construct() 시점엔 생성이 안 끝나 즉시 주입하면 순환 참조가 된다.
public class DoorSystem
{
    public const string DoorTag = "Object/Passable/Door";
    // 목책 — 점령 파티가 문 안쪽에 함께 세우는 인류 기초 시설(코어 점령 v0.2 "인류 문·목책 등"). 문과 같은 방식으로 통행을 막고(보유 진영만 통과) 체력이 0이 되면 철거된다.
    // 문 코드가 가정하는 것(게이트 문턱 타일·방 이탈 판정·진형 문 줄)을 오염시키지 않도록 별도 태그를 쓰고, 체력·소유 진영 필드와 채널링 파괴 경로만 문과 공유한다.
    public const string FenceTag = "Object/Fence";
    // 목책 체력(자리표시자 — 문서가 정하지 않음). 문(300)보다 약하다.
    public const float FenceMaxHp = 200f;

    public const float DoorMaxHp = 300f;
    // 유닛 스탯과 무관한 고정 초당 데미지 — 채널링 중인 유닛 수만큼 자연히 합산된다.
    public const float DoorAttackDamagePerSecond = 20f;

    // 마지막 피해로부터 이 시간(초)이 지나면 회복 시작(GameSession.CoreRegenDelaySeconds와 동일 값).
    public const float DoorRegenDelaySeconds = 5f;
    public const float DoorRegenPerSecond = 10f;

    // 매 프레임 개폐 판정을 돌 대상 캐시(objectGrid 전체 스캔 방지) — 방 쪽 줄(1×2) 묶음당 오브젝트 하나. SpawnDoors/RebuildDoorAt에서 추가, RemoveDoor에서 제거한다.
    private readonly List<InteractableObject> _doors = new List<InteractableObject>();
    // 목책 캐시 — 개폐가 없어 체력 회복과 거점 상실 판정(방의 인류 목책 수)에만 쓴다. SpawnFenceGroup에서 추가, RemoveDoor에서 제거한다.
    private readonly List<InteractableObject> _fences = new List<InteractableObject>();
    // 방마다 방 쪽 문 묶음 대표 타일 — 게이트는 런타임에 안 바뀌어 처음 구한 목록을 재사용한다(철거 대상 탐색이 매 틱 불린다). 맵이 다시 생기면(SpawnDoors) 비운다.
    private readonly Dictionary<Room, List<Vector3Int>> _roomAnchorCache = new Dictionary<Room, List<Vector3Int>>();
    // 문 하나의 스프라이트 전부(짝마다 한 장) — 개폐는 이 배열 전체에 같이 적용한다.
    private readonly System.Collections.Generic.Dictionary<InteractableObject, SpriteRenderer[]> _doorVisuals = new System.Collections.Generic.Dictionary<InteractableObject, SpriteRenderer[]>();

    // 문이 생기거나(최초 배치·재설치) 파괴될 때마다 오른다 — 통행 가능 여부가 바뀌는 유일한 사건이라 경로 판정 메모(RouteAssessment)의 서명으로 쓴다.
    public int StateVersion { get; private set; }

    // UnitFunction.Move가 인접 칸에서 이 문 타일로 넘어가려는 시도를 한 그 프레임에만 채워지는 집합 — UpdateProcess가 매 프레임 끝에 비운다.
    private readonly HashSet<Vector3Int> _approachedThisFrame = new HashSet<Vector3Int>();

    private Sprite _doorOpenSprite;
    private Sprite _doorClosedSprite;

    // UpdateProcess가 매 프레임 개폐 스프라이트를 바꾸므로 GameSession.Initialize가 문을 깔기 전에 await한다.
    public async UniTask LoadAssetsAsync()
    {
        (_doorOpenSprite, _doorClosedSprite) = await UniTask.WhenAll(
            GameAssets.LoadSpriteAsync(AssetKeys.DoorOpenSprite),
            GameAssets.LoadSpriteAsync(AssetKeys.DoorClosedSprite));
    }

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

        _gateTileIndex = null; // 맵이 (재)생성됐다 — 게이트 타일 인덱스는 다음 조회 때 새 지도로 다시 만든다(OccupancySystem 통과 순서)
        _roomAnchorCache.Clear();
        _fences.Clear();
        int doorCount = 0;

        for (int floorIdx = 0; floorIdx < cmap.map.floors.Length; floorIdx++)
        {
            Floor floor = cmap.map.floors[floorIdx];
            if (floor.gates == null) continue;

            foreach (var gate in floor.gates)
            {
                // 기본(회전 0도) 그림은 수직 통로 기준이라 수평 통로는 90도 돌려야 벽 방향이 맞는다.
                float rotation = gate.isHorizontal ? 90f : 0f;

                // 문은 "어느 방에 붙어 있는가"로 묶은 한 줄(1×2)이 오브젝트 하나다 — 게이트당 두 방 쪽에 하나씩.
                foreach (var group in GetGateDoorGroups(gate, floorIdx))
                {
                    var tiles = ToGridTiles(group, floorIdx);
                    if (AnyTileOccupied(tiles)) continue;

                    // 최초 배치 시점의 방 소유 진영을 스냅샷해 고정(이후 방 점령이 바뀌어도 문 소유권은 파괴+재설치로만 변경). 묶음이 붙은 방이 곧 그 문의 방이다.
                    FactionType initialOwner = Session.roomGrid.TryGetValue(tiles[0], out Room initialRoom) && initialRoom != null
                        ? initialRoom.RoomFaction
                        : FactionType.Wild;
                    SpawnDoorGroup(tiles, rotation, initialOwner);
                    doorCount++;
                }
            }
        }

        LogHelper.Log(LogHelper.GAME, $"SpawnDoors: 전체 {cmap.map.floors.Length}개 층에 문 {doorCount}개 배치 완료(방 쪽 줄 단위 묶음, 기본 닫힘).");
    }

    // 게이트의 문 타일을 '그 타일이 실제 속한 방'으로 묶는다(DoorGeometry.GroupByRoom) — GetGateDoorTiles의 줄 순서는 기하학적 규칙일 뿐 방과 무관해 roomGrid로 판정한다.
    public List<DoorGeometry.Group> GetGateDoorGroups(Gate gate, int floorIdx)
    {
        Floor floor = Session.cmap.map.floors[floorIdx];
        return DoorGeometry.GroupByRoom(GetGateDoorTiles(gate, floor.config.chunkSize),
            t => Session.roomGrid.TryGetValue(new Vector3Int(t.x, t.y, floorIdx), out Room room) && room != null ? room.RoomId : -1);
    }

    private static List<Vector3Int> ToGridTiles(DoorGeometry.Group group, int floorIdx)
    {
        var tiles = new List<Vector3Int>(group.Tiles.Count);
        foreach (var t in group.Tiles) tiles.Add(new Vector3Int(t.x, t.y, floorIdx));
        return tiles;
    }

    private bool AnyTileOccupied(List<Vector3Int> tiles)
    {
        foreach (var t in tiles)
            if (Session.objectGrid.ContainsKey(t)) return true;
        return false;
    }

    // SpawnDoors/RebuildDoorAt 공용, 항상 기본 닫힘으로 생성하고 묶음의 모든 타일을 한 오브젝트로 등록한다. ownerFaction은 호출부가 정하며 이 함수는 방 소유권을 조회하지 않는다.
    private void SpawnDoorGroup(List<Vector3Int> tiles, float rotation, FactionType ownerFaction)
    {
        Vector3Int primary = tiles[0];
        string objId = $"Door_{primary.z}_{primary.x}_{primary.y}";
        InteractableObject door = new InteractableObject(
            objId, primary, baseInterest: 0f, baseDanger: 0f,
            tags: new List<string> { DoorTag }, isFullyBlocking: true, doorHp: DoorMaxHp);
        door.DoorOwnerFaction = ownerFaction;
        door.OccupiedTiles = tiles;
        Session.SpawnObject(door, Color.white, rotation);
        if (!Session.objectGrid.TryGetValue(primary, out var placed) || placed != door) return; // 자리가 차 있어 놓지 못했다
        _doors.Add(door);
        StateVersion++;

        // 기본 닫힘 스프라이트를 즉시 적용한다(첫 UpdateProcess를 기다리지 않음) — DoorIsOpenVisual(false)/IsFullyBlocking(true) 기본값과 이미 일치하며, 칸마다 스프라이트 한 장이라 전부에 같이 적용한다.
        var renderers = GetDoorRenderers(door);
        if (renderers != null && _doorClosedSprite != null)
        {
            foreach (var r in renderers) r.sprite = _doorClosedSprite;
            _doorVisuals[door] = renderers;
        }
    }

    // 문 묶음의 스프라이트 전부 — 짝마다 자식(DoorLeafVisual) 한 장씩. 한 칸짜리 문(루트에 스프라이트)이면 그것 하나. 비주얼이 아직 없으면 null.
    private SpriteRenderer[] GetDoorRenderers(InteractableObject door)
    {
        GameObject visual = Session.GetObjectVisual(door.Position);
        if (visual == null) return null;

        var leaves = visual.GetComponentsInChildren<DoorLeafVisual>();
        if (leaves.Length > 0)
        {
            var result = new SpriteRenderer[leaves.Length];
            for (int i = 0; i < leaves.Length; i++) result[i] = leaves[i].Renderer;
            return result;
        }
        SpriteRenderer single = visual.GetComponent<SpriteRenderer>();
        return single != null ? new[] { single } : null;
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
        if (GameSession.Instance == null || (_doors.Count == 0 && _fences.Count == 0)) return;

        foreach (var fence in _fences)
        {
            if (fence.IsCollected || !Session.objectGrid.TryGetValue(fence.Position, out InteractableObject liveFence) || liveFence != fence) continue;
            RegenBarrier(fence);
        }

        foreach (var door in _doors)
        {
            // 파괴돼 별칭 키가 사라진 문은 RemoveDoor가 목록에서 빼지만, 같은 프레임 순서 차이에 대비해 한 번 더 확인한다.
            if (door.IsCollected || !Session.objectGrid.TryGetValue(door.Position, out InteractableObject current) || current != door) continue;

            RegenBarrier(door);
            if (door.DoorAutoOpen) TickAutoClose(door);

            if (!_doorVisuals.TryGetValue(door, out SpriteRenderer[] renderers) || renderers == null || renderers.Length == 0 || renderers[0] == null)
            {
                renderers = GetDoorRenderers(door);
                if (renderers != null) _doorVisuals[door] = renderers;
            }
            if (renderers == null || renderers.Length == 0) continue;

            FactionType ownerFaction = door.DoorOwnerFaction;

            // 묶음의 어느 타일이든 이번 프레임 접근 시도가 있거나 보유 진영 유닛이 서 있으면(통과 중 정지 등) 문 전체가 열림이다. 매 프레임 모든 문을 도는 경로라 AllTiles() 이터레이터 대신 목록을 직접 순회해 할당을 피한다.
            bool shouldBeOpen = door.DoorManualOpen || door.DoorAutoOpen; // 보유 진영이 열어 둔 문(문 제어)과 인류 문 자동 개방 상태는 계속 열림
            var doorTiles = door.OccupiedTiles;
            int tileCount = doorTiles != null ? doorTiles.Count : 1;
            for (int ti = 0; ti < tileCount && !shouldBeOpen; ti++)
            {
                Vector3Int tile = doorTiles != null ? doorTiles[ti] : door.Position;
                shouldBeOpen = _approachedThisFrame.Contains(tile)
                    || (Session.unitGrid.TryGetValue(tile, out Unit occupant) && occupant != null
                        && occupant.Health.hp > 0 && OffenseProcessor.MapToRoomFaction(occupant.FactionBehavior) == ownerFaction);
            }
            if (shouldBeOpen != door.DoorIsOpenVisual)
            {
                door.DoorIsOpenVisual = shouldBeOpen;
                door.IsFullyBlocking = !shouldBeOpen; // "문이 닫혀버리면 벽과 같은 가시성" — 열림/닫힘 공통 규칙, 진영 무관.

                Sprite sprite = shouldBeOpen ? _doorOpenSprite : _doorClosedSprite;
                if (sprite != null) { foreach (var r in renderers) if (r != null) r.sprite = sprite; } // 두 짝이 한 문처럼 같이 열리고 닫힌다
                else LogHelper.Warning(LogHelper.GAME, $"DoorSystem.UpdateProcess: 문 스프라이트가 null입니다 (shouldBeOpen={shouldBeOpen}).");
            }
        }

        _approachedThisFrame.Clear();
    }

    // 문·목책 체력 회복 — 공격 중이면 UnitFunction.OnUpdate가 매 프레임 TimeSinceLastDamaged를 0으로 리셋하므로 회복이 시작되지 않는다.
    private void RegenBarrier(InteractableObject barrier)
    {
        if (barrier.DoorHp >= barrier.DoorMaxHp) return;

        bool wasBeforeDelay = barrier.TimeSinceLastDamaged < DoorRegenDelaySeconds;
        barrier.TimeSinceLastDamaged += Time.deltaTime;
        if (barrier.TimeSinceLastDamaged < DoorRegenDelaySeconds) return;

        barrier.DoorHp = Mathf.Min(barrier.DoorMaxHp, barrier.DoorHp + DoorRegenPerSecond * Time.deltaTime);
        if (wasBeforeDelay)
            Session.GetObjectVisual(barrier.Position)?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);
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
        if (doorFaction.Value == FactionType.Human && HumanAutoControlEnabled) OpenAutomatically(obj);
    }

    // ── 인류 문 자동 개폐(코어 점령 v0.2 5-1·6장) — 인류 소유 문은 평소 닫혀 있고, 인류가 접근하면 실제로 열려 문 앞·뒤 2칸에 어느 진영이든 유닛이 있는 동안 누구나 지나갈 수 있다(후퇴 인류를 따라 몬스터가 들어올 수 있다).
    // 구역이 모두 비면 인류가 문을 닫는다. 플레이어 소유 문은 자동으로 열리지 않는다(클릭 제어만) — 플레이어 문까지 따라 들어올 수 있게 하는 건 문서의 대상이 아니다.
    public static bool HumanAutoControlEnabled => AIConfigLoader.Behavior?.humanDoorAutoControlEnabled ?? true;

    // 열림 상태가 된 뒤 이 시간(초)이 지나기 전에는 닫기 검사를 하지 않는다 — 접근 직후 깜빡임 방지(내부 판단).
    private const float AutoCloseMinOpenSeconds = 0.5f;
    private const float AutoCloseCheckSeconds = 0.25f;
    // 문마다 문 앞·뒤 비움 구역 타일(게이트 조회를 한 번만 하려고 캐시) — 문이 제거되면 비운다.
    private readonly Dictionary<InteractableObject, List<Vector3Int>> _clearanceCache = new Dictionary<InteractableObject, List<Vector3Int>>();

    private void OpenAutomatically(InteractableObject door)
    {
        if (door.DoorAutoOpen) return;
        door.DoorAutoOpen = true;
        door.DoorAutoOpenSince = Time.time;
        door.DoorNextCloseCheck = Time.time + AutoCloseMinOpenSeconds;
        StateVersion++;
    }

    // 구역에 유닛이 없으면 문을 닫는다 — 어느 진영의 유닛이든(은신 포함) 범위 안에 있으면 닫을 수 없다(6장).
    private void TickAutoClose(InteractableObject door)
    {
        float now = Time.time;
        if (now < door.DoorNextCloseCheck) return;
        door.DoorNextCloseCheck = now + AutoCloseCheckSeconds;
        if (now - door.DoorAutoOpenSince < AutoCloseMinOpenSeconds || AnyUnitInDoorClearance(door)) return;

        door.DoorAutoOpen = false;
        StateVersion++;
    }

    // 열어 둔(플레이어 클릭) 문, 자동으로 열린(인류) 문, 또는 그런 문에 연결된 목책 — 진영 판정 없이 누구나 통과한다.
    public static bool IsHeldOpen(InteractableObject obj)
    {
        if (obj == null) return false;
        if (obj.DoorManualOpen || obj.DoorAutoOpen) return true;
        var linked = obj.LinkedGateDoor;
        return linked != null && !linked.IsCollected && (linked.DoorManualOpen || linked.DoorAutoOpen);
    }

    // 문과 목책 — 보유 진영만 지나갈 수 있고 아니면 체력을 깎아 철거해야 하는 통행 장벽이다(같은 체력·소유 진영 필드와 채널링 파괴 경로를 쓴다).
    public static bool IsBarrier(InteractableObject obj)
        => obj != null && obj.Tags != null && (obj.Tags.Contains(DoorTag) || obj.Tags.Contains(FenceTag));

    public static bool IsFence(InteractableObject obj)
        => obj != null && obj.Tags != null && obj.Tags.Contains(FenceTag);

    // UnitFunction.CanMove/AStarMovement.IsTileWalkable이 이동 판정에 직접 호출 — 시각적 개폐와
    // 무관하게 항상 "문·목책 보유 진영 == 유닛 진영"으로만 결정하며, 없으면(파괴됨) 막지 않는다.
    public bool IsBlockedByClosedDoor(Vector3Int pos, Unit unit)
    {
        if (unit == null || !Session.objectGrid.TryGetValue(pos, out InteractableObject obj)) return false;
        if (!IsBarrier(obj)) return false;

        if (IsHeldOpen(obj)) return false; // 열어 둔(문 제어)·자동으로 열린(인류 문) 문과 그 문에 연결된 목책은 누구나 지나간다

        FactionType? doorFaction = obj.DoorOwnerFaction;
        FactionType? myFaction = OffenseProcessor.MapToRoomFaction(unit.FactionBehavior);
        return doorFaction == null || myFaction == null || doorFaction.Value != myFaction.Value;
    }

    // ── 문 제어(코어 점령 v0.2 4·6·19장) — 플레이어가 자기 진영 문을 클릭해 열어 두거나 닫는다. 판정은 DoorControlRules(순수), 여기서는 세션 상태(코어·유닛 위치)를 모아 넘기고 상태를 바꾼다.
    // 이 수동 열림은 플레이어 문 전용이고, 인류 문의 자동 개폐는 위 OpenAutomatically/TickAutoClose가 맡는다.

    // 이 문을 지금 열기/닫기 할 수 있는가. 닫기는 문 앞·뒤 2칸에 유닛이 없어야 한다(어느 진영이든·은신 포함·전투 중에도 가능).
    public DoorControlBlock CheckControl(InteractableObject door, bool wantOpen, FactionType actor = FactionType.Player)
    {
        if (door == null || door.Tags == null || !door.Tags.Contains(DoorTag) || door.IsCollected) return DoorControlBlock.NotPlayerDoor;
        bool coreActive = Session.Core == null || Session.Core.AreFunctionsActive(door.Position.z);
        bool anyUnit = !wantOpen && AnyUnitInDoorClearance(door);
        return DoorControlRules.Check(door.DoorOwnerFaction, actor, coreActive, wantOpen, door.DoorManualOpen, anyUnit);
    }

    // 문 앞·뒤 비움 구역(문 줄 + 통로 방향 앞뒤 2칸)에 살아 있는 유닛이 한 명이라도 있는가 — 아군·적군·야생·표시되지 않은 은신 유닛 모두 센다(unitGrid는 모든 유닛을 담는다).
    public bool AnyUnitInDoorClearance(InteractableObject door)
    {
        if (door == null) return false;
        if (!_clearanceCache.TryGetValue(door, out List<Vector3Int> area))
        {
            if (!TryFindGateAt(door.Position, out Gate gate)) return false;
            var flat = new List<Vector2Int>();
            DoorGeometry.ClearanceTiles(new List<Vector3Int>(door.AllTiles()), gate.isHorizontal, DoorControlRules.ClearanceDepth, flat);
            area = new List<Vector3Int>(flat.Count);
            foreach (var t in flat) area.Add(new Vector3Int(t.x, t.y, door.Position.z));
            _clearanceCache[door] = area;
        }
        foreach (var key in area)
            if (Session.unitGrid.TryGetValue(key, out Unit occupant) && occupant != null && occupant.Health.hp > 0) return true;
        return false;
    }

    // 문을 열어 두거나 닫는다. 성공하면 상태를 바꾸고 경로 판정 메모가 새로 계산되도록 StateVersion을 올린다. 불가 사유를 돌려준다(None = 성공).
    public DoorControlBlock TrySetManualOpen(InteractableObject door, bool open, FactionType actor = FactionType.Player)
    {
        DoorControlBlock block = CheckControl(door, open, actor);
        if (block != DoorControlBlock.None) return block;

        door.DoorManualOpen = open;
        StateVersion++;
        LogHelper.Log(LogHelper.GAME, $"[문 제어] {door.Id}({door.Position.x},{door.Position.y}) {(open ? "열어 둠 — 누구나 통과 가능" : "닫음 — 보유 진영만 통과")}");
        return DoorControlBlock.None;
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
                if (visited.Contains(neighbor) || !IsGatePassableForFaction(g, floorIndex, faction))
                    continue;

                if (neighbor == targetRoomId) return true;
                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return false;
    }

    // 문은 방 쪽 줄(1×2 묶음)마다 독립 오브젝트(소유 진영·파괴 상태가 각자)라 게이트를 지나려면 두 방 쪽 문이 모두 통행 가능해야 한다.
    private bool IsGatePassableForFaction(Gate gate, int floorIndex, FactionType faction)
    {
        var groups = GetGateDoorGroups(gate, floorIndex);
        if (groups.Count == 0) return true; // 문 타일 정보가 없으면 막을 이유 없음

        var fenceTiles = new List<Vector3Int>(2);
        foreach (var group in groups)
        {
            Vector2Int tile = group.Tiles[0];
            var doorPos = new Vector3Int(tile.x, tile.y, floorIndex);
            FactionType? owner = GetDoorOwnerFaction(doorPos);
            bool heldOpen = Session.objectGrid.TryGetValue(doorPos, out InteractableObject doorObj) && IsHeldOpen(doorObj);
            if (owner != null && owner.Value != faction && !heldOpen) return false; // null = 문이 없음(파괴됨) = 이 쪽은 통과 가능, 열어 둔·자동으로 열린 문도 누구나 통과

            // 문 안쪽에 목책이 서 있으면 그것도 통과 조건이다(없거나 파괴됐으면 막지 않는다).
            if (TryGetFenceTiles(gate, group, floorIndex, fenceTiles))
                foreach (var fenceTile in fenceTiles)
                    if (Session.objectGrid.TryGetValue(fenceTile, out InteractableObject fence) && IsFence(fence) && fence.DoorOwnerFaction != faction && !IsHeldOpen(fence)) return false;
        }
        return true;
    }

    // 문이 있는 자리는 오브젝트/몬스터 배치 모두 불가능(이동만 가능) — BuildingManager.CanInstallAt/
    // UnitGenerate.IsAreaClear/RoomConfinedMovement 등이 이 메서드로 문 타일을 걸러낸다.
    public bool IsDoorTile(Vector3Int pos)
    {
        return Session.objectGrid.TryGetValue(pos, out InteractableObject obj) &&
               obj.Tags != null && obj.Tags.Contains(DoorTag);
    }

    // 목책이 서 있는 칸 — 건물을 지을 수 없다(BuildingManager). 문 타일(IsDoorTile)과 달리 방 이탈·진형 문 줄 판정에는 들어가지 않는다.
    public bool IsFenceTile(Vector3Int pos)
        => Session.objectGrid.TryGetValue(pos, out InteractableObject obj) && IsFence(obj);

    // 문·목책 체력이 0이 되면 UnitFunction.OnUpdate가 호출한다(TrapDestroy/코어 공격과 같은 채널링 패턴) — 문은 재설치 전까지 진영 판정 대상이 없어 아무나 통과하고, 목책은 철거되면 그만이다.
    // 1×2 묶음이 오브젝트 하나라 pos가 어느 칸이든 묶음 전체가 사라지며 별칭 키·비주얼·캐시를 모두 정리한다.
    public void RemoveDoor(Vector3Int pos)
    {
        if (!Session.objectGrid.TryGetValue(pos, out InteractableObject door) || !IsBarrier(door)) return;

        bool fence = IsFence(door);
        var tiles = new List<Vector3Int>(door.AllTiles());
        _doors.Remove(door);
        _fences.Remove(door);
        _doorVisuals.Remove(door);
        _clearanceCache.Remove(door);
        StateVersion++;
        Session.CollectObject(pos); // objectGrid의 별칭 키 전부 제거 + 비주얼 파괴
        foreach (var tile in tiles) ClearStaleWallCache(tile);
        LogHelper.Log(LogHelper.GAME, fence
            ? $"RemoveDoor: 목책 {door.Id}({tiles.Count}칸)이 철거됐습니다."
            : $"RemoveDoor: {door.Id}({tiles.Count}칸)이 파괴됐습니다 — 재설치 전까지 아무나 통과 가능.");
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

    // ObjectPlacementController의 문 재설치 모드 전용 — SpawnDoorGroup처럼 기본 닫힘으로 재생성하며, 재설치는 플레이어만 실행하므로 소유 진영은 항상 Player다. pos가 속한 방 쪽 줄(1×2 묶음) 전체를 한 번에 설치한다.
    public void RebuildDoorAt(Vector3Int pos)
    {
        if (!TryGetDoorGroupAt(pos, out DoorGeometry.Group group, out Gate gate)) return;
        var tiles = ToGridTiles(group, pos.z);
        if (AnyTileOccupied(tiles)) return;

        SpawnDoorGroup(tiles, gate.isHorizontal ? 90f : 0f, FactionType.Player);
        LogHelper.Log(LogHelper.GAME, $"RebuildDoorAt: {tiles[0]} 쪽 문 묶음({tiles.Count}칸)을 재설치했습니다(기본 닫힘, 소유 진영: Player).");
    }

    // pos가 속한 방 쪽 줄 묶음과 그 게이트.
    private bool TryGetDoorGroupAt(Vector3Int pos, out DoorGeometry.Group group, out Gate gate)
    {
        group = default;
        if (!TryFindGateAt(pos, out gate)) return false;
        var tile = new Vector2Int(pos.x, pos.y);
        foreach (var g in GetGateDoorGroups(gate, pos.z))
        {
            if (g.Tiles.Contains(tile)) { group = g; return true; }
        }
        return false;
    }

    // 재설치 고스트가 묶음의 모든 칸과 회전을 알아야 한다(PlacementGhost.UpdateTiles — 칸마다 스프라이트 한 장).
    public bool TryGetDoorGroupTiles(Vector3Int pos, List<Vector3Int> tilesOut, out float rotationZ)
    {
        rotationZ = 0f;
        tilesOut.Clear();
        if (!TryGetDoorGroupAt(pos, out DoorGeometry.Group group, out Gate gate)) return false;
        foreach (var t in group.Tiles) tilesOut.Add(new Vector3Int(t.x, t.y, pos.z));
        rotationZ = gate.isHorizontal ? 90f : 0f;
        return true;
    }

    // room의 방 쪽 문 묶음마다 대표 타일(묶음 가운데) 하나 — 점령 파티가 기초 시설로 짓는 인류 문의 자리이자 건설 현장 자리를 고르는 기준이다.
    // 게이트는 런타임에 안 바뀌어 방마다 한 번 구한 목록을 그대로 돌려준다(읽기 전용으로 쓸 것).
    public IReadOnlyList<Vector3Int> GetRoomDoorAnchors(Room room)
    {
        if (room == null) return System.Array.Empty<Vector3Int>();
        if (_roomAnchorCache.TryGetValue(room, out List<Vector3Int> cached)) return cached;

        var anchors = new List<Vector3Int>();
        var cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null || room.Floor < 0 || room.Floor >= cmap.map.floors.Length) return anchors;
        Floor floor = cmap.map.floors[room.Floor];
        if (floor.gates == null) return anchors;
        foreach (var gate in floor.gates)
        {
            if (gate.roomA != room.RoomId && gate.roomB != room.RoomId) continue;
            foreach (var group in GetGateDoorGroups(gate, room.Floor))
            {
                if (group.RoomId != room.RoomId || group.Tiles.Count == 0) continue;
                var mid = group.Tiles[group.Tiles.Count / 2];
                anchors.Add(new Vector3Int(mid.x, mid.y, room.Floor));
            }
        }
        _roomAnchorCache[room] = anchors;
        return anchors;
    }

    // 문 묶음 하나의 목책 후보 자리 — 문 타일을 방 안쪽으로 한 칸 민 칸들이다. 방 안쪽을 정할 수 없으면(양쪽이 같은 방이거나 어느 쪽도 아님) false.
    private bool TryGetFenceTiles(Gate gate, DoorGeometry.Group group, int floorIdx, List<Vector3Int> tilesOut)
    {
        tilesOut.Clear();
        if (group.Tiles.Count == 0 || group.RoomId < 0) return false;

        int roomId = group.RoomId;
        bool InRoom(Vector2Int t) => Session.roomGrid.TryGetValue(new Vector3Int(t.x, t.y, floorIdx), out Room r) && r != null && r.RoomId == roomId;
        if (!DoorGeometry.TryGetInward(gate.isHorizontal, group.Tiles[0], InRoom, out Vector2Int inward)) return false;

        foreach (var t in DoorGeometry.Shifted(group.Tiles, inward)) tilesOut.Add(new Vector3Int(t.x, t.y, floorIdx));
        return true;
    }

    // 목책을 세워도 되는 칸 — 이 방 안의 걸을 수 있는 바닥이고 게이트 문턱이 아니며 다른 오브젝트가 없다.
    private bool IsFenceSiteFree(Room room, Vector3Int tile)
    {
        if (!Session.roomGrid.TryGetValue(tile, out Room owner) || owner != room) return false;
        if (Session.cmap == null || !Session.cmap.IsStaticTileWalkable(tile.z, new Vector2Int(tile.x, tile.y))) return false;
        if (TryGetGateKeyAt(tile, out _)) return false;
        return !Session.objectGrid.ContainsKey(tile);
    }

    // room의 방 쪽 문 묶음마다 기초 시설 한 벌의 자리(문 + 문 안쪽 목책)를 모은다 — 철거 뒤 재설치 청사진과 설치가 같은 목록을 쓴다.
    public void CollectRoomGates(Room room, List<RoomGate> result)
    {
        result.Clear();
        var cmap = Session.cmap;
        if (room == null || cmap == null || cmap.map.floors == null || room.Floor < 0 || room.Floor >= cmap.map.floors.Length) return;
        Floor floor = cmap.map.floors[room.Floor];
        if (floor.gates == null) return;

        var fenceCandidates = new List<Vector3Int>(2);
        foreach (var gate in floor.gates)
        {
            if (gate.roomA != room.RoomId && gate.roomB != room.RoomId) continue;
            foreach (var group in GetGateDoorGroups(gate, room.Floor))
            {
                if (group.RoomId != room.RoomId || group.Tiles.Count == 0) continue;
                var mid = group.Tiles[group.Tiles.Count / 2];
                var anchor = new Vector3Int(mid.x, mid.y, room.Floor);

                var fenceTiles = new List<Vector3Int>(2);
                var fenceSites = new List<Vector3Int>(2);
                bool hasFence = false;
                if (TryGetFenceTiles(gate, group, room.Floor, fenceCandidates))
                {
                    fenceSites.AddRange(fenceCandidates);
                    foreach (var tile in fenceCandidates)
                    {
                        if (Session.objectGrid.TryGetValue(tile, out InteractableObject there))
                        {
                            if (IsFence(there)) hasFence = true;
                            continue;
                        }
                        if (IsFenceSiteFree(room, tile)) fenceTiles.Add(tile);
                    }
                }

                result.Add(new RoomGate
                {
                    Anchor = anchor,
                    DoorTiles = ToGridTiles(group, room.Floor),
                    Rotation = gate.isHorizontal ? 90f : 0f,
                    FenceTiles = fenceTiles,
                    FenceSites = fenceSites,
                    HasDoor = Session.objectGrid.TryGetValue(anchor, out InteractableObject door) && door.Tags != null && door.Tags.Contains(DoorTag),
                    HasFence = hasFence,
                });
            }
        }
    }

    // 인류가 새로 짓는 기초 시설을 한 번에 세운다(코어 점령 v0.2 12장 "기존 문 제어권이 없으면 문을 파괴하고 인류 문을 세운다") — 방 쪽 문 묶음마다 인류 문과 그 안쪽 목책이다.
    // 정상 흐름에선 기존 문이 이미 철거돼 있어(OccupationSystem 철거 단계) 빈 자리에 세울 뿐이고, 인류 문이 남아 있으면 체력만 채우며, 인류가 아닌 문이 남아 있으면(debug 즉시 거점) 부수고 다시 세운다.
    public (int doors, int fences) InstallHumanGates(Room room)
    {
        var gates = new List<RoomGate>();
        CollectRoomGates(room, gates);

        int doors = 0, fences = 0;
        foreach (var g in gates)
        {
            if (g.HasDoor && Session.objectGrid.TryGetValue(g.Anchor, out InteractableObject existing))
            {
                if (existing.DoorOwnerFaction == FactionType.Human) existing.DoorHp = existing.DoorMaxHp;
                else
                {
                    RemoveDoor(g.Anchor);
                    if (!AnyTileOccupied(g.DoorTiles)) { SpawnDoorGroup(g.DoorTiles, g.Rotation, FactionType.Human); doors++; }
                }
            }
            else if (!AnyTileOccupied(g.DoorTiles))
            {
                SpawnDoorGroup(g.DoorTiles, g.Rotation, FactionType.Human);
                doors++;
            }
            else
            {
                LogHelper.Warning(LogHelper.GAME, $"InstallHumanGates: {g.Anchor} 문 자리에 다른 오브젝트가 있어 인류 문을 세우지 못했습니다.");
            }

            // 같은 기초 시설의 문 — 목책이 이 문이 열린 동안 함께 열리도록 연결한다(IsBlockedByClosedDoor).
            InteractableObject gateDoor = Session.objectGrid.TryGetValue(g.Anchor, out InteractableObject d) && d.Tags != null && d.Tags.Contains(DoorTag) ? d : null;
            if (!g.HasFence && g.FenceTiles.Count > 0 && SpawnFenceGroup(g.FenceTiles, FactionType.Human, gateDoor)) fences++;
            else if (g.HasFence && gateDoor != null) LinkExistingFence(g, gateDoor);
        }
        return (doors, fences);
    }

    // 이미 서 있는 목책을 새로(다시) 세운 문에 연결한다.
    private void LinkExistingFence(RoomGate g, InteractableObject gateDoor)
    {
        foreach (var tile in g.FenceSites)
            if (Session.objectGrid.TryGetValue(tile, out InteractableObject fence) && IsFence(fence)) fence.LinkedGateDoor = gateDoor;
    }

    // 목책 묶음(1×2)을 하나의 오브젝트로 세운다 — 문 묶음과 같은 방식으로 모든 칸을 같은 오브젝트의 별칭으로 등록한다. tiles는 (x, y) 오름차순이라 첫 칸이 대표 타일이다.
    private bool SpawnFenceGroup(List<Vector3Int> tiles, FactionType owner, InteractableObject gateDoor = null)
    {
        Vector3Int primary = tiles[0];
        InteractableObject fence = new InteractableObject(
            $"Fence_{primary.z}_{primary.x}_{primary.y}", primary, baseInterest: 0f, baseDanger: 0f,
            tags: new List<string> { FenceTag }, isFullyBlocking: false, doorHp: FenceMaxHp);
        fence.DoorOwnerFaction = owner;
        fence.OccupiedTiles = tiles;
        fence.LinkedGateDoor = gateDoor;
        Session.SpawnObject(fence, Color.white);
        if (!Session.objectGrid.TryGetValue(primary, out var placed) || placed != fence) return false; // 자리가 차 있어 놓지 못했다
        _fences.Add(fence);
        StateVersion++;
        return true;
    }

    // room의 방 쪽 문 묶음 중 owner 소유로 남아 있는(파괴되지 않은) 것의 수 — 인류 기초 시설이 아직 남아 있는지 판단한다.
    public int CountRoomDoors(Room room, FactionType owner)
    {
        int count = 0;
        foreach (var pos in GetRoomDoorAnchors(room))
            if (Session.objectGrid.TryGetValue(pos, out InteractableObject door) && door.Tags != null && door.Tags.Contains(DoorTag)
                && door.DoorHp > 0f && door.DoorOwnerFaction == owner) count++;
        return count;
    }

    // room 안에 owner 소유로 남아 있는(철거되지 않은) 목책의 수 — 문과 함께 인류 기초 시설이 남아 있는지 판단한다(거점 상실 조건).
    public int CountRoomFences(Room room, FactionType owner)
    {
        int count = 0;
        foreach (var fence in _fences)
        {
            if (fence.IsCollected || fence.DoorHp <= 0f || fence.DoorOwnerFaction != owner) continue;
            if (Session.roomGrid.TryGetValue(fence.Position, out Room fenceRoom) && fenceRoom == room) count++;
        }
        return count;
    }

    // room의 방 쪽 문 묶음 중 인류 소유가 아닌 채 서 있는 것 — 점령 파티가 기초 시설을 짓기 전에 실제로 부술 문이다(철거 단계). result는 호출부가 재사용하는 버퍼다.
    public void CollectHostileRoomDoors(Room room, List<InteractableObject> result)
    {
        result.Clear();
        foreach (var pos in GetRoomDoorAnchors(room))
            if (Session.objectGrid.TryGetValue(pos, out InteractableObject door) && door.Tags != null && door.Tags.Contains(DoorTag)
                && door.DoorHp > 0f && door.DoorOwnerFaction != FactionType.Human) result.Add(door);
    }

    // 원래 게이트(통로) 타일 좌표만 재설치를 허용한다.
    public bool IsRepairableDoorTile(Vector3Int pos) => TryFindGateAt(pos, out _);

    // '좁은 통로' = 게이트 문턱 타일(폭 2 고정 통로, 04번 8장). 문 오브젝트가 파괴돼도 성립해야 해서 objectGrid가 아니라 Floor.gates로 판정하고, 게이트는 런타임에 안 바뀌므로 첫 호출 때 타일 → 게이트 키 인덱스를 한 번 만들어 재사용한다.
    private Dictionary<Vector3Int, int> _gateTileIndex;

    public bool TryGetGateKeyAt(Vector3Int pos, out int gateKey)
    {
        if (_gateTileIndex == null) BuildGateTileIndex();
        return _gateTileIndex.TryGetValue(pos, out gateKey);
    }

    private void BuildGateTileIndex()
    {
        _gateTileIndex = new Dictionary<Vector3Int, int>();
        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null) return;

        for (int floorIdx = 0; floorIdx < cmap.map.floors.Length; floorIdx++)
        {
            Floor floor = cmap.map.floors[floorIdx];
            if (floor.gates == null) continue;

            for (int gateIdx = 0; gateIdx < floor.gates.Count; gateIdx++)
            {
                int key = floorIdx * 100000 + gateIdx;
                foreach (var row in GetGateDoorTiles(floor.gates[gateIdx], floor.config.chunkSize))
                    foreach (var tile in row)
                        _gateTileIndex[new Vector3Int(tile.x, tile.y, floorIdx)] = key;
            }
        }
    }

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
