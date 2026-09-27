// 04번 문서(이동경로_속도_점유충돌) 4번 항목 순수 계산 상수 모음.
public static class MovementMath
{
	// "회피 가능 경로 우선 → 노출시간 최소 → 전체길이 최소"라는 3단계 우선순위를, 회피 대상 타일 1칸당
	// 이 상수만큼 이동비용을 더하는 방식으로 하나의 가중치 합으로 성립시킨다. 이 프로토타입 맵 규모에서
	// 나올 수 있는 기본 경로비용 차이(수백~수천 수준)보다 압도적으로 커야 사전식(lexicographic) 순서가
	// 깨지지 않는다.
	public const int AttackRangeAvoidExtraCost = 100000;
}
