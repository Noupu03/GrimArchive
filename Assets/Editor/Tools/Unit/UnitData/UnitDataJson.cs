using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace UnitDataTool
{
    // units.json / skills.json 읽기·쓰기. 목표는 "읽고 다시 써도 원본과 같은 모양" — 저장할 때마다 파일 전체가
    // 재포맷되어 git diff가 커지는 걸 막는다. 원본 서식 관례(실측):
    //  · 들여쓰기 2칸, 줄바꿈 CRLF, BOM 없음, 끝 줄바꿈 있음
    //  · 원시값 배열은 한 줄: "footprint": [1, 1]
    //  · 숫자는 정수값이면 소수점 없이(150), 단 소수형 필드(walkSpeed·baseCooldown 등)는 "7.0", 0은 항상 "0"
    //  · 선택 필드는 기본값이면 생략
    public static class UnitDataJson
    {
        // 정수값이어도 "N.0"으로 쓰는 float 필드 이름(원본 파일에서 실측).
        private static readonly HashSet<string> DecimalStyle = new HashSet<string>
        {
            "walkSpeed", "baseCooldown", "damageMultiplier", "stunDuration", "priorityKillMultiplier",
            "priorityRangeThreshold", "projectileSpeed", "effectAmount", "explosionRadius", "effectDuration",
        };

        // 기본값이면 JSON에 쓰지 않는 SkillDto 필드.
        private static readonly HashSet<string> OptionalSkillFields = new HashSet<string>
        {
            "isProjectile", "projectilePrefab", "projectileSpeed", "isPiercing", "skillArchetype", "hitEffectPrefab",
            "effectAmount", "explosionRadius", "effectDuration", "multiHitCount",
        };

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Resolver SharedResolver = new Resolver();

        // ── 숫자 서식 ───────────────────────────────────────────────────
        public static string FormatFloat(float f, bool decimalStyle)
        {
            if (float.IsNaN(f) || float.IsInfinity(f) || f == 0f) return "0";
            if (f == (float)Math.Floor(f) && Math.Abs(f) < 1e9f)
            {
                string whole = ((long)f).ToString(Inv);
                return decimalStyle ? whole + ".0" : whole;
            }
            // 런타임(Mono)마다 "R" 결과가 달라서 가장 짧은 왕복 가능 표기를 직접 찾는다.
            string s = f.ToString("G7", Inv);
            if (float.Parse(s, Inv) != f) s = f.ToString("G9", Inv);
            return s;
        }

        public static bool IsDecimalStyleField(string name) => DecimalStyle.Contains(name);

        private sealed class FloatConverter : JsonConverter
        {
            private readonly bool _decimalStyle;
            public FloatConverter(bool decimalStyle) { _decimalStyle = decimalStyle; }
            public override bool CanRead => false;
            public override bool CanConvert(Type t) => t == typeof(float);
            public override object ReadJson(JsonReader r, Type t, object existing, JsonSerializer s) => throw new NotSupportedException();
            public override void WriteJson(JsonWriter w, object value, JsonSerializer s) => w.WriteRawValue(FormatFloat((float)value, _decimalStyle));
        }

        private sealed class Resolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                // 필드 선언 순서(메타데이터 토큰)대로 — 리플렉션 반환 순서에 기대지 않는다.
                IList<JsonProperty> props = base.CreateProperties(type, memberSerialization);
                return props.OrderBy(p => OrderOf(type, p)).ToList();
            }

            private static int OrderOf(Type type, JsonProperty p)
            {
                FieldInfo f = type.GetField(p.UnderlyingName, BindingFlags.Public | BindingFlags.Instance);
                return f != null ? f.MetadataToken : int.MaxValue;
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty p = base.CreateProperty(member, memberSerialization);
                if (p.PropertyType == typeof(float))
                    p.Converter = new FloatConverter(DecimalStyle.Contains(p.PropertyName));

                if (member.DeclaringType == typeof(SkillDto) && OptionalSkillFields.Contains(member.Name))
                {
                    JsonProperty captured = p;
                    p.ShouldSerialize = o => !IsDefault(captured.ValueProvider.GetValue(o));
                }
                else if (member.GetCustomAttribute<AssetRefAttribute>() != null)
                {
                    JsonProperty captured = p;
                    p.ShouldSerialize = o => !string.IsNullOrEmpty(captured.ValueProvider.GetValue(o) as string);
                }
                return p;
            }

            private static bool IsDefault(object v)
            {
                if (v == null) return true;
                if (v is string s) return s.Length == 0;
                if (v is bool b) return !b;
                if (v is float f) return f == 0f;
                if (v is int i) return i == 0;
                return false;
            }
        }

        // ── 읽기 ────────────────────────────────────────────────────────
        // problems: 모르는 키·형식 오류 같은 "저장하면 사라지거나 잘못 읽힌" 항목을 모아 돌려준다(읽기는 계속 진행).
        public static UnitsFileDto ReadUnits(string text, List<string> problems = null)
            => Deserialize<UnitsFileDto>(text, problems) ?? new UnitsFileDto();

        public static SkillsFileDto ReadSkills(string text, List<string> problems = null)
            => Deserialize<SkillsFileDto>(text, problems) ?? new SkillsFileDto();

        private static T Deserialize<T>(string text, List<string> problems) where T : class
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = SharedResolver,
                MissingMemberHandling = MissingMemberHandling.Error,
                DateParseHandling = DateParseHandling.None,
            };
            settings.Error = (sender, args) =>
            {
                string path = args.ErrorContext.Path ?? "?";
                string msg = args.ErrorContext.Error.Message;
                int cut = msg.IndexOf(". Path", StringComparison.Ordinal);
                if (cut > 0) msg = msg.Substring(0, cut);
                problems?.Add($"{path}: {msg}");
                args.ErrorContext.Handled = true;
            };
            return JsonConvert.DeserializeObject<T>(text, settings);
        }

        // ── 쓰기 ────────────────────────────────────────────────────────
        public static string WriteUnits(UnitsFileDto dto) => Write(WithFormattedOverrides(dto));
        public static string WriteSkills(SkillsFileDto dto) => Write(dto);

        private static JsonSerializerSettings WriteSettings()
        {
            var s = new JsonSerializerSettings
            {
                ContractResolver = SharedResolver,
                NullValueHandling = NullValueHandling.Ignore,
                DateParseHandling = DateParseHandling.None,
            };
            s.Converters.Add(new FloatConverter(false));
            return s;
        }

        private static string Write(object dto)
        {
            var sw = new StringWriter(new StringBuilder()) { NewLine = "\n" };
            using (var jw = new JsonTextWriter(sw) { Formatting = Formatting.Indented, Indentation = 2, IndentChar = ' ' })
                JsonSerializer.Create(WriteSettings()).Serialize(jw, dto);

            string raw = sw.ToString();
            string collapsed = CollapsePrimitiveArrays(raw);
            // 접은 결과가 의미까지 같은지 확인하고, 다르면 접지 않은 원본을 쓴다(서식보다 데이터 보존이 우선).
            if (!JToken.DeepEquals(JToken.Parse(raw), JToken.Parse(collapsed))) collapsed = raw;
            return collapsed.Replace("\n", "\r\n") + "\r\n";
        }

        // Newtonsoft는 배열 원소를 한 줄씩 펼친다 — 원시값만 든 배열은 원본 관례대로 한 줄로 접는다.
        public static string CollapsePrimitiveArrays(string text)
        {
            string[] lines = text.Split('\n');
            var output = new List<string>(lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.EndsWith("[", StringComparison.Ordinal))
                {
                    var items = new List<string>();
                    bool primitiveOnly = true;
                    int j = i + 1;
                    for (; j < lines.Length; j++)
                    {
                        string t = lines[j].Trim();
                        if (t == "]" || t == "],") break;
                        string item = t.EndsWith(",", StringComparison.Ordinal) ? t.Substring(0, t.Length - 1) : t;
                        if (item.Length == 0 || item[0] == '{' || item[0] == '[' || item[0] == '}' || item[0] == ']')
                        {
                            primitiveOnly = false;
                            break;
                        }
                        items.Add(item);
                    }
                    if (primitiveOnly && j < lines.Length && items.Count > 0)
                    {
                        output.Add(line + string.Join(", ", items) + lines[j].Trim());
                        i = j;
                        continue;
                    }
                }
                output.Add(line);
            }
            return string.Join("\n", output);
        }

        // skillOverrides의 숫자는 JObject 안이라 필드 이름을 모르는 변환기가 서식을 못 정한다 — 쓰기 직전에
        // 복사본의 숫자를 필드 종류에 맞는 원문(JRaw)으로 바꿔 전역 스킬과 같은 표기로 맞춘다.
        private static UnitsFileDto WithFormattedOverrides(UnitsFileDto dto)
        {
            if (dto.units.All(u => u.skillOverrides == null || u.skillOverrides.Count == 0)) return dto;

            UnitsFileDto copy = Clone(dto);
            foreach (UnitDto u in copy.units)
            {
                if (u.skillOverrides == null) continue;
                foreach (JObject ov in u.skillOverrides.Values)
                {
                    foreach (JProperty p in ov.Properties().ToList())
                    {
                        if (!(p.Value is JValue jv) || (jv.Type != JTokenType.Float && jv.Type != JTokenType.Integer)) continue;
                        FieldInfo fi = typeof(SkillDto).GetField(p.Name);
                        if (fi == null) continue;
                        string raw = fi.FieldType == typeof(float)
                            ? FormatFloat(Convert.ToSingle(jv.Value, Inv), DecimalStyle.Contains(p.Name))
                            : Convert.ToInt64(jv.Value, Inv).ToString(Inv);
                        p.Value = new JRaw(raw);
                    }
                }
            }
            return copy;
        }

        // ── 복제·직렬화 보조 ────────────────────────────────────────────
        public static T Clone<T>(T source) where T : class
        {
            if (source == null) return null;
            var sw = new StringWriter(new StringBuilder()) { NewLine = "\n" };
            using (var jw = new JsonTextWriter(sw) { Formatting = Formatting.None })
                JsonSerializer.Create(WriteSettings()).Serialize(jw, source);
            return Deserialize<T>(sw.ToString(), null);
        }

        // 비교용 평탄화에 쓰는 compact JSON (기본값 생략 규칙이 쓰기와 같다).
        public static JObject ToJObject(object dto)
        {
            var sw = new StringWriter(new StringBuilder()) { NewLine = "\n" };
            using (var jw = new JsonTextWriter(sw) { Formatting = Formatting.None })
                JsonSerializer.Create(WriteSettings()).Serialize(jw, dto);
            using (var reader = new JsonTextReader(new StringReader(sw.ToString())) { DateParseHandling = DateParseHandling.None })
                return JObject.Load(reader);
        }
    }
}
