using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace UnitDataTool
{
    public readonly struct DiffItem
    {
        public readonly string Path;
        public readonly string Left;     // 왼쪽(보통 JSON 쪽) 값, 없으면 "(없음)"
        public readonly string Right;    // 오른쪽(보통 프리팹 쪽) 값
        public DiffItem(string path, string left, string right) { Path = path; Left = left; Right = right; }
    }

    // 한 유닛의 두 표현(JSON ↔ 프리팹에서 읽은 값)을 필드 단위로 비교한다.
    //  · 스킬은 "전역 + 오버라이드"가 아니라 유닛이 실제로 쓰는 유효 값끼리 비교한다.
    //  · 기본값과 "없음"은 같은 취급이다(JSON은 기본값을 생략하므로).
    //  · unitClass는 프리팹이 모르는 값이라 비교하지 않는다.
    public static class UnitDiff
    {
        private const double Tolerance = 1e-4;
        public const string Missing = "(없음)";

        public static List<DiffItem> Compare(UnitDto left, List<SkillDto> leftSkills, UnitDto right, List<SkillDto> rightSkills)
        {
            var a = Flatten(left, leftSkills);
            var b = Flatten(right, rightSkills);

            var keys = new SortedSet<string>(a.Keys, StringComparer.Ordinal);
            keys.UnionWith(b.Keys);

            var diffs = new List<DiffItem>();
            foreach (string key in keys)
            {
                a.TryGetValue(key, out JValue va);
                b.TryGetValue(key, out JValue vb);
                if (LeafEquals(va, vb)) continue;
                diffs.Add(new DiffItem(key, Text(va), Text(vb)));
            }
            return diffs;
        }

        private static Dictionary<string, JValue> Flatten(UnitDto unit, List<SkillDto> effectiveSkills)
        {
            UnitDto copy = UnitDataJson.Clone(unit);
            copy.unitClass = null;
            copy.skillOverrides = new Dictionary<string, JObject>();
            JObject root = UnitDataJson.ToJObject(copy);

            var effective = new JObject();
            foreach (SkillDto s in effectiveSkills ?? new List<SkillDto>())
            {
                JObject sj = UnitDataJson.ToJObject(s);
                sj.Remove(nameof(SkillDto.skillName));
                effective[s.skillName] = sj;
            }
            root["effectiveSkills"] = effective;

            var leaves = new Dictionary<string, JValue>(StringComparer.Ordinal);
            Walk(root, "", leaves);
            return leaves;
        }

        private static void Walk(JToken token, string path, Dictionary<string, JValue> leaves)
        {
            switch (token)
            {
                case JObject obj:
                    foreach (JProperty p in obj.Properties())
                        Walk(p.Value, path.Length == 0 ? p.Name : path + "." + p.Name, leaves);
                    break;
                case JArray arr:
                    for (int i = 0; i < arr.Count; i++) Walk(arr[i], $"{path}[{i}]", leaves);
                    break;
                case JValue v:
                    leaves[path] = v;
                    break;
            }
        }

        private static bool LeafEquals(JValue a, JValue b)
        {
            if (a == null && b == null) return true;
            if (a == null) return IsDefault(b);
            if (b == null) return IsDefault(a);

            if (IsNumber(a) && IsNumber(b))
                return Math.Abs(Convert.ToDouble(a.Value, CultureInfo.InvariantCulture) - Convert.ToDouble(b.Value, CultureInfo.InvariantCulture)) <= Tolerance;
            if (a.Type == JTokenType.String && b.Type == JTokenType.String)
                return string.Equals(StripGuid((string)a.Value), StripGuid((string)b.Value), StringComparison.Ordinal);
            return JToken.DeepEquals(a, b);
        }

        // 한쪽에만 있는 값이 기본값(0/false/빈 문자열)이면 "같다"로 본다.
        private static bool IsDefault(JValue v)
        {
            if (v.Type == JTokenType.Null) return true;
            if (v.Type == JTokenType.Boolean) return !(bool)v.Value;
            if (IsNumber(v)) return Math.Abs(Convert.ToDouble(v.Value, CultureInfo.InvariantCulture)) <= Tolerance;
            if (v.Type == JTokenType.String) return string.IsNullOrEmpty((string)v.Value);
            return false;
        }

        private static bool IsNumber(JValue v) => v.Type == JTokenType.Integer || v.Type == JTokenType.Float;

        // "이름#GUID" 표기는 같은 에셋을 가리키므로 이름 부분만 비교한다.
        private static string StripGuid(string s)
        {
            if (s == null) return "";
            int i = s.IndexOf('#');
            return i < 0 ? s : s.Substring(0, i);
        }

        private static string Text(JValue v) => v == null ? Missing : (v.Type == JTokenType.Null ? "null" : Convert.ToString(v.Value, CultureInfo.InvariantCulture));
    }
}
