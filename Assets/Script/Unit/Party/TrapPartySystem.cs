using UnityEngine;
using Haare.Util.Logger;

// 03문서 9장 구현부 — 함정 발견 시 발견자 혼자 처리하지 않고, 파티 중 함정과 가장 가까운 1명을 실제
// 담당(선정 유닛)으로 뽑는다(2026-09-04: 해제 성공률 비교 대신 거리 단일 기준으로 단순화).
// 나머지 파티원은 정보만 기록한 채 기존 행동을 유지한다.
// 03번 v0.12 8장(순서도 03-12/03-12-2): 담당자는 해제 위치까지의 도착 예정 시점을 계산해 조율 기록에 남기고,
// 발견 유닛(대기자)은 전파 조건을 충족할 때만 그 정보를 받아 대기 기한(도착 예정 시점+3초)을 갱신한다.
public static class TrapPartySystem
{
	// 대기자가 조율 기록의 새 보고를 다시 읽어 보는 간격(전파 조건 실패 시) — 매 프레임 범위 판정을 돌리지 않는다.
	private const float ReportPollIntervalSeconds = 0.25f;
	// 담당자가 도착 예정을 다시 계산하는 간격 — 경로 탐색(A*) 비용을 줄이는 성능 장치(허용오차보다 길지 않게 둔다).
	private const float ProgressCheckIntervalSeconds = 0.5f;

	private static float GraceSeconds
		=> AIConfigLoader.Behavior?.trapSelectedUnitLateGraceSeconds ?? ExplorationMath.TrapSelectedUnitLateGraceSeconds;

	private static float ChangeToleranceSeconds
		=> AIConfigLoader.Behavior?.trapArrivalChangeToleranceSeconds ?? ExplorationMath.TrapArrivalChangeToleranceSeconds;

	// ─────────────────────────── 9-1~9-3장: 함정 최초 발견 ───────────────────────────
	public static void OnTrapDiscovered(Human discoverer, InteractableObject trapObj)
	{
		var party = discoverer.party;
		if (party == null)
		{
			// 파티 없는 단독 유닛(테스트 등) — 예전처럼 발견 즉시 자기 자신이 처리.
			if (discoverer.currentTrapInteraction == null)
				discoverer.currentTrapInteraction = new TrapInteractionState { TrapObjectId = trapObj.Id, TrapPosition = trapObj.Position };
			return;
		}
		if (party.TrapCoordinations.ContainsKey(trapObj.Id)) return; // 이미 다른 파티원이 먼저 발견해 처리 중

		var coord = new TrapPartyCoordination
		{
			TrapObjectId = trapObj.Id,
			TrapPosition = trapObj.Position,
			DiscovererName = discoverer.name,
		};
		party.TrapCoordinations[trapObj.Id] = coord;

		// 9-3장 전파는 07문서 6장 일반 전파 조건(비전투+같은 공간+전파 범위)을 따른다 — 발견자는 항상
		// 알고, 조건을 만족하는 파티원만 함께 안다(비선정 유닛이 위치를 알아야 9-6장 "인접 1칸 회피"가 성립).
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0) continue;
			bool receivesInfo = m == discoverer || PropagationSystem.CanPropagate(discoverer, m);
			if (!receivesInfo) continue;

			if (!m.personalMap.IsObjectKnown(trapObj.Id))
				m.personalMap.RegisterObject(trapObj.Id, trapObj.Position, trapObj.BaseDanger, trapObj.BaseInterest, trapObj.Tags, trapObj.CauserStage);

			// 9-6장: 인접 1칸에 있던 비발견 유닛은 즉시 안전 타일로 물러난다(발견자/해제 담당은 그
			// 자리로 가야 하므로 제외).
			if (m == discoverer) continue;
			Vector2Int trapPos2D = new Vector2Int(trapObj.Position.x, trapObj.Position.y);
			if (m.currentTrapInteraction == null && Mathf.Max(Mathf.Abs(m.position.x - trapPos2D.x), Mathf.Abs(m.position.y - trapPos2D.y)) <= 1)
			{
				StepAwayFromTrap(m, trapPos2D);
			}
		}

		// 9-3장: 발견 유닛이 파티 내에서 함정과 가장 가까우면 2초 응답 없이 즉시 해제 담당으로 확정한다.
		// 2026-09-04(사용자 요청): 선정 기준을 해제 성공률 비교에서 "가장 가까운 1명"으로 단순화.
		if (IsClosestPartyMember(discoverer, party, trapObj.Position))
		{
			coord.SelectedUnitName = discoverer.name;
			coord.SelectionLocked = true;
			discoverer.currentTrapInteraction = new TrapInteractionState
			{
				TrapObjectId = trapObj.Id,
				TrapPosition = trapObj.Position,
				JoinWaitElapsed = true,
				AutoConfirmed = true,
				IsSelectedDisarmer = true,
				SelectedUnitName = discoverer.name,
			};
		}
		else
		{
			discoverer.currentTrapInteraction = new TrapInteractionState { TrapObjectId = trapObj.Id, TrapPosition = trapObj.Position };
		}
	}

	// 07문서 9장 재전파 — 최초 발견 시 전파 범위 밖이었던 파티원도 나중에 범위 안으로 들어오면 함정
	// 정보를 받는다(PartyDeathSystem.TickOngoingPropagation과 동일 패턴).
	public static void TickOngoingPropagation(Human human)
	{
		var party = human.party;
		if (party == null || party.TrapCoordinations.Count == 0 || human.Session == null) return;

		foreach (var coord in party.TrapCoordinations.Values)
		{
			if (human.personalMap.IsObjectKnown(coord.TrapObjectId)) continue;

			foreach (var carrier in party.Members)
			{
				if (carrier == null || carrier == human || carrier.hp <= 0) continue;
				if (!carrier.personalMap.IsObjectKnown(coord.TrapObjectId)) continue;
				if (!PropagationSystem.CanPropagate(carrier, human)) continue;
				if (!human.Session.objectGrid.TryGetValue(coord.TrapPosition, out var trapObj)) break;

				human.personalMap.RegisterObject(trapObj.Id, trapObj.Position, trapObj.BaseDanger, trapObj.BaseInterest, trapObj.Tags, trapObj.CauserStage);
				break;
			}
		}
	}

	private static void StepAwayFromTrap(Human unit, Vector2Int trapPos2D)
	{
		Vector2Int away = unit.position - trapPos2D;
		if (away == Vector2Int.zero) away = new Vector2Int(1, 0);
		Vector2Int stepTarget = unit.position + new Vector2Int(System.Math.Sign(away.x), System.Math.Sign(away.y));
		AIMovementHelper.MoveTowardsPos(unit, stepTarget);
	}

	// ─────────────────────────── 9-2장: 최근접 유닛 판정 ───────────────────────────
	// 2026-09-04(사용자 요청): 해제 시도자 선정을 성공률 비교 대신 "가장 가까운 1명"으로 단순화.
	private static bool IsClosestPartyMember(Human discoverer, Party party, Vector3Int trapPos)
	{
		Vector2Int trapPos2D = new Vector2Int(trapPos.x, trapPos.y);
		float discovererDist = Vector2Int.Distance(discoverer.position, trapPos2D);
		foreach (var m in party.Members)
		{
			if (m == null || m == discoverer || m.hp <= 0) continue;
			if (Vector2Int.Distance(m.position, trapPos2D) < discovererDist) return false;
		}
		return true;
	}

	// ─────────────────────────── 9-2~9-4장: 2초 경과/기록함정 확정 시점의 실제 선정 ───────────────────────────
	// UnitFunction.OnUpdate가 discoverer의 trap.JoinWaitElapsed가 false→true로 바뀌는 바로 그 프레임에
	// (AutoConfirmed가 아닌 경우에만) 호출한다.
	public static void ResolveSelection(Human discoverer, TrapInteractionState trap)
	{
		var party = discoverer.party;
		if (party == null) { trap.IsSelectedDisarmer = true; return; }
		if (!party.TrapCoordinations.TryGetValue(trap.TrapObjectId, out var coord)) { trap.IsSelectedDisarmer = true; return; }
		if (coord.SelectionLocked)
		{
			trap.SelectedUnitName = coord.SelectedUnitName;
			trap.IsSelectedDisarmer = coord.SelectedUnitName == discoverer.name;
			if (!trap.IsSelectedDisarmer)
				BeginWaitingFor(discoverer, trap, coord, party.Members.Find(m => m != null && m.name == coord.SelectedUnitName));
			return;
		}

		// 9-2장: 전파 범위(07문서 6장, PropagationSystem.CanPropagate) 안의 모든 유닛 중 함정 대응으로
		// 전환 가능한 유닛만 대상으로 가장 가까운(해제 위치까지 예상 도착시간이 짧은) 1명을 선정한다.
		// 기한 안에 도착하지 못해 제외된 유닛은 후보가 아니다.
		Human best = discoverer;
		float bestEta = discoverer.EstimateRemainingSecondsToInteract(trap.TrapPosition);

		foreach (var m in party.Members)
		{
			if (m == null || m == discoverer || m.hp <= 0) continue;
			if (coord.ExcludedUnitNames.Contains(m.name)) continue;
			if (!PropagationSystem.CanPropagate(discoverer, m)) continue;
			if (!CanSwitchToTrapResponse(m)) continue;

			float eta = m.EstimateRemainingSecondsToInteract(trap.TrapPosition);
			if (eta < bestEta)
			{
				best = m; bestEta = eta;
			}
		}

		coord.SelectedUnitName = best.name;
		coord.SelectionLocked = true;
		trap.SelectedUnitName = best.name;
		trap.IsSelectedDisarmer = best == discoverer;
		if (best == discoverer) return;

		if (best.currentTrapInteraction == null)
			best.currentTrapInteraction = new TrapInteractionState { TrapObjectId = trap.TrapObjectId, TrapPosition = trap.TrapPosition };
		best.currentTrapInteraction.JoinWaitElapsed = true;
		best.currentTrapInteraction.IsSelectedDisarmer = true;
		best.currentTrapInteraction.SelectedUnitName = best.name;

		BeginWaitingFor(discoverer, trap, coord, best);
	}

	private static bool CanSwitchToTrapResponse(Human m)
	{
		if (m.currentTrapInteraction != null) return false; // 이미 다른 함정 대응 중
		if (m.currentInvestigation != null) return false;
		if (m.currentFormation != null) return false;
		if (m.personalSpottedEnemies.Count > 0) return false; // 전투 중 근사(명시적 전투 상태 플래그 부재)
		return true;
	}

	// ─────────────────────────── 03번 v0.12 8장: 도착 예정 시점의 계산·전달·기한 ───────────────────────────

	// 순서도 03-12 "해제 의사·예상 도착시간·계산 시점 전달": 선정된 담당자의 첫 추정을 조율 기록에 남기고 대기자에게
	// 즉시 적용한다. 선정은 전파 조건을 충족한 유닛만 대상이라 이 시점 전달은 항상 성립한다.
	private static void BeginWaitingFor(Human waiter, TrapInteractionState trap, TrapPartyCoordination coord, Human assignee)
	{
		float now = Time.time;
		float remaining = assignee != null ? assignee.EstimateRemainingSecondsToInteract(trap.TrapPosition) : 0f;
		PostReport(coord, coord.SelectedUnitName, TrapAssigneeStatus.EnRoute, now, remaining);
		ApplyEstimate(trap, now, remaining);
		trap.LastSeenReportSequence = coord.Report.Sequence;
		LogHelper.Log(LogHelper.GAME, $"[함정] {waiter.name}: 담당 {coord.SelectedUnitName}의 도착 예정 {remaining:F1}초 후, 대기 기한 {trap.WaitDeadline - now:F1}초 후");
	}

	private static void PostReport(TrapPartyCoordination coord, string senderName, TrapAssigneeStatus status, float calcTime, float remainingSeconds)
	{
		coord.Report = new TrapAssigneeReport
		{
			SenderName = senderName,
			Status = status,
			CalcTime = calcTime,
			RemainingSeconds = remainingSeconds,
			Sequence = (coord.Report?.Sequence ?? 0) + 1,
		};
	}

	private static void ApplyEstimate(TrapInteractionState trap, float calcTime, float remainingSeconds)
	{
		trap.HasAppliedEstimate = true;
		trap.AppliedCalcTime = calcTime;
		trap.AppliedArrivalTime = ExplorationMath.TrapArrivalTime(calcTime, remainingSeconds);
		trap.WaitDeadline = ExplorationMath.TrapWaitDeadline(calcTime, remainingSeconds, GraceSeconds);
	}

	// 현재 담당이고, 따로 기다리는 발견 유닛(대기자)이 있을 때만 보고 대상이 된다 — 발견자 본인이 담당이면 기다리는
	// 사람이 없다.
	private static bool TryGetReportContext(Human unit, TrapInteractionState trap, out TrapPartyCoordination coord)
	{
		coord = null;
		var party = unit.party;
		if (party == null || !party.TrapCoordinations.TryGetValue(trap.TrapObjectId, out coord)) return false;
		return coord.SelectedUnitName == unit.name && coord.DiscovererName != unit.name;
	}

	// 순서도 03-12 "새 경로·지형·속도 정보로 예상시간이 달라졌는가?": 해제 위치로 이동하는 동안(MoveToTrap, 행동
	// 틱마다) 남은 이동시간을 다시 계산하고, 도착 예정 시점이 마지막으로 알린 값과 크게 달라졌을 때만 새로 알린다.
	// 틱 시점에만 계산하므로 정상 이동 중엔 계산 시점+남은 시간(=도착 예정)이 일정하다.
	public static void ReportProgress(Human assignee, TrapInteractionState trap)
	{
		if (!TryGetReportContext(assignee, trap, out var coord)) return;

		float now = Time.time;
		if (now < trap.NextProgressCheckTime) return;
		trap.NextProgressCheckTime = now + ProgressCheckIntervalSeconds;

		float remaining = assignee.EstimateRemainingSecondsToInteract(trap.TrapPosition);
		var last = coord.Report;
		bool hasLast = last != null && last.SenderName == assignee.name && last.Status == TrapAssigneeStatus.EnRoute;
		if (hasLast && !ExplorationMath.HasArrivalEstimateChanged(last.ArrivalTime, ExplorationMath.TrapArrivalTime(now, remaining), ChangeToleranceSeconds))
			return; // 기존 시간으로 이동 계속

		PostReport(coord, assignee.name, TrapAssigneeStatus.EnRoute, now, remaining);
		LogHelper.Log(LogHelper.GAME, $"[함정] {assignee.name}: 도착 예정 시점 변경 통지(남은 {remaining:F1}초)");
	}

	// 해제 위치(함정 인접 1칸)에 도달하면 한 번 알린다 — 대기자는 이 통지로 기다림을 끝낸다.
	public static void ReportArrived(Human assignee, TrapInteractionState trap)
	{
		if (!TryGetReportContext(assignee, trap, out var coord)) return;
		var last = coord.Report;
		if (last != null && last.SenderName == assignee.name && last.Status == TrapAssigneeStatus.Arrived) return;

		PostReport(coord, assignee.name, TrapAssigneeStatus.Arrived, Time.time, 0f);
		LogHelper.Log(LogHelper.GAME, $"[함정] {assignee.name}: 해제 위치 도착 통지");
	}

	// 재선정으로 담당이 바뀐 뒤 이전 담당자가 뒤늦게 도착하면 양보한다(같은 함정에 이중 담당 방지). 파티나 조율
	// 기록이 없는 단독 유닛은 항상 담당이다.
	public static bool IsStillAssigned(Human unit, TrapInteractionState trap)
	{
		var party = unit.party;
		if (party == null || !party.TrapCoordinations.TryGetValue(trap.TrapObjectId, out var coord)) return true;
		return coord.SelectedUnitName == unit.name;
	}

	// ─────────────────────────── 순서도 03-12-2: 담당자를 기다리는 기한 ───────────────────────────
	// UnitFunction.OnUpdate가 선정되지 않은 발견 유닛(대기자)에게 매 프레임 호출한다. 담당자 보고는 전파 조건을
	// 충족할 때만 읽고(범위 밖이면 못 받은 것 — 기한을 늘리지 않는다), 담당자의 도착·종료를 통지로 받거나 시야로
	// 직접 확인하면 기다림이 끝나며, 기한이 지나면 담당 재선정·우회·파괴·다른 목표를 다시 판단한다.
	public static void TickWaitingForSelectedUnit(Human waiter, TrapInteractionState trap)
	{
		if (trap.SelectedUnitName == null || trap.IsSelectedDisarmer || trap.AssigneeDone) return;

		float now = Time.time;
		TrapPartyCoordination coord = null;
		if (waiter.party != null && waiter.party.TrapCoordinations.TryGetValue(trap.TrapObjectId, out coord))
			TryReceiveReport(waiter, trap, coord, now);

		// "담당자가 실제로 도착했는가?": 전파 범위(기본 3칸)보다 멀리서 함정을 발견한 대기자도 담당자가 시야에 들어와
		// 해제 위치에 서 있으면 직접 확인한다.
		if (!trap.AssigneeDone && SeesAssigneeAtTrap(waiter, trap))
		{
			trap.AssigneeDone = true;
			LogHelper.Log(LogHelper.GAME, $"[함정] {waiter.name}: 담당 {trap.SelectedUnitName}의 도착을 직접 확인 — 대기 종료");
		}

		if (trap.AssigneeDone) return;
		if (now >= trap.WaitDeadline) OnWaitDeadlineExpired(waiter, trap, coord);
	}

	// 담당자가 이번 시야 패스에서 보였고 함정 인접 1칸(체비셰프)에 있는가 — MoveToTrap의 도착 판정과 같은 기준이다.
	private static bool SeesAssigneeAtTrap(Human waiter, TrapInteractionState trap)
	{
		Vector2Int trapPos = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		foreach (var member in waiter.visiblePartyMembers)
		{
			if (member != null && member.name == trap.SelectedUnitName && AIMovementHelper.IsAdjacent(member.position, trapPos))
				return true;
		}
		return false;
	}

	private static void TryReceiveReport(Human waiter, TrapInteractionState trap, TrapPartyCoordination coord, float now)
	{
		var report = coord.Report;
		if (report == null || report.Sequence == trap.LastSeenReportSequence || report.SenderName != trap.SelectedUnitName) return;
		if (now < trap.NextReportPollTime) return;

		Human sender = waiter.party.Members.Find(m => m != null && m.name == report.SenderName);
		if (sender == null || !PropagationSystem.CanPropagate(sender, waiter))
		{
			trap.NextReportPollTime = now + ReportPollIntervalSeconds;
			return;
		}

		trap.LastSeenReportSequence = report.Sequence;
		if (report.Status != TrapAssigneeStatus.EnRoute)
		{
			// 도착 또는 도착 전 대응 종료 — 이후 해제 절차는 담당자 몫이라 기다림은 여기서 끝난다.
			trap.AssigneeDone = true;
			LogHelper.Log(LogHelper.GAME, $"[함정] {waiter.name}: 담당 {report.SenderName} {(report.Status == TrapAssigneeStatus.Arrived ? "도착" : "대응 종료")} 통지 수신 — 대기 종료");
			return;
		}

		if (ExplorationMath.ShouldApplyArrivalEstimate(trap.HasAppliedEstimate, trap.AppliedCalcTime, trap.AppliedArrivalTime, report.CalcTime, report.ArrivalTime))
		{
			ApplyEstimate(trap, report.CalcTime, report.RemainingSeconds);
			LogHelper.Log(LogHelper.GAME, $"[함정] {waiter.name}: 도착 예정 갱신 수신(계산 {report.CalcTime:F1}s, 남은 {report.RemainingSeconds:F1}초) → 대기 기한 {trap.WaitDeadline:F1}s");
		}
	}

	// "대기 기한이 지났는가?" 예 → 담당 재선정·우회·파괴·다른 목표 판단. 미도착만으로 사망·도주를 확정하지 않고,
	// 미도착 담당자는 재선정 후보에서만 뺀다.
	private static void OnWaitDeadlineExpired(Human waiter, TrapInteractionState trap, TrapPartyCoordination coord)
	{
		if (coord != null && !string.IsNullOrEmpty(trap.SelectedUnitName) && !coord.ExcludedUnitNames.Contains(trap.SelectedUnitName))
			coord.ExcludedUnitNames.Add(trap.SelectedUnitName);
		LogHelper.Log(LogHelper.GAME, $"[함정] {waiter.name}: 담당 {trap.SelectedUnitName}의 도착 기한이 지나 담당을 다시 정합니다");
		RestartSelection(waiter, trap);
	}

	// 선정을 초기화해 표준 선정 절차(2초 응답 대기 → ResolveSelection)부터 다시 시작한다. 재선정 후보에서 뺀 유닛은
	// coord.ExcludedUnitNames가 기억한다.
	private static void RestartSelection(Human discoverer, TrapInteractionState trap)
	{
		if (discoverer.party != null && discoverer.party.TrapCoordinations.TryGetValue(trap.TrapObjectId, out var coord))
			ReleaseAssignment(coord);
		trap.IsSelectedDisarmer = false;
		trap.SelectedUnitName = null;
		trap.JoinWaitElapsed = false;
		trap.JoinWaitTimer = 0f;
		trap.HasAppliedEstimate = false;
		trap.WaitDeadline = float.PositiveInfinity;
		trap.AssigneeDone = false;
		trap.LastSeenReportSequence = 0;
		trap.NextReportPollTime = 0f;
	}

	// 조율 기록은 남기고(정보 재전파의 기준) 담당 배정과 마지막 보고만 지운다.
	private static void ReleaseAssignment(TrapPartyCoordination coord)
	{
		coord.SelectedUnitName = null;
		coord.SelectionLocked = false;
		coord.Report = null;
	}

	// ─────────────────────────── 함정 대응 종료·집결 전환 ───────────────────────────

	// currentTrapInteraction을 끝내는 유일한 지점. 현재 담당자가 대응을 마치면(따로 기다리는 발견 유닛이 있을 때)
	// 그 사실을 조율 기록에 남기고, 집결로 접으면 담당 배정만 푼다. 양보·대기 종료는 조율 기록을 건드리지 않는다.
	public static void EndResponse(Unit unit, TrapEndReason reason)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null) return;

		if (unit is Human human && human.party != null
			&& human.party.TrapCoordinations.TryGetValue(trap.TrapObjectId, out var coord)
			&& coord.SelectedUnitName == human.name)
		{
			if (reason == TrapEndReason.Rally)
				ReleaseAssignment(coord);
			else if (coord.DiscovererName != human.name)
				PostReport(coord, human.name, TrapAssigneeStatus.Concluded, Time.time, 0f);
		}
		unit.currentTrapInteraction = null;
	}

	// 03번 0장·8장, 순서도 05-03: 집결 명령을 받으면 아직 시작하지 않은 함정 대응(응답 대기·담당자 도착 대기·
	// 해제하러 가는 이동)은 끝내고 집결한다. 이미 해제·파괴를 시작했으면 기존 중단 조건으로 완료한 뒤 합류한다.
	public static void ReleaseForRally(Human human)
	{
		var trap = human.currentTrapInteraction;
		if (trap == null || trap.Phase != TrapPhase.AwaitingJoin) return;

		LogHelper.Log(LogHelper.GAME, $"[함정] {human.name}: 집결 명령으로 함정 대응을 접고 집결합니다");
		EndResponse(human, TrapEndReason.Rally);
	}
}
