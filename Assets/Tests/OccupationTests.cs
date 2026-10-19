#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

// ========================================================================
// 인류 점령(거점·건설·진출) 순수 판정 테스트(NUnit, #if UNITY_INCLUDE_TESTS) — 코어 점령 설계명세서 v0.2 12~14장과 20-1장 검증 사례 중 순수 판정에 해당하는 것, 회의록 10.19(상한 2, 출구에 가까운 쪽이 후방)를 고정한다.
// 세션이 필요한 건설 진행·문 소유 전환·웨이브 인수인계는 플레이 확인 대상이다.
// ========================================================================

public class OccupationTests
{
	// ── 건설 작업량 (자리표시자 150 인-초: 5명이면 30초) ──

	[Test]
	public void Work_FiveWorkersFinishInThirtySeconds()
	{
		float done = 0f;
		for (int i = 0; i < 300; i++) done += OccupationMath.WorkDelta(5, 0.1f); // 0.1초 × 300틱 = 30초
		Assert.IsTrue(OccupationMath.IsWorkComplete(done, OccupationMath.BasicWorkUnits), $"done={done}");
	}

	[Test]
	public void Work_NoWorkersNoProgress()
	{
		Assert.AreEqual(0f, OccupationMath.WorkDelta(0, 1f));
		Assert.AreEqual(0f, OccupationMath.WorkDelta(3, 0f));
	}

	[TestCase(0f, 150f, 0f)]
	[TestCase(75f, 150f, 0.5f)]
	[TestCase(150f, 150f, 1f)]
	[TestCase(400f, 150f, 1f)]
	public void Progress01_IsClamped(float done, float total, float expected)
	{
		Assert.AreEqual(expected, OccupationMath.Progress01(done, total), 0.0001f);
	}

	// ── 12장: 시설 상태 판정 ──

	[Test]
	public void BaseStage_UsabilityAndActivityComplete()
	{
		Assert.IsFalse(OccupationMath.IsBaseUsable(BaseStage.None), "기초 시설 미완공은 점령 작업 진행 중 — 후퇴 목적지 아님");
		Assert.IsTrue(OccupationMath.IsBaseUsable(BaseStage.Basic), "기초 거점도 후퇴·휴식 가능");
		Assert.IsTrue(OccupationMath.IsBaseUsable(BaseStage.Defense));
		Assert.IsFalse(OccupationMath.IsRoomActivityComplete(BaseStage.None));
		Assert.IsTrue(OccupationMath.IsRoomActivityComplete(BaseStage.Basic), "통과 방은 기초 설치만 하고 전진한다");
	}

	[Test]
	public void DefenseBuild_OnlyForDesignatedBasicBase()
	{
		Assert.IsTrue(OccupationMath.NeedsDefenseBuild(BaseStage.Basic, true), "방어 거점으로 정한 기초 거점은 예정 방어 시설을 지어야 한다(13장)");
		Assert.IsFalse(OccupationMath.NeedsDefenseBuild(BaseStage.Basic, false), "방어 거점으로 정하지 않은 방은 기초 거점으로 끝");
		Assert.IsFalse(OccupationMath.NeedsDefenseBuild(BaseStage.Defense, true), "이미 완공");
		Assert.IsFalse(OccupationMath.NeedsDefenseBuild(BaseStage.None, true));
	}

	// ── 14장·회의록: 주둔 거점 상한 2 ──

	[TestCase(0, true)]
	[TestCase(1, true)]
	[TestCase(2, false)]
	[TestCase(3, false)]
	public void GarrisonCap_IsTwo(int counted, bool canAdd)
	{
		Assert.AreEqual(2, OccupationMath.GarrisonBaseCap);
		Assert.AreEqual(canAdd, OccupationMath.CanAddGarrisonBase(counted));
	}

	// ── 14장 표·20-1장: 정원 100% + 상한 여유 → 다음 방어 거점 지정 ──

	[Test]
	public void Designate_FullGarrisonWithRoomToSpare()
	{
		// 정원 10인 기존 거점 인구수 10, 거점 1개 → 유효한 가까운 기초 거점으로 확장 가능.
		Assert.IsTrue(OccupationMath.ShouldDesignateDefenseBase(garrison: 10, capacity: 10, countedBases: 1, anyDesignated: false, candidateAlreadyCounted: false));
	}

	[Test]
	public void Designate_NotFullGarrisonOnlyFillsExistingBase()
	{
		// 정원 10인 기존 거점 인구수 9 → 주둔 거점 수는 늘리지 않고 기존 거점의 인구수 1을 충원한다.
		Assert.IsFalse(OccupationMath.ShouldDesignateDefenseBase(garrison: 9, capacity: 10, countedBases: 1, anyDesignated: false, candidateAlreadyCounted: false));
	}

	[Test]
	public void Designate_CapReachedBlocksNewBaseButNotTheSameOne()
	{
		// 거점 2개 모두 만석 → 추가 거점 불가. 이미 세어지는 거점을 방어 거점으로 정하는 것은 새 거점이 아니라 상한을 쓰지 않는다.
		Assert.IsFalse(OccupationMath.ShouldDesignateDefenseBase(10, 10, countedBases: 2, anyDesignated: false, candidateAlreadyCounted: false));
		Assert.IsTrue(OccupationMath.ShouldDesignateDefenseBase(10, 10, countedBases: 2, anyDesignated: false, candidateAlreadyCounted: true));
	}

	[Test]
	public void Designate_OnlyOneAtATimeAndNeedsCapacity()
	{
		Assert.IsFalse(OccupationMath.ShouldDesignateDefenseBase(10, 10, 1, anyDesignated: true, candidateAlreadyCounted: true));
		Assert.IsFalse(OccupationMath.ShouldDesignateDefenseBase(10, 0, 1, anyDesignated: false, candidateAlreadyCounted: true), "정원이 정해지지 않았으면 지정하지 않는다");
	}

	// ── 12-1장: 진출 방 선택 ──

	private static AdvanceCandidate Room(int id, bool human = false, bool known = false, float danger = 0f, int hops = 0)
		=> new AdvanceCandidate { RoomId = id, IsHumanBase = human, DangerKnown = known, Danger = danger, HopsFromExit = hops };

	[Test]
	public void PickAdvance_SkipsHumanBases()
	{
		var list = new List<AdvanceCandidate> { Room(1, human: true, known: true, danger: 0f), Room(2, known: true, danger: 0.3f) };
		Assert.AreEqual(1, OccupationMath.PickAdvance(list, 0.5f, allowUnknown: false), "이미 인류 거점인 방은 진출이 아니다");
	}

	[Test]
	public void PickAdvance_KnownDangerAboveLimitIsExcluded()
	{
		var list = new List<AdvanceCandidate> { Room(1, known: true, danger: 0.9f) };
		Assert.AreEqual(-1, OccupationMath.PickAdvance(list, 0.5f, allowUnknown: true));
	}

	[Test]
	public void PickAdvance_UnknownOnlyWhenAllowed()
	{
		var list = new List<AdvanceCandidate> { Room(1), Room(2) };
		Assert.AreEqual(-1, OccupationMath.PickAdvance(list, 0.5f, allowUnknown: false), "정보가 없으면 진출 대상 없음 — 회복·대기(13장 '대상 없음')");
		Assert.AreEqual(0, OccupationMath.PickAdvance(list, 0.5f, allowUnknown: true));
	}

	[Test]
	public void PickAdvance_KnownSafeBeatsUnknownThenLowerDangerThenForward()
	{
		var list = new List<AdvanceCandidate>
		{
			Room(1, known: false, hops: 9),
			Room(2, known: true, danger: 0.4f, hops: 1),
			Room(3, known: true, danger: 0.1f, hops: 1),
			Room(4, known: true, danger: 0.1f, hops: 5),
		};
		Assert.AreEqual(3, OccupationMath.PickAdvance(list, 0.5f, allowUnknown: true), "알려진 안전한 방 중 위험도가 같으면 전방(출구에서 먼 쪽)");
	}

	[Test]
	public void PickAdvance_EmptyHasNoChoice()
	{
		Assert.AreEqual(-1, OccupationMath.PickAdvance(new List<AdvanceCandidate>(), 0.5f, true));
	}

	// ── 회의록 10.19: 출구에 더 가까운 거점이 후방, 더 먼 거점이 전방 ──

	[Test]
	public void RearFront_ClosestToExitIsRear()
	{
		OccupationMath.ClassifyRearFront(new List<int> { 2, 5, 3 }, out int rear, out int front);
		Assert.AreEqual(0, rear);
		Assert.AreEqual(1, front);
	}

	[Test]
	public void RearFront_SingleBaseIsBoth()
	{
		OccupationMath.ClassifyRearFront(new List<int> { 4 }, out int rear, out int front);
		Assert.AreEqual(0, rear);
		Assert.AreEqual(0, front);
	}

	[Test]
	public void Hops_ChainFromExitAndUnreachableRoomsAreOmitted()
	{
		// 입구 → A(1) → B(2) → C(3) → D(4), 별개 섬 9-10 (명세서 14-4장 예시 구조).
		var edges = new List<(int a, int b)> { (1, 2), (2, 3), (3, 4), (9, 10) };
		var hops = OccupationMath.HopsFrom(1, edges);
		Assert.AreEqual(0, hops[1]);
		Assert.AreEqual(1, hops[2]);
		Assert.AreEqual(2, hops[3]);
		Assert.AreEqual(3, hops[4]);
		Assert.IsFalse(hops.ContainsKey(9));
	}

	[Test]
	public void Hops_UsesShortestRouteThroughLoops()
	{
		var edges = new List<(int a, int b)> { (1, 2), (2, 3), (3, 4), (1, 4) };
		Assert.AreEqual(1, OccupationMath.HopsFrom(1, edges)[4]);
	}
}
#endif
