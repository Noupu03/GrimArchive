#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 03번 6-4·6-5장 보호 포메이션 자리 규칙(순수 함수) — 근접 전방 → 좌우 → 가장 가까운 이동 가능 타일, 원거리 후방 2칸 이상 → 좌우 후방 → 가장 가까운 비점유 타일, 여럿이 같은 자리로 몰리지 않는다.
public class EscortSlotTests
{
	private static readonly Vector2Int Escort = new Vector2Int(10, 10);
	private static readonly Vector2Int Up = Vector2Int.up; // 상호작용 유닛이 위쪽 오브젝트를 바라봄 → 전방 (10,11), 좌 (9,10), 우 (11,10), 후방 (10,9)...

	private static bool Everywhere(Vector2Int t) => !(t == Escort);

	[Test]
	public void Lateral_IsPerpendicularToFacing_AndDiagonalForDiagonalFacing()
	{
		Assert.AreEqual(new Vector2Int(-1, 0), EscortSlotMath.Lateral(Vector2Int.up));
		Assert.AreEqual(new Vector2Int(0, 1), EscortSlotMath.Lateral(Vector2Int.right));
		Assert.AreEqual(new Vector2Int(-1, 1), EscortSlotMath.Lateral(new Vector2Int(1, 1)));
	}

	[Test]
	public void Melee_FirstEscortTakesTheFront()
	{
		var slot = EscortSlotMath.PickSlot(Escort, Up, false, 2, Everywhere, new HashSet<Vector2Int>());
		Assert.AreEqual(new Vector2Int(10, 11), slot.Value);
	}

	[Test]
	public void Melee_FrontUnusable_UsesTheSidesLeftThenRight()
	{
		var front = new Vector2Int(10, 11); // 상호작용 오브젝트 타일 등
		var claimed = new HashSet<Vector2Int>();
		Assert.AreEqual(new Vector2Int(9, 10), EscortSlotMath.PickSlot(Escort, Up, false, 2, t => Everywhere(t) && t != front, claimed).Value);
		claimed.Add(new Vector2Int(9, 10));
		Assert.AreEqual(new Vector2Int(11, 10), EscortSlotMath.PickSlot(Escort, Up, false, 2, t => Everywhere(t) && t != front, claimed).Value);
	}

	[Test]
	public void Melee_FrontAndSidesUnusable_FallsBackToTheNearestMovableTile()
	{
		bool Free(Vector2Int t) => Everywhere(t) && t != new Vector2Int(10, 11) && t != new Vector2Int(9, 10) && t != new Vector2Int(11, 10);
		var slot = EscortSlotMath.PickSlot(Escort, Up, false, 2, Free, new HashSet<Vector2Int>());
		// 체비셰프 1칸 고리에서 남은 칸 중 직선 거리가 짧은 쪽(대각선보다 직교가 가깝다) — 후방 (10,9)가 첫째.
		Assert.AreEqual(new Vector2Int(10, 9), slot.Value);
	}

	[Test]
	public void Melee_ThreeEscorts_FillFrontThenBothSides_NoSharedSlot()
	{
		var ranged = new List<bool> { false, false, false };
		var slots = EscortSlotMath.AssignSlots(Escort, Up, ranged, 2, (i, t) => Everywhere(t));
		Assert.AreEqual(new Vector2Int(10, 11), slots[0].Value);
		Assert.AreEqual(new Vector2Int(9, 10), slots[1].Value);
		Assert.AreEqual(new Vector2Int(11, 10), slots[2].Value);
	}

	[Test]
	public void Melee_FourthEscort_GoesToTheNearestTile_NotTheSameSlot()
	{
		var ranged = new List<bool> { false, false, false, false };
		var slots = EscortSlotMath.AssignSlots(Escort, Up, ranged, 2, (i, t) => Everywhere(t));
		var seen = new HashSet<Vector2Int>();
		foreach (var s in slots) Assert.IsTrue(s.HasValue && seen.Add(s.Value), "자리가 겹치면 안 된다");
		Assert.AreEqual(new Vector2Int(10, 9), slots[3].Value); // 후방 인접 — 남은 직교 칸 중 하나(전방·좌·우 다음으로 가깝다)
	}

	[Test]
	public void Ranged_PrefersStraightBack_TwoTilesAway()
	{
		var slot = EscortSlotMath.PickSlot(Escort, Up, true, 2, Everywhere, new HashSet<Vector2Int>());
		Assert.AreEqual(new Vector2Int(10, 8), slot.Value);
	}

	[Test]
	public void Ranged_StraightBackTaken_GoesDeeperBeforeUsingTheBackDiagonals()
	{
		var claimed = new HashSet<Vector2Int> { new Vector2Int(10, 8) };
		var slot = EscortSlotMath.PickSlot(Escort, Up, true, 2, Everywhere, claimed);
		Assert.AreEqual(new Vector2Int(10, 7), slot.Value);
	}

	[Test]
	public void Ranged_StraightBackBlocked_UsesLeftBackThenRightBack()
	{
		bool Free(Vector2Int t) => Everywhere(t) && !(t.x == 10 && t.y <= 8); // 정후방 줄 전체가 막힘
		var claimed = new HashSet<Vector2Int>();
		var first = EscortSlotMath.PickSlot(Escort, Up, true, 2, Free, claimed);
		Assert.AreEqual(new Vector2Int(9, 8), first.Value);
		claimed.Add(first.Value);
		Assert.AreEqual(new Vector2Int(11, 8), EscortSlotMath.PickSlot(Escort, Up, true, 2, Free, claimed).Value);
	}

	[Test]
	public void Ranged_NoSpaceBehind_StillKeepsTwoTilesAwayWhenAnotherSideAllows()
	{
		// 후방 전부 벽 — 전방 쪽에 2칸 이상 떨어진 타일이 있으면 가까운 타일보다 먼저 쓴다.
		bool Free(Vector2Int t) => Everywhere(t) && t.y >= 10;
		var slot = EscortSlotMath.PickSlot(Escort, Up, true, 2, Free, new HashSet<Vector2Int>());
		Assert.IsTrue(slot.HasValue);
		Assert.IsTrue(Mathf.Max(Mathf.Abs(slot.Value.x - Escort.x), Mathf.Abs(slot.Value.y - Escort.y)) >= 2, "가능하면 최소 2칸 거리를 유지한다");
	}

	[Test]
	public void Ranged_TightSpace_FallsBackToTheClosestFreeTile()
	{
		// 상호작용 유닛 주변 1칸 고리만 비어 있다 — 2칸 거리를 못 지키므로 가장 가까운 비점유 타일.
		bool Free(Vector2Int t) => Mathf.Max(Mathf.Abs(t.x - Escort.x), Mathf.Abs(t.y - Escort.y)) == 1;
		var slot = EscortSlotMath.PickSlot(Escort, Up, true, 2, Free, new HashSet<Vector2Int>());
		Assert.IsTrue(slot.HasValue);
		Assert.AreEqual(1, Mathf.Max(Mathf.Abs(slot.Value.x - Escort.x), Mathf.Abs(slot.Value.y - Escort.y)));
	}

	[Test]
	public void AssignSlots_MixedRoles_NeverShareATile_AndNoFreeTileMeansNoSlot()
	{
		var ranged = new List<bool> { false, false, true, true };
		var slots = EscortSlotMath.AssignSlots(Escort, Up, ranged, 2, (i, t) => Everywhere(t));
		var seen = new HashSet<Vector2Int>();
		foreach (var s in slots) Assert.IsTrue(s.HasValue && seen.Add(s.Value), "자리가 겹치면 안 된다");

		var none = EscortSlotMath.AssignSlots(Escort, Up, new List<bool> { false }, 2, (i, t) => false);
		Assert.IsFalse(none[0].HasValue);
	}

	[Test]
	public void AssignSlots_IsDeterministic_SoRecomputingEveryTickDoesNotFlap()
	{
		var ranged = new List<bool> { false, false, true };
		var a = EscortSlotMath.AssignSlots(Escort, Up, ranged, 2, (i, t) => Everywhere(t));
		var b = EscortSlotMath.AssignSlots(Escort, Up, ranged, 2, (i, t) => Everywhere(t));
		Assert.AreEqual(a, b);
	}
}
#endif
