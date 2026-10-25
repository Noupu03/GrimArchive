using System.Collections.Generic;
using UnityEngine;

// 리더의 다음 이동 게이트 결정(04번 1장, 05번 7장) — 리더가 '아는' 게이트로 방 그래프를 만들어 RoomRouteMath(순수)가 고른 게이트의 문 타일을 돌려준다. 예전 TryFindKnownDoorInCurrentRoom과 달리 ① 파괴된 문(열린 통로)도 후보 ② 이미 지나온 방은 피하고 ③ 막다른 방에서는 되돌아갈 게이트를 고른다. 방 선택의 세부 기준은 리더 문서 몫이라 같은 순위 안의 판단은 목표 방 중심 거리 휴리스틱을 그대로 쓴다.
public static class LeaderRoutePlanner
{
	public enum PlanResult
	{
		Picked,
		NoKnownGate,            // 고를 수 있는 아는 게이트가 없다(문을 아직 모름·막다른 곳) — 문 찾기를 계속한다
		OnlyClosedBossRoomLeft, // 아는 게이트 중 갈 수 있는 일반 방이 없고 보스가 살아 있는 보스방으로 가는 게이트만 남았다 — 정상 귀환 사유(행동경로 01 8장)
	}

	public enum NextDoorResult { Found, NotYetKnown, OnlyBossRoomLeft }

	private static readonly HashSet<int> ClosedBossRoomScratch = new HashSet<int>();

	// 리더가 다음에 지날 문을 정한다 — 방 그래프 계획(Plan)을 먼저, 못 고르면 현재 방의 아는 문 중 목표 방에 가까운 것으로 근사한다. HumanWaveManager(웨이브 파티)와 PartyMissionDriver(점령 파티)가 같이 쓴다. 목표 방이 보스가 살아 있는 보스방이면 그 방은 목표도 후보도 아니다(인류 출입 제한).
	public static NextDoorResult FindNextDoor(Human leader, Room targetRoom, out Vector2Int doorPos, out int doorFloor)
	{
		doorPos = default;
		doorFloor = 0;
		bool targetClosed = BossRoomAccess.IsClosedToHumans(leader?.Session, targetRoom);
		if (AIConfigLoader.Behavior?.leaderRoutePlannerEnabled ?? true)
		{
			switch (Plan(leader, targetClosed ? null : targetRoom, out doorPos, out doorFloor))
			{
				case PlanResult.Picked: return NextDoorResult.Found;
				case PlanResult.OnlyClosedBossRoomLeft: return NextDoorResult.OnlyBossRoomLeft;
			}
		}
		Vector2? center = targetClosed || targetRoom == null ? (Vector2?)null : targetRoom.Bounds.center;
		return AIMovementHelper.TryFindKnownDoorInCurrentRoom(leader, center, out doorPos, out doorFloor) ? NextDoorResult.Found : NextDoorResult.NotYetKnown;
	}

	public static bool TryPickNextGate(Human leader, Room targetRoom, out Vector2Int doorPos, out int doorFloor)
		=> Plan(leader, targetRoom, out doorPos, out doorFloor) == PlanResult.Picked;

	// doorPos는 고른 게이트의 문 타일 하나(리더 방 쪽 줄의 가운데) — PartyAdvanceSystem.TryBuildGate가 이 타일로 두 줄을 찾으며, 이미 열린 게이트면 계획이 돌파 없이 입장한다.
	public static PlanResult Plan(Human leader, Room targetRoom, out Vector2Int doorPos, out int doorFloor)
	{
		doorPos = default;
		doorFloor = 0;
		var session = leader?.Session;
		var party = leader?.party;
		if (leader == null || party == null || session?.cmap == null || leader.currentRoom == null) return PlanResult.NoKnownGate;

		int floorIndex = leader.currentFloor;
		var floors = session.cmap.map.floors;
		if (floors == null || floorIndex < 0 || floorIndex >= floors.Length) return PlanResult.NoKnownGate;
		Floor floor = floors[floorIndex];
		if (floor.gates == null || floor.gates.Count == 0) return PlanResult.NoKnownGate;
		int fromRoom = leader.currentRoom.RoomId;

		// 보스가 살아 있는 보스방은 인류가 들어갈 수 없어 간선에서 뺀다. 그 방으로 가는 아는 게이트가 있었다는 사실은 남겨 둬서, 다른 갈 곳이 없을 때 "문을 모르는 것"과 "일반 방이 더는 없는 것"을 가른다.
		BossRoomAccess.CollectClosedRoomIds(session, floorIndex, ClosedBossRoomScratch);
		bool closedBossGateKnown = false;
		PlanResult NothingToPick() => closedBossGateKnown ? PlanResult.OnlyClosedBossRoomLeft : PlanResult.NoKnownGate;

		// 리더가 아는 게이트만 간선으로 — 문 타일을 시야로 확인했거나 문 오브젝트로 알고 있다(보지 않은 출구를 목표로 삼지 않는다, 05번 7장 394줄).
		var edges = new List<RouteEdge>();
		var rowsByGate = new Dictionary<int, List<Vector2Int>[]>();
		for (int g = 0; g < floor.gates.Count; g++)
		{
			var gate = floor.gates[g];
			var rows = DoorSystem.GetGateDoorTiles(gate, floor.config.chunkSize);
			if (!IsGateKnown(leader, session, rows, floorIndex)) continue;
			if (ClosedBossRoomScratch.Contains(gate.roomA) || ClosedBossRoomScratch.Contains(gate.roomB)) { closedBossGateKnown = true; continue; }
			edges.Add(new RouteEdge(g, gate.roomA, gate.roomB));
			rowsByGate[g] = rows;
		}
		if (edges.Count == 0) return NothingToPick();

		var visited = new HashSet<int> { fromRoom };
		foreach (var e in edges)
		{
			if (party.HasVisitedRoom(floorIndex, e.RoomA)) visited.Add(e.RoomA);
			if (party.HasVisitedRoom(floorIndex, e.RoomB)) visited.Add(e.RoomB);
		}

		int targetRoomId = targetRoom != null && targetRoom.Floor == floorIndex ? targetRoom.RoomId : -1;
		Vector2 targetCenter = targetRoom != null ? targetRoom.Bounds.center : (Vector2)leader.position;
		float Score(RouteEdge e)
		{
			var rows = rowsByGate[e.GateId];
			var mid = rows[0][rows[0].Count / 2];
			return (new Vector2(mid.x, mid.y) - targetCenter).sqrMagnitude;
		}

		if (!RoomRouteMath.TryPickGate(fromRoom, edges, visited, targetRoomId, Score, out RouteEdge picked)) return NothingToPick();

		// 리더 방 쪽 줄의 가운데 타일을 문 위치로 쓴다.
		var pickedRows = rowsByGate[picked.GateId];
		List<Vector2Int> row = pickedRows[0];
		foreach (var candidate in pickedRows)
		{
			if (candidate.Count == 0) continue;
			var t = candidate[0];
			if (session.roomGrid.TryGetValue(new Vector3Int(t.x, t.y, floorIndex), out Room r) && r == leader.currentRoom) { row = candidate; break; }
		}
		if (row.Count == 0) return PlanResult.NoKnownGate;
		doorPos = row[row.Count / 2];
		doorFloor = floorIndex;
		return PlanResult.Picked;
	}

	private static bool IsGateKnown(Human leader, GameSession session, List<Vector2Int>[] rows, int floorIndex)
	{
		foreach (var row in rows)
		{
			foreach (var tile in row)
			{
				var pos3 = new Vector3Int(tile.x, tile.y, floorIndex);
				if (leader.personalMap.IsTileRevealed(pos3)) return true;
				if (session.objectGrid.TryGetValue(pos3, out InteractableObject door) && door.Tags != null && door.Tags.Contains(DoorSystem.DoorTag)
					&& PropagationSystem.KnowsDoor(leader, door)) return true;
			}
		}
		return false;
	}
}
