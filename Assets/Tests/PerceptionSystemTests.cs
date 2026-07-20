#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 인지·정보판정·실패처리 시스템(02_인지·정보판정·실패처리_시스템_v0.2)의 숫자 예시를 고정하는
// 테스트. VisionSystemTests.cs/WeightSystemTests.cs와 동일한 컨벤션(NUnit, #if UNITY_INCLUDE_TESTS).
// PerceptionMath는 순수 함수라 Unity 오브젝트 없이 직접 검증 가능하다.
// ========================================================================

public class PerceptionSystemTests
{
	// ── 10장. 경계 반영 감지 보정: 기본은 감지 스탯 그대로, 경계 중이면 +20 ──
	[Test]
	public void DetectionCorrection_AlertAddsTwenty()
	{
		Assert.AreEqual(40f, PerceptionMath.DetectionCorrection(40f, false), 0.001f);
		Assert.AreEqual(60f, PerceptionMath.DetectionCorrection(40f, true), 0.001f);
	}

	// ── 9장. 정신력 6단계 임계값(0~100%를 17/33/50/67/83 경계로 6등분, 2026-07-20 사용자 확인
	// — 기존 MentalErrorState의 25%/50% 기준과는 무관하게 이 문서만으로 새로 정함) ──
	[Test]
	public void GetMentalTier_Boundaries()
	{
		Assert.AreEqual(MentalTier.Panic, PerceptionMath.GetMentalTier(0f));
		Assert.AreEqual(MentalTier.Panic, PerceptionMath.GetMentalTier(16.9f));
		Assert.AreEqual(MentalTier.Fear, PerceptionMath.GetMentalTier(17f));
		Assert.AreEqual(MentalTier.Fear, PerceptionMath.GetMentalTier(32.9f));
		Assert.AreEqual(MentalTier.Tension, PerceptionMath.GetMentalTier(33f));
		Assert.AreEqual(MentalTier.Tension, PerceptionMath.GetMentalTier(49.9f));
		Assert.AreEqual(MentalTier.Stable, PerceptionMath.GetMentalTier(50f));
		Assert.AreEqual(MentalTier.Stable, PerceptionMath.GetMentalTier(66.9f));
		Assert.AreEqual(MentalTier.Calm, PerceptionMath.GetMentalTier(67f));
		Assert.AreEqual(MentalTier.Calm, PerceptionMath.GetMentalTier(82.9f));
		Assert.AreEqual(MentalTier.Excited, PerceptionMath.GetMentalTier(83f));
		Assert.AreEqual(MentalTier.Excited, PerceptionMath.GetMentalTier(100f));
	}

	// ── 9장. 정신력 단계별 가시성 보정표(흥분+10/냉정+5/안정0/긴장-5/공포-15/공황-30) ──
	[Test]
	public void MentalVisibilityCorrection_Table()
	{
		Assert.AreEqual(10f, PerceptionMath.MentalVisibilityCorrection(MentalTier.Excited), 0.001f);
		Assert.AreEqual(5f, PerceptionMath.MentalVisibilityCorrection(MentalTier.Calm), 0.001f);
		Assert.AreEqual(0f, PerceptionMath.MentalVisibilityCorrection(MentalTier.Stable), 0.001f);
		Assert.AreEqual(-5f, PerceptionMath.MentalVisibilityCorrection(MentalTier.Tension), 0.001f);
		Assert.AreEqual(-15f, PerceptionMath.MentalVisibilityCorrection(MentalTier.Fear), 0.001f);
		Assert.AreEqual(-30f, PerceptionMath.MentalVisibilityCorrection(MentalTier.Panic), 0.001f);
	}

	// ── 9장: 몬스터는 정신력 보정 없음, maxMental<=0(스탯 미설정)이면 안정(0) 취급 ──
	[Test]
	public void MentalCorrectionForHuman_UnsetMaxMentalIsStable()
	{
		Assert.AreEqual(0f, PerceptionMath.MentalCorrectionForHuman(0f, 0f), 0.001f);
		Assert.AreEqual(10f, PerceptionMath.MentalCorrectionForHuman(100f, 100f), 0.001f); // 100% → 흥분
		Assert.AreEqual(-30f, PerceptionMath.MentalCorrectionForHuman(0f, 100f), 0.001f);  // 0% → 공황
	}

	// ── 8장. 최종 계산 가시성 = 대상 가시성 + 감지 보정 + 정신력 보정 (예시 그대로 고정) ──
	[Test]
	public void TotalPerceptionVisibility_ExampleFromDoc()
	{
		// 은신 기반 가시성 -30 + 공격후 가시성 증가 +10 + 감지 보정 +30 = 10 (11장 예시,
		// 대상 가시성 자체는 VisionMath.FinalVisibility가 이미 계산해 넘겨준다는 전제)
		Assert.AreEqual(10f, PerceptionMath.TotalPerceptionVisibility(-20f, 30f, 0f), 0.001f);
	}

	// ── 12장. 인지 결과 확률표 구간 경계 ──
	[Test]
	public void OutcomeProbabilities_Table()
	{
		Assert.AreEqual((0.00f, 0.05f, 0.95f), PerceptionMath.OutcomeProbabilities(0f));
		Assert.AreEqual((0.00f, 0.05f, 0.95f), PerceptionMath.OutcomeProbabilities(-10f));
		Assert.AreEqual((0.10f, 0.20f, 0.70f), PerceptionMath.OutcomeProbabilities(1f));
		Assert.AreEqual((0.10f, 0.20f, 0.70f), PerceptionMath.OutcomeProbabilities(19f));
		Assert.AreEqual((0.25f, 0.35f, 0.40f), PerceptionMath.OutcomeProbabilities(20f));
		Assert.AreEqual((0.25f, 0.35f, 0.40f), PerceptionMath.OutcomeProbabilities(39f));
		Assert.AreEqual((0.50f, 0.35f, 0.15f), PerceptionMath.OutcomeProbabilities(40f));
		Assert.AreEqual((0.50f, 0.35f, 0.15f), PerceptionMath.OutcomeProbabilities(59f));
		Assert.AreEqual((0.75f, 0.20f, 0.05f), PerceptionMath.OutcomeProbabilities(60f));
		Assert.AreEqual((0.75f, 0.20f, 0.05f), PerceptionMath.OutcomeProbabilities(79f));
		Assert.AreEqual((0.90f, 0.08f, 0.02f), PerceptionMath.OutcomeProbabilities(80f));
		Assert.AreEqual((0.90f, 0.08f, 0.02f), PerceptionMath.OutcomeProbabilities(200f));
	}

	// ── 12장. 확률표를 실제로 굴리는 RollOutcome — roll01 구간에 따라 정확인지/수상한타일/미인식 분기 ──
	[Test]
	public void RollOutcome_SplitsByRollValue()
	{
		// 가시성 40~59 구간: 정확 50% / 수상 35% / 미인식 15%
		Assert.AreEqual(PerceptionOutcome.AccuratePerception, PerceptionMath.RollOutcome(50f, 0f));
		Assert.AreEqual(PerceptionOutcome.AccuratePerception, PerceptionMath.RollOutcome(50f, 0.49f));
		Assert.AreEqual(PerceptionOutcome.SuspiciousTile, PerceptionMath.RollOutcome(50f, 0.50f));
		Assert.AreEqual(PerceptionOutcome.SuspiciousTile, PerceptionMath.RollOutcome(50f, 0.84f));
		Assert.AreEqual(PerceptionOutcome.Unrecognized, PerceptionMath.RollOutcome(50f, 0.85f));
		Assert.AreEqual(PerceptionOutcome.Unrecognized, PerceptionMath.RollOutcome(50f, 0.999f));
	}

	// ── 21장. 수상한 타일 임시 위험도/흥미도(+5/+5) ──
	[Test]
	public void SuspiciousTileTempWeights_AreFive()
	{
		var record = new PerceptionRecord { Outcome = PerceptionOutcome.SuspiciousTile };
		Assert.AreEqual(5f, record.TempDanger, 0.001f);
		Assert.AreEqual(5f, record.TempInterest, 0.001f);

		record.Outcome = PerceptionOutcome.AccuratePerception;
		Assert.AreEqual(0f, record.TempDanger, 0.001f);
		Assert.AreEqual(0f, record.TempInterest, 0.001f);
	}

	// ── 02문서 6장. 오브젝트 유형별 가시성: 시체/전멸흔적은 BaseVisibility와 무관하게 100 고정 ──
	[Test]
	public void ResolveObjectVisibility_CorpseAndWipeoutAreFixedAt100()
	{
		Assert.AreEqual(100f, VisionMath.ResolveObjectVisibility(0f, new List<string> { "Object/Passable/Corpse", "Human" }), 0.001f);
		Assert.AreEqual(100f, VisionMath.ResolveObjectVisibility(0f, new List<string> { "Object/Passable/WipeoutTrace" }), 0.001f);
		Assert.AreEqual(0f, VisionMath.ResolveObjectVisibility(0f, new List<string> { "Object/Passable/Loot" }), 0.001f);
		Assert.AreEqual(40f, VisionMath.ResolveObjectVisibility(40f, new List<string> { "Object/Passable/Loot" }), 0.001f);
	}
}
#endif
