#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 말풍선(대사 연출) 테스트(NUnit, #if UNITY_INCLUDE_TESTS) — 이름 가공·조사·문구 선택·덮어쓰기/쿨다운/페이드 판정과 대사 목록의 명세서 문구(후퇴도주 4-3·7-3장), 세션 없는 유닛에서의 안전성을 고정한다.
// ========================================================================

public class SpeechSystemTests
{
	[SetUp]
	public void Setup()
	{
		SpeechSystem.Enabled = true;
		SpeechSystem.ClearHistory();
	}

	[TearDown]
	public void ResetStatics()
	{
		SpeechSystem.Enabled = true;
		SpeechSystem.Random01 = () => UnityEngine.Random.value;
		SpeechSystem.Now = () => Time.time;
		SpeechSystem.ClearHistory();
	}

	// ── 이름·조사 ──

	[Test]
	public void DisplayName_CutsAtFirstUnderscore()
	{
		Assert.AreEqual("사제", SpeechMath.DisplayName("사제_7_5_0"));
		Assert.AreEqual("기사형", SpeechMath.DisplayName("기사형_5_5_0"));
		Assert.AreEqual("무도가", SpeechMath.DisplayName("무도가"), "밑줄이 없으면 그대로");
		Assert.IsNull(SpeechMath.DisplayName(null));
	}

	[Test]
	public void WithGa_FollowsBatchim()
	{
		Assert.AreEqual("사제가", SpeechMath.WithGa("사제"));
		Assert.AreEqual("무도가가", SpeechMath.WithGa("무도가"));
		Assert.AreEqual("아처형이", SpeechMath.WithGa("아처형"));
		Assert.AreEqual("음유시인이", SpeechMath.WithGa("음유시인"));
		Assert.AreEqual("기사형이", SpeechMath.WithGa("기사형"));
		Assert.AreEqual("A가", SpeechMath.WithGa("A"), "한글이 아니면 받침 없음으로 본다");
	}

	// ── 문구 선택 ──

	[Test]
	public void Format_ReplacesSubjectTokens()
	{
		Assert.AreEqual("무도가가 없는데?", SpeechMath.Format("{S가} 없는데?", "무도가"));
		Assert.AreEqual("사제, 어디가!", SpeechMath.Format("{S}, 어디가!", "사제"));
	}

	[Test]
	public void PickLine_SkipsSubjectLinesWithoutSubject()
	{
		var lines = new[] { "{S가} 없는데?", "다들 어디 갔지?" };
		Assert.AreEqual("다들 어디 갔지?", SpeechMath.PickLine(lines, null, 0f), "대상 이름이 없으면 {S} 문구는 후보에서 뺀다");
		Assert.AreEqual("사제가 없는데?", SpeechMath.PickLine(lines, "사제", 0f));
		Assert.AreEqual("다들 어디 갔지?", SpeechMath.PickLine(lines, "사제", 0.99f));
		Assert.IsNull(SpeechMath.PickLine(new[] { "{S가} 없는데?" }, null, 0f), "쓸 수 있는 문구가 없으면 null");
		Assert.IsNull(SpeechMath.PickLine(new string[0], "사제", 0f));
	}

	[Test]
	public void Wrap_SplitsLongLinesAtSpaceNearestTheMiddle()
	{
		Assert.AreEqual("더는 버틸 수\n없다… 후퇴한다!", SpeechMath.Wrap("더는 버틸 수 없다… 후퇴한다!"), "17자의 가운데(8)에 가장 가까운 공백은 7번째");
		Assert.AreEqual("도망쳐야 해!", SpeechMath.Wrap("도망쳐야 해!"), "짧으면 그대로");
		Assert.AreEqual("가나다라마바사아자차카타파하", SpeechMath.Wrap("가나다라마바사아자차카타파하"), "공백이 없으면 그대로");
		Assert.IsNull(SpeechMath.Wrap(null));
		foreach (SpeechId id in System.Enum.GetValues(typeof(SpeechId)))
		{
			Assert.IsTrue(SpeechCatalog.TryGet(id, out var entry));
			foreach (var line in entry.Lines)
				foreach (var part in SpeechMath.Wrap(SpeechMath.Format(line, "음유시인")).Split('\n'))
					Assert.LessOrEqual(part.Length, 14, $"{id} \"{line}\"는 두 줄로 나눠도 한 줄이 너무 길다");
		}
	}

	[Test]
	public void WithIda_FollowsBatchim()
	{
		Assert.AreEqual("근접 탱커다", SpeechMath.WithIda("근접 탱커"));
		Assert.AreEqual("아처형이다", SpeechMath.WithIda("아처형"));
		Assert.AreEqual("사제다", SpeechMath.WithIda("사제"));
		Assert.AreEqual("적이다!!!", SpeechMath.Format("{S이다}!!!", "적"));
	}

	[Test]
	public void Catalog_LinesFormatCleanlyAndSubjectLinesAreNeverTheOnlyChoice()
	{
		foreach (SpeechId id in System.Enum.GetValues(typeof(SpeechId)))
		{
			Assert.IsTrue(SpeechCatalog.TryGet(id, out var entry));
			bool allNeedSubject = true;
			foreach (var line in entry.Lines)
			{
				string formatted = SpeechMath.Format(line, "음유시인");
				Assert.IsFalse(formatted.Contains("{") || formatted.Contains("}"), $"{id} \"{line}\"에 치환되지 않은 자리가 남는다");
				if (!line.Contains("{S")) allNeedSubject = false;
			}
			// 대상 이름이 필수인 항목은 호출부가 대상을 넘기는 AbsentMember뿐이다 — 다른 항목이 대상 전용이면 대상 없이 불려 아무 말도 못 한다.
			if (allNeedSubject) Assert.AreEqual(SpeechId.AbsentMember, id, $"{id}의 모든 문구가 대상 이름을 요구하는데 호출부가 대상을 넘기지 않으면 침묵한다");
		}
	}

	[Test]
	public void Catalog_SubjectlessPickNeverReturnsRawTokens()
	{
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.EnemySpotted, out var entry));
		for (float r = 0f; r < 1f; r += 0.1f)
		{
			string line = SpeechMath.PickLine(entry.Lines, null, r);
			Assert.IsNotNull(line);
			StringAssert.DoesNotContain("{", line);
		}
	}

	// ── 덮어쓰기·쿨다운·페이드 ──

	[Test]
	public void CanReplace_OnlyWhenIdleOrNotLowerPriority()
	{
		Assert.IsTrue(SpeechMath.CanReplace(false, 90, 10), "떠 있지 않으면 항상 가능");
		Assert.IsFalse(SpeechMath.CanReplace(true, 90, 60), "더 높은 순위가 떠 있으면 덮어쓰지 않는다");
		Assert.IsTrue(SpeechMath.CanReplace(true, 60, 60), "같은 순위는 덮어쓴다");
		Assert.IsTrue(SpeechMath.CanReplace(true, 60, 90));
	}

	[Test]
	public void CooldownElapsed_NeverSaidAlwaysPasses()
	{
		Assert.IsTrue(SpeechMath.CooldownElapsed(0f, null, 30f));
		Assert.IsFalse(SpeechMath.CooldownElapsed(10f, 5f, 30f));
		Assert.IsTrue(SpeechMath.CooldownElapsed(35f, 5f, 30f));
		Assert.IsTrue(SpeechMath.CooldownElapsed(10f, 10f, 0f), "쿨다운 0이면 바로 가능");
	}

	[Test]
	public void FadeAlpha_FullThenLinearThenZero()
	{
		Assert.AreEqual(1f, SpeechMath.FadeAlpha(0f, 2f, 0.4f), 0.0001f);
		Assert.AreEqual(1f, SpeechMath.FadeAlpha(1.6f, 2f, 0.4f), 0.0001f, "끝나기 정확히 fade 전까지는 불투명");
		Assert.AreEqual(0.5f, SpeechMath.FadeAlpha(1.8f, 2f, 0.4f), 0.0001f);
		Assert.AreEqual(0f, SpeechMath.FadeAlpha(2f, 2f, 0.4f), 0.0001f);
		Assert.AreEqual(1f, SpeechMath.FadeAlpha(1.9f, 2f, 0f), 0.0001f, "fade가 0이면 끝까지 불투명");
	}

	[Test]
	public void TryClaim_UnitAndGlobalCooldowns()
	{
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.AllyDeathSeen, out var entry));
		Assert.Greater(entry.UnitCooldown, 0f);
		Assert.Greater(entry.GlobalCooldown, 0f);

		Assert.IsTrue(SpeechSystem.TryClaim("사제_1", SpeechId.AllyDeathSeen, null, entry, 0f, false));
		SpeechSystem.Record("사제_1", SpeechId.AllyDeathSeen, null, 0f);

		Assert.IsFalse(SpeechSystem.TryClaim("사제_1", SpeechId.AllyDeathSeen, null, entry, 1f, false), "같은 유닛은 유닛 쿨다운 안에 다시 못 말한다");
		Assert.IsFalse(SpeechSystem.TryClaim("전사_2", SpeechId.AllyDeathSeen, null, entry, 1f, false), "다른 유닛도 전체 쿨다운 안에는 못 말한다(합창 방지)");
		Assert.IsFalse(SpeechSystem.TryClaim("사제_1", SpeechId.AllyDeathSeen, null, entry, entry.GlobalCooldown + 0.1f, false), "같은 유닛의 유닛 쿨다운은 전체 쿨다운이 지나도 남아 있다");
		Assert.IsTrue(SpeechSystem.TryClaim("사제_1", SpeechId.AllyDeathSeen, null, entry, 1f, true), "ignoreCooldown은 debug 확인용으로 항상 통과");
	}

	[Test]
	public void TryClaim_UnitBlockedByGlobalCooldownLosesTheMomentInsteadOfQueueing()
	{
		// 전체 쿨다운에 막힌 유닛이 틱마다 재시도하다 쿨다운이 풀리는 순간 차례로 말하면 말풍선이 "밀려서" 뜬다 — 놓친 사건은 놓친 것으로 본다.
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.EnemySpotted, out var entry));
		Assert.Greater(entry.GlobalCooldown, 0f);
		Assert.IsTrue(SpeechSystem.TryClaim("사제_1", SpeechId.EnemySpotted, "고블린", entry, 0f, false));
		SpeechSystem.Record("사제_1", SpeechId.EnemySpotted, "고블린", 0f);

		Assert.IsFalse(SpeechSystem.TryClaim("전사_2", SpeechId.EnemySpotted, "고블린", entry, 0.1f, false), "전체 쿨다운에 막힌다");
		float afterGlobal = entry.GlobalCooldown + 0.1f;
		Assert.IsFalse(SpeechSystem.TryClaim("전사_2", SpeechId.EnemySpotted, "고블린", entry, afterGlobal, false), "막혔던 유닛은 전체 쿨다운이 풀려도 뒤늦게 말하지 않는다");
		Assert.IsTrue(SpeechSystem.TryClaim("전사_2", SpeechId.EnemySpotted, "고블린", entry, 0.1f + entry.UnitCooldown + 0.1f, false), "자기 쿨다운이 지난 뒤의 새 사건에는 말할 수 있다");
		Assert.IsTrue(SpeechSystem.TryClaim("전사_2", SpeechId.EnemySpotted, "오크", entry, afterGlobal, false), "다른 대상이면 다른 사건이다");
	}

	[Test]
	public void Suppress_ConsumesOnlyTheUnitCooldown()
	{
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.Investigate, out var entry));
		SpeechSystem.Suppress("사제_1", SpeechId.Investigate, null, 0f);
		Assert.IsFalse(SpeechSystem.TryClaim("사제_1", SpeechId.Investigate, null, entry, 1f, false), "놓친 유닛은 자기 쿨다운 동안 재시도해도 말하지 않는다");
		Assert.IsTrue(SpeechSystem.TryClaim("전사_2", SpeechId.Investigate, null, entry, 1f, false), "전체 쿨다운은 건드리지 않아 다른 유닛은 말할 수 있다");
	}

	[Test]
	public void TryClaim_SubjectsHaveSeparateUnitCooldowns()
	{
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.AbsentMember, out var entry));
		SpeechSystem.Record("리더_1", SpeechId.AbsentMember, "사제", 0f);
		// 다른 대상은 유닛 쿨다운이 따로지만 전체 쿨다운(4초)은 공유한다.
		Assert.IsFalse(SpeechSystem.TryClaim("리더_1", SpeechId.AbsentMember, "전사", entry, 1f, false));
		Assert.IsTrue(SpeechSystem.TryClaim("리더_1", SpeechId.AbsentMember, "마법사", entry, entry.GlobalCooldown + 0.1f, false), "전체 쿨다운이 지나면 새 대상은 말할 수 있다");
		Assert.IsFalse(SpeechSystem.TryClaim("리더_1", SpeechId.AbsentMember, "사제", entry, entry.GlobalCooldown + 0.1f, false), "같은 대상은 유닛 쿨다운(30초) 안에는 반복하지 않는다");
	}

	// ── 대사 목록 ──

	[Test]
	public void Catalog_EveryIdHasUsableLines()
	{
		foreach (SpeechId id in System.Enum.GetValues(typeof(SpeechId)))
		{
			Assert.IsTrue(SpeechCatalog.TryGet(id, out var entry), $"{id} 항목이 없다");
			Assert.Greater(entry.Lines.Length, 0, $"{id} 문구가 비었다");
			Assert.Greater(entry.Seconds, 0f, $"{id} 표시 시간이 0이다");
			foreach (var line in entry.Lines) Assert.IsFalse(string.IsNullOrWhiteSpace(line), $"{id}에 빈 문구가 있다");
		}
	}

	[Test]
	public void Catalog_ContainsSpecExampleLines()
	{
		AssertHasLine(SpeechId.EncounterGap2, "상대하기 버겁다…");        // 4-3장
		AssertHasLine(SpeechId.EncounterPanic, "몸이 움직이지 않아…");     // 4-3장
		AssertHasLine(SpeechId.EncounterFleeMax, "도망쳐야 해!");          // 4-3장
		AssertHasLine(SpeechId.FleeConfirmed, "난 살아남을거야!!!!");      // 7-3장
		AssertHasLine(SpeechId.FleeConfirmed, "흐아아악");                 // 7-3장
		AssertHasLine(SpeechId.FleeWitnessed, "어디가!!!");                // 7-3장
		AssertHasLine(SpeechId.AbsentMember, "{S가} 없는데?");             // 7-3장 "OO가 없는데?"
	}

	[Test]
	public void Catalog_AbsentMemberNeverImpliesDeathOrFlight()
	{
		// 7-3장: 미집결 사실 표현일 뿐 사망·도주 확정이 아니다 — 문구에 단정이 들어가면 안 된다.
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.AbsentMember, out var entry));
		foreach (var line in entry.Lines)
		{
			StringAssert.DoesNotContain("죽", line);
			StringAssert.DoesNotContain("도망", line);
		}
	}

	[Test]
	public void Catalog_FleeLinesOutrankRoutineLines()
	{
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.FleeConfirmed, out var flee));
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.RallyStart, out var rally));
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.ArriveBase, out var arrive));
		Assert.Greater(flee.Priority, rally.Priority);
		Assert.Greater(flee.Priority, arrive.Priority);
	}

	// ── 연출 전용 안전성 ──

	[Test]
	public void Say_WithoutSessionDoesNothingAndDoesNotThrow()
	{
		var h = ScriptableObject.CreateInstance<Human>();
		h.name = "사제_7_5_0";
		h.maxHp = 100f;
		h.hp = 100f;
		float mentalBefore = h.BaseStat.mental;

		Assert.IsFalse(SpeechSystem.Say(h, SpeechId.FleeConfirmed));
		Assert.IsFalse(SpeechSystem.Say(null, SpeechId.FleeConfirmed));
		Assert.AreEqual(mentalBefore, h.BaseStat.mental, "대사는 게임 상태를 바꾸지 않는다");
		Assert.AreEqual(100f, h.hp);
	}

	[Test]
	public void Say_DisabledReturnsFalse()
	{
		SpeechSystem.Enabled = false;
		var h = ScriptableObject.CreateInstance<Human>();
		h.hp = 100f;
		Assert.IsFalse(SpeechSystem.Say(h, SpeechId.RallyStart));
	}

	static void AssertHasLine(SpeechId id, string line)
	{
		Assert.IsTrue(SpeechCatalog.TryGet(id, out var entry));
		CollectionAssert.Contains(entry.Lines, line, $"{id}에 명세서 문구 \"{line}\"가 없다");
	}
}
#endif
