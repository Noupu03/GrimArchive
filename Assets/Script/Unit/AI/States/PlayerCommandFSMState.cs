using System.Collections.Generic;
using UnityEngine;

// 플레이어 지정 명령 상태 — 우클릭 이동/공격 명령이 있으면 무조건 최우선. UnitFSM._states 배열
// 맨 앞에 위치해, 이 상태의 GetPriority가 0보다 크면 Combat/Tactical/Navigation의 GetPriority는
// 아예 호출되지도 않는다 — 패닉/함정/경계를 포함해 그 어떤 판단도 이 상태를 가로챌 수 없다.
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
			// 코어/문 명령 공격 — 유닛 공격과 동급, 이동보다 먼저 확인한다.
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
		// 공황 중엔 플레이어 명령도 받지 않는다 — 정신력이 임계값 이하일 때 발동하며, 이 상태에선
		// TacticalFSMState.Panic BT가 대신 실행.
		if (IsPanicMode(unit)) return 0f;
		return HasActivePlayerCommand(unit) ? (AIConfigLoader.Behavior?.playerCommandPriority ?? 200f) : 0f;
	}

	// 명령 수행 중엔 고착(IsSticky) — 명령이 끝나면 다음 틱부터 자동으로 풀린다. 공황 발생 시엔
	// 고착 해제 → TacticalFSMState.Panic으로 즉시 전환.
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
	// ⚠️ 아래 필드들을 "unit is Human"으로 한정하지 말 것 — 플레이어 몬스터에게 우클릭 명령을 내려도
	// 전투/전술 AI가 우선권을 가져가 명령이 씹히는 버그가 생긴다. 항상 Unit 그대로 사용한다.

	private static bool HasPlayerAttackTarget(Unit unit)
		=> unit.playerAttackTarget != null && unit.playerAttackTarget.hp > 0;

	// 코어/문 공격 명령이 걸려 있는지. 대상 자체가 여전히 유효한지(파괴/소유권 전환됐는지)는
	// ExecutePlayerAttackObject가 매 틱 다시 확인해 스스로 정리한다.
	private static bool HasPlayerAttackObjectTarget(Unit unit)
		=> unit.playerAttackObjectTarget.HasValue;

	private static bool HasPlayerMoveCommand(Unit unit)
		=> unit.playerMoveTarget.HasValue && unit.isManualMoveCommand;

	private static bool HasActivePlayerCommand(Unit unit)
		=> HasPlayerAttackTarget(unit) || HasPlayerAttackObjectTarget(unit) || HasPlayerMoveCommand(unit);

	// pos에 있는 오브젝트가 unit이 명령으로 공격할 수 있는 대상인지 판정한다(코어: 파괴 전+내 진영
	// 소유 아님 / 문: 파괴 전). InputManager가 명령 발행 전 검증에도 그대로 재사용한다(public).
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
			// 자기 진영 문은 공격 대상이 아니다(항상 통과 가능) — 호출부가 대신 일반 이동으로 처리한다.
			// 문 소유 진영은 방 소유권과 분리된 고정값(InteractableObject.DoorOwnerFaction)이라 방
			// 조회 없이 바로 읽는다.
			FactionType? myFaction = OffenseProcessor.MapToRoomFaction(unit.FactionBehavior);
			if (myFaction != null && obj.DoorOwnerFaction == myFaction.Value) return false;
			return true;
		}
		// 함정 공격 명령 — 코어/문과 동일한 우클릭 오브젝트 공격 명령 체계에 편입. 함정은 진영 소유
		// 개념이 없어 코어/문과 달리 소유권 확인 없이 체력만 확인한다.
		if (obj.Tags.Exists(t => t.Contains("Trap")))
			return obj.TrapHp > 0f;
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
			// SetAttackCommand가 방 경계·문 타일 제한 우회를 위해 켠 플래그이므로 명령 종료 시 반드시
			// 끈다 — 안 그러면 이후 자율 행동 중에도 방 경계를 무단으로 넘나들 수 있다.
			unit.isManualMoveCommand = false;
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
			// 적에게 다가가는 길이 완전히 막혔다 — 적 바로 옆 갈 수 있는 빈 칸으로 접근 지점을 다시 잡는다.
			Vector2Int fallback = AIMovementHelper.FindNearbyOpenTile(unit, target.position);
			if (fallback != target.position)
				AIMovementHelper.MoveTowardsPos(unit, fallback);
		}
		return BTStatus.Running;
	}

	// ExecutePlayerAttack(유닛 대상)과 동일한 구조: 인접하면 currentAttackObjectTarget을 채워 채널링을
	// 맡기고(UnitFunction.OnUpdate가 실제 데미지 적용), 아니면 접근한다. 대상 무효화 시 스스로 종료한다.
	private static BTStatus ExecutePlayerAttackObject(Unit unit)
	{
		if (!unit.playerAttackObjectTarget.HasValue) return BTStatus.Failure;
		Vector3Int targetPos = unit.playerAttackObjectTarget.Value;

		if (unit.currentFloor != targetPos.z || !IsPendingObjectAttackValid(unit, targetPos))
		{
			unit.playerAttackObjectTarget = null;
			unit.ClearAttackObjectTarget();
			unit.oneTimeReactUsed = false;
			// 명령 발행 시 방 경계/문 타일 제한을 우회하려고 켠 예외를 명령 종료 시점에 반드시 끈다 —
			// 안 그러면 대상 무효화 후에도 RoomConfinedMovement 유닛이 방 밖을 자유롭게 드나들 수 있다.
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
			// (여러 유닛이 같은 지점으로 명령받아 한 명만 도착하는 문제의 대응). FindNearbyOpenTile은
			// 실제 도달 가능성을 확인하지 않아 target이 멀면 fallback이 매번 성립해 stuckTurns 리셋이
			// 무력화되므로, 유닛이 이미 목표에 인접해 있을 때만 적용하고 멀면 아래 혼잡 판정으로 넘어간다.
			Vector2Int fallback = AIMovementHelper.IsAdjacent(unit.position, target, radius: 2)
				? AIMovementHelper.FindNearbyOpenTile(unit, target)
				: target;
			if (fallback != target)
			{
				unit.playerMoveTarget      = fallback;
				unit.playerCommandStuckTurns = 0;
			}
			// 혼잡 판정 기준은 target이 아니라 유닛 자신의 현재 위치(실제 막힌 지점) — target 기준이면
			// 방 안쪽처럼 구조적으로 뚫린 곳이 항상 true가 돼 "진짜 완전히 막힘" 판정이 안 나온다.
			else if (AIMovementHelper.HasAnyStructurallyOpenAdjacentTile(unit))
			{
				// 벽이 아니라 전투 중 유닛들이 잠깐 몰려 점유된 경우일 수 있으므로, 명령을 포기하지 않고
				// 재시도하다가 stuckTurns 타임아웃 시 포기하고 피드백을 준다.
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
		// "집결 및 정지"/"제자리 공격" 명령의 상태 전이 논리는 unit.FinishMoveCommand 내부로 캡슐화되었다.
		unit.FinishMoveCommand();
		return BTStatus.Success;
	}
}
