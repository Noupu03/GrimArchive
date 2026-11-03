using System;
using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 일반 리더의 전술 명령 발령(리더 명령 설계명세서 4~9·11장): 0.25초마다 발령 가능한 후보를 모으고 8장 순서(고유 스킬 → 상위 공동 공격 → 집중 공격 → 행동 우선 명령)로 하나만 골라 내린다.
// 공통 조건: 현재 통솔력·명령 보유·리더 명령 쿨타임·전파 범위 안의 수행 가능 파티원. 명령을 못 내려도 개인 전투는 계속된다 — 이 클래스는 아무것도 막지 않고 내릴 수 있을 때 내리기만 한다.
// 상위 지휘관의 상위 공동 공격은 ExpeditionCommandSystem이 같은 도구로 발령한다.
public static class CommandIssueSystem
{
	private const float EvaluateIntervalSeconds = 0.25f;
	private const float EngagementEndGraceSeconds = 5f; // 전투 종료 후 5초 경계(5-2장)와 같은 값 — 적이 잠깐 시야에서 사라진 것을 '새 교전'으로 세지 않는다 (내부 판단)

	public static Func<float> Clock = () => Time.time;

	private static readonly List<Human> _recipients = new List<Human>();
	private static readonly List<Unit> _visible = new List<Unit>();
	private static readonly List<StrikeTriggerTracker.EnemyView> _views = new List<StrikeTriggerTracker.EnemyView>();
	private static readonly List<Unit> _pool = new List<Unit>();

	// UnitFunction.OnUpdate가 인류마다 부른다 — 현재 일반 리더만 일한다.
	public static void Tick(Human human)
	{
		if (!LeaderCommandSettings.Enabled || human == null) return;
		var party = human.party;
		if (party == null || party.Leader != human) return;

		float now = Clock();
		var st = party.Command;
		if (now < st.NextEvaluateTime) return;
		st.NextEvaluateTime = now + EvaluateIntervalSeconds;
		if (human.hp <= 0f || human.currentFloor < 1 || human.isInDungeonEntranceSequence) return;

		foreach (var seen in human.visiblePartyMembers) // 리더가 직접 본 파티원의 마지막 위치 — 집결 기한 뒤 수색 대상 판단(5-2장)
			if (seen != null && seen.hp > 0f) st.LeaderLastSeen[seen.name] = (seen.position, seen.currentFloor, now);
		UpdateFocusValidity(party);
		UpdateEngagement(party, now);
		bool canIssue = !RetreatSystem.IsBoundToRetreat(human) && human.StatusEffects.State.stunDuration <= 0f && !party.Retreat.IsRetreating;
		_visible.Clear();
		CollectVisibleEnemies(human, _visible);
		ObserveStrikeTriggers(human, party, _visible);
		if (!canIssue) { st.Strike.CancelPending(); return; } // 후퇴·행동 불능 중에는 새 명령을 내리지 않는다(종료된 상황의 명령을 나중에 내리지 않는다)
		if (now < st.CooldownUntil) return; // 쿨타임 중 — 파티원은 공격·기술·기존 명령 수행을 계속한다

		_recipients.Clear();
		CollectRecipients(human, _recipients);

		// 8장 순서. 고유 지휘 스킬(80/100)은 콘텐츠가 미정이라 후보가 없다. 상위 공동 공격은 상위 지휘관 쪽(ExpeditionCommandSystem).
		if (TryIssueObjectFocus(human, party, now)) return;
		if (TryIssueFocus(human, party, now)) return;
		TryIssueActionPriority(human, party, now);
	}

	// ─────────────────────────── 상태 갱신 ───────────────────────────

	// 지정한 적이 사망하거나 적대가 아니게 됐으면 파티 차원의 지정도 끝낸다 — 파티원 개인의 종료는 FocusAttackSystem이 따로 판단한다.
	// 개인 지정이 전부 끝나(추적 5초 만료 등) 이 지정을 가진 파티원이 아무도 없고 리더도 지정 적을 직접 보고 있지 않으면 파티 지정도 끝난다 — 11장 "기존 지정이 해제되면 신규 조건으로 다시 판단". 안 끝내면 사라진 적이 지정으로 남아 새 지정(1.5배)은 막히고 교체(1.2배)만 판단한다.
	internal static void UpdateFocusValidity(Party party)
	{
		var st = party.Command;
		if (!st.HasFocus) return;
		if (st.FocusTarget != null && (st.FocusTarget.hp <= 0f)) st.ClearFocus();
		else if (st.FocusObject.HasValue && !FocusAttackSystem.IsObjectTargetValid(party.Leader, st.FocusObject.Value)) st.ClearFocus();
		else if (st.FocusTarget == null && !st.FocusObject.HasValue) st.ClearFocus(); // 대상 유닛이 Destroy됨
		else if (!AnyMemberHoldsFocus(party, st.FocusCommandId) && !(st.FocusTarget != null && party.Leader != null && party.Leader.personalSpottedEnemies.Contains(st.FocusTarget))) st.ClearFocus();
	}

	private static bool AnyMemberHoldsFocus(Party party, string commandId)
	{
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f) continue; // 죽은 파티원이 들고 있던 지정은 세지 않는다
			var held = m.LeaderState?.GeneralFocus;
			if (held != null && held.CommandId == commandId) return true;
		}
		return false;
	}

	private static void UpdateEngagement(Party party, float now)
	{
		var st = party.Command;
		bool anyEnemySeen = false;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f) continue;
			foreach (var e in m.personalSpottedEnemies)
				if (e != null && e.hp > 0f) { anyEnemySeen = true; break; }
			if (anyEnemySeen) break;
		}
		if (anyEnemySeen) { st.LastCombatSeenAt = now; st.InEngagement = true; }
		else if (st.InEngagement && now - st.LastCombatSeenAt >= EngagementEndGraceSeconds)
		{
			st.InEngagement = false; // 교전 종료 — 같은 교전 단위 기억(직전 제압 대상·동점 순서)을 비운다
			st.LastSuppressTarget = null;
			st.TieOrder.Clear();
		}
	}

	private static void CollectVisibleEnemies(Human leader, List<Unit> into)
	{
		foreach (var e in leader.personalSpottedEnemies)
			if (e != null && e.hp > 0f && e.currentFloor == leader.currentFloor) into.Add(e); // 리더가 현재 직접 확인한 적만 — 보고로만 알거나 놓친 적은 제외
	}

	private static void ObserveStrikeTriggers(Human leader, Party party, List<Unit> visible)
	{
		var st = party.Command;
		_views.Clear();
		foreach (var e in visible)
		{
			float ratio = 1f;
			bool hpKnown = EnemyInfoAccess.TryGetEnemyHpRatio(leader, e, out ratio);
			_views.Add(new StrikeTriggerTracker.EnemyView
			{
				Id = e.GetInstanceID(),
				IsEngagementTarget = IsEngagementTarget(party, e),
				IsAttackTarget = IsAttackTarget(leader, party, e),
				HpUnlocked = hpKnown,
				HpRatio = ratio,
				Incapacitated = e.StatusEffects.State.stunDuration > 0f,
			});
		}
		st.Strike.Observe(st.InEngagement, st.FocusCommandId, _views);
	}

	// 지금 교전 중인 적: 파티원의 공격 대상이거나 파티원을 공격하는 적.
	private static bool IsEngagementTarget(Party party, Unit enemy)
	{
		var enemyTarget = enemy.CombatTargeting.AttackTarget;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f) continue;
			if (m.CombatTargeting.AttackTarget == enemy || (enemyTarget != null && enemyTarget == m)) return true;
		}
		return false;
	}

	private static bool IsAttackTarget(Human leader, Party party, Unit enemy)
		=> leader.CombatTargeting.AttackTarget == enemy || party.Command.FocusTarget == enemy;

	// 명령을 받을 소속 파티원(리더 제외) — 현재 통솔력에 따른 전파 반경과 공간 조건 안에 있는 살아 있는 인원. 도주·후퇴 중인 인원은 명령을 따르지 않는다.
	private static void CollectRecipients(Human leader, List<Human> into)
	{
		foreach (var m in leader.party.Members)
		{
			if (m == null || m == leader || m.hp <= 0f || m.currentFloor != leader.currentFloor) continue;
			if (RetreatSystem.IsBoundToRetreat(m)) continue;
			if (!PropagationSystem.InPropagationRange(leader, m)) continue;
			into.Add(m);
		}
	}

	// ─────────────────────────── 집중 공격 ───────────────────────────

	private static float ScoreOf(Human leader, Party party, Unit enemy)
	{
		float danger = leader.Knowledge != null ? leader.Knowledge.GetPersonalDanger(leader, enemy) : 0f; // 리더가 기록한 위험도 — 다른 파티원의 수치는 가져오지 않는다
		return LeaderMath.CommandTargetScore(danger, party.Concept, CombatScoreMath.ResolveCombatRole(enemy.unitType), CombatScoreMath.ResolveTargetCategory(enemy));
	}

	private static bool TryIssueFocus(Human leader, Party party, float now)
	{
		var st = party.Command;
		if (!Holds(leader, TacticalCommand.FocusAttack) || !LeaderMath.MeetsLeadership(TacticalCommand.FocusAttack, leader.LeaderState.CurrentLeadership)) return false;
		if (_recipients.Count == 0) return false; // 전파 범위 안에 명령을 받을 소속 파티원이 있어야 한다

		Unit pick = null;
		float pickScore = 0f;
		if (st.HasFocus && st.FocusTarget != null && st.FocusTarget.hp > 0f)
		{
			// 교체: 다른 적의 점수 ≥ 현재 지정 적 점수 × 1.2 (현재 지정 적은 놓쳤을 수도 있어 리더 기록 위험도로 다시 계산)
			float currentScore = ScoreOf(leader, party, st.FocusTarget);
			_pool.Clear();
			foreach (var e in _visible) if (e != st.FocusTarget) _pool.Add(e);
			Unit other = CommandSelectionMath.PickTopScored(_pool, e => ScoreOf(leader, party, e), e => Vector2Int.Distance(leader.position, e.position), st.TieRankOf, out float otherScore, out _);
			if (other != null && LeaderMath.MeetsReplaceRatio(currentScore, otherScore)) { pick = other; pickScore = otherScore; }
		}
		else
		{
			// 신규: 직접 확인한 적이 2명 이상이고 최고 점수 ≥ 두 번째 × 1.5. 개인이 이미 공격 중인 적도 후보에 포함한다.
			if (_visible.Count < 2) return false;
			Unit top = CommandSelectionMath.PickTopScored(_visible, e => ScoreOf(leader, party, e), e => Vector2Int.Distance(leader.position, e.position), st.TieRankOf, out float topScore, out float secondScore);
			if (top != null && LeaderMath.MeetsNewFocusRatio(topScore, secondScore)) { pick = top; pickScore = topScore; }
		}
		if (pick == null) return false;

		IssueFocus(leader, party, pick, pickScore, now);
		return true;
	}

	// 12-3장: 리더가 전투 중 집중 공격 대상으로 가는 길을 막는 파괴 가능한 차단물(닫힌 상대 진영 문 등)을 확인하면 그 오브젝트 자체를 공동 파괴 대상으로 지정할 수 있다 — 이전 적 지정을 교체하고 제거 뒤 자동 복원하지 않으며, 적 후보 수와 1.5배·1.2배 점수 조건은 적용하지 않는다(지정 조건·쿨타임은 확인).
	private static bool TryIssueObjectFocus(Human leader, Party party, float now)
	{
		var st = party.Command;
		if (!st.HasFocus || st.FocusKind == TacticalCommand.ObjectFocus || st.FocusTarget == null || st.FocusTarget.hp <= 0f || !st.InEngagement) return false;
		if (!Holds(leader, TacticalCommand.FocusAttack) || !LeaderMath.MeetsLeadership(TacticalCommand.ObjectFocus, leader.LeaderState.CurrentLeadership)) return false;
		if (_recipients.Count == 0 || now < st.NextBlockerCheckTime) return false;
		st.NextBlockerCheckTime = now + 1f;
		if (!leader.personalSpottedEnemies.Contains(st.FocusTarget)) return false; // 리더가 지금 직접 확인한 대상으로 가는 길만 본다

		if (leader.MovementAlgorithm is AStarMovement astar && astar.TryGetPathTiles(leader, st.FocusTarget.position, out _)) return false; // 길이 있으면 막힌 게 아니다
		if (!RetreatBlockResolver.TryFindBlockingDoor(leader, st.FocusTarget.position, out Vector3Int door) || !FocusAttackSystem.IsObjectTargetValid(leader, door)) return false;

		string id = NewCommandId();
		st.FocusCommandId = id;
		st.FocusKind = TacticalCommand.ObjectFocus;
		st.FocusTarget = null;
		st.FocusObject = door;
		st.FocusScore = 0f;
		StartCooldown(party, TacticalCommand.ObjectFocus, now);

		var template = new FocusOrder { CommandId = id, Command = TacticalCommand.ObjectFocus, Authority = CommandAuthority.Leader, IssuerName = leader.name, ObjectTarget = door };
		DeliverFocus(leader, template, _recipients, now);
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [지휘] 차단물 공동 파괴 — 리더 {leader.name} → 문 {door}, 전달 {_recipients.Count}명(이전 적 지정 교체, 제거 뒤 자동 복원 없음)");
		return true;
	}

	private static void IssueFocus(Human leader, Party party, Unit target, float score, float now)
	{
		var st = party.Command;
		string id = NewCommandId();
		st.FocusCommandId = id;
		st.FocusKind = TacticalCommand.FocusAttack;
		st.FocusTarget = target;
		st.FocusObject = null;
		st.FocusScore = score;
		StartCooldown(party, TacticalCommand.FocusAttack, now);

		var template = new FocusOrder
		{
			CommandId = id, Command = TacticalCommand.FocusAttack, Authority = CommandAuthority.Leader, IssuerName = leader.name,
			Target = target, LastKnownPosition = target.position, EverSeen = false,
		};
		DeliverFocus(leader, template, _recipients, now);
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [지휘] 집중 공격 — 리더 {leader.name}(현재 통솔력 {leader.LeaderState.CurrentLeadership:F0}) → {target.name} 점수 {score:F1}, 전달 {_recipients.Count}명, 쿨타임 {LeaderMath.GeneralCooldownSeconds:F0}초");
	}

	// 받는 파티원마다 자기 사본을 만들어 준다(추적 시계는 개인별). 리더 본인도 받는다. 같은 명령 식별을 이미 가진 파티원에게는 다시 만들지 않는다(재전달은 새 명령이 아니다).
	public static void DeliverFocus(Human leader, FocusOrder template, List<Human> recipients, float now)
	{
		GiveFocus(leader, template, now);
		foreach (var m in recipients) GiveFocus(m, template, now);
	}

	public static void GiveFocus(Human member, FocusOrder template, float now)
	{
		var state = member.LeaderState;
		var existing = template.Authority == CommandAuthority.Superior ? state.SuperiorFocus : state.GeneralFocus;
		if (existing != null && existing.CommandId == template.CommandId) return;

		var order = new FocusOrder
		{
			CommandId = template.CommandId, Command = template.Command, Authority = template.Authority, IssuerName = template.IssuerName,
			ReceivedAt = now, Target = template.Target, ObjectTarget = template.ObjectTarget, LastKnownPosition = template.LastKnownPosition,
			EverSeen = member.personalSpottedEnemies.Contains(template.Target),
		};
		if (order.EverSeen) order.LostAt = -1f;
		if (template.Authority == CommandAuthority.Superior)
		{
			state.SuperiorFocus = order;
			if (state.GeneralFocus != null) state.GeneralFocus.Suspended = true; // 충돌하는 일반 명령은 잠시 중단(종료 조건·기한은 계속 계산)
		}
		else
		{
			state.GeneralFocus = order;
			order.Suspended = state.SuperiorFocus != null;
		}
		// 새 지정은 이전 지정을 교체한다 — 이전 대상의 개인 추적 상태는 FocusOrder와 함께 사라지므로 자동 재개가 없다.
	}

	// ─────────────────────────── 행동 우선 명령 ───────────────────────────

	private static void TryIssueActionPriority(Human leader, Party party, float now)
	{
		var st = party.Command;
		TacticalCommand pick = CommandSelectionMath.PickActionPriority(party.Concept, c => IsActionReady(leader, party, c, out _));
		if (pick == TacticalCommand.None) return;

		IsActionReady(leader, party, pick, out Unit suppressTarget); // 발령 직전 대상을 다시 확인해 가져온다
		IssueAction(leader, party, pick, suppressTarget, now);
	}

	// 공통 발령 조건(5-1-표1)과 명령별 계기·수행 조건을 모두 확인한다. 하나라도 틀리면 그 명령은 이번에 불가능하고 다음 순위 후보를 본다.
	private static bool IsActionReady(Human leader, Party party, TacticalCommand command, out Unit suppressTarget)
	{
		suppressTarget = null;
		if (!Holds(leader, command) || !LeaderMath.MeetsLeadership(command, leader.LeaderState.CurrentLeadership)) return false;

		var st = party.Command;
		switch (command)
		{
			case TacticalCommand.Strike:
				return st.Strike.CanJudge && AnyDoerWithTarget(leader, command, null); // 계기 + 리더가 교전 중인 적을 직접 확인(5-1장)

			case TacticalCommand.Suppress:
				suppressTarget = ChooseSuppressTarget(leader, party);
				return suppressTarget != null;

			case TacticalCommand.Protect:
				return HasProtectEmergency(leader, party) && AnyDoerWithTarget(leader, command, null);

			default:
				return false;
		}
	}

	private static bool AnyDoerWithTarget(Human leader, TacticalCommand command, Unit commandTarget)
		=> AnyDoer(leader, command, commandTarget, party => party.Command.FocusTarget);

	// 명령을 받을 파티원(리더 포함) 중 연결 기술을 지금 쓸 수 있는 인원이 1명 이상인가 — 전원 수행 불가면 명령하지 않는다(L11).
	private static bool AnyDoer(Human leader, TacticalCommand command, Unit commandTarget, Func<Party, Unit> focusOf)
	{
		Unit focus = focusOf != null ? focusOf(leader.party) : leader.party.Command.FocusTarget;
		if (CommandSkillSelector.CanUseNow(leader, command, CommandSkillSelector.DefaultAttackTarget(leader, focus), commandTarget)) return true;
		foreach (var m in _recipients)
			if (CommandSkillSelector.CanUseNow(m, command, CommandSkillSelector.DefaultAttackTarget(m, focus), commandTarget)) return true;
		return false;
	}

	// 5-1-표3·표4: 후위(힐러·원거리) 아군을 자기 근접 사거리 안에서 공격하는 적 중 그 적에게 쓸 CC 사용자가 있으면 그 적들끼리, 없으면 아군 근접 교전 적(CC 또는 공격 버프 사용자 있음)끼리 비교한다.
	private static Unit ChooseSuppressTarget(Human leader, Party party)
	{
		var st = party.Command;
		_pool.Clear();
		foreach (var e in _visible)
			if (IsAttackingBackline(party, e) && AnyDoer(leader, TacticalCommand.Suppress, e, null) && AnyCcUser(leader, e)) _pool.Add(e);
		if (_pool.Count == 0)
		{
			foreach (var e in _visible)
				if (IsEngagedInMeleeWithAlly(party, e) && AnyDoer(leader, TacticalCommand.Suppress, e, null)) _pool.Add(e);
		}
		return CommandSelectionMath.PickSuppressTarget(_pool, st.LastSuppressTarget, e => ScoreOf(leader, party, e), st.TieRankOf);
	}

	// 제압 CC(적 대상 연결 기술)를 쓸 수 있는 인원이 있는가 — 후위 위협 대응은 CC가 필요하다.
	private static bool AnyCcUser(Human leader, Unit enemy)
	{
		if (CcUsable(leader, enemy)) return true;
		foreach (var m in _recipients) if (CcUsable(m, enemy)) return true;
		return false;
	}

	private static bool CcUsable(Human m, Unit enemy)
	{
		if (!CommandSkillSelector.TryPick(m, TacticalCommand.Suppress, null, enemy, exact: false, out var skill, out _)) return false;
		return skill.Affinity == SkillAffinity.Enemy;
	}

	private static bool IsBackline(Human ally)
	{
		var role = CombatScoreMath.ResolveCombatRole(ally.unitType);
		return role == CombatRole.RangedDps || role == CombatRole.RangedSupport;
	}

	// 적의 근접 공격 사거리 — 적 스킬 중 근접으로 볼 수 있는 가장 긴 사거리(rangedSkillMinRange 미만). 적이 쓰는 스킬 구성은 모두 알려진 것으로 본다(게임 내 정보가 아니라 거리 판정의 편의).
	private static int MeleeReachOf(Unit enemy)
	{
		int ranged = AIConfigLoader.Behavior?.rangedSkillMinRange ?? 4;
		int reach = 1;
		var skills = enemy.Generate?.GetSkills(enemy.unitType.typeName);
		if (skills != null)
			foreach (var s in skills)
				if (s != null && s.Affinity == SkillAffinity.Enemy && s.HitRange < ranged && s.HitRange > reach) reach = s.HitRange;
		return reach;
	}

	private static bool IsAttackingBackline(Party party, Unit enemy)
	{
		var target = enemy.CombatTargeting.AttackTarget as Human;
		if (target == null || target.party != party || target.hp <= 0f || !IsBackline(target)) return false;
		return AIMovementHelper.ChebyshevDistance(enemy.position, target.position) <= MeleeReachOf(enemy);
	}

	private static bool IsEngagedInMeleeWithAlly(Party party, Unit enemy)
	{
		int reach = MeleeReachOf(enemy);
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f || m.currentFloor != enemy.currentFloor) continue;
			bool engaged = m.CombatTargeting.AttackTarget == enemy || enemy.CombatTargeting.AttackTarget == m;
			if (engaged && AIMovementHelper.ChebyshevDistance(enemy.position, m.position) <= reach) return true;
		}
		return false;
	}

	// 5-1-표5: 리더가 직접 확인한 아군(자기 포함)의 HP 30% 이하 + 공격 위협, 또는 행동 불능 + 공격받음. 사망 예측은 쓰지 않는다.
	private static bool HasProtectEmergency(Human leader, Party party)
	{
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f || m.currentFloor != leader.currentFloor) continue;
			if (!EnemyInfoAccess.TryGetAllyHpRatio(leader, m, out float ratio)) continue; // 시야 밖 아군의 HP는 자동 갱신하지 않는다
			bool underThreat = m.HasActiveThreatGroundTruth();
			bool incapacitated = m.StatusEffects.State.stunDuration > 0f;
			if (CommandSelectionMath.IsProtectEmergency(ratio, underThreat, incapacitated, m.isHitThisTurn)) return true;
		}
		return false;
	}

	private static void IssueAction(Human leader, Party party, TacticalCommand command, Unit suppressTarget, float now)
	{
		var st = party.Command;
		string id = NewCommandId();
		StartCooldown(party, command, now);
		st.Strike.ConsumeAll(); // 지금 유효한 강공격 계기는 이 발령으로 소비된다(여러 계기를 명령 횟수로 누적하지 않는다)
		if (command == TacticalCommand.Suppress) st.LastSuppressTarget = suppressTarget; // 5초 적용이 끝나도 기억한다 — 기억만으로 끝난 우선순위를 계속 적용하지는 않는다

		var template = new ActionOrder { CommandId = id, Command = command, Authority = CommandAuthority.Leader, IssuerName = leader.name, SuppressTarget = suppressTarget };
		GiveAction(leader, template, now); // 리더 본인은 발령 시점부터 5초
		foreach (var m in _recipients) GiveAction(m, template, now); // 파티원은 받은 시점부터 5초
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [지휘] {command.ToKorean()} — 리더 {leader.name}({party.Concept.ToKorean()} 파티){(suppressTarget != null ? $" → {suppressTarget.name}" : "")}, 전달 {_recipients.Count}명, 적용 {LeaderMath.ActionPriorityApplySeconds:F0}초·쿨타임 {LeaderMath.GeneralCooldownSeconds:F0}초");
	}

	public static void GiveAction(Human member, ActionOrder template, float now)
	{
		var state = member.LeaderState;
		if (state.Action != null && state.Action.CommandId == template.CommandId) return; // 같은 명령의 재전달은 종료 시각을 늘리지 않는다
		state.Action = new ActionOrder
		{
			CommandId = template.CommandId, Command = template.Command, Authority = template.Authority, IssuerName = template.IssuerName,
			ReceivedAt = now, ExpiresAt = now + LeaderMath.ActionPriorityApplySeconds, SuppressTarget = template.SuppressTarget,
		};
	}

	// ─────────────────────────── 공통 ───────────────────────────

	private static bool Holds(Human leader, TacticalCommand command)
		=> leader.unitType != null && LeaderCommandTable.Holds(leader.unitType.typeName, command);

	private static void StartCooldown(Party party, TacticalCommand command, float now)
	{
		var st = party.Command;
		st.CooldownUntil = now + LeaderMath.CooldownSecondsOf(command);
		// 같은 지휘자의 일반·상위 전술 명령은 함께 기다린다(7장) — 일반 리더가 상위 지휘관을 겸했으면 일반 명령도 상위 쿨타임을 시작한다.
		var leaderState = party.Leader?.LeaderState;
		if (leaderState != null && leaderState.IsSuperiorCommander) leaderState.SuperiorCooldownUntil = Mathf.Max(leaderState.SuperiorCooldownUntil, st.CooldownUntil);
		st.LastIssued = command;
		st.LastIssuedAt = now;
		st.IssuedCount++;
	}

	private static int _idCounter;
	public static string NewCommandId() => "cmd" + (++_idCounter);

	// 지휘 상실 — 잃은 지휘자의 명령을 파티원 개인 상태에서도 모두 끝낸다(FocusAttackSystem의 개인 종료와 같은 결과).
	public static void OnLeaderLost(Party party, string leaderName)
	{
		party.Command.ClearOnLeaderLoss();
		foreach (var m in party.Members)
		{
			var state = m?.LeaderState;
			if (state == null) continue;
			if (state.GeneralFocus != null && state.GeneralFocus.IssuerName == leaderName) state.GeneralFocus = null;
			if (state.SuperiorFocus != null && state.SuperiorFocus.IssuerName == leaderName) state.SuperiorFocus = null;
			if (state.Action != null && state.Action.IssuerName == leaderName && state.Action.Authority == CommandAuthority.Leader) state.Action = null;
		}
	}
}
