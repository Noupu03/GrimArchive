#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 문 묶음: 게이트의 문 타일을 '어느 방에 붙어 있는가'로 묶어 한 줄(폭 2 → 1×2)을 문 오브젝트 하나로 다룬다. DoorGeometry(순수)와 InteractableObject의 멀티 타일 헬퍼(IsAdjacentTo/NearestTileTo/AllTiles)를 세션 없이 확인하며, 비주얼·개폐·재설치·인접 판정 호출부 연동은 플레이로만 확인된다.
// ========================================================================

public class DoorGroupTests
{
	// 수직 게이트(위아래 방): 폭 2칸, 아래 방 쪽 줄 y=47 / 위 방 쪽 줄 y=48.
	private static List<Vector2Int> RowA() => new List<Vector2Int> { new Vector2Int(6, 47), new Vector2Int(7, 47) };
	private static List<Vector2Int> RowB() => new List<Vector2Int> { new Vector2Int(6, 48), new Vector2Int(7, 48) };
	private static int RoomByY(Vector2Int t) => t.y <= 47 ? 44 : 45;

	[Test]
	public void GroupByRoom_SplitsAGateIntoOneGroupPerRoomSide_EachOneByTwo()
	{
		var groups = DoorGeometry.GroupByRoom(new List<List<Vector2Int>> { RowA(), RowB() }, RoomByY);

		Assert.AreEqual(2, groups.Count);
		Assert.AreEqual(44, groups[0].RoomId);
		Assert.AreEqual(45, groups[1].RoomId);
		Assert.AreEqual(2, groups[0].Length);
		Assert.AreEqual(2, groups[1].Length);
		Assert.AreEqual(new Vector2Int(6, 47), groups[0].Tiles[0], "대표 타일은 가장 작은 좌표");
	}

	[Test]
	public void GroupByRoom_UsesTheRoomOfEachTile_NotTheRowOrder()
	{
		// 줄 배열 순서가 뒤바뀌어도(B 쪽이 먼저) 각 묶음은 그 타일이 실제 속한 방에 붙는다 — GetGateDoorTiles의 순서는 기하학적 규칙일 뿐 방과 무관하다.
		var groups = DoorGeometry.GroupByRoom(new List<List<Vector2Int>> { RowB(), RowA() }, RoomByY);

		Assert.AreEqual(2, groups.Count);
		Assert.AreEqual(45, groups[0].RoomId);
		Assert.AreEqual(47, groups[1].Tiles[0].y);
		Assert.AreEqual(44, groups[1].RoomId);
		Assert.AreEqual(48, groups[0].Tiles[0].y);
	}

	[Test]
	public void GroupByRoom_SortsTilesAscending_EvenWhenInputIsUnordered()
	{
		var shuffled = new List<Vector2Int> { new Vector2Int(7, 47), new Vector2Int(6, 47) };
		var groups = DoorGeometry.GroupByRoom(new List<List<Vector2Int>> { shuffled }, RoomByY);

		Assert.AreEqual(new Vector2Int(6, 47), groups[0].Tiles[0]);
		Assert.AreEqual(new Vector2Int(7, 47), groups[0].Tiles[1]);
	}

	[Test]
	public void GroupByRoom_BothRowsInTheSameRoom_StillSplitsByRow()
	{
		// 비정상 데이터(한 방이 두 줄을 다 차지) — 한 문 오브젝트가 문턱 두 줄을 먹지 않게 줄 단위로 분리한다.
		var groups = DoorGeometry.GroupByRoom(new List<List<Vector2Int>> { RowA(), RowB() }, _ => 44);

		Assert.AreEqual(2, groups.Count);
		Assert.AreEqual(2, groups[0].Length);
		Assert.AreEqual(2, groups[1].Length);
	}

	[Test]
	public void GroupByRoom_ARowSpanningTwoRooms_SplitsByRoom()
	{
		var groups = DoorGeometry.GroupByRoom(new List<List<Vector2Int>> { RowA() }, t => t.x == 6 ? 1 : 2);

		Assert.AreEqual(2, groups.Count);
		Assert.AreEqual(1, groups[0].Length);
		Assert.AreEqual(1, groups[1].Length);
	}

	[Test]
	public void GroupByRoom_UnknownRoom_GroupsPerRow()
	{
		var groups = DoorGeometry.GroupByRoom(new List<List<Vector2Int>> { RowA(), RowB() }, _ => -1);

		Assert.AreEqual(2, groups.Count);
		Assert.AreEqual(-1, groups[0].RoomId);
	}

	[Test]
	public void Center_IsBetweenTheTwoTiles_InTileCoordinates()
	{
		var groups = DoorGeometry.GroupByRoom(new List<List<Vector2Int>> { RowA(), RowB() }, RoomByY);

		Assert.AreEqual(new Vector2(7.0f, 47.5f), groups[0].Center);
		Assert.AreEqual(new Vector2(7.0f, 48.5f), groups[1].Center);

		// 수평 게이트(좌우 방): x가 줄 방향, y가 폭 방향.
		var horizontal = new List<Vector2Int> { new Vector2Int(10, 20), new Vector2Int(10, 21) };
		Assert.AreEqual(new Vector2(10.5f, 21.0f), DoorGeometry.CenterOf(horizontal));
	}

	[Test]
	public void Center_Vector3Overload_MatchesVector2Overload()
	{
		var v2 = new List<Vector2Int> { new Vector2Int(6, 47), new Vector2Int(7, 47) };
		var v3 = new List<Vector3Int> { new Vector3Int(6, 47, 1), new Vector3Int(7, 47, 1) };
		Assert.AreEqual(DoorGeometry.CenterOf(v2), DoorGeometry.CenterOf(v3));
	}

	[Test]
	public void IsAdjacentToAny_TrueNextToEitherTile_FalseAtDistanceTwo()
	{
		var tiles = new List<Vector3Int> { new Vector3Int(6, 47, 1), new Vector3Int(7, 47, 1) };

		Assert.IsTrue(DoorGeometry.IsAdjacentToAny(new Vector2Int(5, 46), tiles));   // 왼쪽 칸의 대각
		Assert.IsTrue(DoorGeometry.IsAdjacentToAny(new Vector2Int(8, 46), tiles));   // 오른쪽 칸의 대각 — 어느 칸에 붙어도 된다
		Assert.IsTrue(DoorGeometry.IsAdjacentToAny(new Vector2Int(7, 46), tiles));
		Assert.IsFalse(DoorGeometry.IsAdjacentToAny(new Vector2Int(9, 46), tiles));
		Assert.IsFalse(DoorGeometry.IsAdjacentToAny(new Vector2Int(6, 45), tiles));  // 문 앞 2칸 — 대기 자리, 공격은 못 한다
	}

	[Test]
	public void NearestTile_PicksTheCloserOne_AndFallsBackToFromWhenEmpty()
	{
		var tiles = new List<Vector3Int> { new Vector3Int(6, 47, 1), new Vector3Int(7, 47, 1) };

		Assert.AreEqual(new Vector2Int(7, 47), DoorGeometry.NearestTile(new Vector2Int(9, 47), tiles));
		Assert.AreEqual(new Vector2Int(6, 47), DoorGeometry.NearestTile(new Vector2Int(3, 47), tiles));
		Assert.AreEqual(new Vector2Int(3, 3), DoorGeometry.NearestTile(new Vector2Int(3, 3), new List<Vector3Int>()));
	}

	[Test]
	public void IsMirroredLeaf_FrontHalfKeepsArt_BackHalfMirrored_SoTheLeavesSplitOutward()
	{
		// door_open 아트는 왼쪽 7열에만 문짝이 있다 — 첫 칸은 그대로(문짝이 앞쪽 바깥 끝), 두 번째 칸은 반전(문짝이 뒤쪽 바깥 끝)이어야 양쪽 여닫이가 된다.
		Assert.IsFalse(DoorGeometry.IsMirroredLeaf(0, 2));
		Assert.IsTrue(DoorGeometry.IsMirroredLeaf(1, 2));
		// 폭이 늘어도 앞쪽 절반/뒤쪽 절반으로 나뉜다(홀수는 가운데가 앞쪽).
		Assert.IsFalse(DoorGeometry.IsMirroredLeaf(0, 3));
		Assert.IsFalse(DoorGeometry.IsMirroredLeaf(1, 3));
		Assert.IsTrue(DoorGeometry.IsMirroredLeaf(2, 3));
		Assert.IsFalse(DoorGeometry.IsMirroredLeaf(1, 4));
		Assert.IsTrue(DoorGeometry.IsMirroredLeaf(2, 4));
		Assert.IsTrue(DoorGeometry.IsMirroredLeaf(3, 4));
		// 한 칸짜리 문은 반전하지 않는다.
		Assert.IsFalse(DoorGeometry.IsMirroredLeaf(0, 1));
	}

	// ── InteractableObject 멀티 타일 헬퍼 ────────────────────────────────

	private static InteractableObject DoorOn(params Vector3Int[] tiles)
	{
		var obj = new InteractableObject("Door_1_6_47", tiles[0], 0f, tags: new List<string> { DoorSystem.DoorTag }, isFullyBlocking: true, doorHp: 300f);
		obj.OccupiedTiles = new List<Vector3Int>(tiles);
		return obj;
	}

	[Test]
	public void InteractableObject_AllTiles_ListsEveryOccupiedTile_OrJustPosition()
	{
		var door = DoorOn(new Vector3Int(6, 47, 1), new Vector3Int(7, 47, 1));
		Assert.AreEqual(new List<Vector3Int> { new Vector3Int(6, 47, 1), new Vector3Int(7, 47, 1) }, new List<Vector3Int>(door.AllTiles()));

		var single = new InteractableObject("loot", new Vector3Int(3, 3, 0), 1f);
		Assert.AreEqual(new List<Vector3Int> { new Vector3Int(3, 3, 0) }, new List<Vector3Int>(single.AllTiles()));
	}

	[Test]
	public void InteractableObject_IsAdjacentTo_CoversBothTilesOfADoor_ButSingleTileObjectsKeepOldBehavior()
	{
		var door = DoorOn(new Vector3Int(6, 47, 1), new Vector3Int(7, 47, 1));
		Assert.IsTrue(door.IsAdjacentTo(new Vector2Int(8, 46)));
		Assert.IsFalse(door.IsAdjacentTo(new Vector2Int(9, 46)));

		var core = new InteractableObject("core", new Vector3Int(10, 10, 1), 0f);
		Assert.IsTrue(core.IsAdjacentTo(new Vector2Int(11, 11)));
		Assert.IsFalse(core.IsAdjacentTo(new Vector2Int(12, 10)));
	}

	[Test]
	public void InteractableObject_NearestTileTo_ReturnsTheTileClosestToTheAttacker()
	{
		var door = DoorOn(new Vector3Int(6, 47, 1), new Vector3Int(7, 47, 1));
		Assert.AreEqual(new Vector2Int(7, 47), door.NearestTileTo(new Vector2Int(9, 46)));

		var core = new InteractableObject("core", new Vector3Int(10, 10, 1), 0f);
		Assert.AreEqual(new Vector2Int(10, 10), core.NearestTileTo(new Vector2Int(2, 2)));
	}

	// ── 목책 자리: 문 줄을 방 안쪽으로 한 칸 민 칸들 ──

	[Test]
	public void Inward_VerticalGate_PointsIntoTheRoomsOwnSide()
	{
		// 아래 방(44)은 y <= 47 쪽이다 — 아래 방 쪽 문 줄(y=47)의 안쪽은 아래(-y), 위 방(45) 쪽 줄(y=48)의 안쪽은 위(+y).
		Assert.IsTrue(DoorGeometry.TryGetInward(false, new Vector2Int(6, 47), t => RoomByY(t) == 44, out Vector2Int lower));
		Assert.AreEqual(new Vector2Int(0, -1), lower);

		Assert.IsTrue(DoorGeometry.TryGetInward(false, new Vector2Int(6, 48), t => RoomByY(t) == 45, out Vector2Int upper));
		Assert.AreEqual(new Vector2Int(0, 1), upper);
	}

	[Test]
	public void Inward_HorizontalGate_UsesTheXAxis()
	{
		// 수평 게이트(왼쪽 방 x <= 9, 오른쪽 방 x >= 10): 왼쪽 방 쪽 줄(x=9)의 안쪽은 -x, 오른쪽 방 쪽 줄(x=10)의 안쪽은 +x.
		Assert.IsTrue(DoorGeometry.TryGetInward(true, new Vector2Int(9, 5), t => t.x <= 9, out Vector2Int left));
		Assert.AreEqual(new Vector2Int(-1, 0), left);

		Assert.IsTrue(DoorGeometry.TryGetInward(true, new Vector2Int(10, 5), t => t.x >= 10, out Vector2Int right));
		Assert.AreEqual(new Vector2Int(1, 0), right);
	}

	[Test]
	public void Inward_AmbiguousOrUnknown_ReturnsFalse()
	{
		// 양쪽 이웃이 모두 같은 방이거나 어느 쪽도 아니면 안쪽을 정할 수 없다 — 그 문에는 목책을 세우지 않는다.
		Assert.IsFalse(DoorGeometry.TryGetInward(false, new Vector2Int(6, 47), _ => true, out _));
		Assert.IsFalse(DoorGeometry.TryGetInward(false, new Vector2Int(6, 47), _ => false, out _));
	}

	[Test]
	public void Shifted_KeepsOrderAndMovesEveryTile()
	{
		var fence = DoorGeometry.Shifted(RowA(), new Vector2Int(0, -1));

		Assert.AreEqual(2, fence.Count);
		Assert.AreEqual(new Vector2Int(6, 46), fence[0]);
		Assert.AreEqual(new Vector2Int(7, 46), fence[1]);
		Assert.AreEqual(new Vector2Int(6, 47), RowA()[0], "원본 문 줄은 건드리지 않는다");
	}

	// ── 문 제어: 문 앞·뒤 비움 구역과 열기·닫기 판정(코어 점령 v0.2 6·19장) ──

	private static List<Vector3Int> DoorTiles3D(params (int x, int y)[] tiles)
	{
		var list = new List<Vector3Int>();
		foreach (var t in tiles) list.Add(new Vector3Int(t.x, t.y, 1));
		return list;
	}

	[Test]
	public void Clearance_VerticalGate_ExtendsTwoTilesAlongYWithTheDoorWidth()
	{
		var area = new List<Vector2Int>();
		DoorGeometry.ClearanceTiles(DoorTiles3D((6, 47), (7, 47)), horizontalGate: false, depth: 2, area);

		Assert.AreEqual(10, area.Count, "폭 2 × (앞 2 + 문 줄 1 + 뒤 2) = 10칸");
		CollectionAssert.Contains(area, new Vector2Int(6, 45));
		CollectionAssert.Contains(area, new Vector2Int(7, 49));
		CollectionAssert.Contains(area, new Vector2Int(6, 47), "문 줄 자신도 비어 있어야 한다");
		CollectionAssert.DoesNotContain(area, new Vector2Int(5, 47), "문 폭 밖 옆 칸은 구역이 아니다");
		CollectionAssert.DoesNotContain(area, new Vector2Int(6, 50));
	}

	[Test]
	public void Clearance_HorizontalGate_ExtendsTwoTilesAlongX()
	{
		var area = new List<Vector2Int>();
		DoorGeometry.ClearanceTiles(DoorTiles3D((9, 5), (9, 6)), horizontalGate: true, depth: 2, area);

		Assert.AreEqual(10, area.Count);
		CollectionAssert.Contains(area, new Vector2Int(7, 5));
		CollectionAssert.Contains(area, new Vector2Int(11, 6));
		CollectionAssert.DoesNotContain(area, new Vector2Int(9, 4));
		CollectionAssert.DoesNotContain(area, new Vector2Int(12, 5));
	}

	[Test]
	public void DoorControl_OnlyPlayerOwnedDoorsAreControllable()
	{
		Assert.AreEqual(DoorControlBlock.NotPlayerDoor,
			DoorControlRules.Check(FactionType.Human, FactionType.Player, coreFunctionsActive: true, wantOpen: true, currentlyOpen: false, anyUnitInClearance: false),
			"인류 전용 문은 플레이어가 제어할 수 없다");
		Assert.AreEqual(DoorControlBlock.NotPlayerDoor,
			DoorControlRules.Check(FactionType.Wild, FactionType.Player, true, true, false, false));
		Assert.AreEqual(DoorControlBlock.None,
			DoorControlRules.Check(FactionType.Player, FactionType.Player, true, true, false, false));
	}

	[Test]
	public void DoorControl_StoppedWhileCoreIsDestroyed()
	{
		Assert.AreEqual(DoorControlBlock.CoreDestroyed,
			DoorControlRules.Check(FactionType.Player, FactionType.Player, coreFunctionsActive: false, wantOpen: true, currentlyOpen: false, anyUnitInClearance: false));
		Assert.AreEqual(DoorControlBlock.CoreDestroyed,
			DoorControlRules.Check(FactionType.Player, FactionType.Player, false, wantOpen: false, currentlyOpen: true, anyUnitInClearance: false));
	}

	[Test]
	public void DoorControl_CloseNeedsAnEmptyClearanceButOpenNeverDoes()
	{
		Assert.AreEqual(DoorControlBlock.UnitsNearby,
			DoorControlRules.Check(FactionType.Player, FactionType.Player, true, wantOpen: false, currentlyOpen: true, anyUnitInClearance: true),
			"문 앞·뒤에 유닛이 있으면 닫을 수 없다");
		Assert.AreEqual(DoorControlBlock.None,
			DoorControlRules.Check(FactionType.Player, FactionType.Player, true, wantOpen: false, currentlyOpen: true, anyUnitInClearance: false));
		Assert.AreEqual(DoorControlBlock.None,
			DoorControlRules.Check(FactionType.Player, FactionType.Player, true, wantOpen: true, currentlyOpen: false, anyUnitInClearance: true),
			"열기는 근처 유닛과 무관하다");
		Assert.AreEqual(DoorControlBlock.AlreadyInState,
			DoorControlRules.Check(FactionType.Player, FactionType.Player, true, wantOpen: true, currentlyOpen: true, anyUnitInClearance: false));
		StringAssert.Contains("문 근처에 유닛이 있어", DoorControlRules.Describe(DoorControlBlock.UnitsNearby));
	}

	// ── 열려 있는 문·목책 판정(문 제어·인류 문 자동 개폐) ──

	[Test]
	public void HeldOpen_ManualOrAutoOpenDoorPassesEveryone_FenceFollowsItsLinkedDoor()
	{
		var door = new InteractableObject("Door_a", new Vector3Int(6, 47, 1), 0f, 0f, new List<string> { DoorSystem.DoorTag }, isFullyBlocking: true, doorHp: 300f);
		var fence = new InteractableObject("Fence_a", new Vector3Int(6, 46, 1), 0f, 0f, new List<string> { DoorSystem.FenceTag }, doorHp: 200f);
		fence.LinkedGateDoor = door;

		Assert.IsFalse(DoorSystem.IsHeldOpen(door), "기본은 닫힘 — 보유 진영만 통과");
		Assert.IsFalse(DoorSystem.IsHeldOpen(fence));

		door.DoorAutoOpen = true; // 인류 문이 인류의 접근으로 열림
		Assert.IsTrue(DoorSystem.IsHeldOpen(door));
		Assert.IsTrue(DoorSystem.IsHeldOpen(fence), "연결된 목책도 문이 열린 동안 통과할 수 있다(열린 문을 따라 들어오는 흐름)");

		door.DoorAutoOpen = false; // 구역이 비어 닫힘
		Assert.IsFalse(DoorSystem.IsHeldOpen(fence));

		door.DoorManualOpen = true; // 플레이어가 열어 둠
		Assert.IsTrue(DoorSystem.IsHeldOpen(door));

		door.DoorManualOpen = false;
		door.DoorAutoOpen = true;
		door.IsCollected = true; // 문이 파괴됨 — 목책 연결이 끊긴다
		Assert.IsFalse(DoorSystem.IsHeldOpen(fence), "파괴된 문에 연결된 목책은 열림이 아니다");
		Assert.IsFalse(DoorSystem.IsHeldOpen(null));
	}

	// ── 야생 생성 정원(코어 점령 v0.2 11-1장·20-2장) ──

	[Test]
	public void WildSpawn_RespectsRoomMaxPopulation()
	{
		Assert.IsTrue(WildSpawnMath.CanSpawn(livingPopulation: 9, newPopulation: 1, roomMaxPopulation: 10, fallbackCapacity: 6), "정원 10, 생존 9 → 인구수 1 생성 가능");
		Assert.IsFalse(WildSpawnMath.CanSpawn(9, 2, 10, 6), "인구수 2는 상한 초과");
		Assert.IsFalse(WildSpawnMath.CanSpawn(10, 1, 10, 6));
	}

	[Test]
	public void WildSpawn_FallsBackToMvpCapWhenRoomHasNoMaxPopulation()
	{
		Assert.IsTrue(WildSpawnMath.CanSpawn(5, 1, 0, 6));
		Assert.IsFalse(WildSpawnMath.CanSpawn(6, 1, 0, 6));
	}
}
#endif
