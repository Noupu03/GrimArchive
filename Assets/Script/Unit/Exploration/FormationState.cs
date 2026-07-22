// 6장: 보호 포메이션 참여 상태. "호위하는 쪽"(일반 탐색 중이던 유닛)만 이 레코드를 들고 있고,
// "호위받는 쪽"(상호작용 유닛)은 이 레코드가 없다 — 대신 Human.AnyEscortHitThisTurn()이 같은 파티
// 안에서 EscortTarget==this인 멤버를 역으로 찾아 8-2장 판정에 쓴다(양방향 리스트를 따로 관리하지
// 않기 위한 설계, 시야인지반응_03_GOAP목표우선순위표_2026-07-22.txt 5절 "상호 참조" 메모 참고).
public class FormationState
{
	public Human EscortTarget;
}
