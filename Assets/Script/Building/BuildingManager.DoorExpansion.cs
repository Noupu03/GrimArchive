using System.Collections.Generic;
using UnityEngine;

// 문 확장 작업 하나(건설 명세 2-9) - 문(방 쪽 줄 묶음)·확장 계획(양 끝 벽)·순수 규칙(DoorExpansionSession)·시작 전 선점액.
public sealed class DoorExpansionTask
{
	public InteractableObject Door;
	public DoorSystem.GateExpansionPlan Plan;
	public DoorExpansionSession Session;
	public int ReservedCost;                     // 시작 전 선점액 - 첫 실제 작업에서 한 번 차감하고 0이 된다
	public string WaitReason = string.Empty;     // 작업 현황·UI용 대기 사유(비어 있으면 진행 중)
}

// 문 확장 후보 한 줄(작업자 목록)
public struct DoorWorkerCandidate
{
	public Unit Unit;
	public string RoomName;
	public bool IsAssigned;
	public string BlockReason; // null이면 선택 가능
	public float Speed;
}

// 건설 설계명세서 v0.2 2-9·2-12·1-7 - 문 확장 작업: 문 양 끝 벽을 각각 작업량 50%로 열고, 한 명도 한쪽을 마치고 반대쪽으로 가서 완료할 수 있다.
// 비용은 등록 때 선점하고 첫 실제 작업에서 한 번 차감하며, 그때 문이 열린다. 직접 취소하면 환급하고 공사 중 문이 파괴되면 환급·확장이 없다. 확보한 방에서는 코어가 파괴돼도 가능하다.
// 비용·시간·폭·HP 배율은 문 종류 문서가 정할 자리표시자다.
public partial class BuildingManager
{
	public const int DoorExpansionCost = 100;               // 생명 에너지 자리표시자
	public const double DoorExpansionWorkSeconds = 12.0;    // 보정 없는 1명 기준 양 끝 합계 자리표시자
	public const float DoorExpansionHpMultiplier = 1.5f;    // 문서 예시값(120/200 -> 180/300)

	private readonly List<DoorExpansionTask> _doorExpansions = new List<DoorExpansionTask>();
	public IReadOnlyList<DoorExpansionTask> DoorExpansions => _doorExpansions;

	public DoorExpansionTask GetDoorExpansionOf(InteractableObject door)
	{
		if (door == null) return null;
		foreach (DoorExpansionTask t in _doorExpansions)
			if (t.Door == door) return t;
		return null;
	}

	// ── 등록 ──

	// null이면 이 문의 확장을 등록할 수 있다.
	public string GetDoorExpansionBlockReason(InteractableObject door, out DoorSystem.GateExpansionPlan plan)
	{
		plan = null;
		GameSession session = GameSession.Instance;
		if (session == null || door == null) return "문 정보를 찾을 수 없습니다.";
		if (!DoorSystem.IsBarrier(door) || DoorSystem.IsFence(door)) return "확장할 수 있는 문이 아닙니다.";
		if (door.DoorOwnerFaction != FactionType.Player) return "플레이어가 소유한 문만 확장할 수 있습니다.";
		if (GetDoorExpansionOf(door) != null) return "이미 확장 작업이 등록되어 있습니다.";
		// 확보한 방에서만 가능하고 담당 코어가 파괴된 상태에서도 가능하다(건설 명세 1-7-표1).
		if (!session.roomGrid.TryGetValue(door.Position, out Room room) || room == null || room.RoomFaction != FactionType.Player) return "확보한 방의 문만 확장할 수 있습니다.";
		if (!session.TryBuildDoorExpansionPlan(door, out plan, out string fail)) return fail;
		ResourceAccount acc = resourceManager?.LifeEnergyAccount;
		if (acc == null || !acc.CanAffordInt(DoorExpansionCost)) return $"생명 에너지가 부족합니다. (필요 {DoorExpansionCost})";
		return null;
	}

	public bool TryRegisterDoorExpansion(InteractableObject door, out string reason)
	{
		reason = GetDoorExpansionBlockReason(door, out DoorSystem.GateExpansionPlan plan);
		if (reason != null) return false;

		ResourceAccount acc = resourceManager.LifeEnergyAccount;
		if (!acc.TryReserve(DoorExpansionCost, out int reserved)) { reason = $"생명 에너지가 부족합니다. (필요 {DoorExpansionCost})"; return false; }

		int curWidth = GameSession.Instance.cmap.map.floors[plan.Floor].gates[plan.GateIndex].width;
		_doorExpansions.Add(new DoorExpansionTask
		{
			Door = door,
			Plan = plan,
			Session = new DoorExpansionSession(curWidth, plan.NewWidth, DoorExpansionHpMultiplier, DoorExpansionCost, DoorExpansionWorkSeconds),
			ReservedCost = reserved,
			WaitReason = "작업자 없음",
		});
		NoticeCenter.Instance?.PushMomentary($"문 확장을 등록했습니다. 작업자를 지정하세요. (폭 {curWidth}→{plan.NewWidth})", NoticeCenter.QuestCompleteColor);
		return true;
	}

	// 직접 취소: 시작 전이면 선점만 풀고, 시작했으면 확장 비용을 돌려준다. 현재 폭·HP는 그대로다(2-9).
	public void CancelDoorExpansion(DoorExpansionTask task)
	{
		if (task == null || !_doorExpansions.Contains(task)) return;
		ResourceAccount acc = resourceManager?.LifeEnergyAccount;
		int refund = task.Session.Cancel();
		if (refund > 0) acc?.RefundPaid(refund);
		if (task.ReservedCost > 0) { acc?.ReleaseReservation(task.ReservedCost); task.ReservedCost = 0; }
		EndDoorExpansion(task);
		NoticeCenter.Instance?.PushMomentary(refund > 0 ? $"문 확장을 취소하고 {refund}을 환급했습니다." : "문 확장을 취소했습니다.", NoticeCenter.WarningColor);
	}

	// 공사 중 문이 파괴됐다: 환급이나 미완료 폭 확장이 없고, 시작 전이면 선점만 푼다.
	private void OnDoorExpansionDoorLost(DoorExpansionTask task)
	{
		task.Session.OnDoorDestroyed();
		if (task.ReservedCost > 0) { resourceManager?.LifeEnergyAccount?.ReleaseReservation(task.ReservedCost); task.ReservedCost = 0; }
		EndDoorExpansion(task);
		NoticeCenter.Instance?.PushMomentary("확장 중이던 문이 파괴되어 문 확장이 끝났습니다.", NoticeCenter.WarningColor);
	}

	private void EndDoorExpansion(DoorExpansionTask task)
	{
		_doorExpansions.Remove(task);
		if (GameSession.Instance?.units == null) return;
		foreach (Unit u in GameSession.Instance.units)
		{
			if (u == null || u.assignedDoorExpansion != task) continue;
			u.assignedDoorExpansion = null;
			if (u.hp > 0f) DefensePlacementSystem.Instance?.OnWorkTaskEnded(u);
		}
	}

	// ── 작업자 ──

	public int CountDoorWorkers(DoorExpansionTask task)
	{
		int n = 0;
		if (GameSession.Instance?.units == null) return 0;
		foreach (Unit u in GameSession.Instance.units)
			if (u != null && u.hp > 0f && u.assignedDoorExpansion == task) n++;
		return n;
	}

	public string GetDoorWorkBlockReason(Unit worker, DoorExpansionTask task)
	{
		if (worker == null || worker.hp <= 0f) return "사망";
		if (!worker.IsPlayerMonsterFaction) return "아군이 아님";
		if (worker.IsInEvolutionPresentation) return "진화 연출 중";
		if (worker.currentFloor != task.Plan.Floor) return "다른 층";
		if (worker.assignedDoorExpansion == task) return null; // 이미 배정됨 - 다시 선택하면 해제
		if (CountDoorWorkers(task) >= WorkSpeedMath.MaxWorkersPerTask) return $"빈 자리 없음(최대 {WorkSpeedMath.MaxWorkersPerTask}명)";
		if (!TryFindDoorWorkSlot(worker, task, 0, out _) && !TryFindDoorWorkSlot(worker, task, 1, out _)) return "몸체가 커서 작업 자리가 없음";
		return null;
	}

	public bool TryAssignWorkerToDoorExpansion(Unit worker, DoorExpansionTask task)
	{
		string reason = GetDoorWorkBlockReason(worker, task);
		if (reason != null || worker.assignedDoorExpansion == task)
		{
			if (reason != null) NoticeCenter.Instance?.PushMomentary($"배정할 수 없습니다: {reason}", NoticeCenter.WarningColor);
			return reason == null;
		}

		NotifyWorkerCommandChanged(worker); // 기존 배정·운반은 새로 선택한 작업자만 정리한다
		worker.assignedDoorExpansion = task;
		worker.doorExpansionEnd = ChooseDoorEnd(task, worker);
		SendWorkerToDoorEnd(worker, task);
		return true;
	}

	public void ReleaseWorkerFromDoorExpansion(Unit worker)
	{
		if (worker == null || worker.assignedDoorExpansion == null) return;
		worker.assignedDoorExpansion = null;
		DefensePlacementSystem.Instance?.OnWorkTaskEnded(worker);
	}

	public bool ToggleDoorWorker(Unit worker, DoorExpansionTask task)
	{
		if (worker.assignedDoorExpansion == task) { ReleaseWorkerFromDoorExpansion(worker); return true; }
		return TryAssignWorkerToDoorExpansion(worker, task);
	}

	public List<DoorWorkerCandidate> BuildDoorWorkerCandidates(DoorExpansionTask task)
	{
		var result = new List<DoorWorkerCandidate>();
		GameSession session = GameSession.Instance;
		if (task == null || session?.units == null) return result;

		var door = new Vector2Int(task.Door.Position.x, task.Door.Position.y);
		var keyed = new List<(float key, DoorWorkerCandidate c)>();
		foreach (Unit u in session.units)
		{
			if (u == null || u.hp <= 0f || !u.IsPlayerMonsterFaction || u.currentFloor != task.Plan.Floor) continue;
			var c = new DoorWorkerCandidate
			{
				Unit = u,
				RoomName = u.currentRoom != null ? u.currentRoom.RoomName : "통로",
				IsAssigned = u.assignedDoorExpansion == task,
				BlockReason = GetDoorWorkBlockReason(u, task),
				Speed = WorkerSpeed(u),
			};
			keyed.Add((Vector2Int.Distance(u.position, door) - c.Speed * 0.01f, c));
		}
		keyed.Sort((a, b) => a.key.CompareTo(b.key));
		foreach (var k in keyed) result.Add(k.c);
		return result;
	}

	private static int OtherEnd(int end) => end == 0 ? 1 : 0;

	// 끝이 아직 안 끝난 쪽 중 일하는 사람이 적은 쪽, 같으면 가까운 쪽(한 명은 한쪽을 마치고 반대쪽으로 간다).
	private int ChooseDoorEnd(DoorExpansionTask task, Unit worker)
	{
		bool done0 = task.Session.IsEndDone(0), done1 = task.Session.IsEndDone(1);
		if (done0 && !done1) return 1;
		if (done1 && !done0) return 0;

		int c0 = 0, c1 = 0;
		foreach (Unit u in GameSession.Instance.units)
		{
			if (u == null || u == worker || u.hp <= 0f || u.assignedDoorExpansion != task) continue;
			if (u.doorExpansionEnd == 0) c0++; else c1++;
		}
		if (c0 != c1) return c0 < c1 ? 0 : 1;

		float d0 = DistanceToEnd(worker, task, 0), d1 = DistanceToEnd(worker, task, 1);
		return d0 <= d1 ? 0 : 1;
	}

	private static float DistanceToEnd(Unit worker, DoorExpansionTask task, int end)
	{
		float best = float.MaxValue;
		foreach (Vector2Int w in task.Plan.EndWalls(end)) best = Mathf.Min(best, Vector2Int.Distance(worker.position, w));
		return best;
	}

	// 끝 벽 타일에 상하좌우로 맞닿는 작업 자리(몸 전체가 들어가는 빈 곳) - 작업자에게 가장 가까운 곳.
	private bool TryFindDoorWorkSlot(Unit worker, DoorExpansionTask task, int end, out Vector2Int slot)
	{
		slot = worker.position;
		float best = float.MaxValue;
		bool found = false;
		foreach (Vector2Int wall in task.Plan.EndWalls(end))
		{
			if (!TryPickNearestAdjacentWorkTile(wall, Vector2Int.one, worker, out Vector2Int s)) continue;
			float d = Vector2Int.Distance(worker.position, s);
			if (d >= best) continue;
			best = d;
			slot = s;
			found = true;
		}
		return found;
	}

	private static bool IsWorkerAtDoorEnd(Unit worker, DoorExpansionTask task, int end)
	{
		foreach (Vector2Int wall in task.Plan.EndWalls(end))
			if (WorkSpeedMath.IsOrthogonallyAdjacent(wall, Vector2Int.one, worker.position, worker.FootprintSize)) return true;
		return false;
	}

	private void SendWorkerToDoorEnd(Unit worker, DoorExpansionTask task)
	{
		if (IsWorkerAtDoorEnd(worker, task, worker.doorExpansionEnd)) return;
		if (TryFindDoorWorkSlot(worker, task, worker.doorExpansionEnd, out Vector2Int slot)) IssueInternalEconomyMove(worker, slot);
	}

	// 전투·경계가 끝나면 문 확장 작업 자리로 돌아온다(건설 명세 1-5와 같은 규칙).
	private void ResumeDoorWorker(Unit u)
	{
		DoorExpansionTask task = u.assignedDoorExpansion;
		if (task == null || u.hp <= 0f || !u.IsPlayerMonsterFaction || u.IsInEvolutionPresentation || u.StatusEffects.State.stunDuration > 0f) return;
		if (task.Session.IsEndDone(u.doorExpansionEnd)) return; // 끝난 쪽이면 TickDoorExpansions가 반대쪽으로 보낸다
		bool atSpot = IsWorkerAtDoorEnd(u, task, u.doorExpansionEnd);
		IFSMState st = u.fsm?.CurrentState;
		bool inCombatOrAlert = st is CombatFSMState || st is TacticalFSMState;
		bool hasOtherTask = u.playerMoveTarget.HasValue || u.isHalted || u.carriedCorpse != null || u.pendingCorpsePickupTarget != null
			|| u.carriedDismantledBuilding != null || u.pendingDismantledPackagePickup != null;
		if (ConstructionRules.ShouldResumeAssignedWork(true, atSpot, inCombatOrAlert, hasOtherTask)) SendWorkerToDoorEnd(u, task);
	}

	// ── 진행 ──

	private void TickDoorExpansions(GameSession session, float dt)
	{
		for (int i = _doorExpansions.Count - 1; i >= 0; i--)
		{
			DoorExpansionTask task = _doorExpansions[i];
			if (task.Door == null || task.Door.IsCollected || !session.objectGrid.TryGetValue(task.Door.Position, out InteractableObject cur) || cur != task.Door)
			{
				OnDoorExpansionDoorLost(task);
				continue;
			}

			bool secured = session.roomGrid.TryGetValue(task.Door.Position, out Room room) && room != null && room.RoomFaction == FactionType.Player;
			var speeds = new[] { new List<float>(), new List<float>() };
			int assigned = 0;
			for (int k = 0; k < session.units.Count; k++)
			{
				Unit w = session.units[k];
				if (w == null || w.hp <= 0f || w.assignedDoorExpansion != task) continue;
				assigned++;
				if (w.StatusEffects.State.stunDuration > 0f || w.IsInEvolutionPresentation) continue;

				int end = w.doorExpansionEnd;
				if (task.Session.IsEndDone(end))
				{
					// 한쪽을 마쳤으면 반대쪽으로 간다 - 한 명도 양쪽을 끝낼 수 있다(2-9).
					if (task.Session.IsEndDone(OtherEnd(end))) continue;
					w.doorExpansionEnd = OtherEnd(end);
					SendWorkerToDoorEnd(w, task);
					continue;
				}
				if (IsWorkerAtDoorEnd(w, task, end)) speeds[end].Add(WorkerSpeed(w));
			}

			if (!secured) { task.WaitReason = "방 미확보"; continue; }
			if (speeds[0].Count + speeds[1].Count == 0) { task.WaitReason = assigned == 0 ? "작업자 없음" : "작업자 이동 중"; continue; }
			task.WaitReason = string.Empty;

			// 첫 실제 작업: 선점액을 한 번 차감하고 문이 열린다(접근·등록만으로는 열리지 않는다).
			if (!task.Session.HasStartedWork)
			{
				ResourceAccount acc = resourceManager?.LifeEnergyAccount;
				if (acc == null || !acc.CommitReservedCost(task.ReservedCost, DoorExpansionCost, out _))
				{
					acc?.ReleaseReservation(task.ReservedCost);
					task.ReservedCost = 0;
					EndDoorExpansion(task);
					NoticeCenter.Instance?.PushMomentary("생명 에너지가 부족해 문 확장을 시작할 수 없어 취소했습니다.", NoticeCenter.WarningColor);
					continue;
				}
				task.ReservedCost = 0;
				session.OpenDoorForWork(task.Door);
			}

			int pot = DoorExpansionCost; // 위에서 실제 계정에서 이미 차감했다 - 세션 규칙이 한 번 지불 처리한다
			for (int end = 0; end < 2; end++)
				if (speeds[end].Count > 0) task.Session.Tick(end, dt * WorkSpeedMath.CalculateTotalWorkSpeed(speeds[end]), ref pot);

			if (task.Session.IsCompleted) CompleteDoorExpansion(task, session);
		}
	}

	private void CompleteDoorExpansion(DoorExpansionTask task, GameSession session)
	{
		session.ApplyDoorExpansion(task.Door, task.Plan, task.Session.MaxHpMultiplier);
		_doorExpansions.Remove(task);
		foreach (Unit u in session.units)
		{
			if (u == null || u.assignedDoorExpansion != task) continue;
			u.assignedDoorExpansion = null;
			if (u.hp > 0f) DefensePlacementSystem.Instance?.OnWorkTaskEnded(u);
		}
		NoticeCenter.Instance?.PushMomentary($"문 확장이 완료되었습니다. (폭 {task.Session.TargetWidth})", NoticeCenter.QuestCompleteColor);
	}

	// ── 작업 현황(1-8) ──
	private void AddDoorExpansionRows(List<WorkStatusRow> rows, int floor, GameSession session)
	{
		foreach (DoorExpansionTask task in _doorExpansions)
		{
			if (task.Plan.Floor != floor) continue;
			Room room = null;
			session?.roomGrid.TryGetValue(task.Door.Position, out room);
			int working = 0;
			if (session?.units != null)
				foreach (Unit u in session.units)
					if (u != null && u.hp > 0f && u.assignedDoorExpansion == task && IsWorkerAtDoorEnd(u, task, u.doorExpansionEnd)) working++;
			double remaining = System.Math.Max(0.0, task.Session.TotalWorkSeconds - task.Session.EndWork[0] - task.Session.EndWork[1]);

			rows.Add(new WorkStatusRow
			{
				Building = null,
				Door = task.Door,
				RoomName = room != null ? room.RoomName : "통로",
				Target = $"문 ({task.Session.CurrentWidth}→{task.Session.TargetWidth}칸)",
				Task = "문 확장",
				Progress = (float)task.Session.Progress01,
				RemainingSeconds = remaining,
				Assigned = CountDoorWorkers(task),
				Working = working,
				Wait = task.WaitReason ?? string.Empty,
			});
		}
	}
}
