#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 미집결자 수색의 순수 계산 — 파티 구성 명세서 8-1장 2·3·4(경로 기준 도착 예상시간 우선·관찰 지점 조건·방향 분담). 세션 없이 EditMode에서 돈다.
// ========================================================================

public class RallyGroupSearchTests
{
	private static LeaderMath.RallySearchCandidate C(string n, int x, int y, float seen, int? eta = null)
		=> new LeaderMath.RallySearchCandidate { Name = n, Position = new Vector2Int(x, y), Floor = 1, SeenAt = seen, EtaSteps = eta };

	[Test]
	public void PickTarget_PrefersShorterRouteOverShorterStraightLine()
	{
		var leader = new Vector2Int(5, 5);
		// near는 직선으로 가깝지만 벽을 돌아가는 길이 멀고(25걸음), far는 직선으론 멀어도 길이 곧다(10걸음).
		var list = new List<LeaderMath.RallySearchCandidate> { C("near", 8, 5, 5f, eta: 25), C("far", 14, 5, 5f, eta: 10) };
		Assert.AreEqual(1, LeaderMath.PickRallySearchTarget(leader, 1, list), "알려진 경로 기준 도착 예상시간이 짧은 위치가 먼저다(8-1장 2번)");
	}

	[Test]
	public void PickTarget_EqualRouteLength_MoreRecentlySeenWins()
	{
		var list = new List<LeaderMath.RallySearchCandidate> { C("older", 8, 5, 5f, eta: 12), C("newer", 14, 5, 9f, eta: 12) };
		Assert.AreEqual(1, LeaderMath.PickRallySearchTarget(new Vector2Int(5, 5), 1, list), "예상시간이 같으면 실제 확인 시각이 최근인 쪽");
	}

	[Test]
	public void PickTarget_WithoutRouteLength_FallsBackToStraightLine()
	{
		var list = new List<LeaderMath.RallySearchCandidate> { C("far", 20, 5, 9f), C("near", 8, 5, 1f) };
		Assert.AreEqual(1, LeaderMath.PickRallySearchTarget(new Vector2Int(5, 5), 1, list), "경로를 모르면 예전처럼 직선(체비셰프) 거리");
	}

	[Test]
	public void ObservationTile_NeedsWalkable_NoCorpse_AndAClearLineToTheTarget()
	{
		Assert.IsTrue(RallyGroupSearchMath.IsObservationTile(true, false, true));
		Assert.IsFalse(RallyGroupSearchMath.IsObservationTile(false, false, true), "서 있을 수 없는 칸");
		Assert.IsFalse(RallyGroupSearchMath.IsObservationTile(true, true, true), "시체 점유 칸은 정지 목적지로 잡지 않는다(8-1장 3번)");
		Assert.IsFalse(RallyGroupSearchMath.IsObservationTile(true, false, false), "수색 위치가 벽에 가려 안 보이는 칸");
	}

	[Test]
	public void FacingVector_RotatesFrontRightBackLeft_AndCyclesForMoreThanFourPeople()
	{
		var up = new Vector2Int(0, 7); // 수색 위치가 위쪽
		Assert.AreEqual(new Vector2Int(0, 1), RallyGroupSearchMath.FacingVector(0, up), "0번은 수색 위치 쪽(전방)");
		Assert.AreEqual(new Vector2Int(1, 0), RallyGroupSearchMath.FacingVector(1, up), "1번은 오른쪽");
		Assert.AreEqual(new Vector2Int(0, -1), RallyGroupSearchMath.FacingVector(2, up), "2번은 후방");
		Assert.AreEqual(new Vector2Int(-1, 0), RallyGroupSearchMath.FacingVector(3, up), "3번은 왼쪽");
		Assert.AreEqual(RallyGroupSearchMath.FacingVector(0, up), RallyGroupSearchMath.FacingVector(4, up), "다섯 번째 인원은 다시 전방부터");
	}

	[Test]
	public void FacingVector_FoldsComponentsToEightDirections_AndHandlesTheSameTile()
	{
		Assert.AreEqual(new Vector2Int(1, 1), RallyGroupSearchMath.FacingVector(0, new Vector2Int(9, 3)), "성분을 −1/0/1로 접는다");
		Assert.AreEqual(new Vector2Int(1, -1), RallyGroupSearchMath.FacingVector(1, new Vector2Int(9, 3)), "대각선도 90도씩 돈다");
		Assert.AreEqual(new Vector2Int(0, -1), RallyGroupSearchMath.FacingVector(0, Vector2Int.zero), "수색 위치와 같은 칸이면 아래쪽을 전방으로 본다");
		Assert.AreEqual(new Vector2Int(-1, 0), RallyGroupSearchMath.FacingVector(1, Vector2Int.zero));
		Assert.AreEqual(RallyGroupSearchMath.FacingVector(3, new Vector2Int(2, 2)), RallyGroupSearchMath.FacingVector(-1, new Vector2Int(2, 2)), "음수 번호도 순환한다");
	}
}
#endif
