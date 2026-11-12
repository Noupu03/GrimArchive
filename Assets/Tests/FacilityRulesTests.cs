#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

// 건설 설계명세서 v0.2 2·3단계 순수 규칙 테스트 - 정면 출현 구역(2-10), 회전(2-6), 문 확장(2-9, 사례 16), 내부 작업(1-6), 전투 시설 정지(2-14, 사례 28).
[TestFixture]
public class FacilityRulesTests
{
	// ── 회전·정면 ──

	[Test]
	public void Rotation_NormalizesAndSwapsFootprintOnOddTurns()
	{
		Assert.AreEqual(3, ConstructionRules.NormalizeRotation(-1));
		Assert.AreEqual(1, ConstructionRules.NormalizeRotation(5));
		Assert.AreEqual(new Vector2Int(2, 3), ConstructionRules.RotatedFootprint(new Vector2Int(2, 3), 2));
		Assert.AreEqual(new Vector2Int(3, 2), ConstructionRules.RotatedFootprint(new Vector2Int(2, 3), 1));
	}

	[Test]
	public void FrontDirection_FollowsClockwiseRotation()
	{
		Assert.AreEqual(new Vector2Int(0, -1), ConstructionRules.FrontDirection(0));
		Assert.AreEqual(new Vector2Int(-1, 0), ConstructionRules.FrontDirection(1));
		Assert.AreEqual(new Vector2Int(0, 1), ConstructionRules.FrontDirection(2));
		Assert.AreEqual(new Vector2Int(1, 0), ConstructionRules.FrontDirection(3));
	}

	[Test]
	public void FrontZoneRect_StripAlongFrontSideWithDepth()
	{
		var origin = new Vector2Int(10, 10);
		var fp = new Vector2Int(3, 3);
		Assert.AreEqual(new RectInt(10, 8, 3, 2), ConstructionRules.FrontZoneRect(origin, fp, 0, 2));  // 아래
		Assert.AreEqual(new RectInt(8, 10, 2, 3), ConstructionRules.FrontZoneRect(origin, fp, 1, 2));  // 왼쪽
		Assert.AreEqual(new RectInt(10, 13, 3, 2), ConstructionRules.FrontZoneRect(origin, fp, 2, 2)); // 위
		Assert.AreEqual(new RectInt(13, 10, 2, 3), ConstructionRules.FrontZoneRect(origin, fp, 3, 2)); // 오른쪽
	}

	[Test]
	public void FrontSpawn_PicksClearSpotNearestZoneCenter_AndLargeUnitsCannotFit()
	{
		var zone = new RectInt(10, 8, 3, 2);
		Assert.IsTrue(ConstructionRules.TryFindFrontSpawnOrigin(zone, new Vector2Int(1, 1), (o, f) => true, out Vector2Int center));
		Assert.AreEqual(new Vector2Int(11, 8), center, "중앙에 가까운 칸 우선");

		// 중앙이 막혀 있으면 다음 자리
		Assert.IsTrue(ConstructionRules.TryFindFrontSpawnOrigin(zone, new Vector2Int(1, 1), (o, f) => o.x != 11, out Vector2Int side));
		Assert.AreNotEqual(11, side.x);

		// 구역보다 큰 유닛은 나오지 못한다(소형 시설은 강화해도 대형 유닛 생산 불가)
		Assert.IsFalse(ConstructionRules.TryFindFrontSpawnOrigin(zone, new Vector2Int(3, 3), (o, f) => true, out _));
		// 전부 막히면 출현 대기
		Assert.IsFalse(ConstructionRules.TryFindFrontSpawnOrigin(zone, new Vector2Int(1, 1), (o, f) => false, out _));
	}

	// ── 문 확장(사례 16) ──

	[Test]
	public void DoorExpansion_OneWorkerFinishesBothEnds_WidthAndHpChangeOnlyWhenBothDone()
	{
		var s = new DoorExpansionSession(2, 4, 1.5f, cost: 100, totalWorkSeconds: 10.0);
		int resource = 500;

		s.Tick(0, 5.0, ref resource); // 첫 끝 완료
		Assert.IsTrue(s.IsEndDone(0));
		Assert.IsFalse(s.IsCompleted, "한쪽 완료만으로 폭이 바뀌지 않는다");
		Assert.AreEqual(400, resource, "비용은 첫 실제 작업 때 한 번만 지불");
		Assert.IsTrue(s.OpensDoor, "실제 작업이 시작되면 문이 열린다");

		s.Tick(0, 5.0, ref resource); // 이미 끝난 끝에는 더 쌓이지 않는다
		Assert.IsFalse(s.IsCompleted);

		s.Tick(1, 5.0, ref resource); // 반대쪽으로 가서 마친다
		Assert.IsTrue(s.IsCompleted);
		Assert.AreEqual(400, resource, "중복 차감 없음");

		var (newMax, newHp) = s.ComputeCompletedHp(120f, 200f);
		Assert.AreEqual(300f, newMax, 1e-3f);
		Assert.AreEqual(180f, newHp, 1e-3f, "남은 HP 비율 60% 유지");
	}

	[Test]
	public void DoorExpansion_CancelRefundsPaidCost_DestroyedDoesNot()
	{
		int resource = 500;
		var s = new DoorExpansionSession(2, 4, 1.5f, 100, 10.0);
		s.Tick(1, 2.0, ref resource);
		Assert.AreEqual(100, s.Cancel(), "직접 취소는 확장 비용을 돌려준다");
		Assert.IsTrue(s.IsEnded);

		var d = new DoorExpansionSession(2, 4, 1.5f, 100, 10.0);
		d.Tick(0, 2.0, ref resource);
		d.OnDoorDestroyed();
		Assert.IsTrue(d.IsEnded);
		Assert.IsFalse(d.IsCompleted, "공사 중 파괴되면 미완료 폭 확장은 없다");
		Assert.AreEqual(0, d.Cancel(), "파괴 뒤엔 환급 없음");
	}

	[Test]
	public void DoorExpansion_InsufficientResource_DoesNotStart()
	{
		int resource = 50;
		var s = new DoorExpansionSession(2, 4, 1.5f, 100, 10.0);
		s.Tick(0, 5.0, ref resource);
		Assert.IsFalse(s.HasStartedWork);
		Assert.AreEqual(0.0, s.EndWork[0], 1e-9);
	}

	// ── 내부 작업 ──

	[Test]
	public void InternalWork_CapacityCoreAndRepairBlockEntry()
	{
		var body = new Vector2Int(1, 1);
		Assert.IsNull(InternalWorkRules.GetEnterBlockReason(FacilityKind.Research, 1, body, true, false));
		Assert.IsNotNull(InternalWorkRules.GetEnterBlockReason(FacilityKind.Research, 2, body, true, false), "정원 가득");
		Assert.IsNotNull(InternalWorkRules.GetEnterBlockReason(FacilityKind.LifeEnergy, 0, body, false, false), "코어 파괴 중 새 내부 작업자 불가");
		Assert.IsNotNull(InternalWorkRules.GetEnterBlockReason(FacilityKind.LifeEnergy, 0, body, true, true), "실제 수리·강화 중 입장 불가");
		Assert.IsNotNull(InternalWorkRules.GetEnterBlockReason(FacilityKind.LifeEnergy, 0, new Vector2Int(3, 3), true, false), "몸체 크기 초과");
		Assert.IsNotNull(InternalWorkRules.GetEnterBlockReason(FacilityKind.UnitProduction, 0, body, true, false), "유닛 생산은 내부 작업자 불필요");
	}

	[Test]
	public void InternalWork_CombatResponseSettingAndExitCauses()
	{
		Assert.IsTrue(InternalWorkRules.ShouldExitForCombat(InternalCombatSetting.Respond, true, false));
		Assert.IsTrue(InternalWorkRules.ShouldExitForCombat(InternalCombatSetting.Respond, false, true));
		Assert.IsFalse(InternalWorkRules.ShouldExitForCombat(InternalCombatSetting.Ignore, true, true));
		Assert.IsTrue(InternalWorkRules.KeepsAssignmentAfterExit(InternalWorkRules.ExitCause.Dismantle), "분해에는 배정을 유지");
		Assert.IsFalse(InternalWorkRules.KeepsAssignmentAfterExit(InternalWorkRules.ExitCause.Demolish), "철거에는 배정을 해제");
	}

	[Test]
	public void InternalWork_YieldTable()
	{
		Assert.AreEqual(InternalWorkRules.YieldAction.StepAside, InternalWorkRules.ResolveYield(InternalWorkRules.BlockerState.WaitingAlly));
		Assert.AreEqual(InternalWorkRules.YieldAction.StepAsideAlsoNeighbors, InternalWorkRules.ResolveYield(InternalWorkRules.BlockerState.ExitWaitingAlly));
		Assert.AreEqual(InternalWorkRules.YieldAction.WorkerStepsBackThenResume, InternalWorkRules.ResolveYield(InternalWorkRules.BlockerState.WorkerOnlyEscape));
		Assert.AreEqual(InternalWorkRules.YieldAction.WaitWithoutForcing, InternalWorkRules.ResolveYield(InternalWorkRules.BlockerState.BusyOrFighting));
		Assert.AreEqual(InternalWorkRules.YieldAction.KeepExitWait, InternalWorkRules.ResolveYield(InternalWorkRules.BlockerState.NoFreeSpace));
		Assert.AreEqual(InternalWorkRules.YieldAction.StepAside, InternalWorkRules.ResolveYield(InternalWorkRules.BlockerState.NaturalRecoveryOnly));
	}

	// ── 전투 시설 정지(사례 28) ──

	[Test]
	public void CombatFacilityStop_PreparingCancelsProjectilesContinue_CooldownFrozen()
	{
		Assert.IsTrue(CombatFacilityStopRules.CancelsOnStop(CombatAttackPhase.Preparing));
		Assert.IsTrue(CombatFacilityStopRules.CancelsOnStop(CombatAttackPhase.FacilityDependentSustained));
		Assert.IsTrue(CombatFacilityStopRules.ContinuesAfterStop(CombatAttackPhase.ProjectileInFlight));
		Assert.IsTrue(CombatFacilityStopRules.ContinuesAfterStop(CombatAttackPhase.AppliedDot));
		Assert.IsFalse(CombatFacilityStopRules.CancelsOnStop(CombatAttackPhase.ProjectileInFlight));

		Assert.AreEqual(3.0f, CombatFacilityStopRules.AdvanceCooldown(3.0f, 1.0f, stopped: true), 1e-6f, "정지 동안 재사용 시간은 줄지 않고 보존");
		Assert.AreEqual(2.0f, CombatFacilityStopRules.AdvanceCooldown(3.0f, 1.0f, stopped: false), 1e-6f);
		Assert.AreEqual(0.0f, CombatFacilityStopRules.AdvanceCooldown(0.5f, 1.0f, stopped: false), 1e-6f);
	}
}
#endif
