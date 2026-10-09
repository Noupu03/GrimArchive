using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace UnitDataTool
{
    // 전역 스킬 정의(skills.json) + 유닛별 오버라이드(units.json의 skillOverrides)  ↔  유닛이 실제로 쓰는 "유효 스킬".
    //
    // 프리팹은 유닛마다 스킬 사본(SkillData)을 들고 있어서, 같은 이름의 스킬이 유닛마다 값이 다를 수 있다.
    // JSON은 이름당 전역 정의 1개 + "다른 필드만 적은 오버라이드"로 그걸 무손실로 표현한다.
    public static class SkillTools
    {
        private const float Tolerance = 1e-4f;

        // skillName을 뺀 비교·오버라이드 대상 필드(선언 순서).
        public static readonly FieldInfo[] ValueFields = typeof(SkillDto)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.Name != nameof(SkillDto.skillName))
            .OrderBy(f => f.MetadataToken)
            .ToArray();

        private static readonly Dictionary<string, FieldInfo> FieldByName = ValueFields.ToDictionary(f => f.Name);

        public static bool IsOverridableField(string name) => FieldByName.ContainsKey(name);
        public static Type FieldType(string name) => FieldByName.TryGetValue(name, out FieldInfo f) ? f.FieldType : null;

        public static SkillDto Clone(SkillDto s)
        {
            var c = new SkillDto { skillName = s.skillName };
            foreach (FieldInfo f in ValueFields) f.SetValue(c, f.GetValue(s));
            return c;
        }

        public static object GetValue(SkillDto s, string field) => FieldByName[field].GetValue(s);
        public static void SetValue(SkillDto s, string field, object value) => FieldByName[field].SetValue(s, value);

        public static bool ValuesEqual(object a, object b)
        {
            if (a is string || b is string) return string.Equals(a as string ?? "", b as string ?? "", StringComparison.Ordinal);
            if (a is float fa && b is float fb) return Math.Abs(fa - fb) <= Tolerance;
            return Equals(a, b);
        }

        // ── 유효 스킬 계산 ─────────────────────────────────────────────
        public static SkillDto ApplyOverride(SkillDto global, JObject ov, List<string> problems, string context)
        {
            SkillDto result = Clone(global);
            if (ov == null) return result;
            foreach (JProperty p in ov.Properties())
            {
                if (!FieldByName.TryGetValue(p.Name, out FieldInfo f))
                {
                    problems?.Add($"{context}: 오버라이드 필드 '{p.Name}'은(는) 스킬에 없는 필드입니다.");
                    continue;
                }
                try { f.SetValue(result, p.Value.ToObject(f.FieldType)); }
                catch (Exception e) { problems?.Add($"{context}: '{p.Name}' 값을 읽지 못했습니다 ({e.Message})."); }
            }
            return result;
        }

        public static List<SkillDto> Effective(UnitDto unit, IReadOnlyDictionary<string, SkillDto> globals, List<string> problems)
        {
            var list = new List<SkillDto>();
            foreach (string name in unit.skills)
            {
                if (!globals.TryGetValue(name, out SkillDto g))
                {
                    problems?.Add($"{unit.typeName}: 스킬 '{name}'의 전역 정의가 없습니다.");
                    continue;
                }
                JObject ov = null;
                unit.skillOverrides?.TryGetValue(name, out ov);
                list.Add(ApplyOverride(g, ov, problems, $"{unit.typeName}/{name}"));
            }
            return list;
        }

        public static Dictionary<string, SkillDto> IndexByName(IEnumerable<SkillDto> skills, List<string> problems = null)
        {
            var map = new Dictionary<string, SkillDto>();
            foreach (SkillDto s in skills)
            {
                if (string.IsNullOrEmpty(s.skillName)) { problems?.Add("이름이 없는 스킬 정의가 있습니다."); continue; }
                if (map.ContainsKey(s.skillName)) { problems?.Add($"스킬 이름 '{s.skillName}'이(가) 중복됩니다."); continue; }
                map[s.skillName] = s;
            }
            return map;
        }

        // ── 통합: 유닛별 유효 스킬 → 전역 정의 + 오버라이드 ───────────────
        public sealed class ConsolidateResult
        {
            public List<SkillDto> Globals = new List<SkillDto>();
            // 유닛 이름 → (스킬 이름 → 다른 필드만 담은 오버라이드)
            public Dictionary<string, Dictionary<string, JObject>> OverridesByUnit = new Dictionary<string, Dictionary<string, JObject>>();
        }

        // 필드마다 전역 기준값을 이렇게 고른다(오버라이드가 가장 적게 생기도록):
        //  1) 가장 많은 유닛이 쓰는 값
        //  2) 동률이면 기존 전역 값(동률 후보 중에 있을 때), 없으면 먼저 나온 유닛의 값
        // 어떤 경우에도 "전역 + 오버라이드 = 각 유닛의 유효 스킬"이 정확히 성립한다(무손실).
        public static ConsolidateResult Consolidate(IReadOnlyList<(string unit, List<SkillDto> skills)> perUnit, IReadOnlyList<SkillDto> existingGlobals)
        {
            var existing = new Dictionary<string, SkillDto>();
            foreach (SkillDto g in existingGlobals) if (!string.IsNullOrEmpty(g.skillName)) existing[g.skillName] = g;

            var order = new List<string>(existing.Keys);
            var users = new Dictionary<string, List<(string unit, SkillDto dto)>>();
            foreach ((string unit, List<SkillDto> skills) in perUnit)
            {
                foreach (SkillDto s in skills)
                {
                    if (!users.TryGetValue(s.skillName, out var list)) { list = new List<(string, SkillDto)>(); users[s.skillName] = list; }
                    list.Add((unit, s));
                    if (!order.Contains(s.skillName)) order.Add(s.skillName);
                }
            }

            var result = new ConsolidateResult();
            foreach (string name in order)
            {
                existing.TryGetValue(name, out SkillDto oldGlobal);
                if (!users.TryGetValue(name, out var skillUsers) || skillUsers.Count == 0)
                {
                    result.Globals.Add(oldGlobal);       // 아무도 안 쓰는 전역 정의는 그대로 둔다
                    continue;
                }

                SkillDto baseSkill = Clone(skillUsers[0].dto);
                foreach (FieldInfo f in ValueFields)
                {
                    object chosen = ChooseBase(f, skillUsers.Select(u => u.dto), oldGlobal);
                    f.SetValue(baseSkill, chosen);
                }
                result.Globals.Add(baseSkill);

                foreach ((string unit, SkillDto dto) in skillUsers)
                {
                    JObject ov = null;
                    foreach (FieldInfo f in ValueFields)
                    {
                        object v = f.GetValue(dto);
                        if (ValuesEqual(v, f.GetValue(baseSkill))) continue;
                        ov = ov ?? new JObject();
                        ov[f.Name] = JToken.FromObject(v ?? "");
                    }
                    if (ov == null) continue;
                    if (!result.OverridesByUnit.TryGetValue(unit, out var map)) { map = new Dictionary<string, JObject>(); result.OverridesByUnit[unit] = map; }
                    map[name] = ov;
                }
            }
            return result;
        }

        private static object ChooseBase(FieldInfo f, IEnumerable<SkillDto> users, SkillDto oldGlobal)
        {
            var groups = new List<(object value, int count)>();
            foreach (object v in users.Select(u => f.GetValue(u)))
            {
                int idx = groups.FindIndex(g => ValuesEqual(g.value, v));
                if (idx < 0) groups.Add((v, 1)); else groups[idx] = (groups[idx].value, groups[idx].count + 1);
            }

            int max = groups.Max(g => g.count);
            if (oldGlobal != null)
            {
                object old = f.GetValue(oldGlobal);
                foreach (var g in groups) if (g.count == max && ValuesEqual(g.value, old)) return old;
            }
            return groups.First(g => g.count == max).value;
        }
    }
}
