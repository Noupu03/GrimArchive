using System.Collections.Generic;
using UnityEngine;

// 리더의 다음 이동 게이트 결정(04번 1장 30~35·47줄, 05번 7장 412줄, 검증 05-06 관찰 2) — 리더가 "아는" 게이트로 방 그래프를 만들어 RoomRouteMath(순수)가 고른 게이트의 문 타일을 돌려준다.
// 예전 방식(AIMovementHelper.TryFindKnownDoorInCurrentRoom: 남아 있는 문 오브젝트 중 목표 방에 가장 가까운 것)과 달리 ① 파괴된 문(열린 통로)도 후보 ② 이미 지나온 방은 피하고 ③ 막다른 방에서는 되돌아갈 게이트를 고른다.
// 방 선택의 세부 기준은 문서가 리더 문서에 맡겼으므로(04번 49줄) 목표 방 중심 거리 같은 같은 순위 안의 판단은 기존 휴리스틱을 그대로 쓴다.
public static class LeaderRoutePlanner
{
	// doorPos는 고른 게이트의 문 타일 하나(리더 방 쪽 줄의 가운데) — PartyAdvanceSystem.TryBuildGate가 이 타일로 게이트 두 줄을 찾는다. 이미 열린 게이트면 계획이 돌파 없이 입장한다.
	public static bool TryPickNextGate(Human leader, Room targetRoom, out Vector2Int doorPos, out int doorFloor)
	{
		doorPos = default;
		doorFloor = 0;
		var session = leader?.Session;
		var party = leader?.party;
		if (leader == null || party == null || session?.cmap == null || leader.currentRoom == null) return false;

		int floorIndex = leader.currentFloor;
		var floors = session.cmap.map.floors;
		if (floors == null || floorIndex < 0 || floorIndex >= floors.Length) return false;
		Floor floor = floors[floorIndex];
		if (floor.gates == null || floor.gates.Count == 0) return false;
		int fromRoom = leader.currentRoom.RoomId;

		// 리더가 아는 게이트만 간선으로 — 문 타일을 시야로 확인했거나 문 오브젝트로 알고 있다(보지 않은 출구를 목표로 삼지 않는다, 05번 7장 394줄).
		var edges = new List<RouteEdge>();
		var rowsByGate = new Dictionary<int, List<Vector2Int>[]>();
		for (int g = 0; g < floor.gates.Count; g++)
		{
			var gate = floor.gates[g];
			var rows = DoorSystem.GetGateDoorTiles(gate, floor.config.chunkSize);
			if (!IsGateKnown(leader, session, rows, floorIndex)) continue;
			edges.Add(new RouteEdge(g, gate.roomA, gate.roomB));
			rowsByGate[g] = rows;
		}
		if (edges.Count == 0) return false;

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

		if (!RoomRouteMath.TryPickGate(fromRoom, edges, visited, targetRoomId, Score, out RouteEdge picked)) return false;

		// 리더 방 쪽 줄의 가운데 타일을 문 위치로 쓴다.
		var pickedRows = rowsByGate[picked.GateId];
		List<Vector2Int> row = pickedRows[0];
		foreach (var candidate in pickedRows)
		{
			if (candidate.Count == 0) continue;
			var t = candidate[0];
			if (session.roomGrid.TryGetValue(new Vector3Int(t.x, t.y, floorIndex), out Room r) && r == leader.currentRoom) { row = candidate; break; }
		}
		if (row.Count == 0) return false;
		doorPos = row[row.Count / 2];
		doorFloor = floorIndex;
		return true;
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
