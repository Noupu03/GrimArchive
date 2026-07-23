#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// GoapPlanner(2026-07-22 도입 — GoapCore.cs가 예전에 하던 "목표에 맞는 액션 1개만 즉시 매칭"을
// 대신해 Preconditions/Effects를 실제로 체이닝하는 A*/균일비용 플래너)의 탐색 로직을 고정하는 테스트.
// VisionSystemTests.cs/PerceptionSystemTests.cs와 동일한 컨벤션. 실제 Unit/Goal 대신 최소한의 가짜
// GoapAction만 써서 순수 탐색 로직만 검증한다(IsValid가 unit을 쓰지 않으므로 어떤 Unit 인스턴스를
// 넘겨도 무방 — ScriptableObject.CreateInstance로 만든 빈 Human을 자리 채우기 용도로만 쓴다).
// ========================================================================

public class GoapPlannerTests
{
	private class FakeAction : GoapAction
	{
		public FakeAction(string name, float cost) { ActionName = name; Cost = cost; }
		public override int ActionCode => 0; // 테스트 전용 — 실제 등록된 액션이 아니라 라벨 숫자는 의미 없음
		public override bool IsValid(Unit unit) => true;
		public override void Execute(Unit unit) { }
	}

	private Human _dummyUnit;

	[SetUp]
	public void SetUp()
	{
		_dummyUnit = ScriptableObject.CreateInstance<Human>();
	}

	[TearDown]
	public void TearDown()
	{
		if (_dummyUnit != null) Object.DestroyImmediate(_dummyUnit);
	}

	[Test]
	public void Plan_ChainsTwoActionsWhenFirstEffectUnlocksSecondPrecondition()
	{
		var moveToDoor = new FakeAction("MoveToDoor", 1f);
		moveToDoor.AddEffect("atDoor", true);

		var openDoor = new FakeAction("OpenDoor", 1f);
		openDoor.AddPrecondition("atDoor", true);
		openDoor.AddEffect("doorOpen", true);

		var start = new GoapState { ["atDoor"] = false, ["doorOpen"] = false };
		var desired = new GoapState { ["doorOpen"] = true };

		List<GoapAction> plan = GoapPlanner.Plan(_dummyUnit, start, desired, new List<GoapAction> { moveToDoor, openDoor });

		Assert.IsNotNull(plan);
		Assert.AreEqual(2, plan.Count);
		Assert.AreSame(moveToDoor, plan[0]);
		Assert.AreSame(openDoor, plan[1]);
	}

	[Test]
	public void Plan_ReturnsNullWhenGoalUnreachable()
	{
		var openDoor = new FakeAction("OpenDoor", 1f);
		openDoor.AddPrecondition("atDoor", true); // atDoor를 만들어줄 액션이 없음
		openDoor.AddEffect("doorOpen", true);

		var start = new GoapState { ["atDoor"] = false, ["doorOpen"] = false };
		var desired = new GoapState { ["doorOpen"] = true };

		List<GoapAction> plan = GoapPlanner.Plan(_dummyUnit, start, desired, new List<GoapAction> { openDoor });

		Assert.IsNull(plan);
	}

	// TrapResponse의 Bypass(2)/Pass(3)/Destroy(4)처럼, 여러 액션이 같은 Effect를 선언해 한 스텝만에
	// 목표를 만족시킬 수 있을 때도 그중 가장 싼 것을 골라야 한다(등록 순서가 비용 순서와 달라도).
	[Test]
	public void Plan_PicksCheapestPathWhenMultipleSingleStepOptionsExist()
	{
		var expensiveDetour = new FakeAction("ExpensiveDetour", 5f);
		expensiveDetour.AddEffect("doorOpen", true);

		var cheapDirect = new FakeAction("CheapDirect", 1f);
		cheapDirect.AddEffect("doorOpen", true);

		var start = new GoapState { ["doorOpen"] = false };
		var desired = new GoapState { ["doorOpen"] = true };

		// 일부러 비싼 액션을 목록 앞에 둔다 — 등록 순서에 기대지 않고 비용으로만 골라야 한다.
		List<GoapAction> plan = GoapPlanner.Plan(_dummyUnit, start, desired, new List<GoapAction> { expensiveDetour, cheapDirect });

		Assert.AreEqual(1, plan.Count);
		Assert.AreSame(cheapDirect, plan[0]);
	}

	[Test]
	public void Plan_ReturnsEmptyWhenAlreadySatisfied()
	{
		var start = new GoapState { ["doorOpen"] = true };
		var desired = new GoapState { ["doorOpen"] = true };

		List<GoapAction> plan = GoapPlanner.Plan(_dummyUnit, start, desired, new List<GoapAction>());

		Assert.IsNotNull(plan);
		Assert.AreEqual(0, plan.Count);
	}
}
#endif
