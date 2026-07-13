// 시야-인지-반응 시스템의 열거형 모음.
// 기준 문서: Assets/문서/공식문서/시야인지반응/
//   01_시야·인지범위·가시성_개념_Current (v0.2), 01-A_...연산공식_Current (v0.2)

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
	UnconfirmedTile,     // 8순위: 확인이 필요한 비어있지 않은 타일/경로 재설정 지점 — 10_목표설정 문서 부재
	Alert,               // 9순위: 경계 상태 — 04_탐색반응·경계 문서 부재로 현재 발생 지점 없음
	Moving,              // 10순위: 이동 중
}
