using UnityEngine;

// 할 일 없는 개인의 합류(05번 8장): 수행할 미확인 타일 확인·임무·상호작용·보고·전투가 없으면 아는 리더를 기준으로 파티 진형 자리에 합류한다(개인 이동속도). 개인 탐색·조사를 현재 방 안으로 제한하므로(Human.TryGetExplorationBounds) 방 안에 할 일이 없어진 구성원이 방 밖을 떠돌지 않고 이 행동을 한다.
// 목적지 해석·부재 확인·막힘 기억은 코어 보고 이동과 같은 구현(PartyCoreReportSystem.StepTowardDestination)을 쓰고, 여기서 더하는 건 '리더가 곁에 있으면 그대로 유지'뿐이다(최근 확인한 리더 자리에서 부재 확인을 돌리면 빈 자리로 기록돼 버린다).
// 진형 자리·간격은 후속 문서 몫이라 리더 주변 doorSearchFollowRadius 이내로 근사하고, 리더는 대기한다. 모르는 리더·집결 위치·문은 시스템 정보로 가져오지 않으며, 리더가 없는 인류는 방 제한이 없어 이 시스템에 오지 않는다.
public static class HumanIdleSystem
{
	// 이 시간보다 오래 Step이 불리지 않았으면(다른 행동을 했거나 방을 옮김) 이전 목적지 기억을 버린다 — 낡은 문 대기 자리를 다른 방에서 재사용하지 않게. 느린 유닛도 틱 사이에 지워지지 않도록 부재 확인 대기(3초)와 같은 값이다.
	private const float StaleAfterSeconds = 3f;

	public static BTStatus Step(Human human)
	{
		var party = human.party;
		var leader = party?.Leader;
		if (party == null || leader == null || leader.hp <= 0 || leader == human) return BTStatus.Running;

		float now = Time.time;
		if (human.idleWait == null || now - human.idleLastStepTime > StaleAfterSeconds || human.idleWaitRoom != human.lastKnownRoom)
		{
			human.idleWait = new WaitState { Reason = WaitReason.SearchingNextDoor };
			human.idleWaitRoom = human.lastKnownRoom;
			human.waitStuckTurns = 0;
		}
		human.idleLastStepTime = now;
		var wait = human.idleWait;

		// 리더를 최근에 직접 확인했거나 전파받았고 이미 그 주변이면 합류한 것이다 — 제자리에서 다음 정보를 기다린다(이동 의도가 생기면 BT가 이 행동을 대체한다).
		var known = human.knownLeader;
		if (known.IsFor(leader) && now - known.Timestamp <= ExplorationMath.LastPositionAbsenceWaitSeconds)
		{
			int radius = AIConfigLoader.Behavior?.doorSearchFollowRadius ?? 3;
			if (AIMovementHelper.ChebyshevDistance(human.position, known.Position) <= radius)
			{
				wait.AbsenceStartTime = -1f;
				wait.IsParked = true;
				human.waitStuckTurns = 0;
				return BTStatus.Running;
			}
		}

		PartyCoreReportSystem.StepTowardDestination(human, wait, now);
		return BTStatus.Running;
	}
}
