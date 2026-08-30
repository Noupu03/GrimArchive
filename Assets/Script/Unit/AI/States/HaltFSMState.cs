// "정지"(동상) 상태 — 이동 명령 완료 후 도착 시 전환되며, 플레이어 직접 명령이나 명령 해제 외에는
// 절대 해제되지 않는다. UnitFSM.SelectState가 배열 우선순위 비교와 무관하게 unit.isHalted==true인
// 동안 이 상태를 직접 강제 배정한다(GetPriority는 의미 없음). 완전 무반응이라 적이 인접해도
// 반격하지 않는다 — 위협 반응(회피/블링크)은 FSM Tick 밖에서 호출되므로 UnitFunction.
// OnReactToThreat이 별도로 isHalted(및 isStandGroundAttack)를 확인해 막는다.
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
