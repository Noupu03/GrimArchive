using System.Collections.Generic;
using UnityEngine;

// 02번 문서(전투 목표와 아군 보호 및 지원) 관련 지속 상태를 한데 묶는다 — 인류/플레이어몬스터/야생
// 공통이라 Unit.CombatTargeting으로 항상 존재한다(null이 "비활성"을 뜻하는 currentInvestigation류
// 패턴과 달리, 여기 각 필드는 "지금 값이 뭔지"를 들고 있는 상시 슬롯이라 래퍼 자체는 항상 생성돼 있다).
public class CombatTargetingState
{
	// 3장: 유지 중인 공격 대상 — CombatFSMState.SelectAttackTarget이 매 틱 다시 고르지 않고 이 값을
	// 기준으로 "유지 vs 교체"(20%/더 높음 기준)를 판단한다.
	public Unit AttackTarget;
	// 4장 보스 집중 — 보스를 공격 대상으로 삼은 뒤에는 임시 위협 대응 중에도 이 값을 계속 들고 있다가,
	// 위협이 해소되면 20% 기준 없이 그대로 복귀한다. 보스가 무효화되면 null로 비운다.
	public Unit BossFocusTarget;
	// 5장: 공격 대상을 바꾼 뒤 실제로 이동한 누적 칸수 — 같은(원래) 대상을 계속 추격하는 동안에는
	// 증가하지 않는다(IsChasingRetargetedEnemy가 true인 동안의 이동에만 누적).
	public int  MovedTilesSinceRetarget;
	public bool IsChasingRetargetedEnemy;
	// 11장 큰 피해 시야전환 — 이번 전투에서 가까운 근접 교전 상대에게 받은 식별 가능한 양의 직접 HP
	// 손실 표본. 함정·지속피해·0피해는 포함하지 않는다(UnitFunction.RecordDirectHitForViewSwitch).
	public readonly List<float> RecentDirectHitLosses = new List<float>();
	// 위 배수 조건을 충족했을 때 공격 방향을 잠깐 들고 있는 신호 — ResolveVisionDirection이
	// VisionDirectionReason.HighThreatSurprise 후보로 소비한다(소리 반응의 PendingSound와 동일 패턴).
	public Vector2Int? PendingHighThreatDirection;
	public float PendingHighThreatUntilTime;
	// 7장 일반 치료 대상 — SkillAction 인스턴스는 유닛 타입 전체가 공유해 개인 상태를 못 담으므로
	// (CLAUDE.md 스킬 시스템 규칙) 여기 유닛 쪽에 지속 보관한다. 작은 체력 변화만으로 계속 바뀌지
	// 않도록 SkillAction_Heal이 유지 조건을 확인한 뒤에만 갱신한다.
	public Unit HealTarget;
	// 8~10장 긴급 아군 보호 대상 — 자기 자신도 후보가 될 수 있다.
	public Unit ProtectTarget;
	// 검증문서 03-13(03번 v0.12 9장 566줄): 전투 중 이동 경로를 함정이 막고 대체 경로가 없어 파괴하기로 한 함정 — CombatFSMState.ChaseTarget이 접근·채널링을 이어간다.
	// 스킬을 실제로 쓰거나(=적과 교전) 전투를 벗어나면 비운다. 정체 카운터는 접근이 계속 막힐 때 포기하는 데 쓴다.
	public Vector3Int? BlockingTrapTile;
	public int TrapDestroyStuckTurns;
	// 검증문서 02-04 4번: 보스로 가는 길을 막고 있어 임시 대응 중인 적과 다음 재확인 시각 — 경로 탐색이 무거워 0.5초마다만 다시 계산하고 그 사이엔 이 값을 재사용한다
	// (CombatFSMState.FindPathBlockingEnemy). 전투를 벗어나면 비운다.
	public Unit PathBlocker;
	public float NextPathBlockCheckTime;

	// 공격을 실제로 실행했거나(회피·방어로 무효여도) 대상이 완전히 바뀌었을 때 호출한다 —
	// 재교체 이동 예산만 초기화(CombatFSMState 3곳에서 반복되던 두 줄을 대체).
	public void ResetRetargetTracking()
	{
		MovedTilesSinceRetarget = 0;
		IsChasingRetargetedEnemy = false;
	}

	// 전투 종료(CombatFSMState.OnExit) 시 호출 — 다음 교전은 새 표본·새 대상 선택으로 시작한다.
	// ProtectTarget/HealTarget은 전투와 무관하게(비전투 중에도 아군 보호·치료가 있을 수 있음) 여기서
	// 건드리지 않는다 — 각자 자신의 유효성 검사로 자연히 갈린다.
	public void ResetOnCombatExit()
	{
		RecentDirectHitLosses.Clear();
		AttackTarget = null;
		BossFocusTarget = null;
		PathBlocker = null;
		NextPathBlockCheckTime = 0f;
		ResetRetargetTracking();
	}
}
