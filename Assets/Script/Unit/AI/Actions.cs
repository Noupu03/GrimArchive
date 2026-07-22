using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Haare.Util.Logger;

public class Action_Panic : GoapAction
{
	public Action_Panic() { ActionName = "Panic"; AddEffect("panicResolved", true); }

	public override bool IsValid(Unit unit) => unit is Human && unit.mental < unit.maxMental * 0.3f;

	public override void Execute(Unit unit)
	{
		if (Random.value > 0.5f)
		{
			Dir randomDir = (Dir)Random.Range(0, 8);
			unit.Move(randomDir);
			LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 공황에 빠져 무작위로 이동합니다.");
		}
		else
		{
			LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 공황에 빠져 멈춰있습니다.");
		}
	}
}

public class Action_PlayerCommandExecute : GoapAction
{
	public Action_PlayerCommandExecute() { ActionName = "PlayerCommandExecute"; AddEffect("playerCommandExecuted", true); }

	public override bool IsValid(Unit unit) => unit.playerMoveTarget.HasValue;

	public override void Execute(Unit unit)
	{
		if (!unit.playerMoveTarget.HasValue) return;

		Vector2Int target = unit.playerMoveTarget.Value;
		if (unit.position == target)
		{
			unit.playerMoveTarget = null;
			unit.isManualMoveCommand = false;

			if (unit.playerInteractTarget.HasValue)
			{
				if (unit is Human human && unit.Session != null)
				{
					Vector3Int interactPos = unit.playerInteractTarget.Value;
					if (unit.Session.objectGrid.TryGetValue(interactPos, out InteractableObject obj) && !obj.IsCollected)
					{
						// 17장/19장: 조사→회수(루팅) 또는 확인(시체·흔적) 2단계를 도달 즉시 순서대로
						// 자동 처리한다 — 플레이어 조작은 지금처럼 한 번의 상호작용 그대로 두고,
						// 내부적으로만 "조사 완료(50%감소)" 이벤트를 먼저 찍은 뒤 "회수/확인 완료
						// (0%)"로 이어간다. Corpse/WipeoutTrace는 문서상 조사 단계 없이 확인 즉시
						// 0으로 가므로 조사 단계를 건너뛴다.
						// Tags는 "Object/Passable/Corpse"류 계층형 문자열이라 정확 일치(Contains(string))가 아니라
						// 부분 일치로 검사해야 한다(2026-07-20 발견 — 기존 정확 일치는 항상 false를 반환하는
						// 잠재 버그였다. HumanWaveManager의 "Loot" 판정은 이미 부분 일치를 쓰고 있었음).
						bool isTrace = obj.Tags.Any(t => t.Contains("Corpse")) || obj.Tags.Any(t => t.Contains("WipeoutTrace"));
						if (!isTrace && !obj.IsInvestigated)
						{
							human.personalMap.OnObjectInvestigated(obj.Id);
							obj.IsInvestigated = true;
						}

						unit.Session.CollectObject(interactPos);
						human.personalMap.OnObjectCollected(obj.Id);
						human.collectedObjects.Add(obj.Id);
					}
				}
				unit.playerInteractTarget = null;
			}
		}
		else
		{
			MoveTowardsPos(unit, target);
		}
	}
}

public class Action_RandomExplore : GoapAction
{
	public Action_RandomExplore() { ActionName = "RandomExplore"; AddEffect("explored", true); }

	public override bool IsValid(Unit unit) => true;

	public override void Execute(Unit unit)
	{
		Dir randomDir = (Dir)Random.Range(0, 8);
		Vector2Int targetPos = unit.position + unit.GetDirVector(randomDir);

		if (unit.MovementAlgorithm != null)
		{
			if (unit.MovementAlgorithm.TryGetNextStep(unit, targetPos, out Dir nextDir))
			{
				unit.Move(nextDir);
			}
		}
		else
		{
			unit.Move(randomDir);
		}
	}
}

public class Action_EngageEnemy : GoapAction
{
	public Action_EngageEnemy()
	{
		ActionName = "EngageEnemy";
		AddPrecondition("enemyVisible", true);
		AddEffect("enemyAlive", false);
	}

	public override bool IsValid(Unit unit) => true;

	public override void Execute(Unit unit) => ExecuteSkillActionBased(unit);

	private void ExecuteSkillActionBased(Unit unit)
	{
		if (unit.CombatState.isCastingAttack) return; // 캐스팅 중에는 새로운 행동 판단을 중지

		Unit target = GetClosestEnemy(unit, out float minDist);
		if (target == null) return;

		var unitSkills = unit.Generate != null ? unit.Generate.GetSkills(unit.unitType.typeName) : new System.Collections.Generic.List<SkillAction>();
		int engageSteps = unit.Generate != null ? unit.Generate.GetEngageDistance(unit.unitType.typeName, 2) : 2;

		// [사전 수치 계산] 유닛이 보유한 최대 스킬 사거리를 계산합니다.
		int maxSkillRange = 0;
		foreach (var s in unitSkills)
		{
			if (s != null && s.HitRange > maxSkillRange) maxSkillRange = s.HitRange;
		}
		
		Vector2Int diff     = target.position - unit.position;
		int        chebDist = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y));

		unit.currentDir         = SkillAction.GetDirection8(diff);
		unit.CombatState.currentAttackAngle = ((UnitFunction)unit).CalculateAttackAngleToEnemy(target, 1);

		// 1. 사용 가능한 최우선 스킬을 '가장 먼저' 탐색합니다. (스킬 사거리에 따라 이동 로직이 달라짐)
		SkillAction bestSkill    = null;
		float       bestPriority = float.MinValue;
		foreach (SkillAction skill in unitSkills)
		{
			if (skill == null || !skill.IsAvailable(unit)) continue;
			float priority = skill.GetPriority(unit, target, minDist);
			if (priority > bestPriority) { bestPriority = priority; bestSkill = skill; }
		}

		if (bestSkill != null)
		{
			// 1. 도주(Kiting) 조건 먼저 체크 (조준 여부와 상관없이 거리가 가까우면 우선 도주)
			if (bestSkill.HitRange >= 4)
			{
				int dangerDist = bestSkill.HitRange / 2;
				if (chebDist <= dangerDist && unit.CombatState.evadeCooldown <= 0f)
				{
					MoveAwayFromTarget(unit, target, dangerDist + 1);
					// 이동 방향에 맞게 시각화 업데이트 진행
					unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
					unit.Generate?.UpdateUnitSpriteForDirection(unit);
					return; // 도망가는 중에는 스킬 사용 보류
				}
			}

			// 2. 선택된 스킬의 실제 사거리로 canHit 검사
			Hitbox skillBox = bestSkill.BuildSkillHitbox(unit);
			bool   canHit   = SkillAction.GetEnemiesInHitbox(unit, skillBox).Contains(target);

			if (canHit)
			{
				// 안전하거나 근접 스킬이라면 즉시 실행!
				bestSkill.Execute(unit, target, minDist);
				return;
			}

			// 3. 사거리 밖이거나 쏘는 각도가 안맞으면 다가갑니다 (접근하여 각도 맞추기)
			if (unit.CombatState.evadeCooldown <= 0f)
			{
				MoveTowardsTarget(unit, target);
			}
		}
		else
		{
			// 모든 스킬이 쿨타임일 때 (공격 불가능한 상태)
			// 원거리 유닛은 안전거리(위험거리+1) 밖으로만 도망갑니다.
			int fallbackRange = maxSkillRange >= 4 ? maxSkillRange / 2 + 1 : 1;
			if (chebDist != fallbackRange && unit.CombatState.evadeCooldown <= 0f)
			{
				if (chebDist < fallbackRange)
				{
					MoveAwayFromTarget(unit, target, fallbackRange);
				}
				else
				{
					MoveTowardsTarget(unit, target);
				}
			}
		}

		// 이동 후 방향 업데이트 (계속 적을 바라보게 함, 8방향)
		unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
		unit.Generate?.UpdateUnitSpriteForDirection(unit);
	}
}
