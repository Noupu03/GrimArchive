using UnityEngine;

// 05번 문서 1장 73줄: 집결을 마치고 다음 방으로 이동하려는데 다음 이동 문을 아직 모르면 "진형을 유지하며 문을 찾는다".
// 리더는 대기 없이 기존 자유 탐색(NavigationFSMState)이 미탐사 지형을 넓혀 문을 찾고, 나머지 파티원은 이 시스템의
// SearchingNextDoor 대기로 자기가 아는 리더 위치 주변을 따라다닌다. 문이 알려지면 HumanWaveManager가 이 대기를 기존
// AdvancingToNextRoom으로 덮어써 공동 이동으로 전환한다. 진형 자리·간격은 문서가 차후로 미뤄 "리더 주변 유지"로 근사한다.
public static class PartyDoorSearchSystem
{
	// HumanWaveManager.UpdatePartyDestination이 문을 못 찾았을 때 리더 외 적격 파티원에게 배정 가능한지 판정한다.
	public static bool CanAssign(Human member, float now)
		=> member.currentWait == null && now >= member.nextSearchFollowAllowedTime;

	public static void Assign(Human member)
	{
		// 전파받은 적 위치로 접근하던 경계는 공동 이동이 시작되면 접는다(03번 1장 50줄 — 개인 탐색으로 흩어지지 않음).
		if (member.currentAlertSearch != null && member.currentAlertSearch.IsIndirectEnemyApproach) member.currentAlertSearch = null;
		member.currentFormation = null; // 리더를 따라 문을 찾는 공동 이동에 들어가면 비전투 보호 포메이션은 끝난다(05번 9장, 검증 05-08 관찰 1)
		member.currentWait = new WaitState { Reason = WaitReason.SearchingNextDoor };
		member.waitStuckTurns = 0;
	}

	// TacticalFSMState.ExecuteWait의 SearchingNextDoor 분기가 매 틱 호출한다.
	public static ReportStep StepFollow(Human human, WaitState wait)
	{
		var party = human.party;
		var leader = party?.Leader;
		// 이동 의도가 사라졌다(코어 처리 등으로 해제되거나 리더가 이미 방을 떠남) 또는 리더가 없다 — 추종을 끝낸다.
		if (party == null || leader == null || leader.hp <= 0 || !(party.ReadyToAdvance || party.LeaderKnownCorePosition.HasValue))
			return End(human, retryCooldown: false);

		float now = Time.time;
		// 실제 리더 위치가 아니라 이 유닛이 "아는" 리더 위치(직접 확인·집결 명령·파티원 중계, 03-15)만 쓴다.
		var known = human.knownLeader;
		bool hasKnown = known.IsFor(leader);
		int radius = AIConfigLoader.Behavior?.doorSearchFollowRadius ?? 3;
		int dist = hasKnown ? AIMovementHelper.ChebyshevDistance(human.position, known.Position) : 0;

		var step = PartyReportMath.ResolveFollowStep(hasKnown, dist, radius);
		// 아는 리더 위치 근처에 도착했는데 리더를 마지막으로 확인한 지 오래됐다면(직접 확인은 매 시야 패스마다 시각이 갱신된다) 리더는
		// 이미 그 자리를 떠난 것이다 — 낡은 위치에 영구히 머물지 않게 위치를 잃은 것으로 본다.
		if (step == PartyReportMath.FollowStep.Hold && now - known.Timestamp > ExplorationMath.LastPositionAbsenceWaitSeconds)
			step = PartyReportMath.FollowStep.Lost;

		switch (step)
		{
			case PartyReportMath.FollowStep.Lost:
				// 리더 위치를 모른다 — 잠시(부재 확인 3초와 같은 시간) 기다려 보고, 그래도 모르면 추종을 접고 개인 탐색으로 넘긴다.
				wait.WaitPosition = null;
				if (wait.FollowLeaderLostSince < 0f) wait.FollowLeaderLostSince = now;
				if (ExplorationMath.AbsenceWaitElapsed(wait.FollowLeaderLostSince, now, ExplorationMath.LastPositionAbsenceWaitSeconds))
					return End(human, retryCooldown: true);
				return ReportStep.Continue;

			case PartyReportMath.FollowStep.Hold:
				wait.FollowLeaderLostSince = -1f;
				wait.WaitPosition = known.Position;
				human.waitStuckTurns = 0;
				return ReportStep.Continue; // 이미 리더 주변 — 제자리에서 다음 정보를 기다린다

			default: // Move
				wait.FollowLeaderLostSince = -1f;
				wait.WaitPosition = known.Position;
				AIMovementHelper.MoveTowardsPos(human, known.Position);
				if (AIMovementHelper.ChebyshevDistance(human.position, known.Position) < dist)
				{
					human.waitStuckTurns = 0;
				}
				else if (++human.waitStuckTurns >= (AIConfigLoader.Behavior?.waitStuckTurnLimit ?? 4))
				{
					// 길이 막혀 따라갈 수 없다 — 추종을 접고 잠시 뒤 다시 배정받는다.
					return End(human, retryCooldown: true);
				}
				return ReportStep.Continue;
		}
	}

	private static ReportStep End(Human human, bool retryCooldown)
	{
		human.currentWait = null;
		human.waitStuckTurns = 0;
		if (retryCooldown)
			human.nextSearchFollowAllowedTime = Time.time + (AIConfigLoader.Behavior?.doorSearchFollowRetrySeconds ?? 5f);
		return ReportStep.Ended;
	}
}
