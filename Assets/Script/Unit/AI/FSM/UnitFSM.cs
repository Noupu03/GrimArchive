using UnityEngine;

public class UnitFSM
{
	private IFSMState   _current;
	private readonly PlayerCommandFSMState _playerCommandState = new PlayerCommandFSMState();

	// 우선순위 내림차순: PlayerCommand(200, 활성 시 최우선) → Combat(100) → Tactical(50) →
	// Navigation(10, 항상 활성). 배열에서 먼저 나오는 상태의 GetPriority가 0보다 크면 그 뒤는
	// 검사하지도 않으므로(SelectState 참고) 이 순서 자체가 곧 우선순위다.
	private readonly IFSMState[] _states;

	public UnitFSM()
	{
		_states = new IFSMState[]
		{
			_playerCommandState,
			new CombatFSMState(),
			new TacticalFSMState(),
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
			|| (unit.playerAttackTarget != null && unit.playerAttackTarget.hp > 0);
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

		// Sticky 유지: 현재 상태가 고착이고 아직 중단 조건이 안 됐으면 그대로.
		if (_current != null && _current.IsSticky(unit) && !_current.ShouldInterrupt(unit)) return;

		IFSMState next = null;
		foreach (var s in _states)
		{
			if (s.GetPriority(unit) > 0f) { next = s; break; }
		}

		// 몬스터 배치 프리셋(2026-08-19 버그 수정, 사용자 신고 "미배치 몬스터 소집 상태로 전환되는지
		// 체크해봐. 잘 작동 안되는거 같은데") — 처음엔 next가 TacticalFSMState이기만 하면 무조건
		// 해제했는데, TacticalFSMState는 전투와 무관한 상태(함정 상호작용/전투 종료 후 경계 스윕 등)도
		// 같이 처리하는 상태라 너무 넓었다. 특히 "전투 종료 후 경계 스윕"(IsPostCombatSweep)은 예전
		// 웨이브에서 이미 끝난 전투의 뒷정리일 뿐 "새로 전투를 인지"한 게 아닌데도, 소집을 걸자마자
		// 이 스윕이 아직 안 끝난 몬스터는 다음 틱에 바로 다시 해제돼 버려 소집이 사실상 안 걸리는
		// 것처럼 보였다. 실제 "전투 시작 시점 인지"만 정확히 골라서 해제한다:
		//   - 전투 상태(Combat) 진입
		//   - 적을 직접 목격(personalSpottedEnemies)
		//   - 경계/수색 중이되 전투 종료 후 뒷정리가 아닌 경우(=소리·수상한 타일 등으로 새로 인지)
		bool isGenuineCombatPerception =
			next is CombatFSMState
			|| unit.personalSpottedEnemies.Count > 0
			|| (unit.currentAlertSearch != null && !unit.currentAlertSearch.IsPostCombatSweep);

		if (unit.isMustered && isGenuineCombatPerception)
		{
			unit.isMustered = false;
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

	// 몬스터 배치 프리셋(2026-08-19, 사용자 요청 "소집한 후 다시 전투 상태가 되기까지 몬스터의 행동
	// 상태를 '소집'으로 바꿔줘") — 소집 중엔 실제로는 이동 중(PlayerCommandFSMState)이든 도착 후
	// 대기 중(NavigationFSMState)이든 현재 FSM 상태와 무관하게 항상 "소집"으로 표시한다. 전투/전술
	// 인지로 isMustered가 풀리는 즉시(SelectState 참고) 원래 상태 라벨로 자동 복귀한다.
	public string GetLabel(Unit unit)
	{
		if (unit.isMustered) return "소집";
		return _current?.GetLabel(unit) ?? "0";
	}
}
