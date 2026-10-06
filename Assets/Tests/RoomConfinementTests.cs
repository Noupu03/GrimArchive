#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

// ========================================================================
// 인류 방 이탈 금지 판정 고정 테스트(NUnit, #if UNITY_INCLUDE_TESTS) — RoomConfinementMath는 순수 함수라 Unity 오브젝트 없이 검증한다.
// 사용자 확정(2026-10-05): 리더 지시 이동·낙오자 합류·계단/입구는 예외, 코어 보고·전투 추격·소리 접근은 금지, 리더 없음은 해제.
// ========================================================================

public class RoomConfinementTests
{
	// ── 예외 조합 ──

	[Test]
	public void NoExemption_IsConfined()
	{
		Assert.IsFalse(RoomConfinementMath.IsExempt(new RoomConfinementMath.Exemptions()));
	}

	[Test]
	public void EachExemptionAloneLiftsConfinement()
	{
		Assert.IsTrue(RoomConfinementMath.IsExempt(new RoomConfinementMath.Exemptions { Disabled = true }));
		Assert.IsTrue(RoomConfinementMath.IsExempt(new RoomConfinementMath.Exemptions { NoLeaderToFollow = true }));
		Assert.IsTrue(RoomConfinementMath.IsExempt(new RoomConfinementMath.Exemptions { EntranceSequence = true }));
		Assert.IsTrue(RoomConfinementMath.IsExempt(new RoomConfinementMath.Exemptions { PlayerCommand = true }));
		Assert.IsTrue(RoomConfinementMath.IsExempt(new RoomConfinementMath.Exemptions { GrantedThisTick = true }));
	}

	// ── 리더 지시로 방을 건너는 대기 사유 ──

	[TestCase(WaitReason.AwaitingPartyAtRallyPoint)]
	[TestCase(WaitReason.AdvancingToNextRoom)]
	[TestCase(WaitReason.ApproachingNextDoor)]
	[TestCase(WaitReason.SearchingNextDoor)]
	[TestCase(WaitReason.FormingUpAtDoor)]
	[TestCase(WaitReason.BreachingDoor)]
	[TestCase(WaitReason.EnteringNextRoom)]
	[TestCase(WaitReason.Retreating)]
	public void LeaderOrderWaits_AreExempt(WaitReason reason)
	{
		Assert.IsTrue(RoomConfinementMath.IsLeaderOrderWait(reason));
	}

	// 코어 보고 이동은 리더 지시가 아니라 예외가 아니다(사용자 확정 — 선택하지 않음). 합류 대기 전 접근은 옛 경로라 제외.
	[TestCase(WaitReason.ReportingCoreToLeader)]
	[TestCase(WaitReason.AwaitingJoinBeforeApproach)]
	public void NonOrderWaits_AreNotExempt(WaitReason reason)
	{
		Assert.IsFalse(RoomConfinementMath.IsLeaderOrderWait(reason));
	}

	// ── 걸음 차단 ──

	[Test]
	public void SameRoomTile_IsAllowed()
	{
		Assert.IsFalse(RoomConfinementMath.IsLeaveBlocked(currentRoomId: 3, nextRoomId: 3, nextIsDoorTile: false));
	}

	[Test]
	public void OtherRoomTile_IsBlocked()
	{
		Assert.IsTrue(RoomConfinementMath.IsLeaveBlocked(currentRoomId: 3, nextRoomId: 4, nextIsDoorTile: false));
	}

	[Test]
	public void TileWithoutRoom_IsBlocked()
	{
		Assert.IsTrue(RoomConfinementMath.IsLeaveBlocked(currentRoomId: 3, nextRoomId: -1, nextIsDoorTile: false));
	}

	// 문 타일은 같은 방 소속으로 잡혀도(청크 소유) 자율 이동에선 못 밟는다 — RoomConfinedMovement와 같은 규칙.
	[Test]
	public void DoorTile_IsBlockedEvenIfSameRoom()
	{
		Assert.IsTrue(RoomConfinementMath.IsLeaveBlocked(currentRoomId: 3, nextRoomId: 3, nextIsDoorTile: true));
	}

	// 현재 방이 없는 위치(통로 등)에서는 제한하지 않는다 — RoomConfinedMovement의 _cachedRoom == null 처리와 동일.
	[Test]
	public void NoCurrentRoom_IsNotRestricted()
	{
		Assert.IsFalse(RoomConfinementMath.IsLeaveBlocked(currentRoomId: -1, nextRoomId: 4, nextIsDoorTile: true));
	}
}
#endif
