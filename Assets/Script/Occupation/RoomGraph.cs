using System.Collections.Generic;
using UnityEngine;

// 층의 방 그래프(게이트로 이어진 방 쌍) 조회 — 이웃 방과 출구 방에서의 방 단위 거리. 게이트는 런타임에 안 바뀌어 거리는 층마다 한 번만 계산한다.
internal sealed class RoomGraph
{
	private readonly GameSession _session;
	private readonly Dictionary<int, Dictionary<int, int>> _hopsByFloor = new Dictionary<int, Dictionary<int, int>>();

	public RoomGraph(GameSession session) => _session = session;

	private Room Find(int floor, int roomId)
	{
		foreach (var r in _session.allRooms)
			if (r != null && r.Floor == floor && r.RoomId == roomId) return r;
		return null;
	}

	public void CollectNeighbors(Room room, List<Room> into)
	{
		into.Clear();
		var floors = _session.cmap?.map.floors;
		if (floors == null || room.Floor < 0 || room.Floor >= floors.Length || floors[room.Floor].gates == null) return;
		foreach (var gate in floors[room.Floor].gates)
		{
			int neighborId = gate.roomA == room.RoomId ? gate.roomB : gate.roomB == room.RoomId ? gate.roomA : -1;
			if (neighborId < 0) continue;
			Room neighbor = Find(room.Floor, neighborId);
			if (neighbor != null && !into.Contains(neighbor)) into.Add(neighbor);
		}
	}

	// 출구 방(0층으로 가는 계단이 있는 방)에서 각 방까지의 거리 — 가까운 쪽이 후방, 먼 쪽이 전방이다(회의록 10.19). 출구 방을 못 찾으면 null.
	public Dictionary<int, int> HopsFromExit(int floor)
	{
		if (_hopsByFloor.TryGetValue(floor, out var cached)) return cached;
		var cmap = _session.cmap;
		if (cmap?.map.floors == null || floor < 1 || floor >= cmap.map.floors.Length) return null;
		if (!cmap.TryGetStairApproachPosition(floor, 0, out Vector2Int exitPos)
			|| !_session.roomGrid.TryGetValue(new Vector3Int(exitPos.x, exitPos.y, floor), out Room exitRoom) || exitRoom == null) return null;

		var edges = new List<(int a, int b)>();
		Floor f = cmap.map.floors[floor];
		if (f.gates != null) foreach (var gate in f.gates) edges.Add((gate.roomA, gate.roomB));
		return _hopsByFloor[floor] = OccupationMath.HopsFrom(exitRoom.RoomId, edges);
	}

	public int HopsOf(Room room)
	{
		var hops = HopsFromExit(room.Floor);
		return hops != null && hops.TryGetValue(room.RoomId, out int h) ? h : 0;
	}
}
