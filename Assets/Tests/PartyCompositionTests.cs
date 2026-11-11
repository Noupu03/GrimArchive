#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using GrimArchive.Wave;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 파티 구성 설계 명세서 v0.4 — 임무 수량(3-2·5-1장)·편성 계산(2·3장)·독립 유닛(2-1장). 순수 함수와 Party의 수량 집계라 세션 없이 EditMode에서 돈다.
// ========================================================================

public class PartyCompositionTests
{
	private const string JsonPath = "Assets/Data/party_composition.json";

	[SetUp]
	public void LoadShippedTable() => PartyCompositionTable.LoadFromJson(File.ReadAllText(JsonPath));

	[TearDown]
	public void ResetTable() => PartyCompositionTable.ResetForTests();

	private static Func<float> Seeded(int seed)
	{
		var r = new System.Random(seed);
		return () => (float)r.NextDouble();
	}

	private static Func<float> Constant(float v) => () => v;

	// ── 방 탐색 요구 수 B+E(3-2장) ───────────────────────────────────────

	[TestCase(5, 1, 1, 2)]   // B는 상한 1로 잘리고 E 1을 더한다 — 자리표시자 기본값이 옛 상수 2와 같다
	[TestCase(0, 1, 1, 1)]   // 미완료 방이 없으면 B=0, E만
	[TestCase(3, 10, 2, 5)]  // 상한이 넉넉하면 미완료 방 수 그대로
	public void RoomRequirement_IsBasePlusExtra(int unexplored, int cap, int extra, int expected)
		=> Assert.AreEqual(expected, MissionQuotaMath.RoomRequirement(unexplored, cap, extra));

	[Test]
	public void ForMission_ActivatesOnlyTheMissionsQuantityKinds()
	{
		var explore = MissionQuotaMath.ForMission(PartyType.Explore, 40, 2, 5, 1);
		Assert.AreEqual(40, explore.Tiles); Assert.AreEqual(2, explore.Rooms);
		Assert.AreEqual(0, explore.Kills); Assert.AreEqual(0, explore.Recovers);

		var mopUp = MissionQuotaMath.ForMission(PartyType.MopUp, 40, 2, 5, 1);
		Assert.AreEqual(5, mopUp.Kills); Assert.AreEqual(0, mopUp.Tiles);

		Assert.IsTrue(MissionQuotaMath.ForMission(PartyType.CoreDestroy, 0, 0, 0, 0).CoreDestroyed);
		Assert.IsTrue(MissionQuotaMath.ForMission(PartyType.Occupy, 0, 0, 0, 0).Occupied);
		Assert.IsTrue(MissionQuotaMath.ForMission(PartyType.Boss, 0, 0, 0, 0).BossDone);
	}

	[Test]
	public void IsAchieved_AllOfRequiresEveryActiveQuantity()
	{
		var targets = new QuotaTargets { Tiles = 10, Rooms = 2 };
		Assert.IsFalse(MissionQuotaMath.IsAchieved(targets, new QuotaProgress { Tiles = 10, Rooms = 1 }, false));
		Assert.IsTrue(MissionQuotaMath.IsAchieved(targets, new QuotaProgress { Tiles = 10, Rooms = 2 }, false));
		Assert.IsTrue(MissionQuotaMath.IsAchieved(targets, new QuotaProgress { Tiles = 10, Rooms = 1 }, true)); // 옛 동작(하나만 달성)
		Assert.IsFalse(MissionQuotaMath.IsAchieved(default, new QuotaProgress { Tiles = 99 }, false)); // 요구가 없으면 달성할 수 없다
	}

	// ── 수량 집계(3-2·5-1장) ───────────────────────────────────────────

	[Test]
	public void Quota_TileCount_DeduplicatesAndIgnoresLobby()
	{
		var q = PartyMissionQuota.Create(PartyType.Explore, new QuotaTargets { Tiles = 3 });
		Assert.IsTrue(q.CountTile(new Vector3Int(4, 5, 1)));
		Assert.IsFalse(q.CountTile(new Vector3Int(4, 5, 1)), "같은 타일은 한 번만 센다");
		Assert.IsFalse(q.CountTile(new Vector3Int(4, 5, 0)), "0층(던전 밖)은 이번 진입 이후 확인한 타일이 아니다");
		Assert.AreEqual(1, q.TilesRevealed);
	}

	[Test]
	public void Quota_AchievementIsReportedOnce_AndLaterGrowthDoesNotRetrigger()
	{
		var q = PartyMissionQuota.Create(PartyType.MopUp, new QuotaTargets { Kills = 5 });
		Assert.IsFalse(q.HasPendingAchievement(4f));
		Assert.IsTrue(q.HasPendingAchievement(5f));
		q.ConsumeAchievement();
		Assert.IsFalse(q.HasPendingAchievement(9f), "5-1장: 이후 처치 수가 늘어도 같은 집결을 새로 발령하지 않는다");
		Assert.IsTrue(q.IsAchieved(0f), "달성은 되돌아가지 않는다");
	}

	[Test]
	public void Quota_RecoverCountsDistinctObjects()
	{
		var q = PartyMissionQuota.Create(PartyType.Recover, new QuotaTargets { Recovers = 2 });
		q.OnRecovered("loot-a");
		q.OnRecovered("loot-a");
		Assert.IsFalse(q.IsAchieved(0f));
		q.OnRecovered("loot-b");
		Assert.IsTrue(q.IsAchieved(0f));
	}

	[Test]
	public void Quota_CoreDestroyAndOccupyUseCompletionEvents()
	{
		var core = PartyMissionQuota.Create(PartyType.CoreDestroy, new QuotaTargets { CoreDestroyed = true });
		Assert.IsFalse(core.IsAchieved(0f));
		core.OnCoreDestroyed();
		Assert.IsTrue(core.IsAchieved(0f));

		var occupy = PartyMissionQuota.Create(PartyType.Occupy, new QuotaTargets { Occupied = true });
		Assert.IsFalse(occupy.IsAchieved(0f));
		occupy.OnOccupied();
		Assert.IsTrue(occupy.IsAchieved(0f));
	}

	[Test]
	public void Party_LegacyQuota_KeepsOldRoomTwoBehavior()
	{
		var party = new Party("p", "legacy");
		party.OnRoomExploreCompleted(1);
		party.OnRoomExploreCompleted(1); // 같은 방은 한 번만
		Assert.AreEqual(1, party.CompletedRoomExploreCount);
		Assert.IsFalse(party.IsMissionGoalAchieved);
		party.OnRoomExploreCompleted(2);
		Assert.IsTrue(party.IsMissionGoalAchieved);
		Assert.IsTrue(party.Quota.HasPendingAchievement(0f));
		party.Quota.ConsumeAchievement();
		Assert.IsFalse(party.Quota.HasPendingAchievement(0f));
		Assert.IsTrue(party.IsMissionGoalAchieved, "소비해도 달성 사실은 남는다(후퇴 뒤 귀환/재탐험 분기가 읽는다)");
	}

	[Test]
	public void Party_PlannedQuota_RoomTargetIsFixedAtIssue()
	{
		var party = new Party("p", "planned");
		party.SetQuota(PartyMissionQuota.Create(PartyType.Explore, new QuotaTargets { Rooms = 3 }));
		party.OnRoomExploreCompleted(1);
		party.OnRoomExploreCompleted(2);
		Assert.IsFalse(party.IsMissionGoalAchieved, "요구 수 3은 진행 중 바뀌지 않는다");
		party.OnRoomExploreCompleted(3);
		Assert.IsTrue(party.IsMissionGoalAchieved);
	}

	// ── 단계·임무 후보·가중치(3-1장) ───────────────────────────────────

	[Test]
	public void ResolveStage_PicksHighestSatisfiedStage()
	{
		var stages = PartyCompositionTable.Stages;
		Assert.AreEqual(1, PartyCompositionMath.ResolveStage(stages, 1, 0f));
		Assert.AreEqual(2, PartyCompositionMath.ResolveStage(stages, 4, 0f));
		Assert.AreEqual(3, PartyCompositionMath.ResolveStage(stages, 20, 0f));
	}

	[Test]
	public void MissionEligibility_RespectsUnlockConditions()
	{
		MissionRule Rule(PartyType t) { foreach (var m in PartyCompositionTable.Missions) if (m.Mission == t) return m; return null; }
		var ctx = new PlannerContext { NextWaveNumber = 5, CoreActivated = false, LootExists = true, EnemiesExist = true };

		Assert.IsFalse(PartyCompositionMath.IsMissionEligible(Rule(PartyType.CoreDestroy), ctx), "코어 활성 전에는 코어 파괴 임무가 후보가 아니다");
		Assert.IsFalse(PartyCompositionMath.IsMissionEligible(Rule(PartyType.Occupy), ctx));
		ctx.CoreActivated = true;
		Assert.IsTrue(PartyCompositionMath.IsMissionEligible(Rule(PartyType.CoreDestroy), ctx));

		ctx.LootExists = false;
		Assert.IsFalse(PartyCompositionMath.IsMissionEligible(Rule(PartyType.Recover), ctx), "회수할 대상이 없으면 회수 임무는 후보가 아니다");
		Assert.IsFalse(PartyCompositionMath.IsMissionEligible(Rule(PartyType.Boss), ctx), "보스 임무는 후속 콘텐츠라 비활성");

		ctx.NextWaveNumber = 1;
		ctx.CoreActivated = true;
		Assert.IsFalse(PartyCompositionMath.IsMissionEligible(Rule(PartyType.CoreDestroy), ctx), "minWave 이전에는 후보가 아니다");
	}

	[Test]
	public void PickWeighted_FollowsWeights_AndHandlesEmpty()
	{
		var items = new List<string> { "a", "b" };
		Assert.AreEqual("a", PartyCompositionMath.PickWeighted(items, s => s == "a" ? 3f : 1f, Constant(0.0f)));
		Assert.AreEqual("a", PartyCompositionMath.PickWeighted(items, s => s == "a" ? 3f : 1f, Constant(0.74f)));
		Assert.AreEqual("b", PartyCompositionMath.PickWeighted(items, s => s == "a" ? 3f : 1f, Constant(0.76f)));
		Assert.IsNull(PartyCompositionMath.PickWeighted(new List<string>(), s => 1f, Constant(0.5f)));
	}

	// ── 규모·슬롯(2-2·2-3장) ───────────────────────────────────────────

	private static ConceptRule Concept(PartyConcept c)
	{
		foreach (var r in PartyCompositionTable.Concepts) if (r.Concept == c) return r;
		return null;
	}

	[Test]
	public void Size_AdventurerIsTwoToFour_RogueIsFourToSix_AtStageOne()
	{
		var adv = Concept(PartyConcept.Adventurer);
		Assert.AreEqual(2, PartyCompositionMath.RollSize(adv, 1, Constant(0f)));
		Assert.AreEqual(4, PartyCompositionMath.RollSize(adv, 1, Constant(0.999f)));
		var rogue = Concept(PartyConcept.Rogue);
		Assert.AreEqual(4, PartyCompositionMath.RollSize(rogue, 1, Constant(0f)));
		Assert.AreEqual(6, PartyCompositionMath.RollSize(rogue, 1, Constant(0.999f)));
	}

	[TestCase(PartyConcept.Adventurer)]
	[TestCase(PartyConcept.Rogue)]
	public void BuildMembers_HonorsSizeMinAndMax(PartyConcept concept)
	{
		var rule = Concept(concept);
		for (int seed = 0; seed < 40; seed++)
		{
			var rand = Seeded(seed);
			int size = PartyCompositionMath.RollSize(rule, 1, rand);
			var members = PartyCompositionMath.BuildMembers(rule, size, rand);
			Assert.AreEqual(size, PartyCompositionMath.Total(members), $"seed {seed}: 슬롯 상한 합이 규모 이상이라 규모가 그대로여야 한다");
			foreach (var m in members)
			{
				var slot = rule.Slots.Find(s => s.UnitType == m.UnitType);
				Assert.GreaterOrEqual(m.Count, slot.Min);
				Assert.LessOrEqual(m.Count, slot.Max);
			}
		}
	}

	[Test]
	public void BuildMembers_ShrinksWhenSlotCapsCannotReachSize()
	{
		var rule = new ConceptRule { Concept = PartyConcept.Adventurer, Enabled = true };
		rule.Slots.Add(new CompositionSlot { UnitType = "전사", Min = 0, Max = 1, Weight = 1 });
		var members = PartyCompositionMath.BuildMembers(rule, 4, Constant(0f));
		Assert.AreEqual(1, PartyCompositionMath.Total(members));
	}

	// ── 이전 웨이브 생존자 합류(17-3장 부족분 충원 정신) ─────────────────

	[Test]
	public void ApplySurvivors_SameTypeFillsItsSlotFirst()
	{
		var plan = new List<UnitCount> { new UnitCount("전사", 2), new UnitCount("마법사", 1) };
		var spawn = PartyCompositionMath.ApplySurvivors(plan, new[] { "전사" }, out int surplus);
		Assert.AreEqual(0, surplus);
		Assert.AreEqual(2, PartyCompositionMath.Total(spawn));
		Assert.AreEqual(1, spawn.Find(u => u.UnitType == "전사").Count);
	}

	[Test]
	public void ApplySurvivors_UnplannedTypeStillConsumesBudget_SoPartyDoesNotGrow()
	{
		var plan = new List<UnitCount> { new UnitCount("전사", 2), new UnitCount("마법사", 1) };
		var spawn = PartyCompositionMath.ApplySurvivors(plan, new[] { "도적" }, out int surplus);
		Assert.AreEqual(0, surplus);
		Assert.AreEqual(2, PartyCompositionMath.Total(spawn), "계획 규모 3 − 생존자 1 = 신규 2");
	}

	[Test]
	public void ApplySurvivors_SurplusSurvivorsAreKept_NotDiscarded()
	{
		var plan = new List<UnitCount> { new UnitCount("전사", 1) };
		var spawn = PartyCompositionMath.ApplySurvivors(plan, new[] { "전사", "도적", "사제" }, out int surplus);
		Assert.AreEqual(0, PartyCompositionMath.Total(spawn));
		Assert.AreEqual(2, surplus);
	}

	// ── 편성 계획(PartyPlanner) ───────────────────────────────────────

	private static PlannerContext Ctx(int wave, bool baseExists = false, bool coreActive = false)
		=> new PlannerContext { NextWaveNumber = wave, HumanBaseExists = baseExists, CoreActivated = coreActive, UnexploredNormalRooms = 4, LootExists = true, EnemiesExist = true };

	[Test]
	public void Plan_NoHumanBase_EarlyWave_IsIndependentVagrants_WithoutMissionQuota()
	{
		var plan = PartyPlanner.BuildPlan(Ctx(1), null, Constant(0f));
		Assert.NotNull(plan);
		var p = plan.Primary;
		Assert.IsTrue(p.IsIndependent);
		Assert.AreEqual(PartyConcept.Vagrant, p.Concept);
		Assert.IsFalse(MissionQuotaMath.HasAnyTarget(p.Quota), "독립 유닛은 임무 수량이 없다");
		Assert.GreaterOrEqual(p.TargetSize, 2);
		Assert.LessOrEqual(p.TargetSize, 3);
	}

	[Test]
	public void Plan_HumanBaseExists_NeverIndependent()
	{
		for (int seed = 0; seed < 30; seed++)
		{
			var plan = PartyPlanner.BuildPlan(Ctx(1, baseExists: true), null, Seeded(seed));
			Assert.IsFalse(plan.Primary.IsIndependent, $"seed {seed}: 인류 거점이 있으면 독립 유닛이 등장하지 않는다(2-1장)");
		}
	}

	[Test]
	public void Plan_LateWave_NeverIndependent_EvenWithoutBase()
	{
		var plan = PartyPlanner.BuildPlan(Ctx(10), null, Seeded(1));
		Assert.IsFalse(plan.Primary.IsIndependent);
	}

	[Test]
	public void Plan_Mission_RespectsEligibility_AcrossManySeeds()
	{
		for (int seed = 0; seed < 60; seed++)
		{
			var plan = PartyPlanner.BuildPlan(Ctx(6, baseExists: true, coreActive: false), null, Seeded(seed));
			var t = plan.Primary.Mission;
			Assert.AreNotEqual(PartyType.CoreDestroy, t, $"seed {seed}");
			Assert.AreNotEqual(PartyType.Occupy, t, $"seed {seed}");
			Assert.AreNotEqual(PartyType.Boss, t, $"seed {seed}");
		}
	}

	[Test]
	public void Plan_ForcedMission_OverridesCandidatesAndIndependent()
	{
		var plan = PartyPlanner.BuildPlan(Ctx(1), PartyType.CoreDestroy, Seeded(3));
		Assert.AreEqual(PartyType.CoreDestroy, plan.Primary.Mission);
		Assert.IsFalse(plan.Primary.IsIndependent);
		Assert.IsTrue(plan.Primary.Quota.CoreDestroyed);
	}

	[Test]
	public void Plan_ExploreQuota_FixesRoomRequirementFromContext()
	{
		// Explore만 후보가 되도록 지정 — 방 요구 수는 B(상한 1)+E(1)=2, 타일은 표의 값
		var plan = PartyPlanner.BuildPlan(Ctx(3, baseExists: true), PartyType.Explore, Seeded(9));
		Assert.AreEqual(2, plan.Primary.Quota.Rooms);
		Assert.AreEqual(40, plan.Primary.Quota.Tiles);

		var ctx = Ctx(3, baseExists: true);
		ctx.UnexploredNormalRooms = 0;
		var none = PartyPlanner.BuildPlan(ctx, PartyType.Explore, Seeded(9));
		Assert.AreEqual(1, none.Primary.Quota.Rooms, "미완료 방이 없으면 B=0, E만");
	}

	[Test]
	public void Plan_MembersAreHumanUnitTypesFromTable_AndSizeMatches()
	{
		var plan = PartyPlanner.BuildPlan(Ctx(3, baseExists: true), PartyType.MopUp, Seeded(5));
		var p = plan.Primary;
		Assert.AreEqual(p.TargetSize, p.MemberTotal);
		var allowed = new HashSet<string>();
		foreach (var c in PartyCompositionTable.Concepts) foreach (var s in c.Slots) allowed.Add(s.UnitType);
		foreach (var m in p.Members) Assert.IsTrue(allowed.Contains(m.UnitType), m.UnitType);
		Assert.AreEqual(5, p.Quota.Kills);
	}

	[Test]
	public void Plan_ReinforcementShortfallIsCarriedForDisplay()
	{
		var ctx = Ctx(3, baseExists: true);
		ctx.GarrisonShortfall = 3;
		Assert.AreEqual(3, PartyPlanner.BuildPlan(ctx, PartyType.Explore, Seeded(2)).Primary.ReinforcementCount);
	}

	[Test]
	public void Plan_EmptyTable_ReturnsNull_SoCallerFallsBackToManualWave()
	{
		PartyCompositionTable.LoadFromJson("{}");
		Assert.IsNull(PartyPlanner.BuildPlan(Ctx(1), null, Constant(0f)));
	}

	// ── 표 로드 ────────────────────────────────────────────────────────

	[Test]
	public void Table_LoadsShippedJson_WithDisabledFutureConcepts()
	{
		Assert.IsTrue(Concept(PartyConcept.Vagrant).Independent);
		Assert.IsTrue(Concept(PartyConcept.Vagrant).OnlyWithoutHumanBase);
		Assert.IsFalse(Concept(PartyConcept.Army).Enabled, "군대·용병·용사는 콘텐츠 확정 전까지 꺼져 있다");
		foreach (var m in PartyCompositionTable.Missions)
			if (m.Mission == PartyType.Boss) Assert.IsFalse(m.Enabled, "보스 임무는 후속 콘텐츠라 선택 불가");
	}

	[Test]
	public void Table_UnknownEnumNamesAreSkipped()
	{
		PartyCompositionTable.LoadFromJson("{\"concepts\":[{\"concept\":\"Nope\",\"enabled\":true},{\"concept\":\"Rogue\",\"enabled\":true,\"slots\":[{\"unitType\":\"도적\",\"min\":1,\"max\":1,\"weight\":1}]}],\"missions\":[{\"mission\":\"Bogus\",\"enabled\":true}]}");
		Assert.AreEqual(1, PartyCompositionTable.Concepts.Count);
		Assert.AreEqual(0, PartyCompositionTable.Missions.Count);
	}
}
#endif
