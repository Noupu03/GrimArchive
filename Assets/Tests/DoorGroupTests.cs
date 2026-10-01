#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 문 구조 개편(2026-10-01): 게이트의 문 타일을 "어느 방에 붙어 있는가"로 묶어 한 줄(폭 2 → 1×2)을 문 오브젝트 하나로 다룬다.
// DoorGeometry(순수) + InteractableObject의 멀티 타일 헬퍼(IsAdjacentTo/NearestTileTo/AllTiles)를 세션 없이 확인한다.
// 비주얼 늘림·개폐·재설치·인접 판정 호출부 연동은 Unity 플레이로만 확인된다.
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
}
#endif
