using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UObject = UnityEngine.Object;

namespace UnitDataTool
{
    // JSON의 에셋 참조 문자열 ↔ 실제 에셋. 참조 표기는 "이름" 또는 "이름#GUID"다.
    //  · 이름이 프로젝트에서 유일하면 이름만 쓴다(사람이 JSON을 직접 고치기 쉽다).
    //  · 같은 이름의 에셋이 둘 이상이면 "이름#GUID"로 어느 파일인지 고정한다(GUID는 파일, 이름은 시트 안의 스프라이트).
    //  · 해석이 모호하거나 실패하면 problem 문자열로 알려 준다(조용히 다른 에셋을 고르지 않는다).
    public static class UnitAssetRefs
    {
        private static readonly Dictionary<string, List<UObject>> FindCache = new Dictionary<string, List<UObject>>();
        private static readonly Dictionary<string, KeyValuePair<UObject, string>> ResolveCache = new Dictionary<string, KeyValuePair<UObject, string>>();

        public static Type TypeOf(RefKind kind)
        {
            switch (kind)
            {
                case RefKind.Prefab: return typeof(GameObject);
                case RefKind.Sprite: return typeof(Sprite);
                default: return typeof(SpriteLibraryAsset);
            }
        }

        // 에셋이 바뀌었을 수 있는 시점(새로고침·적용 후)에 부른다.
        public static void ClearCache()
        {
            FindCache.Clear();
            ResolveCache.Clear();
        }

        public static void SplitRef(string reference, out string name, out string guid)
        {
            int i = reference.IndexOf('#');
            if (i < 0) { name = reference; guid = null; return; }
            name = reference.Substring(0, i);
            guid = reference.Substring(i + 1);
        }

        // 이름이 같은 에셋을 전부 찾는다. Assets/ 안의 것이 있으면 패키지 쪽은 버린다.
        public static List<UObject> FindAll(Type type, string name)
        {
            if (string.IsNullOrEmpty(name)) return new List<UObject>();
            string key = type.Name + "|" + name;
            if (FindCache.TryGetValue(key, out List<UObject> cached)) return cached;

            var found = new List<UObject>();
            var seen = new HashSet<UObject>();

            void Collect(string searchTerm)
            {
                string filter = type == typeof(GameObject) ? "t:Prefab" : "t:" + type.Name;
                foreach (string guid in AssetDatabase.FindAssets(filter + " " + searchTerm))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (UObject o in CandidatesAt(type, path))
                        if (o.name == name && seen.Add(o)) found.Add(o);
                }
            }

            Collect(name);
            if (found.Count == 0 && type == typeof(Sprite))
            {
                // 스프라이트 시트 안의 개별 스프라이트는 파일 이름이 이름의 앞부분이다(Weapon_LongSwordA_0 ← Weapon_LongSwordA.png).
                string stem = name;
                for (int i = 0; i < 3 && found.Count == 0; i++)
                {
                    int cut = stem.LastIndexOf('_');
                    if (cut <= 0) break;
                    stem = stem.Substring(0, cut);
                    Collect(stem);
                }
            }

            if (found.Any(o => IsProjectAsset(o)))
                found.RemoveAll(o => !IsProjectAsset(o));

            FindCache[key] = found;
            return found;
        }

        private static bool IsProjectAsset(UObject o)
            => AssetDatabase.GetAssetPath(o).StartsWith("Assets/", StringComparison.Ordinal);

        private static IEnumerable<UObject> CandidatesAt(Type type, string path)
        {
            if (type == typeof(GameObject))
            {
                // 프리팹 파일은 루트 오브젝트만 대상이다(자식 오브젝트가 같은 이름이어도 별개).
                GameObject main = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
                if (main != null) yield return main;
                yield break;
            }
            foreach (UObject o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o != null && type.IsInstanceOfType(o)) yield return o;
        }

        public static UObject Resolve(Type type, string reference, out string problem)
        {
            problem = null;
            if (string.IsNullOrEmpty(reference)) return null;

            string cacheKey = type.Name + "|" + reference;
            if (ResolveCache.TryGetValue(cacheKey, out KeyValuePair<UObject, string> hit) && (hit.Key != null || hit.Value != null))
            {
                problem = hit.Value;
                return hit.Key;
            }

            UObject result = ResolveUncached(type, reference, out problem);
            ResolveCache[cacheKey] = new KeyValuePair<UObject, string>(result, problem);
            return result;
        }

        public static T Resolve<T>(string reference, out string problem) where T : UObject
            => Resolve(typeof(T), reference, out problem) as T;

        private static UObject ResolveUncached(Type type, string reference, out string problem)
        {
            problem = null;
            SplitRef(reference, out string name, out string guid);

            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    problem = $"'{reference}': GUID에 해당하는 에셋이 없어 이름으로 찾습니다.";
                }
                else
                {
                    UObject exact = CandidatesAt(type, path).FirstOrDefault(o => o.name == name);
                    if (exact != null) return exact;
                    problem = $"'{reference}': 그 파일({path})에 '{name}'({type.Name})이 없어 이름으로 찾습니다.";
                }
            }

            List<UObject> all = FindAll(type, name);
            if (all.Count == 0)
            {
                problem = $"'{name}'({type.Name})을(를) 프로젝트에서 찾을 수 없습니다.";
                return null;
            }
            if (all.Count > 1)
            {
                problem = $"'{name}' 이름의 {type.Name}이(가) {all.Count}개 있어 첫 번째를 씁니다. 에셋을 직접 지정해 구분하세요.";
            }
            return all[0];
        }

        // 참조를 JSON 문자열로 바꾼다. 이름이 겹칠 때만 GUID를 붙인다.
        public static string NameOf(Type type, UObject obj)
        {
            if (obj == null) return null;
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && FindAll(type, obj.name).Count > 1)
                return obj.name + "#" + AssetDatabase.AssetPathToGUID(path);
            return obj.name;
        }
    }
}
