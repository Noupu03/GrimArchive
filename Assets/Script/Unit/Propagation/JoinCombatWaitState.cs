using UnityEngine;

// 07문서 7장(전투 진입 시 전파 예외) + 07-A 9장(적 인지 후 전투 시작·합류 판정)의 하위 상태.
// 위험도 2단계 이상 + 비근거리(>2칸)에서 적을 정확 인지하면, 발견자는 즉시 전투 대신 합류 의사
// 응답(2초)과 실제 합류(최대 5초)를 기다린다. AlertSearchState와 같은 패턴 — Human.
// currentJoinCombatWait가 null이 아닌 동안만 의미가 있고, 다른 더 높은 우선순위(플레이어 명령 등)가
// 끼어들면 그냥 버려진다.
public class JoinCombatWaitState
{
	public Unit TargetEnemy; // 정확 인지한 적 — 대기 중 이 적이 죽거나 사라지면 상태를 정리한다.

	// true = 발견자(제자리에서 적을 향해 시야 유지 + 방어 태세), false = 합류자(발견자 위치로 이동).
	public bool IsDiscoverer;

	// ── 발견자 전용 ──
	public float ResponseWaitTimer;   // 9-3장: 합류 의사 응답 대기(2초)
	public bool ResponseReceived;     // 합류 의사가 있는 아군을 찾았는지
	public float ActualJoinWaitTimer; // 9-3장: 응답 이후 실제 합류를 기다리는 시간(최대 5초, 임시)

	// ── 합류자 전용 ──
	public Vector2Int RallyTarget; // 발견자 위치로 이동
}
