using UnityEngine;

public enum TrapPhase
{
	AwaitingJoin, // 해제·파괴 시작 전 전체(응답 대기·담당자 도착 대기·해제하러 가는 이동) — 집결 명령 시 전환 대상
	Disarming,    // Action_TrapDisarmPerform이 실제 해제 시도 진행 중
	Destroying,   // Action_TrapDestroy가 함정 Hp를 깎는 중
}

// currentTrapInteraction을 끝내는 이유 — TrapPartySystem.EndResponse가 이유별로 조율 기록을 다르게 갱신한다.
public enum TrapEndReason
{
	Resolved,  // 해제 성공 또는 함정이 이미 사라짐
	Bypassed,  // 우회
	Passed,    // 통과
	Destroyed, // 파괴
	Yielded,   // 재선정으로 더 이상 담당이 아니라 양보
	Rally,     // 집결 명령으로 전환
	WaitEnded, // 대기자의 도착 대기 종료
	Unreachable, // 함정에 도달할 수 없어 포기(stuck 탈출)
	WaveEnded, // 웨이브 종료로 진행도 제거(Human.ClearInteractionProgress) — 담당 배정만 푼다
}

// 이 유닛이 지금 진행 중인 함정 대응의 하위 상태. Goal_TrapResponse가 고착(IsSticky) 상태를 유지하는
// 동안 Action_TrapJoinWait/MoveToTrap/TrapDisarmPerform/Bypass/Pass/Destroy가 이 레코드를 읽고
// 갱신한다 — "인지 → 전파 → 합류 대기 → 도달 → 처리 → 결과" 흐름 전체를 하나의 상태로 표현한다.
public class TrapInteractionState
{
	public TrapPhase Phase = TrapPhase.AwaitingJoin;
	public string TrapObjectId;
	// 함정은 루팅되지 않는 고정 위치 오브젝트라 위치가 안 바뀐다 — objectGrid를 ID로 역탐색하지 않고
	// 발견 시점 위치를 그대로 들고 다닌다(GameSession.GetObjectAt(Vector3Int) 조회에 사용).
	public Vector3Int TrapPosition;

	// 9-3장(2026-07-27 개정: 5초→2초): 함정 정보 전파 후 2초 동안 발견 유닛이 응답 대기한다. 발견 유닛이
	// 웨이브 진입 전 파티 내 최고 성공률 유닛(Party.EntryBestDisarmerNames)이면 즉시 true.
	public bool JoinWaitElapsed;
	public float JoinWaitTimer;

	// 9-2~9-5장(2026-07-27 개편, 2026-09-30 03-12에서 복원): 예상 성공률 최고 + 동률이면 ETA 우선으로 뽑힌 "해제 담당 유닛"
	// 이름 — TrapPartySystem.ResolveSelection이 JoinWaitElapsed 전환 시점에 채운다. 발견자 자신이
	// 선정되면 IsSelectedDisarmer=true로 계속 해제를 진행하고, 아니면 TrapAwaitSelectedUnit으로 빠진다.
	public string SelectedUnitName;
	public bool IsSelectedDisarmer;
	// 웨이브 진입 전 파티 내 최고 성공률 유닛이 스스로 발견해 2초 응답을 생략한 경우(9-3장).
	public bool AutoConfirmed;

	// 03번 v0.12 8장(대기자 = 담당자의 도착을 기다리는 발견 유닛): 담당자가 알려온 도착 예정 시점 중 현재
	// 적용 중인 것과 그로 정해진 대기 기한. 순서도 03-12-2 — 더 최근에 계산됐고 도착 예정이 실제로 바뀐
	// 정보만 받아 기한을 갱신한다.
	public bool HasAppliedEstimate;
	public float AppliedCalcTime;
	public float AppliedArrivalTime;                    // 계산 시점 + 남은 예상 이동시간
	public float WaitDeadline = float.PositiveInfinity; // 도착 예정 시점 + 여유 3초
	// 담당자의 도착(또는 대응 종료) 통지를 받았는지 — 받으면 기다림이 끝난다(TrapAwaitSelectedUnit).
	public bool AssigneeDone;
	// 조율 기록의 마지막 보고 중 이미 처리한 것 / 다음 수신 시도 시각(전파 조건 실패 시 재시도 간격).
	public int LastSeenReportSequence;
	public float NextReportPollTime;
	// 담당자 쪽: 다음 도착 예정 재계산 시각 — 경로 탐색 비용을 줄이려고 행동 틱마다가 아니라 일정 간격으로 돈다.
	public float NextProgressCheckTime;

	// 9-9장: 파괴 진행 중 함정에 누적한 피해.
	public float DestroyProgressDamage;

	// 9-6장: 해제/파괴 중 시야·인지·반응속도 50% 페널티가 적용 중인지 — UnitFunction.UpdateFOV가 읽는다.
	// 해제 진행도 누적의 게이트이기도 하다(UnitFunction.OnUpdate): 해제를 실제로 수행 중일 때만 true.
	public bool PenaltyActive;

	// 03번 v0.12 9장·검증문서 03-11: 이번 Tactical 틱에 해제/파괴를 실제로 수행했는지(TacticalFSMState.Tick이 틱
	// 전에 비우고 TrapDisarmPerform/TrapDestroy가 채운다). 수행 없이 틱이 끝나면 다른 분기(Panic·합류 대기 등)가
	// 가로챈 중단으로 본다. DestroyActive는 파괴 진행 누적의 게이트(해제의 PenaltyActive에 대응).
	public bool PerformedThisTick;
	public bool DestroyActive;

	// 9-7장: 중단됐다가 재개될 때 남은 진행도(50% 손실 후 유지분, 0~1).
	public float DisarmProgress01;

	// 이 함정이 유닛의 실제 목적지로 가는 유일한 통로인지(대체 경로 있으면 false)를 A* 대체경로
	// 탐색으로 한 번만 계산해서 캐시해둔다 — 매 틱 다시 계산하면 비용이 크다.
	public bool? IsBlockingPath;

	// ⑫: GetComponent<ObjectProgressBarVisual> 호출 비용을 Disarming 첫 틱에만 내고 이후엔 재사용.
	public ObjectProgressBarVisual CachedProgressBar;
}
