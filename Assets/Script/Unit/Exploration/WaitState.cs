using UnityEngine;

// 10장: 대기의 하위 정보. Goal_Wait가 고착 상태를 유지하는 동안 Action_Wait이 이 레코드를 읽고
// 대기 목적이 해결됐는지 확인한다.
public enum WaitReason
{
	// 10장 2번째 상황: 적 위치 접근 전 합류 유닛의 도착을 기다림.
	AwaitingJoinBeforeApproach,
	// 10장 3번째 상황 + 11장(집결): 집결 위치에 먼저 도착해 다른 파티원을 기다림 — Party.RallyPoint 사용.
	AwaitingPartyAtRallyPoint,
	// 05번 문서 1장: 집결 완료 후 진형을 유지해 다음 방(문)으로 이동 — HumanWaveManager.
	// TryFindNextDoorTowardTargetRoom이 목적지를 정한다. "대기"가 아니라 "지정 목적지로 이동"이지만
	// 전투·전술 우선순위에 자연히 밀려야 하므로(00번 4장 순서) WaitState/ExecuteWait 구조를 그대로 쓴다.
	AdvancingToNextRoom,
}

public class WaitState
{
	public WaitReason Reason;
	public Vector2Int? WaitPosition;
}
