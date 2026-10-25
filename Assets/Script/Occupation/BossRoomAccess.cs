using System.Collections.Generic;
using UnityEngine;

// 인류의 보스방 출입 제한 — 보스가 아직 살아 있는 보스방에는 인류가 들어갈 수 없다(사용자 확정 2026-10-26 "인류는 보스가 아직 죽지 않은 보스방에 들어가면 안됨"). 보스를 처치하면 풀린다.
// 적용은 방 이탈 금지와 같은 네 곳이다: UnitFunction.Move(실제 걸음) · AStarMovement.IsTileWalkable(경로 계획) · DefenseSystem 회피/점멸 후보 · AIMovementHelper 한 걸음 이동 — 모두 Human.IsRoomLeaveBlocked를 거치므로 그 한 곳에서 이 판정을 부른다. 계획 단계(LeaderRoutePlanner·문 후보)는 같은 판정으로 그 방을 후보에서 뺀다.
public static class BossRoomAccess
{
	// 켜져 있는가 — AIBehaviorConfig.bossRoomEntryBanEnabled(설정 에셋이 없으면 켜짐).
	public static bool Enabled => AIConfigLoader.Behavior?.bossRoomEntryBanEnabled ?? true;

	// 순수 판정 — 보스방이고 보스가 살아 있으면 인류에게 닫힌다.
	public static bool IsClosed(bool isBossRoom, bool bossAlive) => isBossRoom && bossAlive;

	public static bool IsClosedToHumans(GameSession session, Room room)
	{
		if (!Enabled || session == null || room == null || !session.BossRooms.Contains(room)) return false;
		return HasLivingBoss(room);
	}

	// 이 걸음이 닫힌 보스방으로 "들어가는" 걸음인가. 이미 그 방 안에 있는 유닛(스폰·상태 이상으로 들어가 있는 경우)은 막지 않는다 — 나오지도 못하고 갇히는 것을 피한다. A*가 이웃 타일마다 부르는 핫패스라 층·경계 사각형으로 먼저 거른다.
	public static bool IsEntryBlocked(Unit mover, Vector2Int tile)
	{
		GameSession session = mover?.Session;
		if (session == null || session.BossRooms.Count == 0 || !Enabled) return false;

		for (int i = 0; i < session.BossRooms.Count; i++)
		{
			Room room = session.BossRooms[i];
			if (room.Floor != mover.currentFloor || !room.Bounds.Contains(tile)) continue;
			if (!session.roomGrid.TryGetValue(new Vector3Int(tile.x, tile.y, mover.currentFloor), out Room at) || at != room) continue;
			if (mover.currentRoom == room) return false;
			return HasLivingBoss(room);
		}
		return false;
	}

	// 이 층의 닫힌 보스방 id를 모은다(리더 경로가 그 방으로 이어지는 게이트를 빼는 데 쓴다).
	public static void CollectClosedRoomIds(GameSession session, int floor, HashSet<int> into)
	{
		into.Clear();
		if (!Enabled || session == null) return;
		foreach (var room in session.BossRooms)
			if (room.Floor == floor && HasLivingBoss(room)) into.Add(room.RoomId);
	}

	// 이 문 타일이 닫힌 보스방으로 이어지는 게이트의 문인가 — 문 후보에서 그 문을 뺀다.
	public static bool LeadsToClosedBossRoom(GameSession session, Vector3Int doorTile)
	{
		if (!Enabled || session == null || session.BossRooms.Count == 0 || session.cmap?.map.floors == null) return false;
		if (!session.TryGetGateKeyAt(doorTile, out int key)) return false;

		int floorIdx = key / 100000;
		int gateIdx = key % 100000;
		var floors = session.cmap.map.floors;
		if (floorIdx < 0 || floorIdx >= floors.Length || floors[floorIdx].gates == null || gateIdx >= floors[floorIdx].gates.Count) return false;

		Gate gate = floors[floorIdx].gates[gateIdx];
		foreach (var room in session.BossRooms)
			if (room.Floor == floorIdx && (gate.roomA == room.RoomId || gate.roomB == room.RoomId) && HasLivingBoss(room)) return true;
		return false;
	}

	private static bool HasLivingBoss(Room room)
	{
		var units = room.ContainedUnits;
		for (int i = 0; i < units.Count; i++)
		{
			Unit u = units[i];
			if (u != null && u.hp > 0f && u.unitType != null && u.unitType.isBoss) return true;
		}
		return false;
	}
}
