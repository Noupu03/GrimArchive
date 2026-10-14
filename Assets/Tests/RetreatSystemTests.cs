#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 후퇴·도주 시스템 상태 테스트(NUnit, #if UNITY_INCLUDE_TESTS) — 정신력 변경 단일 진입점(MentalSystem.ApplyChange)·개인 도주 확정·도주 확인 감소·조우 충격 적용·파티/개인 후퇴 시작 기준을 Human/Party 인스턴스만으로 확인한다(세션 불필요). 순수 계산은 RetreatMathTests.
// 정적 상태(EnabledOverride·Random01·Lv→대응 단계 표)는 매 테스트 뒤에 되돌린다.
// ========================================================================

public class RetreatSystemTests
{
	[TearDown]
	public void ResetStatics()
	{
		RetreatSystem.EnabledOverride = null;
		RetreatSystem.Random01 = () => UnityEngine.Random.value;
		EncounterShockMath.LevelToResponseStage = null;
	}

	static Human NewHuman(string name, float mental = -20f, float hp = 100f, float maxHp = 100f)
	{
		var h = ScriptableObject.CreateInstance<Human>();
		h.name = name;
		h.currentFloor = 1; // 후퇴·도주는 던전 안(1층 이상)에서만 일어난다 — 0층은 던전 밖
		h.maxHp = maxHp;
		h.hp = hp;
		h.BaseStat.maxMental = 100f;
		h.BaseStat.mental = mental;
		return h;
	}

	static Party NewParty(PartyType type, params Human[] members)
	{
		var p = new Party("p", "테스트 파티") { Type = type };
		foreach (var m in members) { p.Members.Add(m); m.party = p; }
		p.Leader = members[0];
		return p;
	}

	// ── 꺼져 있으면 범위 제한만 적용 ──

	[Test]
	public void Disabled_OnlyClampsAndNeverRolls()
	{
		RetreatSystem.EnabledOverride = false;
		var h = NewHuman("A", mental: 30f);
		MentalSystem.ApplyChange(h, -5f);
		Assert.AreEqual(25f, h.BaseStat.mental, 0.0001f, "꺼져 있으면 안정 단계 감소 보정(×0.95)도 적용하지 않는다");

		h.BaseStat.mental = -95f;
		MentalSystem.ApplyChange(h, -50f);
		Assert.AreEqual(-100f, h.BaseStat.mental, 0.0001f);
		Assert.IsFalse(h.Retreat.IsFleeing, "꺼져 있으면 −100이어도 도주를 확정하지 않는다");
	}

	[Test]
	public void MonstersAreIgnored()
	{
		RetreatSystem.EnabledOverride = true;
		var m = ScriptableObject.CreateInstance<Monster>();
		float applied = MentalSystem.ApplyChange(m, -10f);
		Assert.AreEqual(0f, applied);
		Assert.AreEqual(0f, m.BaseStat.mental);
	}

	// ── 정신력 감소 보정 (R62·R64) ──

	[Test]
	public void R64_StableStageDecreaseIsScaled()
	{
		RetreatSystem.EnabledOverride = true;
		var h = NewHuman("A", mental: 30f);
		MentalSystem.ApplyChange(h, -5f);
		Assert.AreEqual(25.25f, h.BaseStat.mental, 0.0001f, "정신력 30 안정 단계 5×0.95 = 4.75 감소");
	}

	// ── 개인 도주 판정 (R08~R12) ──

	[Test]
	public void R08_PanicZoneDecreaseRollsOnce_HeldKeepsMentalAndRaisesProbability()
	{
		RetreatSystem.EnabledOverride = true;
		RetreatSystem.Random01 = () => 0.9999f; // 도주 안 함
		var h = NewHuman("A", mental: -70f);
		MentalSystem.ApplyChange(h, -4f); // −74
		MentalSystem.ApplyChange(h, -2f); // −76
		Assert.AreEqual(-76f, h.BaseStat.mental, 0.0001f, "버텨도 감소한 정신력은 그대로");
		Assert.IsFalse(h.Retreat.IsFleeing);
		Assert.AreEqual(1f, h.Retreat.FleeRoll.PendingDecrease, 0.0001f);
		Assert.AreEqual(0.6f, h.Retreat.FleeRoll.NextProbability, 0.0001f);
	}

	[Test]
	public void FirstRollCanConfirmFlee()
	{
		RetreatSystem.EnabledOverride = true;
		RetreatSystem.Random01 = () => 0.1f; // 첫 판정 50%에서 선택됨
		var h = NewHuman("A", mental: -68f);
		MentalSystem.ApplyChange(h, -8f); // −76: 공황 구간 6
		Assert.IsTrue(h.Retreat.IsFleeing);
		Assert.AreEqual(RetreatMode.PersonalFlee, h.Retreat.Mode);
	}

	[Test]
	public void DecreaseOutsidePanicZone_NeverRolls()
	{
		RetreatSystem.EnabledOverride = true;
		RetreatSystem.Random01 = () => 0f; // 판정이 나오면 무조건 도주
		var h = NewHuman("A", mental: -20f);
		MentalSystem.ApplyChange(h, -40f); // −60: 공황(−70) 위
		Assert.IsFalse(h.Retreat.IsFleeing);
		Assert.AreEqual(0f, h.Retreat.FleeRoll.PendingDecrease, 0.0001f);
	}

	[Test]
	public void R11_ReachingMinusHundredConfirmsFleeWithoutRoll()
	{
		RetreatSystem.EnabledOverride = true;
		RetreatSystem.Random01 = () => 0.9999f;
		var h = NewHuman("A", mental: -97f);
		MentalSystem.ApplyChange(h, -3f); // 잔여 감소량이 5 미만이어도 −100이면 확정
		Assert.IsTrue(h.Retreat.IsFleeing);
		Assert.AreEqual(0, h.Retreat.FleeRoll.TotalRolls, "확률 판정 없이 확정");
	}

	[Test]
	public void R10_RecoveryResetsPendingRoll_R12_ConfirmedFleeStays()
	{
		RetreatSystem.EnabledOverride = true;
		RetreatSystem.Random01 = () => 0.9999f;
		var h = NewHuman("A", mental: -70f);
		MentalSystem.ApplyChange(h, -4f);
		MentalSystem.ApplyChange(h, +10f); // 회복
		Assert.AreEqual(0f, h.Retreat.FleeRoll.PendingDecrease, 0.0001f);
		Assert.AreEqual(0.5f, h.Retreat.FleeRoll.NextProbability, 0.0001f);

		RetreatSystem.Random01 = () => 0f;
		MentalSystem.ApplyChange(h, -50f); // 공황 구간에서 판정이 나와 도주 확정
		Assert.IsTrue(h.Retreat.IsFleeing);
		MentalSystem.ApplyChange(h, +80f); // 확정 후 회복
		Assert.IsTrue(h.Retreat.IsFleeing, "R12: 도주 중 정신력 회복해도 도주 유지");
	}

	[Test]
	public void ForcedSet_SkipsDecreaseModifier()
	{
		RetreatSystem.EnabledOverride = true;
		RetreatSystem.Random01 = () => 0.9999f;
		var h = NewHuman("A", mental: -60f);
		MentalSystem.SetForced(h, -71f);
		Assert.AreEqual(-71f, h.BaseStat.mental, 0.0001f);
		Assert.AreEqual(1f, h.Retreat.FleeRoll.PendingDecrease, 0.0001f, "설정 결과의 공황 구간 하락 1만 집계");
		Assert.IsFalse(h.Retreat.IsFleeing, "−71 진입 자체는 도주 확정이 아니다");
	}

	// ── 파티원 도주 확인 감소 (R62~R67) ──

	[Test]
	public void R62_R63_R66_FirstWitnessOnly()
	{
		RetreatSystem.EnabledOverride = true;
		var witness = NewHuman("W", mental: 0f);
		var fleeing = NewHuman("F");
		Assert.IsTrue(MentalSystem.OnFleeWitnessed(witness, fleeing));
		Assert.AreEqual(-5f, witness.BaseStat.mental, 0.0001f, "평범 단계: 기본 5 감소");
		Assert.IsFalse(MentalSystem.OnFleeWitnessed(witness, fleeing), "R63/R66: 같은 사건의 재확인·재보고는 추가 감소 없음");
		witness.BaseStat.mental = -5f;
		Assert.IsFalse(MentalSystem.OnFleeWitnessed(witness, fleeing), "정신력 회복·방 이동으로 같은 사건의 최초 확인을 다시 만들지 않음");
		Assert.AreEqual(-5f, witness.BaseStat.mental, 0.0001f);
	}

	[Test]
	public void R64_StableWitnessLosesFourPointSevenFive()
	{
		RetreatSystem.EnabledOverride = true;
		var witness = NewHuman("W", mental: 30f);
		MentalSystem.OnFleeWitnessed(witness, NewHuman("F"));
		Assert.AreEqual(25.25f, witness.BaseStat.mental, 0.0001f);
	}

	[Test]
	public void R65_R67_EachWitnessAndEachFleeCountSeparately()
	{
		RetreatSystem.EnabledOverride = true;
		var f1 = NewHuman("F1");
		var f2 = NewHuman("F2");
		var w1 = NewHuman("W1", mental: 0f);
		var w2 = NewHuman("W2", mental: 0f);
		MentalSystem.OnFleeWitnessed(w1, f1);
		MentalSystem.OnFleeWitnessed(w2, f1);
		Assert.AreEqual(-5f, w1.BaseStat.mental, 0.0001f, "R65: 서로 다른 인류 유닛은 각자 1회");
		Assert.AreEqual(-5f, w2.BaseStat.mental, 0.0001f);
		MentalSystem.OnFleeWitnessed(w1, f2);
		Assert.AreEqual(-10f, w1.BaseStat.mental, 0.0001f, "R67: 서로 다른 파티원의 도주는 각각 5 감소");
	}

	// ── 몬스터 직접 조우 충격 적용 ──

	[Test]
	public void Encounter_WithoutResponseTable_DoesNothing()
	{
		RetreatSystem.EnabledOverride = true;
		var h = NewHuman("A", mental: -20f);
		var shock = FleeSystem.EvaluateEncounter(h, new[] { new KeyValuePair<string, float>("golem#1", 950f) });
		Assert.AreEqual(EncounterShockKind.None, shock.Kind);
		Assert.AreEqual(-20f, h.BaseStat.mental, 0.0001f);
	}

	[Test]
	public void Encounter_PlusThree_StunsAndSetsMinusSeventyOne()
	{
		RetreatSystem.EnabledOverride = true;
		RetreatSystem.Random01 = () => 0.9999f;
		EncounterShockMath.LevelToResponseStage = level => 2; // 위험도 MAX(5) − 2 = +3
		var h = NewHuman("A", mental: -60f);
		var shock = FleeSystem.EvaluateEncounter(h, new[] { new KeyValuePair<string, float>("golem#1", 950f) });
		Assert.AreEqual(EncounterShockKind.StunAndPanic, shock.Kind);
		Assert.AreEqual(-71f, h.BaseStat.mental, 0.0001f);
		Assert.GreaterOrEqual(h.StatusEffects.State.stunDuration, 1f);
		Assert.IsFalse(h.Retreat.IsFleeing, "신규 공황 진입은 즉시 도주가 아니다");
	}

	[Test]
	public void R25_Encounter_PlusThreeDuringPanic_ConfirmsFlee()
	{
		RetreatSystem.EnabledOverride = true;
		EncounterShockMath.LevelToResponseStage = level => 2;
		var h = NewHuman("A", mental: -80f);
		FleeSystem.EvaluateEncounter(h, new[] { new KeyValuePair<string, float>("golem#1", 950f) });
		Assert.AreEqual(-100f, h.BaseStat.mental, 0.0001f);
		Assert.IsTrue(h.Retreat.IsFleeing);
		Assert.GreaterOrEqual(h.StatusEffects.State.stunDuration, 1f, "이동은 기절 종료 뒤 시작한다 — 이동 단계가 stunDuration을 확인");
	}

	[Test]
	public void R23_Encounter_PlusTwo_AppliesStableScaledDamage()
	{
		RetreatSystem.EnabledOverride = true;
		EncounterShockMath.LevelToResponseStage = level => 0; // 위험도 300~499(순번 2) − 0 = +2
		var h = NewHuman("A", mental: 30f); // 안정 단계
		FleeSystem.EvaluateEncounter(h, new[] { new KeyValuePair<string, float>("ogre#1", 350f) });
		Assert.AreEqual(30f - 20f * 0.95f, h.BaseStat.mental, 0.0001f, "기본 20에 안정 ×0.95 적용");
		Assert.AreEqual(0f, h.StatusEffects.State.stunDuration, "별도 기절 판정 없음");
	}

	// ── 파티 후퇴 시작 (1-1·1-2장, R16·R56·R57) ──

	[Test]
	public void PartyRetreat_StartsOnceAtAverageThreshold()
	{
		RetreatSystem.EnabledOverride = true;
		var leader = NewHuman("L", hp: 10f);
		var member = NewHuman("M", hp: 30f);
		var party = NewParty(PartyType.Explore, leader, member);

		Assert.IsTrue(PartyRetreatSystem.EvaluateStart(party, 1f), "평균 HP 20% → 후퇴");
		Assert.AreEqual(RetreatMode.PartyRetreat, party.Retreat.Mode);
		Assert.AreEqual(RetreatDestination.Exit, party.Retreat.Destination, "세션이 없으면 후보 거점이 없어 출구");
		Assert.IsFalse(PartyRetreatSystem.EvaluateStart(party, 2f), "이미 시작한 후퇴는 재발령·초기화하지 않는다");
		Assert.AreEqual(1f, party.Retreat.StartedAt);
	}

	[Test]
	public void PartyRetreat_NotStartedAboveThresholds()
	{
		RetreatSystem.EnabledOverride = true;
		var party = NewParty(PartyType.Explore, NewHuman("L", hp: 21f), NewHuman("M", hp: 21f));
		Assert.IsFalse(PartyRetreatSystem.EvaluateStart(party, 1f));
		Assert.AreEqual(RetreatMode.None, party.Retreat.Mode);
	}

	[Test]
	public void PartyRetreat_OccupyUsesHigherThresholds()
	{
		RetreatSystem.EnabledOverride = true;
		var party = NewParty(PartyType.Occupy, NewHuman("L", hp: 40f), NewHuman("M", hp: 40f));
		Assert.IsTrue(PartyRetreatSystem.EvaluateStart(party, 1f), "점령 파티는 평균 HP 40%에서 후퇴");
	}

	[Test]
	public void R56_FleeConfirmedMemberIsExcludedFromAverageImmediately()
	{
		RetreatSystem.EnabledOverride = true;
		var leader = NewHuman("L", hp: 100f);
		var fleeing = NewHuman("F", hp: 5f);
		var party = NewParty(PartyType.Explore, leader, fleeing);
		fleeing.Retreat.Mode = RetreatMode.PersonalFlee; // 확정 즉시 평균에서 제외(실제 이탈 전)

		var avg = party.ComputeRetreatAverages();
		Assert.AreEqual(1, avg.Count);
		Assert.AreEqual(100f, avg.HpPercent, 0.001f);
		Assert.IsFalse(PartyRetreatSystem.EvaluateStart(party, 1f));
	}

	[Test]
	public void DeadMembersAreExcluded()
	{
		var leader = NewHuman("L", hp: 100f);
		var dead = NewHuman("D", hp: 0f);
		var party = NewParty(PartyType.Explore, leader, dead);
		Assert.AreEqual(1, party.ComputeRetreatAverages().Count);
	}

	[Test]
	public void R57_StartedPartyRetreatSurvivesLeaderLoss()
	{
		RetreatSystem.EnabledOverride = true;
		var leader = NewHuman("L", hp: 10f);
		var member = NewHuman("M", hp: 100f);
		var party = NewParty(PartyType.Explore, leader, member);
		party.Retreat.Mode = RetreatMode.PartyRetreat;
		leader.hp = 0f;
		party.Leader = null; // 새 리더 없음

		Assert.IsFalse(RetreatSystem.EvaluatePersonalRetreat(member, 3f), "이미 시작한 파티 후퇴가 있으면 새 개인 후퇴를 시작하지 않는다");
		Assert.AreEqual(RetreatMode.PartyRetreat, RetreatSystem.GetEffectiveMode(member));
		Assert.AreEqual(RetreatMode.PartyRetreat, party.Retreat.Mode, "리더 사망으로 취소하지 않는다");
	}

	// ── 리더 없는 인류의 개인 후퇴 (1-4장, R49·R50·R52·R58) ──

	[Test]
	public void R49_PersonalRetreatByHpOnly_R50_NotByMental()
	{
		RetreatSystem.EnabledOverride = true;
		var lowHp = NewHuman("A", mental: -40f, hp: 20f);
		Assert.IsTrue(RetreatSystem.EvaluatePersonalRetreat(lowHp, 1f));
		Assert.AreEqual(RetreatMode.PersonalRetreat, lowHp.Retreat.Mode);

		var lowMental = NewHuman("B", mental: -70f, hp: 30f); // 정신력 15%
		Assert.IsFalse(RetreatSystem.EvaluatePersonalRetreat(lowMental, 1f), "정신력 비율만으로 개인 후퇴하지 않는다");
		Assert.AreEqual(RetreatMode.None, lowMental.Retreat.Mode);
	}

	[Test]
	public void R58_PersonalRetreatIsNotCancelledByHpRecovery()
	{
		RetreatSystem.EnabledOverride = true;
		var h = NewHuman("A", hp: 20f);
		RetreatSystem.EvaluatePersonalRetreat(h, 1f);
		h.hp = 25f;
		RetreatSystem.EvaluatePersonalRetreat(h, 2f);
		Assert.AreEqual(RetreatMode.PersonalRetreat, h.Retreat.Mode);
	}

	[Test]
	public void R52_ConfirmedFleeIsNotOverwrittenByPersonalRetreat()
	{
		RetreatSystem.EnabledOverride = true;
		var h = NewHuman("A", hp: 50f);
		h.Retreat.Mode = RetreatMode.PersonalFlee;
		Assert.IsFalse(RetreatSystem.EvaluatePersonalRetreat(h, 1f));
		Assert.AreEqual(RetreatMode.PersonalFlee, RetreatSystem.GetEffectiveMode(h), "회복 후에도 도주 유지, 개인 행동으로 돌아가지 않음");
	}

	[Test]
	public void LeaderPresent_PersonalRuleDoesNotApply()
	{
		RetreatSystem.EnabledOverride = true;
		var leader = NewHuman("L", hp: 100f);
		var weak = NewHuman("W", hp: 10f);
		NewParty(PartyType.Explore, leader, weak);
		Assert.IsFalse(RetreatSystem.EvaluatePersonalRetreat(weak, 1f), "리더가 있으면 파티 평균 기준을 쓴다");
	}

	// ── 이 틱의 후퇴 행동 (이동 / 거점 휴식) ──

	static Human NewDungeonHuman(string name, float mental = -20f, float hp = 100f, float maxHp = 100f)
	{
		var h = NewHuman(name, mental, hp, maxHp);
		h.currentFloor = 1;
		return h;
	}

	[Test]
	public void Activity_NoneWhenDisabledOrOutsideDungeon()
	{
		var h = NewDungeonHuman("A");
		h.Retreat.Mode = RetreatMode.PersonalFlee;
		RetreatSystem.EnabledOverride = false;
		Assert.AreEqual(RetreatActivity.None, RetreatSystem.GetActivity(h), "꺼져 있으면 도주 상태가 있어도 행동하지 않는다");

		RetreatSystem.EnabledOverride = true;
		Assert.AreEqual(RetreatActivity.Move, RetreatSystem.GetActivity(h));
		h.currentFloor = 0;
		Assert.AreEqual(RetreatActivity.None, RetreatSystem.GetActivity(h), "던전 밖(0층)에서는 후퇴·도주 행동이 없다");
	}

	[Test]
	public void Activity_PersonalRetreatAndFleeAlwaysMove()
	{
		RetreatSystem.EnabledOverride = true;
		var a = NewDungeonHuman("A");
		a.Retreat.Mode = RetreatMode.PersonalRetreat;
		var b = NewDungeonHuman("B");
		b.Retreat.Mode = RetreatMode.PersonalFlee;
		Assert.AreEqual(RetreatActivity.Move, RetreatSystem.GetActivity(a));
		Assert.AreEqual(RetreatActivity.Move, RetreatSystem.GetActivity(b));
		Assert.IsTrue(RetreatSystem.WantsRetreatState(a));
		a.isInDungeonEntranceSequence = true;
		Assert.IsFalse(RetreatSystem.WantsRetreatState(a), "던전 입구 시퀀스 중에는 강제 배정하지 않는다");
	}

	[Test]
	public void Activity_PartyRetreatFollowsPhaseAndArrival()
	{
		RetreatSystem.EnabledOverride = true;
		var leader = NewDungeonHuman("L");
		var member = NewDungeonHuman("M");
		var party = NewParty(PartyType.Explore, leader, member);
		party.Retreat.Mode = RetreatMode.PartyRetreat;
		party.Retreat.Destination = RetreatDestination.Base;
		party.Retreat.Phase = PartyRetreatPhase.Moving;

		Assert.AreEqual(RetreatActivity.None, RetreatSystem.GetActivity(member), "명령을 받지 못한 구성원은 개인 행동");
		member.Retreat.Ordered = true;
		Assert.AreEqual(RetreatActivity.Move, RetreatSystem.GetActivity(member));
		member.Retreat.AtBase = true;
		Assert.AreEqual(RetreatActivity.Rest, RetreatSystem.GetActivity(member), "거점에 도착하면 휴식");
		Assert.IsTrue(RetreatSystem.IsResting(member));
		Assert.IsFalse(RetreatSystem.WantsRetreatState(member), "휴식 중인 인원은 후퇴 FSM이 아니라 평소 상태(Tactical Rest, 전투가 나면 Combat)");

		party.Retreat.Phase = PartyRetreatPhase.Leaving;
		Assert.AreEqual(RetreatActivity.Move, RetreatSystem.GetActivity(member), "출구 이동 단계에서는 도착자도 다시 이동");
	}

	// ── 다른 행동 시스템과의 연결(2026-10-13 재검증) ──

	[Test]
	public void IsBoundToRetreat_TrueForMoveAndRest_FalseOtherwise()
	{
		RetreatSystem.EnabledOverride = true;
		var leader = NewDungeonHuman("L");
		var member = NewDungeonHuman("M");
		var party = NewParty(PartyType.Explore, leader, member);
		Assert.IsFalse(RetreatSystem.IsBoundToRetreat(member), "후퇴 전에는 평소 명령을 받는다");

		member.Retreat.Mode = RetreatMode.PersonalFlee;
		Assert.IsTrue(RetreatSystem.IsBoundToRetreat(member), "도주 확정자는 집결·방 이동·문 찾기·코어 보고 명령을 받지 않는다");
		RetreatSystem.EnabledOverride = false;
		Assert.IsFalse(RetreatSystem.IsBoundToRetreat(member), "꺼져 있으면 예전 동작");

		RetreatSystem.EnabledOverride = true;
		var rester = NewDungeonHuman("R");
		var p2 = NewParty(PartyType.Explore, NewDungeonHuman("L2"), rester);
		p2.Retreat.Mode = RetreatMode.PartyRetreat;
		p2.Retreat.Destination = RetreatDestination.Base;
		p2.Retreat.Phase = PartyRetreatPhase.Resting;
		rester.Retreat.Ordered = true;
		rester.Retreat.AtBase = true;
		Assert.IsTrue(RetreatSystem.IsBoundToRetreat(rester), "거점에서 쉬는 인원도 평소 명령으로 흩어지지 않는다");
	}

	[Test]
	public void SuppressesAlertSlowdown_OnlyWhileMovingForRetreat()
	{
		RetreatSystem.EnabledOverride = true;
		var h = NewDungeonHuman("A");
		Assert.IsFalse(RetreatSystem.SuppressesAlertSlowdown(h));
		h.Retreat.Mode = RetreatMode.PersonalFlee;
		Assert.IsTrue(RetreatSystem.SuppressesAlertSlowdown(h), "도주 이동 중에는 경계 이동 감속(75%)을 받지 않는다 — 전투에서 넘어오며 세팅된 경계 스윕이 도주를 늦추면 안 된다");

		var rester = NewDungeonHuman("R");
		var party = NewParty(PartyType.Explore, NewDungeonHuman("L"), rester);
		party.Retreat.Mode = RetreatMode.PartyRetreat;
		party.Retreat.Destination = RetreatDestination.Base;
		party.Retreat.Phase = PartyRetreatPhase.Resting;
		rester.Retreat.Ordered = true;
		rester.Retreat.AtBase = true;
		Assert.IsFalse(RetreatSystem.SuppressesAlertSlowdown(rester), "휴식 중인 인원의 경계 스윕은 평소 규칙");
	}

	[Test]
	public void FleeKnownToLeader_OnlyAfterLeaderWitnessedIt()
	{
		RetreatSystem.EnabledOverride = true;
		var leader = NewDungeonHuman("L");
		var fleeing = NewDungeonHuman("F");
		NewParty(PartyType.Explore, leader, fleeing);
		Assert.IsFalse(RetreatSystem.IsFleeKnownToLeader(fleeing, leader), "도주 확정 전에는 모른다");

		fleeing.Retreat.Mode = RetreatMode.PersonalFlee;
		Assert.IsFalse(RetreatSystem.IsFleeKnownToLeader(fleeing, leader), "부재·무응답만으로 도주로 확정하지 않는다(명세서 6장) — 리더가 직접 확인하기 전까지는 미집결자");

		MentalSystem.OnFleeWitnessed(leader, fleeing);
		Assert.IsTrue(RetreatSystem.IsFleeKnownToLeader(fleeing, leader), "리더가 도주를 직접 확인하면 집결 대기 대상에서 뺀다");
		Assert.IsFalse(RetreatSystem.IsFleeKnownToLeader(leader, leader), "자기 자신은 해당 없음");
	}

	[Test]
	public void Recovery_DoublesHpRegenAndRestoresMentalOnlyWhileRestingWithoutCombat()
	{
		RetreatSystem.EnabledOverride = true;
		var leader = NewDungeonHuman("L");
		var member = NewDungeonHuman("M", mental: -60f);
		var party = NewParty(PartyType.Explore, leader, member);
		party.Retreat.Mode = RetreatMode.PartyRetreat;
		party.Retreat.Phase = PartyRetreatPhase.Resting;
		party.Retreat.Destination = RetreatDestination.Base;
		member.Retreat.Ordered = true;
		member.Retreat.AtBase = true;

		Assert.IsTrue(RetreatSystem.IsRecovering(member));
		Assert.IsTrue(RetreatSystem.ShouldDoubleRegen(member));
		RetreatSystem.TickRecovery(member, 10f);
		Assert.AreEqual(-50f, member.BaseStat.mental, 0.0001f, "휴식 중 원수치 +1/초");

		var enemy = NewHuman("E");
		member.personalSpottedEnemies.Add(enemy);
		Assert.IsFalse(RetreatSystem.IsRecovering(member), "적을 인지하면 회복이 중단된다(전투 발생)");
		RetreatSystem.TickRecovery(member, 10f);
		Assert.AreEqual(-50f, member.BaseStat.mental, 0.0001f);
	}

	// ── 도주 속도 / 통과 순위 ──

	[Test]
	public void FleeRunsFasterThanWalk()
	{
		RetreatSystem.EnabledOverride = true;
		var h = NewDungeonHuman("A");
		Assert.AreEqual(3f, RetreatSystem.ResolveMoveSpeed(h, 3f), 0.0001f);
		h.Retreat.Mode = RetreatMode.PersonalFlee;
		Assert.AreEqual(3f * RetreatPathMath.DefaultSprintMultiplier, RetreatSystem.ResolveMoveSpeed(h, 3f), 0.0001f, "도주 확정자는 달리기");
		h.Retreat.Mode = RetreatMode.PersonalRetreat;
		Assert.AreEqual(3f, RetreatSystem.ResolveMoveSpeed(h, 3f), 0.0001f, "리더 없는 개인 후퇴는 걷기");
	}

	[Test]
	public void PassRank_ReversedForPartyRetreat_FleeAlwaysFirst()
	{
		RetreatSystem.EnabledOverride = true;
		var h = NewDungeonHuman("A");
		Assert.AreEqual(2, RetreatSystem.ResolvePassRank(h, 2), "후퇴·도주 중이 아니면 평소 입장 순위 그대로");

		var leader = NewDungeonHuman("L");
		var party = NewParty(PartyType.Explore, leader, h);
		party.Retreat.Mode = RetreatMode.PartyRetreat;
		party.Retreat.Phase = PartyRetreatPhase.Leaving;
		h.Retreat.Ordered = true;
		Assert.AreEqual(1, RetreatSystem.ResolvePassRank(h, 2), "원거리 공격(2) → 파티 후퇴 순위 1");

		var runner = NewDungeonHuman("R");
		runner.Retreat.Mode = RetreatMode.PersonalFlee;
		Assert.Less(RetreatSystem.ResolvePassRank(runner, 0), 0, "도주자는 일반 순위보다 항상 먼저");
	}

	// ── 파티 후퇴 전이 (휴식 완료 → 출구 / 재탐험) ──

	static Party RestingParty(PartyType type, float hpPercent, float mentalRaw, out Human a, out Human b)
	{
		a = NewDungeonHuman("A", mentalRaw, hp: hpPercent);
		b = NewDungeonHuman("B", mentalRaw, hp: hpPercent);
		var party = NewParty(type, a, b);
		party.Retreat.Mode = RetreatMode.PartyRetreat;
		party.Retreat.Phase = PartyRetreatPhase.Resting;
		party.Retreat.Destination = RetreatDestination.Base;
		party.Retreat.BaseRoom = new Room { Floor = 1, RoomFaction = FactionType.Human };
		party.Retreat.RestStartedAt = 0f;
		foreach (var m in new[] { a, b }) { m.Retreat.Ordered = true; m.Retreat.AtBase = true; }
		return party;
	}

	[Test]
	public void RestNotComplete_KeepsResting()
	{
		RetreatSystem.EnabledOverride = true;
		var party = RestingParty(PartyType.Explore, 59.9f, -20f, out _, out _); // R05: 평균 HP 59.9%
		PartyRetreatSystem.TickRest(party, 1f);
		Assert.AreEqual(PartyRetreatPhase.Resting, party.Retreat.Phase);
		Assert.IsFalse(party.Retreat.NormalRestCompleted);
	}

	[Test]
	public void R07_RestCompleteWithoutGoal_ResumesExplorationAndLocksSecondRest()
	{
		RetreatSystem.EnabledOverride = true;
		var party = RestingParty(PartyType.Explore, 60f, -20f, out var a, out _); // 평균 HP 60%·정신력 40%
		PartyRetreatSystem.TickRest(party, 1f);

		Assert.AreEqual(RetreatMode.None, party.Retreat.Mode, "목표 미달성 → 탐험 재개");
		Assert.IsTrue(party.Retreat.NormalRestCompleted, "휴식 1회 제한 이력");
		Assert.IsTrue(party.Retreat.Reexploring);
		Assert.IsFalse(a.Retreat.Ordered, "재개되면 개인 행동으로 복귀");
		Assert.AreEqual(RetreatActivity.None, RetreatSystem.GetActivity(a));

		// 재탐험 중 다시 후퇴 기준을 채우면 거점 재휴식 없이 출구(R31 맥락)
		Assert.AreEqual(RetreatDestination.Exit, PartyRetreatMath.ChooseDestination(PartyType.Explore, party.Retreat.NormalRestCompleted, false, true, out _));
	}

	[Test]
	public void RestCompleteWithGoal_HeadsForExit()
	{
		RetreatSystem.EnabledOverride = true;
		var party = RestingParty(PartyType.Explore, 90f, -20f, out var a, out _);
		party.OnRoomExploreCompleted(1);
		party.OnRoomExploreCompleted(2); // 방 탐색 임무 수량 달성
		Assert.IsTrue(party.IsMissionGoalAchieved);

		PartyRetreatSystem.TickRest(party, 1f);
		Assert.AreEqual(PartyRetreatPhase.Leaving, party.Retreat.Phase);
		Assert.AreEqual(RetreatDestination.Exit, party.Retreat.Destination);
		Assert.IsNull(party.Retreat.BaseRoom);
		Assert.IsFalse(a.Retreat.AtBase, "출구 이동 단계에서는 더 이상 거점 도착자가 아니다");
		Assert.AreEqual(RetreatActivity.Move, RetreatSystem.GetActivity(a));
	}

	[Test]
	public void OccupyParty_HoldsAtBaseWithoutSafeAdvanceRoom()
	{
		RetreatSystem.EnabledOverride = true;
		var party = RestingParty(PartyType.Occupy, 90f, 40f, out _, out _);
		party.Retreat.InitialSortieCount = 2;
		PartyRetreatSystem.TickRest(party, 1f); // 세션이 없어 인류 지도상 안전한 진출 방을 확인할 수 없다
		Assert.AreEqual(RetreatMode.PartyRetreat, party.Retreat.Mode, "재출격 3조건 중 하나라도 부족하면 거점 대기 유지");
		Assert.AreEqual(0, party.Retreat.ResortieCount);
	}

	[Test]
	public void RestStragglerGrace_HoldsEvaluationUntilEveryoneArrivesOrGraceElapses()
	{
		RetreatSystem.EnabledOverride = true;
		var party = RestingParty(PartyType.Explore, 90f, -20f, out _, out var b);
		b.Retreat.AtBase = false; // 아직 걸어오는 파티원
		party.Retreat.RestStartedAt = 0f;

		PartyRetreatSystem.TickRest(party, 5f); // 유예(기본 10초) 안
		Assert.AreEqual(RetreatMode.PartyRetreat, party.Retreat.Mode, "도착 못 한 파티원이 있으면 평가를 미룬다");
		PartyRetreatSystem.TickRest(party, 11f);
		Assert.AreNotEqual(RetreatMode.PartyRetreat, party.Retreat.Mode, "유예가 지나면 도착자만으로 평균을 낸다(목표 미달성 → 탐험 재개)");
	}

	[Test]
	public void ForceLeaving_SwitchesRestingPartyToExit()
	{
		RetreatSystem.EnabledOverride = true;
		var party = RestingParty(PartyType.Explore, 30f, -20f, out var a, out _);
		PartyRetreatSystem.ForceLeaving(party);
		Assert.AreEqual(PartyRetreatPhase.Leaving, party.Retreat.Phase);
		Assert.AreEqual(RetreatActivity.Move, RetreatSystem.GetActivity(a));
	}

	[Test]
	public void LostBase_FallsBackToExitWhenNoReplacement()
	{
		RetreatSystem.EnabledOverride = true;
		var party = RestingParty(PartyType.Explore, 30f, -20f, out var a, out _);
		party.Retreat.BaseRoom.RoomFaction = FactionType.Wild; // 코어를 빼앗겨 더는 인류 거점이 아니다
		PartyRetreatSystem.ValidateBase(party);
		Assert.AreEqual(PartyRetreatPhase.Leaving, party.Retreat.Phase, "대체 거점이 없으면(세션 없음) 출구로");
		Assert.IsFalse(a.Retreat.AtBase);
	}

	// ── 파티원 이탈과 전멸 판정 ──

	[Test]
	public void ExitedMemberKeepsPartyFromBeingWiped()
	{
		var survivor = NewDungeonHuman("S");
		var dying = NewDungeonHuman("D");
		var party = NewParty(PartyType.Explore, survivor, dying);

		dying.hp = 0f;
		party.OnMemberExited(survivor); // 살아서 던전 밖으로 나감
		Assert.AreEqual(1, party.ExitedCount);
		Assert.IsFalse(party.IsWiped, "한 명이라도 살아서 나갔으면 남은 인원이 전부 죽어도 전멸이 아니다");

		var all = NewParty(PartyType.Explore, NewDungeonHuman("X"), NewDungeonHuman("Y"));
		foreach (var m in all.Members) m.hp = 0f;
		Assert.IsTrue(all.IsWiped);
	}

	[Test]
	public void LeaderLeavingReassignsLeader()
	{
		var leader = NewDungeonHuman("L");
		var member = NewDungeonHuman("M");
		var party = NewParty(PartyType.Explore, leader, member);
		party.OnMemberExited(leader);
		Assert.AreSame(member, party.Leader);
		Assert.IsFalse(party.Members.Contains(leader));
	}

	// ── 생환자 기록 전달 (10장) ──

	[Test]
	public void Records_OnlyDeliveredObserversReflect_R32_R61()
	{
		var kb = new HumanKnowledgeBase();
		var a = NewDungeonHuman("A");
		var b = NewDungeonHuman("B");
		kb.RecordEventByKey(EventId.E_HUMAN_KILL_SEEN, a, "놀", false, InfoType.DirectWitness, "INC1"); // 위험도 +3
		kb.RecordEventByKey(EventId.E_HUMAN_KILL_SEEN, b, "놀", false, InfoType.DirectWitness, "INC1");

		Assert.AreEqual(0f, kb.GetFinalDanger("놀", null, 0f), 0.0001f, "던전 안에서 기록만 해서는 반영되지 않는다");
		kb.DeliverRecords(a);
		Assert.AreEqual(3f, kb.GetFinalDanger("놀", null, 0f), 0.0001f, "생환자가 던전 밖으로 전달한 기록");
		kb.DeliverRecords(b);
		Assert.AreEqual(3f, kb.GetFinalDanger("놀", null, 0f), 0.0001f, "같은 사건을 다른 생존자가 늦게 전달해도 중복 합산하지 않는다");
	}

	[Test]
	public void Records_LostWhenNoHolderSurvives()
	{
		var kb = new HumanKnowledgeBase();
		var dead = NewDungeonHuman("D");
		kb.RecordEventByKey(EventId.E_HUMAN_KILL_SEEN, dead, "놀", false, InfoType.DirectWitness, "INC2");
		kb.OnWaveEnd(new System.Collections.Generic.List<Unit>()); // 전달할 생존자 없이 웨이브 종료
		Assert.AreEqual(0f, kb.GetFinalDanger("놀", null, 0f), 0.0001f, "미전달 기록의 생존 보유자가 없으면 반영되지 않는다");
		Assert.AreEqual(1, kb.CurrentWave, "생존자가 없어도 웨이브 경계는 처리된다");
	}

	[Test]
	public void Records_OnWaveEndStillReflectsSurvivorsInsideDungeon()
	{
		var kb = new HumanKnowledgeBase();
		var a = NewDungeonHuman("A");
		kb.RecordEventByKey(EventId.E_HUMAN_KILL_SEEN, a, "놀", false, InfoType.DirectWitness, "INC3");
		kb.OnWaveEnd(new System.Collections.Generic.List<Unit> { a });
		Assert.AreEqual(3f, kb.GetFinalDanger("놀", null, 0f), 0.0001f);
	}

	// ── 정신력 단계 전투 효과 연결 ──

	[Test]
	public void StageEffects_AppliedOnlyWhenEnabledForHumans()
	{
		var panic = NewHuman("P", mental: -80f);
		RetreatSystem.EnabledOverride = false;
		Assert.AreEqual(1f, RetreatSystem.GetStageEffects(panic).AttackMultiplier, "꺼져 있으면 효과 없음");
		RetreatSystem.EnabledOverride = true;
		Assert.AreEqual(0.80f, RetreatSystem.GetStageEffects(panic).AttackMultiplier, 0.0001f);
		Assert.AreEqual(-0.20f, RetreatSystem.DefenseSuccessDelta(panic), 0.0001f, "공황 방어 성공률 −20");
		var monster = ScriptableObject.CreateInstance<Monster>();
		Assert.AreEqual(1f, RetreatSystem.GetStageEffects(monster).AttackMultiplier, "몬스터는 정신력 단계 효과가 없다");
	}

	// ── 침공 경계 ──

	[Test]
	public void NewInvasionResetsFleeAndHistory()
	{
		RetreatSystem.EnabledOverride = true;
		var h = NewHuman("A", mental: -97f);
		MentalSystem.ApplyChange(h, -3f);
		h.Retreat.WitnessedFlees.Add("X");
		h.Retreat.Encounters.HandledMonsters.Add("golem#1");
		Assert.IsTrue(h.Retreat.IsFleeing);

		FleeSystem.ResetForNewInvasion(h);
		Assert.IsFalse(h.Retreat.IsFleeing);
		Assert.AreEqual(RetreatMode.None, h.Retreat.Mode);
		Assert.AreEqual(0.5f, h.Retreat.FleeRoll.NextProbability, 0.0001f);
		Assert.AreEqual(0, h.Retreat.WitnessedFlees.Count);
		Assert.AreEqual(0, h.Retreat.Encounters.HandledMonsters.Count);
	}
}
#endif
