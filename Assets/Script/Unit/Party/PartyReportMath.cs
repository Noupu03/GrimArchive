using UnityEngine;

// 보고 이동이 향할 목적지 종류 — 03번 10장 661~671줄/05번 8장 466~494줄의 우선순위 순서 그대로다.
public enum ReportDestinationKind
{
	None = 0,
	ToLeader = 1,       // 알고 있는 리더 마지막 위치
	ToRally = 2,        // 유효하게 아는 집결 위치
	ToDoor = 3,         // 현재 방에서 발견한 문 주변 대기 위치
	SearchFrontier = 4, // 개인 지도의 이동 가능한 지형에서 시야를 넓혀 리더·파티원·문을 찾는다
}

// 검증문서 03-15: 코어 보고 이동의 순수 판정 함수 모음(부수효과 없음, 테스트 용이).
public static class PartyReportMath
{
	public const int KindCount = 5;
	// TacticalFSMState.ExecuteWait의 집결지 도착 판정과 같은 값.
	public const float ArrivalRadius = 1.5f;
	// 문·프론티어 후보 탐색은 개인 지도 전체를 훑어 비싸므로 이 간격으로만 다시 확인한다(내부 판단).
	public const float DoorScanIntervalSeconds = 1f;

	// 아는 리더 기록을 목적지로 쓸 수 있는가 — 지금 리더에 대한 기록이고, 부재를 이미 확인한 같은 위치가 아니어야 한다.
	public static bool LeaderPositionUsable(bool recordIsForCurrentLeader, Vector2Int recordPosition, Vector2Int? emptyConfirmedPosition)
		=> recordIsForCurrentLeader && !(emptyConfirmedPosition.HasValue && emptyConfirmedPosition.Value == recordPosition);

	public static ReportDestinationKind ResolveDestinationKind(bool leaderUsable, bool rallyValid, bool doorKnown, bool frontierAvailable)
	{
		if (leaderUsable) return ReportDestinationKind.ToLeader;
		if (rallyValid) return ReportDestinationKind.ToRally;
		if (doorKnown) return ReportDestinationKind.ToDoor;
		if (frontierAvailable) return ReportDestinationKind.SearchFrontier;
		return ReportDestinationKind.None;
	}

	public static bool HasArrived(Vector2Int position, Vector2Int target)
		=> Vector2Int.Distance(position, target) <= ArrivalRadius;

	public enum FollowStep { Lost, Hold, Move }

	// 검증 갭 정리(05번 1장 73줄): 다음 문을 모를 때 파티원이 아는 리더 위치 주변을 유지한다. 리더 위치를 모르면 Lost,
	// 추종 반경(체비셰프 거리) 이내면 Hold, 벗어났으면 Move.
	public static FollowStep ResolveFollowStep(bool hasKnownLeader, int chebyshevDistance, int followRadius)
	{
		if (!hasKnownLeader) return FollowStep.Lost;
		return chebyshevDistance > followRadius ? FollowStep.Move : FollowStep.Hold;
	}

	// 검증 04-04(04번 3장·05번 "공동 이동과 개인 행동의 속도"): 진형 자리가 없는 지금은 "리더 기준 합류 반경 이내"를 실제 진형 합류의 근사로 쓴다(사용자 확정 2026-10-01).
	public static bool IsJoinedToLeader(bool hasLeaderOnSameFloor, int chebyshevDistanceToLeader, int joinRadius)
		=> hasLeaderOnSameFloor && chebyshevDistanceToLeader <= joinRadius;

	// 공동 이동 중이고 합류했으면 그 구성원에게 적용할 기본 이동속도는 "함께 이동하는 적용 대상 생존 구성원 중 가장 느린 속도"(slowestMember), 아니면(개인 탐색·보고·전투,
	// 뒤처져 복귀하는 구성원) 자기 속도다. 공동 속도가 자기 속도보다 빠르게 나오는 일은 없게 min으로 고정한다. 경계 감속 등 개인 보정은 이 값 위에 호출부가 곱한다.
	public static float ResolveMoveBaseSpeed(bool inCoMovement, bool joined, float individualSpeed, float slowestMemberSpeed)
		=> inCoMovement && joined ? Mathf.Min(individualSpeed, slowestMemberSpeed) : individualSpeed;
}
