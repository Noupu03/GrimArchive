using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 전술 상태 — 제자리·국소 반응 행동 담당 (공황/함정/경계/조사/대기/포메이션).
// FSM은 "적 있으면 Combat, 전술 조건 있으면 Tactical, 나머지는 Navigation"만 결정.
// 세부 우선순위는 BT가 결정 — TacticalBehaviorPriorityConfig의 순서·활성화 여부로 구성.
// 비주얼 스크립팅 전환 시: BuildBTNodes()의 각 항목이 BTNodeSO 인스턴스로 대응된다.
public class TacticalFSMState : IFSMState
{
	private readonly BTNode _bt;

	public TacticalFSMState()
	{
		var nodes = BuildBTNodes();
		var cfg   = AIConfigLoader.TacticalPriority;

		if (cfg != null && cfg.order.Count > 0)
		{
			var children = new List<BTNode>();
			foreach (var entry in cfg.order)
				if (entry.enabled && nodes.TryGetValue(entry.behavior, out var node))
					children.Add(node);
			_bt = new BTSelector(children.ToArray());
		}
		else
		{
			// 설정 에셋 없음 — 03문서 2-1장 기본 순서로 폴백
			_bt = new BTSelector(
				nodes[TacticalBehaviorType.Panic],
				nodes[TacticalBehaviorType.TrapResponse],
				nodes[TacticalBehaviorType.Alert],
				nodes[TacticalBehaviorType.Investigate],
				nodes[TacticalBehaviorType.Wait],
				nodes[TacticalBehaviorType.Formation]
			);
		}
	}

	// 각 TacticalBehaviorType을 BTNode로 빌드한다.
	// 비주얼 스크립팅 전환 시: 이 딕셔너리 대신 BTNodeSO 에셋에서 직접 트리를 읽는다.
	private static Dictionary<TacticalBehaviorType, BTNode> BuildBTNodes()
	{
		return new Dictionary<TacticalBehaviorType, BTNode>
		{
			[TacticalBehaviorType.Panic] = new BTSequence(
				new BTCondition(IsPanic),
				new BTLeaf(Panic)
			),
			[TacticalBehaviorType.TrapResponse] = new BTSelector(
				// Disarm → Bypass → Pass → Destroy 내부 순서는 고정 (문서 9장 우선순위)
				new BTSequence(
					new BTCondition(CanDisarm),
					new BTLeaf(TrapJoinWait),
					new BTLeaf(MoveToTrap),
					new BTLeaf(TrapDisarmPerform)
				),
				new BTLeaf(TrapBypass),
				new BTLeaf(TrapPass),
				new BTLeaf(TrapDestroy)
			),
			[TacticalBehaviorType.Alert] = new BTSequence(
				new BTCondition(HasAlert),
				new BTSelector(
					new BTLeaf(AlertApproach),
					new BTLeaf(AlertPerimeterSearch)
				)
			),
			[TacticalBehaviorType.Investigate] = new BTSequence(
				new BTCondition(CanInvestigate),
				new BTLeaf(MoveToInvestigateTarget),
				new BTLeaf(InvestigatePerform),
				new BTLeaf(PickUpObject)
			),
			[TacticalBehaviorType.Wait] = new BTSequence(
				new BTCondition(HasWait),
				new BTLeaf(ExecuteWait)
			),
			[TacticalBehaviorType.Formation] = new BTSequence(
				new BTCondition(HasFormationNeed),
				new BTSequence(
					new BTSelector(
						new BTLeaf(MoveToMeleeSlot),
						new BTLeaf(MoveToRangedSlot)
					),
					new BTLeaf(HoldFormation)
				)
			)
		};
	}

	public float GetPriority(Unit unit)
	{
		// 플레이어 수동 명령(공격/이동)은 PlayerCommandFSMState(UnitFSM._states 배열 맨 앞, 최우선)가
		// 전담한다 — 명령이 활성 상태면 이 GetPriority는 아예 호출되지도 않으므로(패닉/함정/경계
		// 포함해) 여기서 따로 예외 처리할 필요가 없다.
		float p = AIConfigLoader.Behavior?.tacticalPriority ?? 50f;
		if (IsPanic(unit)) return p;
		if (unit.currentTrapInteraction != null) return p;
		if (unit.currentAlertSearch != null) return p;
		if (unit is Human h && (h.currentInvestigation != null || h.HasReachableInvestigateTarget())) return p;
		if (unit is Human hw && hw.currentWait != null) return p;
		if (unit is Human hf && hf.HasProtectiveFormationNeed()) return p;
		return 0f;
	}

	// Sticky 없음 — BT가 Running 반환으로 서브태스크 연속성을 자연히 유지한다.
	// 피격·위협 등의 인터럽트는 각 BT 브랜치의 Condition이 Failure를 반환하여 처리.
	public bool IsSticky(Unit unit)        => false;
	public bool ShouldInterrupt(Unit unit) => true;
	public void OnEnter(Unit unit)         { }

	public void OnExit(Unit unit)
	{
		// 조사/함정 해제 중 외부 원인(플레이어 명령 등)으로 Tactical 상태 자체를 완전히 벗어날 때
		// 페널티 해제 + 진행도 50% 손실(5-6/9-7장). 같은 Tactical 상태 안에서 브랜치만 바뀌는 중단
		// (피격/위협 인지)은 여기를 안 타므로 CanInvestigate/CanDisarm이 각각 직접 처리한다(검증 발견
		// 2026-07-25 gap 수정 — 아래 ApplyInvestigateInterruptPenalty/ApplyTrapDisarmInterruptPenalty
		// 참고).
		if (unit is Human human && human.currentInvestigation != null && human.currentInvestigation.PenaltyActive)
			ApplyInvestigateInterruptPenalty(human);

		if (unit.currentTrapInteraction != null && unit.currentTrapInteraction.PenaltyActive)
			ApplyTrapDisarmInterruptPenalty(unit);
	}

	public BTStatus Tick(Unit unit)    => _bt.Tick(unit);
	public string   GetLabel(Unit unit) => GetSubLabel(unit);

	// ── FSM 진입 조건 헬퍼 ─────────────────────────────────────────

	private static bool IsPanic(Unit unit)
		=> unit is Human && unit.BaseStat.mental < unit.BaseStat.maxMental * (AIConfigLoader.Behavior?.panicMentalRatio ?? 0.3f);

	// 함정 해제: 피격 또는 위협 인지 시 이 틱에 한해 Failure → Selector가 다음 분기로 넘어감. 9-7장
	// 중단 조건이라 진행도 50% 손실도 여기서 같이 처리한다(검증 발견 2026-07-25 gap 수정 — 예전엔
	// OnExit에서만 처리해서, Tactical 상태를 벗어나지 않고 브랜치만 바뀌는 이 경로에서는 손실이
	// 전혀 안 걸렸다).
	private static bool CanDisarm(Unit unit)
	{
		if (!IsDisarmWorthy(unit)) return false;
		if (unit.isHitThisTurn || unit.HasPerceivedThreatCollider())
		{
			ApplyTrapDisarmInterruptPenalty(unit);
			return false;
		}
		return true;
	}

	// 9-7장: 함정 해제 진행도(DisarmProgress01)의 50% 손실 — TrapDisarmPerform이 매 틱 PenaltyActive를
	// true로 세팅해두므로, 그 값이 남아있을 때(=직전까지 실제로 해제 진행 중이었을 때)만 1회 적용하고
	// false로 내려서 같은 중단이 이어지는 동안 중복 적용되지 않게 한다.
	private static void ApplyTrapDisarmInterruptPenalty(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null || !trap.PenaltyActive) return;
		trap.DisarmProgress01 *= (AIConfigLoader.Behavior?.trapDisarmInterruptLossRatio ?? ExplorationMath.TrapDisarmInterruptLossRatio);
		trap.PenaltyActive = false;
	}

	private static bool CanInvestigate(Unit unit)
	{
		if (!(unit is Human human)) return false;
		if (human.playerAttackTarget != null || (human.playerMoveTarget.HasValue && human.isManualMoveCommand)) return false;
		if (human.currentInvestigation == null && !human.HasReachableInvestigateTarget()) return false;
		// 피격·위협 인지 시 이 틱에 한해 조사 중단(5-6장) — 진행도 50% 손실도 같이 처리(검증 발견
		// 2026-07-25 gap 수정, 위 CanDisarm과 동일한 이유/패턴).
		if (unit.isHitThisTurn || unit.HasPerceivedThreatCollider())
		{
			ApplyInvestigateInterruptPenalty(human);
			return false;
		}
		return true;
	}

	// 5-6장: 조사 진행도(Progress01)의 50% 손실 — InvestigatePerform이 매 틱 PenaltyActive를 true로
	// 세팅해두므로, 그 값이 남아있을 때만 1회 적용한다.
	private static void ApplyInvestigateInterruptPenalty(Human human)
	{
		var inv = human.currentInvestigation;
		if (inv == null || !inv.PenaltyActive) return;
		inv.Progress01 *= (AIConfigLoader.Behavior?.investigateInterruptLossRatio ?? ExplorationMath.InvestigateInterruptLossRatio);
		inv.PenaltyActive = false;
	}

	private static bool HasWait(Unit unit)
	{
		if (!(unit is Human human)) return false;
		if (human.currentWait == null) return false;
		if (human.playerAttackTarget != null || (human.playerMoveTarget.HasValue && human.isManualMoveCommand)) return false;
		if (unit.isHitThisTurn || unit.personalSpottedEnemies.Count > 0) return false;
		return true;
	}

	private static bool HasAlert(Unit unit)   => unit.currentAlertSearch != null;
	private static bool HasFormationNeed(Unit unit)
		=> unit is Human human && human.HasProtectiveFormationNeed();

	// ── 공황 ───────────────────────────────────────────────────────

	private static BTStatus Panic(Unit unit)
	{
		if (Random.value > 0.5f)
		{
			unit.Move((Dir)Random.Range(0, 8));
			LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 공황에 빠져 무작위로 이동합니다.");
		}
		else
		{
			LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 공황에 빠져 멈춰있습니다.");
		}
		return BTStatus.Running;
	}

	// ── 함정 공통 ─────────────────────────────────────────────────

	public static bool IsDisarmWorthy(Unit unit)
	{
		if (!(unit is Human human) || human.currentTrapInteraction == null) return false;
		var trap = human.currentTrapInteraction;
		if (human.Session == null || !human.Session.objectGrid.ContainsKey(trap.TrapPosition)) return false;
		if (!human.personalMap.IsTrapRecorded(trap.TrapObjectId)) return true;
		return human.personalMap.GetTrapExpectedSuccessRate(trap.TrapObjectId)
			> (AIConfigLoader.Behavior?.trapRecordedDisarmThreshold ?? ExplorationMath.TrapRecordedDirectDisarmThreshold) * 100f;
	}

	public static bool IsBlockingPath(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null) return false;
		if (trap.IsBlockingPath.HasValue) return trap.IsBlockingPath.Value;
		trap.IsBlockingPath = ComputeIsBlockingPath(unit, trap.TrapPosition);
		return trap.IsBlockingPath.Value;
	}

	private static bool ComputeIsBlockingPath(Unit unit, Vector3Int trapPos)
	{
		Vector2Int? dest = unit.playerMoveTarget.HasValue ? unit.playerMoveTarget
			: (unit is Human h && h.currentInvestigation != null
				? new Vector2Int(h.currentInvestigation.TargetPosition.x, h.currentInvestigation.TargetPosition.y)
				: (Vector2Int?)null);
		if (!dest.HasValue) return false;

		FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
		int fi = unit.currentFloor;
		if (myData.discoveredMap == null || fi >= myData.discoveredMap.Length || myData.discoveredMap[fi] == null)
			return false;

		int mapW = myData.discoveredMap[fi].GetLength(0);
		int mapH = myData.discoveredMap[fi].GetLength(1);
		Vector2Int trap2D = new Vector2Int(trapPos.x, trapPos.y);

		var visited = new HashSet<Vector2Int> { unit.position, trap2D };
		var queue   = new Queue<Vector2Int>();
		queue.Enqueue(unit.position);

		while (queue.Count > 0)
		{
			Vector2Int cur = queue.Dequeue();
			if (cur == dest.Value) return false;
			foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
			{
				Vector2Int next = cur + unit.GetDirVector(d);
				if (visited.Contains(next)) continue;
				if (next.x < 0 || next.x >= mapW || next.y < 0 || next.y >= mapH) continue;
				if (myData.discoveredMap[fi][next.x, next.y] == 2) continue;
				visited.Add(next); queue.Enqueue(next);
			}
		}
		return true;
	}

	// ── 함정 해제 3단계 ───────────────────────────────────────────

	private static BTStatus TrapJoinWait(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null) return BTStatus.Failure;
		return trap.JoinWaitElapsed ? BTStatus.Success : BTStatus.Running;
	}

	private static BTStatus MoveToTrap(Unit unit)
	{
		if (!IsDisarmWorthy(unit)) return BTStatus.Failure;
		var trap = unit.currentTrapInteraction;
		if (trap == null) return BTStatus.Failure;
		Vector2Int trapPos = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		// 함정도 오브젝트처럼 자신의 타일을 점유하므로 정확 일치 대신 Chebyshev ≤ 1(바로 옆 1칸)로
		// 도달 판정한다(사용자 요청, 2026-07-25 "함정 바로 위에서가 아니라 인근 1칸에서 해제 상호작용
		// 가능하게") — MoveToInvestigateTarget과 동일한 관례.
		if (Mathf.Max(Mathf.Abs(unit.position.x - trapPos.x), Mathf.Abs(unit.position.y - trapPos.y)) <= 1)
			return BTStatus.Success;
		AIMovementHelper.MoveTowardsPos(unit, trapPos);
		return BTStatus.Running;
	}

	private static BTStatus TrapDisarmPerform(Unit unit)
	{
		if (!IsDisarmWorthy(unit)) return BTStatus.Failure;
		var human = (Human)unit;
		var trap  = human.currentTrapInteraction;
		if (trap == null || !human.Session.objectGrid.TryGetValue(trap.TrapPosition, out var obj))
		{
			if (human.currentTrapInteraction != null) human.currentTrapInteraction = null;
			return BTStatus.Success;
		}
		trap.Phase = TrapPhase.Disarming;
		trap.PenaltyActive = true;
		if (trap.DisarmProgress01 < 1f) return BTStatus.Running;

		float rate    = ExplorationMath.TrapDisarmSuccessRate(human.concentration, human.level, understandingApplied: 0);
		human.personalMap.RecordTrapAttempt(trap.TrapObjectId, rate);

		if (Random.value * 100f < rate)
		{
			LogHelper.Log(LogHelper.GAME, $"{human.unitType.typeName}가 함정을 해제했습니다.");
			human.Session.CollectObject(trap.TrapPosition);
			trap.PenaltyActive           = false;
			human.currentTrapInteraction = null;
			return BTStatus.Success;
		}
		trap.DisarmProgress01 = 0f;
		return BTStatus.Running;
	}

	// ── 함정 대안 3종 ─────────────────────────────────────────────

	private static BTStatus TrapBypass(Unit unit)
	{
		if (!(unit is Human human) || human.currentTrapInteraction == null || IsBlockingPath(unit))
			return BTStatus.Failure;
		var    trap    = human.currentTrapInteraction;
		var    trapPos = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		var    away    = human.position - trapPos;
		if (away == Vector2Int.zero) away = new Vector2Int(1, 0);
		var sidePos = trapPos + new Vector2Int(
			System.Math.Sign(away.x) != 0 ? System.Math.Sign(away.x) : 1,
			System.Math.Sign(away.y));
		AIMovementHelper.MoveTowardsPos(human, sidePos);
		human.currentTrapInteraction = null;
		return BTStatus.Success;
	}

	private static BTStatus TrapPass(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null || !IsBlockingPath(unit)) return BTStatus.Failure;
		if (unit.Session == null || !unit.Session.objectGrid.TryGetValue(trap.TrapPosition, out var obj)) return BTStatus.Failure;

		float hpAfter    = unit.hp - obj.TrapDamageMax;
		var   bCfg       = AIConfigLoader.Behavior;
		bool  normalSafe = hpAfter >= unit.maxHp * (bCfg?.trapPassMinHpRatioAfterHit    ?? ExplorationMath.TrapPassMinHpRatioAfterHit);
		bool  rescueSafe = false;
		if (unit is Human human)
		{
			bool rec = human.personalMap.IsTrapRecorded(trap.TrapObjectId);
			rescueSafe = rec
				? hpAfter >= unit.maxHp * (bCfg?.trapRescueMinHpRatioRecorded   ?? ExplorationMath.TrapAllyRescueMinHpRatioAfterHit)
				: unit.hp  >= unit.maxHp * (bCfg?.trapRescueMinHpRatioUnrecorded ?? ExplorationMath.TrapAllyRescueUnrecordedMinCurrentHpRatio);
		}
		if (!normalSafe && !rescueSafe) return BTStatus.Failure;

		var trapPos2D = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		if (unit.position != trapPos2D) { AIMovementHelper.MoveTowardsPos(unit, trapPos2D); return BTStatus.Running; }

		unit.TakeDamage(obj.TrapDamageMax);
		LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 함정을 맞고 통과했습니다({obj.TrapDamageMax} 피해).");
		unit.currentTrapInteraction = null;
		return BTStatus.Success;
	}

	private static BTStatus TrapDestroy(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null) return BTStatus.Failure;
		if (unit.Session == null || !unit.Session.objectGrid.TryGetValue(trap.TrapPosition, out var obj))
		{
			if (unit.currentTrapInteraction != null) unit.currentTrapInteraction = null;
			return BTStatus.Success;
		}
		var trapPos2D = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		if (Vector2Int.Distance(unit.position, trapPos2D) > 1.5f)
		{
			AIMovementHelper.MoveTowardsPos(unit, trapPos2D);
			return BTStatus.Running;
		}
		trap.Phase = TrapPhase.Destroying;
		if (obj.TrapHp > 0f) return BTStatus.Running;
		LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 함정을 파괴했습니다.");
		unit.Session.CollectObject(trap.TrapPosition);
		unit.currentTrapInteraction = null;
		return BTStatus.Success;
	}

	// ── 조사 ──────────────────────────────────────────────────────

	private static BTStatus MoveToInvestigateTarget(Unit unit)
	{
		var human = (Human)unit;
		if (human.currentInvestigation == null)
		{
			var t = human.FindInvestigateTarget();
			if (t == null) return BTStatus.Failure;
			human.currentInvestigation = new InvestigationState { TargetObjectId = t.Id, TargetPosition = t.Position };
		}
		var inv = human.currentInvestigation;
		if (!human.Session.objectGrid.TryGetValue(inv.TargetPosition, out var obj) || obj.IsCollected)
		{
			human.currentInvestigation = null;
			return BTStatus.Failure;
		}
		var pos = new Vector2Int(inv.TargetPosition.x, inv.TargetPosition.y);
		// 오브젝트는 자신의 타일을 점유하므로 정확 일치 대신 Chebyshev ≤ 1로 도달 판정
		if (Mathf.Max(Mathf.Abs(human.position.x - pos.x), Mathf.Abs(human.position.y - pos.y)) <= 1) return BTStatus.Success;
		AIMovementHelper.MoveTowardsPos(human, pos);
		return BTStatus.Running;
	}

	private static BTStatus InvestigatePerform(Unit unit)
	{
		var human = (Human)unit;
		var inv   = human.currentInvestigation;
		if (inv == null) return BTStatus.Failure;
		if (!human.Session.objectGrid.TryGetValue(inv.TargetPosition, out var obj) || obj.IsCollected)
		{
			human.currentInvestigation = null;
			return BTStatus.Success;
		}
		// 이미 조사 완료 → PickUpObject로 넘어간다
		if (obj.IsInvestigated) return BTStatus.Success;

		inv.PenaltyActive = true;
		if (inv.Progress01 < 1f) return BTStatus.Running;

		obj.IsInvestigated = true;
		human.personalMap.OnObjectInvestigated(obj.Id);
		if (human.party != null)
		{
			foreach (var m in human.party.Members)
			{
				if (m == null || m == human || m.hp <= 0) continue;
				if (!m.personalMap.IsObjectKnown(obj.Id))
					m.personalMap.RegisterObject(obj.Id, obj.Position, obj.BaseDanger, obj.BaseInterest, obj.Tags, obj.CauserStage);
				m.personalMap.OnObjectInvestigated(obj.Id);
			}
		}
		inv.PenaltyActive = false;
		return BTStatus.Success;
	}

	// 조사(타이머 완료) 이후 실제 회수 처리 — 설계상 "조사"와 "줍기"는 별개 단계(17장).
	private static BTStatus PickUpObject(Unit unit)
	{
		var human = (Human)unit;
		var inv   = human.currentInvestigation;
		if (inv == null) return BTStatus.Failure;
		if (!human.Session.objectGrid.TryGetValue(inv.TargetPosition, out var obj) || obj.IsCollected)
		{
			human.currentInvestigation = null;
			return BTStatus.Success;
		}

		bool isLoot = obj.Tags.Exists(t => t.Contains("Loot"));
		if (isLoot)
		{
			human.Session.CollectObject(inv.TargetPosition);
			human.collectedObjects.Add(obj.Id);
			// 루팅 완료 → 탈출 시도. 06 문서(도주·후퇴) 미작성이므로 스텁:
			// pendingStairTargetFloor를 위 층으로 세팅해 기존 Stairs 브랜치가 계단 이동을 처리한다.
			if (!human.pendingStairTargetFloor.HasValue)
				human.pendingStairTargetFloor = human.currentFloor + 1;
		}
		human.currentInvestigation = null;
		return BTStatus.Success;
	}

	// ── 대기 ──────────────────────────────────────────────────────

	private static BTStatus ExecuteWait(Unit unit)
	{
		var human = (Human)unit;
		var wait  = human.currentWait;
		if (wait == null) return BTStatus.Success;
		if (wait.Reason == WaitReason.AwaitingPartyAtRallyPoint && wait.WaitPosition.HasValue)
		{
			if (Vector2Int.Distance(human.position, wait.WaitPosition.Value) <= 1.5f)
			{
				human.currentWait = null;
				return BTStatus.Success;
			}
			AIMovementHelper.MoveTowardsPos(human, wait.WaitPosition.Value);
		}
		return BTStatus.Running;
	}

	// ── 경계 ──────────────────────────────────────────────────────

	private static BTStatus AlertApproach(Unit unit)
	{
		var alert = unit.currentAlertSearch;
		if (alert == null || !alert.TargetPosition.HasValue) return BTStatus.Failure;
		Vector2Int target = alert.TargetPosition.Value;
		unit.currentDir = SkillAction.GetDirection8(target - unit.position);
		if (Vector2Int.Distance(unit.position, target) <= 1.5f)
		{
			unit.currentAlertSearch = null;
			return BTStatus.Success;
		}
		AIMovementHelper.MoveTowardsPos(unit, target);
		return BTStatus.Running;
	}

	private static BTStatus AlertPerimeterSearch(Unit unit)
	{
		if (unit.currentAlertSearch == null) return BTStatus.Failure;
		if (unit.MovementAlgorithm == null) return BTStatus.Running;
		for (int i = 0; i < 8; i++)
		{
			Dir tryDir = (Dir)(((int)unit.currentDir + i) % 8);
			Vector2Int pos = unit.position + unit.GetDirVector(tryDir);
			if (unit.MovementAlgorithm.TryGetNextStep(unit, pos, out Dir nextDir))
			{
				unit.currentDir = tryDir;
				unit.Move(nextDir);
				return BTStatus.Running;
			}
		}
		return BTStatus.Running;
	}

	// ── 포메이션 ─────────────────────────────────────────────────

	private static BTStatus MoveToMeleeSlot(Unit unit)
	{
		if (!(unit is Human human) || human.IsRangedFormationRole()) return BTStatus.Failure;
		AIMovementHelper.MoveToEscortSlot(human, backDistance: 1f);
		if (human.currentFormation?.EscortTarget != null)
		{
			Vector2Int slot = human.GetEscortSlotPosition(human.currentFormation.EscortTarget, 1f);
			return human.position == slot ? BTStatus.Success : BTStatus.Running;
		}
		return BTStatus.Running;
	}

	private static BTStatus MoveToRangedSlot(Unit unit)
	{
		if (!(unit is Human human) || !human.IsRangedFormationRole()) return BTStatus.Failure;
		float backDist = AIConfigLoader.Behavior?.formationRangedMinBackDistance ?? ExplorationMath.FormationRangedMinBackDistance;
		AIMovementHelper.MoveToEscortSlot(human, backDistance: backDist);
		if (human.currentFormation?.EscortTarget != null)
		{
			Vector2Int slot = human.GetEscortSlotPosition(human.currentFormation.EscortTarget, backDist);
			return human.position == slot ? BTStatus.Success : BTStatus.Running;
		}
		return BTStatus.Running;
	}

	private static BTStatus HoldFormation(Unit unit)
	{
		var human = (Human)unit;
		var esc   = human.currentFormation?.EscortTarget;
		if (esc == null) return BTStatus.Failure;
		human.currentDir = SkillAction.GetDirection8(esc.position - human.position);
		return BTStatus.Running;
	}

	// ── 라벨 ──────────────────────────────────────────────────────

	private static string GetSubLabel(Unit unit)
	{
		if (IsPanic(unit)) return "전술(공황)";
		if (unit.currentTrapInteraction != null) return "전술(함정)";
		if (unit is Human h)
		{
			if (h.currentInvestigation != null || h.HasReachableInvestigateTarget()) return "전술(조사)";
			if (h.currentWait != null) return "전술(대기)";
		}
		if (unit.currentAlertSearch != null) return "전술(경계)";
		if (unit is Human hf && hf.HasProtectiveFormationNeed()) return "전술(포메이션)";
		return "전술";
	}
}
