using System.Collections.Generic;
using UnityEngine;
using GrimArchive.Wave;
using Haare.Util.Logger;

// 집결 이후 한 번의 방 이동(PartyAdvancePlan)의 수명주기와 단계 전이 — 문 앞 진형(FormingUp) → 문 파괴(Breaching) → 랭크 순 입장(Entering).
// 순수 계산은 PartyFormationMath, 리더의 문 파괴 지시는 PartyBreachCommand, 유닛 한 틱은 PartyAdvanceSteps, 로그 문자열은 PartyDiagnostics.
//  Begin(HumanWaveManager가 집결 완료 직후 1회) → Tick(매 프레임, 내부 0.25초 간격으로 단계 전이) → Finish/Abort.
// 문 파괴는 리더 지시로만 시작하며 기존 DoorAttack(인류가 점령한 방 안에서만 발동)과 별개다.
public static class PartyAdvanceSystem
{
	private const float TickIntervalSeconds = 0.25f;
	// 한 번의 방 이동에서 계획을 시작할 수 있는 최대 횟수(안전한도) — 돌파 실패는 계획 안에서 진형 복귀 후 재돌파(RegroupAfterStall)로 이어 가므로 이 횟수를 쓰지 않는다.
	// "진행할 파티원이 없음" 같은 비정상 중단이 Begin ↔ Abort로 무한 재시작되지 않게 하는 용도이며, 한도에 닿으면 Party.GiveUpAdvance가 잠금을 풀고 쿨다운 뒤 처음부터 다시 한다.
	public const int MaxAttempts = 2;
	// 단계가 이 시간(초) 넘게 끝나지 않으면 미완료 파티원의 상태를 15초마다 로그로 남긴다(원인 진단용, 동작 불변).
	private const float DiagnosticSeconds = 15f;

	public static bool IsPlanWait(WaitReason reason)
		=> reason == WaitReason.FormingUpAtDoor || reason == WaitReason.BreachingDoor || reason == WaitReason.EnteringNextRoom;

	// ── 시작 ─────────────────────────────────────────────────────────────────────────────

	// HumanWaveManager가 집결 완료(ReadyToAdvance)로 다음 문이 정해진 순간 호출한다. 계획을 만들 수 없으면(게이트·파티원 없음) false — 호출부가 예전 방 이동으로 폴백한다.
	public static bool Begin(Party party, Vector2Int doorPos, int doorFloor, ICollection<Human> excluded)
	{
		var leader = party?.Leader;
		var session = leader?.Session;
		if (leader == null || session?.cmap == null || session.roomGrid == null) return false;
		if (!TryBuildGate(session, leader.currentRoom, doorPos, doorFloor, out var near, out var far, out var forward)) return false;

		var members = CollectMembers(party, leader, doorFloor, excluded);
		if (members.Count == 0) return false;

		var plan = new PartyAdvancePlan
		{
			Phase = AdvancePhase.FormingUp,
			Floor = doorFloor,
			DoorPos = doorPos,
			Forward = forward,
			NearTiles = near,
			FarTiles = far,
			FromRoom = leader.currentRoom,
			ToRoom = RoomAt(session, far[(far.Length - 1) / 2], doorFloor),
			PhaseStartTime = Time.time,
			LastClockTime = Time.time,
		};

		AssignFormation(party, plan, members, session);
		party.AdvancePlan = plan;
		party.AdvanceAttempts++;

		string state = PartyBreachCommand.BlockedRowCount(plan, session, leader) == 0 ? "문이 이미 열려 있음" : $"막힌 문 {PartyBreachCommand.BlockedRowCount(plan, session, leader)}/2";
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} 방 이동 지시({party.AdvanceAttempts}/{MaxAttempts}회째) — 사유: 집결 완료, 목표 문 ({doorPos.x},{doorPos.y}), {state}, {PartyDiagnostics.RankSummary(plan)}");
		return true;
	}

	// 계획에 참여할 파티원 — 같은 층의 생존자 중 수동 명령·다른 대기(코어 보고 등) 중이 아닌 유닛. 다음 문을 찾으며 리더를 따라다니던 추종(SearchingNextDoor)만 문이 알려진 지금 계획으로 바꾼다.
	// 방 이동 지시도 집결 명령과 같은 전달 범위를 따른다(Party.IsReachedByLeaderCommand — 같은 방은 무조건, 다른 방은 일반 전파 조건, 방 없는 유닛끼리는 같은 방이 아님). 못 받은 구성원은 개인 행동(진형 합류)으로 남는다.
	private static List<Human> CollectMembers(Party party, Human leader, int doorFloor, ICollection<Human> excluded)
	{
		var members = new List<Human>();
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0 || (excluded != null && excluded.Contains(m))) continue;
			if (m.currentFloor != doorFloor) continue;
			if (!Party.IsReachedByLeaderCommand(leader, m)) continue;
			if (m.isManualMoveCommand && m.playerMoveTarget.HasValue) continue;
			if (m.playerAttackTarget != null) continue;
			if (m.currentWait != null && m.currentWait.Reason != WaitReason.SearchingNextDoor) continue;
			members.Add(m);
		}
		return members;
	}

	private static int RankOf(GameSession session, Party party, Human m)
		=> PartyFormationMath.ResolveRank(m == party.Leader, DungeonEntranceSystem.IsMelee(session, m));

	// 랭크별로 레인을 나누고 진형 자리를 고른다 — 앞랭크부터 자리를 먼저 차지해 뒷랭크가 앞자리를 빼앗지 않는다.
	private static void AssignFormation(Party party, PartyAdvancePlan plan, List<Human> members, GameSession session)
	{
		Vector2Int lateral = PartyFormationMath.LateralAxis(plan.Forward);
		// 0랭크는 문 앞 통과 구간(DoorClearance) 바로 밖 — 문서 규정(05번 3장)대로 대기 중에는 구간을 비우고, 문 파괴는 돌파 단계에서 리더 지시를 받은 유닛이 문 인접 칸으로 접근한다.
		Vector2Int frontAnchor = plan.NearTiles[(plan.NearTiles.Length - 1) / 2] - plan.Forward * PartyFormationMath.DoorClearance;
		plan.FormCenter = frontAnchor;
		var claimed = new HashSet<Vector2Int>();

		for (int rank = 0; rank <= PartyFormationMath.MaxRank; rank++)
		{
			var group = members.FindAll(m => RankOf(session, party, m) == rank);
			if (group.Count == 0) continue;

			var lateralPositions = new List<int>(group.Count);
			foreach (var m in group) lateralPositions.Add(m.position.x * lateral.x + m.position.y * lateral.y);
			int[] lanes = PartyFormationMath.AssignLanes(lateralPositions);

			for (int i = 0; i < group.Count; i++)
			{
				Human member = group[i];
				Vector2Int ideal = PartyFormationMath.SlotTarget(frontAnchor, plan.Forward, rank, lanes[i]);
				Vector2Int slot = PartyFormationMath.TryPickNearestFreeTile(ideal, t => IsSlotFree(session, member, t, plan.Floor, plan.FromRoom, clearOf: plan.NearTiles), claimed, PartyFormationMath.DefaultSearchRadius, out var picked)
					? picked
					: member.position; // 방 안에 자리가 없다 — 제자리에서 기다린다
				EnrollMember(plan, member, rank, slot, WaitReason.FormingUpAtDoor);
			}
		}
		plan.FormRadius = PartyFormationMath.ZoneRadius(plan.FormSlots.Values, frontAnchor);
	}

	private static void EnrollMember(PartyAdvancePlan plan, Human member, int rank, Vector2Int slot, WaitReason reason)
	{
		plan.Ranks[member] = rank;
		plan.EntryTiers[member] = OccupancyMath.EntryRank(CombatScoreMath.ResolveCombatRole(member.unitType));
		plan.FormSlots[member] = slot;
		member.currentWait = new WaitState { Reason = reason, WaitPosition = slot, WaitFloor = plan.Floor, DoorPosition = plan.DoorPos, Rank = rank };
		member.waitStuckTurns = 0;
		member.ReleaseUnstartedInvestigation(); // 시작 전 대상으로 이동 중이던 조사는 공동 이동으로 전환한다(05번 1장 31줄, 검증 05-04)
		member.currentFormation = null; // 공동 이동(방 이동 계획)에 편입되면 비전투 보호 포메이션은 끝난다(05번 9장, 검증 05-08 관찰 1)
		// 전파받은 적 위치로 접근하던 경계는 공동 이동이 시작되면 접는다(03번 1장 50줄).
		if (member.currentAlertSearch != null && member.currentAlertSearch.IsIndirectEnemyApproach) member.currentAlertSearch = null;
	}

	// 진형·입장 자리로 쓸 수 있는 타일: 그 유닛이 설 수 있고(점유는 무시 — 자리를 점유한 아군은 곧 비킨다), 문 타일·게이트 문턱이 아니며, 지정한 방 안.
	// ignoreUnits=false는 막힌 뒤 재선택용 — 지금 다른 유닛이 실제로 서 있는 타일(먼저 도착해 남의 자리에 선 유닛 포함)은 뺀다.
	// clearOf가 있으면 그 문 타일들의 통과 구간(PartyFormationMath.DoorClearance) 안은 대기 자리로 쓰지 않는다 — 진형 대기용이고, 입장 자리에는 넘기지 않는다.
	// knowledge가 있으면 그 유닛의 개인 지도로 바닥이 확인된 타일만 쓴다 — 입장 자리처럼 아직 보지 못한 다음 방 타일을 실제 지형으로 고르지 않는다(04번 0장 12~14줄, 검증 05-06 관찰 4).
	internal static bool IsSlotFree(GameSession session, Human m, Vector2Int t, int floor, Room room, bool ignoreUnits = true, Vector2Int[] clearOf = null, Human knowledge = null)
	{
		if (clearOf != null && PartyFormationMath.IsInDoorClearance(t, clearOf)) return false;
		if (!m.CanMove(t, ignoreUnits)) return false;
		var pos3 = new Vector3Int(t.x, t.y, floor);
		if (knowledge != null && knowledge.personalMap.GetTileTerrain(pos3) != 1) return false;
		if (session.IsDoorTile(pos3) || session.TryGetGateKeyAt(pos3, out _)) return false;
		if (room != null && !(session.roomGrid.TryGetValue(pos3, out var r) && r == room)) return false;
		return true;
	}

	private static Room RoomAt(GameSession session, Vector2Int tile, int floor)
		=> session.roomGrid.TryGetValue(new Vector3Int(tile.x, tile.y, floor), out var r) ? r : null;

	// doorPos가 속한 게이트의 문 타일 두 줄을 찾는다 — 리더 방 쪽 줄이 near. 인접한 두 줄이 아닌 게이트는 지원하지 않는다(false → 예전 방 이동 폴백).
	private static bool TryBuildGate(GameSession session, Room leaderRoom, Vector2Int doorPos, int floorIndex, out Vector2Int[] near, out Vector2Int[] far, out Vector2Int forward)
	{
		near = null; far = null; forward = default;
		var floors = session.cmap.map.floors;
		if (floors == null || floorIndex < 0 || floorIndex >= floors.Length) return false;
		Floor floor = floors[floorIndex];
		if (floor.gates == null) return false;

		foreach (var gate in floor.gates)
		{
			var rows = DoorSystem.GetGateDoorTiles(gate, floor.config.chunkSize);
			for (int r = 0; r < 2; r++)
			{
				if (!rows[r].Contains(doorPos)) continue;
				var a = rows[r];
				var b = rows[1 - r];
				// 반대 줄이 고른 문이라도 리더 방 쪽을 near로 맞춘다.
				if (leaderRoom != null && RoomAt(session, a[0], floorIndex) != leaderRoom && RoomAt(session, b[0], floorIndex) == leaderRoom)
				{
					var swap = a; a = b; b = swap;
				}
				Vector2Int d = b[0] - a[0];
				var fwd = new Vector2Int(Mathf.Clamp(d.x, -1, 1), Mathf.Clamp(d.y, -1, 1));
				if (Mathf.Abs(fwd.x) + Mathf.Abs(fwd.y) != 1 || a.Count != b.Count || a.Count == 0) return false;
				near = a.ToArray();
				far = b.ToArray();
				forward = fwd;
				return true;
			}
		}
		return false;
	}

	// ── 단계 전이 ────────────────────────────────────────────────────────────────────────

	// HumanWaveManager.UpdatePartyDestination이 매 프레임 호출한다 — 내부에서 0.25초 간격으로만 판정한다.
	public static void Tick(Party party)
	{
		var plan = party?.AdvancePlan;
		if (plan == null) return;
		float now = Time.time;
		if (now < plan.NextTickTime) return;
		plan.NextTickTime = now + TickIntervalSeconds;

		party.AssignLeaderIfNeeded();
		var leader = party.Leader;
		var session = leader?.Session;
		if (leader == null || session == null) { Abort(party, "리더가 없음", retry: false); return; }

		PruneMembers(plan);
		if (plan.Ranks.Count == 0) { Abort(party, "진행할 파티원이 없음"); return; }

		ApplyEngagementPause(party, plan, now);
		if (plan.Phase != AdvancePhase.Breaching) EnforcePhaseLimit(party, plan, now);

		switch (plan.Phase)
		{
			case AdvancePhase.FormingUp: TickFormingUp(party, plan, leader, session, now); break;
			case AdvancePhase.Breaching: TickBreaching(party, plan, leader, session, now); break;
			case AdvancePhase.Entering: TickEntering(party, plan, now); break;
		}
	}

	// 교전·경계 중 시간 제한 정지(03번 13항·05번 2장: 전투 후 10초 경계를 마친 뒤 기존 집결·이동을 재개) — 계획 구성원 중 한 명이라도 교전·경계면 그 경과 시간만큼 단계·진행·재시도 시계를 뒤로 밀어
	// 타이머가 흐르지 않은 것으로 만든다. 상한(PartyEngagement.PauseCap)에 닿으면 더 밀지 않아 풀리지 않는 교전이 계획을 영구히 얼리지 않는다. 단계가 바뀌거나 재돌파 사이클이 시작되면 누적을 비운다.
	private static void ApplyEngagementPause(Party party, PartyAdvancePlan plan, float now)
	{
		float dt = now - plan.LastClockTime;
		plan.LastClockTime = now;
		if (dt <= 0f || !PartyEngagement.AnyEngaged(plan.Ranks.Keys)) return;

		float cap = PartyEngagement.PauseCap;
		float credit = PartyFormationMath.PauseCredit(dt, plan.PausedSeconds, cap);
		if (credit <= 0f)
		{
			if (plan.PauseLogged)
			{
				plan.PauseLogged = false; // 상한 도달 로그는 한 번만 — PausedSeconds가 상한에 있어 단계 전환 전에는 다시 멈추지 않는다
				LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} {plan.Phase} 단계의 교전·경계 정지 시간이 상한({cap:F0}초)에 닿아 시간 제한을 다시 셉니다");
			}
			return;
		}

		bool first = plan.PausedSeconds <= 0f;
		plan.PausedSeconds += credit;
		plan.PhaseStartTime += credit;
		plan.LastProgressTime += credit;
		plan.RetryNotBefore += credit;
		plan.NextDiagTime += credit;
		if (first)
		{
			plan.PauseLogged = true;
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} {plan.Phase} 단계 중 교전·경계가 있어 시간 제한을 멈춥니다(상한 {cap:F0}초)");
		}
	}

	// 안전장치 — 개별 이동 포기(IsParked)가 못 푸는 정체(전투·경계가 끝나지 않는 유닛 등)로 단계가 영구히 멈추지 않게 한다. 돌파 단계는 자체 진행 감시(PartyBreachCommand.Tick)를 쓴다.
	// 기준은 기존 doorApproachMaxBlockedSeconds의 2배(내부 판단). 상한에 닿으면 계획을 중단하지 않고 집결과 같은 규칙으로 못 선 인원을 현재 위치에서 인정해 다음 단계로 진행한다
	// (사용자 확정 2026-10-01 "공간 부족·못 서는 유닛은 현재 위치 인정"). 그 전에는 15초마다 미완료 파티원의 상태를 로그로 남긴다.
	private static void EnforcePhaseLimit(Party party, PartyAdvancePlan plan, float now)
	{
		float phaseLimit = 2f * (AIConfigLoader.Behavior?.doorApproachMaxBlockedSeconds ?? 30f);
		float elapsed = now - plan.PhaseStartTime;
		if (elapsed >= phaseLimit) { ParkStragglersAtLimit(party, plan, phaseLimit); return; }
		if (elapsed < DiagnosticSeconds || now < plan.NextDiagTime) return;

		plan.NextDiagTime = now + DiagnosticSeconds;
		string lagging = PartyDiagnostics.DescribeNotDone(plan);
		if (lagging != null) LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} {plan.Phase} 단계 {elapsed:F0}초째 — 미완료: {lagging}");
	}

	private static void PruneMembers(PartyAdvancePlan plan)
	{
		List<Human> gone = null;
		foreach (var m in plan.Ranks.Keys)
		{
			if (m == null || m.hp <= 0 || m.currentFloor != plan.Floor) (gone ??= new List<Human>()).Add(m);
		}
		if (gone == null) return;
		foreach (var m in gone)
		{
			plan.Ranks.Remove(m);
			plan.EntryTiers.Remove(m);
			plan.FormSlots.Remove(m);
			plan.AttackSlots.Remove(m);
		}
	}

	private static void TickFormingUp(Party party, PartyAdvancePlan plan, Human leader, GameSession session, float now)
	{
		foreach (var m in plan.Ranks.Keys)
		{
			if (!IsMemberReady(m)) return;
		}

		if (PartyBreachCommand.IsPassable(plan, session, leader))
		{
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} 문 앞 진형 완료 — 진입로가 이미 열려 있어 문 파괴 없이 입장");
			BeginEntering(party, plan, leader, session, now);
			return;
		}

		if (now < plan.RetryNotBefore) return; // 돌파 실패 뒤 진형으로 물러난 쿨다운 — 진형을 유지한 채 기다린다

		plan.Phase = AdvancePhase.Breaching;
		plan.PhaseStartTime = now;
		plan.LastProgressTime = now;
		plan.PausedSeconds = 0f;
		plan.PauseLogged = false;
		plan.AssignDirty = true;
		foreach (var m in plan.Ranks.Keys) ChangeReason(m, WaitReason.BreachingDoor);
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} 문 앞 진형 완료({plan.Ranks.Count}명) — 리더 {leader.name}의 지시로 문 파괴 {(plan.BreachCycles > 0 ? $"재시도({plan.BreachCycles + 1}번째)" : "시작")}");
	}

	private static void TickBreaching(Party party, PartyAdvancePlan plan, Human leader, GameSession session, float now)
	{
		switch (PartyBreachCommand.Tick(party, plan, session, leader, now))
		{
			case PartyBreachCommand.Outcome.Opened:
				LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} 문 파괴 완료 — 진입로 확보({now - plan.PhaseStartTime:F1}초 소요)");
				BeginEntering(party, plan, leader, session, now);
				break;
			case PartyBreachCommand.Outcome.Stalled:
				RegroupAfterStall(party, plan, now);
				break;
		}
	}

	// 문 파괴 진행이 30초 넘게 없을 때(접근 불가 추정) — 계획을 풀지 않고 전원이 문 앞 진형 자리로 물러나 쿨다운 뒤 같은 계획으로 다시 돌파한다(05번 1장 71·73줄: 집결 후에는 흩어지지 않고 진형을 유지).
	// 못 나아갈 때의 계속/후퇴 판단은 후속 리더·후퇴 문서 몫이라, 그 전까지는 진형을 유지한 채 재시도를 반복한다. 매 사이클 로그가 남는다.
	internal static void RegroupAfterStall(Party party, PartyAdvancePlan plan, float now)
	{
		float cooldown = AIConfigLoader.Behavior?.doorApproachMaxBlockedSeconds ?? 30f;
		plan.BreachCycles++;
		plan.Phase = AdvancePhase.FormingUp;
		plan.PhaseStartTime = now;
		plan.RetryNotBefore = now + cooldown;
		plan.PausedSeconds = 0f;
		plan.PauseLogged = false;
		plan.BreachRow = -1;
		plan.AssignDirty = false;
		plan.LastLoggedRow = -1;
		plan.LastLoggedCount = -1;
		plan.AttackSlots.Clear();

		foreach (var m in plan.Ranks.Keys)
		{
			if (m.currentAttackObjectTarget.HasValue && IsPlanDoorTile(plan, m.currentAttackObjectTarget.Value)) m.ClearAttackObjectTarget();
			ChangeReason(m, WaitReason.FormingUpAtDoor);
		}
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} 문 파괴 {plan.BreachCycles}번째 시도 실패(진행 없음) — 진형으로 물러나 {cooldown:F0}초 뒤 다시 시도");
	}

	private static void BeginEntering(Party party, PartyAdvancePlan plan, Human leader, GameSession session, float now)
	{
		Human entryKnowledge = (AIConfigLoader.Behavior?.entrySlotKnowledgeEnabled ?? true) ? leader : null; // 입장 자리는 리더가 아는 타일에서만 고른다(없으면 방향 목적지 ideal)
		plan.Phase = AdvancePhase.Entering;
		plan.PhaseStartTime = now;
		plan.NextEntryReleaseTime = now; // 첫 유닛은 바로 출발
		plan.PausedSeconds = 0f;
		plan.PauseLogged = false;
		plan.AttackSlots.Clear();
		foreach (var m in plan.Ranks.Keys)
		{
			if (m.currentAttackObjectTarget.HasValue && IsPlanDoorTile(plan, m.currentAttackObjectTarget.Value)) m.ClearAttackObjectTarget();
		}

		Vector2Int lateral = PartyFormationMath.LateralAxis(plan.Forward);
		Vector2Int farAnchor = plan.FarTiles[(plan.FarTiles.Length - 1) / 2];
		plan.EntryCenter = farAnchor;
		var entrySlots = new List<Vector2Int>();
		var claimed = new HashSet<Vector2Int>();
		for (int rank = 0; rank <= PartyFormationMath.MaxRank; rank++)
		{
			var group = new List<Human>();
			foreach (var kv in plan.Ranks)
			{
				var w = kv.Key.currentWait;
				if (kv.Value == rank && w != null && IsPlanWait(w.Reason)) group.Add(kv.Key);
			}
			if (group.Count == 0) continue;

			var lateralPositions = new List<int>(group.Count);
			foreach (var m in group) lateralPositions.Add(m.position.x * lateral.x + m.position.y * lateral.y);
			int[] lanes = PartyFormationMath.AssignLanes(lateralPositions);

			for (int i = 0; i < group.Count; i++)
			{
				Human member = group[i];
				Vector2Int ideal = PartyFormationMath.EntrySlotTarget(farAnchor, plan.Forward, rank, lanes[i]);
				Vector2Int slot = PartyFormationMath.TryPickNearestFreeTile(ideal, t => IsSlotFree(session, member, t, plan.Floor, plan.ToRoom, knowledge: entryKnowledge), claimed, PartyFormationMath.DefaultSearchRadius, out var picked)
					? picked
					: ideal;
				var wait = member.currentWait;
				ChangeReason(member, WaitReason.EnteringNextRoom);
				wait.WaitPosition = slot;
				wait.Released = false;
				entrySlots.Add(slot);
			}
		}
		plan.EntryRadius = PartyFormationMath.ZoneRadius(entrySlots, farAnchor);
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} 진입로 확보 — 리더 {leader.name}이(가) 진형을 유지한 채 역할 순(근접 전방 → 근접 지원 → 원거리 공격 → 원거리 지원, 리더도 자기 역할)으로 한 명씩 입장을 지시");
	}

	// 진입 판정: 문 먼 쪽 줄을 지난 유닛은 그 즉시 개인 행동으로 푼다 — 다음 방 안쪽 자리에 전원이 모일 때까지 붙들지 않는다(사용자 확정 2026-10-01 "다음 방 진입 판정 후 바로 개인 행동").
	// 05번 1장·04번 "다음 방 진입 후에는 개인 탐색·임무 행동을 판단"과 같은 방향이다. 입장 자리는 문을 지나는 방향을 정하는 목적지일 뿐이다.
	internal static bool ReleaseIfEntered(Human m, PartyAdvancePlan plan)
	{
		var w = m.currentWait;
		if (w == null || w.Reason != WaitReason.EnteringNextRoom) return false;
		if (!PartyFormationMath.HasPassedGate(m.position, plan.FarTiles[0], plan.Forward)) return false;
		m.currentWait = null;
		m.waitStuckTurns = 0;
		plan.EnteredCount++;
		return true;
	}

	private static void TickEntering(Party party, PartyAdvancePlan plan, float now)
	{
		foreach (var m in plan.Ranks.Keys) ReleaseIfEntered(m, plan);

		float interval = AIConfigLoader.Behavior?.entryReleaseIntervalSeconds ?? 0.5f;
		if (interval > 0f) ReleaseNextInOrder(plan, now, interval);
		else ReleaseByRoleTier(plan);

		// 아직 문을 지나지 못한 유닛이 남았으면 계속(이미 진입해 풀렸거나 입장에 참여하지 않는 유닛 — 다른 대기로 덮임·자리에 못 가 포기 — 은 세지 않는다).
		bool allDone = true;
		foreach (var kv in plan.Ranks)
		{
			var w = kv.Key.currentWait;
			if (w == null || !IsPlanWait(w.Reason) || w.IsParked) continue;
			allDone = false;
			break;
		}
		if (allDone) Finish(party, plan, $"입장 완료 — {plan.EnteredCount}명이 문을 지나 개인 행동으로 복귀(나머지 {Mathf.Max(0, plan.Ranks.Count - plan.EnteredCount)}명은 포기·다른 대기), 입장 단계 {now - plan.PhaseStartTime:F1}초 소요(교전·경계 정지 시간 제외)");
	}

	// 이 유닛이 지금 입장 중(출발 허가를 받았고 문 먼 쪽 줄을 아직 못 지남)이면 그 계획, 아니면 null — OccupancySystem이 "앞 유닛 대기" 규칙을 걸 대상을 가리는 데 쓴다.
	internal static PartyAdvancePlan EnteringPlanOf(Unit unit)
	{
		if (!(unit is Human h)) return null;
		var w = h.currentWait;
		if (w == null || w.Reason != WaitReason.EnteringNextRoom || !w.Released || w.IsParked) return null;
		var plan = h.party?.AdvancePlan;
		return plan != null && plan.Phase == AdvancePhase.Entering && plan.Ranks.ContainsKey(h) ? plan : null;
	}

	private static void MarkReleased(Human m, WaitState w)
	{
		w.Released = true;
		w.AtSlot = false;
		w.BlockedSince = -1f;
		w.BlockedLogged = false;
		m.waitStuckTurns = 0;
	}

	// 유닛별 간격 출발 — 아직 출발하지 않은 유닛 중 통과 우선순위(역할 → HP 비율 → 유지되는 무작위 → Id, OccupancySystem이 문턱에서 쓰는 같은 키)가 가장 높은 한 명을 interval초마다 출발시킨다.
	// 앞 단계가 문을 완전히 지나야 다음 단계가 출발하던 장벽이 없어 앞뒤 유닛이 겹쳐 흐르고, 앞 유닛이 막혀도 시계가 흘러 뒤 유닛이 영구히 붙들리지 않는다. 실제 문턱 통과 순서는 OccupancySystem이 같은 키로 중재한다.
	private static void ReleaseNextInOrder(PartyAdvancePlan plan, float now, float interval)
	{
		if (now < plan.NextEntryReleaseTime) return;

		Human next = null;
		PassKey bestKey = default;
		foreach (var m in plan.Ranks.Keys)
		{
			var w = m.currentWait;
			if (w == null || !IsPlanWait(w.Reason) || w.IsParked || w.Released) continue;
			PassKey key = OccupancySystem.PassKeyOf(m);
			if (next == null || OccupancyMath.ComparePassPriority(key, bestKey) < 0) { next = m; bestKey = key; }
		}
		if (next == null) return;

		MarkReleased(next, next.currentWait);
		plan.NextEntryReleaseTime = now + interval;
	}

	// 예전 단계 장벽(entryReleaseIntervalSeconds ≤ 0) — 04번 8장 4단계 역할(plan.EntryTiers)이 앞 단계 전원이 문을 지나야 출발한다. 리더도 자기 역할 단계를 따르며, 같은 단계 안의 HP 비율·무작위 순서는 실제 문턱 통과에서 OccupancySystem이 같은 키로 중재한다.
	private static void ReleaseByRoleTier(PartyAdvancePlan plan)
	{
		var tiers = new List<int>(plan.Ranks.Count);
		var passed = new List<bool>(plan.Ranks.Count);
		foreach (var kv in plan.Ranks)
		{
			var w = kv.Key.currentWait;
			// 이미 진입해 풀렸거나, 입장에 참여하지 않는 유닛(다른 대기로 덮임·자리에 못 가 포기)은 뒤 단계를 붙잡지 않는다.
			tiers.Add(plan.EntryTiers[kv.Key]);
			passed.Add(w == null || !IsPlanWait(w.Reason) || w.IsParked);
		}

		foreach (var kv in plan.Ranks)
		{
			var m = kv.Key;
			var w = m.currentWait;
			if (w == null || !IsPlanWait(w.Reason) || w.IsParked) continue;
			if (!w.Released && PartyFormationMath.CanReleaseRank(plan.EntryTiers[m], tiers, passed)) MarkReleased(m, w);
		}
	}

	// ── 종료 ─────────────────────────────────────────────────────────────────────────────

	// 계획을 비정상 중단한다(진행할 파티원 없음 등 — 돌파 실패는 RegroupAfterStall이 계획을 유지한 채 처리). retry=true(기본)면 파티가 멈추지 않게 이어 간다 —
	// 시도가 남았으면 ReadyToAdvance를 되살려 HumanWaveManager가 다시 지시하게 하고, 한도(MaxAttempts)에 닿았으면 잠금을 풀고 쿨다운 뒤 처음부터 다시 하게 한다
	// (Party.GiveUpAdvance — 이동 지시는 한 번 소비되고 AdvanceFromRoom은 리더가 방을 떠나야만 풀려, 그냥 두면 영구 정지한다: 플레이 로그 2026-10-01 101~107줄).
	// 리더 없음·퇴각 전환처럼 이동을 이어 갈 수 없는 중단은 retry=false.
	public static void Abort(Party party, string reason, bool retry = true)
	{
		var plan = party?.AdvancePlan;
		if (plan == null) return;

		string note = "";
		if (retry)
		{
			if (party.AdvanceAttempts < MaxAttempts)
			{
				party.ReadyToAdvance = true;
				note = $" — 재시도({party.AdvanceAttempts}/{MaxAttempts}회 시도함)";
			}
			else
			{
				float cooldown = AIConfigLoader.Behavior?.doorApproachMaxBlockedSeconds ?? 30f;
				party.GiveUpAdvance(cooldown);
				note = $" — 시도 한도({MaxAttempts}회): 잠금을 풀고 {cooldown:F0}초 뒤 재집결로 처음부터 다시 시도";
			}
		}
		Finish(party, plan, $"방 이동 중단 — {reason}, 전원 개인 행동으로 복귀{note}");
	}

	// 단계 상한에 닿았을 때: 자리에 못 선 파티원은 그 자리에서 준비 완료(IsParked)로 인정해 다음 단계가 진행되게 한다.
	private static void ParkStragglersAtLimit(Party party, PartyAdvancePlan plan, float limit)
	{
		System.Text.StringBuilder names = null;
		foreach (var m in plan.Ranks.Keys)
		{
			var w = m.currentWait;
			if (w == null || !IsPlanWait(w.Reason) || w.IsParked || IsMemberDone(m, plan)) continue;
			w.IsParked = true;
			(names ??= new System.Text.StringBuilder()).Append(PartyDiagnostics.DescribeMember(m, SlotOf(m, plan))).Append(' ');
		}
		if (names != null)
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} {plan.Phase} 단계 {limit:F0}초 경과 — 자리에 못 선 [{names.ToString().TrimEnd()}]을(를) 현재 위치에서 인정하고 다음으로 진행합니다");
	}

	private static void Finish(Party party, PartyAdvancePlan plan, string message)
	{
		foreach (var m in plan.Ranks.Keys)
		{
			if (m == null) continue;
			if (m.currentAttackObjectTarget.HasValue && IsPlanDoorTile(plan, m.currentAttackObjectTarget.Value)) m.ClearAttackObjectTarget();
			if (m.currentWait != null && IsPlanWait(m.currentWait.Reason))
			{
				m.currentWait = null;
				m.waitStuckTurns = 0;
			}
		}
		party.AdvancePlan = null;
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} {message}");
	}

	// ── 보조 ─────────────────────────────────────────────────────────────────────────────

	// 이 단계에서 파티원이 할 일을 끝냈는가 — 진형: 자리 근처 도착, 입장: 출발 허가를 받고 입장 자리에 도착. (다른 대기로 덮이거나 포기한 유닛은 끝낸 것으로 본다.)
	internal static bool IsMemberDone(Human m, PartyAdvancePlan plan)
	{
		var w = m.currentWait;
		if (w == null || !IsPlanWait(w.Reason) || w.IsParked || !w.WaitPosition.HasValue) return true;
		// 입장 단계에서 대기가 남아 있다는 것은 아직 문 먼 쪽 줄을 못 지났다는 뜻이다(지나면 ReleaseIfEntered가 대기를 비운다) — 자리 근처라도 끝낸 것이 아니다.
		// 자리 반경만 보면 문 위에 선 유닛이 "끝낸 것"으로 보여 단계 상한(ParkStragglersAtLimit)도 진단 로그(DescribeNotDone)도 건너뛰어 입장이 영구히 멈춘다.
		if (plan.Phase == AdvancePhase.Entering) return false;
		return PartyFormationMath.IsAtSlot(m.position, w.WaitPosition.Value);
	}

	// 진형 단계 준비 완료 — 자리 근처에 서 있거나, 다른 대기로 덮였거나(코어 보고 등), 포기(IsParked)했거나, 이미 해제됐다. 전투 등으로 대기 처리가 못 돌던 유닛은 위치로 직접 판정한다.
	private static bool IsMemberReady(Human m)
	{
		var w = m.currentWait;
		if (w == null || !IsPlanWait(w.Reason) || w.IsParked) return true;
		return !w.WaitPosition.HasValue || PartyFormationMath.IsAtSlot(m.position, w.WaitPosition.Value);
	}

	// 이 유닛이 지금 향하는 자리 — 출발 허가 전 입장 대기는 진형 자리에서 기다린다.
	internal static Vector2Int? SlotOf(Human m, PartyAdvancePlan plan)
	{
		var w = m.currentWait;
		if (w == null) return null;
		if (w.Reason == WaitReason.EnteringNextRoom && !w.Released && plan.FormSlots.TryGetValue(m, out var hold)) return hold;
		return w.WaitPosition;
	}

	internal static HashSet<Vector2Int> ClaimedSlots(PartyAdvancePlan plan, Human except)
	{
		var claimed = new HashSet<Vector2Int>();
		foreach (var kv in plan.FormSlots)
		{
			if (kv.Key == except) continue;
			claimed.Add(kv.Value);
			var w = kv.Key.currentWait;
			if (w != null && w.Reason == WaitReason.EnteringNextRoom && w.WaitPosition.HasValue) claimed.Add(w.WaitPosition.Value);
		}
		return claimed;
	}

	// 단계가 바뀔 때 대기 사유만 바꿔 진형 자리는 유지하고, 막힘·포기 상태는 새 단계에서 새로 시작한다.
	private static void ChangeReason(Human m, WaitReason reason)
	{
		var w = m.currentWait;
		if (w == null || !IsPlanWait(w.Reason)) return;
		w.Reason = reason;
		w.AtSlot = false;
		w.IsParked = false;
		w.BlockedSince = -1f;
		w.BlockedLogged = false;
		w.NextDoorScanTime = 0f;
		w.RejectedSlots = null;
		m.waitStuckTurns = 0;
	}

	private static bool IsPlanDoorTile(PartyAdvancePlan plan, Vector3Int pos)
	{
		if (pos.z != plan.Floor) return false;
		var p = new Vector2Int(pos.x, pos.y);
		foreach (var t in plan.NearTiles) if (t == p) return true;
		foreach (var t in plan.FarTiles) if (t == p) return true;
		return false;
	}
}
