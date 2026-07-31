// 03문서 7-3장(2026-07-27 신규) 구현부 — 코어 발견 시점의 파티 전파를 담당한다.
// 2026-07-31: 07문서 구현으로 "즉시 파티 전체 공유" 근사를 실제 전파 조건(PropagationSystem.
// CanPropagate — 비전투+같은 공간+카리스마 기반 전파 범위)으로 교체했다. 다만 "발견 유닛이 리더의
// 전파 범위 밖이면 직접 리더에게 이동해 전파한다"는 물리적 이동 단계는 여전히 09_목표·이동경로 문서
// 부재로 없다 — 대신 TickLeaderPropagation을 매 틱 재확인해서, 리더가 나중에 전파 범위 안으로
// 들어오면(또는 발견자/다른 보유자가 리더에게 다가가면) 그 시점에 자연히 전달되게 한다.
public static class CorePartySystem
{
	public static void OnCoreDiscovered(Human discoverer, InteractableObject coreObj)
	{
		var party = discoverer.party;
		if (party == null) return;
		if (party.PendingCoreObjectId == coreObj.Id) return; // 이미 파티가 알고 있음
		if (coreObj.IsInvestigated) return; // 이미 조사 완료된 코어

		party.PendingCoreObjectId = coreObj.Id;
		party.PendingCorePosition = coreObj.Position;
		discoverer.personalMap.RegisterObject(coreObj.Id, coreObj.Position, coreObj.BaseDanger, coreObj.BaseInterest, coreObj.Tags, coreObj.CauserStage);

		party.AssignLeaderIfNeeded();
		TryInformLeader(party, coreObj);
	}

	// UnitFunction.OnUpdate의 0.1초 틱에서 인류마다 호출 — 자신이 리더인데 아직 코어를 모르면 전파
	// 조건을 다시 확인한다(발견자 본인이거나, 그사이 정보를 받은 다른 파티원이 근처로 왔을 수 있음).
	public static void TickLeaderPropagation(Human human)
	{
		var party = human.party;
		if (party == null || party.PendingCoreObjectId == null || party.Leader != human) return;
		if (human.personalMap.IsObjectKnown(party.PendingCoreObjectId)) return;
		if (human.Session == null || !human.Session.objectGrid.TryGetValue(party.PendingCorePosition, out var coreObj)) return;
		TryInformLeader(party, coreObj);
	}

	private static void TryInformLeader(Party party, InteractableObject coreObj)
	{
		var leader = party.Leader;
		if (leader == null || leader.hp <= 0 || leader.personalMap.IsObjectKnown(coreObj.Id)) return;

		Human carrier = FindKnownCarrier(party, coreObj.Id);
		if (carrier == null) return; // 아직 아무도 모름(발견자 정보가 아직 등록 전) — 다음 틱 재시도
		if (carrier != leader && !PropagationSystem.CanPropagate(carrier, leader)) return;

		leader.personalMap.RegisterObject(coreObj.Id, coreObj.Position, coreObj.BaseDanger, coreObj.BaseInterest, coreObj.Tags, coreObj.CauserStage);
	}

	private static Human FindKnownCarrier(Party party, string coreObjId)
	{
		foreach (var m in party.Members)
			if (m != null && m.hp > 0 && m.personalMap.IsObjectKnown(coreObjId)) return m;
		return null;
	}
}
