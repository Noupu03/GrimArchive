using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;

// 문 시스템(2026-07-27~28, 사용자 요청) — 모든 방과 방 사이 통로에 문을 깔고, 웨이브 시작 시 전부
// 잠그고, 방이 "야생을 뺀 한 진영만 남으면" 다시 여는 전체 로직. GameSession이 지나치게 커지는 것을
// 막기 위해 분리했다(2026-08-20, InputManager 배치 모드 3종 분리와 동일한 이유·같은 방식).
//
// GameSession을 직접 [Inject]하지 않고 UnitRegistry와 동일한 지연 조회 패턴(IObjectResolver.Resolve
// 를 첫 사용 시점까지 미룸)을 쓴다 — GameSession.Construct()가 이 클래스를 파라미터로 받는 시점에는
// 아직 GameSession 자신의 생성이 끝나지 않아 즉시 주입받으면 순환 참조가 되기 때문(그 시점엔
// GameSession.Instance도 아직 null). roomGrid/cmap/objectGrid/unitGrid/SpawnObject/GetObjectVisual/
// RegisterUnitPos 등 필요한 것은 전부 GameSession의 기존 공개 API로만 접근한다.
//
// 외부 호출부(GameSession이 그대로 얇게 위임): CloseAllDoorsForWaveStart(HumanWaveManager),
// RefreshRoomGateStates(UnitFunction.SyncRoomAffiliation, GameSession.RemoveDeadUnit),
// IsDoorTile(BuildingManager/RoomConfinedMovement/NavigationFSMState/Unit/DefenseSystem/UnitGenerate 등
// 다수), GetGateDoorTiles(정적, MonsterDefensePlacementSystem 등).
public class DoorSystem
{
    // 문 시스템(2026-07-27, 사용자 요청) — 지나갈 수 있는 오브젝트. 이동 판정(UnitFunction.CanMove/
    // AStarMovement.IsTileWalkable)은 objectGrid를 아예 안 보고 Tile.isStructureExist/Wall만 확인하므로
    // (SpawnObject가 이 필드를 건드리지 않음), 다른 오브젝트들과 마찬가지로 별도 처리 없이 이미
    // 통행 가능하다. GameSession.SpawnObject의 아트 스프라이트 선택 분기도 이 태그를 참조한다.
    public const string DoorTag = "Object/Passable/Door";

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
    //
    // 사용자 정정(2026-07-28, "문이 한쪽 공간에 몰려서... 문을 양쪽에 달자, 2*1로 존재하던 문을 2*2로")
    // — 기존에는 통로 양 끝(청크 A/B 경계) 중 B쪽 문턱 한 줄에만 문을 심어서, 방마다 문이 한쪽에만
    // 몰려 보였다. GetGateDoorTiles가 이제 A쪽/B쪽 문턱 두 줄을 모두 돌려주므로(폭 W일 때 W*2개),
    // 각 방 입구마다 독립적으로 문이 생긴다. 회전은 두 줄 각각 기존과 같은 0/180 교대 패턴을 적용
    // — 두 문턱 사이의 힌지 방향을 서로 맞물리게 할지는 실제로 봐야 판단 가능해 일단 독립 적용.
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
                // door_open.png/door_closed.png를 직접 보고 판단(사용자 요청) — door_closed는 가로로
                // 넓은 막대 모양(위아래로 인접한 방 사이, 즉 수직 통로를 가로막는 문을 위에서 내려다본
                // 모습)이고, door_open은 그 문짝이 옆벽에 딱 붙어 접힌 듯 세로로 얇게 보인다(같은 문이
                // 열린 상태). 즉 기본(회전 0도) 그림은 수직 통로(isHorizontal=false) 기준이라, 수평
                // 통로(좌우로 인접한 방)에서는 90도 돌려야 벽 방향과 맞는다. 실제로 봤을 때 반대로
                // 보이면 이 한 줄(0f ↔ 90f)만 바꾸면 된다.
                float baseRotation = gate.isHorizontal ? 90f : 0f;

                // 사용자 정정(2026-07-27, "한쪽 스프라이트가 180도 돌아가야지... 정상,180도,정상,180도
                // 이런식으로... 열어젖힌 형태") — door_open 그림 한 장은 문짝이 한쪽 벽에만 붙어 접힌
                // 모습이라, 통로 폭 전체에 같은 회전만 반복하면 모든 문짝이 같은 벽 쪽으로만 열린 것처럼
                // 보인다. 통로를 가로지르며 타일 순서대로 0도/180도를 번갈아 적용해 절반은 한쪽 벽에,
                // 나머지 절반은 반대쪽 벽에 붙어 열린 것처럼 — 즉 통로 양쪽으로 열어젖힌 이중문 형태로
                // 보이게 한다.
                List<Vector2Int>[] gateTileRows = GetGateDoorTiles(gate);
                foreach (List<Vector2Int> gateTiles in gateTileRows)
                {
                    for (int tileIndex = 0; tileIndex < gateTiles.Count; tileIndex++)
                    {
                        Vector2Int tilePos = gateTiles[tileIndex];
                        Vector3Int gridPos = new Vector3Int(tilePos.x, tilePos.y, floorIdx);
                        if (Session.objectGrid.ContainsKey(gridPos)) continue;

                        float doorRotation = baseRotation + (tileIndex % 2 == 1 ? 180f : 0f);
                        string objId = $"Door_{floorIdx}_{tilePos.x}_{tilePos.y}";
                        InteractableObject door = new InteractableObject(objId, gridPos, 0f, 0f, new List<string> { DoorTag });
                        Session.SpawnObject(door, Color.white, doorRotation);
                        doorCount++;
                    }
                }
            }
        }

        LogHelper.Log(LogHelper.GAME, $"SpawnDoors: 전체 {cmap.map.floors.Length}개 층에 문 {doorCount}개 배치 완료.");
    }

    // Gate(청크 경계 + 폭 + 방향)로부터 실제 문이 놓일 타일 좌표 목록을 계산한다. CreateMap.Connection.cs의
    // OpenHorizontalPassage/OpenVerticalPassage가 통로를 깎을 때 쓴 것과 똑같은 공식(중앙 정렬,
    // (8-width)/2부터 width칸)을 재사용해 정확히 같은 타일들을 되짚는다 — chunkAX/BX(또는 AY/BY) 중
    // 어느 쪽이 A/B로 기록됐는지는 방향(왼쪽/오른쪽, 아래/위)에 따라 뒤바뀔 수 있어 Min으로 왼쪽·아래
    // 청크를 먼저 찾는다.
    //
    // 사용자 정정(2026-07-28, "문을 양쪽에 달자, 2*1로 존재하던 문을 2*2로") — 통로 양 끝(A쪽 청크의
    // 마지막 칸 / B쪽 청크의 첫 칸) 두 줄을 모두 반환한다. 반환값은 [A쪽 문턱 줄, B쪽 문턱 줄] 순서의
    // 배열. MapRandering.ApplyOccupationTint/ChangeRoomColor가 "문이 있는 바닥은 점령 색칠 제외"
    // (사용자 요청 2026-07-28)를 위해 그대로 재사용하므로 public static.
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

    // 문 닫힘 시스템(2026-07-28, 사용자 요청) — "웨이브가 시작되면 모든 문이 닫히며 벽과 같은 판정이
    // 된다(시야 막힘, 이동 불가)." HumanWaveManager.StartWave()가 웨이브 시작 시점에 이 메서드를
    // 호출한다. 이후 "비어있는 방의 인접 문은 다 열어버리는거로 처리해" 요구사항에 따라, 잠그자마자
    // 이미 비어있는 방들은 그 자리에서 곧바로 다시 연다 — 이 "완전히 비어있으면 연다" 예외는 웨이브
    // 시작 시점에만 적용되는 1회성 부트스트랩이다(사용자 정정 2026-07-28 "문도 한 진영이 남을때까지
    // 열리지 않는거로 하자" — RefreshRoomGateStates 쪽 일반 규칙에서는 아래처럼 이 예외를 뺐다). 만약
    // 이 부트스트랩이 없으면, 애초에 아무도 없던 방은 영원히 "한 진영"이 될 기회 자체가 없어 문이
    // 평생 안 열리는 도달 불가 구역이 생긴다 — 그래서 웨이브 시작 순간에 한해서만 별도로 열어준다.
    public void CloseAllDoorsForWaveStart()
    {
        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null) return;

        for (int floorIdx = 0; floorIdx < cmap.map.floors.Length; floorIdx++)
        {
            Floor floor = cmap.map.floors[floorIdx];
            if (floor.gates == null) continue;
            for (int i = 0; i < floor.gates.Count; i++)
                SetGateClosed(floorIdx, i, true);
        }

        foreach (var room in Session.allRooms)
        {
            RefreshRoomGateStates(room);
            if (IsRoomEmpty(room)) OpenAllGatesForRoom(room);
        }

        // 문 끼임 방지(2026-07-28, 사용자 요청 "웨이브가 시작하며 문이 닫혔을때, 문에 있는 유닛들이
        // 끼이지 않도록 밀어줘") — 위 재개방까지 끝나 최종적으로 "닫힘"으로 남은 문들만 대상으로,
        // 마침 그 문 타일 위에 서 있던 유닛을 인접한 빈 칸으로 밀어낸다.
        PushUnitsOffClosedDoorTiles();

        LogHelper.Log(LogHelper.GAME, "CloseAllDoorsForWaveStart: 모든 문을 잠그고, 이미 비어있는 방의 인접 문은 다시 열었습니다.");
    }

    // CloseAllDoorsForWaveStart 전용 — isStructureExist가 켜져도 "이미 그 칸에 있던" 유닛은 저절로
    // 튕겨나가지 않는다(새로 들어오는 이동만 막음, UnitFunction.CanMove 참고) — 그대로 두면 문이 닫힌
    // 칸 위에 유닛이 갇힌 것처럼 보인다. AIMovementHelper.FindNearbyOpenTile과 동일한 "8방향 중 가장
    // 가까운 갈 수 있는 칸" 탐색으로 밀어내고, 위치만 직접 갱신한다(HumanWaveManager의 강제 층 이동과
    // 동일한 텔레포트 관례 — 경로탐색 없이 Unregister→position 대입→Register).
    private void PushUnitsOffClosedDoorTiles()
    {
        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null) return;

        for (int floorIdx = 0; floorIdx < cmap.map.floors.Length; floorIdx++)
        {
            Floor floor = cmap.map.floors[floorIdx];
            if (floor.gates == null) continue;

            foreach (Gate g in floor.gates)
            {
                if (!g.isDoorClosed) continue;

                foreach (var row in GetGateDoorTiles(g))
                {
                    foreach (var doorPos in row)
                    {
                        Vector3Int gridPos = new Vector3Int(doorPos.x, doorPos.y, floorIdx);
                        if (!Session.unitGrid.TryGetValue(gridPos, out Unit u) || u == null || u.hp <= 0) continue;

                        Vector2Int pushTo = AIMovementHelper.FindNearbyOpenTile(u, doorPos);
                        if (pushTo == doorPos) continue; // 밀어낼 빈 칸을 못 찾음(드묾) — 그대로 둠

                        Session.UnregisterUnitPos(u, u.position);
                        u.position = pushTo;
                        Session.RegisterUnitPos(u, u.position);
                        LogHelper.Log(LogHelper.GAME, $"PushUnitsOffClosedDoorTiles: {u.unitType?.typeName}({u.name})를 닫힌 문 {doorPos} → {pushTo}로 밀어냈습니다.");
                    }
                }
            }
        }
    }

    // 문 닫힘 시스템(2026-07-28, 사용자 정정 "문도 한 진영이 남을때까지 열리지 않는거로 하자") — 방
    // 하나의 현재 유닛 구성을 보고 "야생이 아닌 단일 진영만 남음"이면 그 방과 연결된 모든 문을 연다.
    // 사용자 지시 그대로:
    //   - 인간만 남음(야생 전멸, 몬스터 없음) → 열림 / 몬스터만 남음(야생 전멸, 인간 없음) → 열림
    //   - 야생만 남음(인간·몬스터 모두 없음) → 그래도 닫힘 유지
    //   - 인간+몬스터+야생 중 둘 이상이 동시에 살아있음 → 닫힘 유지("야생을 제외한 한 진영이 남을 때까지")
    //   - 완전히 비어있음(셋 다 없음) → 이 메서드만으로는 열리지 않는다(위 정정) — 웨이브 시작 시점의
    //     1회성 예외(CloseAllDoorsForWaveStart)에서만 별도로 처리한다. 웨이브 도중에 방이 나중에
    //     비게 되는 경우(전멸/모두 이탈)까지 자동으로 열어주지는 않는다 — "한 진영"이 아니기 때문.
    // 한번 연 문은 다시 잠그지 않는다(단방향) — 열린 뒤 다른 진영이 흘러들어와 다시 섞여도 그 순간
    // 문을 잠그면 마침 통로에 있던 유닛이 방 사이에 갇히는 부작용이 생긴다. 전체 재잠금은 다음 웨이브
    // 시작 시 CloseAllDoorsForWaveStart가 한 번에 처리한다. GameSession.RemoveDeadUnit(사망 시)과
    // UnitFunction.SyncRoomAffiliation(유닛이 방을 떠날 때)이 관련 방이 바뀔 때마다 이 메서드를 호출한다.
    public void RefreshRoomGateStates(Room room)
    {
        CreateMap cmap = Session.cmap;
        if (room == null || cmap == null || cmap.map.floors == null) return;
        if (room.RoomId < 0 || room.Floor < 0 || room.Floor >= cmap.map.floors.Length) return;

        bool hasHuman = false, hasPlayerMonster = false, hasWild = false;
        foreach (var u in room.ContainedUnits)
        {
            if (u == null || u.hp <= 0) continue;
            if (u.FactionBehavior is HumanFactionBehavior) hasHuman = true;
            else if (u.FactionBehavior is PlayerMonsterBehavior) hasPlayerMonster = true;
            else if (u.FactionBehavior is WildMonsterBehavior) hasWild = true;
        }

        bool isSingleNonWildFaction = !hasWild && (hasHuman ^ hasPlayerMonster);
        if (!isSingleNonWildFaction) return;

        OpenAllGatesForRoom(room);
    }

    // RefreshRoomGateStates/CloseAllDoorsForWaveStart 공용 — 이 방과 연결된 모든 게이트를 연다.
    private void OpenAllGatesForRoom(Room room)
    {
        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null) return;
        if (room.RoomId < 0 || room.Floor < 0 || room.Floor >= cmap.map.floors.Length) return;

        Floor floor = cmap.map.floors[room.Floor];
        if (floor.gates == null) return;

        for (int i = 0; i < floor.gates.Count; i++)
        {
            Gate g = floor.gates[i];
            if (g.roomA == room.RoomId || g.roomB == room.RoomId)
                SetGateClosed(room.Floor, i, false);
        }
    }

    // CloseAllDoorsForWaveStart 전용 — 살아있는 유닛이 하나도 없으면 "빈 방"(UnitFunction.
    // IsRoomEffectivelyEmpty와 동일 기준).
    private static bool IsRoomEmpty(Room room)
    {
        foreach (var u in room.ContainedUnits)
            if (u != null && u.hp > 0) return false;
        return true;
    }

    // 문 닫힘 시스템(2026-07-28) — 게이트 하나를 열거나 잠근다: (1) Gate.isDoorClosed 갱신,
    // (2) 그 게이트의 문 타일(GetGateDoorTiles)에 Tile.isStructureExist를 씌우거나 벗겨 실제 벽처럼
    // 시야·이동을 막고(UnitFunction.CanMove/CastRay가 이미 Wall과 함께 이 필드를 확인), (3) 문
    // 스프라이트를 door_closed/door_open으로 교체한다. 이미 같은 상태면 아무 것도 하지 않는다(중복
    // 호출·재계산 방지).
    public void SetGateClosed(int floorIndex, int gateIndex, bool closed)
    {
        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null) return;
        if (floorIndex < 0 || floorIndex >= cmap.map.floors.Length) return;
        Floor floor = cmap.map.floors[floorIndex];
        if (floor.gates == null || gateIndex < 0 || gateIndex >= floor.gates.Count) return;

        Gate gate = floor.gates[gateIndex];
        if (gate.isDoorClosed == closed) return;
        gate.isDoorClosed = closed;
        floor.gates[gateIndex] = gate;

        ApplyGateTileBlocking(floor, gate, closed);
        ApplyGateDoorVisual(floorIndex, gate, closed);

        // 실제 상태 전환이 일어난 경우에만 남는다(위 short-circuit 덕분에 스팸 아님) — 문 개폐 이력을
        // 추적할 수 있는 이벤트 로그로 계속 유지.
        LogHelper.Log(LogHelper.GAME, $"SetGateClosed: F{floorIndex} Gate[{gateIndex}](room{gate.roomA}<->room{gate.roomB}) → {(closed ? "닫힘" : "열림")}");
    }

    // SetGateClosed 전용 — 문이 놓인 타일들의 Tile.isStructureExist를 토글한다. tile.name은 "Floor"
    // 그대로 둔다(실제 Wall로 바꾸지 않아도 UnitFunction의 모든 차단 판정이 "name==Wall || isStructureExist"
    // OR 조건이라 이것만으로 충분 — CanMove/CastRay/UpdateFOV 전부 동일 패턴 사용).
    private void ApplyGateTileBlocking(Floor floor, Gate gate, bool closed)
    {
        if (floor.chunks == null) return;

        foreach (var row in GetGateDoorTiles(gate))
        {
            foreach (var tilePos in row)
            {
                int cx = tilePos.x / 8, cy = tilePos.y / 8;
                int tx = tilePos.x % 8, ty = tilePos.y % 8;
                if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height) continue;

                Chunks c = floor.chunks[cx, cy];
                if (c.chunk == null) continue;
                Tile t = c.chunk[tx, ty];
                t.isStructureExist = closed;
                c.chunk[tx, ty] = t;
                floor.chunks[cx, cy] = c;
            }
        }
    }

    // SetGateClosed 전용 — 이미 SpawnDoors가 심어둔 문 오브젝트의 스프라이트/회전을 닫힘·열림에 맞게
    // 바꾼다(GetObjectVisual로 기존 GameObject를 그대로 재사용, 새로 생성하지 않음). 닫힘 상태는 통로
    // 전체를 막는 막대 모양이라 타일마다 다른 회전을 줄 필요가 없다 — SpawnDoors의 0/180 교대 패턴은
    // 열림 상태(문짝이 한쪽 벽에 접혀 붙은 모습)에만 의미가 있다.
    //
    // 시야 차단(2026-07-28, 사용자 요청 "문이 닫혀버리면, 벽과 같은 가시성을 가지게 해줘") — 여기서
    // 문 InteractableObject.IsFullyBlocking도 함께 토글한다. UnitFunction.CastRay의 레이 중단 조건이
    // "tile.name==Wall / tile.visibility / IsFullyBlocking"만 보고 Tile.isStructureExist는 안 보는
    // 별개 로직이라(데모_구현현황_검증_2026-07-28.txt "알려진 한계" 참고), isStructureExist만 세워서는
    // 타일 각인(discoveredMap)은 정확해도 실제 레이가 문에서 멈추지 않았다 — IsFullyBlocking을 같이
    // 세워야 진짜로 "벽과 같은 가시성"이 된다.
    private void ApplyGateDoorVisual(int floorIndex, Gate gate, bool closed)
    {
        string spritePath = closed ? "obj/door_closed" : "obj/door_open";
        Sprite sprite = Resources.Load<Sprite>(spritePath);
        // Resources.Load 실패(Import 설정 등)를 조용히 넘기지 않고 경고로 남긴다 — 그 외에는 스프라이트
        // 교체를 그냥 건너뛴다(아래 sprite != null 체크).
        if (sprite == null)
            LogHelper.Warning(LogHelper.GAME, $"ApplyGateDoorVisual: Resources.Load<Sprite>(\"{spritePath}\")가 null을 반환했습니다 — Import 설정(Sprite Mode) 확인 필요.");

        float baseRotation = gate.isHorizontal ? 90f : 0f;

        foreach (var row in GetGateDoorTiles(gate))
        {
            for (int tileIndex = 0; tileIndex < row.Count; tileIndex++)
            {
                Vector3Int gridPos = new Vector3Int(row[tileIndex].x, row[tileIndex].y, floorIndex);

                if (Session.objectGrid.TryGetValue(gridPos, out InteractableObject doorObj))
                    doorObj.IsFullyBlocking = closed;

                GameObject visual = Session.GetObjectVisual(gridPos);
                if (visual == null) continue;

                SpriteRenderer sr = visual.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                if (sprite != null) sr.sprite = sprite;

                float rotation = closed ? baseRotation : baseRotation + (tileIndex % 2 == 1 ? 180f : 0f);
                visual.transform.rotation = Quaternion.Euler(0f, 0f, rotation);

                // SpawnObject와 동일한 관례(2026-07-28, 사용자 요청으로 타일 맞춤 스케일 폐지) —
                // door_open/door_closed 스프라이트를 원본 크기(스케일 1) 그대로 사용.
                visual.transform.localScale = Vector3.one;
            }
        }
    }

    // 사용자 정정(2026-07-28, "문이 있는 자리가 가장 최우선이며, 문이 있는 자리에는 오브젝트 배치
    // 불가능. 몬스터 배치도 불가능(이동만 가능)") — 문은 SpawnDoors()가 다른 오브젝트/유닛보다 먼저
    // 깔아 objectGrid를 선점하므로, 그 뒤에 오는 모든 "이 타일에 뭔가 놓아도 되는지" 판정이 이 메서드로
    // 문 타일을 걸러내야 한다. BuildingManager.CanInstallAt(건물 배치)과 UnitGenerate.IsAreaClear(유닛/
    // 몬스터 스폰 위치 판정)가 호출한다 — 대부분의 이동(AStarMovement 등)은 여전히 objectGrid를 보지
    // 않으므로 문을 그냥 통과할 수 있고, 이 메서드는 원래 "배치"만 막는 용도였다. 다만 2026-07-28
    // 사용자 요청("배회하는 야생 몬스터가 문이 있는 공간을 돌아다니지 못하게")으로 RoomConfinedMovement
    // (야생/일부 플레이어 몬스터의 자율 이동)도 이 메서드를 호출해 문 타일 자체를 walkable에서 제외한다.
    public bool IsDoorTile(Vector3Int pos)
    {
        return Session.objectGrid.TryGetValue(pos, out InteractableObject obj) &&
               obj.Tags != null && obj.Tags.Contains(DoorTag);
    }
}
