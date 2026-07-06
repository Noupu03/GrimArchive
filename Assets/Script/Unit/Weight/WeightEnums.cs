// 대표 가중치 3종(이해도/위험도/흥미도) 시스템의 열거형 모음.
// 연산공식 문서(GrimArchive_대표가중치_연산공식_v0.7) 1~3장, 23장, 25장 기준.

public enum WeightType
{
	Understanding, // 이해도 0~100
	Danger,        // 위험도 0~999
	Interest,      // 흥미도 0~999
}

// 정보 신뢰도 등급 (2장). 3장 표의 변화량은 이미 이 배율이 반영된 최종값이므로,
// 실제 연산에서는 배율을 다시 곱하지 않는다 — WeightEventTable의 값을 그대로 사용.
public enum InfoType
{
	DirectExperience, // 직접 경험 (피격/상호작용) - 문서상 배율 1.0
	DirectWitness,    // 직접 목격 - 배율 0.5
	Indirect,         // 간접 파악 (전파/소리/상호작용 목격) - 배율 0.25
}

// 정신력 저하 단계 (9-2장, 23장, 25장). 문서는 "정신력 저하 상태"만 언급하고 정확한 임계값을
// 별도로 못박지 않아, mental/maxMental 비율 기준으로 WeightMath.GetMentalErrorState()가 판정한다.
// 판단 근거는 구현현황 문서에 기재.
public enum MentalErrorState
{
	Normal, // 오차 없음
	Fear,   // 공포 단계 - 위치 ±1타일, 수치 ±15%
	Panic,  // 공황 단계 - 위치 ±2타일, 수치 ±25%
}

// 위험도 단계 (5-4장 / 12장)
public enum DangerStage
{
	Stage0,   // 0~99
	Stage1,   // 100~299
	Stage2,   // 300~599
	Stage3,   // 600~799
	StageMax, // 800~999
}

// 3장(이해도/위험도) + 3-2장(흥미도) 이벤트 ID 전체 목록.
public enum EventId
{
	// 3-1. 이해도 / 위험도
	E_HUMAN_KILL_SEEN,
	E_HUMAN_KILL_INDIRECT,
	E_HIT_HEAVY_SELF,
	E_HIT_HEAVY_SEEN,
	E_HIT_HEAVY_INDIRECT,
	E_MONSTER_HIT_SELF,
	E_MONSTER_HIT_SEEN,
	E_MONSTER_KILL_SELF,
	E_MONSTER_KILL_SEEN,
	E_MONSTER_KILL_INDIRECT,
	E_STATUS_SELF,
	E_STATUS_SEEN,
	E_DEBUFF_SELF,
	E_DEBUFF_SEEN,
	E_SUMMON_SEEN,
	E_BUFF_SEEN,
	E_TRAP_HIT_SELF,
	E_TRAP_HIT_SEEN,
	E_TRAP_DISARM_SELF,
	E_TRAP_DISARM_SEEN,
	E_DEFENSE_DISABLE_SELF,
	E_DEFENSE_DISABLE_SEEN,
	E_CORPSE_TRACE_CHECK,
	E_PARTY_WIPEOUT,
	E_WIPEOUT_TRACE,
	E_WIPEOUT_UNIT_SKILL_SEEN,

	// 3-2. 흥미도 (덧셈형 변화량이 아니라 상태 전이형 처리 — WeightEventTable에는 등록하지 않고
	// HumanKnowledgeBase의 전용 메서드에서 직접 처리, IncidentLog 기록용으로만 EventId 사용)
	E_UNEXPLORED_TILE,
	E_EXPLORED_SAFE_TILE,
	E_INTEREST_OBJECT_FOUND,
	E_OBJECT_INVESTIGATED,
	E_OBJECT_COLLECTED,
	E_OBJECT_DROPPED_BY_CARRIER_DEATH,
	E_OBJECT_DESTROYED,
	E_CORPSE_TRACE_FOUND,
	E_WIPEOUT_TRACE_FOUND,
	E_WIPEOUT_TRACE_CHECK,
}
