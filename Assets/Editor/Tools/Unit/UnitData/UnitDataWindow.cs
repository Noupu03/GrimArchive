using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GrimArchive.Wave;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnitDataTool
{
    // 유닛 데이터 편집기 — units.json / skills.json을 편집하고 유닛 프리팹과 양방향으로 맞춘다.
    //  · 열면 JSON과 프리팹을 둘 다 읽어 유닛별 상태(동기화됨 / 다름 n / 프리팹 없음 / JSON 없음)를 보여 준다.
    //  · 모든 편집은 작업 사본에만 적용되고, [JSON 저장]을 눌러야 파일에 반영된다(저장 안 하고 닫으면 Unity가 묻는다).
    //  · 프리팹 → JSON 가져오기는 작업 사본만 바꾼다(저장 전에 검토 가능). JSON → 프리팹 적용은 체크한 유닛만,
    //    기존 프리팹을 제자리 갱신한다(JSON 스키마에 없는 항목은 보존).
    //  · 런타임은 이 JSON을 읽지 않는다 — 런타임의 진실은 프리팹이고 JSON은 작성·교환용이다.
    public partial class UnitDataWindow : EditorWindow
    {
        private const string UnitsPath = "Assets/Data/units.json";
        private const string SkillsPath = "Assets/Data/skills.json";

        [MenuItem("Tools(new)/유닛/유닛 데이터 편집기 (JSON ↔ 프리팹)")]
        public static void Open()
        {
            var window = GetWindow<UnitDataWindow>();
            window.titleContent = new GUIContent("유닛 데이터");
            window.minSize = new Vector2(980, 560);
            window.Show();
        }

        // ── 도메인 리로드(스크립트 재컴파일)에서 살아남는 상태 ──────────────
        [SerializeField] private string _workUnits;
        [SerializeField] private string _workSkills;
        [SerializeField] private string _baseUnits;
        [SerializeField] private string _baseSkills;
        [SerializeField] private List<string> _checked = new List<string>();
        [SerializeField] private List<string> _pendingPrefabDeletes = new List<string>();
        [SerializeField] private string _selectedUnit;
        [SerializeField] private string _selectedSkill;
        [SerializeField] private string _lastMessage;
        [SerializeField] private int _tab;
        [SerializeField] private long _unitsStamp;
        [SerializeField] private long _skillsStamp;

        // ── 런타임 상태 ─────────────────────────────────────────────────
        private UnitsFileDto _units;
        private SkillsFileDto _skills;
        private readonly Dictionary<string, PrefabSnapshot> _snapshots = new Dictionary<string, PrefabSnapshot>();
        private readonly Dictionary<string, List<DiffItem>> _diffs = new Dictionary<string, List<DiffItem>>();
        private List<string> _loadProblems = new List<string>();
        private List<string> _parity = new List<string>();
        private bool _externalChange;
        private bool _formatWillChange;

        private string UnitsText => UnitDataJson.WriteUnits(_units);
        private string SkillsText => UnitDataJson.WriteSkills(_skills);

        // ── 수명 주기 ───────────────────────────────────────────────────
        private void OnEnable()
        {
            saveChangesMessage = "저장하지 않은 유닛 데이터 변경이 있습니다. 저장할까요?";
            AssemblyReloadEvents.beforeAssemblyReload += StashWorkingCopy;

            if (!string.IsNullOrEmpty(_workUnits) && !string.IsNullOrEmpty(_workSkills)) RestoreWorkingCopy();
            else LoadFromDisk();

            _parity = PrefabUnitReader.CheckSchemaParity();
            RefreshSnapshots();
            UpdateDirtyFlag();
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= StashWorkingCopy;
        }

        private void OnFocus()
        {
            if (_units == null) return;
            if (StampOf(UnitsPath) == _unitsStamp && StampOf(SkillsPath) == _skillsStamp) return;
            if (hasUnsavedChanges) { _externalChange = true; return; }
            LoadFromDisk();
            RefreshSnapshots();
            Message("디스크의 JSON이 바뀌어 다시 불러왔습니다.", log: false);
        }

        private static long StampOf(string path) => File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;

        // ── 불러오기 / 저장 ─────────────────────────────────────────────
        private void LoadFromDisk()
        {
            var problems = new List<string>();
            string ut = File.Exists(UnitsPath) ? File.ReadAllText(UnitsPath, new UTF8Encoding(false)) : "{\"units\": []}";
            string st = File.Exists(SkillsPath) ? File.ReadAllText(SkillsPath, new UTF8Encoding(false)) : "{\"skills\": []}";
            _units = UnitDataJson.ReadUnits(ut, problems);
            _skills = UnitDataJson.ReadSkills(st, problems);
            _loadProblems = problems;

            _baseUnits = UnitsText;
            _baseSkills = SkillsText;
            // 저장하면 파일 모양이 바뀌는지(폐기 키 제거·키 순서 정리) 미리 알려 준다.
            _formatWillChange = ut != _baseUnits || st != _baseSkills;

            _pendingPrefabDeletes.Clear();
            _externalChange = false;
            _unitsStamp = StampOf(UnitsPath);
            _skillsStamp = StampOf(SkillsPath);

            if (string.IsNullOrEmpty(_selectedUnit) || _units.units.All(u => u.typeName != _selectedUnit))
                _selectedUnit = _units.units.Count > 0 ? _units.units[0].typeName : null;
            if (string.IsNullOrEmpty(_selectedSkill) || _skills.skills.All(s => s.skillName != _selectedSkill))
                _selectedSkill = _skills.skills.Count > 0 ? _skills.skills[0].skillName : null;
            _checked.RemoveAll(n => _units.units.All(u => u.typeName != n));
            hasUnsavedChanges = false;
        }

        private void StashWorkingCopy()
        {
            if (_units == null) return;
            _workUnits = UnitsText;
            _workSkills = SkillsText;
        }

        private void RestoreWorkingCopy()
        {
            _units = UnitDataJson.ReadUnits(_workUnits);
            _skills = UnitDataJson.ReadSkills(_workSkills);
        }

        public override void SaveChanges()
        {
            if (TrySave()) base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            LoadFromDisk();
            RefreshSnapshots();
            base.DiscardChanges();
        }

        private void UpdateDirtyFlag()
        {
            if (_units == null) return;
            bool dirty = UnitsText != _baseUnits || SkillsText != _baseSkills || _pendingPrefabDeletes.Count > 0;
            if (hasUnsavedChanges != dirty) hasUnsavedChanges = dirty;
        }

        // 데이터가 바뀐 뒤 부른다: 비교 갱신 + 저장 필요 표시.
        private void OnDataChanged()
        {
            RecomputeDiffs();
            UpdateDirtyFlag();
            Repaint();
        }

        private bool TrySave()
        {
            List<string> errors = ValidateForSave();
            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("저장할 수 없습니다", string.Join("\n", errors.Take(14)) + (errors.Count > 14 ? $"\n… 외 {errors.Count - 14}건" : ""), "확인");
                return false;
            }

            var refProblems = new List<string>();
            CollectRefProblems(_units, "units", refProblems);
            CollectRefProblems(_skills, "skills", refProblems);
            foreach (UnitDto u in _units.units) CollectOverrideRefProblems(u, refProblems);
            if (refProblems.Count > 0 && !EditorUtility.DisplayDialog("해결되지 않은 에셋 참조",
                    $"찾을 수 없거나 모호한 에셋 참조가 {refProblems.Count}건 있습니다:\n" + string.Join("\n", refProblems.Take(8)) +
                    (refProblems.Count > 8 ? "\n…" : "") + "\n\n그대로 저장할까요?", "저장", "취소"))
                return false;

            string ut = UnitsText, st = SkillsText;
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(UnitsPath, ut, utf8);
            File.WriteAllText(SkillsPath, st, utf8);
            AssetDatabase.ImportAsset(UnitsPath);
            AssetDatabase.ImportAsset(SkillsPath);
            _baseUnits = ut;
            _baseSkills = st;
            _unitsStamp = StampOf(UnitsPath);
            _skillsStamp = StampOf(SkillsPath);
            _formatWillChange = false;
            _externalChange = false;

            int trashed = 0;
            foreach (string name in _pendingPrefabDeletes)
                if (AssetDatabase.MoveAssetToTrash(PrefabUnitReader.PathFor(name))) trashed++;
            _pendingPrefabDeletes.Clear();

            RefreshSnapshots();
            UpdateDirtyFlag();
            Message($"저장했습니다 — 유닛 {_units.units.Count}개, 스킬 {_skills.skills.Count}개" + (trashed > 0 ? $", 프리팹 {trashed}개를 휴지통으로 이동" : ""));
            return true;
        }

        private List<string> ValidateForSave()
        {
            var errors = new List<string>();
            var seen = new HashSet<string>();
            foreach (UnitDto u in _units.units)
            {
                if (string.IsNullOrWhiteSpace(u.typeName)) { errors.Add("이름이 없는 유닛이 있습니다."); continue; }
                if (!seen.Add(u.typeName)) errors.Add($"유닛 이름 '{u.typeName}'이(가) 중복됩니다.");
                if (u.typeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) errors.Add($"'{u.typeName}': 파일 이름에 쓸 수 없는 문자가 있습니다.");
                if (string.IsNullOrEmpty(u.unitClass)) errors.Add($"'{u.typeName}': unitClass가 비어 있습니다.");
                if (u.skillOverrides != null)
                    foreach (string key in u.skillOverrides.Keys)
                        if (!u.skills.Contains(key)) errors.Add($"'{u.typeName}': 스킬 목록에 없는 '{key}'의 개별 값이 있습니다.");
            }

            Dictionary<string, SkillDto> globals = SkillTools.IndexByName(_skills.skills, errors);
            foreach (UnitDto u in _units.units) SkillTools.Effective(u, globals, errors);
            return errors.Distinct().ToList();
        }

        // ── 에셋 참조 점검 ──────────────────────────────────────────────
        private static void CollectRefProblems(object obj, string path, List<string> output)
        {
            if (obj == null) return;
            Type t = obj.GetType();
            if (obj is IDictionary || obj is JToken) return;        // 개별 값(JObject)은 CollectOverrideRefProblems가 본다
            if (obj is IEnumerable enumerable && !(obj is string))
            {
                int i = 0;
                foreach (object item in enumerable) CollectRefProblems(item, $"{path}[{i++}]", output);
                return;
            }
            if (!t.IsClass || t == typeof(string)) return;

            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v = f.GetValue(obj);
                var attr = f.GetCustomAttribute<AssetRefAttribute>();
                if (attr != null)
                {
                    string s = v as string;
                    if (string.IsNullOrEmpty(s)) continue;
                    UnitAssetRefs.Resolve(UnitAssetRefs.TypeOf(attr.Kind), s, out string problem);
                    if (problem != null) output.Add($"{path}.{f.Name}: {problem}");
                }
                else if (v != null && !f.FieldType.IsPrimitive && !f.FieldType.IsEnum && f.FieldType != typeof(string))
                {
                    CollectRefProblems(v, path + "." + f.Name, output);
                }
            }
        }

        private static void CollectOverrideRefProblems(UnitDto u, List<string> output)
        {
            if (u.skillOverrides == null) return;
            foreach (KeyValuePair<string, JObject> pair in u.skillOverrides)
            {
                foreach (JProperty p in pair.Value.Properties())
                {
                    FieldInfo f = typeof(SkillDto).GetField(p.Name);
                    var attr = f != null ? f.GetCustomAttribute<AssetRefAttribute>() : null;
                    if (attr == null) continue;
                    string s = (string)p.Value;
                    if (string.IsNullOrEmpty(s)) continue;
                    UnitAssetRefs.Resolve(UnitAssetRefs.TypeOf(attr.Kind), s, out string problem);
                    if (problem != null) output.Add($"{u.typeName}/{pair.Key}.{p.Name}: {problem}");
                }
            }
        }

        // ── 프리팹 읽기 / 비교 ──────────────────────────────────────────
        // 프리팹 폴더 조회는 비싸서 RefreshSnapshots에서만 갱신한다(OnGUI에서 매 이벤트 부르면 느려진다).
        private List<string> _prefabTypeNames = new List<string>();

        private List<string> AllUnitNames()
        {
            List<string> names = _units.units.Select(u => u.typeName).ToList();
            foreach (string n in _prefabTypeNames)
                if (!names.Contains(n)) names.Add(n);
            return names;
        }

        private void RefreshSnapshots()
        {
            if (_units == null) return;
            UnitAssetRefs.ClearCache();
            _prefabTypeNames = PrefabUnitReader.ListPrefabTypeNames();
            _snapshots.Clear();
            foreach (string name in AllUnitNames())
            {
                PrefabSnapshot s = PrefabUnitReader.ReadAsset(name);
                if (s != null && s.Unit != null) _snapshots[name] = s;
            }
            RecomputeDiffs();
        }

        private void RecomputeDiffs()
        {
            _diffs.Clear();
            if (_units == null) return;
            Dictionary<string, SkillDto> globals = SkillTools.IndexByName(_skills.skills);
            var ignored = new List<string>();
            foreach (UnitDto u in _units.units)
            {
                if (!_snapshots.TryGetValue(u.typeName, out PrefabSnapshot snap)) continue;
                List<SkillDto> eff = SkillTools.Effective(u, globals, ignored);
                _diffs[u.typeName] = UnitDiff.Compare(u, eff, snap.Unit, snap.Skills);
            }
        }

        private enum UnitState { InSync, Different, NoPrefab, NoJson }

        private UnitState StateOf(string name, out int diffCount)
        {
            diffCount = 0;
            bool inJson = _units.units.Any(u => u.typeName == name);
            bool hasPrefab = _snapshots.ContainsKey(name);
            if (!inJson) return UnitState.NoJson;
            if (!hasPrefab) return UnitState.NoPrefab;
            diffCount = _diffs.TryGetValue(name, out List<DiffItem> d) ? d.Count : 0;
            return diffCount == 0 ? UnitState.InSync : UnitState.Different;
        }

        private UnitDto FindUnit(string name) => _units.units.FirstOrDefault(u => u.typeName == name);

        private void Message(string text, bool log = true)
        {
            _lastMessage = text;
            if (log) Debug.Log("[유닛 데이터] " + text);
            Repaint();
        }

        // 구조를 바꾸는 동작(목록 추가/삭제, 대화상자)은 OnGUI 밖에서 실행한다 — 그리는 도중에 컨트롤 수가 바뀌면 오류가 난다.
        private void Defer(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                action();
                OnDataChanged();
            };
        }

        // ── 프리팹 → JSON (작업 사본만 바꾼다) ──────────────────────────
        private void ImportFromPrefabs(List<string> names)
        {
            if (names.Count == 0) return;
            RefreshSnapshots();

            var problems = new List<string>();
            Dictionary<string, SkillDto> globals = SkillTools.IndexByName(_skills.skills);
            var perUnit = new List<(string, List<SkillDto>)>();
            var imported = new List<string>();
            var added = new List<string>();
            var skipped = new List<string>();

            foreach (UnitDto u in _units.units.ToList())
            {
                if (names.Contains(u.typeName) && _snapshots.TryGetValue(u.typeName, out PrefabSnapshot snap))
                {
                    CopyFromSnapshot(u, snap.Unit);
                    perUnit.Add((u.typeName, snap.Skills));
                    imported.Add(u.typeName);
                    continue;
                }
                if (names.Contains(u.typeName)) skipped.Add(u.typeName + "(프리팹 없음)");
                perUnit.Add((u.typeName, SkillTools.Effective(u, globals, problems)));
            }

            foreach (string name in names)
            {
                if (_units.units.Any(u => u.typeName == name)) continue;
                if (!_snapshots.TryGetValue(name, out PrefabSnapshot snap)) { skipped.Add(name); continue; }
                UnitDto created = UnitDataJson.Clone(snap.Unit);
                created.unitClass = "Monster";          // 프리팹에 없는 값 — 기본값. 확인해서 고쳐야 한다.
                _units.units.Add(created);
                perUnit.Add((name, snap.Skills));
                added.Add(name);
            }

            SkillTools.ConsolidateResult result = SkillTools.Consolidate(perUnit, _skills.skills);
            _skills.skills = result.Globals;
            foreach (UnitDto u in _units.units)
                u.skillOverrides = result.OverridesByUnit.TryGetValue(u.typeName, out Dictionary<string, JObject> ov) ? ov : new Dictionary<string, JObject>();

            int overrideCount = _units.units.Sum(u => u.skillOverrides.Count);
            OnDataChanged();
            string note = $"프리팹 → JSON(작업 사본): 유닛 {imported.Count + added.Count}개 반영, 개별 스킬 값 {overrideCount}건";
            if (added.Count > 0) note += $"\nJSON에 새로 추가: {string.Join(", ", added)} — unitClass를 'Monster'로 두었으니 확인하세요.";
            if (skipped.Count > 0) note += $"\n건너뜀: {string.Join(", ", skipped)}";
            if (problems.Count > 0) note += $"\n주의 {problems.Count}건: {string.Join(" / ", problems.Take(3))}";
            Message(note + "\n※ 아직 저장 전입니다 — 내용을 확인하고 [JSON 저장]을 누르세요.");
        }

        private static void CopyFromSnapshot(UnitDto target, UnitDto source)
        {
            UnitDto c = UnitDataJson.Clone(source);          // typeName·unitClass는 JSON 값을 유지한다
            target.footprint = c.footprint;
            target.engageDistance = c.engageDistance;
            target.populationCost = c.populationCost;
            target.visualScaleIgnoresFootprint = c.visualScaleIgnoresFootprint;
            target.skills = c.skills;
            target.stats = c.stats;
            target.weight = c.weight;
            target.visual = c.visual;
        }

        // ── JSON → 프리팹 ───────────────────────────────────────────────
        private void ApplyToPrefabs(List<string> names)
        {
            if (names.Count == 0) return;

            if (hasUnsavedChanges)
            {
                if (!EditorUtility.DisplayDialog("먼저 저장이 필요합니다", "프리팹에 적용하려면 JSON을 먼저 저장해야 합니다(프리팹과 JSON이 어긋나지 않게).", "저장하고 계속", "취소"))
                    return;
                if (!TrySave()) return;
            }

            var problems = new List<string>();
            Dictionary<string, SkillDto> globals = SkillTools.IndexByName(_skills.skills, problems);
            var plan = new List<(UnitDto unit, List<SkillDto> skills, bool exists, int diffs)>();
            foreach (string name in names)
            {
                UnitDto u = FindUnit(name);
                if (u == null) continue;
                bool exists = _snapshots.ContainsKey(name);
                int diffs = exists && _diffs.TryGetValue(name, out List<DiffItem> d) ? d.Count : 0;
                plan.Add((u, SkillTools.Effective(u, globals, problems), exists, diffs));
            }
            if (problems.Count > 0)
            {
                EditorUtility.DisplayDialog("적용할 수 없습니다", string.Join("\n", problems.Distinct().Take(10)), "확인");
                return;
            }

            var todo = plan.Where(p => !p.exists || p.diffs > 0).ToList();
            int upToDate = plan.Count - todo.Count;
            if (todo.Count == 0)
            {
                Message($"선택한 유닛 {plan.Count}개는 이미 프리팹과 같아 건드리지 않았습니다.");
                return;
            }

            string summary = string.Join("\n", todo.Select(p => p.exists ? $"· {p.unit.typeName}: 필드 {p.diffs}개 변경" : $"· {p.unit.typeName}: 새 프리팹 생성"));
            if (!EditorUtility.DisplayDialog("프리팹에 적용",
                    summary + (upToDate > 0 ? $"\n(이미 같은 {upToDate}개는 건너뜀)" : "") +
                    "\n\n기존 프리팹을 제자리 갱신합니다. JSON 스키마에 없는 항목(애니메이션 클립, 인형 장식 등)은 그대로 둡니다.", "적용", "취소"))
                return;

            var results = new List<PrefabUnitWriter.ApplyResult>();
            for (int i = 0; i < todo.Count; i++)
            {
                EditorUtility.DisplayProgressBar("프리팹 적용", todo[i].unit.typeName, (float)i / todo.Count);
                results.Add(PrefabUnitWriter.Apply(todo[i].unit, todo[i].skills));
            }
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshSnapshots();

            int failed = results.Count(r => r.Error != null);
            int created = results.Count(r => r.Created && r.Error == null);
            int warnings = results.Sum(r => r.Warnings.Count);
            foreach (PrefabUnitWriter.ApplyResult r in results)
            {
                if (r.Error != null) Debug.LogError($"[유닛 데이터] '{r.TypeName}' 적용 실패: {r.Error}");
                foreach (string w in r.Warnings) Debug.LogWarning($"[유닛 데이터] '{r.TypeName}': {w}");
            }
            // 안전 규칙(블록이 JSON에 없어도 기존 손·라이브러리 컴포넌트는 지우지 않음) 때문에 적용 뒤에도 남는 차이를 알려 준다.
            var remaining = new List<string>();
            foreach (PrefabUnitWriter.ApplyResult r in results.Where(x => x.Error == null))
            {
                if (!_diffs.TryGetValue(r.TypeName, out List<DiffItem> left) || left.Count == 0) continue;
                remaining.Add($"{r.TypeName}: {left.Count}건 ({string.Join(", ", left.Take(3).Select(d => d.Path))})");
                foreach (DiffItem d in left.Take(20)) Debug.LogWarning($"[유닛 데이터] 적용 후에도 남은 차이 — {r.TypeName}.{d.Path}: JSON {d.Left} / 프리팹 {d.Right}");
            }

            Message($"프리팹 적용: {results.Count - failed}개 완료(새로 생성 {created}), 건너뜀 {upToDate}, 실패 {failed}, 경고 {warnings}건" +
                    (warnings + failed > 0 ? " — 자세한 내용은 Console을 보세요." : "") +
                    (remaining.Count > 0 ? "\n적용 뒤에도 JSON과 다른 항목(JSON에 블록이 없는 컴포넌트는 지우지 않음):\n" + string.Join("\n", remaining.Take(6)) : ""));
        }

        // ── 검증 ────────────────────────────────────────────────────────
        // 파일·프리팹을 바꾸지 않고, JSON을 프리팹에 적용한 결과를 다시 읽어 JSON과 같은지 확인한다.
        private void RunVerification()
        {
            var report = new List<string>();
            int failures = 0;

            foreach (string p in _parity) report.Add("⚠ " + p);

            string ut = UnitsText, st = SkillsText;
            bool unitsRound = UnitDataJson.WriteUnits(UnitDataJson.ReadUnits(ut)) == ut;
            bool skillsRound = UnitDataJson.WriteSkills(UnitDataJson.ReadSkills(st)) == st;
            if (!unitsRound || !skillsRound) failures++;
            report.Add((unitsRound && skillsRound ? "✓" : "✗") + " JSON 쓰기→읽기→쓰기 결과 동일");

            var problems = new List<string>();
            Dictionary<string, SkillDto> globals = SkillTools.IndexByName(_skills.skills, problems);
            var perUnit = new List<(string, List<SkillDto>)>();
            foreach (UnitDto u in _units.units) perUnit.Add((u.typeName, SkillTools.Effective(u, globals, problems)));
            SkillTools.ConsolidateResult consolidated = SkillTools.Consolidate(perUnit, _skills.skills);
            Dictionary<string, SkillDto> newGlobals = SkillTools.IndexByName(consolidated.Globals);
            bool lossless = true;
            foreach ((string unit, List<SkillDto> want) in perUnit)
            {
                var probe = new UnitDto { typeName = unit, skills = want.Select(s => s.skillName).ToList() };
                if (consolidated.OverridesByUnit.TryGetValue(unit, out Dictionary<string, JObject> ov)) probe.skillOverrides = ov;
                List<SkillDto> got = SkillTools.Effective(probe, newGlobals, problems);
                for (int i = 0; i < want.Count && lossless; i++)
                    foreach (FieldInfo f in SkillTools.ValueFields)
                        if (!SkillTools.ValuesEqual(f.GetValue(want[i]), f.GetValue(got[i]))) { lossless = false; break; }
            }
            if (!lossless || problems.Count > 0) failures++;
            report.Add((lossless && problems.Count == 0 ? "✓" : "✗") + " 스킬 전역+개별 값 통합이 무손실" + (problems.Count > 0 ? " (" + problems[0] + ")" : ""));

            for (int i = 0; i < _units.units.Count; i++)
            {
                UnitDto u = _units.units[i];
                EditorUtility.DisplayProgressBar("검증", u.typeName, (float)i / _units.units.Count);
                var warnings = new List<string>();
                try
                {
                    List<SkillDto> eff = SkillTools.Effective(u, globals, warnings);
                    PrefabSnapshot after = PrefabUnitWriter.DryRun(u, eff, warnings);
                    List<DiffItem> diffs = UnitDiff.Compare(u, eff, after.Unit, after.Skills);
                    if (diffs.Count == 0 && warnings.Count == 0) { report.Add($"✓ {u.typeName}: JSON→프리팹→읽기 일치"); continue; }
                    failures++;
                    report.Add($"✗ {u.typeName}: 불일치 {diffs.Count}건, 경고 {warnings.Count}건");
                    foreach (DiffItem d in diffs.Take(4)) report.Add($"     {d.Path}: JSON {d.Left} / 적용 후 {d.Right}");
                    foreach (string w in warnings.Take(4)) report.Add("     경고: " + w);
                }
                catch (Exception e)
                {
                    failures++;
                    report.Add($"✗ {u.typeName}: 검증 중 예외 — {e.Message}");
                    Debug.LogException(e);
                }
            }
            EditorUtility.ClearProgressBar();

            string text = string.Join("\n", report);
            Debug.Log("[유닛 데이터] 검증 결과\n" + text);
            Message((failures == 0 ? "검증 통과" : $"검증 실패 {failures}건") + $" — 상세는 Console에 있습니다.\n" + string.Join("\n", report.Where(r => !r.StartsWith("✓")).Take(10)), log: false);
        }

        // ── 유닛 추가 / 제거 ────────────────────────────────────────────
        private string AddUnit(string name, string unitClass, string cloneFrom)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return "이름을 입력하세요.";
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "파일 이름에 쓸 수 없는 문자가 있습니다.";
            if (AllUnitNames().Contains(name)) return $"'{name}' 유닛(또는 같은 이름의 프리팹)이 이미 있습니다.";

            UnitDto created;
            UnitDto source = string.IsNullOrEmpty(cloneFrom) ? null : FindUnit(cloneFrom);
            if (source != null)
            {
                created = UnitDataJson.Clone(source);
                created.typeName = name;
                if (!string.IsNullOrEmpty(unitClass)) created.unitClass = unitClass;
            }
            else
            {
                created = new UnitDto { typeName = name, unitClass = string.IsNullOrEmpty(unitClass) ? "Monster" : unitClass };
            }
            _units.units.Add(created);
            _selectedUnit = name;
            if (!_checked.Contains(name)) _checked.Add(name);
            _tab = 0;
            return null;
        }

        // 이 유닛을 이름으로 참조하는 곳(웨이브 데이터)을 찾는다.
        private static List<string> FindUnitReferences(string name)
        {
            var found = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:WaveData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                WaveData wave = AssetDatabase.LoadAssetAtPath<WaveData>(path);
                if (wave == null || wave.parties == null) continue;
                foreach (WavePartyConfig party in wave.parties)
                    if (party != null && party.units != null && party.units.Any(g => g != null && g.unitTypeName == name))
                        found.Add($"{wave.name} › {party.partyName}");
            }
            return found;
        }

        private void RemoveUnit(string name)
        {
            UnitDto u = FindUnit(name);
            if (u == null) return;

            List<string> refs = FindUnitReferences(name);
            bool hasPrefab = _snapshots.ContainsKey(name);
            string message = $"'{name}' 유닛을 JSON에서 제거합니다.";
            if (refs.Count > 0) message += "\n\n⚠ 이 유닛을 이름으로 쓰는 곳이 있습니다(제거하면 해당 웨이브에서 소환이 실패합니다):\n· " + string.Join("\n· ", refs.Distinct());
            message += "\n\n(제거는 [JSON 저장]을 누를 때 반영됩니다.)";

            int choice = hasPrefab
                ? EditorUtility.DisplayDialogComplex("유닛 제거", message, "JSON에서만 제거", "프리팹도 함께 제거(휴지통)", "취소")
                : (EditorUtility.DisplayDialog("유닛 제거", message, "제거", "취소") ? 0 : 2);
            if (choice == 2) return;

            _units.units.Remove(u);
            _checked.Remove(name);
            if (choice == 1 && !_pendingPrefabDeletes.Contains(name)) _pendingPrefabDeletes.Add(name);
            if (_selectedUnit == name) _selectedUnit = _units.units.Count > 0 ? _units.units[0].typeName : null;
            RefreshSnapshots();
        }

        private void RenameSkill(string oldName, string newName)
        {
            newName = (newName ?? "").Trim();
            if (newName.Length == 0 || newName == oldName) return;
            if (_skills.skills.Any(s => s.skillName == newName)) { EditorUtility.DisplayDialog("이름 변경", $"'{newName}' 스킬이 이미 있습니다.", "확인"); return; }
            SkillDto skill = _skills.skills.FirstOrDefault(s => s.skillName == oldName);
            if (skill == null) return;

            skill.skillName = newName;
            foreach (UnitDto u in _units.units)
            {
                for (int i = 0; i < u.skills.Count; i++) if (u.skills[i] == oldName) u.skills[i] = newName;
                if (u.skillOverrides != null && u.skillOverrides.TryGetValue(oldName, out JObject ov))
                {
                    u.skillOverrides.Remove(oldName);
                    u.skillOverrides[newName] = ov;
                }
            }
            _selectedSkill = newName;
            Message($"스킬 이름을 '{oldName}' → '{newName}'로 바꿨습니다. 애니메이션 슬롯 이름도 바뀌므로 프리팹 적용 후 이미 꽂은 클립은 새 슬롯으로 옮겨지지 않습니다.");
        }

        private List<string> UsersOfSkill(string skillName)
            => _units.units.Where(u => u.skills.Contains(skillName)).Select(u => u.typeName).ToList();
    }
}
