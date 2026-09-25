using UnityEngine;

// 03번 문서(행동전환_중단_재개) 3번 항목 "코어 발견과 리더 보고" 구현부. 코어는 일반 임무·집결·다음
// 방 이동보다 우선한다 — 리더가 알면 Party.TryStartRally가 스스로 새 집결을 막고, 이미 진행/완료된
// 집결·다음 방 이동은 Party.OnLeaderLearnsCore가 즉시 해제한다. TrapPartySystem/PartyDeathSystem과
// 같은 "파티 단위 정적 시스템" 관례를 따른다.
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
		if (leader == null) return;

		if (discoverer == leader || PropagationSystem.CanPropagate(discoverer, leader))
		{
			discoverer.party.OnLeaderLearnsCore(core.Position);
			return;
		}

		// 리더가 전파 범위 밖 — 발견자는 리더 위치로 보고 이동한다(03번 문서 3번 항목: "코어 보고
		// 이동은 집결 명령을 받았다는 이유로 중단하지 않는다"). 코어는 일반 임무·집결·다음 방 이동보다
		// 우선하므로 기존 대기(집결/다음방이동)를 덮어써도 된다 — 단 이미 시작한 상호작용
		// (currentInvestigation)은 건드리지 않는다. BT에서 Investigate가 Wait보다 우선순위가 높아
		// 자연히 먼저 끝난 뒤에 이 보고 이동으로 넘어간다.
		discoverer.currentWait = new WaitState { Reason = WaitReason.ReportingCoreToLeader, CorePosition = core.Position };
	}

	// TacticalFSMState.ExecuteWait의 ReportingCoreToLeader 분기가 매 틱 호출한다 — 리더가 지금
	// 전파 범위 안이면 전달하고 true, 아니면 false(계속 리더에게 접근).
	public static bool TryDeliverToLeader(Human discoverer, Vector3Int corePos)
	{
		var leader = discoverer.party?.Leader;
		if (leader == null || leader.hp <= 0) return false;
		if (!PropagationSystem.CanPropagate(discoverer, leader)) return false;

		discoverer.party.OnLeaderLearnsCore(corePos);
		return true;
	}
}
