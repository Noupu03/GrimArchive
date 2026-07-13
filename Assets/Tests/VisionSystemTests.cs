#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 시야-인지-반응 문서(01_시야·인지범위·가시성_개념, 01-A_...연산공식, v0.2)의 숫자 예시를 그대로
// 고정하는 테스트. WeightSystemTests.cs와 동일한 컨벤션(NUnit, #if UNITY_INCLUDE_TESTS).
// VisionMath는 순수 함수라 Unity 오브젝트 없이 직접 검증 가능하다.
// ========================================================================

public class VisionSystemTests
{
	// ── 01-A 2장. 시야 거리 표 (기본 6칸 + 감지 20마다 +1칸, 100에서 11칸) ──
	[Test]
	public void ViewDistance_Table()
	{
		Assert.AreEqual(6, VisionMath.ViewDistance(0));
		Assert.AreEqual(6, VisionMath.ViewDistance(19));
		Assert.AreEqual(7, VisionMath.ViewDistance(20));
		Assert.AreEqual(7, VisionMath.ViewDistance(39));
		Assert.AreEqual(8, VisionMath.ViewDistance(40));
		Assert.AreEqual(9, VisionMath.ViewDistance(60));
		Assert.AreEqual(10, VisionMath.ViewDistance(80));
		Assert.AreEqual(11, VisionMath.ViewDistance(100));
	}

	// ── 01-A 3장. 인지 거리 표 (기본 4칸 + 감지 20마다 +1칸, 100에서 9칸) ──
	[Test]
	public void AwarenessDistance_Table()
	{
		Assert.AreEqual(4, VisionMath.AwarenessDistance(0));
		Assert.AreEqual(4, VisionMath.AwarenessDistance(19));
		Assert.AreEqual(5, VisionMath.AwarenessDistance(20));
		Assert.AreEqual(6, VisionMath.AwarenessDistance(40));
		Assert.AreEqual(7, VisionMath.AwarenessDistance(60));
		Assert.AreEqual(8, VisionMath.AwarenessDistance(80));
		Assert.AreEqual(9, VisionMath.AwarenessDistance(100));
	}

	// ── 01-A 4장. 인지각 표 (기본 60도 + 감지 10마다 6도, 최대 120도) ──
	[Test]
	public void AwarenessAngle_Table()
	{
		Assert.AreEqual(60f, VisionMath.AwarenessAngle(0), 0.001f);
		Assert.AreEqual(60f, VisionMath.AwarenessAngle(9), 0.001f);
		Assert.AreEqual(66f, VisionMath.AwarenessAngle(10), 0.001f);
		Assert.AreEqual(90f, VisionMath.AwarenessAngle(50), 0.001f);
		Assert.AreEqual(114f, VisionMath.AwarenessAngle(99), 0.001f);
		Assert.AreEqual(120f, VisionMath.AwarenessAngle(100), 0.001f);
	}

	[Test]
	public void AwarenessAngle_MaxedOut_MeansFullVisionConeIsPerceptionCone()
	{
		Assert.IsFalse(VisionMath.IsAwarenessAngleMaxed(99));
		Assert.IsTrue(VisionMath.IsAwarenessAngleMaxed(100));
	}

	// ── 01-A 7장. 시야 범위 내 임시 위험도/흥미도 (+5, 빈 타일은 0) ──
	[Test]
	public void TempWeightForVisionOnlyTile_NonEmptyIsFive()
	{
		Assert.AreEqual(5f, VisionMath.TempWeightForVisionOnlyTile(true), 0.001f);
		Assert.AreEqual(0f, VisionMath.TempWeightForVisionOnlyTile(false), 0.001f);
	}

	// ── 01장 9절. 대상 기본 가시성: 벽 0, 일반 타일 100, 점유자가 있으면 그 값으로 무효화 ──
	[Test]
	public void ResolveBaseVisibility_WallFloorAndOccupant()
	{
		Assert.AreEqual(0f, VisionMath.ResolveBaseVisibility(true, null), 0.001f);
		Assert.AreEqual(100f, VisionMath.ResolveBaseVisibility(false, null), 0.001f);
		Assert.AreEqual(20f, VisionMath.ResolveBaseVisibility(true, 20f), 0.001f); // 벽이어도 점유자 값이 우선
	}

	// ── 01-A 9장. 최종 가시성 = 기본 - 은신 + 공격후 상승분(진행중일 때만), 0~100 클램프 ──
	[Test]
	public void FinalVisibility_StealthAndAttackBoost()
	{
		Assert.AreEqual(100f, VisionMath.FinalVisibility(100f, 0f, false), 0.001f);
		Assert.AreEqual(70f, VisionMath.FinalVisibility(100f, 30f, false), 0.001f);
		Assert.AreEqual(60f, VisionMath.FinalVisibility(50f, 0f, true), 0.001f);
		// 이미 100인 대상은 공격 상승분(+10)이 있어도 100을 넘지 않는다(01-A 9장 "가시성 최대치 100").
		Assert.AreEqual(100f, VisionMath.FinalVisibility(100f, 0f, true), 0.001f);
	}

	// ── 01-A 13장. 특수 원형 인지 범위 반지름 표 (1/2/3칸) ──
	[Test]
	public void CircularPerceptionRadius_Table()
	{
		Assert.AreEqual(1, VisionMath.CircularPerceptionRadius(0));
		Assert.AreEqual(1, VisionMath.CircularPerceptionRadius(29));
		Assert.AreEqual(2, VisionMath.CircularPerceptionRadius(30));
		Assert.AreEqual(2, VisionMath.CircularPerceptionRadius(59));
		Assert.AreEqual(3, VisionMath.CircularPerceptionRadius(60));
		Assert.AreEqual(3, VisionMath.CircularPerceptionRadius(100));
	}

	// ── 01-A 11장. 기습 방향 전환 고위협 기준: 근접 공격 기대값의 2배 이상 ──
	[Test]
	public void IsHighThreatSurprise_TwiceMeleeExpectedDamage()
	{
		Assert.IsTrue(VisionMath.IsHighThreatSurprise(20f, 10f));
		Assert.IsFalse(VisionMath.IsHighThreatSurprise(19f, 10f));
	}

	// ── 01-A 11장. 우선순위 랭크 순서: 플레이어명령(0) < 스킬(1) < ... < 이동(10) ──
	[Test]
	public void PriorityRank_Ordering()
	{
		Assert.Less(VisionMath.PriorityRank(VisionDirectionReason.PlayerCommand), VisionMath.PriorityRank(VisionDirectionReason.SkillUse));
		Assert.Less(VisionMath.PriorityRank(VisionDirectionReason.SkillUse), VisionMath.PriorityRank(VisionDirectionReason.AdjacentMeleeTarget));
		Assert.Less(VisionMath.PriorityRank(VisionDirectionReason.CurrentAttackTarget), VisionMath.PriorityRank(VisionDirectionReason.Alert));
		Assert.Less(VisionMath.PriorityRank(VisionDirectionReason.Alert), VisionMath.PriorityRank(VisionDirectionReason.Moving));
	}

	// ── 01-A 11장 예시: 이동 중(10순위)이어도 현재 공격 대상(4순위)이 있으면 공격 대상 방향이 이긴다 ──
	[Test]
	public void ResolveVisionDirection_HigherPriorityWins()
	{
		var candidates = new List<VisionMath.VisionDirectionCandidate>
		{
			new VisionMath.VisionDirectionCandidate(VisionDirectionReason.Moving, Dir.DOWN),
			new VisionMath.VisionDirectionCandidate(VisionDirectionReason.CurrentAttackTarget, Dir.RIGHT),
		};
		Assert.AreEqual(Dir.RIGHT, VisionMath.ResolveVisionDirection(candidates, Dir.DOWN));
	}

	// ── 01-A 11장 마지막 규칙: 동일 우선순위 후보가 여럿이면 기존 시야 방향에 가장 가까운 쪽을 채택 ──
	[Test]
	public void ResolveVisionDirection_TieBreak_ClosestToCurrentDirection()
	{
		var candidates = new List<VisionMath.VisionDirectionCandidate>
		{
			new VisionMath.VisionDirectionCandidate(VisionDirectionReason.CurrentAttackTarget, Dir.UP_RIGHT),  // UP에서 1단계
			new VisionMath.VisionDirectionCandidate(VisionDirectionReason.CurrentAttackTarget, Dir.DOWN_LEFT), // UP에서 3단계
		};
		Assert.AreEqual(Dir.UP_RIGHT, VisionMath.ResolveVisionDirection(candidates, Dir.UP));
	}

	[Test]
	public void ResolveVisionDirection_NoCandidates_KeepsCurrentDirection()
	{
		Assert.AreEqual(Dir.LEFT, VisionMath.ResolveVisionDirection(new List<VisionMath.VisionDirectionCandidate>(), Dir.LEFT));
	}

	// ── Dir 8방향 회전 거리 (동률 판정의 기반) ──
	[Test]
	public void DirStepDistance_WrapsAroundCircle()
	{
		Assert.AreEqual(1, VisionMath.DirStepDistance(Dir.UP, Dir.UP_RIGHT));
		Assert.AreEqual(4, VisionMath.DirStepDistance(Dir.UP, Dir.DOWN)); // 정반대(4단계, 최대값)
		Assert.AreEqual(1, VisionMath.DirStepDistance(Dir.UP, Dir.UP_LEFT)); // 반대 방향으로 돌아도 1단계
	}
}
#endif
