using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Haare.Util.Logger;

// "인류 전역 기록"의 실체 — 종별/개별 누적 가중치, 전멸 위험도 누적, 이해도 총량 한도/감소,
// 미표기 정보 오차를 관리한다. 타일·오브젝트·방 위험도/흥미도(15~21장)는 Human.Memory.personalMap
// (PersonalMapKnowledge)으로 개인유닛화되어 이 클래스에는 없다. 순수 C#(VContainer Register<T>().AsSelf()).
// 실제 호출부는 Party.cs + GameSession.CheckPartyWaveState — 파티 전멸 시 OnPartyWipeout()/
// RegisterWipeoutTrace(), 웨이브 몬스터 전멸 시 OnWaveEnd(survivors)를 호출한다.
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
		=> RecordEventByKey(id, observer, ResolveTargetKey(target), target.isSpecialUnit, infoType, incidentId);

	// target Unit이 이미 Destroy됐거나 Unity 네이티브 프로퍼티에 안전하게 접근할 수 없을 때를 위한
	// 경로 — 호출부가 target이 살아있을 때 미리 스냅샷해 둔 종/개체 키를 직접 넘긴다. RecordEvent는
	// 이 메서드에 ResolveTargetKey(target)를 얹어 호출하는 얇은 래퍼다.
	public void RecordEventByKey(EventId id, Unit observer, string targetKey, bool isIndividualTarget, InfoType infoType, string incidentId)
	{
		if (!WeightEventTable.TryGet(id, out var delta))
		{
			LogHelper.Warning(LogHelper.GAME, $"[HumanKnowledgeBase] {id}는 이벤트 표에 없음 (3-2 흥미도 이벤트이거나 13장 동적 이벤트 — 전용 메서드를 쓸 것).");
			return;
		}

		MentalErrorState mentalState = GetMentalState(observer);

		if (delta.Understanding != 0f)
			RecordEventForWeight(id, observer, targetKey, isIndividualTarget, WeightType.Understanding, delta.Understanding, infoType, mentalState, incidentId);
		if (delta.Danger != 0f)
			RecordEventForWeight(id, observer, targetKey, isIndividualTarget, WeightType.Danger, delta.Danger, infoType, mentalState, incidentId);
	}

	// 일반 유닛은 종별 누적(4-3장/7장), 특수 유닛은 개별 누적(7-1장)을 쓰므로 키도 갈라야 한다 —
	// 일반 유닛의 target.name은 스폰마다 유일한 인스턴스명이라 종별 키로 쓰면 GetUnitInterest/
	// GetFinalDanger가 조회하는 unitType.typeName과 어긋난다.
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

	// 정신력 상태 → MentalErrorState 매핑. 문서가 정확한 임계값을 전투 연산 문서로 위임했으므로
	// mental/maxMental 비율로 임의 기준을 잡았다(25% 미만=공황, 50% 미만=공포).
	public static MentalErrorState GetMentalState(Unit u)
	{
		if (u == null || u.BaseStat.maxMental <= 0f) return MentalErrorState.Normal;
		float ratio = u.BaseStat.mental / u.BaseStat.maxMental;
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
			// 기록 당시(RecordEvent)의 target.isSpecialUnit을 그대로 들고 온다 — _individuals.ContainsKey
			// 추측은 호출 순서에 따라 틀릴 수 있어 쓰지 않는다.
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

	// 11-2장: 타격 1회별 기준 피해량 미만 판정 — 몬스터 인스턴스별 누적 감소량(전투당 상한 -1).
	// "같은 전투" 경계가 없어 이 몬스터 인스턴스가 살아있는 동안 전체를 하나의 전투로 근사한다.
	private readonly Dictionary<string, float> _perHitDecreaseAccum = new();

	// rawDamage: 방어/저항 적용 전 원래 피해량("몬스터에게 설정된 타격 1회별 기준 피해량" — 공격자의
	// 공격력 자체가 이 기준값 역할을 한다). actualDamage: 방어력/저항으로 실제 감소된 뒤 적용된 피해량.
	public void ApplyPerHitDangerDecreaseCheck(Unit monster, float rawDamage, float actualDamage)
	{
		if (!WeightMath.PerHitDecreaseQualifies(rawDamage, actualDamage, out _)) return;

		string key = monster.name;
		float accumulated = _perHitDecreaseAccum.GetValueOrDefault(key);
		if (accumulated <= WeightMath.PerHitDecreaseMaxAccumPerBattle) return; // 이미 -1 상한 도달

		float decrease = Mathf.Max(WeightMath.PerHitDecreaseValue, WeightMath.PerHitDecreaseMaxAccumPerBattle - accumulated);
		_perHitDecreaseAccum[key] = accumulated + decrease;

		ApplyDangerGlobal(ResolveTargetKey(monster), monster.isSpecialUnit, decrease);
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
	// GetFinalDanger/GetUnitInterest가 쓰는 종/개체 "전역" 누적값은 OnWaveEnd가 호출돼야 갱신된다(6장) —
	// 웨이브 루프가 없으면 영원히 0이다. observer.personalWeights는 RecordEvent 즉시(4장) 갱신되므로
	// 개인 지도는 이쪽을 쓴다. baseDanger/baseInterest를 기준값 삼아 이 관찰자의 개인 누적만 얹는다.
	public float GetPersonalDanger(Unit observer, Unit target)
	{
		string targetId = ResolveTargetKey(target);
		string key = PersonalWeightRecord.MakeKey(targetId, WeightType.Danger);
		float personalAccum = observer.personalWeights.TryGetValue(key, out var record) ? record.StoredValue : 0f;
		return WeightMath.ComposeFinalDanger(target.BaseStat.baseDanger, personalAccum, 0f);
	}

	// 18장 공식(흥미도 = 기본흥미도 × (100-이해도)%)을 그대로 쓰되 "이해도"를 전역 종별이 아닌 이
	// 관찰자 개인의 이해도(personalWeights)로 계산한다. baseInterest가 0인 유닛은 여전히 0을 반환
	// 하는데, 이는 데이터(units.json) 문제이지 이벤트 연동 문제가 아니다.
	public float GetPersonalInterest(Unit observer, Unit target)
	{
		if (target.isInterestTarget) return WeightMath.Clamp(target.BaseStat.baseInterest, WeightType.Interest);

		string targetId = ResolveTargetKey(target);
		string key = PersonalWeightRecord.MakeKey(targetId, WeightType.Understanding);
		float personalUnderstanding = observer.personalWeights.TryGetValue(key, out var record) ? record.StoredValue : 0f;
		int understandingApplied = Mathf.FloorToInt(WeightMath.Clamp(personalUnderstanding, WeightType.Understanding));
		return WeightMath.UnitInterestFromUnderstanding(target.BaseStat.baseInterest, understandingApplied);
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

	// 15~17장/20~21장(타일·오브젝트·방 위험도/흥미도)은 개인유닛화되어 PersonalMapKnowledge
	// (Human.Memory.personalMap)로 이전했다 — 원래도 인류 유닛별로 획득되는 개인 인지 정보였다.

	// 18장: 일반 유닛 흥미도 = 기본흥미도 × (100-이해도)%. IsInterestTarget이면 감소식 미적용(그대로 유지).
	public float GetUnitInterest(Unit unit)
	{
		if (unit.isInterestTarget) return WeightMath.Clamp(unit.BaseStat.baseInterest, WeightType.Interest);
		int understanding = Mathf.FloorToInt(GetUnderstanding(unit.unitType.typeName, unit.isSpecialUnit ? unit.name : null));
		return WeightMath.UnitInterestFromUnderstanding(unit.BaseStat.baseInterest, understanding);
	}

	// 19장: 시체/흔적 흥미도 = 기본값 + 원인대상 위험도단계 보정
	public float GetCorpseTraceInterest(string causerSpeciesKey, string causerIndividualIdOrNull, float causerBaseDanger)
	{
		var stage = GetDangerStage(causerSpeciesKey, causerIndividualIdOrNull, causerBaseDanger);
		return WeightMath.CorpseTraceInterest(stage);
	}

	// 20장/21장(방 위험도·흥미도)도 PersonalMapKnowledge로 이전했다 — 22장 방 합산 부분도 함께
	// 옮겨가서, 아래 두 메서드는 진영 차원에서 관리하는 파티전멸/전멸흔적 누적값만 반환한다(13장).
	public float GetDungeonDanger() => _dungeonWipeoutDangerAccum + _wipeoutTraceGlobalAccum;

	public float GetDungeonInterest() => 0f;

	// 22장 공식: 던전 전체 위험도 = 방 위험도 합산 + 파티 전멸 누적값 + 전멸 흔적 반영값.
	// "모든 기록된 방"은 진영 전체 지도가 없어 이 관찰자 개인이 아는 방 전체로 근사한다.
	public float GetDungeonDanger(Human observer) => observer.Memory.personalMap.GetPersonalDungeonDanger() + GetDungeonDanger();

	public float GetDungeonInterest(Human observer) => observer.Memory.personalMap.GetPersonalDungeonInterest();

	// ─────────────────────────── 9장/23장. 정보 오차 ───────────────────────────
	public int ApplyHiddenInfoNoise(int actualValue, int understandingApplied) => WeightMath.ApplyHiddenInfoNoise(actualValue, understandingApplied, _rng);

	public int ApplyInfoValueNoise(int actualValue, InfoType info, MentalErrorState mental)
	{
		var (_, ratio) = WeightMath.InfoError(info, mental);
		return WeightMath.ApplyValueNoise(actualValue, ratio, _rng);
	}

	// ─────────────────────────── 23장. 파티 입장 시 정보 오차 공유 ───────────────────────────
	// 같은 파티의 모든 유닛은 동일한 오차 수치를 공유해야 한다(23장) — ApplyHiddenInfoNoise/
	// ApplyInfoValueNoise는 호출마다 _rng로 새로 굴리므로, 파티+cacheKey 조합으로 최초 1회만
	// 계산하고 이후 같은 파티의 호출은 캐시된 값을 돌려준다.
	private readonly Dictionary<string, int> _partyInfoNoiseCache = new();

	private static string PartyCacheKey(Party party, string cacheKey) => party.Id + "|" + cacheKey;

	public int ApplyHiddenInfoNoiseForParty(Party party, string cacheKey, int actualValue, int understandingApplied)
	{
		string key = PartyCacheKey(party, cacheKey);
		if (_partyInfoNoiseCache.TryGetValue(key, out var cached)) return cached;
		int result = ApplyHiddenInfoNoise(actualValue, understandingApplied);
		_partyInfoNoiseCache[key] = result;
		return result;
	}

	public int ApplyInfoValueNoiseForParty(Party party, string cacheKey, int actualValue, InfoType info, MentalErrorState mental)
	{
		string key = PartyCacheKey(party, cacheKey);
		if (_partyInfoNoiseCache.TryGetValue(key, out var cached)) return cached;
		int result = ApplyInfoValueNoise(actualValue, info, mental);
		_partyInfoNoiseCache[key] = result;
		return result;
	}

	// 파티 재입장(같은 Party 인스턴스 재사용) 시 오차를 다시 산출하기 위한 API — 지금은 WaveSpawner가
	// 매번 새 Party를 만들어 캐시가 자연히 비므로 필수 호출은 아니다.
	public void ClearPartyInfoNoiseCache(Party party)
	{
		string prefix = party.Id + "|";
		var keysToRemove = new List<string>();
		foreach (var k in _partyInfoNoiseCache.Keys)
			if (k.StartsWith(prefix)) keysToRemove.Add(k);
		foreach (var k in keysToRemove) _partyInfoNoiseCache.Remove(k);
	}
}
