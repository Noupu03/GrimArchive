using System.Collections.Generic;
using UnityEngine;

// 후퇴 목적지로서의 "거점"(후퇴도주 문서 2-1·2-3·5-1장). 거점 = 점령 파티가 기초 시설을 완공한 방(Room.BaseStage != None, 0층 제외)이고 방어 시설까지 완공하면 방어 거점이다(코어 점령 v0.2 12장).
// 후보 수집·가장 가까운 곳 선택·휴식 자리 배정·도착 판정·재출격용 안전 방 조회를 한곳에 모은다.
public static class RetreatBases
{
	public static bool IsUsable(Room room) => room != null && room.Floor >= 1 && room.BaseStage != BaseStage.None;

	// 한 층의 후보 거점. 층을 건너는 후퇴(다른 층 거점)는 지금 지원하지 않아 같은 층만 모은다.
	public static void Collect(GameSession session, int floor, List<Room> into)
	{
		into.Clear();
		if (session?.allRooms == null) return;
		foreach (var room in session.allRooms)
			if (IsUsable(room) && room.Floor == floor) into.Add(room);
	}

	// 그 방 안에서 실제로 설 수 있는 타일(방 중심에 가장 가까운 칸) — 경로 거리 조회의 목적지로 쓴다.
	public static bool TryGetRoomAnchorTile(Unit unit, Room room, out Vector2Int tile)
	{
		var claimed = new HashSet<Vector2Int>();
		return PartyRetreatMath.TryPickRestSlot(room.Bounds, p => IsValidRestTile(unit, room, p), claimed, out tile);
	}

	// 구성원이 그 방에서 서 있을 수 있는 칸: 걸을 수 있고(점유는 무시 — 자리는 파티가 따로 나눈다) 같은 방 소속이며 문 앞 통과 구간 밖.
	public static bool IsValidRestTile(Unit unit, Room room, Vector2Int p)
	{
		var session = unit.Session;
		if (session?.roomGrid == null) return false;
		var key = new Vector3Int(p.x, p.y, room.Floor);
		if (!session.roomGrid.TryGetValue(key, out Room r) || r != room) return false;
		if (!unit.CanMove(p, ignoreUnits: true)) return false;
		return !AIMovementHelper.IsWithinDoorClearance(session, room.Floor, p);
	}

	// 가까운 순으로 경로가 실제로 닿는 거점을 고른다 — 체비셰프 거리로 후보를 줄이고(상위 3곳) A* 경로 길이(조회, 이동 캐시 불변)로 확정한다. 경로 조회가 안 되면 체비셰프 거리로 대신한다.
	public static Room ChooseNearest(Human from, List<Room> candidates)
	{
		if (candidates == null || candidates.Count == 0) return null;
		candidates.Sort((a, b) => Chebyshev(from, a).CompareTo(Chebyshev(from, b)));

		Room best = null;
		int bestLen = int.MaxValue;
		int tries = Mathf.Min(3, candidates.Count);
		for (int i = 0; i < tries; i++)
		{
			Room room = candidates[i];
			if (!TryGetBaselineLength(from, room, out int len)) continue; // 닿을 수 없는 거점은 이용 불가로 본다(명세서 5-2장 말미 "실제 이용 가능 여부를 확인")
			if (len < bestLen) { bestLen = len; best = room; }
		}
		return best;
	}

	// 후퇴 목적지 우선순위(후퇴도주 5-1장·코어 점령 v0.2 13-2장): 닿을 수 있는 방어 거점이 있으면 그중 가장 가까운 곳을, 없으면 기초 거점 중 가장 가까운 곳을 고른다. 어느 쪽을 골랐는지 chosenIsDefense로 알려 준다.
	public static Room ChoosePreferred(Human from, List<Room> candidates, out bool chosenIsDefense)
	{
		chosenIsDefense = false;
		if (candidates == null || candidates.Count == 0) return null;
		var defenseBases = candidates.FindAll(r => r.BaseStage == BaseStage.Defense);
		Room defense = defenseBases.Count > 0 ? ChooseNearest(from, defenseBases) : null;
		if (defense != null)
		{
			chosenIsDefense = true;
			return defense;
		}
		return ChooseNearest(from, candidates);
	}

	// 그 거점까지 기준 경로(적 점유 제외)가 있는가 — 이동 중 거점 이용 가능 여부 재확인용.
	public static bool IsReachable(Human from, Room room) => TryGetBaselineLength(from, room, out _);

	static bool TryGetBaselineLength(Human from, Room room, out int length)
	{
		length = 0;
		return TryGetRoomAnchorTile(from, room, out Vector2Int anchor) && RetreatBlockResolver.TryGetBaselineLength(from, anchor, out length);
	}

	static int Chebyshev(Unit u, Room room)
	{
		Vector2Int c = new Vector2Int(Mathf.RoundToInt(room.Bounds.center.x), Mathf.RoundToInt(room.Bounds.center.y));
		return AIMovementHelper.ChebyshevDistance(u.position, c);
	}

	// 파티원마다 휴식 자리를 한 번 배정한다(이미 있으면 유지) — 방 중심에 가까운 빈 칸부터. 자리를 못 구하면 false(호출부가 방 안 아무 곳이면 도착으로 본다).
	public static bool EnsureRestSlot(Human h, Room room, HashSet<Vector2Int> claimed)
	{
		var st = h.Retreat;
		if (st.RestSlot.HasValue && IsValidRestTile(h, room, st.RestSlot.Value)) return true;
		if (st.RestSlot.HasValue) claimed.Remove(st.RestSlot.Value);
		st.RestSlot = null;
		if (!PartyRetreatMath.TryPickRestSlot(room.Bounds, p => IsValidRestTile(h, room, p), claimed, out Vector2Int slot)) return false;
		st.RestSlot = slot;
		return true;
	}

	// 지금 다른 유닛이 서 있지 않은 자리로 휴식 자리를 다시 정한다(검증 D4) — 기존 자리는 반납한다. 새 자리를 못 구하면 false(기존 자리는 이미 반납됐으니 호출부가 EnsureRestSlot으로 다시 시도한다).
	public static bool ReassignRestSlot(Human h, Room room, HashSet<Vector2Int> claimed)
	{
		var st = h.Retreat;
		if (st.RestSlot.HasValue) claimed.Remove(st.RestSlot.Value);
		st.RestSlot = null;
		var session = h.Session;
		bool Free(Vector2Int p) => session == null
			|| !session.unitGrid.TryGetValue(new Vector3Int(p.x, p.y, room.Floor), out Unit u) || u == null || u == h || u.hp <= 0f;
		if (!PartyRetreatMath.TryPickRestSlot(room.Bounds, p => IsValidRestTile(h, room, p) && Free(p), claimed, out Vector2Int slot)) return false;
		st.RestSlot = slot;
		return true;
	}

	// 거점에 도착했는가 — 거점 방 안이고 휴식 자리에서 arrivalRadius칸 안(자리가 없으면 방 안이면 도착).
	public static bool HasArrived(Human h, Room room, int arrivalRadius)
	{
		if (room == null || h.Session?.roomGrid == null) return false;
		if (h.currentFloor != room.Floor) return false;
		if (!h.Session.roomGrid.TryGetValue(new Vector3Int(h.position.x, h.position.y, h.currentFloor), out Room here) || here != room) return false;
		var slot = h.Retreat.RestSlot;
		return !slot.HasValue || AIMovementHelper.ChebyshevDistance(h.position, slot.Value) <= arrivalRadius;
	}

	// 재출격용 "인류 지도상 안전한 진출 방"(2-3장 ③): 거점 방과 문(게이트)으로 이어진 이웃 방 중 인류가 소유하지 않은 방이고, 리더 개인 지도에 위험도가 기록돼 있으며 maxDanger 이하인 곳이 하나라도 있는가. 이웃 방 정보가 없으면 false(거점 대기 유지).
	public static bool HasSafeAdvanceRoom(Human leader, Room baseRoom, float maxDanger, bool allowUnknown = false)
	{
		var session = leader?.Session;
		if (session?.cmap == null || baseRoom == null || baseRoom.RoomId < 0 || baseRoom.Floor < 0) return false;
		if (baseRoom.Floor >= session.cmap.map.floors.Length) return false;
		Floor floor = session.cmap.map.floors[baseRoom.Floor];
		if (floor.gates == null) return false;

		foreach (var gate in floor.gates)
		{
			int neighborId;
			if (gate.roomA == baseRoom.RoomId) neighborId = gate.roomB;
			else if (gate.roomB == baseRoom.RoomId) neighborId = gate.roomA;
			else continue;

			Room neighbor = FindRoom(session, baseRoom.Floor, neighborId);
			if (neighbor == null || neighbor.BaseStage != BaseStage.None) continue; // 이미 아군 거점은 "진출"이 아니다
			bool known = leader.personalMap.TryGetKnownRoomDanger(neighborId, out float danger);
			if (known ? PartyRetreatMath.IsSafeAdvanceRoom(true, danger, maxDanger) : allowUnknown) return true; // 위험도를 모르는 방은 점령 파티가 진출 후보로 허용할 때만 안전으로 본다(OccupationSystem.AllowUnknownAdvance)
		}
		return false;
	}

	static Room FindRoom(GameSession session, int floor, int roomId)
	{
		foreach (var r in session.allRooms)
			if (r != null && r.Floor == floor && r.RoomId == roomId) return r;
		return null;
	}
}
