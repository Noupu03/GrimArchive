// "정지"(동상) 상태(2026-08-20, 사용자 요청 "명령 메뉴에 '집결 및 정지' 모드를 넣어줘. 선택한 유닛들을
// 우클릭을 통해 장소를 지정하면 해당 위치로 이동하고, 이동 후에는 '정지' 상태가 됨. 정지 상태는 플레이어
// 직접 명령이나 명령 해제를 제외하고, 절대 해제할 수 없는 상태임") — PlayerCommandFSMState와 동일하게
// UnitFSM.SelectState가 _states 배열의 우선순위 비교와 무관하게 unit.isHalted==true인 동안 이 상태를
// 직접 강제 배정한다(_states 배열에는 넣지 않는다 — GetPriority는 그래서 의미가 없다).
//
// 완전 무반응(사용자 확인, 2026-08-20 — "동상" 선택): 이동/전투/전술/회피 일체 하지 않는다. Tick이
// 정말 아무것도 안 해서 적이 인접해도 반격하지 않는다 — 위협 반응(회피/블링크)은 UnitFunction.
// OnReactToThreat이 별도로 isHalted를 확인해 막는다(FSM Tick 밖에서 호출되는 경로라 여기만으로는
// 못 막음). 2026-08-22부터 같은 지점에서 isStandGroundAttack(제자리 공격)도 함께 확인한다 —
// "제자리 공격"도 회피/점멸로 인한 위치 이동을 예외 없이 차단한다(사용자 신고로 확정).
public class HaltFSMState : IFSMState
{
	public float GetPriority(Unit unit)    => 0f; // SelectState가 직접 강제 배정 — 배열 비교 대상 아님.
	public bool  IsSticky(Unit unit)       => true;
	public bool  ShouldInterrupt(Unit unit)=> false;
	public void  OnEnter(Unit unit)        { }
	public void  OnExit(Unit unit)         { }
	public BTStatus Tick(Unit unit)        => BTStatus.Running;
	public string   GetLabel(Unit unit)    => "정지";
}
