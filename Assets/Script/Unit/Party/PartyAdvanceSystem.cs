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
	// 한 번의 방 이동에서 진형 계획을 시작할 수 있는 최대 횟수 — 1회째는 일반(진형 → 돌파 → 입장), 2회째는 진형 없이 곧바로 돌파(직행). 그래도 중단되면 Party.GiveUpAdvance가 잠금을 풀고 쿨다운 뒤 처음부터 다시 한다.
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

		var members = CollectMembers(party, doorFloor, excluded);
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
			Direct = party.AdvanceAttempts >= 1,
		};

		if (plan.Direct) AssignDirect(party, plan, members, session);
		else AssignFormation(party, plan, members, session);
		party.AdvancePlan = plan;
		party.AdvanceAttempts++;

		string state = PartyBreachCommand.BlockedRowCount(plan, session, leader) == 0 ? "문이 이미 열려 있음" : $"막힌 문 {PartyBreachCommand.BlockedRowCount(plan, session, leader)}/2";
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} 방 이동 지시({party.AdvanceAttempts}/{MaxAttempts}회째{(plan.Direct ? ", 진형 없이 직행 돌파" : "")}) — 사유: 집결 완료, 목표 문 ({doorPos.x},{doorPos.y}), {state}, {PartyDiagnostics.RankSummary(plan)}");
		return true;
	}

	// 계획에 참여할 파티원 — 같은 층의 생존자 중 수동 명령·다른 대기(코어 보고 등) 중이 아닌 유닛. 다음 문을 찾으며 리더를 따라다니던 추종(SearchingNextDoor)만 문이 알려진 지금 계획으로 바꾼다.
	private static List<Human> CollectMembers(Party party, int doorFloor, ICollection<Human> excluded)
	{
		var members = new List<Human>();
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0 || (excluded != null && excluded.Contains(m))) continue;
			if (m.currentFloor != doorFloor) continue;
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

	// 직행 돌파(재시도) — 진형 대기 없이 전원이 제자리에서 곧바로 Breaching으로 시작하고, 리더의 공격 자리 지시를 받는다(PartyBreachCommand). 진형 정체와 무관하게 문에 달라붙게 하는 것이 목적이다.
	private static void AssignDirect(Party party, PartyAdvancePlan plan, List<Human> members, GameSession session)
	{
		Vector2Int center = plan.NearTiles[(plan.NearTiles.Length - 1) / 2];
		plan.FormCenter = center;
		foreach (var m in members) EnrollMember(plan, m, RankOf(session, party, m), m.position, WaitReason.BreachingDoor);
		plan.FormRadius = PartyFormationMath.ZoneRadius(plan.FormSlots.Values, center);

		float now = Time.time;
		plan.Phase = AdvancePhase.Breaching;
		plan.PhaseStartTime = now;
		plan.LastProgressTime = now;
		plan.AssignDirty = true;
	}

	private static void EnrollMember(PartyAdvancePlan plan, Human member, int rank, Vector2Int slot, WaitReason reason)
	{
		plan.Ranks[member] = rank;
		plan.FormSlots[member] = slot;
		member.currentWait = new WaitState { Reason = reason, WaitPosition = slot, WaitFloor = plan.Floor, DoorPosition = plan.DoorPos, Rank = rank };
		member.waitStuckTurns = 0;
		// 전파받은 적 위치로 접근하던 경계는 공동 이동이 시작되면 접는다(03번 1장 50줄).
		if (member.currentAlertSearch != null && member.currentAlertSearch.IsIndirectEnemyApproach) member.currentAlertSearch = null;
	}

	// 진형·입장 자리로 쓸 수 있는 타일: 그 유닛이 설 수 있고(점유는 무시 — 자리를 점유한 아군은 곧 비킨다), 문 타일·게이트 문턱이 아니며, 지정한 방 안.
	// ignoreUnits=false는 막힌 뒤 재선택용 — 지금 다른 유닛이 실제로 서 있는 타일(먼저 도착해 남의 자리에 선 유닛 포함)은 뺀다.
	// clearOf가 있으면 그 문 타일들의 통과 구간(PartyFormationMath.DoorClearance) 안은 대기 자리로 쓰지 않는다 — 진형 대기용이고, 입장 자리에는 넘기지 않는다.
	internal static bool IsSlotFree(GameSession session, Human m, Vector2Int t, int floor, Room room, bool ignoreUnits = true, Vector2Int[] clearOf = null)
	{
		if (clearOf != null && PartyFormationMath.IsInDoorClearance(t, clearOf)) return false;
		if (!m.CanMove(t, ignoreUnits)) return false;
		var pos3 = new Vector3Int(t.x, t.y, floor);
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

		if (plan.Phase != AdvancePhase.Breaching) EnforcePhaseLimit(party, plan, now);

		switch (plan.Phase)
		{
			case AdvancePhase.FormingUp: TickFormingUp(party, plan, leader, session, now); break;
			case AdvancePhase.Breaching: TickBreaching(party, plan, leader, session, now); break;
			case AdvancePhase.Entering: TickEntering(party, plan); break;
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

		plan.Phase = AdvancePhase.Breaching;
		plan.PhaseStartTime = now;
		plan.LastProgressTime = now;
		plan.AssignDirty = true;
		foreach (var m in plan.Ranks.Keys) ChangeReason(m, WaitReason.BreachingDoor);
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} 문 앞 진형 완료({plan.Ranks.Count}명) — 리더 {leader.name}의 지시로 문 파괴 시작");
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
				float limit = AIConfigLoader.Behavior?.doorApproachMaxBlockedSeconds ?? 30f;
				Abort(party, $"문 파괴 진행이 {limit:F0}초 넘게 없음(접근 불가 추정)");
				break;
		}
	}

	private static void BeginEntering(Party party, PartyAdvancePlan plan, Human leader, GameSession session, float now)
	{
		plan.Phase = AdvancePhase.Entering;
		plan.PhaseStartTime = now;
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
				Vector2Int slot = PartyFormationMath.TryPickNearestFreeTile(ideal, t => IsSlotFree(session, member, t, plan.Floor, plan.ToRoom), claimed, PartyFormationMath.DefaultSearchRadius, out var picked)
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
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} 진입로 확보 — 리더 {leader.name}이(가) 진형을 유지한 채 랭크 순(근접 → 리더 → 원거리) 입장을 지시");
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

	private static void TickEntering(Party party, PartyAdvancePlan plan)
	{
		foreach (var m in plan.Ranks.Keys) ReleaseIfEntered(m, plan);

		var ranks = new List<int>(plan.Ranks.Count);
		var passed = new List<bool>(plan.Ranks.Count);
		foreach (var kv in plan.Ranks)
		{
			var w = kv.Key.currentWait;
			// 이미 진입해 풀렸거나, 입장에 참여하지 않는 유닛(다른 대기로 덮임·자리에 못 가 포기)은 뒤 랭크를 붙잡지 않는다.
			ranks.Add(kv.Value);
			passed.Add(w == null || !IsPlanWait(w.Reason) || w.IsParked);
		}

		bool allDone = true;
		foreach (var kv in plan.Ranks)
		{
			var m = kv.Key;
			var w = m.currentWait;
			if (w == null || !IsPlanWait(w.Reason) || w.IsParked) continue;
			if (!w.Released && PartyFormationMath.CanReleaseRank(kv.Value, ranks, passed))
			{
				w.Released = true;
				w.AtSlot = false;
				w.BlockedSince = -1f;
				w.BlockedLogged = false;
				m.waitStuckTurns = 0;
			}
			allDone = false; // 아직 문을 지나지 못한 유닛이 남았다
		}
		if (allDone) Finish(party, plan, $"입장 완료 — {plan.EnteredCount}명이 문을 지나 개인 행동으로 복귀(나머지 {plan.Ranks.Count - plan.EnteredCount}명은 포기·다른 대기)");
	}

	// ── 종료 ─────────────────────────────────────────────────────────────────────────────

	// 계획을 중단한다. retry=true(기본)면 파티가 멈추지 않게 이어 간다 — 시도가 남았으면 ReadyToAdvance를 되살려 HumanWaveManager가 다시 지시하게 하고(다음은 진형 없이 직행 돌파),
	// 한도(MaxAttempts)에 닿았으면 잠금을 풀고 쿨다운 뒤 처음부터 다시 하게 한다(Party.GiveUpAdvance — 이동 지시는 한 번 소비되고 AdvanceFromRoom은 리더가 방을 떠나야만 풀려, 그냥 두면 영구 정지한다:
	// 플레이 로그 2026-10-01 101~107줄). 리더 없음·퇴각 전환처럼 이동을 이어 갈 수 없는 중단은 retry=false.
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
				note = $" — 재시도({party.AdvanceAttempts}/{MaxAttempts}회 시도함, 다음은 진형 없이 직행 돌파)";
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
		if (plan.Phase == AdvancePhase.Entering && !w.Released) return false;
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
