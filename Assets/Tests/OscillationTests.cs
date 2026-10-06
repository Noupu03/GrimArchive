#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

// ========================================================================
// 제자리 왕복(떨림) 감지 판정 고정 테스트(NUnit, #if UNITY_INCLUDE_TESTS) — OscillationMath는 순수 함수라 Unity 오브젝트 없이 검증한다.
// ========================================================================

public class OscillationTests
{
	private static bool Detect(float[] t, int[] x, int[] y, float now, out int moves, out int distinct)
		=> OscillationMath.IsOscillating(new List<float>(t), new List<int>(x), new List<int>(y), now,
			OscillationMath.WindowSeconds, OscillationMath.MinMoves, OscillationMath.MaxDistinctTiles, out moves, out distinct);

	[Test]
	public void BackAndForthBetweenTwoTiles_IsOscillating()
	{
		float[] t = { 0.0f, 0.3f, 0.6f, 0.9f, 1.2f, 1.5f, 1.8f, 2.1f, 2.4f, 2.7f };
		int[] x = { 5, 6, 5, 6, 5, 6, 5, 6, 5, 6 };
		int[] y = { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 };
		Assert.IsTrue(Detect(t, x, y, 3.0f, out int moves, out int distinct));
		Assert.AreEqual(10, moves);
		Assert.AreEqual(2, distinct);
	}

	// 앞뒤·좌우 세 칸을 도는 떨림도 잡는다.
	[Test]
	public void ShufflingAmongThreeTiles_IsOscillating()
	{
		float[] t = { 0f, 0.3f, 0.6f, 0.9f, 1.2f, 1.5f, 1.8f, 2.1f };
		int[] x = { 5, 6, 6, 5, 6, 6, 5, 6 };
		int[] y = { 5, 5, 6, 5, 5, 6, 5, 5 };
		Assert.IsTrue(Detect(t, x, y, 2.5f, out _, out int distinct));
		Assert.AreEqual(3, distinct);
	}

	// 직선으로 걷는 정상 이동은 타일이 계속 늘어 왕복이 아니다.
	[Test]
	public void WalkingInALine_IsNotOscillating()
	{
		float[] t = { 0f, 0.3f, 0.6f, 0.9f, 1.2f, 1.5f, 1.8f, 2.1f, 2.4f };
		int[] x = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
		int[] y = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
		Assert.IsFalse(Detect(t, x, y, 2.5f, out _, out _));
	}

	[Test]
	public void TooFewMoves_IsNotOscillating()
	{
		float[] t = { 0f, 0.3f, 0.6f, 0.9f, 1.2f };
		int[] x = { 5, 6, 5, 6, 5 };
		int[] y = { 5, 5, 5, 5, 5 };
		Assert.IsFalse(Detect(t, x, y, 1.5f, out int moves, out _));
		Assert.AreEqual(5, moves);
	}

	// 창(6초)보다 오래된 이동은 세지 않는다 — 한참 전의 왕복이 지금 판정에 섞이면 안 된다.
	[Test]
	public void MovesOlderThanWindow_AreIgnored()
	{
		float[] t = { 0f, 0.3f, 0.6f, 0.9f, 1.2f, 1.5f, 1.8f, 2.1f, 20.0f, 20.3f };
		int[] x = { 5, 6, 5, 6, 5, 6, 5, 6, 5, 6 };
		int[] y = { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 };
		Assert.IsFalse(Detect(t, x, y, 21.0f, out int moves, out _));
		Assert.AreEqual(2, moves);
	}
}
#endif
