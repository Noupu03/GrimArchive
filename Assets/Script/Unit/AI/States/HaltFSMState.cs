// "정지"(동상) 상태 — 이동 명령 완료 후 도착 시 전환되며 플레이어 직접 명령/명령 해제 외에는 절대
// 해제되지 않는다. UnitFSM.SelectState가 배열 우선순위와 무관하게 강제 배정한다(GetPriority는 무의미).
// 완전 무반응이라 적이 인접해도 반격하지 않으며, FSM Tick 밖의 위협 반응(회피/블링크)도 차단된다.
public class HaltFSMState : IFSMState
{
	public float GetPriority(Unit unit)    => 0f; // SelectState가 직접 강제 배정 — 배열 비교 대상 아님.
	// unit.isHalted를 그대로 반환한다 — 하드코딩된 true였을 때는, 명령 취소로 isHalted가 false가 된
	// 뒤에도 SelectState의 sticky 체크(80줄)가 여전히 통과해 _current가 배열 재판정까지 못 가고 영원히
	// HaltFSMState에 멈춰있었다(새 명령이 와서 위쪽 강제 잠금 분기를 타지 않는 한 절대 안 풀림). 2026-09-04 수정.
	public bool  IsSticky(Unit unit)       => unit.isHalted;
	public bool  ShouldInterrupt(Unit unit)=> false;
	public void  OnEnter(Unit unit)        { }
	public void  OnExit(Unit unit)         { }
	public BTStatus Tick(Unit unit)        => BTStatus.Running;
	public string   GetLabel(Unit unit)    => "정지";
}
