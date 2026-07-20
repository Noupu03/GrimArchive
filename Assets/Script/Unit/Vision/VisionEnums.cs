// 시야-인지-반응 시스템의 열거형 모음.
// 기준 문서: Assets/문서/공식문서/시야인지반응/
//   01_시야·인지범위·가시성_개념_Current (v0.2), 01-A_...연산공식_Current (v0.2)
//   02_인지·정보판정·실패처리_시스템_v0.2

// 02문서 3장/12장: 인지 판정의 기본 결과 3종(안전 확인/간접 인지는 대상 판정 결과가 아니라 별도
// 처리 경로라 이 enum에는 포함하지 않는다 — PerceptionRecord.Outcome이 이 값을 들고 있는다).
public enum PerceptionOutcome
{
	Unrecognized,        // 미인식 — 안전타일로 처리
	SuspiciousTile,       // 수상한 타일 — 경계 상태로 접근해 재확인
	AccuratePerception,   // 정확 인지 — 인지 결과 정보/반응 후보 생성
}

// 02문서 9장: 정신력 단계(인류 전용). 기존 가중치 시스템의 MentalErrorState(9-2장/23장, 정보 오차용
// 2단계 판정)와는 목적이 달라 별도 enum으로 둔다 — 판단 근거는 PerceptionMath.GetMentalTier 주석 참고.
public enum MentalTier
{
	Panic,    // 공황 -3단계
	Fear,     // 공포 -2단계
	Tension,  // 긴장 -1단계
	Stable,   // 안정  0단계
	Calm,     // 냉정 +1단계
	Excited,  // 흥분 +2단계
}

// 02문서 15장: 정확 인지 시 생성되는 반응 후보 5종. 04_탐색반응·경계·조사·함정대응/06_전투반응·기습·
// 방어반응/08_전파·소리·간접입력 문서가 아직 없어 실제로 후보를 "선택해서 행동"으로 옮기는 하위
// 시스템은 없다 — VisionDirectionReason과 동일한 전례로, 미래 연결을 위해 후보 자체만 스캐폴딩한다.
public enum PerceptionReactionCandidate
{
	CombatResponse,   // 전투 반응 후보 (적 유닛)
	TargetCandidate,  // 타겟 후보 (적 유닛)
	Bypass,           // 우회 후보 (적 유닛/함정/건물)
	Propagate,        // 전파 후보 (적 유닛/함정/건물/시체/전멸 흔적) — 인류 전용(26장)
	Alert,            // 경계 후보 (함정/시체/전멸 흔적)
	DisarmTrap,       // 함정 해제 후보
	Investigate,      // 조사 후보 (건물/시체/전멸 흔적)
	Destroy,          // 파괴 후보 (함정/건물)
}

// 시야 방향 전환 후보의 발생 사유 (01-A 11장 우선순위표 1~10 + 01장 13~14절의 "플레이어 명령" 특례).
// 숫자가 작을수록 우선순위가 높다는 규칙은 VisionMath.PriorityRank()가 담당하고, 이 enum 자체는
// 순서에 의미를 두지 않는다(선언 순서와 우선순위 표 순서를 맞춰뒀을 뿐, 실제 랭크는 PriorityRank 참고).
public enum VisionDirectionReason
{
	PlayerCommand,       // 표 밖 특례: "플레이어의 명령에 대한 시야 전환은 항상 최우선 순위" — 랭크 0
	SkillUse,            // 1순위: 스킬 사용 중
	HighThreatSurprise,  // 2순위: 고위험 기습 피격 (기대/실제 피해가 근접 공격 기대값의 2배 이상)
	AdjacentMeleeTarget, // 3순위: 1칸 이내 근접 전투 대상 존재
	CurrentAttackTarget, // 4순위: 현재 공격 대상 존재
	LeaderCommand,       // 5순위: 리더 명령 — 09_명령·리더 문서 부재로 현재 발생 지점 없음
	NormalSurprise,      // 6순위: 일반 기습 피격 — 06_전투반응·기습 문서 부재로 현재 발생 지점 없음
	SoundDetected,       // 7순위: 소리 감지 — 08_전파·소리 문서 부재로 현재 발생 지점 없음
	UnconfirmedTile,     // 8순위: 확인이 필요한 비어있지 않은 타일 — 01-A 7장 구현으로 방향 전환은 연결됨
	                     // (2026-07-20). "경로 재설정 지점" 쪽은 여전히 10_목표설정 문서 부재로 미연결.
	Alert,               // 9순위: 경계 상태 — 02문서 20장 구현으로 연결됨(가장 가까운 수상한 타일 방향)
	Moving,              // 10순위: 이동 중
}
