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
	public string   GetLabel(Unit unit)    => HasPlayerAttackTarget(unit) ? "명령(공격)" : "명령(이동)";

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

	private static bool HasPlayerMoveCommand(Unit unit)
		=> unit.playerMoveTarget.HasValue && unit.isManualMoveCommand;

	private static bool HasActivePlayerCommand(Unit unit)
		=> HasPlayerAttackTarget(unit) || HasPlayerMoveCommand(unit);

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
		int        chebDist = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y));

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
			Hitbox box = best.BuildSkillHitbox(unit);
			if (SkillAction.GetEnemiesInHitbox(unit, box).Contains(target))
			{
				best.Execute(unit, target, chebDist);
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
			Vector2Int fallback = AIMovementHelper.FindNearbyOpenTile(unit, target);
			if (fallback != target)
			{
				unit.playerMoveTarget      = fallback;
				unit.playerCommandStuckTurns = 0;
			}
			else if (AIMovementHelper.HasAnyStructurallyOpenNeighbor(unit, target))
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
					unit.playerMoveTarget        = null;
					unit.isManualMoveCommand     = false;
					unit.oneTimeReactUsed        = false;
					unit.playerCommandStuckTurns = 0;
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
				unit.playerMoveTarget        = null;
				unit.isManualMoveCommand     = false;
				unit.oneTimeReactUsed        = false;
				unit.playerCommandStuckTurns = 0;
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
		unit.playerMoveTarget        = null;
		unit.isManualMoveCommand     = false;
		unit.oneTimeReactUsed        = false;
		unit.playerCommandStuckTurns = 0;
		return BTStatus.Success;
	}
}
