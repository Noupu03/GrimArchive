using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 대기 결정 하나의 상태(Unit.occupancyHold). Kind:
//  · Occupancy: 다음 걸음 자리를 같은 진영 유닛(Blocker)이 차지 — 그 유닛이 자리를 지키는 동안만 유효.
//  · Deferred : 자리가 비어도 Blocker(먼저 지나갈 유닛·마주 막혀 물러난 상대)에게 길을 내주는 중 — 기간(Until)만 본다.
public enum OccupancyHoldKind { Occupancy, Deferred }

public class OccupancyHold
{
	public OccupancyHoldKind Kind;
	public Unit Blocker;
	public Vector2Int NextPos;        // Occupancy: 막힌 걸음의 도착 앵커(점유자가 아직 이 자리를 차지하는지 확인)
	public OccupancyChoice Choice;    // Wait만 행동 틱을 건너뛰게 한다. Detour는 재판정 시각까지 결정만 기억한다(매 틱 A* 조회를 반복하지 않게).
	public float StartedAt;           // 같은 점유자에 대한 대기가 시작된 시각(미확인 대기 상한 판정)
	public float Until;               // Wait: 이 시각까지 행동을 쉰다 / Detour: 이 시각까지 결정 재사용
	public bool WaitKnown;
	// 대기를 시작할 때의 플레이어 명령 스냅샷 — 대기 중 새 명령이 오면 대기를 취소한다(04번 6장: 플레이어 직접 이동 명령은 일시 점유 동안 유지되지만 새 명령은 즉시 따른다).
	public Vector2Int? MoveTarget;
	public Unit AttackTarget;
	public Vector3Int? AttackObject;
}

// 점유 충돌 판단(04번 5~8장): 비전투 이동이 다른 유닛에게 막히면 '대기 vs 우회'를 예상 도착시간으로 비교하고, 제자리 대기 아군에게 비켜 달라 요청하며, 좁은 통로(게이트 문턱)에서는 역할 순서·마주 막힘 양보를 적용한다. 진입점은 AIMovementHelper.MoveTowardsPos(TryHold)와 GameSession.ProcessUnitAction(ShouldHold·TryHonorYield), 계산은 OccupancyMath(순수)에 있다.
// 전투 중 이동(적 인지·피격·시전·Combat 상태)에는 적용하지 않고 기존 '점유=벽' 즉시 우회를 둔다.
public static class OccupancySystem
{
	private static readonly Dir[] AllDirs = (Dir[])System.Enum.GetValues(typeof(Dir));

	// 점유자를 "이동 중"으로 볼 최근 이동 창 — 행동 주기(1/속도)의 이 배수 안에 움직였으면 Moving(처리 지연·큐 대기를 감안한 여유).
	private const float MovingWindowStepMultiplier = 2.5f;
	// 해결 불가 대기를 포기한 점유자에 대해 다시 대기하지 않는 시간 — 그동안은 기존 정체 인내가 포기로 이어 간다.
	private const float GiveUpCooldownSeconds = 3f;
	// 통과 순서 판단에서 주변 유닛을 훑는 반경(칸).
	private const int GateScanRadius = 3;
	// 마주 막혀 물러선 뒤 상대가 지나가길 기다리는 한 걸음 수.
	private const float RetreatHoldSteps = 2f;

	// ═══════════════════════════ 진입점 ═══════════════════════════

	// AIMovementHelper.MoveTowardsPos 맨 앞에서 호출한다. true = 이번 주기는 이동하지 않고 대기(또는 비켜서기)로 썼다, false = 기존 동작(점유를 벽으로 본 A* 이동).
	public static bool TryHold(Unit unit, Vector2Int dest)
	{
		var cfg = AIConfigLoader.Behavior;
		if (!(cfg?.occupancyArbitrationEnabled ?? true)) return false;
		if (!IsEligibleMover(unit)) return false;
		if (!(unit.MovementAlgorithm is AStarMovement astar)) return false;
		// 이번 프레임에 이미 대기·비켜서기로 행동을 썼다 — 한 행동 주기에 이동 호출을 두 번 하는 행동(대체 칸 재시도 등)이 방금의 물러나기를 곧바로 되돌리지 않게 한다.
		if (unit.occupancyActedFrame == Time.frameCount) return true;

		// 입장 중인 유닛은 앞 유닛에 막혀도 길 전체를 돌아가지 않는다 — 구조 경로 그대로 걷다가 문 통로 안·다음 방 쪽 앞 유닛이 막으면 지나갈 때까지 기다린다(사용자 확정).
		if ((cfg?.entryWaitForUnitAheadEnabled ?? true) && TryEntryStep(unit, dest, astar, cfg)) return true;

		// 성능 가드: 다음 걸음이 점유되려면 인접 칸에 다른 유닛이 있어야 하고, 통과 순서는 게이트 근처에서만 의미가 있다. 둘 다 아니면 구조 경로(A*) 조회 없이 곧바로 기존 동작이다.
		bool gateOrderEnabled = cfg?.gatePassOrderEnabled ?? true;
		if (!HasAdjacentOccupant(unit) && !(gateOrderEnabled && IsNearGate(unit)))
		{
			ClearHold(unit);
			return false;
		}

		AStarMovement twin = GetTwin(unit, astar);
		if (!twin.TryGetNextStep(unit, dest, out Dir dir)) { ClearHold(unit); return false; } // 점유를 무시해도 길이 없다 — 기존 동작

		Vector2Int cur = unit.position;
		Vector2Int dirVec = unit.GetDirVector(dir);
		Vector2Int nextPos = cur + dirVec;
		float now = Time.time;

		Unit blocker = FindBlocker(unit, cur, dirVec, out Vector2Int blockedAnchor);
		if (blocker == null)
		{
			// 점유는 없다 — 좁은 통로 진입 순서만 확인한다(04번 8장).
			if ((cfg?.gatePassOrderEnabled ?? true) && TryGateOrderHold(unit, cur, nextPos, now, out Unit deferTo))
				return StartDeferredHold(unit, deferTo, now, cfg);
			ClearHold(unit);
			return false;
		}

		if (unit.IsEnemy(blocker)) { ClearHold(unit); return false; } // 적 유닛: 전투·우회 등 기존 대응
		if (unit.occupancyGiveUpBlocker == blocker && now < unit.occupancyGiveUpUntil) return false; // 이 점유자에 대한 대기는 포기한 상태 — 기존 정체 인내로

		OccupancyHold existing = unit.occupancyHold;
		bool sameBlocker = existing != null && existing.Kind == OccupancyHoldKind.Occupancy && existing.Blocker == blocker;
		// 우회로 정한 결정은 재판정 시각까지 그대로 쓴다 — 우회 중 같은 점유자를 스칠 때마다 A* 조회를 두 번씩 반복하지 않게.
		if (sameBlocker && existing.Choice == OccupancyChoice.Detour && now < existing.Until) return false;

		float startedAt = sameBlocker ? existing.StartedAt : now;
		OccupancyChoice current = sameBlocker ? existing.Choice : OccupancyChoice.None;

		// 마주 막힘·통로 이탈(04번 8장): 통로 안에서 이쪽으로 나오는 유닛이 먼저이고, 그 밖에는 물러날 수 있는 쪽이 양보, 둘 다 가능하면 통과 우선순위가 낮은 쪽이 양보한다.
		if ((cfg?.gatePassOrderEnabled ?? true) && IsHeadOn(unit, blocker))
		{
			bool otherExiting = IsExitingTowards(blocker, unit);
			bool iCanRetreat = FindSidestep(unit, blocker, out Dir myRetreat);
			bool otherCanRetreat = FindSidestep(blocker, unit, out _);
			if (OccupancyMath.ShouldIYield(otherExiting, iCanRetreat, otherCanRetreat, PassKeyOf(unit), PassKeyOf(blocker)) && iCanRetreat)
			{
				unit.Move(myRetreat); // 겹침·자리 교환 없이 빈 타일로 한 걸음 물러난다(Move가 점유·벽을 다시 확인).
				if (unit.position != cur)
				{
					BeginHold(unit, OccupancyHoldKind.Deferred, blocker, nextPos, OccupancyChoice.Wait, startedAt, now + RetreatHoldSteps * OccupancyMath.StepSeconds(unit.AppliedWalkSpeed), true);
					return true;
				}
			}
			// 내가 양보하는 쪽이 아니거나 물러날 곳이 없다 — 아래 일반 판단(상대가 물러나길 대기·다른 경로)으로 넘어간다. 기다린 시간이 길다는 이유로 통과시키지는 않는다.
		}

		OccupantKind kind = Classify(blocker, out float? interactionRemaining);
		float? waitSeconds = OccupancyMath.EstimateWaitSeconds(kind, OccupancyMath.StepSeconds(blocker.AppliedWalkSpeed), interactionRemaining);

		float mySpeed = Mathf.Max(OccupancyMath.MinSpeed, unit.AppliedWalkSpeed);
		// 구조 경로(점유 무시) 길이 — 통로가 열린 뒤 남은 이동. 조회가 실패하면 체비셰프 거리로 근사한다.
		int structuralSteps = twin.TryGetPathLength(unit, dest, out int ls, out _) ? ls : Mathf.Max(1, AIMovementHelper.ChebyshevDistance(cur, dest));
		float structuralSeconds = structuralSteps / mySpeed;
		// 우회 경로(점유를 벽으로 본 실제 경로) 길이 — 없으면 null.
		float? detourSeconds = astar.TryGetPathLength(unit, dest, out int ld, out _) ? ld / mySpeed : (float?)null;

		OccupancyChoice choice = OccupancyMath.Decide(waitSeconds, structuralSeconds, detourSeconds, current);
		float recheck = Mathf.Max(0.05f, cfg?.occupancyRecheckSeconds ?? 0.5f);

		if (choice == OccupancyChoice.Detour)
		{
			BeginHold(unit, OccupancyHoldKind.Occupancy, blocker, blockedAnchor, OccupancyChoice.Detour, startedAt, now + recheck, waitSeconds.HasValue);
			return false; // 점유를 벽으로 본 기존 A* 이동이 우회 경로로 간다
		}

		// 대기를 택했다. 시간을 모르는 채로 상한을 넘겼으면 포기하고 기존 경로(행동별 정체 인내 → 9번 후속 행동)로 넘긴다.
		if (OccupancyMath.UnknownWaitExpired(now, startedAt, cfg?.occupancyWaitMaxSeconds ?? 6f, waitSeconds.HasValue))
		{
			unit.occupancyGiveUpBlocker = blocker;
			unit.occupancyGiveUpUntil = now + GiveUpCooldownSeconds;
			ClearHold(unit);
			LogHelper.Log(LogHelper.GAME, $"[점유] {unit.name}: {blocker.name}이(가) 비워질 시간을 모른 채 {cfg?.occupancyWaitMaxSeconds ?? 6f:F0}초 넘게 막혀 대기를 포기합니다(이후는 행동별 정체 처리)");
			return false;
		}

		// 제자리에서 기다리는 아군이 막고 있고 시간을 알 수 없다면 비켜 달라고 요청한다(04번 5장). 이동 중인 아군에게는 요청하지 않는다.
		if (kind == OccupantKind.IdleInPlace) RequestYield(unit, blocker, now, cfg?.yieldRequestSeconds ?? 2f);

		float holdSeconds = waitSeconds.HasValue ? Mathf.Min(waitSeconds.Value, recheck) : recheck;
		BeginHold(unit, OccupancyHoldKind.Occupancy, blocker, blockedAnchor, OccupancyChoice.Wait, startedAt, now + Mathf.Max(0.05f, holdSeconds), waitSeconds.HasValue);
		return true;
	}

	// 입장 중(PartyAdvanceSystem.EnteringPlanOf) 유닛의 한 걸음 — 점유를 벽으로 보는 A*는 앞 유닛이 문 통로를 막으면 다른 문·방을 도는 긴 우회를 고르므로, 구조 경로(점유 무시)의 다음 걸음을 직접 내딛고 같은 진영 유닛이 막았는데 그 유닛이 문 가까운 줄 이후(통로·다음 방 쪽)에 있으면 우회 없이 기다린다(문턱 통과 순서 TryGateOrderHold를 먼저 확인).
	// 진형 쪽(문 앞)에서 막으면 기존 판단(false)에 맡긴다. 기다림은 점유 대기와 같은 hold이고 영구 대기는 입장 단계 상한이 끊는다. true = 걸었거나 기다렸다.
	private static bool TryEntryStep(Unit unit, Vector2Int dest, AStarMovement astar, AIBehaviorConfig cfg)
	{
		PartyAdvancePlan plan = PartyAdvanceSystem.EnteringPlanOf(unit);
		if (plan == null) return false;

		AStarMovement twin = GetTwin(unit, astar);
		if (!twin.TryGetNextStep(unit, dest, out Dir dir)) return false; // 점유를 무시해도 길이 없다 — 기존 동작

		Vector2Int cur = unit.position;
		Vector2Int dirVec = unit.GetDirVector(dir);
		float now = Time.time;
		Unit blocker = FindBlocker(unit, cur, dirVec, out Vector2Int blockedAnchor);

		if (blocker == null)
		{
			if ((cfg?.gatePassOrderEnabled ?? true) && TryGateOrderHold(unit, cur, cur + dirVec, now, out Unit deferTo) && StartDeferredHold(unit, deferTo, now, cfg)) return true;
			unit.Move(dir);
			if (unit.position == cur) return false; // 점유 외 이유(코너 등)로 못 걸었다 — 기존 동작이 이어받는다
			ClearHold(unit);
			return true;
		}

		if (unit.IsEnemy(blocker) || !PartyFormationMath.IsAtOrBeyondRow(blocker.position, plan.NearTiles[0], plan.Forward)) return false;

		OccupancyHold existing = unit.occupancyHold;
		bool same = existing != null && existing.Kind == OccupancyHoldKind.Occupancy && existing.Blocker == blocker;
		// 제자리에서 기다리는 아군이 막고 있으면 비켜 달라고 요청한다(04번 5장) — 입장을 마치고 개인 행동으로 돌아간 유닛이 문 출구에 서 있는 경우.
		if (Classify(blocker, out _) == OccupantKind.IdleInPlace) RequestYield(unit, blocker, now, cfg?.yieldRequestSeconds ?? 2f);
		BeginHold(unit, OccupancyHoldKind.Occupancy, blocker, blockedAnchor, OccupancyChoice.Wait, same ? existing.StartedAt : now, now + Mathf.Max(0.05f, cfg?.occupancyRecheckSeconds ?? 0.5f), false);
		return true;
	}

	// GameSession.ProcessUnitAction이 JudgeState 뒤·ExecuteAction 앞에서 부른다. true면 이번 행동 주기는 대기로 쓴다(FSM 전환은 이미 끝난 뒤).
	public static bool ShouldHold(Unit unit)
	{
		OccupancyHold hold = unit.occupancyHold;
		if (hold == null) return false;

		float now = Time.time;
		if (hold.Choice != OccupancyChoice.Wait)
		{
			if (now > hold.Until + 1f) unit.occupancyHold = null; // 우회 결정 기억이 낡았다
			return false;
		}
		if (now >= hold.Until) return false; // 만료 — 행동 리프가 돌아 MoveTowardsPos → TryHold가 같은 충돌을 다시 판정한다

		if (!IsEligibleMover(unit) || CommandChanged(unit, hold) || hold.Blocker == null || hold.Blocker.hp <= 0)
		{
			unit.occupancyHold = null;
			return false;
		}
		// 점유 대기는 점유자가 여전히 그 자리를 차지하는 동안만 유효하다 — 떠났으면 곧바로 이동을 재개한다(04번 6장: 실제 점유가 비워지면 이동).
		if (hold.Kind == OccupancyHoldKind.Occupancy && !FootprintOverlaps(unit, hold.NextPos, hold.Blocker))
		{
			unit.occupancyHold = null;
			return false;
		}
		return true;
	}

	// 제자리 대기 중인 이 유닛에게 온 '비켜 달라' 요청을 받아 인접 빈 칸으로 한 걸음 비켜 준다(04번 5·7장, GameSession.ProcessUnitAction이 호출). true면 이번 주기는 비켜 주는 데 썼다.
	public static bool TryHonorYield(Unit unit)
	{
		if (unit.yieldRequestUntil <= 0f) return false;

		float now = Time.time;
		Unit requester = unit.yieldRequester;
		if (now >= unit.yieldRequestUntil || requester == null || requester.hp <= 0) { ClearYield(unit); return false; }
		if (!CanHonorYield(unit)) return false; // 지금은 비켜 줄 수 없다 — 요청 만료 전에 상태가 바뀌면 그때 처리한다

		Vector2Int before = unit.position;
		if (FindSidestep(unit, requester, out Dir dir))
		{
			unit.Move(dir);
			if (unit.position != before)
			{
				ClearYield(unit);
				unit.idleNextMoveTime = Mathf.Max(unit.idleNextMoveTime, now + 1.5f); // 배회 유닛이 곧바로 같은 자리로 되돌아오지 않게
				return true;
			}
		}
		return false;
	}

	// ═══════════════════════════ 대기 결정 보조 ═══════════════════════════

	// 비전투 이동 중인 살아 있는 유닛만 대상이다 — 적 인지·피격·시전·전투 상태·던전 입구 시퀀스는 기존 이동 규칙을 그대로 둔다.
	private static bool IsEligibleMover(Unit unit)
	{
		if (unit == null || unit.hp <= 0 || unit.isImmobile || unit.Session == null || unit.unitType == null) return false;
		if (unit.personalSpottedEnemies.Count > 0 || unit.isHitThisTurn || unit.CombatState.State.isCastingAttack) return false;
		if (unit.CurrentFsmStateIfCreated is CombatFSMState) return false;
		if (unit is Human human && human.isInDungeonEntranceSequence) return false;
		return true;
	}

	private static void BeginHold(Unit unit, OccupancyHoldKind kind, Unit blocker, Vector2Int nextPos, OccupancyChoice choice, float startedAt, float until, bool waitKnown)
	{
		unit.occupancyHold = new OccupancyHold
		{
			Kind = kind,
			Blocker = blocker,
			NextPos = nextPos,
			Choice = choice,
			StartedAt = startedAt,
			Until = until,
			WaitKnown = waitKnown,
			MoveTarget = unit.playerMoveTarget,
			AttackTarget = unit.playerAttackTarget,
			AttackObject = unit.playerAttackObjectTarget,
		};
		// 정당한 대기가 행동별 정체 인내(조사·탐색·함정·코어 문 공격·플레이어 이동 명령·경계 접근)로 포기되지 않게 카운터를 0으로 되돌린다.
		if (choice == OccupancyChoice.Wait)
		{
			unit.ResetNonCombatStuckCounters();
			unit.occupancyActedFrame = Time.frameCount;
		}
	}

	private static bool StartDeferredHold(Unit unit, Unit deferTo, float now, AIBehaviorConfig cfg)
	{
		OccupancyHold existing = unit.occupancyHold;
		bool same = existing != null && existing.Kind == OccupancyHoldKind.Deferred && existing.Blocker == deferTo;
		float startedAt = same ? existing.StartedAt : now;

		// 같은 상대에게 길을 내주는 대기도 상한을 넘기면 풀어 준다 — 상대가 스스로 막혀 영원히 안 지나가는 교착 방지(시간이 길다는 이유로 통과시키는 게 아니라 해결 불가 판정이다).
		if (OccupancyMath.UnknownWaitExpired(now, startedAt, cfg?.occupancyWaitMaxSeconds ?? 6f, false))
		{
			unit.occupancyGiveUpBlocker = deferTo;
			unit.occupancyGiveUpUntil = now + GiveUpCooldownSeconds;
			ClearHold(unit);
			return false;
		}
		BeginHold(unit, OccupancyHoldKind.Deferred, deferTo, unit.position, OccupancyChoice.Wait, startedAt, now + Mathf.Max(0.05f, cfg?.occupancyRecheckSeconds ?? 0.5f), false);
		return true;
	}

	private static void ClearHold(Unit unit) => unit.occupancyHold = null;
	private static void ClearYield(Unit unit) { unit.yieldRequester = null; unit.yieldRequestUntil = 0f; }

	private static bool CommandChanged(Unit unit, OccupancyHold hold)
		=> unit.playerMoveTarget != hold.MoveTarget || unit.playerAttackTarget != hold.AttackTarget || unit.playerAttackObjectTarget != hold.AttackObject;

	// 구조 경로(점유 무시) 쌍둥이 — RouteAssessment도 "점유 때문인 일시 막힘인가"를 가리려고 재사용한다.
	internal static AStarMovement GetTwin(Unit unit, AStarMovement astar)
	{
		if (unit.occupancyTwin == null || !ReferenceEquals(unit.occupancyTwinSource, astar))
		{
			unit.occupancyTwin = astar.CreateStructuralTwin();
			unit.occupancyTwinSource = astar;
		}
		return unit.occupancyTwin;
	}

	// 이 걸음(대각선이면 양옆 직교 이동 포함)의 점유 타일 전체를 차지한 다른 살아 있는 유닛(Move/CanMove가 거부하는 이유와 같은 점유). blockedAnchor는 막힌 이유가 된 점유 영역의 앵커로, 점유자가 계속 그 자리를 차지하는지 확인하는 기준이다.
	private static Unit FindBlocker(Unit unit, Vector2Int cur, Vector2Int dirVec, out Vector2Int blockedAnchor)
	{
		blockedAnchor = cur + dirVec;
		Unit b = OccupantAt(unit, blockedAnchor);
		if (b != null) return b;
		if (Mathf.Abs(dirVec.x) == 1 && Mathf.Abs(dirVec.y) == 1)
		{
			blockedAnchor = cur + new Vector2Int(dirVec.x, 0);
			b = OccupantAt(unit, blockedAnchor);
			if (b != null) return b;
			blockedAnchor = cur + new Vector2Int(0, dirVec.y);
			b = OccupantAt(unit, blockedAnchor);
		}
		return b;
	}

	// 점유 타일 둘레 1칸 안에 다른 살아 있는 유닛이 있는가 — 다음 걸음(대각선 포함)을 막을 수 있는 유닛은 전부 여기 있다.
	private static bool HasAdjacentOccupant(Unit unit)
	{
		Vector2Int size = unit.FootprintSize;
		int floor = unit.currentFloor;
		for (int dx = -1; dx <= size.x; dx++)
		{
			for (int dy = -1; dy <= size.y; dy++)
			{
				if (dx >= 0 && dx < size.x && dy >= 0 && dy < size.y) continue; // 자기 점유 타일
				if (unit.Session.unitGrid.TryGetValue(new Vector3Int(unit.position.x + dx, unit.position.y + dy, floor), out Unit o) && o != null && o != unit && o.hp > 0)
					return true;
			}
		}
		return false;
	}

	// 게이트(좁은 통로) 문턱 타일이 2칸 안에 있는가 — 통과 순서 판단은 진입 직전 한 걸음에서만 필요하다.
	private static bool IsNearGate(Unit unit)
	{
		int floor = unit.currentFloor;
		for (int dx = -2; dx <= 2; dx++)
			for (int dy = -2; dy <= 2; dy++)
				if (unit.Session.TryGetGateKeyAt(new Vector3Int(unit.position.x + dx, unit.position.y + dy, floor), out _)) return true;
		return false;
	}

	private static Unit OccupantAt(Unit unit, Vector2Int anchor)
	{
		Vector2Int size = unit.FootprintSize;
		int floor = unit.currentFloor;
		for (int dx = 0; dx < size.x; dx++)
			for (int dy = 0; dy < size.y; dy++)
				if (unit.Session.unitGrid.TryGetValue(new Vector3Int(anchor.x + dx, anchor.y + dy, floor), out Unit o) && o != null && o != unit && o.hp > 0)
					return o;
		return null;
	}

	// unit이 anchor에 섰을 때의 점유 타일 중 하나라도 other의 점유 타일과 겹치는가.
	private static bool FootprintOverlaps(Unit unit, Vector2Int anchor, Unit other)
	{
		Vector2Int size = unit.FootprintSize;
		for (int dx = 0; dx < size.x; dx++)
			for (int dy = 0; dy < size.y; dy++)
				if (other.ContainsPos(anchor.x + dx, anchor.y + dy)) return true;
		return false;
	}

	// 점유자 상태 분류(04번 5장 표). 인접해 막고 있는 점유자는 직접 확인한 것으로 본다.
	private static OccupantKind Classify(Unit blocker, out float? interactionRemainingSeconds)
	{
		interactionRemainingSeconds = null;

		// 앞 유닛도 무언가를 기다리는 중이면 비워질 시간을 알 수 없다(04번 6장 "앞의 다른 유닛도 막혀 있거나").
		if (blocker.occupancyHold != null && blocker.occupancyHold.Choice == OccupancyChoice.Wait) return OccupantKind.Busy;

		if (blocker is Human human && human.currentInvestigation != null && human.currentInvestigation.PenaltyActive)
		{
			interactionRemainingSeconds = OccupancyMath.InteractionRemainingSeconds(human.currentInvestigation.Progress01, AIConfigLoader.Behavior?.investigateDurationSeconds ?? ExplorationMath.InvestigateDurationSeconds);
			return OccupantKind.Interacting;
		}
		var trap = blocker.currentTrapInteraction;
		if (trap != null)
		{
			if (trap.Phase == TrapPhase.Disarming && trap.PenaltyActive)
			{
				float duration = AIConfigLoader.Behavior?.trapDisarmDurationSeconds ?? ExplorationMath.TrapDisarmDurationSeconds;
				interactionRemainingSeconds = OccupancyMath.InteractionRemainingSeconds(trap.DisarmProgress01, duration);
				return OccupantKind.Interacting;
			}
			if (trap.Phase == TrapPhase.Destroying && trap.DestroyActive) return OccupantKind.Interacting; // 파괴가 끝나는 시간은 모른다(남은 시간 null)
		}

		if (blocker.personalSpottedEnemies.Count > 0 || blocker.CombatState.State.isCastingAttack || blocker.CurrentFsmStateIfCreated is CombatFSMState)
			return OccupantKind.Busy;

		float window = MovingWindowStepMultiplier * OccupancyMath.StepSeconds(blocker.AppliedWalkSpeed);
		if (Time.time - blocker.lastMoveTime <= window) return OccupantKind.Moving;
		return OccupantKind.IdleInPlace;
	}

	// ═══════════════════════════ 비켜 주기(04번 5장·7장) ═══════════════════════════

	private static void RequestYield(Unit requester, Unit target, float now, float seconds)
	{
		// 한 점유자에게 요청은 동시에 하나만 — 여러 유닛이 매 순간 요청을 교환하지 않게 한다.
		if (target.yieldRequestUntil > now && target.yieldRequester != null && target.yieldRequester != requester) return;
		target.yieldRequester = requester;
		target.yieldRequestUntil = now + seconds;
	}

	// 지금 비켜 줄 수 있는 상태인가 — 전투·상호작용·이동 없이 제자리 대기 중인 유닛만. 플레이어 명령/정지/제자리 공격·다른 대기·Human 행동 대기 중이면 진형·임무·명령 위치를 보존한다.
	private static bool CanHonorYield(Unit unit)
	{
		if (!IsEligibleMover(unit)) return false;
		if (unit.HasActivePlayerCommand()) return false;
		if (unit.occupancyHold != null) return false;
		if (unit is Human human && human.currentWait != null) return false;
		return Classify(unit, out _) == OccupantKind.IdleInPlace;
	}

	// other의 경로를 막지 않는 인접 빈 칸을 찾는다(점유 크기·함정 구역·문 타일·방 제한·배회 앵커 반경+1 준수). other의 경로 앞 3칸에 걸치는 자리는 제외하고 통로(게이트) 타일은 후순위, 없으면 false.
	private static bool FindSidestep(Unit unit, Unit other, out Dir bestDir)
	{
		bestDir = Dir.UP;
		if (unit == null || unit.isImmobile || unit.Session == null) return false;

		HashSet<Vector2Int> avoid = BuildAvoidTiles(other);
		int floor = unit.currentFloor;
		bool roomConfined = AIMovementHelper.IsRoomConfined(unit);
		Room myRoom = null;
		if (roomConfined) unit.Session.roomGrid.TryGetValue(new Vector3Int(unit.position.x, unit.position.y, floor), out myRoom);
		int anchorRadius = (AIConfigLoader.Behavior?.idleWanderRadius ?? 2) + 1;
		Vector2Int size = unit.FootprintSize;

		float bestScore = float.MaxValue;
		bool found = false;
		foreach (Dir d in AllDirs)
		{
			Vector2Int vec = unit.GetDirVector(d);
			if (vec == Vector2Int.zero) continue;
			Vector2Int cand = unit.position + vec;

			if (!unit.CanMove(cand)) continue;
			if (Mathf.Abs(vec.x) == 1 && Mathf.Abs(vec.y) == 1 &&
				(!unit.CanMove(unit.position + new Vector2Int(vec.x, 0)) || !unit.CanMove(unit.position + new Vector2Int(0, vec.y)))) continue; // Move의 코너 커팅 규칙과 동일
			if (TrapAvoidance.BlocksGeneralStep(unit, cand)) continue;
			if (unit.Session.IsDoorTile(new Vector3Int(cand.x, cand.y, floor))) continue;
			if (roomConfined && myRoom != null &&
				(!unit.Session.roomGrid.TryGetValue(new Vector3Int(cand.x, cand.y, floor), out Room candRoom) || candRoom != myRoom)) continue;
			if (unit.idleAnchorPosition.HasValue && AIMovementHelper.ChebyshevDistance(cand, unit.idleAnchorPosition.Value) > anchorRadius) continue;

			bool onAvoid = false;
			for (int dx = 0; dx < size.x && !onAvoid; dx++)
				for (int dy = 0; dy < size.y; dy++)
					if (avoid.Contains(new Vector2Int(cand.x + dx, cand.y + dy))) { onAvoid = true; break; }
			if (onAvoid) continue;

			float score = unit.Session.TryGetGateKeyAt(new Vector3Int(cand.x, cand.y, floor), out _) ? 10f : 0f;
			if (unit.idleAnchorPosition.HasValue) score += AIMovementHelper.ChebyshevDistance(cand, unit.idleAnchorPosition.Value);
			if (score < bestScore) { bestScore = score; bestDir = d; found = true; }
		}
		return found;
	}

	// other의 현재 자리와 (캐시된) 경로 앞 3칸 — 비켜 서는 자리가 여기 걸치면 길을 여전히 막는다.
	private static HashSet<Vector2Int> BuildAvoidTiles(Unit other)
	{
		var avoid = new HashSet<Vector2Int>();
		if (other == null) return avoid;

		Vector2Int size = other.FootprintSize;
		for (int dx = 0; dx < size.x; dx++)
			for (int dy = 0; dy < size.y; dy++)
				avoid.Add(new Vector2Int(other.position.x + dx, other.position.y + dy));

		// other가 '점유가 없다면 가려던' 경로(구조 경로 쌍둥이 캐시)를 우선 쓴다 — 실제 이동 경로는 막힌 자리를 돌아가므로 피할 대상이 아니다. 쌍둥이가 없으면 실제 경로 캐시로 대신한다.
		AStarMovement source = other.occupancyTwin ?? other.MovementAlgorithm as AStarMovement;
		if (source != null)
		{
			List<Vector2Int> preview = source.BuildCachedPathPreview(other, 3);
			for (int i = 1; i < preview.Count; i++) avoid.Add(preview[i]);
		}
		return avoid;
	}

	// ═══════════════════════════ 좁은 통로 통과 순서·마주 막힘(04번 8장) ═══════════════════════════

	internal static PassKey PassKeyOf(Unit unit)
		=> new PassKey(OccupancyMath.EntryRank(CombatScoreMath.ResolveCombatRole(unit.unitType)),
			unit.hp / Mathf.Max(1f, unit.maxHp), unit.PassTieBreak, unit.GetInstanceID());

	private static bool IsInGate(Unit unit, int gateKey)
		=> unit.Session.TryGetGateKeyAt(new Vector3Int(unit.position.x, unit.position.y, unit.currentFloor), out int k) && k == gateKey;

	// 마주 막힘 — 점유자가 나를 기다리는 중(서로 기다림)이거나, 이동 중인 점유자의 구조 다음 걸음이 내 자리다.
	private static bool IsHeadOn(Unit unit, Unit blocker)
	{
		if (blocker.occupancyHold != null && blocker.occupancyHold.Choice == OccupancyChoice.Wait && blocker.occupancyHold.Blocker == unit) return true;

		if (Time.time - blocker.lastMoveTime > MovingWindowStepMultiplier * OccupancyMath.StepSeconds(blocker.AppliedWalkSpeed)) return false;
		if (!(blocker.MovementAlgorithm is AStarMovement bAstar) || !bAstar.TryGetCachedDestination(out Vector2Int bDest)) return false;
		AStarMovement bTwin = GetTwin(blocker, bAstar);
		if (!bTwin.TryGetNextStep(blocker, bDest, out Dir bDir)) return false;
		Vector2Int bNext = blocker.position + blocker.GetDirVector(bDir);
		return unit.ContainsPos(bNext.x, bNext.y);
	}

	// v가 이미 통로(게이트) 안에 있고 캐시된 경로상 통로를 벗어나 me 쪽으로 나오는 중인가 — 통로 이탈이 새 진입보다 먼저다.
	private static bool IsExitingTowards(Unit v, Unit me)
	{
		var session = v.Session;
		if (session == null || !session.TryGetGateKeyAt(new Vector3Int(v.position.x, v.position.y, v.currentFloor), out int gateKey)) return false;
		if (!(v.MovementAlgorithm is AStarMovement vAstar)) return false;

		List<Vector2Int> preview = vAstar.BuildCachedPathPreview(v, 4);
		for (int i = 1; i < preview.Count; i++)
		{
			bool inSameGate = session.TryGetGateKeyAt(new Vector3Int(preview[i].x, preview[i].y, v.currentFloor), out int k) && k == gateKey;
			if (inSameGate) continue;
			return AIMovementHelper.ChebyshevDistance(preview[i], me.position) < AIMovementHelper.ChebyshevDistance(v.position, me.position);
		}
		return false;
	}

	// v의 캐시된 경로 앞 4칸이 이 게이트를 지나려 하는가.
	private static bool WantsToEnterGate(Unit v, int gateKey)
	{
		if (!(v.MovementAlgorithm is AStarMovement vAstar)) return false;
		List<Vector2Int> preview = vAstar.BuildCachedPathPreview(v, 4);
		for (int i = 1; i < preview.Count; i++)
			if (v.Session.TryGetGateKeyAt(new Vector3Int(preview[i].x, preview[i].y, v.currentFloor), out int k) && k == gateKey) return true;
		return false;
	}

	// 내 다음 걸음이 게이트 진입이고(통로 안이면 계속 진행) 주변에 (1) 통로에서 이쪽으로 빠져나오는 같은 진영 유닛 (2) 같은 게이트로 진입하려는 더 우선순위 높은 같은 진영 유닛이 있으면 길을 내준다. 우선순위는 역할(전방 근접 → 근접 지원 → 원거리 공격 → 원거리 지원) → HP 비율 → 유지되는 무작위 값이며 리더도 예외 없다.
	private static bool TryGateOrderHold(Unit unit, Vector2Int cur, Vector2Int nextPos, float now, out Unit deferTo)
	{
		deferTo = null;
		var session = unit.Session;
		int floor = unit.currentFloor;

		if (!session.TryGetGateKeyAt(new Vector3Int(nextPos.x, nextPos.y, floor), out int gateKey)) return false;
		if (session.TryGetGateKeyAt(new Vector3Int(cur.x, cur.y, floor), out _)) return false; // 이미 통로 안 — 새 진입이 아니다

		PassKey mine = PassKeyOf(unit);
		var seen = new HashSet<Unit>();
		for (int dx = -GateScanRadius; dx <= GateScanRadius; dx++)
		{
			for (int dy = -GateScanRadius; dy <= GateScanRadius; dy++)
			{
				if (!session.unitGrid.TryGetValue(new Vector3Int(nextPos.x + dx, nextPos.y + dy, floor), out Unit v)) continue;
				if (v == null || v == unit || v.hp <= 0 || unit.IsEnemy(v) || !seen.Add(v)) continue;
				if (unit.occupancyGiveUpBlocker == v && now < unit.occupancyGiveUpUntil) continue;

				if (IsInGate(v, gateKey))
				{
					if (IsExitingTowards(v, unit)) { deferTo = v; return true; } // 통로를 빠져나오는 유닛이 먼저
					continue;
				}
				if (WantsToEnterGate(v, gateKey) && OccupancyMath.ComparePassPriority(PassKeyOf(v), mine) < 0) { deferTo = v; return true; }
			}
		}
		return false;
	}
}
