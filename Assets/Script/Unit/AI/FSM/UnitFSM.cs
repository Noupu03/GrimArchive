using UnityEngine;
using Haare.Util.Logger;

public class UnitFSM
{
	private IFSMState   _current;
	// 우선순위 내림차순: PlayerCommand(200, 활성 시 최우선) → Combat(100) → Tactical(50) →
	// Navigation(10, 항상 활성). 배열에서 먼저 나오는 상태의 GetPriority가 0보다 크면 그 뒤는
	// 검사하지도 않으므로(SelectState 참고) 이 순서 자체가 곧 우선순위다.
	private readonly IFSMState[] _states = new IFSMState[]
	{
		new PlayerCommandFSMState(),
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

		// 진단 로그(2026-07-28, 사용자 신고 "여전히 명령 잘 안먹혀") — 명령이 대기 중인데도
		// PlayerCommandFSMState가 아닌 다른 상태로 전환되면 남긴다. 원인 확인되면 지워도 되는 임시 로그.
		bool hasPendingCommand = (unit.playerMoveTarget.HasValue && unit.isManualMoveCommand)
			|| (unit.playerAttackTarget != null && unit.playerAttackTarget.hp > 0);
		if (hasPendingCommand && !(next is PlayerCommandFSMState))
		{
			LogHelper.Warning(LogHelper.GAME,
				$"[FSM진단] {unit.unitType?.typeName}({unit.name}) 명령 대기 중인데 {(_current?.GetType().Name ?? "null")} → {(next?.GetType().Name ?? "null")}로 전환됨. " +
				$"playerMoveTarget={unit.playerMoveTarget} isManualMoveCommand={unit.isManualMoveCommand} " +
				$"playerAttackTarget={(unit.playerAttackTarget != null ? unit.playerAttackTarget.name : "null")}");
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
