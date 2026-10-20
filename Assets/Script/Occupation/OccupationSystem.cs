using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;

// 인류 점령 파티 구동 — 코어 점령 설계명세서 v0.2 12~14장. 점령 파티는 웨이브와 따로 던전에 상주하며(HumanWaveManager가 도착 직후 Adopt로 넘긴다) 방 하나씩
//   ① 현재 방에 기초 시설(방 쪽 인류 문)을 짓고 → ② 기초 거점이 되면 집결해 → ③ 안전한 이웃 방으로 진출하며(PartyMissionDriver) → 후퇴하면 방어/기초 거점에서 쉬었다 80% 이상 살아 있을 때 재출격한다(PartyRetreatSystem).
// 거점은 플레이어의 방 확보(Room.RoomFaction)와 별개 상태(Room.BaseStage)다 — 인류 시설 완공이 플레이어의 확보·건설 권한을 제거하지 않는다(1-1·12-2장). 순수 판정은 OccupationMath.
public class OccupationSystem
{
	// 방어 시설 자리표시자 — 방어건물 시스템이 없어 기능 없는 표식이다(함정은 진영 소유가 없어 인류도 해치므로 쓰지 않는다).
	public const string DefenseFacilityTag = "Object/Passable/DefenseFacility";

	// 위험도를 모르는 이웃 방도 진출 후보로 볼지. 핵심본 6장은 미확인 방을 안전하다고 보지 않지만, 정보 전달 체계가 없는 지금은 끄면 위험도 기록이 생길 때까지 점령 파티가 진출하지 못한다(사용자 결정: 기본 ON).
	public static bool AllowUnknownAdvance = true;

	private const float ValidateIntervalSeconds = 1f;

	private IObjectResolver _resolver;
	private GameSession Session => _cachedSession ??= _resolver.Resolve<GameSession>();
	private GameSession _cachedSession;
	private BaseConstruction _construction;
	private RoomGraph _graph;
	private BaseConstruction Construction => _construction ??= new BaseConstruction(Session);
	private RoomGraph Graph => _graph ??= new RoomGraph(Session);

	private readonly List<Party> _parties = new List<Party>();
	private readonly Dictionary<Party, PartyMissionDriver> _drivers = new Dictionary<Party, PartyMissionDriver>();
	private readonly Dictionary<Party, Room> _targets = new Dictionary<Party, Room>();
	private readonly Dictionary<Party, float> _nextPickTime = new Dictionary<Party, float>();
	private readonly List<Room> _bases = new List<Room>();
	private readonly HashSet<Room> _defenseRequested = new HashSet<Room>();
	private readonly List<Room> _neighborBuffer = new List<Room>();
	private float _nextValidateTime;

	// 점령 병력이 거점을 옮기며 전진하는 "전방" 거점과 집단의 거점 정원(처음 출격 인원, 자리표시자 — 문서가 정원 수치를 정하지 않음).
	private Room _frontBase;
	private int _groupCapacity;

	[Inject]
	public void Construct(IObjectResolver resolver) => _resolver = resolver;

	public void Adopt(Party party)
	{
		if (party == null || _parties.Contains(party)) return;
		party.Type = PartyType.Occupy;
		_parties.Add(party);
		_drivers[party] = new PartyMissionDriver();
		_groupCapacity = Mathf.Max(_groupCapacity, party.Retreat.InitialSortieCount);
		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] 점령 파티 입양 — {party.Members.Count}명, 거점 정원 {_groupCapacity}(자리표시자)");
	}

	public void Tick(float deltaTime)
	{
		ValidateBases();
		if (_parties.Count == 0) return;

		for (int i = _parties.Count - 1; i >= 0; i--)
		{
			var party = _parties[i];
			if (party == null || !HasLivingMemberInDungeon(party))
			{
				Remove(party);
				continue;
			}
			party.AssignLeaderIfNeeded();
			if (party.Leader == null || party.Leader.hp <= 0f) continue;

			TickAdvance(party);
			TickBasicConstruction(party, deltaTime);
			TickDefenseConstruction(party, deltaTime);
		}
	}

	private void Remove(Party party)
	{
		if (party != null)
		{
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] 던전 안에 산 파티원이 없어 점령 파티를 정리합니다");
			Construction.Forget(party);
		}
		_parties.Remove(party);
		_drivers.Remove(party);
		_targets.Remove(party);
		_nextPickTime.Remove(party);
		if (_parties.Count == 0)
		{
			// 소속 병력이 모두 사라졌다 — 시설만 남은 이전 거점은 주둔 거점 수에서 제외한다(14-3장).
			_frontBase = null;
			_groupCapacity = 0;
		}
	}

	private static bool HasLivingMemberInDungeon(Party party)
	{
		foreach (var m in party.Members)
			if (m != null && m.hp > 0f && m.currentFloor >= 1) return true;
		return false;
	}

	// 집결이 끝나 방 이동 차례가 되면 진출할 방을 고르고, PartyMissionDriver가 문 앞 진형 → 돌파 → 입장 계획을 진행한다.
	private void TickAdvance(Party party)
	{
		if (party.Retreat.IsRetreating) return;
		// 방 밖 타일(통로)에 서 있는 동안에도 진행 중인 계획은 멈추지 않는다 — 진출 방 선정만 현재 방이 있을 때 한다.
		Room room = party.Leader.currentRoom;

		_targets.TryGetValue(party, out Room target);
		if (room != null && party.ReadyToAdvance && NeedsNewTarget(target, room) && Time.time >= (_nextPickTime.TryGetValue(party, out float next) ? next : 0f))
		{
			_nextPickTime[party] = Time.time + 1f;
			target = PickAdvanceRoom(party, room);
			_targets[party] = target;
			if (target != null) LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] 진출할 방 선정 — F{target.Floor} 방 {target.RoomId}({target.RoomName}) (현재 방 {room.RoomId})");
			else LogHelper.Warning(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] 진출할 방이 없어 거점에서 대기합니다(미확인 방 허용={AllowUnknownAdvance})");
		}
		if (_drivers.TryGetValue(party, out PartyMissionDriver driver)) driver.Tick(party, target);
	}

	private static bool NeedsNewTarget(Room target, Room current)
		=> target == null || target == current || target.Floor != current.Floor || target.BaseStage != BaseStage.None;

	// 이웃 방 중 진출할 곳 — 이미 인류 거점인 방은 제외하고, 위험도를 아는 방은 안전 상한 이하일 때만, 모르는 방은 AllowUnknownAdvance일 때만 후보다.
	private Room PickAdvanceRoom(Party party, Room from)
	{
		Graph.CollectNeighbors(from, _neighborBuffer);
		var candidates = new List<AdvanceCandidate>(_neighborBuffer.Count);
		foreach (var neighbor in _neighborBuffer)
		{
			bool known = party.Leader.personalMap.TryGetKnownRoomDanger(neighbor.RoomId, out float danger);
			candidates.Add(new AdvanceCandidate
			{
				RoomId = neighbor.RoomId,
				IsHumanBase = neighbor.BaseStage != BaseStage.None,
				DangerKnown = known,
				Danger = danger,
				HopsFromExit = Graph.HopsOf(neighbor),
			});
		}
		int index = OccupationMath.PickAdvance(candidates, RetreatSystem.ResortieSafeRoomMaxDanger, AllowUnknownAdvance);
		return index >= 0 ? _neighborBuffer[index] : null;
	}

	// 집결·방 이동·후퇴 중이 아니고 리더가 적을 인지하지 않을 때 현재 방에 기초 시설을 짓는다. 적을 인지하면 건설하지 않고 전투한다(12장).
	private void TickBasicConstruction(Party party, float deltaTime)
	{
		if (party.Retreat.IsRetreating) return;
		Room room = party.Leader.currentRoom;
		if (room == null || room.Floor < 1 || OccupationMath.IsRoomActivityComplete(room.BaseStage) || !CanConstructNow(party)) return;

		bool done = Construction.Tick(party, room, defense: false, deltaTime, out bool started);
		if (started)
		{
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 기초 시설 건설 시작(작업량 {OccupationMath.BasicWorkUnits:F0})");
			SpeechSystem.Say(party.Leader, SpeechId.BaseBuildStart);
		}
		if (done) CompleteBasic(party, room);
	}

	private static bool CanConstructNow(Party party)
	{
		if (party.IsRallyActive || party.AdvancePlan != null || party.ReadyToAdvance) return false;
		var leader = party.Leader;
		return !leader.isInDungeonEntranceSequence && !leader.pendingStairTargetFloor.HasValue && leader.personalSpottedEnemies.Count == 0;
	}

	private void CompleteBasic(Party party, Room room)
	{
		int doors = Session.SetRoomDoorsOwner(room, FactionType.Human);
		MarkBase(room, BaseStage.Basic);
		_frontBase = room;
		Construction.Reset(room);
		BaseConstruction.ClearWaits(party);

		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 기초 시설 완공 — 문 {doors}개를 인류 소유로 설치/전환, 기초 거점 형성. {DescribeBases()}");
		NoticeCenter.Instance?.PushMomentary($"인류가 {room.Floor}층 {room.RoomName}에 거점을 세웠습니다.", NoticeCenter.WarningColor);
		SpeechSystem.Say(party.Leader, SpeechId.BaseBuilt);
		TryDesignateDefenseBase(party);
	}

	private void MarkBase(Room room, BaseStage stage)
	{
		room.BaseStage = stage;
		if (!_bases.Contains(room)) _bases.Add(room);
		Session.OffenseProcessor?.RefreshRoomOutline(room, pulse: true);
	}

	// 점령 병력이 정원을 채웠고 거점 상한에 여유가 있으면 가장 가까운 유효한 기초 거점을 다음 방어 거점으로 정한다(14장). 병력이 정원 미만이면 충원이 올 때까지 미룬다.
	private void TryDesignateDefenseBase(Party party)
	{
		if (_bases.Exists(b => b.DefenseDesignated)) return;
		Room candidate = NearestBase(_frontBase);
		if (candidate == null) return;

		int garrison = 0;
		foreach (var m in party.Members) if (m != null && m.hp > 0f && m.currentFloor >= 1) garrison++;
		if (!OccupationMath.ShouldDesignateDefenseBase(garrison, _groupCapacity, CountedBases(), anyDesignated: false, candidateAlreadyCounted: candidate == _frontBase)) return;

		candidate.DefenseDesignated = true;
		LogHelper.Log(LogHelper.GAME, $"[점령] 방어 거점 지정 — F{candidate.Floor} 방 {candidate.RoomId}({candidate.RoomName}): 정원 {garrison}/{_groupCapacity}, 주둔 거점 {CountedBases()}/{OccupationMath.GarrisonBaseCap}. 후퇴·휴식을 마친 뒤 예정 방어 시설을 지어야 재출격한다");
	}

	// 주둔 거점으로 세는 방 수 — 전방 거점 + 방어 거점으로 정한 방(충원 목적지). 같은 방이면 하나다(14-3장).
	private int CountedBases()
	{
		var counted = new HashSet<Room>();
		if (_frontBase != null && _frontBase.BaseStage != BaseStage.None) counted.Add(_frontBase);
		foreach (var b in _bases) if (b.DefenseDesignated) counted.Add(b);
		return counted.Count;
	}

	private Room NearestBase(Room from)
	{
		Room best = null;
		int bestDistance = int.MaxValue;
		foreach (var b in _bases)
		{
			if (from != null && b.Floor != from.Floor) continue;
			int distance = from == null ? 0 : Mathf.Abs(Graph.HopsOf(b) - Graph.HopsOf(from));
			if (best == null || distance < bestDistance) { best = b; bestDistance = distance; }
		}
		return best;
	}

	// 휴식을 마친 점령 파티가 방어 거점으로 정한 기초 거점에서 예정 방어 시설을 지은 뒤에야 재출격한다(13장) — PartyRetreatSystem.TickRest가 재출격 전에 부른다. 공사가 필요 없거나 끝났으면 true.
	public bool EnsureDefenseBuilt(Room room)
	{
		if (room == null || !OccupationMath.NeedsDefenseBuild(room.BaseStage, room.DefenseDesignated)) return true;
		_defenseRequested.Add(room);
		return false;
	}

	private void TickDefenseConstruction(Party party, float deltaTime)
	{
		var st = party.Retreat;
		Room room = st.BaseRoom;
		if (!st.IsRetreating || st.Phase != PartyRetreatPhase.Resting || room == null || !_defenseRequested.Contains(room)) return;
		if (!OccupationMath.NeedsDefenseBuild(room.BaseStage, room.DefenseDesignated)) { _defenseRequested.Remove(room); return; }

		bool done = Construction.Tick(party, room, defense: true, deltaTime, out bool started);
		if (started) LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId} 방어 시설 건설 시작(작업량 {OccupationMath.DefenseWorkUnits:F0}) — 휴식을 마친 파티원이 짓는다");
		if (done) CompleteDefense(party, room);
	}

	private void CompleteDefense(Party party, Room room)
	{
		MarkBase(room, BaseStage.Defense);
		_defenseRequested.Remove(room);
		Construction.Reset(room);
		BaseConstruction.ClearWaits(party);
		SpawnDefenseFacility(room);

		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 예정 방어 시설 완공 — 방어 거점 형성, 재출격 가능");
		NoticeCenter.Instance?.PushMomentary($"인류가 {room.Floor}층 {room.RoomName}을(를) 방어 거점으로 만들었습니다.", NoticeCenter.WarningColor);
		SpeechSystem.Say(party.Leader, SpeechId.DefenseBuilt);
	}

	private void SpawnDefenseFacility(Room room)
	{
		var center = new Vector3Int(Mathf.RoundToInt(room.Bounds.center.x), Mathf.RoundToInt(room.Bounds.center.y), room.Floor);
		Vector3Int pos = Session.objectGrid.ContainsKey(center) ? Session.FindNearbyFreeObjectTile(center) : center;
		var facility = new InteractableObject("DefenseFacility_" + System.Guid.NewGuid().ToString().Substring(0, 4), pos, 0f, 0f, new List<string> { DefenseFacilityTag });
		Session.SpawnObject(facility, new Color(1f, 0.6f, 0.2f));
	}

	// TacticalFSMState.ExecuteWait(평소)와 RestPerform(거점 휴식 중 방어 시설 공사)이 부르는 건설 대기 유닛 한 틱.
	public BTStatus StepConstruct(Human h, WaitState wait) => Construction.Step(h, wait);

	// 방 쪽 인류 문이 하나도 남지 않으면 인류 시설이 파괴돼 이용할 수 없는 거점이다(13-2장 "파괴되어 이용할 수 없으면") — 플레이어가 문을 파괴하거나 자기 문으로 다시 세운 경우.
	private void ValidateBases()
	{
		if (_bases.Count == 0 || Time.time < _nextValidateTime) return;
		_nextValidateTime = Time.time + ValidateIntervalSeconds;

		for (int i = _bases.Count - 1; i >= 0; i--)
		{
			Room room = _bases[i];
			if (Session.GetRoomDoorAnchors(room).Count > 0 && Session.CountRoomDoors(room, FactionType.Human) == 0) LoseBase(room);
		}
	}

	private void LoseBase(Room room)
	{
		LogHelper.Log(LogHelper.GAME, $"[점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 거점 상실 — 인류 문이 모두 사라져 이용할 수 없음(거점 단계 {room.BaseStage})");
		room.BaseStage = BaseStage.None;
		room.DefenseDesignated = false;
		_bases.Remove(room);
		_defenseRequested.Remove(room);
		Construction.Reset(room);
		if (_frontBase == room) _frontBase = _bases.Count > 0 ? _bases[_bases.Count - 1] : null;
		Session.OffenseProcessor?.RefreshRoomOutline(room, pulse: false);
	}

	// 방 라벨에 덧붙일 거점 상태 문구(없으면 빈 문자열)와 변화 감지용 키 — 거점 단계·공사 진행(5% 단위)·방어 거점 지정을 구분한다(19장).
	public string DescribeRoom(Room room)
	{
		switch (room.BaseStage)
		{
			case BaseStage.Defense:
				return "인류 방어 거점";
			case BaseStage.Basic:
				float defense = Construction.Progress01(room, true);
				if (defense > 0f) return $"방어 시설 공사 {defense * 100f:F0}%";
				return room.DefenseDesignated ? "인류 기초 거점(방어 거점 지정)" : "인류 기초 거점";
			default:
				float basic = Construction.Progress01(room, false);
				return basic > 0f ? $"인류 거점 건설 {basic * 100f:F0}%" : string.Empty;
		}
	}

	public int RoomStatusKey(Room room)
	{
		int progress = Mathf.RoundToInt(Mathf.Max(Construction.Progress01(room, false), Construction.Progress01(room, true)) * 20f);
		return (int)room.BaseStage | (room.DefenseDesignated ? 4 : 0) | (progress << 3);
	}

	private string DescribeBases()
	{
		if (_bases.Count == 0) return "거점 없음";
		var hops = new List<int>(_bases.Count);
		foreach (var b in _bases) hops.Add(Graph.HopsOf(b));
		OccupationMath.ClassifyRearFront(hops, out int rear, out int front);
		return $"거점 {_bases.Count}곳(주둔 {CountedBases()}/{OccupationMath.GarrisonBaseCap}) — 후방 방 {_bases[rear].RoomId}({hops[rear]}), 전방 방 {_bases[front].RoomId}({hops[front]})";
	}

	// debug 전용 — 점령 파티 없이 방을 곧바로 인류 기초(또는 방어) 거점으로 만든다. 후퇴 목적지·재출격 확인용이다.
	public void DebugMakeBase(Room room, bool defense)
	{
		if (room == null || room.Floor < 1) return;
		bool newlyDefense = defense && room.BaseStage != BaseStage.Defense;
		Session.SetRoomDoorsOwner(room, FactionType.Human);
		MarkBase(room, defense ? BaseStage.Defense : (room.BaseStage == BaseStage.None ? BaseStage.Basic : room.BaseStage));
		if (defense) room.DefenseDesignated = true;
		if (!defense || _frontBase == null) _frontBase = room;
		Construction.Reset(room);
		if (newlyDefense) SpawnDefenseFacility(room);
		LogHelper.Log(LogHelper.GAME, $"[점령] debug — F{room.Floor} 방 {room.RoomId}({room.RoomName})을(를) 인류 {(defense ? "방어" : "기초")} 거점으로 만듦");
	}
}
