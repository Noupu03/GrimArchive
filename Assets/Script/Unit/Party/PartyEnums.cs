// 파티 종류별로 현재 방 활동 종료·집결 기준이 다르다(01번 7-1장). 편성 가능 유닛·가중치는 미작성 문서 영역이라 스텁이고, 점령은 점령 파티만 한다(보스공략은 제외). 점령 파티의 거점 건설·진출은 OccupationSystem(코어 점령 설계명세서 v0.2 12~14장)이 구동한다.
public enum PartyType
{
	Explore, // 탐색 파티 — 방 지형 전체를 시야로 확인해야 활동 종료
	Recover, // 회수 파티 — 리더가 아는 현재 방의 회수 가능 대상을 모두 처리해야 활동 종료
	MopUp,   // 소탕 파티 — 다음 이동 문까지 이동하며 확인한 범위에 적·교전이 없어야 활동 종료
	Occupy,  // 점령 파티 — 현재 방에 기초 시설을 완공해 기초 거점이 되면 활동 종료(OccupationSystem)
}

public static class PartyTypeUtil
{
	public static string ToKorean(this PartyType type) => type switch
	{
		PartyType.Explore => "탐색",
		PartyType.Recover => "회수",
		PartyType.MopUp    => "소탕",
		PartyType.Occupy   => "점령",
		_                  => type.ToString(),
	};
}
