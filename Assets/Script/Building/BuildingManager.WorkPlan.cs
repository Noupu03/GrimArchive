using System.Collections.Generic;
using UnityEngine;

// 작업자 목록 한 줄(건설 명세 1-2-표1)
public struct WorkerCandidate
{
	public Unit Unit;
	public string RoomName;
	public string Activity;       // 현재 작업 또는 대기
	public float ActivityProgress; // 0~1, 없으면 -1
	public double EstimatedSeconds; // 배정 전: 이 일의 예상 시간(이동·대기 제외)
	public bool IsAssigned;
	public string BlockReason;    // null이면 선택 가능
	public int QueuedCount;       // 이후 맡은 건설 수(연속 건설)
	public double QueueTotalSeconds; // 현재 작업을 포함한 총 남은 작업 시간
	public float Speed;
	public bool ShowCombatSetting; // 내부 작업자로 배정된 행 - 전투 대응/무시 설정을 보여 준다(건설 명세 1-6-표1)
	public InternalCombatSetting Setting;
}

// 작업 현황 한 줄(건설 명세 1-8-표1)
public struct WorkStatusRow
{
	public BuildingData Building;
	public string RoomName;
	public string Target;
	public string Task;
	public float Progress;
	public double RemainingSeconds;
	public int Assigned;
	public int Working;
	public string Wait;
	public InteractableObject Door; // 문 확장 행이면 그 문(Building은 null) - 눌러 문 UI를 연다
	public bool Internal; // 시설 내부 작업(생명 에너지 채취·연구) 행 - 끝나는 시각이 아니라 한 주기 진행을 보인다
}

// 건설 설계명세서 v0.2 2단계 — 작업자 목록·연속 건설 큐·계획 위치 변경·작업 현황(1-2·1-8·2-1·2-11).
public partial class BuildingManager
{
	// ── 연속 건설(2-1·2-11) ──
	// 같은 종류의 미리보기를 유지하며 놓은 계획은 첫 계획의 담당자 순서 끝에 붙는다. 다른 인원은 자동 선발하지 않는다.
	private readonly List<Unit> _continuousWorkers = new List<Unit>();
	private readonly List<BuildingData> _sessionPlans = new List<BuildingData>();
	private bool _continuousActive;

	public void BeginContinuousSession()
	{
		_continuousActive = true;
		_continuousWorkers.Clear();
		_sessionPlans.Clear();
	}

	// ESC 또는 다른 UI 선택으로 미리보기가 끝나도 이미 확정한 순서와 담당자는 유지한다.
	public void EndContinuousSession()
	{
		_continuousActive = false;
		_continuousWorkers.Clear();
		_sessionPlans.Clear();
	}

	private bool IsBusyWithConstruction(Unit w)
	{
		BuildingData cur = w.assignedWorkBuilding;
		return cur != null && cur.ActiveFieldTask == FacilityFieldTaskKind.Construction && _allBuildings.Contains(cur);
	}

	private void OnPlanOrdered(BuildingData plan)
	{
		if (!_continuousActive) return;
		_sessionPlans.Add(plan);
		if (_sessionPlans.Count == 1) return; // 첫 계획 - 담당자는 작업자 목록에서 지정한다
		for (int i = _continuousWorkers.Count - 1; i >= 0; i--)
		{
			Unit w = _continuousWorkers[i];
			if (w == null || w.hp <= 0f) { _continuousWorkers.RemoveAt(i); continue; }
			if (IsBusyWithConstruction(w)) AppendPlanToQueue(w, plan);
			else TryAssignWorkerToBuilding(w, plan);
		}
	}

	private static void AppendPlanToQueue(Unit w, BuildingData plan)
	{
		w.workQueue ??= new List<BuildingData>();
		if (w.assignedWorkBuilding != plan && !w.workQueue.Contains(plan))
			w.workQueue.Add(plan);
	}

	// 현재 건설이 끝나거나 취소·파괴되면 순서의 다음 건설로 넘어간다. 공간·경로가 막혀도 뒤 건설로 건너뛰지 않는다(그 차례에서 기다린다).
	private void AdvanceWorkQueue(Unit w, BuildingData finished, bool notifyEnd)
	{
		w.workQueue?.Remove(finished);
		if (w.assignedWorkBuilding == finished) { w.assignedWorkBuilding = null; w.assignedWorkIsInternal = false; }
		ReleaseWorkPriority(w); // 작업 우선은 현재 건물 한 채를 끝낼 때 해제한다

		while (w.workQueue != null && w.workQueue.Count > 0)
		{
			BuildingData next = w.workQueue[0];
			w.workQueue.RemoveAt(0);
			if (next == null || !_allBuildings.Contains(next) || next.ActiveFieldTask != FacilityFieldTaskKind.Construction) continue;
			BeginWorkOn(w, next);
			return;
		}
		if (notifyEnd) DefensePlacementSystem.Instance?.OnWorkTaskEnded(w);
	}

	// 명령 훅(NotifyWorkerCommandChanged)을 거치지 않는 배정 - 큐를 지우지 않고 다음 현장으로 이동한다.
	private void BeginWorkOn(Unit worker, BuildingData b)
	{
		worker.assignedWorkBuilding = b;
		worker.assignedWorkIsInternal = false;
		worker.workPriorityActive = false;
		Vector2Int origin = b.Origin2D;
		if (!WorkSpeedMath.IsOrthogonallyAdjacent(origin, b.Footprint, worker.position, worker.FootprintSize)
			&& TryPickNearestAdjacentWorkTile(origin, b.Footprint, worker, out Vector2Int slot))
			IssueInternalEconomyMove(worker, slot);
	}

	// ── 작업자 목록(1-2) ──

	// 외부 현장 작업은 최대 4명, 시설 내부 작업(생명 에너지·연구)은 시설별 정원이다(건설 명세 1-4).
	public static int GetWorkerCap(BuildingData b)
		=> b.IsInternalWorkMode
			? InternalWorkRules.GetCapacity(b.Kind)
			: WorkSpeedMath.MaxWorkersPerTask;

	// 내부 작업 시설에 이 유닛이 배정될 수 있는가 - 정원·몸체·코어·실제 수리 조건. 내부 작업 상태가 아니면 null이다.
	private string GetInternalEntryBlockReason(Unit worker, BuildingData b)
		=> b.IsInternalWorkMode
			? InternalWorkRules.GetEnterBlockReason(b.Kind, CountAssignedWorkers(b), worker.FootprintSize, AreCoreFunctionsActive(b.Position.z), false)
			: null;

	private bool CheckInternalEntryAllowed(Unit worker, BuildingData b)
	{
		string reason = GetInternalEntryBlockReason(worker, b);
		if (reason == null) return true;
		NoticeCenter.Instance?.PushMomentary(reason, NoticeCenter.WarningColor);
		return false;
	}

	public static string DescribeTaskName(BuildingData b)
	{
		switch (b.ActiveFieldTask)
		{
			case FacilityFieldTaskKind.Construction: return "건설";
			case FacilityFieldTaskKind.Upgrade: return "강화";
			case FacilityFieldTaskKind.Repair: return "수리";
			case FacilityFieldTaskKind.Demolish: return "철거";
			case FacilityFieldTaskKind.Relocation: return "이동";
		}
		switch (b.Kind)
		{
			case FacilityKind.LifeEnergy: return "생명 에너지 채취";
			case FacilityKind.Research: return "연구";
			default: return "대기";
		}
	}

	// 이동·대기를 제외한 남은 실제 작업 시간(보정 없는 1명 기준).
	public double GetRemainingWorkSeconds(BuildingData b)
	{
		switch (b.ActiveFieldTask)
		{
			case FacilityFieldTaskKind.Construction:
			case FacilityFieldTaskKind.Upgrade:
			{
				var s = b.ActiveConstructionOrUpgrade;
				return s == null ? 0.0 : System.Math.Max(0.0, s.RequiredWorkSeconds - s.CompletedWorkSeconds);
			}
			case FacilityFieldTaskKind.Repair:
			{
				var r = b.ActiveRepair;
				return r == null ? 0.0 : System.Math.Max(0.0, r.TotalRequiredWorkSeconds - r.CompletedWorkSeconds);
			}
			case FacilityFieldTaskKind.Demolish:
				return System.Math.Max(0.0, b.DemolishRequiredSeconds - b.DemolishCompletedSeconds);
			case FacilityFieldTaskKind.Relocation:
			{
				var rel = b.ActiveRelocation;
				if (rel == null) return 0.0;
				double install = System.Math.Max(0.0, rel.InstallRequiredSeconds - rel.InstallCompletedSeconds);
				if (b.ReinstallBodyPlaced) return install;
				return System.Math.Max(0.0, rel.DismantleRequiredSeconds - rel.DismantleCompletedSeconds) + install;
			}
		}
		return 0.0;
	}

	public float GetTaskProgress01(BuildingData b)
	{
		switch (b.ActiveFieldTask)
		{
			case FacilityFieldTaskKind.Construction:
			case FacilityFieldTaskKind.Upgrade:
				return (float)(b.ActiveConstructionOrUpgrade?.Progress01 ?? 0.0);
			case FacilityFieldTaskKind.Repair:
				return (float)(b.ActiveRepair?.Progress01 ?? 0.0);
			case FacilityFieldTaskKind.Demolish:
				return b.DemolishRequiredSeconds > 0.0 ? Mathf.Clamp01((float)(b.DemolishCompletedSeconds / b.DemolishRequiredSeconds)) : 1f;
			case FacilityFieldTaskKind.Relocation:
			{
				var rel = b.ActiveRelocation;
				if (rel == null) return 0f;
				double total = rel.DismantleRequiredSeconds + rel.InstallRequiredSeconds;
				double done = rel.DismantleCompletedSeconds + rel.InstallCompletedSeconds;
				return total > 0.0 ? Mathf.Clamp01((float)(done / total)) : 0f;
			}
		}
		return 0f;
	}

	private float WorkerSpeed(Unit u) => Mathf.Max(0.1f, u.workerSpeedMultiplier);

	// 이 유닛을 더했을 때(이미 배정돼 있으면 그대로) 현재 협력 속도로 계산한 남은 작업 시간.
	public double EstimateSecondsWithWorker(BuildingData b, Unit extra)
	{
		double remaining = GetRemainingWorkSeconds(b);
		var speeds = new List<float>(4);
		foreach (Unit u in CollectAssignedWorkers(b))
			if (u != extra) speeds.Add(WorkerSpeed(u));
		if (extra != null) speeds.Add(WorkerSpeed(extra));
		float total = WorkSpeedMath.CalculateTotalWorkSpeed(speeds);
		return total > 0f ? remaining / total : remaining;
	}

	// 배정 전 생존 아군·작업 자격·빈 자리를 확인한다. null이면 선택 가능(건설 명세 1-2). 경로 사전 검사는 실제 이동 시 정체 처리가 맡는다.
	public string GetAssignBlockReason(Unit worker, BuildingData b)
	{
		if (worker == null || worker.hp <= 0f) return "사망";
		if (!worker.IsPlayerMonsterFaction) return "아군이 아님";
		if (worker.IsInEvolutionPresentation) return "진화 연출 중";
		if (b == null || !DoesBuildingAcceptWorkers(b)) return "배정할 수 있는 작업이 없음";
		if (worker.currentFloor != b.Position.z) return "다른 층";
		if (b.IsWorkerInCurrentRole(worker)) return null; // 이미 이 역할로 배정됨 - 다시 선택하면 해제
		string internalReason = GetInternalEntryBlockReason(worker, b);
		if (internalReason != null) return internalReason;
		if (CountAssignedWorkers(b) >= GetWorkerCap(b)) return $"빈 자리 없음(최대 {GetWorkerCap(b)}명)";
		var origin = b.Origin2D;
		if (WorkSpeedMath.GetOrthogonalAdjacentOrigins(origin, b.Footprint, worker.FootprintSize).Count == 0) return "몸체가 커서 작업 자리가 없음";
		return null;
	}

	public List<WorkerCandidate> BuildWorkerCandidates(BuildingData b)
	{
		var result = new List<WorkerCandidate>();
		GameSession session = GameSession.Instance;
		if (b == null || session?.units == null) return result;

		var origin = b.Origin2D;
		session.roomGrid.TryGetValue(b.Position, out Room targetRoom);

		var keyed = new List<(float key, WorkerCandidate c)>();
		foreach (Unit u in session.units)
		{
			if (u == null || u.hp <= 0f || !u.IsPlayerMonsterFaction || u.currentFloor != b.Position.z) continue;

			Room room = u.currentRoom;
			var c = new WorkerCandidate
			{
				Unit = u,
				RoomName = room != null ? room.RoomName : "통로",
				IsAssigned = b.IsWorkerInCurrentRole(u),
				BlockReason = GetAssignBlockReason(u, b),
				Speed = WorkerSpeed(u),
				ActivityProgress = -1f,
				EstimatedSeconds = EstimateSecondsWithWorker(b, u),
			};

			BuildingData cur = u.assignedWorkBuilding;
			if (cur != null)
			{
				c.Activity = $"{DescribeTaskName(cur)} · {cur.DisplayName}";
				c.ActivityProgress = cur.ActiveFieldTask != FacilityFieldTaskKind.None ? GetTaskProgress01(cur) : -1f;
			}
			else if (u.assignedCoreRestorePos.HasValue) c.Activity = "코어 수복";
			else if (u.carriedCorpse != null || u.pendingCorpsePickupTarget != null) c.Activity = "시체 운반";
			else if (u.carriedDismantledBuilding != null || u.pendingDismantledPackagePickup != null) c.Activity = "분해 시설 운반";
			else c.Activity = "대기";
			if (u.assignedWorkBuilding == b && u.assignedWorkIsInternal)
			{
				c.Setting = u.internalCombatSetting;
				c.ShowCombatSetting = true;
				if (u.IsInsideFacility) c.Activity += u.insideExitRequested ? " · 퇴장 대기" : " · 시설 안";
				else
				{
					string entryWait = DescribeEntryWait(u, b);
					if (entryWait != null) c.Activity += " · 입장 대기: " + entryWait;
				}
			}

			// 연속 건설: 이후 맡은 건설 수와 현재 작업을 포함한 총 남은 작업 시간(이동·대기 제외)
			if (u.workQueue != null && u.workQueue.Count > 0)
			{
				double total = 0.0;
				if (cur != null && cur.ActiveFieldTask != FacilityFieldTaskKind.None) total += GetRemainingWorkSeconds(cur) / c.Speed;
				foreach (BuildingData q in u.workQueue)
					if (q != null && q.ActiveFieldTask != FacilityFieldTaskKind.None) { total += GetRemainingWorkSeconds(q) / c.Speed; c.QueuedCount++; }
				c.QueueTotalSeconds = total;
			}

			// 가까운 방부터, 같은 방에서는 이 작업을 잘하는(작업 속도가 높은) 인원 우선
			float roomKey = room != null && room == targetRoom ? 0f : 1000f + Vector2Int.Distance(u.position, origin);
			keyed.Add((roomKey - c.Speed * 0.01f, c));
		}
		keyed.Sort((a, bb) => a.key.CompareTo(bb.key));
		foreach (var k in keyed) result.Add(k.c);
		return result;
	}

	// 작업자 목록에서 한 줄을 눌렀다: 배정되지 않은 인원은 추가, 이미 배정된 인원은 그 한 명만 해제(건설 명세 1-2-표2).
	public bool ToggleWorkerFromList(Unit worker, BuildingData b)
	{
		if (worker == null || b == null) return false;

		if (b.IsWorkerInCurrentRole(worker))
		{
			ReleaseWorkerFromBuilding(worker, b);
			return true;
		}

		string reason = GetAssignBlockReason(worker, b);
		if (reason != null)
		{
			NoticeCenter.Instance?.PushMomentary($"배정할 수 없습니다: {reason}", NoticeCenter.WarningColor);
			return false;
		}

		// 대기 중인 계획을 다른 인원에게 맡기면 기존 담당자의 미래 순서에서 그 계획을 뺀다(2-11-표1).
		if (b.ActiveFieldTask == FacilityFieldTaskKind.Construction && b.ActiveConstructionOrUpgrade != null && !b.ActiveConstructionOrUpgrade.HasStartedWork)
			foreach (Unit u in GameSession.Instance.units)
				if (u != null && u != worker && u.workQueue != null) u.workQueue.Remove(b);

		if (!TryAssignWorkerToBuilding(worker, b)) return false;

		// 첫 계획의 담당자가 연속 건설의 담당자가 된다 - 그 사이 놓은 뒤 계획도 순서에 붙인다.
		if (_continuousActive && _sessionPlans.Count > 0 && _sessionPlans[0] == b && !_continuousWorkers.Contains(worker))
		{
			_continuousWorkers.Add(worker);
			for (int i = 1; i < _sessionPlans.Count; i++)
				if (_sessionPlans[i] != null && _allBuildings.Contains(_sessionPlans[i])) AppendPlanToQueue(worker, _sessionPlans[i]);
		}
		return true;
	}

	// 선택한 한 명만 해제 - 남은 인원·작업량·선점·지불은 유지하고 새 인원을 자동 배정하지 않는다.
	public void ReleaseWorkerFromBuilding(Unit worker, BuildingData b)
	{
		if (worker == null || worker.assignedWorkBuilding != b) return;
		if (b.ActiveFieldTask == FacilityFieldTaskKind.Relocation && b.ActiveRelocation != null && !b.ActiveRelocation.IsDismantleFinished && !worker.assignedWorkIsInternal)
			b.ActiveRelocation.OnWorkerMovedAwayDuringDismantle();
		worker.assignedWorkBuilding = null;
		worker.assignedWorkIsInternal = false;
		ReleaseInsideUnit(worker); // 시설 안에서 일하던 인원은 나온다
		worker.workQueue?.Remove(b);
		ReleaseWorkPriority(worker);
		_continuousWorkers.Remove(worker);
		DefensePlacementSystem.Instance?.OnWorkTaskEnded(worker); // 저장된 방어 위치로 복귀하거나 미배치 대기
	}

	// ── 계획 위치 변경(2-1-표1: 시작 전 위치 변경) ──
	public bool CanMovePlan(BuildingData plan)
		=> plan != null && plan.ActiveFieldTask == FacilityFieldTaskKind.Construction
		&& plan.ActiveConstructionOrUpgrade != null && !plan.ActiveConstructionOrUpgrade.HasStartedWork
		&& !plan.IsFormallyInstalled;

	public bool TryMovePlan(BuildingData plan, Vector3Int newPos)
	{
		if (!CanMovePlan(plan)) return false;
		if (newPos.z != plan.Position.z) return false;
		BuildBlock block = CheckInstallAt(newPos, plan.Footprint, requireOwnership: true, kind: plan.Kind, ignore: plan);
		if (block != BuildBlock.None)
		{
			NoticeCenter.Instance?.PushMomentary(DescribeBlock(block), NoticeCenter.WarningColor);
			return false;
		}

		UnregisterBuildingFromGrid(plan, clearObstacle: false);
		plan.Position = newPos;
		if (plan.VisualObject != null)
		{
			Vector3 floorOffset = unitGenerate != null ? unitGenerate.GetFloorOffset(newPos.z) : Vector3.zero;
			plan.VisualObject.transform.position = new Vector3(newPos.x + plan.Footprint.x / 2f, newPos.y + plan.Footprint.y / 2f, 0f) + floorOffset;
		}
		RegisterBuildingToGrid(plan, updateObstacle: false);

		// 배정 작업자의 목적지를 갱신한다 - 이미 새 자리에 붙어 있으면 그대로 일한다.
		foreach (Unit w in CollectAssignedWorkers(plan))
		{
			Vector2Int o = new Vector2Int(newPos.x, newPos.y);
			if (!WorkSpeedMath.IsOrthogonallyAdjacent(o, plan.Footprint, w.position, w.FootprintSize)
				&& TryPickNearestAdjacentWorkTile(o, plan.Footprint, w, out Vector2Int slot))
				IssueInternalEconomyMove(w, slot);
		}
		return true;
	}

	// ── 시체·분해물 반출(2-1-표2) ──
	private static bool IsLooseCorpseOrPackage(InteractableObject o)
	{
		if (o == null) return false;
		if (o.CorpseEconomyData != null || o.DismantledBuildingPayload != null) return true;
		if (o.CoexistingObjects != null)
			for (int i = 0; i < o.CoexistingObjects.Count; i++)
				if (o.CoexistingObjects[i] != null && (o.CoexistingObjects[i].CorpseEconomyData != null || o.CoexistingObjects[i].DismantledBuildingPayload != null)) return true;
		return false;
	}

	// 확정 뒤 떨어진 시체·분해물을 설치 몸체 밖의 가까운 빈 곳으로 반출한다. 다른 운반자가 맡은 것은 중복 반출하지 않고 기다린다.
	private void EjectLooseObjects(BuildingData b, GameSession session)
	{
		foreach (Vector3Int tile in FootprintTiles(b.Position, b.Footprint))
		{
			if (!session.objectGrid.TryGetValue(tile, out InteractableObject primary) || primary == null) continue;
			var all = new List<InteractableObject> { primary };
			if (primary.CoexistingObjects != null) all.AddRange(primary.CoexistingObjects);

			foreach (InteractableObject o in all)
			{
				if (o == null || (o.CorpseEconomyData == null && o.DismantledBuildingPayload == null)) continue;
				if (IsClaimedByOtherCarrier(o, null, session)) continue;
				if (!TryFindFreeTileOutsideFootprint(b, tile.z, out Vector3Int target)) return;

				ConsumeSingleObjectOrCoexisting(session, o);
				if (o.CorpseEconomyData != null) DropCorpseToField(o.CorpseEconomyData, target, null, null);
				else if (o.DismantledBuildingPayload != null) DropDismantledBuildingPackage(o.DismantledBuildingPayload, target, null);
			}
		}
	}

	// 겹쳐 놓인 시체 중 회수할 한 구를 고른다 - 영혼 파편 총가치 → 생명 에너지 → 먼저 놓인 순(건설 명세 2-8-표2). 정제할 수 없거나 다른 운반자가 맡은 시체는 제외한다.
	public InteractableObject PickFloorCorpseAt(InteractableObject primary, Unit forCarrier)
	{
		if (primary == null) return null;
		GameSession session = GameSession.Instance;
		var objects = new List<InteractableObject>();
		var records = new List<CorpseRecord>();
		var all = new List<InteractableObject> { primary };
		if (primary.CoexistingObjects != null) all.AddRange(primary.CoexistingObjects);
		foreach (InteractableObject o in all)
		{
			if (o == null || o.IsCollected || o.CorpseEconomyData == null || !o.CorpseEconomyData.CanBeRefined) continue;
			if (session != null && IsClaimedByOtherCarrier(o, forCarrier, session)) continue;
			objects.Add(o);
			records.Add(o.CorpseEconomyData);
		}
		int best = FloorCorpseOrder.PickBestIndex(records);
		return best >= 0 ? objects[best] : null;
	}

	private static bool IsClaimedByOtherCarrier(InteractableObject o, Unit self, GameSession session)
	{
		for (int i = 0; i < session.units.Count; i++)
		{
			Unit u = session.units[i];
			if (u != null && u != self && u.hp > 0f && (u.pendingCorpsePickupTarget == o || u.pendingDismantledPackagePickup == o)) return true;
		}
		return false;
	}

	// 반출 목적지는 몸체에서 두 칸 이상 떨어진 빈 칸이라 떨어뜨릴 때 다시 몸체 안으로 밀리지 않는다.
	private bool TryFindFreeTileOutsideFootprint(BuildingData b, int floor, out Vector3Int result)
	{
		result = b.Position;
		GameSession session = GameSession.Instance;
		int minX = b.Position.x - 2, maxX = b.Position.x + b.Footprint.x + 1;
		int minY = b.Position.y - 2, maxY = b.Position.y + b.Footprint.y + 1;
		for (int r = 0; r <= 8; r++)
		{
			for (int x = minX - r; x <= maxX + r; x++)
			{
				for (int y = minY - r; y <= maxY + r; y++)
				{
					bool onRing = x == minX - r || x == maxX + r || y == minY - r || y == maxY + r;
					if (!onRing) continue;
					Vector3Int t = new Vector3Int(x, y, floor);
					if (buildingGrid.ContainsKey(t) || session.objectGrid.ContainsKey(t) || session.unitGrid.ContainsKey(t)) continue;
					if (createMap != null && !createMap.IsStaticTileWalkable(floor, new Vector2Int(x, y))) continue;
					result = t;
					return true;
				}
			}
		}
		return false;
	}

	// ── 공간 확보: 대기 중인 아군은 비켜 준다(2-1-표2) ──
	private void RequestAlliesToMakeWay(BuildingData b, GameSession session)
	{
		double now = session.ElapsedGameSeconds;
		if (now - b.LastMakeWayAt < 1.0) return;
		b.LastMakeWayAt = now;
		EjectLooseObjects(b, session);

		foreach (Vector3Int tile in FootprintTiles(b.Position, b.Footprint))
		{
			if (!session.unitGrid.TryGetValue(tile, out Unit u) || u == null || u.hp <= 0f || !u.IsPlayerMonsterFaction) continue;
			// 작업·전투·이동 중인 유닛의 행동은 강제로 취소하지 않는다 - 착공만 기다린다.
			if (u.assignedWorkBuilding != null || IsBusyOrFighting(u)) continue;
			if (TryFindNearbyFreeBody(u, session, null, out Vector2Int free))
				IssueInternalEconomyMove(u, free);
		}
	}

	// ── 대기 사유(1-8-표1) ──
	private string ComputeWaitReason(BuildingData b, int workingCount)
	{
		if (b.ActiveFieldTask == FacilityFieldTaskKind.None) return string.Empty;
		int assigned = CountAssignedWorkers(b);
		if (workingCount > 0 && CountInsideWorkers(b) > 0
			&& (b.ActiveFieldTask == FacilityFieldTaskKind.Demolish || (b.ActiveFieldTask == FacilityFieldTaskKind.Relocation && !b.ReinstallBodyPlaced)))
			return "내부 작업자 퇴장 대기";
		if (b.ActiveFieldTask == FacilityFieldTaskKind.Repair && b.ActiveRepair != null && b.ActiveRepair.IsPausedWaitingDiffApproval)
			return "차액 승인 대기";
		if (b.ActiveFieldTask == FacilityFieldTaskKind.Relocation && b.ReinstallBodyPlaced && assigned == 0)
			return "작업자 없음";
		if (assigned == 0) return "작업자 없음";
		if (b.ActiveFieldTask == FacilityFieldTaskKind.Construction && b.ActiveConstructionOrUpgrade != null
			&& !b.ActiveConstructionOrUpgrade.HasStartedWork && !IsConstructionFootprintClearOfUnits(b))
			return "공간 부족";
		if (workingCount == 0)
		{
			foreach (Unit u in CollectAssignedWorkers(b))
			{
				if (u.IsInCombatOrAlertState) return "전투";
			}
			return "작업자 이동 중";
		}
		return string.Empty;
	}

	// ── 작업 현황(1-8) ──
	public List<WorkStatusRow> BuildWorkStatusRows(int floor)
	{
		var rows = new List<WorkStatusRow>();
		GameSession session = GameSession.Instance;
		for (int i = 0; i < _allBuildings.Count; i++)
		{
			BuildingData b = _allBuildings[i];
			if (b == null || b.IsDummy || b.Position.z != floor) continue;
			bool hasField = b.ActiveFieldTask != FacilityFieldTaskKind.None;
			if (!hasField)
			{
				AddInternalWorkRow(rows, b, session);
				continue;
			}

			int working = 0;
			var speeds = new List<float>(4);
			var origin = b.Origin2D;
			foreach (Unit u in CollectAssignedWorkers(b))
			{
				if (WorkSpeedMath.IsOrthogonallyAdjacent(origin, b.Footprint, u.position, u.FootprintSize)) { working++; speeds.Add(WorkerSpeed(u)); }
			}
			float total = WorkSpeedMath.CalculateTotalWorkSpeed(speeds);
			double remaining = GetRemainingWorkSeconds(b);
			Room room = null;
			session?.roomGrid.TryGetValue(b.Position, out room);

			rows.Add(new WorkStatusRow
			{
				Building = b,
				RoomName = room != null ? room.RoomName : "통로",
				Target = !b.IsFormallyInstalled && b.ActiveFieldTask == FacilityFieldTaskKind.Construction ? $"{b.DisplayName} (건설 예정)" : b.DisplayName,
				Task = DescribeTaskName(b),
				Progress = GetTaskProgress01(b),
				RemainingSeconds = total > 0f ? remaining / total : remaining,
				Assigned = CountAssignedWorkers(b),
				Working = working,
				Wait = b.WaitReason ?? string.Empty,
			});
		}
		AddDoorExpansionRows(rows, floor, session);
		rows.Sort((a, bb) => string.CompareOrdinal(a.RoomName, bb.RoomName));
		return rows;
	}

	// 시설 내부 작업(생명 에너지 채취·연구) 한 줄 - 배정·입장 인원과 정지·대기 사유를 보인다(건설 명세 1-8-표1).
	private void AddInternalWorkRow(List<WorkStatusRow> rows, BuildingData b, GameSession session)
	{
		if (!InternalWorkRules.IsInternalWorkKind(b.Kind) || !b.IsFormallyInstalled) return;
		int assigned = CountInternalWorkers(b);
		int inside = CountInsideWorkers(b);
		if (assigned == 0 && inside == 0) return;

		Room room = null;
		session?.roomGrid.TryGetValue(b.Position, out room);
		float progress = 0f;
		if (b.Kind == FacilityKind.LifeEnergy) progress = Mathf.Clamp01((float)b.LifeEnergyCycle.Progress01);
		else if (b.Kind == FacilityKind.Research && b.ResearchQueue != null && b.ResearchQueue.Entries.Count > 0 && b.ResearchQueue.Entries[0].Definition != null && ResourceManager.Instance != null)
			progress = Mathf.Clamp01((float)ResourceManager.Instance.ResearchLedger.GetProgress01(b.ResearchQueue.Entries[0].Definition.ResearchId));

		string wait = string.Empty;
		if (!AreCoreFunctionsActive(b.Position.z)) wait = "코어 수복 대기";
		else if (b.FieldWorkActive) wait = "수리·강화 등 현장 작업 대기";
		else if (inside == 0) wait = "입장 대기";
		else if (b.InsideWorkers.Exists(w => w != null && w.insideExitRequested)) wait = "퇴장 대기";

		rows.Add(new WorkStatusRow
		{
			Building = b,
			RoomName = room != null ? room.RoomName : "통로",
			Target = b.DisplayName,
			Task = DescribeTaskName(b) + " (시설 내부)",
			Progress = progress,
			RemainingSeconds = 0.0,
			Assigned = assigned,
			Working = inside,
			Wait = wait,
			Internal = true,
		});
	}
}
