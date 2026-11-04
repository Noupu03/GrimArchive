using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 리더 명령 설계명세서 v0.15 검증 사례(L01~L30)의 숫자·시간 예시를 순수 함수로 고정한 테스트. 게임 객체가 필요한 흐름(Unity 플레이)은 구현현황 문서의 체크리스트로 확인한다.
public class LeaderCommandMathTests
{
	// ── 3장·L02·L03: 기본/현재 통솔력 ──────────────────────────────────
	[Test]
	public void BasicBuff_QualificationUsesBaseLeadership_StrengthUsesCurrent()
	{
		Assert.IsFalse(LeaderMath.HasBasicBuff(19f));
		Assert.AreEqual(0f, LeaderMath.BasicBuffRate(19f, 19.9f), 1e-6f); // 기본 19 / 현재 19.9 → 버프 없음
		Assert.AreEqual(0.10f, LeaderMath.BasicBuffRate(20f, 20f), 1e-6f);
		Assert.AreEqual(0.105f, LeaderMath.BasicBuffRate(20f, 21f), 1e-6f);
		Assert.AreEqual(0.08f, LeaderMath.BasicBuffRate(20f, 16f), 1e-6f);  // 기본 20 / 현재 16 → 8%
		Assert.AreEqual(0f, LeaderMath.BasicBuffRate(10f, 8f), 1e-6f);      // 기본 10 / 현재 8 → 리더 자격은 있어도 버프는 없다
		Assert.AreEqual(0.195f, LeaderMath.BasicBuffRate(20f, 39f), 1e-6f);
		Assert.AreEqual(0.5f, LeaderMath.BasicBuffRate(100f, 100f), 1e-6f);
	}

	[TestCase(TacticalCommand.FocusAttack, 20f, true)]
	[TestCase(TacticalCommand.FocusAttack, 19.9f, false)]
	[TestCase(TacticalCommand.Strike, 40f, true)]
	[TestCase(TacticalCommand.Strike, 32f, false)]   // L03: 현재 통솔력 40 → 32이면 새 행동 우선 명령은 못 낸다
	[TestCase(TacticalCommand.Suppress, 39f, false)]
	[TestCase(TacticalCommand.Protect, 40f, true)]
	[TestCase(TacticalCommand.SuperiorFocusAttack, 60f, true)]
	[TestCase(TacticalCommand.SuperiorFocusAttack, 48f, false) ] // L03: 60 → 48이면 새 상위 명령은 못 낸다
	[TestCase(TacticalCommand.SignatureSkill, 80f, true)]
	[TestCase(TacticalCommand.SignatureSkill, 79f, false)]
	public void CommandRequiresCurrentLeadership(TacticalCommand command, float current, bool expected)
		=> Assert.AreEqual(expected, LeaderMath.MeetsLeadership(command, current));

	// ── 2-1장·L01: 리더 자격과 선정 ─────────────────────────────────────
	[Test]
	public void LeaderEligibility_RequiresAliveNotFledAndBaseLeadership10()
	{
		Assert.IsFalse(LeaderMath.IsLeaderEligible(9f, true, false));   // 전원 기본 통솔력 9 → 리더 없이 입장
		Assert.IsTrue(LeaderMath.IsLeaderEligible(10f, true, false));
		Assert.IsFalse(LeaderMath.IsLeaderEligible(50f, false, false)); // 사망
		Assert.IsFalse(LeaderMath.IsLeaderEligible(50f, true, true));   // 개인 도주 확정
	}

	[Test]
	public void LeaderCandidates_CompareBaseThenGradeThenFixedRandom()
	{
		// 기본 통솔력 11인 A와 19인 B → B가 우선(음수면 첫 번째가 우선)
		Assert.Less(LeaderMath.CompareLeaderCandidates(19f, 0f, 5, 11f, 0f, 1), 0);
		Assert.Greater(LeaderMath.CompareLeaderCandidates(11f, 0f, 1, 19f, 0f, 5), 0);
		// 기본 통솔력이 같으면 등급, 그것도 같으면 고정 난수(작은 쪽)
		Assert.Less(LeaderMath.CompareLeaderCandidates(30f, 2f, 9, 30f, 1f, 1), 0);
		Assert.Less(LeaderMath.CompareLeaderCandidates(30f, 1f, 1, 30f, 1f, 9), 0);
		Assert.AreEqual(0, LeaderMath.CompareLeaderCandidates(30f, 1f, 4, 30f, 1f, 4));
	}

	// ── 13-1장·L05: 전파 반경 ──────────────────────────────────────────
	[TestCase(0f, 3)] [TestCase(24f, 3)] [TestCase(25f, 4)] [TestCase(49f, 4)] [TestCase(50f, 5)] [TestCase(74f, 5)] [TestCase(75f, 6)] [TestCase(99f, 6)] [TestCase(100f, 7)]
	public void PropagationRadius_ByCurrentLeadership(float current, int expected)
		=> Assert.AreEqual(expected, LeaderMath.PropagationRadius(current));

	// ── 3-1장·L04: 리더 본인의 정신력 감소 완화 ─────────────────────────
	[Test]
	public void MentalMitigation_MatchesDocumentExamples()
	{
		Assert.AreEqual(14f, LeaderMath.MitigatedDecrease(20f, 60f), 1e-4f);              // 20 × 0.70
		Assert.AreEqual(9.5f, LeaderMath.MitigatedDecrease(20f * 0.95f, 100f), 1e-4f);    // 20 × 0.95 × 0.50
		Assert.AreEqual(17f, LeaderMath.MitigatedDecrease(20f, 30f), 1e-4f);              // 사건 당시 15% 완화(과거 통솔력 60을 쓰지 않는다)
		Assert.AreEqual(20f, LeaderMath.MitigatedDecrease(20f, 0f), 1e-4f);
		Assert.AreEqual(0.30f, LeaderMath.MitigationRate(60f), 1e-6f);
	}

	// ── 7장·L07: 쿨타임 ───────────────────────────────────────────────
	[Test]
	public void CooldownLengths_General10_Superior15_ActionApply5()
	{
		Assert.AreEqual(10f, LeaderMath.CooldownSecondsOf(TacticalCommand.FocusAttack));
		Assert.AreEqual(10f, LeaderMath.CooldownSecondsOf(TacticalCommand.Strike));
		Assert.AreEqual(10f, LeaderMath.CooldownSecondsOf(TacticalCommand.Suppress));
		Assert.AreEqual(10f, LeaderMath.CooldownSecondsOf(TacticalCommand.Protect));
		Assert.AreEqual(15f, LeaderMath.CooldownSecondsOf(TacticalCommand.SuperiorFocusAttack));
		Assert.AreEqual(0f, LeaderMath.CooldownSecondsOf(TacticalCommand.None)); // 집결·문 개방·파괴·이동·후퇴는 쿨타임이 없다
		Assert.AreEqual(5f, LeaderMath.ActionPriorityApplySeconds);
	}

	// ── 10·11장·L19·L20: 목표 점수와 신규/교체 ─────────────────────────
	[Test]
	public void CommandTargetScore_MatchesTable10()
	{
		Assert.AreEqual(130f, LeaderMath.CommandTargetScore(100f, PartyConcept.Adventurer, CombatRole.RangedSupport, TargetCategory.Normal), 1e-3f); // 모험가가 확인한 원거리 지원 일반 적
		Assert.AreEqual(130f, LeaderMath.CommandTargetScore(100f, PartyConcept.Rogue, CombatRole.RangedDps, TargetCategory.Normal), 1e-3f);
		Assert.AreEqual(100f, LeaderMath.CommandTargetScore(100f, PartyConcept.Rogue, CombatRole.RangedSupport, TargetCategory.Normal), 1e-3f);
		Assert.AreEqual(115f, LeaderMath.CommandTargetScore(100f, PartyConcept.Adventurer, CombatRole.RangedDps, TargetCategory.AttackBuilding), 1e-3f); // 공격형 방어건물 100 × 1.00 × 1.15
		Assert.AreEqual(150f, LeaderMath.CommandTargetScore(100f, PartyConcept.Army, CombatRole.MeleeTank, TargetCategory.Boss), 1e-3f);
		// 건물은 전투 역할이 없어 원거리 지원 배율 1.30을 받지 않는다
		Assert.AreEqual(115f, LeaderMath.CommandTargetScore(100f, PartyConcept.Adventurer, CombatRole.RangedSupport, TargetCategory.AttackBuilding), 1e-3f);
	}

	[Test]
	public void NewFocus_Requires1_5x_ReplaceRequires1_2x()
	{
		Assert.IsTrue(LeaderMath.MeetsNewFocusRatio(150f, 100f));   // 150점이면 충족
		Assert.IsFalse(LeaderMath.MeetsNewFocusRatio(149f, 100f));  // 149점이면 아니다
		Assert.IsFalse(LeaderMath.MeetsNewFocusRatio(0f, 0f));
		Assert.IsFalse(LeaderMath.MeetsReplaceRatio(100f, 119f));   // 119점이면 유지
		Assert.IsTrue(LeaderMath.MeetsReplaceRatio(100f, 120f));    // 120점이면 교체
		Assert.IsFalse(LeaderMath.MeetsReplaceRatio(0f, 0f));       // 양쪽 0은 유지
	}

	[Test]
	public void PickTopScored_ScoreFirst_ThenDistance_ThenFixedRandom()
	{
		var names = new List<string> { "A", "B", "C" };
		var score = new Dictionary<string, float> { ["A"] = 100f, ["B"] = 120f, ["C"] = 120f };
		var dist = new Dictionary<string, float> { ["A"] = 1f, ["B"] = 5f, ["C"] = 3f };
		var tie = new Dictionary<string, int> { ["A"] = 0, ["B"] = 0, ["C"] = 1 };
		string top = CommandSelectionMath.PickTopScored(names, n => score[n], n => dist[n], n => tie[n], out float topScore, out float second);
		Assert.AreEqual("C", top);          // 최고 점수 동률이면 리더에게 가까운 적(C)
		Assert.AreEqual(120f, topScore);
		Assert.AreEqual(120f, second);      // 두 번째 점수도 120이라 신규 1.5배 조건은 불충족
		dist["B"] = 3f;                     // 거리도 같으면 고정 무작위 순서
		top = CommandSelectionMath.PickTopScored(names, n => score[n], n => dist[n], n => tie[n], out _, out _);
		Assert.AreEqual("B", top);
		// 점수가 낮아도 가까운 적이 먼저 뽑히지 않는다(점수보다 거리를 먼저 비교하지 않는다)
		Assert.AreEqual("B", CommandSelectionMath.PickTopScored(new List<string> { "A", "B" }, n => score[n], n => n == "A" ? 0f : 9f, n => 0, out _, out _));
	}

	// ── 5-1-표2·L15: 적 HP 30% 경계 ────────────────────────────────────
	[Test]
	public void LowHp_Boundary()
	{
		Assert.IsFalse(LeaderMath.IsLowHp(0.301f)); // 30.1%는 계기가 아니다
		Assert.IsTrue(LeaderMath.IsLowHp(0.30f));
		Assert.IsTrue(LeaderMath.IsLowHp(0.299f));
	}

	// ── 12-2장·L23: 추적 5초와 확인 대기 3초 ───────────────────────────
	[Test]
	public void Pursuit_TimesMatchL23()
	{
		Assert.AreEqual(5f, LeaderMath.PursuitRemaining(10f, 10f));
		Assert.AreEqual(3f, LeaderMath.PursuitRemaining(10f, 12f));
		Assert.AreEqual(0f, LeaderMath.PursuitRemaining(10f, 15f));
		Assert.AreEqual(3f, LeaderMath.ConfirmWaitSeconds(0f, 2f), 1e-4f); // 놓친 뒤 2초에 도착 → 확인 대기 3초
		Assert.AreEqual(1f, LeaderMath.ConfirmWaitSeconds(0f, 4f), 1e-4f); // 4초에 도착 → 최대 1초(전체 5초 안에 포함)
		Assert.AreEqual(0f, LeaderMath.ConfirmWaitSeconds(0f, 5f), 1e-4f);
	}

	// ── 11-1장·L21: 접근 제한 거리 ─────────────────────────────────────
	[Test]
	public void ApproachLimit_ExcludesShorterPaths_ZeroDisables()
	{
		Assert.IsFalse(LeaderMath.PassesApproachLimit(1f, 2f)); // B와 최소 1칸인 경로는 제외
		Assert.IsTrue(LeaderMath.PassesApproachLimit(2f, 2f));  // 같으면 통과
		Assert.IsTrue(LeaderMath.PassesApproachLimit(0.5f, 0f)); // 설정값 0은 추가 제한을 쓰지 않는다
		var me = new List<Vector2> { new Vector2(0.5f, 0.5f) };
		var other = new List<Vector2> { new Vector2(2.5f, 0.5f), new Vector2(3.5f, 0.5f) };
		Assert.AreEqual(2f, LeaderMath.MinCenterDistance(me, other), 1e-4f);
		Assert.IsTrue(float.IsPositiveInfinity(LeaderMath.MinCenterDistance(me, new List<Vector2>()))); // 알려진 다른 근접 적이 없으면 제한 없음
	}

	// ── 17·18장·L26·L28~L30: 승계 시각과 충격 감소량 ─────────────────────
	[Test]
	public void Succession_DueExactlyTenSecondsAfterClockStart()
	{
		Assert.IsFalse(LeaderMath.IsSuccessionDue(0f, 9.99f));
		Assert.IsTrue(LeaderMath.IsSuccessionDue(0f, 10f));
		// L30: 도주 확정 4초에 처음 확인 → 확정 후 14초에 후임 선정(Q의 9초 확인으로 19초로 늦추지 않는다)
		Assert.IsFalse(LeaderMath.IsSuccessionDue(4f, 13.9f));
		Assert.IsTrue(LeaderMath.IsSuccessionDue(4f, 14f));
	}

	[Test]
	public void LeaderDeathShock_ReplacesGeneralDeathLoss()
	{
		Assert.AreEqual(20f, LeaderMath.LeaderDeathMentalLoss(true));   // 직접 확인 −20(일반 사망 −10을 추가하지 않음)
		Assert.AreEqual(10f, LeaderMath.LeaderDeathMentalLoss(false));  // 전달받음 −10(일반 −5를 추가하지 않음)
		Assert.AreEqual(10f, LeaderMath.LeaderFleeMentalLoss);          // 리더 도주 −10(일반 −5 대체)
		Assert.AreEqual(0.20f, LeaderMath.DeathShockFraction);
		Assert.AreEqual(10f, LeaderMath.DeathShockSeconds);
	}

	// ── 5-2장: 집결 기한 ──────────────────────────────────────────────
	[Test]
	public void RallyDeadline_BaseEightSeconds_EtaPlusThreeWhenLater()
	{
		Assert.AreEqual(8f, LeaderMath.RallyDeadline(0f, float.NegativeInfinity), 1e-4f);
		Assert.AreEqual(8f, LeaderMath.RallyDeadline(0f, 3f), 1e-4f);   // ETA+3=6 < 8 → 기본 기한
		Assert.AreEqual(15f, LeaderMath.RallyDeadline(0f, 12f), 1e-4f); // ETA+3=15 > 8
		Assert.AreEqual(13f, LeaderMath.RallyDeadline(5f, 3f), 1e-4f);  // 명령 시각 5초 → 기본 기한 13(ETA+3=6보다 늦다)
		Assert.AreEqual(17f, LeaderMath.RallyDeadline(5f, 14f), 1e-4f); // ETA+3=17이 기본 기한 13보다 늦다
	}

	// R4: 도착 예정 시각 = 지금 + 남은 걸음 수 ÷ 걷기 속도(속도 하한 0.1, 음수 걸음은 0).
	[Test]
	public void RallyEta_StepsOverSpeed_ClampsSpeedAndNegativeSteps()
	{
		Assert.AreEqual(106f, LeaderMath.RallyEta(100f, 12, 2f), 1e-4f);
		Assert.AreEqual(100f, LeaderMath.RallyEta(100f, 0, 2f), 1e-4f);
		Assert.AreEqual(100f, LeaderMath.RallyEta(100f, -3, 2f), 1e-4f);
		Assert.AreEqual(110f, LeaderMath.RallyEta(100f, 1, 0f), 1e-3f, "속도가 0이어도 0.1로 올려 무한대를 만들지 않는다");
	}

	// 순서도 P02 "우선 위치": 리더와 같은 층의 마지막 확인 위치 중 가장 가까운 곳, 같으면 더 최근에 본 쪽. 후보가 없거나 다른 층뿐이면 −1.
	[Test]
	public void PickRallySearchTarget_NearestOnLeaderFloor_RecentWinsTie_NoneWhenNoCandidate()
	{
		var leader = new Vector2Int(5, 5);
		LeaderMath.RallySearchCandidate C(string n, int x, int y, int floor, float seen) => new LeaderMath.RallySearchCandidate { Name = n, Position = new Vector2Int(x, y), Floor = floor, SeenAt = seen };

		var list = new List<LeaderMath.RallySearchCandidate> { C("far", 20, 5, 1, 10f), C("near", 8, 5, 1, 5f), C("otherFloor", 5, 6, 2, 99f) };
		Assert.AreEqual(1, LeaderMath.PickRallySearchTarget(leader, 1, list), "가장 가까운 같은 층 후보 — 다른 층은 거리가 가까워도 제외");

		var tie = new List<LeaderMath.RallySearchCandidate> { C("near", 8, 5, 1, 5f), C("newer", 2, 5, 1, 9f) }; // 둘 다 거리 3
		Assert.AreEqual(1, LeaderMath.PickRallySearchTarget(leader, 1, tie), "거리가 같으면 더 최근에 본 쪽");

		Assert.AreEqual(-1, LeaderMath.PickRallySearchTarget(leader, 3, list), "같은 층 후보가 없으면 −1");
		Assert.AreEqual(-1, LeaderMath.PickRallySearchTarget(leader, 1, new List<LeaderMath.RallySearchCandidate>()));
		Assert.AreEqual(-1, LeaderMath.PickRallySearchTarget(leader, 1, null));
	}
}

public class CommandSelectionMathTests
{
	// 4-1장: 명령은 전파 범위 안에 수신자가 있을 때만 — 리더 혼자에게는 내리지 않는다.
	[Test]
	public void HasRecipient_RequiresAtLeastOneMember()
	{
		Assert.IsFalse(CommandSelectionMath.HasRecipient(0));
		Assert.IsTrue(CommandSelectionMath.HasRecipient(1));
	}

	// 5-1-표2: 강공격 계기는 강공격을 낸 때만 소비한다 — 제압·보호가 먼저 나가도 강공격 계기는 남는다.
	[Test]
	public void ConsumesStrikeTriggers_OnlyWhenStrikeItselfIsIssued()
	{
		Assert.IsTrue(CommandSelectionMath.ConsumesStrikeTriggers(TacticalCommand.Strike));
		Assert.IsFalse(CommandSelectionMath.ConsumesStrikeTriggers(TacticalCommand.Suppress));
		Assert.IsFalse(CommandSelectionMath.ConsumesStrikeTriggers(TacticalCommand.Protect));
		Assert.IsFalse(CommandSelectionMath.ConsumesStrikeTriggers(TacticalCommand.FocusAttack));
	}

	// ── 8장·L09: 컨셉별 선택 순서 ──────────────────────────────────────
	[Test]
	public void ActionPriorityOrder_MatchesTable8()
	{
		CollectionAssert.AreEqual(new[] { TacticalCommand.Protect, TacticalCommand.Suppress, TacticalCommand.Strike }, CommandSelectionMath.ActionPriorityOrder(PartyConcept.Adventurer));
		CollectionAssert.AreEqual(new[] { TacticalCommand.Strike, TacticalCommand.Suppress, TacticalCommand.Protect }, CommandSelectionMath.ActionPriorityOrder(PartyConcept.Rogue));
		CollectionAssert.AreEqual(new[] { TacticalCommand.Strike, TacticalCommand.Protect, TacticalCommand.Suppress }, CommandSelectionMath.ActionPriorityOrder(PartyConcept.Mercenary));
		CollectionAssert.AreEqual(new[] { TacticalCommand.Suppress, TacticalCommand.Protect, TacticalCommand.Strike }, CommandSelectionMath.ActionPriorityOrder(PartyConcept.Army));
		CollectionAssert.AreEqual(new[] { TacticalCommand.Protect, TacticalCommand.Strike, TacticalCommand.Suppress }, CommandSelectionMath.ActionPriorityOrder(PartyConcept.Hero));
	}

	[Test]
	public void PickActionPriority_FallsThroughToNextWhenUnavailable()
	{
		// 셋 다 가능: 모험가·용사=보호, 도적·용병=강공격, 군대=제압
		bool All(TacticalCommand c) => true;
		Assert.AreEqual(TacticalCommand.Protect, CommandSelectionMath.PickActionPriority(PartyConcept.Adventurer, All));
		Assert.AreEqual(TacticalCommand.Protect, CommandSelectionMath.PickActionPriority(PartyConcept.Hero, All));
		Assert.AreEqual(TacticalCommand.Strike, CommandSelectionMath.PickActionPriority(PartyConcept.Rogue, All));
		Assert.AreEqual(TacticalCommand.Strike, CommandSelectionMath.PickActionPriority(PartyConcept.Mercenary, All));
		Assert.AreEqual(TacticalCommand.Suppress, CommandSelectionMath.PickActionPriority(PartyConcept.Army, All));
		// 모험가: 보호 불가 → 제압, 제압도 불가 → 강공격, 셋 다 불가 → 명령하지 않음
		Assert.AreEqual(TacticalCommand.Suppress, CommandSelectionMath.PickActionPriority(PartyConcept.Adventurer, c => c != TacticalCommand.Protect));
		Assert.AreEqual(TacticalCommand.Strike, CommandSelectionMath.PickActionPriority(PartyConcept.Adventurer, c => c == TacticalCommand.Strike));
		Assert.AreEqual(TacticalCommand.None, CommandSelectionMath.PickActionPriority(PartyConcept.Adventurer, c => false));
	}

	// ── 5-1-표5·L17: 보호 긴급 조건 ────────────────────────────────────
	[Test]
	public void ProtectEmergency_LowHpUnderThreat_OrIncapacitatedBeingAttacked_NotDeathPrediction()
	{
		Assert.IsTrue(CommandSelectionMath.IsProtectEmergency(0.25f, true, false, false));   // 힐러 HP 25%이고 공격받음
		Assert.IsFalse(CommandSelectionMath.IsProtectEmergency(0.80f, true, false, false));  // 행동 가능한 궁수 HP 80% → 긴급 아님(제압 판단)
		Assert.IsFalse(CommandSelectionMath.IsProtectEmergency(0.25f, false, false, false)); // 위협이 없다
		Assert.IsTrue(CommandSelectionMath.IsProtectEmergency(1.0f, false, true, true));     // HP와 무관하게 행동 불능 + 공격받음
		Assert.IsFalse(CommandSelectionMath.IsProtectEmergency(1.0f, false, true, false));
		Assert.IsFalse(CommandSelectionMath.IsProtectEmergency(0.70f, true, false, true));   // HP 70%, 큰 피해 예상 → 사망 예측만으로는 보호하지 않는다
	}

	// ── 5-1-표3·표4·L18: 제압 대상 ─────────────────────────────────────
	[Test]
	public void SuppressTarget_KeepsPreviousIfInPool_ElseHighestScoreThenFixedRandom()
	{
		var a = "A"; var b = "B"; var c = "C";
		var score = new Dictionary<string, float> { [a] = 100f, [b] = 90f, [c] = 90f };
		var tie = new Dictionary<string, int> { [a] = 5, [b] = 1, [c] = 2 };
		var pool = new List<string> { a, b, c };
		Assert.AreSame(a, CommandSelectionMath.PickSuppressTarget(pool, a, s => score[s], s => tie[s]));   // 직전 대상이 풀에 있으면 다시 지정
		Assert.AreSame(a, CommandSelectionMath.PickSuppressTarget(pool, null, s => score[s], s => tie[s])); // 직전 대상 없음 → 점수 최고
		var backline = new List<string> { b, c };                                                           // 직전 대상 A는 첫 분류(후위 공격)가 아니라 풀 밖 → 기억만으로 먼저 고르지 않는다
		Assert.AreSame(b, CommandSelectionMath.PickSuppressTarget(backline, a, s => score[s], s => tie[s])); // 점수 동률이면 고정 무작위 순서가 앞선 B
		Assert.IsNull(CommandSelectionMath.PickSuppressTarget(new List<string>(), a, s => 0f, s => 0));
	}
}

// 강공격 계기(5-1-표2·L13·L15·L16) — 순수 상태기계.
public class StrikeTriggerTrackerTests
{
	private static StrikeTriggerTracker.EnemyView Enemy(int id, float hp = 1f, bool engaged = true, bool attackTarget = false, bool unlocked = true, bool incapacitated = false)
		=> new StrikeTriggerTracker.EnemyView { Id = id, IsEngagementTarget = engaged, IsAttackTarget = attackTarget, HpUnlocked = unlocked, HpRatio = hp, Incapacitated = incapacitated };

	private static readonly List<StrikeTriggerTracker.EnemyView> None = new List<StrikeTriggerTracker.EnemyView>();

	[Test]
	public void NewEngagement_FiresOnceUntilEngagementEnds()
	{
		var t = new StrikeTriggerTracker();
		t.Observe(false, null, None);
		Assert.IsFalse(t.HasValidTrigger);
		t.Observe(true, null, None);                 // 전투 중이 아니던 파티가 전투를 시작
		Assert.IsTrue(t.HasValidTrigger);
		t.ConsumeAll();                              // 강공격을 한 번 내렸다
		t.Observe(true, null, new[] { Enemy(1) });   // 이미 전투 중인 파티에 적이 추가된 사실만으로는 새 계기가 아니다
		Assert.IsFalse(t.HasValidTrigger);
		t.Observe(false, null, None);
		t.Observe(true, null, None);                 // 교전이 끝났다가 다시 시작하면 새 계기
		Assert.IsTrue(t.HasValidTrigger);
	}

	[Test]
	public void NewEngagement_CancelledWhenEngagementEndsBeforeIssue()
	{
		var t = new StrikeTriggerTracker();
		t.Observe(true, null, None);
		t.Observe(false, null, None);                // 명령 전에 전투 종료 → 해당 강공격은 내리지 않는다(L16)
		Assert.IsFalse(t.HasValidTrigger);
	}

	// 검증 재감사(5-1장): 계기가 있어도 리더가 교전 중인 적을 직접 확인하지 못하면 강공격을 판단하지 않는다. 계기는 소비되지 않고 리더가 보게 되면 그때 판단한다.
	[Test]
	public void CanJudge_RequiresLeaderToSeeEngagedEnemy_TriggerIsKept()
	{
		var t = new StrikeTriggerTracker();
		t.Observe(true, null, None);                          // 파티원만 교전 시작 — 리더가 본 적 없음
		Assert.IsTrue(t.HasValidTrigger);
		Assert.IsFalse(t.CanJudge, "리더가 교전 중인 적을 못 보면 판단하지 않는다");

		t.Observe(true, null, new[] { Enemy(1, engaged: false) });
		Assert.IsFalse(t.CanJudge, "보이는 적이 교전 중이 아니면 마찬가지");

		t.Observe(true, null, new[] { Enemy(1, engaged: true) });
		Assert.IsTrue(t.CanJudge, "리더가 교전 중인 적을 직접 확인하면 남아 있던 새 교전 계기를 판단");

		t.ConsumeAll();
		t.Observe(true, null, new[] { Enemy(1, engaged: true) });
		Assert.IsFalse(t.CanJudge, "소비한 계기는 다시 판단하지 않는다");
	}

	[Test]
	public void FocusAssigned_FiresPerRealAssignment_NotForResendOrScoreChange()
	{
		var t = new StrikeTriggerTracker();
		t.Observe(true, "c1", None);
		t.ConsumeAll();
		t.Observe(true, "c1", None);                 // 같은 지정의 재전달·점수 변화는 새 계기가 아니다
		Assert.IsFalse(t.FocusValid);
		t.Observe(true, "c2", None);                 // 교체로 새 지정
		Assert.IsTrue(t.FocusValid);
		t.Observe(true, null, None);                 // 지정이 사라지면 계기도 사라진다
		Assert.IsFalse(t.FocusValid);
	}

	[Test]
	public void LowHp_BoundaryAndOncePerEnemyPerEngagement()
	{
		var t = new StrikeTriggerTracker();
		t.Observe(true, null, None); t.ConsumeAll();            // 새 교전 계기는 비워 두고 저HP만 본다
		t.Observe(true, null, new[] { Enemy(7, hp: 0.301f) });
		Assert.IsFalse(t.LowHpValid);                            // 30.1%
		t.Observe(true, null, new[] { Enemy(7, hp: 0.30f) });
		Assert.IsTrue(t.LowHpValid);                             // 30%
		t.Observe(true, null, new[] { Enemy(8, hp: 0.25f) });    // 처음 봤을 때부터 25%인 다른 적도 포함
		Assert.IsTrue(t.LowHpValid);
		t.ConsumeAll();
		t.Observe(true, null, new[] { Enemy(8, hp: 0.25f) });    // 발령한 뒤 계속 25% → 다시 발령하지 않는다(적용시간·쿨타임이 지나도 초기화 안 됨)
		Assert.IsFalse(t.LowHpValid);
		t.Observe(true, null, new[] { Enemy(8, hp: 0.60f) });
		t.Observe(true, null, new[] { Enemy(8, hp: 0.25f) });    // 회복 후 같은 전투에서 다시 25%
		Assert.IsFalse(t.LowHpValid);
		t.Observe(false, null, None);                            // 교전이 끝나면 이력이 비워진다
		t.Observe(true, null, new[] { Enemy(8, hp: 0.25f) });
		Assert.IsTrue(t.LowHpValid);
	}

	[Test]
	public void LowHp_IgnoredWithoutHpUnlockOrWhenNotEngaged()
	{
		var t = new StrikeTriggerTracker();
		t.Observe(true, null, None); t.ConsumeAll();
		t.Observe(true, null, new[] { Enemy(1, hp: 0.20f, unlocked: false) }); // HP 정보 미해금 — 과거 20%를 현재값으로 삼지 않는다
		Assert.IsFalse(t.LowHpValid);
		t.Observe(true, null, new[] { Enemy(2, hp: 0.20f, engaged: false) });  // 교전 대상이 아니다
		Assert.IsFalse(t.LowHpValid);
	}

	[Test]
	public void Incapacitation_ValidOnlyWhileActive_NotRepeatedForSameStun()
	{
		var t = new StrikeTriggerTracker();
		t.Observe(true, null, None); t.ConsumeAll();
		t.Observe(true, null, new[] { Enemy(3, attackTarget: true, incapacitated: true) });
		Assert.IsTrue(t.IncapacitationValid);
		t.Observe(true, null, new[] { Enemy(3, attackTarget: true, incapacitated: false) }); // 발령 전에 상태가 해제되면 그 계기를 취소(L16)
		Assert.IsFalse(t.IncapacitationValid);
		t.Observe(true, null, new[] { Enemy(3, attackTarget: true, incapacitated: true) });
		t.ConsumeAll();
		t.Observe(true, null, new[] { Enemy(3, attackTarget: true, incapacitated: true) });  // 같은 행동 불능으로 반복하지 않는다
		Assert.IsFalse(t.IncapacitationValid);
		t.Observe(true, null, new[] { Enemy(3, attackTarget: true, incapacitated: false) });
		t.Observe(true, null, new[] { Enemy(3, attackTarget: true, incapacitated: true) });  // 풀렸다가 다시 걸리면 새 계기
		Assert.IsTrue(t.IncapacitationValid);
		t.Observe(true, null, new[] { Enemy(4, attackTarget: false, incapacitated: true) });  // 현재 공격 대상이 아닌 적의 행동 불능은 계기가 아니다
		Assert.IsFalse(t.IncapacitationValid);
	}

	[Test]
	public void ConsumeAll_ClearsEveryCurrentTriggerAtOnce_AndCancelDropsWithoutConsuming()
	{
		var t = new StrikeTriggerTracker();
		t.Observe(true, "c1", new[] { Enemy(1, hp: 0.2f, attackTarget: true, incapacitated: true) });
		Assert.IsTrue(t.NewEngagementValid && t.FocusValid && t.IncapacitationValid && t.LowHpValid);
		t.ConsumeAll();                              // 여러 계기를 명령 횟수로 누적하지 않는다
		Assert.IsFalse(t.HasValidTrigger);

		var t2 = new StrikeTriggerTracker();
		t2.Observe(true, "c1", None);
		t2.CancelPending();                          // 후퇴·지휘 상실로 취소
		Assert.IsFalse(t2.HasValidTrigger);
	}
}

// leader_commands.json 파싱(JsonUtility) — Unity 런타임이 필요하다(콘솔 하니스는 LEADER_HARNESS로 이 픽스처를 뺀다).
#if !LEADER_HARNESS
public class LeaderCommandTableTests
{
	private const string Json = @"{
		""units"": [ { ""typeName"": ""성기사"", ""leadership"": 60, ""grade"": 1, ""commands"": [""FocusAttack"", ""Strike"", ""Bogus""] },
		              { ""typeName"": ""아처형"", ""leadership"": 150, ""commands"": [] } ],
		""skills"": [ { ""skillName"": ""방패 타격"", ""links"": [ { ""command"": ""Suppress"", ""rank"": 1 }, { ""command"": ""Protect"", ""rank"": 3 }, { ""command"": ""FocusAttack"", ""rank"": 1 } ] } ]
	}";

	[SetUp] public void Load() => LeaderCommandTable.LoadFromJson(Json);
	[TearDown] public void Reset() => LeaderCommandTable.ResetForTests();

	[Test]
	public void Parses_Leadership_Commands_Grade_AndClampsTo100()
	{
		Assert.AreEqual(60f, LeaderCommandTable.BaseLeadership("성기사"));
		Assert.AreEqual(100f, LeaderCommandTable.BaseLeadership("아처형")); // 0~100으로 제한
		Assert.AreEqual(0f, LeaderCommandTable.BaseLeadership("없는 유닛")); // 표에 없으면 0 → 리더 자격 없음
		Assert.AreEqual(1f, LeaderCommandTable.Grade("성기사"));
		Assert.IsTrue(LeaderCommandTable.Holds("성기사", TacticalCommand.Strike));
		Assert.IsFalse(LeaderCommandTable.Holds("성기사", TacticalCommand.Protect));
		Assert.IsFalse(LeaderCommandTable.Holds("아처형", TacticalCommand.FocusAttack));
	}

	[Test]
	public void SkillLinks_OnlyForActionPriorityCommands_WithRank()
	{
		Assert.IsTrue(LeaderCommandTable.TryGetRank("방패 타격", TacticalCommand.Suppress, out int r1)); Assert.AreEqual(1, r1);
		Assert.IsTrue(LeaderCommandTable.TryGetRank("방패 타격", TacticalCommand.Protect, out int r2)); Assert.AreEqual(3, r2);
		Assert.IsFalse(LeaderCommandTable.TryGetRank("방패 타격", TacticalCommand.Strike, out _));        // 미지정 후보는 그 명령에 포함하지 않는다
		Assert.IsFalse(LeaderCommandTable.TryGetRank("방패 타격", TacticalCommand.FocusAttack, out _));   // 행동 우선 명령이 아닌 연결은 무시
		Assert.IsFalse(LeaderCommandTable.TryGetRank("없는 스킬", TacticalCommand.Suppress, out _));
	}
}
#endif
