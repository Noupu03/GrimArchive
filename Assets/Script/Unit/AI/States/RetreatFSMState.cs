// 후퇴·도주 이동 상태(후퇴도주 문서 5장) — 개인 도주 확정자·개인 후퇴자·파티 후퇴 중 이동 단계의 인류가 들어온다. "정지"/"제자리 공격"과 같이 UnitFSM.SelectState가 RetreatSystem.WantsRetreatState로 직접 강제 배정하는 상태라 배열 우선순위(GetPriority)와 무관하다.
// 전투(Combat)·전술(Tactical)보다 우선한다 — 후퇴·도주 중 인류는 적과 싸우려고 멈추지 않고(길을 막는 적만 RetreatMovement가 상대), 소리 조사·새 수집·중간 휴식을 위해 정지하지 않는다. 거점에 도착해 쉬는 인원은 이 상태가 아니라 평소 상태(Tactical Rest, 전투가 나면 Combat이 우선)로 돌아간다.
public class RetreatFSMState : IFSMState
{
	private readonly BTNode _bt = new BTLeaf(unit => unit is Human human ? RetreatMovement.Step(human) : BTStatus.Failure);

	public float GetPriority(Unit unit)    => 0f; // SelectState가 직접 강제 배정 — 배열 비교 대상 아님.
	public bool  IsSticky(Unit unit)       => false; // 조건이 풀리면 다음 SelectState에서 평소 우선순위 판정으로 돌아간다
	public bool  ShouldInterrupt(Unit unit)=> true;
	public void  OnEnter(Unit unit)
	{
		// 전투에서 넘어온 경우 CombatFSMState.OnExit가 방금 세팅한 "전투 후 경계 스윕"을 버린다 — 후퇴 중엔 경계를 위해 멈추지 않고(5-1장) 경계 리프도 안 돌아 그대로 두면 거점에 도착한 뒤 뒤늦게 스윕을 시작한다.
		unit.currentAlertSearch = null;
	}

	public void OnExit(Unit unit)
	{
		// 문/함정 부수기 채널링과 차단 대응 상태를 남기지 않는다 — 안 비우면 다른 상태로 돌아가서도 계속 깎는다.
		if (unit is Human human)
		{
			human.ClearAttackObjectTarget();
			human.Retreat?.Move.ClearBlock();
		}
	}

	public BTStatus Tick(Unit unit) => _bt.Tick(unit);

	public string GetLabel(Unit unit)
		=> unit is Human human && human.Retreat != null && human.Retreat.IsFleeing ? "도주" : "후퇴";
}
