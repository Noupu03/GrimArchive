using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 리더의 문 파괴 지시 — 돌파 단계(AdvancePhase.Breaching)에서 "공격 자리가 남는 만큼 최대한 많은 인원"이 문에 달라붙게 한다(사용자 확정 2026-10-01).
// 문은 방 쪽 줄(1×2)마다 오브젝트 하나라 파괴 대상은 near → far 두 행뿐이고, 한 행의 문 타일 어느 칸에든 인접 1칸이면 채널링할 수 있다(UnitFunction.OnUpdate).
// 이 클래스는 ① 지금 부술 행 판정 ② 공격 자리 계산·배정(안정적으로 유지하며 주기적으로 재지시) ③ 진행 감시를 맡고, 지시받은 유닛의 한 틱은 PartyAdvanceSteps.StepBreach가 수행한다.
public static class PartyBreachCommand
{
	public enum Outcome { Continue, Opened, Stalled }

	// 재지시 주기 — 자리가 비는 것(포기·이탈)을 주워 담는 용도(내부 판단).
	private const float ReassignSeconds = 3f;
	// 지시받은 자리에 이 시간(초) 넘게 못 가면 그 자리를 포기하고 재지시를 요청한다(내부 판단 — ReassignSeconds와 같은 기준).
	public const float SlotRejectSeconds = 3f;

	// ── 문 상태 판정 ─────────────────────────────────────────────────────────────────────

	// 한 행(줄)이 막혀 있는가 — 실제 통행과 같은 기준(DoorSystem.IsBlockedByClosedDoor), 대표로 리더를 쓴다(인류 전원이 같은 진영). 문이 줄당 하나라 어느 타일이든 같은 결과다.
	private static bool RowBlocked(PartyAdvancePlan plan, GameSession session, Human representative, int row)
	{
		foreach (var t in plan.RowTiles(row))
		{
			if (session.IsBlockedByClosedDoor(new Vector3Int(t.x, t.y, plan.Floor), representative)) return true;
		}
		return false;
	}

	// 다음에 부술 행(0 near / 1 far / -1 둘 다 열림 = 진입로 확보).
	public static int ResolveRow(PartyAdvancePlan plan, GameSession session, Human representative)
		=> PartyFormationMath.NextBreachRow(RowBlocked(plan, session, representative, 0), RowBlocked(plan, session, representative, 1));

	public static bool IsPassable(PartyAdvancePlan plan, GameSession session, Human representative)
		=> ResolveRow(plan, session, representative) < 0;

	public static int BlockedRowCount(PartyAdvancePlan plan, GameSession session, Human representative)
		=> (RowBlocked(plan, session, representative, 0) ? 1 : 0) + (RowBlocked(plan, session, representative, 1) ? 1 : 0);

	private static float RowDoorHp(PartyAdvancePlan plan, GameSession session, int row)
	{
		var t = plan.RowTiles(row)[0];
		return session.objectGrid.TryGetValue(new Vector3Int(t.x, t.y, plan.Floor), out var obj) && obj != null && obj.DoorHp > 0f ? obj.DoorHp : 0f;
	}

	// ── 한 틱 ────────────────────────────────────────────────────────────────────────────

	// PartyAdvanceSystem.TickBreaching이 0.25초 간격으로 호출한다. Opened = 진입로 확보(입장 단계로), Stalled = 진행이 없어 계획을 포기해야 함.
	public static Outcome Tick(Party party, PartyAdvancePlan plan, GameSession session, Human leader, float now)
	{
		int row = ResolveRow(plan, session, leader);
		if (row < 0) return Outcome.Opened;

		if (row != plan.BreachRow)
		{
			// 새 행(near → far) — 공격 자리가 다르므로 지시를 새로 내고, 이전 행에서 막혀 포기했던 자리 기억도 비운다. 진행 시계도 새로 시작.
			plan.BreachRow = row;
			plan.LastProgressTime = now;
			plan.LastDoorHp = float.MaxValue;
			plan.AssignDirty = true;
			plan.AttackSlots.Clear();
			foreach (var m in plan.Ranks.Keys)
				if (m.currentWait != null) m.currentWait.RejectedSlots = null;
		}

		if (plan.AssignDirty || now >= plan.NextAssignTime) Assign(party, plan, session, leader, now);

		// 진행 감시 — 목표 행 문 체력이 줄거나 행이 바뀌면 진행이다. 접근 불가·무한 대치로 doorApproachMaxBlockedSeconds 넘게 진행이 없으면 Stalled.
		float hp = RowDoorHp(plan, session, row);
		if (hp < plan.LastDoorHp - 0.001f) plan.LastProgressTime = now;
		plan.LastDoorHp = hp;
		float limit = AIConfigLoader.Behavior?.doorApproachMaxBlockedSeconds ?? 30f;
		return now - plan.LastProgressTime >= limit ? Outcome.Stalled : Outcome.Continue;
	}

	// ── 지시: 공격 자리 계산·배정 ───────────────────────────────────────────────────────────

	// 우선순위(작을수록 먼저): 근접 0 → 원거리 1 → 리더 2 — 자리가 모자라면 앞줄에 설 근접부터 차지한다.
	private static int BreachPriority(int rank)
		=> rank == PartyFormationMath.LeaderRank ? 2 : (rank == 0 ? 0 : 1);

	private static void Assign(Party party, PartyAdvancePlan plan, GameSession session, Human leader, float now)
	{
		plan.AssignDirty = false;
		plan.NextAssignTime = now + ReassignSeconds;

		int row = plan.BreachRow;
		// 서 있을 수 있는 인접 칸 전부 — 자리가 남는 만큼만 투입한다(닫힌 적 문 칸·벽은 CanMove가 걸러내고, 문 줄 너머 칸은 plan.Forward로 뺀다 — CanMove는 칸 하나만 봐서 닫힌 far 문 너머도 통과시킨다).
		var slots = PartyFormationMath.AttackSlotsAround(plan.RowTiles(row), t => leader.CanMove(t, ignoreUnits: true), plan.Forward);

		// 후보 — 돌파 중이고 자리 이동을 포기하지 않은 파티원 전부(근접이든 원거리든 리더든).
		var candidates = new List<Human>();
		foreach (var m in plan.Ranks.Keys)
		{
			var w = m.currentWait;
			if (w != null && w.Reason == WaitReason.BreachingDoor && !w.IsParked) candidates.Add(m);
		}

		// 안정성 — 이미 받은 자리가 아직 유효하면 그대로 두고, 새 인원·자리를 잃은 인원만 남은 자리에 넣는다(매번 갈아엎어 우왕좌왕하지 않게).
		var kept = new Dictionary<Human, Vector2Int>();
		var taken = new HashSet<Vector2Int>();
		foreach (var m in candidates)
		{
			if (plan.AttackSlots.TryGetValue(m, out var s) && slots.Contains(s) && !taken.Contains(s) && !IsRejected(m, s))
			{
				kept[m] = s;
				taken.Add(s);
			}
		}

		var freeSlots = slots.FindAll(s => !taken.Contains(s));
		var unassigned = candidates.FindAll(m => !kept.ContainsKey(m));
		var positions = new List<Vector2Int>(unassigned.Count);
		var priorities = new List<int>(unassigned.Count);
		foreach (var m in unassigned)
		{
			positions.Add(m.position);
			priorities.Add(BreachPriority(plan.Ranks[m]));
		}
		int[] pick = PartyFormationMath.AssignNearest(positions, priorities, freeSlots, (mi, si) => !IsRejected(unassigned[mi], freeSlots[si]));

		plan.AttackSlots.Clear();
		foreach (var kv in kept) plan.AttackSlots[kv.Key] = kv.Value;
		for (int i = 0; i < unassigned.Count; i++)
			if (pick[i] >= 0) plan.AttackSlots[unassigned[i]] = freeSlots[pick[i]];

		int count = plan.AttackSlots.Count;
		if (plan.LastLoggedRow != row || plan.LastLoggedCount != count)
		{
			plan.LastLoggedRow = row;
			plan.LastLoggedCount = count;
			var door = plan.BreachTile.Value;
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} 리더 {leader.name}의 지시 — {count}명이 {(row == 0 ? "가까운 쪽" : "먼 쪽")} 문 ({door.x},{door.y})에 달라붙어 파괴(공격 자리 {slots.Count}개, 자리 없어 대기 {candidates.Count - count}명)");
		}
	}

	private static bool IsRejected(Human m, Vector2Int slot)
		=> m.currentWait?.RejectedSlots != null && m.currentWait.RejectedSlots.Contains(slot);

	// 지시받은 유닛이 자리에 오래 못 가 포기했다 — 그 자리를 이 유닛에게는 다시 주지 않고(RejectedSlots, 행이 바뀌면 비움) 다음 재지시에서 다른 자리·인원으로 메운다.
	public static void ReleaseSlot(PartyAdvancePlan plan, Human human, WaitState wait, Vector2Int slot)
	{
		(wait.RejectedSlots ??= new HashSet<Vector2Int>()).Add(slot);
		plan.AttackSlots.Remove(human);
		plan.AssignDirty = true;
		wait.BlockedSince = -1f;
		human.waitStuckTurns = 0;
	}
}
