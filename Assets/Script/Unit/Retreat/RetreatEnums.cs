// 후퇴·도주 시스템 열거형(후퇴도주 문서 2026-10-13: 핵심본 + 설계명세서).

// 후퇴·도주 상태 — 한 번 시작하면 회복이나 리더 사망으로 취소하지 않는다(명세서 1-4·3-4·7-1장). 유닛(Human.Retreat)은 PersonalRetreat/PersonalFlee만 갖고, PartyRetreat은 파티(Party.Retreat)가 갖는다.
public enum RetreatMode
{
	None,            // 후퇴·도주 중 아님
	PartyRetreat,    // 리더가 평균 HP·정신력으로 내린 파티 후퇴 — 걷기·진형 유지
	PersonalRetreat, // 리더 없는 인류의 개인 후퇴(개인 HP ≤20%) — 걷기, 거점 휴식 없이 출구
	PersonalFlee,    // 개인 도주 확정 — 달리기, 평균에서 즉시 제외, 취소·파티 복귀 없음
}

// 후퇴 목적지 종류 — 거점은 인류 소유 방(RetreatBaseProvider), 출구는 던전 밖으로 나가는 지점.
public enum RetreatDestination
{
	None,
	Base,
	Exit,
}

// 거점 종류(핵심본 4-1장) — 기초 시설 완공 = 기초 거점, 방어 시설까지 완공 = 방어 거점. 시설 시스템이 없어 지금은 모든 거점이 Basic이다.
public enum BaseKind
{
	Basic,
	Defense,
}

// 개인 도주가 확정된 이유.
public enum FleeReason
{
	RollSucceeded,  // 공황 구간 5 감소마다 시행하는 확률 판정에서 선택됨
	MentalFloor,    // 정신력 −100 도달(확률 판정 없이 확정)
	EncounterShock, // 격차 +3 이상 몬스터 직접 조우 중 직전 공황 → −100
}

// 정신력 7단계(명세서 3-2장). 기존 6단계 MentalTier(인지 보정표, VisionEnums)와 경계가 달라 별도 타입으로 둔다.
public enum MentalStage
{
	Excited, // 흥분  70 < SP ≤ 100
	Calm,    // 냉정  40 < SP ≤ 70
	Stable,  // 안정  10 < SP ≤ 40
	Normal,  // 평범 −10 ≤ SP ≤ 10
	Tension, // 긴장 −40 ≤ SP < −10
	Fear,    // 공포 −70 ≤ SP < −40
	Panic,   // 공황 −100 ≤ SP < −70 — 전투 불이익 + 도주 판정 대상
}

// 휴식 종료 뒤 일반 파티의 다음 행동(명세서 2-2장).
public enum PostRestAction
{
	LeaveDungeon, // 목표 달성 → 던전 밖으로 후퇴
	Reexplore,    // 목표 미달성 → 탐험 재개(이후 후퇴 기준을 다시 채우면 재휴식 없이 출구)
}

// 파티 후퇴의 진행 단계(Party.Retreat.Phase). Moving → Resting(거점에 한 명이라도 도착) → 휴식 완료 뒤 Leaving(출구) 또는 재탐험/재출격(None으로 복귀).
public enum PartyRetreatPhase
{
	None,
	Moving,  // 거점으로 걷는 중(거점이 없으면 곧바로 Leaving)
	Resting, // 도착한 인원은 휴식, 못 도착한 인원은 계속 이동
	Leaving, // 던전 출구로 이동해 개인별로 던전 밖으로 나간다
}

// 이 틱에 인류가 후퇴 때문에 하는 행동. Move = 후퇴 FSM 상태가 이동을 맡는다(전투 무시), Rest = 거점에서 휴식(전투가 나면 Combat이 우선).
public enum RetreatActivity
{
	None,
	Move,
	Rest,
}

// 후퇴·도주 이동을 막는 요소의 종류(명세서 5장).
public enum RetreatBlockKind
{
	None,
	Door,        // 닫힌 상대 진영 문 — 개방은 없고 가능한 파괴
	Enemy,       // 길을 막는 적 — 통과에 필요한 만큼만 대응
	Trap,        // 우회 불가 함정 — 가능한 파괴, 안 되면 조건부 강행
	Unreachable, // 알려진 정보로는 갈 길이 없음 — 목적지는 유지하고 길 정보를 기다린다
}
