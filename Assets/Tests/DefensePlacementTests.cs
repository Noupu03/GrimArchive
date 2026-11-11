#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 배치모드 설계 명세서 v0.2 — 순서·정원·유입·범위의 순수 계산(DefensePlacementMath)과 층별 선택 상태(PlacementSelection). 세션 없이 EditMode에서 돈다.
// ========================================================================

public class DefensePlacementTests
{
	private static Monster NewMonster(int pop = 1, float hp = 10f)
	{
		var m = ScriptableObject.CreateInstance<Monster>();
		m.populationCost = pop;
		m.hp = hp;
		return m;
	}

	// ── 순차 배치 순서(1-9 표1) ────────────────────────────────────────

	[Test]
	public void SequentialOrder_NearestToDragStartFirst_ThenUpper_ThenLeft()
	{
		// 드래그 시작점 (0,0): A는 거리 1(위쪽 (0,1)), B는 거리 1(아래쪽 (0,-1)), C는 거리 1(왼쪽 (-1,0)), D는 거리 1(오른쪽 (1,0)), E는 더 멀다
		var items = new List<string> { "E", "D", "C", "B", "A" };
		var centers = new Dictionary<string, Vector2>
		{
			["A"] = new Vector2(0, 1), ["B"] = new Vector2(0, -1), ["C"] = new Vector2(-1, 0), ["D"] = new Vector2(1, 0), ["E"] = new Vector2(5, 5),
		};
		DefensePlacementMath.SortForSequentialPlacement(items, k => centers[k], Vector2.zero);
		Assert.AreEqual(new[] { "A", "C", "D", "B", "E" }, items.ToArray(), "거리가 같으면 위쪽, 다시 같으면 왼쪽부터 — 무작위 추첨 없음");
	}

	[Test]
	public void SequentialOrder_IsStableForRepeatedRuns()
	{
		var centers = new Dictionary<int, Vector2> { [1] = new Vector2(2, 0), [2] = new Vector2(1, 0), [3] = new Vector2(3, 0) };
		var a = new List<int> { 1, 2, 3 };
		var b = new List<int> { 3, 1, 2 };
		DefensePlacementMath.SortForSequentialPlacement(a, k => centers[k], Vector2.zero);
		DefensePlacementMath.SortForSequentialPlacement(b, k => centers[k], Vector2.zero);
		Assert.AreEqual(a, b);
		Assert.AreEqual(new[] { 2, 1, 3 }, a.ToArray());
	}

	[Test]
	public void SwapOrder_ExchangesTwoPortraits_AndIgnoresInvalid()
	{
		var order = new List<string> { "A", "B", "C" };
		Assert.IsTrue(DefensePlacementMath.SwapOrder(order, 0, 2));
		Assert.AreEqual(new[] { "C", "B", "A" }, order.ToArray(), "사례 8: A와 C의 순서 교환");
		Assert.IsFalse(DefensePlacementMath.SwapOrder(order, 1, 1));
		Assert.IsFalse(DefensePlacementMath.SwapOrder(order, 0, 5));
	}

	// ── 정원(1-5장) ────────────────────────────────────────────────────

	[TestCase(5, 1, 6, true)]   // 방어 배치 5/6에 인구 1을 더하면 6/6 — 가능
	[TestCase(6, 1, 6, false)]  // 6/6에 더 넣을 수 없다
	[TestCase(0, 3, 2, false)]
	public void CanSaveWithinCapacity_AddsOwnCostToPlannedOthers(int plannedOthers, int cost, int max, bool expected)
		=> Assert.AreEqual(expected, DefensePlacementMath.CanSaveWithinCapacity(plannedOthers, cost, max));

	[Test]
	public void PlannedOthers_SameRoomExcludesOwnOldShare()
	{
		// 같은 방에서 자기 위치만 바꿀 때는 기존 몫을 빼고 계산한다 — 6/6 방에서 위치만 옮겨도 정원 초과가 아니다
		int others = DefensePlacementMath.PlannedOthers(plannedTotal: 6, sameRoomAsOld: true, ownOldCost: 1);
		Assert.AreEqual(5, others);
		Assert.IsTrue(DefensePlacementMath.CanSaveWithinCapacity(others, 1, 6));

		// 다른 방에서 새로 저장하면 몫을 빼지 않는다
		Assert.AreEqual(6, DefensePlacementMath.PlannedOthers(6, false, 1));
	}

	[TestCase(1, 2, true)]
	[TestCase(2, 2, false)]  // 같은 방 안 재배치·저장만 한 유닛은 유입이 아니다
	[TestCase(-1, 3, true)]  // 방 밖(통로)에서 들어오는 유닛은 유입
	[TestCase(1, -1, false)]
	public void CountsAsIncoming_OnlyWhenDestinationRoomDiffers(int currentRoom, int destRoom, bool expected)
		=> Assert.AreEqual(expected, DefensePlacementMath.CountsAsIncoming(currentRoom, destRoom));

	[Test]
	public void RemainingCapacity_SubtractsLivingAndConfirmedIncoming()
		=> Assert.AreEqual(1, DefensePlacementMath.RemainingCapacity(6, 4, 1)); // 현 인원 4/6 (이동 중 1) → 남은 1

	// 사례 3: 방 잔여 인구수보다 배치 후보 합계가 큼 — 새 후보는 짧은 예상 이동 시간 순으로 가능한 인원만
	[Test]
	public void AcceptWithinRemaining_ShorterEtaFirst_SkipsThoseThatDoNotFit()
	{
		var candidates = new List<DefensePlacementMath.Candidate>
		{
			new DefensePlacementMath.Candidate(id: 0, cost: 2, etaSeconds: 9f),
			new DefensePlacementMath.Candidate(id: 1, cost: 1, etaSeconds: 3f),
			new DefensePlacementMath.Candidate(id: 2, cost: 1, etaSeconds: 5f),
		};
		var accepted = DefensePlacementMath.AcceptWithinRemaining(candidates, remaining: 2);
		Assert.AreEqual(new[] { 1, 2 }, accepted.ToArray(), "남은 정원 2 — ETA 3초·5초 후보가 먼저 들어가고 ETA 9초(인구 2)는 못 들어간다");
	}

	[Test]
	public void AcceptWithinRemaining_BigCandidateIsSkippedButLaterSmallOneStillFits()
	{
		var candidates = new List<DefensePlacementMath.Candidate>
		{
			new DefensePlacementMath.Candidate(0, cost: 3, etaSeconds: 1f),
			new DefensePlacementMath.Candidate(1, cost: 1, etaSeconds: 2f),
		};
		var accepted = DefensePlacementMath.AcceptWithinRemaining(candidates, remaining: 1);
		Assert.AreEqual(new[] { 1 }, accepted.ToArray(), "예상 시간이 짧아도 정원에 안 들어가면 건너뛰고 다음 후보를 본다(사례 15: 두 명 중 한 명만 들어갈 수 있음)");
	}

	[Test]
	public void AcceptWithinRemaining_NothingFitsWhenNoRoom()
		=> Assert.IsEmpty(DefensePlacementMath.AcceptWithinRemaining(new[] { new DefensePlacementMath.Candidate(0, 1, 1f) }, 0));

	// ── 몸체(1-2·1-9장) ────────────────────────────────────────────────

	[Test]
	public void BodyTiles_CoversWholeFootprintFromOrigin()
	{
		var tiles = new List<Vector2Int>();
		DefensePlacementMath.BodyTiles(new Vector2Int(2, 3), new Vector2Int(2, 3), tiles);
		Assert.AreEqual(6, tiles.Count);
		Assert.Contains(new Vector2Int(2, 3), tiles);
		Assert.Contains(new Vector2Int(3, 5), tiles);
	}

	[TestCase(0, 0, 3, 3, 2, 2, 1, 1, true)]   // 3x3 몸의 모서리 칸이 1x1과 겹친다
	[TestCase(0, 0, 3, 3, 3, 0, 1, 1, false)]  // 맞닿아 있을 뿐 겹치지 않는다
	[TestCase(5, 5, 1, 1, 5, 5, 1, 1, true)]
	public void BodiesOverlap_UsesWholeBodiesNotJustOrigins(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh, bool expected)
		=> Assert.AreEqual(expected, DefensePlacementMath.BodiesOverlap(new Vector2Int(ax, ay), new Vector2Int(aw, ah), new Vector2Int(bx, by), new Vector2Int(bw, bh)));

	// ── 범위 표시(1-10장) ──────────────────────────────────────────────

	[Test]
	public void ExpectedVision_FacingRight_IncludesFrontNotBehind()
	{
		var result = new HashSet<Vector2Int>();
		DefensePlacementMath.ExpectedVisionTiles(new Vector2Int(10, 10), 5, Dir.RIGHT, p => false, result);
		Assert.IsTrue(result.Contains(new Vector2Int(13, 10)), "정면은 보인다");
		Assert.IsFalse(result.Contains(new Vector2Int(7, 10)), "120도 콘 밖(뒤쪽)은 보이지 않는다");
	}

	[Test]
	public void ExpectedVision_WallBlocksTilesBehindIt()
	{
		var result = new HashSet<Vector2Int>();
		DefensePlacementMath.ExpectedVisionTiles(new Vector2Int(10, 10), 6, Dir.RIGHT, p => p.x == 12 && p.y == 10, result);
		Assert.IsTrue(result.Contains(new Vector2Int(11, 10)));
		Assert.IsFalse(result.Contains(new Vector2Int(14, 10)), "벽 뒤 같은 줄은 가려진다 — 실제 적을 새로 발견하는 것이 아니라 표시만 한다");
	}

	[Test]
	public void ExpectedAttack_RangeAndLineOfFire()
	{
		var result = new HashSet<Vector2Int>();
		DefensePlacementMath.ExpectedAttackTiles(new Vector2Int(5, 5), 2, p => p == new Vector2Int(6, 5), result);
		Assert.IsTrue(result.Contains(new Vector2Int(6, 5)), "막은 벽 자체는 끝점이라 포함");
		Assert.IsFalse(result.Contains(new Vector2Int(7, 5)), "벽 뒤는 공격선이 막힌다");
		Assert.IsTrue(result.Contains(new Vector2Int(5, 7)));
	}

	[Test]
	public void ClearLine_IgnoresEndpoints()
	{
		Assert.IsTrue(DefensePlacementMath.HasClearLine(new Vector2Int(0, 0), new Vector2Int(3, 0), p => p == new Vector2Int(0, 0) || p == new Vector2Int(3, 0)));
		Assert.IsFalse(DefensePlacementMath.HasClearLine(new Vector2Int(0, 0), new Vector2Int(3, 0), p => p == new Vector2Int(1, 0)));
	}

	[Test]
	public void AllDirs_AreEightDistinctUnitDirections()
	{
		var seen = new HashSet<Vector2Int>();
		foreach (var d in DefensePlacementMath.AllDirs) seen.Add(DefensePlacementMath.DirVector(d));
		Assert.AreEqual(8, seen.Count);
	}

	// ── 층별 임시 선택(1-9장) ──────────────────────────────────────────

	[Test]
	public void Selection_SelectGroup_OrdersByDistanceFromDragStart_AndFillsOrder()
	{
		var near = NewMonster(); var far = NewMonster();
		var s = new PlacementSelection();
		var centers = new Dictionary<Unit, Vector2> { [near] = new Vector2(1, 0), [far] = new Vector2(9, 0) };
		s.SelectGroup(new Unit[] { far, near }, Vector2.zero, u => centers[u], additive: false);
		Assert.AreEqual(2, s.Selected.Count);
		Assert.AreSame(near, s.Next);
	}

	[Test]
	public void Selection_OnPlaced_RemovesOnlyFromOrder_KeepsSelection()
	{
		var a = NewMonster(); var b = NewMonster();
		var s = new PlacementSelection();
		s.Selected.AddRange(new[] { a, b });
		s.Order.AddRange(new[] { a, b });
		Assert.IsTrue(s.OnPlaced(a));
		Assert.AreSame(b, s.Next);
		Assert.AreEqual(2, s.Selected.Count, "새 타일을 지정한 인원도 전체 선택에는 남는다 — 이어서 회전·미배치·이동 적용을 쓴다");
		Assert.IsTrue(s.OnPlaced(b));
		Assert.IsNull(s.Next, "마지막 유닛을 놓으면 순서 칸이 비고 빈 타일을 다시 눌러도 첫 유닛이 새 차례가 되지 않는다");
		Assert.AreEqual(2, s.Selected.Count);
	}

	[Test]
	public void Selection_Toggle_AddsToEndOfOrder_AndRemovesFromBoth()
	{
		var a = NewMonster(); var b = NewMonster();
		var s = new PlacementSelection();
		s.SelectOnly(a);
		s.Toggle(b);
		Assert.AreEqual(new Unit[] { a, b }, s.Order.ToArray());
		s.Toggle(a);
		Assert.AreEqual(new Unit[] { b }, s.Selected.ToArray());
		Assert.AreEqual(new Unit[] { b }, s.Order.ToArray());
	}

	[Test]
	public void Selection_Swap_ChangesOrderOnly()
	{
		var a = NewMonster(); var b = NewMonster(); var c = NewMonster();
		var s = new PlacementSelection();
		s.Selected.AddRange(new[] { a, b, c });
		s.Order.AddRange(new[] { a, b, c });
		Assert.IsTrue(s.Swap(0, 2));
		Assert.AreSame(c, s.Next);
		Assert.AreEqual(new Unit[] { a, b, c }, s.Selected.ToArray(), "순서 교환은 모습·번호만 바꾼다");
	}

	[Test]
	public void Selection_Prune_DropsDeadUnits()
	{
		var alive = NewMonster(); var dead = NewMonster(hp: 0f);
		var s = new PlacementSelection();
		s.Selected.AddRange(new[] { alive, dead });
		s.Order.AddRange(new[] { alive, dead });
		s.Prune();
		Assert.AreEqual(1, s.Selected.Count);
		Assert.AreEqual(1, s.Order.Count);
	}

	[Test]
	public void SelectionStore_RemembersPerFloor_AndClearsAll()
	{
		var a = NewMonster();
		var store = new PlacementSelectionStore();
		store.For(1).SelectOnly(a);
		Assert.IsFalse(store.For(2).HasSelection, "층 2의 선택은 층 1과 따로다");
		Assert.IsTrue(store.For(1).HasSelection, "층을 옮겼다 돌아오면 그 층의 남은 인원부터 이어간다");
		store.ClearAll();
		Assert.IsFalse(store.For(1).HasSelection, "모드를 완전히 나가면 모든 층의 임시 선택을 지운다");
	}

	// ── 사유 문구 ───────────────────────────────────────────────────────

	[Test]
	public void EveryPlacementBlock_HasKoreanText()
	{
		foreach (PlacementBlock b in System.Enum.GetValues(typeof(PlacementBlock)))
			Assert.IsFalse(string.IsNullOrEmpty(b.ToKorean()), b.ToString());
		Assert.AreEqual("코어 수복 후 배치 가능", PlacementBlock.CoreDestroyed.ToKorean());
	}

	[Test]
	public void ApplySummary_ListsEachFailureReasonOnce()
	{
		var result = new DefenseApplyResult();
		result.Accepted.Add(NewMonster());
		result.Failed.Add((NewMonster(), PlacementBlock.RoomFull));
		result.Failed.Add((NewMonster(), PlacementBlock.RoomFull));
		string text = result.Summary();
		StringAssert.Contains("이동 적용 1기", text);
		StringAssert.Contains("제외 2기", text);
		Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(text, "방 정원이 부족합니다").Count);
	}
}
#endif
