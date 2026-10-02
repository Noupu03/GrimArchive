// 01번 문서(파티 목표와 비전투 목표 선택) 순수 계산 함수 — WeightMath.cs/VisionMath.cs와 동일 컨벤션.
public static class PartyGoalMath
{
	// 3·5장: 파티종류×목표종류 차등 가중치 표는 미작성 "03_파티 종류·목표·포메이션" 문서 대기 —
	// 그 표가 나오기 전까지는 모든 후보에 균일 1.0을 적용한다(00-06에서 확정한 "임의 배율 금지"
	// 원칙 — 차등 없이 그대로 두는 것이지 배율을 지어내는 게 아니다).
	public const float UniformPartyTypeWeightPlaceholder = 1f;

	// 2장: 방 탐색 임무 요구 수량(B+E). 정확한 K·E 값은 미작성 "침입·웨이브" 문서 대기 — 자리표시자
	// (밸런스 미확정). 작게 잡아 짧은 플레이로도 "방금 달성" 전환을 확인할 수 있게 했다.
	public const int RequiredRoomExploreCount = 2;

	// 2장: 몬스터 처치 임무 요구 수량. 정확한 값은 미작성 "침입·웨이브" 문서 대기 — 자리표시자
	// (밸런스 미확정).
	public const int RequiredMonsterKillCount = 5;

	// 2장: 몬스터 종류별 처치 수 반영 배율 — "목표 종류 1.0, 다른 종류 0.5"는 문서의 설정 예시일 뿐
	// 모든 임무의 확정값이 아니고, 애초에 "이번 임무의 목표 종류가 무엇인지" 지정할 임무 데이터 자체가
	// 없다(01번 문서 1장 임무 선택 절차 미구현 — 검증문서 01-01 참고). 그 데이터가 나오기 전까지는
	// 종류 구분 없이 균일 1.0을 적용한다(위 UniformPartyTypeWeightPlaceholder와 동일한 "임의 배율
	// 금지" 원칙). speciesTypeName은 실제 배율표가 생기면 바로 꽂을 수 있도록 미리 받아만 둔다.
	public static float MonsterKillWeight(string speciesTypeName) => 1f;

	// 5장: 선택 점수 = 개인에게 알려진 유효 흥미도 × 적용 가중치 ÷ max(1, 목표까지의 이동 칸수).
	public static float NonCombatGoalScore(float interest, float partyTypeWeight, int distanceTiles)
		=> interest * partyTypeWeight / UnityEngine.Mathf.Max(1, distanceTiles);

	// 6장: 같은 범주에서 대상을 바꿀 때 새 점수가 현재 점수의 1.2배 이상이어야 교체 가능.
	// "현재 0·새 후보 양수"면 20%와 무관하게 교체 가능, "양쪽 0"이면 유지(표의 예시 그대로).
	public static bool ShouldSwitchNonCombatGoal(float currentScore, float newScore)
	{
		if (currentScore <= 0f) return newScore > 0f;
		return CombatScoreMath.MeetsSwitchRatio(currentScore, newScore);
	}
}
