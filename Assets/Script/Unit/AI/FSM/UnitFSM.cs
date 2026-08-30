using UnityEngine;

public class UnitFSM
{
	private IFSMState   _current;
	private readonly PlayerCommandFSMState _playerCommandState = new PlayerCommandFSMState();
	// "정지"/"제자리 공격" — PlayerCommand와 동일하게 SelectState가 직접 강제 배정하는 상태라
	// _states 배열에는 넣지 않는다(HaltFSMState.cs 참고).
	private readonly HaltFSMState _haltState = new HaltFSMState();
	private readonly StandGroundAttackFSMState _standGroundState = new StandGroundAttackFSMState();

	// 우선순위 내림차순: PlayerCommand(200) → Combat(100) → Tactical(50) → Idle(20, 오펜스/디펜스
	// 아닌 방에서 명령 없는 몬스터/야생의 배회) → Navigation(10, 항상 활성). 배열에서 먼저 나온
	// 상태의 GetPriority가 0보다 크면 그 뒤는 검사하지 않으므로(SelectState) 배열 순서 자체가 곧 우선순위다.
	private readonly IFSMState[] _states;

	public UnitFSM()
	{
		_states = new IFSMState[]
		{
			_playerCommandState,
			new CombatFSMState(),
			new TacticalFSMState(),
			new IdleFSMState(),
			new NavigationFSMState(),
		};
	}

	public IFSMState CurrentState => _current;

	// JudgeState 대응: 우선순위 순으로 전환 후보를 탐색하고 상태를 바꾼다.
	public void SelectState(Unit unit)
	{
		// 명령 강제 잠금 — IsSticky/ShouldInterrupt와 배열 순서만으로는 "다른 상태가 스스로 명령을
		// 존중해야" 성립하는 간접적 보장에 그치므로, 대기 중인 명령이 있으면 상태 구현과 무관하게 무조건
		// 이 상태로 고정한다(PlayerCommandFSMState가 도착/대상 무효화로 스스로 끝내기 전까진 못 벗어남).
		bool hasPendingCommand = (unit.playerMoveTarget.HasValue && unit.isManualMoveCommand)
			|| (unit.playerAttackTarget != null && unit.playerAttackTarget.hp > 0)
			// 코어/문 공격 명령(ExecutePlayerAttackObject)도 유닛 공격과 동일하게 최우선 강제 잠금 대상이다.
			|| unit.playerAttackObjectTarget.HasValue;
		if (hasPendingCommand)
		{
			if (_current != _playerCommandState)
			{
				_current?.OnExit(unit);
				_current = _playerCommandState;
				_current.OnEnter(unit);
			}
			return;
		}

		// "정지" 강제 잠금 — 새 직접 명령(위에서 처리됨) 또는 명령 취소(InputManager.
		// CancelSelectedUnitsCommands가 isHalted를 false로 되돌림)만이 풀 수 있다. 다른 상태는 검사조차 안 된다.
		if (unit.isHalted)
		{
			if (_current != _haltState)
			{
				_current?.OnExit(unit);
				_current = _haltState;
				_current.OnEnter(unit);
			}
			return;
		}

		// "제자리 공격" 강제 잠금 — "정지"와 동일한 패턴: 이동은 안 하지만 사거리 내 적은 공격한다. 새
		// 직접 명령이나 명령 취소만 풀 수 있다. 고정 유닛(보스 골렘 등)은 동작이 같아 같은 상태를
		// 재사용하지만 영구 성질이라 명령 취소로는 안 풀린다.
		if (unit.isStandGroundAttack || unit.isImmobile)
		{
			if (_current != _standGroundState)
			{
				_current?.OnExit(unit);
				_current = _standGroundState;
				_current.OnEnter(unit);
			}
			return;
		}

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

	public string GetLabel(Unit unit)
	{
		if (unit.isHalted) return "정지";
		if (unit.isImmobile) return "고정"; // 보스 골렘 등 영구 고정 유닛(플레이어 명령인 "제자리 공격"과 구분)
		if (unit.isStandGroundAttack) return "제자리 공격";
		return _current?.GetLabel(unit) ?? "0";
	}
}
