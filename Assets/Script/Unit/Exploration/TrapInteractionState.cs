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

	// 9-3장: 미기록 함정 발견 시 5초 동안 더 높은 성공률의 합류 유닛을 기다린다. 기록된 함정이거나
	// 이미 5초가 지났으면 true.
	public bool JoinWaitElapsed;
	public float JoinWaitTimer;

	// 9-3장: 5초 동안 이 유닛보다 더 높은 예상 성공률의 아군이 합류 의사를 전파했는지 — 있으면
	// 그 아군이 실제 해제를 맡고, 이 유닛(발견자)은 Action_Wait이 아니라 이 상태를 유지한 채
	// 그 아군이 도착할 때까지 대기한다(9-5장: 함정 대응이라는 하나의 연속 처리).
	public Human HigherRateJoiner;

	// 9-9장: 파괴 진행 중 함정에 누적한 피해.
	public float DestroyProgressDamage;

	// 9-6장: 해제/파괴 중 시야·인지·반응속도 50% 페널티가 적용 중인지 — UnitFunction.UpdateFOV가 읽는다.
	public bool PenaltyActive;

	// 9-7장: 중단됐다가 재개될 때 남은 진행도(50% 손실 후 유지분, 0~1).
	public float DisarmProgress01;
}
