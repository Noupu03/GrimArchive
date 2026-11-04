using UnityEngine;
using Haare.Util.Logger;

// 리더·상위 지휘관의 기본 버프(리더 명령 14장). 기본 통솔력 20 이상인 리더가 소속 파티 전체에 거리와 무관하게 적용하며, 종류는 인류 파티 컨셉(초반 모험가=물리·마법 방어력, 초반 도적=물리·마법 공격력)이 정하고 증가율은 현재 통솔력 × 0.5%다.
// 상위 지휘관의 유효 기본 버프가 있으면 일반 리더의 버프는 종류·강도가 달라도 적용하지 않고, 상위 버프가 끝나면(사망·도주·원정 이탈) 살아 있는 일반 리더의 버프를 다시 적용한다. 후임은 종류를 유지하고 자기 통솔력으로 자격·강도를 다시 계산한다.
// 80·100 고유 스킬의 효과는 이 대체 규칙과 무관하다(고유 스킬 콘텐츠가 정한다).
public static class LeaderBuffSystem
{
	private const string SourceKey = "leader.basicBuff";
	private const float TickIntervalSeconds = 0.5f;

	// UnitFunction.OnUpdate가 인류마다 부른다 — 0.5초 스로틀. 현재 통솔력 변화에 따른 강도 갱신과 자격 변화를 여기서 따라간다.
	public static void Tick(Human human)
	{
		var state = human?.LeaderState;
		if (state == null) return;
		if (!LeaderCommandSettings.Enabled && state.BuffRate <= 0f && !state.Stats.Has(SourceKey)) return; // 꺼져 있고 되돌릴 버프도 없다
		float now = Time.time;
		if (now < state.NextBuffTick) return;
		state.NextBuffTick = now + TickIntervalSeconds;
		Refresh(human);
	}

	public static void RefreshParty(Party party)
	{
		if (party == null) return;
		foreach (var m in party.Members) if (m != null) Refresh(m);
	}

	public static void RefreshAll(GameSession session)
	{
		if (session?.parties == null) return;
		foreach (var p in session.parties) RefreshParty(p);
	}

	private static void Refresh(Human member)
	{
		var state = member.LeaderState;
		if (state == null) return;

		ResolveEffect(member, out PartyConcept concept, out float rate, out string sourceName);
		bool inParty = member.party != null && member.party.Members.Contains(member);
		if (!LeaderCommandSettings.Enabled || member.hp <= 0f || !inParty || rate <= 0f)
		{
			if (state.Stats.Has(SourceKey)) state.Stats.Remove(SourceKey);
			state.BuffRate = 0f;
			return;
		}
		if (state.Stats.Has(SourceKey) && Mathf.Approximately(state.BuffRate, rate) && state.BuffConcept == concept) return; // 같은 강도·종류면 건드리지 않는다

		state.Stats.Set(SourceKey, EffectsOf(concept, rate));
		state.BuffRate = rate;
		state.BuffConcept = concept;
		if (state.BuffSource != sourceName)
		{
			state.BuffSource = sourceName;
			LogHelper.Log(LogHelper.GAME, $"[지휘] {member.name}에게 {sourceName}의 기본 버프 적용 — {concept.ToKorean()} {rate * 100f:F1}%");
		}
	}

	// 이 파티원에게 적용될 기본 버프의 종류·증가율·출처. 상위 지휘관의 유효 버프가 있으면 그것만(일반 리더 버프는 적용하지 않는다).
	private static void ResolveEffect(Human member, out PartyConcept concept, out float rate, out string sourceName)
	{
		concept = PartyConcept.Adventurer;
		rate = 0f;
		sourceName = null;
		var party = member.party;
		if (party == null) return;

		var superior = ExpeditionCommandSystem.FindSuperior(member.Session);
		if (superior != null && superior.party != null && LeaderMath.HasBasicBuff(superior.BaseStat.leadership) && ExpeditionCommandSystem.InSameExpedition(superior.party, party))
		{
			concept = superior.party.Concept;
			rate = LeaderMath.BasicBuffRate(superior.BaseStat.leadership, superior.LeaderState.CurrentLeadership);
			sourceName = superior.name + "(상위)";
			return;
		}

		var leader = party.Leader;
		if (leader == null || leader.hp <= 0f || !LeaderMath.HasBasicBuff(leader.BaseStat.leadership)) return;
		concept = party.Concept; // 후임의 직업이 달라도 파티 종류가 버프 종류를 정한다
		rate = LeaderMath.BasicBuffRate(leader.BaseStat.leadership, leader.LeaderState.CurrentLeadership);
		sourceName = leader.name;
	}

	// 14-2-표1: 초반 모험가 = 물리 방어력·마법 방어력, 초반 도적 = 물리 공격력·마법 공격력. 그 밖의 종류는 콘텐츠 설계에서 효과를 확정할 때까지 효과 없음.
	public static System.Collections.Generic.List<(DetailStat stat, float fraction)> EffectsOf(PartyConcept concept, float rate)
	{
		var list = new System.Collections.Generic.List<(DetailStat, float)>();
		switch (concept)
		{
			case PartyConcept.Adventurer:
				list.Add((DetailStat.PhysicalDefense, rate));
				list.Add((DetailStat.MagicalDefense, rate));
				break;
			case PartyConcept.Rogue:
				list.Add((DetailStat.PhysicalAttack, rate));
				list.Add((DetailStat.MagicalAttack, rate));
				break;
		}
		return list;
	}
}
