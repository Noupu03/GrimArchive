using System.Collections.Generic;
using UnityEngine;

// 건설 설계명세서 v0.2 1-6·2-8 — 내부 작업자의 입장·퇴장·비켜주기·내부 사망·시설 파괴 시 배출.
// 내부 작업자(생명 에너지·연구 시설에 내부 역할로 배정된 유닛)는 상하좌우 입장 지점에서 시설 안으로 들어가 모습이 사라진다 - 유닛 격자·시야·타깃팅·AI 행동에서 빠지고
// 시설이 대신 공격받는다. 정원·몸체 면적·담당 코어·실제 수리 조건으로 출입하며, 퇴장할 공간이 없으면 순간이동 없이 퇴장 대기한다.
public partial class BuildingManager
{
	private const double InsideTickSeconds = 0.25;
	private const double FacilityHitMemorySeconds = 2.0;   // 시설 피격을 '지금 일어난 전투'로 보는 시간
	private const double YieldHoldSeconds = 2.5;           // 비켜 준 작업자가 다시 자리로 돌아오기 전 기다리는 시간
	private const double YieldAskIntervalSeconds = 1.0;    // 같은 퇴장 대기에 대한 비켜 달라는 요청 간격
	private double _nextInsideTickAt;
	private readonly Dictionary<Unit, double> _yieldAskedAt = new Dictionary<Unit, double>();

	// ── 역할 ──

	// 이 유닛이 b의 내부 작업자(배정 + 내부 역할)인가 - 외부 현장 작업자와 구분한다. 수리·강화·철거 같은 현장 작업이 등록돼도 내부 작업자는 그대로 내부 작업자다.
	public static bool IsInternalWorkerOf(Unit u, BuildingData b) => u != null && b != null && u.assignedWorkBuilding == b && u.assignedWorkIsInternal;

	public List<Unit> CollectInternalWorkers(BuildingData b)
	{
		var workers = new List<Unit>();
		if (b == null || GameSession.Instance?.units == null) return workers;
		foreach (var u in GameSession.Instance.units)
			if (u != null && u.hp > 0f && IsInternalWorkerOf(u, b)) workers.Add(u);
		return workers;
	}

	public int CountInternalWorkers(BuildingData b)
	{
		if (b == null || GameSession.Instance?.units == null) return 0;
		int count = 0;
		foreach (var u in GameSession.Instance.units)
			if (u != null && u.hp > 0f && IsInternalWorkerOf(u, b)) count++;
		return count;
	}

	public int CountInsideWorkers(BuildingData b)
	{
		if (b == null) return 0;
		b.InsideWorkers.RemoveAll(u => u == null || u.hp <= 0f || u.insideFacility != b);
		return b.InsideWorkers.Count;
	}

	private static int InsideBodyArea(BuildingData b)
	{
		int area = 0;
		foreach (Unit u in b.InsideWorkers)
			if (u != null) area += u.FootprintSize.x * u.FootprintSize.y;
		return area;
	}

	// ── 입장 조건 ──

	// 시설 피격을 '현재 방의 전투'로 인지하는 시간 안인가(건설 명세 1-6-표1). 시설 피격만으로 적의 정체·위치를 알 수는 없으므로 판정에만 쓴다.
	private static bool FacilityHitRecently(BuildingData b, double now) => now - b.LastHitGameSeconds <= FacilityHitMemorySeconds;

	// 시설이 있는 방에 이 유닛에게 적대적인 살아 있는 유닛(시설 안에 있지 않은)이 있는가 - '현재 방의 전투 인지'의 근사다. 인지 판정 자체는 밖으로 나온 뒤 일반 시야 규칙을 따른다.
	private bool RoomHasHostileTo(BuildingData b, Unit worker, GameSession session, Dictionary<BuildingData, bool> cache)
	{
		if (cache != null && cache.TryGetValue(b, out bool cached)) return cached;
		bool found = false;
		if (session.roomGrid.TryGetValue(b.Position, out Room room) && room != null)
		{
			for (int i = 0; i < session.units.Count && !found; i++)
			{
				Unit e = session.units[i];
				if (e == null || e.hp <= 0f || e.IsInsideFacility || e.currentFloor != b.Position.z) continue;
				if (!worker.IsEnemy(e)) continue;
				if (session.roomGrid.TryGetValue(new Vector3Int(e.position.x, e.position.y, e.currentFloor), out Room er) && er == room) found = true;
			}
		}
		if (cache != null) cache[b] = found;
		return found;
	}

	// 내부 작업자가 밖으로 나와 전투에 참여해야 하는가 - '대응' 설정이고 작업 우선(재지시)이 아니며 시설이 피격됐거나 같은 방에 적이 있을 때(건설 명세 1-6-표1).
	private bool ShouldRespondToCombat(Unit u, BuildingData b, GameSession session, double now, Dictionary<BuildingData, bool> hostileCache)
	{
		if (u.internalCombatSetting != InternalCombatSetting.Respond || u.workPriorityActive) return false;
		return InternalWorkRules.ShouldExitForCombat(u.internalCombatSetting, RoomHasHostileTo(b, u, session, hostileCache), FacilityHitRecently(b, now));
	}

	private bool CanEnterNow(Unit u, BuildingData b, GameSession session, double now, Dictionary<BuildingData, bool> hostileCache, out string reason)
	{
		reason = null;
		if (u.StatusEffects.State.stunDuration > 0f || u.IsInEvolutionPresentation) { reason = "행동 불가"; return false; }
		if (u.carriedCorpse != null || u.carriedDismantledBuilding != null || u.pendingCorpsePickupTarget != null || u.pendingDismantledPackagePickup != null) { reason = "운반 중"; return false; }
		if (u.playerMoveTarget.HasValue) { reason = "이동 중"; return false; }
		IFSMState st = u.fsm?.CurrentState;
		if (st is CombatFSMState || st is TacticalFSMState) { reason = "전투·경계 중"; return false; }
		if (ShouldRespondToCombat(u, b, session, now, hostileCache)) { reason = "전투 대응"; return false; }

		// 정원·몸체 크기·코어·실제 수리·강화 중 입장 불가 + 전원이 겹치지 않고 나올 면적(건설 명세 1-6·2-8).
		reason = InternalWorkRules.GetEnterBlockReason(b.Kind, CountInsideWorkers(b), u.FootprintSize, AreCoreFunctionsActive(b.Position.z), b.FieldWorkActive,
			InsideBodyArea(b), b.Footprint.x * b.Footprint.y);
		return reason == null;
	}

	// ── 틱 ──

	// 입장·퇴장·비켜주기를 0.25초마다 처리한다. 나갈 유닛을 먼저 통과시키고(건설 명세 1-6) 그다음에 들어올 유닛을 확인한다.
	private void TickInternalWork(GameSession session)
	{
		double now = session.ElapsedGameSeconds;
		TickPendingInsideCorpses(session, now);
		if (now < _nextInsideTickAt) return;
		_nextInsideTickAt = now + InsideTickSeconds;

		var hostileCache = new Dictionary<BuildingData, bool>();
		var units = session.units;

		for (int i = 0; i < units.Count; i++)
		{
			Unit u = units[i];
			if (u != null && u.hp > 0f && u.IsInsideFacility) TickInsideWorker(u, session, now, hostileCache);
		}
		for (int i = 0; i < units.Count; i++)
		{
			Unit u = units[i];
			if (u == null || u.hp <= 0f || u.IsInsideFacility || !u.assignedWorkIsInternal || u.assignedWorkBuilding == null) continue;
			TickOutsideInternalWorker(u, session, now, hostileCache);
		}
	}

	private void TickInsideWorker(Unit u, GameSession session, double now, Dictionary<BuildingData, bool> hostileCache)
	{
		BuildingData b = u.insideFacility;
		if (b == null || !_allBuildings.Contains(b))
		{
			// 시설이 사라졌다 - 정상 경로(파괴·철거·분해 완료)는 각자 배출하지만 놓친 경우의 안전망이다.
			EjectInsideWorkersAtFootprint(b, session);
			return;
		}

		if (u.assignedWorkBuilding != b)
		{
			RequestInsideExit(u, InternalWorkRules.ExitCause.Released);
		}
		else if (!u.insideExitRequested)
		{
			if (!AreCoreFunctionsActive(b.Position.z)) RequestInsideExit(u, InternalWorkRules.ExitCause.CoreDestroyed);
			else if (ShouldRespondToCombat(u, b, session, now, hostileCache)) RequestInsideExit(u, InternalWorkRules.ExitCause.Combat);
		}

		if (!u.insideExitRequested) return;
		if (TryExitFacility(u, b, session)) return;
		RequestExitYield(u, b, session, now);
	}

	private static void RequestInsideExit(Unit u, InternalWorkRules.ExitCause cause)
	{
		if (u.insideExitRequested && u.insideExitCause != InternalWorkRules.ExitCause.Combat) return; // 더 중요한 사유가 이미 있다
		u.insideExitRequested = true;
		u.insideExitCause = cause;
	}

	private void TickOutsideInternalWorker(Unit u, GameSession session, double now, Dictionary<BuildingData, bool> hostileCache)
	{
		BuildingData b = u.assignedWorkBuilding;
		if (!_allBuildings.Contains(b) || !b.IsFormallyInstalled || u.currentFloor != b.Position.z) return;
		if (now < u.workResumeHoldUntil) return;
		var origin = new Vector2Int(b.Position.x, b.Position.y);
		if (!WorkSpeedMath.IsOrthogonallyAdjacent(origin, b.Footprint, u.position, u.FootprintSize)) return;
		if (!CanEnterNow(u, b, session, now, hostileCache, out _)) return;
		EnterFacility(u, b, session);
	}

	// 입장 대기 중인 이유(작업 현황·시설 UI) - 접근 중이면 null이다.
	public string DescribeEntryWait(Unit u, BuildingData b)
	{
		if (u == null || b == null || u.IsInsideFacility) return null;
		var origin = new Vector2Int(b.Position.x, b.Position.y);
		if (!WorkSpeedMath.IsOrthogonallyAdjacent(origin, b.Footprint, u.position, u.FootprintSize)) return null;
		if (!AreCoreFunctionsActive(b.Position.z)) return "코어 수복 대기";
		if (b.FieldWorkActive) return "수리·강화 대기";
		GameSession session = GameSession.Instance;
		if (session == null) return null;
		return CanEnterNow(u, b, session, session.ElapsedGameSeconds, null, out string reason) ? null : reason;
	}

	// ── 입장·퇴장 ──

	private void EnterFacility(Unit u, BuildingData b, GameSession session)
	{
		session.UnregisterUnitPos(u, u.position); // 격자에서 빠진다 - 타깃팅·시야·광역 판정·점유에서 사라진다
		u.insideEntryTile = u.position;
		u.insideFacility = b;
		u.insideExitRequested = false;
		u.insideExitCause = InternalWorkRules.ExitCause.None;
		if (!b.InsideWorkers.Contains(u)) b.InsideWorkers.Add(u);

		// 이미 이 유닛을 보고 있던 쪽의 인지 기록을 정리한다 - 시야는 매 갱신마다 다시 만들어지지만 지속 인지 기록은 남는다.
		for (int i = 0; i < session.units.Count; i++)
		{
			Unit other = session.units[i];
			if (other == null || other == u) continue;
			other.Perception.RemovePerceptionRecord(u);
			other.personalSpottedEnemies.Remove(u);
		}
		u.currentAlertSearch = null;
		u.MovementAlgorithm?.ClearCache();

		unitGenerate?.SetUnitHiddenInside(u, true);
		u.InputMgr?.DeselectUnit(u); // 숨은 유닛은 선택하지 않는다 - 작업자 목록으로만 다룬다
		LogHelper_InsideLog(u, b, "입장");
	}

	private bool TryExitFacility(Unit u, BuildingData b, GameSession session)
	{
		if (!TryFindExitOrigin(u, b, session, out Vector2Int origin)) return false;
		CompleteExit(u, b, origin, session);
		return true;
	}

	private void CompleteExit(Unit u, BuildingData b, Vector2Int origin, GameSession session)
	{
		InternalWorkRules.ExitCause cause = u.insideExitCause;
		u.insideFacility = null;
		u.insideExitRequested = false;
		u.insideExitCause = InternalWorkRules.ExitCause.None;
		b?.InsideWorkers.Remove(u);
		_yieldAskedAt.Remove(u);

		u.position = origin;
		session.RegisterUnitPos(u, origin);
		u.MovementAlgorithm?.ClearCache();
		unitGenerate?.SetUnitHiddenInside(u, false);
		LogHelper_InsideLog(u, b, $"퇴장({cause})");

		// 원인별 배정 처리(건설 명세 2-5-표2): 철거는 해제 후 방어 위치 복귀, 분해·코어 파괴·수리 대기·전투는 배정을 유지한다.
		if (!InternalWorkRules.KeepsAssignmentAfterExit(cause) && u.assignedWorkBuilding == b && b != null)
		{
			u.assignedWorkBuilding = null;
			u.workQueue?.Remove(b);
			ReleaseWorkPriority(u);
			DefensePlacementSystem.Instance?.OnWorkTaskEnded(u);
		}
		else if (cause != InternalWorkRules.ExitCause.Combat)
		{
			ReleaseWorkPriority(u);
		}
	}

	private static void LogHelper_InsideLog(Unit u, BuildingData b, string what)
	{
		Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, $"[내부 작업] {u.unitType?.typeName} {what} — {b?.DisplayName}#{b?.FacilityId}");
	}

	// 몸 전체가 들어가는 상하좌우 인접 빈 공간(입장 지점 우선). 순간이동·자리 교환은 없다.
	private bool TryFindExitOrigin(Unit u, BuildingData b, GameSession session, out Vector2Int result)
	{
		result = u.position;
		var origin = new Vector2Int(b.Position.x, b.Position.y);
		var candidates = WorkSpeedMath.GetOrthogonalAdjacentOrigins(origin, b.Footprint, u.FootprintSize);
		float best = float.MaxValue;
		bool found = false;
		foreach (Vector2Int c in candidates)
		{
			if (!IsBodyFree(c, u.FootprintSize, b.Position.z, session, u)) continue;
			float d = Vector2Int.Distance(c, u.insideEntryTile);
			if (d >= best) continue;
			best = d;
			result = c;
			found = true;
		}
		return found;
	}

	private bool IsBodyStructurallyOpen(Vector2Int origin, Vector2Int size, int floor, GameSession session)
	{
		for (int dx = 0; dx < size.x; dx++)
		{
			for (int dy = 0; dy < size.y; dy++)
			{
				var t = new Vector3Int(origin.x + dx, origin.y + dy, floor);
				if (buildingGrid.ContainsKey(t)) return false;
				if (createMap != null && !createMap.IsStaticTileWalkable(floor, new Vector2Int(t.x, t.y))) return false;
				if (session.IsDoorTile(t)) return false;
				if (session.objectGrid.TryGetValue(t, out InteractableObject o) && o != null && DoorSystem.IsBarrier(o)) return false;
			}
		}
		return true;
	}

	private bool IsBodyFree(Vector2Int origin, Vector2Int size, int floor, GameSession session, Unit ignore)
	{
		if (!IsBodyStructurallyOpen(origin, size, floor, session)) return false;
		for (int dx = 0; dx < size.x; dx++)
			for (int dy = 0; dy < size.y; dy++)
				if (session.unitGrid.TryGetValue(new Vector3Int(origin.x + dx, origin.y + dy, floor), out Unit occ) && occ != null && occ != ignore && occ.hp > 0f) return false;
		return true;
	}

	// ── 비켜주기(1-6-표2) ──

	private void CollectBodyBlockers(Vector2Int origin, Vector2Int size, int floor, GameSession session, Unit ignore, List<Unit> result)
	{
		for (int dx = 0; dx < size.x; dx++)
		{
			for (int dy = 0; dy < size.y; dy++)
			{
				if (session.unitGrid.TryGetValue(new Vector3Int(origin.x + dx, origin.y + dy, floor), out Unit occ) && occ != null && occ != ignore && occ.hp > 0f && !result.Contains(occ))
					result.Add(occ);
			}
		}
	}

	private bool IsBusyOrFighting(Unit occ)
	{
		if (occ.StatusEffects.State.stunDuration > 0f || occ.IsInEvolutionPresentation) return true;
		IFSMState st = occ.fsm?.CurrentState;
		if (st is CombatFSMState || st is TacticalFSMState) return true;
		if (occ.carriedCorpse != null || occ.carriedDismantledBuilding != null || occ.pendingCorpsePickupTarget != null || occ.pendingDismantledPackagePickup != null) return true;
		if (occ.playerMoveTarget.HasValue || occ.assignedCoreRestorePos.HasValue) return true;
		// 외부 현장 작업을 하고 있는 유닛은 작업 자리를 비워 주지 않는다 - 행동을 강제로 취소시키지 않는다.
		if (occ.assignedWorkBuilding != null && !occ.assignedWorkIsInternal) return true;
		return false;
	}

	// 퇴장할 공간이 없을 때 막고 있는 유닛을 처리한다. 순간이동·자리 교환은 없고, 조건이 안 되면 퇴장 대기를 유지한다.
	private void RequestExitYield(Unit exiting, BuildingData b, GameSession session, double now)
	{
		if (_yieldAskedAt.TryGetValue(exiting, out double last) && now - last < YieldAskIntervalSeconds) return;
		_yieldAskedAt[exiting] = now;

		var origin = new Vector2Int(b.Position.x, b.Position.y);
		var candidates = WorkSpeedMath.GetOrthogonalAdjacentOrigins(origin, b.Footprint, exiting.FootprintSize);

		// 구조적으로 설 수 있는 후보 중 막은 유닛이 모두 비켜 줄 수 있는 쪽을 고른다. 대기 중인 아군(일반)을 먼저, 없으면 작업자가 물러난다.
		List<Unit> chosen = null;
		int chosenWorkers = int.MaxValue;
		bool anyOpen = false;
		foreach (Vector2Int c in candidates)
		{
			if (!IsBodyStructurallyOpen(c, exiting.FootprintSize, b.Position.z, session)) continue;
			anyOpen = true;
			var blockers = new List<Unit>();
			CollectBodyBlockers(c, exiting.FootprintSize, b.Position.z, session, exiting, blockers);
			if (blockers.Count == 0) continue; // 이미 비어 있다 - 다음 틱에 나간다
			bool movable = true;
			int workers = 0;
			foreach (Unit occ in blockers)
			{
				if (!occ.IsPlayerMonsterFaction) { movable = false; break; } // 적대·야생 유닛이 막은 곳은 기다린다
				var state = InternalWorkRules.ClassifyBlocker(IsBusyOrFighting(occ), occ.assignedWorkIsInternal && occ.assignedWorkBuilding == b, exitingWaiting: true);
				var action = InternalWorkRules.ResolveYield(state);
				if (action == InternalWorkRules.YieldAction.WaitWithoutForcing || action == InternalWorkRules.YieldAction.KeepExitWait) { movable = false; break; }
				if (action == InternalWorkRules.YieldAction.WorkerStepsBackThenResume) workers++;
			}
			if (!movable) continue;
			if (workers < chosenWorkers) { chosen = blockers; chosenWorkers = workers; }
		}
		if (!anyOpen || chosen == null) return; // 빈 공간이 없음 - 퇴장 대기 유지

		foreach (Unit occ in chosen) StepAsideFor(occ, b, session, now);

		// 퇴장 대기 중인 아군이 있으면 출구를 막은 주변 대기 아군에게도 같은 비켜주기를 적용한다(1-6-표2).
		for (int i = 0; i < session.units.Count; i++)
		{
			Unit near = session.units[i];
			if (near == null || near.hp <= 0f || near.IsInsideFacility || !near.IsPlayerMonsterFaction || chosen.Contains(near)) continue;
			if (near.currentFloor != b.Position.z || IsBusyOrFighting(near) || near.assignedWorkBuilding != null) continue;
			if (!WorkSpeedMath.IsOrthogonallyAdjacent(origin, b.Footprint, near.position, near.FootprintSize)) continue;
			StepAsideFor(near, b, session, now);
		}
	}

	private void StepAsideFor(Unit occ, BuildingData b, GameSession session, double now)
	{
		if (!TryFindYieldTile(occ, b, session, out Vector2Int free)) return;
		occ.workResumeHoldUntil = now + YieldHoldSeconds; // 잠시 물러난 작업자는 퇴장이 끝날 때까지 자리로 돌아오지 않는다
		IssueInternalEconomyMove(occ, free);
	}

	// 시설 둘레(입장·퇴장 지점)에서 한 칸 이상 떨어진 가까운 빈 공간 - 비켜 줄 때 다른 출구를 다시 막지 않게 한다.
	private bool TryFindYieldTile(Unit occ, BuildingData b, GameSession session, out Vector2Int result)
	{
		result = occ.position;
		var origin = new Vector2Int(b.Position.x, b.Position.y);
		for (int r = 1; r <= 6; r++)
		{
			for (int dx = -r; dx <= r; dx++)
			{
				for (int dy = -r; dy <= r; dy++)
				{
					if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
					var c = new Vector2Int(occ.position.x + dx, occ.position.y + dy);
					if (!IsBodyFree(c, occ.FootprintSize, occ.currentFloor, session, occ)) continue;
					if (WorkSpeedMath.IsOrthogonallyAdjacent(origin, b.Footprint, c, occ.FootprintSize)) continue;
					result = c;
					return true;
				}
			}
		}
		return false;
	}

	// ── 배정이 사라졌을 때 / 시설이 사라졌을 때 ──

	// 안에 있는 유닛의 배정이 풀렸다(직접 명령·목록 해제) - 나온다. 공간이 없으면 퇴장 대기로 남고 매 틱 다시 시도한다.
	public void ReleaseInsideUnit(Unit u)
	{
		if (u == null || !u.IsInsideFacility) return;
		GameSession session = GameSession.Instance;
		BuildingData b = u.insideFacility;
		RequestInsideExit(u, InternalWorkRules.ExitCause.Released);
		if (session != null && b != null) TryExitFacility(u, b, session);
	}

	// 시설이 파괴·철거·분해되어 격자에서 빠졌다 - 안에 남은 생존 유닛은 해제된 시설 영역에 겹치지 않게 나온다(건설 명세 2-8).
	private void EjectInsideWorkersAtFootprint(BuildingData b, GameSession session)
	{
		if (b == null || session == null) return;
		var inside = new List<Unit>(b.InsideWorkers);
		foreach (Unit u in inside)
		{
			if (u == null || u.hp <= 0f || u.insideFacility != b) continue;
			Vector2Int target = u.insideEntryTile;
			bool placed = false;
			// 시설 영역 안에서 몸 전체가 겹치지 않고 서는 자리를 찾는다 - 없으면 가까운 빈 곳으로 나온다.
			for (int dx = 0; dx <= b.Footprint.x - u.FootprintSize.x && !placed; dx++)
			{
				for (int dy = 0; dy <= b.Footprint.y - u.FootprintSize.y && !placed; dy++)
				{
					var c = new Vector2Int(b.Position.x + dx, b.Position.y + dy);
					if (!IsBodyFree(c, u.FootprintSize, b.Position.z, session, u)) continue;
					target = c;
					placed = true;
				}
			}
			if (!placed)
			{
				var origin = new Vector2Int(b.Position.x, b.Position.y);
				foreach (Vector2Int c in WorkSpeedMath.GetOrthogonalAdjacentOrigins(origin, b.Footprint, u.FootprintSize))
				{
					if (!IsBodyFree(c, u.FootprintSize, b.Position.z, session, u)) continue;
					target = c;
					placed = true;
					break;
				}
			}
			if (!placed) continue; // 자리가 없으면 안에 남는다 - 다음 틱에 다시 시도한다(시설이 없으면 TickInsideWorker가 이 함수를 다시 부른다)
			u.insideExitCause = InternalWorkRules.ExitCause.Dismantle; // 시설이 사라진 뒤라 배정 유지/해제는 호출부(철거 완료·파괴)가 이미 정했다
			CompleteExit(u, b, target, session);
		}
	}

	// ── 내부 사망(2-8) ──

	// 내부 작업자가 시설 안에서 죽었다: 시체는 몸 전체가 들어갈 빈 인접 공간으로 배출하고, 공간이 없으면 배출 대기로 남긴다. 시설 안 자리·생존 인구수는 즉시 풀린다.
	// 반환값은 그 시설이고, pending이 false면 u.position이 배출 위치로 바뀐다(GameSession.RemoveDeadUnit이 그 자리에 시체를 만든다).
	public BuildingData ResolveInsideDeath(Unit u, out bool pending)
	{
		pending = false;
		BuildingData b = u.insideFacility;
		if (b == null) return null;
		GameSession session = GameSession.Instance;

		b.InsideWorkers.Remove(u);
		u.insideFacility = null;
		u.insideExitRequested = false;
		_yieldAskedAt.Remove(u);

		if (session != null && TryFindCorpseEjectOrigin(u, b, session, out Vector2Int origin))
		{
			u.position = origin;
			unitGenerate?.SetUnitHiddenInside(u, false); // 시체가 놓이는 자리에서 사망 연출을 보인다
		}
		else
		{
			pending = true;
		}
		return b;
	}

	// 시체(통행 가능한 오브젝트)가 놓일 빈 인접 칸 - 몸 전체가 들어가는 칸이고 다른 오브젝트가 없어야 한다.
	private bool TryFindCorpseEjectOrigin(Unit u, BuildingData b, GameSession session, out Vector2Int result)
	{
		result = u.position;
		var origin = new Vector2Int(b.Position.x, b.Position.y);
		float best = float.MaxValue;
		bool found = false;
		foreach (Vector2Int c in WorkSpeedMath.GetOrthogonalAdjacentOrigins(origin, b.Footprint, u.FootprintSize))
		{
			if (!IsBodyStructurallyOpen(c, u.FootprintSize, b.Position.z, session)) continue;
			if (session.objectGrid.ContainsKey(new Vector3Int(c.x, c.y, b.Position.z))) continue;
			float d = Vector2Int.Distance(c, u.insideEntryTile);
			if (d >= best) continue;
			best = d;
			result = c;
			found = true;
		}
		return found;
	}

	// 배출 대기 중인 작업자 시체 - 정식 보관물이 아니라 부패가 계속되고, 공간이 비면 밖으로 나온다(건설 명세 2-8-표1).
	private float _nextCorpseEjectAt;

	private void TickPendingInsideCorpses(GameSession session, double now)
	{
		float dt = Time.deltaTime;
		bool tryEject = Time.unscaledTime >= _nextCorpseEjectAt;
		if (tryEject) _nextCorpseEjectAt = Time.unscaledTime + 0.5f;

		for (int i = 0; i < _allBuildings.Count; i++)
		{
			BuildingData b = _allBuildings[i];
			if (b == null || b.PendingInsideCorpses.Count == 0) continue;
			for (int k = b.PendingInsideCorpses.Count - 1; k >= 0; k--)
			{
				InteractableObject corpse = b.PendingInsideCorpses[k];
				corpse.CorpseEconomyData?.TickFieldOrCarryDecay(dt);
				if (!tryEject) continue;
				if (!TryEjectPendingCorpse(b, corpse, session)) continue;
				b.PendingInsideCorpses.RemoveAt(k);
			}
		}
	}

	private bool TryEjectPendingCorpse(BuildingData b, InteractableObject corpse, GameSession session)
	{
		var origin = new Vector2Int(b.Position.x, b.Position.y);
		foreach (Vector2Int c in WorkSpeedMath.GetOrthogonalAdjacentOrigins(origin, b.Footprint, Vector2Int.one))
		{
			var t = new Vector3Int(c.x, c.y, b.Position.z);
			if (!IsBodyStructurallyOpen(c, Vector2Int.one, b.Position.z, session) || session.objectGrid.ContainsKey(t)) continue;
			corpse.Position = t;
			session.SpawnObject(corpse, new Color(0.45f, 0.2f, 0.05f));
			return true;
		}
		return false;
	}

	// 분해·철거가 끝났다: 미배출 작업자 시체는 원래 시설 자리에 남는다(필요하면 겹친다)(건설 명세 2-8-표1). 시설이 파괴되면 소멸한다.
	private void ReleasePendingInsideCorpsesAtFootprint(BuildingData b, GameSession session)
	{
		if (b == null || session == null || b.PendingInsideCorpses.Count == 0) return;
		var tiles = new List<Vector3Int>(FootprintTiles(b.Position, b.Footprint));
		int idx = 0;
		foreach (InteractableObject corpse in b.PendingInsideCorpses)
		{
			Vector3Int t = tiles[idx++ % tiles.Count];
			corpse.Position = t;
			if (session.objectGrid.TryGetValue(t, out InteractableObject existing) && existing != null)
			{
				existing.CoexistingObjects ??= new List<InteractableObject>();
				existing.CoexistingObjects.Add(corpse);
			}
			else
			{
				session.SpawnObject(corpse, new Color(0.45f, 0.2f, 0.05f));
			}
		}
		b.PendingInsideCorpses.Clear();
	}

	// ── 분해·철거 착수 전 퇴장(2-5·2-6) ──

	// 실제 분해·철거를 시작하려면 살아 있는 내부 작업자가 먼저 나와야 한다. 나오지 못하면 착수하지 않고 퇴장 대기를 유지한다. 분해는 배정을 유지하고 철거는 해제한다.
	private bool BlockUntilInsideWorkersExit(BuildingData b, InternalWorkRules.ExitCause cause)
	{
		if (CountInsideWorkers(b) == 0) return false;
		foreach (Unit u in b.InsideWorkers)
			if (u != null) RequestInsideExit(u, cause);
		b.WaitReason = "내부 작업자 퇴장 대기";
		b.FieldWorkActive = false;
		return true;
	}

	// 코어 파괴·현장 작업 같은 상태 설명(작업 현황·시설 UI).
	public string DescribeInternalState(BuildingData b)
	{
		if (b == null || !InternalWorkRules.IsInternalWorkKind(b.Kind)) return string.Empty;
		int assigned = CountInternalWorkers(b);
		int inside = CountInsideWorkers(b);
		int cap = InternalWorkRules.GetCapacity(b.Kind);
		if (assigned == 0 && inside == 0) return string.Empty;
		return $"내부 작업자 {inside}/{cap} (배정 {assigned})";
	}
}
