using Haare.Util.Logger;

// 거점 안 정보 전달(후퇴도주 7-2장·R45 "거점 안에서 정보 전달", 코어 점령 6장 "정보 갱신"·12-1장 "다른 파티가 후퇴해 거점에 도착했을 때 허용된 전달 정보로 갱신").
// 후퇴해 거점에 도착한 인류가 그 거점 방에 있는 다른 파티 인류(주로 상주 점령 파티)와 만나면 서로의 지도 정보를 주고받는다 — 던전 안의 정보 공유일 뿐이라 던전 밖 기록 전달·생환 경험치 정산은 아니다(HumanKnowledgeBase는 건드리지 않는다).
// 같은 파티끼리는 기존 전파(PropagationSystem)가 맡으므로 다른 파티 인류하고만 교환한다. 교환 내용은 PersonalMapKnowledge.MergeSharedFrom이 정하고, 같은 상대와는 거점 방문당 한 번만 한다.
public static class BaseInfoSharing
{
	// PartyRetreatSystem이 거점으로 후퇴 중인 파티를 틱할 때(0.5초) 부른다 — 도착해 있는 파티원마다 방 안의 다른 파티 인류와 아직 교환하지 않았으면 교환한다.
	public static void Tick(Party party, Room baseRoom)
	{
		var session = party?.Leader?.Session ?? FirstSession(party);
		if (session == null || baseRoom == null) return;

		foreach (var m in party.Members)
		{
			if (!CanShare(m) || !m.Retreat.AtBase) continue; // 거점에 도착한(휴식 대상) 파티원만
			var shared = m.Retreat.BaseSharedWith;
			foreach (var u in session.units)
			{
				if (!(u is Human h) || h == m || h.party == m.party || !CanShare(h)) continue;
				if (h.currentFloor != baseRoom.Floor || h.currentRoom != baseRoom) continue;
				if (shared.Contains(h.name)) continue;

				Exchange(m, h);
				shared.Add(h.name);
			}
		}
	}

	// 정보를 주고받을 수 있는 상태 — 살아 있고 도주 중이 아니며 전투 중이 아니다(07-A 6장 일반 전파 조건과 같은 기준: 발신자·수신자 모두 비전투). 후퇴 도착자는 거점에 도착한 상태여야 한다.
	private static bool CanShare(Human h)
		=> h != null && h.hp > 0f && h.Retreat != null && !h.Retreat.IsFleeing && h.personalSpottedEnemies.Count == 0 && h.party != null;

	// 두 인류가 서로의 지도를 병합한다(양방향). 주고받은 양은 로그로 남긴다.
	public static void Exchange(Human a, Human b)
	{
		var toB = b.personalMap.MergeSharedFrom(a.personalMap);
		var toA = a.personalMap.MergeSharedFrom(b.personalMap);
		if (!toA.Any && !toB.Any) return;
		LogHelper.Log(LogHelper.GAME, $"[후퇴] 거점 정보 전달 — {a.name} ↔ {b.name} (받은 정보: {a.name} 지형 {toA.Tiles}·오브젝트 {toA.Objects}·몬스터 {toA.Monsters}·방 {toA.Rooms} / {b.name} 지형 {toB.Tiles}·오브젝트 {toB.Objects}·몬스터 {toB.Monsters}·방 {toB.Rooms})");
	}

	private static GameSession FirstSession(Party party)
	{
		foreach (var m in party.Members)
			if (m?.Session != null) return m.Session;
		return null;
	}
}
