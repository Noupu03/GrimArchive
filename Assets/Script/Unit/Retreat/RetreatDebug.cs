using Haare.Util.Logger;

// 후퇴·도주 플레이 확인용 debug 메뉴 동작(임시) — 명세서 7-1장 정신력 감소 입력 중 HP 피해·낮은 HP는 수치가 문서에 없어 연결되지 않았다. 그래서 자연 플레이로는 도주 판정까지 가기 어렵다. 값만 바꾸고 판정은 실제 경로(MentalSystem.ApplyChange·파티 평균 HP)를 그대로 탄다. 확인이 끝나면 지워도 되는 코드다.
public static class RetreatDebug
{
	// 시스템은 기본 켜짐이다 — 이 토글은 이상 동작이 의심될 때 이 세션만 끄는 용도(RetreatSystem.EnabledOverride).
	public static void ToggleSystem()
	{
		RetreatSystem.EnabledOverride = !RetreatSystem.Enabled;
		LogHelper.Log(LogHelper.GAME, $"[후퇴] (debug) 후퇴·도주 시스템 {(RetreatSystem.Enabled ? "ON" : "OFF")}");
	}

	// 선택한 인류의 정신력을 amount만큼 깎는다 — 일반 감소 보정·공황 구간 누적·도주 판정이 그대로 이어진다.
	public static void DropMental(Unit unit, float amount)
	{
		if (!(unit is Human human)) return;
		float before = human.BaseStat.mental;
		MentalSystem.ApplyChange(human, -amount);
		LogHelper.Log(LogHelper.GAME, $"[후퇴] (debug) {human.name} 정신력 {before:F1}→{human.BaseStat.mental:F1} (원수치, 공황은 −70 미만)");
	}

	// 선택한 인류 파티의 던전 안 생존자 전원 HP를 최대 HP의 percent%로 맞춘다 — 평균 HP 기준(일반 20%, 점령 40%)을 넘나드는 후퇴 시작 확인용.
	public static void SetPartyHpPercent(Unit unit, float percent)
	{
		if (!(unit is Human human) || human.party == null) return;
		int count = 0;
		foreach (var m in human.party.Members)
		{
			if (m == null || m.hp <= 0f || m.currentFloor < 1) continue;
			m.hp = m.maxHp * percent / 100f;
			count++;
		}
		LogHelper.Log(LogHelper.GAME, $"[후퇴] (debug) 파티 생존자 {count}명 HP를 {percent:F0}%로 설정");
	}
}
