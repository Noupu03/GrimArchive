#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

// 리더 선정·승계·사망 충격·정신력 완화·기본 버프를 Human/Party 인스턴스로 확인한다(세션 불필요, Unity 런타임 필요). 시계는 LeaderSuccessionSystem.Clock으로 주입한다. 순수 계산은 LeaderCommandTests.cs.
public class LeaderSuccessionTests
{
	private float _now;

	[SetUp]
	public void Setup()
	{
		_now = 100f;
		LeaderSuccessionSystem.Clock = () => _now;
		RetreatSystem.EnabledOverride = true;
		LeaderCommandSettings.EnabledOverride = true;
	}

	[TearDown]
	public void Reset()
	{
		LeaderSuccessionSystem.Clock = () => Time.time;
		RetreatSystem.EnabledOverride = null;
		RetreatSystem.Random01 = () => UnityEngine.Random.value;
		LeaderCommandSettings.EnabledOverride = null;
		LeaderCommandTable.ResetForTests();
	}

	private static Human NewHuman(string name, float baseLeadership, float mental = 30f)
	{
		var h = ScriptableObject.CreateInstance<Human>();
		h.name = name;
		h.currentFloor = 1;
		h.maxHp = 100f; h.hp = 100f;
		h.BaseStat.maxMental = 100f; h.BaseStat.mental = mental;
		h.BaseStat.leadership = baseLeadership;
		return h;
	}

	private static Party NewParty(PartyConcept concept, params Human[] members)
	{
		var p = new Party("p", "테스트 파티") { Type = PartyType.Explore, Concept = concept };
		foreach (var m in members) { p.Members.Add(m); m.party = p; }
		p.AssignLeaderIfNeeded();
		return p;
	}

	// L01: 생존·도주 미확정·기본 통솔력 10 이상 중 기본 통솔력이 높은 쪽이 초기 리더. 충원자의 높은 통솔력만으로 교체하지 않는다.
	[Test]
	public void InitialLeader_HighestBaseLeadershipAbove10_AndNotReplacedByRecruit()
	{
		var low = NewHuman("low", 9f); var a = NewHuman("A", 11f); var b = NewHuman("B", 19f);
		var party = NewParty(PartyConcept.Adventurer, low, a, b);
		Assert.AreSame(b, party.Leader);

		var recruit = NewHuman("R", 90f);
		party.Members.Add(recruit); recruit.party = party;
		party.AssignLeaderIfNeeded();
		Assert.AreSame(b, party.Leader, "통솔력이 더 높은 인원이 충원돼도 자동 교체하지 않는다");

		var none = NewParty(PartyConcept.Rogue, NewHuman("x", 9f), NewHuman("y", 3f));
		Assert.IsNull(none.Leader, "전원 기본 통솔력 10 미만이면 리더 없이 행동한다");
	}

	// L26: 사망 즉시 지휘 종료, 10초 뒤 그 시점의 생존·도주 상태로 후임 선정(사망한 A와 도주 확정 B는 제외).
	[Test]
	public void LeaderDeath_SuccessorChosenTenSecondsLater_ExcludingDeadAndFled()
	{
		var leader = NewHuman("L", 50f); var a = NewHuman("A", 45f); var b = NewHuman("B", 40f); var c = NewHuman("C", 20f);
		var party = NewParty(PartyConcept.Adventurer, leader, a, b, c);
		Assert.AreSame(leader, party.Leader);
		party.Retreat.Mode = RetreatMode.PartyRetreat;
		party.Command.CooldownUntil = _now + 3f; // 사망 당시 쿨타임 3초 남음

		a.hp = 0f;                                   // 후임 후보 A는 10초 전에 사망
		b.Retreat.Mode = RetreatMode.PersonalFlee;   // 통솔력이 높은 B는 개인 도주 확정
		leader.hp = 0f;
		LeaderSuccessionSystem.OnMemberDied(leader);

		Assert.IsNull(party.Leader);
		Assert.IsTrue(party.Command.SuccessionPending);
		Assert.AreEqual(RetreatMode.None, party.Retreat.Mode, "사망 즉시 파티 후퇴가 끝난다");
		Assert.AreEqual(103f, party.Command.CooldownUntil, "남은 쿨타임은 흐르고 후임이 이어받는다");

		_now = 109.9f; LeaderSuccessionSystem.TickParty(party);
		Assert.IsNull(party.Leader, "10초 전에는 후임을 고르지 않는다");
		_now = 110f; LeaderSuccessionSystem.TickParty(party);
		Assert.AreSame(c, party.Leader);
		Assert.IsFalse(party.Command.SuccessionPending);
		Assert.AreEqual(103f, party.Command.CooldownUntil, "후임이 이전 명령을 복원하거나 쿨타임을 초기화하지 않는다");
	}

	[Test]
	public void LeaderDeath_NoEligibleSuccessor_ActsWithoutLeader()
	{
		var leader = NewHuman("L", 50f); var a = NewHuman("A", 5f);
		var party = NewParty(PartyConcept.Rogue, leader, a);
		leader.hp = 0f;
		LeaderSuccessionSystem.OnMemberDied(leader);
		_now = 110f; LeaderSuccessionSystem.TickParty(party);
		Assert.IsNull(party.Leader);
		Assert.IsTrue(party.Command.NoSuccessor);
		Assert.IsFalse(party.Command.SuccessionPending);
		// 후임 없음 — HP 20% 이하이면 출구로 개인 후퇴한다(L27)
		a.hp = 20f;
		Assert.IsTrue(RetreatSystem.EvaluatePersonalRetreat(a, _now));
	}

	// L30: 도주 확정 즉시 지휘 종료, 첫 확인 4초 뒤 시계 시작, Q의 뒤늦은 확인(9초)은 시계를 늦추지 않는다. 각자 처음 알았을 때 정신력 −10.
	[Test]
	public void LeaderFlee_ClockStartsAtFirstConfirmation_MentalLossTenOnce()
	{
		var leader = NewHuman("L", 50f); var p = NewHuman("P", 30f, mental: 30f); var q = NewHuman("Q", 25f, mental: 30f);
		var party = NewParty(PartyConcept.Adventurer, leader, p, q);
		FleeSystem.ConfirmFlee(leader, FleeReason.MentalFloor);
		Assert.IsNull(party.Leader, "도주 확정 즉시 리더의 지휘가 끝난다");
		Assert.IsTrue(party.Command.SuccessionPending);
		Assert.IsFalse(party.Command.SuccessionClockStarted, "아직 아무도 도주를 확인하지 못했다");

		_now = 104f;
		float pBefore = p.BaseStat.mental;
		Assert.IsTrue(MentalSystem.OnFleeKnown(p, "L", direct: true));
		Assert.IsTrue(party.Command.SuccessionClockStarted);
		Assert.AreEqual(104f, party.Command.SuccessionStartedAt);
		Assert.LessOrEqual(pBefore - p.BaseStat.mental, 10.001f);
		Assert.GreaterOrEqual(pBefore - p.BaseStat.mental, 9f, "기본 −10에 기존 보정을 적용한다(일반 파티원 도주 −5와 중복하지 않음)");
		float afterFirst = p.BaseStat.mental;
		Assert.IsFalse(MentalSystem.OnFleeKnown(p, "L", direct: false), "같은 사건 재확인은 추가 감소가 없다");
		Assert.AreEqual(afterFirst, p.BaseStat.mental, 1e-4f);

		_now = 109f;
		MentalSystem.OnFleeKnown(q, "L", direct: false);
		Assert.AreEqual(104f, party.Command.SuccessionStartedAt, "뒤늦은 확인은 승계 시계를 다시 시작하지 않는다");
		_now = 113.9f; LeaderSuccessionSystem.TickParty(party);
		Assert.IsNull(party.Leader);
		_now = 114f; LeaderSuccessionSystem.TickParty(party);
		Assert.AreSame(p, party.Leader, "확정 후 14초에 후임 선정(도주한 리더는 제외)");
	}

	// L04: 리더 본인의 일반 정신력 감소만 현재 통솔력 × 0.5% 완화.
	[Test]
	public void MentalMitigation_OnlyForCommandingLeader_UsesCurrentLeadership()
	{
		var leader = NewHuman("L", 60f); var member = NewHuman("M", 15f);
		NewParty(PartyConcept.Adventurer, leader, member);
		Assert.AreSame(leader, leader.party.Leader);

		float normalLoss = -MentalSystem.ApplyChange(member, -20f);
		float leaderLoss = -MentalSystem.ApplyChange(leader, -20f);
		Assert.AreEqual(normalLoss * 0.70f, leaderLoss, 1e-3f, "현재 통솔력 60 → 30% 완화(일반 정신력 보정 뒤 곱한다)");

		leader.LeaderState.SetLeadershipModifier("t", -30f); // 현재 통솔력 30 → 15% 완화, 사건 당시의 값을 쓴다
		leader.BaseStat.mental = 30f;
		float later = -MentalSystem.ApplyChange(leader, -20f);
		Assert.AreEqual(normalLoss * 0.85f, later, 1e-3f);

		leader.BaseStat.mental = 0f;
		MentalSystem.SetForced(leader, -71f);
		Assert.AreEqual(-71f, leader.BaseStat.mental, 1e-4f, "강제 −71/−100 설정에는 완화를 적용하지 않는다");
	}

	// L29: 능력치 −20% 10초는 중첩하지 않고 시간만 갱신한다. 현재·최대 HP는 제외.
	[Test]
	public void DeathShock_ReducesDetailStats20Percent_NoStackingAndRestoresAfterTenSeconds()
	{
		var h = NewHuman("H", 20f);
		h.CombatStat.physicalDefense = 10f; h.CombatStat.physicalAttack = 20f; h.BaseStat.walkSpeed = 3f;
		float hpBefore = h.hp;

		DeathShockSystem.Apply(h, 100f);
		Assert.AreEqual(8f, h.CombatStat.physicalDefense, 1e-4f);
		Assert.AreEqual(16f, h.CombatStat.physicalAttack, 1e-4f);
		Assert.AreEqual(2.4f, h.BaseStat.walkSpeed, 1e-4f);
		Assert.AreEqual(hpBefore, h.hp, "현재·최대 HP는 제외");

		DeathShockSystem.Apply(h, 105f); // 다른 지휘자 사망 — 40%로 중첩하지 않고 새 10초로 갱신
		Assert.AreEqual(8f, h.CombatStat.physicalDefense, 1e-4f);
		DeathShockSystem.Tick(h, 114.9f);
		Assert.AreEqual(8f, h.CombatStat.physicalDefense, 1e-4f, "마지막 사망을 안 때부터 10초(115초)까지");
		DeathShockSystem.Tick(h, 115f);
		Assert.AreEqual(10f, h.CombatStat.physicalDefense, 1e-4f);
		Assert.AreEqual(3f, h.BaseStat.walkSpeed, 1e-4f);
	}

	// 14장·L26: 기본 버프 — 파티 컨셉이 종류, 현재 통솔력이 강도. 지휘를 잃으면 즉시 끝난다.
	[Test]
	public void BasicBuff_AdventurerDefense_StrengthFromCurrentLeadership_EndsOnLeaderLoss()
	{
		var leader = NewHuman("L", 40f); var m = NewHuman("M", 15f);
		m.CombatStat.physicalDefense = 10f; m.CombatStat.magicalDefense = 10f; m.CombatStat.physicalAttack = 10f;
		var party = NewParty(PartyConcept.Adventurer, leader, m);
		LeaderBuffSystem.RefreshParty(party);
		Assert.AreEqual(12f, m.CombatStat.physicalDefense, 1e-4f, "현재 통솔력 40 → 20% 방어력 증가");
		Assert.AreEqual(12f, m.CombatStat.magicalDefense, 1e-4f);
		Assert.AreEqual(10f, m.CombatStat.physicalAttack, 1e-4f, "모험가 버프는 공격력을 바꾸지 않는다");

		leader.LeaderState.SetLeadershipModifier("t", 20f); // 현재 60 → 30%
		LeaderBuffSystem.RefreshParty(party);
		Assert.AreEqual(13f, m.CombatStat.physicalDefense, 1e-4f, "강도가 바뀌어도 누적 중첩하지 않고 갱신");

		leader.hp = 0f;
		LeaderSuccessionSystem.OnMemberDied(leader);
		Assert.AreEqual(10f, m.CombatStat.physicalDefense, 1e-4f, "리더가 지휘를 잃으면 기본 버프가 즉시 끝난다");
	}

	// 검증 F5: 버프가 걸린 채 시스템을 끄면 UnitFunction이 Tick을 계속 불러(꺼진 상태에서도) 이미 건 델타가 되돌아간다. 꺼진 뒤 건 게 없으면 Tick은 아무것도 하지 않는다.
	[Test]
	public void BasicBuff_RevertedByTick_WhenSystemSwitchedOff()
	{
		var leader = NewHuman("L", 40f); var m = NewHuman("M", 15f);
		m.CombatStat.physicalDefense = 10f; m.CombatStat.magicalDefense = 10f;
		var party = NewParty(PartyConcept.Adventurer, leader, m);
		LeaderBuffSystem.RefreshParty(party);
		Assert.AreEqual(12f, m.CombatStat.physicalDefense, 1e-4f);

		LeaderCommandSettings.EnabledOverride = false;
		m.LeaderState.NextBuffTick = 0f;
		LeaderBuffSystem.Tick(m);
		Assert.AreEqual(10f, m.CombatStat.physicalDefense, 1e-4f, "꺼진 뒤 첫 Tick에서 기본 버프가 되돌아간다");
		Assert.AreEqual(10f, m.CombatStat.magicalDefense, 1e-4f);

		m.LeaderState.NextBuffTick = 0f;
		LeaderBuffSystem.Tick(m); // 건 게 없으니 변화 없음
		Assert.AreEqual(10f, m.CombatStat.physicalDefense, 1e-4f);
	}
}
#endif
