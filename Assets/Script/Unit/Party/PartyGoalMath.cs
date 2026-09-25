// 01번 문서(파티 목표와 비전투 목표 선택) 순수 계산 함수 — WeightMath.cs/VisionMath.cs와 동일 컨벤션.
public static class PartyGoalMath
{
	// 5장: 선택 점수 = 개인에게 알려진 유효 흥미도 × 적용 가중치 ÷ max(1, 목표까지의 이동 칸수).
	public static float NonCombatGoalScore(float interest, float partyTypeWeight, int distanceTiles)
		=> interest * partyTypeWeight / UnityEngine.Mathf.Max(1, distanceTiles);

	// 6장: 같은 범주에서 대상을 바꿀 때 새 점수가 현재 점수의 1.2배 이상이어야 교체 가능.
	// "현재 0·새 후보 양수"면 20%와 무관하게 교체 가능, "양쪽 0"이면 유지(표의 예시 그대로).
	public static bool ShouldSwitchNonCombatGoal(float currentScore, float newScore)
	{
		if (currentScore <= 0f) return newScore > 0f;
		return newScore >= currentScore * 1.2f;
	}
}
