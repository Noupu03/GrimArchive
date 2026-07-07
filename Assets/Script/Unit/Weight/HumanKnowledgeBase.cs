using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Haare.Util.Logger;

// "인류 전역 기록"의 실체 — 종별/개별 누적 가중치, 전멸 위험도 누적, 이해도 총량 한도/감소,
// 미표기 정보 오차를 관리한다. 타일·오브젝트·방 위험도/흥미도(15~21장, "지도" 데이터)는
// Human.personalMap(PersonalMapKnowledge)으로 개인유닛화되어 이 클래스에는 없다. 순수 C#
// (DataManager/UnitGenerate와 동일하게 VContainer Register<T>().AsSelf()로 등록,
// GameCompositionRoot.cs 참고).
//
// 웨이브 루프가 아직 게임에 없으므로(CLAUDE.md 참고), OnWaveEnd(survivors)는 실제 웨이브 종료
// 지점이 생기면 그때 호출하는 공개 API로 열어 둔다.
public class HumanKnowledgeBase
{
	private readonly Dictionary<string, SpeciesWeightState> _species = new();
	private readonly Dictionary<string, IndividualWeightState> _individuals = new();
	private readonly List<IncidentEntry> _pendingIncidents = new();
	private readonly System.Random _rng = new();

	public int CurrentWave { get; private set; } = 0;

	// ─────────────────────────── 종/개체 조회 ───────────────────────────
	private SpeciesWeightState GetOrCreateSpecies(string speciesKey)
	{
		if (!_species.TryGetValue(speciesKey, out var s))
		{
			s = new SpeciesWeightState(speciesKey);
			_species[speciesKey] = s;
		}
		return s;
	}

	private IndividualWeightState GetOrCreateIndividual(string unitId)
	{
		if (!_individuals.TryGetValue(unitId, out var s))
		{
			s = new IndividualWeightState(unitId);
			_individuals[unitId] = s;
		}
		return s;
	}

	// ─────────────────────────── 4장/10-1장. 이벤트 기록 (개인 즉시 반영 + 전역 반영 큐잉) ───────────────────────────
	// observer: 이 사건을 겪은/목격한 인류 유닛(개인 판단이 즉시 갱신됨).
	// target: 이해도/위험도의 대상이 되는 유닛(주로 몬스터). incidentId: 같은 사건을 여러 유닛이
	// 공유할 때 묶는 키 — 호출부(전투 이벤트 발생 지점)에서 사건 1건당 하나 발급해서 전달한다.
	public void RecordEvent(EventId id, Unit observer, Unit target, InfoType infoType, string incidentId)
	{
		if (!WeightEventTable.TryGet(id, out var delta))
		{
			LogHelper.Warning(LogHelper.GAME, $"[HumanKnowledgeBase] {id}는 이벤트 표에 없음 (3-2 흥미도 이벤트이거나 13장 동적 이벤트 — 전용 메서드를 쓸 것).");
			return;
		}

		string targetId = ResolveTargetKey(target);
		bool isIndividualTarget = target.isSpecialUnit;
		MentalErrorState mentalState = GetMentalState(observer);

		// 실제로 코드에서 호출되는(=지금 작동하는) 이벤트만 이 지점을 탄다 — 트랩/버프/소환처럼
		// 아직 연결 안 된 이벤트는 애초에 RecordEvent를 호출하는 코드가 없어서 이 로그도 안 찍힌다.
		LogHelper.Log($"<b><color=blue>[EventId:{id}]</color></b>", $"관찰자={observer.name} 대상={targetId} 정보유형={infoType} 이해도Δ={delta.Understanding} 위험도Δ={delta.Danger}");

		if (delta.Understanding != 0f)
			RecordEventForWeight(id, observer, targetId, isIndividualTarget, WeightType.Understanding, delta.Understanding, infoType, mentalState, incidentId);
		if (delta.Danger != 0f)
			RecordEventForWeight(id, observer, targetId, isIndividualTarget, WeightType.Danger, delta.Danger, infoType, mentalState, incidentId);
	}

	// 일반 유닛은 종별 누적(4-3장/7장), 특수 유닛(보스/네메시스)은 개별 누적(7-1장)을 쓰므로
	// 대상 식별 키도 그에 맞춰 갈라야 한다 — 일반 유닛의 target.name은 스폰마다 유일한 인스턴스명
	// (UnitGenerate: "{typeName}_{x}_{y}_{floor}")이라 종별 누적 키로 쓰면 GetUnitInterest/GetFinalDanger
	// 등이 조회하는 unitType.typeName과 어긋나 절대 매칭되지 않는다.
	private static string ResolveTargetKey(Unit target) => target.isSpecialUnit ? target.name : target.unitType.typeName;

	private void RecordEventForWeight(EventId id, Unit observer, string targetId, bool isIndividualTarget, WeightType type,
		float changeValue, InfoType infoType, MentalErrorState mentalState, string incidentId)
	{
		// 개인 즉시 반영 (4장)
		string key = PersonalWeightRecord.MakeKey(targetId, type);
		if (!observer.personalWeights.TryGetValue(key, out var record))
		{
			record = new PersonalWeightRecord(targetId, type);
			observer.personalWeights[key] = record;
		}
		record.StoredValue = WeightMath.ApplyPersonalChange(record.StoredValue, changeValue, type);
		record.LastInfoType = infoType;
		record.MentalStateAtRecord = mentalState;

		// 전역 반영 큐잉 (6장/10-2장 — 웨이브 종료 후 생존자 정보만 반영)
		_pendingIncidents.Add(new IncidentEntry(incidentId, id, targetId, type, infoType, changeValue, mentalState, observer.name, isIndividualTarget));
	}

	// 정신력 상태 → MentalErrorState 매핑. 문서(9-2장/23장)는 "정신력 저하 상태(공포/공황)"만 언급하고
	// 정확한 임계값은 전투 연산 문서로 위임했으므로, mental/maxMental 비율로 임의 기준을 잡았다
	// (25% 미만=공황, 50% 미만=공포). 구현현황 문서에 판단 근거 기재.
	public static MentalErrorState GetMentalState(Unit u)
	{
		if (u == null || u.maxMental <= 0f) return MentalErrorState.Normal;
		float ratio = u.mental / u.maxMental;
		if (ratio < 0.25f) return MentalErrorState.Panic;
		if (ratio < 0.5f) return MentalErrorState.Fear;
		return MentalErrorState.Normal;
	}

	// ─────────────────────────── 5장. 신규 진입 유닛 정보 ───────────────────────────
	// 개인 기억이 없는 신규 유닛 스폰 시, 알려진 모든 종/개체의 최신 전역값을 개인 초기값으로 복사한다.
	public void InitializeNewUnitPersonalInfo(Unit unit)
	{
		foreach (var kv in _species)
		{
			SetPersonalInitial(unit, kv.Key, WeightType.Understanding, kv.Value.UnderstandingStored);
			SetPersonalInitial(unit, kv.Key, WeightType.Danger, kv.Value.DangerAccumulatedStored);
		}
		foreach (var kv in _individuals)
		{
			SetPersonalInitial(unit, kv.Key, WeightType.Understanding, kv.Value.UnderstandingStored);
			SetPersonalInitial(unit, kv.Key, WeightType.Danger, kv.Value.DangerAccumulatedStored);
		}
	}

	private void SetPersonalInitial(Unit unit, string targetId, WeightType type, float value)
	{
		string key = PersonalWeightRecord.MakeKey(targetId, type);
		if (unit.personalWeights.ContainsKey(key)) return; // 이미 개인 기억이 있으면 덮어쓰지 않음(5-2장)
		unit.personalWeights[key] = new PersonalWeightRecord(targetId, type) { StoredValue = value };
	}

	// ─────────────────────────── 6장/8장/10-2장. 웨이브 종료 — 생존자 전역 반영 ───────────────────────────
	public void OnWaveEnd(List<Unit> survivors)
	{
		var survivorNames = new HashSet<string>(survivors.Select(u => u.name));
		var survivorEntries = _pendingIncidents.Where(e => survivorNames.Contains(e.ObserverUnitName)).ToList();

		var groups = survivorEntries.GroupBy(e => (e.IncidentId, e.TargetId, e.WeightType));
		foreach (var group in groups)
		{
			float amount = WeightMath.ComputeGlobalReflectionAmount(group.ToList());
			if (amount == 0f) continue;

			string targetId = group.Key.TargetId;
			// 기록 당시(RecordEvent)의 target.isSpecialUnit을 그대로 들고 온다 — 예전에는
			// _individuals.ContainsKey(targetId)로 추측했는데, 그건 GetUnderstanding류를 먼저
			// 한 번이라도 호출해야 우연히 채워지는 값이라 호출 순서에 따라 틀릴 수 있었다.
			bool isIndividual = group.First().IsIndividualTarget;

			if (group.Key.WeightType == WeightType.Understanding)
				ApplyUnderstandingGlobal(targetId, isIndividual, amount);
			else if (group.Key.WeightType == WeightType.Danger)
				ApplyDangerGlobal(targetId, isIndividual, amount);
		}

		_pendingIncidents.Clear();
		CurrentWave++;
		TickWipeoutTraces();
	}

	// ─────────────────────────── 7장/7-1장/8장. 이해도 전역 반영 (총량 한도 포함) ───────────────────────────
	private void ApplyUnderstandingGlobal(string targetId, bool isIndividual, float delta)
	{
		float currentTotal = ComputeUnderstandingTotal();

		// 감소 대상 후보: 2웨이브 이상 미갱신 것 중 하나(1순위) — 없으면 최대치 도달한 것(2순위, 여기선 단순화해
		// "가장 오래된 것"으로 통일 처리하고 판단 근거를 구현현황에 남긴다.
		var staleCandidate = FindStaleUnderstandingTarget();

		var overflow = staleCandidate != null
			? WeightMath.ComputePoolDecrease(currentTotal, delta, WeightMath.ValidationUnderstandingPoolCap, staleCandidate.Value.currentValue)
			: default;

		if (staleCandidate != null && overflow.ActualDecrease > 0f)
		{
			ApplyRawUnderstanding(staleCandidate.Value.key, staleCandidate.Value.isIndividual, -overflow.ActualDecrease, touchWave: false);
		}

		ApplyRawUnderstanding(targetId, isIndividual, delta, touchWave: true);
	}

	private void ApplyRawUnderstanding(string key, bool isIndividual, float delta, bool touchWave)
	{
		if (isIndividual)
		{
			var ind = GetOrCreateIndividual(key);
			ind.UnderstandingStored = WeightMath.Clamp(ind.UnderstandingStored + delta, WeightType.Understanding);
			if (touchWave) ind.LastUnderstandingUpdateWave = CurrentWave;
		}
		else
		{
			var sp = GetOrCreateSpecies(key);
			sp.UnderstandingStored = WeightMath.Clamp(sp.UnderstandingStored + delta, WeightType.Understanding);
			if (touchWave) sp.LastUnderstandingUpdateWave = CurrentWave;
		}
	}

	private float ComputeUnderstandingTotal()
		=> _species.Values.Sum(s => s.UnderstandingStored) + _individuals.Values.Sum(i => i.UnderstandingStored);

	private (string key, bool isIndividual, float currentValue)? FindStaleUnderstandingTarget()
	{
		(string key, bool isIndividual, float currentValue, int lastWave)? best = null;
		foreach (var kv in _species)
		{
			if (CurrentWave - kv.Value.LastUnderstandingUpdateWave < WeightMath.UnderstandingDecayStaleWaves) continue;
			if (best == null || kv.Value.LastUnderstandingUpdateWave < best.Value.lastWave)
				best = (kv.Key, false, kv.Value.UnderstandingStored, kv.Value.LastUnderstandingUpdateWave);
		}
		foreach (var kv in _individuals)
		{
			if (CurrentWave - kv.Value.LastUnderstandingUpdateWave < WeightMath.UnderstandingDecayStaleWaves) continue;
			if (best == null || kv.Value.LastUnderstandingUpdateWave < best.Value.lastWave)
				best = (kv.Key, true, kv.Value.UnderstandingStored, kv.Value.LastUnderstandingUpdateWave);
		}
		return best == null ? null : (best.Value.key, best.Value.isIndividual, best.Value.currentValue);
	}

	// 7-1장: 특수 유닛 최종 이해도 조회 (종별 50 + 개별 50 캡)
	public float GetUnderstanding(string speciesKey, string individualIdOrNull)
	{
		float speciesApplied = WeightMath.AppliedValue(GetOrCreateSpecies(speciesKey).UnderstandingStored);
		if (string.IsNullOrEmpty(individualIdOrNull)) return speciesApplied;
		float individualApplied = WeightMath.AppliedValue(GetOrCreateIndividual(individualIdOrNull).UnderstandingStored);
		return WeightMath.ComposeUnderstanding(speciesApplied, individualApplied, isSpecialUnit: true);
	}

	// ─────────────────────────── 10장/11장/12장/12-1장. 위험도 전역 반영/조회 ───────────────────────────
	private void ApplyDangerGlobal(string targetId, bool isIndividual, float delta)
	{
		bool isBossOrNemesis = isIndividual; // 개별 누적값을 갖는 대상 = 특수 유닛으로 취급
		if (isIndividual)
		{
			var ind = GetOrCreateIndividual(targetId);
			ind.DangerAccumulatedStored = delta >= 0f
				? Mathf.Min(ind.DangerAccumulatedStored + delta, WeightMath.DangerMax)
				: WeightMath.ApplyDangerDecrease(ind.DangerAccumulatedStored, delta, isBossOrNemesis);
		}
		else
		{
			var sp = GetOrCreateSpecies(targetId);
			sp.DangerAccumulatedStored = delta >= 0f
				? Mathf.Min(sp.DangerAccumulatedStored + delta, WeightMath.DangerMax)
				: WeightMath.ApplyDangerDecrease(sp.DangerAccumulatedStored, delta, isBossOrNemesis);
		}
	}

	// 11-1장: 총 피해량 기준 위험도 감소 판정 — 조건 충족 시 해당 종 위험도 -1
	public void ApplyTotalDamageDangerDecreaseCheck(string speciesKey, float totalDamageTaken, float baselineDamage)
	{
		if (WeightMath.TotalDamageDecreaseQualifies(totalDamageTaken, baselineDamage, out _))
			ApplyDangerGlobal(speciesKey, false, -1f);
	}

	// 12-1장: 최종 위험도 = 기본 + 종별 누적 + 개별 누적
	public float GetFinalDanger(string speciesKey, string individualIdOrNull, float baseDanger)
	{
		float species = GetOrCreateSpecies(speciesKey).DangerAccumulatedStored;
		float individual = string.IsNullOrEmpty(individualIdOrNull) ? 0f : GetOrCreateIndividual(individualIdOrNull).DangerAccumulatedStored;
		return WeightMath.ComposeFinalDanger(baseDanger, species, individual);
	}

	public DangerStage GetDangerStage(string speciesKey, string individualIdOrNull, float baseDanger)
		=> WeightMath.GetDangerStage(Mathf.FloorToInt(GetFinalDanger(speciesKey, individualIdOrNull, baseDanger)));

	// ─────────────────── 개인 지도(PersonalMapKnowledge)용 — 관찰자 개인 기준 즉시 반영 ───────────────────
	// GetFinalDanger/GetUnitInterest는 종/개체 "전역" 누적값을 쓰는데, 그건 OnWaveEnd가 호출돼야만
	// 갱신된다(6장) — 웨이브 루프가 없는 지금은 영원히 0이다. 반면 observer.personalWeights는
	// RecordEvent()가 호출되는 즉시(4장) 갱신되므로, 개인 지도에 기록할 값은 이쪽을 써야
	// "이벤트와 연결된 즉시 반영"이 된다. target.baseDanger/baseInterest를 기준값으로 삼고
	// 그 위에 이 관찰자 한 명의 개인 누적(personalWeights)만 얹는다(다른 관찰자의 경험은 섞이지 않음).
	public float GetPersonalDanger(Unit observer, Unit target)
	{
		string targetId = ResolveTargetKey(target);
		string key = PersonalWeightRecord.MakeKey(targetId, WeightType.Danger);
		float personalAccum = observer.personalWeights.TryGetValue(key, out var record) ? record.StoredValue : 0f;
		return WeightMath.ComposeFinalDanger(target.baseDanger, personalAccum, 0f);
	}

	// 18장 공식(흥미도 = 기본흥미도 × (100-이해도)%)은 그대로 쓰되, "이해도"를 전역 종별 이해도가
	// 아니라 이 관찰자 개인의 이해도(personalWeights)로 계산한다. 단, 이 공식 자체는 흥미도를
	// "증가"시키는 이벤트가 없다 — 이해도가 오를수록 감소만 한다(18장). 그래서 baseInterest가
	// 0으로 비어있는 유닛은 이 메서드도 여전히 0을 반환한다 — 이건 데이터(units.json) 문제이지
	// 이벤트 연동 문제가 아니다.
	public float GetPersonalInterest(Unit observer, Unit target)
	{
		if (target.isInterestTarget) return WeightMath.Clamp(target.baseInterest, WeightType.Interest);

		string targetId = ResolveTargetKey(target);
		string key = PersonalWeightRecord.MakeKey(targetId, WeightType.Understanding);
		float personalUnderstanding = observer.personalWeights.TryGetValue(key, out var record) ? record.StoredValue : 0f;
		int understandingApplied = Mathf.FloorToInt(WeightMath.Clamp(personalUnderstanding, WeightType.Understanding));
		return WeightMath.UnitInterestFromUnderstanding(target.baseInterest, understandingApplied);
	}

	// ─────────────────────────── 14장. 특수 행동 누적 ───────────────────────────
	public void AccumulateSpecialAction(string speciesKey, EventId kind, float delta)
	{
		var sp = GetOrCreateSpecies(speciesKey);
		switch (kind)
		{
			case EventId.E_SUMMON_SEEN: sp.AccumulateSummon(delta); break;
			case EventId.E_BUFF_SEEN: sp.AccumulateBuff(delta); break;
			case EventId.E_DEBUFF_SEEN: sp.AccumulateDebuff(delta); break;
		}
	}

	// ─────────────────────────── 13장. 전멸 위험도 ───────────────────────────
	private float _dungeonWipeoutDangerAccum;
	private float _wipeoutTraceGlobalAccum;

	private class WipeoutTraceRecord
	{
		public string TraceId;
		public int RemainingWaves;
		public DangerStage CauserStage;
		public bool Reflected;
	}
	private readonly Dictionary<string, WipeoutTraceRecord> _wipeoutTraces = new();

	public void OnPartyWipeout()
	{
		_dungeonWipeoutDangerAccum += WeightMath.PartyWipeoutDungeonDangerIncrease;
	}

	public string RegisterWipeoutTrace(DangerStage causerStage)
	{
		string traceId = Guid.NewGuid().ToString();
		_wipeoutTraces[traceId] = new WipeoutTraceRecord
		{
			TraceId = traceId,
			RemainingWaves = WeightMath.WipeoutTraceLifespanWaves,
			CauserStage = causerStage,
			Reflected = false,
		};
		return traceId;
	}

	// 전멸 흔적 발견 → 생환 시 1회만 전역 던전 위험도 반영 (13-2장)
	public void OnWipeoutTraceReflected(string traceId)
	{
		if (!_wipeoutTraces.TryGetValue(traceId, out var rec) || rec.Reflected) return;
		_wipeoutTraceGlobalAccum += WeightMath.WipeoutTraceGlobalReflection(rec.CauserStage);
		rec.Reflected = true;
	}

	private void TickWipeoutTraces()
	{
		var expired = new List<string>();
		foreach (var kv in _wipeoutTraces)
		{
			kv.Value.RemainingWaves--;
			if (kv.Value.RemainingWaves <= 0) expired.Add(kv.Key);
		}
		foreach (var id in expired) _wipeoutTraces.Remove(id);
	}

	// 15~17장/20~21장(타일·오브젝트·방 위험도/흥미도)은 2026-07-07부로 개인유닛화되어
	// PersonalMapKnowledge(Human.personalMap)로 이전했다 — 지도관련_정리 문서가 명시하듯 이 데이터는
	// 원래도 "인류 유닛별로 획득"되는 개인 인지 정보였다. 자세한 내용은
	// Assets/문서/GrimArchive_지도_구현현황.txt 참고.

	// 18장: 일반 유닛 흥미도 = 기본흥미도 × (100-이해도)%. IsInterestTarget이면 감소식 미적용(그대로 유지).
	public float GetUnitInterest(Unit unit)
	{
		if (unit.isInterestTarget) return WeightMath.Clamp(unit.baseInterest, WeightType.Interest);
		int understanding = Mathf.FloorToInt(GetUnderstanding(unit.unitType.typeName, unit.isSpecialUnit ? unit.name : null));
		return WeightMath.UnitInterestFromUnderstanding(unit.baseInterest, understanding);
	}

	// 19장: 시체/흔적 흥미도 = 기본값 + 원인대상 위험도단계 보정
	public float GetCorpseTraceInterest(string causerSpeciesKey, string causerIndividualIdOrNull, float causerBaseDanger)
	{
		var stage = GetDangerStage(causerSpeciesKey, causerIndividualIdOrNull, causerBaseDanger);
		return WeightMath.CorpseTraceInterest(stage);
	}

	// 20장/21장(방 위험도·흥미도)도 PersonalMapKnowledge로 이전했다 — 위 15~17장과 동일 사유.
	// 22장 "던전 전체 위험도/흥미도"의 방 합산 부분도 함께 옮겨갔으므로, 아래 두 메서드는 이제
	// 진영 차원에서 관리하는 파티전멸/전멸흔적 누적값만 반환한다(13장, 이번에 안 건드림). 방 데이터를
	// 포함한 진짜 "던전 전체" 값이 필요하면 Human.personalMap.GetPersonalDungeonDanger()/
	// GetPersonalDungeonInterest()(개인 인지 기준)를 쓴다 — 진영 전체 지도는 아직 없다.
	public float GetDungeonDanger() => _dungeonWipeoutDangerAccum + _wipeoutTraceGlobalAccum;

	public float GetDungeonInterest() => 0f;

	// ─────────────────────────── 9장/23장. 정보 오차 ───────────────────────────
	public int ApplyHiddenInfoNoise(int actualValue, int understandingApplied) => WeightMath.ApplyHiddenInfoNoise(actualValue, understandingApplied, _rng);

	public int ApplyInfoValueNoise(int actualValue, InfoType info, MentalErrorState mental)
	{
		var (_, ratio) = WeightMath.InfoError(info, mental);
		return WeightMath.ApplyValueNoise(actualValue, ratio, _rng);
	}
}
