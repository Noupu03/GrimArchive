using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;
using GrimArchive.Wave;

// 전술 상태 — 제자리·국소 반응 행동 담당(공황/함정/경계/조사/대기/포메이션). FSM은 "적 있으면 Combat,
// 전술 조건 있으면 Tactical, 나머지는 Navigation"만 결정하고, 세부 우선순위는 BT가
// TacticalBehaviorPriorityConfig의 순서·활성화 여부로 구성한다. 비주얼 스크립팅 전환 시 BuildBTNodes()의
// 각 항목이 BTNodeSO 인스턴스로 대응된다.
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
			// 설정 에셋 없음 — 03문서 2-1장 기본 순서로 폴백.
			// 2026-09-04(사용자 요청): 보호 포메이션은 인류측 전부 비활성화 — 되돌릴 때는 아래 줄 주석을
			// 해제한다(TacticalBehaviorPriorityConfig.cs 기본값도 같이). 함정 대응은 그대로 유지한다(같은 날 해제
			// 시도자 선정을 "가장 가까운 1명"으로 줄였던 단순화는 2026-09-30 검증 03-12에서 문서대로 복원했다).
			_bt = new BTSelector(
				nodes[TacticalBehaviorType.Panic],
				nodes[TacticalBehaviorType.JoinCombatWait],
				nodes[TacticalBehaviorType.TrapResponse],
				nodes[TacticalBehaviorType.Alert],
				nodes[TacticalBehaviorType.Investigate],
				nodes[TacticalBehaviorType.Wait],
				// nodes[TacticalBehaviorType.Formation],
				nodes[TacticalBehaviorType.CoreAttack],
				nodes[TacticalBehaviorType.DoorAttack],
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
				// 07문서 16-4장: 함정 대응 대기·해제는 함정작동음을 제외한 소리에 중단된다.
				// 함정작동음 자체는 이 조건에 애초에 걸리지 않아 표대로 "유지"된다.
				// 검증문서 03-11(순서도 03-13): 해제자·보호자 피격·위협 인지·미식별 공격 수색 중에도 양보한다 — 소리와 달리
				// 필요한 이동을 함정이 막을 때는 양보하지 않고 우회·통과·파괴로 이어진다(IsTrapResponseInterrupted).
				new BTCondition(unit => !IsTrapResponseBlockedBySound(unit) && !IsTrapResponseInterrupted(unit)),
				new BTSelector(
					// 9-2~9-6장(신규): 발견 유닛이지만 해제 담당으로 선정되지 않았으면 현 위치에서 담당자 도착을 기다린다.
					new BTSequence(new BTCondition(IsAwaitingSelectedUnit), new BTLeaf(TrapAwaitSelectedUnit)),
					// Disarm → Bypass → Destroy 내부 순서는 고정 (문서 9장 우선순위, 통과는 비전투 체인에서 제외 — TrapBypass 주석 참고)
					new BTSequence(
						new BTCondition(CanDisarm),
						new BTLeaf(TrapJoinWait),
						new BTLeaf(MoveToTrap),
						new BTLeaf(TrapDisarmPerform)
					),
					new BTLeaf(TrapBypass),
					new BTLeaf(TrapDestroy),
					// 검증문서 03-13(v0.6 9-6): 응답 상태 없이 알려진 활성 함정의 인접 1칸 안에 있는 유닛은 구역 밖으로 빠져나온다 — 응답자는 위 분기가 먼저 잡는다.
					new BTLeaf(ZoneEscape)
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
			[TacticalBehaviorType.CoreAttack] = new BTSequence(
				new BTCondition(HasCoreAttackTarget),
				new BTLeaf(MoveToCoreAttack),
				new BTLeaf(CoreAttackPerform)
			),
			[TacticalBehaviorType.DoorAttack] = new BTSequence(
				new BTCondition(HasDoorAttackTarget),
				new BTLeaf(MoveToDoorAttack),
				new BTLeaf(DoorAttackPerform)
			)
		};
	}

	public float GetPriority(Unit unit)
	{
		// 플레이어 수동 명령(공격/이동)은 PlayerCommandFSMState(UnitFSM._states 배열 맨 앞, 최우선)가
		// 전담한다 — 명령이 활성 상태면 이 GetPriority는 아예 호출되지도 않으므로(패닉/함정/경계
		// 포함해) 여기서 따로 예외 처리할 필요가 없다.
		// 던전 입구 시퀀스 동안은 이 상태를 비활성화한다 — 입구 이동·대기 단계는 그 시스템이 위치를
		// 직접 다루고, 계단 접근 이동 명령(우선순위 200)이 끝난 뒤 실제로 계단을 건너기 전까지의 틈도
		// 보호해야 한다(안 그러면 조사 대상 발견 등으로 이 상태가 끼어들어 새치기한다).
		// isInDungeonEntranceSequence는 CrossStairs가 실제로 층을 건너는 순간 개인별로 해제한다.
		if (unit is Human entranceHu && entranceHu.isInDungeonEntranceSequence) return 0f;
		float p = AIConfigLoader.Behavior?.tacticalPriority ?? 50f;
		if (IsPanic(unit)) return p;
		if (unit is Human joinWaitHu && joinWaitHu.currentJoinCombatWait != null) return p;
		if (unit.currentTrapInteraction != null) return p;
		if (TrapAvoidance.NeedsZoneEscape(unit)) return p; // 검증문서 03-13: 응답 없이 함정 구역 안에 있으면 탈출(ZoneEscape)이 우선
		if (unit.currentAlertSearch != null) return p;
		Human hu = unit as Human;
		if (hu != null && (hu.currentInvestigation != null || hu.HasReachableInvestigateTarget())) return p;
		if (hu != null && hu.currentWait != null) return p;
		// 2026-09-04(사용자 요청): 인류측 보호 포메이션 전부 비활성화 — GetPriority도 같이 꺼야
		// Tick()의 BT에 그 행동이 없는데도 Tactical에 붙잡혀 Idle/Navigation을 못 하는 걸 막는다.
		// if (hu != null && hu.HasProtectiveFormationNeed()) return p;
		if (HasCoreAttackTarget(unit)) return p;
		if (HasDoorAttackTarget(unit)) return p;
		return 0f;
	}

	// Sticky 없음 — BT가 Running 반환으로 서브태스크 연속성을 자연히 유지한다.
	// 피격·위협 등의 인터럽트는 각 BT 브랜치의 Condition이 Failure를 반환하여 처리.
	public bool IsSticky(Unit unit)        => false;
	public bool ShouldInterrupt(Unit unit) => true;
	public void OnEnter(Unit unit)         { }

	public void OnExit(Unit unit)
	{
		// 조사/함정 해제 중 외부 원인(플레이어 명령 등)으로 Tactical 상태를 완전히 벗어날 때 페널티
		// 해제 + 진행도 50% 손실(5-6/9-7장). 같은 상태 안에서 브랜치만 바뀌는 중단(피격/위협 인지)은
		// 여기를 안 타므로 CanInvestigate/CanDisarm이 각각 직접 처리한다.
		if (unit is Human human && human.currentInvestigation != null && human.currentInvestigation.PenaltyActive)
			ApplyInvestigateInterruptPenalty(human);

		if (unit.currentTrapInteraction != null)
		{
			TrapPartySystem.ApplyDisarmInterruptPenalty(unit); // PenaltyActive 가드 내장 — 대응 상태·남은 진행도는 유지
			unit.currentTrapInteraction.DestroyActive = false; // Tactical을 벗어나면 파괴 진행도 누적도 멈춘다
		}
	}

	// 검증문서 03-11: 이번 틱에 해제/파괴를 실제로 수행하지 못했는데 진행 중이었다면(Panic·전투 합류 대기 등 BT 상위
	// 분기가 가로챔) 그 자리에서 중단으로 처리한다 — 같은 상태 안에서 브랜치만 바뀌는 경로는 OnExit를 안 타고 각 분기의
	// 조건도 이 사건을 모르기 때문이다. 수행 여부는 TrapDisarmPerform/TrapDestroy가 PerformedThisTick으로 알린다.
	public BTStatus Tick(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap != null) trap.PerformedThisTick = false;

		BTStatus status = _bt.Tick(unit);

		if (trap != null && ReferenceEquals(trap, unit.currentTrapInteraction) && !trap.PerformedThisTick)
		{
			TrapPartySystem.ApplyDisarmInterruptPenalty(unit);
			trap.DestroyActive = false;
		}
		return status;
	}
	public string   GetLabel(Unit unit) => GetSubLabel(unit);

	// ── FSM 진입 조건 헬퍼 ─────────────────────────────────────────

	// 공황 판정의 단일 출처(PlayerCommandFSMState도 이걸 쓴다). 2026-10-02부터 AIBehaviorConfig.panicBehaviorEnabled(기본 false)로 임시 비활성 — 도주·후퇴 문서 이후 켠다.
	internal static bool IsPanic(Unit unit)
		=> (AIConfigLoader.Behavior?.panicBehaviorEnabled ?? false)
			&& unit is Human && unit.BaseStat.mental < unit.BaseStat.maxMental * (AIConfigLoader.Behavior?.panicMentalRatio ?? 0.3f);

	// 함정 해제: 피격 또는 위협 인지 시 이 틱에 한해 Failure → Selector가 다음 분기로 넘어감. 9-7장
	// 중단 조건이라 진행도 50% 손실도 여기서 같이 처리한다(브랜치만 바뀌는 경로는 OnExit을 안 탄다).
	private static bool CanDisarm(Unit unit)
	{
		if (!IsDisarmWorthy(unit)) return false;
		if (unit.isHitThisTurn || unit.HasPerceivedThreatCollider())
		{
			TrapPartySystem.ApplyDisarmInterruptPenalty(unit);
			return false;
		}
		// 8-2장: 보호 유닛이 피격당하면 예외 없이 항상 중단(함정 해제는 7장의 "파티 목표 오브젝트"가
		// 될 수 없음 — 대상은 회수/조사 오브젝트뿐).
		if (unit is Human humanDisarmer && humanDisarmer.AnyEscortHitThisTurn())
		{
			TrapPartySystem.ApplyDisarmInterruptPenalty(unit);
			return false;
		}
		return true;
	}

	private static bool CanInvestigate(Unit unit)
	{
		if (!(unit is Human human)) return false;
		if (human.playerAttackTarget != null || (human.playerMoveTarget.HasValue && human.isManualMoveCommand)) return false;
		// 00-04/01-06/01-07/01-08: 아직 조사를 시작하지 않았다면 집결 대기·다음 방 공동 이동·코어 보고
		// 이동·귀환 중에는 새 조사를 시작하지 않는다. 이미 시작한 조사(currentInvestigation != null)는
		// 이 게이트에 안 걸려 기존 유지·중단 조건 그대로 계속된다.
		if (human.currentInvestigation == null && human.currentWait != null &&
			(human.currentWait.Reason == WaitReason.AwaitingPartyAtRallyPoint ||
			 human.currentWait.Reason == WaitReason.AdvancingToNextRoom ||
			 human.currentWait.Reason == WaitReason.ReportingCoreToLeader ||
			 human.currentWait.Reason == WaitReason.SearchingNextDoor ||
			 human.currentWait.Reason == WaitReason.FormingUpAtDoor ||
			 human.currentWait.Reason == WaitReason.BreachingDoor ||
			 human.currentWait.Reason == WaitReason.EnteringNextRoom ||
			 human.currentWait.Reason == WaitReason.Retreating))
			return false;
		if (human.currentInvestigation == null && !human.HasReachableInvestigateTarget()) return false;
		// 03번 문서 4장: 전투 관련 소리·이동음도 일반 조사를 중단시키며 진행도 손실이 적용된다 —
		// IsTrapResponseBlockedBySound(함정 해제 쪽)와 동일 패턴. HasAlert보다 먼저 걸려야 Alert
		// 분기가 대신 가로채면서 페널티 없이 조사가 조용히 밀려나는 걸 막는다. 검증문서 03-04: 같은
		// 표가 "파티 목표·코어 상호작용 당사자는 소리 반응 없음"도 명시하므로, IsPartyGoalTarget이면
		// 이 체크 자체를 건너뛴다(PropagationSystem.IsSoundReactionSuppressed가 Alert 승격 자체를
		// 막는 것과 짝 — 그쪽만으로는 이 독립된 체크까지 막지 못해 따로 게이팅이 필요했다).
		if (human.currentInvestigation?.IsPartyGoalTarget != true && PropagationSystem.HasPendingInterruptingSound(human))
		{
			ApplyInvestigateInterruptPenalty(human);
			return false;
		}
		// 피격·위협 인지 시 이 틱에 한해 조사 중단(5-6장) — 진행도 50% 손실도 같이 처리한다(CanDisarm과 동일 패턴).
		if (unit.isHitThisTurn || unit.HasPerceivedThreatCollider())
		{
			ApplyInvestigateInterruptPenalty(human);
			return false;
		}
		// 검증문서 03-01 4번: 원본 표 41줄(일반 조사: 보호자 피격도 중단)과 46줄(유지 우선 파티
		// 목표 상호작용: 보호자 피격은 유지, 보호자가 대응)은 서로 다른 행이다 — currentInvestigation.
		// IsPartyGoalTarget이 그 구분을 담당한다. 본인 피격(위)은 41·46줄 공통이라 이 플래그와 무관하게
		// 항상 중단된다. 아직 조사를 시작 전(currentInvestigation==null)이면 "유지" 예외를 적용할
		// 대상 자체가 없으므로 `?.` 로 안전하게 기본값(41줄 규칙)으로 처리한다.
		if (human.currentInvestigation?.IsPartyGoalTarget != true && human.AnyEscortHitThisTurn())
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

	// 07문서 16장: currentAlertSearch가 비어있어도 시작 안 한 유효한 소리 반응이 있으면 여기서 지연
	// 승격한다. 인류/몬스터 구분 없이 호출 — Human 전용 게이팅을 걸면 몬스터 쪽이 영영 승격 못 한다.
	// 05번 문서 10장: 귀환 중엔 이미 시작한 경계(currentAlertSearch)는 유지하되, 새로 소리로 경계를
	// 승격하는 것만 막는다(방향 확인 자체는 ResolveVisionDirection이 독립적으로 처리).
	private static bool HasAlert(Unit unit)
	{
		if (unit.currentAlertSearch != null) return true;
		if (unit is Human retreatingHuman && retreatingHuman.currentWait?.Reason == WaitReason.Retreating) return false;
		return PropagationSystem.TryPromotePendingSoundToAlert(unit);
	}
	private static bool HasFormationNeed(Unit unit)
		=> unit is Human human && human.HasProtectiveFormationNeed();
	private static bool HasJoinCombatWait(Unit unit) => unit is Human human && human.currentJoinCombatWait != null;

	// 07문서 16-4장: 함정 대응 대기·해제는 함정작동음을 제외한 소리에 중단된다. 진행도가 있었으면
	// (5-6/9-7장과 동일한 패턴) 중단 시 50% 손실을 함께 적용한다.
	private static bool IsTrapResponseBlockedBySound(Unit unit)
	{
		if (!(unit is Human human)) return false;
		if (!PropagationSystem.HasPendingInterruptingSound(human)) return false;
		TrapPartySystem.ApplyDisarmInterruptPenalty(unit);
		return true;
	}

	// 검증문서 03-11(순서도 03-13, v0.6 9-9·12-2): 해제자 피격·위협 콜라이더 인지·보호자 피격·자기 피격으로 시작된 미식별 공격
	// 수색 중이면 해제를 중단(진행도 50% 손실)하고 우선 대응에 양보한다. 대응 상태와 남은 진행도는 유지하므로 원인이
	// 사라지면(수색 15초 완료 등) 그대로 재개된다 — 이전엔 CanDisarm 실패 직후 TrapBypass가 상태를 지워 재개가 불가능했다.
	// 필요한 이동을 함정이 막는 경우만 양보하지 않고 기존 체인(우회 실패 → 통과·파괴, 순서도 03-14)으로 넘긴다.
	private static bool IsTrapResponseInterrupted(Unit unit)
	{
		if (!(unit is Human human) || human.currentTrapInteraction == null) return false;
		bool interrupted = human.isHitThisTurn || human.HasPerceivedThreatCollider() || human.AnyEscortHitThisTurn()
			|| human.currentAlertSearch?.IsUnidentifiedAttackSearch == true;
		if (!interrupted) return false;

		TrapPartySystem.ApplyDisarmInterruptPenalty(human);
		return !IsBlockingPath(human);
	}

	// ── 공황 ───────────────────────────────────────────────────────

	private static BTStatus Panic(Unit unit)
	{
		// 공황은 매 틱 돌아 로그가 콘솔을 뒤덮는다 — 이동/정지 로그는 두지 않는다.
		if (Random.value > 0.5f)
			unit.Move((Dir)Random.Range(0, 8));
		return BTStatus.Running;
	}

	// ── 함정 공통 ─────────────────────────────────────────────────

	private static bool IsDisarmWorthy(Unit unit)
	{
		if (!(unit is Human human) || human.currentTrapInteraction == null) return false;
		var trap = human.currentTrapInteraction;
		if (human.Session == null || !human.Session.objectGrid.ContainsKey(trap.TrapPosition)) return false;
		return TrapPartySystem.WouldAttemptDisarm(human, trap.TrapObjectId); // 50% 규칙의 단일 출처 — 선정 후보 판정과 공유
	}

	private static bool IsBlockingPath(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null) return false;
		if (trap.IsBlockingPath.HasValue) return trap.IsBlockingPath.Value;
		trap.IsBlockingPath = ComputeIsBlockingPath(unit, trap.TrapPosition);
		return trap.IsBlockingPath.Value;
	}

	// 검증문서 03-13: 목적지는 명령 이동·조사 목표뿐 아니라 파티 이동(대기 위치·리더 보고·집결지)과 자유 탐색 목표까지 본다(TrapPartySystem.GetCurrentDestination).
	private static bool ComputeIsBlockingPath(Unit unit, Vector3Int trapPos)
	{
		Vector2Int? dest = unit is Human h ? TrapPartySystem.GetCurrentDestination(h) : unit.playerMoveTarget;
		return dest.HasValue && IsRouteBlockedByTrap(unit, dest.Value, trapPos);
	}

	// 유닛 위치에서 dest까지 이 함정의 회피 구역(함정 타일 + 인접 1칸, 03번 v0.12 9장·v0.6 9-6)에 들어가지 않고는 갈 수 없는지(대체 경로 없음) — 이미 발견한
	// 지형(discoveredMap) 기준 BFS이고 미탐색 타일은 통행 가능으로 본다. 출발 타일은 구역 안이어도 된다(이미 안에 있으면 빠져나온다). 목적지가 구역 안이면 막힌
	// 것이다. TrapPartySystem이 집결·이동 중인 유닛의 "경로를 막는 함정" 예외 판정에도 쓴다.
	public static bool IsRouteBlockedByTrap(Unit unit, Vector2Int dest, Vector3Int trapPos)
	{
		FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
		int fi = unit.currentFloor;
		if (myData == null || myData.discoveredMap == null || fi >= myData.discoveredMap.Length || myData.discoveredMap[fi] == null)
			return false;

		int mapW = myData.discoveredMap[fi].GetLength(0);
		int mapH = myData.discoveredMap[fi].GetLength(1);
		// 시작점이 지형 정보 밖이면 판단 근거가 없다 — 막지 않는 것으로 본다(실제 유닛은 항상 안이라 게임 동작은 그대로).
		if (unit.position.x < 0 || unit.position.x >= mapW || unit.position.y < 0 || unit.position.y >= mapH) return false;
		Vector2Int trap2D = new Vector2Int(trapPos.x, trapPos.y);
		// 검증 04-08: 길찾기와 같은 "개인이 아는 지형"으로 판정한다(끄면 진영 공용 지도) — 이 BFS만 공용 지도를 쓰면 A*와 막힘 판정이 어긋난다.
		IKnownTerrain known = (AIConfigLoader.Behavior?.personalMapPathingEnabled ?? true) ? unit.KnownTerrain : null;

		var visited = new HashSet<Vector2Int> { unit.position };
		var queue   = new Queue<Vector2Int>();
		queue.Enqueue(unit.position);

		while (queue.Count > 0)
		{
			Vector2Int cur = queue.Dequeue();
			if (cur == dest) return false;
			foreach (Dir d in _allDirs)
			{
				Vector2Int next = cur + unit.GetDirVector(d);
				if (visited.Contains(next)) continue;
				if (next.x < 0 || next.x >= mapW || next.y < 0 || next.y >= mapH) continue;
				int terrain = known != null ? known.GetTileTerrain(new Vector3Int(next.x, next.y, fi)) : myData.discoveredMap[fi][next.x, next.y];
				if (terrain == 2) continue;
				if (ExplorationMath.IsInTrapZone(next, trap2D)) continue;
				visited.Add(next); queue.Enqueue(next);
			}
		}
		return true;
	}

	// ── 함정 발견/선정 조율(9-2~9-7장 신규) ─────────────────────────

	private static bool IsAwaitingSelectedUnit(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		return trap != null && trap.SelectedUnitName != null && !trap.IsSelectedDisarmer;
	}

	// 순서도 03-12-2: 담당자가 도착했다는(또는 도착 전에 대응을 마쳤다는) 통지를 받기 전까지 발견 유닛은 현재
	// 위치에서 기다린다. 통지 수신과 기한 만료 판단은 TrapPartySystem.TickWaitingForSelectedUnit(OnUpdate)이
	// 맡고, 도착 이후 해제 절차는 담당자의 몫이라 통지를 받으면 개인 행동으로 복귀한다.
	private static BTStatus TrapAwaitSelectedUnit(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null) return BTStatus.Failure;
		if (!trap.AssigneeDone) return BTStatus.Running;

		TrapPartySystem.EndResponse(unit, TrapEndReason.WaitEnded);
		return BTStatus.Success;
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
		var human = unit as Human;
		Vector2Int trapPos = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		// 함정도 오브젝트처럼 자신의 타일을 점유하므로 정확 일치 대신 Chebyshev ≤ 1(바로 옆 1칸)로
		// 도달 판정한다 — MoveToInvestigateTarget과 동일한 관례.
		if (AIMovementHelper.IsAdjacent(unit.position, trapPos))
		{
			if (human != null)
			{
				// 재선정으로 담당이 바뀐 뒤 뒤늦게 도착한 이전 담당자는 해제를 시작하기 전에 양보한다(이중 담당 방지).
				// 이미 시작한 해제(Phase != AwaitingJoin)는 기존 중단 조건으로 끝까지 간다.
				if (trap.Phase == TrapPhase.AwaitingJoin && !TrapPartySystem.IsStillAssigned(human, trap))
				{
					LogHelper.Log(LogHelper.GAME, $"[함정] {human.name}: 재선정으로 담당이 바뀌어 해제를 양보합니다");
					TrapPartySystem.EndResponse(unit, TrapEndReason.Yielded);
					return BTStatus.Failure;
				}
				TrapPartySystem.ReportArrived(human, trap);
			}
			return BTStatus.Success;
		}
		// 순서도 03-12: 이동 중 예상 도착 시점이 달라졌으면 기다리는 발견 유닛에게 알린다.
		if (human != null) TrapPartySystem.ReportProgress(human, trap);
		return MoveTowardsTrapGuarded(unit, trapPos);
	}

	// 검증문서 03-11: 함정 접근 이동(MoveToTrap/TrapDestroy 공용) — 다른 BT 리프(MoveToInvestigateTarget 등)와 같은
	// stuck 탈출 패턴이다. 리셋 기준은 "이동 성공 여부"가 아니라 "체비셰프 거리가 실제로 줄었는지"(혼잡 구역의 제자리
	// 셔플도 Move() 관점에선 성공이라 그것만 보면 카운터가 안 쌓인다). 대체 자리 탐색은 이미 근접(반경 2)했을 때만 쓴다.
	// 사방이 구조적으로 막혀 있으면 즉시, 그저 몰려 막힌 거면 한도까지 인내한 뒤 포기한다 — 포기하면 대응을 접고
	// 경계로 전환한다(안 하면 도달 불가능한 함정 앞에서 영원히 멈춘다).
	private static BTStatus MoveTowardsTrapGuarded(Unit unit, Vector2Int trapPos)
	{
		int distBefore = AIMovementHelper.ChebyshevDistance(unit.position, trapPos);
		AIMovementHelper.MoveTowardsPos(unit, trapPos);
		if (AIMovementHelper.ChebyshevDistance(unit.position, trapPos) < distBefore)
		{
			unit.trapMoveStuckTurns = 0;
			return BTStatus.Running;
		}

		Vector2Int fallback = AIMovementHelper.IsAdjacent(unit.position, trapPos, radius: 2)
			? AIMovementHelper.FindNearbyOpenTile(unit, trapPos)
			: trapPos;
		if (fallback != trapPos)
		{
			AIMovementHelper.MoveTowardsPos(unit, fallback);
			if (AIMovementHelper.ChebyshevDistance(unit.position, trapPos) < distBefore)
			{
				unit.trapMoveStuckTurns = 0;
				return BTStatus.Running;
			}
		}

		if (AIMovementHelper.HasAnyStructurallyOpenAdjacentTile(unit)
			&& ++unit.trapMoveStuckTurns < (AIConfigLoader.Behavior?.trapMoveStuckTurnLimit ?? 4))
			return BTStatus.Running;

		LogHelper.Log(LogHelper.GAME, $"[함정] {unit.name}: 함정에 도달할 수 없어 대응을 포기합니다");
		unit.trapMoveStuckTurns = 0;
		TrapPartySystem.EndResponse(unit, TrapEndReason.Unreachable);
		unit.currentAlertSearch = new AlertSearchState();
		return BTStatus.Failure;
	}

	private static BTStatus TrapDisarmPerform(Unit unit)
	{
		if (!IsDisarmWorthy(unit)) return BTStatus.Failure;
		var human = (Human)unit;
		var trap  = human.currentTrapInteraction;
		if (trap == null || !human.Session.objectGrid.TryGetValue(trap.TrapPosition, out var obj))
		{
			TrapPartySystem.EndResponse(human, TrapEndReason.Resolved);
			return BTStatus.Success;
		}
		trap.Phase = TrapPhase.Disarming;
		// 07문서 10장: 해제가 실제로 시작되는 시점에 "진행 중" 정보를 1회 전파(보호 포메이션 참여 자격).
		if (!trap.PenaltyActive) PropagationSystem.NotifyInteractionStarted(human);
		trap.PenaltyActive = true;
		trap.PerformedThisTick = true; // 이번 틱에 해제를 수행했다 — Tick 래핑이 "수행 없이 끝난 틱"을 중단으로 잡는다
		if (trap.DisarmProgress01 < 1f) return BTStatus.Running;

		float rate    = ExplorationMath.TrapDisarmSuccessRate(human.concentration, human.level, understandingApplied: 0);
		human.personalMap.RecordTrapAttempt(trap.TrapObjectId, rate);

		// 9-7/9-8장: 성공/실패 결과 문구 — 오브젝트가 사라지기 전에 위치를 먼저 잡아둔다(성공 시
		// CollectObject가 비주얼을 파괴함). 문구는 독립 GameObject라 오브젝트 파괴와 무관하게 유지된다.
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
			// 9-5장: 해제 성공 시에만 도감에 기록한다(우회·파괴·통과는 기록 안 함). 함정 종류별 도감
			// ID 체계가 아직 없어 오브젝트 고유 Id를 그대로 넘긴다.
			Game.Encyclopedia.EncyclopediaManager.Instance?.UnlockEntry(trap.TrapObjectId);
			TrapPartySystem.EndResponse(human, TrapEndReason.Resolved);
			human.currentAlertSearch = null; // 03문서 4-5장: 낡은 경계 상태 잔재 정리
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

	// ── 함정 대안 2종(우회·파괴) ───────────────────────────────────
	// 검증문서 03-13: 함정 통과(피해를 맞고 지나감)는 이 비전투 체인에서 뺐다 — 03번 v0.12 9장 통과 판단표 1행("일반 탐색·이동: 우회·해제·파괴 중 가능한 대응,
	// 미확인 피해를 감수한 임의 통과 금지")대로 통과는 전투 합류·긴급 아군 보호·도주 후퇴에서만 판단하며, 그 판단은 이동 계층(TrapAvoidance 전투 모드)과
	// AIMovementHelper.MoveTowardsProtectTargetWithRiskCheck가 맡는다.

	// 우회 — 필요한 이동을 함정이 막지 않을 때만 성립한다. "이동 경로 변경"은 이동 계층이 담당한다(알려진 함정 구역을 피해 가는 A*) — 예전처럼 함정 곁으로
	// 한 걸음 옮기고 대응만 끝내면 실제 경로는 그대로라 이후 이동이 함정을 밟을 수 있었다.
	private static BTStatus TrapBypass(Unit unit)
	{
		if (!(unit is Human human) || human.currentTrapInteraction == null || IsBlockingPath(unit))
			return BTStatus.Failure;
		TrapPartySystem.EndResponse(human, TrapEndReason.Bypassed);
		human.currentAlertSearch = null; // 03문서 4-5장: 낡은 경계 상태 잔재 정리
		return BTStatus.Success;
	}

	private static BTStatus TrapDestroy(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null) return BTStatus.Failure;
		if (unit.Session == null || !unit.Session.objectGrid.TryGetValue(trap.TrapPosition, out var obj))
		{
			TrapPartySystem.EndResponse(unit, TrapEndReason.Resolved);
			return BTStatus.Success;
		}
		// 03번 v0.12 9장(584~586줄): 모든 오브젝트를 파괴 가능한 것으로 취급하지 않는다 — 함정은 체력이 있어야 하고 공격력이
		// 없는 유닛은 깰 수 없다. 파괴할 수 없으면 다른 대응(순서도 03-11)으로 넘긴다.
		if (obj.TrapMaxHp <= 0f || unit.physicalAttack <= 0f)
		{
			TrapPartySystem.EndResponse(unit, TrapEndReason.Bypassed);
			return BTStatus.Success;
		}
		var trapPos2D = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		if (Vector2Int.Distance(unit.position, trapPos2D) > 1.5f)
			return MoveTowardsTrapGuarded(unit, trapPos2D);
		trap.Phase = TrapPhase.Destroying;
		trap.PerformedThisTick = true; // 파괴 진행도 누적 게이트(DestroyActive)는 이 틱에 파괴를 수행한 동안만 켜진다
		trap.DestroyActive = true;
		if (obj.TrapHp > 0f) return BTStatus.Running;
		LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 함정을 파괴했습니다.");
		unit.Session.CollectObject(trap.TrapPosition);
		TrapPartySystem.EndResponse(unit, TrapEndReason.Destroyed);
		unit.currentAlertSearch = null; // 03문서 4-5장: 낡은 경계 상태 잔재 정리
		return BTStatus.Success;
	}

	// 검증문서 03-13(v0.6 9-6): 함정 대응 상태가 없는 유닛이 알려진 활성 함정의 인접 1칸(3×3) 안에 있으면 구역 밖의 걸을 수 있는 비점유 타일로 나온다.
	// 이동은 A*가 맡는다 — 구역 안에서 출발한 탐색은 구역 타일을 막지 않고 큰 비용만 물려 가장 적게 밟고 나오게 한다(TrapMoveContext). 구역 밖으로 나오면
	// NeedsZoneEscape가 거짓이 돼 Failure로 다음 행동(경계·조사·대기 등)이 이어진다. 나갈 곳이 없거나 정체되면 잠시 탈출 시도를 꺼(trapZoneEscapeSuppressUntil)
	// Tactical에 얼어붙지 않는다.
	private static BTStatus ZoneEscape(Unit unit)
	{
		if (!(unit is Human human) || !TrapAvoidance.NeedsZoneEscape(human))
		{
			unit.trapEscapeStuckTurns = 0;
			return BTStatus.Failure;
		}

		if (TrapAvoidance.TryFindEscapeTile(human, out Vector2Int exit))
		{
			Vector2Int before = human.position;
			AIMovementHelper.MoveTowardsPos(human, exit);
			if (human.position != before)
			{
				human.trapEscapeStuckTurns = 0;
				return BTStatus.Running;
			}
		}

		if (++human.trapEscapeStuckTurns >= (AIConfigLoader.Behavior?.trapMoveStuckTurnLimit ?? 4))
		{
			human.trapEscapeStuckTurns = 0;
			human.trapZoneEscapeSuppressUntil = Time.time + TrapAvoidance.EscapeSuppressSeconds;
			return BTStatus.Failure;
		}
		return BTStatus.Running;
	}

	// ── 조사 ──────────────────────────────────────────────────────

	private static BTStatus MoveToInvestigateTarget(Unit unit)
	{
		var human = (Human)unit;
		if (human.currentInvestigation == null)
		{
			human.currentInvestigation = human.FindInvestigateTarget();
			if (human.currentInvestigation == null) return BTStatus.Failure;
		}
		var inv = human.currentInvestigation;

		// 00-07(정보 격리): 아직 도착하지 않은 동안은 전역 objectGrid를 직접 읽지 않는다 — 다른 유닛이
		// 수거한 사실을 직접 확인(도착)하거나 전파로 전달받기 전엔 원거리에서 알지 못한다(타일 전용
		// 후보는 "수거" 개념이 없어 해당 없음).
		if (!inv.IsTileOnly && human.personalMap.IsKnownCollected(inv.TargetObjectId))
		{
			human.currentInvestigation = null;
			return BTStatus.Failure;
		}

		var pos = new Vector2Int(inv.TargetPosition.x, inv.TargetPosition.y);
		// 오브젝트는 자신의 타일을 점유하므로 정확 일치 대신 Chebyshev ≤ 1로 도달 판정
		if (AIMovementHelper.IsAdjacent(human.position, pos))
		{
			human.investigateStuckTurns = 0;
			// 01번 문서 4장: 맨 타일 후보는 별도 "조사·줍기" 단계가 없다 — 인지·안전 확인은
			// PersonalMapKnowledge의 기존 시간 경과 감쇠가 도착 여부와 무관하게 담당하므로, 도착한
			// 순간 그대로 완료 처리한다(정리는 PickUpObject 쪽에서 한 곳으로 통일).
			if (inv.IsTileOnly) return BTStatus.Success;
			// 도착 — 이제부터는 실제 objectGrid 조회가 "직접 확인"이므로 그대로 신뢰한다.
			if (!human.Session.objectGrid.TryGetValue(inv.TargetPosition, out var obj) || obj.IsCollected)
			{
				human.personalMap.OnObjectCollected(inv.TargetObjectId); // 직접 확인한 결과를 스스로도 기록
				human.currentInvestigation = null;
				return BTStatus.Failure;
			}
			return BTStatus.Success;
		}

		// 검증문서 01-11 6행: 대상이 진짜 도달 불가능하면(우회도 실패) 포기하고 경계로 전환한다 —
		// 그대로 두면 영원히 같은 목표를 붙잡는다(04번 문서 9번 항목). MoveToCoreAttack과 동일 관례:
		// 리셋 기준은 "이동 성공 여부"가 아니라 "체비셰프 거리가 실제로 줄었는지"다 — 혼잡 구역의
		// 제자리 셔플도 Move() 관점에선 성공이라 그것만 보면 stuckTurns가 절대 쌓이지 않는다.
		int distBefore = AIMovementHelper.ChebyshevDistance(human.position, pos);
		AIMovementHelper.MoveTowardsPos(human, pos);
		if (AIMovementHelper.ChebyshevDistance(human.position, pos) < distBefore)
		{
			human.investigateStuckTurns = 0;
			return BTStatus.Running;
		}

		// 완전히 막히면(A*가 한 걸음도 못 감) 근처 빈 칸으로 우회 시도한다 — MoveToTrap과 동일한 관례.
		// FindNearbyOpenTile은 목표가 멀면 도달 가능성과 무관하게 뭔가를 찾아버려 stuckTurns가 계속
		// 리셋될 수 있다 — 이미 근접(반경 2)했을 때만 쓴다(MoveToCoreAttack과 동일 관례).
		Vector2Int fallback = AIMovementHelper.IsAdjacent(human.position, pos, radius: 2)
			? AIMovementHelper.FindNearbyOpenTile(human, pos)
			: pos;
		if (fallback != pos)
		{
			AIMovementHelper.MoveTowardsPos(human, fallback);
			if (AIMovementHelper.ChebyshevDistance(human.position, pos) < distBefore)
			{
				human.investigateStuckTurns = 0;
				return BTStatus.Running;
			}
		}

		// 다른 유닛이 잠깐 몰려 막힌 것뿐이면 몇 틱 인내하며 재시도하고, 벽/닫힌 문으로 사방이 진짜
		// 막혀 있으면 즉시 포기한다 — 포기 시 이 목표를 놓아줘야 다음 틱 FindInvestigateTarget이
		// 다른 후보를 고르거나(후보가 없으면 NavigationFSMState 프론티어 탐색으로 자연히 폴백).
		if (AIMovementHelper.HasAnyStructurallyOpenAdjacentTile(human))
		{
			human.investigateStuckTurns++;
			int limit = AIConfigLoader.Behavior?.investigateStuckTurnLimit ?? 4;
			if (human.investigateStuckTurns >= limit)
			{
				human.investigateStuckTurns = 0;
				human.currentInvestigation = null;
				human.currentAlertSearch = new AlertSearchState();
				return BTStatus.Failure;
			}
			return BTStatus.Running;
		}

		human.investigateStuckTurns = 0;
		human.currentInvestigation = null;
		human.currentAlertSearch = new AlertSearchState();
		return BTStatus.Failure;
	}

	private static BTStatus InvestigatePerform(Unit unit)
	{
		var human = (Human)unit;
		var inv   = human.currentInvestigation;
		if (inv == null) return BTStatus.Failure;
		// 타일 전용 후보는 여기서 할 일이 없다 — MoveToInvestigateTarget이 이미 도착 시점에 처리한다.
		if (inv.IsTileOnly) return BTStatus.Success;
		// MoveToInvestigateTarget이 도착(인접)을 보장한 뒤에만 이 분기가 실행되므로 여기 objectGrid
		// 조회는 "직접 확인"이다 — 원거리 조회 문제는 위 MoveToInvestigateTarget 쪽만 해당.
		if (!human.Session.objectGrid.TryGetValue(inv.TargetPosition, out var obj) || obj.IsCollected)
		{
			human.personalMap.OnObjectCollected(inv.TargetObjectId);
			human.currentInvestigation = null;
			return BTStatus.Success;
		}
		// 이미 조사 완료 → PickUpObject로 넘어간다
		if (obj.IsInvestigated) return BTStatus.Success;

		// 07문서 10장: 조사가 실제로 시작되는 시점에 "진행 중" 정보를 1회 전파(보호 포메이션 참여 자격).
		if (!inv.PenaltyActive) PropagationSystem.NotifyInteractionStarted(human);
		// 검증문서 03-02(03번 문서 3번 항목): 파티 목표 오브젝트면 같은 시점에 "합류 정보"도 전파한다.
		// 자유로운 파티원은 personalMap 등록만으로 기존 FindInvestigateTarget이 자연히 합류 이동을
		// 시작하고, 전투·도주·다른 상호작용·집결 중인 파티원은 CanInvestigate의 기존 우선순위 게이트에
		// 막혀 그대로 현재 행동을 유지한다 — "유지해야 하는 수신자" 처리를 새 강제 상태 없이 재현한다.
		if (!inv.PenaltyActive && inv.IsPartyGoalTarget) PropagationSystem.NotifyPartyGoalInteractionStarted(human, obj);
		inv.PenaltyActive = true;
		if (inv.Progress01 < 1f) return BTStatus.Running;

		obj.IsInvestigated = true;
		human.personalMap.OnObjectInvestigated(obj.Id);
		// 00-03/00-07(정보 격리): 조사 완료 정보도 위 NotifyInteractionStarted와 동일하게 일반 전파
		// 조건(CanPropagate)을 거쳐야 한다(TrapPartySystem.OnTrapDiscovered와 동일한 게이트).
		if (human.party != null)
		{
			// 01-09 2번: 이 순간 범위 밖(CanPropagate 실패)이었던 파티원도 나중에
			// PropagationSystem.TickOngoingObjectPropagation이 채워줄 수 있도록 등록.
			human.party.KnownInvestigatedObjects[obj.Id] = obj.Position;
			foreach (var m in human.party.Members)
			{
				if (m == null || m == human || m.hp <= 0) continue;
				if (!PropagationSystem.CanPropagate(human, m)) continue;
				if (!m.personalMap.IsObjectKnown(obj.Id))
					m.personalMap.RegisterObject(obj.Id, obj.Position, obj.BaseDanger, obj.BaseInterest, obj.Tags, obj.CauserStage);
				m.personalMap.OnObjectInvestigated(obj.Id);
			}
		}

		// 5-2장: 파티원 시체 조사로 사망 원인(간접) 확인.
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
		// 타일 전용 후보는 여기서 정리하고 끝낸다 — 안전 확인은 시간 경과로 자연히 처리되므로 "줍기"
		// 단계 자체가 없다.
		if (inv.IsTileOnly)
		{
			human.currentInvestigation = null;
			human.currentAlertSearch = null;
			return BTStatus.Success;
		}
		// MoveToInvestigateTarget이 도착(인접)을 보장한 뒤에만 실행되므로 여기 objectGrid 조회도
		// "직접 확인"이다.
		if (!human.Session.objectGrid.TryGetValue(inv.TargetPosition, out var obj) || obj.IsCollected)
		{
			human.personalMap.OnObjectCollected(inv.TargetObjectId);
			human.currentInvestigation = null;
			human.currentAlertSearch = null; // 03문서 4-5장: 낡은 경계 상태 잔재 정리
			return BTStatus.Success;
		}

		bool isLoot = obj.Tags.Exists(t => t.Contains("Loot"));
		if (isLoot)
		{
			human.Session.CollectObject(inv.TargetPosition);
			human.collectedObjects.Add(obj.Id);
			human.personalMap.OnObjectCollected(obj.Id); // 방금 직접 수거 — 스스로도 기록
			// 00-07(정보 격리): 수거 사실도 조사 완료 전파(위 InvestigatePerform)와 동일하게 일반 전파
			// 조건을 거쳐야 다른 파티원이 안다 — 범위 밖 파티원은 이 순간 모른다.
			if (human.party != null)
			{
				// 01-09 2번: 위 InvestigatePerform과 동일하게, 이 순간 놓친 파티원도
				// TickOngoingObjectPropagation이 나중에 채워줄 수 있도록 등록.
				human.party.KnownCollectedObjectIds.Add(obj.Id);
				foreach (var m in human.party.Members)
				{
					if (m == null || m == human || m.hp <= 0) continue;
					if (!PropagationSystem.CanPropagate(human, m)) continue;
					m.personalMap.OnObjectCollected(obj.Id);
				}
			}
			// 루팅 완료 → 탈출 시도. 06 문서(도주·후퇴) 미작성이므로 스텁:
			// pendingStairTargetFloor를 위 층으로 세팅해 기존 Stairs 브랜치가 계단 이동을 처리한다.
			if (!human.pendingStairTargetFloor.HasValue)
				human.pendingStairTargetFloor = human.currentFloor + 1;
		}
		human.currentInvestigation = null;
		human.currentAlertSearch = null; // 03문서 4-5장: 낡은 경계 상태 잔재 정리
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
			// 집결지는 좌표뿐이라 층을 건넌 유닛이 다른 층의 같은 좌표로 걸어가지 않게 한다 — 대기를 접고 집결 완료 판정에서 빠진다.
			if (wait.WaitFloor >= 0 && human.currentFloor != wait.WaitFloor)
			{
				human.currentWait = null;
				human.waitStuckTurns = 0;
				if (human.party != null) human.party.CheckRallyComplete();
				return BTStatus.Success;
			}

			// 집결지 근처 개별 자리로 이동·대기 — 도착해도 대기를 유지하고 전원이 모이면 Party.CheckRallyComplete가 한꺼번에 푼다(rallyGatherNearbyEnabled).
			if (AIConfigLoader.Behavior?.rallyGatherNearbyEnabled ?? true)
				return PartyAdvanceSteps.StepRallyGather(human, wait);

			// 아래는 rallyGatherNearbyEnabled를 끈 예전 방식 — 전원이 집결지 하나로 향하고, 도착하거나 오래 막히면 스스로 대기를 푼다.
			if (Vector2Int.Distance(human.position, wait.WaitPosition.Value) <= 1.5f)
			{
				human.currentWait = null;
				human.waitStuckTurns = 0;
				if (human.party != null) human.party.CheckRallyComplete();
				return BTStatus.Success;
			}

			// 집결지가 아군에게 막혀 MoveTowardsPos가 계속 실패하는 경우 — 몇 틱을 기다려도 안 풀리면
			// "집결 완료"와 동일하게 처리해 파티 전체가 계속 진행하게 한다.
			int distBefore = AIMovementHelper.ChebyshevDistance(human.position, wait.WaitPosition.Value);
			AIMovementHelper.MoveTowardsPos(human, wait.WaitPosition.Value);
			if (AIMovementHelper.ChebyshevDistance(human.position, wait.WaitPosition.Value) < distBefore)
			{
				human.waitStuckTurns = 0;
			}
			else if (++human.waitStuckTurns >= (AIConfigLoader.Behavior?.waitStuckTurnLimit ?? 4))
			{
				human.waitStuckTurns = 0;
				human.currentWait = null;
				if (human.party != null) human.party.CheckRallyComplete();
				return BTStatus.Success;
			}
		}
		else if (wait.Reason == WaitReason.ApproachingNextDoor && wait.WaitPosition.HasValue)
		{
			// 01번 7-1장 소탕 파티: 다음 이동 문 앞 자리까지 이동하며 확인한다. 도착하면 집결 판단 자격이 생긴다(Party.MarkLeaderReachedNextDoor).
			// 자리가 막혀 더 다가갈 수 없어도(정체 한도 초과) 도착으로 처리한다 — 못 가는 자리 때문에 집결 판단이 영영 막히면 안 된다.
			bool arrivedAtDoorSlot = AIMovementHelper.IsAdjacent(human.position, wait.WaitPosition.Value);
			bool gaveUp = false;
			if (!arrivedAtDoorSlot)
			{
				int distBefore = AIMovementHelper.ChebyshevDistance(human.position, wait.WaitPosition.Value);
				AIMovementHelper.MoveTowardsPos(human, wait.WaitPosition.Value);
				if (AIMovementHelper.ChebyshevDistance(human.position, wait.WaitPosition.Value) < distBefore)
					human.waitStuckTurns = 0;
				else if (++human.waitStuckTurns >= (AIConfigLoader.Behavior?.waitStuckTurnLimit ?? 4))
					gaveUp = true;
			}
			if (arrivedAtDoorSlot || gaveUp)
			{
				human.waitStuckTurns = 0;
				human.currentWait = null;
				if (human.party != null && human.party.Leader == human) // 접근 도중 리더가 바뀌었다면 표식은 새 리더가 스스로 만든다
				{
					human.party.MarkLeaderReachedNextDoor();
					LogHelper.Log(LogHelper.GAME, $"[파티] {human.name}: 다음 이동 문 앞 {(arrivedAtDoorSlot ? "도착" : "접근 한계")} — 문까지 이동하며 확인을 마쳐 집결 판단 가능");
				}
				return BTStatus.Success;
			}
		}
		else if (wait.Reason == WaitReason.AdvancingToNextRoom && wait.WaitPosition.HasValue)
		{
			// 05번 1장: 문 앞 자리에 도착하면 개인 행동으로 넘긴다 — 적대 문 파괴는 TacticalBehaviorType.DoorAttack(인류 전용) 등 기존 전술이 이어받는다.
			// 길이 막힌 것만으로는 명령을 풀지 않는다(검증문서 04-02) — StepAdvanceToDoor가 인내·다른 자리·보류를 처리한다.
			if (StepAdvanceToDoor(human, wait) == BTStatus.Success) return BTStatus.Success;
		}
		else if (PartyAdvanceSystem.IsPlanWait(wait.Reason))
		{
			// 집결 뒤 문 앞 진형 → 리더 지시 문 파괴 → 랭크 순 입장(PartyAdvanceSystem). 단계 전이는 파티 쪽 Tick이 하고, 여기서는 유닛 자기 몫(자리 이동·채널링·입장)만 수행한다.
			if (PartyAdvanceSteps.StepMember(human, wait) == BTStatus.Success) return BTStatus.Success;
		}
		else if (wait.Reason == WaitReason.ReportingCoreToLeader && wait.CorePosition.HasValue)
		{
			// 검증문서 03-15: 목적지는 이 유닛이 "아는" 리더 위치·집결 위치·발견한 문·미탐색 지형으로만 정한다(실제 리더
			// 위치를 읽지 않는다). 전달·목적지 결정·부재 확인·접근 실패 처리는 전부 PartyCoreReportSystem이 담당하고,
			// 종료 경로는 모두 집결 완료를 다시 확인한다(03-14). 보고 의무는 종료돼도 남을 수 있다(TickPendingReport).
			if (PartyCoreReportSystem.StepReportMovement(human, wait) == ReportStep.Ended)
				return BTStatus.Success;
		}
		else if (wait.Reason == WaitReason.SearchingNextDoor)
		{
			// 05번 1장 73줄: 다음 문을 아직 모르면 진형을 유지하며 문을 찾는다 — 아는 리더 위치 주변을 따라다닌다.
			if (PartyDoorSearchSystem.StepFollow(human, wait) == ReportStep.Ended)
				return BTStatus.Success;
		}
		else if (wait.Reason == WaitReason.Retreating && wait.WaitPosition.HasValue)
		{
			// 05번 문서 10장: 탈출 지점 도착(또는 더 다가갈 수 없음)하면 대기를 해제한다. "탈출 완료로
			// 웨이브 생존자 집계에 반영"하는 처리는 아직 없다(구현현황 문서 참고 — 범위 밖).
			if (AIMovementHelper.IsAdjacent(human.position, wait.WaitPosition.Value)
				|| !AIMovementHelper.MoveTowardsPos(human, wait.WaitPosition.Value))
			{
				human.currentWait = null;
				return BTStatus.Success;
			}
		}
		return BTStatus.Running;
	}

	// 방 이동(AdvancingToNextRoom) 한 틱 — 검증문서 04-02(04번 1장 "개인 경로가 막힌 경우", 03번 13장): 집결·방 이동은 길이 막혔다는 이유만으로 풀어 개인 행동(다른 조사 목표 선택 포함)으로
	// 이탈하지 않는다. 막히면 ① waitStuckTurnLimit 틱 인내 ② 같은 문의 다른 자리를 다시 고르고 ③ 한 걸음도 못 다가가면 풀지 않고 제자리에서 기다렸다 doorApproachHoldRetrySeconds마다 다시
	// 시도한다. 닿을 수 없는 자리에 영구히 얼어붙지 않게 doorApproachMaxBlockedSeconds 넘게 막혀 있을 때만 안전장치로 푼다. Success = 명령 종료(도착·안전장치), Running = 계속.
	private static BTStatus StepAdvanceToDoor(Human human, WaitState wait)
	{
		Vector2Int slot = wait.WaitPosition.Value;
		var cfg = AIConfigLoader.Behavior;

		if (AIMovementHelper.IsAdjacent(human.position, slot))
		{
			LogHelper.Log(LogHelper.GAME, $"[파티] {human.name}: 방 이동 명령 종료 — 문 앞 자리 도착, 이후 개인 행동으로 복귀");
			human.currentWait = null;
			human.waitStuckTurns = 0;
			return BTStatus.Success;
		}

		float now = Time.time;
		if (now < wait.NextDoorScanTime) return BTStatus.Running; // 막혀 보류 중 — 다음 재시도까지 제자리에서 기다린다

		int distBefore = AIMovementHelper.ChebyshevDistance(human.position, slot);
		bool moved = AIMovementHelper.MoveTowardsPos(human, slot);
		if (AIMovementHelper.ChebyshevDistance(human.position, slot) < distBefore)
		{
			human.waitStuckTurns = 0;
			wait.BlockedSince = -1f;
			return BTStatus.Running;
		}

		// 거리가 줄지 않았다 — 혼잡·우회 구간일 수 있어 한도까지는 인내한다(집결·조사 이동과 같은 기준).
		if (++human.waitStuckTurns < (cfg?.waitStuckTurnLimit ?? 4)) return BTStatus.Running;
		human.waitStuckTurns = 0;

		bool newlyBlocked = wait.BlockedSince < 0f;
		if (newlyBlocked) wait.BlockedSince = now;
		else if (now - wait.BlockedSince >= (cfg?.doorApproachMaxBlockedSeconds ?? 30f))
		{
			LogHelper.Log(LogHelper.GAME, $"[파티] {human.name}: 방 이동 명령 종료 — 오래 막혀 안전장치로 해제, 이후 개인 행동으로 복귀");
			human.currentWait = null;
			wait.BlockedSince = -1f;
			return BTStatus.Success;
		}

		// 같은 문의 다른 자리를 다시 고른다 — 다른 파티원의 자리와 지금 막힌 자리는 제외한다. 고를 자리가 없으면(문 타일로 폴백) 그대로 둔다.
		if (wait.DoorPosition.HasValue)
		{
			var claimed = new HashSet<Vector2Int> { slot };
			if (human.party != null)
				foreach (var m in human.party.Members)
					if (m != null && m != human && m.hp > 0 && m.currentWait != null
						&& m.currentWait.Reason == WaitReason.AdvancingToNextRoom && m.currentWait.WaitPosition.HasValue)
						claimed.Add(m.currentWait.WaitPosition.Value);

			Vector2Int alternate = AIMovementHelper.FindDoorWaitSlot(human, wait.DoorPosition.Value, claimed);
			if (alternate != wait.DoorPosition.Value && alternate != slot)
			{
				// 로그는 막히기 시작한 첫 재선택에만 남긴다(같은 막힘에서 자리를 여러 번 바꿔도 콘솔을 뒤덮지 않게).
				if (newlyBlocked)
					LogHelper.Log(LogHelper.GAME, $"[파티] {human.name}: 방 이동 자리 ({slot.x},{slot.y})가 막혀 다른 문 앞 자리 ({alternate.x},{alternate.y})로 바꿉니다");
				wait.WaitPosition = alternate;
			}
		}

		// 한 걸음도 못 움직였다면(완전히 막힘) 풀지 않고 잠시 기다렸다 다시 시도한다. 움직이고는 있었다면(우회·혼잡 셔플) 곧바로 이어 간다.
		if (!moved) wait.NextDoorScanTime = now + (cfg?.doorApproachHoldRetrySeconds ?? 1f);
		return BTStatus.Running;
	}

	// ── 경계 ──────────────────────────────────────────────────────

	private static BTStatus AlertApproach(Unit unit)
	{
		var alert = unit.currentAlertSearch;
		if (alert == null || !alert.TargetPosition.HasValue) return BTStatus.Failure;
		if (alert.IsIndirectEnemyApproach) return IndirectEnemyApproach(unit, alert);
		Vector2Int target = alert.TargetPosition.Value;
		unit.currentDir = SkillAction.GetDirection8(target - unit.position);
		if (Vector2Int.Distance(unit.position, target) <= 1.5f)
		{
			// 소리 반응은 이 분기가 아니라 자기 유지 시간(이동음 2초·추정 지역 2초)을 쓴다 — 3초를 덧붙이지 않는다(03번 332~334줄).
			if (alert.IsSoundResponse)
			{
				unit.currentAlertSearch = null;
				return BTStatus.Success;
			}

			// 검증문서 03-16: 마지막 위치에 도착했는데 대상이 없다(정확 인지했다면 Combat이 이 분기를 이미 선점했다) —
			// 3초 부재 확인 대기. 이 대기는 경계의 수색 시간(ElapsedSeconds) 안에 포함되므로 그 값은 건드리지 않는다.
			if (alert.AbsenceWaitStartTime < 0f) alert.AbsenceWaitStartTime = Time.time;
			switch (ExplorationMath.ResolveAlertArrival(alert.AbsenceWaitStartTime, Time.time, alert.IsAttackDirectionSearch))
			{
				case ExplorationMath.AlertArrivalResult.Waiting:
					return BTStatus.Running;
				case ExplorationMath.AlertArrivalResult.ContinueSearching:
					// 이 위치는 비어 있다 — 같은 위치를 다시 고르지 않고 남은 수색 기한 동안 정면 수색(AlertPerimeterSearch)을 이어 간다.
					// 기한은 UnitFunction.OnUpdate의 15초 워치독이 종료한다.
					alert.TargetPosition = null;
					return BTStatus.Running;
				default:
					unit.currentAlertSearch = null;
					return BTStatus.Success;
			}
		}

		// 부재 확인 대기 중 밀려나 도착 위치를 벗어났다면 3초는 다시 도착한 시점부터 센다.
		alert.AbsenceWaitStartTime = -1f;
		AIMovementHelper.MoveTowardsPos(unit, target);
		return BTStatus.Running;
	}

	// 03번 v0.6 4-7(간접 인지 후 접근): 전파받은 적 위치로 일반 이동속도로 접근한다. 적을 정확 인지하면 Combat이 이 분기를 선점하고
	// (OnEnter가 경계를 비움), 기록 위치 주위 3칸까지 들어왔는데 적이 없으면 그 정보를 갱신(폐기)하고 일반 탐색으로 복귀한다.
	// 공격 방향 수색·수상한 타일의 3초 부재 대기·15초 수색 기한은 적용하지 않는다.
	private static BTStatus IndirectEnemyApproach(Unit unit, AlertSearchState alert)
	{
		Unit enemy = alert.IndirectEnemy;
		if (enemy == null || enemy.hp <= 0 || !alert.TargetPosition.HasValue)
		{
			EndIndirectEnemyApproach(unit, alert);
			return BTStatus.Success;
		}

		Vector2Int target = alert.TargetPosition.Value;
		unit.currentDir = SkillAction.GetDirection8(target - unit.position);
		if (ExplorationMath.IsWithinIndirectCheckRadius(Vector2Int.Distance(unit.position, target)))
		{
			EndIndirectEnemyApproach(unit, alert);
			return BTStatus.Success;
		}

		int distBefore = AIMovementHelper.ChebyshevDistance(unit.position, target);
		AIMovementHelper.MoveTowardsPos(unit, target);
		if (AIMovementHelper.ChebyshevDistance(unit.position, target) < distBefore)
		{
			alert.ApproachStuckTurns = 0;
		}
		else if (++alert.ApproachStuckTurns >= (AIConfigLoader.Behavior?.indirectEnemyApproachStuckTurns ?? 4))
		{
			EndIndirectEnemyApproach(unit, alert); // 길이 막혀 접근할 수 없다 — 이 정보로 다시 접근하지 않는다.
			return BTStatus.Success;
		}
		return BTStatus.Running;
	}

	private static void EndIndirectEnemyApproach(Unit unit, AlertSearchState alert)
	{
		if (alert.IndirectEnemy != null) unit.Propagation.PropagatedInfo.Remove(alert.IndirectEnemy);
		unit.currentAlertSearch = null;
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

	// 07문서 8-2장: 추정 지역형 소리 반응 — '?' 표시 → 추정 지역 중심이 인지 범위 안에 들어오는
	// 거리까지 접근 → 인지 판정 1회 → 원인 미확인이면 2초 유지 후 종료. 도중에 정확 인지하면
	// personalSpottedEnemies 등 다른 경로가 다음 틱에 우선권을 가져간다.
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
			// E_HIT_HEAVY_INDIRECT 연결: 인지 판정 1회가 이뤄지는 이 시점에 "원인을 정확 인지했는지"
			// 확인한다 — 조건 미충족이면 TryConfirmIndirectHit 내부 게이팅이 조용히 무시한다.
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
		// PendingSound는 Propagation(base Unit 소유)이라 Human 게이팅 없이 정리한다 — Human 전용
		// 게이팅을 두면 몬스터의 PendingSound가 영영 안 지워져 새 소리를 못 받는 상태로 고착된다.
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
				// TryGetNextStep이 방향을 승인해도 currentDir을 먼저 찍으면 코너 커팅 등 실패 시
				// 방향만 계속 바뀌어 제자리에서 홱홱 도는 버그가 생긴다 — 실제로 위치가 바뀐 경우에만
				// 멈추고, 실패하면 다음 후보 방향을 계속 시도한다.
				Vector2Int before = unit.position;
				unit.Move(nextDir);
				if (unit.position != before) return BTStatus.Running;
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

	// 03문서 6-6/6-7장 — 같은 대상을 호위 중인 다른 파티원들과 위치(전방/후방/측면) 기준으로 좌우
	// 정렬해 인원수별 감시 방향을 표대로 배정하고, 벽으로 절반 이상 막히면 인접 유효 방향으로
	// 재배정한다. 상호작용 유닛 본인의 시야는 조사/함정 코드가 이미 처리하므로 여긴 다른 호위 유닛만.
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

	// ── 코어 공격 — 코어는 각 층 보스방에만 있고, 체력이 0이 되면 막타친 유닛의 진영으로 방 소유권이
	// 즉시 전환된다(코어 자체는 반피로 회복돼 사라지지 않음).
	// ⚠️ 하드 룰: 자동 오브젝트 공격(코어/문)은 인류 전용 — 플레이어 몬스터는 절대 스스로 시도하지 않고
	// 항상 PlayerCommandFSMState.ExecutePlayerAttackObject(우클릭 명령)를 거쳐야 한다. DoorAttack도 동일.
	private static bool HasCoreAttackTarget(Unit unit)
	{
		return FindHostileRoomCore(unit, out _, out _);
	}

	// unit이 지금 서 있는 방의 코어가 "공격 대상"인지 확인한다 — 인류가 아니면 항상 대상 아님.
	// 그 외엔 방이 이미 내 진영 소유이거나 코어 정보가 없거나 HP가 0이면 대상이 아니다.
	internal static bool FindHostileRoomCore(Unit unit, out Room room, out InteractableObject core)
	{
		room = null;
		core = null;
		if (!(unit is Human)) return false; // 인류 전용 — 플레이어 몬스터는 자동으로 코어를 공격하지 않는다.
		if (unit.Session?.cmap == null) return false;

		Vector3Int gridPos = new Vector3Int(unit.position.x, unit.position.y, unit.currentFloor);
		if (!unit.Session.roomGrid.TryGetValue(gridPos, out room) || room == null) return false;
		if (room.CoreObjectId == null) return false;
		if (!IsRoomCoreStillHostile(unit, room.CorePosition)) return false;
		unit.Session.objectGrid.TryGetValue(room.CorePosition, out core);
		return true;
	}

	// unit 진영 기준으로 corePos의 코어가 여전히 적대적인지 확인 — FindHostileRoomCore와 달리 종족
	// 제한이 없다. 여기 인류 게이트를 걸면 플레이어 몬스터의 수동 코어 공격 명령까지 매 프레임 취소된다.
	internal static bool IsRoomCoreStillHostile(Unit unit, Vector3Int corePos)
	{
		if (unit.Session?.cmap == null) return false;
		FactionType? myFaction = OffenseProcessor.MapToRoomFaction(unit.FactionBehavior);
		if (myFaction == null) return false; // 방 소유권 개념이 없는 진영(매핑 불가)
		if (!unit.Session.roomGrid.TryGetValue(corePos, out Room room) || room == null) return false;
		if (room.RoomFaction == myFaction.Value) return false; // 이미 내 진영 소유
		if (!unit.Session.objectGrid.TryGetValue(corePos, out InteractableObject core) || core.CoreHp <= 0f) return false;
		return true;
	}

	private static BTStatus MoveToCoreAttack(Unit unit)
	{
		if (!FindHostileRoomCore(unit, out Room room, out InteractableObject core))
		{
			unit.ClearAttackObjectTarget();
			unit.tacticalObjectAttackStuckTurns = 0;
			return BTStatus.Failure;
		}

		Vector2Int pos = new Vector2Int(room.CorePosition.x, room.CorePosition.y);

		// 코어는 Passable이라 유닛이 그 타일 위까지 걸어가 설 수 있다 — MoveToCore(구 코어 조사)와
		// 동일하게, 정확히 그 타일에 서 있으면 인접 빈 칸으로 한 걸음 물러난 뒤에만 "도착"으로 인정한다.
		if (unit.position == pos)
		{
			StepOffObjectTile(unit, pos);
			unit.tacticalObjectAttackStuckTurns = 0;
			return BTStatus.Running;
		}

		if (AIMovementHelper.IsAdjacent(unit.position, pos))
		{
			unit.SetAttackObjectTarget(room.CorePosition);
			unit.tacticalObjectAttackStuckTurns = 0;
			return BTStatus.Success;
		}

		unit.ClearAttackObjectTarget();
		// 리셋 기준은 "이동 성공 여부"가 아니라 "체비셰프 거리가 실제로 줄었는지"다 — 혼잡 구역의
		// 제자리 셔플도 Move() 관점에선 성공이라 그것만 보면 stuckTurns가 절대 쌓이지 않는다.
		int distBefore = AIMovementHelper.ChebyshevDistance(unit.position, pos);
		AIMovementHelper.MoveTowardsPos(unit, pos);
		if (AIMovementHelper.ChebyshevDistance(unit.position, pos) < distBefore)
		{
			unit.tacticalObjectAttackStuckTurns = 0;
			return BTStatus.Running;
		}

		// FindNearbyOpenTile은 목표가 멀면 도달 가능성과 무관하게 뭔가를 찾아버려 stuckTurns가 계속
		// 리셋될 수 있다 — 이미 근접(반경 2)했을 때만 쓰고, 아니면 스킵해서 혼잡/완전차단 판정으로 넘어간다.
		Vector2Int fallback = AIMovementHelper.IsAdjacent(unit.position, pos, radius: 2)
			? AIMovementHelper.FindNearbyOpenTile(unit, pos)
			: pos;
		if (fallback != pos)
		{
			AIMovementHelper.MoveTowardsPos(unit, fallback);
			if (AIMovementHelper.ChebyshevDistance(unit.position, pos) < distBefore)
			{
				unit.tacticalObjectAttackStuckTurns = 0;
				return BTStatus.Running;
			}
		}

		// 지형상 갈 곳이 아예 없는지 확인 — 다른 유닛이 잠깐 몰려 막힌 것뿐이면 몇 틱 인내하며
		// 재시도하고, 벽/닫힌 문으로 사방이 진짜 막혀 있으면 즉시 포기하고 경계 상태로 전환한다.
		if (AIMovementHelper.HasAnyStructurallyOpenAdjacentTile(unit))
		{
			unit.tacticalObjectAttackStuckTurns++;
			int limit = AIConfigLoader.Behavior?.tacticalObjectAttackStuckTurnLimit ?? 4;
			if (unit.tacticalObjectAttackStuckTurns >= limit)
			{
				unit.tacticalObjectAttackStuckTurns = 0;
				unit.currentAlertSearch = new AlertSearchState();
				return BTStatus.Failure;
			}
			return BTStatus.Running;
		}

		unit.tacticalObjectAttackStuckTurns = 0;
		unit.currentAlertSearch = new AlertSearchState();
		return BTStatus.Failure;
	}

	// 오브젝트 자신의 타일에 정확히 서 있을 때 인접한 이동 가능 타일로 한 걸음 물러난다.
	private static void StepOffObjectTile(Unit unit, Vector2Int objectPos)
	{
		for (int i = 0; i < 8; i++)
		{
			Vector2Int candidate = objectPos + unit.GetDirVector((Dir)i);
			if (unit.CanMove(candidate))
			{
				AIMovementHelper.MoveTowardsPos(unit, candidate);
				return;
			}
		}
	}

	// 실제 데미지 적용은 UnitFunction.OnUpdate가 currentAttackObjectTarget을 보고 매 프레임 처리한다
	// (TrapPhase.Destroying과 동일한 채널링 패턴) — 여기서는 도착 유지/파괴 완료만 확인한다.
	private static BTStatus CoreAttackPerform(Unit unit)
	{
		if (!unit.currentAttackObjectTarget.HasValue) return BTStatus.Failure;
		Vector3Int corePos = unit.currentAttackObjectTarget.Value;

		if (unit.Session == null || !unit.Session.objectGrid.TryGetValue(corePos, out var obj))
		{
			unit.ClearAttackObjectTarget();
			return BTStatus.Success;
		}

		// 코어는 문과 달리 파괴돼도 사라지지 않고 즉시 반피로 회복된다 — obj.CoreHp > 0f만 보면 동료가
		// 이미 점령을 끝낸 뒤에도 나머지 공격자들이 채널링을 무한히 이어가므로, FindHostileRoomCore로
		// "지금도 적대적인 코어인지"를 매 틱 재검증한다.
		if (!FindHostileRoomCore(unit, out _, out _))
		{
			unit.ClearAttackObjectTarget();
			return BTStatus.Success;
		}

		return BTStatus.Running;
	}

	// ── 문 공격(인류 전용) — CoreAttack과 반대로, 이미 점령한 방에서 그 방 경계 게이트 중 아직 다른
	// 진영 소유인 문을 찾아 부순다. 대상이 없어지면 자연히 false가 되어 BT가 다음 우선순위(조사/탐험
	// 등)로 넘어가므로 별도의 "탐험 재개" 코드가 필요 없다.
	private static bool HasDoorAttackTarget(Unit unit)
	{
		return FindHostileExitDoor(unit, out _, out _);
	}

	// unit이 서 있는 방이 이미 인류 소유이고, 게이트 문턱 타일 중 파괴되지 않았고 인류 소유가 아닌
	// 문이 있으면 그 위치를 돌려준다(인류가 아닌 유닛엔 적용 안 됨). 가까운 문부터 공격 — GetGateDoorTiles의
	// 배열 순서(청크 기준일 뿐 실제 거리와 무관)를 믿지 않고 후보를 전부 모아 체비셰프 거리가 가장 가까운 것만 고른다.
	private static bool FindHostileExitDoor(Unit unit, out Vector3Int doorPos, out InteractableObject door)
	{
		doorPos = default;
		door = null;
		if (!(unit is Human)) return false; // 인류 전용
		if (unit.Session?.cmap == null) return false;

		Vector3Int gridPos = new Vector3Int(unit.position.x, unit.position.y, unit.currentFloor);
		if (!unit.Session.roomGrid.TryGetValue(gridPos, out Room room) || room == null) return false;
		if (room.RoomFaction != FactionType.Human) return false; // "방을 점령하고 난 다음"
		if (room.RoomId < 0 || room.Floor < 0 || room.Floor >= unit.Session.cmap.map.floors.Length) return false;

		Floor floor = unit.Session.cmap.map.floors[room.Floor];
		if (floor.gates == null) return false;

		int bestDist = int.MaxValue;

		foreach (var gate in floor.gates)
		{
			if (gate.roomA != room.RoomId && gate.roomB != room.RoomId) continue;

			foreach (var tileRow in DoorSystem.GetGateDoorTiles(gate, floor.config.chunkSize))
			{
				foreach (var tile in tileRow)
				{
					Vector3Int pos = new Vector3Int(tile.x, tile.y, room.Floor);
					if (!unit.Session.objectGrid.TryGetValue(pos, out InteractableObject obj)) continue;
					if (obj.Tags == null || !obj.Tags.Contains(DoorSystem.DoorTag)) continue;
					if (obj.DoorHp <= 0f) continue; // 이미 파괴됨 — 재설치 전까지 통행 가능이라 대상 아님
					if (obj.DoorOwnerFaction == FactionType.Human) continue; // 이미 아군 문

					int dist = AIMovementHelper.ChebyshevDistance(unit.position, new Vector2Int(tile.x, tile.y));
					if (dist < bestDist)
					{
						bestDist = dist;
						doorPos = pos;
						door = obj;
					}
				}
			}
		}
		return door != null;
	}

	private static BTStatus MoveToDoorAttack(Unit unit)
	{
		if (!FindHostileExitDoor(unit, out Vector3Int doorPos, out _))
		{
			unit.ClearAttackObjectTarget();
			unit.tacticalObjectAttackStuckTurns = 0;
			return BTStatus.Failure;
		}

		Vector2Int pos = new Vector2Int(doorPos.x, doorPos.y);

		// 문은 아군 소유가 아니면 IsBlockedByClosedDoor가 항상 막으므로, 이 문 타일 위에 직접 서는
		// 경우(StepOffObjectTile) 자체가 없다 — 인접에서 채널링만 하면 된다.
		if (AIMovementHelper.IsAdjacent(unit.position, pos))
		{
			BeginDoorChannel(unit, doorPos);
			unit.tacticalObjectAttackStuckTurns = 0;
			return BTStatus.Success;
		}

		// 채널링 시작 조건은 항상 반경 1로 고정한다 — 데미지 적용부가 반경 1만 인정하므로 반경 2에서
		// 시작하면 매 틱 시작→취소가 반복된다. 진짜로 반경 1까지 못 붙는 경우는 stuckTurns가 처리한다.
		unit.ClearAttackObjectTarget();
		// 리셋 기준은 "이동 성공 여부"가 아니라 "체비셰프 거리가 실제로 줄었는지"다 — 혼잡 구역의
		// 제자리 셔플도 Move() 관점에선 성공이라 그것만 보면 stuckTurns가 절대 쌓이지 않는다.
		int distBefore = AIMovementHelper.ChebyshevDistance(unit.position, pos);
		AIMovementHelper.MoveTowardsPos(unit, pos);
		if (AIMovementHelper.ChebyshevDistance(unit.position, pos) < distBefore)
		{
			unit.tacticalObjectAttackStuckTurns = 0;
			return BTStatus.Running;
		}

		// FindNearbyOpenTile은 목표가 멀면 도달 가능성과 무관하게 뭔가를 찾아버려 stuckTurns가 계속
		// 리셋될 수 있다 — 이미 근접(반경 2)했을 때만 쓰고, 아니면 스킵해서 혼잡/완전차단 판정으로 넘어간다.
		Vector2Int fallback = AIMovementHelper.IsAdjacent(unit.position, pos, radius: 2)
			? AIMovementHelper.FindNearbyOpenTile(unit, pos)
			: pos;
		if (fallback != pos)
		{
			AIMovementHelper.MoveTowardsPos(unit, fallback);
			if (AIMovementHelper.ChebyshevDistance(unit.position, pos) < distBefore)
			{
				unit.tacticalObjectAttackStuckTurns = 0;
				return BTStatus.Running;
			}
		}

		// 지형상 갈 곳이 아예 없는지 확인 — 다른 유닛이 잠깐 몰려 막힌 것뿐이면 몇 틱 인내하며
		// 재시도하고, 벽/닫힌 문으로 사방이 진짜 막혀 있으면 즉시 포기하고 경계 상태로 전환한다.
		if (AIMovementHelper.HasAnyStructurallyOpenAdjacentTile(unit))
		{
			unit.tacticalObjectAttackStuckTurns++;
			int limit = AIConfigLoader.Behavior?.tacticalObjectAttackStuckTurnLimit ?? 4;
			if (unit.tacticalObjectAttackStuckTurns >= limit)
			{
				unit.tacticalObjectAttackStuckTurns = 0;
				unit.currentAlertSearch = new AlertSearchState();
				return BTStatus.Failure;
			}
			return BTStatus.Running;
		}

		unit.tacticalObjectAttackStuckTurns = 0;
		unit.currentAlertSearch = new AlertSearchState();
		return BTStatus.Failure;
	}

	private static void BeginDoorChannel(Unit unit, Vector3Int doorPos)
	{
		unit.SetAttackObjectTarget(doorPos);
	}

	// 실제 데미지 적용은 UnitFunction.OnUpdate가 currentAttackObjectTarget을 보고 처리한다
	// (CoreAttackPerform과 동일한 채널링 패턴) — 여기서는 도착 유지/파괴 완료만 확인한다.
	private static BTStatus DoorAttackPerform(Unit unit)
	{
		if (!unit.currentAttackObjectTarget.HasValue) return BTStatus.Failure;
		Vector3Int doorPos = unit.currentAttackObjectTarget.Value;

		if (unit.Session == null || !unit.Session.objectGrid.TryGetValue(doorPos, out var obj))
		{
			unit.ClearAttackObjectTarget();
			return BTStatus.Success; // 파괴 완료(DoorSystem.RemoveDoor가 objectGrid에서 제거)
		}
		if (obj.DoorHp > 0f) return BTStatus.Running;

		unit.ClearAttackObjectTarget();
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
		if (unit.currentAttackObjectTarget.HasValue)
		{
			InteractableObject attackObj = null;
			unit.Session?.objectGrid.TryGetValue(unit.currentAttackObjectTarget.Value, out attackObj);
			bool isDoor = attackObj?.Tags != null && attackObj.Tags.Contains(DoorSystem.DoorTag);
			return isDoor ? "전술(문 공격)" : "전술(코어 공격)";
		}
		return "전술";
	}
}
