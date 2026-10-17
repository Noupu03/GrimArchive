using System.Collections.Generic;
using UnityEngine;

// 파티 후퇴·거점 휴식·재출격 판정(순수) — 후퇴도주 명세서 1~2장. 비율은 전부 % (0~100)이고, 평균은 구성원 개인 비율을 같은 비중으로 평균한다(총 HP ÷ 총 최대 HP 아님).
public static class PartyRetreatMath
{
	// 후퇴 시작 기준(8-1장) — HP·정신력 중 하나만 기준 이하여도 후퇴한다.
	public const float GeneralHpPercent = 20f, GeneralMentalPercent = 15f;
	public const float OccupyHpPercent = 40f, OccupyMentalPercent = 30f;
	// 거점 휴식 완료 기준 — 평균 HP·평균 정신력 두 조건을 모두 충족해야 한다.
	public const float RestCompleteHpPercent = 60f, RestCompleteMentalPercent = 40f;
	// 리더 없는 인류의 개인 후퇴 — 개인 HP 기준(정신력 비율만으로는 시작하지 않는다).
	public const float PersonalRetreatHpPercent = 20f;
	// 점령 파티 재출격 — 현장 생존 개체 수가 최초 출격 인원의 80% 이상(소수점 올림).
	public const int ResortieSurvivorPercent = 80;
	// 부동소수 경계 오차 — 0.3f×100 = 30.000002 같은 값이 "정확히 N%" 판정을 뒤집지 않게 한다(단위: %).
	public const float BoundaryEpsilon = 1e-3f;

	public readonly struct Thresholds
	{
		public readonly float HpPercent;
		public readonly float MentalPercent;
		public Thresholds(float hp, float mental) { HpPercent = hp; MentalPercent = mental; }
	}

	// 구성원 한 명의 현재 상태 — 개인 비율(%)로 이미 환산된 값.
	public readonly struct MemberVitals
	{
		public readonly float HpPercent;
		public readonly float MentalPercent;
		public MemberVitals(float hpPercent, float mentalPercent) { HpPercent = hpPercent; MentalPercent = mentalPercent; }

		public static MemberVitals From(float hp, float maxHp, float mentalRaw)
			=> new MemberVitals(PercentOf(hp, maxHp), MentalMath.ToRatioPercent(mentalRaw));
	}

	public readonly struct Averages
	{
		public readonly bool Valid; // 계산 대상 구성원이 한 명 이상
		public readonly int Count;
		public readonly float HpPercent;
		public readonly float MentalPercent;
		public Averages(int count, float hp, float mental) { Valid = count > 0; Count = count; HpPercent = hp; MentalPercent = mental; }
	}

	public static float PercentOf(float value, float max) => max <= 0f ? 0f : (float)((double)value / max * 100.0);

	// 일반 파티(탐색·회수·소탕)는 20/15, 점령 파티는 40/30.
	public static Thresholds ThresholdsFor(PartyType type)
		=> type == PartyType.Occupy ? new Thresholds(OccupyHpPercent, OccupyMentalPercent) : new Thresholds(GeneralHpPercent, GeneralMentalPercent);

	public static Averages ComputeAverages(IReadOnlyList<MemberVitals> members)
	{
		if (members == null || members.Count == 0) return new Averages(0, 0f, 0f);
		double hp = 0.0, mental = 0.0;
		for (int i = 0; i < members.Count; i++) { hp += members[i].HpPercent; mental += members[i].MentalPercent; }
		return new Averages(members.Count, (float)(hp / members.Count), (float)(mental / members.Count));
	}

	public static bool ShouldRetreat(in Averages avg, in Thresholds th)
		=> avg.Valid && (avg.HpPercent <= th.HpPercent + BoundaryEpsilon || avg.MentalPercent <= th.MentalPercent + BoundaryEpsilon);

	public static bool ShouldStartPersonalRetreat(float hpPercent) => hpPercent <= PersonalRetreatHpPercent + BoundaryEpsilon;

	// 평균 HP ≥60% 그리고 평균 정신력 ≥40%를 동시에 충족해야 휴식 종료. 개인별 최소 조건은 없다.
	public static bool IsRestComplete(in Averages avg)
		=> avg.Valid && avg.HpPercent >= RestCompleteHpPercent - BoundaryEpsilon && avg.MentalPercent >= RestCompleteMentalPercent - BoundaryEpsilon;

	// 휴식 종료 뒤 일반 파티: 목표 달성 → 출구, 미달성 → 재탐험(2-2장).
	public static PostRestAction DecidePostRest(bool goalAchieved)
		=> goalAchieved ? PostRestAction.LeaveDungeon : PostRestAction.Reexplore;

	// 후퇴 목적지(5-1장). 점령 파티는 방어 거점 → 기초 거점 → 출구 순, 일반 파티는 첫 후퇴만 거점(없으면 출구)이고 휴식을 마친 뒤의 재후퇴는 거점 재휴식 없이 출구다. 거점의 "가장 가까운 곳" 선택은 경로 거리가 필요해 이동 단계가 맡는다.
	public static RetreatDestination ChooseDestination(PartyType type, bool normalRestAlreadyDone, bool hasDefenseBase, bool hasBasicBase, out BaseKind baseKind)
	{
		baseKind = BaseKind.Basic;
		if (type == PartyType.Occupy)
		{
			if (hasDefenseBase) { baseKind = BaseKind.Defense; return RetreatDestination.Base; }
			if (hasBasicBase) return RetreatDestination.Base;
			return RetreatDestination.Exit;
		}
		if (normalRestAlreadyDone) return RetreatDestination.Exit;
		if (hasDefenseBase) { baseKind = BaseKind.Defense; return RetreatDestination.Base; }
		return hasBasicBase ? RetreatDestination.Base : RetreatDestination.Exit;
	}

	// 재출격에 필요한 현장 생존 인원 = 최초 출격 인원 N0의 80%를 올림(5명→4, 6명→5). 정수 연산이라 부동소수 오차가 없다.
	public static int RequiredResortieSurvivors(int initialCount)
		=> initialCount <= 0 ? 0 : (initialCount * ResortieSurvivorPercent + 99) / 100;

	// 재출격은 세 조건을 모두 충족해야 한다: ① 휴식 대상 평균 HP·정신력 회복 ② 현장 생존 인원 ③ 인류 지도상 안전한 진출 방. 시체·외출자·미집결자·미도착 충원은 fieldSurvivors에서 뺀 값을 넘긴다.
	public static bool CanResortie(in Averages restAvg, int fieldSurvivors, int initialCount, bool hasSafeAdvanceRoom)
		=> IsRestComplete(restAvg) && fieldSurvivors >= RequiredResortieSurvivors(initialCount) && hasSafeAdvanceRoom;

	// 거점 도착 직후 바로 휴식을 끝내지 않도록 막는 보류 — 첫 도착자가 건강하면 아직 걸어오는 파티원을 두고 평균 기준이 곧바로 충족돼 휴식이 사라진다. 문서는 "도착·집결한 인원"의 평균이라고만 정해(2-1장) 이 대기는 구현 판단이다: 살아 있는 비도주 파티원이 전부 도착했거나 첫 도착 뒤 grace초가 지나면 평가한다(0 이하면 문서 그대로 도착자만 평균).
	public static bool RestEvaluationReady(bool everyoneArrived, float firstArrivalTime, float now, float graceSeconds)
		=> everyoneArrived || graceSeconds <= 0f || now - firstArrivalTime >= graceSeconds;

	// 인류 지도상 "안전한 진출 방" 한 곳의 판정(2-3장 재출격 ③): 지도로 알고 있는(미확인 제외) 방이고 알려진 위험도가 maxDanger 이하. "안전"의 수치 기준은 문서가 정하지 않아 설정값(자리표시자)이다.
	public static bool IsSafeAdvanceRoom(bool roomKnown, float knownDanger, float maxDanger) => roomKnown && knownDanger <= maxDanger;

	// 거점 방 안에서 파티원 한 명의 휴식 자리를 고른다 — 방 중심에 가까운 순으로 isValid(걸을 수 있고 방 안이며 문 앞 통과 구간 밖)를 만족하고 아직 아무도 안 잡은 첫 칸. 없으면 false(호출부가 방 중심 근처로 폴백).
	public static bool TryPickRestSlot(RectInt bounds, System.Func<Vector2Int, bool> isValid, HashSet<Vector2Int> claimed, out Vector2Int slot)
	{
		slot = default;
		Vector2 center = bounds.center;
		float best = float.MaxValue;
		bool found = false;
		for (int x = bounds.xMin; x < bounds.xMax; x++)
		{
			for (int y = bounds.yMin; y < bounds.yMax; y++)
			{
				var p = new Vector2Int(x, y);
				if (claimed.Contains(p) || !isValid(p)) continue;
				float d = (new Vector2(x + 0.5f, y + 0.5f) - center).sqrMagnitude;
				if (d < best) { best = d; slot = p; found = true; }
			}
		}
		if (found) claimed.Add(slot);
		return found;
	}

	// HP 회복(2-1장): 탐험 중 재생력×시간, 휴식 중 재생력×2×시간. 최대 HP를 넘기지 않는다.
	public static float NextHp(float hp, float maxHp, float regenPerSecond, float seconds, bool resting)
		=> Mathf.Min(maxHp, hp + regenPerSecond * (resting ? 2f : 1f) * seconds);

	// 정신력 회복(2-1장): 휴식 중에만 원수치 +1/초(비율 +0.5%p/초), 상한 +100. 탐험 중 자연 회복은 없다.
	public const float RestMentalPerSecond = 1f;
	public static float NextMentalAtRest(float raw, float seconds) => MentalMath.Clamp(raw + RestMentalPerSecond * seconds);

	// 던전 밖 회복 바닥값 — 문서가 던전 밖 회복을 정하지 않아(후속 성장·회복 문서 몫) 문서가 "충분히 회복됐다"고 정한 거점 휴식 완료 기준(HP 60%·정신력 40%)을 쓴다. 이미 그 이상이면 그대로 둔다(내림 없음).
	public static float OutsideRecoveryHp(float hp, float maxHp) => maxHp <= 0f ? hp : Mathf.Max(hp, maxHp * RestCompleteHpPercent / 100f);
	public static float OutsideRecoveryMental(float raw) => Mathf.Max(raw, MentalMath.FromRatioPercent(RestCompleteMentalPercent));
}
