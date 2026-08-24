using System.Collections.Generic;
using UnityEngine;

// 플레이어 지정 명령 상태 — 우클릭 이동/공격 명령이 있으면 무조건 최우선(사용자 요청, 2026-07-24
// "플레이어 지정 명령이라는 상태를 따로 만들어서 최우선 순위로 둬"). UnitFSM._states 배열에서 맨
// 앞에 위치해, 이 상태의 GetPriority가 0보다 크면 Combat/Tactical/Navigation의 GetPriority는 아예
// 호출되지도 않는다 — 패닉/함정/경계(원래 Panic=150으로 원본 GOAP에서도 명령보다 우선했던 것)까지
// 포함해 그 어떤 판단도 이 상태를 가로챌 수 없다.
//
// 원래 NavigationFSMState 안의 BT 분기 2/3(플레이어 공격/이동 명령)이었던 걸 별도 상태로 분리했다
// — 그 전엔 Combat(100)이 배열상 Navigation(10)보다 먼저 검사돼서, 적을 인지 중이면 우클릭 명령이
// 로그만 찍히고 실제로는 전혀 먹히지 않는 버그가 있었고(CombatFSMState/TacticalFSMState에 임시로
// 넣었던 예외 처리로 1차 완화했었음), 그 예외 처리만으로는 명령 도중 조건이 바뀌면 여전히 매 틱
// 재평가가 일어나 전환 여지가 남아 있었다. 전용 상태 + IsSticky 고착으로 완전히 해결한다.
public class PlayerCommandFSMState : IFSMState
{
	private readonly BTNode _bt;

	public PlayerCommandFSMState()
	{
		_bt = new BTSelector(
			new BTSequence(
				new BTCondition(HasPlayerAttackTarget),
				new BTLeaf(ExecutePlayerAttack)
			),
			// 기초문서.md 피드백(2026-08-22, "코어와 문을 명령으로 인한 파괴 대상으로 지정할 수 있게
			// 해줘") — 유닛 공격과 동급, 이동보다 먼저 확인한다.
			new BTSequence(
				new BTCondition(HasPlayerAttackObjectTarget),
				new BTLeaf(ExecutePlayerAttackObject)
			),
			new BTSequence(
				new BTCondition(HasPlayerMoveCommand),
				new BTLeaf(ExecutePlayerMove),
				new BTLeaf(CompletePlayerCommand)
			)
		);
	}

	public float GetPriority(Unit unit)
	{
		// 공황 중엔 플레이어 명령도 받지 않는다 — 사용자 확인(2026-07-28, "Panic이 명령을 차단").
		// 공황은 정신력이 임계값 이하일 때 발동하며, 이 상태에선 TacticalFSMState.Panic BT가 대신 실행.
		if (IsPanicMode(unit)) return 0f;
		return HasActivePlayerCommand(unit) ? (AIConfigLoader.Behavior?.playerCommandPriority ?? 200f) : 0f;
	}

	// 명령 수행 중엔 고착 — UnitFSM.SelectState가 매 틱 다른 상태의 우선순위를 재검사하지 못하게
	// 막는다. 명령이 끝나면(공격 대상 무효화/이동 목표 도착) 다음 틱부터 자동으로 고착이 풀린다.
	// 공황이 발생해도 고착 해제 → TacticalFSMState.Panic으로 즉시 전환.
	public bool  IsSticky(Unit unit)       => HasActivePlayerCommand(unit) && !IsPanicMode(unit);
	public bool  ShouldInterrupt(Unit unit)=> !HasActivePlayerCommand(unit) || IsPanicMode(unit);
	public void  OnEnter(Unit unit)        { unit.playerCommandStuckTurns = 0; }
	public void  OnExit(Unit unit)         { }
	public BTStatus Tick(Unit unit)        => _bt.Tick(unit);
	public string   GetLabel(Unit unit)    => (HasPlayerAttackTarget(unit) || HasPlayerAttackObjectTarget(unit)) ? "명령(공격)" : "명령(이동)";

	// ── 헬퍼 ─────────────────────────────────────────────────────

	// 공황 판정 — 정신력이 panicMentalRatio 이하면 true. Human 전용(몬스터는 정신력 시스템 없음).
	private static bool IsPanicMode(Unit unit)
		=> unit is Human && unit.BaseStat.mental < unit.BaseStat.maxMental * (AIConfigLoader.Behavior?.panicMentalRatio ?? 0.3f);

	// 이동 명령 포기 시 UI 피드백
	private static void ShowMoveFailFeedback(Unit unit)
		=> unit.UI?.ShowFloatingTextAt(
			new UnityEngine.Vector3(unit.position.x + 0.5f, unit.position.y + 0.5f),
			"이동 불가",
			UnityEngine.Color.yellow,
			1.5f);

	// ── 조건 ─────────────────────────────────────────────────────
	// 2026-07-27 사용자 신고("플레이어 명령이 최우선순위 명령이어야 한다") 수정 — playerMoveTarget/
	// isManualMoveCommand/playerAttackTarget/oneTimeReactUsed는 전부 Unit 기반 클래스 필드인데
	// (InputManager도 Human/Monster 구분 없이 똑같이 세팅함) 이 조건들이 "unit is Human"으로 한정돼
	// 있어서 몬스터(플레이어 진영 포함)에게는 이 최우선순위 상태가 아예 발동하지 않았다 — 몬스터에게
	// 우클릭 이동/공격 명령을 내려도 전투/전술 AI가 매 틱 우선권을 계속 가져가 명령이 씹히는 것처럼
	// 보이는 버그였다. Human 전용 캐스팅을 없애고 Unit 그대로 사용한다.

	private static bool HasPlayerAttackTarget(Unit unit)
		=> unit.playerAttackTarget != null && unit.playerAttackTarget.hp > 0;

	// 기초문서.md 피드백(2026-08-22) — 코어/문 공격 명령이 걸려 있는지. 대상 자체가 여전히 유효한지
	// (파괴/소유권 전환됐는지)는 ExecutePlayerAttackObject가 매 틱 다시 확인해 스스로 정리한다.
	private static bool HasPlayerAttackObjectTarget(Unit unit)
		=> unit.playerAttackObjectTarget.HasValue;

	private static bool HasPlayerMoveCommand(Unit unit)
		=> unit.playerMoveTarget.HasValue && unit.isManualMoveCommand;

	private static bool HasActivePlayerCommand(Unit unit)
		=> HasPlayerAttackTarget(unit) || HasPlayerAttackObjectTarget(unit) || HasPlayerMoveCommand(unit);

	// 기초문서.md 피드백(2026-08-22) — pos에 있는 오브젝트가 unit이 명령으로 공격할 수 있는 대상인지
	// 판정한다(코어: 아직 파괴 전이고 이미 내 진영 소유가 아닐 것 / 문: 아직 파괴 전일 것). InputManager
	// 가 명령 발행 전 검증에도 그대로 재사용한다(public) — 자동 코어 공격(TacticalFSMState.
	// HasCoreAttackTarget/FindHostileRoomCore)과 별개 경로지만 코어 쪽 유효성 판정 취지는 동일하다.
	public static bool IsPendingObjectAttackValid(Unit unit, Vector3Int pos)
	{
		if (unit == null || unit.Session == null || !unit.Session.objectGrid.TryGetValue(pos, out var obj) || obj.Tags == null)
			return false;

		if (obj.Tags.Contains(GameSession.CoreTag))
		{
			if (obj.CoreHp <= 0f) return false;
			// 코어는 파괴돼도 사라지지 않고 반피로 회복된다 — 이미 내 진영 소유가 됐으면(=파괴 성공)
			// 더 공격할 이유가 없으므로 명령을 완료 처리한다.
			FactionType? myFaction = OffenseProcessor.MapToRoomFaction(unit.FactionBehavior);
			if (myFaction != null && unit.Session.roomGrid.TryGetValue(pos, out Room room) && room.RoomFaction == myFaction.Value)
				return false;
			return true;
		}
		if (obj.Tags.Contains(DoorSystem.DoorTag))
		{
			if (obj.DoorHp <= 0f) return false;
			// 자기 진영 문은 공격 대상이 아니다(사용자 요청, 2026-08-22 "자기 진영의 문은 우클릭시
			// 공격 대상이 되면 안돼... 문 너머의 상대 진영 문 선택시, 공격이 가능하게 해주면 됨") —
			// 어차피 항상 통과 가능하므로, 이 경우 호출부(InputManager.ExecuteRightClickCommand)가
			// 대신 일반 이동 명령으로 처리한다. 문 소유 진영은 방 소유권과 분리된 고정값이라
			// (InteractableObject.DoorOwnerFaction, 2026-08-22 재조정) 방 조회 없이 바로 읽는다.
			FactionType? myFaction = OffenseProcessor.MapToRoomFaction(unit.FactionBehavior);
			if (myFaction != null && obj.DoorOwnerFaction == myFaction.Value) return false;
			return true;
		}
		return false;
	}

	// ── 실행 ─────────────────────────────────────────────────────

	private static BTStatus ExecutePlayerAttack(Unit unit)
	{
		if (unit.playerAttackTarget == null) return BTStatus.Failure;
		Unit target = unit.playerAttackTarget;
		if (target.hp <= 0 || target.currentFloor != unit.currentFloor)
		{
			unit.playerAttackTarget = null;
			unit.oneTimeReactUsed   = false;
			return BTStatus.Success;
		}

		Vector2Int diff     = target.position - unit.position;
		int        chebDist = AIMovementHelper.ChebyshevDistance(target.position, unit.position);

		unit.currentDir = SkillAction.GetDirection8(diff);
		unit.CombatState.State.currentAttackAngle = ((UnitFunction)unit).CalculateAttackAngleToEnemy(target, 1);

		var  skills   = unit.Generate != null ? unit.Generate.GetSkills(unit.unitType.typeName) : new List<SkillAction>();
		SkillAction best = null;
		float bestP = float.MinValue;
		foreach (var s in skills)
		{
			if (s == null || !s.IsAvailable(unit)) continue;
			float p = s.GetPriority(unit, target, chebDist);
			if (p > bestP) { bestP = p; best = s; }
		}

		if (best != null)
		{
			// 스킬이 겨눌 대상 결정(아군 대상 스킬은 여기서 자기 대상을 찾는다) → 사거리 판정 → 실행.
			Unit resolved = best.ResolveTarget(unit, target);
			if (best.CanExecuteAgainst(unit, resolved, chebDist))
			{
				best.Execute(unit, resolved, chebDist);
				return BTStatus.Running;
			}
		}

		if (!AIMovementHelper.MoveTowardsTarget(unit, target))
		{
			// 적에게 다가가는 길이 완전히 막혔다(적 자신이 서 있는 칸이라 어차피 진입은 불가능하고,
			// 그 주변까지도 다른 유닛/벽으로 막힌 경우) — 적 바로 옆 갈 수 있는 빈 칸으로 접근 지점을
			// 다시 잡는다(사용자 요청, 2026-07-24 "길이 막혀서... 공격 명령을 수행하지 못하면 근처
			// 바로 옆의 빈칸으로 목표 재지정").
			Vector2Int fallback = AIMovementHelper.FindNearbyOpenTile(unit, target.position);
			if (fallback != target.position)
				AIMovementHelper.MoveTowardsPos(unit, fallback);
		}
		return BTStatus.Running;
	}

	// 기초문서.md 피드백(2026-08-22, "코어와 문을 명령으로 인한 파괴 대상으로 지정할 수 있게 해줘") —
	// ExecutePlayerAttack(유닛 대상)과 동일한 구조: 인접하면 currentAttackObjectTarget을 채워 채널링을
	// 맡기고(UnitFunction.OnUpdate가 실제 데미지 적용), 아니면 접근한다. 대상이 무효화되면(파괴/소유권
	// 전환/사라짐) 명령을 스스로 종료한다.
	private static BTStatus ExecutePlayerAttackObject(Unit unit)
	{
		if (!unit.playerAttackObjectTarget.HasValue) return BTStatus.Failure;
		Vector3Int targetPos = unit.playerAttackObjectTarget.Value;

		if (unit.currentFloor != targetPos.z || !IsPendingObjectAttackValid(unit, targetPos))
		{
			unit.playerAttackObjectTarget = null;
			unit.ClearAttackObjectTarget();
			unit.oneTimeReactUsed = false;
			// InputManager.ExecuteRightClickCommand가 이 명령을 걸 때 방 경계/문 타일 제한을 우회하려고
			// 켰던 예외(2026-08-22, 위 참고)를 명령 종료 시점에 반드시 꺼야 한다 — 안 그러면 대상이
			// 파괴/무효화된 뒤에도 RoomConfinedMovement 유닛이 방 밖을 자유롭게 드나들 수 있게 된다.
			unit.isManualMoveCommand = false;
			return BTStatus.Success;
		}

		Vector2Int pos2D = new Vector2Int(targetPos.x, targetPos.y);

		if (AIMovementHelper.IsAdjacent(unit.position, pos2D))
		{
			unit.currentDir = SkillAction.GetDirection8(pos2D - unit.position);
			unit.SetAttackObjectTarget(targetPos);
			return BTStatus.Running;
		}

		unit.ClearAttackObjectTarget(); // 인접하지 않게 됐으면(밀려남 등) 채널링 중단
		if (!AIMovementHelper.MoveTowardsPos(unit, pos2D))
		{
			Vector2Int fallback = AIMovementHelper.FindNearbyOpenTile(unit, pos2D);
			if (fallback != pos2D) AIMovementHelper.MoveTowardsPos(unit, fallback);
		}
		return BTStatus.Running;
	}

	private static BTStatus ExecutePlayerMove(Unit unit)
	{
		if (!unit.playerMoveTarget.HasValue) return BTStatus.Failure;
		Vector2Int target = unit.playerMoveTarget.Value;
		if (unit.position == target) return BTStatus.Success;

		if (!AIMovementHelper.MoveTowardsPos(unit, target))
		{
			// 목표 칸이 다른 유닛/벽으로 막혀 더 다가갈 수 없다 — 바로 옆 빈 칸으로 목표를 재지정한다
			// (사용자 요청, 2026-07-24 "길이 막혀서... 이동 명령을 수행하지 못하면 근처 바로 옆의
			// 빈칸으로 목표 재지정"). 여러 유닛을 같은 지점으로 한꺼번에 명령했을 때(전원이 동일한
			// gridPos를 목표로 받음, InputManager.cs) 한 명만 그 칸을 차지하고 나머지가 영원히
			// 도착하지 못하던 문제의 근본 원인이었다.
			//
			// 버그 수정(2026-08-23, 사용자 신고 "2*2 문 중 1개만 남기고 파괴했을 때 방 진입 직전에서
			// 이동 명령이 간헐적으로 멈춤") — FindNearbyOpenTile(unit, target)은 target 주변 로컬
			// 칸이 "지금 당장 비어있는지"만 보고 실제 도달 가능성(경로 연결)은 전혀 확인하지 않는다.
			// target이 방 안쪽처럼 멀리 있으면 그 주변 8칸 중 하나는 거의 항상 로컬로 비어있어서
			// fallback != target이 매 틱 성립해 버렸고, 그때마다 아래 stuckTurns가 0으로 리셋돼
			// "8틱 지나면 포기" 안전장치가 사실상 무력화됐다 — 좁은 1칸 병목(부서진 2*2 문 중 1개만
			// 남은 통로)에서 다른 유닛이 그 한 칸을 순간 점유할 때마다 이 오판단이 반복돼, 유닛이
			// 아무 피드백 없이 문 앞에서 계속 멈춰있는 것처럼 보였다. 이 재지정은 원래 "목표 칸 자체가
			// 다른 유닛에 점유된 경우"(여러 유닛이 같은 지점으로 명령받은 경우)를 위한 것이므로, 유닛이
			// 이미 목표에 인접해 있을 때만 적용한다 — 목표가 멀리 있으면(=병목이 경로 중간 어딘가에
			// 있다는 뜻) 이 재지정은 의미가 없으므로 건너뛰고 아래 혼잡/완전차단 판정으로 곧장 넘어간다.
			Vector2Int fallback = AIMovementHelper.IsAdjacent(unit.position, target, radius: 2)
				? AIMovementHelper.FindNearbyOpenTile(unit, target)
				: target;
			if (fallback != target)
			{
				unit.playerMoveTarget      = fallback;
				unit.playerCommandStuckTurns = 0;
			}
			// 혼잡 판정 기준도 target(멀리 있을 수 있음)이 아니라 유닛 자신의 현재 위치(실제 막힌
			// 지점)로 바꾼다 — target 기준이면 방 안쪽처럼 구조적으로 뚫린 곳이 항상 true를 반환해
			// "진짜 완전히 막힘" 판정이 나올 수 없었다. HasAnyStructurallyOpenAdjacentTile은 유닛
			// 자신의 칸(항상 트루가 되는 자기 자신)은 제외하고 인접 8칸만 확인한다.
			else if (AIMovementHelper.HasAnyStructurallyOpenAdjacentTile(unit))
			{
				// 명령 포기 오판 방지(2026-07-28, 사용자 신고 "자꾸 전투중에 한번씩 플레이어 명령
				// 무시해") — 지금 당장 갈 수 있는 빈 칸이 없는 건 벽/닫힌 문 때문이 아니라 전투 중
				// 다른 유닛들이 잠깐 몰려서(점유)일 수 있다. 그런 경우엔 명령을 포기하지 않고 다음
				// 틱에 다시 시도한다 — 혼잡이 풀리면 자연히 이어서 이동한다.
				// stuck 타임아웃(2026-07-28): 혼잡이 너무 오래 지속되면 명령을 포기하고 피드백을 준다.
				unit.playerCommandStuckTurns++;
				int limit = AIConfigLoader.Behavior?.playerCommandStuckTurnLimit ?? 8;
				if (unit.playerCommandStuckTurns >= limit)
				{
					ShowMoveFailFeedback(unit);
					unit.AbortMoveCommand();
					return BTStatus.Success;
				}
				return BTStatus.Running;
			}
			else
			{
				// 목표 주변이 지형(벽/닫힌 문)으로 진짜 완전히 막혀 있다 — 더 이상 수행 불가능하므로
				// 명령을 포기하고 다음 틱부터 정상 판단으로 돌아간다(무한 고착 방지).
				// 진단 로그(2026-07-28, 임시) — 이 give-up이 실제로 얼마나 자주/왜 발동하는지 추적.
				Haare.Util.Logger.LogHelper.Warning(Haare.Util.Logger.LogHelper.GAME,
					$"[FSM진단] {unit.unitType?.typeName}({unit.name}) 이동 명령 포기 — target={target} pos={unit.position} (구조적으로 완전히 막힘)");
				unit.AbortMoveCommand();
				return BTStatus.Success;
			}
		}
		else
		{
			unit.playerCommandStuckTurns = 0;
		}
		return BTStatus.Running;
	}

	private static BTStatus CompletePlayerCommand(Unit unit)
	{
		// "집결 및 정지" 명령(2026-08-20)과 "제자리 공격" 명령(2026-08-22)의 
		// 상태 전이 논리는 unit.FinishMoveCommand 내부로 캡슐화되었다.
		unit.FinishMoveCommand();
		return BTStatus.Success;
	}
}
