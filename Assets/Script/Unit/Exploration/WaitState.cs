using UnityEngine;

// 10장: 대기의 하위 정보. Goal_Wait가 고착 상태를 유지하는 동안 Action_Wait이 이 레코드를 읽고
// 대기 목적이 해결됐는지 확인한다.
public enum WaitReason
{
	// 10장 2번째 상황: 적 위치 접근 전 합류 유닛의 도착을 기다림.
	AwaitingJoinBeforeApproach,
	// 10장 3번째 상황 + 11장(집결): 집결 위치에 먼저 도착해 다른 파티원을 기다림 — Party.RallyPoint 사용.
	AwaitingPartyAtRallyPoint,
}

public class WaitState
{
	public WaitReason Reason;
	public Vector2Int? WaitPosition;
}
