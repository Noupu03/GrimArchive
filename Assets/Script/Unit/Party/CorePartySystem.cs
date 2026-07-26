// 03문서 7-3장(2026-07-27 신규) 구현부 — 코어 발견 시점의 파티 전파를 담당한다. "발견 유닛이 리더의
// 전파 범위 밖이면 직접 리더에게 이동해 전파한다"는 물리적 이동 단계는 07_전파·소리·간접입력 문서
// 부재로 5-5/9-3장과 동일한 관례(즉시 파티 전체 공유)로 근사했다 — 사용자 확인(2026-07-27: "발견~
// 리더조사 흐름만 구현")대로 리더 도착 이후의 실제 조사 흐름(TacticalFSMState.CanContinueCore 등)에
// 공수를 집중했다.
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

		party.AssignLeaderIfNeeded();
		var leader = party.Leader;
		if (leader != null && !leader.personalMap.IsObjectKnown(coreObj.Id))
			leader.personalMap.RegisterObject(coreObj.Id, coreObj.Position, coreObj.BaseDanger, coreObj.BaseInterest, coreObj.Tags, coreObj.CauserStage);
	}
}
