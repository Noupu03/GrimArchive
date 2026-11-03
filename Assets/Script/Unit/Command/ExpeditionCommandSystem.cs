using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 상위 지휘관과 원정 단위 지휘(리더 명령 9·16장). 상위 지휘관은 원정 편성 단계에서 별도로 임명하며 원정당 최대 한 명이고, 일반 리더는 자기 파티를, 상위 지휘관은 같은 원정의 여러 소속 파티를 지휘한다.
// 원정 = 같은 웨이브가 던전에 들여보낸 파티들(점령 파티는 웨이브와 따로 상주해 제외). 파티 구성 문서가 아직 없어 자동 임명은 없고 Designate(코드 API·debug 메뉴)로만 지정한다.
// 상위 지휘관이 사망·개인 도주를 확정하면 상위 명령·상위 기본 버프가 끝나고 자동 승계하지 않는다(16-2장).
public static class ExpeditionCommandSystem
{
	private const float EvaluateIntervalSeconds = 0.25f;
	public static System.Func<float> Clock = () => Time.time;

	private static float _nextEvaluate;
	private static readonly List<Party> _parties = new List<Party>();
	private static readonly List<Human> _direct = new List<Human>();

	// ─────────────────────────── 지정·조회 ───────────────────────────

	// 현재 원정의 상위 지휘관(살아 있고 도주 미확정인 사람) — 없으면 null.
	public static Human FindSuperior(GameSession session)
	{
		if (session?.parties == null) return null;
		foreach (var p in session.parties)
		{
			if (p == null) continue;
			foreach (var m in p.Members)
				if (m != null && m.hp > 0f && m.LeaderState != null && m.LeaderState.IsSuperiorCommander && !(m.Retreat != null && m.Retreat.IsFleeing)) return m;
		}
		return null;
	}

	public static bool IsSuperiorCommanding(string name)
	{
		var session = GameSession.Instance;
		var superior = FindSuperior(session);
		return superior != null && superior.name == name;
	}

	// 상위 지휘관으로 임명한다 — 원정당 최대 한 명이라 기존 지정은 해제한다. 임명은 승계가 아니라 편성 결정이다.
	public static void Designate(Human human)
	{
		if (human == null || human.LeaderState == null || !LeaderCommandSettings.Enabled) return;
		var session = human.Session;
		if (session?.parties != null)
			foreach (var p in session.parties)
				if (p != null)
					foreach (var m in p.Members)
						if (m != null && m != human && m.LeaderState != null) m.LeaderState.IsSuperiorCommander = false;
		human.LeaderState.IsSuperiorCommander = true;
		LogHelper.Log(LogHelper.GAME, $"[지휘] 상위 지휘관 임명 — {human.name}(기본 통솔력 {human.BaseStat.leadership:F0}, 현재 {human.LeaderState.CurrentLeadership:F0}). 원정당 한 명, 사망·도주 시 자동 승계 없음");
	}

	// 같은 원정에 속한 파티들 — 점령 파티와 이미 끝난 파티는 제외한다.
	public static void CollectExpeditionParties(GameSession session, List<Party> into)
	{
		into.Clear();
		if (session?.parties == null) return;
		foreach (var p in session.parties)
		{
			if (p == null || p.Type == PartyType.Occupy || p.WaveEnded) continue;
			bool anyAlive = false;
			foreach (var m in p.Members) if (m != null && m.hp > 0f) { anyAlive = true; break; }
			if (anyAlive) into.Add(p);
		}
	}

	// ─────────────────────────── 상위 공동 공격 ───────────────────────────

	// UnitFunction.OnUpdate가 인류마다 부른다 — 상위 지휘관 본인만 일한다. 상위 공동 공격은 일반 집중 공격을 같은 원정의 여러 소속 파티에 적용하는 명령이다(16-1장):
	// 현재 통솔력 60 이상·명령 보유·지휘자 쿨타임 종료, 지휘관이 현재 직접 확인한 적끼리 신규(후보 2명 이상, 1위 ≥ 2위×1.5)·교체(≥ 현재×1.2) 점수 조건을 충족하고, 같은 교전에 참여할 소속 파티 둘 이상에 전달할 수 있어야 한다.
	public static void Tick(Human human)
	{
		var state = human?.LeaderState;
		if (!LeaderCommandSettings.Enabled || state == null || !state.IsSuperiorCommander) return;

		float now = Clock();
		if (now < state.NextSuperiorTick) return;
		state.NextSuperiorTick = now + EvaluateIntervalSeconds;
		if (human.hp <= 0f || human.currentFloor < 1 || human.StatusEffects.State.stunDuration > 0f || RetreatSystem.IsBoundToRetreat(human)) return;

		UpdateSuperiorFocusValidity(human);

		if (!LeaderMath.MeetsLeadership(TacticalCommand.SuperiorFocusAttack, state.CurrentLeadership)) return; // 통솔력이 떨어져도 직책·기존 명령은 유지하고 새 상위 명령만 못 낸다
		if (human.unitType == null || !LeaderCommandTable.Holds(human.unitType.typeName, TacticalCommand.SuperiorFocusAttack)) return;
		if (now < state.SuperiorCooldownUntil) return;
		var ownParty = human.party;
		if (ownParty != null && ownParty.Leader == human && now < ownParty.Command.CooldownUntil) return; // 같은 지휘자의 일반·상위 전술 명령은 함께 기다린다

		_visibleEnemies.Clear();
		foreach (var e in human.personalSpottedEnemies)
			if (e != null && e.hp > 0f && e.currentFloor == human.currentFloor) _visibleEnemies.Add(e);
		if (_visibleEnemies.Count == 0) return;

		Party scorer = ownParty ?? human.party;
		if (scorer == null) return;
		System.Func<Unit, float> score = e => ScoreOf(human, scorer, e);
		System.Func<Unit, float> dist = e => Vector2Int.Distance(human.position, e.position);
		System.Func<Unit, int> tie = e => scorer.Command.TieRankOf(e);

		Unit pick = null;
		float pickScore = 0f;
		if (state.SuperiorFocusTarget != null)
		{
			float currentScore = score(state.SuperiorFocusTarget);
			_others.Clear();
			foreach (var e in _visibleEnemies) if (e != state.SuperiorFocusTarget) _others.Add(e);
			Unit other = CommandSelectionMath.PickTopScored(_others, score, dist, tie, out float otherScore, out _);
			if (other != null && LeaderMath.MeetsReplaceRatio(currentScore, otherScore)) { pick = other; pickScore = otherScore; }
		}
		else if (_visibleEnemies.Count >= 2)
		{
			Unit top = CommandSelectionMath.PickTopScored(_visibleEnemies, score, dist, tie, out float topScore, out float secondScore);
			if (top != null && LeaderMath.MeetsNewFocusRatio(topScore, secondScore)) { pick = top; pickScore = topScore; }
		}
		if (pick == null) return;

		var recipients = new List<Human>();
		var partiesReached = new HashSet<Party>();
		CollectDirectRecipients(human, recipients, partiesReached);
		if (partiesReached.Count < 2) return; // 같은 교전에 참여할 소속 파티 둘 이상에 전달할 수 있어야 한다

		Issue(human, state, pick, pickScore, recipients, partiesReached, now);
	}

	private static readonly List<Unit> _visibleEnemies = new List<Unit>();
	private static readonly List<Unit> _others = new List<Unit>();

	// 지정한 적이 죽었거나, 이 지정을 가진 살아 있는 소속 파티원이 아무도 없고(추적 5초 만료 등) 지휘관도 지정 적을 직접 보지 못하면 지휘관의 상위 지정을 끝낸다 — 11장 "기존 지정이 해제되면 신규 조건으로 다시 판단"을 상위 공동 공격에도 적용한다(16-1장 "개인 수행·추적·해제는 11·12항을 따른다").
	// 안 끝내면 사라진 적이 지정으로 남아 새 지정(1.5배)은 막히고 교체(1.2배)만 판단한다. 일반 리더 쪽은 CommandIssueSystem.UpdateFocusValidity.
	internal static void UpdateSuperiorFocusValidity(Human commander)
	{
		var state = commander.LeaderState;
		if (state.SuperiorFocusTarget != null && state.SuperiorFocusTarget.hp <= 0f) { state.SuperiorFocusTarget = null; state.SuperiorFocusCommandId = null; } // 지정 적 사망 — 점수 비교 대상에서 제외
		if (state.SuperiorFocusTarget == null)
		{
			if (state.SuperiorFocusCommandId != null) state.SuperiorFocusCommandId = null;
			return;
		}
		if (!AnyoneHoldsSuperiorFocus(commander, state.SuperiorFocusCommandId) && !commander.personalSpottedEnemies.Contains(state.SuperiorFocusTarget))
		{
			state.SuperiorFocusTarget = null;
			state.SuperiorFocusCommandId = null;
		}
	}

	private static bool AnyoneHoldsSuperiorFocus(Human commander, string commandId)
	{
		if (HoldsSuperiorFocus(commander, commandId)) return true;
		if (commander.party != null)
			foreach (var m in commander.party.Members)
				if (HoldsSuperiorFocus(m, commandId)) return true;
		CollectExpeditionParties(commander.Session, _parties);
		foreach (var p in _parties)
			foreach (var m in p.Members)
				if (HoldsSuperiorFocus(m, commandId)) return true;
		return false;
	}

	private static bool HoldsSuperiorFocus(Human m, string commandId)
		=> m != null && m.hp > 0f && m.LeaderState?.SuperiorFocus != null && m.LeaderState.SuperiorFocus.CommandId == commandId; // 죽은 파티원이 들고 있던 지정은 세지 않는다

	private static float ScoreOf(Human commander, Party party, Unit enemy)
	{
		float danger = commander.Knowledge != null ? commander.Knowledge.GetPersonalDanger(commander, enemy) : 0f; // 상위 지휘관의 위험도 기록과 해당 배율로 계산한다
		return LeaderMath.CommandTargetScore(danger, party.Concept, CombatScoreMath.ResolveCombatRole(enemy.unitType), CombatScoreMath.ResolveTargetCategory(enemy));
	}

	// 상위 지휘관에게 직접 명령을 받을 수 있는 소속 파티원 — 지휘관의 현재 통솔력 전파 반경과 공간 조건 안에 있는 원정 파티원.
	private static void CollectDirectRecipients(Human commander, List<Human> into, HashSet<Party> reached)
	{
		CollectExpeditionParties(commander.Session, _parties);
		foreach (var p in _parties)
		{
			foreach (var m in p.Members)
			{
				if (m == null || m == commander || m.hp <= 0f || m.currentFloor != commander.currentFloor) continue;
				if (RetreatSystem.IsBoundToRetreat(m) || !PropagationSystem.InPropagationRange(commander, m)) continue;
				into.Add(m);
				reached.Add(p);
			}
		}
		if (commander.party != null) reached.Add(commander.party); // 지휘관 본인의 파티는 지휘관 자신이 명령에 참여한다
	}

	private static void Issue(Human commander, LeaderComponent state, Unit target, float score, List<Human> direct, HashSet<Party> reached, float now)
	{
		string id = CommandIssueSystem.NewCommandId();
		state.SuperiorFocusCommandId = id;
		state.SuperiorFocusTarget = target;
		state.SuperiorCooldownUntil = now + LeaderMath.SuperiorCooldownSeconds;
		if (commander.party != null && commander.party.Leader == commander)
			commander.party.Command.CooldownUntil = now + LeaderMath.SuperiorCooldownSeconds; // 일반 전술 명령도 함께 기다린다

		var template = new FocusOrder
		{
			CommandId = id, Command = TacticalCommand.SuperiorFocusAttack, Authority = CommandAuthority.Superior, IssuerName = commander.name,
			Target = target, LastKnownPosition = target.position,
		};
		var received = new HashSet<Human> { commander };
		CommandIssueSystem.GiveFocus(commander, template, now);
		foreach (var m in direct)
		{
			CommandIssueSystem.GiveFocus(m, template, now);
			received.Add(m);
		}

		// 상위 지휘관에게 직접 명령을 받은 소속 리더는 자기 전파 범위 안의 자기 파티원에게 같은 명령을 한 번 전달한다 — 다른 리더를 거쳐 계속 전달하지 않고(전달받은 파티원은 다시 전달하지 않음), 새 명령이나 추가 쿨타임을 만들지 않는다.
		int relayed = 0;
		var directLeaders = new List<Human>();
		foreach (var m in received)
			if (m.party != null && m.party.Leader == m && m != commander) directLeaders.Add(m);
		foreach (var leader in directLeaders)
		{
			foreach (var member in leader.party.Members)
			{
				if (member == null || member.hp <= 0f || received.Contains(member) || member.currentFloor != leader.currentFloor) continue;
				if (RetreatSystem.IsBoundToRetreat(member) || !PropagationSystem.InPropagationRange(leader, member)) continue;
				CommandIssueSystem.GiveFocus(member, template, now);
				received.Add(member);
				relayed++;
			}
		}
		LogHelper.Log(LogHelper.GAME, $"[지휘] 상위 공동 공격 — 상위 지휘관 {commander.name}(현재 통솔력 {state.CurrentLeadership:F0}) → {target.name} 점수 {score:F1}, 직접 전달 {direct.Count}명·소속 리더 전달 {relayed}명, 소속 파티 {reached.Count}개, 쿨타임 {LeaderMath.SuperiorCooldownSeconds:F0}초");
	}

	// ─────────────────────────── 지휘 상실 ───────────────────────────

	// 상위 지휘관이 사망·개인 도주를 확정했다 — 상위 명령과 상위 기본 버프를 끝낸다. 상위 지휘관을 자동 승계하지 않으며, 일반 리더를 겸했다면 그 파티의 일반 리더 후임만 선정한다(LeaderSuccessionSystem이 따로 처리).
	public static void OnSuperiorLost(Human superior, LeaderLossReason reason)
		=> EndSuperior(superior, reason == LeaderLossReason.Death ? "사망" : "개인 도주 확정");

	// 상위 지휘관이 던전 밖으로 이탈해 원정을 떠났다 — 직책·상위 명령·상위 기본 버프를 끝낸다(14-2장 "원정에서 이탈하면 상위 기본 버프를 종료"). 다음 웨이브에 생환자로 합류해도 직책이 남지 않게 한다(원정 편성 단계에서 다시 지정). Party.OnMemberExited가 부른다.
	public static void OnSuperiorLeftExpedition(Human superior) => EndSuperior(superior, "원정 이탈");

	private static void EndSuperior(Human superior, string reasonText)
	{
		if (superior == null || superior.LeaderState == null || !superior.LeaderState.IsSuperiorCommander) return;
		string name = superior.name;
		superior.LeaderState.IsSuperiorCommander = false;
		superior.LeaderState.SuperiorFocusTarget = null; // 낡은 지정이 재임명 때 되살아나 새 지정을 막지 않게 한다 (쿨타임은 시간 경과로 흐르므로 그대로 둔다)
		superior.LeaderState.SuperiorFocusCommandId = null;

		var session = superior.Session;
		CollectExpeditionParties(session, _parties);
		foreach (var p in _parties)
		{
			foreach (var m in p.Members)
			{
				var state = m?.LeaderState;
				if (state == null) continue;
				if (state.SuperiorFocus != null && state.SuperiorFocus.IssuerName == name)
				{
					state.SuperiorFocus = null;
					if (state.GeneralFocus != null) state.GeneralFocus.Suspended = false; // 아직 끝나지 않은 일반 명령만 재개
				}
				if (state.Action != null && state.Action.Authority == CommandAuthority.Superior && state.Action.IssuerName == name) state.Action = null;
			}
		}
		LeaderBuffSystem.RefreshAll(session);
		LogHelper.Log(LogHelper.GAME, $"[지휘] 상위 지휘관 {name} 지휘 상실({reasonText}) — 상위 명령·상위 기본 버프 종료, 자동 승계 없음. 살아 있는 일반 리더가 각자 자기 파티를 지휘합니다");
	}
}
