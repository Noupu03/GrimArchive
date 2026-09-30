using System.Collections.Generic;
using UnityEngine;

public enum ReportStep { Continue, Ended }

// 03번 문서(행동전환_중단_재개) 3번·10번 항목 "코어 발견과 리더 보고" 구현부. 코어는 일반 임무·집결·다음
// 방 이동보다 우선한다 — 리더가 알면 Party.TryStartRally가 스스로 새 집결을 막고, 이미 진행/완료된
// 집결·다음 방 이동은 Party.OnLeaderLearnsCore가 즉시 해제한다. TrapPartySystem/PartyDeathSystem과
// 같은 "파티 단위 정적 시스템" 관례를 따른다.
//
// 보고 의무(Human.pendingCoreReportPos)는 대기 상태(currentWait)와 분리돼 있어 이동이 끊겨도 남는다 —
// 전달되거나 코어가 처리될 때만 사라진다(검증문서 03-14 발견 2).
public static class PartyCoreReportSystem
{
	// UnitFunction.CastRay가 코어를 정확 인지(AccuratePerception)로 처음 발견하는 시점에 호출한다
	// (TrapPartySystem.OnTrapDiscovered/PartyDeathSystem.OnCorpseDiscovered와 동일한 호출 관례).
	public static void OnCoreDiscovered(Human discoverer, InteractableObject core)
	{
		if (discoverer.party == null) return; // 파티 없는 단독 유닛 — 보고 대상 없음
		if (!TacticalFSMState.IsRoomCoreStillHostile(discoverer, core.Position)) return; // 이미 처리됨(내 진영 소유)
		if (discoverer.party.LeaderKnownCorePosition == core.Position) return; // 리더가 이미 앎

		var leader = discoverer.party.Leader;
		if (leader != null && (discoverer == leader || PropagationSystem.CanPropagate(discoverer, leader)))
		{
			discoverer.party.OnLeaderLearnsCore(core.Position);
			return;
		}

		// 즉시 전달하지 못했다 — 보고 의무를 기록한다. 리더가 아직 없으면 리더가 생길 때 TickPendingReport가 이어 받는다.
		discoverer.pendingCoreReportPos = core.Position;
		if (leader == null) return;

		// 리더가 전파 범위 밖 — 발견자는 보고 이동을 시작한다(03번 문서 3번 항목: "코어 보고 이동은 집결 명령을
		// 받았다는 이유로 중단하지 않는다"). 코어는 일반 임무·집결·다음 방 이동보다 우선하므로 기존 대기(집결/
		// 다음방이동)를 덮어써도 된다 — 단 이미 시작한 상호작용(currentInvestigation)은 건드리지 않는다. BT에서
		// Investigate가 Wait보다 우선순위가 높아 자연히 먼저 끝난 뒤에 이 보고 이동으로 넘어간다.
		StartReportWait(discoverer, core.Position);
	}

	private static void StartReportWait(Human human, Vector3Int corePos)
	{
		human.currentWait = new WaitState { Reason = WaitReason.ReportingCoreToLeader, CorePosition = corePos };
		// 기존 대기(집결 등)를 덮어쓰므로, 그 대기에서 쌓인 스턱카운트가 이어지지 않도록 같이 리셋한다.
		human.waitStuckTurns = 0;
	}

	// 지금 전파 범위 안이면 그 자리에서 전달하고 true — 보고 의무를 해제하고, 보고 이동 중이었다면 그것도 끝낸다.
	public static bool TryDeliverToLeader(Human discoverer, Vector3Int corePos)
	{
		var leader = discoverer.party?.Leader;
		if (leader == null || leader.hp <= 0) return false;
		if (!PropagationSystem.CanPropagate(discoverer, leader)) return false;

		discoverer.party.OnLeaderLearnsCore(corePos);
		if (discoverer.pendingCoreReportPos == corePos) discoverer.pendingCoreReportPos = null;
		if (discoverer.currentWait?.Reason == WaitReason.ReportingCoreToLeader) EndReportWait(discoverer);
		return true;
	}

	private static ReportStep EndReportWait(Human human)
	{
		human.currentWait = null;
		human.waitStuckTurns = 0;
		// 보고 중인 유닛은 집결 완료를 붙잡고 있었으므로(Party.CheckRallyComplete) 끝날 때 반드시 다시 확인한다.
		if (human.party != null) human.party.CheckRallyComplete();
		return ReportStep.Ended;
	}

	// ── 리더 위치를 "아는" 경로 (직접 확인 / 전파 / 집결 명령) ─────────────────────────────────────

	// UnitFunction.UpdateFOV가 패스 끝에서 호출한다 — 같은 파티 아군은 시야에 들어오는 것만으로 직접 확인이다.
	public static void RefreshKnownLeaderFromSight(Human human)
	{
		var leader = human.party?.Leader;
		if (leader == null || leader == human || leader.hp <= 0) return;
		if (human.visiblePartyMembers.Contains(leader))
			human.knownLeader.Update(leader, leader.position, Time.time);
	}

	// 03번 10장 "리더의 새 위치를 직접 확인하거나 전파받으면" — 같은 파티원이 나보다 최신으로 아는 리더 위치를
	// 일반 전파 조건(CanPropagate)이 성립할 때 받는다.
	private static void RelayKnownLeader(Human human)
	{
		var party = human.party;
		var leader = party?.Leader;
		if (leader == null) return;

		foreach (var m in party.Members)
		{
			if (m == null || m == human || m == leader || m.hp <= 0) continue;
			var info = m.knownLeader;
			if (!info.IsFor(leader)) continue;
			if (human.knownLeader.IsFor(leader) && info.Timestamp <= human.knownLeader.Timestamp) continue;
			if (!PropagationSystem.CanPropagate(m, human)) continue;

			human.knownLeader.Update(leader, info.Position, info.Timestamp);
		}
	}

	// UnitFunction.OnUpdate의 0.1초 틱이 TickOngoingObjectPropagation 옆에서 호출한다.
	public static void TickPendingReport(Human human)
	{
		if (!human.pendingCoreReportPos.HasValue) return;
		var party = human.party;
		if (party == null) { human.pendingCoreReportPos = null; return; }

		Vector3Int corePos = human.pendingCoreReportPos.Value;
		if (!TacticalFSMState.IsRoomCoreStillHostile(human, corePos)) { human.pendingCoreReportPos = null; return; } // 이미 처리된 코어

		RelayKnownLeader(human);

		// 03번 10장: 집결하러 이동하는 동안이나 합류 후에도 일반 전파 조건을 충족하면 어떤 행동 중이든 전달한다.
		if (TryDeliverToLeader(human, corePos)) return;

		var leader = party.Leader;
		if (leader == null || leader.hp <= 0) return; // 리더가 생길 때까지 의무만 유지

		// 재무장 — 보고 이동이 끊겼거나 집결·다음 방 이동으로 덮인 상태에서도 코어 보고는 우선한다.
		var wait = human.currentWait;
		if (wait == null || wait.Reason == WaitReason.AwaitingPartyAtRallyPoint || wait.Reason == WaitReason.AdvancingToNextRoom)
			StartReportWait(human, corePos);
	}

	// ── 보고 이동 (TacticalFSMState.ExecuteWait의 ReportingCoreToLeader 분기가 매 틱 호출) ───────────────

	public static ReportStep StepReportMovement(Human human, WaitState wait)
	{
		Vector3Int corePos = wait.CorePosition.Value;

		if (!TacticalFSMState.IsRoomCoreStillHostile(human, corePos))
		{
			human.pendingCoreReportPos = null; // 이미 처리된 코어 — 보고할 것이 없다
			return EndReportWait(human);
		}
		if (TryDeliverToLeader(human, corePos)) return ReportStep.Ended;

		var leader = human.party?.Leader;
		if (leader == null || leader.hp <= 0)
			return EndReportWait(human); // 의무는 남는다 — 리더가 생기면 TickPendingReport가 이 대기를 다시 만든다

		float now = Time.time;
		ResolveDestination(human, wait, now, out var kind, out var target);
		if (kind != wait.ReportKind || (kind != ReportDestinationKind.None && target != wait.ReportTarget))
		{
			// 목적지가 바뀌었다(더 우선하는 정보가 생겼거나 다음 후보로 넘어감) — 부재 확인·대기 상태를 새로 시작한다.
			wait.ReportKind = kind;
			wait.ReportTarget = target;
			wait.AbsenceStartTime = -1f;
			wait.IsParked = false;
			human.waitStuckTurns = 0;
		}

		if (kind == ReportDestinationKind.None)
		{
			// 확인할 위치도 갈 곳도 없다 — 05번 8장: 알고 있는 허용 위치(지금 자리)에서 대기한다. 새 정보가 생기면 다시 움직인다.
			wait.IsParked = true;
			return ReportStep.Continue;
		}

		bool arrived = PartyReportMath.HasArrived(human.position, target);
		if (arrived)
		{
			switch (kind)
			{
				case ReportDestinationKind.ToLeader:
				case ReportDestinationKind.ToRally:
					return ConfirmAbsence(human, wait, now);
				case ReportDestinationKind.ToDoor:
					wait.IsParked = true; // 문 주변 대기 위치 — 이후 새 정보가 오면 다시 판단한다
					return ReportStep.Continue;
				case ReportDestinationKind.SearchFrontier:
					wait.ReportKind = ReportDestinationKind.None; // 시야가 넓어졌으니 다음 틱에 새 후보를 고른다
					return ReportStep.Continue;
			}
		}

		// 부재 확인 대기 중 밀려나 목적지를 벗어났다면 3초는 다시 도착한 시점부터 센다(도착 전 시간이 섞이지 않게).
		wait.AbsenceStartTime = -1f;

		int distBefore = AIMovementHelper.ChebyshevDistance(human.position, target);
		AIMovementHelper.MoveTowardsPos(human, target);
		if (AIMovementHelper.ChebyshevDistance(human.position, target) < distBefore)
		{
			human.waitStuckTurns = 0;
		}
		else if (++human.waitStuckTurns >= (AIConfigLoader.Behavior?.waitStuckTurnLimit ?? 4))
		{
			// 보고를 포기하지 않는다 — 이 종류의 목적지만 잠시 제외하고 다음 후보로 넘어간다(시간이 지나면 다시 후보).
			human.waitStuckTurns = 0;
			wait.ReportBlockedUntil[(int)kind] = now + (AIConfigLoader.Behavior?.coreReportBlockedRetrySeconds ?? 5f);
			wait.ReportKind = ReportDestinationKind.None;
		}
		return ReportStep.Continue;
	}

	// 03번 11장: 마지막 확인 위치에 도착해 대상 부재를 확인하면 3초 대기한다. 그 사이 리더를 직접 확인·전파받으면
	// (ResolveDestination이 다른 목적지를 돌려주므로) 대기는 새로 시작되고 보고가 이어진다.
	private static ReportStep ConfirmAbsence(Human human, WaitState wait, float now)
	{
		if (wait.AbsenceStartTime < 0f) wait.AbsenceStartTime = now;
		if (!ExplorationMath.AbsenceWaitElapsed(wait.AbsenceStartTime, now, ExplorationMath.LastPositionAbsenceWaitSeconds))
			return ReportStep.Continue;

		// 빈 위치로 확정 — 새 정보 없이 같은 위치를 다시 목적지로 고르지 않는다.
		if (wait.ReportKind == ReportDestinationKind.ToLeader)
			human.knownLeader.MarkEmpty(wait.ReportTarget, now);
		else
			human.knownRallyPoint = null;

		wait.AbsenceStartTime = -1f;
		wait.ReportKind = ReportDestinationKind.None;
		return ReportStep.Continue;
	}

	private static bool IsBlocked(WaitState wait, ReportDestinationKind kind, float now)
		=> now < wait.ReportBlockedUntil[(int)kind];

	// 목적지 우선순위(03번 10장 661~671줄/05번 8장): 아는 리더 위치 → 유효한 집결 위치 → 발견한 문 주변 → 시야 넓히기.
	// 앞의 둘은 값싼 조회라 매 틱 확인해 새 정보가 생기면 즉시 갈아타고, 문·프론티어는 비싸서 진행 중이면 목적지를 재사용한다.
	private static void ResolveDestination(Human human, WaitState wait, float now, out ReportDestinationKind kind, out Vector2Int target)
	{
		var party = human.party;
		var known = human.knownLeader;

		bool leaderUsable = !IsBlocked(wait, ReportDestinationKind.ToLeader, now)
			&& PartyReportMath.LeaderPositionUsable(known.IsFor(party.Leader), known.Position, known.EmptyConfirmedPosition);
		bool rallyValid = !IsBlocked(wait, ReportDestinationKind.ToRally, now)
			&& party.IsRallyPointOnFloor(human.currentFloor) && human.knownRallyPoint == party.RallyPoint;

		kind = PartyReportMath.ResolveDestinationKind(leaderUsable, rallyValid, false, false);
		if (kind == ReportDestinationKind.ToLeader) { target = known.Position; return; }
		if (kind == ReportDestinationKind.ToRally) { target = party.RallyPoint.Value; return; }

		if (!IsBlocked(wait, ReportDestinationKind.ToDoor, now))
		{
			if (wait.ReportKind == ReportDestinationKind.ToDoor)
			{
				kind = ReportDestinationKind.ToDoor; target = wait.ReportTarget; return;
			}
			if (now >= wait.NextDoorScanTime)
			{
				wait.NextDoorScanTime = now + PartyReportMath.DoorScanIntervalSeconds;
				if (TryFindDoorWaitSlot(human, out target)) { kind = ReportDestinationKind.ToDoor; return; }
			}
		}

		if (!IsBlocked(wait, ReportDestinationKind.SearchFrontier, now))
		{
			// 진행 중인 프론티어 목적지는 아직 미탐색(0) 타일이고 도착 전이면 그대로 쓴다(NavigationFSMState와 같은 규칙).
			if (wait.ReportKind == ReportDestinationKind.SearchFrontier
				&& human.personalMap.GetTileTerrain(new Vector3Int(wait.ReportTarget.x, wait.ReportTarget.y, human.currentFloor)) == 0
				&& !PartyReportMath.HasArrived(human.position, wait.ReportTarget))
			{
				kind = ReportDestinationKind.SearchFrontier; target = wait.ReportTarget; return;
			}
			if (human.personalMap.TryGetNearestFrontierTile(human.currentFloor, human.position, out target))
			{
				kind = ReportDestinationKind.SearchFrontier; return;
			}
		}

		kind = ReportDestinationKind.None;
		target = default;
	}

	private static bool TryFindDoorWaitSlot(Human human, out Vector2Int slot)
	{
		slot = default;
		Vector2? targetCenter = GrimArchive.Wave.HumanWaveManager.Instance?.TargetRoomCenter;
		if (!AIMovementHelper.TryFindKnownDoorInCurrentRoom(human, targetCenter, out Vector2Int doorPos, out _)) return false;
		slot = AIMovementHelper.FindDoorWaitSlot(human, doorPos, new HashSet<Vector2Int>());
		return true;
	}
}
