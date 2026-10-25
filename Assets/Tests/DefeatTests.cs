#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

// ========================================================================
// 패배 판정·연출 타이밍 순수 계산 테스트(NUnit, #if UNITY_INCLUDE_TESTS) — 코어 점령 설계명세서 v0.2 1장.
// 세션 상태를 읽는 RoomClearSystem.CheckDefeat와 OnGUI 화면(DefeatScreen)은 플레이 확인 대상이다.
// ========================================================================

public class DefeatTests
{
	// ── 1장: 플레이어 점령 방이 0개면 패배 ──

	[Test]
	public void NoSecuredRoom_IsDefeat_EvenWithUnitsAndBuildingsLeft()
	{
		// 인류 거점에 은신 유닛이 살아 있는 것과 마찬가지로, 플레이어 쪽 유닛·건물이 남아 있어도 점령 방이 0개면 게임 종료다.
		Assert.AreEqual(DefeatReason.NoSecuredRoom, DefeatMath.Evaluate(true, 0, true, 5, 3));
	}

	[Test]
	public void SecuredRoomWithUnitsOrBuildings_KeepsPlaying()
	{
		Assert.AreEqual(DefeatReason.None, DefeatMath.Evaluate(true, 1, true, 4, 0), "건물이 없어도 유닛이 남아 있으면 진행할 수 있다");
		Assert.AreEqual(DefeatReason.None, DefeatMath.Evaluate(true, 1, true, 0, 2), "유닛이 없어도 건물이 남아 있으면(생산으로 만회) 진행할 수 있다");
	}

	[Test]
	public void SecuredRoomButNothingLeft_IsDefeat()
	{
		Assert.AreEqual(DefeatReason.NoUnitsOrBuildings, DefeatMath.Evaluate(true, 2, true, 0, 0));
	}

	[Test]
	public void RoomLossTakesPrecedenceOverNothingLeft()
	{
		Assert.AreEqual(DefeatReason.NoSecuredRoom, DefeatMath.Evaluate(true, 0, true, 0, 0));
	}

	// ── 시작 직후 오탐 방지 ──

	[Test]
	public void NeverHadASecuredRoom_NeverDefeats()
	{
		// 방 소유 동기화 전 첫 틱 — 아직 확보한 방이 없었으니 잃은 것도 없다.
		Assert.AreEqual(DefeatReason.None, DefeatMath.Evaluate(false, 0, false, 0, 0));
		Assert.AreEqual(DefeatReason.None, DefeatMath.Evaluate(false, 0, true, 3, 3));
	}

	[Test]
	public void NeverHadAssets_NoUnitsOrBuildingsIsNotDefeat()
	{
		// 시작방에 초기 건물을 놓을 자리를 못 찾아 건물도 유닛도 없이 시작한 경우 — 잃은 게 없으므로 곧바로 패배하지 않는다.
		Assert.AreEqual(DefeatReason.None, DefeatMath.Evaluate(true, 1, false, 0, 0));
	}

	[Test]
	public void Describe_HasTextForEveryDefeatReason()
	{
		Assert.IsNotEmpty(DefeatMath.Describe(DefeatReason.NoSecuredRoom));
		Assert.IsNotEmpty(DefeatMath.Describe(DefeatReason.NoUnitsOrBuildings));
		Assert.IsEmpty(DefeatMath.Describe(DefeatReason.None));
	}

	// ── 한 판의 기록 표기 ──

	[TestCase(0f, "00:00")]
	[TestCase(59.9f, "00:59")]
	[TestCase(61f, "01:01")]
	[TestCase(3725f, "62:05")]
	[TestCase(-5f, "00:00")]
	public void FormatElapsed_IsMinutesAndSeconds(float seconds, string expected)
	{
		Assert.AreEqual(expected, DefeatMath.FormatElapsed(seconds));
	}

	// ── 연출 타이밍(rythoom 사망 화면: 배경 0.6초 → 1초 대기 → 버튼 0.4초) ──

	[Test]
	public void BackgroundFade_RunsZeroToOneOverFadeDuration()
	{
		Assert.AreEqual(0f, DefeatMath.FadeAlpha(0f), 1e-5f);
		Assert.AreEqual(1f, DefeatMath.FadeAlpha(DefeatMath.FadeInSeconds), 1e-5f);
		Assert.AreEqual(1f, DefeatMath.FadeAlpha(DefeatMath.FadeInSeconds + 10f), 1e-5f);
		Assert.Less(DefeatMath.FadeAlpha(0.2f), DefeatMath.FadeAlpha(0.4f), "단조 증가");
	}

	[Test]
	public void Button_StaysHiddenUntilFadeAndDelayHavePassed()
	{
		float appearAt = DefeatMath.FadeInSeconds + DefeatMath.ButtonDelaySeconds;
		Assert.AreEqual(0f, DefeatMath.ButtonAlpha(0f), 1e-5f);
		Assert.AreEqual(0f, DefeatMath.ButtonAlpha(DefeatMath.FadeInSeconds + 0.5f), 1e-5f, "배경 페이드가 끝나도 지연 동안은 숨겨 둔다(사망 직전 클릭으로 눌리는 사고 방지)");
		Assert.IsFalse(DefeatMath.ButtonClickable(appearAt), "나타나기 시작하는 순간(알파 0)에는 아직 누를 수 없다");
		Assert.IsTrue(DefeatMath.ButtonClickable(appearAt + 0.05f));
		Assert.AreEqual(1f, DefeatMath.ButtonAlpha(appearAt + DefeatMath.ButtonFadeInSeconds), 1e-5f);
		Assert.AreEqual(1f, DefeatMath.ButtonAlpha(appearAt + 30f), 1e-5f);
	}
}
#endif
