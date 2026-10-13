using System;
using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 파티 후퇴의 전이를 구동한다(후퇴도주 명세서 1~2장): 시작 판정 → 명령 전달 → 거점 이동 → 도착·휴식 → 휴식 완료 뒤 출구 이동/재탐험/재출격. 단계 값은 Party.Retreat, 유닛별 값은 Human.Retreat에 있고 순수 판정은 PartyRetreatMath에 있다.
// 이동 자체는 RetreatFSMState/RetreatMovement가, 휴식 중 행동은 TacticalFSMState의 Rest 분기가 맡는다. 구동은 Party.RetreatDriver(리더, 없으면 던전 안 생존자 첫 번째)의 RetreatSystem.Tick이 0.5초마다 한다.
public static class PartyRetreatSystem
{
	public static event Action<Party> OnPartyRetreatStarted;

	// ─────────────────────────── 전이 틱 ───────────────────────────

	public static void Tick(Party party, float now)
	{
		var st = party.Retreat;
		if (!st.IsRetreating)
		{
			EvaluateStart(party, now);
			return;
		}

		ValidateBase(party, now);
		DeliverOrders(party);
		UpdateArrivals(party, now);
		if (st.Phase == PartyRetreatPhase.Resting) TickRest(party, now);
	}

	// ─────────────────────────── 시작 ───────────────────────────

	// 파티 후퇴 시작 기준(명세서 1-1~1-3장): 집결·동행 중인 생존 파티원의 평균 HP·정신력 비율이 종류별 기준 이하면 리더의 후퇴 명령으로 이어진다. 이미 시작한 후퇴는 재발령·초기화하지 않는다. 리더가 없으면 개인 후퇴 규칙이다. 시작했으면 true.
	public static bool EvaluateStart(Party party, float now)
	{
		var st = party.Retreat;
		if (st.IsRetreating) return false;
		var leader = party.Leader;
		if (leader == null || leader.hp <= 0f || leader.currentFloor < 1) return false; // 리더 없는 인류는 개인 후퇴 규칙

		var avg = party.ComputeRetreatAverages();
		var thresholds = PartyRetreatMath.ThresholdsFor(party.Type);
		if (!PartyRetreatMath.ShouldRetreat(avg, thresholds)) return false;

		Begin(party, leader, avg, thresholds, now);
		return true;
	}

	static void Begin(Party party, Human leader, PartyRetreatMath.Averages avg, PartyRetreatMath.Thresholds thresholds, float now)
	{
		var st = party.Retreat;
		st.Mode = RetreatMode.PartyRetreat;
		st.StartedAt = now;
		st.RetreatCount++;
		st.StartReason = avg.HpPercent <= thresholds.HpPercent + PartyRetreatMath.BoundaryEpsilon ? "평균 HP" : "평균 정신력";
		st.RestStartedAt = -1f;
		st.ClaimedRestSlots.Clear();
		st.Reexploring = false;

		var bases = new List<Room>();
		RetreatBases.Collect(leader.Session, leader.currentFloor, bases);
		Room chosen = RetreatBases.ChooseNearest(leader, bases);
		st.Destination = PartyRetreatMath.ChooseDestination(party.Type, st.NormalRestCompleted, hasDefenseBase: false, hasBasicBase: chosen != null, out st.PreferredBaseKind);
		st.BaseRoom = st.Destination == RetreatDestination.Base ? chosen : null;
		st.Phase = st.Destination == RetreatDestination.Base ? PartyRetreatPhase.Moving : PartyRetreatPhase.Leaving;

		party.AbortActivitiesForRetreat();
		foreach (var m in party.Members)
			if (m != null) m.Retreat?.ClearPartyRetreatState();
		DeliverOrders(party);

		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [후퇴] 파티 후퇴 시작({st.StartReason}) — {party.Type.ToKorean()} 파티, 평균 HP {avg.HpPercent:F1}%(기준 ≤{thresholds.HpPercent:F0}) / 평균 정신력 {avg.MentalPercent:F1}%(기준 ≤{thresholds.MentalPercent:F0}), 대상 {avg.Count}명, 목적지 {DescribeDestination(st)}, 후퇴 {st.RetreatCount}회째");
		OnPartyRetreatStarted?.Invoke(party);
	}

	static string DescribeDestination(PartyRetreatState st)
		=> st.Destination == RetreatDestination.Base && st.BaseRoom != null ? $"거점(방 {st.BaseRoom.RoomId})" : "던전 출구";

	// ─────────────────────────── 명령 전달·개인 활동 접기 ───────────────────────────

	// 후퇴 명령은 집결 명령과 같은 전달 범위 규칙을 따른다(Party.IsReachedByLeaderCommand) — 못 받은 구성원은 개인 행동으로 남고, 범위에 들어오면 다음 틱에 받는다. 도주 확정자는 명령 대상이 아니다.
	static void DeliverOrders(Party party)
	{
		var st = party.Retreat;
		var driver = party.RetreatDriver;
		if (driver == null) return;

		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f || m.currentFloor < 1) continue;
			var rs = m.Retreat;
			if (rs == null || rs.Ordered || rs.IsFleeing) continue;
			if (m != driver && !Party.IsReachedByLeaderCommand(driver, m)) continue;

			rs.Ordered = true;
			InterruptActivities(m);
			if (st.Destination == RetreatDestination.Base && st.BaseRoom != null)
				RetreatBases.EnsureRestSlot(m, st.BaseRoom, st.ClaimedRestSlots);
		}
	}

	// 후퇴·도주를 시작하는 유닛이 하던 비전투 활동을 접는다 — 집결·공동 이동 대기, 진형, 조사, 함정 대응, 문/함정 채널링, 계단 예약, 경계. 소리 조사·새 수집·중간 휴식을 위해 정지하지 않는다(명세서 5-1장).
	public static void InterruptActivities(Human m)
	{
		m.currentWait = null;
		m.waitStuckTurns = 0;
		m.idleWait = null;
		m.currentFormation = null;
		m.currentJoinCombatWait = null;
		m.currentInvestigation = null;
		m.currentAlertSearch = null;
		m.pendingStairTargetFloor = null;
		m.ClearAttackObjectTarget();
		TrapPartySystem.EndResponse(m, TrapEndReason.Retreat);
	}

	// ─────────────────────────── 거점 검증·도착 ───────────────────────────

	// 이동·휴식 중 거점을 잃으면(방 소유권이 인류가 아니게 됨) 다른 거점으로 바꾸고, 없으면 출구로 간다(명세서 8-2장 "거점 상실").
	internal static void ValidateBase(Party party, float now = 0f)
	{
		var st = party.Retreat;
		if (st.Destination != RetreatDestination.Base) return;

		bool usable = RetreatBases.IsUsable(st.BaseRoom);
		// 아직 아무도 못 닿은 거점은 5초마다 기준 경로(적 점유 제외)로 닿을 수 있는지 다시 본다 — 벽·상대 진영 문으로 영영 닿지 못하는 거점에 계속 가려 하지 않게 한다(명세서 5-2장 "실제 이용 가능 여부 확인").
		if (usable && st.Phase == PartyRetreatPhase.Moving && now >= st.NextReachCheckTime)
		{
			st.NextReachCheckTime = now + 5f;
			var checker = party.RetreatDriver;
			if (checker != null && checker.Session != null && !RetreatBases.IsReachable(checker, st.BaseRoom)) usable = false;
		}
		if (usable) return;

		var driver = party.RetreatDriver;
		var bases = new List<Room>();
		if (driver != null) RetreatBases.Collect(driver.Session, driver.currentFloor, bases);
		Room replacement = driver != null ? RetreatBases.ChooseNearest(driver, bases) : null;

		foreach (var m in party.Members)
			if (m?.Retreat != null) { m.Retreat.AtBase = false; m.Retreat.RestSlot = null; m.Retreat.Move.Reset(); }
		st.ClaimedRestSlots.Clear();
		st.RestStartedAt = -1f;

		if (replacement != null)
		{
			st.BaseRoom = replacement;
			st.Phase = PartyRetreatPhase.Moving;
			foreach (var m in party.Members)
				if (m != null && m.hp > 0f && m.Retreat != null && m.Retreat.Ordered) RetreatBases.EnsureRestSlot(m, replacement, st.ClaimedRestSlots);
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [후퇴] 거점을 잃어 다른 거점(방 {replacement.RoomId})으로 바꿉니다");
		}
		else
		{
			ToLeaving(party, "거점을 잃었고 이용 가능한 다른 거점이 없음");
		}
	}

	// 거점 도착 갱신: 휴식 자리에서 가까워지면 휴식 대상(AtBase)이 된다. 첫 도착자가 생기면 휴식 단계로 넘어간다.
	static void UpdateArrivals(Party party, float now)
	{
		var st = party.Retreat;
		if (st.Destination != RetreatDestination.Base || st.BaseRoom == null) return;

		int radius = RetreatSystem.BaseArrivalRadius;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f || m.Retreat == null || !m.Retreat.Ordered || m.Retreat.IsFleeing) continue;

			// 도착은 휴식 자리 곁(radius)에서, 이탈은 더 넓은 반경(radius+3) 밖에서만 판정한다 — 거점에서 교전하다 조금 밀려난 인원이 곧바로 "미도착"이 되어 후퇴 이동으로 전투를 끊고 끌려가지 않게 한다. 적을 인지한 동안은 도착 상태를 유지한다(후퇴 목적 거점에 도착한 병력은 현장 전투에 참여, 명세서 8장).
			bool arrived = m.Retreat.AtBase
				? (m.personalSpottedEnemies.Count > 0 || RetreatBases.HasArrived(m, st.BaseRoom, radius + 3))
				: RetreatBases.HasArrived(m, st.BaseRoom, radius);
			if (arrived == m.Retreat.AtBase) continue;
			m.Retreat.AtBase = arrived;
			if (arrived)
			{
				m.Retreat.AtBaseSince = now;
				m.Retreat.Move.Reset();
				if (st.RestStartedAt < 0f)
				{
					st.RestStartedAt = now;
					st.Phase = PartyRetreatPhase.Resting;
					LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [후퇴] 거점에 첫 도착({m.name}) — 휴식 시작");
				}
			}
		}
	}

	// ─────────────────────────── 휴식 완료 판정 ───────────────────────────

	internal static void TickRest(Party party, float now)
	{
		var st = party.Retreat;
		var rested = new List<Human>();
		int ordered = 0;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f || m.Retreat == null || !m.Retreat.Ordered || m.Retreat.IsFleeing) continue;
			ordered++;
			if (m.Retreat.AtBase) rested.Add(m);
		}
		if (rested.Count == 0) return;

		if (!PartyRetreatMath.RestEvaluationReady(rested.Count == ordered, st.RestStartedAt, now, RetreatSystem.RestStragglerGraceSeconds)) return;

		var avg = Party.AveragesOf(rested);
		if (!PartyRetreatMath.IsRestComplete(avg)) return;

		if (party.Type == PartyType.Occupy) { TryResortie(party, rested, avg, now); return; }

		st.NormalRestCompleted = true; // 같은 침공에서 거점 휴식은 완료 기준으로 1회 — 재탐험 뒤 다시 후퇴하면 재휴식 없이 출구
		bool goal = party.IsMissionGoalAchieved;
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [후퇴] 거점 휴식 완료(평균 HP {avg.HpPercent:F1}% / 정신력 {avg.MentalPercent:F1}%, 대상 {avg.Count}명) — 목표 {(goal ? "달성" : "미달성")}");
		if (PartyRetreatMath.DecidePostRest(goal) == PostRestAction.LeaveDungeon) ToLeaving(party, "목표 달성 후 휴식 완료");
		else Resume(party, resortie: false);
	}

	// 점령 파티: 휴식 완료 + 현장 생존 인원 80% 이상 + 인류 지도상 안전한 진출 방이 있어야 재출격한다(2-3장). 조건이 안 되면 거점 대기를 유지한다.
	static void TryResortie(Party party, List<Human> rested, PartyRetreatMath.Averages avg, float now)
	{
		var st = party.Retreat;
		var leader = party.Leader != null && party.Leader.hp > 0f ? party.Leader : rested[0];
		bool safeRoom = RetreatBases.HasSafeAdvanceRoom(leader, st.BaseRoom, RetreatSystem.ResortieSafeRoomMaxDanger);
		if (!PartyRetreatMath.CanResortie(avg, rested.Count, st.InitialSortieCount, safeRoom))
		{
			if (now >= st.NextResortieLogTime)
			{
				st.NextResortieLogTime = now + 15f;
				LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [후퇴] 재출격 대기 — 현장 생존 {rested.Count}/{PartyRetreatMath.RequiredResortieSurvivors(st.InitialSortieCount)}명 필요, 안전한 진출 방 {(safeRoom ? "있음" : "없음")}");
			}
			return;
		}
		st.ResortieCount++;
		Resume(party, resortie: true);
	}

	// ─────────────────────────── 종료·복귀 ───────────────────────────

	// 출구로 이동 단계로 바꾼다 — 휴식을 마친 목표 달성 파티, 거점 상실.
	internal static void ToLeaving(Party party, string reason)
	{
		var st = party.Retreat;
		st.Destination = RetreatDestination.Exit;
		st.Phase = PartyRetreatPhase.Leaving;
		st.BaseRoom = null;
		st.ClaimedRestSlots.Clear();
		foreach (var m in party.Members)
		{
			var rs = m?.Retreat;
			if (rs == null) continue;
			rs.AtBase = false;
			rs.RestSlot = null;
			rs.Move.Reset();
		}
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [후퇴] 던전 출구로 이동 — {reason}");
	}

	// 웨이브 승리(목표 방 코어 파괴) 뒤 퇴각이 시작되면 진행 중이던 파티 후퇴(거점 이동·휴식)보다 우선해 곧바로 출구로 간다.
	public static void ForceLeaving(Party party)
	{
		var st = party?.Retreat;
		if (st == null || !st.IsRetreating || st.Phase == PartyRetreatPhase.Leaving) return;
		ToLeaving(party, "웨이브 승리 퇴각");
	}

	// 후퇴를 끝내고 평소 행동(탐험/전진)으로 돌아간다 — 목표 미달성 재탐험(Reexploring) 또는 점령 파티 재출격. 이후 후퇴 기준을 다시 채우면 일반 파티는 재휴식 없이 출구로 간다.
	internal static void Resume(Party party, bool resortie)
	{
		var st = party.Retreat;
		st.Mode = RetreatMode.None;
		st.Phase = PartyRetreatPhase.None;
		st.Destination = RetreatDestination.None;
		st.BaseRoom = null;
		st.RestStartedAt = -1f;
		st.ClaimedRestSlots.Clear();
		st.Reexploring = !resortie;
		foreach (var m in party.Members)
			m?.Retreat?.ClearPartyRetreatState();
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [후퇴] {(resortie ? $"재출격({st.ResortieCount}회째)" : "목표 미달성 — 탐험 재개")}");
	}
}
