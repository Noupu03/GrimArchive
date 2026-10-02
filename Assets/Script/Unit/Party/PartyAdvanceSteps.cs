using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

public enum SeekStatus { Arrived, Progressing, Waiting, Stuck, GaveUp }

// "자리로 이동 → 진행 판정 → 정체 카운트 → 안전장치" 골격 — 집결 자리·진형/입장 자리·돌파 공격 자리 이동이 공유한다. 상태만 돌려주고 반응(재선택·포기·정착)은 호출부가 정한다.
public static class SlotSeek
{
	// 막힘 시계를 새로 시작하는 Step 간격의 하한(초) — 느린 유닛은 행동 주기(1/이동속도)의 3배까지 늘어난다.
	private const float StepGapResetSeconds = 3f;

	// useHold: 한 걸음도 못 움직였을 때 doorApproachHoldRetrySeconds 동안 제자리에서 기다렸다 재시도(진형·돌파). arrivalRadius: 도착으로 보는 거리(진형 1칸, 공격 자리는 정확히 0).
	public static SeekStatus Step(Human human, WaitState wait, Vector2Int slot, bool useHold, int arrivalRadius = PartyFormationMath.ArrivalRadius)
	{
		// 이전 Step과 간격이 크게 벌어졌다 = 그동안 교전·경계·함정 대응·점유 대기로 이 로직이 안 돌았다 — 중단된 시간을 막힘 시계(30초 개별 포기)에 세지 않도록 새로 시작한다(03번 13항).
		float now = Time.time;
		float resetGap = Mathf.Max(StepGapResetSeconds, 3f * OccupancyMath.StepSeconds(human.AppliedWalkSpeed));
		if (wait.LastStepTime >= 0f && now - wait.LastStepTime > resetGap) ResetBlocked(human, wait);
		wait.LastStepTime = now;

		if (AIMovementHelper.ChebyshevDistance(human.position, slot) <= arrivalRadius)
		{
			wait.AtSlot = true;
			ResetBlocked(human, wait);
			return SeekStatus.Arrived;
		}

		wait.AtSlot = false;
		var cfg = AIConfigLoader.Behavior;
		if (useHold && now < wait.NextDoorScanTime) return SeekStatus.Waiting; // 막혀 보류 중 — 다음 재시도까지 제자리

		int distBefore = AIMovementHelper.ChebyshevDistance(human.position, slot);
		bool moved = AIMovementHelper.MoveTowardsPos(human, slot);
		if (AIMovementHelper.ChebyshevDistance(human.position, slot) < distBefore)
		{
			ResetBlocked(human, wait);
			return SeekStatus.Progressing;
		}

		// 거리가 줄지 않았다 — 혼잡일 수 있어 한도까지는 인내한다(집결·조사 이동과 같은 기준).
		if (++human.waitStuckTurns < (cfg?.waitStuckTurnLimit ?? 4)) return SeekStatus.Waiting;
		human.waitStuckTurns = 0;

		if (wait.BlockedSince < 0f) wait.BlockedSince = now;
		else if (now - wait.BlockedSince >= (cfg?.doorApproachMaxBlockedSeconds ?? 30f)) return SeekStatus.GaveUp;

		// 한 걸음도 못 움직였다면 잠시 기다렸다 다시 시도한다. 움직이고는 있었다면(우회·혼잡 셔플) 곧바로 이어 간다.
		if (useHold && !moved) wait.NextDoorScanTime = now + (cfg?.doorApproachHoldRetrySeconds ?? 1f);
		return SeekStatus.Stuck;
	}

	private static void ResetBlocked(Human human, WaitState wait)
	{
		wait.BlockedSince = -1f;
		wait.BlockedLogged = false;
		human.waitStuckTurns = 0;
	}

	// 막힌 자리를 다시 고르지 않도록 기억하고 기억 집합을 돌려준다 — 좁은 길에서 같은 두 자리를 오가던 문제의 해법.
	public static HashSet<Vector2Int> Reject(WaitState wait, Vector2Int slot)
	{
		(wait.RejectedSlots ??= new HashSet<Vector2Int>()).Add(slot);
		return wait.RejectedSlots;
	}

	// 자리를 바꾸거나 선 자리에서 정착한다는 로그 — 막힘 하나당 한 번만 남긴다. alt가 자기 타일이면 "공간이 없어 현재 위치에서 settledVerb".
	public static void LogRetarget(Human human, WaitState wait, string label, Vector2Int slot, Vector2Int alt, string settledVerb)
	{
		if (wait.BlockedLogged) return;
		wait.BlockedLogged = true;
		LogHelper.Log(LogHelper.GAME, alt == human.position
			? $"[파티] {human.name}: {label} 자리 ({slot.x},{slot.y})에 들어갈 공간이 없어 현재 위치 ({alt.x},{alt.y})에서 {settledVerb}"
			: $"[파티] {human.name}: {label} 자리 ({slot.x},{slot.y})가 막혀 근처 ({alt.x},{alt.y})로 바꿉니다");
	}
}

// 집결·진형·돌파·입장에서 유닛 한 명이 한 틱에 하는 일(TacticalFSMState.ExecuteWait가 호출) — 단계 전이·지시는 PartyAdvanceSystem/PartyBreachCommand가 정한다.
public static class PartyAdvanceSteps
{
	// ── 집결 ─────────────────────────────────────────────────────────────────────────────

	// rallyGatherNearbyEnabled — 배정받은 집결 자리로 가서 도착해도 대기를 유지하고, 완료는 Party.CheckRallyComplete가 전원 도착 시 일괄 처리한다. 자리가 막히면 막힌 자리를 기억하고 가장 가까운 구역 안 빈 칸으로 옮기며, 더 못 가면 선 자리에서 집결 처리하고, doorApproachMaxBlockedSeconds 넘게 막히면 그 유닛을 집결에서 뺀다.
	public static BTStatus StepRallyGather(Human human, WaitState wait)
	{
		var party = human.party;
		Vector2Int slot = wait.WaitPosition.Value;
		bool wasAtSlot = wait.AtSlot;

		switch (SlotSeek.Step(human, wait, slot, useHold: false))
		{
			case SeekStatus.Arrived:
				if (!wasAtSlot) party?.CheckRallyComplete();
				return human.currentWait == null ? BTStatus.Success : BTStatus.Running; // 전원 도착이면 위 호출이 대기를 비웠다

			case SeekStatus.GaveUp:
				LogHelper.Log(LogHelper.GAME, $"[파티] {human.name}: 집결 자리 ({slot.x},{slot.y})에 오래 못 가 안전장치로 집결 대기에서 제외합니다");
				human.currentWait = null;
				party?.CheckRallyComplete();
				return BTStatus.Success;

			case SeekStatus.Stuck:
				return RetargetRally(human, wait, party, slot);

			default:
				return BTStatus.Running;
		}
	}

	private static BTStatus RetargetRally(Human human, WaitState wait, Party party, Vector2Int slot)
	{
		if (party == null || !party.RallyPoint.HasValue) return BTStatus.Running;

		var rejected = SlotSeek.Reject(wait, slot);
		var claimed = new HashSet<Vector2Int> { party.RallyPoint.Value };
		foreach (var m in party.Members)
		{
			if (m == null || m == human || m.hp <= 0 || m.currentWait == null || m.currentWait.Reason != WaitReason.AwaitingPartyAtRallyPoint) continue;
			if (m.currentWait.WaitPosition.HasValue) claimed.Add(m.currentWait.WaitPosition.Value);
		}

		if (party.TryPickGatherFallback(human, claimed, rejected, out Vector2Int alt))
		{
			SlotSeek.LogRetarget(human, wait, "집결", slot, alt, "집결 처리합니다");
			wait.WaitPosition = alt;
		}
		else if (party.IsInRallyZone(human.position))
		{
			// 구역 안에 빈 타일이 하나도 없다 — 이 유닛은 더 못 들어가므로 선 자리에서 집결으로 인정하고 파티가 기다리지 않게 한다.
			wait.IsParked = true;
			LogHelper.Log(LogHelper.GAME, $"[파티] {human.name}: 집결 구역 안에 빈 자리가 없어 현재 위치 ({human.position.x},{human.position.y})에서 집결 처리합니다");
			party.CheckRallyComplete();
			return human.currentWait == null ? BTStatus.Success : BTStatus.Running;
		}
		return BTStatus.Running;
	}

	// ── 집결 이후(진형·돌파·입장) ─────────────────────────────────────────────────────────────

	// FormingUpAtDoor/BreachingDoor/EnteringNextRoom 대기마다 매 틱 호출된다. Success = 대기 종료(계획이 없어졌거나 층을 떠남), Running = 계속.
	public static BTStatus StepMember(Human human, WaitState wait)
	{
		var plan = human.party?.AdvancePlan;
		if (plan == null || !plan.Ranks.ContainsKey(human) || human.currentFloor != plan.Floor)
		{
			// 계획이 사라졌다(코어 확인으로 폐기·입장 완료·중단 등) — 전파가 안 닿아 아직 남은 대기를 스스로 푼다.
			human.currentWait = null;
			human.waitStuckTurns = 0;
			return BTStatus.Success;
		}

		// 리더의 지시를 받은 유닛 — 자기 공격 자리로 가서 문에 달라붙는다. 자리를 못 받은 유닛은 아래에서 진형 자리에 대기한다.
		if (wait.Reason == WaitReason.BreachingDoor && plan.AttackSlots.TryGetValue(human, out Vector2Int attackSlot))
			return StepBreach(human, wait, plan, attackSlot);

		// 진입 판정(문 먼 쪽 줄을 지남) — 파티 쪽 Tick을 기다리지 않고 이 유닛이 곧바로 개인 행동으로 돌아간다.
		if (wait.Reason == WaitReason.EnteringNextRoom && PartyAdvanceSystem.ReleaseIfEntered(human, plan)) return BTStatus.Success;

		bool entering = wait.Reason == WaitReason.EnteringNextRoom && wait.Released;
		Vector2Int slot = entering ? wait.WaitPosition.Value : plan.FormSlots[human];
		return StepGoToSlot(human, wait, plan, slot, entering);
	}

	// 문 채널링 — 문의 어느 칸에든 인접 1칸이면 SetAttackObjectTarget만 건다(데미지는 UnitFunction.OnUpdate, 인원 × 고정 초당 데미지라 모일수록 빨리 부서진다). 아직 인접하지 않으면 지시받은 공격 자리로 이동하고, 오래 못 가면 포기해 재지시를 요청한다(PartyBreachCommand.ReleaseSlot).
	private static BTStatus StepBreach(Human human, WaitState wait, PartyAdvancePlan plan, Vector2Int slot)
	{
		Vector3Int? target = plan.BreachTile;
		if (target.HasValue && human.Session != null
			&& human.Session.objectGrid.TryGetValue(target.Value, out var door) && door.IsAdjacentTo(human.position))
		{
			Vector2Int tile = door.NearestTileTo(human.position);
			human.SetAttackObjectTarget(new Vector3Int(tile.x, tile.y, target.Value.z));
			wait.AtSlot = true;
			wait.BlockedSince = -1f;
			human.waitStuckTurns = 0;
			return BTStatus.Running;
		}

		if (human.currentAttackObjectTarget.HasValue) human.ClearAttackObjectTarget(); // 밀려나 인접이 아니면 채널링은 이미 끊긴 것

		switch (SlotSeek.Step(human, wait, slot, useHold: true, arrivalRadius: 0))
		{
			case SeekStatus.Stuck:
				if (Time.time - wait.BlockedSince >= PartyBreachCommand.SlotRejectSeconds) PartyBreachCommand.ReleaseSlot(plan, human, wait, slot);
				break;
			case SeekStatus.GaveUp:
				PartyBreachCommand.ReleaseSlot(plan, human, wait, slot);
				break;
		}
		return BTStatus.Running;
	}

	// 진형 자리(또는 입장 자리)로 이동·대기 — 도착하면 움직이지 않고, 막히면 같은 방 안에서 다른 자리를 점진 확장으로 다시 고른다. doorApproachMaxBlockedSeconds 넘게 막히면 이 유닛만 자리 이동을 포기(IsParked)해 파티 전체가 교착되지 않게 한다.
	private static BTStatus StepGoToSlot(Human human, WaitState wait, PartyAdvancePlan plan, Vector2Int slot, bool entering)
	{
		if (wait.IsParked) return BTStatus.Running;

		// 입장 이동은 자리에 정확히 서야 도착이다(반경 0) — 입장 자리가 문 먼 쪽 줄 바로 다음 칸일 수 있어 반경 1이면 문 위(통과 전)에서 '도착'으로 멈춘다. 줄을 넘는 순간 ReleaseIfEntered가 풀어 주므로 자리까지 갈 필요는 없다.
		switch (SlotSeek.Step(human, wait, slot, useHold: true, arrivalRadius: entering ? 0 : PartyFormationMath.ArrivalRadius))
		{
			case SeekStatus.GaveUp:
				wait.IsParked = true;
				LogHelper.Log(LogHelper.GAME, $"[파티] {human.name}: 자리 ({slot.x},{slot.y})에 오래 못 가 안전장치로 {(entering ? "입장" : "진형")} 이동을 포기하고 제자리에서 기다립니다");
				break;
			case SeekStatus.Stuck:
				RetargetPlanSlot(human, wait, plan, slot, entering);
				break;
		}
		return BTStatus.Running;
	}

	// 막힌 자리는 다시 고르지 않고(RejectedSlots) 이 유닛에서 가장 가까운 구역 안 '지금 아무도 서 있지 않은' 타일로 옮긴다 — 자기 타일이 뽑히면(구역 안이고 더 갈 곳이 없음) 그 자리에서 준비 완료로 인정된다.
	private static void RetargetPlanSlot(Human human, WaitState wait, PartyAdvancePlan plan, Vector2Int slot, bool entering)
	{
		var rejected = SlotSeek.Reject(wait, slot);
		var claimed = PartyAdvanceSystem.ClaimedSlots(plan, human);
		Room room = entering ? plan.ToRoom : plan.FromRoom;
		Vector2Int zoneCenter = entering ? plan.EntryCenter : plan.FormCenter;
		int zoneRadius = entering ? plan.EntryRadius : plan.FormRadius;
		var session = human.Session;
		bool useKnowledge = AIConfigLoader.Behavior?.entrySlotKnowledgeEnabled ?? true; // 입장 자리는 본인이 아는 타일에서만(04번 0장)
		string label = entering ? "입장" : "진형";

		if (PartyFormationMath.TryPickNearestFreeTile(human.position,
				t => !rejected.Contains(t) && PartyFormationMath.IsWithinZone(t, zoneCenter, zoneRadius)
					&& PartyAdvanceSystem.IsSlotFree(session, human, t, plan.Floor, room, ignoreUnits: false, clearOf: entering ? null : plan.NearTiles, knowledge: entering && useKnowledge ? human : null),
				claimed, PartyFormationMath.DefaultSearchRadius, out var alt))
		{
			SlotSeek.LogRetarget(human, wait, label, slot, alt, "대기합니다");
			if (entering) wait.WaitPosition = alt;
			else
			{
				plan.FormSlots[human] = alt;
				if (wait.Reason != WaitReason.EnteringNextRoom) wait.WaitPosition = alt;
			}
		}
		else if (entering && useKnowledge && plan.FarAnchor + plan.Forward != slot)
		{
			// 아는 입장 자리가 아직 없으면(문이 열린 직후라 다음 방을 못 봄) 통과 방향의 첫 칸으로 향해 문을 지나게 한다 — 지나가면 ReleaseIfEntered가 개인 행동으로 푼다.
			wait.WaitPosition = plan.FarAnchor + plan.Forward;
		}
		else if (PartyFormationMath.IsWithinZone(human.position, zoneCenter, zoneRadius))
		{
			// 구역 안에 빈 타일이 하나도 없다 — 이 유닛은 더 못 들어가므로 선 자리에서 준비 완료로 인정한다.
			wait.IsParked = true;
			LogHelper.Log(LogHelper.GAME, $"[파티] {human.name}: {label} 구역 안에 빈 자리가 없어 현재 위치 ({human.position.x},{human.position.y})에서 대기합니다");
		}
	}
}
