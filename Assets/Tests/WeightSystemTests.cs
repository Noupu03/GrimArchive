#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 대표 가중치 3종(이해도/위험도/흥미도) 연산공식 문서 v0.7의 숫자 예시를 그대로 고정하는 테스트.
// CreateMapPlayTests.cs와 동일한 컨벤션(NUnit, #if UNITY_INCLUDE_TESTS)을 따른다.
// WeightMath는 순수 함수라 Unity 오브젝트 없이 직접 검증 가능하다.
// ========================================================================

public class WeightSystemTests
{
	// ── 4장. 개인 가중치 계산 예시: 2.0 + 3 = 5.0, 적용값 5 ──
	[Test]
	public void PersonalWeightChange_Example()
	{
		float result = WeightMath.ApplyPersonalChange(2.0f, 3f, WeightType.Danger);
		Assert.AreEqual(5.0f, result, 0.0001f);
		Assert.AreEqual(5, WeightMath.AppliedValue(result));
	}

	// ── 6-1장. 전역 반영 비율 예시 1~3 ──
	[Test]
	public void ReflectionRatio_Example1_WitnessAndIndirect()
	{
		var ratios = WeightMath.ComputeReflectionRatios(new[] { InfoType.DirectWitness, InfoType.Indirect });
		Assert.AreEqual(0.6f, ratios[InfoType.DirectWitness], 0.001f);
		Assert.AreEqual(0.4f, ratios[InfoType.Indirect], 0.001f);
	}

	[Test]
	public void ReflectionRatio_Example2_ExperienceAndIndirect()
	{
		var ratios = WeightMath.ComputeReflectionRatios(new[] { InfoType.DirectExperience, InfoType.Indirect });
		Assert.AreEqual(0.71f, ratios[InfoType.DirectExperience], 0.001f);
		Assert.AreEqual(0.29f, ratios[InfoType.Indirect], 0.001f);
	}

	[Test]
	public void ReflectionRatio_Example3_WitnessOnly()
	{
		var ratios = WeightMath.ComputeReflectionRatios(new[] { InfoType.DirectWitness });
		Assert.AreEqual(1.0f, ratios[InfoType.DirectWitness], 0.001f);
	}

	// ── 6-2장. 동일 수치 정보 중복 제거 예시: B,C(직접목격+3) 중복, D(간접파악+1.5)는 남음 ──
	[Test]
	public void Dedup_IdenticalEntries_CollapseToOne()
	{
		var entries = new List<IncidentEntry>
		{
			new IncidentEntry("INCIDENT_001", EventId.E_HUMAN_KILL_SEEN, "Monster_A", WeightType.Danger, InfoType.DirectWitness, 3f, MentalErrorState.Normal, "B"),
			new IncidentEntry("INCIDENT_001", EventId.E_HUMAN_KILL_SEEN, "Monster_A", WeightType.Danger, InfoType.DirectWitness, 3f, MentalErrorState.Normal, "C"),
			new IncidentEntry("INCIDENT_001", EventId.E_HUMAN_KILL_INDIRECT, "Monster_A", WeightType.Danger, InfoType.Indirect, 1.5f, MentalErrorState.Normal, "D"),
		};
		var deduped = WeightMath.DedupExact(entries);
		Assert.AreEqual(2, deduped.Count);
	}

	// ── 6-3장. 정신력 상태로 인한 수치 오차 예시: (3 + 3.2) / 2 = 3.1 ──
	[Test]
	public void GlobalReflection_PanicNoiseAveraging_Example()
	{
		var group = new List<IncidentEntry>
		{
			new IncidentEntry("INCIDENT_020", EventId.E_HUMAN_KILL_SEEN, "Monster_A", WeightType.Danger, InfoType.DirectWitness, 3f, MentalErrorState.Normal, "B"),
			new IncidentEntry("INCIDENT_020", EventId.E_HUMAN_KILL_SEEN, "Monster_A", WeightType.Danger, InfoType.DirectWitness, 3f, MentalErrorState.Normal, "C"),
			new IncidentEntry("INCIDENT_020", EventId.E_HUMAN_KILL_SEEN, "Monster_A", WeightType.Danger, InfoType.DirectWitness, 3.2f, MentalErrorState.Fear, "D"),
		};
		// 이 경우 직접 목격만 존재하므로 전역 반영량 = 대표변화량(3.1) 그대로 (비율 3/3=1.0)
		float amount = WeightMath.ComputeGlobalReflectionAmount(group);
		Assert.AreEqual(3.1f, amount, 0.001f);
	}

	// ── 6-4장 예시 1. 직접경험/직접목격/간접파악 모두 존재 → 0.21 ──
	[Test]
	public void GlobalReflection_Example1_AllThreeTypes()
	{
		var group = new List<IncidentEntry>
		{
			new IncidentEntry("INCIDENT_010", EventId.E_HIT_HEAVY_SELF, "Monster_A", WeightType.Danger, InfoType.DirectExperience, 0.3f, MentalErrorState.Normal, "B"),
			new IncidentEntry("INCIDENT_010", EventId.E_HIT_HEAVY_SEEN, "Monster_A", WeightType.Danger, InfoType.DirectWitness, 0.15f, MentalErrorState.Normal, "C"),
			new IncidentEntry("INCIDENT_010", EventId.E_HIT_HEAVY_INDIRECT, "Monster_A", WeightType.Danger, InfoType.Indirect, 0.075f, MentalErrorState.Normal, "D"),
		};
		float amount = WeightMath.ComputeGlobalReflectionAmount(group);
		Assert.AreEqual(0.21f, amount, 0.001f);
		Assert.AreEqual(0, WeightMath.AppliedValue(amount)); // 적용값은 소수점 버림 -> 0
	}

	// ── 6-4장 예시 2. 직접목격+간접파악만 (직접목격 중복 제거) → 2.4 ──
	[Test]
	public void GlobalReflection_Example2_WitnessAndIndirectWithDedup()
	{
		var group = new List<IncidentEntry>
		{
			new IncidentEntry("INCIDENT_011", EventId.E_HUMAN_KILL_SEEN, "Monster_A", WeightType.Danger, InfoType.DirectWitness, 3f, MentalErrorState.Normal, "B"),
			new IncidentEntry("INCIDENT_011", EventId.E_HUMAN_KILL_SEEN, "Monster_A", WeightType.Danger, InfoType.DirectWitness, 3f, MentalErrorState.Normal, "C"),
			new IncidentEntry("INCIDENT_011", EventId.E_HUMAN_KILL_INDIRECT, "Monster_A", WeightType.Danger, InfoType.Indirect, 1.5f, MentalErrorState.Normal, "D"),
		};
		float amount = WeightMath.ComputeGlobalReflectionAmount(group);
		Assert.AreEqual(2.4f, amount, 0.001f);
		Assert.AreEqual(2, WeightMath.AppliedValue(amount));
	}

	// ── 7장. 이해도 증가 예시: 0.9 + 0.1 = 1.0, 적용값 1 ──
	[Test]
	public void UnderstandingIncrease_Example()
	{
		float result = WeightMath.ApplyPersonalChange(0.9f, 0.1f, WeightType.Understanding);
		Assert.AreEqual(1.0f, result, 0.0001f);
		Assert.AreEqual(1, WeightMath.AppliedValue(result));
	}

	// ── 7-1장. 특수 유닛 이해도 = min(종별,50) + min(개별,50) ──
	[Test]
	public void SpecialUnitUnderstanding_CapsAt50Each()
	{
		Assert.AreEqual(100f, WeightMath.ComposeUnderstanding(70f, 60f, true), 0.001f); // 50+50 캡
		Assert.AreEqual(55f, WeightMath.ComposeUnderstanding(30f, 25f, true), 0.001f); // 캡 안걸림
		Assert.AreEqual(70f, WeightMath.ComposeUnderstanding(70f, 60f, false), 0.001f); // 일반 유닛은 종별만
	}

	// ── 8장. 이해도 총량 한도/감소 예시: 초과량2, 하한6, 가능량14, 실제감소2 ──
	[Test]
	public void UnderstandingPoolDecrease_Example()
	{
		var result = WeightMath.ComputePoolDecrease(199f, 3f, 200f, 20f);
		Assert.AreEqual(2f, result.OverflowAmount, 0.001f);
		Assert.AreEqual(6f, result.DecreaseFloor, 0.001f);
		Assert.AreEqual(14f, result.DecreaseCapacity, 0.001f);
		Assert.AreEqual(2f, result.ActualDecrease, 0.001f);
	}

	[Test]
	public void UnderstandingPoolDecrease_NoOverflow_NoDecrease()
	{
		var result = WeightMath.ComputePoolDecrease(100f, 3f, 200f, 20f);
		Assert.AreEqual(0f, result.ActualDecrease, 0.001f);
	}

	// ── 9장. 미표기 내부 정보 오차 예시: 실제100, 이해도35 -> ±30%, 인식범위 70~130 ──
	[Test]
	public void HiddenInfoNoise_Example()
	{
		Assert.AreEqual(0.3f, WeightMath.HiddenInfoNoiseRatio(35), 0.0001f);

		var rng = new System.Random(1);
		for (int i = 0; i < 50; i++)
		{
			int noisy = WeightMath.ApplyHiddenInfoNoise(100, 35, rng);
			Assert.GreaterOrEqual(noisy, 70);
			Assert.LessOrEqual(noisy, 130);
		}
	}

	// ── 10장. 위험도 증가 예시: 2.0 + 0.3 = 2.3, 적용값 2 ──
	[Test]
	public void DangerIncrease_Example()
	{
		float result = WeightMath.ApplyPersonalChange(2.0f, 0.3f, WeightType.Danger);
		Assert.AreEqual(2.3f, result, 0.0001f);
		Assert.AreEqual(2, WeightMath.AppliedValue(result));
	}

	// ── 11-1장. 총 피해량 기준 감소 판정 예시: 2 - 12 = -10, 충족 ──
	[Test]
	public void TotalDamageDecrease_Example()
	{
		bool qualifies = WeightMath.TotalDamageDecreaseQualifies(2f, 12f, out float judgement);
		Assert.AreEqual(-10f, judgement, 0.001f);
		Assert.IsTrue(qualifies);
	}

	// ── 11-2장. 타격 1회 기준 판정 ──
	[Test]
	public void PerHitDecrease_QualifiesWhenDiffAtLeastOne()
	{
		Assert.IsTrue(WeightMath.PerHitDecreaseQualifies(12f, 10f, out float diff));
		Assert.AreEqual(2f, diff, 0.001f);
		Assert.IsFalse(WeightMath.PerHitDecreaseQualifies(12f, 11.5f, out _));
	}

	// ── 11장. 위험도 감소 하한 ──
	[Test]
	public void DangerDecrease_RespectsFloor()
	{
		Assert.AreEqual(3f, WeightMath.ApplyDangerDecrease(3.5f, -1f, false), 0.001f); // 일반 유닛 하한 3
		Assert.AreEqual(10f, WeightMath.ApplyDangerDecrease(10.2f, -1f, true), 0.001f); // 보스/네메시스 하한 10
	}

	// ── 12장. 위험도 단계 경계값 ──
	[Test]
	public void DangerStage_Boundaries()
	{
		Assert.AreEqual(DangerStage.Stage0, WeightMath.GetDangerStage(0));
		Assert.AreEqual(DangerStage.Stage0, WeightMath.GetDangerStage(99));
		Assert.AreEqual(DangerStage.Stage1, WeightMath.GetDangerStage(100));
		Assert.AreEqual(DangerStage.Stage1, WeightMath.GetDangerStage(299));
		Assert.AreEqual(DangerStage.Stage2, WeightMath.GetDangerStage(300));
		Assert.AreEqual(DangerStage.Stage2, WeightMath.GetDangerStage(599));
		Assert.AreEqual(DangerStage.Stage3, WeightMath.GetDangerStage(600));
		Assert.AreEqual(DangerStage.Stage3, WeightMath.GetDangerStage(799));
		Assert.AreEqual(DangerStage.StageMax, WeightMath.GetDangerStage(800));
		Assert.AreEqual(DangerStage.StageMax, WeightMath.GetDangerStage(999));
	}

	// ── 12-1장. 최종 위험도 = 기본 + 종별 + 개별 ──
	[Test]
	public void FinalDanger_Composition()
	{
		float result = WeightMath.ComposeFinalDanger(baseDanger: 10f, speciesAccumulated: 640f, individualAccumulated: 480f);
		Assert.AreEqual(999f, result, 0.001f); // 999 클램프 (10+640+480=1130 -> 999)
	}

	// ── 17장. 오브젝트 흥미도 감소/복구 예시 ──
	[Test]
	public void ObjectInterest_InvestigateAndDrop_Examples()
	{
		Assert.AreEqual(60f, WeightMath.ObjectInterestAfterInvestigate(120f), 0.001f);
		Assert.AreEqual(60f, WeightMath.ObjectInterestAfterDrop(120f), 0.001f);
	}

	// ── 16장. 타일 흥미도 예시: 미탐사5 + 오브젝트120 = 125 ──
	[Test]
	public void TileInterest_Example()
	{
		Assert.AreEqual(125f, WeightMath.ComposeTileInterest(5f, 120f), 0.001f);
	}

	// ── 18장. 유닛 흥미도-이해도 관계 예시: 100 * 65% = 65 ──
	[Test]
	public void UnitInterestFromUnderstanding_Example()
	{
		Assert.AreEqual(65f, WeightMath.UnitInterestFromUnderstanding(100f, 35), 0.001f);
	}

	// ── 19장 / 13-2장. 위험도 단계 보정값 ──
	[Test]
	public void DangerStageBonus_Table()
	{
		Assert.AreEqual(0f, WeightMath.DangerStageBonus(DangerStage.Stage0));
		Assert.AreEqual(5f, WeightMath.DangerStageBonus(DangerStage.Stage1));
		Assert.AreEqual(10f, WeightMath.DangerStageBonus(DangerStage.Stage2));
		Assert.AreEqual(15f, WeightMath.DangerStageBonus(DangerStage.Stage3));
		Assert.AreEqual(20f, WeightMath.DangerStageBonus(DangerStage.StageMax));
	}

	// ── 23장. 정보 오차 예시: 간접파악, 실제100 -> ±10%, 인식범위 90~110 ──
	[Test]
	public void InfoError_IndirectExample()
	{
		var (tileError, valueRatio) = WeightMath.InfoError(InfoType.Indirect, MentalErrorState.Normal);
		Assert.AreEqual(1, tileError);
		Assert.AreEqual(0.10f, valueRatio, 0.0001f);

		var rng = new System.Random(2);
		for (int i = 0; i < 50; i++)
		{
			int noisy = WeightMath.ApplyValueNoise(100, valueRatio, rng);
			Assert.GreaterOrEqual(noisy, 90);
			Assert.LessOrEqual(noisy, 110);
		}
	}

	[Test]
	public void InfoError_MentalStateOverridesWhenLarger()
	{
		// 직접 목격(±5%)이라도 공황 단계(±25%)가 더 크면 공황 쪽을 채택
		var (tileError, valueRatio) = WeightMath.InfoError(InfoType.DirectWitness, MentalErrorState.Panic);
		Assert.AreEqual(2, tileError);
		Assert.AreEqual(0.25f, valueRatio, 0.0001f);
	}

	// ── 25장. 공황 단계 정보 평균 처리 예시: 140*0.3 + 100*0.7 = 112 ──
	[Test]
	public void PanicBlend_Example()
	{
		float result = WeightMath.BlendPanicValue(140f, 100f);
		Assert.AreEqual(112f, result, 0.001f);
	}

	// ── 13-1장. 파티 전멸 던전 위험도 증가값 ──
	[Test]
	public void PartyWipeout_DungeonDangerIncrease()
	{
		var kb = new HumanKnowledgeBase();
		kb.OnPartyWipeout();
		Assert.AreEqual(WeightMath.PartyWipeoutDungeonDangerIncrease, kb.GetDungeonDanger(), 0.001f);
	}

	// ── 13-2장. 전멸 흔적 발견 전역 반영값 = 25 + 단계보정, IncidentID당 1회만 반영 ──
	[Test]
	public void WipeoutTrace_ReflectsOncePerTraceId()
	{
		var kb = new HumanKnowledgeBase();
		string traceId = kb.RegisterWipeoutTrace(DangerStage.Stage2); // +10 보정
		kb.OnWipeoutTraceReflected(traceId);
		kb.OnWipeoutTraceReflected(traceId); // 중복 호출 — 반영되면 안 됨
		Assert.AreEqual(35f, kb.GetDungeonDanger(), 0.001f); // 25 + 10
	}
}
#endif
