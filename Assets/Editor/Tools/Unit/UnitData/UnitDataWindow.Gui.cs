using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace UnitDataTool
{
    // 화면 그리기. DTO 필드는 리플렉션으로 그린다(스탯·스킬 필드마다 UI 코드를 따로 쓰지 않는다).
    //  · [AssetRef] string 필드 → ObjectField (고르면 에셋 이름이 JSON 값이 된다)
    //  · [OptionalBlock] 하위 블록 → [추가]/[제거] 버튼이 붙은 접이식 구역
    // 목록 추가/삭제·대화상자 같은 "구조를 바꾸는 동작"은 Defer로 OnGUI 밖에서 실행한다.
    public partial class UnitDataWindow
    {
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private Vector2 _compareScroll;
        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();
        private bool _showAddPanel;
        private string _newName = "";
        private string _newClass = "Monster";
        private int _cloneIndex;
        private string _addUnitError;
        private string _renameBuffer;
        private string _renameFor;
        private bool _dirtyRequested;

        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();
        private static readonly string[] PoseDirs = { "UP", "UP_LEFT", "LEFT", "DOWN_LEFT", "DOWN" };
        private static readonly string[] DefaultClasses = { "Human", "Monster", "Wild" };

        private static FieldInfo[] FieldsOf(Type type)
        {
            if (!FieldCache.TryGetValue(type, out FieldInfo[] fields))
            {
                fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.MetadataToken).ToArray();
                FieldCache[type] = fields;
            }
            return fields;
        }

        private void OnGUI()
        {
            if (_units == null)
            {
                EditorGUILayout.HelpBox("데이터를 불러오는 중입니다…", MessageType.Info);
                return;
            }

            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.control || e.command) && e.keyCode == KeyCode.S)
            {
                e.Use();
                Defer(() => TrySave());
            }

            DrawBanners();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawUnitList();
                using (new EditorGUILayout.VerticalScope())
                {
                    _tab = GUILayout.Toolbar(_tab, new[] { "유닛", "스킬 정의", "프리팹 비교" }, GUILayout.Height(24));
                    _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                    EditorGUI.BeginChangeCheck();
                    switch (_tab)
                    {
                        case 0: DrawUnitTab(); break;
                        case 1: DrawSkillTab(); break;
                        default: DrawCompareTab(); break;
                    }
                    if (EditorGUI.EndChangeCheck()) _dirtyRequested = true;
                    EditorGUILayout.EndScrollView();
                }
            }
            DrawBottomBar();

            if (_dirtyRequested && Event.current.type == EventType.Repaint)
            {
                _dirtyRequested = false;
                OnDataChanged();
            }
        }

        // ── 상단 안내 ───────────────────────────────────────────────────
        private void DrawBanners()
        {
            if (_externalChange)
            {
                EditorGUILayout.HelpBox("JSON 파일이 이 창 밖에서 바뀌었습니다. 작업 중인 변경이 있어 자동으로 불러오지 않았습니다.", MessageType.Warning);
                if (GUILayout.Button("디스크 내용으로 다시 불러오기 (내 변경 버림)"))
                    Defer(() =>
                    {
                        if (!EditorUtility.DisplayDialog("다시 불러오기", "이 창에서 한 변경을 버리고 디스크의 JSON을 불러옵니다.", "불러오기", "취소")) return;
                        LoadFromDisk();
                        RefreshSnapshots();
                    });
            }
            if (_loadProblems.Count > 0)
                EditorGUILayout.HelpBox($"JSON을 읽는 중 문제 {_loadProblems.Count}건 — 저장하면 해당 항목이 사라질 수 있습니다:\n" +
                                        string.Join("\n", _loadProblems.Take(4)), MessageType.Warning);
            if (_parity.Count > 0)
                EditorGUILayout.HelpBox("스키마 점검:\n" + string.Join("\n", _parity.Take(4)), MessageType.Warning);
            if (_formatWillChange)
                EditorGUILayout.HelpBox("저장하면 JSON 서식이 한 번 정리됩니다(폐기된 animatorController 키 제거, 스킬 키 순서 정리, 기본값 생략). 값은 바뀌지 않습니다.", MessageType.Info);

            List<string> different = AllUnitNames().Where(n =>
            {
                UnitState s = StateOf(n, out _);
                return s == UnitState.Different || s == UnitState.NoJson;
            }).ToList();
            if (different.Count > 0)
            {
                EditorGUILayout.HelpBox($"프리팹과 값이 다르거나 JSON에 없는 유닛이 {different.Count}개 있습니다. 프리팹이 최신이면 아래 버튼으로 프리팹 값을 JSON 작업 사본으로 가져오세요" +
                                        "(저장 전에 검토할 수 있습니다).", MessageType.Info);
                if (GUILayout.Button($"다름 {different.Count}개 전부 프리팹 값으로 가져오기"))
                {
                    List<string> names = different.ToList();
                    Defer(() => ImportFromPrefabs(names));
                }
            }
        }

        // ── 왼쪽: 유닛 목록 ─────────────────────────────────────────────
        private void DrawUnitList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(280)))
            {
                List<string> names = AllUnitNames();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("유닛 (체크 = 프리팹 작업 대상)", EditorStyles.boldLabel);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("전체", EditorStyles.miniButtonLeft)) { _checked.Clear(); _checked.AddRange(names); }
                    if (GUILayout.Button("다름만", EditorStyles.miniButtonMid))
                    {
                        _checked.Clear();
                        _checked.AddRange(names.Where(n => { UnitState s = StateOf(n, out _); return s != UnitState.InSync; }));
                    }
                    if (GUILayout.Button("해제", EditorStyles.miniButtonRight)) _checked.Clear();
                }

                _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
                foreach (string name in names) DrawUnitRow(name);
                EditorGUILayout.EndScrollView();

                if (_pendingPrefabDeletes.Count > 0)
                {
                    EditorGUILayout.HelpBox("저장하면 휴지통으로 가는 프리팹: " + string.Join(", ", _pendingPrefabDeletes), MessageType.Warning);
                    if (GUILayout.Button("삭제 예약 취소")) Defer(() => _pendingPrefabDeletes.Clear());
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("＋ 유닛 추가")) { _showAddPanel = !_showAddPanel; _addUnitError = null; }
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_selectedUnit) || FindUnit(_selectedUnit) == null))
                        if (GUILayout.Button("－ 선택 유닛 제거")) { string target = _selectedUnit; Defer(() => RemoveUnit(target)); }
                }
                if (_showAddPanel) DrawAddUnitPanel();
            }
        }

        private void DrawUnitRow(string name)
        {
            UnitState state = StateOf(name, out int diffCount);
            Color color;
            string tag;
            switch (state)
            {
                case UnitState.InSync: color = new Color(0.45f, 0.85f, 0.45f); tag = "동기화"; break;
                case UnitState.Different: color = new Color(1f, 0.7f, 0.25f); tag = $"다름 {diffCount}"; break;
                case UnitState.NoPrefab: color = new Color(0.45f, 0.8f, 1f); tag = "프리팹 없음"; break;
                default: color = new Color(0.7f, 0.7f, 0.7f); tag = "JSON 없음"; break;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool isChecked = _checked.Contains(name);
                bool now = EditorGUILayout.Toggle(isChecked, GUILayout.Width(16));
                if (now != isChecked) { if (now) _checked.Add(name); else _checked.Remove(name); }

                GUIStyle style = name == _selectedUnit ? EditorStyles.whiteLabel : EditorStyles.label;
                Rect rect = GUILayoutUtility.GetRect(new GUIContent(name), style, GUILayout.ExpandWidth(true));
                if (name == _selectedUnit) EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.9f, 0.55f));
                if (GUI.Button(rect, name, style)) _selectedUnit = name;

                Color previous = GUI.contentColor;
                GUI.contentColor = color;
                GUILayout.Label(tag, GUILayout.Width(74));
                GUI.contentColor = previous;
            }
        }

        private void DrawAddUnitPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _newName = EditorGUILayout.TextField("이름", _newName);

                string[] classes = _units.units.Select(u => u.unitClass).Concat(DefaultClasses).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToArray();
                int classIndex = Mathf.Max(0, Array.IndexOf(classes, _newClass));
                classIndex = EditorGUILayout.Popup("종류(unitClass)", classIndex, classes);
                _newClass = classes[classIndex];

                string[] cloneOptions = new[] { "(복제 안 함 — 빈 유닛)" }.Concat(_units.units.Select(u => u.typeName)).ToArray();
                _cloneIndex = Mathf.Clamp(_cloneIndex, 0, cloneOptions.Length - 1);
                _cloneIndex = EditorGUILayout.Popup("복제 원본", _cloneIndex, cloneOptions);

                if (!string.IsNullOrEmpty(_addUnitError)) EditorGUILayout.HelpBox(_addUnitError, MessageType.Error);
                if (GUILayout.Button("추가"))
                {
                    string name = _newName, cls = _newClass;
                    string clone = _cloneIndex > 0 ? cloneOptions[_cloneIndex] : null;
                    Defer(() =>
                    {
                        _addUnitError = AddUnit(name, cls, clone);
                        if (_addUnitError == null) { _showAddPanel = false; _newName = ""; _cloneIndex = 0; }
                    });
                }
                EditorGUILayout.LabelField("추가한 유닛의 프리팹은 체크 후 [JSON → 프리팹 적용]에서 만들어집니다.", EditorStyles.miniLabel);
            }
        }

        // ── 유닛 탭 ─────────────────────────────────────────────────────
        private void DrawUnitTab()
        {
            UnitDto u = string.IsNullOrEmpty(_selectedUnit) ? null : FindUnit(_selectedUnit);
            if (u == null)
            {
                if (!string.IsNullOrEmpty(_selectedUnit) && _snapshots.ContainsKey(_selectedUnit))
                    EditorGUILayout.HelpBox("JSON에 없고 프리팹만 있는 유닛입니다. 왼쪽에서 체크하고 [프리팹 → JSON 가져오기]로 JSON에 추가하세요.", MessageType.Info);
                else
                    EditorGUILayout.HelpBox("왼쪽에서 유닛을 선택하세요.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(u.typeName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("이름(typeName)", u.typeName + "   — 프리팹 파일명·Addressables 주소·웨이브 데이터의 키라서 이름 변경은 지원하지 않습니다");

            string[] classes = _units.units.Select(x => x.unitClass).Concat(DefaultClasses).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToArray();
            int classIndex = Mathf.Max(0, Array.IndexOf(classes, u.unitClass));
            int picked = EditorGUILayout.Popup("종류(unitClass)", classIndex, classes);
            if (classes[picked] != u.unitClass) u.unitClass = classes[picked];

            DrawObject(u, "unit/" + u.typeName, new HashSet<string> { nameof(UnitDto.typeName), nameof(UnitDto.unitClass), nameof(UnitDto.skillOverrides) });
        }

        // 필드를 선언 순서대로 그린다. UnitDto.skills는 전용 UI(스킬 목록 + 개별 값)로 그린다.
        private void DrawObject(object obj, string path, ISet<string> skip = null)
        {
            foreach (FieldInfo f in FieldsOf(obj.GetType()))
            {
                if (skip != null && skip.Contains(f.Name)) continue;
                if (obj is VisualDto && f.Name == nameof(VisualDto.animatorController)) continue;     // 폐기된 필드 — 읽기 전용 호환용
                DrawField(obj, f, path + "/" + f.Name);
            }
        }

        private void DrawField(object owner, FieldInfo f, string path)
        {
            Type t = f.FieldType;
            object v = f.GetValue(owner);
            string label = ObjectNames.NicifyVariableName(f.Name);
            var refAttr = f.GetCustomAttribute<AssetRefAttribute>();

            if (owner is UnitDto unit && f.Name == nameof(UnitDto.skills)) { DrawSkillSection(unit); return; }
            if (t == typeof(float[])) { f.SetValue(owner, DrawVec2(label, (float[])v)); return; }
            if (t == typeof(List<PoseDto>)) { DrawPoses(owner, f, label, path); return; }
            if (t == typeof(float) || t == typeof(int) || t == typeof(bool) || t == typeof(string))
            {
                f.SetValue(owner, DrawLeaf(label, t, v, refAttr));
                return;
            }
            if (t.IsClass && !t.IsGenericType && !t.IsArray) DrawBlock(owner, f, v, label, path);
        }

        private object DrawLeaf(string label, Type type, object value, AssetRefAttribute refAttr)
        {
            if (type == typeof(float)) return EditorGUILayout.FloatField(label, value is float f ? f : 0f);
            if (type == typeof(int)) return EditorGUILayout.IntField(label, value is int i ? i : 0);
            if (type == typeof(bool)) return EditorGUILayout.Toggle(label, value is bool b && b);
            if (type == typeof(string))
            {
                string s = value as string;
                if (refAttr != null) return DrawRef(label, s, refAttr.Kind);
                return EditorGUILayout.TextField(label, s ?? "");
            }
            EditorGUILayout.LabelField(label, "(지원하지 않는 형식)");
            return value;
        }

        private string DrawRef(string label, string current, RefKind kind)
        {
            Type type = UnitAssetRefs.TypeOf(kind);
            string problem = null;
            UObject resolved = string.IsNullOrEmpty(current) ? null : UnitAssetRefs.Resolve(type, current, out problem);

            var content = new GUIContent(problem != null ? label + "  ⚠" : label, problem);
            UObject picked = EditorGUILayout.ObjectField(content, resolved, type, false);
            if (picked != resolved) return UnitAssetRefs.NameOf(type, picked);

            if (resolved == null && !string.IsNullOrEmpty(current))
                EditorGUILayout.HelpBox($"해결되지 않은 참조 '{current}' — {problem}\n에셋을 다시 지정하면 고쳐집니다.", MessageType.Warning);
            return current;
        }

        private static float[] DrawVec2(string label, float[] value)
        {
            float x = value != null && value.Length > 0 ? value[0] : 0f;
            float y = value != null && value.Length > 1 ? value[1] : 0f;
            Vector2 edited = EditorGUILayout.Vector2Field(label, new Vector2(x, y));
            if (value != null && value.Length == 2 && edited.x == x && edited.y == y) return value;
            return new[] { edited.x, edited.y };
        }

        private bool Foldout(string path, string label, Rect? rect = null)
        {
            _foldouts.TryGetValue(path, out bool open);
            open = rect.HasValue ? EditorGUI.Foldout(rect.Value, open, label, true) : EditorGUILayout.Foldout(open, label, true);
            _foldouts[path] = open;
            return open;
        }

        private void DrawBlock(object owner, FieldInfo f, object block, string label, string path)
        {
            bool optional = f.GetCustomAttribute<OptionalBlockAttribute>() != null;
            if (block == null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label, "(없음)");
                    if (GUILayout.Button("추가", GUILayout.Width(56)))
                    {
                        Type blockType = f.FieldType;
                        Defer(() => { f.SetValue(owner, Activator.CreateInstance(blockType)); _foldouts[path] = true; });
                    }
                }
                return;
            }

            Rect row = EditorGUILayout.GetControlRect();
            bool open = Foldout(path, label, new Rect(row.x, row.y, row.width - (optional ? 60 : 0), row.height));
            if (optional && GUI.Button(new Rect(row.xMax - 56, row.y, 56, row.height), "제거"))
            {
                Defer(() => f.SetValue(owner, null));
                return;
            }
            if (!open) return;

            EditorGUI.indentLevel++;
            DrawObject(block, path);
            EditorGUI.indentLevel--;
        }

        private void DrawPoses(object owner, FieldInfo f, string label, string path)
        {
            var poses = (List<PoseDto>)f.GetValue(owner);
            if (poses == null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label, "기본 포즈 사용 (WeaponAttachment 기본값)");
                    if (GUILayout.Button("직접 설정", GUILayout.Width(70))) Defer(() => f.SetValue(owner, WeaponDefaults.Poses));
                }
                return;
            }

            Rect row = EditorGUILayout.GetControlRect();
            bool open = Foldout(path, $"{label} ({poses.Count}방향)", new Rect(row.x, row.y, row.width - 86, row.height));
            if (GUI.Button(new Rect(row.xMax - 82, row.y, 82, row.height), "기본값으로")) { Defer(() => f.SetValue(owner, null)); return; }
            if (!open) return;

            EditorGUI.indentLevel++;
            foreach (PoseDto p in poses)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int di = Mathf.Max(0, Array.IndexOf(PoseDirs, p.direction));
                    di = EditorGUILayout.Popup(di, PoseDirs, GUILayout.Width(96));
                    p.direction = PoseDirs[di];
                    p.offset = DrawInlineVec2(p.offset);
                    GUILayout.Label("각도", GUILayout.Width(30));
                    p.targetAngle = EditorGUILayout.FloatField(p.targetAngle, GUILayout.Width(48));
                    GUILayout.Label("순서", GUILayout.Width(30));
                    p.sortingOrder = EditorGUILayout.IntField(p.sortingOrder, GUILayout.Width(36));
                }
            }
            EditorGUI.indentLevel--;
        }

        private static float[] DrawInlineVec2(float[] value)
        {
            float x = value != null && value.Length > 0 ? value[0] : 0f;
            float y = value != null && value.Length > 1 ? value[1] : 0f;
            Vector2 edited = EditorGUILayout.Vector2Field(GUIContent.none, new Vector2(x, y), GUILayout.Width(150));
            if (value != null && value.Length == 2 && edited.x == x && edited.y == y) return value;
            return new[] { edited.x, edited.y };
        }

        // ── 유닛의 스킬 목록 + 이 유닛만 다른 값 ─────────────────────────
        private void DrawSkillSection(UnitDto u)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("스킬", EditorStyles.boldLabel);
            Dictionary<string, SkillDto> globals = SkillTools.IndexByName(_skills.skills);
            if (u.skillOverrides == null) u.skillOverrides = new Dictionary<string, JObject>();

            for (int i = 0; i < u.skills.Count; i++)
            {
                string skill = u.skills[i];
                int index = i;
                bool known = globals.TryGetValue(skill, out SkillDto global);
                u.skillOverrides.TryGetValue(skill, out JObject ov);
                string key = $"skill/{u.typeName}/{skill}";

                bool open;
                using (new EditorGUILayout.HorizontalScope())
                {
                    string title = skill + (ov != null && ov.Count > 0 ? $"    [이 유닛만 다른 값 {ov.Count}]" : "") + (known ? "" : "    ⚠ 전역 정의 없음");
                    open = Foldout(key, title);
                    if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24)) && index > 0) Defer(() => Swap(u.skills, index, index - 1));
                    if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24)) && index < u.skills.Count - 1) Defer(() => Swap(u.skills, index, index + 1));
                    if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(24)))
                        Defer(() => { u.skills.RemoveAt(index); u.skillOverrides.Remove(skill); });
                }
                if (open && known) DrawOverrideEditor(u, skill, global);
            }

            string[] candidates = globals.Keys.Where(k => !u.skills.Contains(k)).ToArray();
            int pick = EditorGUILayout.Popup("스킬 추가", 0, new[] { "(전역 스킬에서 선택)" }.Concat(candidates).ToArray());
            if (pick > 0)
            {
                string chosen = candidates[pick - 1];
                Defer(() => u.skills.Add(chosen));
            }
        }

        private static void Swap<T>(IList<T> list, int a, int b)
        {
            T tmp = list[a];
            list[a] = list[b];
            list[b] = tmp;
        }

        // 체크한 항목만 이 유닛에서 전역 정의와 다른 값을 쓴다(skillOverrides). 체크를 풀면 전역 값으로 돌아간다.
        private void DrawOverrideEditor(UnitDto u, string skill, SkillDto global)
        {
            u.skillOverrides.TryGetValue(skill, out JObject ov);
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox("체크한 항목만 이 유닛에서 전역 정의와 다른 값을 씁니다. 체크하지 않은 항목은 전역 값을 따라가므로, 전역 값을 바꾸면 이 스킬을 쓰는 다른 유닛에도 반영됩니다.", MessageType.None);

            foreach (FieldInfo f in SkillTools.ValueFields)
            {
                bool on = ov != null && ov.ContainsKey(f.Name);
                object current = on ? ov[f.Name].ToObject(f.FieldType) : f.GetValue(global);
                var refAttr = f.GetCustomAttribute<AssetRefAttribute>();
                string label = ObjectNames.NicifyVariableName(f.Name);

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool nowOn = EditorGUILayout.Toggle(on, GUILayout.Width(18));
                    object edited;
                    using (new EditorGUI.DisabledScope(!nowOn))
                        edited = DrawLeaf(label, f.FieldType, current, refAttr);

                    if (nowOn && !on)
                    {
                        if (ov == null) { ov = new JObject(); u.skillOverrides[skill] = ov; }
                        ov[f.Name] = JToken.FromObject(current ?? "");
                        GUI.changed = true;
                    }
                    else if (!nowOn && on)
                    {
                        ov.Remove(f.Name);
                        if (ov.Count == 0) { u.skillOverrides.Remove(skill); ov = null; }
                        GUI.changed = true;
                    }
                    else if (nowOn && !SkillTools.ValuesEqual(edited, current))
                    {
                        ov[f.Name] = JToken.FromObject(edited ?? "");
                    }
                }
            }
            EditorGUI.indentLevel--;
        }

        // ── 스킬 정의 탭 ────────────────────────────────────────────────
        private void DrawSkillTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(230)))
                {
                    EditorGUILayout.LabelField($"전역 스킬 정의 ({_skills.skills.Count})", EditorStyles.boldLabel);
                    foreach (SkillDto s in _skills.skills)
                    {
                        int users = UsersOfSkill(s.skillName).Count;
                        GUIStyle style = s.skillName == _selectedSkill ? EditorStyles.whiteLabel : EditorStyles.label;
                        Rect rect = GUILayoutUtility.GetRect(new GUIContent(s.skillName), style, GUILayout.ExpandWidth(true));
                        if (s.skillName == _selectedSkill) EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.9f, 0.55f));
                        if (GUI.Button(rect, $"{s.skillName}   ({users}명)", style)) _selectedSkill = s.skillName;
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("＋ 새 스킬")) Defer(AddSkill);
                        if (GUILayout.Button("－ 선택 제거")) { string target = _selectedSkill; Defer(() => RemoveSkill(target)); }
                    }
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    SkillDto skill = _skills.skills.FirstOrDefault(s => s.skillName == _selectedSkill);
                    if (skill == null) { EditorGUILayout.HelpBox("왼쪽에서 스킬을 선택하세요.", MessageType.Info); return; }

                    EditorGUILayout.LabelField(skill.skillName, EditorStyles.boldLabel);
                    List<string> users = UsersOfSkill(skill.skillName);
                    EditorGUILayout.LabelField("사용 유닛", users.Count == 0 ? "(없음)" : string.Join(", ", users));

                    if (_renameFor != skill.skillName) { _renameFor = skill.skillName; _renameBuffer = skill.skillName; }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _renameBuffer = EditorGUILayout.TextField("이름 변경", _renameBuffer);
                        if (GUILayout.Button("적용", GUILayout.Width(50))) { string from = skill.skillName, to = _renameBuffer; Defer(() => RenameSkill(from, to)); }
                    }
                    EditorGUILayout.HelpBox("여기 값은 이 스킬을 쓰는 모든 유닛의 기준값입니다. 특정 유닛만 다르게 하려면 [유닛] 탭의 스킬 목록에서 '이 유닛만 다른 값'을 체크하세요.", MessageType.None);
                    DrawObject(skill, "skill/" + skill.skillName, new HashSet<string> { nameof(SkillDto.skillName) });
                }
            }
        }

        private void AddSkill()
        {
            string baseName = "새 스킬";
            string name = baseName;
            for (int n = 2; _skills.skills.Any(s => s.skillName == name); n++) name = $"{baseName} {n}";
            _skills.skills.Add(new SkillDto
            {
                skillName = name, baseDelayMs = 800f, baseCooldown = 5f, hitShape = "LINE",
                hitRange = 1, hitWidth = 1, hitDepth = 1, threatRange = 1, threatWidth = 1, threatDepth = 1, damageMultiplier = 1f,
            });
            _selectedSkill = name;
        }

        private void RemoveSkill(string name)
        {
            SkillDto skill = _skills.skills.FirstOrDefault(s => s.skillName == name);
            if (skill == null) return;
            List<string> users = UsersOfSkill(name);
            if (users.Count > 0)
            {
                EditorUtility.DisplayDialog("스킬 제거", $"'{name}' 스킬을 쓰는 유닛이 있어 제거할 수 없습니다:\n{string.Join(", ", users)}\n먼저 해당 유닛의 스킬 목록에서 빼세요.", "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog("스킬 제거", $"'{name}' 전역 스킬 정의를 제거합니다.", "제거", "취소")) return;
            _skills.skills.Remove(skill);
            _selectedSkill = _skills.skills.Count > 0 ? _skills.skills[0].skillName : null;
        }

        // ── 프리팹 비교 탭 ──────────────────────────────────────────────
        private void DrawCompareTab()
        {
            if (string.IsNullOrEmpty(_selectedUnit)) { EditorGUILayout.HelpBox("왼쪽에서 유닛을 선택하세요.", MessageType.Info); return; }
            string name = _selectedUnit;
            UnitState state = StateOf(name, out int count);

            EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
            switch (state)
            {
                case UnitState.NoPrefab:
                    EditorGUILayout.HelpBox("프리팹이 아직 없습니다. 체크하고 [JSON → 프리팹 적용]을 누르면 새로 만듭니다.", MessageType.Info);
                    break;
                case UnitState.NoJson:
                    EditorGUILayout.HelpBox("JSON에 없는 프리팹입니다. 체크하고 [프리팹 → JSON 가져오기]로 JSON에 추가하세요.", MessageType.Info);
                    break;
                case UnitState.InSync:
                    EditorGUILayout.HelpBox("JSON과 프리팹이 같습니다.", MessageType.Info);
                    break;
                default:
                    EditorGUILayout.HelpBox($"JSON과 프리팹이 {count}곳 다릅니다. 프리팹이 최신이면 [프리팹 값을 JSON으로], JSON이 맞으면 [JSON 값을 프리팹에]를 누르세요.", MessageType.Warning);
                    break;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_snapshots.ContainsKey(name)))
                    if (GUILayout.Button("이 유닛: 프리팹 값을 JSON으로 가져오기")) Defer(() => ImportFromPrefabs(new List<string> { name }));
                using (new EditorGUI.DisabledScope(FindUnit(name) == null))
                    if (GUILayout.Button("이 유닛: JSON 값을 프리팹에 적용")) Defer(() => ApplyToPrefabs(new List<string> { name }));
            }

            if (!_diffs.TryGetValue(name, out List<DiffItem> diffs) || diffs.Count == 0) return;

            float pathWidth = Mathf.Max(220f, position.width * 0.30f);
            float valueWidth = Mathf.Max(120f, (position.width - 280f - pathWidth - 40f) * 0.5f);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("항목", GUILayout.Width(pathWidth));
                GUILayout.Label("JSON", GUILayout.Width(valueWidth));
                GUILayout.Label("프리팹", GUILayout.Width(valueWidth));
            }
            _compareScroll = EditorGUILayout.BeginScrollView(_compareScroll, GUILayout.MinHeight(160));
            foreach (DiffItem d in diffs.Take(400))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(d.Path, GUILayout.Width(pathWidth));
                    GUILayout.Label(d.Left, GUILayout.Width(valueWidth));
                    GUILayout.Label(d.Right, GUILayout.Width(valueWidth));
                }
            }
            if (diffs.Count > 400) GUILayout.Label($"… 외 {diffs.Count - 400}건");
            EditorGUILayout.EndScrollView();
        }

        // ── 하단 바 ─────────────────────────────────────────────────────
        private void DrawBottomBar()
        {
            if (!string.IsNullOrEmpty(_lastMessage)) EditorGUILayout.HelpBox(_lastMessage, MessageType.None);

            List<string> selected = _checked.Where(n => AllUnitNames().Contains(n)).ToList();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(!hasUnsavedChanges))
                {
                    if (GUILayout.Button("JSON 저장 (Ctrl+S)", EditorStyles.toolbarButton, GUILayout.Width(130))) Defer(() => TrySave());
                    if (GUILayout.Button("변경 버리기", EditorStyles.toolbarButton, GUILayout.Width(80)))
                        Defer(() =>
                        {
                            if (!EditorUtility.DisplayDialog("변경 버리기", "저장하지 않은 변경을 모두 버리고 디스크의 JSON을 다시 불러옵니다.", "버리기", "취소")) return;
                            LoadFromDisk();
                            RefreshSnapshots();
                        });
                }
                if (GUILayout.Button("프리팹 다시 읽기", EditorStyles.toolbarButton, GUILayout.Width(100))) Defer(RefreshSnapshots);

                GUILayout.FlexibleSpace();
                GUILayout.Label($"체크 {selected.Count}개", GUILayout.Width(60));
                using (new EditorGUI.DisabledScope(selected.Count == 0))
                {
                    if (GUILayout.Button($"프리팹 → JSON 가져오기 ({selected.Count})", EditorStyles.toolbarButton, GUILayout.Width(190))) Defer(() => ImportFromPrefabs(selected));
                    if (GUILayout.Button($"JSON → 프리팹 적용 ({selected.Count})", EditorStyles.toolbarButton, GUILayout.Width(170))) Defer(() => ApplyToPrefabs(selected));
                }
                if (GUILayout.Button("검증", EditorStyles.toolbarButton, GUILayout.Width(50))) Defer(RunVerification);
            }
        }
    }
}
