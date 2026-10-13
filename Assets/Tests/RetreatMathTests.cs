#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 후퇴·도주 순수 계산 고정 테스트(NUnit, #if UNITY_INCLUDE_TESTS) — 후퇴도주 설계명세서(2026-10-13)의 숫자 예시와 검증 사례(R01~R67 중 순수 계산에 해당하는 것)를 그대로 고정한다. Unity 오브젝트가 필요한 정신력 단일 진입점·파티 상태 테스트는 RetreatSystemTests.
// ========================================================================

public class RetreatMathTests
{
	// ── 정신력 수치와 비율 (3-1장 환산 예시) ──

	[TestCase(-100f, 0f)]
	[TestCase(-70f, 15f)]
	[TestCase(-20f, 40f)]
	[TestCase(0f, 50f)]
	[TestCase(100f, 100f)]
	public void RatioPercent_FollowsDocumentExamples(float raw, float expectedPercent)
	{
		Assert.AreEqual(expectedPercent, MentalMath.ToRatioPercent(raw), 0.0001f);
		Assert.AreEqual(raw, MentalMath.FromRatioPercent(expectedPercent), 0.0001f);
	}

	[Test]
	public void AuthoringScale_FortyOfHundredIsRatioFortyPercent()
	{
		Assert.AreEqual(-20f, MentalMath.FromAuthoringScale(40f, 100f), 0.0001f, "units.json의 인류 40/100 = 비율 40% = 원수치 −20");
		Assert.AreEqual(100f, MentalMath.FromAuthoringScale(100f, 100f), 0.0001f);
		Assert.AreEqual(-100f, MentalMath.FromAuthoringScale(0f, 100f), 0.0001f);
		Assert.AreEqual(0f, MentalMath.FromAuthoringScale(50f, 100f), 0.0001f);
		Assert.AreEqual(0f, MentalMath.FromAuthoringScale(0f, 0f), "maxMental ≤ 0은 정신력 시스템이 없는 유닛");
	}

	[Test]
	public void Clamp_KeepsMinusHundredToPlusHundred()
	{
		Assert.AreEqual(-100f, MentalMath.Clamp(-130f));
		Assert.AreEqual(100f, MentalMath.Clamp(120f));
		Assert.AreEqual(-42.5f, MentalMath.Clamp(-42.5f));
	}

	// ── 정신력 7단계 경계 (3-2장): 소수점에도 같은 경계 ──

	[TestCase(100f, MentalStage.Excited)]
	[TestCase(70.001f, MentalStage.Excited)]
	[TestCase(70f, MentalStage.Calm)]
	[TestCase(40.001f, MentalStage.Calm)]
	[TestCase(40f, MentalStage.Stable)]
	[TestCase(10.001f, MentalStage.Stable)]
	[TestCase(10f, MentalStage.Normal)]
	[TestCase(0f, MentalStage.Normal)]
	[TestCase(-10f, MentalStage.Normal)]
	[TestCase(-10.001f, MentalStage.Tension)]
	[TestCase(-40f, MentalStage.Tension)]
	[TestCase(-40.001f, MentalStage.Fear)]
	[TestCase(-70f, MentalStage.Fear)]
	[TestCase(-70.001f, MentalStage.Panic)]
	[TestCase(-100f, MentalStage.Panic)]
	public void GetStage_BoundariesFollowTable(float raw, MentalStage expected)
	{
		Assert.AreEqual(expected, MentalMath.GetStage(raw));
	}

	[Test]
	public void Panic_IsStrictlyBelowMinusSeventy()
	{
		Assert.IsFalse(MentalMath.IsPanic(-70f), "−70은 공포");
		Assert.IsTrue(MentalMath.IsPanic(-70.0001f));
		Assert.IsFalse(MentalMath.IsPanic(-71f + 2f));
		Assert.IsTrue(MentalMath.ReachedFloor(-100f));
		Assert.IsFalse(MentalMath.ReachedFloor(-99.9f));
	}

	[Test]
	public void StageEffects_MatchTable()
	{
		var panic = MentalMath.GetStageEffects(MentalStage.Panic);
		Assert.AreEqual(0.80f, panic.AttackMultiplier, 0.0001f);
		Assert.AreEqual(-20f, panic.DefenseSuccessDelta, 0.0001f);
		Assert.AreEqual(1.15f, panic.ReactionTimeMultiplier, 0.0001f);
		Assert.IsTrue(panic.FleeRollTarget);
		Assert.IsFalse(MentalMath.GetStageEffects(MentalStage.Fear).FleeRollTarget, "공포는 도주 판정 대상이 아니다");
		Assert.AreEqual(0.95f, MentalMath.GetStageEffects(MentalStage.Stable).DecreaseMultiplier, 0.0001f);
		Assert.AreEqual(1.10f, MentalMath.GetStageEffects(MentalStage.Excited).AttackMultiplier, 0.0001f);
		var normal = MentalMath.GetStageEffects(MentalStage.Normal);
		Assert.AreEqual(1f, normal.AttackMultiplier, 0.0001f, "평범 단계는 단계 효과 없음(배율 1, 증감 0)");
		Assert.AreEqual(1f, normal.DecreaseMultiplier, 0.0001f);
		Assert.AreEqual(1f, normal.ReactionTimeMultiplier, 0.0001f);
		Assert.AreEqual(0f, normal.DefenseSuccessDelta, 0.0001f);
		Assert.IsFalse(normal.FleeRollTarget);
	}

	[Test]
	public void DecreaseModifier_StableStageOnly()
	{
		Assert.AreEqual(4.75f, MentalMath.ApplyDecreaseModifier(5f, 30f), 0.0001f, "R64: 안정 단계 5×0.95");
		Assert.AreEqual(5f, MentalMath.ApplyDecreaseModifier(5f, 0f), 0.0001f, "R62: 평범 단계는 보정 ×1");
		Assert.AreEqual(5f, MentalMath.ApplyDecreaseModifier(5f, -60f), 0.0001f);
	}

	// ── 공황 구간 실제 감소량 (3-3·3-4장) ──

	[TestCase(-68f, -76f, 6f)]   // 전체 8, 공황 구간 6
	[TestCase(-70f, -82f, 12f)]  // 공황 구간 12
	[TestCase(-60f, -71f, 1f)]   // R24: 공황 구간 하락은 1만
	[TestCase(-50f, -60f, 0f)]   // 공황 밖 감소는 누적하지 않는다
	[TestCase(-80f, -90f, 10f)]
	[TestCase(-80f, -70f, 0f)]   // 회복
	[TestCase(-80f, -80f, 0f)]
	public void PanicZoneDecrease_CountsOnlyBelowMinusSeventy(float before, float after, float expected)
	{
		Assert.AreEqual(expected, MentalMath.PanicZoneDecrease(before, after), 0.0001f);
	}

	// ── 개인 도주 판정 (3-3·3-4장, R08~R12, R38) ──

	static float Never() => 0.9999f;  // 어떤 확률(<100%)에도 도주하지 않는 난수
	static float Always() => 0.0f;

	[TestCase(1, 0.5f)]
	[TestCase(2, 0.6f)]
	[TestCase(3, 0.7f)]
	[TestCase(4, 0.8f)]
	[TestCase(5, 0.9f)]
	[TestCase(6, 1.0f)]
	[TestCase(9, 1.0f)]
	public void ProbabilityForRoll_StartsFiftyAndRisesTenPoints(int roll, float expected)
	{
		Assert.AreEqual(expected, FleeRollMath.ProbabilityForRoll(roll), 0.0001f);
	}

	[Test]
	public void R08_FourThenTwoMakesOneRoll_RemainderOne_NextSixty()
	{
		var s = new FleeRollState();
		var first = FleeRollMath.Advance(s, MentalMath.PanicZoneDecrease(-70f, -74f), Never);
		Assert.AreEqual(0, first.Rolls, "하락량 4는 아직 5 미만");
		var second = FleeRollMath.Advance(s, MentalMath.PanicZoneDecrease(-74f, -76f), Never);
		Assert.AreEqual(1, second.Rolls);
		Assert.IsFalse(second.Fled);
		Assert.AreEqual(1f, s.PendingDecrease, 0.0001f, "잔여 1");
		Assert.AreEqual(0.6f, s.NextProbability, 0.0001f, "다음 확률 60%");
	}

	[Test]
	public void R09_FiveHeldRollsMakeSixthCertain()
	{
		var s = new FleeRollState();
		var outcome = FleeRollMath.Advance(s, 30f, Never); // 6회 판정분
		Assert.AreEqual(6, outcome.Rolls);
		Assert.IsTrue(outcome.Fled, "6회째는 100%");
		Assert.IsTrue(s.Confirmed);
	}

	[Test]
	public void FirstRollFlee_StopsFurtherRolls()
	{
		var s = new FleeRollState();
		var outcome = FleeRollMath.Advance(s, MentalMath.PanicZoneDecrease(-70f, -82f), Always); // 공황 구간 12 → 최대 2회
		Assert.AreEqual(1, outcome.Rolls, "첫 판정에서 도주하면 두 번째는 하지 않는다");
		Assert.IsTrue(outcome.Fled);
	}

	[Test]
	public void R10_RecoveryResetsProbabilityAndPendingDecrease()
	{
		var s = new FleeRollState();
		FleeRollMath.Advance(s, 19f, Never); // 3회 판정(15) + 잔여 4 → 다음 확률 80%
		Assert.AreEqual(0.8f, s.NextProbability, 0.0001f);
		Assert.AreEqual(4f, s.PendingDecrease, 0.0001f);

		s.OnRecovered();
		Assert.AreEqual(0.5f, s.NextProbability, 0.0001f);
		Assert.AreEqual(0f, s.PendingDecrease, 0.0001f);

		var after = FleeRollMath.Advance(s, 1f, Never);
		Assert.AreEqual(0, after.Rolls, "회복 뒤 1 감소는 누적 1이라 아직 판정하지 않는다");
		Assert.AreEqual(1f, s.PendingDecrease, 0.0001f);
	}

	[Test]
	public void R12_ConfirmedFleeSurvivesRecovery()
	{
		var s = new FleeRollState();
		FleeRollMath.Advance(s, 5f, Always);
		Assert.IsTrue(s.Confirmed);
		s.OnRecovered();
		Assert.IsTrue(s.Confirmed, "이미 확정된 도주는 회복으로 취소하지 않는다");
		Assert.AreEqual(0, FleeRollMath.Advance(s, 50f, Always).Rolls, "확정 뒤에는 추가 판정을 하지 않는다");
	}

	[Test]
	public void R38_DecimalsAccumulate_FourSevenFiveThenNineFive()
	{
		var s = new FleeRollState();
		Assert.AreEqual(0, FleeRollMath.Advance(s, 4.75f, Never).Rolls);
		var outcome = FleeRollMath.Advance(s, 0.95f, Never);
		Assert.AreEqual(1, outcome.Rolls, "합계 5.70이라 1회 판정");
		Assert.AreEqual(0.70f, s.PendingDecrease, 0.0001f);
		Assert.AreEqual(0.6f, s.NextProbability, 0.0001f);
	}

	[Test]
	public void NewInvasion_ResetsEverything()
	{
		var s = new FleeRollState();
		FleeRollMath.Advance(s, 12f, Never);
		FleeRollMath.Advance(s, 5f, Always);
		s.ResetForNewInvasion();
		Assert.IsFalse(s.Confirmed);
		Assert.AreEqual(0.5f, s.NextProbability, 0.0001f);
		Assert.AreEqual(0f, s.PendingDecrease, 0.0001f);
	}

	// ── 파티 후퇴 기준 (1-1~1-3장, R01~R03, R33~R34) ──

	static PartyRetreatMath.Averages Avg(params (float hp, float mentalRaw)[] members)
	{
		var list = new List<PartyRetreatMath.MemberVitals>();
		foreach (var m in members) list.Add(PartyRetreatMath.MemberVitals.From(m.hp, 100f, m.mentalRaw));
		return PartyRetreatMath.ComputeAverages(list);
	}

	[Test]
	public void R01_HpExactlyTwentyRetreats()
	{
		var avg = new PartyRetreatMath.Averages(2, 20f, 30f); // 평균 HP 정확히 20%, 정신력 30%
		Assert.IsTrue(PartyRetreatMath.ShouldRetreat(avg, PartyRetreatMath.ThresholdsFor(PartyType.Explore)));
	}

	[Test]
	public void R02_MentalExactlyFifteenRetreats()
	{
		var avg = new PartyRetreatMath.Averages(2, 21f, 15f);
		Assert.IsTrue(PartyRetreatMath.ShouldRetreat(avg, PartyRetreatMath.ThresholdsFor(PartyType.Recover)));
	}

	[Test]
	public void R03_BothAboveThresholdsDoNotRetreat()
	{
		var avg = new PartyRetreatMath.Averages(2, 21f, 16f);
		foreach (var type in new[] { PartyType.Explore, PartyType.Recover, PartyType.MopUp })
			Assert.IsFalse(PartyRetreatMath.ShouldRetreat(avg, PartyRetreatMath.ThresholdsFor(type)));
	}

	[Test]
	public void AverageExamples_FromDocumentTable()
	{
		var general = PartyRetreatMath.ThresholdsFor(PartyType.Explore);
		// A 10/100, B 30/100 → 평균 20% → 후퇴
		Assert.IsTrue(PartyRetreatMath.ShouldRetreat(Avg((10f, 0f), (30f, 0f)), general));
		// A 정신력 −80, B −60 → 비율 10%·20% → 평균 15% → 후퇴
		var mentalAvg = Avg((100f, -80f), (100f, -60f));
		Assert.AreEqual(15f, mentalAvg.MentalPercent, 0.001f);
		Assert.IsTrue(PartyRetreatMath.ShouldRetreat(mentalAvg, general));
	}

	[Test]
	public void AverageIsPerMemberRatio_NotTotalHpOverTotalMax()
	{
		// A 10/100, B 60/200 → 각자 10%·30% → 평균 20%(총 HP/총 최대 HP인 23.3%를 쓰지 않는다)
		var vitals = new List<PartyRetreatMath.MemberVitals>
		{
			PartyRetreatMath.MemberVitals.From(10f, 100f, 0f),
			PartyRetreatMath.MemberVitals.From(60f, 200f, 0f),
		};
		var avg = PartyRetreatMath.ComputeAverages(vitals);
		Assert.AreEqual(20f, avg.HpPercent, 0.001f);
		Assert.IsTrue(PartyRetreatMath.ShouldRetreat(avg, PartyRetreatMath.ThresholdsFor(PartyType.Explore)));
	}

	[Test]
	public void R04_ExcludedMembersDoNotEnterTheAverage()
	{
		// A 25%, B 25%, 미집결 C 0% — 호출부가 C를 넘기지 않으면 평균 HP는 25%
		var avg = Avg((25f, 0f), (25f, 0f));
		Assert.AreEqual(25f, avg.HpPercent, 0.001f);
		Assert.IsFalse(PartyRetreatMath.ShouldRetreat(avg, PartyRetreatMath.ThresholdsFor(PartyType.Explore)));
	}

	[Test]
	public void EmptyMemberSet_IsNotValid_AndNeverRetreats()
	{
		var avg = PartyRetreatMath.ComputeAverages(new List<PartyRetreatMath.MemberVitals>());
		Assert.IsFalse(avg.Valid);
		Assert.IsFalse(PartyRetreatMath.ShouldRetreat(avg, PartyRetreatMath.ThresholdsFor(PartyType.Explore)));
		Assert.IsFalse(PartyRetreatMath.IsRestComplete(avg));
	}

	[Test]
	public void R33_R34_OccupyUsesFortyThirty()
	{
		var occupy = PartyRetreatMath.ThresholdsFor(PartyType.Occupy);
		Assert.IsTrue(PartyRetreatMath.ShouldRetreat(new PartyRetreatMath.Averages(3, 40f, 31f), occupy), "R33: HP 40% 기준으로 후퇴");
		Assert.IsTrue(PartyRetreatMath.ShouldRetreat(new PartyRetreatMath.Averages(3, 41f, 30f), occupy), "R34: 정신력 30% 기준으로 후퇴");
		Assert.IsFalse(PartyRetreatMath.ShouldRetreat(new PartyRetreatMath.Averages(3, 41f, 31f), occupy));
		Assert.IsFalse(PartyRetreatMath.ShouldRetreat(new PartyRetreatMath.Averages(3, 40f, 31f), PartyRetreatMath.ThresholdsFor(PartyType.Explore)), "같은 값이라도 일반 파티(20/15)는 후퇴하지 않는다 — 점령 파티에 일반 기준을 적용하지 않는다");
	}

	[Test]
	public void ThresholdIsNotAffectedByFloatNoise()
	{
		// 0.3f×100 = 30.000002 같은 부동소수 값이 "정확히 N%" 판정을 뒤집지 않는다.
		Assert.AreEqual(30f, PartyRetreatMath.PercentOf(30f, 100f), 0.001f);
		var avg = Avg((20f, 0f), (20f, 0f));
		Assert.IsTrue(PartyRetreatMath.ShouldRetreat(avg, PartyRetreatMath.ThresholdsFor(PartyType.Explore)));
	}

	// ── 거점 휴식 완료 (2-1장, R05·R06·R55) ──

	[Test]
	public void R05_AverageHpBelowSixtyKeepsResting()
	{
		Assert.IsFalse(PartyRetreatMath.IsRestComplete(new PartyRetreatMath.Averages(2, 59.9f, 40f)));
	}

	[Test]
	public void R55_AverageMentalBelowFortyKeepsResting()
	{
		Assert.IsFalse(PartyRetreatMath.IsRestComplete(new PartyRetreatMath.Averages(2, 60f, 39.9f)));
	}

	[Test]
	public void R06_AverageMeetsBothEvenIfIndividualsDoNot()
	{
		// HP 30%·90%, 정신력 30%(원수치 −40)·50%(0) → 평균 HP 60%, 평균 정신력 40%
		var avg = Avg((30f, -40f), (90f, 0f));
		Assert.AreEqual(60f, avg.HpPercent, 0.001f);
		Assert.AreEqual(40f, avg.MentalPercent, 0.001f);
		Assert.IsTrue(PartyRetreatMath.IsRestComplete(avg), "개인별 미달이 종료를 막지 않는다");
	}

	[Test]
	public void R06_R07_PostRestDecisionFollowsGoal()
	{
		Assert.AreEqual(PostRestAction.LeaveDungeon, PartyRetreatMath.DecidePostRest(true));
		Assert.AreEqual(PostRestAction.Reexplore, PartyRetreatMath.DecidePostRest(false));
	}

	[Test]
	public void R53_R54_RecoveryRates()
	{
		// R54: 재생력 0.5인 유닛이 10초 — 탐험 중 +5, 휴식 중 +10
		Assert.AreEqual(55f, PartyRetreatMath.NextHp(50f, 100f, 0.5f, 10f, resting: false), 0.0001f);
		Assert.AreEqual(60f, PartyRetreatMath.NextHp(50f, 100f, 0.5f, 10f, resting: true), 0.0001f);
		Assert.AreEqual(100f, PartyRetreatMath.NextHp(99f, 100f, 0.5f, 10f, resting: true), 0.0001f, "최대 HP 초과 없음");
		// R53: −70에서 50초 휴식 → −20(비율 40%), 정신력은 탐험 중 자연 회복 없음(호출부가 휴식 중에만 부른다)
		Assert.AreEqual(-20f, PartyRetreatMath.NextMentalAtRest(-70f, 50f), 0.0001f);
		Assert.AreEqual(40f, MentalMath.ToRatioPercent(PartyRetreatMath.NextMentalAtRest(-70f, 50f)), 0.0001f);
		Assert.AreEqual(100f, PartyRetreatMath.NextMentalAtRest(99f, 10f), 0.0001f, "상한 +100");
	}

	// ── 개인 후퇴 (1-4장, R49·R50·R52·R58) ──

	[TestCase(20f, true)]
	[TestCase(19.9f, true)]
	[TestCase(20.5f, false)]
	[TestCase(30f, false)]
	public void PersonalRetreat_StartsAtTwentyPercentHp(float hpPercent, bool expected)
	{
		Assert.AreEqual(expected, PartyRetreatMath.ShouldStartPersonalRetreat(hpPercent));
	}

	// ── 후퇴 목적지 (5-1장, R36·R59) ──

	[Test]
	public void Destination_GeneralFirstRetreatGoesToBaseElseExit()
	{
		Assert.AreEqual(RetreatDestination.Base, PartyRetreatMath.ChooseDestination(PartyType.Explore, false, false, true, out _));
		Assert.AreEqual(RetreatDestination.Exit, PartyRetreatMath.ChooseDestination(PartyType.Explore, false, false, false, out _), "기존 거점 부재 → 출구");
		Assert.AreEqual(RetreatDestination.Exit, PartyRetreatMath.ChooseDestination(PartyType.Explore, true, false, true, out _), "휴식을 마친 뒤 재후퇴는 거점 재휴식 없이 출구");
	}

	[Test]
	public void Destination_OccupyPrefersDefenseThenBasicThenExit()
	{
		var dest = PartyRetreatMath.ChooseDestination(PartyType.Occupy, false, true, true, out var kind);
		Assert.AreEqual(RetreatDestination.Base, dest);
		Assert.AreEqual(BaseKind.Defense, kind);

		dest = PartyRetreatMath.ChooseDestination(PartyType.Occupy, false, false, true, out kind);
		Assert.AreEqual(RetreatDestination.Base, dest, "R59: 방어 거점 이용 불가, 기초 거점 이용 가능 → 기초 거점");
		Assert.AreEqual(BaseKind.Basic, kind);

		Assert.AreEqual(RetreatDestination.Exit, PartyRetreatMath.ChooseDestination(PartyType.Occupy, false, false, false, out _), "R36: 이용 가능한 거점 0개 → 던전 밖");
		Assert.AreEqual(RetreatDestination.Base, PartyRetreatMath.ChooseDestination(PartyType.Occupy, true, false, true, out _), "점령 파티는 휴식 1회 제한이 없다");
	}

	// ── 점령 파티 재출격 (2-3장, R35·R43) ──

	[TestCase(5, 4)]
	[TestCase(6, 5)]
	[TestCase(1, 1)]
	[TestCase(2, 2)]
	[TestCase(10, 8)]
	[TestCase(0, 0)]
	public void RequiredResortieSurvivors_IsEightyPercentRoundedUp(int n0, int expected)
	{
		Assert.AreEqual(expected, PartyRetreatMath.RequiredResortieSurvivors(n0));
	}

	[Test]
	public void R35_ResortieNeedsAllThreeConditions()
	{
		var recovered = new PartyRetreatMath.Averages(4, 60f, 40f);
		Assert.IsTrue(PartyRetreatMath.CanResortie(recovered, fieldSurvivors: 4, initialCount: 5, hasSafeAdvanceRoom: true));
		Assert.IsFalse(PartyRetreatMath.CanResortie(recovered, 4, 5, hasSafeAdvanceRoom: false), "안전한 진출 방이 없으면 거점 대기");
		Assert.IsFalse(PartyRetreatMath.CanResortie(new PartyRetreatMath.Averages(4, 59f, 40f), 4, 5, true), "회복 미완료");
	}

	[Test]
	public void R43_CorpsesDoNotCountAsSurvivors()
	{
		var recovered = new PartyRetreatMath.Averages(4, 60f, 40f);
		Assert.IsFalse(PartyRetreatMath.CanResortie(recovered, fieldSurvivors: 4, initialCount: 6, hasSafeAdvanceRoom: true), "N0=6이면 현장 생존자 5명이 필요");
		Assert.IsTrue(PartyRetreatMath.CanResortie(recovered, 5, 6, true));
	}

	// ── 몬스터 조우 충격 (4장, R22~R27, R39·R40) ──

	[TestCase(0f, 0)] [TestCase(99f, 0)]
	[TestCase(100f, 1)] [TestCase(299f, 1)]
	[TestCase(300f, 2)] [TestCase(499f, 2)]
	[TestCase(500f, 3)] [TestCase(699f, 3)]
	[TestCase(700f, 4)] [TestCase(899f, 4)]
	[TestCase(900f, 5)] [TestCase(999f, 5)] [TestCase(1000f, 5)]
	public void R22_DangerOrdinalBoundaries(float danger, int expected)
	{
		Assert.AreEqual(expected, EncounterShockMath.DangerOrdinal(danger));
	}

	[TestCase(-1, 0)] [TestCase(0, 0)] [TestCase(1, 0)]
	[TestCase(2, 2)]
	[TestCase(3, 3)] [TestCase(4, 3)] [TestCase(5, 3)]
	public void TierOfGap_PlusThreeAndAboveShareOneTier(int gap, int expectedTier)
	{
		Assert.AreEqual(expectedTier, EncounterShockMath.TierOfGap(gap));
	}

	[Test]
	public void R23_GapPlusTwo_IsPlainMentalDamage()
	{
		var shock = EncounterShockMath.Resolve(2, -20f);
		Assert.AreEqual(EncounterShockKind.MentalDamage, shock.Kind);
		Assert.AreEqual(20f, shock.MentalDamage, 0.0001f);
		Assert.AreEqual(0f, shock.StunSeconds, "별도 기절 판정 없음");
	}

	[Test]
	public void R24_GapPlusThreeBeforePanic_StunAndMinusSeventyOne()
	{
		var shock = EncounterShockMath.Resolve(3, -60f);
		Assert.AreEqual(EncounterShockKind.StunAndPanic, shock.Kind);
		Assert.AreEqual(-71f, shock.SetMentalTo, 0.0001f);
		Assert.AreEqual(1f, shock.StunSeconds, 0.0001f);
		Assert.AreEqual(1f, MentalMath.PanicZoneDecrease(-60f, shock.SetMentalTo), 0.0001f, "공황 구간 하락은 1만 누적");
	}

	[Test]
	public void R25_GapPlusThreeDuringPanic_StunAndMinusHundred()
	{
		var shock = EncounterShockMath.Resolve(3, -80f);
		Assert.AreEqual(EncounterShockKind.StunAndMax, shock.Kind);
		Assert.AreEqual(-100f, shock.SetMentalTo, 0.0001f);
		Assert.AreEqual(1f, shock.StunSeconds, 0.0001f);
	}

	[Test]
	public void PanicBranch_UsesStateBeforeTheEvent()
	{
		Assert.AreEqual(EncounterShockKind.StunAndPanic, EncounterShockMath.Resolve(3, -70f).Kind, "−70은 공포라 신규 공황");
		Assert.AreEqual(EncounterShockKind.StunAndMax, EncounterShockMath.Resolve(3, -70.01f).Kind);
		Assert.AreEqual(EncounterShockKind.None, EncounterShockMath.Resolve(0, -80f).Kind);
	}

	[Test]
	public void ResponseStageTable_UnsetMeansNoGapIsComputed()
	{
		EncounterShockMath.LevelToResponseStage = null;
		Assert.IsFalse(EncounterShockMath.TryComputeGap(900f, 5, out _), "Lv→대응 단계 표가 없으면 격차를 임의로 계산하지 않는다");
		try
		{
			EncounterShockMath.LevelToResponseStage = level => level >= 5 ? 2 : 1;
			Assert.IsTrue(EncounterShockMath.TryComputeGap(900f, 5, out int gap));
			Assert.AreEqual(3, gap); // MAX(5) − 2
			Assert.IsTrue(EncounterShockMath.TryComputeGap(300f, 1, out gap));
			Assert.AreEqual(1, gap); // 2 − 1
		}
		finally { EncounterShockMath.LevelToResponseStage = null; }
	}

	static List<KeyValuePair<string, int>> Seen(params (string key, int gap)[] items)
	{
		var list = new List<KeyValuePair<string, int>>();
		foreach (var i in items) list.Add(new KeyValuePair<string, int>(i.key, i.gap));
		return list;
	}

	[Test]
	public void R27_SameMonsterIsHandledOncePerInvasion()
	{
		var t = new EncounterShockTracker();
		Assert.AreEqual(EncounterShockKind.StunAndPanic, t.Evaluate(Seen(("golem#1", 3)), -20f).Kind);
		t.EndEncounterBundle(); // 시야 이탈·재교전 — 조우 묶음이 끝나도 개체 이력은 유지
		Assert.AreEqual(EncounterShockKind.None, t.Evaluate(Seen(("golem#1", 3)), -20f).Kind, "이미 처리한 개체는 다시 적용하지 않는다");
	}

	[Test]
	public void R39_TwoHighMonstersAtOnce_OnlyOneEffect()
	{
		var t = new EncounterShockTracker();
		var shock = t.Evaluate(Seen(("a", 3), ("b", 4)), -20f);
		Assert.AreEqual(EncounterShockKind.StunAndPanic, shock.Kind);
		Assert.AreEqual(-71f, shock.SetMentalTo, 0.0001f, "−71 설정 뒤 다른 개체로 −100을 연속 적용하지 않는다");
		Assert.IsTrue(t.HandledMonsters.Contains("a") && t.HandledMonsters.Contains("b"), "함께 판단한 개체도 이력에 기록");
		Assert.AreEqual(EncounterShockKind.None, t.Evaluate(Seen(("c", 3)), -71f).Kind, "같은 조우에서 동급 이하 추가 발견은 적용하지 않는다");
	}

	[Test]
	public void R40_PlusTwoThenNewPlusThree_AppliesHigherTierOnce()
	{
		var t = new EncounterShockTracker();
		Assert.AreEqual(EncounterShockKind.MentalDamage, t.Evaluate(Seen(("a", 2)), -20f).Kind);
		var upper = t.Evaluate(Seen(("b", 3)), -40f); // +2 적용 뒤 정신력 −40(공황 아님)
		Assert.AreEqual(EncounterShockKind.StunAndPanic, upper.Kind);
		Assert.AreEqual(EncounterShockKind.None, t.Evaluate(Seen(("c", 2)), -71f).Kind, "상위 구간 적용 뒤 하위 구간은 없음");
	}

	[Test]
	public void LowerOrEqualTierAfterwards_DoesNothing_UntilBundleEnds()
	{
		var t = new EncounterShockTracker();
		t.Evaluate(Seen(("a", 2)), -20f);
		Assert.AreEqual(EncounterShockKind.None, t.Evaluate(Seen(("b", 2)), -40f).Kind, "동급 추가 적용 없음");
		t.EndEncounterBundle();
		Assert.AreEqual(EncounterShockKind.MentalDamage, t.Evaluate(Seen(("c", 2)), -40f).Kind, "새 조우 묶음에서는 다시 적용");
		t.ResetForNewInvasion();
		Assert.AreEqual(EncounterShockKind.MentalDamage, t.Evaluate(Seen(("a", 2)), -20f).Kind, "새 침공은 개체 이력을 초기화");
	}

	[Test]
	public void GapPlusOneOrLess_IsRecordedButHasNoEffect()
	{
		var t = new EncounterShockTracker();
		Assert.AreEqual(EncounterShockKind.None, t.Evaluate(Seen(("a", 1), ("b", 0)), -20f).Kind);
		Assert.IsTrue(t.HandledMonsters.Contains("a"));
	}

	// ── 이동 (5장, R13·R41·R42·R60) ──

	[Test]
	public void R13_DetourAllowsSixExtraTiles()
	{
		Assert.IsTrue(RetreatPathMath.IsDetourAllowed(4, 10));
		Assert.IsFalse(RetreatPathMath.IsDetourAllowed(4, 11));
		Assert.IsTrue(RetreatPathMath.IsDetourAllowed(4, 4), "우회가 없는 기준 경로");
		Assert.IsTrue(RetreatPathMath.IsDetourAllowed(10, 16, extraTileLimit: 6));
		Assert.IsFalse(RetreatPathMath.IsDetourAllowed(10, 17, extraTileLimit: 6));
	}

	[Test]
	public void R42_R60_TrapForcePassNeedsKnownDamageLeavingPositiveHp()
	{
		Assert.IsFalse(RetreatPathMath.CanForcePassTrap(10f, 10f), "예상 피해가 현재 HP와 같으면 강행 금지");
		Assert.IsFalse(RetreatPathMath.CanForcePassTrap(10f, null), "피해량 불명이면 강행 금지");
		Assert.IsFalse(RetreatPathMath.CanForcePassTrap(10f, 12f));
		Assert.IsTrue(RetreatPathMath.CanForcePassTrap(10f, 9.5f));
	}

	[Test]
	public void FleeRunsAndPartyRetreatWalks()
	{
		Assert.AreEqual(4.5f, RetreatPathMath.SprintSpeed(3f), 0.0001f);
		Assert.AreEqual(4.5f, RetreatPathMath.RetreatSpeed(RetreatMode.PersonalFlee, 3f), 0.0001f);
		Assert.AreEqual(3f, RetreatPathMath.RetreatSpeed(RetreatMode.PartyRetreat, 3f), 0.0001f);
		Assert.AreEqual(3f, RetreatPathMath.RetreatSpeed(RetreatMode.PersonalRetreat, 3f), 0.0001f, "리더 없는 개인 후퇴는 걷기");
	}

	// ── 집결 기한 (6장, R28·R29) ──

	[Test]
	public void R29_RallyDeadline()
	{
		Assert.AreEqual(13f, RallySearchMath.Deadline(0f, 10f), 0.0001f, "도착 예정 10초 → 기한 13초");
		Assert.AreEqual(7f, RallySearchMath.Deadline(0f, 4f), 0.0001f, "도착 예정 4초 → 기한 7초(8초와 비교해 늦은 쪽을 고르지 않음)");
		Assert.AreEqual(8f, RallySearchMath.Deadline(0f, null), 0.0001f, "예정 정보 없음 → 발령부터 8초");
		Assert.AreEqual(12f, RallySearchMath.Deadline(2f, null, waitSeconds: 10f), 0.0001f);
	}

	// ── 우회 누적 한도 (R13: 같은 차단 구간은 이미 걸은 우회 거리를 합산) ──

	[Test]
	public void R13_DetourLimitCountsWalkedDistanceAcrossRecalculations()
	{
		int baseline = 4;
		// 처음 우회 10칸(허용) → 3칸 걸은 뒤 재탐색에서 남은 우회 7칸: 합계 10칸이라 계속 허용
		Assert.IsTrue(RetreatPathMath.IsDetourAllowed(baseline, RetreatPathMath.TotalDetourLength(3, 7)));
		// 같은 구간에서 3칸 걸은 뒤 남은 우회가 8칸으로 늘면 합계 11칸 — 매번 6칸을 새로 주지 않으므로 불허
		Assert.IsFalse(RetreatPathMath.IsDetourAllowed(baseline, RetreatPathMath.TotalDetourLength(3, 8)));
	}

	// ── 후퇴·도주 중 통과 순서 (5-1장) ──

	[TestCase(3, 0)] // 원거리 지원(사제형)이 파티 후퇴에서 가장 먼저
	[TestCase(2, 1)] // 원거리 공격(궁수형)
	[TestCase(1, 2)] // 근접 지원(지휘관형)
	[TestCase(0, 3)] // 전방 근접(기사형)이 마지막
	public void PartyRetreatRank_IsReverseOfEntryOrder(int entryRank, int expectedRank)
	{
		Assert.AreEqual(expectedRank, RetreatPathMath.PartyRetreatRank(entryRank));
	}

	[Test]
	public void FleeRank_ShorterEscapeFirst_TiesFollowArcherPriestCommanderKnight()
	{
		// 탈출 시간이 짧은 쪽이 먼저
		Assert.Less(RetreatPathMath.FleeRank(2.0f, 0), RetreatPathMath.FleeRank(5.0f, 3));
		// 같은 탈출 시간이면 궁수형(2) → 사제형(3) → 지휘관형(1) → 기사형(0)
		int archer = RetreatPathMath.FleeRank(3f, 2), priest = RetreatPathMath.FleeRank(3f, 3), commander = RetreatPathMath.FleeRank(3f, 1), knight = RetreatPathMath.FleeRank(3f, 0);
		Assert.Less(archer, priest);
		Assert.Less(priest, commander);
		Assert.Less(commander, knight);
		// 도주자는 어떤 일반 순위(0~3)보다 항상 먼저
		Assert.Less(RetreatPathMath.FleeRank(9999f, 0), 0);
	}

	[Test]
	public void EscapeSeconds_IsStepsOverSpeed()
	{
		Assert.AreEqual(5f, RetreatPathMath.EscapeSeconds(10, 2f), 0.0001f);
		Assert.AreEqual(0f, RetreatPathMath.EscapeSeconds(-3, 2f), 0.0001f);
		Assert.Greater(RetreatPathMath.EscapeSeconds(10, 0f), 10f, "속도 0이어도 0 나눗셈 없이 매우 느린 값");
	}

	// ── 휴식 자리·평가 보류·안전한 진출 방 (2-1·2-3장) ──

	[Test]
	public void RestSlot_PicksNearestToCenterAndClaimsIt()
	{
		var bounds = new RectInt(0, 0, 5, 5);
		var claimed = new HashSet<Vector2Int>();
		Assert.IsTrue(PartyRetreatMath.TryPickRestSlot(bounds, p => true, claimed, out var first));
		Assert.AreEqual(new Vector2Int(2, 2), first, "5×5 방의 중심 칸");
		Assert.IsTrue(PartyRetreatMath.TryPickRestSlot(bounds, p => true, claimed, out var second));
		Assert.AreNotEqual(first, second, "이미 잡힌 자리는 다시 고르지 않는다");
		Assert.AreEqual(2, claimed.Count);
	}

	[Test]
	public void RestSlot_SkipsInvalidTilesAndFailsWhenNone()
	{
		var bounds = new RectInt(0, 0, 3, 3);
		var claimed = new HashSet<Vector2Int>();
		Assert.IsTrue(PartyRetreatMath.TryPickRestSlot(bounds, p => p.x == 0, claimed, out var slot));
		Assert.AreEqual(0, slot.x);
		Assert.IsFalse(PartyRetreatMath.TryPickRestSlot(bounds, p => false, claimed, out _));
	}

	[Test]
	public void RestEvaluation_WaitsForStragglersUntilGrace()
	{
		Assert.IsTrue(PartyRetreatMath.RestEvaluationReady(everyoneArrived: true, firstArrivalTime: 10f, now: 10f, graceSeconds: 10f), "전원 도착 시 즉시 평가");
		Assert.IsFalse(PartyRetreatMath.RestEvaluationReady(false, 10f, 15f, 10f), "도착 못 한 인원이 있으면 유예 동안 대기");
		Assert.IsTrue(PartyRetreatMath.RestEvaluationReady(false, 10f, 20f, 10f), "유예가 지나면 도착자만으로 평가");
		Assert.IsTrue(PartyRetreatMath.RestEvaluationReady(false, 10f, 10f, 0f), "유예 0 = 문서 그대로 도착자만 평균");
	}

	[Test]
	public void SafeAdvanceRoom_NeedsKnownRoomWithLowDanger()
	{
		Assert.IsTrue(PartyRetreatMath.IsSafeAdvanceRoom(true, 100f, 100f));
		Assert.IsFalse(PartyRetreatMath.IsSafeAdvanceRoom(true, 100.1f, 100f));
		Assert.IsFalse(PartyRetreatMath.IsSafeAdvanceRoom(false, 0f, 100f), "지도로 모르는 방은 안전하다고 보지 않는다");
	}
}
#endif
