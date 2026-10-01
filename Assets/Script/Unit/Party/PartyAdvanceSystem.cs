using System.Collections.Generic;
using UnityEngine;
using GrimArchive.Wave;
using Haare.Util.Logger;

public enum AdvancePhase { FormingUp, Breaching, Entering }

// 집결을 마친 파티가 다음 방으로 가는 한 번의 진행 — 문 앞 진형(FormingUp) → 문 파괴(Breaching, 막힌 문이 있을 때만) → 랭크 순 입장(Entering).
// Party.AdvancePlan이 들고 있고, 끝나면(입장 완료·중단) null이 된다.
public class PartyAdvancePlan
{
	public AdvancePhase Phase;
	public int Floor;
	public Vector2Int DoorPos;
	// 리더 방에서 다음 방으로 향하는 축(near 줄 → far 줄 방향, 단위 벡터).
	public Vector2Int Forward;
	// 게이트의 문 타일 두 줄 — 레인 i는 NearTiles[i] → FarTiles[i]를 차례로 지난다. near가 리더 방 쪽.
	public Vector2Int[] NearTiles;
	public Vector2Int[] FarTiles;
	public Room FromRoom;
	public Room ToRoom;
	public readonly Dictionary<Human, int> Ranks = new Dictionary<Human, int>();
	// 진형 자리(입장 전 대기 위치) — 입장 단계에서 아직 출발 허가가 없는 랭크는 여기서 기다린다.
	public readonly Dictionary<Human, Vector2Int> FormSlots = new Dictionary<Human, Vector2Int>();
	// 진형/입장 구역 — 자리 배정에 실제로 쓴 범위(중심 + 가장 먼 자리까지의 반경). 좁아서 자기 자리에 못 들어간 유닛이 구역 안이면 그 자리에서 준비 완료로 인정하는 기준이다.
	public Vector2Int FormCenter;
	public int FormRadius = 1;
	public Vector2Int EntryCenter;
	public int EntryRadius = 1;
	public readonly HashSet<Human> Breachers = new HashSet<Human>();
	public Vector3Int? BreachTile;
	public int BreachLane = -1;
	public float PhaseStartTime;
	public float NextTickTime;
	public float NextDiagTime;
	// 입장 단계에서 문을 지나 개인 행동으로 풀린 유닛 수(로그용).
	public int EnteredCount;
	public float LastProgressTime;
	public float LastLaneHp = float.MaxValue;
}

// 집결 이후 진행의 부수효과 있는 호출부(PartyDoorSearchSystem/PartyCoreReportSystem과 같은 성격) — 순수 계산은 PartyFormationMath. 흐름:
//  Begin(HumanWaveManager가 집결 완료 직후 1회) → Tick(매 프레임, 내부 0.25초 간격으로 단계 전이) / StepMember(TacticalFSMState.ExecuteWait가 유닛마다 매 틱).
// 문 파괴는 리더 지시로만 시작하며 기존 DoorAttack(인류가 점령한 방 안에서만 발동)과 별개다.
public static class PartyAdvanceSystem
{
	private const float TickIntervalSeconds = 0.25f;
	// 한 번의 방 이동에서 진형 계획을 시작할 수 있는 최대 횟수(처음 + 재시도 1회). 넘으면 예전 방식(문 앞 자리로 이동 뒤 개인 행동)으로 폴백한다 — 중단 뒤 파티가 멈추지 않게 하는 장치.
	public const int MaxAttempts = 2;
	// 단계가 이 시간(초) 넘게 끝나지 않으면 미완료 파티원의 상태를 15초마다 로그로 남긴다(원인 진단용, 동작 불변).
	private const float DiagnosticSeconds = 15f;

	public static bool IsPlanWait(WaitReason reason)
		=> reason == WaitReason.FormingUpAtDoor || reason == WaitReason.BreachingDoor || reason == WaitReason.EnteringNextRoom;

	// ── 집결 중 자리 이동 ───────────────────────────────────────────────────────────────────

	// TacticalFSMState.ExecuteWait의 집결 분기가 매 틱 호출한다(rallyGatherNearbyEnabled). 배정받은 자리로 가서 도착해도 대기를 유지하고, 완료는 Party.CheckRallyComplete가 전원 도착 시 일괄 처리한다.
	// 자리가 막히면 같은 방식으로 다른 근처 자리를 다시 고르고(도착 간주 X), 오래 막힌 채(doorApproachMaxBlockedSeconds)일 때만 안전장치로 그 유닛을 집결에서 뺀다.
	public static BTStatus StepRallyGather(Human human, WaitState wait)
	{
		var party = human.party;
		Vector2Int slot = wait.WaitPosition.Value;

		if (PartyFormationMath.IsAtSlot(human.position, slot))
		{
			bool newlyArrived = !wait.AtSlot;
			wait.AtSlot = true;
			wait.BlockedSince = -1f;
			wait.BlockedLogged = false;
			human.waitStuckTurns = 0;
			if (newlyArrived) party?.CheckRallyComplete();
			return human.currentWait == null ? BTStatus.Success : BTStatus.Running; // 전원 도착이면 위 호출이 대기를 비웠다
		}

		wait.AtSlot = false;
		var cfg = AIConfigLoader.Behavior;
		float now = Time.time;
		int distBefore = AIMovementHelper.ChebyshevDistance(human.position, slot);
		AIMovementHelper.MoveTowardsPos(human, slot);
		if (AIMovementHelper.ChebyshevDistance(human.position, slot) < distBefore)
		{
			human.waitStuckTurns = 0;
			wait.BlockedSince = -1f;
			wait.BlockedLogged = false;
			return BTStatus.Running;
		}

		// 거리가 줄지 않았다 — 혼잡일 수 있어 한도까지는 인내한다(집결·조사 이동과 같은 기준).
		if (++human.waitStuckTurns < (cfg?.waitStuckTurnLimit ?? 4)) return BTStatus.Running;
		human.waitStuckTurns = 0;

		if (wait.BlockedSince < 0f) wait.BlockedSince = now;
		else if (now - wait.BlockedSince >= (cfg?.doorApproachMaxBlockedSeconds ?? 30f))
		{
			LogHelper.Log(LogHelper.GAME, $"[파티] {human.name}: 집결 자리 ({slot.x},{slot.y})에 오래 못 가 안전장치로 집결 대기에서 제외합니다");
			human.currentWait = null;
			party?.CheckRallyComplete();
			return BTStatus.Success;
		}

		if (party != null && party.RallyPoint.HasValue)
		{
			// 막힌 자리는 다시 고르지 않는다(RejectedSlots) — 좁은 길에서 같은 두 자리를 오가던 문제(플레이 로그 2026-10-01). 재선택은 이 유닛에서 가장 가까운 구역 안 빈 타일이고,
			// 자기 타일이 빈 자리로 뽑히면(구역 안이고 더 갈 곳이 없음) 그 자리에서 집결 처리된다.
			(wait.RejectedSlots ??= new HashSet<Vector2Int>()).Add(slot);
			var claimed = new HashSet<Vector2Int> { party.RallyPoint.Value };
			foreach (var m in party.Members)
			{
				if (m == null || m == human || m.hp <= 0 || m.currentWait == null || m.currentWait.Reason != WaitReason.AwaitingPartyAtRallyPoint) continue;
				if (m.currentWait.WaitPosition.HasValue) claimed.Add(m.currentWait.WaitPosition.Value);
			}
			if (party.TryPickGatherFallback(human, claimed, wait.RejectedSlots, out Vector2Int alt))
			{
				if (!wait.BlockedLogged)
				{
					wait.BlockedLogged = true;
					LogHelper.Log(LogHelper.GAME, alt == human.position
						? $"[파티] {human.name}: 집결 자리 ({slot.x},{slot.y})에 들어갈 공간이 없어 현재 위치 ({alt.x},{alt.y})에서 집결 처리합니다"
						: $"[파티] {human.name}: 집결 자리 ({slot.x},{slot.y})가 막혀 근처 ({alt.x},{alt.y})로 바꿉니다");
				}
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
		}
		return BTStatus.Running;
	}

	// ── 시작: 문 앞 진형 배정 ───────────────────────────────────────────────────────────────

	// HumanWaveManager가 집결 완료(ReadyToAdvance)로 다음 문이 정해진 순간 호출한다. 진형을 만들 수 없으면(게이트·파티원 없음) false — 호출부가 예전 방 이동으로 폴백한다.
	public static bool Begin(Party party, Vector2Int doorPos, int doorFloor, ICollection<Human> excluded)
	{
		var leader = party?.Leader;
		var session = leader?.Session;
		if (leader == null || session?.cmap == null || session.roomGrid == null) return false;
		if (!TryBuildGate(session, leader.currentRoom, doorPos, doorFloor, out var near, out var far, out var forward)) return false;

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
		};

		var members = new List<Human>();
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0 || (excluded != null && excluded.Contains(m))) continue;
			if (m.currentFloor != doorFloor) continue;
			if (m.isManualMoveCommand && m.playerMoveTarget.HasValue) continue;
			if (m.playerAttackTarget != null) continue;
			// 이미 다른 대기(코어 보고 등) 중이면 덮어쓰지 않는다. 다음 문을 찾으며 리더를 따라다니던 추종(SearchingNextDoor)만 문이 알려진 지금 진형으로 바꾼다.
			if (m.currentWait != null && m.currentWait.Reason != WaitReason.SearchingNextDoor) continue;
			members.Add(m);
		}
		if (members.Count == 0) return false;

		AssignFormation(party, plan, members, session);
		party.AdvancePlan = plan;
		party.AdvanceAttempts++;

		var nearBlocked = new bool[near.Length];
		var farBlocked = new bool[far.Length];
		ComputeBlocked(session, plan, leader, nearBlocked, farBlocked);
		int blockedCount = 0;
		for (int i = 0; i < near.Length; i++) blockedCount += (nearBlocked[i] ? 1 : 0) + (farBlocked[i] ? 1 : 0);
		LogHelper.Log(LogHelper.GAME, $"{PartyTag(party)} 방 이동 지시({party.AdvanceAttempts}/{MaxAttempts}회째) — 사유: 집결 완료, 목표 문 ({doorPos.x},{doorPos.y}), 막힌 문 타일 {blockedCount}/{near.Length * 2}, 문 앞 진형 {RankSummary(plan)}");
		return true;
	}

	// 랭크별로 레인을 나누고 진형 자리를 고른다 — 앞랭크부터 자리를 먼저 차지해 뒷랭크가 앞자리를 빼앗지 않는다.
	private static void AssignFormation(Party party, PartyAdvancePlan plan, List<Human> members, GameSession session)
	{
		Vector2Int lateral = PartyFormationMath.LateralAxis(plan.Forward);
		// 0랭크는 문 앞 통과 구간(DoorClearance) 바로 밖 — 문서 규정(05번 3장)대로 대기 중에는 구간을 비우고, 문 파괴를 맡은 유닛만 파괴 단계에서 문 인접 1칸으로 접근한다(StepBreach).
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
				plan.Ranks[member] = rank;
				plan.FormSlots[member] = slot;
				member.currentWait = new WaitState { Reason = WaitReason.FormingUpAtDoor, WaitPosition = slot, WaitFloor = plan.Floor, DoorPosition = plan.DoorPos, Rank = rank };
				member.waitStuckTurns = 0;
				// 전파받은 적 위치로 접근하던 경계는 공동 이동이 시작되면 접는다(03번 1장 50줄).
				if (member.currentAlertSearch != null && member.currentAlertSearch.IsIndirectEnemyApproach) member.currentAlertSearch = null;
			}
		}
		plan.FormRadius = PartyFormationMath.ZoneRadius(plan.FormSlots.Values, frontAnchor);
	}

	private static int RankOf(GameSession session, Party party, Human m)
		=> PartyFormationMath.ResolveRank(m == party.Leader, DungeonEntranceSystem.IsMelee(session, m));

	// 진형·입장 자리로 쓸 수 있는 타일: 그 유닛이 설 수 있고(점유는 무시 — 자리를 점유한 아군은 곧 비킨다), 문 타일·게이트 문턱이 아니며, 지정한 방 안.
	// ignoreUnits=false는 막힌 뒤 재선택용 — 지금 다른 유닛이 실제로 서 있는 타일(먼저 도착해 남의 자리에 선 유닛 포함)은 뺀다.
	// clearOf가 있으면 그 문 타일들의 통과 구간(PartyFormationMath.DoorClearance) 안은 대기 자리로 쓰지 않는다 — 진형 대기용이고, 입장 자리에는 넘기지 않는다.
	private static bool IsSlotFree(GameSession session, Human m, Vector2Int t, int floor, Room room, bool ignoreUnits = true, Vector2Int[] clearOf = null)
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

	// ── 단계 전이 ───────────────────────────────────────────────────────────────────────────

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

		// 안전장치 — 개별 이동 포기(IsParked)가 못 푸는 정체(전투·경계가 끝나지 않는 유닛 등)로 단계가 영구히 멈추지 않게 한다. 문 파괴 단계는 자체 진행 감시(TickBreaching)를 쓴다.
		// 기준은 기존 doorApproachMaxBlockedSeconds의 2배(내부 판단). 상한에 닿으면 계획을 중단하지 않고, 집결과 같은 규칙으로 못 선 인원을 현재 위치에서 인정하고 다음 단계로 진행한다
		// (사용자 확정 2026-10-01 "공간 부족·못 서는 유닛은 현재 위치 인정"). 그 전에는 15초마다 미완료 파티원의 상태를 로그로 남긴다.
		if (plan.Phase != AdvancePhase.Breaching)
		{
			float phaseLimit = 2f * (AIConfigLoader.Behavior?.doorApproachMaxBlockedSeconds ?? 30f);
			float elapsed = now - plan.PhaseStartTime;
			if (elapsed >= phaseLimit) ParkStragglersAtLimit(party, plan, phaseLimit);
			else if (elapsed >= DiagnosticSeconds && now >= plan.NextDiagTime)
			{
				plan.NextDiagTime = now + DiagnosticSeconds;
				string lagging = DescribeNotDone(plan);
				if (lagging != null) LogHelper.Log(LogHelper.GAME, $"{PartyTag(party)} {plan.Phase} 단계 {elapsed:F0}초째 — 미완료: {lagging}");
			}
		}

		switch (plan.Phase)
		{
			case AdvancePhase.FormingUp: TickFormingUp(party, plan, leader, session, now); break;
			case AdvancePhase.Breaching: TickBreaching(party, plan, leader, session, now); break;
			case AdvancePhase.Entering: TickEntering(party, plan, leader, now); break;
		}
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
			plan.Breachers.Remove(m);
		}
	}

	private static void TickFormingUp(Party party, PartyAdvancePlan plan, Human leader, GameSession session, float now)
	{
		foreach (var kv in plan.Ranks)
		{
			if (!IsMemberReady(kv.Key)) return;
		}

		var nearBlocked = new bool[plan.NearTiles.Length];
		var farBlocked = new bool[plan.FarTiles.Length];
		ComputeBlocked(session, plan, leader, nearBlocked, farBlocked);
		if (PartyFormationMath.TryFindOpenLane(nearBlocked, farBlocked, out _))
		{
			LogHelper.Log(LogHelper.GAME, $"{PartyTag(party)} 문 앞 진형 완료 — 진입로가 이미 열려 있어 문 파괴 없이 입장");
			BeginEntering(party, plan, leader, session, now);
			return;
		}

		plan.Phase = AdvancePhase.Breaching;
		plan.PhaseStartTime = now;
		plan.LastProgressTime = now;
		plan.LastLaneHp = float.MaxValue;
		plan.BreachLane = -1;
		foreach (var m in plan.Ranks.Keys) ChangeReason(m, WaitReason.BreachingDoor);
		LogHelper.Log(LogHelper.GAME, $"{PartyTag(party)} 문 앞 진형 완료({plan.Ranks.Count}명) — 리더 {leader.name}의 지시로 최전방이 문 파괴 시작");
	}

	private static void TickBreaching(Party party, PartyAdvancePlan plan, Human leader, GameSession session, float now)
	{
		var nearBlocked = new bool[plan.NearTiles.Length];
		var farBlocked = new bool[plan.FarTiles.Length];
		ComputeBlocked(session, plan, leader, nearBlocked, farBlocked);
		if (PartyFormationMath.TryFindOpenLane(nearBlocked, farBlocked, out int openLane))
		{
			LogHelper.Log(LogHelper.GAME, $"{PartyTag(party)} 문 파괴 완료 — 레인 {openLane} 진입로 확보({now - plan.PhaseStartTime:F1}초 소요)");
			BeginEntering(party, plan, leader, session, now);
			return;
		}

		if (plan.BreachLane < 0) plan.BreachLane = PartyFormationMath.PickBreachLane(nearBlocked, farBlocked, LaneDistances(plan));
		int lane = plan.BreachLane;
		int row = PartyFormationMath.NextBreachRow(nearBlocked[lane], farBlocked[lane]);
		Vector2Int tile = row == 1 ? plan.FarTiles[lane] : plan.NearTiles[lane];
		plan.BreachTile = new Vector3Int(tile.x, tile.y, plan.Floor);
		UpdateBreachers(plan, tile);

		// 진행 감시 — 레인 문 체력 합이 줄거나 문이 사라지면 진행이다. 접근 불가·무한 대치로 doorApproachMaxBlockedSeconds 넘게 진행이 없으면 계획을 포기한다.
		float hp = LaneDoorHp(session, plan, lane);
		if (hp < plan.LastLaneHp - 0.001f) plan.LastProgressTime = now;
		plan.LastLaneHp = hp;
		float limit = AIConfigLoader.Behavior?.doorApproachMaxBlockedSeconds ?? 30f;
		if (now - plan.LastProgressTime >= limit) Abort(party, $"문 파괴 진행이 {limit:F0}초 넘게 없음(접근 불가 추정)");
	}

	private static void BeginEntering(Party party, PartyAdvancePlan plan, Human leader, GameSession session, float now)
	{
		plan.Phase = AdvancePhase.Entering;
		plan.PhaseStartTime = now;
		plan.Breachers.Clear();
		plan.BreachTile = null;
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
		LogHelper.Log(LogHelper.GAME, $"{PartyTag(party)} 진입로 확보 — 리더 {leader.name}이(가) 진형을 유지한 채 랭크 순(근접 → 리더 → 원거리) 입장을 지시");
	}

	// 진입 판정: 문 먼 쪽 줄을 지난 유닛은 그 즉시 개인 행동으로 푼다 — 다음 방 안쪽 자리에 전원이 모일 때까지 붙들지 않는다(사용자 확정 2026-10-01 "다음 방 진입 판정 후 바로 개인 행동").
	// 05번 1장·04번 "다음 방 진입 후에는 개인 탐색·임무 행동을 판단"과 같은 방향이다. 입장 자리는 문을 지나는 방향을 정하는 목적지일 뿐이다.
	private static bool ReleaseIfEntered(Human m, PartyAdvancePlan plan)
	{
		var w = m.currentWait;
		if (w == null || w.Reason != WaitReason.EnteringNextRoom) return false;
		if (!PartyFormationMath.HasPassedGate(m.position, plan.FarTiles[0], plan.Forward)) return false;
		m.currentWait = null;
		m.waitStuckTurns = 0;
		plan.EnteredCount++;
		return true;
	}

	private static void TickEntering(Party party, PartyAdvancePlan plan, Human leader, float now)
	{
		foreach (var kv in plan.Ranks) ReleaseIfEntered(kv.Key, plan);

		var ranks = new List<int>(plan.Ranks.Count);
		var passed = new List<bool>(plan.Ranks.Count);
		foreach (var kv in plan.Ranks)
		{
			var w = kv.Key.currentWait;
			// 이미 진입해 풀렸거나, 입장에 참여하지 않는 유닛(다른 대기로 덮임·자리에 못 가 포기)은 뒤 랭크를 붙잡지 않는다.
			bool done = w == null || !IsPlanWait(w.Reason) || w.IsParked;
			ranks.Add(kv.Value);
			passed.Add(done);
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

	// ── 종료 ────────────────────────────────────────────────────────────────────────────────

	// retry=true(기본)면 중단 뒤 파티가 멈추지 않게 ReadyToAdvance를 되살려 HumanWaveManager가 다시 지시하게 한다 — 시도가 MaxAttempts에 닿았으면 그 지시가 예전 방식으로 폴백한다.
	// 중단 후에도 AdvanceFromRoom은 리더가 방을 떠나야만 풀리고 이동 지시는 한 번 소비되므로, 되살리지 않으면 파티가 영구히 멈춘다(플레이 로그 2026-10-01 101~107줄).
	// 리더 없음·퇴각 전환처럼 이동을 이어 갈 수 없는 중단은 retry=false.
	public static void Abort(Party party, string reason, bool retry = true)
	{
		var plan = party?.AdvancePlan;
		if (plan == null) return;
		string note = "";
		if (retry)
		{
			party.ReadyToAdvance = true;
			note = party.AdvanceAttempts < MaxAttempts
				? $" — 재시도({party.AdvanceAttempts}/{MaxAttempts}회 시도함)"
				: $" — 시도 한도({MaxAttempts}회), 예전 방식으로 이동";
		}
		Finish(party, plan, $"방 이동 중단 — {reason}, 전원 개인 행동으로 복귀{note}");
	}

	// 단계 상한에 닿았을 때: 자리에 못 선 파티원은 그 자리에서 준비 완료(IsParked)로 인정해 다음 단계가 진행되게 한다.
	private static void ParkStragglersAtLimit(Party party, PartyAdvancePlan plan, float limit)
	{
		System.Text.StringBuilder names = null;
		foreach (var kv in plan.Ranks)
		{
			var m = kv.Key;
			var w = m.currentWait;
			if (w == null || !IsPlanWait(w.Reason) || w.IsParked || IsMemberDone(m, plan)) continue;
			w.IsParked = true;
			(names ??= new System.Text.StringBuilder()).Append(DescribeMember(m, SlotOf(m, plan))).Append(' ');
		}
		if (names != null)
			LogHelper.Log(LogHelper.GAME, $"{PartyTag(party)} {plan.Phase} 단계 {limit:F0}초 경과 — 자리에 못 선 [{names.ToString().TrimEnd()}]을(를) 현재 위치에서 인정하고 다음으로 진행합니다");
	}

	// 이 단계에서 파티원이 할 일을 끝냈는가 — 진형: 자리 근처 도착, 입장: 출발 허가를 받고 입장 자리에 도착. (다른 대기로 덮이거나 포기한 유닛은 호출부가 따로 거른다.)
	private static bool IsMemberDone(Human m, PartyAdvancePlan plan)
	{
		var w = m.currentWait;
		if (w == null || !IsPlanWait(w.Reason) || w.IsParked || !w.WaitPosition.HasValue) return true;
		if (plan.Phase == AdvancePhase.Entering && !w.Released) return false;
		return PartyFormationMath.IsAtSlot(m.position, w.WaitPosition.Value);
	}

	private static Vector2Int? SlotOf(Human m, PartyAdvancePlan plan)
	{
		var w = m.currentWait;
		if (w == null) return null;
		if (w.Reason == WaitReason.EnteringNextRoom && !w.Released && plan.FormSlots.TryGetValue(m, out var hold)) return hold;
		return w.WaitPosition;
	}

	private static string DescribeNotDone(PartyAdvancePlan plan)
	{
		System.Text.StringBuilder sb = null;
		foreach (var kv in plan.Ranks)
		{
			if (IsMemberDone(kv.Key, plan)) continue;
			(sb ??= new System.Text.StringBuilder()).Append(DescribeMember(kv.Key, SlotOf(kv.Key, plan))).Append(' ');
		}
		return sb?.ToString().TrimEnd();
	}

	// 진단 로그 한 줄 — 대기 중인 유닛이 왜 자리에 못 서는지 가리는 용도. FSM 상태 라벨(GetSubLabel)은 대기를 경계보다 먼저 판정해 경계 중에도 "전술(대기)"로 보이므로
	// 경계 종류·보이는 적·합류 대기를 따로 적는다. 집결 로그(Party.DescribeActiveRally)도 같은 함수를 쓴다.
	public static string DescribeMember(Human m, Vector2Int? slot)
	{
		var sb = new System.Text.StringBuilder(m.name).Append('[');
		string state = m.fsm?.CurrentState?.GetType().Name.Replace("FSMState", "") ?? "상태 없음";
		sb.Append(state);
		var a = m.currentAlertSearch;
		if (a != null) sb.Append(", 경계=").Append(AlertKind(a)).Append(' ').Append(a.ElapsedSeconds.ToString("F0")).Append('초');
		if (m.personalSpottedEnemies.Count > 0) sb.Append(", 보이는 적 ").Append(m.personalSpottedEnemies.Count).Append('명');
		if (m.currentJoinCombatWait != null) sb.Append(", 전투 합류 대기");
		if (m.currentInvestigation != null) sb.Append(", 조사 중");
		var w = m.currentWait;
		if (w != null && w.IsParked) sb.Append(", 포기");
		if (w != null && w.Reason == WaitReason.EnteringNextRoom && !w.Released) sb.Append(", 출발 허가 대기");
		if (slot.HasValue) sb.Append(", 자리까지 ").Append(AIMovementHelper.ChebyshevDistance(m.position, slot.Value)).Append('칸');
		return sb.Append(']').ToString();
	}

	private static string AlertKind(AlertSearchState a)
	{
		if (a.IsDeathSearch) return "사망 수색";
		if (a.IsSoundResponse) return "소리 반응";
		if (a.IsUnidentifiedAttackSearch) return "미식별 공격 수색";
		if (a.IsPostCombatSweep) return "전투 후 스윕";
		if (a.IsIndirectEnemyApproach) return "전파 적 접근";
		if (a.IsAttackDirectionSearch) return "공격 방향 수색";
		return "일반 경계";
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
		LogHelper.Log(LogHelper.GAME, $"{PartyTag(party)} {message}");
	}

	// ── 유닛별 한 틱 ────────────────────────────────────────────────────────────────────────

	// TacticalFSMState.ExecuteWait가 FormingUpAtDoor/BreachingDoor/EnteringNextRoom 대기마다 매 틱 호출한다. Success = 대기 종료(계획이 없어졌거나 층을 떠남), Running = 계속.
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

		if (wait.Reason == WaitReason.BreachingDoor && plan.Breachers.Contains(human) && plan.BreachTile.HasValue)
			return StepBreach(human, wait, plan);

		// 진입 판정(문 먼 쪽 줄을 지남) — 파티 쪽 Tick을 기다리지 않고 이 유닛이 곧바로 개인 행동으로 돌아간다.
		if (wait.Reason == WaitReason.EnteringNextRoom && ReleaseIfEntered(human, plan)) return BTStatus.Success;

		bool entering = wait.Reason == WaitReason.EnteringNextRoom && wait.Released;
		Vector2Int slot = entering ? wait.WaitPosition.Value : plan.FormSlots[human];
		return StepGoToSlot(human, wait, plan, slot, entering);
	}

	// 문 채널링 — 인접 1칸에서만 데미지가 들어가므로(UnitFunction.OnUpdate) 문 타일 인접까지 가서 SetAttackObjectTarget만 건다. 접근이 막히면 잠시 쉬었다 다시 시도하고,
	// 파티 차원의 포기는 Tick의 진행 감시가 맡는다.
	private static BTStatus StepBreach(Human human, WaitState wait, PartyAdvancePlan plan)
	{
		Vector3Int tile3 = plan.BreachTile.Value;
		var tile = new Vector2Int(tile3.x, tile3.y);
		// 문은 1×2 묶음이라 어느 칸에든 인접하면 된다 — 이 유닛에서 가장 가까운 칸을 접근·채널링 대상으로 삼는다. 이미 사라진 문(다음 틱에 대상이 갱신됨)이면 그대로 둔다.
		if (human.Session != null && human.Session.objectGrid.TryGetValue(tile3, out var breachDoor) && breachDoor.OccupiedTiles != null)
		{
			tile = breachDoor.NearestTileTo(human.position);
			tile3 = new Vector3Int(tile.x, tile.y, tile3.z);
		}

		if (AIMovementHelper.IsAdjacent(human.position, tile))
		{
			human.SetAttackObjectTarget(tile3);
			wait.AtSlot = true;
			wait.BlockedSince = -1f;
			human.waitStuckTurns = 0;
			return BTStatus.Running;
		}

		wait.AtSlot = false;
		if (human.currentAttackObjectTarget.HasValue) human.ClearAttackObjectTarget(); // 밀려나 인접이 아니면 채널링은 이미 끊긴 것
		float now = Time.time;
		if (now < wait.NextDoorScanTime) return BTStatus.Running;

		int distBefore = AIMovementHelper.ChebyshevDistance(human.position, tile);
		AIMovementHelper.MoveTowardsPos(human, tile);
		if (AIMovementHelper.ChebyshevDistance(human.position, tile) < distBefore)
		{
			human.waitStuckTurns = 0;
			return BTStatus.Running;
		}
		if (++human.waitStuckTurns >= (AIConfigLoader.Behavior?.waitStuckTurnLimit ?? 4))
		{
			human.waitStuckTurns = 0;
			wait.NextDoorScanTime = now + (AIConfigLoader.Behavior?.doorApproachHoldRetrySeconds ?? 1f);
		}
		return BTStatus.Running;
	}

	// 진형 자리(또는 입장 자리)로 이동·대기. 도착하면 움직이지 않고, 막히면 같은 방 안에서 다른 자리를 점진 확장으로 다시 고른다.
	// doorApproachMaxBlockedSeconds 넘게 막힌 채면 이 유닛만 자리 이동을 포기(IsParked)해 파티 전체가 교착되지 않게 한다.
	private static BTStatus StepGoToSlot(Human human, WaitState wait, PartyAdvancePlan plan, Vector2Int slot, bool entering)
	{
		if (wait.IsParked) return BTStatus.Running;

		if (PartyFormationMath.IsAtSlot(human.position, slot))
		{
			wait.AtSlot = true;
			wait.BlockedSince = -1f;
			wait.BlockedLogged = false;
			human.waitStuckTurns = 0;
			return BTStatus.Running;
		}

		wait.AtSlot = false;
		float now = Time.time;
		if (now < wait.NextDoorScanTime) return BTStatus.Running; // 막혀 보류 중 — 다음 재시도까지 제자리

		var cfg = AIConfigLoader.Behavior;
		int distBefore = AIMovementHelper.ChebyshevDistance(human.position, slot);
		bool moved = AIMovementHelper.MoveTowardsPos(human, slot);
		if (AIMovementHelper.ChebyshevDistance(human.position, slot) < distBefore)
		{
			human.waitStuckTurns = 0;
			wait.BlockedSince = -1f;
			wait.BlockedLogged = false;
			return BTStatus.Running;
		}

		if (++human.waitStuckTurns < (cfg?.waitStuckTurnLimit ?? 4)) return BTStatus.Running;
		human.waitStuckTurns = 0;

		if (wait.BlockedSince < 0f) wait.BlockedSince = now;
		else if (now - wait.BlockedSince >= (cfg?.doorApproachMaxBlockedSeconds ?? 30f))
		{
			wait.IsParked = true;
			LogHelper.Log(LogHelper.GAME, $"[파티] {human.name}: 자리 ({slot.x},{slot.y})에 오래 못 가 안전장치로 {(entering ? "입장" : "진형")} 이동을 포기하고 제자리에서 기다립니다");
			return BTStatus.Running;
		}

		// 막힌 자리는 다시 고르지 않고(RejectedSlots), 이 유닛에서 가장 가까운 구역 안 "지금 아무도 서 있지 않은" 타일로 옮긴다 — 좁은 길에서 같은 두 자리를 오가거나 남의 자리에 선 유닛을
		// 향해 계속 밀고 들어가던 문제(플레이 로그 2026-10-01 집결 단계에서 확인). 자기 타일이 뽑히면(구역 안이고 더 갈 곳이 없음) 그 자리에서 준비 완료로 인정된다.
		(wait.RejectedSlots ??= new HashSet<Vector2Int>()).Add(slot);
		var claimed = ClaimedSlots(plan, human);
		Room room = entering ? plan.ToRoom : plan.FromRoom;
		Vector2Int zoneCenter = entering ? plan.EntryCenter : plan.FormCenter;
		int zoneRadius = entering ? plan.EntryRadius : plan.FormRadius;
		var session = human.Session;
		var rejected = wait.RejectedSlots;
		if (PartyFormationMath.TryPickNearestFreeTile(human.position,
				t => !rejected.Contains(t) && PartyFormationMath.IsWithinZone(t, zoneCenter, zoneRadius)
					&& IsSlotFree(session, human, t, plan.Floor, room, ignoreUnits: false, clearOf: entering ? null : plan.NearTiles),
				claimed, PartyFormationMath.DefaultSearchRadius, out var alt))
		{
			if (!wait.BlockedLogged)
			{
				wait.BlockedLogged = true;
				LogHelper.Log(LogHelper.GAME, alt == human.position
					? $"[파티] {human.name}: {(entering ? "입장" : "진형")} 자리 ({slot.x},{slot.y})에 들어갈 공간이 없어 현재 위치 ({alt.x},{alt.y})에서 대기합니다"
					: $"[파티] {human.name}: {(entering ? "입장" : "진형")} 자리 ({slot.x},{slot.y})가 막혀 근처 ({alt.x},{alt.y})로 바꿉니다");
			}
			if (entering) wait.WaitPosition = alt;
			else
			{
				plan.FormSlots[human] = alt;
				if (wait.Reason != WaitReason.EnteringNextRoom) wait.WaitPosition = alt;
			}
		}
		else if (PartyFormationMath.IsWithinZone(human.position, zoneCenter, zoneRadius))
		{
			// 구역 안에 빈 타일이 하나도 없다 — 이 유닛은 더 못 들어가므로 선 자리에서 준비 완료로 인정한다.
			wait.IsParked = true;
			LogHelper.Log(LogHelper.GAME, $"[파티] {human.name}: {(entering ? "입장" : "진형")} 구역 안에 빈 자리가 없어 현재 위치 ({human.position.x},{human.position.y})에서 대기합니다");
			return BTStatus.Running;
		}

		// 한 걸음도 못 움직였다면 잠시 기다렸다 다시 시도한다. 움직이고는 있었다면(우회·혼잡 셔플) 곧바로 이어 간다.
		if (!moved) wait.NextDoorScanTime = now + (cfg?.doorApproachHoldRetrySeconds ?? 1f);
		return BTStatus.Running;
	}

	// ── 보조 ────────────────────────────────────────────────────────────────────────────────

	private static HashSet<Vector2Int> ClaimedSlots(PartyAdvancePlan plan, Human except)
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

	// 준비 완료 — 자리 근처에 서 있거나, 다른 대기로 덮였거나(코어 보고 등), 포기(IsParked)했거나, 이미 해제됐다. 전투 등으로 대기 처리가 못 돌던 유닛은 위치로 직접 판정한다.
	private static bool IsMemberReady(Human m)
	{
		var w = m.currentWait;
		if (w == null || !IsPlanWait(w.Reason) || w.IsParked) return true;
		if (!w.WaitPosition.HasValue) return true;
		return PartyFormationMath.IsAtSlot(m.position, w.WaitPosition.Value);
	}

	// 외부 시스템(유닛 사망 등)이 보는 막힘 판정은 실제 통행과 같은 기준(DoorSystem.IsBlockedByClosedDoor) — 대표로 리더를 쓴다(인류 전원이 같은 진영).
	private static void ComputeBlocked(GameSession session, PartyAdvancePlan plan, Human representative, bool[] nearBlocked, bool[] farBlocked)
	{
		for (int i = 0; i < plan.NearTiles.Length; i++)
		{
			nearBlocked[i] = session.IsBlockedByClosedDoor(new Vector3Int(plan.NearTiles[i].x, plan.NearTiles[i].y, plan.Floor), representative);
			farBlocked[i] = session.IsBlockedByClosedDoor(new Vector3Int(plan.FarTiles[i].x, plan.FarTiles[i].y, plan.Floor), representative);
		}
	}

	// 레인별 접근 거리 — 계획 구성원 중 그 레인 near 타일에 가장 가까운 유닛까지의 체비셰프 거리(파괴 레인 동률 판정용).
	private static int[] LaneDistances(PartyAdvancePlan plan)
	{
		var d = new int[plan.NearTiles.Length];
		for (int i = 0; i < d.Length; i++)
		{
			int best = int.MaxValue;
			foreach (var m in plan.Ranks.Keys)
				best = Mathf.Min(best, AIMovementHelper.ChebyshevDistance(m.position, plan.NearTiles[i]));
			d[i] = best;
		}
		return d;
	}

	private static float LaneDoorHp(GameSession session, PartyAdvancePlan plan, int lane)
	{
		float sum = 0f;
		foreach (var t in new[] { plan.NearTiles[lane], plan.FarTiles[lane] })
		{
			if (session.objectGrid.TryGetValue(new Vector3Int(t.x, t.y, plan.Floor), out var obj) && obj != null && obj.DoorHp > 0f) sum += obj.DoorHp;
		}
		return sum;
	}

	// 문 파괴 담당: 0랭크(근접) 전원. 근접이 없으면 목표 문 타일에 가장 가까운 한 명이 맡는다.
	private static void UpdateBreachers(PartyAdvancePlan plan, Vector2Int tile)
	{
		plan.Breachers.Clear();
		foreach (var kv in plan.Ranks)
			if (kv.Value == 0) plan.Breachers.Add(kv.Key);
		if (plan.Breachers.Count > 0) return;

		Human closest = null;
		int best = int.MaxValue;
		foreach (var m in plan.Ranks.Keys)
		{
			int d = AIMovementHelper.ChebyshevDistance(m.position, tile);
			if (d < best) { best = d; closest = m; }
		}
		if (closest != null) plan.Breachers.Add(closest);
	}

	private static bool IsPlanDoorTile(PartyAdvancePlan plan, Vector3Int pos)
	{
		if (pos.z != plan.Floor) return false;
		var p = new Vector2Int(pos.x, pos.y);
		for (int i = 0; i < plan.NearTiles.Length; i++)
			if (plan.NearTiles[i] == p || plan.FarTiles[i] == p) return true;
		return false;
	}

	private static string PartyTag(Party party) => $"[파티] {party.Name}({party.Type.ToKorean()})";

	private static string RankSummary(PartyAdvancePlan plan)
	{
		int[] counts = new int[PartyFormationMath.MaxRank + 1];
		foreach (var rank in plan.Ranks.Values) counts[rank]++;
		return $"근접 {counts[0]}명·리더 {counts[PartyFormationMath.LeaderRank]}명·원거리 {counts[PartyFormationMath.MaxRank]}명";
	}
}
