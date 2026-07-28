using UnityEngine;

// 4장: 경계 상태의 하위 정보. Goal_Alert는 고착이 아니므로(시야인지반응_03_GOAP목표우선순위표_
// 2026-07-22.txt 3-2절) 이 레코드는 트리거 조건이 계속 참인 동안에만 의미가 있고, 다른 더 높은
// 목표가 끼어들면 그냥 버려진다(다음에 Goal_Alert가 다시 선택될 때 새로 만들어짐).
public class AlertSearchState
{
	// 4-4장(수상한 타일)/4-10장(공격 방향 인지) — 확인해야 할 구체적 위치가 있으면 그 방향으로
	// 접근(Action_AlertApproach), 없으면 주변 수색(Action_AlertPerimeterSearch).
	public Vector2Int? TargetPosition;

	// 4-8장: 전투 종료 후 10초 스윕인지(true) — 이 경우 TargetPosition 없이 그냥 10초만 채우면 된다.
	public bool IsPostCombatSweep;

	// 4-12장: 미식별 공격 수색시간(15초) — 새 공격이 오면 15초로 재설정된다. 전투종료후 스윕은
	// ExplorationMath.PostCombatAlertSeconds(10초)를 쓴다.
	public float ElapsedSeconds;

	// 4-15장(2026-07-27 신규): 원인미상 파티원 사망 수색인지 — true면 ElapsedSeconds 제한을
	// ExplorationMath.DeathSearchSeconds(10초)로 쓰고, TargetPosition으로 수렴하는 대신
	// DeathSearchOrigin에서 AssignedSearchDir 방향으로 퍼져나가며 수색한다(TacticalFSMState.
	// DeathSearchMove).
	public bool IsDeathSearch;
	public Vector2Int? DeathSearchOrigin;
	public Dir AssignedSearchDir;
	// PartyDeathRecord 조회용 — Party.DeathRecords의 키(시체 오브젝트 Id)를 그대로 들고 있는다.
	public string DeathRecordCorpseId;
}
