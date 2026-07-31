#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 07_전파·소리·간접입력 문서(개념 v0.2 / 연산공식 v0.2)의 숫자 예시를 그대로 고정하는 테스트.
// VisionSystemTests.cs와 동일한 컨벤션(NUnit, #if UNITY_INCLUDE_TESTS). PropagationMath는 순수
// 함수라 Unity 오브젝트 없이 직접 검증 가능하다.
// ========================================================================

public class PropagationSystemTests
{
	// ── 1장. 공간 판정 ──
	[Test]
	public void SameSpace_RoomIdEquality()
	{
		Assert.IsTrue(PropagationMath.SameSpace(3, 3));
		Assert.IsFalse(PropagationMath.SameSpace(3, 4));
		Assert.IsFalse(PropagationMath.SameSpace(-1, -1)); // 방 밖(-1)은 공간 자체가 아님
	}

	// ── 1-2장. 거리 계산: 장애물 없음 → 직선 경로 그대로 성공 ──
	[Test]
	public void SpaceDistance_NoObstacle_FindsDirectPath()
	{
		bool ok = PropagationMath.TryGetSpaceDistance(Vector2Int.zero, new Vector2Int(2, 0), 3, _ => true, out int dist);
		Assert.IsTrue(ok);
		Assert.AreEqual(2, dist);
	}

	// ── 1-2장 예시: 직선거리 2칸이지만 벽을 돌아 실제 최단 경로가 4칸이면 범위3은 실패, 범위4는 성공 ──
	[Test]
	public void SpaceDistance_WallDetour_MatchesDocExample()
	{
		// (1,0)과 (1,1)을 막아 (0,0)→(2,0) 직선을 봉쇄, 위로 돌아가는 경로만 허용.
		bool IsWalkable(Vector2Int p) => !(p == new Vector2Int(1, 0) || p == new Vector2Int(1, 1));

		bool okRange3 = PropagationMath.TryGetSpaceDistance(Vector2Int.zero, new Vector2Int(2, 0), 3, IsWalkable, out _);
		Assert.IsFalse(okRange3);

		bool okRange4 = PropagationMath.TryGetSpaceDistance(Vector2Int.zero, new Vector2Int(2, 0), 4, IsWalkable, out int dist4);
		Assert.IsTrue(okRange4);
		Assert.AreEqual(4, dist4);
	}

	[Test]
	public void SpaceDistance_Unreachable_Fails()
	{
		bool ok = PropagationMath.TryGetSpaceDistance(Vector2Int.zero, new Vector2Int(5, 5), 3, _ => true, out _);
		Assert.IsFalse(ok);
	}

	// ── 2-1장. 인류 전파 범위 = 기본 3칸 + 카리스마 보정(25당 +1, 100에서 +4) ──
	[Test]
	public void PropagationRange_CharismaTable()
	{
		Assert.AreEqual(3, PropagationMath.PropagationRange(0));
		Assert.AreEqual(3, PropagationMath.PropagationRange(24));
		Assert.AreEqual(4, PropagationMath.PropagationRange(25));
		Assert.AreEqual(4, PropagationMath.PropagationRange(49));
		Assert.AreEqual(5, PropagationMath.PropagationRange(50));
		Assert.AreEqual(6, PropagationMath.PropagationRange(75));
		Assert.AreEqual(7, PropagationMath.PropagationRange(100));
	}

	// ── 4장. 소리 기본 범위 6종 ──
	[Test]
	public void SoundBaseRange_Table()
	{
		Assert.AreEqual(2, PropagationMath.SoundBaseRange(SoundType.Movement));
		Assert.AreEqual(3, PropagationMath.SoundBaseRange(SoundType.AttackExecution));
		Assert.AreEqual(5, PropagationMath.SoundBaseRange(SoundType.HitImpact));
		Assert.AreEqual(6, PropagationMath.SoundBaseRange(SoundType.HitScream));
		Assert.AreEqual(7, PropagationMath.SoundBaseRange(SoundType.Death));
		Assert.AreEqual(5, PropagationMath.SoundBaseRange(SoundType.TrapActivation));
	}

	// ── 4-1장. 피격 비명 발생 기준: 최종 HP 감소량 ≥ 최대 HP × 10% ──
	[Test]
	public void HitScream_ThresholdExample()
	{
		Assert.IsTrue(PropagationMath.IsHitScreamTriggered(10f, 100f));
		Assert.IsFalse(PropagationMath.IsHitScreamTriggered(9.99f, 100f));
	}

	// ── 5장. 소리 감지 범위 = 소리 기본 범위 + 감지 보정(25당 +1) ──
	[Test]
	public void SoundDetectionRange_HumanNoAlert()
	{
		// 이동음(기본2) + 감지50(+2) = 4
		Assert.AreEqual(4, PropagationMath.SoundDetectionRange(SoundType.Movement, 50f, isAlert: false, isMonster: false));
	}

	// ── 5-1장. 경계 상태: 감지 = min(기본+20, 100)을 먼저 적용한 뒤 범위 보정 ──
	[Test]
	public void SoundDetectionRange_AlertAppliesBonusBeforeBreakpoint()
	{
		// 기본감지 10 + 20 = 30 → 25~49 구간(+1). 경계 아니면 10은 0~24 구간(+0)이라 값이 달라야 한다.
		int alert = PropagationMath.SoundDetectionRange(SoundType.Movement, 10f, isAlert: true, isMonster: false);
		int normal = PropagationMath.SoundDetectionRange(SoundType.Movement, 10f, isAlert: false, isMonster: false);
		Assert.AreEqual(3, alert);  // 2(기본) + 1(30→+1구간)
		Assert.AreEqual(2, normal); // 2(기본) + 0
	}

	[Test]
	public void SoundDetectionRange_AlertCapsAt100()
	{
		Assert.AreEqual(100f, PropagationMath.AlertFinalDetection(90f), 0.001f);
	}

	// ── 5-2장. 몬스터 청각 보정 +2칸 ──
	[Test]
	public void SoundDetectionRange_MonsterHearingBonus()
	{
		int monster = PropagationMath.SoundDetectionRange(SoundType.Movement, 0f, isAlert: false, isMonster: true);
		int human = PropagationMath.SoundDetectionRange(SoundType.Movement, 0f, isAlert: false, isMonster: false);
		Assert.AreEqual(human + 2, monster);
	}

	// ── 6장. 추정 지역 반경(전투 관련 소리 2칸, 함정 작동음 1칸, 이동음 없음) ──
	[Test]
	public void EstimatedAreaRadius_Table()
	{
		Assert.AreEqual(2, PropagationMath.EstimatedAreaRadius(SoundType.AttackExecution));
		Assert.AreEqual(2, PropagationMath.EstimatedAreaRadius(SoundType.HitImpact));
		Assert.AreEqual(2, PropagationMath.EstimatedAreaRadius(SoundType.HitScream));
		Assert.AreEqual(2, PropagationMath.EstimatedAreaRadius(SoundType.Death));
		Assert.AreEqual(1, PropagationMath.EstimatedAreaRadius(SoundType.TrapActivation));
		Assert.AreEqual(0, PropagationMath.EstimatedAreaRadius(SoundType.Movement));
		Assert.IsFalse(PropagationMath.HasEstimatedArea(SoundType.Movement));
		Assert.IsTrue(PropagationMath.HasEstimatedArea(SoundType.Death));
	}

	// ── 7-1장. 소리 내부 우선순위(사망/비명=1 최우선 ... 이동음=5 최하) ──
	[Test]
	public void SoundPriorityRank_Order()
	{
		Assert.AreEqual(1, PropagationMath.SoundPriorityRank(SoundType.Death));
		Assert.AreEqual(1, PropagationMath.SoundPriorityRank(SoundType.HitScream));
		Assert.AreEqual(2, PropagationMath.SoundPriorityRank(SoundType.HitImpact));
		Assert.AreEqual(3, PropagationMath.SoundPriorityRank(SoundType.TrapActivation));
		Assert.AreEqual(4, PropagationMath.SoundPriorityRank(SoundType.AttackExecution));
		Assert.AreEqual(5, PropagationMath.SoundPriorityRank(SoundType.Movement));
	}

	[Test]
	public void ShouldReplaceSound_HigherPriorityWins_ThenCloserDistance()
	{
		Assert.IsTrue(PropagationMath.ShouldReplaceSound(candidateRank: 1, candidateDist: 10f, currentRank: 5, currentDist: 1f));
		Assert.IsFalse(PropagationMath.ShouldReplaceSound(candidateRank: 5, candidateDist: 1f, currentRank: 1, currentDist: 10f));
		Assert.IsTrue(PropagationMath.ShouldReplaceSound(candidateRank: 2, candidateDist: 3f, currentRank: 2, currentDist: 5f));
		Assert.IsFalse(PropagationMath.ShouldReplaceSound(candidateRank: 2, candidateDist: 5f, currentRank: 2, currentDist: 3f));
	}

	// ── 9-1/9-2장. 근거리 즉시 전투(2칸), 위험도 단계별 합류 대기 ──
	[Test]
	public void RequiresJoinWait_Table()
	{
		Assert.IsFalse(PropagationMath.RequiresJoinWait(DangerStage.StageMax, distanceTiles: 2)); // 근거리
		Assert.IsFalse(PropagationMath.RequiresJoinWait(null, distanceTiles: 5));                 // 미확인
		Assert.IsFalse(PropagationMath.RequiresJoinWait(DangerStage.Stage0, distanceTiles: 5));
		Assert.IsFalse(PropagationMath.RequiresJoinWait(DangerStage.Stage1, distanceTiles: 5));
		Assert.IsTrue(PropagationMath.RequiresJoinWait(DangerStage.Stage2, distanceTiles: 5));
		Assert.IsTrue(PropagationMath.RequiresJoinWait(DangerStage.Stage3, distanceTiles: 5));   // "2단계 규칙 임시 적용"
		Assert.IsTrue(PropagationMath.RequiresJoinWait(DangerStage.StageMax, distanceTiles: 5));
	}

	// ── 17장. 공격 방향 간접입력: 광역/지면 영역만 방향 정보 없음 ──
	[Test]
	public void AttackShapeProvidesDirection_Table()
	{
		Assert.IsTrue(PropagationMath.AttackShapeProvidesDirection(AttackShape.Melee));
		Assert.IsTrue(PropagationMath.AttackShapeProvidesDirection(AttackShape.Projectile));
		Assert.IsFalse(PropagationMath.AttackShapeProvidesDirection(AttackShape.AreaGround));
	}
}
#endif
