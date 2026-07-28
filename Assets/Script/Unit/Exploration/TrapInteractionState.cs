using UnityEngine;

public enum TrapPhase
{
	AwaitingJoin, // 9-3장: 미기록 함정 5초 합류 대기 중(기록 함정은 이 단계를 건너뜀)
	Disarming,    // Action_TrapDisarmPerform이 실제 해제 시도 진행 중
	Destroying,   // Action_TrapDestroy가 함정 Hp를 깎는 중
}

// 9장: 이 유닛이 지금 진행 중인 함정 대응의 하위 상태. Goal_TrapResponse가 고착(IsSticky) 상태를
// 유지하는 동안 Action_TrapJoinWait/MoveToTrap/TrapDisarmPerform/Bypass/Pass/Destroy가 이 레코드를
// 읽고 갱신한다. 9-5장 통합
// 흐름("함정 정확 인지 → 정보 전파 → 5초 합류 대기 또는 즉시 판단 → 위치 도달 → 처리 → 결과")을
// 전부 이 하나의 상태로 표현한다 — 별도 Goal_Wait로 쪼개지 않은 이유는
// 시야인지반응_03_GOAP목표우선순위표_2026-07-22.txt 1절 참고.
public class TrapInteractionState
{
	public TrapPhase Phase = TrapPhase.AwaitingJoin;
	public string TrapObjectId;
	// 함정은 루팅되지 않는 고정 위치 오브젝트라 위치가 안 바뀐다 — objectGrid를 ID로 역탐색하지 않고
	// 발견 시점 위치를 그대로 들고 다닌다(GameSession.GetObjectAt(Vector3Int) 조회에 사용).
	public Vector3Int TrapPosition;

	// 9-3장(2026-07-27 개정: 5초→2초): 함정 정보 전파 후 2초 동안 발견 유닛이 응답 대기한다. 기록된
	// 함정이거나 발견 유닛이 웨이브 진입 전 파티 내 최고 성공률 유닛이면 즉시 true.
	public bool JoinWaitElapsed;
	public float JoinWaitTimer;

	// 9-2~9-5장(2026-07-27 개편): 파티 전체 성공률 비교 + ETA 동률 우선으로 뽑힌 "해제 담당 유닛"
	// 이름 — TrapPartySystem.ResolveSelection이 JoinWaitElapsed 전환 시점에 채운다. 발견자 자신이
	// 선정되면 IsSelectedDisarmer=true로 계속 해제를 진행하고, 아니면 TrapAwaitSelectedUnit으로 빠진다.
	public string SelectedUnitName;
	public bool IsSelectedDisarmer;
	// 웨이브 진입 전 파티 내 최고 성공률 유닛이 스스로 발견해 2초 응답을 생략한 경우(9-3장).
	public bool AutoConfirmed;

	// 9-7장(신규): 발견 유닛이 선정 유닛의 도착을 기다리는 동안 쓰는 ETA 기준 미도착 타이머와
	// 마지막으로 확인된 선정 유닛 위치. 미도착 유예(ETA+3초)를 넘기면 SearchingForSelectedUnit=true로
	// 전환해 그 위치로 직접 찾아간다.
	public float MissingUnitTimer;
	public bool SearchingForSelectedUnit;
	public Vector2Int? SelectedUnitLastKnownPos;

	// 9-9장: 파괴 진행 중 함정에 누적한 피해.
	public float DestroyProgressDamage;

	// 9-6장: 해제/파괴 중 시야·인지·반응속도 50% 페널티가 적용 중인지 — UnitFunction.UpdateFOV가 읽는다.
	public bool PenaltyActive;

	// 9-7장: 중단됐다가 재개될 때 남은 진행도(50% 손실 후 유지분, 0~1).
	public float DisarmProgress01;

	// 사용자 요청(2026-07-22): "길이 막혀있는 경우만 해제, 안 막혀있으면 그냥 피해간다" — 이 함정이
	// 유닛의 실제 목적지(playerMoveTarget/조사 대상)로 가는 유일한 통로인지(대체 경로 있으면 false)를
	// A* 대체경로 탐색(Action_TrapJoinWait.IsBlockingPath)으로 한 번만 계산해서 캐시해둔다 — 매 틱
	// 다시 계산하면 비용이 크다.
	public bool? IsBlockingPath;
}
