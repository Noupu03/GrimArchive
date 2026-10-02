#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 히트박스 겹침 비율(Hitbox.CalculateOverlapRatio) — 범위 공격 데미지 배율의 기준값.
// 2026-08-23 규칙: 분모는 "두 히트박스 중 작은 쪽 면적"이다(CLAUDE.md 스킬/공격 시스템 섹션).
//   - 공격 면적으로만 나누면 범위가 넓을수록 데미지가 깎이고(3x3이면 1/9),
//   - 대상 면적으로만 나누면 대형 유닛이 작은 공격에 덜 맞는다.
// 예전 로그 출력용 검증 도구(Script/Unit/Debug/AreaBasedDamageValidator, 2026-10-02 삭제)를 대체한다 —
// 그 도구의 예시는 기대값이 틀려 있었다(아래 OldValidatorExample_* 참고).
// ========================================================================

public class HitboxOverlapTests
{
	private const float Eps = 1e-5f;

	private static Hitbox Box(float cx, float cy, float w, float h)
		=> new Hitbox { center = new Vector2(cx, cy), size = new Vector2(w, h), rotation = 0f };

	[Test]
	public void SameBoxes_FullOverlap_IsOne()
	{
		Assert.AreEqual(1f, Box(0, 0, 1, 1).CalculateOverlapRatio(Box(0, 0, 1, 1)), Eps);
	}

	[Test]
	public void WideAttack_FullyCoveringSmallUnit_IsNotReducedByAttackSize()
	{
		// 3x3 광역 공격이 1x1 유닛을 완전히 덮으면 1.0 — 공격 면적(9)으로 나누던 예전 공식은 1/9였다.
		var attack = Box(0, 0, 3, 3);
		var unit = Box(1, 1, 1, 1);

		Assert.AreEqual(1f, attack.CalculateOverlapArea(unit), Eps);
		Assert.AreEqual(1f, attack.CalculateOverlapRatio(unit), Eps);
	}

	[Test]
	public void SmallAttack_OnLargeUnit_IsNotReducedByUnitSize()
	{
		// 1x1 공격이 3x3 대형 유닛 안에 들어가면 1.0 — 대상 면적으로 나누면 대형 유닛이 덜 맞는다.
		Assert.AreEqual(1f, Box(0, 0, 1, 1).CalculateOverlapRatio(Box(0, 0, 3, 3)), Eps);
	}

	[Test]
	public void PartialOverlap_IsFractionOfTheSmallerArea()
	{
		// 2x2 두 개가 가로로 1칸 어긋남 → 겹침 1x2 = 2, 작은 쪽 면적 4 → 0.5
		var attack = Box(0, 0, 2, 2);
		var unit = Box(1, 0, 2, 2);

		Assert.AreEqual(2f, attack.CalculateOverlapArea(unit), Eps);
		Assert.AreEqual(0.5f, attack.CalculateOverlapRatio(unit), Eps);
	}

	[Test]
	public void Ratio_IsSymmetric()
	{
		var a = Box(0, 0, 3, 3);
		var b = Box(1.5f, 1.5f, 2, 2);

		Assert.AreEqual(a.CalculateOverlapRatio(b), b.CalculateOverlapRatio(a), Eps);
	}

	[Test]
	public void EdgeTouching_IsZero()
	{
		// 4x4(-2~2)와 2x2(2~4)는 가장자리만 닿는다 — 겹침 0.
		Assert.AreEqual(0f, Box(0, 0, 4, 4).CalculateOverlapRatio(Box(3, 3, 2, 2)), Eps);
	}

	[Test]
	public void Separated_IsZero()
	{
		Assert.AreEqual(0f, Box(0, 0, 1, 1).CalculateOverlapRatio(Box(5, 5, 1, 1)), Eps);
	}

	[Test]
	public void ZeroSizedBox_IsZero()
	{
		Assert.AreEqual(0f, Box(0, 0, 0, 0).CalculateOverlapRatio(Box(0, 0, 1, 1)), Eps);
	}

	[Test]
	public void OldValidatorExample_OverlapIsOneNotTwoPointTwoFive()
	{
		// 삭제한 검증 도구의 주석은 "교차 면적 1.5 x 1.5 = 2.25, 비율 2.25/9 = 25%"라고 적었지만,
		// 3x3(-1.5~1.5)와 중심 (1.5, 1.5)의 2x2(0.5~2.5)는 1x1 = 1만 겹친다. 비율은 작은 쪽 면적 4 기준 25%.
		var a = Box(0, 0, 3, 3);
		var b = Box(1.5f, 1.5f, 2, 2);

		Assert.AreEqual(1f, a.CalculateOverlapArea(b), Eps);
		Assert.AreEqual(0.25f, a.CalculateOverlapRatio(b), Eps);
	}
}
#endif
