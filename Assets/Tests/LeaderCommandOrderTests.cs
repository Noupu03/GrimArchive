#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

// 파티원 개인이 받은 명령의 수명주기(리더 명령 9·11-1·12·16·17장)를 Human/Monster 인스턴스로 확인한다 — 세션·맵 없이 되는 범위만. 전파 반경(맵 필요)·스킬 선택(UnitGenerate 필요)·실제 발령 판단은 플레이 확인 대상이다.
// FocusAttackSystem.Tick은 Time.time과 0.1초 스로틀을 쓰므로 테스트는 시각을 직접 되돌려(ReceivedAt −= 6) 기한 경과를 만든다.
public class LeaderOrderTests
{
	[SetUp]
	public void Setup()
	{
		RetreatSystem.EnabledOverride = true;
		LeaderCommandSettings.EnabledOverride = true;
	}

	[TearDown]
	public void Reset()
	{
		RetreatSystem.EnabledOverride = null;
		LeaderCommandSettings.EnabledOverride = null;
		LeaderCommandSettings.ApproachLimitOverride = null;
		LeaderSuccessionSystem.Clock = () => Time.time;
		LeaderCommandTable.ResetForTests();
	}

	private static Human NewHuman(string name, float baseLeadership = 30f, float mental = 30f)
	{
		var h = ScriptableObject.CreateInstance<Human>();
		h.name = name;
		h.currentFloor = 1;
		h.maxHp = 100f; h.hp = 100f;
		h.BaseStat.maxMental = 100f; h.BaseStat.mental = mental;
		h.BaseStat.leadership = baseLeadership;
		return h;
	}

	private static Monster NewEnemy(string name)
	{
		var m = ScriptableObject.CreateInstance<Monster>();
		m.name = name;
		m.currentFloor = 1;
		m.maxHp = 100f; m.hp = 100f;
		m.FactionBehavior = new WildMonsterBehavior();
		return m;
	}

	private static Party NewParty(PartyConcept concept, params Human[] members)
	{
		var p = new Party("p", "테스트 파티") { Type = PartyType.Explore, Concept = concept };
		foreach (var m in members) { p.Members.Add(m); m.party = p; }
		p.AssignLeaderIfNeeded();
		return p;
	}

	private static FocusOrder FocusTemplate(string id, Unit target, string issuer, CommandAuthority authority = CommandAuthority.Leader)
		=> new FocusOrder
		{
			CommandId = id, Command = authority == CommandAuthority.Superior ? TacticalCommand.SuperiorFocusAttack : TacticalCommand.FocusAttack,
			Authority = authority, IssuerName = issuer, Target = target, LastKnownPosition = target.position,
		};

	private static void TickFocus(Human h)
	{
		h.LeaderState.NextFocusTick = 0f; // 0.1초 스로틀 해제
		FocusAttackSystem.Tick(h);
	}

	// 12-2-표1: 한 번도 못 본 지정 적은 명령을 받은 때부터 최대 5초 — 안에서는 유지, 넘으면 이 파티원만 지정을 끝낸다.
	[Test]
	public void NeverSeenTarget_KeptWithinFiveSeconds_EndedAfter()
	{
		var leader = NewHuman("L", 50f); var m = NewHuman("M");
		NewParty(PartyConcept.Adventurer, leader, m);
		var enemy = NewEnemy("E");
		CommandIssueSystem.GiveFocus(m, FocusTemplate("c1", enemy, "L"), Time.time);

		TickFocus(m);
		Assert.IsNotNull(m.LeaderState.GeneralFocus, "5초 안에는 지정을 유지한다");
		Assert.IsTrue(FocusAttackSystem.NeedsPursuit(m), "못 본 지정 적은 마지막 위치를 추적한다");

		m.LeaderState.GeneralFocus.ReceivedAt -= 6f;
		TickFocus(m);
		Assert.IsNull(m.LeaderState.GeneralFocus, "추적 5초가 끝나면 이 파티원의 지정을 끝낸다");
		Assert.IsFalse(FocusAttackSystem.NeedsPursuit(m), "종료한 지정을 근거로 추적을 자동 재개하지 않는다");
	}

	// 12-2-표1: 다른 적과 교전 중이면(추적 시작 시/추적 중) 그 파티원의 지정만 즉시 끝나고 다른 파티원은 유지한다.
	[Test]
	public void OtherEnemyVisible_EndsOnlyThatMembersFocus()
	{
		var leader = NewHuman("L", 50f); var busy = NewHuman("busy"); var free = NewHuman("free");
		NewParty(PartyConcept.Rogue, leader, busy, free);
		var target = NewEnemy("A"); var other = NewEnemy("B");
		CommandIssueSystem.GiveFocus(busy, FocusTemplate("c1", target, "L"), Time.time);
		CommandIssueSystem.GiveFocus(free, FocusTemplate("c1", target, "L"), Time.time);
		busy.personalSpottedEnemies.Add(other); // busy는 B와 교전 중

		TickFocus(busy); TickFocus(free);
		Assert.IsNull(busy.LeaderState.GeneralFocus, "다른 적과 교전하면 즉시 종료");
		Assert.IsNotNull(free.LeaderState.GeneralFocus, "다른 파티원의 지정은 유지");
	}

	// 직접 보는 동안은 추적하지 않고 마지막 위치를 갱신하며, 놓치면 그때부터 5초(L23: 도착 후 확인 대기 3초는 5초 안에 포함).
	[Test]
	public void SeenTarget_UpdatesLastPosition_LostStartsClock_ConfirmWaitEndsAtThreeSeconds()
	{
		var leader = NewHuman("L", 50f); var m = NewHuman("M");
		NewParty(PartyConcept.Adventurer, leader, m);
		var enemy = NewEnemy("E");
		enemy.position = new Vector2Int(7, 3);
		CommandIssueSystem.GiveFocus(m, FocusTemplate("c1", enemy, "L"), Time.time);
		m.personalSpottedEnemies.Add(enemy);

		TickFocus(m);
		var order = m.LeaderState.GeneralFocus;
		Assert.IsTrue(order.EverSeen);
		Assert.AreEqual(new Vector2Int(7, 3), order.LastKnownPosition);
		Assert.AreEqual(-1f, order.LostAt);
		Assert.IsFalse(FocusAttackSystem.NeedsPursuit(m));

		enemy.position = new Vector2Int(9, 9);
		m.personalSpottedEnemies.Clear(); // 시야에서 놓침
		TickFocus(m);
		Assert.GreaterOrEqual(order.LostAt, 0f, "놓친 때부터 5초 시계가 시작된다");
		Assert.AreEqual(new Vector2Int(7, 3), order.LastKnownPosition, "마지막으로 직접 확인한 위치를 쓴다(실제 현재 위치를 자동 갱신하지 않는다)");
		Assert.IsTrue(FocusAttackSystem.NeedsPursuit(m));

		// 마지막 위치에 도착한 시각을 기준으로 3초(전체 5초 안에 포함) — 시각을 직접 넘겨 결정적으로 확인한다.
		float arrived = Time.time + 100f;
		order.LostAt = arrived - 1f;
		order.ArrivedAt = arrived;
		m.LeaderState.NextFocusTick = 0f; FocusAttackSystem.Tick(m, arrived + 2.9f);
		Assert.IsNotNull(m.LeaderState.GeneralFocus, "도착 후 2.9초에는 아직 확인을 기다린다");
		m.LeaderState.NextFocusTick = 0f; FocusAttackSystem.Tick(m, arrived + 3.0f);
		Assert.IsNull(m.LeaderState.GeneralFocus, "도착 후 직접 확인하지 못한 채 3초가 지나면 끝난다");

		// 놓친 때부터 5초(도착이 늦어도 전체 5초 안): 4초에 도착해 1초만 기다려도 놓친 뒤 5초에 끝난다.
		CommandIssueSystem.GiveFocus(m, FocusTemplate("c2", enemy, "L"), arrived);
		var order2 = m.LeaderState.GeneralFocus;
		order2.EverSeen = true; order2.LostAt = arrived; order2.ArrivedAt = arrived + 4f;
		m.LeaderState.NextFocusTick = 0f; FocusAttackSystem.Tick(m, arrived + 4.9f);
		Assert.IsNotNull(m.LeaderState.GeneralFocus);
		m.LeaderState.NextFocusTick = 0f; FocusAttackSystem.Tick(m, arrived + 5.0f);
		Assert.IsNull(m.LeaderState.GeneralFocus, "추적과 대기를 합쳐 최초 위치 상실 후 최대 5초");
	}

	[Test]
	public void TargetDeath_EndsFocus()
	{
		var leader = NewHuman("L", 50f); var m = NewHuman("M");
		NewParty(PartyConcept.Adventurer, leader, m);
		var enemy = NewEnemy("E");
		CommandIssueSystem.GiveFocus(m, FocusTemplate("c1", enemy, "L"), Time.time);
		enemy.hp = 0f;
		TickFocus(m);
		Assert.IsNull(m.LeaderState.GeneralFocus);
	}

	// 17장: 명령한 지휘자가 지휘를 잃으면 그 리더의 집중 공격·행동 우선 명령이 파티원 개인 상태에서도 전부 끝난다. 다른 지휘자의 명령은 남긴다.
	[Test]
	public void LeaderLoss_EndsThatLeadersOrders_KeepsOthers()
	{
		var leader = NewHuman("L", 50f); var m = NewHuman("M");
		var party = NewParty(PartyConcept.Adventurer, leader, m);
		var enemy = NewEnemy("E");
		float now = Time.time;
		CommandIssueSystem.GiveFocus(m, FocusTemplate("c1", enemy, "L"), now);
		CommandIssueSystem.GiveAction(m, new ActionOrder { CommandId = "a1", Command = TacticalCommand.Strike, Authority = CommandAuthority.Leader, IssuerName = "L" }, now);
		m.LeaderState.SuperiorFocus = new FocusOrder { CommandId = "s1", Authority = CommandAuthority.Superior, IssuerName = "S", Target = enemy, Command = TacticalCommand.SuperiorFocusAttack };
		party.Command.FocusCommandId = "c1"; party.Command.FocusTarget = enemy;

		CommandIssueSystem.OnLeaderLost(party, "L");
		Assert.IsNull(m.LeaderState.GeneralFocus);
		Assert.IsNull(m.LeaderState.Action);
		Assert.IsFalse(party.Command.HasFocus);
		Assert.IsNotNull(m.LeaderState.SuperiorFocus, "살아 있는 상위 지휘관의 명령은 유지된다(16-2장)");
	}

	// 16-1장·L24: 상위 명령이 일반 집중 공격을 잠시 중단시키고, 끝나면 아직 끝나지 않은 일반 명령만 재개한다. 중단 중에도 일반 명령의 종료 조건은 계속 적용된다.
	[Test]
	public void SuperiorOrder_SuspendsGeneral_ResumesOnlyIfStillValid()
	{
		var leader = NewHuman("L", 50f); var m = NewHuman("M");
		NewParty(PartyConcept.Adventurer, leader, m);
		var a = NewEnemy("A"); var b = NewEnemy("B");
		m.personalSpottedEnemies.Add(a); // A는 계속 직접 확인 중
		float now = Time.time;
		CommandIssueSystem.GiveFocus(m, FocusTemplate("c1", a, "L"), now);
		CommandIssueSystem.GiveFocus(m, FocusTemplate("s1", b, "S", CommandAuthority.Superior), now);

		Assert.IsTrue(m.LeaderState.GeneralFocus.Suspended, "상위 명령과 충돌하는 일반 명령은 잠시 중단");
		Assert.AreSame(m.LeaderState.SuperiorFocus, m.LeaderState.EffectiveFocus);

		// 상위 지휘관이 유효하지 않으면(여기선 세션이 없어 확인 불가) 상위 명령은 끝나고 유효한 일반 명령이 재개된다.
		TickFocus(m);
		Assert.IsNull(m.LeaderState.SuperiorFocus);
		Assert.IsNotNull(m.LeaderState.GeneralFocus, "A가 계속 확인되고 다른 종료 조건이 없으면 재개");
		Assert.IsFalse(m.LeaderState.GeneralFocus.Suspended);

		// 중단 중 일반 명령이 종료 조건에 닿으면 복원하지 않는다.
		CommandIssueSystem.GiveFocus(m, FocusTemplate("s2", b, "S", CommandAuthority.Superior), now);
		a.hp = 0f;
		TickFocus(m);
		Assert.IsNull(m.LeaderState.SuperiorFocus);
		Assert.IsNull(m.LeaderState.GeneralFocus, "중단 중 대상이 죽었으면 상위 명령이 끝나도 복원하지 않는다");
	}

	// 4-1·9-표2: 행동 우선 명령은 받은 때부터 5초, 같은 명령 재전달은 종료 시각을 늘리지 않고, 새 명령은 이전 우선순위를 대체한다.
	[Test]
	public void ActionOrder_FiveSecondsFromReceipt_RedeliveryDoesNotExtend_NewCommandReplaces()
	{
		var m = NewHuman("M");
		var template = new ActionOrder { CommandId = "a1", Command = TacticalCommand.Suppress, Authority = CommandAuthority.Leader, IssuerName = "L" };
		CommandIssueSystem.GiveAction(m, template, 100f);
		Assert.AreEqual(105f, m.LeaderState.Action.ExpiresAt, 1e-4f);

		CommandIssueSystem.GiveAction(m, template, 102f); // 2초에 같은 명령 재전달
		Assert.AreEqual(105f, m.LeaderState.Action.ExpiresAt, 1e-4f, "최초 5초 종료 시각을 늘리지 않는다(L10)");
		Assert.IsTrue(m.LeaderState.Action.IsActive(104.9f));
		Assert.IsFalse(m.LeaderState.Action.IsActive(105f));

		var next = new ActionOrder { CommandId = "a2", Command = TacticalCommand.Protect, Authority = CommandAuthority.Leader, IssuerName = "L" };
		CommandIssueSystem.GiveAction(m, next, 110f);
		Assert.AreEqual(TacticalCommand.Protect, m.LeaderState.Action.Command, "새 행동 우선 명령이 이전 우선순위를 대체");
		Assert.AreEqual(115f, m.LeaderState.Action.ExpiresAt, 1e-4f);

		m.LeaderState.Action.ExpiresAt = Time.time - 1f; // 5초 경과
		Assert.IsFalse(CommandSkillSelector.TryPickActive(m, null, out _, out _));
		Assert.IsNull(m.LeaderState.Action, "5초가 끝나면 우선순위 적용이 끝난다(직전 대상 기억만으로 계속 적용하지 않음)");
	}

	// 18장: 리더 사망 사건은 직접 목격한 파티원의 정신력을 일반 사망(−10)이 아니라 −20(안정 단계 ×0.95)으로 대체하고 능력치 충격을 건다. 일반 파티원 사망은 −10에 충격 없음.
	[Test]
	public void LeaderDeathRecord_ReplacesGeneralDeathLoss_AndShocksWitness()
	{
		var leader = NewHuman("L", 50f); var witness = NewHuman("W", 30f, mental: 30f);
		var party = NewParty(PartyConcept.Adventurer, leader, witness);
		witness.CombatStat.physicalDefense = 10f;
		float before = witness.BaseStat.mental;

		leader.hp = 0f;
		PartyDeathSystem.OnPartyMemberDied(leader, "corpse-leader");
		Assert.IsTrue(party.DeathRecords["corpse-leader"].DeadWasLeader);
		Assert.AreEqual(before - 19f, witness.BaseStat.mental, 0.05f, "직접 확인 −20 × 안정 단계 0.95");
		Assert.AreEqual(8f, witness.CombatStat.physicalDefense, 1e-4f, "사망을 안 때부터 세부 스테이터스 −20%");

		// 같은 사망의 재확인(두 번째 호출)은 추가 감소·충격 갱신이 없다.
		float afterFirst = witness.BaseStat.mental;
		PartyDeathSystem.OnPartyMemberDied(leader, "corpse-leader");
		Assert.AreEqual(afterFirst, witness.BaseStat.mental, 1e-4f);

		var l2 = NewHuman("L2", 50f); var w2 = NewHuman("W2", 30f, mental: 30f); var dead2 = NewHuman("D2", 20f);
		var party2 = NewParty(PartyConcept.Adventurer, l2, w2, dead2);
		w2.CombatStat.physicalDefense = 10f;
		float before2 = w2.BaseStat.mental;
		dead2.hp = 0f;
		PartyDeathSystem.OnPartyMemberDied(dead2, "corpse-member");
		Assert.IsFalse(party2.DeathRecords["corpse-member"].DeadWasLeader);
		Assert.AreEqual(before2 - 9.5f, w2.BaseStat.mental, 0.05f, "일반 파티원 사망은 −10 × 0.95");
		Assert.AreEqual(10f, w2.CombatStat.physicalDefense, 1e-4f, "일반 파티원 사망에는 능력치 충격이 없다");
	}

	// kill-switch: 끄면 예전처럼 사망 즉시 통솔력 최댓값을 재선정하고 승계 대기·충격이 없다.
	[Test]
	public void KillSwitchOff_ReassignsImmediately_NoSuccessionWindow()
	{
		LeaderCommandSettings.EnabledOverride = false;
		var leader = NewHuman("L", 50f); var a = NewHuman("A", 5f);
		var party = NewParty(PartyConcept.Adventurer, leader, a);
		Assert.AreSame(leader, party.Leader);

		leader.hp = 0f;
		PartyDeathSystem.OnPartyMemberDied(leader, "corpse");
		Assert.IsFalse(party.DeathRecords["corpse"].DeadWasLeader, "꺼져 있으면 리더 사망 충격을 적용하지 않는다");
		LeaderSuccessionSystem.OnMemberDied(leader);
		Assert.AreSame(a, party.Leader, "기본 통솔력 10 미만이어도 즉시 재선정(예전 동작)");
		Assert.IsFalse(party.Command.SuccessionPending);
	}

	// 검증 F3: 파티 지정은 개인 지정을 가진 파티원이 한 명도 없고 리더도 지정 적을 직접 보지 못하면 끝난다 — 안 끝나면 사라진 적이 지정으로 남아 새 집중 공격(1.5배)이 막힌다.
	[Test]
	public void PartyFocus_EndsWhenNoMemberHoldsIt_UnlessLeaderStillSeesTarget()
	{
		var leader = NewHuman("L", 50f); var m = NewHuman("M");
		var party = NewParty(PartyConcept.Adventurer, leader, m);
		var enemy = NewEnemy("E");
		float now = Time.time;
		CommandIssueSystem.GiveFocus(leader, FocusTemplate("c1", enemy, "L"), now);
		CommandIssueSystem.GiveFocus(m, FocusTemplate("c1", enemy, "L"), now);
		party.Command.FocusCommandId = "c1"; party.Command.FocusTarget = enemy; party.Command.FocusKind = TacticalCommand.FocusAttack;

		CommandIssueSystem.UpdateFocusValidity(party);
		Assert.IsTrue(party.Command.HasFocus, "지정을 가진 파티원이 있으면 유지");

		leader.LeaderState.GeneralFocus = null; // 리더 개인 지정은 끝남
		CommandIssueSystem.UpdateFocusValidity(party);
		Assert.IsTrue(party.Command.HasFocus, "한 명이라도 가졌으면 유지");

		m.hp = 0f; // 죽은 파티원이 들고 있는 지정은 세지 않는다
		leader.personalSpottedEnemies.Add(enemy); // 리더는 지정 적을 아직 직접 보고 있다
		CommandIssueSystem.UpdateFocusValidity(party);
		Assert.IsTrue(party.Command.HasFocus, "리더가 지정 적을 직접 보면 유지");

		leader.personalSpottedEnemies.Clear();
		CommandIssueSystem.UpdateFocusValidity(party);
		Assert.IsFalse(party.Command.HasFocus, "아무도 가지지 않고 리더도 못 보면 파티 지정이 끝나 신규 조건으로 다시 판단한다");
	}

	// 검증 F3: 리더가 던전 밖으로 정상 이탈해도 그 리더의 지정이 후임에게 넘어가지 않는다.
	[Test]
	public void LeaderNormalExit_ClearsPartyFocus_SuccessorStartsFresh()
	{
		var leader = NewHuman("L", 50f); var m = NewHuman("M", 30f);
		var party = NewParty(PartyConcept.Adventurer, leader, m);
		var enemy = NewEnemy("E");
		CommandIssueSystem.GiveFocus(m, FocusTemplate("c1", enemy, "L"), Time.time);
		party.Command.FocusCommandId = "c1"; party.Command.FocusTarget = enemy; party.Command.FocusKind = TacticalCommand.FocusAttack;

		party.OnMemberExited(leader);
		Assert.AreSame(m, party.Leader, "정상 이탈은 기존처럼 즉시 재선정");
		Assert.IsFalse(party.Command.HasFocus, "이전 리더의 파티 지정은 후임에게 넘어가지 않는다");
		Assert.IsNull(m.LeaderState.GeneralFocus, "그 리더가 낸 개인 지정도 끝난다");
	}

	// 검증 재감사: 상위 공동 공격 지정도 일반 지정과 같이 — 지정을 가진 살아 있는 파티원이 없고 지휘관도 지정 적을 직접 보지 못하면 끝난다(안 끝나면 새 상위 지정이 1.2배 교체로만 판단된다).
	[Test]
	public void SuperiorFocus_EndsWhenNobodyHoldsIt_UnlessCommanderStillSeesTarget()
	{
		var cmd = NewHuman("S", 70f); var m = NewHuman("M");
		NewParty(PartyConcept.Adventurer, cmd, m);
		cmd.LeaderState.IsSuperiorCommander = true;
		var enemy = NewEnemy("E");
		float now = Time.time;
		cmd.LeaderState.SuperiorFocusCommandId = "s1"; cmd.LeaderState.SuperiorFocusTarget = enemy;
		CommandIssueSystem.GiveFocus(cmd, FocusTemplate("s1", enemy, "S", CommandAuthority.Superior), now);
		CommandIssueSystem.GiveFocus(m, FocusTemplate("s1", enemy, "S", CommandAuthority.Superior), now);

		ExpeditionCommandSystem.UpdateSuperiorFocusValidity(cmd);
		Assert.AreSame(enemy, cmd.LeaderState.SuperiorFocusTarget, "지정을 가진 파티원이 있으면 유지");

		cmd.LeaderState.SuperiorFocus = null; // 지휘관 개인 지정은 끝남 — 파티원 M이 아직 가졌다
		ExpeditionCommandSystem.UpdateSuperiorFocusValidity(cmd);
		Assert.AreSame(enemy, cmd.LeaderState.SuperiorFocusTarget);

		m.hp = 0f; // 죽은 파티원이 들고 있는 지정은 세지 않는다
		cmd.personalSpottedEnemies.Add(enemy);
		ExpeditionCommandSystem.UpdateSuperiorFocusValidity(cmd);
		Assert.AreSame(enemy, cmd.LeaderState.SuperiorFocusTarget, "지휘관이 지정 적을 직접 보면 유지");

		cmd.personalSpottedEnemies.Clear();
		ExpeditionCommandSystem.UpdateSuperiorFocusValidity(cmd);
		Assert.IsNull(cmd.LeaderState.SuperiorFocusTarget, "아무도 가지지 않고 직접 보지도 못하면 상위 지정이 끝나 신규 조건으로 다시 판단한다");
		Assert.IsNull(cmd.LeaderState.SuperiorFocusCommandId);
	}

	// 검증 재감사: 상위 지휘관이 지휘를 잃거나 던전 밖으로 이탈하면 직책과 낡은 상위 지정을 비운다 — 재임명·다음 웨이브 합류 때 되살아나지 않게.
	[Test]
	public void SuperiorRole_ClearedOnLoss_AndOnLeavingExpedition()
	{
		var s = NewHuman("S", 70f); var m = NewHuman("M", 30f);
		var party = NewParty(PartyConcept.Adventurer, s, m);
		var enemy = NewEnemy("E");

		s.LeaderState.IsSuperiorCommander = true;
		s.LeaderState.SuperiorFocusCommandId = "s1"; s.LeaderState.SuperiorFocusTarget = enemy;
		ExpeditionCommandSystem.OnSuperiorLost(s, LeaderLossReason.Death);
		Assert.IsFalse(s.LeaderState.IsSuperiorCommander);
		Assert.IsNull(s.LeaderState.SuperiorFocusTarget, "낡은 상위 지정이 남지 않는다");
		Assert.IsNull(s.LeaderState.SuperiorFocusCommandId);

		s.LeaderState.IsSuperiorCommander = true; // 다시 임명된 뒤 던전 밖으로 이탈
		s.LeaderState.SuperiorFocusCommandId = "s2"; s.LeaderState.SuperiorFocusTarget = enemy;
		party.OnMemberExited(s);
		Assert.IsFalse(s.LeaderState.IsSuperiorCommander, "원정을 떠나면 상위 지휘관 직책이 끝난다");
		Assert.IsNull(s.LeaderState.SuperiorFocusTarget);
		Assert.AreSame(m, party.Leader, "일반 리더 재선정은 기존처럼 즉시");
	}

	// 검증 F1: 접근 제한 거리 — 선택한 공격 대상은 검사에서 제외하고 다른 근접 적과의 점유 중심 거리가 설정값 미만이면 그 위치를 거른다(같으면 통과).
	[Test]
	public void ApproachLimit_FiltersPositionsNearOtherMelee_ExcludesAttackTarget()
	{
		LeaderCommandSettings.ApproachLimitOverride = 2f;
		var h = NewHuman("H"); var a = NewEnemy("A"); var b = NewEnemy("B");
		a.position = new Vector2Int(0, 3); b.position = new Vector2Int(5, 0);
		h.personalSpottedEnemies.Add(a); h.personalSpottedEnemies.Add(b);

		Assert.IsFalse(FocusAttackSystem.ApproachLimitAllows(h, new Vector2Int(4, 0), a), "B와 1칸인 위치는 제외");
		Assert.IsTrue(FocusAttackSystem.ApproachLimitAllows(h, new Vector2Int(3, 0), a), "B와 정확히 2칸이면 통과");
		Assert.IsTrue(FocusAttackSystem.ApproachLimitAllows(h, new Vector2Int(0, 2), a), "대상 A와의 거리는 검사하지 않는다");

		LeaderCommandSettings.ApproachLimitOverride = 0f;
		Assert.IsTrue(FocusAttackSystem.ApproachLimitAllows(h, new Vector2Int(4, 0), a), "설정값 0은 추가 제한 없음");
		LeaderCommandSettings.ApproachLimitOverride = null;
	}

	// 검증 F1: 허용 경로가 없다는 상태가 3초 이어지면 그 파티원의 지정만 끝나고, 중간에 허용되면 시계가 풀린다. 길을 모르는 경우(이동 알고리즘 없음)·설정 0·다른 대상은 제한하지 않는다.
	[Test]
	public void ApproachLimit_BlockedThreeSeconds_EndsOnlyThatMembersFocus()
	{
		LeaderCommandSettings.ApproachLimitOverride = 2f;
		var leader = NewHuman("L", 50f); var p = NewHuman("P"); var q = NewHuman("Q");
		NewParty(PartyConcept.Rogue, leader, p, q);
		var a = NewEnemy("A"); var other = NewEnemy("O");
		CommandIssueSystem.GiveFocus(p, FocusTemplate("c1", a, "L"), 0f);
		CommandIssueSystem.GiveFocus(q, FocusTemplate("c1", a, "L"), 0f);
		var order = p.LeaderState.GeneralFocus;

		FocusAttackSystem.NoteApproachBlocked(p, order, true, 10f);
		FocusAttackSystem.NoteApproachBlocked(p, order, true, 12.9f);
		Assert.IsNotNull(p.LeaderState.GeneralFocus, "3초 전에는 유지");
		FocusAttackSystem.NoteApproachBlocked(p, order, false, 12.95f); // 허용 경로가 생김 → 시계 초기화
		FocusAttackSystem.NoteApproachBlocked(p, order, true, 13f);
		FocusAttackSystem.NoteApproachBlocked(p, order, true, 15.9f);
		Assert.IsNotNull(p.LeaderState.GeneralFocus, "중간에 풀렸으니 새로 3초를 센다");
		FocusAttackSystem.NoteApproachBlocked(p, order, true, 16f);
		Assert.IsNull(p.LeaderState.GeneralFocus, "허용 경로 없음이 3초 이어지면 이 파티원의 지정만 끝난다");
		Assert.IsNotNull(q.LeaderState.GeneralFocus, "다른 파티원의 지정은 유지");

		// 제한하지 않는 경우: 지정 없음 / 지정 대상이 아닌 추격 / 이동 알고리즘이 없어 길을 모름 → 항상 true, 막힘 기록 없음.
		var r = NewHuman("R");
		Assert.IsTrue(FocusAttackSystem.AllowsChaseStep(r, a, 20f), "지정이 없으면 제한하지 않는다");
		CommandIssueSystem.GiveFocus(r, FocusTemplate("c2", a, "L"), 0f);
		Assert.IsTrue(FocusAttackSystem.AllowsChaseStep(r, other, 20f), "지정 적이 아닌 대상을 쫓을 때는 제한하지 않는다");
		Assert.IsTrue(FocusAttackSystem.AllowsChaseStep(r, a, 20f), "가는 길을 아직 모르면 수행 불가로 확정하지 않는다");
		Assert.AreEqual(-1f, r.LeaderState.GeneralFocus.PathBlockedSince, "막힘 시계도 돌지 않는다");
		LeaderCommandSettings.ApproachLimitOverride = 0f;
		Assert.IsTrue(FocusAttackSystem.AllowsChaseStep(r, a, 20f), "설정값 0은 사용 안 함");
		LeaderCommandSettings.ApproachLimitOverride = null;
	}

	// 검증 F2: 연결 기술이 공격 대상과 다른 적을 겨누면 그 적 쪽으로 방향·공격 각도를 맞춘다(사거리 판정과 시전이 같은 히트박스 방향을 쓰도록). 기술을 고르지 못했으면 호출 전 방향이 유지된다.
	[Test]
	public void CommandSkillSelector_AimAt_PointsTowardNamedEnemy_NoPickRestoresAim()
	{
		var h = NewHuman("H"); var e = NewEnemy("E");
		h.position = new Vector2Int(0, 0); e.position = new Vector2Int(3, 3);
		CommandSkillSelector.AimAt(h, e);
		Assert.AreEqual(Dir.UP_RIGHT, h.currentDir);
		Assert.AreEqual(Mathf.PI / 4f, h.CombatState.State.currentAttackAngle, 1e-4f);

		h.currentDir = Dir.LEFT; h.CombatState.State.currentAttackAngle = Mathf.PI;
		CommandIssueSystem.GiveAction(h, new ActionOrder { CommandId = "a1", Command = TacticalCommand.Suppress, Authority = CommandAuthority.Leader, IssuerName = "L", SuppressTarget = e }, Time.time);
		Assert.IsFalse(CommandSkillSelector.TryPickActive(h, null, out _, out _), "스킬 목록이 없어 고를 기술이 없다");
		Assert.AreEqual(Dir.LEFT, h.currentDir, "고르지 못했으면 방향을 바꾸지 않는다");
		Assert.AreEqual(Mathf.PI, h.CombatState.State.currentAttackAngle, 1e-4f);
	}
}
#endif
