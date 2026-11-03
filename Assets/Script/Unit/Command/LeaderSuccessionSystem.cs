using System;
using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 리더 선정·지휘 상실·후임 승계(리더 명령 설계명세서 2·17·17-1장)의 부수효과 호출부. 순수 판정은 LeaderMath에 있다.
// 흐름: 리더 사망/개인 도주 확정 → 즉시 그 리더의 명령·버프·집결·공동 이동·진형·파티 후퇴 종료(OnLeaderLost) → 파티 공통 10초(사망은 실제 사망 시각, 도주는 소속 생존 파티원의 최초 확인 시각) → 그 시점의 생존·기본 통솔력·도주 상태로 후임 선정(Resolve).
public static class LeaderSuccessionSystem
{
	// 테스트가 시계를 주입한다.
	public static Func<float> Clock = () => Time.time;
	private static float Now => Clock();

	// ─────────────────────────── 선정 ───────────────────────────

	// 지금 이 파티에서 리더가 될 최적 후보 — 생존·도주 미확정·기본 통솔력 10 이상 중 기본 통솔력 → 등급 → 고정 난수 순. 후보가 없으면 null(리더 없이 행동).
	// 시스템을 끈 상태(kill-switch)에서는 예전처럼 생존자 중 통솔력 최댓값을 자격 없이 고른다.
	public static Human PickBest(Party party)
	{
		bool legacy = !LeaderCommandSettings.Enabled;
		Human best = null;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f) continue;
			bool fleeing = m.Retreat != null && m.Retreat.IsFleeing;
			float baseLeadership = m.BaseStat.leadership;
			if (!legacy && !LeaderMath.IsLeaderEligible(baseLeadership, true, fleeing)) continue;
			if (legacy && fleeing) continue;
			if (best == null || Compare(m, best) < 0) best = m;
		}
		return best;
	}

	private static int Compare(Human a, Human b)
		=> LeaderMath.CompareLeaderCandidates(
			a.BaseStat.leadership, LeaderCommandTable.Grade(a.unitType?.typeName), a.PassTieBreak,
			b.BaseStat.leadership, LeaderCommandTable.Grade(b.unitType?.typeName), b.PassTieBreak);

	// 지금 지휘 중인 일반 리더 또는 상위 지휘관인가 — 정신력 감소 완화(3-1장)의 적용 대상. 두 직책을 겸해도 한 번만 적용하므로 bool이다.
	public static bool IsCommanding(Human human)
	{
		if (human == null || !LeaderCommandSettings.Enabled) return false;
		if (human.LeaderState != null && human.LeaderState.IsSuperiorCommander) return true;
		return human.party != null && human.party.Leader == human;
	}

	// ─────────────────────────── 지휘 상실 ───────────────────────────

	// GameSession.CheckPartyWaveState가 인류가 죽을 때마다 부른다 — 죽은 유닛이 현재 일반 리더면 지휘 상실을 시작한다.
	public static void OnMemberDied(Human dead)
	{
		if (dead == null) return;
		if (dead.LeaderState != null && dead.LeaderState.IsSuperiorCommander) ExpeditionCommandSystem.OnSuperiorLost(dead, LeaderLossReason.Death); // 상위 지휘관 직책은 자동 승계하지 않는다 — 일반 리더를 겸했다면 아래에서 일반 직책만 후임을 정한다
		var party = dead.party;
		if (party == null) return;
		if (party.Leader == dead) OnLeaderLost(party, dead, LeaderLossReason.Death);
		else if (party.Leader != null && party.Leader.hp <= 0f) OnLeaderLost(party, party.Leader, LeaderLossReason.Death); // 안전망 — 다른 경로로 죽은 리더
	}

	// FleeSystem.ConfirmFlee가 개인 도주 확정 직후 부른다(도주 목격 감소 처리보다 먼저 — 아래 OnFleeFirstKnown이 대기 상태를 봐야 한다).
	public static void OnLeaderFled(Human human)
	{
		if (human == null) return;
		if (human.LeaderState != null && human.LeaderState.IsSuperiorCommander) ExpeditionCommandSystem.OnSuperiorLost(human, LeaderLossReason.Flee);
		var party = human.party;
		if (party == null || party.Leader != human) return;
		OnLeaderLost(party, human, LeaderLossReason.Flee);
	}

	// 일반 리더의 지휘를 끝낸다. 10초 시계: 사망은 지금(실제 사망 시각)부터, 도주는 소속 생존 파티원이 처음 확인할 때부터.
	public static void OnLeaderLost(Party party, Human leader, LeaderLossReason reason)
	{
		if (party == null || leader == null) return;

		if (!LeaderCommandSettings.Enabled)
		{
			// kill-switch: 예전 동작 — 즉시 재선정.
			if (party.Leader == leader) party.Leader = null;
			party.AssignLeaderIfNeeded();
			return;
		}

		var st = party.Command;
		if (st.SuccessionPending || party.Leader != leader) return; // 이미 지휘 상실 처리 중이거나 이 유닛은 현재 리더가 아니다

		string name = leader.name;
		party.Leader = null;
		st.StartSuccession(reason, name);
		if (reason == LeaderLossReason.Death) st.StartClock(Now);
		else st.FledLeaderNames.Add(name);

		EndLeaderAuthority(party, name);
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [지휘] 리더 {name} 지휘 상실({(reason == LeaderLossReason.Death ? "사망" : "개인 도주 확정")}) — 명령·기본 버프·집결·공동 이동·파티 후퇴 종료, {(reason == LeaderLossReason.Death ? "지금부터" : "소속 생존 파티원의 최초 확인 후")} {LeaderMath.SuccessionDelaySeconds:F0}초 뒤 후임 선정");
	}

	// 잃은 지휘자의 권한으로 돌던 것을 끝낸다(17-표1 "실제 사망 즉시"). 명령·버프의 정리는 각 시스템이 이 호출에 합류한다.
	private static void EndLeaderAuthority(Party party, string leaderName)
	{
		party.EndLeaderDrivenActivities();
		PartyRetreatSystem.EndByLeaderLoss(party, leaderName);
		CommandIssueSystem.OnLeaderLost(party, leaderName); // 집중 공격 지정·강공격 계기·제압 기억과 파티원이 받은 그 리더의 명령을 모두 끝낸다
		LeaderBuffSystem.RefreshParty(party);               // 리더 기본 버프 종료(상위 지휘관의 유효 버프가 있으면 그쪽만 남는다)
	}

	// MentalSystem.OnFleeKnown이 어떤 파티원이 도주 사실을 처음 알게 된 순간마다 부른다 — 대기 중인 일반 리더 도주면 첫 확인에서 파티 공통 10초가 시작된다(17-1-표1). 뒤늦은 확인은 시계를 다시 시작하지 않는다.
	public static void OnFleeFirstKnown(Human receiver, string fleeingName)
	{
		var party = receiver?.party;
		if (party == null || receiver.hp <= 0f || receiver.name == fleeingName) return;
		var st = party.Command;
		if (!st.SuccessionPending || st.PendingReason != LeaderLossReason.Flee || st.LostLeaderName != fleeingName) return;
		if (st.SuccessionClockStarted) return;

		st.StartClock(Now);
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [지휘] {receiver.name}이(가) 리더 {fleeingName}의 도주를 처음 확인 — 후임 선정까지 {LeaderMath.SuccessionDelaySeconds:F0}초");
	}

	// ─────────────────────────── 틱·후임 결정 ───────────────────────────

	// 승계 대기 중인 파티의 시계를 확인한다 — UnitFunction.OnUpdate에서 구성원이 부르며(대기 중이 아니면 즉시 반환) 어느 구성원이 불러도 같은 결과다.
	public static void TickParty(Party party)
	{
		var st = party?.Command;
		if (st == null || !st.SuccessionPending || !st.SuccessionClockStarted) return;
		if (!LeaderMath.IsSuccessionDue(st.SuccessionStartedAt, Now)) return;
		Resolve(party);
	}

	// 10초가 지났다 — 그 시점의 상태로 후임을 정한다. 이전 명령은 복원하지 않는다(후임은 현재 상황으로 새로 판단).
	public static void Resolve(Party party)
	{
		var st = party.Command;
		string lost = st.LostLeaderName;
		Human best = PickBest(party);
		st.FinishSuccession();

		if (best != null)
		{
			party.Leader = best;
			st.LeaderLastSeen.Clear(); // 후임이 직접 본 위치만 쓴다
			st.SuccessionCount++;
			st.NoSuccessor = false;
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [지휘] 후임 선정 — {best.name}(기본 통솔력 {best.BaseStat.leadership:F0}, 현재 {best.LeaderState.CurrentLeadership:F0}). 이전 리더 {lost}의 명령은 복원하지 않습니다");
			SpeechSystem.Say(best, SpeechId.SuccessorTakesCommand);
			LeaderBuffSystem.RefreshParty(party); // 후임의 기본 통솔력으로 버프 자격을, 현재 통솔력으로 강도를 다시 계산한다(종류는 파티 컨셉이 유지)
			return;
		}

		st.NoSuccessor = true;
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [지휘] 후임 없음 — 자격(생존·도주 미확정·기본 통솔력 {LeaderMath.MinLeaderBaseLeadership:F0} 이상)을 갖춘 파티원이 없어 리더 없이 소속·임무를 유지합니다");
		SpeechSystem.Say(PickSurvivorSpeaker(party), SpeechId.CommandCollapse);
	}

	private static Human PickSurvivorSpeaker(Party party)
	{
		var alive = new List<Human>();
		foreach (var m in party.Members)
			if (m != null && m.hp > 0f && !(m.Retreat != null && m.Retreat.IsFleeing)) alive.Add(m);
		return alive.Count == 0 ? null : alive[UnityEngine.Random.Range(0, alive.Count)];
	}
}
