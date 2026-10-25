#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

// ========================================================================
// 코어 상태·방 클리어 순수 판정 테스트(NUnit, #if UNITY_INCLUDE_TESTS) — 코어 점령 설계명세서 v0.2의 3·4·9·10·11장과 20장 검증 사례 중 순수 판정에 해당하는 것을 고정한다.
// Unity 오브젝트가 필요한 CoreSystem 전이(활성화·파괴·수복·UI)는 플레이/배치 모드 확인 대상이다.
// ========================================================================

public class CoreSystemTests
{
	// ── 10장·20-2장: 최초 완전 활성화 전/파괴 상태에는 조사·공격을 시작하지 않는다 ──

	[TestCase(false, false, false)] // 불완전 활성화 — 공격 불가
	[TestCase(true, false, true)]   // 완전 활성화 — 공격 가능
	[TestCase(true, true, false)]   // 활성화 이력이 있어도 파괴됐으면 불가
	[TestCase(false, true, false)]
	public void IsAttackable_RequiresEverActivatedAndNotDestroyed(bool everActivated, bool destroyed, bool expected)
	{
		Assert.AreEqual(expected, CoreMath.IsAttackable(everActivated, destroyed));
	}

	[Test]
	public void IsAttackable_RestoredCoreIsAttackableAgain()
	{
		// 20-2장: 최초 완전 활성화 이후 수복했고 현재 파괴되지 않은 코어는 조사·공격 대상이다(활성화 이력은 수복 후에도 유지).
		bool everActivated = false, destroyed = false;
		Assert.IsFalse(CoreMath.IsAttackable(everActivated, destroyed));
		everActivated = true;
		Assert.IsTrue(CoreMath.IsAttackable(everActivated, destroyed));
		destroyed = true;
		Assert.IsFalse(CoreMath.IsAttackable(everActivated, destroyed));
		destroyed = false; // 수복 — CoreSystem.TryRestore는 CoreDestroyed만 되돌리고 CoreEverActivated는 건드리지 않는다
		Assert.IsTrue(CoreMath.IsAttackable(everActivated, destroyed), "수복 후에도 활성화 이력이 유지된다");
	}

	// ── 3장·20장: 완전 활성화 조건 = 보스 처치 + 코어 방 클리어 ──

	[Test]
	public void Activate_BossAliveBlocks()
	{
		Assert.AreEqual(CoreActivateBlock.BossAlive, CoreMath.CheckActivate(false, false, bossAlive: true, coreRoomCleared: true));
	}

	[Test]
	public void Activate_HiddenEnemyKeepsRoomUncleared()
	{
		// 20장: 보스 처치 후 은신 적 1명 잔존 → 코어 방 미클리어, 완전 활성화 불가. 은신 유닛도 hostile 집계에 들어가므로 hostileAlive>0 → 미클리어.
		Assert.IsFalse(RoomClearMath.IsCleared(1));
		Assert.AreEqual(CoreActivateBlock.RoomNotCleared, CoreMath.CheckActivate(false, false, bossAlive: false, coreRoomCleared: false));
	}

	[Test]
	public void Activate_AllConditionsMetAllowsIt()
	{
		Assert.AreEqual(CoreActivateBlock.None, CoreMath.CheckActivate(false, false, bossAlive: false, coreRoomCleared: true));
	}

	[Test]
	public void Activate_AlreadyActivatedOrDestroyedIsRefused()
	{
		Assert.AreEqual(CoreActivateBlock.AlreadyActivated, CoreMath.CheckActivate(true, false, false, true), "보스 처치·완전 활성화는 반복하지 않는다(3장 수복 이후)");
		Assert.AreEqual(CoreActivateBlock.Destroyed, CoreMath.CheckActivate(false, true, false, true), "파괴된 코어는 먼저 수복해야 한다");
	}

	// ── 9장: 수복 명령 = 코어 방 클리어 + 수복 자원 ──

	[Test]
	public void Restore_ChecksInDocumentOrder()
	{
		Assert.AreEqual(CoreRestoreBlock.NotDestroyed, CoreMath.CheckRestore(false, true, true));
		Assert.AreEqual(CoreRestoreBlock.RoomNotCleared, CoreMath.CheckRestore(true, false, true));
		Assert.AreEqual(CoreRestoreBlock.NotEnoughResources, CoreMath.CheckRestore(true, true, false));
		Assert.AreEqual(CoreRestoreBlock.None, CoreMath.CheckRestore(true, true, true));
	}

	[Test]
	public void Functions_StopWhileDestroyed()
	{
		Assert.IsFalse(CoreMath.FunctionsActive(destroyed: true), "코어 파괴 중에는 건설·생산·문 제어를 수복 전까지 쓸 수 없다");
		Assert.IsTrue(CoreMath.FunctionsActive(destroyed: false));
	}

	// ── 19장: 표시 상태 ──

	[Test]
	public void Status_ReflectsFlags()
	{
		Assert.AreEqual(CoreStatus.Incomplete, CoreMath.StatusOf(false, false));
		Assert.AreEqual(CoreStatus.FullyActivated, CoreMath.StatusOf(true, false));
		Assert.AreEqual(CoreStatus.Destroyed, CoreMath.StatusOf(true, true));
	}

	[Test]
	public void Describe_NeverEmptyForAnyValue()
	{
		foreach (CoreStatus v in System.Enum.GetValues(typeof(CoreStatus))) Assert.IsNotEmpty(CoreMath.Describe(v));
		foreach (CoreActivateBlock v in System.Enum.GetValues(typeof(CoreActivateBlock))) Assert.IsNotEmpty(CoreMath.Describe(v));
		foreach (CoreRestoreBlock v in System.Enum.GetValues(typeof(CoreRestoreBlock))) Assert.IsNotEmpty(CoreMath.Describe(v));
	}

	// ── 11장: 방 클리어 ──

	[TestCase(FactionType.Wild, true)]
	[TestCase(FactionType.Human, true)]
	[TestCase(FactionType.Player, false)]
	public void HostileToPlayer_IsWildAndHuman(FactionType faction, bool expected)
	{
		Assert.AreEqual(expected, RoomClearMath.IsHostileToPlayer(faction));
	}

	[Test]
	public void HostileToPlayer_UnknownFactionIsNotHostile()
	{
		Assert.IsFalse(RoomClearMath.IsHostileToPlayer(null));
	}

	[Test]
	public void RoomClear_ZeroHostileIsCleared()
	{
		Assert.IsTrue(RoomClearMath.IsCleared(0));
		Assert.IsFalse(RoomClearMath.IsCleared(1));
	}

	[Test]
	public void RoomClear_OnlyRoomsWithPlayerUnitsAreEvaluated()
	{
		Assert.IsFalse(RoomClearMath.ShouldEvaluate(0), "아무도 가지 않은 빈 방이 저절로 확보되면 안 된다");
		Assert.IsTrue(RoomClearMath.ShouldEvaluate(1));
	}
}

// ========================================================================
// 코어 조사 단계(코어 점령 v0.2 10-1장) — 누가 조사하는지(IsCoreInvestigator)와 조사 기록(Party.InvestigatedCoreIds)을 Human/Party 인스턴스만으로 확인한다. 이동·진행·중단은 일반 조사 경로를 그대로 쓰므로 플레이로 확인한다.
// ========================================================================

public class CoreInvestigationTests
{
	static Human NewHuman(string name, Room room)
	{
		var h = UnityEngine.ScriptableObject.CreateInstance<Human>();
		h.name = name;
		h.currentFloor = 1;
		h.maxHp = 100f;
		h.hp = 100f;
		h.currentRoom = room;
		return h;
	}

	static InteractableObject NewCore() => new InteractableObject("Core_1", new UnityEngine.Vector3Int(5, 5, 1), 120f, 0f, new System.Collections.Generic.List<string> { GameSession.CoreTag }, coreHp: 1000f);

	static Party NewParty(params Human[] members)
	{
		var p = new Party("p", "테스트 파티");
		foreach (var m in members) { p.Members.Add(m); m.party = p; }
		p.Leader = members[0];
		return p;
	}

	[Test]
	public void LeaderInTheRoomInvestigates_MembersWait()
	{
		var room = new Room { RoomId = 3, Floor = 1 };
		var leader = NewHuman("L", room);
		var member = NewHuman("M", room);
		NewParty(leader, member);
		var core = NewCore();

		Assert.IsTrue(TacticalFSMState.IsCoreInvestigator(leader, room, core, now: 0f), "리더가 조사한다(10-1장)");
		Assert.IsFalse(TacticalFSMState.IsCoreInvestigator(member, room, core, now: 0f), "파티원은 조사가 끝날 때까지 기다린다");
	}

	[Test]
	public void LeaderAbsent_MemberTakesOverOnlyAfterTheWait()
	{
		var room = new Room { RoomId = 3, Floor = 1 };
		var leader = NewHuman("L", new Room { RoomId = 9, Floor = 1 }); // 리더는 다른 방에 있다
		var member = NewHuman("M", room);
		var second = NewHuman("N", room);
		NewParty(leader, member, second);
		var core = NewCore();

		Assert.IsFalse(TacticalFSMState.IsCoreInvestigator(member, room, core, now: 100f), "기다림이 막 시작됐다 — 아직 대신 맡지 않는다");
		Assert.IsFalse(TacticalFSMState.IsCoreInvestigator(member, room, core, now: 105f));
		Assert.IsTrue(TacticalFSMState.IsCoreInvestigator(member, room, core, now: 113f), "리더가 오래 안 오면 이 방의 첫 파티원이 조사를 맡는다");
		Assert.IsFalse(TacticalFSMState.IsCoreInvestigator(second, room, core, now: 113f), "한 명만 맡는다");
	}

	[Test]
	public void WhileAnotherMemberInvestigates_NobodyStartsASecondOne()
	{
		var room = new Room { RoomId = 3, Floor = 1 };
		var leader = NewHuman("L", room);
		var member = NewHuman("M", room);
		NewParty(leader, member);
		var core = NewCore();

		member.currentInvestigation = new InvestigationState { TargetObjectId = core.Id };
		Assert.IsFalse(TacticalFSMState.IsCoreInvestigator(leader, room, core, now: 0f), "이미 조사 중인 파티원이 있으면 새로 시작하지 않는다");
	}

	[Test]
	public void FleeingMemberDoesNotInvestigate()
	{
		RetreatSystem.EnabledOverride = true;
		try
		{
			var room = new Room { RoomId = 3, Floor = 1 };
			var leader = NewHuman("L", room);
			NewParty(leader);
			leader.Retreat.Mode = RetreatMode.PersonalFlee;
			Assert.IsFalse(TacticalFSMState.IsCoreInvestigator(leader, room, NewCore(), now: 0f), "도주·후퇴 중인 인원은 조사하지 않는다");
		}
		finally { RetreatSystem.EnabledOverride = null; }
	}

	[Test]
	public void InvestigationIsRecordedPerPartyAndNotRepeated()
	{
		var room = new Room { RoomId = 3, Floor = 1 };
		var a = NewParty(NewHuman("A", room));
		var b = NewParty(NewHuman("B", room));
		a.InvestigatedCoreIds.Add("Core_1");

		Assert.IsTrue(a.InvestigatedCoreIds.Contains("Core_1"));
		Assert.IsFalse(b.InvestigatedCoreIds.Contains("Core_1"), "다른 파티는 따로 조사해야 한다 — 조사 기록은 파티 단위다");
	}
}
#endif
