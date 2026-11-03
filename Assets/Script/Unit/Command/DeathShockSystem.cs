using System.Collections.Generic;
using UnityEngine;

// 리더·상위 지휘관 사망을 처음 안 파티원의 능력치 충격(리더 명령 18-1장): 알게 된 때부터 10초 동안 적용 대상 세부 스테이터스 20% 감소, 현재·최대 HP/MP 제외.
// 같은 사건의 재확인은 PartyDeathRecord.InfoKnownUnits가 막고(호출 자체가 안 일어남), 다른 지휘자의 사망은 별개 사건이라 20%를 중첩하지 않고 남은 시간만 새 10초로 갱신한다. 도주에는 적용하지 않는다(17-1-표1).
public static class DeathShockSystem
{
	private const string SourceKey = "leader.deathShock";

	private static readonly List<(DetailStat stat, float fraction)> Effects = BuildEffects();

	private static List<(DetailStat stat, float fraction)> BuildEffects()
	{
		var list = new List<(DetailStat, float)>();
		foreach (var stat in StatModifierLedger.AllDetailStats) list.Add((stat, -LeaderMath.DeathShockFraction));
		return list;
	}

	public static void Apply(Human receiver) => Apply(receiver, Time.time);

	public static void Apply(Human receiver, float now)
	{
		if (receiver == null || receiver.hp <= 0f || !LeaderCommandSettings.Enabled) return;
		var state = receiver.LeaderState;
		if (!state.Stats.Has(SourceKey)) state.Stats.Set(SourceKey, Effects); // 이미 걸려 있으면 곱셈 중첩 없이 그대로 두고 시간만 갱신한다
		state.DeathShockUntil = now + LeaderMath.DeathShockSeconds;
	}

	// UnitFunction.OnUpdate가 인류마다 부른다 — 충격이 없으면 한 줄 비교로 끝난다.
	public static void Tick(Human human) => Tick(human, Time.time);

	public static void Tick(Human human, float now)
	{
		var state = human.LeaderState;
		if (state == null || state.DeathShockUntil <= 0f || now < state.DeathShockUntil) return;
		state.DeathShockUntil = 0f;
		state.Stats.Remove(SourceKey);
	}
}
