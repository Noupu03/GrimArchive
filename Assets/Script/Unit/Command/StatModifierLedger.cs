using System.Collections.Generic;

// 리더 명령 문서의 세부 스테이터스 변화(기본 버프 14장, 사망 충격 18-1장)가 쓰는 조절 장부 — 출처 키마다 적용한 가산 델타를 기억해 정확히 되돌린다. SkillAction_PartyBuff의 "더하고 나중에 같은 양을 뺀다"와 같은 방식이라 서로 순서와 무관하게 합쳐진다.
// 18-1장 "감소는 세부 스테이터스에 한 번만 적용한다. 연결된 기본 능력치·표시값은 다시 계산하되 같은 20%를 다시 곱하지 않는다" — 델타를 세부 스테이터스(CombatStat/BaseStat/VisionStat)에만 적용하고 파생 능력치는 Unit.CalculateDerivedStats가 그 값에서 새로 계산한다.
public enum DetailStat
{
	PhysicalAttack, MagicalAttack, PhysicalDefense, MagicalDefense,
	AttackSpeed, CriticalChance, WalkSpeed, Reaction, StatusResistance, HPRegen, CooltimeReduction, Spotting,
}

public sealed class StatModifierLedger
{
	// 문서가 '세부 스테이터스'를 열거하지 않아 현재·최대 HP/MP, 정신력, 통솔력을 뺀 전투·이동·감각 12종을 대상으로 삼았다(구현현황 문서의 판단 기록).
	public static readonly DetailStat[] AllDetailStats =
	{
		DetailStat.PhysicalAttack, DetailStat.MagicalAttack, DetailStat.PhysicalDefense, DetailStat.MagicalDefense,
		DetailStat.AttackSpeed, DetailStat.CriticalChance, DetailStat.WalkSpeed, DetailStat.Reaction,
		DetailStat.StatusResistance, DetailStat.HPRegen, DetailStat.CooltimeReduction, DetailStat.Spotting,
	};

	private readonly Unit _owner;
	private readonly Dictionary<string, Dictionary<DetailStat, float>> _applied = new Dictionary<string, Dictionary<DetailStat, float>>();

	public StatModifierLedger(Unit owner) { _owner = owner; }

	public bool Has(string source) => _applied.ContainsKey(source);

	// 출처의 효과를 (스탯, 비율) 목록으로 다시 정한다 — 같은 출처의 이전 효과를 먼저 되돌린 뒤 그때의 현재값에 비율(+0.1 = 10% 증가, −0.2 = 20% 감소)을 곱해 가산한다. 같은 출처를 거듭 호출해도 중첩되지 않고 갱신만 된다.
	public void Set(string source, IEnumerable<(DetailStat stat, float fraction)> effects)
	{
		Remove(source, recalc: false);
		var deltas = new Dictionary<DetailStat, float>();
		foreach (var (stat, fraction) in effects)
		{
			if (fraction == 0f) continue;
			float delta = Get(stat) * fraction;
			Add(stat, delta);
			deltas[stat] = deltas.TryGetValue(stat, out float prev) ? prev + delta : delta;
		}
		if (deltas.Count > 0) _applied[source] = deltas;
		Recalculate();
	}

	public void Remove(string source) => Remove(source, recalc: true);

	private void Remove(string source, bool recalc)
	{
		if (!_applied.TryGetValue(source, out var deltas)) return;
		foreach (var kv in deltas) Add(kv.Key, -kv.Value);
		_applied.Remove(source);
		if (recalc) Recalculate();
	}

	public void Clear()
	{
		var keys = new List<string>(_applied.Keys);
		foreach (var k in keys) Remove(k, recalc: false);
		Recalculate();
	}

	// 현재 이 출처가 적용 중인 델타(테스트·진단용).
	public float AppliedDelta(string source, DetailStat stat)
		=> _applied.TryGetValue(source, out var d) && d.TryGetValue(stat, out float v) ? v : 0f;

	private void Recalculate() => _owner?.CalculateDerivedStats();

	public float Get(DetailStat stat)
	{
		switch (stat)
		{
			case DetailStat.PhysicalAttack:    return _owner.CombatStat.physicalAttack;
			case DetailStat.MagicalAttack:     return _owner.CombatStat.magicalAttack;
			case DetailStat.PhysicalDefense:   return _owner.CombatStat.physicalDefense;
			case DetailStat.MagicalDefense:    return _owner.CombatStat.magicalDefense;
			case DetailStat.AttackSpeed:       return _owner.CombatStat.attackspeed;
			case DetailStat.CriticalChance:    return _owner.CombatStat.criticalChance;
			case DetailStat.WalkSpeed:         return _owner.BaseStat.walkSpeed;
			case DetailStat.Reaction:          return _owner.BaseStat.reaction;
			case DetailStat.StatusResistance:  return _owner.BaseStat.statusResistance;
			case DetailStat.HPRegen:           return _owner.BaseStat.HPRegen;
			case DetailStat.CooltimeReduction: return _owner.BaseStat.cooltimeReduction;
			case DetailStat.Spotting:          return _owner.VisionStat.spotting;
			default:                           return 0f;
		}
	}

	private void Add(DetailStat stat, float delta)
	{
		switch (stat)
		{
			case DetailStat.PhysicalAttack:    _owner.CombatStat.physicalAttack += delta; break;
			case DetailStat.MagicalAttack:     _owner.CombatStat.magicalAttack += delta; break;
			case DetailStat.PhysicalDefense:   _owner.CombatStat.physicalDefense += delta; break;
			case DetailStat.MagicalDefense:    _owner.CombatStat.magicalDefense += delta; break;
			case DetailStat.AttackSpeed:       _owner.CombatStat.attackspeed += delta; break;
			case DetailStat.CriticalChance:    _owner.CombatStat.criticalChance += delta; break;
			case DetailStat.WalkSpeed:         _owner.BaseStat.walkSpeed += delta; break;
			case DetailStat.Reaction:          _owner.BaseStat.reaction += delta; break;
			case DetailStat.StatusResistance:  _owner.BaseStat.statusResistance += delta; break;
			case DetailStat.HPRegen:           _owner.BaseStat.HPRegen += delta; break;
			case DetailStat.CooltimeReduction: _owner.BaseStat.cooltimeReduction += delta; break;
			case DetailStat.Spotting:          _owner.VisionStat.spotting += delta; break;
		}
	}
}
