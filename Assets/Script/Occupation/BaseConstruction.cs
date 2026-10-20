using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 점령 파티의 거점 시설 공사 — 방마다 기초/방어 공사 진행도를 들고, 현장 자리 배정·일하는 인원 집계·유닛 한 틱(WaitReason.ConstructingBase)을 맡는다(코어 점령 v0.2 12장).
// 진행도는 현장 자리에 선 인원 수에 비례한다. 전투·경계가 우선한 틱엔 유닛이 일하는 리프를 못 돌아 인원에서 빠지고, 진행도는 남는다.
internal sealed class BaseConstruction
{
	private const float WorkerFreshSeconds = 2.5f;
	private const float AssignIntervalSeconds = 1f;
	private const float LogIntervalSeconds = 10f;

	private sealed class Progress
	{
		public float Done;
		public float NextLogTime = -1f;
		public float NextTalkTime;   // 일꾼 작업 말풍선을 다시 띄울 수 있는 시각
		public bool HalfAnnounced;   // 절반 진행 알림을 이미 냈는지
	}

	// 공사 현장 표식 한 장 — 문 곁에 놓고 공사 진행 막대를 붙인다. 오브젝트 로직(InteractableObject)이 없는 순수 비주얼이라 이동·조사·인지에 영향을 주지 않는다.
	private sealed class SiteMarker
	{
		public GameObject Go;
		public ObjectProgressBarVisual Bar;
	}

	private static Sprite _siteSprite;

	private readonly GameSession _session;
	private readonly Dictionary<(Room room, bool defense), Progress> _progress = new Dictionary<(Room, bool), Progress>();
	private readonly Dictionary<(Room room, bool defense), List<SiteMarker>> _sites = new Dictionary<(Room, bool), List<SiteMarker>>();
	private readonly Dictionary<Party, float> _nextAssignTime = new Dictionary<Party, float>();

	public BaseConstruction(GameSession session) => _session = session;

	public static bool IsOpen(Room room, bool defense)
		=> defense ? OccupationMath.NeedsDefenseBuild(room.BaseStage, room.DefenseDesignated) : room.BaseStage == BaseStage.None;

	public float Progress01(Room room, bool defense)
		=> _progress.TryGetValue((room, defense), out var p) ? OccupationMath.Progress01(p.Done, Total(defense)) : 0f;

	public void Reset(Room room)
	{
		_progress.Remove((room, false));
		_progress.Remove((room, true));
		RemoveSite(room);
	}

	public void Forget(Party party) => _nextAssignTime.Remove(party);

	// 한 방의 공사를 한 프레임 진행한다. started는 이번 틱에 처음 시작했을 때, halfway는 기초 공사가 처음 절반을 넘었을 때(한 번), 반환값은 공사가 끝났을 때 true다.
	public bool Tick(Party party, Room room, bool defense, float deltaTime, out bool started, out bool halfway)
	{
		if (!_progress.TryGetValue((room, defense), out var progress)) _progress[(room, defense)] = progress = new Progress { NextTalkTime = Time.time + 3f };
		started = progress.NextLogTime < 0f;

		AssignBuilders(party, room, defense);
		int workers = CountWorkers(party, room, out Human talker);
		progress.Done += OccupationMath.WorkDelta(workers, deltaTime);

		float progress01 = OccupationMath.Progress01(progress.Done, Total(defense));
		UpdateSite(room, defense, progress01);
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

	// 건설 대기의 유닛 한 틱 — 현장 자리로 가서 서 있으면 일하는 것으로 센다. 일이 필요 없어졌으면 대기를 풀고 Success.
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

	private static float Total(bool defense) => defense ? OccupationMath.DefenseWorkUnits : OccupationMath.BasicWorkUnits;

	// 같은 방 파티원에게 현장 자리를 나눠 준다 — 방 쪽 문 묶음 곁을 번갈아 쓴다. 다른 대기(집결·보고 등)가 있는 유닛과 후퇴 이동 중인 유닛은 건드리지 않는다(거점에서 쉬는 인원은 방어 시설 공사에 참여한다).
	private void AssignBuilders(Party party, Room room, bool defense)
	{
		if (Time.time < (_nextAssignTime.TryGetValue(party, out float next) ? next : 0f)) return;
		_nextAssignTime[party] = Time.time + AssignIntervalSeconds;

		var anchors = _session.GetRoomDoorAnchors(room);
		var claimed = new HashSet<Vector2Int>();
		foreach (var m in party.Members)
			if (m?.currentWait != null && m.currentWait.Reason == WaitReason.ConstructingBase && m.currentWait.WaitPosition.HasValue)
				claimed.Add(m.currentWait.WaitPosition.Value);

		int index = 0;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f || m.currentFloor != room.Floor || m.currentRoom != room) continue;
			if (RetreatSystem.WantsRetreatState(m)) continue;
			var wait = m.currentWait;
			if (wait != null && wait.Reason != WaitReason.SearchingNextDoor && wait.Reason != WaitReason.ConstructingBase) continue;
			if (wait != null && wait.Reason == WaitReason.ConstructingBase) { index++; continue; }

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

	// talker는 일하는 인원 중 무작위 한 명(작업 말풍선용) — 없으면 null.
	private static int CountWorkers(Party party, Room room, out Human talker)
	{
		talker = null;
		int workers = 0;
		float now = Time.time;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f || m.currentRoom != room) continue;
			var wait = m.currentWait;
			if (wait == null || wait.Reason != WaitReason.ConstructingBase || wait.WorkRoom != room || !wait.AtSlot) continue;
			if (now - wait.LastStepTime > WorkerFreshSeconds) continue;
			workers++;
			if (Random.Range(0, workers) == 0) talker = m;
		}
		return workers;
	}

	// ── 공사 현장 표식 ──
	// 공사하는 방의 문마다 방 쪽 안쪽 칸에 표식을 놓고 공통 진행도를 막대로 보여 준다. 공사가 끝나거나 거점을 잃으면(Reset) 걷고, 파티가 사라져 중단된 공사는 진행도와 함께 표식도 남는다. 안개 속 방에서는 숨긴다.
	private void UpdateSite(Room room, bool defense, float progress01)
	{
		if (!_sites.TryGetValue((room, defense), out var markers))
		{
			markers = CreateSiteMarkers(room);
			_sites[(room, defense)] = markers;
		}
		foreach (var marker in markers)
			if (marker.Go != null) marker.Bar.SetProgress(progress01, true);
	}

	private List<SiteMarker> CreateSiteMarkers(Room room)
	{
		var markers = new List<SiteMarker>();
		Transform group = _session.GetFloorCategoryGroup(room.Floor, "Objects");
		Vector3 offset = _session.unitGenerate != null ? _session.unitGenerate.GetFloorOffset(room.Floor) : Vector3.zero;

		var tiles = new List<Vector2Int>();
		foreach (var anchor in _session.GetRoomDoorAnchors(room)) tiles.Add(InwardTile(room, anchor));
		if (tiles.Count == 0) tiles.Add(new Vector2Int(Mathf.FloorToInt(room.Bounds.center.x), Mathf.FloorToInt(room.Bounds.center.y)));

		foreach (var tile in tiles)
		{
			var go = new GameObject($"BaseSite_{room.RoomId}");
			if (group != null) go.transform.SetParent(group);
			var sr = go.AddComponent<SpriteRenderer>();
			sr.sprite = GetSiteSprite();
			sr.sortingOrder = 4;
			go.transform.position = new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f) + offset;
			go.SetActive(room.FogRevealed);
			markers.Add(new SiteMarker { Go = go, Bar = go.AddComponent<ObjectProgressBarVisual>() });
		}
		return markers;
	}

	// 문 앵커에서 방 중심 쪽으로 한 칸 들어간 칸 — 방 안이 아니면 앵커 칸 그대로.
	private Vector2Int InwardTile(Room room, Vector3Int anchor)
	{
		Vector2 toCenter = room.Bounds.center - new Vector2(anchor.x + 0.5f, anchor.y + 0.5f);
		Vector2Int step = Mathf.Abs(toCenter.x) >= Mathf.Abs(toCenter.y)
			? new Vector2Int(toCenter.x >= 0f ? 1 : -1, 0)
			: new Vector2Int(0, toCenter.y >= 0f ? 1 : -1);
		var inward = new Vector3Int(anchor.x + step.x, anchor.y + step.y, anchor.z);
		return _session.roomGrid.TryGetValue(inward, out Room found) && found == room
			? new Vector2Int(inward.x, inward.y)
			: new Vector2Int(anchor.x, anchor.y);
	}

	// 매 프레임 호출 — 방의 안개 상태에 맞춰 표식을 켜고 끈다(바뀔 때만 SetActive).
	public void RefreshSiteVisibility()
	{
		foreach (var pair in _sites)
		{
			bool visible = pair.Key.room.FogRevealed;
			foreach (var marker in pair.Value)
				if (marker.Go != null && marker.Go.activeSelf != visible) marker.Go.SetActive(visible);
		}
	}

	private void RemoveSite(Room room)
	{
		foreach (bool defense in new[] { false, true })
		{
			if (!_sites.TryGetValue((room, defense), out var markers)) continue;
			foreach (var marker in markers)
				if (marker.Go != null) Object.Destroy(marker.Go);
			_sites.Remove((room, defense));
		}
	}

	// 전용 아트가 생기기 전까지 쓰는 자리표시자 — 가로대와 X자 버팀목을 댄 나무 울타리 모양(16×16). 모든 표식이 공유한다(텍스처 누수 방지).
	private static Sprite GetSiteSprite()
	{
		if (_siteSprite != null) return _siteSprite;

		const int size = 16;
		var wood = new Color(0.62f, 0.42f, 0.22f, 1f);
		var dark = new Color(0.32f, 0.2f, 0.1f, 1f);
		var texture = new Texture2D(size, size) { filterMode = FilterMode.Point };
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				bool frame = x < 2 || x >= size - 2 || y < 2 || y >= size - 2;
				bool brace = Mathf.Abs(x - y) <= 1 || Mathf.Abs(x - (size - 1 - y)) <= 1;
				bool plank = y % 5 == 0;
				texture.SetPixel(x, y, frame || brace || plank ? dark : wood);
			}
		}
		texture.Apply();
		_siteSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
		return _siteSprite;
	}
}
