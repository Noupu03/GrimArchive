// 인류 방 이탈 금지 판정(순수) — 몬스터의 RoomConfinedMovement와 같은 규칙을 인류에 적용한다(2026-10-05): 지금 있는 방을 떠나는 걸음(문 타일·다른 방 타일·방이 없는 타일)은 리더 지시 이동·낙오자 합류·계단/입구·플레이어 명령 외엔 막는다. 공황·도주 문서가 나오면 예외를 재고한다.
public static class RoomConfinementMath
{
	// 제한을 풀어 주는 사유 묶음 — 하나라도 true면 방을 떠나도 된다.
	public struct Exemptions
	{
		public bool Disabled;          // kill-switch(AIBehaviorConfig.humanRoomConfinementEnabled) 꺼짐
		public bool NoLeaderToFollow;  // 파티 없음·리더 없음/사망·리더 본인의 방 밖 탐색 허용(Party.LeaderMayExploreBeyondRoom) — 영구 정지 방지
		public bool EntranceSequence;  // 던전 입구 시퀀스(0층 → 1층)
		public bool PlayerCommand;     // 플레이어 이동 명령(몬스터 규칙과 대칭, 인류엔 없음)
		public bool GrantedThisTick;   // 이 틱에 리더 지시 이동·낙오자 합류·계단 이동 리프가 실제로 이동을 수행 중(Human.GrantRoomLeave)
	}

	public static bool IsExempt(in Exemptions e)
		=> e.Disabled || e.NoLeaderToFollow || e.EntranceSequence || e.PlayerCommand || e.GrantedThisTick;

	// 리더 지시로 방을 건너는 대기 사유 — 집결·방 이동(진형·돌파·입장)·문 찾기 추종·퇴각. 코어 보고(ReportingCoreToLeader)와 AwaitingJoinBeforeApproach는 지시 이동이 아니라 제외한다(사용자 확정 2026-10-05).
	public static bool IsLeaderOrderWait(WaitReason reason)
	{
		switch (reason)
		{
			case WaitReason.AwaitingPartyAtRallyPoint:
			case WaitReason.AdvancingToNextRoom:
			case WaitReason.ApproachingNextDoor:
			case WaitReason.SearchingNextDoor:
			case WaitReason.FormingUpAtDoor:
			case WaitReason.BreachingDoor:
			case WaitReason.EnteringNextRoom:
			case WaitReason.Retreating:
				return true;
			default:
				return false;
		}
	}

	// 제한 중인 유닛이 이 걸음(다음 타일)을 못 가는가. 방 id는 모르면 -1. 현재 방이 없으면(통로 등) 제한하지 않고(RoomConfinedMovement와 동일), 다음 타일이 문이거나 방이 없거나 다른 방이면 막는다.
	public static bool IsLeaveBlocked(int currentRoomId, int nextRoomId, bool nextIsDoorTile)
	{
		if (currentRoomId < 0) return false;
		if (nextIsDoorTile) return true;
		return nextRoomId != currentRoomId;
	}
}
