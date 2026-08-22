using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;

// 문 시스템(2026-07-27~28 최초 구현, 2026-08-22 진영 기반 개폐로 전면 개편 — 기초문서.md 피드백
// "문은 진영별로 색상을 다르게 하되, 기본적으로 닫혀있게 해줘. 보유 진영의 유닛만 지나갈 수 있고,
// (지나갈때만 열렸다가 닫힘). 그게 아니라면, 공격해서 파괴해야 해.") — 모든 방과 방 사이 통로에 문을
// 깔고, 각 문 타일이 InteractableObject.DoorOwnerFaction(생성/재설치 시점에 고정되는 값)을 "보유
// 진영"으로 삼는다. 2026-08-22 후속 사용자 요청("점령으로 인해서 문의 소유권을 바뀌지 않아. 부수고
// 다시 재설치하는게 원칙임")으로 방 소유권(Room.RoomFaction)과 완전히 분리됐다 — 코어 파괴로 방
// 주인이 바뀌어도 그 방에 붙은 기존 문들의 소유권은 그대로 남고, 오직 파괴 후 재설치로만 바뀐다.
// 기본은 항상 닫힘(시야 차단) — 보유 진영의 유닛이 인접 칸에서 문 타일로
// 넘어가려는 시도(NotifyApproachAttempt, 2026-08-22 재조정) 순간에만 시각적으로 열리고, 그 시도가
// 끊기면(다음 프레임에 재시도가 없으면) 다시 닫힌다. 실제 통행 가능 여부는 이 시각 상태와 무관하게
// 항상 "진영 일치"로만 결정된다(UnitFunction.CanMove/AStarMovement.IsTileWalkable이 매 이동 시도마다
// 확인) — 다른 진영은 문을 파괴(DoorHp, RemoveDoor)해야만 지나갈 수 있다. 원안의 "진영별 색상"은
// 2026-08-22 후속 피드백("문색깔과 방 진영색이 동일해서 아예 안 보여")으로 걷어냈다 — 문은 항상
// 기본 색(흰색)이고, 소유 진영은 클릭 시 정보 패널(BuildingControlPanel.ShowForObject)로 확인한다.
// GameSession이 지나치게 커지는 것을 막기 위해 분리했다(2026-08-20, InputManager 배치 모드 3종
// 분리와 동일한 이유·같은 방식).
//
// GameSession을 직접 [Inject]하지 않고 UnitRegistry와 동일한 지연 조회 패턴(IObjectResolver.Resolve
// 를 첫 사용 시점까지 미룸)을 쓴다 — GameSession.Construct()가 이 클래스를 파라미터로 받는 시점에는
// 아직 GameSession 자신의 생성이 끝나지 않아 즉시 주입받으면 순환 참조가 되기 때문(그 시점엔
// GameSession.Instance도 아직 null). roomGrid/cmap/objectGrid/unitGrid/SpawnObject/GetObjectVisual/
// RegisterUnitPos 등 필요한 것은 전부 GameSession의 기존 공개 API로만 접근한다.
//
// 외부 호출부(GameSession이 그대로 얇게 위임): IsDoorTile(BuildingManager/RoomConfinedMovement/
// NavigationFSMState/Unit/DefenseSystem/UnitGenerate 등 다수), GetGateDoorTiles(정적,
// MonsterDefensePlacementSystem 등), IsBlockedByClosedDoor(UnitFunction.CanMove/AStarMovement.
// IsTileWalkable, 신규 — 이동 판정에 직접 사용).
public class DoorSystem
{
    public const string DoorTag = "Object/Passable/Door";

    // 문도 방어건물화(기초문서.md 피드백, 2026-08-22) — 자리표시자 값(플레이 테스트 후 조정). 물리공격력
    // 40 기준 파괴 배율(ExplorationMath.TrapDestroyDamagePerSecondPerAttack=0.5)을 그대로 적용하면
    // 약 5초 만에 파괴된다. 2026-08-22 사용자 요청 "문의 체력을 3배로 늘려줘" — 100 → 300(약 15초).
    public const float DoorMaxHp = 300f;
    // 문 공격 데미지 고정 초당 비율(2026-08-22 사용자 요청 "코어 공격을... 초당으로 다는 형식으로
    // 바꿔줘... 문도 동일") — GameSession.CoreAttackDamagePerSecond와 동일한 설계: 유닛 스탯과
    // 무관하게 채널링 중인 유닛 1명당 고정 초당 데미지, 여러 명이 동시에 공격하면 자연히 합산된다.
    // 자리표시자 20은 기존 물리공격력 40 기준 수치와 동일하게 맞춰 1명 공격 시 파괴 시간(약 15초)이
    // 그대로 유지되게 했다 — 플레이 테스트 후 조정.
    public const float DoorAttackDamagePerSecond = 20f;

    // 문 전체 위치 목록(2026-08-22 신규) — 매 프레임 개폐 판정을 돌 대상. SpawnDoors/RebuildDoorAt에서
    // 추가하고 RemoveDoor에서 제거한다(objectGrid를 매 프레임 전체 스캔하지 않기 위한 캐시).
    private readonly List<Vector3Int> _doorPositions = new List<Vector3Int>();
    private readonly System.Collections.Generic.Dictionary<Vector3Int, SpriteRenderer> _doorVisuals = new System.Collections.Generic.Dictionary<Vector3Int, SpriteRenderer>();

    // 진영 개폐 시각 트리거(2026-08-22 재조정, 사용자 요청 "자기 진영 문 1칸 접근시 열리는 형식이
    // 아닌, 문 인접 칸에서 문에 접근 시도시 열리는 방식으로") — 단순 반경 내 존재 여부(정적 위치)
    // 대신, UnitFunction.Move가 인접 칸에서 이 문 타일로 넘어가려는 시도를 한 그 프레임에만 채워지는
    // 집합. UpdateProcess가 매 프레임 끝에 비운다(1프레임 지연은 시각 연출이라 체감상 문제 없음).
    private readonly HashSet<Vector3Int> _approachedThisFrame = new HashSet<Vector3Int>();

    private Sprite _doorOpenSprite;
    private Sprite _doorClosedSprite;

    private Sprite DoorOpenSprite => _doorOpenSprite ??= Resources.Load<Sprite>("obj/door_open");
    private Sprite DoorClosedSprite => _doorClosedSprite ??= Resources.Load<Sprite>("obj/door_closed");

    private IObjectResolver _resolver;
    private GameSession Session => _cachedSession ??= _resolver.Resolve<GameSession>();
    private GameSession _cachedSession;

    [Inject]
    public void Construct(IObjectResolver resolver)
    {
        _resolver = resolver;
    }

    // 문 시스템(2026-07-27, 사용자 요청 "모든 방과 방 사이 통로에 문이 일렬로 설치") — CreateMap이
    // 생성 단계에서 이미 기록해 둔 Floor.gates(방 연결 통로: 청크 좌표+폭+수평/수직)를 그대로 재사용해
    // 실제 통로 타일 좌표를 되짚는다. 새로 통로를 탐색하는 로직을 만들 필요가 없다 — 모든 층을 순회.
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
                // door_open.png/door_closed.png를 직접 보고 판단(사용자 요청) — 기본(회전 0도) 그림은
                // 수직 통로(isHorizontal=false) 기준이라, 수평 통로에서는 90도 돌려야 벽 방향과 맞는다.
                float rotation = gate.isHorizontal ? 90f : 0f;

                foreach (List<Vector2Int> gateTiles in GetGateDoorTiles(gate))
                {
                    foreach (var tilePos in gateTiles)
                    {
                        Vector3Int gridPos = new Vector3Int(tilePos.x, tilePos.y, floorIdx);
                        if (Session.objectGrid.ContainsKey(gridPos)) continue;

                        // 문 소유 진영 고정(2026-08-22) — 최초 배치 시점의 방 소유 진영을 스냅샷해
                        // 고정한다(이후 방 점령이 바뀌어도 문 소유권은 그대로 — 파괴+재설치로만 변경).
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

    // SpawnDoors/RebuildDoorAt 공용 — 항상 기본적으로 닫힌 상태로 문을 생성한다(기초문서.md 피드백,
    // 2026-08-22 "기본적으로 닫혀있게 해줘"). 회전은 게이트 방향에 따라 고정 — 예전엔 통로 폭 전체에
    // 타일마다 0/180도를 번갈아 적용해 "양쪽으로 열어젖힌 이중문" 느낌을 냈지만, 이제 타일마다 색상도
    // 다르고(보유 진영별) 개별적으로 열리고 닫히므로 그 장식용 교대 회전은 걷어냈다(자리표시자,
    // 필요하면 재도입). ownerFaction은 호출부가 정한다(SpawnDoors=배치 시점 방 소유 진영 스냅샷,
    // RebuildDoorAt=재설치한 플레이어 진영 고정) — 이 함수 자신은 방 소유권을 조회하지 않는다.
    private void SpawnDoorAt(Vector3Int gridPos, float rotation, FactionType ownerFaction)
    {
        string objId = $"Door_{gridPos.z}_{gridPos.x}_{gridPos.y}";
        InteractableObject door = new InteractableObject(
            objId, gridPos, baseInterest: 0f, baseDanger: 0f,
            tags: new List<string> { DoorTag }, isFullyBlocking: true, doorHp: DoorMaxHp);
        door.DoorOwnerFaction = ownerFaction;
        Session.SpawnObject(door, Color.white, rotation);
        _doorPositions.Add(gridPos);

        // 기본 닫힘 스프라이트를 즉시 적용(첫 UpdateProcess 틱을 기다리지 않음) — door.DoorIsOpenVisual
        // 기본값(false)과 IsFullyBlocking=true(생성자 인자)는 이미 "닫힘"과 일치한다.
        GameObject visual = Session.GetObjectVisual(gridPos);
        SpriteRenderer sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (sr != null && DoorClosedSprite != null)
        {
            sr.sprite = DoorClosedSprite;
        }
    }

    // Gate(청크 경계 + 폭 + 방향)로부터 실제 문이 놓일 타일 좌표 목록을 계산한다. CreateMap.Connection.cs의
    // OpenHorizontalPassage/OpenVerticalPassage가 통로를 깎을 때 쓴 것과 똑같은 공식(중앙 정렬,
    // (8-width)/2부터 width칸)을 재사용해 정확히 같은 타일들을 되짚는다 — chunkAX/BX(또는 AY/BY) 중
    // 어느 쪽이 A/B로 기록됐는지는 방향(왼쪽/오른쪽, 아래/위)에 따라 뒤바뀔 수 있어 Min으로 왼쪽·아래
    // 청크를 먼저 찾는다.
    //
    // 통로 양 끝(A쪽 청크의 마지막 칸 / B쪽 청크의 첫 칸) 두 줄을 모두 반환한다(2026-07-28, "문을
    // 양쪽에 달자"). 반환값은 [A쪽 문턱 줄, B쪽 문턱 줄] 순서의 배열. MapRandering.ApplyOccupationTint/
    // ChangeRoomColor가 "문이 있는 바닥은 점령 색칠 제외"를 위해 그대로 재사용하므로 public static.
    public static List<Vector2Int>[] GetGateDoorTiles(Gate gate)
    {
        var tilesA = new List<Vector2Int>();
        var tilesB = new List<Vector2Int>();
        int start = (8 - gate.width) / 2;

        if (gate.isHorizontal)
        {
            int leftChunkX = Mathf.Min(gate.chunkAX, gate.chunkBX);
            int doorWorldXA = leftChunkX * 8 + 7; // 왼쪽 청크의 마지막 칸
            int doorWorldXB = (leftChunkX + 1) * 8; // 오른쪽(문턱 너머) 청크의 첫 칸
            int chunkY = gate.chunkAY; // 수평 게이트는 두 청크가 같은 행(chunkY == chunkBY)
            for (int i = 0; i < gate.width; i++)
            {
                tilesA.Add(new Vector2Int(doorWorldXA, chunkY * 8 + start + i));
                tilesB.Add(new Vector2Int(doorWorldXB, chunkY * 8 + start + i));
            }
        }
        else
        {
            int bottomChunkY = Mathf.Min(gate.chunkAY, gate.chunkBY);
            int doorWorldYA = bottomChunkY * 8 + 7; // 아래쪽 청크의 마지막 칸
            int doorWorldYB = (bottomChunkY + 1) * 8; // 위쪽 청크의 첫 칸
            int chunkX = gate.chunkAX; // 수직 게이트는 두 청크가 같은 열(chunkX == chunkBX)
            for (int i = 0; i < gate.width; i++)
            {
                tilesA.Add(new Vector2Int(chunkX * 8 + start + i, doorWorldYA));
                tilesB.Add(new Vector2Int(chunkX * 8 + start + i, doorWorldYB));
            }
        }

        return new[] { tilesA, tilesB };
    }

    // 문 진영 개폐(기초문서.md 피드백, 2026-08-22, 접근 시도 기반으로 재조정) — 매 프레임 모든 문의
    // 시각 상태(스프라이트/시야 차단)를 갱신한다. 통행 가능 여부 자체는 이 값과 무관하게
    // IsBlockedByClosedDoor가 항상 그 순간의 진영 일치로 판정한다 — 시각적으로 닫혀 보여도 보유
    // 진영은 이미 통과 가능하고, 반대로 시각적으로 열려 보인다고 다른 진영이 지나갈 수 있게 되는
    // 것도 아니다(순수 코스메틱).
    // 문 색상 진영 틴트 제거(2026-08-22 후속 사용자 요청 "문색깔과 방 진영색이 동일해서 아예 안
    // 보여. 문 색상은 바꾸지 않고 그대로 두자") — 방 바닥 타일도 같은 GetRoomOwnerColor로 칠해지다
    // 보니 문이 방과 같은 색으로 묻혀 안 보이는 문제가 있었다. 이제 문은 스폰 시 지정한 기본 색
    // (Color.white, SpawnDoorAt 참고)을 그대로 유지하고, 소유 진영은 대신 클릭 시 뜨는 오브젝트
    // 정보 패널(BuildingControlPanel.ShowForObject)로 확인한다.
    public void UpdateProcess()
    {
        if (GameSession.Instance == null || _doorPositions.Count == 0) return;

        foreach (var pos in _doorPositions)
        {
            if (!Session.objectGrid.TryGetValue(pos, out InteractableObject door)) continue;

            if (!_doorVisuals.TryGetValue(pos, out SpriteRenderer sr) || sr == null)
            {
                GameObject visual = Session.GetObjectVisual(pos);
                sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
                if (sr != null) _doorVisuals[pos] = sr;
            }
            if (sr == null) continue;

            FactionType ownerFaction = door.DoorOwnerFaction;

            // 접근 시도(이번 프레임 NotifyApproachAttempt) 또는 이미 문 타일 위에 보유 진영 유닛이
            // 서 있는 경우(통과 도중 정지 등 방어적 케이스) 열림으로 본다.
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

    // 문의 소유 진영 — 2026-08-22 사용자 요청 "점령으로 인해서 문의 소유권을 바뀌지 않아. 부수고
    // 다시 재설치하는게 원칙임"으로, 방 소유권(Room.RoomFaction)을 실시간 조회하던 방식에서 문
    // 오브젝트 자신이 들고 있는 InteractableObject.DoorOwnerFaction(생성/재설치 시점에 고정)을 그대로
    // 읽는 방식으로 바뀌었다 — 방이 코어 파괴로 점령당해도 그 방의 문은 소유권이 그대로 유지되고,
    // 오직 파괴 후 재설치(RebuildDoorAt)로만 바뀐다.
    public FactionType? GetDoorOwnerFaction(Vector3Int pos)
    {
        if (!Session.objectGrid.TryGetValue(pos, out InteractableObject door) || door.Tags == null || !door.Tags.Contains(DoorTag))
            return null;
        return door.DoorOwnerFaction;
    }

    // 문 개폐 시각 트리거 진입점(2026-08-22 재조정) — UnitFunction.Move가 인접 칸에서 이 문 타일로
    // 넘어가려는 시도를 할 때마다(이동 성공 여부와 무관하게) 호출한다. 문이 아니거나 접근한 유닛의
    // 진영이 문 보유 진영과 다르면(어차피 통과 못 하므로) 무시한다.
    public void NotifyApproachAttempt(Vector3Int pos, Unit unit)
    {
        if (unit == null) return;
        if (!Session.objectGrid.TryGetValue(pos, out InteractableObject obj) || obj.Tags == null || !obj.Tags.Contains(DoorTag)) return;

        FactionType? doorFaction = obj.DoorOwnerFaction;
        FactionType? myFaction = OffenseProcessor.MapToRoomFaction(unit.FactionBehavior);
        if (doorFaction == null || myFaction == null || doorFaction.Value != myFaction.Value) return;

        _approachedThisFrame.Add(pos);
    }

    // 문 진영 통행 판정(기초문서.md 피드백, 2026-08-22 "보유 진영의 유닛만 지나갈 수 있고... 그게
    // 아니라면 공격해서 파괴해야 해") — UnitFunction.CanMove/AStarMovement.IsTileWalkable이 이동 판정에
    // 직접 호출한다. 시각적 개폐(DoorIsOpenVisual)와 무관하게 항상 "이 문이 속한 방의 현재 소유 진영
    // == 이동하려는 유닛의 진영"인지로만 결정 — 야생(방 소유권 개념이 있는 진영에 매핑 안 됨)이거나
    // 문 자체가 없으면(파괴됨) 막지 않는다.
    public bool IsBlockedByClosedDoor(Vector3Int pos, Unit unit)
    {
        if (unit == null || !Session.objectGrid.TryGetValue(pos, out InteractableObject obj)) return false;
        if (obj.Tags == null || !obj.Tags.Contains(DoorTag)) return false;

        FactionType? doorFaction = obj.DoorOwnerFaction;
        FactionType? myFaction = OffenseProcessor.MapToRoomFaction(unit.FactionBehavior);
        return doorFaction == null || myFaction == null || doorFaction.Value != myFaction.Value;
    }

    // 사용자 정정(2026-07-28, "문이 있는 자리가 가장 최우선이며, 문이 있는 자리에는 오브젝트 배치
    // 불가능. 몬스터 배치도 불가능(이동만 가능)") — 문은 SpawnDoors()가 다른 오브젝트/유닛보다 먼저
    // 깔아 objectGrid를 선점하므로, 그 뒤에 오는 모든 "이 타일에 뭔가 놓아도 되는지" 판정이 이 메서드로
    // 문 타일을 걸러내야 한다. BuildingManager.CanInstallAt(건물 배치)과 UnitGenerate.IsAreaClear(유닛/
    // 몬스터 스폰 위치 판정)가 호출한다. RoomConfinedMovement(야생/일부 플레이어 몬스터의 자율 이동)도
    // 이 메서드를 호출해 문 타일 자체를 walkable에서 제외한다(사용자 요청, 2026-07-28).
    public bool IsDoorTile(Vector3Int pos)
    {
        return Session.objectGrid.TryGetValue(pos, out InteractableObject obj) &&
               obj.Tags != null && obj.Tags.Contains(DoorTag);
    }

    // 문도 방어건물화(기초문서.md 피드백, 2026-08-22) — 문 체력이 0이 되면 UnitFunction.OnUpdate가
    // 호출한다(TrapDestroy/코어 공격과 동일한 채널링 데미지 패턴). objectGrid에서 제거하고 개폐 판정
    // 대상 목록에서도 뺀다 — 재설치 전까지는 아무나 통과 가능(진영 판정 자체가 대상이 없어 성립 안 함).
    public void RemoveDoor(Vector3Int pos)
    {
        _doorPositions.Remove(pos);
        _doorVisuals.Remove(pos);
        Session.CollectObject(pos); // objectGrid 제거 + 비주얼 파괴
        LogHelper.Log(LogHelper.GAME, $"RemoveDoor: {pos} 위치의 문이 파괴됐습니다 — 재설치 전까지 아무나 통과 가능.");
    }

    // 문도 방어건물화(기초문서.md 피드백, 2026-08-22) — ObjectPlacementController의 문 재설치 모드
    // 전용. SpawnDoorAt과 동일하게 기본 닫힘 상태로 재생성한다. 원래 게이트 방향을 되짚어 회전을
    // 맞춘다(원래 게이트 자리가 아니면 재설치하지 않음 — 호출부가 IsRepairableDoorTile로 이미
    // 검증하지만 방어적으로 재확인). 소유 진영은 항상 Player로 고정된다(2026-08-22 사용자 요청
    // "점령으로 인해서 문의 소유권을 바뀌지 않아. 부수고 다시 재설치하는게 원칙임") — 재설치는
    // ObjectPlacementController를 통해 오직 플레이어만 실행하므로, 재설치된 문은 곧 플레이어가
    // 새로 세운 방어선이다.
    public void RebuildDoorAt(Vector3Int pos)
    {
        if (Session.objectGrid.ContainsKey(pos)) return;
        if (!TryFindGateAt(pos, out Gate gate)) return;

        float rotation = gate.isHorizontal ? 90f : 0f;
        SpawnDoorAt(pos, rotation, FactionType.Player);
        LogHelper.Log(LogHelper.GAME, $"RebuildDoorAt: {pos}에 문을 재설치했습니다(기본 닫힘, 소유 진영: Player).");
    }

    // 문도 방어건물화(기초문서.md 피드백, 2026-08-22 "복도 자리에만 설치 가능") — ObjectPlacementController
    // 의 문 재설치 모드가 배치 가능 여부를 판정할 때 쓴다. 원래 게이트(통로) 타일 좌표만 허용한다.
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
            foreach (var row in GetGateDoorTiles(g))
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
