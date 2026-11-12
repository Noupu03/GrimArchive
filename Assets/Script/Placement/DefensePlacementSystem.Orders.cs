using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Haare.Util.Logger;

public sealed class DefenseApplyResult
{
	public readonly List<Unit> Accepted = new List<Unit>();
	public readonly List<(Unit unit, PlacementBlock reason)> Failed = new List<(Unit, PlacementBlock)>();

	// "이동 적용 3기, 제외 1기(방 정원이 부족합니다)" — 사유는 종류별로 한 번만 적는다.
	public string Summary()
	{
		var sb = new StringBuilder($"이동 적용 {Accepted.Count}기");
		if (Failed.Count == 0) return sb.ToString();

		var reasons = new List<string>();
		foreach (var f in Failed)
		{
			string r = f.reason.ToKorean();
			if (!reasons.Contains(r)) reasons.Add(r);
		}
		return sb.Append($", 제외 {Failed.Count}기({string.Join(" / ", reasons)})").ToString();
	}
}

public sealed partial class DefensePlacementSystem
{
	// 방별·전체·목록 한 명·다중 선택이 모두 이 진입점이다. 유닛별로 검사해 통과한 유닛만 작업을 멈추고 저장 위치로 걷는다.
	// 일부만 가능하면 가능한 유닛부터 적용하고, 정원이 모자라면 예상 이동 시간이 짧은 순으로 수용한다.
	public DefenseApplyResult ApplyMoves(IEnumerable<Unit> units)
	{
		var result = new DefenseApplyResult();
		var seen = new HashSet<Unit>();
		var passed = new List<(Unit unit, DefenseRecord record, Room room)>();

		foreach (var u in units)
		{
			if (u == null || !seen.Add(u)) continue;
			PlacementBlock block = CheckApply(u, out DefenseRecord record, out Room room);
			if (block != PlacementBlock.None) result.Failed.Add((u, block));
			else passed.Add((u, record, room));
		}

		var accepted = new List<(Unit unit, DefenseRecord record)>();
		var incomingByRoom = new Dictionary<Room, List<(Unit unit, DefenseRecord record)>>();
		foreach (var p in passed)
		{
			int fromRoom = p.unit.currentRoom != null ? p.unit.currentRoom.RoomId : -1;
			if (!DefensePlacementMath.CountsAsIncoming(fromRoom, p.room.RoomId)) { accepted.Add((p.unit, p.record)); continue; }
			if (!incomingByRoom.TryGetValue(p.room, out var list)) incomingByRoom[p.room] = list = new List<(Unit, DefenseRecord)>();
			list.Add((p.unit, p.record));
		}

		var batch = new HashSet<Unit>();
		foreach (var p in passed) batch.Add(p.unit);
		foreach (var kv in incomingByRoom)
			AdmitWithinCapacity(kv.Key, kv.Value, batch, accepted, result);

		foreach (var (u, rec) in accepted)
		{
			IssueMove(u, rec, DefenseMoveSource.Direct);
			result.Accepted.Add(u);
		}

		LogHelper.Log(LogHelper.GAME, $"[배치] {result.Summary()}");
		return result;
	}

	void AdmitWithinCapacity(Room room, List<(Unit unit, DefenseRecord record)> candidates, HashSet<Unit> batch,
		List<(Unit unit, DefenseRecord record)> accepted, DefenseApplyResult result)
	{
		int remaining = DefensePlacementMath.RemainingCapacity(room.MaxPopulation, room.CurrentPopulation, CommittedInflow(room, batch));
		var scored = new List<DefensePlacementMath.Candidate>(candidates.Count);
		for (int i = 0; i < candidates.Count; i++)
		{
			var (u, rec) = candidates[i];
			scored.Add(new DefensePlacementMath.Candidate(i, Mathf.Max(0, u.populationCost), EstimateSeconds(u, rec.Tile)));
		}

		var admitted = new HashSet<int>(DefensePlacementMath.AcceptWithinRemaining(scored, remaining));
		for (int i = 0; i < candidates.Count; i++)
		{
			if (admitted.Contains(i)) accepted.Add(candidates[i]);
			else result.Failed.Add((candidates[i].unit, PlacementBlock.RoomFull));
		}
	}

	// 정원을 뺀 유닛별 사전 검사. 코어 권한은 현재 방과 저장 목적 방 모두 가동 중이어야 한다.
	public PlacementBlock CheckApply(Unit unit, out DefenseRecord record, out Room destRoom)
	{
		record = null;
		destRoom = null;
		if (unit == null || unit.hp <= 0f) return PlacementBlock.Dead;
		if (!unit.IsPlayerMonsterFaction) return PlacementBlock.NotPlayerUnit;
		if (!_records.TryGetValue(unit, out record)) return PlacementBlock.NotPlaced;
		if (record.NeedsReassign) return PlacementBlock.NeedsReassign;
		if (unit.IsInEvolutionPresentation) return PlacementBlock.Evolving;
		if (!BuildingManager.AreCoreFunctionsActive(unit.currentFloor) || !BuildingManager.AreCoreFunctionsActive(record.Floor)) return PlacementBlock.CoreDestroyed;
		if (unit.currentFloor != record.Floor) return PlacementBlock.WrongFloor;

		PlacementBlock body = CheckBody(record.Floor, record.Tile, unit.FootprintSize, out destRoom);
		if (body == PlacementBlock.Facility || body == PlacementBlock.FrontZone) return PlacementBlock.DestinationReserved;
		if (body != PlacementBlock.None) return body;
		if (destRoom.RoomFaction != FactionType.Player) return PlacementBlock.RoomNotSecured;

		int fromRoom = unit.currentRoom != null ? unit.currentRoom.RoomId : -1;
		if (fromRoom < 0 || fromRoom == destRoom.RoomId) return PlacementBlock.None;

		// 문 폭과 방 도달성을 따로 판정해 사유를 구분한다(실제 A* 경로 조회가 아니라 방 단위 근사).
		Vector2Int fp = unit.FootprintSize;
		if (!Session.cmap.CanEntityEnterRoom(Mathf.Max(fp.x, fp.y), record.Floor, destRoom.RoomId)) return PlacementBlock.DoorTooNarrow;
		if (!Session.CanFactionReachRoom(FactionType.Player, record.Floor, fromRoom, destRoom.RoomId)) return PlacementBlock.Unreachable;
		return PlacementBlock.None;
	}

	static float EstimateSeconds(Unit unit, Vector2Int to)
	{
		int steps = Mathf.Max(Mathf.Abs(unit.position.x - to.x), Mathf.Abs(unit.position.y - to.y));
		return steps / Mathf.Max(0.1f, unit.AppliedWalkSpeed);
	}

	// 작업 해제·운반물 내려놓기는 SetMoveCommand가 BuildingManager.NotifyWorkerCommandChanged로 처리한다.
	// 반복 적용은 기존 명령을 교체하므로(이동 명령 하나가 곧 예약) 유입 인구가 중복되지 않는다.
	void IssueMove(Unit unit, DefenseRecord record, DefenseMoveSource source)
	{
		_holds.Remove(unit);

		if (unit.position == record.Tile)
		{
			_issuing = true;
			try { BuildingManager.Instance?.NotifyWorkerCommandChanged(unit); }
			finally { _issuing = false; }
			_moves.Remove(unit);
			ApplyHold(unit, record.Tile, record.Facing);
			return;
		}

		_issuing = true;
		try
		{
			unit.playerInteractTarget = null;
			unit.SetMoveCommand(record.Tile, false, false);
			// 자동 복귀는 전투·경계에 밀리는 이동이다(명세 1-6 표1). 표식은 SetMoveCommand의 경로 미리 계산 뒤에 켜야 그 계산이 방 이탈 제한 없이 구해진다.
			// 직접 이동 적용은 "지정 위치 이동 우선"이라 표식 없이 예전처럼 강제 고착이다.
			unit.isDefenseAutoReturn = source == DefenseMoveSource.AutoReturn && (AIConfigLoader.Behavior?.defenseReturnYieldsToCombatEnabled ?? true);
		}
		finally { _issuing = false; }
		_moves[unit] = new IssuedMove { Unit = unit, Source = source, Floor = record.Floor, RoomId = record.RoomId, Tile = record.Tile, Facing = record.Facing };
	}

	void ApplyHold(Unit unit, Vector2Int tile, Dir facing)
	{
		_holds[unit] = (tile, facing);
		unit.currentDir = facing;
		unit.Generate?.UpdateUnitSpriteForDirection(unit);
	}

	void CancelMove(Unit unit, bool abortCommand)
	{
		if (unit == null) return;
		_moves.Remove(unit);
		if (abortCommand && unit.playerMoveTarget.HasValue && unit.isManualMoveCommand) unit.AbortMoveCommand();
	}

	// 단일 작업 종료 때만 부른다. 정원이 없거나 위치를 쓸 수 없으면 복귀하지 않고 자동 재시도도 없다.
	public bool TryAutoReturn(Unit unit)
	{
		if (!IsManageable(unit) || !_records.TryGetValue(unit, out var record)) return false;
		if (unit.HasActivePlayerCommand()) return false;
		if (CheckApply(unit, out _, out Room room) != PlacementBlock.None) return false;

		int fromRoom = unit.currentRoom != null ? unit.currentRoom.RoomId : -1;
		if (DefensePlacementMath.CountsAsIncoming(fromRoom, room.RoomId))
		{
			int remaining = DefensePlacementMath.RemainingCapacity(room.MaxPopulation, room.CurrentPopulation, CommittedInflow(room, new HashSet<Unit> { unit }));
			if (Mathf.Max(0, unit.populationCost) > remaining) return false;
		}

		IssueMove(unit, record, DefenseMoveSource.AutoReturn);
		return true;
	}

	// 걷는 중인 자동 복귀는 최신 저장 위치를 따른다. 직접 발행한 이동은 바꾸지 않는다.
	void RetargetAutoReturn(Unit unit)
	{
		if (!_moves.TryGetValue(unit, out var move) || move.Source != DefenseMoveSource.AutoReturn) return;
		CancelMove(unit, abortCommand: true);
		TryAutoReturn(unit);
	}
}
