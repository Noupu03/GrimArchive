using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 몬스터 배치 프리셋(2026-08-19 구현, 2026-08-20 GameSession에서 분리) — PropagationSystem/
// PartyDeathSystem과 동일 성격의 부수효과 있는 호출부. GameSession이 계속 커지는 것을 막기 위해,
// "0층에 인류가 소환된 시점"에 디펜스 시작 위치로 몬스터를 이동시키고 소집 대기에 들어가게 하는
// 로직과 소집 도착 후 바라보는 방향을 계산하는 로직을 별도 정적 클래스로 뺐다. GameSession/Unit의
// 필요한 상태는 전부 공개 프로퍼티(cmap/roomGrid/units/unitGenerate)로만 접근한다.
//
// 호출부: HumanWaveManager.PreSpawnWaveUnits()/StartWave()의 즉시 스폰 폴백(0층 인류 소환 시점,
// ApplyDefenseStartPositions), PlayerCommandFSMState.CompletePlayerCommand(소집 이동 도착 시점,
// ApplyDefenseFacingDirection).
public static class MonsterDefensePlacementSystem
{
    // 배치가 지정되지 않은 플레이어 몬스터도(2026-08-19 사용자 요청 "배치가 진행되지 않은 플레이어
    // 진영 몬스터들은 제자리에서 소집 상태 처리되게") 이동 없이 그 자리에서 곧바로 소집 대기에
    // 들어간다 — 해제 시점(전투/전술 인지)은 배치된 몬스터와 완전히 동일(UnitFSM.SelectState).
    public static void ApplyDefenseStartPositions(GameSession session)
    {
        if (session == null) return;

        int placedCount = 0, inPlaceCount = 0;

        foreach (var unit in session.units)
        {
            if (unit == null || unit.Health.hp <= 0) continue;
            if (!unit.IsPlayerMonsterFaction) continue;

            if (unit.defenseStartPosition.HasValue)
            {
                unit.playerMoveTarget    = unit.defenseStartPosition.Value;
                unit.isManualMoveCommand = true;
                unit.playerAttackTarget  = null;
                unit.isMustered          = true;
                placedCount++;
            }
            else
            {
                unit.isMustered = true;
                ApplyDefenseFacingDirection(session, unit); // 제자리 소집도 배치된 몬스터와 동일하게 방향을 맞춘다.
                inPlaceCount++;
            }

            // 버그 수정(2026-08-19, 사용자 신고 "머리 위에 소집 상태가 안뜨고, 바라보는 방향이 전환
            // 안되는듯") — GameSession.ProcessUnitAction의 SyncVisual 호출은 그 함수 안에서 직접 잰
            // 이동 전/후 스냅샷(oldLabel/oldDir vs 실행 후 값)이 달라졌을 때만 실행된다. 그런데 여기서
            // isMustered/currentDir를 그 함수 밖에서 미리 바꿔버리면, 나중에 ProcessUnitAction이 이
            // 유닛을 처리할 때는 "바뀌기 전" 스냅샷 자체가 이미 바뀐 값으로 찍혀서 차이가 감지되지
            // 않는다 — 그래서 안 움직이는(제자리 소집) 유닛은 라벨/방향이 영영 화면에 반영되지 않았다
            // (이동해서 배치 위치로 가는 유닛은 위치가 바뀌므로 그 사이에 자연히 한 번 갱신됨). 여기서
            // 직접 즉시 동기화해 그 갭을 없앤다.
            session.unitGenerate?.SyncVisual(unit);
        }

        // 진단 로그(2026-08-19, 사용자 신고 "미배치 몬스터 소집 상태로 전환되는지... 잘 작동 안되는거
        // 같은데") — 이 시점에 실제로 몇 기가 소집 처리됐는지부터 확인한다. 여기 로그가 0/0이면
        // 플레이어 몬스터 자체가 아직 없는 것(생산 전)이고, inPlaceCount>0인데도 게임 화면에서 계속
        // 돌아다니는 것처럼 보인다면 UnitFSM.SelectState 쪽(Tactical/Combat이 항상 우선권을 가져가는
        // 상태)을 의심해야 한다.
        LogHelper.Log(LogHelper.GAME, $"ApplyMonsterDefenseStartPositions: 배치 이동 {placedCount}기, 제자리 소집 {inPlaceCount}기.");
    }

    // 몬스터 배치 프리셋(2026-08-19, 사용자 요청 "1. 시작방에서 소집 시, 해당 층의 계단 방향을
    // 바라봐야 함. 2. 시작방 이외의 방에서 소집시, 시작방으로 향하는 문 방향을 바라봐야 함") — 소집
    // 이동이 끝나 제자리에 서는 순간 PlayerCommandFSMState.CompletePlayerCommand가 호출한다. 시작방은
    // 인류가 이 층에 처음 올라오는 계단(이전 층으로 내려가는 계단) 쪽을, 그 외의 방은 시작방으로 가는
    // 최단 경로상 첫 번째 문(=이 방에 침입자가 들어올 때 반드시 지나야 하는 문)을 바라보게 한다.
    public static void ApplyDefenseFacingDirection(GameSession session, Unit unit)
    {
        if (session == null || unit == null || session.cmap == null) return;
        if (!session.roomGrid.TryGetValue(new Vector3Int(unit.position.x, unit.position.y, unit.currentFloor), out Room room)) return;

        Vector2Int target;
        bool found;
        if (TryGetStartRoomId(session, unit.currentFloor, out int startRoomId) && room.RoomId == startRoomId)
        {
            // 시작방 — 이 층으로 올라오는 계단(이전 층으로 내려가는 계단) 방향을 바라본다.
            found = session.cmap.TryGetStairApproachPosition(unit.currentFloor, unit.currentFloor - 1, unit.position, out target)
                || session.cmap.TryGetStairPosition(unit.currentFloor, unit.currentFloor - 1, out target);
        }
        else
        {
            found = TryGetDefenseFacingDoorTile(session, unit.currentFloor, room.RoomId, out target);
        }

        if (!found) return;

        Vector2Int diff = target - unit.position;
        if (diff == Vector2Int.zero) return;
        unit.currentDir = SkillAction.GetDirection8(diff);
    }

    private static bool TryGetStartRoomId(GameSession session, int floorIndex, out int startRoomId)
    {
        startRoomId = -1;
        if (session.cmap == null || session.cmap.map.floors == null || floorIndex < 0 || floorIndex >= session.cmap.map.floors.Length) return false;

        Floor floor = session.cmap.map.floors[floorIndex];
        int w = floor.config.width, h = floor.config.height;
        for (int x = 0; x < w && startRoomId < 0; x++)
            for (int y = 0; y < h && startRoomId < 0; y++)
                if (floor.chunks[x, y].roomRole == RoomRole.StartRoom && floor.chunks[x, y].roomId >= 0)
                    startRoomId = floor.chunks[x, y].roomId;

        return startRoomId >= 0;
    }

    // 시작방에서부터 BFS로 "이 방에 처음 도달할 때 지나온 게이트"(=시작방쪽을 향하는 최단 경로상
    // 가장 가까운 문)를 찾아 그 문턱 타일(이 방 쪽 문턱)을 반환한다. roomId가 시작방 자신인 경우는
    // 호출하지 않는다(ApplyDefenseFacingDirection이 그 경우 계단 방향으로 먼저 분기함).
    private static bool TryGetDefenseFacingDoorTile(GameSession session, int floorIndex, int roomId, out Vector2Int doorTile)
    {
        doorTile = default;
        if (session.cmap == null || session.cmap.map.floors == null || floorIndex < 0 || floorIndex >= session.cmap.map.floors.Length) return false;

        Floor floor = session.cmap.map.floors[floorIndex];
        if (floor.gates == null || floor.gates.Count == 0) return false;
        if (!TryGetStartRoomId(session, floorIndex, out int startRoomId)) return false;

        var parentGate = new Dictionary<int, Gate>();
        var visited = new HashSet<int> { startRoomId };
        var queue = new Queue<int>();
        queue.Enqueue(startRoomId);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (Gate g in floor.gates)
            {
                if (g.roomA != current && g.roomB != current) continue;
                int neighbor = g.roomA == current ? g.roomB : g.roomA;
                if (visited.Contains(neighbor)) continue;

                visited.Add(neighbor);
                parentGate[neighbor] = g;
                queue.Enqueue(neighbor);
            }
        }

        if (!parentGate.TryGetValue(roomId, out Gate doorGate)) return false;

        var doorTiles = GameSession.GetGateDoorTiles(doorGate);
        var mySideTiles = doorGate.roomA == roomId ? doorTiles[0] : doorTiles[1];
        if (mySideTiles.Count == 0) return false;

        doorTile = mySideTiles[mySideTiles.Count / 2];
        return true;
    }
}
