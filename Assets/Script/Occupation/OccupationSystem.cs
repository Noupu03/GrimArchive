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

	// 위험도를 모르는 이웃 방도 진출 후보로 볼지. 핵심본 6장은 미확인 방을 안전하다고 보지 않으므로 기본은 꺼짐이다(사용자 결정 2026-10-21). 꺼두면 위험도 기록이 생길 때까지 점령 파티가 진출하지 못한다 — debug 메뉴 토글로 켠다.
	public static bool AllowUnknownAdvance = false;

	private const float ValidateIntervalSeconds = 1f;
	// 철거 계획을 만들지 못했을 때(투입할 파티원·게이트 없음) 다시 시도하기까지의 간격 — 매 프레임 재시도·로그를 막는 용도(내부 판단).
	private const float DemolitionRetrySeconds = 3f;

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
	private readonly Dictionary<Room, Vector3Int> _facilities = new Dictionary<Room, Vector3Int>(); // 방마다 실제로 세운 방어 시설 오브젝트의 칸 — 거점을 잃으면 철거한다
	private readonly List<Room> _neighborBuffer = new List<Room>();
	// 기초 시설을 짓기 전에 방 쪽 기존 문을 실제로 부수는 중(철거 단계)인 방. 철거가 끝나면 청사진을 깔고 인류 문·목책을 새로 설치한다.
	private readonly HashSet<Room> _demolishing = new HashSet<Room>();
	// 철거 계획을 만들지 못한 방의 다음 시도 시각, 그리고 그 실패를 이미 경고로 남긴 방(실패가 이어지는 동안 한 번만 남긴다).
	private readonly Dictionary<Room, float> _demolishRetryAt = new Dictionary<Room, float>();
	private readonly HashSet<Room> _demolishWarned = new HashSet<Room>();
	private readonly List<InteractableObject> _doorBuffer = new List<InteractableObject>();
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
		Construction.RefreshVisuals();
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
			_demolishing.Clear();
			_demolishRetryAt.Clear();
			_demolishWarned.Clear();
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

	// 집결·방 이동·후퇴 중이 아니고 그 방의 파티원 누구도 교전 중이 아닐 때 현재 방에 기초 시설을 짓는다. 적 발견·피격 시 중단하고 전투한다(12장) — 진행도는 남고 교전이 끝나면(경계까지 마친 뒤) 이어 짓는다.
	private void TickBasicConstruction(Party party, float deltaTime)
	{
		if (party.Retreat.IsRetreating) return;
		Room room = party.Leader.currentRoom;
		if (room == null || room.Floor < 1 || OccupationMath.IsRoomActivityComplete(room.BaseStage) || !CanConstructNow(party) || IsConstructionDisturbed(party)) return;
		if (!TickDemolition(party, room)) return;

		bool done = Construction.Tick(party, room, defense: false, deltaTime, out bool started, out bool halfway);
		if (started)
		{
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 기초 시설 건설 시작(작업량 {OccupationMath.BasicWorkUnits:F0})");
			SpeechSystem.Say(party.Leader, SpeechId.BaseBuildStart);
			NoticeCenter.Instance?.PushMomentary($"인류가 {room.Floor}층 {room.RoomName}에서 거점 건설을 시작했습니다.", NoticeCenter.WarningColor);
		}
		if (halfway)
		{
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 기초 시설 건설 절반 진행");
			NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 {room.RoomName}의 인류 거점 건설이 절반 진행됐습니다.", NoticeCenter.WarningColor);
		}
		if (done) CompleteBasic(party, room);
	}

	// 기초 시설을 짓기 전에 방 쪽의 기존 문(인류 소유가 아닌 것)을 먼저 실제로 부순다 — 코어 점령 v0.2 12장 "기존 문 제어권이 없으면 문을 파괴하고 인류 문을 세운다"(2026-10-22 사용자 요청: 소유권을 바로 바꾸지 말고 실제 파괴 로직으로 부순 뒤 시간을 들여 다시 짓는다).
	// 부수는 일은 집결 뒤 문 파괴와 같은 경로다 — 리더가 문 하나씩 철거 계획(PartyAdvanceSystem.BeginDemolition)을 세우면 파티원이 지시받은 공격 자리로 가서 문에 달라붙어 채널링하고(PartyBreachCommand + PartyAdvanceSteps.StepBreach), 그 문이 부서지면 계획이 끝난다.
	// 이 함수는 철거 단계의 시작·끝과 "다음 문" 선택만 맡는다(계획이 진행 중인 동안은 CanConstructNow가 막아 불리지 않는다). 철거할 문이 없으면 true(공사 시작).
	// 예전엔 파티원 각자의 DoorAttack이 따로 문을 찾아다니고 90초가 지나면 남은 문을 파괴 연출 없이 허물었다 — 둘 다 없앴다. 문에 닿지 못해도 문서의 돌파와 같이 지시를 다시 내며 재시도한다.
	private bool TickDemolition(Party party, Room room)
	{
		Session.CollectHostileRoomDoors(room, _doorBuffer);
		if (_doorBuffer.Count == 0)
		{
			if (_demolishing.Remove(room))
			{
				_demolishRetryAt.Remove(room);
				_demolishWarned.Remove(room);
				LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 기존 문 철거 완료 — 인류 문·목책 설치 시작");
			}
			return true;
		}

		if (_demolishing.Add(room))
		{
			Construction.DismissGateBlueprint(room, celebrate: false); // 철거 중에는 설치 청사진이 없다
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 기존 문 {_doorBuffer.Count}개를 부수며 거점 건설을 준비합니다");
			NoticeCenter.Instance?.PushMomentary($"인류가 {room.Floor}층 {room.RoomName}의 기존 문을 부수고 있습니다.", NoticeCenter.WarningColor);
		}

		if (Time.time < (_demolishRetryAt.TryGetValue(room, out float retryAt) ? retryAt : 0f)) return false;

		// 공사 대기(ConstructingBase)가 남아 있으면 계획에 참여할 수 없다 — 먼저 풀고, 가장 가까운 문부터 한 개씩 부순다.
		BaseConstruction.ClearWaits(party);
		if (PartyAdvanceSystem.BeginDemolition(party, room, NearestDoor(party.Leader, _doorBuffer)))
		{
			_demolishRetryAt.Remove(room);
			_demolishWarned.Remove(room);
			return false;
		}

		_demolishRetryAt[room] = Time.time + DemolitionRetrySeconds;
		if (_demolishWarned.Add(room))
			LogHelper.Warning(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId} 기존 문 철거 계획을 만들지 못했습니다(투입할 파티원이 없거나 게이트를 찾지 못함) — {DemolitionRetrySeconds:F0}초마다 다시 시도합니다");
		return false;
	}

	private static InteractableObject NearestDoor(Human from, List<InteractableObject> doors)
	{
		InteractableObject best = null;
		int bestDistance = int.MaxValue;
		foreach (var door in doors)
		{
			int distance = AIMovementHelper.ChebyshevDistance(from.position, door.NearestTileTo(from.position));
			if (distance >= bestDistance) continue;
			bestDistance = distance;
			best = door;
		}
		return best;
	}

	private static bool CanConstructNow(Party party)
	{
		if (party.IsRallyActive || party.AdvancePlan != null || party.ReadyToAdvance) return false;
		var leader = party.Leader;
		return !leader.isInDungeonEntranceSequence && !leader.pendingStairTargetFloor.HasValue;
	}

	// 파티원 누구든(어느 방이든) 전투·경계·합류 대기·적 인지 중(PartyEngagement.IsEngaged, 집결·진형의 시간 정지와 같은 판정)이거나 방금 맞았으면 공사 전체를 멈춘다 — 진행도는 남고, 모두 가라앉으면 이어 짓는다.
	// 리더만 보면 다른 파티원이 싸우는 동안에도 나머지 일꾼이 계속 지었다. 피격은 한 행동 틱짜리 표식이라 맞은 시각(SecondsSinceLastHit)을 따로 본다(2026-10-22 사용자 요청 "한 유닛이라도 피격당하거나 전투 상태가 되면 정지").
	private static bool IsConstructionDisturbed(Party party)
	{
		bool anyEngaged = false;
		float sinceHit = float.PositiveInfinity;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f) continue;
			anyEngaged |= PartyEngagement.IsEngaged(m);
			sinceHit = Mathf.Min(sinceHit, m.SecondsSinceLastHit);
		}
		return OccupationMath.ShouldPauseConstruction(anyEngaged, sinceHit);
	}

	private void CompleteBasic(Party party, Room room)
	{
		// 철거 단계에서 기존 문이 이미 부서져 있어(TickDemolition) 빈 자리에 인류 문과 목책을 세울 뿐이다 — 소유권만 바꿔치기하지 않는다.
		var (doors, fences) = Session.InstallHumanGates(room);
		Construction.Complete(room, defense: false); // 건설물이 천막 상징물로 남고, 문·목책 설치 청사진은 옅어진다
		MarkBase(room, BaseStage.Basic);
		_frontBase = room;
		BaseConstruction.ClearWaits(party);

		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 기초 시설 완공 — 인류 문 {doors}개·목책 {fences}개를 설치, 기초 거점 형성. {DescribeBases()}");
		NoticeCenter.Instance?.PushMomentary($"인류가 {room.Floor}층 {room.RoomName}에 거점을 세웠습니다.", NoticeCenter.WarningColor);
		SpeechSystem.Say(party.Leader, SpeechId.BaseBuilt);
		TryDesignateDefenseBase(party);
	}

	// 거점 단계는 방 소유(RoomFaction)와 따로라 방 테두리를 바꾸지 않는다 — 테두리는 플레이어 방 확보만 표현하고, 거점은 현장 상징물·방 정보 패널로 보인다.
	private void MarkBase(Room room, BaseStage stage)
	{
		room.BaseStage = stage;
		if (!_bases.Contains(room)) _bases.Add(room);
		_demolishing.Remove(room);
		_demolishRetryAt.Remove(room);
		_demolishWarned.Remove(room);
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
		if (IsConstructionDisturbed(party)) return; // 건설 중 공격받으면 전투한다(13장) — 기초 공사와 같은 중단 규칙

		bool done = Construction.Tick(party, room, defense: true, deltaTime, out bool started, out _);
		if (started) LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId} 방어 시설 건설 시작(작업량 {OccupationMath.DefenseWorkUnits:F0}) — 휴식을 마친 파티원이 짓는다");
		if (done) CompleteDefense(party, room);
	}

	private void CompleteDefense(Party party, Room room)
	{
		Construction.Complete(room, defense: true); // 천막 상징물은 걷히고 아래 SpawnDefenseFacility가 같은 칸에 망루를 세운다
		MarkBase(room, BaseStage.Defense);
		_defenseRequested.Remove(room);
		BaseConstruction.ClearWaits(party);
		SpawnDefenseFacility(room);

		LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 예정 방어 시설 완공 — 방어 거점 형성, 재출격 가능");
		NoticeCenter.Instance?.PushMomentary($"인류가 {room.Floor}층 {room.RoomName}을(를) 방어 거점으로 만들었습니다.", NoticeCenter.WarningColor);
		SpeechSystem.Say(party.Leader, SpeechId.DefenseBuilt);
	}

	private void SpawnDefenseFacility(Room room)
	{
		// 공사 현장 한가운데에 세운다 — 청사진이 이미 계단·건물·코어·오브젝트·문을 피해 고른 자리이고, 건설물이 완공 연출로 사라진 바로 그 자리다.
		// 자리를 못 구했을 때만 방 중앙 근처에서 찾되, 중앙이 계단·건물 같은 구조물 칸이어도 비켜 간다(오브젝트 칸만 보면 계단 위에 서 버린다).
		if (!Construction.TryGetFacilityTile(room, out Vector3Int pos, out int radius))
		{
			radius = 0; // 현장 자리를 못 구한 폴백 칸은 이웃을 덮지 않게 한 칸 크기로 둔다
			var center = new Vector3Int(Mathf.RoundToInt(room.Bounds.center.x), Mathf.RoundToInt(room.Bounds.center.y), room.Floor);
			bool blocked = Session.objectGrid.ContainsKey(center) || !Session.cmap.IsStaticTileWalkable(room.Floor, new Vector2Int(center.x, center.y));
			pos = blocked ? Session.FindNearbyFreeObjectTile(center) : center;
		}
		var facility = new InteractableObject("DefenseFacility_" + System.Guid.NewGuid().ToString().Substring(0, 4), pos, 0f, 0f, new List<string> { DefenseFacilityTag });
		Session.SpawnObject(facility, new Color(1f, 0.6f, 0.2f));
		// 방어 거점의 상징물은 이 오브젝트 자신이다 — 주황 자리표시자 대신 공사 현장에서 올리던 망루로 보이게 한다. 칸이 이미 차 있어 SpawnObject가 무시됐다면 남의 비주얼을 건드리지 않는다.
		if (Session.objectGrid.TryGetValue(pos, out InteractableObject placed) && placed == facility)
		{
			_facilities[room] = pos;
			BaseSiteVisual.ApplyDefenseEmblem(Session.GetObjectVisual(pos), radius);
		}
	}

	// 거점을 잃으면 방어 시설도 함께 철거된다 — 상징물인 망루만 덩그러니 남지 않게(사용자 요청 2026-10-21). 다시 거점을 세우면 방어 시설도 처음부터 다시 짓는다.
	private void RemoveDefenseFacility(Room room)
	{
		if (!_facilities.TryGetValue(room, out Vector3Int pos)) return;
		_facilities.Remove(room);
		if (Session.objectGrid.TryGetValue(pos, out InteractableObject obj) && obj.Tags != null && obj.Tags.Contains(DefenseFacilityTag))
			Session.CollectObject(pos);
	}

	// TacticalFSMState.ExecuteWait(평소)와 RestPerform(거점 휴식 중 방어 시설 공사)이 부르는 건설 대기 유닛 한 틱.
	public BTStatus StepConstruct(Human h, WaitState wait) => Construction.Step(h, wait);

	// 방 쪽 인류 문과 목책이 하나도 남지 않으면 인류 시설이 파괴돼 이용할 수 없는 거점이다(13-2장 "파괴되어 이용할 수 없으면") — 플레이어가 문·목책을 모두 부수거나 문을 자기 문으로 다시 세운 경우.
	// 사용자 결정(2026-10-22): 기초 시설 = 문 + 목책이라 둘 다 사라져야 상실이고, 상실하면 방어 시설 포함 거점 전체가 무너진다(방어 시설을 따로 철거하는 수단은 아직 없다).
	private void ValidateBases()
	{
		if (_bases.Count == 0 || Time.time < _nextValidateTime) return;
		_nextValidateTime = Time.time + ValidateIntervalSeconds;

		for (int i = _bases.Count - 1; i >= 0; i--)
		{
			Room room = _bases[i];
			if (Session.GetRoomDoorAnchors(room).Count > 0
				&& Session.CountRoomDoors(room, FactionType.Human) == 0
				&& Session.CountRoomFences(room, FactionType.Human) == 0) LoseBase(room);
		}
	}

	private void LoseBase(Room room)
	{
		LogHelper.Log(LogHelper.GAME, $"[점령] F{room.Floor} 방 {room.RoomId}({room.RoomName}) 거점 상실 — 인류 문과 목책이 모두 사라져 이용할 수 없음(거점 단계 {room.BaseStage})");
		room.BaseStage = BaseStage.None;
		room.DefenseDesignated = false;
		_bases.Remove(room);
		_defenseRequested.Remove(room);
		Construction.Reset(room);
		RemoveDefenseFacility(room);
		if (_frontBase == room) _frontBase = _bases.Count > 0 ? _bases[_bases.Count - 1] : null;
	}

	// 방 정보 패널(RoomStatusText.BaseText)에 보여 줄 거점 상태 문구(거점도 공사도 없으면 빈 문자열) — 거점 단계·공사 진행·방어 거점 지정을 구분한다(19장).
	public string DescribeRoom(Room room)
	{
		switch (room.BaseStage)
		{
			case BaseStage.Defense:
				return "인류 방어 거점" + ResortieText(room);
			case BaseStage.Basic:
				float defense = Construction.Progress01(room, true);
				if (defense > 0f) return $"방어 시설 공사 {defense * 100f:F0}%" + ResortieText(room);
				return (room.DefenseDesignated ? "인류 기초 거점(방어 거점 지정)" : "인류 기초 거점") + ResortieText(room);
			default:
				if (_demolishing.Contains(room)) return "인류 거점 건설 준비 — 기존 문 철거 중";
				float basic = Construction.Progress01(room, false);
				return basic > 0f ? $"인류 거점 건설 {basic * 100f:F0}%" : string.Empty;
		}
	}

	// 이 거점에서 쉬는 점령 파티의 재출격 현장 인원(19장 "주둔 사용·최대 인구수와 재출격 현장 인원 구분") — 현장 생존 인원 = 거점에 도착한 도주 미확정 생존 파티원이고, 필요 인원은 최초 출격 인원의 80%(올림)다.
	// 거점 소속 정원 대비 주둔 사용 인구수는 14장 충원 규칙이 정해질 때 함께 보여 준다(정원 수치 보류).
	private string ResortieText(Room room)
	{
		foreach (var party in _parties)
		{
			var st = party.Retreat;
			if (!st.IsRetreating || st.BaseRoom != room || st.Phase != PartyRetreatPhase.Resting) continue;
			int present = 0;
			foreach (var m in party.Members)
				if (m != null && m.hp > 0f && m.Retreat != null && !m.Retreat.IsFleeing && m.Retreat.AtBase) present++;
			int need = PartyRetreatMath.RequiredResortieSurvivors(st.InitialSortieCount);
			return $" — 재출격 현장 인원 {present}/{need}(최초 출격 {st.InitialSortieCount}명의 80%)";
		}
		return string.Empty;
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
		// 즉시 거점 — 철거·설치 연출 없이 인류가 아닌 문은 부수고 인류 문과 목책을 곧바로 세운다(InstallHumanGates).
		Session.InstallHumanGates(room);
		MarkBase(room, defense ? BaseStage.Defense : (room.BaseStage == BaseStage.None ? BaseStage.Basic : room.BaseStage));
		if (defense) room.DefenseDesignated = true;
		if (!defense || _frontBase == null) _frontBase = room;
		// 공사 연출 마무리 — 기초 거점이면 천막 상징물이 서고, 방어 거점이면 천막을 걷고 아래에서 망루를 세운다(이미 방어 거점인 방에 기초를 요청해도 천막은 세우지 않는다).
		Construction.Complete(room, room.BaseStage == BaseStage.Defense);
		if (newlyDefense) SpawnDefenseFacility(room);
		LogHelper.Log(LogHelper.GAME, $"[점령] debug — F{room.Floor} 방 {room.RoomId}({room.RoomName})을(를) 인류 {(defense ? "방어" : "기초")} 거점으로 만듦");
	}
}
