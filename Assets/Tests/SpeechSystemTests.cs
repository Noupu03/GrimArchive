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
		Assert.AreEqual("사제를 지켜라!", SpeechMath.Format("{S를} 지켜라!", "사제"));
		Assert.AreEqual("고블린을 제압하라!", SpeechMath.Format("{S를} 제압하라!", "고블린"));
	}

	[Test]
	public void WithEul_FollowsBatchim()
	{
		Assert.AreEqual("사제를", SpeechMath.WithEul("사제"));
		Assert.AreEqual("아처형을", SpeechMath.WithEul("아처형"));
		Assert.AreEqual("무도가를", SpeechMath.WithEul("무도가"));
		Assert.AreEqual("A를", SpeechMath.WithEul("A"), "한글이 아니면 받침 없음으로 본다");
	}

	[Test]
	public void PickLine_SkipsSubjectLinesWithoutSubject()
	{
		var lines = new[] { "{S가} 없는데?", "다들 어디 갔지?" };
		Assert.AreEqual("다들 어디 갔지?", SpeechMath.PickLine(lines, null, 0f), "대상 이름이 없으면 {S} 문구는 후보에서 뺀다");
		Assert.AreEqual("사제가 없는데?", SpeechMath.PickLine(lines, "사제", 0f));
		Assert.AreEqual("사제가 없는데?", SpeechMath.PickLine(lines, "사제", 0.99f), "대상 이름이 있으면 대상 문구만 후보다 — 대상 없는 문구는 폴백일 뿐");
		Assert.AreEqual("적이다!!!", SpeechMath.PickLine(new[] { "적이다!!!" }, "고블린", 0.5f), "대상 이름을 쓰는 문구가 하나도 없는 항목은 대상이 있어도 전체에서 고른다");
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
		foreach (var id in new[] { SpeechId.EnemySpotted, SpeechId.CmdFocusAttack })
		{
			Assert.IsTrue(SpeechCatalog.TryGet(id, out var entry));
			for (float r = 0f; r < 1f; r += 0.1f)
			{
				string line = SpeechMath.PickLine(entry.Lines, null, r);
				Assert.IsNotNull(line, $"{id}는 대상 이름 없이도 말할 수 있어야 한다");
				StringAssert.DoesNotContain("{", line);
			}
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
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.CmdFocusAttack, out var entry));
		Assert.Greater(entry.GlobalCooldown, 0f);
		Assert.Greater(entry.UnitCooldown, entry.GlobalCooldown, "이 테스트는 유닛 쿨다운이 전체 쿨다운보다 긴 항목을 전제한다");
		Assert.IsTrue(SpeechSystem.TryClaim("사제_1", SpeechId.CmdFocusAttack, "고블린", entry, 0f, false));
		SpeechSystem.Record("사제_1", SpeechId.CmdFocusAttack, "고블린", 0f);

		Assert.IsFalse(SpeechSystem.TryClaim("전사_2", SpeechId.CmdFocusAttack, "고블린", entry, 0.1f, false), "전체 쿨다운에 막힌다");
		float afterGlobal = entry.GlobalCooldown + 0.1f;
		Assert.IsFalse(SpeechSystem.TryClaim("전사_2", SpeechId.CmdFocusAttack, "고블린", entry, afterGlobal, false), "막혔던 유닛은 전체 쿨다운이 풀려도 뒤늦게 말하지 않는다");
		Assert.IsTrue(SpeechSystem.TryClaim("전사_2", SpeechId.CmdFocusAttack, "고블린", entry, 0.1f + entry.UnitCooldown + 0.1f, false), "자기 쿨다운이 지난 뒤의 새 사건에는 말할 수 있다");
		Assert.IsTrue(SpeechSystem.TryClaim("전사_2", SpeechId.CmdFocusAttack, "오크", entry, afterGlobal, false), "다른 대상이면 다른 사건이다");
	}

	[Test]
	public void Suppress_ConsumesOnlyTheUnitCooldown()
	{
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.CmdStrike, out var entry));
		SpeechSystem.Suppress("사제_1", SpeechId.CmdStrike, null, 0f);
		Assert.IsFalse(SpeechSystem.TryClaim("사제_1", SpeechId.CmdStrike, null, entry, 1f, false), "놓친 유닛은 자기 쿨다운 동안 재시도해도 말하지 않는다");
		Assert.IsTrue(SpeechSystem.TryClaim("전사_2", SpeechId.CmdStrike, null, entry, 1f, false), "전체 쿨다운은 건드리지 않아 다른 유닛은 말할 수 있다");
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
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.AdvanceOrder, out var advance));
		Assert.Greater(flee.Priority, rally.Priority);
		Assert.Greater(flee.Priority, advance.Priority);
	}

	// ── 왕관(리더의 명령) ──

	[Test]
	public void Catalog_LeaderOrdersGetCrown_IndividualChangesDoNot()
	{
		// 리더의 명령 — 집결·이동·돌파·후퇴·승계와 전술 명령 6종.
		foreach (var id in new[]
		{
			SpeechId.RallyStart, SpeechId.AdvanceOrder, SpeechId.BreachOrder, SpeechId.EntryOrder, SpeechId.WaveStart,
			SpeechId.PartyRetreatByHp, SpeechId.PartyRetreatByMental, SpeechId.SuccessorTakesCommand,
			SpeechId.CmdFocusAttack, SpeechId.CmdObjectFocus, SpeechId.CmdStrike, SpeechId.CmdSuppress, SpeechId.CmdProtect, SpeechId.CmdProtectSelf, SpeechId.CmdSuperiorFocus,
		})
		{
			Assert.IsTrue(SpeechCatalog.TryGet(id, out var e));
			Assert.IsTrue(e.LeaderOrder, $"{id}는 리더의 명령이라 왕관이 붙어야 한다");
		}

		// 개인의 변화 — 도주·공황·정신력·HP·동료 사망·지휘 붕괴에는 왕관이 없다.
		foreach (var id in new[]
		{
			SpeechId.FleeConfirmed, SpeechId.FleeWitnessed, SpeechId.EncounterPanic, SpeechId.PersonalRetreat, SpeechId.MentalPanic,
			SpeechId.HpLow, SpeechId.AllyDeathSeen, SpeechId.LeaderDeathAlarm, SpeechId.CommandCollapse, SpeechId.AbsentMember,
			// 사용자 허용 예외(2026-11-04): 소리 반응·"적이다!" 외침·조사·줍기 — 왕관 없는 일반 말풍선이다.
			SpeechId.EnemySpotted, SpeechId.SoundMove, SpeechId.SoundCombat, SpeechId.SoundScream, SpeechId.SoundDeath, SpeechId.SoundTrap, SpeechId.Investigate, SpeechId.LootPicked,
		})
		{
			Assert.IsTrue(SpeechCatalog.TryGet(id, out var e));
			Assert.IsFalse(e.LeaderOrder, $"{id}는 개인의 변화라 왕관이 붙으면 안 된다");
		}
	}

	[Test]
	public void ShouldShowCrown_OnlyForLeaderOrdersSpokenByTheActualCommander()
	{
		Assert.IsTrue(SpeechMath.ShouldShowCrown(true, true));
		Assert.IsFalse(SpeechMath.ShouldShowCrown(true, false), "리더 명령 대사라도 지휘자가 아닌 사람(승계 전 대신 말한 파티원 등)이 말하면 왕관이 없다");
		Assert.IsFalse(SpeechMath.ShouldShowCrown(false, true), "개인의 변화 대사는 리더가 말해도 왕관이 없다");
		Assert.IsFalse(SpeechMath.ShouldShowCrown(false, false));
	}

	[Test]
	public void TacticalCommandLines_OutrankMovementOrdersButNotPersonalCrisis()
	{
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.CmdStrike, out var strike));
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.RallyStart, out var rally));
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.FleeConfirmed, out var flee));
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.MentalPanic, out var panic));
		Assert.Greater(strike.Priority, rally.Priority, "전투 중 전술 명령이 집결 같은 이동 지시에 밀리면 안 된다");
		Assert.Less(strike.Priority, flee.Priority, "개인 도주 확정은 리더 명령에 가려지지 않는다");
		Assert.Less(strike.Priority, panic.Priority, "공황 진입은 리더 명령에 가려지지 않는다");
	}

	[Test]
	public void RestoredReactionLines_NeverOutrankLeaderOrders()
	{
		// 개인 반응(소리·적 발견·조사·줍기)은 리더 명령이 떠 있는 유닛에서 밀려야 한다 — 순위가 이동·집결 지시(60)보다 낮다.
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.RallyStart, out var rally));
		foreach (var id in new[] { SpeechId.EnemySpotted, SpeechId.SoundMove, SpeechId.SoundCombat, SpeechId.SoundScream, SpeechId.SoundDeath, SpeechId.SoundTrap, SpeechId.Investigate, SpeechId.LootPicked })
		{
			Assert.IsTrue(SpeechCatalog.TryGet(id, out var e));
			Assert.Less(e.Priority, rally.Priority, $"{id}가 리더 명령을 가리면 안 된다");
		}
	}

	[Test]
	public void CommandLinesWithSubject_FormatTargetNameAndFallBackWithoutIt()
	{
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.CmdSuppress, out var entry));
		Assert.AreEqual("고블린을 제압하라!", SpeechMath.Format(entry.Lines[0], "고블린"));
		string noSubject = SpeechMath.PickLine(entry.Lines, null, 0f);
		Assert.AreEqual("놈을 묶어라! 제압해!", noSubject, "대상 이름이 없으면 대상 없는 문구만 쓴다");
	}

	// 2026-11-04 사용자 요청: 리더 명령은 명령 대상을 대사에 밝힌다 — 대상 이름이 주어지면 어떤 문구가 뽑혀도 그 이름이 들어가야 한다.
	[Test]
	public void CommandLines_AlwaysNameTheTargetWhenOneIsKnown()
	{
		foreach (var id in new[] { SpeechId.CmdFocusAttack, SpeechId.CmdStrike, SpeechId.CmdSuppress, SpeechId.CmdProtect, SpeechId.CmdSuperiorFocus })
		{
			Assert.IsTrue(SpeechCatalog.TryGet(id, out var e));
			for (float r = 0f; r < 1f; r += 0.05f)
			{
				string line = SpeechMath.PickLine(e.Lines, "고블린", r);
				Assert.IsNotNull(line, $"{id}");
				StringAssert.Contains("고블린", line, $"{id}의 대사에 명령 대상이 빠졌다");
			}
		}
	}

	[Test]
	public void ObjectFocusLines_NameTheDoor_AndProtectSelfNamesNoOneElse()
	{
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.CmdObjectFocus, out var door));
		foreach (var line in door.Lines) StringAssert.Contains("문", line, "차단물 공동 파괴 대사는 부술 대상(문)을 가리켜야 한다");
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.CmdProtectSelf, out var self));
		foreach (var line in self.Lines) Assert.IsFalse(line.Contains("{S"), "리더 자신이 위급할 땐 다른 사람 이름을 넣지 않는다");
	}

	// 2026-11-04 사용자 요청: 개인 상태 변화 대사 중 체력·동료 사망은 직관적인 말로 — 무슨 일이 일어났는지 대사만 봐도 알 수 있어야 한다.
	[Test]
	public void PersonalChangeLines_HpAndAllyDeath_SayPlainlyWhatHappened()
	{
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.HpLow, out var hp));
		CollectionAssert.Contains(hp.Lines, "체력이 너무 낮아졌어!");
		foreach (var line in hp.Lines) StringAssert.Contains("체력", line);

		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.AllyDeathSeen, out var death));
		CollectionAssert.Contains(death.Lines, "동료가 죽었어..");
		foreach (var line in death.Lines) StringAssert.Contains("동료가 죽었", line);

		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.FleeSurvived, out var survived));
		CollectionAssert.AreEqual(new[] { "도망치지 않는다!" }, survived.Lines, "도주 판정을 버텨낸 대사");
	}

	// 2026-11-04 사용자 요청: "적이다!!!" 외침은 전파를 실시할 때만(받는 쪽은 상관없음), 리더는 외치지 않는다.
	[Test]
	public void EnemySpottedShout_OnlyWhenPropagating_AndNeverByTheLeader()
	{
		Assert.IsTrue(SpeechMath.ShouldShoutOnPropagation(true, false), "전파를 실시하는 일반 파티원은 외친다");
		Assert.IsFalse(SpeechMath.ShouldShoutOnPropagation(true, true), "파티 리더는 외치지 않는다");
		Assert.IsFalse(SpeechMath.ShouldShoutOnPropagation(false, false), "파티가 없으면 전파 자체가 없어 외치지 않는다");
		Assert.IsFalse(SpeechMath.ShouldShoutOnPropagation(false, true));
	}

	[Test]
	public void EnemySpotted_IsUnifiedToASingleShout()
	{
		Assert.IsTrue(SpeechCatalog.TryGet(SpeechId.EnemySpotted, out var e));
		CollectionAssert.AreEqual(new[] { "적이다!!!" }, e.Lines, "적 발견 외침은 \"적이다!!!\" 하나로 통일");
		Assert.AreEqual("적이다!!!", SpeechMath.PickLine(e.Lines, "고블린", 0.9f), "호출부가 넘기는 적 이름은 쿨다운 키일 뿐 문구에는 안 들어간다");
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
