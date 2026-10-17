using UnityEngine;

// 정신력 변경의 단일 진입점(후퇴도주 문서 3장) — 정신력을 바꾸는 코드는 BaseStat.mental을 직접 건드리지 않고 여기를 거친다. 범위 −100~+100 제한은 항상 적용하고, 후퇴·도주 시스템이 켜져 있으면 일반 감소 보정(안정 단계 ×0.95)과 개인 도주 판정(FleeSystem)이 이어진다. 정신력이 없는 유닛(몬스터 등)은 무시한다.
public static class MentalSystem
{
	// 같은 파티원의 도주를 처음 직접 확인하거나 유효한 보고를 받았을 때의 기본 감소량(명세서 7-1장).
	public const float FleeWitnessBaseDecrease = 5f;

	// 정신력 변화량(음수 = 감소)을 적용한다. 실제로 적용된 변화량을 돌려준다. 한 사건으로 여러 번 도주 판정이 나와도 정신력 감소는 한 번만 적용되니 사건당 한 번만 부른다.
	public static float ApplyChange(Unit unit, float delta)
	{
		if (!(unit is Human human) || human.BaseStat.maxMental <= 0f || delta == 0f) return 0f;

		bool enabled = RetreatSystem.Enabled;
		float before = human.BaseStat.mental;
		float applied = (delta < 0f && enabled) ? MentalMath.ApplyDecreaseModifier(delta, before) : delta;
		float after = MentalMath.Clamp(before + applied);
		human.BaseStat.mental = after;

		if (enabled) FleeSystem.OnMentalChanged(human, before, after);
		return after - before;
	}

	// 격차 +3 이상 조우의 −71/−100 설정처럼 값을 강제로 정한다 — 일반 감소 보정으로 완화하지 않고, 설정 결과로 실제 하락한 양만 도주 판정에 한 번 집계한다.
	public static void SetForced(Unit unit, float newValue)
	{
		if (!(unit is Human human) || human.BaseStat.maxMental <= 0f) return;
		float before = human.BaseStat.mental;
		float after = MentalMath.Clamp(newValue);
		human.BaseStat.mental = after;
		if (RetreatSystem.Enabled) FleeSystem.OnMentalChanged(human, before, after);
	}

	// 거점 휴식 중 정신력 회복(원수치 +1/초) — 회복은 도주 판정 상태를 초기화한다(OnMentalChanged가 처리).
	public static void RecoverAtRest(Human human, float seconds)
	{
		if (seconds <= 0f || human.BaseStat.maxMental <= 0f) return;
		float before = human.BaseStat.mental;
		float after = PartyRetreatMath.NextMentalAtRest(before, seconds);
		if (after <= before) return;
		human.BaseStat.mental = after;
		FleeSystem.OnMentalChanged(human, before, after);
	}

	// 던전 밖 회복으로 정신력을 올린다 — 이미 floor 이상이면 건드리지 않고, 올렸다면 휴식 회복과 같이 도주 판정 상태를 초기화한다(OnMentalChanged). 올린 양을 돌려준다.
	public static float RecoverTo(Human human, float floor)
	{
		if (human == null || human.BaseStat.maxMental <= 0f) return 0f;
		float before = human.BaseStat.mental;
		float after = MentalMath.Clamp(Mathf.Max(before, floor));
		if (after <= before) return 0f;
		human.BaseStat.mental = after;
		FleeSystem.OnMentalChanged(human, before, after);
		return after - before;
	}

	// 같은 파티원의 도주를 처음 직접 확인하거나 유효한 보고를 받았을 때의 정신력 감소(기본 5, 일반 감소 보정 적용 — 안정 단계 4.75). 인류 유닛별·도주 사건별 1회이며 재확인·재보고·단순 부재·무응답·추정에는 감소가 없다. 감소를 적용했으면 true.
	public static bool OnFleeWitnessed(Human witness, Human fleeing)
	{
		if (!RetreatSystem.Enabled || witness == null || fleeing == null || witness == fleeing || witness.hp <= 0f) return false;
		var state = witness.Retreat;
		if (state == null || !state.WitnessedFlees.Add(fleeing.name)) return false;
		ApplyChange(witness, -FleeWitnessBaseDecrease);
		return true;
	}
}
