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

	// ── CombatDecisionTrace(2026-11-04 진단) — "무엇이 유닛을 움직였는지" 링버퍼 ──

	[Test]
	public void Trace_KeepsOrderAndFormatsAgeTagDistanceAndDetail()
	{
		var ring = new CombatDecisionTrace.Ring();
		ring.Add(10.0f, "후퇴", 3, "정밀 사격");
		ring.Add(10.4f, "추격(스킬불가)", 4, "정밀 사격");
		Assert.AreEqual("-2.0s 후퇴(d3)[정밀 사격] | -1.6s 추격(스킬불가)(d4)[정밀 사격]", ring.Format(12.0f, 10));
	}

	// 매 틱 반복되는 판단(제자리 대기 등)은 새 항목 대신 횟수만 올리고 거리는 최근 값으로 갱신해 링을 밀어내지 않는다.
	[Test]
	public void Trace_RepeatedIdenticalDecisionsAreCollapsedWithCount()
	{
		var ring = new CombatDecisionTrace.Ring();
		ring.Add(1.0f, "쿨다운대기", 5, null);
		ring.Add(1.3f, "쿨다운대기", 5, null);
		ring.Add(1.6f, "쿨다운대기", 4, null);
		Assert.AreEqual(1, ring.Count);
		Assert.AreEqual("-1.0s 쿨다운대기×3(d4)", ring.Format(2.0f, 10));
	}

	// 후퇴와 추격이 번갈아 나오는 왕복은 하나로 합쳐지지 않고 순서 그대로 남아야 원인을 읽을 수 있다.
	[Test]
	public void Trace_AlternatingDecisionsStaySeparate()
	{
		var ring = new CombatDecisionTrace.Ring();
		for (int i = 0; i < 4; i++)
		{
			ring.Add(i * 0.4f, "후퇴", 3, "정밀 사격");
			ring.Add(i * 0.4f + 0.2f, "추격(스킬불가)", 4, "정밀 사격");
		}
		Assert.AreEqual(8, ring.Count);
		Assert.AreEqual("후퇴", ring[6].Tag);
		Assert.AreEqual("추격(스킬불가)", ring[7].Tag);
	}

	[Test]
	public void Trace_CapacityDropsOldestAndDescribeLimitsToRecent()
	{
		var ring = new CombatDecisionTrace.Ring();
		for (int i = 0; i < CombatDecisionTrace.Capacity + 5; i++) ring.Add(i, "T" + i, i, null);
		Assert.AreEqual(CombatDecisionTrace.Capacity, ring.Count);
		Assert.AreEqual("T5", ring[0].Tag, "가장 오래된 5개가 밀려난다");

		string text = ring.Format(100f, 3);
		Assert.AreEqual(2, CountOf(text, " | "), "최근 3개만 싣는다");
		StringAssert.Contains("T16", text);
		StringAssert.DoesNotContain("T13", text);
	}

	[Test]
	public void Trace_EmptyRingFormatsAsNoRecord()
	{
		Assert.AreEqual("기록 없음", new CombatDecisionTrace.Ring().Format(1f, 10));
		Assert.AreEqual("기록 없음", CombatDecisionTrace.Describe(null, 1f));
	}

	// 몬스터·진단 스위치 꺼짐에서는 기록하지 않는다 — Human이 아니면 Record가 즉시 반환한다.
	[Test]
	public void Trace_RecordIgnoresDisabledSwitchAndNonHumans()
	{
		CombatDecisionTrace.EnabledOverride = false;
		try
		{
			var h = UnityEngine.ScriptableObject.CreateInstance<Human>();
			CombatDecisionTrace.Record(h, "후퇴", 3);
			Assert.AreEqual("기록 없음", CombatDecisionTrace.Describe(h, 0f), "스위치가 꺼지면 기록하지 않는다");

			CombatDecisionTrace.EnabledOverride = true;
			CombatDecisionTrace.Clock = () => 5f;
			CombatDecisionTrace.Record(h, "후퇴", 3, "정밀 사격");
			Assert.AreEqual("-1.0s 후퇴(d3)[정밀 사격]", CombatDecisionTrace.Describe(h, 6f));
		}
		finally
		{
			CombatDecisionTrace.EnabledOverride = null;
			CombatDecisionTrace.Clock = () => UnityEngine.Time.time;
		}
	}

	private static int CountOf(string text, string needle)
	{
		int count = 0, i = 0;
		while ((i = text.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
		return count;
	}
}
#endif
