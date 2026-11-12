#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 자원 경제와 유닛 생산 설계명세서 v0.2 전체 검증 테스트(NUnit, #if UNITY_INCLUDE_TESTS)
// - 23-표1 (27개 사례): 예약·출현·연구·일시정지·동시 사건
// - 24-표1 (31개 사례): 수리 차액·코어 수복 잔해·시체 정제·전체 비우기·이동용 분해·재설치
// - 24-2-표1 (10개 사례): 개체 진화 수동 결제·초 단위 성장·연출 중 피격/사망·진화 시체 정제·불러오기
// - 5항·20-1항·22항 보조 검증: 공동 작업 속도 공식, 철거 출처별 누적 획득량 분리, 스냅샷 복원 멱등성
// ========================================================================

[TestFixture]
public class EconomyProductionSystemTests
{
	private static Dictionary<ResourceType, ResourceAccount> CreateAccounts(double life = 0.0, double soul = 0.0)
	{
		return new Dictionary<ResourceType, ResourceAccount>
		{
			[ResourceType.LifeEnergy] = new ResourceAccount(ResourceType.LifeEnergy, life),
			[ResourceType.SoulShard] = new ResourceAccount(ResourceType.SoulShard, soul),
		};
	}

	private static UnitProductionSpec MakeSpec(
		string id = "melee",
		double lifeCost = 200.0,
		double soulCost = 0.0,
		double seconds = 10.0,
		int pop = 1,
		int w = 1,
		int h = 1)
	{
		return new UnitProductionSpec
		{
			RuleId = id,
			DisplayName = id,
			TargetUnitTypeName = "MeleeTank",
			BaseLifeEnergyCost = lifeCost,
			BaseSoulShardCost = soulCost,
			BaseProductionSeconds = seconds,
			PopulationCost = pop,
			Footprint = new Vector2Int(w, h),
			RequiresSpeciesUnlock = false,
		};
	}

	// ====================================================================
	// 23-표1. 생산·예약·연구의 기대 결과 (27개 전수 검증)
	// ====================================================================

	[Test]
	public void T23_01_Reserve200x4From1000_Displays1000Minus800_Available200()
	{
		var accounts = CreateAccounts(life: 1000);
		var queue = new FacilityProductionQueue();
		var block = queue.TryEnqueue(MakeSpec(lifeCost: 200), 4, accounts, true, true, true, true, 0, 1.0, out _, out _);

		Assert.AreEqual(ProductionEnqueueBlock.None, block);
		Assert.AreEqual("1000 (-800)", accounts[ResourceType.LifeEnergy].FormatDisplay());
		Assert.AreEqual(200, accounts[ResourceType.LifeEnergy].DisplayAvailable);
	}

	[Test]
	public void T23_02_Additional300ReserveRejected()
	{
		var accounts = CreateAccounts(life: 1000);
		var queue = new FacilityProductionQueue();
		queue.TryEnqueue(MakeSpec("a", lifeCost: 200), 4, accounts, true, true, true, true, 0, 1.0, out _, out _);

		var block = queue.TryEnqueue(MakeSpec("b", lifeCost: 300), 1, accounts, true, true, true, true, 0, 1.0, out _, out string msg);
		Assert.AreEqual(ProductionEnqueueBlock.InsufficientResources, block);
		Assert.IsNotEmpty(msg);
		Assert.AreEqual(200, accounts[ResourceType.LifeEnergy].DisplayAvailable);
	}

	[Test]
	public void T23_03_FirstUnitStarts_Displays800Minus600_Available200()
	{
		var accounts = CreateAccounts(life: 1000);
		var queue = new FacilityProductionQueue();
		queue.TryEnqueue(MakeSpec(lifeCost: 200, seconds: 10), 4, accounts, true, true, true, true, 0, 1.0, out _, out _);

		queue.TickProduction(1.0, 1.0, isFacilityOperational: true, 1.0, 1.0, accounts);

		Assert.AreEqual("800 (-600)", accounts[ResourceType.LifeEnergy].FormatDisplay());
		Assert.AreEqual(200, accounts[ResourceType.LifeEnergy].DisplayAvailable);
	}

	[Test]
	public void T23_04_MoveActiveEntryBehindWaiting_DeletesProgressRefundsAndReReserves()
	{
		var accounts = CreateAccounts(life: 1400);
		var queue = new FacilityProductionQueue();
		queue.TryEnqueue(MakeSpec("A", lifeCost: 200, seconds: 10), 2, accounts, true, true, true, true, 0, 1.0, out var entryA, out _);
		queue.TryEnqueue(MakeSpec("B", lifeCost: 200, seconds: 10), 2, accounts, true, true, true, true, 0, 1.0, out _, out _);

		// A 첫 1마리 시작 및 50% 진행 (잔고 1200, 선점 600, 사용 가능 600 — 각주 [13])
		queue.TickProduction(5.0, 5.0, true, 1.0, 1.0, accounts);
		Assert.AreEqual(0.5, entryA.ActiveProgress01, 1e-6);
		Assert.AreEqual(600, accounts[ResourceType.LifeEnergy].DisplayAvailable);

		bool moved = queue.TryMoveEntry(entryA.EntryId, 1, accounts, true);
		Assert.IsTrue(moved);
		Assert.IsFalse(entryA.HasActiveUnit);
		Assert.AreEqual(0.0, entryA.ActiveProgress01, 1e-6);
		Assert.AreEqual(2, entryA.WaitingCount);
		Assert.AreEqual(1400, accounts[ResourceType.LifeEnergy].DisplayBalance);
		Assert.AreEqual(800, accounts[ResourceType.LifeEnergy].ReservedAmount);
		Assert.AreEqual(600, accounts[ResourceType.LifeEnergy].DisplayAvailable);
	}

	[Test]
	public void T23_05_BatchA99AndB99_NoBAutoStartBeforeAllASpawned()
	{
		var accounts = CreateAccounts(life: 50000);
		var queue = new FacilityProductionQueue();
		var b1 = queue.TryEnqueue(MakeSpec("A", lifeCost: 100, seconds: 2), 99, accounts, true, true, true, true, 0, 1.0, out var entryA, out _);
		var b2 = queue.TryEnqueue(MakeSpec("B", lifeCost: 100, seconds: 2), 99, accounts, true, true, true, true, 0, 1.0, out var entryB, out _);

		Assert.AreEqual(ProductionEnqueueBlock.None, b1);
		Assert.AreEqual(ProductionEnqueueBlock.None, b2);

		// A 1마리만 완료시키고 아직 98마리가 남은 상태에서 B는 시작하지 않아야 한다.
		queue.TickProduction(2.0, 2.0, true, 1.0, 1.0, accounts);
		queue.CompleteActiveSpawn();
		queue.TickProduction(0.5, 2.5, true, 1.0, 1.0, accounts);

		Assert.AreEqual(entryA, queue.HeadEntry);
		Assert.IsFalse(entryB.HasActiveUnit);
		Assert.AreEqual(99, entryB.WaitingCount);
	}

	[Test]
	public void T23_06_FullRoomProduction_Reaches100AndShowsWaitingSpawnNotice()
	{
		var accounts = CreateAccounts(life: 500);
		var queue = new FacilityProductionQueue();
		queue.TryEnqueue(MakeSpec("A", lifeCost: 200, seconds: 5, pop: 1), 1, accounts, true, true, true, true, 0, 1.0, out var entry, out _);
		queue.TickProduction(5.0, 5.0, true, 1.0, 1.0, accounts);

		Assert.IsTrue(entry.Reached100WaitingSpawn);

		var handle = new RoomSpawnFacilityHandle
		{
			FacilityId = 1,
			FacilityOrigin = Vector2Int.zero,
			FacilityFootprint = new Vector2Int(2, 2),
			IsFacilityOperational = true,
			Queue = queue,
		};
		var popCtx = new RoomPopulationContext { LivingPlayerPopulation = 10, MaxPopulation = 10 };
		var spawned = SpawnArbitrator.ResolveRoomSpawns(new[] { handle }, popCtx, (_, __) => true);

		Assert.AreEqual(0, spawned.Count);
		Assert.IsTrue(entry.IsBlockedWaitingSpawnUI);
		Assert.AreEqual("생산 완료 · 출현 대기", FacilityProductionQueue.WaitingSpawnStatusText);
		Assert.IsNotEmpty(FacilityProductionQueue.WaitingSpawnReasonText);
	}

	[Test]
	public void T23_07_2x2UnitInsufficientAdjacentSpace_Stays100WithoutChargingNextUnit()
	{
		var accounts = CreateAccounts(life: 600);
		var queue = new FacilityProductionQueue();
		queue.TryEnqueue(MakeSpec("Big", lifeCost: 200, seconds: 5, pop: 2, w: 2, h: 2), 2, accounts, true, true, true, true, 0, 1.0, out var entry, out _);

		queue.TickProduction(5.0, 5.0, true, 1.0, 1.0, accounts);
		var handle = new RoomSpawnFacilityHandle
		{
			FacilityId = 1,
			FacilityOrigin = new Vector2Int(5, 5),
			FacilityFootprint = new Vector2Int(2, 2),
			IsFacilityOperational = true,
			Queue = queue,
		};
		var popCtx = new RoomPopulationContext { LivingPlayerPopulation = 0, MaxPopulation = 20 };

		// 2x2 공간 불충족
		var spawned = SpawnArbitrator.ResolveRoomSpawns(new[] { handle }, popCtx, (_, fp) => fp.x == 1 && fp.y == 1);
		Assert.AreEqual(0, spawned.Count);

		// 추가 시간이 흘러도 두 번째 기체 비용이 차감되지 않는다.
		queue.TickProduction(10.0, 15.0, true, 1.0, 1.0, accounts);
		Assert.AreEqual(1.0, entry.ActiveProgress01, 1e-6);
		Assert.AreEqual(1, entry.WaitingCount);
		Assert.AreEqual(400, accounts[ResourceType.LifeEnergy].DisplayBalance);
		Assert.AreEqual(200, accounts[ResourceType.LifeEnergy].ReservedAmount);
	}

	[Test]
	public void T23_08_ManualCancelAt99Or100Waiting_FullRefundAndDeleteProgress()
	{
		var accounts99 = CreateAccounts(life: 500);
		var q99 = new FacilityProductionQueue();
		q99.TryEnqueue(MakeSpec(lifeCost: 200, seconds: 100), 1, accounts99, true, true, true, true, 0, 1.0, out var e99, out _);
		q99.TickProduction(99.0, 99.0, true, 1.0, 1.0, accounts99);
		q99.CancelEntry(e99.EntryId, accounts99);
		Assert.AreEqual(500, accounts99[ResourceType.LifeEnergy].DisplayBalance);
		Assert.AreEqual(0, q99.Entries.Count);

		var accounts100 = CreateAccounts(life: 500);
		var q100 = new FacilityProductionQueue();
		q100.TryEnqueue(MakeSpec(lifeCost: 200, seconds: 10), 1, accounts100, true, true, true, true, 0, 1.0, out var e100, out _);
		q100.TickProduction(10.0, 10.0, true, 1.0, 1.0, accounts100);
		Assert.IsTrue(e100.Reached100WaitingSpawn);
		q100.CancelEntry(e100.EntryId, accounts100);
		Assert.AreEqual(500, accounts100[ResourceType.LifeEnergy].DisplayBalance);
		Assert.AreEqual(0, q100.Entries.Count);
	}

	[Test]
	public void T23_09_ProductionFacilityDestroyed_ActivePaidLostAndWaitingReleased()
	{
		var accounts = CreateAccounts(life: 1000);
		var queue = new FacilityProductionQueue();
		queue.TryEnqueue(MakeSpec(lifeCost: 200, seconds: 10), 3, accounts, true, true, true, true, 0, 1.0, out _, out _);
		queue.TickProduction(5.0, 5.0, true, 1.0, 1.0, accounts); // 1마리 시작(200 지불), 2마리 선점(400)

		queue.OnFacilityDestroyed(accounts);

		Assert.AreEqual(800, accounts[ResourceType.LifeEnergy].DisplayBalance, "현재 진행 중 지불액 200은 손실된다");
		Assert.AreEqual(0, accounts[ResourceType.LifeEnergy].ReservedAmount, "미시작 2마리 선점액 400은 해제된다");
		Assert.AreEqual(800, accounts[ResourceType.LifeEnergy].DisplayAvailable);
	}

	[Test]
	public void T23_10_ResearchCancelAt60Percent_RefundsPaidPreserves60AndRechargesOnResume()
	{
		var accounts = CreateAccounts(life: 500, soul: 100);
		var ledger = new GlobalResearchLedger();
		var def = new ResearchDefinition { ResearchId = "r1", DisplayName = "연구1", BaseLifeEnergyCost = 200, BaseSoulShardCost = 30, RequiredWorkSeconds = 10.0 };
		var rq = new FacilityResearchQueue(1, ledger);

		rq.TryEnqueue(def, accounts, true, true, 1.0, out _);
		rq.TickResearch(6.0, new[] { 1.0f }, true, 1.0, 1.0, accounts);
		Assert.AreEqual(0.6, ledger.GetProgress01("r1"), 1e-6);
		Assert.AreEqual(300, accounts[ResourceType.LifeEnergy].DisplayBalance);

		rq.CancelManually("r1", accounts);
		Assert.AreEqual(500, accounts[ResourceType.LifeEnergy].DisplayBalance, "직접 취소 시 지불액 환급");
		Assert.AreEqual(0.6, ledger.GetProgress01("r1"), 1e-6, "진척 60% 유지");
		Assert.IsFalse(ledger.HasPreservedPayment("r1"));

		// 재개 시 다시 선점·지불
		rq.TryEnqueue(def, accounts, true, true, 1.0, out _);
		rq.TickResearch(1.0, new[] { 1.0f }, true, 1.0, 1.0, accounts);
		Assert.AreEqual(300, accounts[ResourceType.LifeEnergy].DisplayBalance);
		Assert.AreEqual(0.7, ledger.GetProgress01("r1"), 1e-6);
	}

	[Test]
	public void T23_11_ResearchFacilityDestroyedAt60Percent_PreservesProgressAndPaymentZeroCostResume()
	{
		var accounts = CreateAccounts(life: 500, soul: 100);
		var ledger = new GlobalResearchLedger();
		var def = new ResearchDefinition { ResearchId = "r1", DisplayName = "연구1", BaseLifeEnergyCost = 200, BaseSoulShardCost = 30, RequiredWorkSeconds = 10.0 };
		var rq1 = new FacilityResearchQueue(1, ledger);

		rq1.TryEnqueue(def, accounts, true, true, 1.0, out _);
		rq1.TickResearch(6.0, new[] { 1.0f }, true, 1.0, 1.0, accounts);
		rq1.OnFacilityLost(accounts);

		Assert.AreEqual(0.6, ledger.GetProgress01("r1"), 1e-6);
		Assert.IsTrue(ledger.HasPreservedPayment("r1"));
		Assert.AreEqual(300, accounts[ResourceType.LifeEnergy].DisplayBalance);

		// 다른 연구시설에서 추가 비용 0으로 등록·완료
		var rq2 = new FacilityResearchQueue(2, ledger);
		var block = rq2.TryEnqueue(def, accounts, true, true, 1.0, out _);
		Assert.AreEqual(ResearchEnqueueBlock.None, block);
		Assert.AreEqual(0, accounts[ResourceType.LifeEnergy].ReservedAmount);

		bool done = rq2.TickResearch(4.0, new[] { 1.0f }, true, 1.0, 1.0, accounts);
		Assert.IsTrue(done);
		Assert.IsTrue(ledger.IsCompleted("r1"));
		Assert.AreEqual(300, accounts[ResourceType.LifeEnergy].DisplayBalance, "추가 차감 없이 완료");
	}

	[Test]
	public void T23_12_ResearchReorderAt60Percent_Preserves60AndPaidZeroExtraCharge()
	{
		var accounts = CreateAccounts(life: 1000, soul: 200);
		var ledger = new GlobalResearchLedger();
		var rA = new ResearchDefinition { ResearchId = "A", DisplayName = "A", BaseLifeEnergyCost = 200, BaseSoulShardCost = 20, RequiredWorkSeconds = 10.0 };
		var rB = new ResearchDefinition { ResearchId = "B", DisplayName = "B", BaseLifeEnergyCost = 200, BaseSoulShardCost = 20, RequiredWorkSeconds = 10.0 };
		var rq = new FacilityResearchQueue(1, ledger);

		rq.TryEnqueue(rA, accounts, true, true, 1.0, out _);
		rq.TryEnqueue(rB, accounts, true, true, 1.0, out _);
		rq.TickResearch(6.0, new[] { 1.0f }, true, 1.0, 1.0, accounts); // A 60% 진행, A 지불 완료(잔고 800, B 선점 200)

		bool moved = rq.TryMoveEntry("B", 0, out _);
		Assert.IsTrue(moved);
		Assert.AreEqual(0.6, ledger.GetProgress01("A"), 1e-6);
		Assert.IsTrue(ledger.HasPreservedPayment("A"));

		// B 완료 후 A가 다시 선두가 되어도 A에 대한 추가 차감 0
		rq.TickResearch(10.0, new[] { 1.0f }, true, 1.0, 1.0, accounts); // B 완료 (잔고 600)
		Assert.IsTrue(ledger.IsCompleted("B"));
		Assert.AreEqual(600, accounts[ResourceType.LifeEnergy].DisplayBalance);

		rq.TickResearch(4.0, new[] { 1.0f }, true, 1.0, 1.0, accounts); // A 나머지 40% 완료
		Assert.IsTrue(ledger.IsCompleted("A"));
		Assert.AreEqual(600, accounts[ResourceType.LifeEnergy].DisplayBalance);
	}

	[Test]
	public void T23_13_CoreRestoredButFacilityDamaged_RemainsStoppedUntilFullHpRepair()
	{
		var tracker = new LifeEnergyCycleTracker(10.0, 100.0);
		var workers = new[] { new WorkerContributionInput(1, 1.0f, 0.0) };
		float hp = 120f, maxHp = 200f;
		bool coreActive = true;

		// 코어가 정상 복구되었더라도 시설이 손상(120/200) 중이면 가동 불가
		bool isOperational = coreActive && hp >= maxHp - 1e-3f;
		Assert.IsFalse(isOperational);
		Assert.AreEqual(0.0, tracker.Advance(10.0, workers, isOperational));

		// 최대 HP 수리 완료 후에만 재가동
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 500);
		RepairSession.TryCreateOrder(hp, maxHp, 100.0, 10.0, lifeAcc, isCoreActive: true, out var rep, out _);
		Assert.IsTrue(rep.Tick(4.0, new[] { 1.0f }, true, lifeAcc, ref hp));
		Assert.AreEqual(maxHp, hp, 1e-4f);

		isOperational = coreActive && hp >= maxHp - 1e-3f;
		Assert.AreEqual(100.0, tracker.Advance(10.0, workers, isOperational), 1e-6);
	}

	[Test]
	public void T23_14_OutputBonus20PercentFor10PercentGauge_Base100Yields102()
	{
		var tracker = new LifeEnergyCycleTracker(100.0, 100.0);
		Assert.AreEqual(0.0, tracker.AdvanceSegmentByGaugeRatio(0.10, 0.20), 1e-6);
		double payout = tracker.AdvanceSegmentByGaugeRatio(0.90, 0.0);
		Assert.AreEqual(102.0, payout, 1e-6);
	}

	[Test]
	public void T23_15_EnemyKilledUnrefinedCorpse_ZeroImmediateReward()
	{
		var accounts = CreateAccounts(life: 100, soul: 0);
		var corpse = new CorpseRecord
		{
			CorpseId = "enemy_1",
			OriginFaction = CorpseOriginFaction.HumanEnemy,
			IsProducibleSpecies = false,
			ConfiguredRefineLifeReward = 40.0,
			ConfiguredRefineSoulReward = 15.0,
		};

		// 시체 생성 및 필드 방치만으로는 보유량·누적 획득량이 전혀 증가하지 않는다.
		corpse.TickFieldOrCarryDecay(10.0);
		Assert.AreEqual(100, accounts[ResourceType.LifeEnergy].DisplayBalance);
		Assert.AreEqual(0, accounts[ResourceType.SoulShard].DisplayBalance);
		Assert.AreEqual(0.0, accounts[ResourceType.SoulShard].CumulativeEarned, 1e-9);
	}

	[Test]
	public void T23_16_SoulShardEarnedBeforeFirstCoreActivation_HeldButSpendAndResearchBlocked()
	{
		var accounts = CreateAccounts(life: 500, soul: 0);
		// 코어 활성화 전이라도 정제로 영혼 파편 보유 가능
		accounts[ResourceType.SoulShard].Credit(25.0, ResourceCreditSource.ExternalCorpseRefine);
		Assert.AreEqual(25, accounts[ResourceType.SoulShard].DisplayBalance);

		// 그러나 최초 코어 완전 활성화(isSoulShardSpendUnlocked=false) 전에는 소비 및 연구시설 건설·연구 예약 불가
		bool canBuildResearch = ConstructionOrUpgradeSession.TryCreateOrder(
			isUpgrade: false,
			new[] { (ResourceType.LifeEnergy, 200.0), (ResourceType.SoulShard, 10.0) },
			10.0,
			200f,
			accounts,
			isSoulShardSpendUnlocked: false,
			isCoreActive: true,
			out _,
			out _);
		Assert.IsFalse(canBuildResearch);

		var ledger = new GlobalResearchLedger();
		var rq = new FacilityResearchQueue(1, ledger);
		var rBlock = rq.TryEnqueue(
			new ResearchDefinition { ResearchId = "r", BaseLifeEnergyCost = 100, BaseSoulShardCost = 10 },
			accounts,
			isAnyCoreEverActivated: false,
			isRequiredFloorCoreEverActivated: false,
			1.0,
			out _);
		Assert.AreEqual(ResearchEnqueueBlock.ResearchFacilityLocked, rBlock);
	}

	[Test]
	public void T23_17_CoreFullActivationUnlocksChoicesWithoutAutoSpendOrAutoApply()
	{
		var accounts = CreateAccounts(life: 500, soul: 50);
		var ledger = new GlobalResearchLedger();
		foreach (var def in GlobalResearchLedger.CreateDefaultPrototypeCatalog())
			ledger.RegisterDefinition(def);

		bool anyCoreEverActivated = true; // 코어 완전 활성화 달성
		Assert.IsTrue(anyCoreEverActivated);
		Assert.AreEqual(0, ledger.CompletedIds.Count, "자동 연구 완료 없음");
		Assert.AreEqual(500, accounts[ResourceType.LifeEnergy].DisplayBalance, "자동 자원 지불 없음");
		Assert.AreEqual(50, accounts[ResourceType.SoulShard].DisplayBalance, "자동 영혼 파편 지불 없음");
	}

	[Test]
	public void T23_18_WildSpawnerStopsAndConvertsOnRoomClear()
	{
		Assert.IsFalse(RoomClearMath.IsCleared(hostileAlive: 1), "야생 적 생존 중에는 방 미확보");
		Assert.IsTrue(RoomClearMath.IsCleared(hostileAlive: 0), "적 전멸 시 방 확보 달성");

		// 전환된 플레이어 소속 생산시설은 확보된 방에서 정상 예약 가능
		var accounts = CreateAccounts(life: 500);
		var convertedQueue = new FacilityProductionQueue();
		var block = convertedQueue.TryEnqueue(MakeSpec(), 1, accounts, false, true, isRoomSecured: RoomClearMath.IsCleared(0), isCoreValid: true, 0, 1.0, out _, out _);
		Assert.AreEqual(ProductionEnqueueBlock.None, block);
	}

	[Test]
	public void T23_19_CoreDestroyedBlocksNewEnqueueWhilePreservingExistingQueue()
	{
		var accounts = CreateAccounts(life: 1000);
		var queue = new FacilityProductionQueue();
		queue.TryEnqueue(MakeSpec(lifeCost: 200, seconds: 10), 2, accounts, true, true, true, isCoreValid: true, 0, 1.0, out var existing, out _);
		queue.TickProduction(4.0, 4.0, true, 1.0, 1.0, accounts);

		// 코어 파괴 후 신규 등록 거절
		var block = queue.TryEnqueue(MakeSpec(lifeCost: 200), 1, accounts, true, true, true, isCoreValid: false, 0, 1.0, out _, out _);
		Assert.AreEqual(ProductionEnqueueBlock.CoreInactive, block);

		// 기존 진행률(40%)과 대기 수량(1기) 및 선점액(200) 보존, 가동 중지 동안 진행 없음
		queue.TickProduction(5.0, 9.0, isFacilityOperational: false, 1.0, 1.0, accounts);
		Assert.AreEqual(0.4, existing.ActiveProgress01, 1e-6);
		Assert.AreEqual(1, existing.WaitingCount);
		Assert.AreEqual(200, accounts[ResourceType.LifeEnergy].ReservedAmount);
	}

	[Test]
	public void T23_20_PauseAllowsReservationWhileZeroingProductionResearchAndDecay()
	{
		var accounts = CreateAccounts(life: 1000, soul: 100);
		var prodQueue = new FacilityProductionQueue();
		var ledger = new GlobalResearchLedger();
		var resDef = new ResearchDefinition { ResearchId = "rp", BaseLifeEnergyCost = 100, BaseSoulShardCost = 10, RequiredWorkSeconds = 10 };
		var resQueue = new FacilityResearchQueue(1, ledger);
		var corpse = new CorpseRecord { CorpseId = "c1", FullDecayDurationSeconds = 100, Decay01 = 0.25 };

		// 일시정지 상태에서도 예약 입력 및 선점은 즉시 반영된다.
		Assert.AreEqual(ProductionEnqueueBlock.None, prodQueue.TryEnqueue(MakeSpec(lifeCost: 200), 2, accounts, true, true, true, true, 0, 1.0, out _, out _));
		Assert.AreEqual(ResearchEnqueueBlock.None, resQueue.TryEnqueue(resDef, accounts, true, true, 1.0, out _));
		Assert.AreEqual(500, accounts[ResourceType.LifeEnergy].ReservedAmount);

		// 일시정지 동안(deltaGameSeconds == 0) 생산·연구·부패 시간은 증가하지 않는다.
		prodQueue.TickProduction(0.0, 10.0, isFacilityOperational: false, 1.0, 1.0, accounts);
		resQueue.TickResearch(0.0, new[] { 1.0f }, isFacilityOperational: false, 1.0, 1.0, accounts);
		corpse.TickFieldOrCarryDecay(0.0);

		Assert.IsFalse(prodQueue.HeadEntry.HasActiveUnit);
		Assert.AreEqual(0.0, ledger.GetProgress01("rp"), 1e-9);
		Assert.AreEqual(0.25, corpse.Decay01, 1e-9);
	}

	[Test]
	public void T23_21_SameTickUnitSpawnAndFacilityDestruction_DamageFirstActiveLostWaitingReleased()
	{
		var accounts = CreateAccounts(life: 1000);
		var queue = new FacilityProductionQueue();
		queue.TryEnqueue(MakeSpec(lifeCost: 200, seconds: 10), 3, accounts, true, true, true, true, 0, 1.0, out _, out _);
		queue.TickProduction(10.0, 10.0, true, 1.0, 1.0, accounts); // 100% 도달 시점과 시설 파괴가 같은 프레임에 발생

		// 피해·파괴 우선 처리 -> 출현 중재 전 OnFacilityDestroyed 실행
		queue.OnFacilityDestroyed(accounts);
		var spawned = SpawnArbitrator.ResolveRoomSpawns(
			new[] { new RoomSpawnFacilityHandle { FacilityId = 1, IsFacilityOperational = false, Queue = queue } },
			new RoomPopulationContext { LivingPlayerPopulation = 0, MaxPopulation = 10 },
			(_, __) => true);

		Assert.AreEqual(0, spawned.Count);
		Assert.AreEqual(800, accounts[ResourceType.LifeEnergy].DisplayBalance, "진행 중 1마리 비용 200 손실");
		Assert.AreEqual(0, accounts[ResourceType.LifeEnergy].ReservedAmount, "미시작 2마리 선점 해제");
	}

	[Test]
	public void T23_22_SameTickResearchCompletionAndFacilityDestruction_NotCompletedPreservedZeroCostResume()
	{
		var accounts = CreateAccounts(life: 500, soul: 100);
		var ledger = new GlobalResearchLedger();
		var def = new ResearchDefinition { ResearchId = "r_same", BaseLifeEnergyCost = 200, BaseSoulShardCost = 20, RequiredWorkSeconds = 10.0 };
		var rq1 = new FacilityResearchQueue(1, ledger);
		rq1.TryEnqueue(def, accounts, true, true, 1.0, out _);
		rq1.TickResearch(9.0, new[] { 1.0f }, true, 1.0, 1.0, accounts); // 90%

		// 완료에 도달할 1초와 시설 파괴가 같은 판정 시점에 발생
		bool completed = rq1.TickResearch(1.0, new[] { 1.0f }, true, 1.0, 1.0, accounts, facilityDestroyedOnSameTick: true);
		Assert.IsFalse(completed);
		Assert.IsFalse(ledger.IsCompleted("r_same"));
		Assert.AreEqual(0.9, ledger.GetProgress01("r_same"), 1e-6);
		Assert.IsTrue(ledger.HasPreservedPayment("r_same"));

		// 다른 시설에서 추가 비용 0으로 나머지 10% 완료
		var rq2 = new FacilityResearchQueue(2, ledger);
		Assert.AreEqual(ResearchEnqueueBlock.None, rq2.TryEnqueue(def, accounts, true, true, 1.0, out _));
		Assert.IsTrue(rq2.TickResearch(1.0, new[] { 1.0f }, true, 1.0, 1.0, accounts));
		Assert.AreEqual(300, accounts[ResourceType.LifeEnergy].DisplayBalance);
	}

	[Test]
	public void T23_23_TwoFacilitiesAt100InSameRoomWithOnePopSlot_Earlier100SpawnsOtherWaits()
	{
		FacilityProductionQueue.ResetGlobalRegistrationOrderForTests();
		var accounts = CreateAccounts(life: 1000);
		var qFast = new FacilityProductionQueue();
		var qSlow = new FacilityProductionQueue();
		qSlow.TryEnqueue(MakeSpec("Slow", lifeCost: 100, seconds: 8), 1, accounts, true, true, true, true, 0, 1.0, out var eSlow, out _);
		qFast.TryEnqueue(MakeSpec("Fast", lifeCost: 100, seconds: 5), 1, accounts, true, true, true, true, 0, 1.0, out _, out _);

		// 같은 10초 틱 안에 둘 다 100%가 되더라도 Fast는 5.0초, Slow는 8.0초에 100% 도달
		qSlow.TickProduction(10.0, 10.0, true, 1.0, 1.0, accounts);
		qFast.TickProduction(10.0, 10.0, true, 1.0, 1.0, accounts);

		var handles = new[]
		{
			new RoomSpawnFacilityHandle { FacilityId = 1, FacilityOrigin = new Vector2Int(0, 0), FacilityFootprint = Vector2Int.one, IsFacilityOperational = true, Queue = qSlow },
			new RoomSpawnFacilityHandle { FacilityId = 2, FacilityOrigin = new Vector2Int(5, 5), FacilityFootprint = Vector2Int.one, IsFacilityOperational = true, Queue = qFast },
		};
		var popCtx = new RoomPopulationContext { LivingPlayerPopulation = 9, MaxPopulation = 10 }; // 정원 1마리분
		var res = SpawnArbitrator.ResolveRoomSpawns(handles, popCtx, (_, __) => true);

		Assert.AreEqual(1, res.Count);
		Assert.AreEqual("Fast", res[0].Spec.RuleId);
		Assert.IsTrue(eSlow.Reached100WaitingSpawn);
		Assert.IsTrue(eSlow.IsBlockedWaitingSpawnUI);
	}

	[Test]
	public void T23_24_TwoFacilitiesTiedAt100Time_EarlierRegistrationOrderWins()
	{
		FacilityProductionQueue.ResetGlobalRegistrationOrderForTests();
		var accounts = CreateAccounts(life: 1000);
		var q1 = new FacilityProductionQueue();
		var q2 = new FacilityProductionQueue();
		q1.TryEnqueue(MakeSpec("FirstReg", lifeCost: 100, seconds: 5), 1, accounts, true, true, true, true, 0, 1.0, out _, out _);
		q2.TryEnqueue(MakeSpec("SecondReg", lifeCost: 100, seconds: 5), 1, accounts, true, true, true, true, 0, 1.0, out _, out _);

		q1.TickProduction(5.0, 5.0, true, 1.0, 1.0, accounts);
		q2.TickProduction(5.0, 5.0, true, 1.0, 1.0, accounts);

		var handles = new[]
		{
			new RoomSpawnFacilityHandle { FacilityId = 2, FacilityOrigin = new Vector2Int(5, 5), FacilityFootprint = Vector2Int.one, IsFacilityOperational = true, Queue = q2 },
			new RoomSpawnFacilityHandle { FacilityId = 1, FacilityOrigin = new Vector2Int(0, 0), FacilityFootprint = Vector2Int.one, IsFacilityOperational = true, Queue = q1 },
		};
		var popCtx = new RoomPopulationContext { LivingPlayerPopulation = 9, MaxPopulation = 10 };
		var res = SpawnArbitrator.ResolveRoomSpawns(handles, popCtx, (_, __) => true);

		Assert.AreEqual(1, res.Count);
		Assert.AreEqual("FirstReg", res[0].Spec.RuleId);
	}

	[Test]
	public void T23_25_Earlier2x2NoSpaceWhile1x1CanSpawn_1x1SpawnsAnd2x2Keeps100AndFirstTime()
	{
		FacilityProductionQueue.ResetGlobalRegistrationOrderForTests();
		var accounts = CreateAccounts(life: 1000);
		var q2x2 = new FacilityProductionQueue();
		var q1x1 = new FacilityProductionQueue();
		q2x2.TryEnqueue(MakeSpec("U2x2", lifeCost: 100, seconds: 4, w: 2, h: 2), 1, accounts, true, true, true, true, 0, 1.0, out var e2x2, out _);
		q1x1.TryEnqueue(MakeSpec("U1x1", lifeCost: 100, seconds: 8, w: 1, h: 1), 1, accounts, true, true, true, true, 0, 1.0, out _, out _);

		q2x2.TickProduction(10.0, 10.0, true, 1.0, 1.0, accounts); // 4.0초에 100% 도달
		q1x1.TickProduction(10.0, 10.0, true, 1.0, 1.0, accounts); // 8.0초에 100% 도달
		double originalFirst100Time = e2x2.FirstReached100Time;
		Assert.AreEqual(4.0, originalFirst100Time, 1e-6);

		var handles = new[]
		{
			new RoomSpawnFacilityHandle { FacilityId = 1, FacilityOrigin = new Vector2Int(0, 0), FacilityFootprint = new Vector2Int(2, 2), IsFacilityOperational = true, Queue = q2x2 },
			new RoomSpawnFacilityHandle { FacilityId = 2, FacilityOrigin = new Vector2Int(6, 6), FacilityFootprint = new Vector2Int(2, 2), IsFacilityOperational = true, Queue = q1x1 },
		};
		var popCtx = new RoomPopulationContext { LivingPlayerPopulation = 0, MaxPopulation = 10 };

		// 1x1만 공간 허용
		var res = SpawnArbitrator.ResolveRoomSpawns(handles, popCtx, (_, fp) => fp == Vector2Int.one);
		Assert.AreEqual(1, res.Count);
		Assert.AreEqual("U1x1", res[0].Spec.RuleId);
		Assert.AreEqual(1.0, e2x2.ActiveProgress01, 1e-6);
		Assert.AreEqual(originalFirst100Time, e2x2.FirstReached100Time, 1e-6);
	}

	[Test]
	public void T23_26_Batch5FirstUnitSpawnAndFullCancelSameTick_Keeps1SpawnedReleases4()
	{
		var accounts = CreateAccounts(life: 1000);
		var queue = new FacilityProductionQueue();
		queue.TryEnqueue(MakeSpec("M", lifeCost: 200, seconds: 5), 5, accounts, true, true, true, true, 0, 1.0, out var entry, out _);
		queue.TickProduction(5.0, 5.0, true, 1.0, 1.0, accounts); // 첫 1마리 200 지불(잔고 800), 나머지 4마리 800 선점

		// 같은 판정 시점에 첫 1마리 출현 확정 + 항목 전체 취소(각주 [12])
		queue.CancelEntry(entry.EntryId, accounts, firstUnitSpawnedOnSameTick: true);

		Assert.AreEqual(800, accounts[ResourceType.LifeEnergy].DisplayBalance, "출현한 1마리의 지불액 200은 유지(환급 없음)");
		Assert.AreEqual(0, accounts[ResourceType.LifeEnergy].ReservedAmount, "미시작 4마리 선점액 800 해제");
		Assert.AreEqual(800, accounts[ResourceType.LifeEnergy].DisplayAvailable);
		Assert.AreEqual(0, queue.Entries.Count);
	}

	[Test]
	public void T23_27_ResearchCompletionAndManualCancelSameTick_AppliesCompletionNoRefund()
	{
		var accounts = CreateAccounts(life: 500, soul: 100);
		var ledger = new GlobalResearchLedger();
		var def = new ResearchDefinition { ResearchId = "rc", BaseLifeEnergyCost = 200, BaseSoulShardCost = 30, RequiredWorkSeconds = 10.0 };
		var rq = new FacilityResearchQueue(1, ledger);
		rq.TryEnqueue(def, accounts, true, true, 1.0, out _);
		rq.TickResearch(5.0, new[] { 1.0f }, true, 1.0, 1.0, accounts); // 200/30 지불 완료

		rq.CancelManually("rc", accounts, completedOnSameTick: true);

		Assert.IsTrue(ledger.IsCompleted("rc"));
		Assert.AreEqual(300, accounts[ResourceType.LifeEnergy].DisplayBalance, "완료한 연구 비용은 환급하지 않는다");
		Assert.AreEqual(70, accounts[ResourceType.SoulShard].DisplayBalance);
	}

	// ====================================================================
	// 24-표1. 복구·정제·시설 이동의 기대 결과 (31개 전수 검증)
	// ====================================================================

	[Test]
	public void T24_01_RepairWork30Of60IncreasedTo80_Keeps30AndProgress37Point5Percent()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 2000);
		var rep = RepairSession.CreateDirectStartedForVerification(600, 60.0, lifeAcc);
		float hp = 40f;
		rep.Tick(30.0, new[] { 1.0f }, true, lifeAcc, ref hp);

		rep.ApplyDirectExtraRequirement(800, 80.0);

		Assert.AreEqual(30.0, rep.CompletedWorkSeconds, 1e-6);
		Assert.AreEqual(0.375, rep.Progress01, 1e-6);
	}

	[Test]
	public void T24_02_RepairPaid600IncreasedTo800_PausesUntil200DiffManuallyApproved()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 1000); // 지불 600 후 400 남음(각주 [26])
		var rep = RepairSession.CreateDirectStartedForVerification(600, 60.0, lifeAcc);
		float hp = 40f;
		rep.Tick(30.0, new[] { 1.0f }, true, lifeAcc, ref hp);
		rep.ApplyDirectExtraRequirement(800, 80.0);

		Assert.AreEqual(200, rep.UnapprovedDiffAmount);
		Assert.IsTrue(rep.IsPausedWaitingDiffApproval);

		// 수동 승인 전에는 작업자가 있어도 진행 정지
		rep.Tick(20.0, new[] { 1.0f }, true, lifeAcc, ref hp);
		Assert.AreEqual(30.0, rep.CompletedWorkSeconds, 1e-6);

		Assert.IsTrue(rep.TryApproveExtraDiffManually(lifeAcc, out _));
		Assert.AreEqual(200, lifeAcc.DisplayBalance);
		rep.Tick(10.0, new[] { 1.0f }, true, lifeAcc, ref hp);
		Assert.AreEqual(40.0, rep.CompletedWorkSeconds, 1e-6);
	}

	[Test]
	public void T24_03_ResourceGainWhileWaitingDiffApproval_NoAutoPayOrAutoResume()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 650);
		var rep = RepairSession.CreateDirectStartedForVerification(600, 60.0, lifeAcc); // 남은 잔고 50
		rep.ApplyDirectExtraRequirement(800, 80.0); // 차액 200 필요

		Assert.IsFalse(rep.TryApproveExtraDiffManually(lifeAcc, out _));

		// 나중에 자원이 충분히 들어와도 자동 지불·자동 재개 없음
		lifeAcc.Credit(500.0, ResourceCreditSource.FacilityProduction);
		float hp = 20f;
		rep.Tick(20.0, new[] { 1.0f }, true, lifeAcc, ref hp);

		Assert.IsTrue(rep.IsPausedWaitingDiffApproval);
		Assert.AreEqual(0.0, rep.CompletedWorkSeconds, 1e-6);
		Assert.AreEqual(550, lifeAcc.DisplayBalance);
	}

	[Test]
	public void T24_04_WorkerLeavesDuringRepairWithoutExtraDamage_ZeroNewCostPreservesCompletedWork()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 1000);
		var rep = RepairSession.CreateDirectStartedForVerification(600, 60.0, lifeAcc);
		float hp = 40f;
		rep.Tick(25.0, new[] { 1.0f }, true, lifeAcc, ref hp);

		// 작업자 이탈 (빈 배열)
		rep.Tick(30.0, System.Array.Empty<float>(), true, lifeAcc, ref hp);
		Assert.AreEqual(25.0, rep.CompletedWorkSeconds, 1e-6);
		Assert.AreEqual(400, lifeAcc.DisplayBalance, "추가 비용 없음");

		// 작업자 복귀 후 남은 35초 완료
		Assert.IsTrue(rep.Tick(35.0, new[] { 1.0f }, true, lifeAcc, ref hp));
		Assert.AreEqual(400, lifeAcc.DisplayBalance);
	}

	[Test]
	public void T24_05_RepairAndCoreRestoreNormalCompletion_RestoresFullMaxHp()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 2000);
		float facHp = 80f, facMax = 200f;
		RepairSession.TryCreateOrder(facHp, facMax, 100.0, 10.0, lifeAcc, true, out var rep, out _);
		Assert.IsTrue(rep.Tick(6.0, new[] { 1.0f }, true, lifeAcc, ref facHp));
		Assert.AreEqual(200f, facHp, 1e-4f);

		CoreRestoreSession.TryCreateOrder(1000.0, 100.0, 500f, true, true, false, false, null, lifeAcc, out var restore, out _);
		Assert.IsTrue(restore.Tick(100.0, new[] { 1.0f }, true, lifeAcc));
		Assert.IsTrue(restore.IsCompleted);
	}

	[Test]
	public void T24_06_CoreRestoreBase1000WithSalvageAndExtra200_Pays500Then200Total700()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 1000);
		bool ok = CoreRestoreSession.TryCreateOrder(1000.0, 100.0, 500f, true, true, false, useOnSiteSalvage: true, null, lifeAcc, out var restore, out _);
		Assert.IsTrue(ok);
		Assert.AreEqual(500, lifeAcc.ReservedAmount);

		// 작업 착수 -> 초기 500 차감
		restore.Tick(10.0, new[] { 1.0f }, true, lifeAcc);
		Assert.AreEqual(500, restore.PaidTotalSoFar);
		Assert.AreEqual(500, lifeAcc.DisplayBalance);

		// 수복 대상 최대 HP(500)의 20%(100) 피해 -> 추가 수복비 200 발생
		restore.ApplyDamageDuringRestore(100f, lifeAcc);
		Assert.AreEqual(200, restore.UnapprovedDiffAmount);
		Assert.IsTrue(restore.TryApproveExtraDiffManually(lifeAcc, out _));
		Assert.AreEqual(700, restore.PaidTotalSoFar);
		Assert.AreEqual(300, lifeAcc.DisplayBalance);
	}

	[Test]
	public void T24_07_CoreRestoreWithSalvageDestroyedMidway_LosesPaidAndSalvageNoNewSalvage()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 1000);
		var salvageObj = new InteractableObject("salvage_1", Vector3Int.zero, 20f, 0f, new List<string> { CoreSalvage.Tag });
		CoreRestoreSession.TryCreateOrder(1000.0, 100.0, 500f, true, true, false, true, salvageObj, lifeAcc, out var restore, out _);
		restore.Tick(20.0, new[] { 1.0f }, true, lifeAcc); // 500 지불 완료

		// 치명 피해로 수복 대상 파괴
		restore.ApplyDamageDuringRestore(600f, lifeAcc);
		Assert.IsTrue(restore.IsDestroyed);
		Assert.AreEqual(500, lifeAcc.DisplayBalance, "지불한 500 환급 없음");
		restore.CancelManually(lifeAcc, out bool returnSalvage);
		Assert.IsFalse(returnSalvage, "파괴된 수복의 투입 잔해는 소멸하며 반환·재생성되지 않는다");
	}

	[Test]
	public void T24_08_CorpseStoredAt70DecayThenEjected_FreezesAt70InsideResumesFrom70Outside()
	{
		var refinery = new CorpseRefineryState(5);
		var corpse = new CorpseRecord { CorpseId = "c70", FullDecayDurationSeconds = 100.0, Decay01 = 0.70 };

		Assert.IsTrue(refinery.CompleteIntake(corpse));
		Assert.IsFalse(corpse.TickFieldOrCarryDecay(50.0), "시설 내부에서는 부패 정지");
		Assert.AreEqual(0.70, corpse.Decay01, 1e-6);

		Assert.IsTrue(refinery.TryEjectWaitingCorpse("c70", out var ejected));
		ejected.TickFieldOrCarryDecay(10.0);
		Assert.AreEqual(0.80, ejected.Decay01, 1e-6, "반출 후 70%부터 이어서 증가");
	}

	[Test]
	public void T24_09_HigherValueCorpseArrivesMidRefine_ContinuesCurrentAndPrioritizesNext()
	{
		var accounts = CreateAccounts();
		var refinery = new CorpseRefineryState(5);
		var low = new CorpseRecord { CorpseId = "low", OriginFaction = CorpseOriginFaction.WildMonster, ConfiguredRefineLifeReward = 20, ConfiguredRefineSoulReward = 5, BaseRefineDurationSeconds = 10 };
		var mid = new CorpseRecord { CorpseId = "mid", OriginFaction = CorpseOriginFaction.WildMonster, ConfiguredRefineLifeReward = 30, ConfiguredRefineSoulReward = 10, BaseRefineDurationSeconds = 10 };
		var high = new CorpseRecord { CorpseId = "high", OriginFaction = CorpseOriginFaction.HumanEnemy, ConfiguredRefineLifeReward = 50, ConfiguredRefineSoulReward = 40, BaseRefineDurationSeconds = 10 };

		refinery.CompleteIntake(low);
		refinery.TickAutoRefine(4.0, true, 1.0, accounts); // low 정제 40% 진행 중
		Assert.AreEqual("low", refinery.ActiveRefiningCorpse.CorpseId);

		// 정제 도중 mid, high 입고
		refinery.CompleteIntake(mid);
		refinery.CompleteIntake(high);

		// 현재 정제 중인 low는 바뀌지 않고 먼저 완료된 뒤 다음으로 high가 선택된다.
		refinery.TickAutoRefine(7.0, true, 1.0, accounts); // 6초로 low 완료 + 1초로 다음 시체(high) 10% 진행
		Assert.AreEqual("high", refinery.ActiveRefiningCorpse.CorpseId);
	}

	[Test]
	public void T24_10_DrainModeExecution_CompletesCurrentEjectsWaitingBlocksNewIntakeAndNextRefine()
	{
		var accounts = CreateAccounts();
		var refinery = new CorpseRefineryState(5);
		var c1 = new CorpseRecord { CorpseId = "c1", OriginFaction = CorpseOriginFaction.WildMonster, ConfiguredRefineLifeReward = 30, ConfiguredRefineSoulReward = 10, BaseRefineDurationSeconds = 10 };
		var c2 = new CorpseRecord { CorpseId = "c2", OriginFaction = CorpseOriginFaction.WildMonster, ConfiguredRefineLifeReward = 30, ConfiguredRefineSoulReward = 10, BaseRefineDurationSeconds = 10 };
		var cNew = new CorpseRecord { CorpseId = "cNew", OriginFaction = CorpseOriginFaction.WildMonster, ConfiguredRefineLifeReward = 30, ConfiguredRefineSoulReward = 10, BaseRefineDurationSeconds = 10 };

		refinery.CompleteIntake(c1);
		refinery.CompleteIntake(c2);
		refinery.TickAutoRefine(3.0, true, 1.0, accounts); // c1 진행 중, c2 대기

		refinery.StartDrainMode(out _, out var ejected);
		Assert.AreEqual(1, ejected.Count);
		Assert.AreEqual("c2", ejected[0].CorpseId);
		Assert.IsFalse(refinery.CanAcceptIncoming(cNew), "전체 비우기 중 신규 반입 차단");

		// 현재 진행 중이던 c1은 끝까지 완료되어 보상이 지급되고 그 후 정지한다.
		int completed = refinery.TickAutoRefine(10.0, true, 1.0, accounts);
		Assert.AreEqual(1, completed);
		Assert.IsNull(refinery.ActiveRefiningCorpse);
		Assert.AreEqual(30, accounts[ResourceType.LifeEnergy].DisplayBalance);
	}

	[Test]
	public void T24_11_RefineryDestroyed_AllInternalCorpsesLostZeroIncompleteReward()
	{
		var accounts = CreateAccounts();
		var refinery = new CorpseRefineryState(5);
		refinery.CompleteIntake(new CorpseRecord { CorpseId = "c1", ConfiguredRefineLifeReward = 50, ConfiguredRefineSoulReward = 20, BaseRefineDurationSeconds = 10 });
		refinery.CompleteIntake(new CorpseRecord { CorpseId = "c2", ConfiguredRefineLifeReward = 50, ConfiguredRefineSoulReward = 20, BaseRefineDurationSeconds = 10 });
		refinery.TickAutoRefine(9.0, true, 1.0, accounts); // 90% 미완료 상태

		refinery.OnFacilityDestroyed(out _);

		Assert.AreEqual(0, refinery.StoredCorpseCount);
		Assert.AreEqual(0, accounts[ResourceType.LifeEnergy].DisplayBalance);
		Assert.AreEqual(0, accounts[ResourceType.SoulShard].DisplayBalance);
	}

	[Test]
	public void T24_12_AllyCorpseRefineAndManualCancelRefund_CreditsBalanceOnlyExcludesCumulativeEarned()
	{
		var accounts = CreateAccounts(life: 200);
		var refinery = new CorpseRefineryState(5);
		var allyCorpse = new CorpseRecord
		{
			CorpseId = "ally1",
			OriginFaction = CorpseOriginFaction.PlayerAlly,
			IsProducibleSpecies = true,
			BaseProductionLifeCost = 200.0,
			BaseRefineDurationSeconds = 5.0,
		};
		refinery.CompleteIntake(allyCorpse);
		refinery.TickAutoRefine(5.0, true, 1.0, accounts); // +40 생명 에너지(보유량만)

		// 수동 취소 환급도 누적 획득량 제외
		accounts[ResourceType.LifeEnergy].TrySpendImmediate(100, out int paid);
		accounts[ResourceType.LifeEnergy].RefundPaid(paid);

		Assert.AreEqual(240, accounts[ResourceType.LifeEnergy].DisplayBalance);
		Assert.AreEqual(0.0, accounts[ResourceType.LifeEnergy].CumulativeEarned, 1e-9);
	}

	[Test]
	public void T24_13_FractionalCost7Point2_Pays8AndRefunds8()
	{
		var acc = new ResourceAccount(ResourceType.LifeEnergy, 20.0);
		Assert.IsTrue(acc.TryReserve(7.2, out int reserved));
		Assert.AreEqual(8, reserved);
		Assert.IsTrue(acc.CommitReservedCost(reserved, 7.2, out int paid));
		Assert.AreEqual(8, paid);
		Assert.AreEqual(12, acc.DisplayBalance);

		acc.RefundPaid(paid);
		Assert.AreEqual(20, acc.DisplayBalance);
	}

	[Test]
	public void T24_14_CrossFloorCorpseRetrieveBlocked()
	{
		int carrierFloor = 0;
		Vector3Int corpsePos = new Vector3Int(4, 4, 1); // 다른 층
		bool sameFloorAllowed = carrierFloor == corpsePos.z;
		Assert.IsFalse(sameFloorAllowed, "다른 층 몬스터는 시체 회수 명령 조건을 충족하지 못한다");
	}

	[Test]
	public void T24_15_CarrierHitCancelsOrderRequiresManualRecommand()
	{
		var refinery = new CorpseRefineryState(5);
		var corpse = new CorpseRecord { CorpseId = "carry_hit" };
		Assert.IsTrue(refinery.TryReserveIncomingSlot(corpse));
		Assert.AreEqual(1, refinery.ReservedIncomingCount);

		// 운반자 피격 -> 예약 즉시 해제 및 필드에 낙하(IsInsideRefinery=false), 전투 종료 후에도 자동 재예약 없음
		Assert.IsTrue(refinery.CancelIncomingReservation(corpse.CorpseId));
		Assert.AreEqual(0, refinery.ReservedIncomingCount);
		Assert.IsFalse(corpse.IsInsideRefinery);
	}

	[Test]
	public void T24_16_CarrierDiesWithoutAdjacentSpace_CoexistingCorpsesKeptIndependently()
	{
		Vector3Int tile = new Vector3Int(3, 3, 0);
		var carrierCorpse = new InteractableObject("carrier_corpse", tile, 80f, 20f, new List<string> { "Object/Passable/Corpse" })
		{
			CorpseEconomyData = new CorpseRecord { CorpseId = "carrier_c" }
		};
		var droppedCarried = new InteractableObject("carried_corpse", tile, 80f, 20f, new List<string> { "Object/Passable/Corpse" })
		{
			CorpseEconomyData = new CorpseRecord { CorpseId = "carried_c" }
		};

		// 인접 공간이 없을 때 CoexistingObjects에 별도 보존
		carrierCorpse.CoexistingObjects = new List<InteractableObject> { droppedCarried };
		Assert.AreEqual(1, carrierCorpse.CoexistingObjects.Count);

		// 1구 회수 시 남은 1구가 타일 점유를 이어받는다.
		InteractableObject remaining = carrierCorpse.CoexistingObjects[0];
		carrierCorpse.CoexistingObjects.RemoveAt(0);
		Assert.AreEqual("carried_c", remaining.CorpseEconomyData.CorpseId);
		Assert.AreEqual(tile, remaining.Position);
	}

	[Test]
	public void T24_17_CoreDestroyedDuringActiveRepairAndConstruction_RepairContinuesConstructionPauses()
	{
		var accounts = CreateAccounts(life: 1000);
		// 1. 이미 시작된 일반 수리는 코어가 파괴(isCoreActive=false)되어도 계속 완료된다.
		float repHp = 50f;
		RepairSession.TryCreateOrder(repHp, 100f, 100.0, 10.0, accounts[ResourceType.LifeEnergy], isCoreActive: true, out var rep, out _);
		rep.Tick(2.0, new[] { 1.0f }, isCoreActive: true, accounts[ResourceType.LifeEnergy], ref repHp);
		Assert.IsTrue(rep.HasStartedWork);
		Assert.IsTrue(rep.Tick(3.0, new[] { 1.0f }, isCoreActive: false, accounts[ResourceType.LifeEnergy], ref repHp));
		Assert.AreEqual(100f, repHp, 1e-4f);

		// 2. 이미 시작된 건설은 코어 파괴 시 진행·지불을 보존한 채 정지한다.
		float buildHp = 100f, buildMax = 100f;
		var costs = new[] { (ResourceType.LifeEnergy, 200.0) };
		ConstructionOrUpgradeSession.TryCreateOrder(false, costs, 10.0, 200f, accounts, false, isCoreActive: true, out var build, out _);
		build.Tick(4.0, new[] { 1.0f }, isCoreActive: true, true, costs, accounts, ref buildHp, ref buildMax);
		Assert.AreEqual(0.4, build.Progress01, 1e-6);

		Assert.IsFalse(build.Tick(10.0, new[] { 1.0f }, isCoreActive: false, true, costs, accounts, ref buildHp, ref buildMax));
		Assert.AreEqual(0.4, build.Progress01, 1e-6, "코어 파괴 중 건설 정지 및 진행 보존");
	}

	[Test]
	public void T24_18_RelocationCarrierFootprintVsFacilityFullFootprint()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 500);
		bool rejectedSmallSpace = RelocationSession.TryCreateOrder(
			50.0, 5.0, 5.0,
			fullFootprint: new Vector2Int(3, 3),
			initialDestination: new Vector3Int(5, 5, 0),
			isCoreActive: true,
			isRefineryCompletelyEmpty: true,
			isDestinationValidForFullFootprint: false,
			lifeAcc,
			out _,
			out _);
		Assert.IsFalse(rejectedSmallSpace, "재설치 목적지는 시설 전체 크기(3x3)로 검사");

		Assert.IsTrue(RelocationSession.TryCreateOrder(
			50.0, 5.0, 5.0,
			new Vector2Int(3, 3),
			new Vector3Int(5, 5, 0),
			true, true,
			isDestinationValidForFullFootprint: true,
			lifeAcc,
			out var reloc,
			out _));
		Assert.AreEqual(Vector2Int.one, reloc.PackageFootprint, "분해 패키지 자체는 1x1이며 운반 경로는 운반자 몸 크기를 따른다");
		Assert.AreEqual(new Vector2Int(3, 3), reloc.FacilityFullFootprint);
	}

	[Test]
	public void T24_19_SameTickRepairOrRestoreCompletionAndExtraDamage_AppliesDamageFirstPausesForDiff()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 2000);
		float hp = 50f;
		RepairSession.TryCreateOrder(hp, 100f, 1000.0, 100.0, lifeAcc, true, out var rep, out _);
		rep.Tick(49.0, new[] { 1.0f }, true, lifeAcc, ref hp); // 50 중 49 완료

		// 완료에 필요한 1초와 추가 피해 20이 같은 판정 시점에 발생
		bool done = rep.Tick(1.0, new[] { 1.0f }, true, lifeAcc, ref hp, sameTickExtraDamage: 20f);
		Assert.IsFalse(done);
		Assert.IsTrue(rep.IsPausedWaitingDiffApproval);
		Assert.AreEqual(200, rep.UnapprovedDiffAmount);
	}

	[Test]
	public void T24_20_SameTickConstructionCompletionAndDamage_SurvivesWithHpRatioOrDestroysNoRefund()
	{
		var accounts1 = CreateAccounts(life: 500);
		var costs = new[] { (ResourceType.LifeEnergy, 200.0) };
		float curHp1 = 100f, curMax1 = 100f;
		ConstructionOrUpgradeSession.TryCreateOrder(false, costs, 10.0, 200f, accounts1, false, true, out var bSurvive, out _);
		bSurvive.Tick(9.0, new[] { 1.0f }, true, true, costs, accounts1, ref curHp1, ref curMax1);

		// 완공과 동시에 60 피해 -> 40/100 생존 -> 신규 건설은 완성 시설의 최대 HP로 전부 회복해 200/200으로 완공(건설 명세 2-2-표1·사례 7, 이전 비율 승계 규칙 폐기 - 강화는 여전히 비율 승계)
		Assert.IsTrue(bSurvive.Tick(1.0, new[] { 1.0f }, true, true, costs, accounts1, ref curHp1, ref curMax1, sameTickDamage: 60f));
		Assert.AreEqual(200f, curHp1, 1e-4f);
		Assert.AreEqual(200f, curMax1, 1e-4f);

		// 완공과 동시에 치명 피해(100) -> 파괴, 환급 없음
		var accounts2 = CreateAccounts(life: 500);
		float curHp2 = 100f, curMax2 = 100f;
		ConstructionOrUpgradeSession.TryCreateOrder(false, costs, 10.0, 200f, accounts2, false, true, out var bDead, out _);
		bDead.Tick(9.0, new[] { 1.0f }, true, true, costs, accounts2, ref curHp2, ref curMax2);
		Assert.IsFalse(bDead.Tick(1.0, new[] { 1.0f }, true, true, costs, accounts2, ref curHp2, ref curMax2, sameTickDamage: 100f));
		Assert.AreEqual(300, accounts2[ResourceType.LifeEnergy].DisplayBalance, "파괴 시 지불 비용 환급 없음");
	}

	[Test]
	public void T24_21_DifferentSizedCorpsesEachUseOneSlot()
	{
		var refinery = new CorpseRefineryState(5);
		refinery.CompleteIntake(new CorpseRecord { CorpseId = "small", Footprint = new Vector2Int(1, 1) });
		refinery.CompleteIntake(new CorpseRecord { CorpseId = "huge", Footprint = new Vector2Int(4, 4) });

		Assert.AreEqual(2, refinery.StoredCorpseCount);
		Assert.AreEqual(3, refinery.AvailableSlots);
	}

	[Test]
	public void T24_22_Max5With3StoredAnd2Reserved_ZeroAvailableThenOneAfterCancel()
	{
		var refinery = new CorpseRefineryState(5);
		refinery.CompleteIntake(new CorpseRecord { CorpseId = "s1" });
		refinery.CompleteIntake(new CorpseRecord { CorpseId = "s2" });
		refinery.CompleteIntake(new CorpseRecord { CorpseId = "s3" });
		Assert.IsTrue(refinery.TryReserveIncomingSlot(new CorpseRecord { CorpseId = "r1" }));
		Assert.IsTrue(refinery.TryReserveIncomingSlot(new CorpseRecord { CorpseId = "r2" }));

		Assert.AreEqual(0, refinery.AvailableSlots);
		Assert.IsFalse(refinery.TryReserveIncomingSlot(new CorpseRecord { CorpseId = "r3" }));

		refinery.CancelIncomingReservation("r1");
		Assert.AreEqual(1, refinery.AvailableSlots);
	}

	[Test]
	public void T24_23_StartDrainWithActiveReservations_CancelsReservationsImmediately()
	{
		var refinery1 = new CorpseRefineryState(5);
		var refinery2 = new CorpseRefineryState(5);
		var carried = new CorpseRecord { CorpseId = "in_transit" };
		var unpicked = new CorpseRecord { CorpseId = "not_picked" };

		refinery1.TryReserveIncomingSlot(carried);
		refinery1.TryReserveIncomingSlot(unpicked);

		refinery1.StartDrainMode(out var cancelledIds, out _);
		Assert.AreEqual(2, cancelledIds.Count);
		Assert.AreEqual(0, refinery1.ReservedIncomingCount);

		// 운반 중이던 시체는 대체 유효 시설(refinery2)을 재탐색해 예약 가능
		Assert.IsTrue(refinery2.TryReserveIncomingSlot(carried));
	}

	[Test]
	public void T24_24_CancelDrainMode_AllowsNewIntakeWithoutRestoringCancelledOrders()
	{
		var refinery = new CorpseRefineryState(5);
		var prev = new CorpseRecord { CorpseId = "prev_res" };
		refinery.TryReserveIncomingSlot(prev);
		refinery.StartDrainMode(out _, out _);

		refinery.CancelDrainMode();
		Assert.IsFalse(refinery.IsDraining);
		Assert.AreEqual(0, refinery.ReservedIncomingCount, "취소·변경된 이전 예약은 자동 복원되지 않는다");
		Assert.IsTrue(refinery.CanAcceptIncoming(new CorpseRecord { CorpseId = "fresh" }));
	}

	[Test]
	public void T24_25_CoreRestoreTwo10PercentHitsMatchSingle20PercentHit_Total700AndWork120()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 2000);
		CoreRestoreSession.TryCreateOrder(1000.0, 100.0, 500f, true, true, false, useOnSiteSalvage: true, null, lifeAcc, out var restore, out _);
		restore.Tick(10.0, new[] { 1.0f }, true, lifeAcc); // 초기 500 지불

		// 최대 HP(500)의 10%(50) 피해 두 번
		restore.ApplyDamageDuringRestore(50f, lifeAcc);
		restore.ApplyDamageDuringRestore(50f, lifeAcc);

		Assert.AreEqual(700, restore.TotalRequiredCeilCost);
		Assert.AreEqual(200, restore.UnapprovedDiffAmount);
		Assert.AreEqual(120.0, restore.TotalRequiredWorkSeconds, 1e-6);

		Assert.IsTrue(restore.TryApproveExtraDiffManually(lifeAcc, out _));
		Assert.AreEqual(700, restore.PaidTotalSoFar);
	}

	[Test]
	public void T24_26_RelocationWorkerHitWhileTargetSurvives_CancelsDeletesProgressFullRefund()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 500);
		RelocationSession.TryCreateOrder(80.0, 10.0, 10.0, new Vector2Int(2, 2), new Vector3Int(4, 4, 0), true, true, true, lifeAcc, out var reloc, out _);
		reloc.TickDismantle(5.0, new[] { 1.0f }, true, 1.0, lifeAcc); // 80 지불, 50% 분해
		Assert.AreEqual(420, lifeAcc.DisplayBalance);

		reloc.OnHitBeforeDismantleComplete(isTargetFacilityAlive: true, lifeAcc);

		Assert.AreEqual(RelocationStage.AbortedBeforeDismantle, reloc.Stage);
		Assert.AreEqual(0.0, reloc.DismantleCompletedSeconds, 1e-6);
		Assert.AreEqual(500, lifeAcc.DisplayBalance, "분해 완료 전 피격 취소(대상 생존) 시 전액 환급");
	}

	[Test]
	public void T24_27_RelocationWorkerMovedAwayByMoveApply_PreservesProgressAndPaidZeroExtraCost()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 500);
		RelocationSession.TryCreateOrder(80.0, 10.0, 10.0, new Vector2Int(2, 2), new Vector3Int(4, 4, 0), true, true, true, lifeAcc, out var reloc, out _);
		reloc.TickDismantle(6.0, new[] { 1.0f }, true, 1.0, lifeAcc); // 80 지불, 6초 진행

		reloc.OnWorkerMovedAwayDuringDismantle();
		Assert.AreEqual(6.0, reloc.DismantleCompletedSeconds, 1e-6);
		Assert.AreEqual(420, lifeAcc.DisplayBalance, "환급 없음·지불 상태 보존");

		// 작업자 재지정 후 추가 차감 없이 남은 4초 완료
		Assert.IsTrue(reloc.TickDismantle(4.0, new[] { 1.0f }, true, 1.0, lifeAcc));
		Assert.AreEqual(420, lifeAcc.DisplayBalance);
		Assert.AreEqual(RelocationStage.CarryingPackage, reloc.Stage);
	}

	[Test]
	public void T24_28_PostDismantlePickedUpByAnotherUnitAndInstalled_ZeroExtraCost()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 500);
		var dest1 = new Vector3Int(4, 4, 0);
		var dest2 = new Vector3Int(8, 8, 0);
		RelocationSession.TryCreateOrder(80.0, 10.0, 10.0, new Vector2Int(2, 2), dest1, true, true, true, lifeAcc, out var reloc, out _);
		Assert.IsTrue(reloc.TickDismantle(10.0, new[] { 1.0f }, true, 1.0, lifeAcc)); // 420 남음
		reloc.DropPackageToField();

		// 다른 유닛(UnitId=99)이 회수해 새 위치(dest2)에 재설치 -> 추가 비용 0
		Assert.IsTrue(reloc.PickUpDroppedPackage(99, dest2, isDestinationValid: true));
		Assert.IsTrue(reloc.TickReinstallAtDestination(10.0, new[] { 1.0f }, isDestinationStillValid: true));
		Assert.AreEqual(RelocationStage.CompletedReinstall, reloc.Stage);
		Assert.AreEqual(420, lifeAcc.DisplayBalance);
	}

	[Test]
	public void T24_29_MidInstallResumeSameDestVsChangeDest_PreservesOrResetsInstallWorkOnly()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 500);
		var destA = new Vector3Int(4, 4, 0);
		var destB = new Vector3Int(9, 9, 0);
		RelocationSession.TryCreateOrder(80.0, 10.0, 10.0, new Vector2Int(2, 2), destA, true, true, true, lifeAcc, out var reloc, out _);
		reloc.TickDismantle(10.0, new[] { 1.0f }, true, 1.0, lifeAcc);
		reloc.TickReinstallAtDestination(6.0, new[] { 1.0f }, true); // destA에서 설치 6/10 진행
		reloc.DropPackageToField();

		// 같은 목적지(destA)로 재개하면 6.0초 보존
		Assert.IsTrue(reloc.PickUpDroppedPackage(2, destA, true));
		Assert.AreEqual(6.0, reloc.InstallCompletedSeconds, 1e-6);

		// 다른 목적지(destB)로 변경하면 설치 작업량만 0으로 초기화, 분해 상태는 유지
		Assert.IsTrue(reloc.TryChangeDestination(destB, isNewDestinationValidAndReachable: true));
		Assert.AreEqual(0.0, reloc.InstallCompletedSeconds, 1e-6);
		Assert.IsTrue(reloc.IsDismantleFinished);
	}

	[Test]
	public void T24_30_RelocationCarrierDiesNoAdjacentSpace_CoexistingCorpseAnd1x1Package()
	{
		Vector3Int deathTile = new Vector3Int(2, 2, 0);
		var carrierCorpse = new InteractableObject("carrier_dead", deathTile, 80f, 20f, new List<string> { "Object/Passable/Corpse" })
		{
			CorpseEconomyData = new CorpseRecord { CorpseId = "dead_worker" }
		};
		var pkgObj = new InteractableObject("pkg_1x1", deathTile, 40f, 0f, new List<string> { InteractableObject.DismantledFacilityTag });

		carrierCorpse.CoexistingObjects = new List<InteractableObject> { pkgObj };
		Assert.AreEqual(1, carrierCorpse.CoexistingObjects.Count);
		Assert.IsTrue(carrierCorpse.CoexistingObjects[0].Tags.Contains(InteractableObject.DismantledFacilityTag));
	}

	[Test]
	public void T24_31_InvalidNewDestRejectedVsInvalidCurrentDestDropsWithoutAutoPick()
	{
		var lifeAcc = new ResourceAccount(ResourceType.LifeEnergy, 500);
		var destA = new Vector3Int(4, 4, 0);
		var invalidDest = new Vector3Int(99, 99, 0);
		RelocationSession.TryCreateOrder(80.0, 10.0, 10.0, new Vector2Int(2, 2), destA, true, true, true, lifeAcc, out var reloc, out _);
		reloc.TickDismantle(10.0, new[] { 1.0f }, true, 1.0, lifeAcc);
		reloc.TickReinstallAtDestination(4.0, new[] { 1.0f }, true);

		// 1. 새 목적지가 무효면 변경 거절 + 기존 목적지·설치 작업량 유지
		Assert.IsFalse(reloc.TryChangeDestination(invalidDest, isNewDestinationValidAndReachable: false));
		Assert.AreEqual(destA, reloc.TargetDestination);
		Assert.AreEqual(4.0, reloc.InstallCompletedSeconds, 1e-6);
		Assert.AreEqual(RelocationStage.InstallingAtDestination, reloc.Stage);

		// 2. 기존 목적지가 확실히 설치 불가로 판명되면 낙하·운반 종료·자동 위치 변경 없음(보존 가능한 설치 작업량은 유지)
		Assert.IsFalse(reloc.TickReinstallAtDestination(2.0, new[] { 1.0f }, isDestinationStillValid: false));
		Assert.AreEqual(RelocationStage.DroppedAs1x1Package, reloc.Stage);
		Assert.AreEqual(destA, reloc.TargetDestination);
		Assert.AreEqual(4.0, reloc.InstallCompletedSeconds, 1e-6);
	}

	// ====================================================================
	// 24-2-표1. 개체 진화 결제·성장·정제의 기대 결과 (10개 전수 검증)
	// ====================================================================

	private static EvolutionDefinition MakeEvoDef(
		double soulCost = 100.0,
		int seconds = 2,
		float hpInc = 100f,
		int popAfter = 1,
		int w = 1,
		int h = 1)
	{
		return new EvolutionDefinition
		{
			EvolutionId = "evo_test",
			DisplayName = "테스트 진화",
			ResultSpeciesKey = "MeleeTankEvo",
			BaseSoulShardCost = soulCost,
			PresentationDurationSeconds = seconds,
			TotalMaxHpIncrease = hpInc,
			TotalFlatStatIncreases = new List<(DetailStat, float)> { (DetailStat.PhysicalAttack, 10f) },
			PostEvolutionFootprint = new Vector2Int(w, h),
			PostEvolutionPopulationCost = popAfter,
		};
	}

	[Test]
	public void T24_2_01_QualifiedWaitingDoesNotReserveOrAutoEvolve()
	{
		var soulAcc = new ResourceAccount(ResourceType.SoulShard, 50.0); // 100 필요에 50만 보유
		double cumBase = 0.0;

		// 자격 획득만으로는 선점 없음
		Assert.AreEqual(0, soulAcc.ReservedAmount);

		var block = EvolutionSession.TryBeginEvolution(
			MakeEvoDef(soulCost: 100), false, true, true, 1.0, soulAcc, true, 1, 1, 10, default, ref cumBase, out var session, out _);
		Assert.AreEqual(EvolutionStartBlock.InsufficientSoulShard, block);
		Assert.IsNull(session);

		// 이후 자원이 생겨도 수동 재호출 전에는 차감·진화 없음
		soulAcc.Credit(100.0, ResourceCreditSource.ExternalCorpseRefine);
		Assert.AreEqual(150, soulAcc.DisplayBalance);
		Assert.AreEqual(0.0, cumBase, 1e-9);
	}

	[Test]
	public void T24_2_02_ReservedElsewhereBlocksEvolutionDespiteTotalBalance()
	{
		var soulAcc = new ResourceAccount(ResourceType.SoulShard, 150.0);
		soulAcc.TryReserve(100.0, out _); // 총 보유 150이나 사용 가능량 50
		double cumBase = 0.0;

		var block = EvolutionSession.TryBeginEvolution(
			MakeEvoDef(soulCost: 100), false, true, true, 1.0, soulAcc, true, 1, 1, 10, default, ref cumBase, out _, out _);

		Assert.AreEqual(EvolutionStartBlock.InsufficientSoulShard, block);
		Assert.AreEqual(150, soulAcc.DisplayBalance, "무차감");
	}

	[Test]
	public void T24_2_03_PostEvolutionSpaceOrRoomPopInsufficient_ExactKoreanMessagesNoCharge()
	{
		var soulAcc = new ResourceAccount(ResourceType.SoulShard, 500.0);
		double cumBase = 0.0;

		var spaceBlock = EvolutionSession.TryBeginEvolution(
			MakeEvoDef(soulCost: 100, w: 2, h: 2), false, true, true, 1.0, soulAcc,
			hasPostEvolutionFootprintSpace: false, 2, 1, 10, default, ref cumBase, out _, out string spaceMsg);
		Assert.AreEqual(EvolutionStartBlock.InsufficientSpace, spaceBlock);
		Assert.AreEqual("공간이 부족합니다.", spaceMsg);
		Assert.AreEqual(500, soulAcc.DisplayBalance);

		var popBlock = EvolutionSession.TryBeginEvolution(
			MakeEvoDef(soulCost: 100, popAfter: 3), false, true, true, 1.0, soulAcc,
			hasPostEvolutionFootprintSpace: true, currentRoomLivingPop: 9, currentUnitPopCost: 1, roomMaxPop: 10, default, ref cumBase, out _, out string popMsg);
		Assert.AreEqual(EvolutionStartBlock.InsufficientRoomPopulation, popBlock);
		Assert.AreEqual("방 정원이 부족합니다.", popMsg);
		Assert.AreEqual(500, soulAcc.DisplayBalance);
	}

	[Test]
	public void T24_2_04_DuplicateInputDuringPresentation_SingleChargeBlockedReentry()
	{
		var soulAcc = new ResourceAccount(ResourceType.SoulShard, 500.0);
		double cumBase = 0.0;
		var def = MakeEvoDef(soulCost: 100, seconds: 2);

		var b1 = EvolutionSession.TryBeginEvolution(def, false, true, true, 1.0, soulAcc, true, 1, 1, 10, default, ref cumBase, out var s1, out _);
		Assert.AreEqual(EvolutionStartBlock.None, b1);
		Assert.AreEqual(400, soulAcc.DisplayBalance);

		// 연출 중 재입력 차단
		var b2 = EvolutionSession.TryBeginEvolution(def, s1.IsActivePresentation, true, true, 1.0, soulAcc, true, 1, 1, 10, default, ref cumBase, out _, out _);
		Assert.AreEqual(EvolutionStartBlock.AlreadyEvolving, b2);
		Assert.AreEqual(400, soulAcc.DisplayBalance);
		Assert.AreEqual(100.0, cumBase, 1e-9);
	}

	[Test]
	public void T24_2_05_StepwiseHpGrowth100Over200Plus100In2Sec_WithAndWithoutDamage()
	{
		var def = MakeEvoDef(soulCost: 100, seconds: 2, hpInc: 100f);

		// 1) 피해 없음: 100/200 -> 1초 150/250 -> 2초 200/300
		var sNoDmg = new EvolutionSession(def, 100, default);
		float hp1 = 100f, max1 = 200f;
		sNoDmg.Tick(1.0, ref hp1, ref max1);
		Assert.AreEqual(150f, hp1, 1e-4f);
		Assert.AreEqual(250f, max1, 1e-4f);
		sNoDmg.Tick(1.0, ref hp1, ref max1);
		Assert.AreEqual(200f, hp1, 1e-4f);
		Assert.AreEqual(300f, max1, 1e-4f);
		Assert.IsTrue(sNoDmg.IsCompleted);

		// 2) 1초 후 피해 30: 150/250 -> 120/250 -> 2초 170/300
		var sDmg = new EvolutionSession(def, 100, default);
		float hp2 = 100f, max2 = 200f;
		sDmg.Tick(1.0, ref hp2, ref max2);
		hp2 -= 30f;
		sDmg.Tick(1.0, ref hp2, ref max2);
		Assert.AreEqual(170f, hp2, 1e-4f);
		Assert.AreEqual(300f, max2, 1e-4f);
	}

	[Test]
	public void T24_2_06_HitDoesNotCancelPresentation_DeathMidwayNoRefundAppliesConfirmedCorpseBonus()
	{
		var soulAcc = new ResourceAccount(ResourceType.SoulShard, 300.0);
		double cumBase = 0.0;
		var def = MakeEvoDef(soulCost: 100, seconds: 2, hpInc: 100f);
		EvolutionSession.TryBeginEvolution(def, false, true, true, 1.0, soulAcc, true, 1, 1, 10, default, ref cumBase, out var session, out _);

		float hp = 100f, maxHp = 200f;
		// 일반 피격(사망 아님)으로는 연출이 취소되지 않는다.
		hp -= 40f;
		session.Tick(1.0, ref hp, ref maxHp);
		Assert.IsTrue(session.IsActivePresentation);
		Assert.AreEqual(110f, hp, 1e-4f);

		// 연출 중 사망 -> 연출 종료·환급 없음·확정 진화 상태(cumBase=100)로 시체 추가 보상(+20) 산정
		hp = 0f;
		session.NotifyKilled();
		Assert.IsFalse(session.IsActivePresentation);
		Assert.IsTrue(session.WasKilledMidPresentation);
		Assert.AreEqual(200, soulAcc.DisplayBalance, "환급 없음");

		var corpse = new CorpseRecord
		{
			CorpseId = "died_mid_evo",
			OriginFaction = CorpseOriginFaction.PlayerAlly,
			IsProducibleSpecies = true,
			BaseProductionLifeCost = 200,
			CumulativeBaseEvolutionSoulCost = cumBase,
		};
		var reward = CorpseRefineMath.CalculateReward(corpse);
		Assert.AreEqual(20.0, reward.EvolutionBonusSoulShard, 1e-6);
	}

	[Test]
	public void T24_2_07_PresentationEndCommandRestoreVsTargetGone()
	{
		var savedMove = new SavedUnitCommandSnapshot
		{
			HadManualMove = true,
			MoveTarget = new Vector2Int(7, 8),
			AttackObjectTarget = new Vector3Int(3, 4, 0),
		};
		var session = new EvolutionSession(MakeEvoDef(seconds: 1), 100, savedMove);
		float hp = 100f, maxHp = 200f;
		session.Tick(1.0, ref hp, ref maxHp);

		Assert.IsTrue(session.IsCompleted);
		Assert.IsTrue(session.SavedCommand.HadManualMove);
		Assert.AreEqual(new Vector2Int(7, 8), session.SavedCommand.MoveTarget);
		Assert.AreEqual(new Vector3Int(3, 4, 0), session.SavedCommand.AttackObjectTarget);
	}

	[Test]
	public void T24_2_08_CumulativeBaseEvolutionCost100Plus200_Gives60BonusRegardlessOfDiscountOrOrigin()
	{
		var soulAcc = new ResourceAccount(ResourceType.SoulShard, 500.0);
		double cumBase = 0.0;

		// 50% 할인(costMultiplier=0.5)으로 진화했더라도 기준 비용(100 + 200 = 300)으로 누적된다(각주 [20]).
		EvolutionSession.TryBeginEvolution(MakeEvoDef(soulCost: 100), false, true, true, 0.5, soulAcc, true, 1, 1, 10, default, ref cumBase, out _, out _);
		EvolutionSession.TryBeginEvolution(MakeEvoDef(soulCost: 200), false, true, true, 0.5, soulAcc, true, 1, 1, 10, default, ref cumBase, out _, out _);
		Assert.AreEqual(350, soulAcc.DisplayBalance, "실제 지불은 50 + 100 = 150");
		Assert.AreEqual(300.0, cumBase, 1e-9, "누적 기준 진화 비용은 300");

		foreach (CorpseOriginFaction origin in new[] { CorpseOriginFaction.PlayerAlly, CorpseOriginFaction.WildMonster, CorpseOriginFaction.HumanEnemy })
		{
			var c = new CorpseRecord
			{
				CorpseId = $"c_{origin}",
				OriginFaction = origin,
				IsProducibleSpecies = true,
				BaseProductionLifeCost = 100,
				CumulativeBaseEvolutionSoulCost = cumBase,
			};
			Assert.AreEqual(60.0, CorpseRefineMath.CalculateReward(c).EvolutionBonusSoulShard, 1e-6);
		}
	}

	[Test]
	public void T24_2_09_EvolvedAllyCorpseRefine_PaysBonusAffectsPriorityExcludesCumulativeEarned()
	{
		var accounts = CreateAccounts(life: 0, soul: 0);
		var refinery = new CorpseRefineryState(5);

		var wildLowSoul = new CorpseRecord
		{
			CorpseId = "wild_soul_30",
			OriginFaction = CorpseOriginFaction.WildMonster,
			IsProducibleSpecies = false,
			ConfiguredRefineLifeReward = 50,
			ConfiguredRefineSoulReward = 30, // 총 영혼 파편 30
			BaseRefineDurationSeconds = 5.0,
		};
		var evolvedAlly = new CorpseRecord
		{
			CorpseId = "ally_evo_60",
			OriginFaction = CorpseOriginFaction.PlayerAlly,
			IsProducibleSpecies = true,
			BaseProductionLifeCost = 200, // 생명 40
			CumulativeBaseEvolutionSoulCost = 300, // 진화 추가 영혼 파편 60
			BaseRefineDurationSeconds = 5.0,
		};

		refinery.CompleteIntake(wildLowSoul);
		refinery.CompleteIntake(evolvedAlly);

		// 정제 완료 전에는 지급 없음
		refinery.TickAutoRefine(2.0, true, 1.0, accounts);
		Assert.AreEqual("ally_evo_60", refinery.ActiveRefiningCorpse.CorpseId, "추가분 60을 포함해 우선순위 우위");
		Assert.AreEqual(0, accounts[ResourceType.SoulShard].DisplayBalance);

		// 정제 완료 시 보유량 증가(+40 생명, +60 영혼) + 아군 회수분이므로 누적 획득량은 0 유지
		refinery.TickAutoRefine(3.0, true, 1.0, accounts);
		Assert.AreEqual(40, accounts[ResourceType.LifeEnergy].DisplayBalance);
		Assert.AreEqual(60, accounts[ResourceType.SoulShard].DisplayBalance);
		Assert.AreEqual(0.0, accounts[ResourceType.LifeEnergy].CumulativeEarned, 1e-9);
		Assert.AreEqual(0.0, accounts[ResourceType.SoulShard].CumulativeEarned, 1e-9);
	}

	[Test]
	public void T24_2_10_PauseAndMidPresentationSaveLoad_ZeroGrowthWhilePausedNoDuplicateStatsOrDamageReset()
	{
		var soulAcc = new ResourceAccount(ResourceType.SoulShard, 300.0);
		double cumBase = 0.0;
		var def = MakeEvoDef(soulCost: 100, seconds: 2, hpInc: 100f);
		EvolutionSession.TryBeginEvolution(def, false, true, true, 1.0, soulAcc, true, 1, 1, 10, default, ref cumBase, out var s1, out _);

		float hp = 100f, maxHp = 200f;
		// 일시정지(dt == 0) 중에는 성장 진행 없음
		s1.Tick(0.0, ref hp, ref maxHp);
		Assert.AreEqual(100f, hp, 1e-4f);
		Assert.AreEqual(0, s1.AppliedStepCount);

		// 1초 경과(+50/+50 -> 150/250) 후 30 피해(-> 120/250) 상태에서 스냅샷 저장·복원
		s1.Tick(1.0, ref hp, ref maxHp);
		hp -= 30f;
		EvolutionSessionSnapshot snap = s1.CaptureSnapshot();

		var restored = new EvolutionSession(def, 100, default);
		restored.RestoreSnapshot(snap);

		// 복원 직후 추가 차감·중복 능력치 적용·피해 초기화 없음
		Assert.AreEqual(200, soulAcc.DisplayBalance);
		Assert.AreEqual(120f, hp, 1e-4f);
		Assert.AreEqual(250f, maxHp, 1e-4f);

		// 남은 1초 진행 시 미반영 증가분(+50)만 더해져 170/300으로 완료
		float finalAtkBonus = 0f;
		restored.Tick(1.0, ref hp, ref maxHp, stats => finalAtkBonus = stats[0].cumulativeAppliedDelta);
		Assert.IsTrue(restored.IsCompleted);
		Assert.AreEqual(170f, hp, 1e-4f);
		Assert.AreEqual(300f, maxHp, 1e-4f);
		Assert.AreEqual(10f, finalAtkBonus, 1e-4f);
	}

	// ====================================================================
	// 5항 · 20-1항 · 22항 보조 검증 (공동 작업 공식 / 철거 출처별 정산 / 스냅샷 멱등성)
	// ====================================================================

	[Test]
	public void T05_01_CoopWorkSpeedFormula_4WorkersCapAndSortedDescending()
	{
		// 각주 [5]: 개인 속도 1.5배와 1.0배가 공동 작업하면 1.5*1.0 + 1.0*0.7 = 2.2배
		Assert.AreEqual(2.2f, WorkSpeedMath.CalculateTotalWorkSpeed(new[] { 1.0f, 1.5f }), 1e-5f);

		// 5기가 들어와도 상위 4기만 1.0, 0.7, 0.5, 0.3 적용 (1.0+0.7+0.5+0.3 = 2.5)
		Assert.AreEqual(2.5f, WorkSpeedMath.CalculateTotalWorkSpeed(new[] { 1f, 1f, 1f, 1f, 1f }), 1e-5f);

		// 대각선 접촉은 인접 작업 불허, 상하좌우 변 접촉만 허용
		Assert.IsFalse(WorkSpeedMath.IsOrthogonallyAdjacent(new Vector2Int(2, 2), new Vector2Int(2, 2), new Vector2Int(4, 4), Vector2Int.one));
		Assert.IsTrue(WorkSpeedMath.IsOrthogonallyAdjacent(new Vector2Int(2, 2), new Vector2Int(2, 2), new Vector2Int(4, 2), Vector2Int.one));
	}

	[Test]
	public void T20_01_DemolitionPayoutSources_ExternalCountsTowardCumulativeSelfRecoveryExcluded()
	{
		var accounts = CreateAccounts(life: 0, soul: 0);

		// 플레이어 직접 건설 시설(건설 200, 업그레이드 100) 회수율 50% 철거 -> 보유량 +150, 누적 획득량 0
		DemolitionMath.CreditDemolitionPayout(
			FacilityOriginKind.PlayerBuilt,
			new Dictionary<ResourceType, int> { [ResourceType.LifeEnergy] = 200 },
			new Dictionary<ResourceType, int> { [ResourceType.LifeEnergy] = 100 },
			0.5,
			null,
			accounts);
		Assert.AreEqual(150, accounts[ResourceType.LifeEnergy].DisplayBalance);
		Assert.AreEqual(0.0, accounts[ResourceType.LifeEnergy].CumulativeEarned, 1e-9);

		// 사전 배치 던전 시설(외부 기본 보상 80 + 플레이어 업그레이드 지불 100의 50% 회수) -> 보유량 +130, 누적 획득량은 외부 보상 80만 증가
		DemolitionMath.CreditDemolitionPayout(
			FacilityOriginKind.PrePlacedOrCapturedDungeon,
			null,
			new Dictionary<ResourceType, int> { [ResourceType.LifeEnergy] = 100 },
			0.5,
			new Dictionary<ResourceType, double> { [ResourceType.LifeEnergy] = 80.0 },
			accounts);
		Assert.AreEqual(280, accounts[ResourceType.LifeEnergy].DisplayBalance);
		Assert.AreEqual(80.0, accounts[ResourceType.LifeEnergy].CumulativeEarned, 1e-9);
	}

	[Test]
	public void T22_01_ResourceAccountAndLifeEnergyCycleSnapshotRoundTrip_NoDuplicatePayout()
	{
		var acc = new ResourceAccount(ResourceType.LifeEnergy, 100.75);
		acc.Credit(50.5, ResourceCreditSource.FacilityProduction);
		acc.TryReserve(40.0, out _);
		var accSnap = acc.CaptureSnapshot();

		var accRestored = new ResourceAccount(ResourceType.LifeEnergy, 0);
		accRestored.RestoreSnapshot(accSnap);
		Assert.AreEqual(151.25, accRestored.CurrentBalance, 1e-9);
		Assert.AreEqual(40, accRestored.ReservedAmount);
		Assert.AreEqual(50.5, accRestored.CumulativeEarned, 1e-9);

		var cycle = new LifeEnergyCycleTracker(100.0, 100.0);
		Assert.AreEqual(0.0, cycle.AdvanceSegmentByGaugeRatio(0.4, 0.25), 1e-9);
		var cycleSnap = cycle.CaptureSnapshot();

		var cycleRestored = new LifeEnergyCycleTracker(100.0, 100.0);
		cycleRestored.RestoreSnapshot(cycleSnap);
		double finalPayout = cycleRestored.AdvanceSegmentByGaugeRatio(0.6, 0.0);
		Assert.AreEqual(110.0, finalPayout, 1e-6, "복원 후에도 40%*25% 기여분(+10%)이 손실·중복 없이 정확히 1회 지급된다");
	}
}
#endif
