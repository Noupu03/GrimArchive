#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

// 건설 설계명세서 v0.2(2026-11-12) 1단계(규칙 정정) 숫자 예시 고정 테스트 - 부록 A 3-3-표1, 부록 B 사례 7·11·13·16.
[TestFixture]
public class ConstructionRulesTests
{
	private static Dictionary<ResourceType, ResourceAccount> CreateAccounts(double life)
	{
		return new Dictionary<ResourceType, ResourceAccount>
		{
			[ResourceType.LifeEnergy] = new ResourceAccount(ResourceType.LifeEnergy, life),
			[ResourceType.SoulShard] = new ResourceAccount(ResourceType.SoulShard, 0.0),
		};
	}

	// ── 2-4 손상 효율 ──

	[Test]
	public void DamageCoefficient_Hp60Percent_IsPoint8()
	{
		Assert.AreEqual(0.8, ConstructionRules.DamageCoefficient(60f, 100f), 1e-9);
		Assert.AreEqual(1.0, ConstructionRules.DamageCoefficient(100f, 100f), 1e-9);
		Assert.AreEqual(0.5, ConstructionRules.DamageCoefficient(0f, 100f), 1e-9);
		Assert.AreEqual(1.0, ConstructionRules.DamageCoefficient(10f, 0f), 1e-9, "최대 HP가 없으면 감소시키지 않는다");
	}

	[Test]
	public void DamageCoefficient_EveryTwoPercentLossCostsOnePercent()
	{
		Assert.AreEqual(0.99, ConstructionRules.DamageCoefficient(98f, 100f), 1e-9);
		Assert.AreEqual(0.98, ConstructionRules.DamageCoefficient(96f, 100f), 1e-9);
	}

	[Test]
	public void LifeEnergy_Hp60Percent_SpeedAndPayoutEachEightyPercent_HourlyOutput64Percent()
	{
		var workers = new List<WorkerContributionInput> { new WorkerContributionInput(1, 1.0f, 0.0f) };
		var healthy = new LifeEnergyCycleTracker(10.0, 50.0);
		var damaged = new LifeEnergyCycleTracker(10.0, 50.0);
		double healthyTotal = 0.0, damagedTotal = 0.0;
		for (int i = 0; i < 5000; i++)
		{
			healthyTotal += healthy.Advance(0.1, workers, true, 0.0, 1.0);
			damagedTotal += damaged.Advance(0.1, workers, true, 0.0, ConstructionRules.DamageCoefficient(60f, 100f));
		}
		Assert.AreEqual(2500.0, healthyTotal, 1e-3);
		Assert.AreEqual(0.64, damagedTotal / healthyTotal, 1e-3, "속도 80% x 회당 산출 80% = 시간당 64%");
	}

	[Test]
	public void LifeEnergy_EfficiencyChangeMidCycle_PaysBySegmentContribution()
	{
		var workers = new List<WorkerContributionInput> { new WorkerContributionInput(1, 1.0f, 0.0f) };
		var t = new LifeEnergyCycleTracker(10.0, 50.0);
		Assert.AreEqual(0.0, t.Advance(5.0, workers, true, 0.0, 1.0), 1e-9);
		// 남은 절반 게이지는 e=0.5라 10초가 걸리고, 이 구간 기여는 절반이라 한 주기 산출 = 50 x (0.5 + 0.5 x 0.5) = 37.5
		Assert.AreEqual(37.5, t.Advance(10.0, workers, true, 0.0, 0.5), 1e-6);
	}

	[Test]
	public void LifeEnergy_SnapshotKeepsEfficiencyDelta()
	{
		var workers = new List<WorkerContributionInput> { new WorkerContributionInput(1, 1.0f, 0.0f) };
		var t = new LifeEnergyCycleTracker(10.0, 50.0);
		t.Advance(4.0, workers, true, 0.0, 0.5);
		var restored = new LifeEnergyCycleTracker(10.0, 50.0);
		restored.RestoreSnapshot(t.CaptureSnapshot());
		Assert.AreEqual(t.WeightedEfficiencyDelta, restored.WeightedEfficiencyDelta, 1e-12);
		Assert.AreEqual(t.Advance(100.0, workers, true, 0.0, 0.5), restored.Advance(100.0, workers, true, 0.0, 0.5), 1e-9);
	}

	[Test]
	public void CombatFacility_Hp60Percent_Damage100Becomes80()
	{
		Assert.AreEqual(80.0, ConstructionRules.ApplyToCombatDamage(100.0, 60f, 100f), 1e-9);
	}

	[Test]
	public void DisplayHp_RoundsAndShowsOneWhileAlive()
	{
		Assert.AreEqual(0, ConstructionRules.DisplayHp(0f));
		Assert.AreEqual(1, ConstructionRules.DisplayHp(0.2f), "생존 HP가 1 미만이어도 1을 표시한다");
		Assert.AreEqual(13, ConstructionRules.DisplayHp(12.5f));
		Assert.AreEqual(12, ConstructionRules.DisplayHp(12.4f));
	}

	// ── 2-2 건설 중 HP / 2-7 재설치 HP / 2-9 문 확장 HP ──

	[Test]
	public void NewConstruction_CompletesAtFullMaxHp_EvenAfterDamage()
	{
		var accounts = CreateAccounts(1000);
		var costs = new[] { (ResourceType.LifeEnergy, 100.0) };
		float hp = 100f, max = 100f; // 건설 단계 최대 HP 100
		ConstructionOrUpgradeSession.TryCreateOrder(false, costs, 10.0, 1000f, accounts, false, true, out var s, out _);
		s.Tick(5.0, new[] { 1.0f }, true, true, costs, accounts, ref hp, ref max);
		hp = 30f; // 건설 중 피격 후 생존 - 감소한 건설 HP를 유지한다
		Assert.AreEqual(0.5, s.Progress01, 1e-9, "피해만으로 진행도가 지워지지 않는다");
		Assert.IsTrue(s.Tick(5.0, new[] { 1.0f }, true, true, costs, accounts, ref hp, ref max));
		Assert.AreEqual(1000f, hp, 1e-4f, "신규 건설은 완성 시설의 최대 HP로 전부 회복");
		Assert.AreEqual(1000f, max, 1e-4f);
	}

	[Test]
	public void NewConstruction_HpZeroAtCompletionTick_DestroyedWithoutRefund()
	{
		var accounts = CreateAccounts(1000);
		var costs = new[] { (ResourceType.LifeEnergy, 100.0) };
		float hp = 30f, max = 100f;
		ConstructionOrUpgradeSession.TryCreateOrder(false, costs, 10.0, 1000f, accounts, false, true, out var s, out _);
		s.Tick(9.0, new[] { 1.0f }, true, true, costs, accounts, ref hp, ref max);
		Assert.IsFalse(s.Tick(1.0, new[] { 1.0f }, true, true, costs, accounts, ref hp, ref max, sameTickDamage: 30f));
		Assert.IsTrue(s.IsEnded);
		Assert.IsFalse(s.IsCompleted, "파괴된 건설물은 완공하지 않는다");
		Assert.AreEqual(900.0, accounts[ResourceType.LifeEnergy].CurrentBalance, 1e-6, "이미 지불한 건설비는 환급하지 않는다");
	}

	[Test]
	public void Upgrade_CompletesWithHpRatioCarriedToNewMax()
	{
		var accounts = CreateAccounts(1000);
		var costs = new[] { (ResourceType.LifeEnergy, 100.0) };
		float hp = 100f, max = 200f;
		ConstructionOrUpgradeSession.TryCreateOrder(true, costs, 10.0, 300f, accounts, false, true, out var s, out _);
		Assert.IsTrue(s.Tick(10.0, new[] { 1.0f }, true, true, costs, accounts, ref hp, ref max));
		Assert.AreEqual(300f, max, 1e-4f);
		Assert.AreEqual(150f, hp, 1e-4f, "강화 완료는 기존 HP 비율(50%)을 새 최대 HP에 적용");
	}

	[Test]
	public void ConstructionStageMaxHp_IsTenthOfCompletedPlaceholder()
	{
		Assert.AreEqual(100f, ConstructionRules.ConstructionStageMaxHp(1000f), 1e-4f);
		Assert.AreEqual(1f, ConstructionRules.ConstructionStageMaxHp(5f), 1e-4f, "최소 1");
	}

	[Test]
	public void ReinstallHp_Conversion_600of1000_Start60_Damage20_Complete400()
	{
		float start = ConstructionRules.ReinstallStartHp(600f, 1000f, 100f);
		Assert.AreEqual(60f, start, 1e-4f);
		float afterDamage = start - 20f;
		Assert.AreEqual(40f, afterDamage, 1e-4f);
		Assert.AreEqual(400f, ConstructionRules.ReinstallCompleteHp(afterDamage, 100f, 1000f), 1e-3f);
	}

	[Test]
	public void DoorExpansion_Hp120of200_MaxX1_5_Becomes180of300()
	{
		Assert.AreEqual(180f, ConstructionRules.HpAfterMaxChange(120f, 200f, 300f), 1e-4f);
	}

	// ── 2-3 실제 수리와 가동 / 1-5 전투 뒤 재개 / 1-3 선점 해제 ──

	[Test]
	public void FieldWork_StopsFacilityOnlyWhileActuallyWorked()
	{
		Assert.IsFalse(ConstructionRules.IsFieldWorkStoppingFacility(false, 2, false), "등록만 됐거나 접근 중");
		Assert.IsFalse(ConstructionRules.IsFieldWorkStoppingFacility(true, 0, false), "작업자 전원 이탈");
		Assert.IsFalse(ConstructionRules.IsFieldWorkStoppingFacility(true, 2, true), "차액 승인 대기");
		Assert.IsTrue(ConstructionRules.IsFieldWorkStoppingFacility(true, 1, false), "일부만 남아도 실제 수리 중이면 가동 중단");
	}

	[Test]
	public void ResumeAssignedWork_OnlyAfterCombatAndAlertAndNoOtherTask()
	{
		Assert.IsTrue(ConstructionRules.ShouldResumeAssignedWork(true, false, false, false));
		Assert.IsFalse(ConstructionRules.ShouldResumeAssignedWork(true, false, true, false), "전투/경계 중");
		Assert.IsFalse(ConstructionRules.ShouldResumeAssignedWork(true, true, false, false), "이미 작업 자리");
		Assert.IsFalse(ConstructionRules.ShouldResumeAssignedWork(true, false, false, true), "다른 이동/운반 진행 중");
		Assert.IsFalse(ConstructionRules.ShouldResumeAssignedWork(false, false, false, false), "배정 없음");
	}

	[Test]
	public void ReleasedAfterStart_ReservedEightyPaidSeventy_ReleasesAllReservedAndFreesTen()
	{
		Assert.AreEqual(10, ConstructionRules.ReleasedAfterStart(80, 70));
		Assert.AreEqual(0, ConstructionRules.ReleasedAfterStart(70, 80));
	}
}
#endif
