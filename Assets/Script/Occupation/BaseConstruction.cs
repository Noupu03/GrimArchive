using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 점령 파티의 거점 시설 공사 — 방마다 기초/방어 공사 진행도를 들고, 현장 자리 배정·일하는 인원 집계·유닛 한 틱(WaitReason.ConstructingBase)을 맡는다(코어 점령 v0.2 12장).
// 진행도는 현장 자리에 선 인원 수에 비례한다. 파티원 누구든 전투·경계 중이거나 방금 맞았으면 공사 자체가 멈추고(OccupationSystem.IsConstructionDisturbed) 진행도는 남는다.
// 일꾼 자리는 리더의 건설 명령(AssignBuilders)으로 배정된다 — 근접 역할은 문·목책 둘레 1칸(게이트별로 고르게), 그 밖은 건설물 둘레 1칸에 서서 가만히 일한다(2026-10-22 사용자 요청).
// 공사 연출은 ① 방 안 빈자리에 바닥 청사진 + 건설물이 서서 단계별로 올라가며 ② 일꾼 둘레에서 먼지·망치 불꽃이 튀고 ③ 완공하면 번쩍이며 사라진다. 현장 자리는 계단·건물·코어·오브젝트·문과 겹치지 않는 곳만 고른다.
// 방 테두리는 플레이어 방 확보(RoomFaction)만 쓰는 채널이라 거점 공사는 건드리지 않는다(2026-10-22).
internal sealed class BaseConstruction
{
	private const float WorkerFreshSeconds = 2.5f;
	private const float AssignIntervalSeconds = 1f;
	private const float LogIntervalSeconds = 10f;
	private const float LayoutCheckSeconds = 0.5f;   // 현장 자리가 아직 비어 있는지 다시 보는 주기
	private const float RelocateAfterSeconds = 3f;   // 자리가 이만큼 계속 막혀 있어야 옮긴다 — 잠깐 놓인 시체 같은 일시적 겹침에 현장이 뛰어다니지 않게
	private const float LayoutRetrySeconds = 2f;     // 자리를 못 찾았을 때 다시 찾는 주기
	private const float EmblemCheckSeconds = 1f;     // 완공 상징물의 칸이 아직 비어 있는지 보는 주기

	private sealed class Progress
	{
		public float Done;
		public float NextLogTime = -1f;
		public float NextTalkTime;   // 일꾼 작업 말풍선을 다시 띄울 수 있는 시각
		public float NextFxTime;     // 일꾼 먼지·망치 불꽃을 다시 낼 수 있는 시각
		public bool HalfAnnounced;   // 절반 진행 알림을 이미 냈는지
	}

	// 방 하나의 현장 자리 — 기초 공사와 방어 시설 공사, 완공한 방어 시설 자리가 같은 곳을 쓴다(자리가 막히면 그때 옮긴다).
	private sealed class LayoutState
	{
		public bool Has;
		public SiteLayout Layout;
		public float NextCheck;
		public float BlockedSince = -1f;
		public float NextPick;
	}

	private sealed class Site
	{
		public BaseSiteVisual Visual;
		public SiteLayout Layout;
	}

	// 완공한 기초 거점의 상징물(방 안 천막) — 방 하나에 하나.
	private sealed class Emblem
	{
		public BaseSiteVisual Visual;
		public Vector2Int Tile;
		public int Radius;   // 상징물이 덮는 영역 반경 — 3×3 자리면 1(크게 그림), 1×1 자리면 0
		public float NextCheck;
		public float BlockedSince = -1f;
	}

	// 근접 일꾼이 곁에 서는 게이트 하나 — 문 칸과 목책이 서는 자리(목책이 아직 없어도 자리 기준). 게이트 지오메트리는 안 바뀌어 방마다 한 번만 구한다.
	private sealed class GuardGate
	{
		public Vector3Int Anchor;
		public List<Vector2Int> Tiles;
	}

	private readonly GameSession _session;
	private readonly Dictionary<(Room room, bool defense), Progress> _progress = new Dictionary<(Room, bool), Progress>();
	private readonly Dictionary<(Room room, bool defense), Site> _sites = new Dictionary<(Room, bool), Site>();
	private readonly Dictionary<Room, LayoutState> _layouts = new Dictionary<Room, LayoutState>();
	private readonly Dictionary<Room, Emblem> _emblems = new Dictionary<Room, Emblem>();
	private readonly Dictionary<Room, List<GuardGate>> _guardGates = new Dictionary<Room, List<GuardGate>>();
	// 기초 시설(문·목책)을 새로 설치하는 동안만 보이는 청사진 — 방 하나에 하나, 설치가 끝나거나 거점을 잃으면 걷는다.
	private readonly Dictionary<Room, BaseGateBlueprint> _gateBlueprints = new Dictionary<Room, BaseGateBlueprint>();
	private readonly List<RoomGate> _gateBuffer = new List<RoomGate>();
	private readonly Dictionary<Party, float> _nextAssignTime = new Dictionary<Party, float>();
	private readonly List<Human> _workerBuffer = new List<Human>();

	public BaseConstruction(GameSession session) => _session = session;

	public static bool IsOpen(Room room, bool defense)
		=> defense ? OccupationMath.NeedsDefenseBuild(room.BaseStage, room.DefenseDesignated) : room.BaseStage == BaseStage.None;

	public float Progress01(Room room, bool defense)
		=> _progress.TryGetValue((room, defense), out var p) ? OccupationMath.Progress01(p.Done, Total(defense)) : 0f;

	// 방의 공사 진행도·현장 연출·완공 상징물을 조용히 전부 걷는다 — 거점 상실·중단.
	// 현장 자리(_layouts)는 남긴다: 다음 공사도 같은 자리를 다시 검사해 쓴다.
	public void Reset(Room room)
	{
		ClearProgress(room);
		RemoveEmblem(room);
		DismissGateBlueprint(room, celebrate: false);
	}

	// 공사 한 단계를 완공했다 — 진행도를 걷고 현장 연출을 마무리한다.
	// 기초 시설: 건설물이 그 자리에 천막 상징물로 남는다(순수 비주얼, 방 안 어디에도 영향 없음). 방어 시설: 현장이 번쩍이며 사라지고, 같은 칸에 서는 방어 시설 오브젝트가 망루 상징물이 된다
	//   (OccupationSystem.SpawnDefenseFacility → BaseSiteVisual.ApplyDefenseEmblem) — 그래서 천막 상징물은 걷는다.
	public void Complete(Room room, bool defense)
	{
		bool visible = room.FogRevealed;
		_sites.TryGetValue((room, defense), out Site finished);
		_sites.Remove((room, defense));
		ClearProgress(room);
		if (!defense) DismissGateBlueprint(room, celebrate: visible); // 실제 문·목책이 선 자리의 청사진 — 채움이 가득 찬 채 옅어진다

		if (defense)
		{
			if (finished?.Visual != null) finished.Visual.Dismiss(visible);
			RemoveEmblem(room);
			return;
		}
		PlaceEmblem(room, finished, visible);
	}

	private void ClearProgress(Room room)
	{
		_progress.Remove((room, false));
		_progress.Remove((room, true));
		RemoveSite(room, false);
		RemoveSite(room, true);
	}

	public void Forget(Party party) => _nextAssignTime.Remove(party);

	// 방어 시설을 세울 칸 — 현장 한가운데(청사진이 이미 계단·건물·코어·오브젝트를 피해 고른 자리)다. 자리를 못 구하면 false. radius = 그 현장 영역의 반경(망루를 얼마나 크게 그릴지 정한다).
	public bool TryGetFacilityTile(Room room, out Vector3Int pos, out int radius)
	{
		SiteLayout? layout = ResolveLayout(room, force: true, out _);
		pos = layout.HasValue ? new Vector3Int(layout.Value.Center.x, layout.Value.Center.y, room.Floor) : default;
		radius = layout.HasValue ? layout.Value.Radius : 0;
		return layout.HasValue;
	}

	// 한 방의 공사를 한 프레임 진행한다. started는 이번 틱에 처음 시작했을 때, halfway는 기초 공사가 처음 절반을 넘었을 때(한 번), 반환값은 공사가 끝났을 때 true다.
	public bool Tick(Party party, Room room, bool defense, float deltaTime, out bool started, out bool halfway)
	{
		if (!_progress.TryGetValue((room, defense), out var progress)) _progress[(room, defense)] = progress = new Progress { NextTalkTime = Time.time + 3f };
		started = progress.NextLogTime < 0f;

		SiteLayout? layout = ResolveLayout(room, force: false, out bool relocated);
		if (relocated) ReleaseWorkers(party, room); // 일꾼이 옛 자리에 서 있지 않게 — 다음 배정에서 새 자리를 받는다
		AssignBuilders(party, room, defense, layout);
		int workers = CountWorkers(party, room, _workerBuffer, out Human talker);
		progress.Done += OccupationMath.WorkDelta(workers, deltaTime);

		float progress01 = OccupationMath.Progress01(progress.Done, Total(defense));
		UpdateSite(room, defense, progress, progress01, layout);
		if (!defense) UpdateGateBlueprint(room, progress01);
		halfway = !defense && !progress.HalfAnnounced && progress01 >= 0.5f;
		if (halfway) progress.HalfAnnounced = true;

		// 일하는 인원 중 한 명이 주기적으로 작업 소리를 낸다 — 교전으로 공사가 멈춘 틱엔 이 함수가 불리지 않아 말하지 않는다.
		if (talker != null && Time.time >= progress.NextTalkTime)
		{
			progress.NextTalkTime = Time.time + Random.Range(5f, 9f);
			SpeechSystem.Say(talker, SpeechId.BaseBuildWork);
		}

		if (Time.time >= progress.NextLogTime)
		{
			progress.NextLogTime = Time.time + LogIntervalSeconds;
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] 방 {room.RoomId} {(defense ? "방어" : "기초")} 시설 건설 {Progress01(room, defense) * 100f:F0}% — 현장 인원 {workers}명");
		}
		return OccupationMath.IsWorkComplete(progress.Done, Total(defense));
	}

	public static void ClearWaits(Party party)
	{
		foreach (var m in party.Members)
			if (m != null && m.currentWait != null && m.currentWait.Reason == WaitReason.ConstructingBase)
			{
				m.currentWait = null;
				m.waitStuckTurns = 0;
			}
	}

	// 건설 대기의 유닛 한 틱 — 배정받은 자리로 가서 정확히 그 칸에 서 있으면 일하는 것으로 센다(자리에서 벗어나 있으면 일하지 않고 돌아간다). 일이 필요 없어졌으면 대기를 풀고 Success.
	public BTStatus Step(Human h, WaitState wait)
	{
		Room room = wait.WorkRoom;
		if (room == null || h.party == null || !IsOpen(room, wait.WorkIsDefense))
		{
			h.currentWait = null;
			h.waitStuckTurns = 0;
			return BTStatus.Success;
		}
		if (!wait.WaitPosition.HasValue) wait.WaitPosition = h.position;

		Vector2Int slot = wait.WaitPosition.Value;
		int distance = AIMovementHelper.ChebyshevDistance(h.position, slot);
		if (distance <= 0)
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
			wait.WaitPosition = h.position; // 자리가 다른 유닛에게 막혀 못 닿으면 서 있는 곳에서 일한다 — 자리 근처에서 멈췄을 테니 충분하다
			h.waitStuckTurns = 0;
		}
		return BTStatus.Running;
	}

	private static float Total(bool defense) => defense ? OccupationMath.DefenseWorkUnits : OccupationMath.BasicWorkUnits;

	// 리더의 건설 명령 — 같은 방 파티원에게 일꾼 자리를 나눠 준다(같은 방이면 거리와 무관하게 리더 명령이 닿는다, Party.IsReachedByLeaderCommand).
	// 근접 역할(전방 근접·근접 지원)은 문·목책 둘레 1칸에 게이트별로 고르게 나눠 서고, 그 밖(원거리 공격·원거리 지원)은 현장(천막) 바로 바깥 둘레에 선다. 문·목책 곁 자리가 다 차면 근접도 건설물 둘레를 쓴다.
	// 현장 자리를 못 구한 방은 방 쪽 문 묶음 곁을 번갈아 쓴다. 다른 대기(집결·보고 등)가 있는 유닛과 후퇴 이동 중인 유닛은 건드리지 않는다(거점에서 쉬는 인원은 방어 시설 공사에 참여한다).
	private void AssignBuilders(Party party, Room room, bool defense, SiteLayout? layout)
	{
		if (Time.time < (_nextAssignTime.TryGetValue(party, out float next) ? next : 0f)) return;
		_nextAssignTime[party] = Time.time + AssignIntervalSeconds;

		var anchors = _session.GetRoomDoorAnchors(room);
		var gates = GetGuardGates(room);
		var gateLoads = new int[gates.Count];
		var claimed = new HashSet<Vector2Int>();
		foreach (var m in party.Members)
		{
			var held = m?.currentWait;
			if (held == null || held.Reason != WaitReason.ConstructingBase || !held.WaitPosition.HasValue) continue;
			claimed.Add(held.WaitPosition.Value);
			int heldGate = IndexOfGate(gates, held.WorkGateAnchor);
			if (heldGate >= 0) gateLoads[heldGate]++;
		}

		int index = 0;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f || m.currentFloor != room.Floor || m.currentRoom != room) continue;
			if (RetreatSystem.WantsRetreatState(m)) continue;
			var wait = m.currentWait;
			if (wait != null && wait.Reason != WaitReason.SearchingNextDoor && wait.Reason != WaitReason.ConstructingBase) continue;
			if (wait != null && wait.Reason == WaitReason.ConstructingBase) { index++; continue; }

			Vector2Int slot = m.position;
			Vector3Int? gateAnchor = null;
			if (BaseSiteMath.IsGateGuardRole(CombatScoreMath.ResolveCombatRole(m.unitType)) && TryFindGuardSlot(m, room, gates, gateLoads, claimed, layout, out Vector2Int guardSlot, out int gateIndex))
			{
				slot = guardSlot;
				gateAnchor = gates[gateIndex].Anchor;
				gateLoads[gateIndex]++;
			}
			else if (layout.HasValue && AIMovementHelper.TryFindWorkSlot(m, layout.Value.Center, layout.Value.Radius + 1, room, claimed, out Vector2Int ringSlot))
			{
				slot = ringSlot;
			}
			else if (anchors.Count > 0)
			{
				Vector3Int anchor = anchors[index % anchors.Count];
				slot = AIMovementHelper.FindDoorWaitSlot(m, new Vector2Int(anchor.x, anchor.y), claimed);
			}
			claimed.Add(slot);
			index++;
			m.currentWait = new WaitState { Reason = WaitReason.ConstructingBase, WorkRoom = room, WorkIsDefense = defense, WaitPosition = slot, WaitFloor = room.Floor, WorkGateAnchor = gateAnchor };
			m.waitStuckTurns = 0;
		}
	}

	private List<GuardGate> GetGuardGates(Room room)
	{
		if (_guardGates.TryGetValue(room, out List<GuardGate> cached)) return cached;

		_session.CollectRoomGates(room, _gateBuffer);
		var gates = new List<GuardGate>(_gateBuffer.Count);
		foreach (var gate in _gateBuffer)
		{
			var tiles = new List<Vector2Int>(gate.DoorTiles.Count + gate.FenceSites.Count);
			foreach (var tile in gate.DoorTiles) tiles.Add(new Vector2Int(tile.x, tile.y));
			foreach (var tile in gate.FenceSites) tiles.Add(new Vector2Int(tile.x, tile.y));
			gates.Add(new GuardGate { Anchor = gate.Anchor, Tiles = tiles });
		}
		_guardGates[room] = gates;
		return gates;
	}

	private static int IndexOfGate(List<GuardGate> gates, Vector3Int? anchor)
	{
		if (!anchor.HasValue) return -1;
		for (int i = 0; i < gates.Count; i++)
			if (gates[i].Anchor == anchor.Value) return i;
		return -1;
	}

	// 문·목책 둘레 1칸 자리를 고른다 — 서 있는 인원이 적은 게이트부터(같으면 유닛에게 가까운 쪽), 한 게이트 안에서는 문 앞 통과 구간을 비운 자리가 먼저고 없으면 완화한다. 자리가 없으면 false.
	private bool TryFindGuardSlot(Human unit, Room room, List<GuardGate> gates, int[] loads, HashSet<Vector2Int> claimed, SiteLayout? layout, out Vector2Int slot, out int gateIndex)
	{
		slot = default;
		gateIndex = -1;
		if (gates.Count == 0) return false;

		var distances = new int[gates.Count];
		for (int i = 0; i < gates.Count; i++)
		{
			int nearest = int.MaxValue;
			foreach (var tile in gates[i].Tiles) nearest = Mathf.Min(nearest, AIMovementHelper.ChebyshevDistance(unit.position, tile));
			distances[i] = nearest;
		}

		foreach (int i in BaseSiteMath.OrderGatesByLoad(loads, distances))
		{
			for (int pass = 0; pass < 2; pass++)
			{
				bool clearPassage = pass == 0;
				if (!BaseSiteMath.TryPickGuardSlot(gates[i].Tiles, unit.position, p => IsGuardSlotFree(unit, room, p, claimed, layout, clearPassage), out slot)) continue;
				gateIndex = i;
				return true;
			}
		}
		return false;
	}

	// 문·목책 곁에 설 수 있는 칸 — 이 방 안, 아무도 안 맡았고, 건설물 영역 밖이며, 서 있을 수 있다. clearPassage면 문 앞 통과 구간 밖일 때만.
	private bool IsGuardSlotFree(Human unit, Room room, Vector2Int p, HashSet<Vector2Int> claimed, SiteLayout? layout, bool clearPassage)
	{
		if (claimed.Contains(p)) return false;
		if (layout.HasValue && BaseSiteMath.IsInFootprint(layout.Value, p)) return false;
		if (!_session.roomGrid.TryGetValue(new Vector3Int(p.x, p.y, room.Floor), out Room owner) || owner != room) return false;
		if (!unit.CanMove(p)) return false;
		return !clearPassage || !AIMovementHelper.IsWithinDoorClearance(_session, room.Floor, p);
	}

	// 현장이 다른 자리로 옮겨졌을 때 옛 자리에 배정된 일꾼의 대기를 풀고 곧바로 다시 배정받게 한다.
	private void ReleaseWorkers(Party party, Room room)
	{
		foreach (var m in party.Members)
			if (m?.currentWait != null && m.currentWait.Reason == WaitReason.ConstructingBase && m.currentWait.WorkRoom == room)
			{
				m.currentWait = null;
				m.waitStuckTurns = 0;
			}
		_nextAssignTime.Remove(party);
	}

	// workers에 현장에서 일하는 인원을 채우고, talker는 그중 무작위 한 명(작업 말풍선용) — 없으면 null.
	private static int CountWorkers(Party party, Room room, List<Human> workers, out Human talker)
	{
		talker = null;
		workers.Clear();
		float now = Time.time;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f || m.currentRoom != room) continue;
			var wait = m.currentWait;
			if (wait == null || wait.Reason != WaitReason.ConstructingBase || wait.WorkRoom != room || !wait.AtSlot) continue;
			if (now - wait.LastStepTime > WorkerFreshSeconds) continue;
			workers.Add(m);
			if (Random.Range(0, workers.Count) == 0) talker = m;
		}
		return workers.Count;
	}

	// ── 현장 자리 ──
	// 청사진 영역은 방 안의 바닥 칸이면서 오브젝트·구조물(계단·건물·코어)·문(문 앞 통과 구간 포함)이 없는 칸이어야 하고, 가능하면 둘레도 그런 것에 닿지 않아야 한다(BaseSiteMath.TryPickSite).
	// 정해진 자리는 방마다 기억하고, 그 자리가 계속 막히면(플레이어가 건물을 짓는 등) 다른 자리로 옮긴다. force는 막힘을 기다리지 않고 바로 다시 검사·선정한다(방어 시설을 세우기 직전).
	private SiteLayout? ResolveLayout(Room room, bool force, out bool relocated)
	{
		// 매 프레임 불리는 길 — 검사 주기 안이면 아무것도 할당하지 않고 기억한 자리를 그대로 돌려준다(아래 본 판정은 람다를 만든다).
		relocated = false;
		if (!force && _layouts.TryGetValue(room, out var cached))
		{
			if (cached.Has && Time.time < cached.NextCheck) return cached.Layout;
			if (!cached.Has && Time.time < cached.NextPick) return null;
		}
		return RefreshLayout(room, force, out relocated);
	}

	private SiteLayout? RefreshLayout(Room room, bool force, out bool relocated)
	{
		relocated = false;
		if (!_layouts.TryGetValue(room, out var state)) _layouts[room] = state = new LayoutState();
		float now = Time.time;

		if (state.Has)
		{
			if (!force && now < state.NextCheck) return state.Layout;
			state.NextCheck = now + LayoutCheckSeconds;
			if (BaseSiteMath.IsFootprintClear(state.Layout, p => IsTileClear(room, p)))
			{
				state.BlockedSince = -1f;
				return state.Layout;
			}
			if (!force)
			{
				if (state.BlockedSince < 0f) state.BlockedSince = now;
				if (now - state.BlockedSince < RelocateAfterSeconds) return state.Layout;
			}
		}
		else if (!force && now < state.NextPick)
		{
			return null;
		}

		SiteLayout previous = state.Layout;
		bool hadLayout = state.Has;
		var clearCache = new Dictionary<Vector2Int, bool>();
		bool IsClear(Vector2Int p)
		{
			if (!clearCache.TryGetValue(p, out bool clear)) clearCache[p] = clear = IsTileClear(room, p);
			return clear;
		}

		state.BlockedSince = -1f;
		state.NextCheck = now + LayoutCheckSeconds;
		if (BaseSiteMath.TryPickSite(room.Bounds, room.Bounds.center, IsClear, p => IsTileCrowded(room, p), out SiteLayout picked))
		{
			state.Has = true;
			state.Layout = picked;
			relocated = hadLayout && !picked.Equals(previous);
		}
		else
		{
			state.Has = false;
			state.NextPick = now + LayoutRetrySeconds;
			relocated = hadLayout;
		}
		return state.Has ? state.Layout : (SiteLayout?)null;
	}

	// 현장 영역에 들어가도 되는 칸 — 이 방 안, 걸을 수 있는 바닥(벽·계단·건물·코어 제외), 오브젝트·문 없음, 문 앞 통과 구간 밖, 방 인원 라벨 아래가 아님.
	private bool IsTileClear(Room room, Vector2Int p)
	{
		var key = new Vector3Int(p.x, p.y, room.Floor);
		if (!_session.roomGrid.TryGetValue(key, out Room owner) || owner != room) return false;
		if (BaseSiteMath.IsUnderRoomLabel(room.Bounds.center, p)) return false;
		if (_session.cmap == null || !_session.cmap.IsStaticTileWalkable(room.Floor, p)) return false;
		if (_session.objectGrid.ContainsKey(key)) return false;
		if (_session.IsRepairableDoorTile(key)) return false;
		return !AIMovementHelper.IsWithinDoorClearance(_session, room.Floor, p);
	}

	// 현장 둘레에 닿으면 답답해 보이는 칸 — 오브젝트(코어·전리품·함정·시체 등)나 구조물(계단·건물·코어). 벽은 방 가장자리라 세지 않는다.
	private bool IsTileCrowded(Room room, Vector2Int p)
	{
		if (_session.objectGrid.ContainsKey(new Vector3Int(p.x, p.y, room.Floor))) return true;
		return TryGetTile(room.Floor, p, out Tile tile) && tile.isStructureExist;
	}

	private bool TryGetTile(int floorIndex, Vector2Int p, out Tile tile)
	{
		tile = default;
		var cmap = _session.cmap;
		if (cmap == null || cmap.map.floors == null || floorIndex < 0 || floorIndex >= cmap.map.floors.Length || p.x < 0 || p.y < 0) return false;
		Floor floor = cmap.map.floors[floorIndex];
		if (floor.chunks == null) return false;
		int cs = floor.config.chunkSize;
		int cx = p.x / cs, cy = p.y / cs;
		if (cx >= floor.config.width || cy >= floor.config.height) return false;
		Chunks chunk = floor.chunks[cx, cy];
		if (chunk.chunk == null) return false;
		tile = chunk.chunk[p.x % cs, p.y % cs];
		return true;
	}

	// ── 공사 현장 연출 ──
	// 공사하는 방에 청사진 + 건설물을 세우고 진행도를 따라 올린다. 공사가 끝나거나 거점을 잃으면(Reset) 걷고, 파티가 사라져 중단된 공사는 진행도와 함께 현장도 남는다. 안개 속 방에서는 숨긴다.
	private void UpdateSite(Room room, bool defense, Progress progress, float progress01, SiteLayout? layout)
	{
		_sites.TryGetValue((room, defense), out Site site);
		if (!layout.HasValue)
		{
			if (site != null) RemoveSite(room, defense);
			return;
		}
		if (site != null && !site.Layout.Equals(layout.Value))
		{
			RemoveSite(room, defense); // 현장이 옮겨졌다 — 옛 자리 연출을 걷고 새 자리에 다시 세운다
			site = null;
		}
		if (site == null)
		{
			Transform group = _session.GetFloorCategoryGroup(room.Floor, "Objects");
			var visual = BaseSiteVisual.Create(group, TileCenterWorld(room.Floor, layout.Value.Center), layout.Value, defense);
			visual.SetVisible(room.FogRevealed);
			_sites[(room, defense)] = site = new Site { Visual = visual, Layout = layout.Value };
		}

		site.Visual.SetProgress(progress01);
		EmitWorkFx(room, progress, site);
	}

	// 일하는 인원 한 명의 발치에서 먼지가 피고 건설물에서 망치 불꽃이 튄다 — 인원이 많을수록 잦다. 교전으로 공사가 멈춘 틱엔 이 함수가 불리지 않아 효과도 멈춘다.
	private void EmitWorkFx(Room room, Progress progress, Site site)
	{
		if (_workerBuffer.Count == 0 || Time.time < progress.NextFxTime) return;
		progress.NextFxTime = Time.time + BaseSiteMath.WorkFxInterval(_workerBuffer.Count) * Random.Range(0.8f, 1.2f);

		Human worker = _workerBuffer[Random.Range(0, _workerBuffer.Count)];
		Vector3 workerWorld = TileCenterWorld(room.Floor, worker.position);
		site.Visual.EmitWorkerDust(workerWorld);
		site.Visual.EmitHammerSpark();
	}

	private Vector3 TileCenterWorld(int floor, Vector2Int tile)
	{
		Vector3 offset = _session.unitGenerate != null ? _session.unitGenerate.GetFloorOffset(floor) : Vector3.zero;
		return new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f) + offset;
	}

	// 매 프레임 호출 — 방의 안개 상태에 맞춰 현장·상징물을 켜고 끈다(바뀔 때만 SetActive). 상징물은 이 틱에서 자기 칸이 아직 비어 있는지도 본다.
	public void RefreshVisuals()
	{
		foreach (var pair in _sites)
			if (pair.Value.Visual != null) pair.Value.Visual.SetVisible(pair.Key.room.FogRevealed);
		foreach (var pair in _gateBlueprints)
			if (pair.Value != null) pair.Value.SetVisible(pair.Key.FogRevealed);
		TickEmblems();
	}

	// ── 문·목책 설치 청사진 ──
	// 기존 문을 모두 철거한 뒤 인류 문과 목책을 새로 설치하는 동안만 그 자리에 청사진을 보여 준다(2026-10-22 사용자 요청 "설치할 때 만"). 전체 공사 진행도를 따라 채워지고, 완공하면 실제 문·목책이 서며 사라진다.
	private void UpdateGateBlueprint(Room room, float progress01)
	{
		if (!_gateBlueprints.TryGetValue(room, out BaseGateBlueprint blueprint) || blueprint == null)
		{
			_session.CollectRoomGates(room, _gateBuffer);
			var doorCenters = new List<Vector3>();
			var fenceCenters = new List<Vector3>();
			foreach (var gate in _gateBuffer)
			{
				if (!gate.HasDoor)
					foreach (var tile in gate.DoorTiles) doorCenters.Add(TileCenterWorld(room.Floor, new Vector2Int(tile.x, tile.y)));
				foreach (var tile in gate.FenceTiles) fenceCenters.Add(TileCenterWorld(room.Floor, new Vector2Int(tile.x, tile.y)));
			}
			if (doorCenters.Count == 0 && fenceCenters.Count == 0) return;

			Transform group = _session.GetFloorCategoryGroup(room.Floor, "Objects");
			blueprint = BaseGateBlueprint.Create(group, doorCenters, fenceCenters);
			blueprint.SetVisible(room.FogRevealed);
			_gateBlueprints[room] = blueprint;
		}
		blueprint.SetProgress(progress01);
	}

	// 설치 청사진을 걷는다 — 철거 단계로 되돌아갈 때(설치 중 인류가 아닌 문이 다시 섰다)·거점을 잃을 때는 곧바로, 설치를 마칠 때는 옅어지며(celebrate).
	public void DismissGateBlueprint(Room room, bool celebrate)
	{
		if (!_gateBlueprints.TryGetValue(room, out BaseGateBlueprint blueprint)) return;
		_gateBlueprints.Remove(room);
		if (blueprint != null) blueprint.Finish(celebrate);
	}

	private void RemoveSite(Room room, bool defense)
	{
		if (!_sites.TryGetValue((room, defense), out Site site)) return;
		if (site.Visual != null) site.Visual.Dismiss(false);
		_sites.Remove((room, defense));
	}

	// ── 기초 거점 상징물 ──
	// 기초 시설을 완공하면 건설물(천막)이 그 자리에 상징물로 남는다. 방어 시설 공사 중에는 같은 칸에 망루가 올라가므로 숨기고, 거점을 잃으면 걷는다(Reset).
	// 순수 비주얼이라 아무도 이 칸을 막지 못한다 — 플레이어가 건물을 짓거나 함정·전리품이 놓여 칸이 3초 넘게 막히면 근처 빈 칸으로 옮긴다(시체는 일시적이라 세지 않는다).
	private void PlaceEmblem(Room room, Site finished, bool animate)
	{
		RemoveEmblem(room);

		BaseSiteVisual visual;
		Vector2Int tile;
		int radius;
		if (finished?.Visual != null)
		{
			visual = finished.Visual;
			tile = finished.Layout.Center;
			radius = finished.Layout.Radius;
			visual.Settle(animate);
		}
		else
		{
			// 현장 연출이 없던 완공(자리가 없었거나 debug로 곧바로 만든 거점) — 지금 고른 자리에 상징물만 바로 세운다. 자리가 없으면 상징물 없이 거점만 선다.
			SiteLayout? layout = ResolveLayout(room, force: true, out _);
			if (!layout.HasValue) return;
			tile = layout.Value.Center;
			radius = layout.Value.Radius;
			Transform group = _session.GetFloorCategoryGroup(room.Floor, "Objects");
			visual = BaseSiteVisual.Create(group, TileCenterWorld(room.Floor, tile), layout.Value, defense: false);
			visual.Settle(false);
		}

		_emblems[room] = new Emblem { Visual = visual, Tile = tile, Radius = radius, BlockedSince = -1f };
	}

	private void RemoveEmblem(Room room)
	{
		if (!_emblems.TryGetValue(room, out Emblem emblem)) return;
		if (emblem.Visual != null) emblem.Visual.Dismiss(false);
		_emblems.Remove(room);
	}

	private void TickEmblems()
	{
		if (_emblems.Count == 0) return;
		float now = Time.time;
		foreach (var pair in _emblems)
		{
			Room room = pair.Key;
			Emblem emblem = pair.Value;
			if (emblem.Visual == null) continue;

			emblem.Visual.SetVisible(room.FogRevealed && !_sites.ContainsKey((room, true))); // 방어 시설 공사 중에는 같은 칸에 망루가 올라간다
			if (now < emblem.NextCheck) continue;
			emblem.NextCheck = now + EmblemCheckSeconds;

			if (!IsEmblemFootprintBlocked(room, emblem)) { emblem.BlockedSince = -1f; continue; }
			if (emblem.BlockedSince < 0f) emblem.BlockedSince = now;
			if (now - emblem.BlockedSince < RelocateAfterSeconds) continue;
			RelocateEmblem(room, emblem);
		}
	}

	// 상징물이 덮는 영역(크게 그린 3×3 자리면 둘레 한 겹까지)에 무언가 막고 섰는가 — 큰 천막이 새로 지은 건물·함정 위로 겹쳐 보이지 않게 한다.
	private bool IsEmblemFootprintBlocked(Room room, Emblem emblem)
	{
		for (int dx = -emblem.Radius; dx <= emblem.Radius; dx++)
			for (int dy = -emblem.Radius; dy <= emblem.Radius; dy++)
				if (IsEmblemTileBlocked(room, emblem.Tile + new Vector2Int(dx, dy))) return true;
		return false;
	}

	// 상징물 칸이 막혔는가 — 건물·계단·코어 같은 구조물이 서거나, 시체가 아닌 오브젝트(함정·전리품 등)가 놓였다.
	private bool IsEmblemTileBlocked(Room room, Vector2Int tile)
	{
		if (_session.cmap == null || !_session.cmap.IsStaticTileWalkable(room.Floor, tile)) return true;
		if (!_session.objectGrid.TryGetValue(new Vector3Int(tile.x, tile.y, room.Floor), out InteractableObject obj)) return false;
		return obj.Tags == null || !obj.Tags.Exists(t => t.Contains("Corpse"));
	}

	private void RelocateEmblem(Room room, Emblem emblem)
	{
		var preferred = new Vector2(emblem.Tile.x + 0.5f, emblem.Tile.y + 0.5f);
		// 3×3 자리를 먼저 찾고 없으면 1×1로 줄여 옮긴다 — 1×1 자리에선 천막도 한 칸 크기로 줄어 이웃 칸을 덮지 않는다.
		if (BaseSiteMath.TryPickSite(room.Bounds, preferred, p => IsTileClear(room, p), p => IsTileCrowded(room, p), out SiteLayout picked))
		{
			emblem.Tile = picked.Center;
			emblem.Radius = picked.Radius;
			emblem.Visual.MoveTo(TileCenterWorld(room.Floor, picked.Center), picked.Radius);
		}
		emblem.BlockedSince = -1f; // 옮겼든 자리를 못 찾았든 다음 확인부터 다시 센다
	}
}
