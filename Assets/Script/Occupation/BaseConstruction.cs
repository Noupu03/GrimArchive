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
	}

	private readonly GameSession _session;
	private readonly Dictionary<(Room room, bool defense), Progress> _progress = new Dictionary<(Room, bool), Progress>();
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
	}

	public void Forget(Party party) => _nextAssignTime.Remove(party);

	// 한 방의 공사를 한 프레임 진행한다. started는 이번 틱에 처음 시작했을 때, 반환값은 공사가 끝났을 때 true다.
	public bool Tick(Party party, Room room, bool defense, float deltaTime, out bool started)
	{
		if (!_progress.TryGetValue((room, defense), out var progress)) _progress[(room, defense)] = progress = new Progress();
		started = progress.NextLogTime < 0f;

		AssignBuilders(party, room, defense);
		int workers = CountWorkers(party, room);
		progress.Done += OccupationMath.WorkDelta(workers, deltaTime);

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

	private static int CountWorkers(Party party, Room room)
	{
		int workers = 0;
		float now = Time.time;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0f || m.currentRoom != room) continue;
			var wait = m.currentWait;
			if (wait == null || wait.Reason != WaitReason.ConstructingBase || wait.WorkRoom != room || !wait.AtSlot) continue;
			if (now - wait.LastStepTime <= WorkerFreshSeconds) workers++;
		}
		return workers;
	}
}
