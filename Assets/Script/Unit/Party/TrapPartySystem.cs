using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 03문서 9장 구현부 — 함정 발견 시 파티 중 예상 해제 성공률이 가장 높은 1명(동률이면 해제 위치 도착시간이 짧은 유닛)을 담당으로 뽑고(03번 8장), 나머지는 정보만 기록한 채 기존 행동을 유지한다.
// 담당자는 도착 예정 시점을 조율 기록에 남기고, 발견 유닛(대기자)은 전파 조건을 충족할 때만 받아 대기 기한(도착 예정 + 3초)을 갱신한다.
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
		// 집결·공동 이동·귀환 중에는 다른 임무 대상을 위한 해제 담당을 맡지 않는다(03번 8장) — 그 이동 경로를 함정이 막을 때만 절차를 적용한다. 보류해도 정보 전파·인접 1칸 회피는 진행하고 대응(응답 대기·선정)만 시작하지 않는다.
		bool defer = IsCommittedToPartyMovement(discoverer) && !DoesTrapBlockPartyMovement(discoverer, trapObj.Position);

		if (party.TrapCoordinations.TryGetValue(trapObj.Id, out var coord))
		{
			// 이미 다른 파티원이 먼저 발견해 처리 중이거나, 보류된 함정을 또 집결·이동 중인 유닛이 본 경우는 할 일이 없다.
			if (!coord.Deferred || defer) return;
			// 보류돼 있던 함정 — 집결·이동 중이 아닌 유닛이 처음 발견했으니 이 유닛이 표준 절차를 시작한다.
			coord.Deferred = false;
			coord.DiscovererName = discoverer.name;
		}
		else
		{
			coord = new TrapPartyCoordination
			{
				TrapObjectId = trapObj.Id,
				TrapPosition = trapObj.Position,
				DiscovererName = discoverer.name,
				Deferred = defer,
			};
			party.TrapCoordinations[trapObj.Id] = coord;
		}

		// 9-3장 전파는 07문서 6장 일반 전파 조건(비전투+같은 공간+전파 범위)을 따른다 — 발견자는 항상
		// 알고, 조건을 만족하는 파티원만 함께 안다(비선정 유닛이 위치를 알아야 9-6장 "인접 1칸 회피"가 성립).
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0) continue;
			bool receivesInfo = m == discoverer || PropagationSystem.CanPropagate(discoverer, m);
			if (!receivesInfo) continue;

			// 인접 1칸 회피(9-6장)는 이동 계층이 맡는다 — 등록한 함정이 알려진 활성 함정 구역(TrapAvoidance)이 돼 일반 이동이 들어가지 않고, 이미 구역 안이면 TacticalFSMState.ZoneEscape가 내보낸다(늦게 정보를 받은 유닛 포함).
			if (!m.personalMap.IsObjectKnown(trapObj.Id))
				m.personalMap.RegisterObject(trapObj.Id, trapObj.Position, trapObj.BaseDanger, trapObj.BaseInterest, trapObj.Tags, trapObj.CauserStage);
		}

		if (defer)
		{
			LogHelper.Log(LogHelper.GAME, $"[함정] {discoverer.name}: 집결·이동 중이라 함정 대응을 보류합니다(기록만)");
			return;
		}

		BeginDiscovererResponse(discoverer, trapObj, party, coord);
	}

	// 발견자로서 표준 대응 절차를 시작한다 — 최초 발견(OnTrapDiscovered)과 "알던 함정이 이동 경로를 막음"(OnPathBlockedByKnownTrap)이 공유한다.
	private static void BeginDiscovererResponse(Human discoverer, InteractableObject trapObj, Party party, TrapPartyCoordination coord)
	{
		// 웨이브 진입 전 파티 내 최고 성공률로 확인된 유닛이 직접 발견했으면 2초 응답 없이 즉시 담당으로 확정한다(9-3장) — 근처 유닛 중 최고라는 이유만으로 생략하지 않는다. 해제하지 않을 유닛(기록 함정·성공률 50% 이하)이면 확정하지 않는다.
		if (party.EntryBestDisarmerNames.Contains(discoverer.name) && WouldAttemptDisarm(discoverer, trapObj.Id))
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

	// ─────────────────────────── 알던 함정이 필요한 이동을 막을 때 ───────────────────────────
	// 이동 계층은 알려진 함정 인접 1칸에 안 들어가므로 그 구역이 유일한 통로면 이동이 막힌다. 이미 아는 함정은 최초 발견 이벤트가 없어 아무도 대응을 안 열 수 있으니(03번 9장), 막힌 유닛이 표준 절차(우회·해제·통과·파괴)를 다시 연다.
	private const float BlockedSignalFreshSeconds = 1f;
	// 조율 기록별 재시작 간격 — 해제·파괴가 모두 불가능한 함정에서 대응이 끝나자마자 다시 열리는 무한 반복을 막는다.
	private const float BlockedRetryCooldownSeconds = 10f;

	// UnitFunction.OnUpdate의 0.1초 틱이 호출한다 — A*가 남긴 막힘 신호(Unit.trapBlockTrapId)를 소비하며, 전투 중이면 CombatFSMState가 전투 중 파괴 판단에 쓴다.
	public static void TickBlockedPathResponse(Human human)
	{
		string trapId = human.trapBlockTrapId;
		if (string.IsNullOrEmpty(trapId)) return;
		human.trapBlockTrapId = null; // 신호는 한 번만 소비한다

		if (Time.time - human.trapBlockSignalTime > BlockedSignalFreshSeconds) return;
		if (Time.time < human.trapBlockResponseCooldownUntil) return; // 파티가 없는 단독 유닛도 무한 재시작하지 않게 유닛 단위 쿨다운도 둔다
		if (human.currentTrapInteraction != null || human.Session == null) return;
		if (human.CurrentFsmStateIfCreated is CombatFSMState) return;
		if (!human.personalMap.KnownTrapTiles.TryGetValue(trapId, out Vector3Int tile)) return;
		if (!human.Session.objectGrid.TryGetValue(tile, out InteractableObject trapObj) || trapObj.Id != trapId || trapObj.IsCollected) return;

		OnPathBlockedByKnownTrap(human, trapObj);
		if (human.currentTrapInteraction != null) human.trapBlockResponseCooldownUntil = Time.time + BlockedRetryCooldownSeconds;
	}

	public static void OnPathBlockedByKnownTrap(Human human, InteractableObject trapObj)
	{
		if (human.currentTrapInteraction != null) return;

		var party = human.party;
		if (party == null)
		{
			human.currentTrapInteraction = new TrapInteractionState { TrapObjectId = trapObj.Id, TrapPosition = trapObj.Position };
			return;
		}

		if (party.TrapCoordinations.TryGetValue(trapObj.Id, out var coord))
		{
			if (IsBeingHandled(party, trapObj.Id)) return; // 다른 파티원이 이미 표준 절차를 진행 중 — 막힌 유닛은 기존 stuck 탈출로 기다린다
			if (Time.time < coord.NextBlockedRetryTime) return;
			ReleaseAssignment(coord);
			coord.Deferred = false;
			coord.DiscovererName = human.name;
		}
		else
		{
			coord = new TrapPartyCoordination { TrapObjectId = trapObj.Id, TrapPosition = trapObj.Position, DiscovererName = human.name };
			party.TrapCoordinations[trapObj.Id] = coord;
		}

		coord.NextBlockedRetryTime = Time.time + BlockedRetryCooldownSeconds;
		LogHelper.Log(LogHelper.GAME, $"[함정] {human.name}: 알던 함정이 이동 경로를 막아 대응을 다시 시작합니다");
		BeginDiscovererResponse(human, trapObj, party, coord);
	}

	private static bool IsBeingHandled(Party party, string trapObjectId)
	{
		foreach (var m in party.Members)
			if (m != null && m.hp > 0 && m.currentTrapInteraction != null && m.currentTrapInteraction.TrapObjectId == trapObjectId) return true;
		return false;
	}

	// 이 유닛이 가려는 곳(IsRouteBlockedByTrap의 목적지) — 명령 이동 → 조사 목표 → 파티 이동(대기 위치·리더 보고·집결지) → 자유 탐색 목표 순이며, 없으면 null(막지 않음).
	public static Vector2Int? GetCurrentDestination(Human human)
	{
		if (human.playerMoveTarget.HasValue) return human.playerMoveTarget;
		if (human.currentInvestigation != null)
			return new Vector2Int(human.currentInvestigation.TargetPosition.x, human.currentInvestigation.TargetPosition.y);
		Vector2Int? partyDest = PartyMovementDestination(human);
		if (partyDest.HasValue) return partyDest;
		return human.currentExplorationTarget;
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

	// ─────────────────────────── 8장: 해제 시도 여부와 성공률 ───────────────────────────

	// 미기록 함정은 최초 1회 시도하고, 기록된 함정은 예상 성공률이 50%(trapRecordedDisarmThreshold)를 초과할 때만 해제한다(정확히 50%는 우회·파괴). TacticalFSMState.IsDisarmWorthy와 담당 후보 판정이 공유하는 단일 출처다.
	public static bool WouldAttemptDisarm(Human human, string trapObjectId)
	{
		if (!human.personalMap.IsTrapRecorded(trapObjectId)) return true;
		return human.personalMap.GetTrapExpectedSuccessRate(trapObjectId)
			> (AIConfigLoader.Behavior?.trapRecordedDisarmThreshold ?? ExplorationMath.TrapRecordedDirectDisarmThreshold) * 100f;
	}

	// 담당 선정에 쓰는 예상 해제 성공률(%) — 기록이 있으면 그 기록, 없으면 스탯 기반 공식(이해도 보정 0, 실제 해제 시도와 같은 호출).
	private static float ExpectedDisarmRate(Human human, string trapObjectId)
		=> human.personalMap.IsTrapRecorded(trapObjectId)
			? human.personalMap.GetTrapExpectedSuccessRate(trapObjectId)
			: ExplorationMath.TrapDisarmSuccessRate(human.concentration, human.level, understandingApplied: 0);

	// 웨이브 진입 전(파티 생성 시점) 스냅샷 — 스탯 기반 성공률이 가장 높은 유닛 이름들(동률이면 전원). SetupStats가 파생 스탯(concentration)을 이미 계산해 두며 GameSession.CreateParty가 호출한다.
	public static HashSet<string> SnapshotEntryBestDisarmers(IEnumerable<Human> members)
	{
		var best = new HashSet<string>();
		float bestRate = float.NegativeInfinity;
		foreach (var m in members)
		{
			if (m == null) continue;
			float rate = ExplorationMath.TrapDisarmSuccessRate(m.concentration, m.level, understandingApplied: 0);
			if (Mathf.Approximately(rate, bestRate))
			{
				best.Add(m.name);
			}
			else if (rate > bestRate)
			{
				best.Clear();
				best.Add(m.name);
				bestRate = rate;
			}
		}
		return best;
	}

	// ─────────────────────────── 8장: 집결·공동 이동·귀환 중에는 새 해제 담당을 맡지 않는다 ───────────────────────────
	// 조사 쪽 CanInvestigate가 쓰는 currentWait 사유 4종 + 집결지 도착 후 대기. 도착하면 currentWait이 비워지므로 Party.IsRallyActive를 따로 보되 명령을 못 받은 파티원은 집결지 반경 조건으로 거른다.
	private const float RallyArrivalRadius = 1.5f; // TacticalFSMState.ExecuteWait의 집결지 도착 판정과 같은 값

	public static bool IsCommittedToPartyMovement(Human human)
	{
		var wait = human.currentWait;
		if (wait != null && (wait.Reason == WaitReason.AwaitingPartyAtRallyPoint || wait.Reason == WaitReason.AdvancingToNextRoom
			|| wait.Reason == WaitReason.ReportingCoreToLeader || wait.Reason == WaitReason.SearchingNextDoor || wait.Reason == WaitReason.Retreating
			|| wait.Reason == WaitReason.FormingUpAtDoor || wait.Reason == WaitReason.BreachingDoor || wait.Reason == WaitReason.EnteringNextRoom))
			return true;

		var party = human.party;
		return party != null && party.IsRallyPointOnFloor(human.currentFloor)
			&& Vector2Int.Distance(human.position, party.RallyPoint.Value) <= RallyArrivalRadius;
	}

	// 집결·이동 중인 유닛이 가고 있는 목적지 — 함정이 그 경로를 막는지 판정하는 데 쓴다(코어 보고는 보고자가 지금 향하는 목적지, 그 외는 대기 위치·집결지).
	private static Vector2Int? PartyMovementDestination(Human human)
	{
		var wait = human.currentWait;
		var party = human.party;
		if (wait != null)
		{
			// 코어 보고는 실제 리더 위치가 아니라 보고자가 지금 향하는 목적지(아는 리더 위치·집결지·문·프론티어)를 쓴다.
			if (wait.Reason == WaitReason.ReportingCoreToLeader)
				return wait.ReportKind != ReportDestinationKind.None ? wait.ReportTarget : (Vector2Int?)null;
			if (wait.WaitPosition.HasValue) return wait.WaitFloor >= 0 && wait.WaitFloor != human.currentFloor ? null : wait.WaitPosition;
		}
		return party != null && party.IsRallyPointOnFloor(human.currentFloor) ? party.RallyPoint : null;
	}

	// 집결·이동 중인 유닛의 경로를 이 함정이 막는가(대체 경로 없음 — 예: 좁은 통로 한가운데의 함정). 목적지를 모르거나 지형이 없으면 "막지 않음".
	public static bool DoesTrapBlockPartyMovement(Human human, Vector3Int trapPos)
	{
		var dest = PartyMovementDestination(human);
		return dest.HasValue && TacticalFSMState.IsRouteBlockedByTrap(human, dest.Value, trapPos);
	}

	// ─────────────────────────── 9-2~9-3장: 2초 응답 대기가 끝난 시점의 실제 선정 ───────────────────────────
	// UnitFunction.OnUpdate가 JoinWaitElapsed가 false→true로 바뀌는 프레임에(AutoConfirmed가 아닐 때만) 호출한다.
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

		// 전파 범위(CanPropagate) 안에서 함정 대응으로 전환 가능하고 실제로 해제할 유닛 중 예상 성공률이 가장 높은 1명(동률이면 도착시간이 짧은 유닛)을 선정한다(9-2장).
		Human best = ChooseDisarmer(discoverer, party.Members, coord, trap.TrapObjectId, trap.TrapPosition,
			m => PropagationSystem.CanPropagate(discoverer, m));
		LogHelper.Log(LogHelper.GAME, $"[함정] {discoverer.name}: 담당 선정 {best.name} (예상 성공률 {ExpectedDisarmRate(best, trap.TrapObjectId):F0}%)");

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

	// 후보 확인 → 예상 성공률 최고 1명, 동률이면 도착시간이 짧은 유닛(03번 8장). 발견자도 해제할 유닛이면 후보 중 하나(동률·같은 도착시간이면 발견자 우선)이고, 해제할 후보가 없으면 발견자가 맡아 BT 체인(우회·통과·파괴)으로 판단한다. inRange는 전파 범위 조건(운영에선 CanPropagate)이며 테스트용으로 주입한다.
	public static Human ChooseDisarmer(Human discoverer, IEnumerable<Human> members, TrapPartyCoordination coord,
		string trapObjectId, Vector3Int trapPos, System.Func<Human, bool> inRange)
	{
		Human best = null;
		float bestRate = 0f, bestEta = 0f;
		if (WouldAttemptDisarm(discoverer, trapObjectId))
		{
			best = discoverer;
			bestRate = ExpectedDisarmRate(discoverer, trapObjectId);
			bestEta = discoverer.EstimateRemainingSecondsToInteract(trapPos);
		}

		foreach (var m in members)
		{
			if (m == null || m == discoverer || m.hp <= 0) continue;
			if (!inRange(m) || !IsDisarmCandidate(m, coord, trapObjectId, trapPos)) continue;

			float rate = ExpectedDisarmRate(m, trapObjectId);
			float eta = m.EstimateRemainingSecondsToInteract(trapPos);
			if (best == null || ExplorationMath.IsBetterTrapDisarmCandidate(rate, eta, bestRate, bestEta))
			{
				best = m; bestRate = rate; bestEta = eta;
			}
		}
		return best ?? discoverer;
	}

	// 담당 전환 가능한 후보 확인(유지해야 할 전투·조사·보호 등 제외, 해제하지 않을 유닛은 후보 아님). 발견자는 이미 응답 상태라 이 필터를 거치지 않는다.
	private static bool IsDisarmCandidate(Human m, TrapPartyCoordination coord, string trapObjectId, Vector3Int trapPos)
	{
		if (coord.ExcludedUnitNames.Contains(m.name)) return false; // 기한 안에 도착하지 못해 재선정에서 제외
		if (!m.personalMap.IsObjectKnown(trapObjectId)) return false; // 함정 정보를 받지 못한 유닛
		if (m.currentTrapInteraction != null) return false; // 이미 다른 함정 대응 중
		if (m.currentInvestigation != null) return false;
		if (m.currentFormation != null) return false;
		if (m.currentJoinCombatWait != null) return false; // 전투 합류 대기 중인 합류자(PropagationSystem.CanRespondToJoinRequest와 대칭)
		if (m.personalSpottedEnemies.Count > 0) return false; // 전투 중 근사(명시적 전투 상태 플래그 부재)
		// 집결·공동 이동·귀환 중에는 새 담당을 맡지 않는다 — 그 이동 경로를 함정이 막을 때만 예외.
		if (IsCommittedToPartyMovement(m) && !DoesTrapBlockPartyMovement(m, trapPos)) return false;
		return WouldAttemptDisarm(m, trapObjectId);
	}

	// ─────────────────────────── 03번 v0.12 8장: 도착 예정 시점의 계산·전달·기한 ───────────────────────────

	// 선정된 담당자의 첫 추정을 조율 기록에 남기고 대기자에게 즉시 적용한다 — 선정은 전파 조건을 충족한 유닛만 대상이라 이 시점 전달은 항상 성립한다.
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

	// 해제 위치로 이동하는 동안(MoveToTrap, 행동 틱마다) 남은 이동시간을 다시 계산하고 도착 예정 시점이 마지막으로 알린 값과 크게 달라졌을 때만 새로 알린다 — 틱 시점에만 계산하므로 정상 이동 중엔 (계산 시점+남은 시간)이 일정하다.
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

	// ─────────────────────────── 담당자를 기다리는 기한 ───────────────────────────
	// UnitFunction.OnUpdate가 선정되지 않은 발견 유닛에게 매 프레임 호출한다. 담당자 보고는 전파 조건을 충족할 때만 읽고(범위 밖이면 기한을 늘리지 않음), 도착·종료 통지나 시야 확인이 오면 기다림이 끝나며, 기한이 지나면 재선정·우회·파괴·다른 목표를 판단한다.
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

	// currentTrapInteraction을 끝내는 유일한 지점 — 현재 담당자가 대응을 마치면(기다리는 발견 유닛이 있을 때) 조율 기록에 남기고, 집결·웨이브 종료로 접으면 담당 배정만 푼다. 양보·대기 종료는 조율 기록을 건드리지 않는다.
	public static void EndResponse(Unit unit, TrapEndReason reason)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null) return;

		if (unit is Human human && human.party != null
			&& human.party.TrapCoordinations.TryGetValue(trap.TrapObjectId, out var coord)
			&& coord.SelectedUnitName == human.name)
		{
			if (reason == TrapEndReason.Rally || reason == TrapEndReason.WaveEnded)
				ReleaseAssignment(coord);
			else if (coord.DiscovererName != human.name)
				PostReport(coord, human.name, TrapAssigneeStatus.Concluded, Time.time, 0f);
		}
		unit.currentTrapInteraction = null;
	}

	// 집결 명령을 받으면 아직 시작하지 않은 함정 대응(응답 대기·담당자 대기·해제 이동)은 끝내고 집결한다(03번 0·8장). 이미 해제·파괴를 시작했으면 기존 중단 조건으로 완료한 뒤 합류한다.
	public static void ReleaseForRally(Human human)
	{
		var trap = human.currentTrapInteraction;
		if (trap == null || trap.Phase != TrapPhase.AwaitingJoin) return;

		LogHelper.Log(LogHelper.GAME, $"[함정] {human.name}: 집결 명령으로 함정 대응을 접고 집결합니다");
		EndResponse(human, TrapEndReason.Rally);
	}

	// ─────────────────────────── 해제 중단(9-9장) ───────────────────────────

	// 해제 진행도 50% 손실은 PenaltyActive일 때만 1회 적용하고 false로 내려 같은 중단이 이어지는 동안 중복 적용되지 않게 한다. 대응 상태와 남은 진행도는 유지돼 원인이 사라지면 재개된다.
	public static void ApplyDisarmInterruptPenalty(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null || !trap.PenaltyActive) return;
		trap.DisarmProgress01 *= (AIConfigLoader.Behavior?.trapDisarmInterruptLossRatio ?? ExplorationMath.TrapDisarmInterruptLossRatio);
		trap.PenaltyActive = false;
		if (trap.CachedProgressBar != null)
		{
			trap.CachedProgressBar.SetProgress(0f, false);
		}
		else
		{
			unit.Session?.GetObjectVisual(trap.TrapPosition)?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);
		}
	}

	// 9-9장 "함정이 작동함": 그 함정을 해제 중인 유닛 전부의 해제를 중단한다(대응 상태는 유지).
	public static void InterruptDisarmersOf(string trapObjectId, IEnumerable<Unit> units)
	{
		if (units == null) return;
		foreach (var u in units)
		{
			if (u == null || u.currentTrapInteraction == null) continue;
			if (u.currentTrapInteraction.TrapObjectId != trapObjectId) continue;
			ApplyDisarmInterruptPenalty(u);
		}
	}
}
