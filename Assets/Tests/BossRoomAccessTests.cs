#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 인류의 보스방 출입 제한(2026-10-26 사용자 확정) + 정상 귀환 상태 테스트(NUnit, #if UNITY_INCLUDE_TESTS).
// 이동 실행·A*·회피 후보가 모두 부르는 Human.IsRoomLeaveBlocked까지 실제 GameSession/Room/Human 객체로 확인한다. 리더 경로 계획(LeaderRoutePlanner.Plan)과 웨이브 흐름은 맵이 필요해 플레이 확인 대상이다.
// ========================================================================

public class BossRoomAccessTests
{
	private static readonly RectInt BossBounds = new RectInt(10, 10, 6, 6);

	private static void Attach(Unit unit, GameSession session)
		=> typeof(Unit).GetField("_gameSession", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(unit, session);

	private static Room NewBossRoom(GameSession session, int floor = 1, int roomId = 7)
	{
		var room = new Room { RoomId = roomId, Floor = floor, Bounds = BossBounds };
		session.BossRooms.Add(room);
		for (int x = BossBounds.xMin; x < BossBounds.xMax; x++)
			for (int y = BossBounds.yMin; y < BossBounds.yMax; y++)
				session.roomGrid[new Vector3Int(x, y, floor)] = room;
		return room;
	}

	private static Monster NewBoss(Room room, float hp)
	{
		var boss = ScriptableObject.CreateInstance<Monster>();
		boss.unitType = new BossGolem();
		boss.currentFloor = room.Floor;
		boss.Health.maxHp = 500f;
		boss.Health.hp = hp;
		room.AddUnit(boss);
		return boss;
	}

	private static Human NewHuman(GameSession session, Vector2Int pos, int floor = 1)
	{
		var h = ScriptableObject.CreateInstance<Human>();
		h.unitType = new Knight { footprint = new Vector2(1, 1) };
		h.position = pos;
		h.currentFloor = floor;
		h.Health.maxHp = 100f;
		h.Health.hp = 100f;
		Attach(h, session);
		return h;
	}

	[TestCase(true, true, true)]
	[TestCase(true, false, false)]  // 보스를 처치하면 풀린다
	[TestCase(false, true, false)]  // 보스방이 아니면 보스 유닛이 있어도(예: 필드 보스) 이 규칙의 대상이 아니다
	[TestCase(false, false, false)]
	public void IsClosed_NeedsBossRoomAndLivingBoss(bool isBossRoom, bool bossAlive, bool expected)
	{
		Assert.AreEqual(expected, BossRoomAccess.IsClosed(isBossRoom, bossAlive));
	}

	[Test]
	public void LivingBoss_BlocksHumanEntering_FromOutside()
	{
		var session = new GameSession();
		Room room = NewBossRoom(session);
		NewBoss(room, hp: 500f);
		Human human = NewHuman(session, new Vector2Int(9, 12)); // 방 바로 밖

		Assert.IsTrue(BossRoomAccess.IsClosedToHumans(session, room));
		Assert.IsTrue(BossRoomAccess.IsEntryBlocked(human, new Vector2Int(10, 12)), "방 안 타일로 들어가는 걸음은 막힌다");
		Assert.IsTrue(human.IsRoomLeaveBlocked(new Vector2Int(10, 12)), "이동 실행·A*·회피가 모두 부르는 Human.IsRoomLeaveBlocked에서도 막힌다");
		Assert.IsFalse(BossRoomAccess.IsEntryBlocked(human, new Vector2Int(8, 12)), "방 밖 타일은 막지 않는다");
	}

	[Test]
	public void DeadBoss_OpensTheRoom()
	{
		var session = new GameSession();
		Room room = NewBossRoom(session);
		Monster boss = NewBoss(room, hp: 500f);
		Human human = NewHuman(session, new Vector2Int(9, 12));
		Assert.IsTrue(BossRoomAccess.IsEntryBlocked(human, new Vector2Int(10, 12)));

		boss.Health.hp = 0f;

		Assert.IsFalse(BossRoomAccess.IsClosedToHumans(session, room), "보스를 처치하면 인류도 들어갈 수 있다");
		Assert.IsFalse(BossRoomAccess.IsEntryBlocked(human, new Vector2Int(10, 12)));
		Assert.IsFalse(human.IsRoomLeaveBlocked(new Vector2Int(10, 12)));
	}

	[Test]
	public void BossRoomWithoutABoss_StaysOpen()
	{
		// 지금 보스 골렘은 1층에만 있다 — 보스 유닛이 없는 다른 층 보스방은 "아직 죽지 않은 보스"가 없으니 막지 않는다.
		var session = new GameSession();
		Room room = NewBossRoom(session, floor: 2, roomId: 3);
		Human human = NewHuman(session, new Vector2Int(9, 12), floor: 2);

		Assert.IsFalse(BossRoomAccess.IsEntryBlocked(human, new Vector2Int(10, 12)));
	}

	[Test]
	public void HumanAlreadyInside_IsNotTrapped()
	{
		var session = new GameSession();
		Room room = NewBossRoom(session);
		NewBoss(room, hp: 500f);
		Human human = NewHuman(session, new Vector2Int(12, 12));
		human.currentRoom = room;

		Assert.IsFalse(BossRoomAccess.IsEntryBlocked(human, new Vector2Int(13, 12)), "이미 안에 있는 유닛은 방 안에서 움직일 수 있어야 나올 수도 있다");
	}

	[Test]
	public void OtherFloor_IsNotAffected()
	{
		var session = new GameSession();
		Room room = NewBossRoom(session, floor: 1);
		NewBoss(room, hp: 500f);
		Human human = NewHuman(session, new Vector2Int(9, 12), floor: 2); // 같은 좌표 범위의 다른 층

		Assert.IsFalse(BossRoomAccess.IsEntryBlocked(human, new Vector2Int(10, 12)));
	}

	[Test]
	public void PlayerManualMoveCommand_IsTheOnlyException()
	{
		var session = new GameSession();
		Room room = NewBossRoom(session);
		NewBoss(room, hp: 500f);
		Human human = NewHuman(session, new Vector2Int(9, 12));

		Assert.IsTrue(human.IsRoomLeaveBlocked(new Vector2Int(10, 12)));
		human.isManualMoveCommand = true; // 방 이탈 금지의 플레이어 명령 예외와 같은 규칙(테스트 조작)
		Assert.IsFalse(human.IsRoomLeaveBlocked(new Vector2Int(10, 12)));
	}

	[Test]
	public void CollectClosedRoomIds_ListsOnlyLivingBossRoomsOfThatFloor()
	{
		var session = new GameSession();
		Room withBoss = NewBossRoom(session, floor: 1, roomId: 7);
		NewBoss(withBoss, hp: 500f);
		NewBossRoom(session, floor: 2, roomId: 9); // 다른 층 — 제외

		var ids = new HashSet<int> { 123 }; // 호출이 비워 주는지도 확인한다
		BossRoomAccess.CollectClosedRoomIds(session, 1, ids);

		CollectionAssert.AreEquivalent(new[] { 7 }, ids);
	}

	// ── 정상 귀환 상태(행동경로 01번 8장) ──

	[Test]
	public void NormalReturn_StartsOnce_AndStopsNewRally()
	{
		var party = new Party("p", "테스트 파티");
		Assert.IsFalse(party.NormalReturning);

		party.BeginNormalReturn(10f);
		party.BeginNormalReturn(99f); // 한 번 정하면 되돌리거나 다시 시작하지 않는다

		Assert.IsTrue(party.NormalReturning);
		Assert.AreEqual(10f, party.NormalReturnStartedAt, 1e-5f);
		party.TryStartRally();
		Assert.IsFalse(party.IsRallyActive, "정상 귀환 중에는 새 집결을 시작하지 않는다(집결을 해제한 채 출구로 간다)");
	}

	[Test]
	public void NormalReturn_ClearsRallyAndAdvanceIntent()
	{
		var party = new Party("p", "테스트 파티");
		party.ReadyToAdvance = true;
		party.IsRallyActive = true;

		party.BeginNormalReturn(0f);

		Assert.IsFalse(party.ReadyToAdvance);
		Assert.IsFalse(party.IsRallyActive);
	}
}
#endif
