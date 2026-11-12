using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

public sealed partial class DefensePlacementSystem
{
	// 사망자는 저장·예약을 모두 지운다.
	public void OnUnitDied(Unit unit)
	{
		if (unit == null) return;
		if (_records.Remove(unit)) _plannedDirty = true;
		_moves.Remove(unit);
		_holds.Remove(unit);
	}

	// 진화 결과 몸체·인구가 저장 위치에 안 맞으면 그 유닛의 저장만 해제한다(실제 소속·위치 불변). 코어 파괴는 일시적이라 지우지 않는다.
	public void OnUnitEvolved(Unit unit)
	{
		_plannedDirty = true;
		if (unit == null || !_records.TryGetValue(unit, out var record)) return;
		PlacementBlock block = CheckSave(unit, record.Floor, record.Tile);
		if (block == PlacementBlock.None || block == PlacementBlock.CoreDestroyed) return;

		LogHelper.Log(LogHelper.GAME, $"[배치] {unit.unitType?.typeName} 진화 뒤 저장 위치를 쓸 수 없어({block.ToKorean()}) 미배치로 돌립니다.");
		Unplace(unit);
		NoticeCenter.Instance?.PushMomentary($"{unit.unitType?.typeName}: 진화 뒤 몸이 저장 위치에 맞지 않아 방어 위치가 해제되었습니다.", NoticeCenter.WarningColor);
	}

	// 다른 플레이어 명령이 발행한 이동을 교체했다(BuildingManager.NotifyWorkerCommandChanged).
	public void OnPlayerCommandReplaced(Unit unit)
	{
		if (_issuing || unit == null) return;
		_moves.Remove(unit);
		_holds.Remove(unit);
	}

	// 도착 — 명령에 담긴 방향으로 그 자리에서 대기한다. A에 도착해도 B로 자동 재이동하지 않는다.
	public void OnMoveFinished(Unit unit)
	{
		if (unit == null || !_moves.TryGetValue(unit, out var move)) return;
		_moves.Remove(unit);
		ApplyHold(unit, unit.position, move.Facing);
	}

	public void OnMoveAborted(Unit unit)
	{
		if (unit != null) _moves.Remove(unit);
	}

	public void OnWorkTaskEnded(Unit unit)
	{
		if (unit != null) TryAutoReturn(unit);
	}

	// 건설 예약 충돌·코어 파괴 점검(0.5초 실시간).
	public void Tick(float unscaledDeltaTime)
	{
		if (Time.unscaledTime < _nextValidateTime) return;
		_nextValidateTime = Time.unscaledTime + ValidateIntervalSeconds;

		PruneUnmanageable();
		FlagReassignNeeds();
		CancelInvalidMoves();
	}

	void PruneUnmanageable()
	{
		List<Unit> gone = null;
		foreach (var u in _records.Keys)
			if (!IsManageable(u)) (gone ??= new List<Unit>()).Add(u);
		foreach (var u in _moves.Keys)
			if (!IsManageable(u) && (gone == null || !gone.Contains(u))) (gone ??= new List<Unit>()).Add(u);
		if (gone != null)
			foreach (var u in gone) OnUnitDied(u);
	}

	// 저장 위치가 건설·재설치와 겹치면 위치·방향은 유지하고 표시만 켠다. 다시 쓸 수 있으면 걷는다.
	void FlagReassignNeeds()
	{
		foreach (var record in _records.Values)
		{
			bool blocked = IsOccupiedByConstruction(record.Floor, record.Tile, record.Unit.FootprintSize);
			if (blocked == record.NeedsReassign) continue;
			record.NeedsReassign = blocked;
			LogHelper.Log(LogHelper.GAME, $"[배치] {record.Unit.unitType?.typeName} 방어 위치({record.Tile.x},{record.Tile.y}) {(blocked ? "가 건설 계획과 겹쳐 재지정이 필요합니다" : "를 다시 쓸 수 있습니다")}");
		}
	}

	// 최종 몸체가 건설 예약과 겹친 이동은 취소하고(중간 경로 겹침은 무관) 복원하지 않는다. 자동 복귀는 코어가 파괴된 층에서 중단한다.
	void CancelInvalidMoves()
	{
		List<Unit> cancel = null;
		foreach (var kv in _moves)
		{
			var move = kv.Value;
			bool overlaps = IsOccupiedByConstruction(move.Floor, move.Tile, kv.Key.FootprintSize);
			bool coreStopped = move.Source == DefenseMoveSource.AutoReturn && !BuildingManager.AreCoreFunctionsActive(move.Floor);
			if (overlaps || coreStopped) (cancel ??= new List<Unit>()).Add(kv.Key);
		}
		if (cancel == null) return;
		foreach (var u in cancel)
		{
			LogHelper.Log(LogHelper.GAME, $"[배치] {u.unitType?.typeName}의 이동을 취소합니다 — 최종 방어 타일이 건설 계획과 겹치거나 담당 코어가 파괴됐습니다");
			CancelMove(u, abortCommand: true);
		}
	}

	bool IsOccupiedByConstruction(int floor, Vector2Int tile, Vector2Int footprint)
	{
		PlacementBlock body = CheckBody(floor, tile, footprint, out _);
		return body == PlacementBlock.Facility || body == PlacementBlock.DoorOrStair || body == PlacementBlock.FrontZone;
	}
}
