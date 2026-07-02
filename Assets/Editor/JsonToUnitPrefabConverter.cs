using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 기존 Assets/Data/skills.json, units.json의 값을 유닛 타입 프리팹(UnitVisualDefinition)의
// 초기값으로 옮겨주는 1회성 에디터 도구. 스프라이트/애니메이션/이펙트는 JSON에 없던 정보라
// 여기서는 채워지지 않는다 — 생성된 프리팹을 열어서 직접 할당해야 한다.
public static class JsonToUnitPrefabConverter
{
    private const string UnitsJsonPath  = "Assets/Data/units.json";
    private const string SkillsJsonPath = "Assets/Data/skills.json";
    private const string OutputFolder   = "Assets/Prefabs/Units";

    [System.Serializable]
    private class JsonUnitData
    {
        public string typeName;
        public string unitClass;
        public float[] footprint;
        public int engageDistance;
        public string[] skills;
        public UnitStatsData stats;
    }

    [System.Serializable]
    private class JsonUnitDatabase { public JsonUnitData[] units; }

    [System.Serializable]
    private class JsonSkillDatabase { public SkillData[] skills; }

    [MenuItem("Tools/GrimArchive/JSON -> 유닛 프리팹 생성")]
    public static void ConvertJsonToPrefabs()
    {
        if (!File.Exists(UnitsJsonPath))
        {
            Debug.LogError($"[JsonToUnitPrefabConverter] {UnitsJsonPath} 파일을 찾을 수 없습니다.");
            return;
        }
        if (!File.Exists(SkillsJsonPath))
        {
            Debug.LogError($"[JsonToUnitPrefabConverter] {SkillsJsonPath} 파일을 찾을 수 없습니다.");
            return;
        }

        var unitDb  = JsonUtility.FromJson<JsonUnitDatabase>(File.ReadAllText(UnitsJsonPath));
        var skillDb = JsonUtility.FromJson<JsonSkillDatabase>(File.ReadAllText(SkillsJsonPath));

        var skillLookup = new Dictionary<string, SkillData>();
        foreach (var s in skillDb.skills) skillLookup[s.skillName] = s;

        if (!AssetDatabase.IsValidFolder(OutputFolder))
            CreateFolderRecursive(OutputFolder);

        var spriteManager = Object.FindObjectOfType<UnitSpriteManager>();

        int created = 0;
        foreach (var u in unitDb.units)
        {
            GameObject root = new GameObject(u.typeName);
            var def = root.AddComponent<UnitVisualDefinition>();

            def.unitTypeName    = u.typeName;
            def.footprint       = (u.footprint != null && u.footprint.Length >= 2) ? new Vector2(u.footprint[0], u.footprint[1]) : Vector2.one;
            def.engageDistance  = u.engageDistance;
            def.stats           = u.stats;

            def.skills = new List<SkillData>();
            foreach (var skillName in u.skills)
            {
                if (skillLookup.TryGetValue(skillName, out var sd))
                    def.skills.Add(sd);
                else
                    Debug.LogWarning($"[JsonToUnitPrefabConverter] '{u.typeName}'의 스킬 '{skillName}'을 skills.json에서 찾을 수 없습니다.");
            }

            // 스프라이트/애니메이션 작업을 바로 시작할 수 있도록 최소 자식 계층까지 만들어 둔다.
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.AddComponent<SpriteRenderer>();

            GameObject outline = new GameObject("Outline");
            outline.transform.SetParent(visual.transform);
            outline.transform.localPosition = Vector3.zero;
            outline.transform.localScale    = new Vector3(1.2f, 1.2f, 1f);
            var outlineSr = outline.AddComponent<SpriteRenderer>();
            outlineSr.color        = Color.black;
            outlineSr.sortingOrder = 9;
            outline.SetActive(false);

            string path = $"{OutputFolder}/{SanitizeFileName(u.typeName)}.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            created++;

            if (spriteManager != null)
            {
                var existing = spriteManager.unitTypePrefabMap.Find(e => e.unitTypeName == u.typeName);
                if (existing != null) existing.prefab = savedPrefab;
                else spriteManager.unitTypePrefabMap.Add(new UnitSpriteManager.UnitTypePrefab { unitTypeName = u.typeName, prefab = savedPrefab });
                EditorUtility.SetDirty(spriteManager);
            }

            Debug.Log($"[JsonToUnitPrefabConverter] 생성됨: {path} (스킬 {def.skills.Count}/{u.skills.Length}개 연결)");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (spriteManager != null)
        {
            EditorUtility.SetDirty(spriteManager);
            EditorSceneManager.MarkSceneDirty(spriteManager.gameObject.scene);
            Debug.Log("[JsonToUnitPrefabConverter] 씬의 UnitSpriteManager에도 자동 등록했습니다 (씬 저장 필요, Ctrl+S).");
        }
        else
        {
            Debug.LogWarning("[JsonToUnitPrefabConverter] 씬에서 UnitSpriteManager를 못 찾았습니다 (ssh.unity가 열려있는지 확인) — " +
                              "unitTypePrefabMap에 직접 등록해야 합니다.");
        }

        Debug.Log($"[JsonToUnitPrefabConverter] 완료 — 프리팹 {created}개 생성됨 ({OutputFolder}). " +
                  "스프라이트/애니메이션/이펙트는 JSON에 없던 정보라 각 프리팹을 열어 직접 채워야 합니다.");
    }

    private static void CreateFolderRecursive(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
