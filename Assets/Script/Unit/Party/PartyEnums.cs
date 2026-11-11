// PartyType = 임무(컨셉 PartyConcept와 별개 축). 임무마다 방 활동 종료·집결 기준이 다르다.
public enum PartyType
{
	Explore, // 탐색 파티 — 방 지형 전체를 시야로 확인해야 활동 종료
	Recover, // 회수 파티 — 리더가 아는 현재 방의 회수 가능 대상을 모두 처리해야 활동 종료
	MopUp,   // 소탕 파티 — 다음 이동 문까지 이동하며 확인한 범위에 적·교전이 없어야 활동 종료
	Occupy,  // 점령 파티 — 현재 방에 기초 시설을 완공해 기초 거점이 되면 활동 종료(OccupationSystem)
	CoreDestroy, // 코어 파괴 — 실제 파괴까지
	Boss, // 보스 — 지정 보스 완료와 남은 교전(후속 콘텐츠, 선택 불가)
}

public static class PartyTypeUtil
{
	public static string ToKorean(this PartyType type) => type switch
	{
		PartyType.Explore     => "탐색",
		PartyType.Recover     => "회수",
		PartyType.MopUp       => "소탕",
		PartyType.Occupy      => "점령",
		PartyType.CoreDestroy => "코어 파괴",
		PartyType.Boss        => "보스",
		_                     => type.ToString(),
	};
}
