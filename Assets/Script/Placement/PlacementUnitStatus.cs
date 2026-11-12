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
			// 1-7 표1 "진화 연출: 남은 시간 표시. 이동 적용 불가 사유 함께 표시"
			EvolutionSession evo = unit.activeEvolutionSession;
			int duration = Mathf.Max(1, evo.Definition.PresentationDurationSeconds);
			line.Activity = "진화 연출 중";
			line.Progress = DefensePlacementMath.EvolutionProgress01(evo.ElapsedSeconds, duration);
			line.Note = $"남은 {DefensePlacementMath.EvolutionRemainingSeconds(evo.ElapsedSeconds, duration)}초 — 이동 적용 불가(연출 중 새 명령을 받지 않습니다)";
		}
		else if (work != null)
		{
			line.Location += $" · {work.DisplayName}";
			line.Activity = WorkLabel(work, out line.Progress, out line.Note);
			if (unit.IsInsideFacility) line.Note = unit.insideExitRequested ? "시설 안 — 퇴장 대기 중" : "시설 안에서 작업 중 — 이동 적용 시 밖으로 나옵니다";
		}
		else if (unit.assignedDoorExpansion != null)
		{
			line.Location += " · 문";
			line.Activity = "문 확장";
			line.Progress = Mathf.Clamp01((float)unit.assignedDoorExpansion.Session.Progress01);
		}
		else if (unit.assignedCoreRestorePos.HasValue)
		{
			line.Location += " · 코어";
			line.Activity = "코어 수복";
			CoreRestoreSession restore = GameSession.Instance?.Core?.GetActiveRestoreAt(unit.assignedCoreRestorePos.Value);
			if (restore != null) line.Progress = Mathf.Clamp01((float)restore.Progress01);
			else line.Note = "수복 작업이 아직 시작되지 않았습니다";
		}
		else if (unit.carriedDismantledBuilding != null)
		{
			line.Activity = $"분해한 {unit.carriedDismantledBuilding.DisplayName} 운반·재설치";
		}
		else if (unit.carriedCorpse != null)
		{
			line.Activity = "시체 운반";
		}
		else if (unit.playerMoveTarget.HasValue && unit.isManualMoveCommand && unit.isDefenseAutoReturn)
		{
			// 자동 방어 복귀는 전투·경계에 밀린다 — 걷고 있으면 복귀 중, 전투·경계 중이면 끝난 뒤 이어 간다고 알린다(명세 1-6 표1).
			bool walking = unit.fsm?.CurrentState is DefenseReturnFSMState;
			line.Activity = walking ? "방어 복귀 중" : (unit.fsm?.CurrentState?.GetLabel(unit) ?? "대기");
			if (!walking) line.Note = "방어 복귀는 전투·경계(전투 후 10초)가 끝나면 이어집니다";
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
				if (b.Kind == FacilityKind.LifeEnergy)
				{
					// 반복 작업은 끝나는 시각이 아니라 다음 산출까지의 게이지를 보여 준다(1-7 표1).
					progress = Mathf.Clamp01((float)b.LifeEnergyCycle.Progress01);
					note = "반복 작업 — 다음 산출까지";
					return "생명 에너지 채취";
				}
				if (b.Kind == FacilityKind.Research)
				{
					FacilityResearchEntry head = b.ResearchQueue != null && b.ResearchQueue.Entries.Count > 0 ? b.ResearchQueue.Entries[0] : null;
					if (head?.Definition != null && ResourceManager.Instance != null)
						progress = Mathf.Clamp01((float)ResourceManager.Instance.ResearchLedger.GetProgress01(head.Definition.ResearchId));
					note = head?.Definition != null ? $"{head.Definition.DisplayName} — 대기열이 모두 끝나야 작업이 종료됩니다" : "대기열이 비어 있습니다";
					return "연구";
				}
				return "시설 작업";
		}
	}
}
