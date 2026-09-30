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
	// 05번 문서 10장: 코어 파괴(웨이브 승리) 후 생존자 전원이 탈출 지점으로 귀환 — AdvancingToNextRoom과
	// 동일하게 WaitState 기반으로 이동한다. playerMoveTarget+isManualMoveCommand=false 조합은 실제로
	// 아무 코드도 소비하지 않는 죽은 경로이니 되돌리지 말 것.
	Retreating,
	// 05번 문서 1장 73줄: 집결을 마쳤는데 다음 이동 문을 아직 모르면 진형을 유지하며 문을 찾는다. 리더 외 파티원이
	// 자기가 아는 리더 위치 주변을 따라다닌다(PartyDoorSearchSystem.StepFollow). 리더는 대기 없이 기존 자유 탐색이 문을 찾는다.
	SearchingNextDoor,
	// 01번 문서 7-1장·04번 문서 소탕 파티: 리더가 다음 이동 문 앞 자리까지 현재 방 안에서 이동하며 실제 시야로 확인한다. 도착하면 집결 판단 자격이 생긴다
	// (Party.MarkLeaderReachedNextDoor) — 집결 뒤 공동 이동(AdvancingToNextRoom)과 달리 이동 명령이 아니라 집결 전 단계다. 소탕 파티 리더 전용.
	ApproachingNextDoor,
}

public class WaitState
{
	public WaitReason Reason;
	public Vector2Int? WaitPosition;
	// AdvancingToNextRoom 전용 — 이동 목표인 문 타일(WaitPosition은 그 문 주변의 이 유닛 대기 자리). 자리가 막히면 같은 문의 다른 자리를 다시 고르는 데 쓴다(검증 04-02).
	public Vector2Int? DoorPosition;
	// AdvancingToNextRoom 전용 — 길이 막히기 시작한 시각(음수 = 막히지 않음). 오래 막힌 채면 안전장치로 명령을 푼다.
	public float BlockedSince = -1f;
	// AwaitingPartyAtRallyPoint 전용 — WaitPosition이 속한 층(Vector2Int라 층 정보가 없다). 유닛이 다른 층에 있으면 그 좌표로 걷지 않고 대기를 접는다.
	public int WaitFloor = -1;
	// ReportingCoreToLeader 전용 — 보고 대상 코어 위치(WaitPosition은 대신 "리더 위치"를 매 틱
	// 실시간으로 다시 읽어야 해서 여기 스냅샷하지 않는다, TacticalFSMState.ExecuteWait 참고).
	public Vector3Int? CorePosition;

	// ReportingCoreToLeader 전용 — 03-15 보고 이동의 진행 상태(PartyCoreReportSystem.StepReportMovement가 관리).
	public ReportDestinationKind ReportKind;
	public Vector2Int ReportTarget;
	// 갈 곳이 없거나 문 주변 대기 위치에 도착해 제자리에서 기다리는 중 — 집결 완료 판정을 붙잡지 않는다(Party.CheckRallyComplete).
	public bool IsParked;
	// 목적지(리더 마지막 위치/집결지)에 도착해 부재 확인 3초 대기를 시작한 시각. 음수면 대기 중 아님.
	public float AbsenceStartTime = -1f;
	public float NextDoorScanTime;
	// 접근이 막혀 잠시 제외한 목적지 종류(ReportDestinationKind 값으로 색인) — 이 시각이 지나면 다시 후보가 된다.
	public readonly float[] ReportBlockedUntil = new float[PartyReportMath.KindCount];

	// SearchingNextDoor 전용 — 아는 리더 위치가 없어진 시각(음수 = 리더 위치를 알고 있음). 일정 시간 넘게 모르면 추종을 접는다.
	public float FollowLeaderLostSince = -1f;
}
