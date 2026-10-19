using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;

// 인류 점령 파티 구동(세션 연결부) — 코어 점령 설계명세서 v0.2 12~14장. 점령 파티는 웨이브와 따로 던전에 상주하며(HumanWaveManager가 도착 직후 Adopt로 넘긴다), 방 하나씩
//   ① 현재 방에 기초 시설(방 쪽 인류 문)을 짓고 → ② 기초 거점이 되면 집결해 → ③ 안전한 이웃 방으로 진출하고(PartyMissionDriver) → 후퇴하면 가장 가까운 방어/기초 거점에서 쉬었다 80% 이상 살아 있으면 재출격한다(PartyRetreatSystem).
// 방어 거점으로 정한 방(14장)은 후퇴·휴식을 마친 뒤 예정 방어 시설까지 지어야 재출격한다(13장). 순수 판정은 OccupationMath에 있다.
public class OccupationSystem
{
	// 방어 시설 자리표시자 오브젝트 — 방어건물 시스템이 아직 없어 기능 없는 표식이다(함정은 진영 소유 개념이 없어 인류도 해치므로 쓰지 않는다).
	public const string DefenseFacilityTag = "Object/Passable/DefenseFacility";

	// 진출할 방의 위험도를 모르는 경우도 후보로 볼지 — 문서(12-1장)의 "전달받은 확인 결과"는 정보 전달 체계가 아직 없어 유효한 정보가 없는 게 기본 상태라, 끄면 점령 파티가 위험도 기록이 생길 때까지 진출하지 못한다.
	public static bool AllowUnknownAdvance = true;

	// 현장에서 일하는 인원으로 인정하는 마지막 일 시각의 유효 시간 — 전투·경계가 우선해 일하는 리프가 안 돈 유닛은 진행에서 빠진다.
	private const float WorkerFreshSeconds = 2.5f;
	private const float AssignIntervalSeconds = 1f;
	private const float LogIntervalSeconds = 10f;

	private sealed class WorkProgress
	{
		public float Basic;
		public float Defense;
		public float NextLogTime;
	}

	private IObjectResolver _resolver;
	private GameSession Session => _cachedSession ??= _resolver.Resolve<GameSession>();
	private GameSession _cachedSession;

	private readonly List<Party> _parties = new List<Party>();
	private readonly Dictionary<Party, PartyMissionDriver> _drivers = new Dictionary<Party, PartyMissionDriver>();
	private readonly Dictionary<Party, Room> _targets = new Dictionary<Party, Room>();
	private readonly Dictionary<Party, float> _nextAssignTime = new Dictionary<Party, float>();
	private readonly Dictionary<Party, float> _nextPickTime = new Dictionary<Party, float>();
	private readonly Dictionary<Room, WorkProgress> _work = new Dictionary<Room, WorkProgress>();
	private readonly List<Room> _bases = new List<Room>();
	private readonly Dictionary<int, Dictionary<int, int>> _hopsByFloor = new Dictionary<int, Dictionary<int, int>>();

	// 가장 최근에 기초 시설을 완공한 거점 — 점령 병력이 거점을 옮기며 전진하는 "전방"이다(14-3장 기존 거점의 이전은 한 개로 센다).
	public Room FrontBase { get; private set; }
	// 점령 집단의 거점 정원(처음 출격 인원, 자리표시자 — 문서가 정원 수치를 정하지 않음, 14장).
	public int GroupCapacity { get; private set; }
	public IReadOnlyList<Party> Parties => _parties;
	public IReadOnlyList<Room> Bases => _bases;

	[Inject]
	public void Construct(IObjectResolver resolver) => _resolver = resolver;

	// ─────────────────────────── 파티 입양 ───────────────────────────

	// 점령 파티를 이 시스템이 구동하도록 넘긴다 — 웨이브가 던전 도착을 확인한 직후(HumanWaveManager.TryHandOffToOccupation) 또는 debug 스폰이 부른다.
	public void Adopt(Party party)
	{
		if (party == null || _parties.Contains(party)) return;
		party.Type = PartyType.Occupy;
		_parties.Add(party);
		_drivers[party] = new PartyMissionDriver();
		GroupCapacity = Mathf.Max(GroupCapacity, party.Retreat.InitialSortieCount);
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] 점령 파티 입양 — {party.Members.Count}명, 거점 정원 {GroupCapacity}(자리표시자)");
	}

	private void Remove(Party party)
	{
		_parties.Remove(party);
		_drivers.Remove(party);
		_targets.Remove(party);
		_nextAssignTime.Remove(party);
		_nextPickTime.Remove(party);
	}

	// ─────────────────────────── 틱 ───────────────────────────

	public void Tick(float deltaTime)
	{
		if (_parties.Count == 0) return;
		for (int i = _parties.Count - 1; i >= 0; i--)
		{
			var party = _parties[i];
			if (party == null || !HasLivingMemberInDungeon(party))
			{
				if (party != null) LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] 던전 안에 산 파티원이 없어 점령 파티를 정리합니다");
				Remove(party);
				continue;
			}
			party.AssignLeaderIfNeeded();
			if (party.Leader == null || party.Leader.hp <= 0f) continue;

			TickAdvance(party);
			TickConstruction(party, deltaTime);
			TickDefenseBuild(party, deltaTime);
		}
	}

	private static bool HasLivingMemberInDungeon(Party party)
	{
		foreach (var m in party.Members)
			if (m != null && m.hp > 0f && m.currentFloor >= 1) return true;
		return false;
	}

	// 집결이 끝나 방 이동 차례가 되면 진출할 방을 고르고 PartyMissionDriver가 문 앞 진형 → 돌파 → 입장 계획을 진행한다.
	private void TickAdvance(Party party)
	{
		if (party.Retreat.IsRetreating) return;
		Room room = party.Leader.currentRoom;
		if (room == null) return;

		if (party.ReadyToAdvance)
		{
			_targets.TryGetValue(party, out Room target);
			bool needPick = target == null || target == room || target.Floor != room.Floor || (target.RoomFaction == FactionType.Human && target.BaseStage != BaseStage.None);
			if (needPick && Time.time >= (_nextPickTime.TryGetValue(party, out float t) ? t : 0f))
			{
				_nextPickTime[party] = Time.time + 1f;
				target = PickAdvanceRoom(party, room);
				if (target != null) LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] 진출할 방 선정 — F{target.Floor} 방 {target.RoomId}({target.RoomName}) (현재 방 {room.RoomId})");
				else LogHelper.Warning(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] 진출할 방이 없어 거점에서 대기합니다(안전한 이웃 방 없음, 미확인 방 허용={AllowUnknownAdvance})");
				_targets[party] = target;
			}
		}

		_targets.TryGetValue(party, out Room driveTarget);
		if (_drivers.TryGetValue(party, out PartyMissionDriver driver)) driver.Tick(party, driveTarget);
	}

	// 현재 방과 게이트로 이어진 이웃 방 중 진출할 곳을 고른다 — 이미 인류 거점인 방은 제외하고, 위험도를 아는 방은 안전 상한 이하일 때만, 모르는 방은 AllowUnknownAdvance일 때만 후보다.
	private Room PickAdvanceRoom(Party party, Room from)
	{
		var session = Session;
		var cmap = session?.cmap;
		if (cmap == null || cmap.map.floors == null || from.Floor < 0 || from.Floor >= cmap.map.floors.Length) return null;
		Floor floor = cmap.map.floors[from.Floor];
		if (floor.gates == null) return null;

		var hops = GetHopsFromExit(from.Floor);
		var neighbors = new List<Room>();
		var candidates = new List<AdvanceCandidate>();
		foreach (var gate in floor.gates)
		{
			int neighborId = gate.roomA == from.RoomId ? gate.roomB : gate.roomB == from.RoomId ? gate.roomA : -1;
			if (neighborId < 0) continue;
			Room neighbor = FindRoom(from.Floor, neighborId);
			if (neighbor == null || neighbors.Contains(neighbor)) continue;

			bool known = party.Leader.personalMap.TryGetKnownRoomDanger(neighborId, out float danger);
			neighbors.Add(neighbor);
			candidates.Add(new AdvanceCandidate
			{
				RoomId = neighborId,
				IsHumanBase = neighbor.RoomFaction == FactionType.Human && neighbor.BaseStage != BaseStage.None,
				DangerKnown = known,
				Danger = danger,
				HopsFromExit = hops != null && hops.TryGetValue(neighborId, out int h) ? h : 0,
			});
		}

		int idx = OccupationMath.PickAdvance(candidates, RetreatSystem.ResortieSafeRoomMaxDanger, AllowUnknownAdvance);
		return idx >= 0 ? neighbors[idx] : null;
	}

	// ─────────────────────────── 기초 시설 건설 ───────────────────────────

	// 점령 파티가 현재 방에서 기초 시설을 짓는다 — 집결·방 이동·후퇴 중이 아니고 리더가 적을 인지하지 않을 때만. 일하는 인원(현장 자리에 선 파티원)만큼 진행되고, 전투가 나면 Combat이 우선해 자연히 멈추며 진행도는 남는다.
	private void TickConstruction(Party party, float deltaTime)
	{
		if (party.Retreat.IsRetreating) return;
		var leader = party.Leader;
		Room room = leader.currentRoom;
		if (room == null || room.Floor < 1 || OccupationMath.IsRoomActivityComplete(room.BaseStage)) return; // 이미 기초 거점이면 더 지을 게 없다
		if (!CanConstructNow(party)) return;

		var work = GetWork(room);
		if (work.Basic <= 0f && work.NextLogTime <= 0f)
		{
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 기초 시설 건설 시작(작업량 {OccupationMath.BasicWorkUnits:F0})");
			SpeechSystem.Say(leader, SpeechId.BaseBuildStart);
		}
		AssignBuilders(party, room, defense: false);

		int workers = CountWorkers(party, room);
		work.Basic += OccupationMath.WorkDelta(workers, deltaTime);
		LogProgress(party, room, work, work.Basic, OccupationMath.BasicWorkUnits, workers, "기초");
		if (OccupationMath.IsWorkComplete(work.Basic, OccupationMath.BasicWorkUnits)) CompleteBasic(party, room);
	}

	private static bool CanConstructNow(Party party)
	{
		if (party.IsRallyActive || party.AdvancePlan != null || party.ReadyToAdvance) return false;
		var leader = party.Leader;
		if (leader.isInDungeonEntranceSequence || leader.pendingStairTargetFloor.HasValue) return false;
		if (leader.personalSpottedEnemies.Count > 0) return false; // 적을 인지하고 있으면 건설을 시작하지 않고 전투한다(12장 "적 발견·피격 시 중단하고 전투")
		return true;
	}

	// 같은 방의 파티원에게 건설 현장 자리를 배정한다 — 방 쪽 문 묶음 곁을 번갈아 쓴다. 이미 다른 대기가 있는 유닛(집결·보고 등)은 건드리지 않는다.
	private void AssignBuilders(Party party, Room room, bool defense)
	{
		if (Time.time < (_nextAssignTime.TryGetValue(party, out float next) ? next : 0f)) return;
		_nextAssignTime[party] = Time.time + AssignIntervalSeconds;

		var anchors = Session.GetRoomDoorAnchors(room);
		var claimed = new HashSet<Vector2Int>();
		foreach (var m in party.Members)
			if (m != null && m.currentWait != null && m.currentWait.Reason == WaitReason.ConstructingBase && m.currentWait.WaitPosition.HasValue)
				claimed.Add(m.currentWait.WaitPosition.Value);

		int index = 0;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f || m.currentFloor != room.Floor || m.currentRoom != room) continue;
			if (RetreatSystem.WantsRetreatState(m)) continue; // 후퇴·도주 이동 중인 인원은 제외 — 거점에서 쉬는 인원(Rest)은 방어 시설 공사에 참여한다
			if (m.currentWait != null && m.currentWait.Reason != WaitReason.SearchingNextDoor && m.currentWait.Reason != WaitReason.ConstructingBase) continue;
			if (m.currentWait != null && m.currentWait.Reason == WaitReason.ConstructingBase) { index++; continue; } // 이미 배정됨

			Vector2Int slot = m.position;
			if (anchors.Count > 0)
			{
				Vector3Int anchor = anchors[index % anchors.Count];
				slot = AIMovementHelper.FindDoorWaitSlot(m, new Vector2Int(anchor.x, anchor.y), claimed);
			}
			claimed.Add(slot);
			index++;
			m.currentWait = new WaitState { Reason = WaitReason.ConstructingBase, WorkRoom = room, WorkIsDefense = defense, WaitPosition = slot, WaitFloor = room.Floor };
			m.waitStuckTurns = 0;
		}
	}

	private static int CountWorkers(Party party, Room room)
	{
		int workers = 0;
		float now = Time.time;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f || m.currentRoom != room) continue;
			var wait = m.currentWait;
			if (wait == null || wait.Reason != WaitReason.ConstructingBase || wait.WorkRoom != room || !wait.AtSlot) continue;
			if (now - wait.LastStepTime > WorkerFreshSeconds) continue;
			workers++;
		}
		return workers;
	}

	// 건설 대기의 한 틱 — TacticalFSMState.ExecuteWait(평소)와 RestPerform(거점 휴식 중 방어 시설 공사)가 부른다. 현장 자리로 가서 서 있는 동안 일하는 것으로 센다(진행도는 TickConstruction/TickDefenseBuild가 인원 수로 계산).
	public BTStatus StepConstruct(Human h, WaitState wait)
	{
		Room room = wait.WorkRoom;
		var party = h.party;
		if (room == null || party == null || !IsWorkOpen(room, wait.WorkIsDefense))
		{
			h.currentWait = null;
			h.waitStuckTurns = 0;
			return BTStatus.Success;
		}
		if (!wait.WaitPosition.HasValue) wait.WaitPosition = h.position;

		Vector2Int slot = wait.WaitPosition.Value;
		int distance = AIMovementHelper.ChebyshevDistance(h.position, slot);
		if (distance <= 0 || (distance <= 1 && h.currentRoom == room))
		{
			wait.AtSlot = true;
			wait.LastStepTime = Time.time;
			h.waitStuckTurns = 0;
			return BTStatus.Running;
		}

		wait.AtSlot = false;
		AIMovementHelper.MoveTowardsPos(h, slot);
		if (AIMovementHelper.ChebyshevDistance(h.position, slot) < distance)
		{
			h.waitStuckTurns = 0;
			return BTStatus.Running;
		}
		if (++h.waitStuckTurns >= (AIConfigLoader.Behavior?.waitStuckTurnLimit ?? 4))
		{
			wait.WaitPosition = h.position; // 현장 자리를 못 잡으면 서 있는 곳에서 일한다 — 방 안이면 충분하다
			h.waitStuckTurns = 0;
		}
		return BTStatus.Running;
	}

	private static bool IsWorkOpen(Room room, bool defense)
		=> defense ? OccupationMath.NeedsDefenseBuild(room.BaseStage, room.DefenseDesignated) : room.BaseStage == BaseStage.None;

	private void CompleteBasic(Party party, Room room)
	{
		var session = Session;
		int doors = session.SetRoomDoorsOwner(room, FactionType.Human);
		session.OffenseProcessor?.ApplyRoomOwnership(room, FactionType.Human); // 먼저 소유를 바꾼다 — 소유가 인류가 아니게 바뀔 때만 거점 단계를 되돌리므로 단계는 그 뒤에 올린다
		room.BaseStage = BaseStage.Basic;
		if (!_bases.Contains(room)) _bases.Add(room);
		FrontBase = room;
		_work.Remove(room);
		ClearConstructionWaits(party);

		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 기초 시설 완공 — 기초 거점 형성(문 {doors}개를 인류 소유로 설치/전환), 전방 거점으로 이동. {DescribeBases()}");
		NoticeCenter.Instance?.PushMomentary($"인류가 {room.Floor}층 {room.RoomName}에 거점을 세웠습니다.", NoticeCenter.WarningColor);
		SpeechSystem.Say(party.Leader, SpeechId.BaseBuilt);

		TryDesignateDefenseBase(party);
	}

	private static void ClearConstructionWaits(Party party)
	{
		foreach (var m in party.Members)
			if (m != null && m.currentWait != null && m.currentWait.Reason == WaitReason.ConstructingBase)
			{
				m.currentWait = null;
				m.waitStuckTurns = 0;
			}
	}

	// ─────────────────────────── 방어 거점 지정·방어 시설 공사 ───────────────────────────

	// 점령 병력이 정원을 채웠고 거점 상한에 여유가 있으면, 가장 가까운 유효한 기초 거점을 다음 방어 거점으로 정한다(14장). 병력이 정원 미만이면 정원을 채울 충원이 올 때까지 지정을 미룬다.
	private void TryDesignateDefenseBase(Party party)
	{
		bool anyDesignated = false;
		foreach (var b in _bases) if (b.DefenseDesignated) { anyDesignated = true; break; }
		if (anyDesignated) return;

		Room candidate = NearestBasicBase(FrontBase);
		if (candidate == null) return;

		int garrison = 0;
		foreach (var m in party.Members) if (m != null && m.hp > 0f && m.currentFloor >= 1) garrison++;
		bool alreadyCounted = candidate == FrontBase;
		if (!OccupationMath.ShouldDesignateDefenseBase(garrison, GroupCapacity, CountedBases(), anyDesignated, alreadyCounted)) return;

		candidate.DefenseDesignated = true;
		LogHelper.Log(LogHelper.GAME, $"[점령] 방어 거점 지정 — F{candidate.Floor} 방 {candidate.RoomId}({candidate.RoomName}): 정원 {garrison}/{GroupCapacity}, 주둔 거점 {CountedBases()}/{OccupationMath.GarrisonBaseCap}. 후퇴·휴식을 마친 뒤 예정 방어 시설을 지어야 재출격한다");
	}

	// 지금 주둔 거점으로 세는 방 수 — 전방 거점 + 방어 거점으로 정한 방(충원 목적지). 둘이 같은 방이면 하나다(14-3장).
	public int CountedBases()
	{
		var counted = new HashSet<Room>();
		if (FrontBase != null && FrontBase.BaseStage != BaseStage.None) counted.Add(FrontBase);
		foreach (var b in _bases) if (b.DefenseDesignated && b.BaseStage != BaseStage.None) counted.Add(b);
		return counted.Count;
	}

	private Room NearestBasicBase(Room from)
	{
		Room best = null;
		int bestDist = int.MaxValue;
		var hops = from != null ? GetHopsFromExit(from.Floor) : null;
		foreach (var b in _bases)
		{
			if (b == null || b.BaseStage == BaseStage.None || (from != null && b.Floor != from.Floor)) continue;
			int d = from == null || hops == null || !hops.TryGetValue(b.RoomId, out int hb) || !hops.TryGetValue(from.RoomId, out int hf) ? 0 : Mathf.Abs(hb - hf);
			if (best == null || d < bestDist) { best = b; bestDist = d; }
		}
		return best;
	}

	// 후퇴·휴식을 마친 점령 파티가 방어 거점으로 정한 기초 거점에서 예정 방어 시설을 짓게 한다 — PartyRetreatSystem.TickRest가 휴식 완료 뒤 재출격 전에 부른다. 공사가 끝나면 true.
	public bool RequestDefenseBuild(Party party, Room room)
	{
		if (room == null || !OccupationMath.NeedsDefenseBuild(room.BaseStage, room.DefenseDesignated)) return true;
		_defenseRequested.Add(room);
		return false;
	}

	private readonly HashSet<Room> _defenseRequested = new HashSet<Room>();

	private void TickDefenseBuild(Party party, float deltaTime)
	{
		var st = party.Retreat;
		Room room = st.BaseRoom;
		if (!st.IsRetreating || st.Phase != PartyRetreatPhase.Resting || room == null || !_defenseRequested.Contains(room)) return;
		if (!OccupationMath.NeedsDefenseBuild(room.BaseStage, room.DefenseDesignated)) { _defenseRequested.Remove(room); return; }

		var work = GetWork(room);
		if (work.Defense <= 0f) LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId} 방어 시설 건설 시작(작업량 {OccupationMath.DefenseWorkUnits:F0}) — 휴식을 마친 파티원이 짓는다");
		AssignBuilders(party, room, defense: true);

		int workers = CountWorkers(party, room);
		work.Defense += OccupationMath.WorkDelta(workers, deltaTime);
		LogProgress(party, room, work, work.Defense, OccupationMath.DefenseWorkUnits, workers, "방어");
		if (OccupationMath.IsWorkComplete(work.Defense, OccupationMath.DefenseWorkUnits)) CompleteDefense(party, room);
	}

	private void CompleteDefense(Party party, Room room)
	{
		room.BaseStage = BaseStage.Defense;
		_defenseRequested.Remove(room);
		_work.Remove(room);
		ClearConstructionWaits(party);
		SpawnDefenseFacility(room);
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 예정 방어 시설 완공 — 방어 거점 형성, 재출격 가능");
		NoticeCenter.Instance?.PushMomentary($"인류가 {room.Floor}층 {room.RoomName}을(를) 방어 거점으로 만들었습니다.", NoticeCenter.WarningColor);
		SpeechSystem.Say(party.Leader, SpeechId.DefenseBuilt);
	}

	private void SpawnDefenseFacility(Room room)
	{
		var session = Session;
		var center = new Vector3Int(Mathf.RoundToInt(room.Bounds.center.x), Mathf.RoundToInt(room.Bounds.center.y), room.Floor);
		Vector3Int pos = session.objectGrid.ContainsKey(center) ? session.FindNearbyFreeObjectTile(center) : center;
		var facility = new InteractableObject("DefenseFacility_" + System.Guid.NewGuid().ToString().Substring(0, 4), pos, 0f, 0f, new List<string> { DefenseFacilityTag });
		session.SpawnObject(facility, new Color(1f, 0.6f, 0.2f));
	}

	// debug 전용 — 점령 파티 없이 방을 곧바로 인류 기초(또는 방어) 거점으로 만든다. 후퇴 목적지·재출격 확인용이다.
	public void DebugMakeBase(Room room, bool defense)
	{
		if (room == null || room.Floor < 1) return;
		Session.SetRoomDoorsOwner(room, FactionType.Human);
		Session.OffenseProcessor?.ApplyRoomOwnership(room, FactionType.Human);
		bool wasDefense = room.BaseStage == BaseStage.Defense;
		room.BaseStage = defense ? BaseStage.Defense : (room.BaseStage == BaseStage.None ? BaseStage.Basic : room.BaseStage);
		if (defense) room.DefenseDesignated = true;
		if (!_bases.Contains(room)) _bases.Add(room);
		if (!defense || FrontBase == null) FrontBase = room;
		_work.Remove(room);
		if (defense && !wasDefense) SpawnDefenseFacility(room);
		LogHelper.Log(LogHelper.GAME, $"[점령] debug — F{room.Floor} 방 {room.RoomId}({room.RoomName})을(를) 인류 {(defense ? "방어" : "기초")} 거점으로 만듦");
	}

	// ─────────────────────────── 거점 상실·조회 ───────────────────────────

	// 방 소유가 인류에서 다른 진영으로 넘어갔다(플레이어가 방을 다시 클리어함) — 거점 자격을 잃는다. 인류가 지은 시설(문·방어 시설 표식)은 그대로 남는다(14-6장).
	public void OnBaseLost(Room room)
	{
		if (room == null) return;
		LogHelper.Log(LogHelper.GAME, $"[점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 거점 상실 — 방 소유가 인류가 아니게 됨(거점 단계 {room.BaseStage}), 시설은 남음");
		room.BaseStage = BaseStage.None;
		room.DefenseDesignated = false;
		_bases.Remove(room);
		_work.Remove(room);
		_defenseRequested.Remove(room);
		if (FrontBase == room) FrontBase = LatestBase();
	}

	// 거점 목록을 후방/전방으로 나눠 로그용으로 설명한다 — 출구(계단)까지 방 단위 거리가 가까운 쪽이 후방, 먼 쪽이 전방이다(회의록 10.19).
	public string DescribeBases()
	{
		if (_bases.Count == 0) return "거점 없음";
		var hops = new List<int>();
		foreach (var b in _bases)
		{
			var map = GetHopsFromExit(b.Floor);
			hops.Add(map != null && map.TryGetValue(b.RoomId, out int h) ? h : 0);
		}
		OccupationMath.ClassifyRearFront(hops, out int rear, out int front);
		return $"거점 {_bases.Count}곳(주둔 {CountedBases()}/{OccupationMath.GarrisonBaseCap}) — 후방 방 {_bases[rear].RoomId}({hops[rear]}), 전방 방 {_bases[front].RoomId}({hops[front]})";
	}

	private Room LatestBase() => _bases.Count > 0 ? _bases[_bases.Count - 1] : null;

	private WorkProgress GetWork(Room room)
	{
		if (!_work.TryGetValue(room, out var work)) _work[room] = work = new WorkProgress();
		return work;
	}

	private void LogProgress(Party party, Room room, WorkProgress work, float done, float total, int workers, string kind)
	{
		if (Time.time < work.NextLogTime) return;
		work.NextLogTime = Time.time + LogIntervalSeconds;
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] 방 {room.RoomId} {kind} 시설 건설 {OccupationMath.Progress01(done, total) * 100f:F0}% — 현장 인원 {workers}명");
	}

	private Room FindRoom(int floor, int roomId)
	{
		foreach (var r in Session.allRooms)
			if (r != null && r.Floor == floor && r.RoomId == roomId) return r;
		return null;
	}

	// 출구 방에서 각 방까지의 방 단위 거리(게이트 간선 BFS) — 출구에 가까운 쪽이 후방, 먼 쪽이 전방이다(회의록 10.19). 게이트는 런타임에 안 바뀌어 층마다 한 번만 계산한다.
	private Dictionary<int, int> GetHopsFromExit(int floor)
	{
		if (_hopsByFloor.TryGetValue(floor, out var cached)) return cached;
		var session = Session;
		var cmap = session?.cmap;
		if (cmap == null || cmap.map.floors == null || floor < 1 || floor >= cmap.map.floors.Length) return null;
		if (!cmap.TryGetStairApproachPosition(floor, 0, out Vector2Int exitPos)
			|| !session.roomGrid.TryGetValue(new Vector3Int(exitPos.x, exitPos.y, floor), out Room exitRoom) || exitRoom == null) return null;

		Floor f = cmap.map.floors[floor];
		var edges = new List<(int a, int b)>();
		if (f.gates != null) foreach (var gate in f.gates) edges.Add((gate.roomA, gate.roomB));
		return _hopsByFloor[floor] = OccupationMath.HopsFrom(exitRoom.RoomId, edges);
	}
}
