using UnityEngine;

public class UnitFSM
{
	private IFSMState   _current;
	private readonly PlayerCommandFSMState _playerCommandState = new PlayerCommandFSMState();
	// "집결 및 정지"/"제자리 공격"(2026-08-20/2026-08-22) — PlayerCommand와 동일한 방식으로
	// SelectState가 직접 강제 배정하는 상태라 _states 배열에는 넣지 않는다(HaltFSMState.cs 주석 참고).
	private readonly HaltFSMState _haltState = new HaltFSMState();
	private readonly StandGroundAttackFSMState _standGroundState = new StandGroundAttackFSMState();

	// 우선순위 내림차순: PlayerCommand(200, 활성 시 최우선) → Combat(100) → Tactical(50) → Idle(20,
	// 2026-08-20 신규 — 오펜스/디펜스 중이 아닌 방에서 명령 없는 플레이어 몬스터/야생의 "1칸 이동 후
	// 정지" 배회) → Navigation(10, 항상 활성). 배열에서 먼저 나오는 상태의 GetPriority가 0보다 크면
	// 그 뒤는 검사하지도 않으므로(SelectState 참고) 이 순서 자체가 곧 우선순위다.
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
		// 명령 강제 잠금(2026-07-28, 사용자 신고 "전투 중에 플레이어 명령 안 들어와... 도착할 때까지
		// 다른 상태로 전환하지 않도록 강하게 통제해줘") — 예전엔 PlayerCommandFSMState.IsSticky/
		// ShouldInterrupt와 배열 순서(GetPriority)의 조합에만 의존했는데, 그건 "다른 상태 구현들이
		// 스스로 명령을 존중해야" 성립하는 간접적인 보장이었다(진단 로그를 심어도 실제 이탈 지점을
		// 못 찾았던 이유이기도 함). 여기서는 그 어떤 상태의 IsSticky/ShouldInterrupt/GetPriority
		// 구현과도 무관하게, 대기 중인 명령이 있으면 아래 판단을 전부 건너뛰고 무조건 이 상태로
		// 고정한다 — 도착(또는 공격 대상 무효화)으로 PlayerCommandFSMState가 스스로 명령을 끝내기
		// 전까지는 다른 어떤 조건으로도 벗어날 수 없다.
		bool hasPendingCommand = (unit.playerMoveTarget.HasValue && unit.isManualMoveCommand)
			|| (unit.playerAttackTarget != null && unit.playerAttackTarget.hp > 0)
			// 기초문서.md 피드백(2026-08-22) — 코어/문 공격 명령(PlayerCommandFSMState.
			// ExecutePlayerAttackObject)도 유닛 공격과 동일하게 최우선 강제 잠금 대상이다.
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

		// "정지"(동상) 강제 잠금(2026-08-20, 사용자 요청 "정지 상태는 플레이어 직접 명령이나 명령
		// 해제를 제외하고, 절대 해제할 수 없는 상태임") — 위 hasPendingCommand 게이트 바로 다음에
		// 둬서, 새 직접 명령(위에서 이미 처리됨) 또는 명령 취소(InputManager.
		// CancelSelectedUnitsCommands가 isHalted를 직접 false로 되돌림)만이 이 잠금을 풀 수 있다.
		// Combat/Tactical/Idle/Navigation 그 무엇도 이 잠금 아래에서는 검사조차 되지 않는다.
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

		// "제자리 공격" 강제 잠금(기초문서.md 피드백, 2026-08-22, R키 배치모드를 대체) — "정지"(동상)와
		// 동일한 패턴: 이동은 절대 하지 않지만 사거리 내 적은 공격한다(StandGroundAttackFSMState.cs
		// 참고). 새 직접 명령이나 명령 취소만 이 잠금을 풀 수 있다.
		// 고정 유닛(2026-08-24, 보스 골렘) — "제자리 공격"과 동작이 완전히 같아 같은 상태를 재사용한다.
		// 다만 이건 명령이 아니라 유닛의 영구 성질이라 위 hasPendingCommand 게이트보다 아래에 둔다(플레이어
		// 명령 대상이 될 일은 없지만, 만약 생기더라도 명령이 이 고정을 이기지는 못하게 할 이유가 없다).
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
