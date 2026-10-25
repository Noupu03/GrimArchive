using UnityEngine;

public enum DefeatReason
{
	None,
	NoSecuredRoom,      // 플레이어가 확보한 방이 0개
	NoUnitsOrBuildings, // 확보한 방은 있지만 유닛도 건물도 남지 않아 진행할 수 없음
}

// 패배 판정·패배 화면 연출 타이밍의 순수 계산 — 코어 점령 설계명세서 v0.2 1장. 세션 상태를 읽는 호출부는 RoomClearSystem.CheckDefeat, 화면은 DefeatScreen.
public static class DefeatMath
{
	// 패배 화면 연출(rythoom DeathScreen과 같은 순서): 배경이 FadeInSeconds에 걸쳐 나타나고, 그 뒤 ButtonDelaySeconds를 더 기다렸다가 버튼이 ButtonFadeInSeconds에 걸쳐 나타난다. 패배 직전에 누르던 클릭으로 버튼이 눌리는 사고를 막으려는 지연이다.
	public const float FadeInSeconds = 0.6f;
	public const float ButtonDelaySeconds = 1.0f;
	public const float ButtonFadeInSeconds = 0.4f;

	// hadSecuredRoom/hadAssets는 "한 번이라도 가졌던 적이 있는가"다. 게임 시작 직후 방 소유 동기화·초기 건물 설치가 끝나기 전이나 설치 자리를 못 찾은 경우에 곧바로 패배하지 않도록, 잃을 것을 가졌던 적이 있어야 잃은 것으로 본다.
	public static DefeatReason Evaluate(bool hadSecuredRoom, int securedRooms, bool hadAssets, int playerUnits, int buildings)
	{
		if (!hadSecuredRoom) return DefeatReason.None;
		if (securedRooms <= 0) return DefeatReason.NoSecuredRoom; // 인류 거점에 은신 유닛이 남아 있어도 게임 종료(1장)
		// 1장: 점령 방이 있고 유닛·건물 중 하나라도 남아 있으면 진행할 수 있다. 둘 다 없으면 진행할 수단이 없으므로 패배로 본다(문서는 이 경우의 결과를 따로 적지 않아 해석한 값).
		if (hadAssets && playerUnits <= 0 && buildings <= 0) return DefeatReason.NoUnitsOrBuildings;
		return DefeatReason.None;
	}

	public static string Describe(DefeatReason reason) => reason switch
	{
		DefeatReason.NoSecuredRoom => "확보한 방이 모두 사라졌습니다.",
		DefeatReason.NoUnitsOrBuildings => "더 이상 진행할 수 있는 유닛과 건물이 없습니다.",
		_ => string.Empty,
	};

	// 분:초 — 60분을 넘으면 분 자리가 그대로 커진다(rythoom 사망 화면과 같은 표기).
	public static string FormatElapsed(float seconds)
	{
		int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
		return $"{total / 60:00}:{total % 60:00}";
	}

	// 배경 페이드 진행(0~1) — Sine Out.
	public static float FadeAlpha(float secondsSinceShown)
		=> Mathf.Sin(Mathf.Clamp01(secondsSinceShown / FadeInSeconds) * Mathf.PI * 0.5f);

	// 버튼 불투명도(0~1) — 배경 페이드와 지연이 끝난 뒤에야 0을 벗어난다.
	public static float ButtonAlpha(float secondsSinceShown)
	{
		float t = secondsSinceShown - FadeInSeconds - ButtonDelaySeconds;
		if (t <= 0f) return 0f;
		return Mathf.Sin(Mathf.Clamp01(t / ButtonFadeInSeconds) * Mathf.PI * 0.5f);
	}

	// 버튼이 눌릴 수 있는 상태인가 — 보이기 시작한 뒤부터(알파 > 0).
	public static bool ButtonClickable(float secondsSinceShown) => ButtonAlpha(secondsSinceShown) > 0f;
}
