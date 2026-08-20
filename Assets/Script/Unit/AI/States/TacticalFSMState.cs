using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;
using GrimArchive.Wave;

// 전술 상태 — 제자리·국소 반응 행동 담당 (공황/함정/경계/조사/대기/포메이션).
// FSM은 "적 있으면 Combat, 전술 조건 있으면 Tactical, 나머지는 Navigation"만 결정.
// 세부 우선순위는 BT가 결정 — TacticalBehaviorPriorityConfig의 순서·활성화 여부로 구성.
// 비주얼 스크립팅 전환 시: BuildBTNodes()의 각 항목이 BTNodeSO 인스턴스로 대응된다.
public class TacticalFSMState : IFSMState
{
	private readonly BTNode _bt;

	// GetPriority 조건과 BT 내부 Condition이 엇갈려 전체 Selector가 Failure를 반환하는 극단적 상황
	// 방지용 폴백 — 어떤 브랜치도 매치되지 않아도 Running을 반환해 Combat/Navigation으로 튀는 것을 막는다.
	private static readonly BTNode _fallbackRunning = new BTLeaf(_ => BTStatus.Running);
	// F: ComputeIsBlockingPath BFS 내 Enum.GetValues(typeof(Dir)) 매 노드 호출 → static 캐시
	private static readonly Dir[] _allDirs = (Dir[])System.Enum.GetValues(typeof(Dir));
	// G: AssignFormationWatchDirection 매 틱 new List<Human>() × 2 → static 버퍼 재사용
	private static readonly List<Human> _frontBuffer = new List<Human>();
	private static readonly List<Human> _backBuffer  = new List<Human>();

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
			children.Add(_fallbackRunning);
			_bt = new BTSelector(children.ToArray());
		}
		else
		{
			// 설정 에셋 없음 — 03문서 2-1장 기본 순서로 폴백
			_bt = new BTSelector(
				nodes[TacticalBehaviorType.Panic],
				nodes[TacticalBehaviorType.JoinCombatWait],
				nodes[TacticalBehaviorType.TrapResponse],
				nodes[TacticalBehaviorType.Alert],
				nodes[TacticalBehaviorType.Investigate],
				nodes[TacticalBehaviorType.Wait],
				nodes[TacticalBehaviorType.Formation],
				nodes[TacticalBehaviorType.Core],
				_fallbackRunning
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
			[TacticalBehaviorType.JoinCombatWait] = new BTSequence(
				new BTCondition(HasJoinCombatWait),
				new BTLeaf(JoinCombatWaitPerform)
			),
			[TacticalBehaviorType.TrapResponse] = new BTSequence(
				// 07문서 16-4장: 함정 대응 대기·해제는 함정 작동음을 제외한 소리(전투 관련/이동음)에
				// 중단된다. 함정작동음은 이 조건에서 애초에 걸리지 않아(PropagationSystem 참고) 표대로
				// "유지"된다.
				new BTCondition(unit => !IsTrapResponseBlockedBySound(unit)),
				new BTSelector(
					// 9-7장(신규): 선정 유닛을 찾아 나서는 중이면 최우선으로 그 이동을 계속한다.
					new BTSequence(new BTCondition(IsSearchingForMissingUnit), new BTLeaf(TrapSearchForMissingUnit)),
					// 9-2~9-6장(신규): 발견 유닛이지만 해제 담당으로 선정되지 않았으면 현 위치에서 대기.
					new BTSequence(new BTCondition(IsAwaitingSelectedUnit), new BTLeaf(TrapAwaitSelectedUnit)),
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
				)
			),
			[TacticalBehaviorType.Alert] = new BTSequence(
				new BTCondition(HasAlert),
				new BTSelector(
					new BTLeaf(DeathSearchMove),   // 4-15장: 원인미상 파티원 사망 수색(배정 방향으로 퍼져 수색)
					new BTLeaf(SoundMoveReact),    // 07문서 8-1장: 이동음 반응(시야전환+2초 유지, 이동 없음)
					new BTLeaf(SoundAreaApproach), // 07문서 8-2장: 추정 지역형 소리 반응(접근+2초 유지)
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
			),
			[TacticalBehaviorType.Core] = new BTSequence(
				new BTCondition(CanContinueCore),
				new BTLeaf(MoveToCore),
				new BTLeaf(CoreInvestigatePerform)
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
		if (unit is Human joinWaitHu && joinWaitHu.currentJoinCombatWait != null) return p;
		if (unit.currentTrapInteraction != null) return p;
		if (unit.currentAlertSearch != null) return p;
		Human hu = unit as Human;
		if (hu != null && (hu.currentInvestigation != null || hu.HasReachableInvestigateTarget())) return p;
		if (hu != null && hu.currentWait != null) return p;
		if (hu != null && hu.HasProtectiveFormationNeed()) return p;
		if (hu != null && hu.party != null && hu.party.Leader == hu && hu.party.PendingCoreObjectId != null)
		{
			// 2026-07-27 버그 수정("코어가 없는 이상한 곳에서 코어 로직이 실행됨"): 리더가 코어와 다른
			// 층에 있으면(예: 웨이브 시작 직후 아직 0층 대기 구역) 여기서 층 이동을 계단 이동
			// 파이프라인(NavigationFSMState.HasPendingStairs/MoveToStairs/CrossStairs — 이미 검증된
			// 기존 메커니즘)에 맡기고, 코어 쪽은 우선순위를 양보한다. 그래야 MoveToCore의 도착 판정
			// (X/Y만 비교, 층 비교 없음)이 다른 층에서 좌표만 우연히 근접했을 때 오작동하는 걸 막는다.
			if (hu.currentFloor != hu.party.PendingCorePosition.z)
			{
				// [의도적 부수효과 — GetPriority 내 유일 예외]
				// 리더가 코어와 다른 층에 있을 때 계단 이동 파이프라인에 목적지 층 정보를 주입한다.
				// NavigationFSMState.HasPendingStairs가 이 값을 읽어 즉시 계단 이동을 시작하게 하는 것이
				// 목적이며, 이 조건 이외에서 GetPriority가 상태를 변경하는 부분은 없다.
				if (!hu.pendingStairTargetFloor.HasValue) hu.pendingStairTargetFloor = hu.party.PendingCorePosition.z;
				return 0f;
			}
			return p;
		}
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
		// 8-2장: 보호 유닛이 피격당하면 중단(함정 해제는 7장의 "파티 목표 오브젝트"가 될 수 없으므로
		// — 대상은 회수/조사 오브젝트뿐 — 예외 없이 항상 중단. 2026-07-25 사용자 요청으로 연결,
		// AnyEscortHitThisTurn()이 그동안 아무도 호출하지 않는 죽은 코드였다).
		if (unit is Human humanDisarmer && humanDisarmer.AnyEscortHitThisTurn())
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
		if (trap.CachedProgressBar != null)
		{
			trap.CachedProgressBar.SetProgress(0f, false);
		}
		else
		{
			unit.Session?.GetObjectVisual(trap.TrapPosition)?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);
		}
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
		// 8-2장: 보호 유닛이 피격당했을 때 — 지금 조사 중인 대상이 파티의 웨이브 목표 오브젝트(7장)면
		// 상호작용을 유지하고(공격받은/대응 가능한 보호 유닛만 각자 알아서 전투·경계로 전환 — 그건 그
		// 유닛 자신의 FSM이 담당하므로 여기서 따로 처리할 게 없다), 그 밖의 일반 조사면 중단한다.
		// 2026-07-25 사용자 요청으로 연결(AnyEscortHitThisTurn()이 그동안 아무도 호출하지 않는 죽은
		// 코드였다) — InteractableObject에 "파티 목표" 플래그가 따로 없어(7장 재검증 참고)
		// HumanWaveManager.dummyTarget(지금 이 웨이브의 유일한 목표 오브젝트)과 ID를 비교해 근사한다.
		if (human.AnyEscortHitThisTurn() && !IsPartyObjectiveInvestigation(human.currentInvestigation))
		{
			ApplyInvestigateInterruptPenalty(human);
			return false;
		}
		return true;
	}

	private static bool IsPartyObjectiveInvestigation(InvestigationState inv)
	{
		var wave = HumanWaveManager.Instance;
		return wave != null && wave.dummyTarget != null && inv != null && wave.dummyTarget.Id == inv.TargetObjectId;
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

	// 07문서 16장(2026-07-31): currentAlertSearch가 비어있어도 아직 시작 안 한 유효한 소리 반응이
	// 있으면 이 지점에서 지연 승격한다(함정 대응 등 상위 분기가 먼저 실패해야 여기 도달하므로,
	// 함정작동음처럼 "현재 행동을 유지"시키는 소리는 자연히 그 행동이 끝난 뒤에야 승격된다).
	// 2026-08-06: 07문서 1장 검증 중 소리 감지 승격이 Human으로만 게이팅돼 몬스터는 PendingSound가
	// 채워져도 currentAlertSearch로 승격되지 못하던 갭을 발견해 인류/몬스터 구분 없이 호출하도록 고쳤다
	// (TryPromotePendingSoundToAlert 자체가 이미 Unit 기준으로 일반화됨).
	private static bool HasAlert(Unit unit)
		=> unit.currentAlertSearch != null || PropagationSystem.TryPromotePendingSoundToAlert(unit);
	private static bool HasFormationNeed(Unit unit)
		=> unit is Human human && human.HasProtectiveFormationNeed();
	private static bool HasJoinCombatWait(Unit unit) => unit is Human human && human.currentJoinCombatWait != null;

	// 07문서 16-4장: 함정 대응 대기·해제는 함정작동음을 제외한 소리에 중단된다. 진행도가 있었으면
	// (5-6/9-7장과 동일한 패턴) 중단 시 50% 손실을 함께 적용한다.
	private static bool IsTrapResponseBlockedBySound(Unit unit)
	{
		if (!(unit is Human human)) return false;
		if (!PropagationSystem.HasPendingInterruptingSound(human)) return false;
		ApplyTrapDisarmInterruptPenalty(unit);
		return true;
	}

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

	private static bool IsDisarmWorthy(Unit unit)
	{
		if (!(unit is Human human) || human.currentTrapInteraction == null) return false;
		var trap = human.currentTrapInteraction;
		if (human.Session == null || !human.Session.objectGrid.ContainsKey(trap.TrapPosition)) return false;
		if (!human.personalMap.IsTrapRecorded(trap.TrapObjectId)) return true;
		return human.personalMap.GetTrapExpectedSuccessRate(trap.TrapObjectId)
			> (AIConfigLoader.Behavior?.trapRecordedDisarmThreshold ?? ExplorationMath.TrapRecordedDirectDisarmThreshold) * 100f;
	}

	private static bool IsBlockingPath(Unit unit)
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
			foreach (Dir d in _allDirs)
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

	// ── 함정 발견/선정 조율(9-2~9-7장 신규) ─────────────────────────

	private static bool IsAwaitingSelectedUnit(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		return trap != null && trap.SelectedUnitName != null && !trap.IsSelectedDisarmer && !trap.SearchingForSelectedUnit;
	}

	private static bool IsSearchingForMissingUnit(Unit unit) => unit.currentTrapInteraction?.SearchingForSelectedUnit == true;

	// 9-6장: 선정 유닛이 도착할 때까지 발견 유닛은 현재 위치에서 대기한다. 함정 오브젝트 자체가
	// 사라지거나(해제/파괴 성공) 선정 유닛이 이미 이 함정과의 상호작용을 끝냈으면(우회/통과 — 오브젝트는
	// 남아있을 수 있음) 대기를 정리한다.
	private static BTStatus TrapAwaitSelectedUnit(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null) return BTStatus.Failure;

		bool trapGone = unit.Session == null || !unit.Session.objectGrid.ContainsKey(trap.TrapPosition);
		bool selectedUnitDone = false;
		if (!trapGone && unit is Human h && h.party != null)
		{
			var selected = h.party.Members.Find(m => m != null && m.name == trap.SelectedUnitName);
			if (selected != null && selected.hp > 0 && selected.currentTrapInteraction == null)
				selectedUnitDone = true; // 선정 유닛이 우회/통과로 이 함정을 이미 벗어남
		}

		if (trapGone || selectedUnitDone)
		{
			unit.currentTrapInteraction = null;
			return BTStatus.Success;
		}
		return BTStatus.Running;
	}

	// 9-7장: 선정 유닛의 예상 도착시간+3초를 넘겨도 도착하지 않으면 발견 유닛이 마지막 전파 위치로
	// 직접 찾아간다(그 자리에 선정 유닛의 시체가 있으면 CastRay가 자연히 PartyDeathSystem.
	// OnCorpseDiscovered를 트리거해 4-12~4-15장 사망 처리로 이어진다).
	private static BTStatus TrapSearchForMissingUnit(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null || !trap.SearchingForSelectedUnit) return BTStatus.Failure;
		var target = trap.SelectedUnitLastKnownPos ?? new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		if (Vector2Int.Distance(unit.position, target) <= 1.5f)
		{
			if (unit is Human h) TrapPartySystem.RestartSelection(h, trap);
			return BTStatus.Success;
		}
		AIMovementHelper.MoveTowardsPos(unit, target);
		return BTStatus.Running;
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
		if (AIMovementHelper.IsAdjacent(unit.position, trapPos))
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
		// 07문서 10장: 해제가 실제로 시작되는 시점에 "진행 중" 정보를 1회 전파(보호 포메이션 참여 자격).
		if (!trap.PenaltyActive) PropagationSystem.NotifyInteractionStarted(human);
		trap.PenaltyActive = true;
		if (trap.DisarmProgress01 < 1f) return BTStatus.Running;

		float rate    = ExplorationMath.TrapDisarmSuccessRate(human.concentration, human.level, understandingApplied: 0);
		human.personalMap.RecordTrapAttempt(trap.TrapObjectId, rate);

		// 9-7/9-8장(2026-07-27 추가): 성공/실패 결과 문구 — 오브젝트가 사라지기 전에 위치를 먼저
		// 잡아둔다(성공 시 CollectObject가 비주얼을 파괴하므로 그 뒤엔 위치를 못 구함). 문구는 이
		// 트랩 오브젝트의 자식이 아니라 독립 GameObject라 오브젝트 파괴와 무관하게 1초간 유지된다.
		var trapVisual = human.Session.GetObjectVisual(trap.TrapPosition);
		Vector3 resultTextPos = trapVisual != null
			? trapVisual.transform.position + Vector3.down * 0.45f
			: new Vector3(trap.TrapPosition.x + 0.5f, trap.TrapPosition.y + 0.5f, 0f);

		if (Random.value * 100f < rate)
		{
			LogHelper.Log(LogHelper.GAME, $"{human.unitType.typeName}가 함정을 해제했습니다.");
			human.UI?.ShowFloatingTextAt(resultTextPos, "성공", Color.green, 1f);
			human.Session.CollectObject(trap.TrapPosition);
			trap.PenaltyActive           = false;
			// 9-5장(신규): 해제 성공 시에만 도감에 기록한다(우회·파괴·통과는 기록 안 함). 함정 종류별
			// 도감 ID 체계가 아직 없어 오브젝트 고유 Id를 그대로 넘긴다 — 등록된 항목이 없으면
			// EncyclopediaManager가 경고만 남기고 조용히 무시하므로 안전하다.
			Game.Encyclopedia.EncyclopediaManager.Instance?.UnlockEntry(trap.TrapObjectId);
			human.currentTrapInteraction = null;
			human.currentAlertSearch = null; // 03문서 4-5장(2026-08-06): 낡은 경계 상태 잔재 정리
			return BTStatus.Success;
		}
		human.UI?.ShowFloatingTextAt(resultTextPos, "실패", Color.red, 1f);
		if (trap.CachedProgressBar != null)
		{
			trap.CachedProgressBar.SetProgress(0f, false);
		}
		else
		{
			trapVisual?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);
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
		human.currentAlertSearch = null; // 03문서 4-5장(2026-08-06): 낡은 경계 상태 잔재 정리
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

		// 4-14장: 이 피해가 사망으로 이어지면 PartyDeathSystem이 "함정이 원인"임을 알 수 있어야 한다 —
		// lastAttacker(Unit)로는 표현이 안 되니 lastTrapAttacker에 남기고, 더 오래된 몬스터 공격 기록과
		// 섞이지 않도록 lastAttacker는 비운다.
		unit.lastAttacker = null;
		unit.lastTrapAttacker = obj;
		// 07문서 14장: 함정 작동 시 작동 위치에서 함정 작동음 발생.
		PropagationSystem.EmitSound(unit.Session, SoundType.TrapActivation, trapPos2D, unit.currentFloor, null);
		unit.TakeDamage(obj.TrapDamageMax);
		LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 함정을 맞고 통과했습니다({obj.TrapDamageMax} 피해).");
		unit.currentTrapInteraction = null;
		unit.currentAlertSearch = null; // 03문서 4-5장(2026-08-06): 낡은 경계 상태 잔재 정리
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
		unit.currentAlertSearch = null; // 03문서 4-5장(2026-08-06): 낡은 경계 상태 잔재 정리
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
		if (AIMovementHelper.IsAdjacent(human.position, pos)) return BTStatus.Success;
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

		// 07문서 10장: 조사가 실제로 시작되는 시점에 "진행 중" 정보를 1회 전파(보호 포메이션 참여 자격).
		if (!inv.PenaltyActive) PropagationSystem.NotifyInteractionStarted(human);
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

		// 5-2장(2026-07-27 신규): 파티원 시체 조사로 사망 원인(간접) 확인.
		if (obj.Tags.Contains("Human") && obj.Tags.Exists(t => t.Contains("Corpse")))
			PartyDeathSystem.OnCorpseInvestigated(human, obj);

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
			human.currentAlertSearch = null; // 03문서 4-5장(2026-08-06): 낡은 경계 상태 잔재 정리
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
		human.currentAlertSearch = null; // 03문서 4-5장(2026-08-06): 낡은 경계 상태 잔재 정리
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
				if (human.party != null) human.party.CheckRallyComplete();
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

	// 4-15장: 원인미상 파티원 사망 수색 — 시체 위치(DeathSearchOrigin)를 기준으로 배정된 방향
	// (전방/후방/좌/우)을 바라보며 그 방향으로 퍼져나간다(AlertApproach처럼 한 점으로 수렴하지 않음).
	private static BTStatus DeathSearchMove(Unit unit)
	{
		var alert = unit.currentAlertSearch;
		if (alert == null || !alert.IsDeathSearch) return BTStatus.Failure;
		Vector2Int origin = alert.DeathSearchOrigin ?? unit.position;
		Vector2Int dirVec = unit.GetDirVector(alert.AssignedSearchDir);
		Vector2Int target = origin + dirVec * 3;
		unit.currentDir = alert.AssignedSearchDir;
		if (Vector2Int.Distance(unit.position, target) <= 1f)
			return BTStatus.Running; // 배정 위치 도착 — 방향을 유지한 채 대기(시간 종료는 OnUpdate가 처리)
		AIMovementHelper.MoveTowardsPos(unit, target);
		return BTStatus.Running;
	}

	// 07문서 8-1장: 이동음 반응 — '?' 표시 → 방향으로 시야 전환 → 인지 판정 1회(평소 UpdateFOV 패스가
	// 자연히 처리하므로 별도 재판정 호출 없음) → 2초간 그 방향 시야만 유지하고 종료. 이동은 하지 않는다.
	private static BTStatus SoundMoveReact(Unit unit)
	{
		var alert = unit.currentAlertSearch;
		if (alert == null || !alert.IsSoundResponse || alert.SoundKind != SoundType.Movement || !alert.TargetPosition.HasValue)
			return BTStatus.Failure;

		unit.currentDir = SkillAction.GetDirection8(alert.TargetPosition.Value - unit.position);
		if (!alert.SoundPerceptionRolled)
		{
			alert.SoundPerceptionRolled = true;
			alert.ElapsedSeconds = 0f; // 여기서부터 2초 유지 타이머 시작(OnUpdate가 매 프레임 증가시킴)
		}
		if (alert.ElapsedSeconds >= PropagationMath.MoveSoundHoldSeconds)
		{
			ClearSoundAlert(unit);
			return BTStatus.Success;
		}
		return BTStatus.Running;
	}

	// 07문서 8-2장: 추정 지역형 소리 반응(공격 실행음/피격 발생 공격음/피격 비명/사망음/함정 작동음) —
	// '?' 표시 → 추정 지역 중심이 인지 범위 안에 들어오는 거리까지 접근 → 인지 판정 1회(평소 UpdateFOV
	// 패스가 자연히 처리) → 원인 미확인이면 2초 유지 후 종료. 도중에 원인을 정확 인지하면(적/시체 등)
	// personalSpottedEnemies 등 다른 경로가 다음 틱에 자연히 우선권을 가져간다.
	private static BTStatus SoundAreaApproach(Unit unit)
	{
		var alert = unit.currentAlertSearch;
		if (alert == null || !alert.IsSoundResponse || !alert.SoundHasEstimatedArea || !alert.TargetPosition.HasValue)
			return BTStatus.Failure;

		Vector2Int center = alert.TargetPosition.Value;
		unit.currentDir = SkillAction.GetDirection8(center - unit.position);

		float effectiveSpotting = unit.VisionStat.spotting + (unit.Perception.IsAlert ? PerceptionMath.AlertDetectionBonus : 0f);
		float perceptionDistance = VisionMath.AwarenessDistance(effectiveSpotting);
		if (Vector2Int.Distance(unit.position, center) > perceptionDistance)
		{
			AIMovementHelper.MoveTowardsPos(unit, center);
			return BTStatus.Running;
		}

		if (!alert.SoundPerceptionRolled)
		{
			alert.SoundPerceptionRolled = true;
			alert.ElapsedSeconds = 0f;
			// E_HIT_HEAVY_INDIRECT 연결(2026-08-05): 인지 판정 1회가 이뤄지는 바로 이 시점에 "원인을
			// 정확 인지했는지" 확인한다 — 피격 발생 공격음/피격 비명이 아니거나 조건 미충족이면 조용히
			// 무시된다(PropagationSystem.TryConfirmIndirectHit 내부 게이팅).
			if (unit is Human indirectObserver)
				PropagationSystem.TryConfirmIndirectHit(indirectObserver, alert);
		}
		if (alert.ElapsedSeconds >= PropagationMath.EstimatedAreaHoldSeconds)
		{
			ClearSoundAlert(unit);
			return BTStatus.Success;
		}
		return BTStatus.Running;
	}

	private static void ClearSoundAlert(Unit unit)
	{
		unit.currentAlertSearch = null;
		// 2026-08-06: PendingSound도 Propagation(base Unit 소유)이라 Human 게이팅 없이 정리한다 —
		// 예전 Human 전용 게이팅을 그대로 두면 몬스터의 PendingSound가 영영 안 지워져 새 소리를
		// 못 받는 상태로 고착됐다.
		unit.Propagation.PendingSound = null;
	}

	// 07-A 9장: 발견자는 제자리에서 가장 가까운 정확 인지 적을 향해 시야를 유지하고(09_전투반응 문서
	// 부재로 "방어 계산 활성화"는 스텁), 합류자는 발견자 위치로 이동한다.
	private static BTStatus JoinCombatWaitPerform(Unit unit)
	{
		if (!(unit is Human human) || human.currentJoinCombatWait == null) return BTStatus.Failure;
		var wait = human.currentJoinCombatWait;

		if (wait.IsDiscoverer)
		{
			if (wait.TargetEnemy != null)
				unit.currentDir = SkillAction.GetDirection8(wait.TargetEnemy.position - unit.position);
			return BTStatus.Running;
		}

		AIMovementHelper.MoveTowardsPos(unit, wait.RallyTarget);
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
		human.currentDir = AssignFormationWatchDirection(human, esc);
		return BTStatus.Running;
	}

	// 03문서 6-6/6-7장(2026-07-27 신규) — 같은 대상을 호위 중인 다른 파티원들과 위치(전방/후방/측면)
	// 기준으로 좌우 정렬해 인원수별 감시 방향을 표대로 배정하고, 배정된 방향이 벽으로 절반 이상
	// 막히면 인접한 유효 방향으로 재배정한다. 상호작용 유닛 본인의 시야는 6-3장대로 이미 오브젝트를
	// 향하도록 조사/함정 코드가 처리하므로, 이 함수는 "다른 보호 포메이션 유닛"에만 쓰인다.
	private static Dir AssignFormationWatchDirection(Human self, Human esc)
	{
		var partyMembers = self.party?.Members;
		if (partyMembers == null) return SkillAction.GetDirection8(esc.position - self.position);

		Vector2 facing = esc.GetDirVector(esc.currentDir);
		if (facing == Vector2.zero) facing = Vector2.down;
		Vector2 right = new Vector2(facing.y, -facing.x);

		// G: 매 틱 new List 할당 → static 버퍼 재사용
		_frontBuffer.Clear();
		_backBuffer.Clear();
		var front = _frontBuffer;
		var back  = _backBuffer;

		foreach (var m in partyMembers)
		{
			if (m == null || m.hp <= 0 || m == esc) continue;
			if (m.currentFormation == null || m.currentFormation.EscortTarget != esc) continue;

			Vector2 rel = m.position - esc.position;
			if (rel == Vector2.zero) rel = facing;
			rel.Normalize();

			float fwd  = Vector2.Dot(rel, facing);
			float side = Vector2.Dot(rel, right);
			if (Mathf.Abs(fwd) >= Mathf.Abs(side)) { if (fwd >= 0f) front.Add(m); else back.Add(m); }
		}

		System.Comparison<Human> bySide = (a, b) =>
			Vector2.Dot(((Vector2)(a.position - esc.position)).normalized, right)
				.CompareTo(Vector2.Dot(((Vector2)(b.position - esc.position)).normalized, right));
		front.Sort(bySide);
		back.Sort(bySide);

		Dir ideal;
		if (front.Contains(self))
			ideal = AssignZoneDirection(front, front.IndexOf(self), esc.currentDir, isFront: true);
		else if (back.Contains(self))
			ideal = AssignZoneDirection(back, back.IndexOf(self), esc.currentDir, isFront: false);
		else
		{
			// 6-6장 "중간" 구간 — 좌우 위치는 좌/우, 애매한 중앙 잔여 인원은 감시 인원이 적은 방향.
			float side = Vector2.Dot(((Vector2)(self.position - esc.position)).normalized, right);
			if (side < -0.2f) ideal = DirUtil.RotateCW(esc.currentDir, -2);
			else if (side > 0.2f) ideal = DirUtil.RotateCW(esc.currentDir, 2);
			else ideal = LeastWatchedDirection(partyMembers, esc, self);
		}

		return ResolveWallBlocked(self, ideal, esc.currentDir);
	}

	// 전방/후방 구간 내 인원수·좌우 순서에 따라 6-6장 표대로 정면·좌우전방(후방) 방향을 배정한다.
	private static Dir AssignZoneDirection(List<Human> zone, int index, Dir facingDir, bool isFront)
	{
		Dir centerDir = isFront ? facingDir : DirUtil.Opposite(facingDir);
		Dir leftDir   = isFront ? DirUtil.RotateCW(facingDir, -1) : DirUtil.RotateCW(facingDir, -3);
		Dir rightDir  = isFront ? DirUtil.RotateCW(facingDir, 1)  : DirUtil.RotateCW(facingDir, 3);

		int count = zone.Count;
		if (count <= 1) return centerDir;

		if (count % 2 == 1)
		{
			int mid = count / 2;
			if (index == mid) return centerDir;
			return index < mid ? leftDir : rightDir;
		}
		// 짝수 — 왼쪽 절반/오른쪽 절반(중앙 없음, 겹치는 정면 시야는 자연히 재현된다).
		return index < count / 2 ? leftDir : rightDir;
	}

	private static Dir LeastWatchedDirection(List<Human> members, Human esc, Human self)
	{
		var counts = new int[8];
		foreach (var m in members)
		{
			if (m == null || m == self || m.hp <= 0) continue;
			if (m.currentFormation == null || m.currentFormation.EscortTarget != esc) continue;
			counts[(int)m.currentDir]++;
		}
		Dir best = Dir.UP;
		int bestCount = int.MaxValue;
		for (int i = 0; i < 8; i++)
			if (counts[i] < bestCount) { bestCount = counts[i]; best = (Dir)i; }
		return best;
	}

	// 6-7장: 배정된 방향의 정면 시야 거리 절반 이상이 벽/구조물에 막히면 인접한 유효 방향으로
	// 재배정한다. 후보도 전부 막히면 막히지 않은 방향 중 감시 인원이 가장 적은 방향을 쓴다.
	private static Dir ResolveWallBlocked(Human self, Dir ideal, Dir facingDir)
	{
		if (!IsDirectionWallBlocked(self, ideal)) return ideal;

		foreach (var d in GetFallbackDirections(ideal, facingDir))
			if (!IsDirectionWallBlocked(self, d)) return d;

		var members = self.party?.Members;
		Dir best = ideal;
		int bestCount = int.MaxValue;
		for (int i = 0; i < 8; i++)
		{
			var d = (Dir)i;
			if (IsDirectionWallBlocked(self, d)) continue;
			int c = 0;
			if (members != null)
				foreach (var m in members)
					if (m != null && m != self && m.currentDir == d) c++;
			if (c < bestCount) { bestCount = c; best = d; }
		}
		return best;
	}

	// 좌/우/정면/후면 차단 시 인접 유효 방향 후보(6-7장 표) — facingDir(상호작용 유닛 정면) 기준
	// 상대 위치로 판단한다.
	private static Dir[] GetFallbackDirections(Dir blocked, Dir facingDir)
	{
		int rel = ((int)blocked - (int)facingDir + 8) % 8; // 0=정면,2=우측,4=후면,6=좌측
		switch (rel)
		{
			case 6: return new[] { DirUtil.RotateCW(facingDir, -3), DirUtil.RotateCW(facingDir, -1) }; // 좌측 → 좌측후방/좌측전방
			case 2: return new[] { DirUtil.RotateCW(facingDir, 1),  DirUtil.RotateCW(facingDir, 3)  }; // 우측 → 우측전방/우측후방
			case 0: return new[] { DirUtil.RotateCW(facingDir, -1), DirUtil.RotateCW(facingDir, 1)  }; // 정면 → 좌측전방/우측전방
			case 4: return new[] { DirUtil.RotateCW(facingDir, -3), DirUtil.RotateCW(facingDir, 3)  }; // 후면 → 좌측후방/우측후방
			default: return new[] { DirUtil.RotateCW(blocked, -1), DirUtil.RotateCW(blocked, 1) };      // 이미 대각 방향이면 인접 방향
		}
	}

	private static bool IsDirectionWallBlocked(Human self, Dir dir)
	{
		if (self.Session?.cmap == null) return false;
		Vector2Int step = self.GetDirVector(dir);
		int tiles = Mathf.Max(1, Mathf.RoundToInt(VisionMath.ViewDistance(self.spotting)));
		int blocked = 0;
		for (int i = 1; i <= tiles; i++)
		{
			Vector2Int p = self.position + step * i;
			if (!self.Session.cmap.IsStaticTileWalkable(self.currentFloor, p)) blocked++;
		}
		return blocked >= tiles * 0.5f;
	}


	// ── 코어(7-3장, 2026-07-27 신규) — 리더 전용 ───────────────────

	// 리더 승계로 새로 리더가 된 유닛도 이 조건을 통해 자연스럽게 CoreInteractionState를 새로 만든다
	// (Party.PendingCoreObjectId는 파티 소유라 리더가 바뀌어도 그대로 유지됨).
	private static bool CanContinueCore(Unit unit)
	{
		if (!(unit is Human human) || human.party == null || human.party.Leader != human || human.party.PendingCoreObjectId == null)
			return false;

		// 방어적 재확인(2026-07-27) — 정상 경로면 GetPriority가 층이 다를 때 이미 우선순위를 양보해서
		// 이 지점에 도달하지 않지만, 혹시라도 층이 다른 채로 들어오면 X/Y만 보는 MoveToCore의 오작동을
		// 막기 위해 여기서도 한 번 더 막는다.
		if (human.currentFloor != human.party.PendingCorePosition.z) return false;

		if (human.currentCoreInteraction == null || human.currentCoreInteraction.CoreObjectId != human.party.PendingCoreObjectId)
		{
			human.currentCoreInteraction = new CoreInteractionState
			{
				CoreObjectId = human.party.PendingCoreObjectId,
				CorePosition = human.party.PendingCorePosition,
			};
		}

		// 8-1장: 리더 본인이 피격되거나 위협을 인지하면 코어 조사를 중단(전투/경계로 전환)한다.
		// 8-2장의 "보호 유닛 피격 시 유지" 예외는 AnyEscortHitThisTurn을 의도적으로 확인하지 않아
		// 이미 충족된다.
		if (human.isHitThisTurn || human.HasPerceivedThreatCollider())
		{
			// 2026-07-27 추가: 조사 중단으로 진행 막대도 숨긴다(트랩 해제 중단과 동일 관례).
			if (human.currentCoreInteraction.CachedProgressBar != null)
			{
				human.currentCoreInteraction.CachedProgressBar.SetProgress(0f, false);
			}
			else
			{
				human.Session?.GetObjectVisual(human.currentCoreInteraction.CorePosition)?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);
			}
			human.currentCoreInteraction = null;
			return false;
		}
		return true;
	}

	private static BTStatus MoveToCore(Unit unit)
	{
		var human = (Human)unit;
		var core  = human.currentCoreInteraction;
		if (core == null) return BTStatus.Failure;

		// 2026-07-27 버그 수정("코어가 없는 이상한 곳에서 코어 로직이 실행됨") — 아래 도착 판정이
		// X/Y만 비교하고 층은 안 봐서, 리더가 다른 층에서 우연히 같은 X/Y 근처에 있으면 그 자리를
		// "도착"으로 착각했다. 정상 경로면 GetPriority가 층이 다를 때 이미 계단 이동으로 양보하지만,
		// 여기서도 한 번 더 막아 절대 다른 층에서 도착 판정이 나지 않게 한다.
		if (human.currentFloor != core.CorePosition.z) return BTStatus.Running;

		if (human.Session == null || !human.Session.objectGrid.TryGetValue(core.CorePosition, out var obj) || obj.IsInvestigated)
		{
			human.currentCoreInteraction = null;
			human.party.PendingCoreObjectId = null;
			return BTStatus.Success;
		}
		if (!human.personalMap.IsObjectKnown(obj.Id))
			human.personalMap.RegisterObject(obj.Id, obj.Position, obj.BaseDanger, obj.BaseInterest, obj.Tags, obj.CauserStage);

		var pos = new Vector2Int(core.CorePosition.x, core.CorePosition.y);

		// 2026-07-27 사용자 신고("유닛이 코어에 겹쳐서 포메이션을 잡음") — 코어는 Passable이라 리더가
		// 실제로 그 타일 위까지 걸어가 설 수 있다(도착 판정이 Chebyshev≤1이라 거리 0도 통과). 리더가
		// 코어 타일 자체를 점유하면 호위 슬롯 계산의 기준점(escortTarget.position)도 코어 위가 돼버려
		// 포메이션 전체가 코어 위/주변에 이상하게 겹친다. 정확히 그 타일에 서 있으면 인접 빈 칸으로
		// 한 걸음 물러난 뒤에만 "도착"으로 인정한다.
		if (human.position == pos)
		{
			StepOffObjectTile(human, pos);
			return BTStatus.Running;
		}

		if (AIMovementHelper.IsAdjacent(human.position, pos)) return BTStatus.Success;

		// 2026-07-27 사용자 신고("보호 포메이션 동안 다른 유닛에게 길이 막혀서 리더가 코어에 영구히
		// 도착 못함") 방어책 — 근본 원인(호위가 이동 중인 리더의 전방을 가로막던 것)은
		// GetEscortSlotPosition 쪽에서 고쳤지만, 혹시 다른 이유로 한 칸도 못 나아가면(길이 완전히
		// 막힘) PlayerCommandFSMState.ExecutePlayerMove와 동일한 관례로 근처 빈 칸으로 목표를 잠깐
		// 대신해 우회를 시도한다 — 매 틱 다시 원래 pos로 재시도하므로 영구 고착은 아니다.
		if (!AIMovementHelper.MoveTowardsPos(human, pos))
		{
			Vector2Int fallback = AIMovementHelper.FindNearbyOpenTile(human, pos);
			if (fallback != pos) AIMovementHelper.MoveTowardsPos(human, fallback);
		}
		return BTStatus.Running;
	}

	// 위 MoveToCore 전용 — 오브젝트 자신의 타일에 정확히 서 있을 때 인접한 이동 가능 타일로 한 걸음
	// 물러난다(9-6장 TrapPartySystem.StepAwayFromTrap과 동일한 관례).
	private static void StepOffObjectTile(Human human, Vector2Int objectPos)
	{
		for (int i = 0; i < 8; i++)
		{
			Vector2Int candidate = objectPos + human.GetDirVector((Dir)i);
			if (human.CanMove(candidate))
			{
				AIMovementHelper.MoveTowardsPos(human, candidate);
				return;
			}
		}
	}

	private static BTStatus CoreInvestigatePerform(Unit unit)
	{
		var human = (Human)unit;
		var core  = human.currentCoreInteraction;
		if (core == null) return BTStatus.Failure;
		if (human.Session == null || !human.Session.objectGrid.TryGetValue(core.CorePosition, out var obj) || obj.IsInvestigated)
		{
			human.currentCoreInteraction = null;
			human.currentAlertSearch = null; // 03문서 4-5장(2026-08-06): 낡은 경계 상태 잔재 정리
			if (human.party != null) human.party.PendingCoreObjectId = null;
			return BTStatus.Success;
		}

		// 07문서 10장: 코어 조사가 실제로 시작되는 시점에 "진행 중" 정보를 1회 전파(보호 포메이션 참여 자격).
		if (!core.Active) PropagationSystem.NotifyInteractionStarted(human);
		core.Active = true; // UnitFunction.OnUpdate가 이 플래그를 보고 전파/진행도 타이머를 흘려보낸다.
		if (!core.PropagationDone) return BTStatus.Running;
		if (core.Progress01 < 1f) return BTStatus.Running;

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
			human.party.PendingCoreObjectId = null;
		}

		// 2026-07-27 수정("코어 조사 후에도 코어가 사라지지 않는다") — 보스방 던전 코어는 Loot 하위
		// 태그도 함께 갖도록 통합됐는데(GameSession.SpawnInitialDungeonCore), 코어 조사는 일반
		// InvestigationState가 아니라 이 별도 CoreInteractionState로 진행되기 때문에 조사가 끝나도
		// TacticalFSMState.PickUpObject(Loot 실제 회수 처리)로 자연스럽게 이어지지 않았다. 리더 조사가
		// "발견~조사 흐름"의 마지막 단계이자 이 오브젝트의 최초·유일한 조사이므로, PickUpObject의
		// Loot 분기와 동일한 처리를 여기서 그대로 수행해 리더 본인이 즉시 회수하게 한다(일반 Loot
		// 오브젝트가 조사 직후 같은 유닛이 즉시 집어가던 것과 동일 동작 — "다른 코어의 기능은 그대로
		// 둔채" 요청대로 웨이브 목표 회수 자체는 그대로 유지). Loot 태그가 없는 테스트 전용 코어
		// (SpawnCoreAt)는 이 분기를 안 타 그대로 남는다(의도대로).
		if (obj.Tags.Exists(t => t.Contains("Loot")))
		{
			human.Session.CollectObject(obj.Position);
			human.collectedObjects.Add(obj.Id);
			if (!human.pendingStairTargetFloor.HasValue)
				human.pendingStairTargetFloor = human.currentFloor + 1;
		}
		else
		{
			// 2026-07-27 추가: Loot 태그 없는 테스트 전용 코어는 회수되지 않고 그대로 남으므로,
			// 완료된 진행 막대를 직접 숨겨야 한다(Loot 케이스는 CollectObject가 오브젝트째 파괴함).
			if (human.currentCoreInteraction != null && human.currentCoreInteraction.CachedProgressBar != null)
			{
				human.currentCoreInteraction.CachedProgressBar.SetProgress(0f, false);
			}
			else
			{
				human.Session?.GetObjectVisual(obj.Position)?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);
			}
		}

		// 코어 파괴/특정 상호작용/인류 메리트·플레이어 디메리트 등 후속 효과는 코어·핵심방어목표 문서
		// (추후 작성)의 몫 — 이번 구현은 사용자 확인대로 "발견~리더조사 흐름"까지만 다룬다.
		human.currentCoreInteraction = null;
		human.currentAlertSearch = null; // 03문서 4-5장(2026-08-06): 낡은 경계 상태 잔재 정리
		return BTStatus.Success;
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
		if (unit is Human hc && hc.party != null && hc.party.Leader == hc && hc.party.PendingCoreObjectId != null) return "전술(코어)";
		return "전술";
	}
}
