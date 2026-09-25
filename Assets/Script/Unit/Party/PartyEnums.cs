// 01번 문서 7-1장: 파티 종류별로 "현재 방 활동 종료·집결 기준"이 다르다. 편성 가능 유닛·선택
// 가중치는 (미작성)03_파티 종류·목표·포메이션_시스템 문서로 미뤄진 영역이라 스텁이다.
// 2026-09-25 회의록(코어·점령 재설계) 반영: "점령은 오직 점령 파티만" — Occupy를 다시 추가했다
// (보스공략은 여전히 제외 — 별도 파티 종류로 구별할 근거가 아직 없음). 점령을 실제로 트리거하던
// "임시 점령 오브젝트" 메커니즘은 같은 날 삭제됐다(연출이 마음에 안 든다는 피드백이 아니라, 더
// 나은 다른 점령 조건으로 대체하기로 함) — PartyType.Occupy 자체와 Party.IsRoomActivityComplete의
// 판정 기준(방이 인류 소유가 되면 완료)만 세팅으로 남겨뒀다. 다음 점령 조건을 붙일 때 참고:
// OffenseProcessor.OnCoreDestroyed가 "체력 0 → ApplyRoomOwnership + 방어적 재확인" 패턴의 예시고,
// IMapColorizer.PulseRoomOutline은 이미 구현된 점령 완료 시 재생할 공용 연출이다.
public enum PartyType
{
	Explore, // 탐색 파티 — 방 지형 전체를 시야로 확인해야 활동 종료
	Recover, // 회수 파티 — 리더가 아는 현재 방의 회수 가능 대상을 모두 처리해야 활동 종료
	MopUp,   // 소탕 파티 — 다음 이동 문까지 이동하며 확인한 범위에 적·교전이 없어야 활동 종료
	Occupy,  // 점령 파티 — 방이 인류 소유로 전환돼야 활동 종료(트리거 조건은 아직 미정, 위 주석 참고)
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
