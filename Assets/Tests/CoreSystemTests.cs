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

	// ── 9장·20장: 수복 명령 = 부산물 장치 없음 + 코어 방 클리어 + 수복 자원 ──

	[Test]
	public void Restore_ChecksInDocumentOrder()
	{
		Assert.AreEqual(CoreRestoreBlock.NotDestroyed, CoreMath.CheckRestore(false, false, true, true));
		Assert.AreEqual(CoreRestoreBlock.DeviceAttached, CoreMath.CheckRestore(true, true, true, true), "부산물 장치가 남아 있으면 수복 불가(20장 '수복된 코어에 장치' / 9장)");
		Assert.AreEqual(CoreRestoreBlock.RoomNotCleared, CoreMath.CheckRestore(true, false, false, true));
		Assert.AreEqual(CoreRestoreBlock.NotEnoughResources, CoreMath.CheckRestore(true, false, true, false));
		Assert.AreEqual(CoreRestoreBlock.None, CoreMath.CheckRestore(true, false, true, true));
	}

	[Test]
	public void Restore_DeviceDestroyedOnlyDoesNotRestoreByItself()
	{
		// 20장: 코어 부산물 장치 파괴만 완료 → 코어는 파괴 상태, 건설·생산·문 제어는 수복 전까지 불가.
		Assert.IsFalse(CoreMath.FunctionsActive(destroyed: true));
		Assert.IsTrue(CoreMath.FunctionsActive(destroyed: false));
	}

	// ── 19장: 표시 상태 ──

	[Test]
	public void Status_ReflectsFlags()
	{
		Assert.AreEqual(CoreStatus.Incomplete, CoreMath.StatusOf(false, false, false));
		Assert.AreEqual(CoreStatus.FullyActivated, CoreMath.StatusOf(true, false, false));
		Assert.AreEqual(CoreStatus.Destroyed, CoreMath.StatusOf(true, true, false));
		Assert.AreEqual(CoreStatus.DeviceAttached, CoreMath.StatusOf(true, true, true));
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
#endif
