#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
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
		Party.RallyClock = () => Time.time;
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

	private static Party NewPartyWithId(string id, PartyConcept concept, params Human[] members)
	{
		var p = NewParty(concept, members);
		p.Id = id; // 시체의 소유 파티 조회(OwnerPartyId)가 파티를 가려내려면 파티마다 Id가 달라야 한다
		return p;
	}

	// 16-2·18-1장: 상위 지휘관의 사망은 같은 원정의 다른 소속 파티원에게도 "자신에게 적용되는 상위 지휘관의 사망"이다 — 직접 확인한 대원은 −20(×0.95)과 능력치 충격.
	// 점령 파티는 원정이 아니라(웨이브와 따로 상주) 전파 대상이 아니다. 세션·맵이 없어 전파(전파 범위 판정) 경로가 아니라 사망 순간 직접 목격 경로를 확인한다.
	[Test]
	public void SuperiorDeath_ShocksSameExpeditionOtherParty_NotOccupyParty()
	{
		var superior = NewHuman("S", 70f); var a1 = NewHuman("A1", 30f, mental: 30f);
		var partyA = NewPartyWithId("pA", PartyConcept.Adventurer, superior, a1);
		var b1 = NewHuman("B1", 30f, mental: 30f);
		var partyB = NewPartyWithId("pB", PartyConcept.Rogue, NewHuman("LB", 50f), b1);
		var o1 = NewHuman("O1", 30f, mental: 30f);
		var occupy = NewPartyWithId("pO", PartyConcept.Rogue, NewHuman("LO", 50f), o1);
		occupy.Type = PartyType.Occupy;
		superior.LeaderState.IsSuperiorCommander = true;
		a1.CombatStat.physicalDefense = 10f; b1.CombatStat.physicalDefense = 10f; o1.CombatStat.physicalDefense = 10f;
		float aBefore = a1.BaseStat.mental, bBefore = b1.BaseStat.mental, oBefore = o1.BaseStat.mental;

		superior.hp = 0f;
		PartyDeathSystem.OnPartyMemberDied(superior, "corpse-superior", new List<Party> { partyA, partyB, occupy });

		var record = partyA.DeathRecords["corpse-superior"];
		Assert.IsTrue(record.DeadWasLeader && record.DeadWasSuperior);
		Assert.AreEqual(aBefore - 19f, a1.BaseStat.mental, 0.05f, "자기 파티원: 직접 확인 −20 × 안정 단계 0.95");
		Assert.AreEqual(8f, a1.CombatStat.physicalDefense, 1e-4f);
		Assert.AreEqual(bBefore - 19f, b1.BaseStat.mental, 0.05f, "같은 원정의 다른 파티원도 같은 사망 충격을 받는다");
		Assert.AreEqual(8f, b1.CombatStat.physicalDefense, 1e-4f);
		Assert.AreEqual(oBefore, o1.BaseStat.mental, 1e-4f, "점령 파티는 같은 원정이 아니라 사망 정보·충격이 전파되지 않는다");
		Assert.AreEqual(10f, o1.CombatStat.physicalDefense, 1e-4f);

		// 일반 리더(상위 지휘관이 아님)의 사망은 다른 파티로 번지지 않는다 — 자기 파티원만.
		var leader = NewHuman("L2", 50f); var m = NewHuman("M2", 30f, mental: 30f);
		var partyC = NewPartyWithId("pC", PartyConcept.Adventurer, leader, m);
		var b2 = NewHuman("B2", 30f, mental: 30f);
		var partyD = NewPartyWithId("pD", PartyConcept.Rogue, NewHuman("LD", 50f), b2);
		float b2Before = b2.BaseStat.mental;
		leader.hp = 0f;
		PartyDeathSystem.OnPartyMemberDied(leader, "corpse-L2", new List<Party> { partyC, partyD });
		Assert.IsFalse(partyC.DeathRecords["corpse-L2"].DeadWasSuperior);
		Assert.AreEqual(b2Before, b2.BaseStat.mental, 1e-4f, "일반 리더 사망은 다른 파티원에게 전파되지 않는다");
	}

	// 18-2장 "관련 없는 다른 파티는 일반 사실 정보만 기록한다": 다른 파티의 리더 시체를 뒤늦게 발견해도 리더 사망 효과(−20·능력치 충격)가 아니라 일반 사망 감소(−10 × 0.95)뿐이다.
	[Test]
	public void LeaderCorpseDiscovered_ByUnrelatedParty_OrdinaryDeathLossWithoutShock()
	{
		var leader = NewHuman("L", 50f); var m = NewHuman("M", 30f, mental: 30f);
		var partyA = NewPartyWithId("pA", PartyConcept.Adventurer, leader, m);
		var stranger = NewHuman("X", 30f, mental: 30f);
		stranger.CombatStat.physicalDefense = 10f;
		var partyX = NewPartyWithId("pX", PartyConcept.Rogue, NewHuman("LX", 50f), stranger);
		var parties = new List<Party> { partyA, partyX };

		leader.hp = 0f;
		PartyDeathSystem.OnPartyMemberDied(leader, "corpse-L", parties);
		Assert.IsTrue(partyA.DeathRecords["corpse-L"].DeadWasLeader);

		var corpse = new InteractableObject("corpse-L", new Vector3Int(0, 0, 1), 0f, tags: new List<string> { "Corpse", "Human" }) { OwnerPartyId = "pA" };
		float before = stranger.BaseStat.mental;
		PartyDeathSystem.OnCorpseDiscovered(stranger, corpse, parties);
		Assert.AreEqual(before - 9.5f, stranger.BaseStat.mental, 0.05f, "관련 없는 파티의 발견자는 일반 사망 감소 −10 × 0.95뿐");
		Assert.AreEqual(10f, stranger.CombatStat.physicalDefense, 1e-4f, "리더 사망 충격은 걸리지 않는다");
	}

	// R4: 집결 도착 예정 시각의 걸음 수는 직선거리가 아니라 실제 경로 길이다 — 벽을 돌아가야 하는 대원의 기한이 일찍 잡히지 않는다. 다른 층이거나 길을 못 찾으면 예전처럼 직선거리.
	[Test]
	public void RallyEta_WalkSteps_UsesPathAroundWall_StraightWhenOtherFloor()
	{
		var saved = Unit.humanFactionData;
		try
		{
			const int w = 14, h = 10;
			System.Func<int, int, int> tile = (x, y) => (x == 6 && y < 9) ? 2 : 1; // x=6 세로 벽, y=9에만 틈
			var map = new int[w, h];
			for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) map[x, y] = tile(x, y);
			Unit.humanFactionData = new FactionData { discoveredMap = new[] { map } };

			var human = ScriptableObject.CreateInstance<Human>();
			human.unitType = new Knight();
			human.currentFloor = 0;
			human.position = new Vector2Int(2, 0);
			for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) human.personalMap.RevealTile(new Vector3Int(x, y, 0), tile(x, y) == 2);

			var goal = new Vector2Int(10, 0);
			int straight = AIMovementHelper.ChebyshevDistance(human.position, goal);
			Assert.AreEqual(8, straight);
			Assert.GreaterOrEqual(Party.EstimateRallyWalkSteps(human, goal, 0), 16, "벽을 돌아 틈(y=9)을 지나 내려오는 길이 직선 8칸보다 훨씬 길다");
			Assert.AreEqual(straight, Party.EstimateRallyWalkSteps(human, goal, 1), "집결 자리가 다른 층이면 직선거리로 대신한다");
		}
		finally { Unit.humanFactionData = saved; }
	}

	// ── 5-2장·순서도 P02: 미집결자 수색 이동 ───────────────────────────────────────────────
	// 집결 명령을 못 받아 안 온 대원 A(마지막 확인 위치가 멀다)와 이미 자리에 선 대원 B가 있는 집결 상황. 기한(8초)은 이미 지났고, 시계는 Party.RallyClock으로 주입한다.
	private float _rallyNow;

	private Party NewRallyParty(out Human leader, out Human absent, out Human present, Vector2Int lastSeen)
	{
		_rallyNow = 100f;
		Party.RallyClock = () => _rallyNow;
		leader = NewHuman("L", 50f); absent = NewHuman("A", 30f); present = NewHuman("B", 30f);
		leader.position = new Vector2Int(5, 5); present.position = new Vector2Int(6, 5); absent.position = new Vector2Int(30, 5);
		var party = NewParty(PartyConcept.Adventurer, leader, absent, present);
		party.IsRallyActive = true;
		party.RallyPoint = new Vector2Int(5, 5);
		party.RallyFloor = 1;
		party.RallyStartTime = _rallyNow - 9f;
		leader.currentWait = new WaitState { Reason = WaitReason.AwaitingPartyAtRallyPoint, WaitPosition = new Vector2Int(5, 5), WaitFloor = 1 };
		present.currentWait = new WaitState { Reason = WaitReason.AwaitingPartyAtRallyPoint, WaitPosition = new Vector2Int(6, 5), WaitFloor = 1 };
		party.Command.LeaderLastSeen["A"] = (lastSeen, 1, 40f);
		return party;
	}

	// 기한 뒤: 리더가 마지막 확인 위치로 나가고, 도착 후 5초 관찰하고, 못 찾으면 제외한 뒤 집결지로 돌아와야 집결이 끝난다.
	[Test]
	public void RallySearch_LeaderTravelsObservesFiveSeconds_ExcludesUnfound_ThenReturnsBeforeCompletion()
	{
		var party = NewRallyParty(out var leader, out var absent, out var present, new Vector2Int(14, 5));
		party.CheckRallyComplete();
		Assert.IsTrue(party.IsRallyActive, "리더가 나가 있는 동안 집결은 끝나지 않는다");
		Assert.AreEqual(WaitReason.SearchingAbsentee, leader.currentWait.Reason);
		Assert.AreEqual(new Vector2Int(14, 5), leader.currentWait.WaitPosition.Value, "미집결자의 마지막 확인 위치로 간다");

		leader.position = new Vector2Int(14, 5); leader.currentWait.AtSlot = true; // 도착
		_rallyNow = 101f; party.CheckRallyComplete();                               // 관찰 시작(101초)
		_rallyNow = 105.9f; party.CheckRallyComplete();
		Assert.AreEqual(WaitReason.SearchingAbsentee, leader.currentWait.Reason, "도착 후 5초가 끝나기 전에는 계속 관찰");
		_rallyNow = 106f; party.CheckRallyComplete();
		Assert.AreEqual(WaitReason.AwaitingPartyAtRallyPoint, leader.currentWait.Reason, "관찰이 끝나면 집결 대기로 돌아온다");
		Assert.AreEqual(new Vector2Int(5, 5), leader.currentWait.WaitPosition.Value, "리더의 자리는 집결지");
		Assert.IsTrue(party.IsRallyActive, "리더가 집결지로 돌아오기 전에는 집결을 끝내지 않는다");

		leader.position = new Vector2Int(5, 5);
		party.CheckRallyComplete();
		Assert.IsFalse(party.IsRallyActive, "찾지 못한 A는 미확인 상태로 제외 — 리더가 돌아오면 집결 완료");
		Assert.IsTrue(party.ReadyToAdvance);
	}

	// 수색 중 시야로 다시 본 미집결자는 집결 명령을 받아 집결지로 걸어온다(제외되지 않는다) — 그가 도착해야 집결이 끝난다.
	[Test]
	public void RallySearch_FoundMemberReceivesRallyOrder_NotExcluded_RallyWaitsForArrival()
	{
		var party = NewRallyParty(out var leader, out var absent, out var present, new Vector2Int(14, 5));
		var room = new Room(); leader.currentRoom = room; absent.currentRoom = room; // 같은 방 — 보이면 집결 명령이 닿는다
		party.CheckRallyComplete();
		Assert.AreEqual(WaitReason.SearchingAbsentee, leader.currentWait.Reason);

		leader.position = new Vector2Int(14, 5);
		leader.visiblePartyMembers.Add(absent); // 수색 중 A를 시야로 확인
		_rallyNow = 101f; party.CheckRallyComplete();

		Assert.IsNotNull(absent.currentWait, "발견한 A에게 집결 명령(자리)이 전달된다");
		Assert.AreEqual(WaitReason.AwaitingPartyAtRallyPoint, absent.currentWait.Reason);
		Assert.IsFalse(absent.currentWait.IsParked, "발견한 사람은 제외(현재 위치에서 인정)하지 않고 집결지로 오게 둔다");
		Assert.AreEqual(WaitReason.AwaitingPartyAtRallyPoint, leader.currentWait.Reason, "다 찾았으니 수색을 끝내고 리더가 돌아간다");
		Assert.IsTrue(party.IsRallyActive);

		leader.position = new Vector2Int(5, 5);
		party.CheckRallyComplete();
		Assert.IsTrue(party.IsRallyActive, "찾은 A가 아직 도착하지 않았다");
		absent.position = absent.currentWait.WaitPosition.Value;
		party.CheckRallyComplete();
		Assert.IsFalse(party.IsRallyActive, "A가 자리에 도착하면 집결 완료");
	}

	// 이미 곁에 있는 마지막 위치는 이동 없이 관찰만 하고, 5초 뒤 예전처럼 곧바로 끝낸다(수색 이동 도입 전과 같은 동작).
	[Test]
	public void RallySearch_LastSeenAdjacentToLeader_NoTravel_EndsAfterFiveSecondsLikeBefore()
	{
		var party = NewRallyParty(out var leader, out _, out _, new Vector2Int(6, 6));
		party.CheckRallyComplete();
		Assert.AreEqual(WaitReason.AwaitingPartyAtRallyPoint, leader.currentWait.Reason, "이미 곁이라 리더는 움직이지 않는다");
		Assert.IsTrue(party.IsRallyActive);
		_rallyNow = 104.9f; party.CheckRallyComplete();
		Assert.IsTrue(party.IsRallyActive, "관찰 5초가 끝나기 전");
		_rallyNow = 105f; party.CheckRallyComplete();
		Assert.IsFalse(party.IsRallyActive, "이동한 적도 찾은 사람도 없으면 5초 뒤 곧바로 끝난다");
	}

	// 수색 위치에 끝내 못 가도 이동은 20초에서 끊고 그 자리에서 5초 관찰한다.
	[Test]
	public void RallySearch_TravelCappedAtTwentySeconds_ThenObservesFiveSeconds()
	{
		var party = NewRallyParty(out var leader, out _, out _, new Vector2Int(14, 5));
		party.CheckRallyComplete();
		leader.position = new Vector2Int(9, 5); // 가던 중에 막혀 집결지에서 떨어진 채 못 도착한다
		_rallyNow = 119.9f; party.CheckRallyComplete();
		_rallyNow = 120f; party.CheckRallyComplete(); // 이동 상한 도달 → 관찰 시작(120초)
		_rallyNow = 124.9f; party.CheckRallyComplete();
		Assert.AreEqual(WaitReason.SearchingAbsentee, leader.currentWait.Reason, "이동 상한 뒤 5초가 끝나기 전");
		_rallyNow = 125f; party.CheckRallyComplete();
		Assert.AreEqual(WaitReason.AwaitingPartyAtRallyPoint, leader.currentWait.Reason, "관찰이 끝나면 집결지로 돌아온다");
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
