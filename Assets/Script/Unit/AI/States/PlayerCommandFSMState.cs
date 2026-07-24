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
		=> HasActivePlayerCommand(unit) ? (AIConfigLoader.Behavior?.playerCommandPriority ?? 200f) : 0f;

	// 명령 수행 중엔 고착 — UnitFSM.SelectState가 매 틱 다른 상태의 우선순위를 재검사하지 못하게
	// 막는다. 명령이 끝나면(공격 대상 무효화/이동 목표 도착) 다음 틱부터 자동으로 고착이 풀린다.
	public bool  IsSticky(Unit unit)       => HasActivePlayerCommand(unit);
	public bool  ShouldInterrupt(Unit unit)=> !HasActivePlayerCommand(unit);
	public void  OnEnter(Unit unit)        { }
	public void  OnExit(Unit unit)         { }
	public BTStatus Tick(Unit unit)        => _bt.Tick(unit);
	public string   GetLabel(Unit unit)    => HasPlayerAttackTarget(unit) ? "명령(공격)" : "명령(이동)";

	// ── 조건 ─────────────────────────────────────────────────────

	private static bool HasPlayerAttackTarget(Unit unit)
		=> unit is Human h && h.playerAttackTarget != null && h.playerAttackTarget.hp > 0;

	private static bool HasPlayerMoveCommand(Unit unit)
		=> unit is Human h && h.playerMoveTarget.HasValue && h.isManualMoveCommand;

	private static bool HasActivePlayerCommand(Unit unit)
		=> HasPlayerAttackTarget(unit) || HasPlayerMoveCommand(unit);

	// ── 실행 ─────────────────────────────────────────────────────

	private static BTStatus ExecutePlayerAttack(Unit unit)
	{
		if (!(unit is Human human) || human.playerAttackTarget == null) return BTStatus.Failure;
		Unit target = human.playerAttackTarget;
		if (target.hp <= 0 || target.currentFloor != human.currentFloor)
		{
			human.playerAttackTarget = null;
			human.oneTimeReactUsed   = false;
			return BTStatus.Success;
		}

		Vector2Int diff     = target.position - human.position;
		int        chebDist = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y));

		human.currentDir = SkillAction.GetDirection8(diff);
		human.CombatState.State.currentAttackAngle = ((UnitFunction)human).CalculateAttackAngleToEnemy(target, 1);

		var  skills   = human.Generate != null ? human.Generate.GetSkills(human.unitType.typeName) : new List<SkillAction>();
		SkillAction best = null;
		float bestP = float.MinValue;
		foreach (var s in skills)
		{
			if (s == null || !s.IsAvailable(human)) continue;
			float p = s.GetPriority(human, target, chebDist);
			if (p > bestP) { bestP = p; best = s; }
		}

		if (best != null)
		{
			Hitbox box = best.BuildSkillHitbox(human);
			if (SkillAction.GetEnemiesInHitbox(human, box).Contains(target))
			{
				best.Execute(human, target, chebDist);
				return BTStatus.Running;
			}
		}

		if (!AIMovementHelper.MoveTowardsTarget(human, target))
		{
			// 적에게 다가가는 길이 완전히 막혔다(적 자신이 서 있는 칸이라 어차피 진입은 불가능하고,
			// 그 주변까지도 다른 유닛/벽으로 막힌 경우) — 적 바로 옆 갈 수 있는 빈 칸으로 접근 지점을
			// 다시 잡는다(사용자 요청, 2026-07-24 "길이 막혀서... 공격 명령을 수행하지 못하면 근처
			// 바로 옆의 빈칸으로 목표 재지정").
			Vector2Int fallback = AIMovementHelper.FindNearbyOpenTile(human, target.position);
			if (fallback != target.position)
				AIMovementHelper.MoveTowardsPos(human, fallback);
		}
		return BTStatus.Running;
	}

	private static BTStatus ExecutePlayerMove(Unit unit)
	{
		if (!(unit is Human human) || !human.playerMoveTarget.HasValue) return BTStatus.Failure;
		Vector2Int target = human.playerMoveTarget.Value;
		if (human.position == target) return BTStatus.Success;

		if (!AIMovementHelper.MoveTowardsPos(human, target))
		{
			// 목표 칸이 다른 유닛/벽으로 막혀 더 다가갈 수 없다 — 바로 옆 빈 칸으로 목표를 재지정한다
			// (사용자 요청, 2026-07-24 "길이 막혀서... 이동 명령을 수행하지 못하면 근처 바로 옆의
			// 빈칸으로 목표 재지정"). 여러 유닛을 같은 지점으로 한꺼번에 명령했을 때(전원이 동일한
			// gridPos를 목표로 받음, InputManager.cs) 한 명만 그 칸을 차지하고 나머지가 영원히
			// 도착하지 못하던 문제의 근본 원인이었다.
			Vector2Int fallback = AIMovementHelper.FindNearbyOpenTile(human, target);
			if (fallback != target)
			{
				human.playerMoveTarget = fallback;
			}
			else
			{
				// 목표 주변에도 갈 수 있는 칸이 하나도 없다 — 더 이상 수행 불가능하므로 명령을
				// 포기하고 다음 틱부터 정상 판단으로 돌아간다(무한 고착 방지).
				human.playerMoveTarget    = null;
				human.isManualMoveCommand = false;
				human.oneTimeReactUsed    = false;
				return BTStatus.Success;
			}
		}
		return BTStatus.Running;
	}

	private static BTStatus CompletePlayerCommand(Unit unit)
	{
		if (!(unit is Human human)) return BTStatus.Failure;
		human.playerMoveTarget      = null;
		human.isManualMoveCommand   = false;
		human.oneTimeReactUsed      = false;
		return BTStatus.Success;
	}
}
