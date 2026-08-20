// 소집 대기 상태(2026-08-20, 사용자 요청 "소집 규칙이 더 우선이여야 해 지금 소집으로 전환을 못하네.
// 대기 규칙은 우선도가 낮은 기본으로 깔리는 상태고") — 몬스터 배치 프리셋(2026-08-19)의 "소집 상태
// 동안 제자리에서 대기"는 원래 NavigationFSMState(배열 맨 끝, 가장 낮은 우선순위)의 BT 분기 하나에
// 파묻혀 있었다. 그런데 그 위에 IdleFSMState(오펜스/디펜스 아닌 방에서의 평시 배회)가 새로 끼어들면서,
// "소집이 대기보다 우선"이라는 규칙이 IdleFSMState.GetPriority 안의 isMustered 조건 하나에만 의존하게
// 됐다 — 배열 순서 자체는 그 규칙을 전혀 보장해주지 않는 구조였다. 소집을 Tactical(50) 다음, Idle
// (20)보다 앞(30)의 전용 상태로 승격해서, "전투/전술 인지가 없는 한 소집이 항상 대기보다 우선"임을
// UnitFSM._states 배열 순서 자체가 보장하게 한다 — 진짜 전투/전술 인지가 오면 그보다 앞선 Combat/
// Tactical이 배열에서 먼저 걸려 자연히 소집을 가로챈다(=소집 해제, UnitFSM.SelectState의
// isGenuineCombatPerception 처리와 동일한 결과).
public class MusterFSMState : IFSMState
{
	public float GetPriority(Unit unit) => unit.isMustered ? (AIConfigLoader.Behavior?.musterPriority ?? 30f) : 0f;
	public bool  IsSticky(Unit unit)        => false;
	public bool  ShouldInterrupt(Unit unit) => true;
	public void  OnEnter(Unit unit)         { }
	public void  OnExit(Unit unit)          { }
	public BTStatus Tick(Unit unit)         => BTStatus.Running;
	// isMustered==true인 동안엔 UnitFSM.GetLabel이 이 상태와 무관하게 항상 "소집"으로 표시하므로
	// (이동 중이든 도착 후 대기든 동일) 여기 라벨은 그 폴백용일 뿐 실질적으로 화면에 노출되지 않는다.
	public string GetLabel(Unit unit)       => "소집";
}
