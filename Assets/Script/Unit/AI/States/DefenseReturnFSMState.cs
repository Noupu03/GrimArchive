// 자동 방어 복귀 상태 — 작업이 끝난 플레이어 몬스터가 저장된 방어 위치로 돌아가는 이동을 맡는다(배치모드 설계명세서 1-6 표1).
//
// 직접 [이동 적용]은 "지정 위치 이동 우선"이라 PlayerCommandFSMState가 강제 고착으로 맡지만, 자동 복귀는 "현재 방에서 인지한 전투에 참여 → 10초 경계 후 복귀 재개"여야 한다.
// 그래서 이 상태를 Combat(100)·Tactical(50) 아래, Idle(20) 위에 둔다 — 전투가 나면 Combat이, 전투가 끝난 뒤 10초 경계(CombatFSMState.OnExit가 세팅, Tactical이 유지)는 Tactical이 가져가고,
// 둘 다 끝나면 남아 있는 playerMoveTarget으로 이 상태가 복귀를 이어 간다(별도 타이머·재발행 없음).
// 이동 한 틱은 플레이어 이동 명령과 같은 로직(PlayerCommandFSMState.ExecutePlayerMove — 점유 대기·대체 칸·막힘 포기)을 그대로 쓴다.
public class DefenseReturnFSMState : IFSMState
{
	private readonly BTNode _bt = new BTSequence(
		new BTLeaf(PlayerCommandFSMState.ExecutePlayerMove),
		new BTLeaf(PlayerCommandFSMState.CompletePlayerCommand)
	);

	// 우선순위 값은 "0보다 크다"만 쓰인다(배열 순서가 곧 우선순위) — 문서화용 30.
	public float GetPriority(Unit unit)
	{
		if (unit is Human || !unit.IsPlayerMonsterFaction) return 0f;
		return unit.isDefenseAutoReturn && unit.isManualMoveCommand && unit.playerMoveTarget.HasValue ? 30f : 0f;
	}

	public bool IsSticky(Unit unit)        => false;
	public bool ShouldInterrupt(Unit unit) => true;
	public void OnEnter(Unit unit)         { unit.playerCommandStuckTurns = 0; }
	public void OnExit(Unit unit)          { }
	public BTStatus Tick(Unit unit)        => _bt.Tick(unit);
	public string GetLabel(Unit unit)
		=> unit.assignedWorkBuilding != null || unit.assignedDoorExpansion != null
		|| unit.assignedCoreRestorePos.HasValue || unit.IsHaulingCorpseOrPackage
		? "작업 이동" // 건설·문 확장 작업 이동도 같은 자동 이동 상태를 쓴다(건설 명세 1-5)
		: "방어 복귀";
}
