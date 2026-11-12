using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;

// 배치모드 세션 서비스 — 유닛별 방어 위치 저장, 정원·유입 집계, 이동 적용, 자동 복귀.
// 저장·이동 명령·실제 도착은 서로 다른 시점이다. 저장은 계획이고 걷기는 [이동 적용] 뒤 시간을 재개해야 일어난다.
//   .cs 저장 편집·조회 / .Orders.cs 이동 적용·자동 복귀 / .Hooks.cs 다른 시스템이 부르는 훅과 주기 점검
public sealed partial class DefensePlacementSystem
{
	public static DefensePlacementSystem Instance { get; private set; }

	const float ValidateIntervalSeconds = 0.5f; // 실시간 — 일시정지 중에도 돈다

	IObjectResolver _resolver;
	GameSession _cachedSession;
	GameSession Session => _cachedSession ??= _resolver.Resolve<GameSession>();

	readonly Dictionary<Unit, DefenseRecord> _records = new Dictionary<Unit, DefenseRecord>();
	readonly Dictionary<Unit, IssuedMove> _moves = new Dictionary<Unit, IssuedMove>();
	// 도착한 자리 — IdleFSMState가 흩어지지 않고 이 방향으로 선다.
	readonly Dictionary<Unit, (Vector2Int tile, Dir dir)> _holds = new Dictionary<Unit, (Vector2Int, Dir)>();
	// 자기 발행의 SetMoveCommand를 '다른 명령으로 교체'로 오해하지 않게 한다.
	bool _issuing;
	float _nextValidateTime;

	// 방 라벨·UI가 방마다 조회하므로 프레임당 한 번만 모은다.
	readonly Dictionary<(int floor, int roomId), int> _planned = new Dictionary<(int, int), int>();
	readonly Dictionary<Room, int> _incoming = new Dictionary<Room, int>();
	int _plannedFrame = -1;
	bool _plannedDirty = true;
	int _incomingFrame = -1;

	[Inject]
	public void Construct(IObjectResolver resolver)
	{
		_resolver = resolver;
		Instance = this;
	}

	// ── 조회 ──

	public IReadOnlyCollection<DefenseRecord> Records => _records.Values;

	public bool TryGetRecord(Unit unit, out DefenseRecord record)
	{
		record = null;
		return unit != null && _records.TryGetValue(unit, out record);
	}

	public bool IsPlaced(Unit unit) => unit != null && _records.ContainsKey(unit);

	public bool TryGetHold(Unit unit, out Vector2Int tile, out Dir dir)
	{
		tile = default;
		dir = default;
		if (unit == null || !_holds.TryGetValue(unit, out var h)) return false;
		tile = h.tile;
		dir = h.dir;
		return true;
	}

	public void ReleaseHold(Unit unit)
	{
		if (unit != null) _holds.Remove(unit);
	}

	public static bool IsManageable(Unit u) => u != null && u.hp > 0f && u.IsPlayerMonsterFaction;

	// 방어 배치 N — 그 방에 저장된 유닛의 인구수 합. 계획이라 실제 소속(현 인원)과 다르다.
	public int PlannedPopulation(Room room)
	{
		if (room == null) return 0;
		if (_plannedDirty || _plannedFrame != Time.frameCount) RebuildPlanned();
		return _planned.TryGetValue((room.Floor, room.RoomId), out int n) ? n : 0;
	}

	void RebuildPlanned()
	{
		_planned.Clear();
		foreach (var r in _records.Values)
		{
			if (!IsManageable(r.Unit)) continue;
			var key = (r.Floor, r.RoomId);
			_planned.TryGetValue(key, out int n);
			_planned[key] = n + Mathf.Max(0, r.Unit.populationCost);
		}
		_plannedFrame = Time.frameCount;
		_plannedDirty = false;
	}

	public List<Unit> UnitsSavedInRoom(int floor, int roomId)
	{
		var list = new List<Unit>();
		foreach (var r in _records.Values)
			if (r.Floor == floor && r.RoomId == roomId && IsManageable(r.Unit)) list.Add(r.Unit);
		return list;
	}

	public List<Unit> UnitsSavedOnFloor(int floor)
	{
		var list = new List<Unit>();
		foreach (var r in _records.Values)
			if (r.Floor == floor && IsManageable(r.Unit)) list.Add(r.Unit);
		return list;
	}

	public bool AnySavedOnFloor(int floor)
	{
		foreach (var r in _records.Values)
			if (r.Floor == floor && IsManageable(r.Unit)) return true;
		return false;
	}

	// 이 방으로 이동 중인 인구 — 생산 정원 검사(BuildingManager.CountConfirmedIncomingPlayerPop)와 같은 기준이다.
	public int IncomingPopulation(Room room)
	{
		if (room == null) return 0;
		if (_incomingFrame != Time.frameCount)
		{
			_incoming.Clear();
			CollectIncoming(_incoming, null);
			_incomingFrame = Time.frameCount;
		}
		return _incoming.TryGetValue(room, out int n) ? n : 0;
	}

	int CountIncoming(Room room, HashSet<Unit> excluding)
	{
		var scratch = new Dictionary<Room, int>();
		CollectIncoming(scratch, excluding);
		return scratch.TryGetValue(room, out int n) ? n : 0;
	}

	void CollectIncoming(Dictionary<Room, int> into, HashSet<Unit> excluding)
	{
		var session = Session;
		if (session?.units == null) return;
		foreach (var u in session.units)
		{
			if (!IsManageable(u) || (excluding != null && excluding.Contains(u))) continue;
			if (!u.isManualMoveCommand || !u.playerMoveTarget.HasValue) continue;
			Vector2Int target = u.playerMoveTarget.Value;
			if (!session.roomGrid.TryGetValue(new Vector3Int(target.x, target.y, u.currentFloor), out Room dest) || dest == null) continue;
			if (dest.Floor != u.currentFloor) continue;
			if (!DefensePlacementMath.CountsAsIncoming(u.currentRoom != null ? u.currentRoom.RoomId : -1, dest.RoomId)) continue;
			into.TryGetValue(dest, out int n);
			into[dest] = n + Mathf.Max(0, u.populationCost);
		}
	}

	// 이 방에서 진화 연출 중인 유닛이 확보한 추가 인구(1-5 "실제 유입 = 생존 소속 + 다른 유입 확정분 + 진화 추가 확보분").
	int EvolutionReservedExtra(Room room)
	{
		var session = Session;
		if (room == null || session?.units == null) return 0;
		int extra = 0;
		foreach (var u in session.units)
		{
			if (u == null || u.hp <= 0f || !u.IsInEvolutionPresentation || u.currentRoom != room) continue;
			extra += DefensePlacementMath.EvolutionExtraPopulation(u.activeEvolutionSession.Definition.PostEvolutionPopulationCost, Mathf.Max(0, u.populationCost));
		}
		return extra;
	}

	// 실제 유입 검사에 쓰는 "이미 확정된 몫" — 이동 중인 유입과 진화 확보분.
	int CommittedInflow(Room room, HashSet<Unit> excluding) => CountIncoming(room, excluding) + EvolutionReservedExtra(room);

	bool TryGetRoom(int floor, Vector2Int tile, out Room room)
	{
		room = null;
		return Session != null && Session.roomGrid.TryGetValue(new Vector3Int(tile.x, tile.y, floor), out room) && room != null;
	}

	// ── 저장 위치 검사 ──

	readonly List<Vector2Int> _bodyBuffer = new List<Vector2Int>();

	// None이면 저장 가능.
	public PlacementBlock CheckSave(Unit unit, int floor, Vector2Int tile)
	{
		if (unit == null) return PlacementBlock.NotPlayerUnit;
		if (unit.hp <= 0f) return PlacementBlock.Dead;
		if (!unit.IsPlayerMonsterFaction) return PlacementBlock.NotPlayerUnit;
		if (unit.IsInEvolutionPresentation) return PlacementBlock.Evolving;
		if (!BuildingManager.AreCoreFunctionsActive(floor)) return PlacementBlock.CoreDestroyed;

		Vector2Int footprint = unit.FootprintSize;
		PlacementBlock body = CheckBody(floor, tile, footprint, out Room room);
		if (body != PlacementBlock.None) return body;
		if (room.RoomFaction != FactionType.Player) return PlacementBlock.RoomNotSecured;

		foreach (var other in _records.Values)
		{
			if (other.Unit == unit || other.Floor != floor || !IsManageable(other.Unit)) continue;
			if (DefensePlacementMath.BodiesOverlap(tile, footprint, other.Tile, other.Unit.FootprintSize)) return PlacementBlock.OverlapsOtherDefense;
		}

		bool sameRoomAsOld = _records.TryGetValue(unit, out var old) && old.Floor == room.Floor && old.RoomId == room.RoomId;
		int plannedOthers = DefensePlacementMath.PlannedOthers(PlannedPopulation(room), sameRoomAsOld, unit.populationCost);
		return DefensePlacementMath.CanSaveWithinCapacity(plannedOthers, unit.populationCost, room.MaxPopulation) ? PlacementBlock.None : PlacementBlock.RoomFull;
	}

	// 몸 전체가 한 방 안의 서 있을 수 있는 바닥인가 — 저장 위치와 이동 최종 몸체가 함께 쓴다.
	public PlacementBlock CheckBody(int floor, Vector2Int tile, Vector2Int footprint, out Room room)
	{
		room = null;
		var session = Session;
		if (session?.cmap == null) return PlacementBlock.OutOfRoom;

		DefensePlacementMath.BodyTiles(tile, footprint, _bodyBuffer);
		foreach (var cell in _bodyBuffer)
		{
			var key = new Vector3Int(cell.x, cell.y, floor);
			if (!session.roomGrid.TryGetValue(key, out Room cellRoom) || cellRoom == null) return PlacementBlock.OutOfRoom;
			if (room == null) room = cellRoom;
			else if (room != cellRoom) return PlacementBlock.OutOfRoom;

			if (!session.cmap.IsStaticTileWalkable(floor, cell)) return PlacementBlock.NotWalkable;
			if (session.IsDoorTile(key)) return PlacementBlock.DoorOrStair;
			if (session.objectGrid.TryGetValue(key, out InteractableObject obj) && DoorSystem.IsBarrier(obj)) return PlacementBlock.DoorOrStair;
			if (BuildingManager.Instance != null && BuildingManager.Instance.GetBuildingAt(key) != null) return PlacementBlock.Facility;
			if (BuildingManager.Instance != null && BuildingManager.Instance.IsFrontZoneReserved(key)) return PlacementBlock.FrontZone;
		}
		return room == null ? PlacementBlock.OutOfRoom : PlacementBlock.None;
	}

	// ── 저장 편집 ──

	// 다른 방에 새로 저장하면 이전 방의 지정을 교체한다. 현재 작업·전투·이동은 건드리지 않는다.
	public bool TrySave(Unit unit, int floor, Vector2Int tile, Dir facing, out PlacementBlock block)
	{
		block = CheckSave(unit, floor, tile);
		if (block != PlacementBlock.None) return false;
		if (!TryGetRoom(floor, tile, out Room room)) { block = PlacementBlock.OutOfRoom; return false; }

		_records[unit] = new DefenseRecord { Unit = unit, Floor = floor, RoomId = room.RoomId, Tile = tile, Facing = facing };
		_plannedDirty = true;
		LogHelper.Log(LogHelper.GAME, $"[배치] {unit.unitType?.typeName} 방어 위치 저장 — F{floor} 방 {room.RoomId} ({tile.x},{tile.y}) {facing}");
		RetargetAutoReturn(unit);
		return true;
	}

	public bool SetFacing(Unit unit, Dir facing)
	{
		if (!TryGetRecord(unit, out var record)) return false;
		record.Facing = facing;
		return true;
	}

	// 저장 위치·방향과 자동 복귀만 해제한다. 이미 발행한 직접 이동과 현재 작업·전투는 그대로다.
	public void Unplace(Unit unit)
	{
		if (unit == null) return;
		if (_records.Remove(unit)) _plannedDirty = true;
		if (_moves.TryGetValue(unit, out var move) && move.Source == DefenseMoveSource.AutoReturn)
			CancelMove(unit, abortCommand: true);
	}
}
