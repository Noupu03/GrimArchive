using UnityEngine;

// 인원 목록 한 줄의 표시 값.
public struct UnitStatusLine
{
	public string Name;
	public string Location;
	public string Activity;
	public float? Progress;
	public string Note;
	public string Failure;
}

public static class PlacementUnitStatus
{
	public static UnitStatusLine Describe(Unit unit, DefenseRecord record, PlacementBlock? lastFailure)
	{
		var line = new UnitStatusLine
		{
			Name = unit.unitType != null ? unit.unitType.typeName : unit.name,
			Location = $"{unit.currentFloor}층 " + (unit.currentRoom != null ? unit.currentRoom.RoomName : "통로"),
		};

		BuildingData work = unit.assignedWorkBuilding;
		if (unit.IsInEvolutionPresentation)
		{
			line.Activity = "진화 연출 중";
			line.Note = "이동 적용 불가 — 연출 중 새 명령을 받지 않습니다";
		}
		else if (work != null)
		{
			line.Location += $" · {work.DisplayName}";
			line.Activity = WorkLabel(work, out line.Progress, out line.Note);
		}
		else if (unit.playerMoveTarget.HasValue && unit.isManualMoveCommand)
		{
			line.Activity = "이동 중";
		}
		else
		{
			string label = unit.fsm?.CurrentState?.GetLabel(unit);
			line.Activity = string.IsNullOrEmpty(label) ? "대기" : label;
			if (unit.fsm?.CurrentState is CombatFSMState) line.Note = "전투 중 — 끝나는 시각을 알 수 없습니다";
		}

		if (lastFailure.HasValue) line.Failure = lastFailure.Value.ToKorean();
		else if (record != null && record.NeedsReassign) line.Failure = PlacementBlock.NeedsReassign.ToKorean();
		return line;
	}

	static string WorkLabel(BuildingData b, out float? progress, out string note)
	{
		progress = null;
		note = null;
		switch (b.ActiveFieldTask)
		{
			case FacilityFieldTaskKind.Construction: progress = (float?)b.ActiveConstructionOrUpgrade?.Progress01; return "건설";
			case FacilityFieldTaskKind.Upgrade: progress = (float?)b.ActiveConstructionOrUpgrade?.Progress01; return "업그레이드";
			case FacilityFieldTaskKind.Repair: progress = (float?)b.ActiveRepair?.Progress01; return "수리";
			case FacilityFieldTaskKind.Demolish:
				if (b.DemolishRequiredSeconds > 0) progress = Mathf.Clamp01((float)(b.DemolishCompletedSeconds / b.DemolishRequiredSeconds));
				return "철거";
			case FacilityFieldTaskKind.Relocation: return "이동용 분해";
			default:
				if (b.Kind == FacilityKind.LifeEnergy) { note = "반복 작업 — 다음 산출까지"; return "생명 에너지 채취"; }
				if (b.Kind == FacilityKind.Research) { note = "연구 대기열이 끝나야 작업이 종료됩니다"; return "연구"; }
				return "시설 작업";
		}
	}
}
