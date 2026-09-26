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
	// 03번 문서 3번 항목: 리더가 전파 범위 밖일 때 코어 발견자가 리더에게 다가가 보고한다. 집결 명령을
	// 받아도 중단하지 않는다(PartyCoreReportSystem.OnCoreDiscovered가 기존 대기를 덮어써 시작한다).
	ReportingCoreToLeader,
	// 05번 문서 10장: 코어 파괴(웨이브 승리) 후 생존자 전원이 탈출 지점으로 귀환. 예전엔
	// playerMoveTarget+isManualMoveCommand=false 조합으로 이동시켰는데, 이 조합을 실제로 소비하는
	// 코드가 어디에도 없어(전부 "&& isManualMoveCommand" AND 조건) 사실상 이동이 발생하지 않는
	// 죽은 경로였다 — AdvancingToNextRoom과 동일하게 WaitState 기반으로 교체.
	Retreating,
}

public class WaitState
{
	public WaitReason Reason;
	public Vector2Int? WaitPosition;
	// ReportingCoreToLeader 전용 — 보고 대상 코어 위치(WaitPosition은 대신 "리더 위치"를 매 틱
	// 실시간으로 다시 읽어야 해서 여기 스냅샷하지 않는다, TacticalFSMState.ExecuteWait 참고).
	public Vector3Int? CorePosition;
}
