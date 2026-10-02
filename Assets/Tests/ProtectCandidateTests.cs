#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

// 02번 8·9장 긴급 보호 — 치명적 공격 예상 조건과 후보 비교 순서(순수 함수, 검증 02-08·02-09).
public class ProtectCandidateTests
{
	private static CombatScoreMath.ProtectCandidateKey Key(bool lethal, float eta, float hp, bool incap, float dist)
		=> new CombatScoreMath.ProtectCandidateKey(lethal, eta, hp, incap, dist);

	[Test]
	public void LethalAttackExpected_IsAnEmergencyEvenAtFullHpWithNoOtherCondition()
	{
		Assert.IsTrue(CombatScoreMath.IsEmergencyProtectCandidate(1.0f, false, false, false, true));
		Assert.IsFalse(CombatScoreMath.IsEmergencyProtectCandidate(1.0f, false, false, false, false));
	}

	[Test]
	public void ExistingConditions_StillApplyWithoutLethal()
	{
		Assert.IsTrue(CombatScoreMath.IsEmergencyProtectCandidate(0.30f, true, false, false, false));   // HP 30% 이하 + 위협
		Assert.IsFalse(CombatScoreMath.IsEmergencyProtectCandidate(0.31f, true, false, false, false));  // 30% 초과는 위협만으로 부족
		Assert.IsTrue(CombatScoreMath.IsEmergencyProtectCandidate(0.9f, false, true, true, false));     // 행동불능 + 피격
		Assert.IsFalse(CombatScoreMath.IsEmergencyProtectCandidate(0.9f, false, true, false, false));   // 행동불능만으로는 부족
	}

	[Test]
	public void DamageAfterDefense_SubtractsDefenseAndFloorsAtOne()
	{
		Assert.AreEqual(40f, CombatScoreMath.DamageAfterDefense(50f, 10f), 0.0001f);
		Assert.AreEqual(1f, CombatScoreMath.DamageAfterDefense(5f, 10f), 0.0001f);
		Assert.AreEqual(1f, CombatScoreMath.DamageAfterDefense(10f, 10f), 0.0001f);
	}

	[Test]
	public void IsLethalAttack_BoundaryIsInclusive_AndUsesDamageAfterDefense()
	{
		Assert.IsTrue(CombatScoreMath.IsLethalAttack(50f, 10f, 40f));   // 방어 후 40 ≥ 남은 HP 40
		Assert.IsFalse(CombatScoreMath.IsLethalAttack(50f, 10f, 41f));
		Assert.IsTrue(CombatScoreMath.IsLethalAttack(5f, 10f, 1f));     // 방어가 더 커도 최소 1
		Assert.IsFalse(CombatScoreMath.IsLethalAttack(5f, 10f, 2f));
	}

	[Test]
	public void Compare_LethalBeatsLowerHp()
	{
		var lethalFullHp = Key(true, 1.0f, 1.0f, false, 5f);
		var nonLethalLowHp = Key(false, 0f, 0.1f, false, 1f);
		Assert.IsTrue(CombatScoreMath.IsBetterProtectCandidate(lethalFullHp, nonLethalLowHp));
		Assert.IsFalse(CombatScoreMath.IsBetterProtectCandidate(nonLethalLowHp, lethalFullHp));
	}

	[Test]
	public void Compare_BothLethal_EarlierArrivalWinsEvenWithHigherHp()
	{
		var sooner = Key(true, 0.5f, 0.9f, false, 9f);
		var later = Key(true, 1.5f, 0.1f, true, 1f);
		Assert.IsTrue(CombatScoreMath.IsBetterProtectCandidate(sooner, later));
		Assert.IsFalse(CombatScoreMath.IsBetterProtectCandidate(later, sooner));
	}

	[Test]
	public void Compare_FallsThroughHpThenIncapacitatedThenDistance()
	{
		Assert.IsTrue(CombatScoreMath.IsBetterProtectCandidate(Key(true, 1f, 0.2f, false, 9f), Key(true, 1f, 0.3f, true, 1f)));    // 도달 시점 동률 → HP 낮음
		Assert.IsTrue(CombatScoreMath.IsBetterProtectCandidate(Key(false, 0f, 0.2f, true, 9f), Key(false, 0f, 0.2f, false, 1f)));  // HP 동률 → 행동불능
		Assert.IsTrue(CombatScoreMath.IsBetterProtectCandidate(Key(false, 0f, 0.2f, false, 2f), Key(false, 0f, 0.2f, false, 5f))); // 그래도 동률 → 가까움
	}

	[Test]
	public void Compare_NonLethalIgnoresEtaField_AndFullTieIsNotBetter()
	{
		var a = Key(false, 3f, 0.2f, false, 2f); // 치명적이 아니면 eta는 0으로 정규화돼 비교·동률 판정에서 빠진다
		var b = Key(false, 9f, 0.2f, false, 2f);
		Assert.IsFalse(CombatScoreMath.IsBetterProtectCandidate(a, b));
		Assert.IsFalse(CombatScoreMath.IsBetterProtectCandidate(b, a));
		Assert.IsTrue(a.Equals(b));
	}
}
#endif
