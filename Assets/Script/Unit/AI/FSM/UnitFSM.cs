using UnityEngine;

public class UnitFSM
{
	private IFSMState   _current;
	// 우선순위 내림차순: Combat(100) → Tactical(50) → Navigation(10, 항상 활성)
	private readonly IFSMState[] _states = new IFSMState[]
	{
		new CombatFSMState(),
		new TacticalFSMState(),
		new NavigationFSMState(),
	};

	public IFSMState CurrentState => _current;

	// JudgeState 대응: 우선순위 순으로 전환 후보를 탐색하고 상태를 바꾼다.
	public void SelectState(Unit unit)
	{
		// Sticky 유지: 현재 상태가 고착이고 아직 중단 조건이 안 됐으면 그대로.
		if (_current != null && _current.IsSticky(unit) && !_current.ShouldInterrupt(unit)) return;

		IFSMState next = null;
		foreach (var s in _states)
		{
			if (s.GetPriority(unit) > 0f) { next = s; break; }
		}

		if (next == _current)
		{
			// 전투 상태가 아닐 때 oneTimeReact 리셋 (원본 GoapBrain.JudgeState 동작)
			if (!(_current is CombatFSMState)) unit.oneTimeReactUsed = false;
			return;
		}

		_current?.OnExit(unit); // CombatFSMState.OnExit가 AlertSearch 세팅을 담당
		_current = next;
		_current?.OnEnter(unit);
	}

	// ExecuteAction 대응: 현재 상태의 BT를 한 틱 실행한다.
	public void RunCurrentState(Unit unit)
	{
		if (unit.CombatState.State.isCastingAttack) return;

		if (_current != null)
			_current.Tick(unit);
		else
		{
			Dir randomDir = (Dir)Random.Range(0, 8);
			unit.Move(randomDir);
		}

		unit.CombatState.State.isHitThisTurn = false;
	}

	public string GetLabel(Unit unit) => _current?.GetLabel(unit) ?? "0";
}
