using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

// Assets/Data/units.json, skills.json 값으로 유닛 타입 프리팹(UnitVisualDefinition + Visual 자식
// 계층)을 처음부터 끝까지 원클릭으로 생성하는 에디터 도구. 스탯/스킬뿐 아니라 스프라이트 라이브러리,
// 무기 소켓(WeaponAttachment), 피격/가드/패리 이펙트, 투사체 프리팹까지 전부 JSON에 적은 "이름"으로
// 프로젝트를 검색해 자동 연결한다 — 실행 후 인스펙터에서 수동으로 드래그할 값이 남지 않는 게 목표.
// (스프라이트 애니메이션 클립에 개별로 찍는 AnimationEvent만은 이 도구의 범위 밖이라 여전히 수동이다.)
//
// UnitSpriteManager.GetPrefab()이 Resources.Load(Assets/Resources/Units/{typeName}.prefab) 경로
// 컨벤션으로 조회하므로, 출력 폴더도 반드시 Resources 아래여야 한다 — 별도 등록 절차 불필요.
//
// 재실행하면 같은 이름의 프리팹을 통째로 덮어쓴다 — JSON이 유일한 원본(source of truth)이라는 뜻.
// 프리팹을 인스펙터에서 직접 손댄 값(예: WeaponAttachment의 방향별 poses 세부 튜닝)은 JSON에 없으므로
// 재실행 시 컴포넌트 기본값으로 되돌아간다.
public static class JsonToUnitPrefabConverter
{
    private const string UnitsJsonPath  = "Assets/Data/units.json";
    private const string SkillsJsonPath = "Assets/Data/skills.json";
    private const string OutputFolder   = "Assets/Resources/Units";

    // ── JSON DTO ──────────────────────────────────────────────────────
    // 엔진 에셋(스프라이트/프리팹/애니메이터 컨트롤러) 참조는 전부 "이름" 문자열로만 받는다.
    // JsonUtility는 GameObject/Sprite 같은 엔진 레퍼런스 필드를 채워줄 수 없어서, 여기서는 이름만
    // 파싱한 뒤 FindAssetByName으로 프로젝트를 검색해 실제 레퍼런스로 바꿔치기한다.

    [System.Serializable]
    private class JsonWeightData
    {
        public bool  isSpecialUnit;
        public bool  isInterestTarget;
        public float baseInterest;
        public float baseDanger;
        public float heavyHitThreshold = 10f;
        public float stealth;
        public float baseVisibility = 100f;
    }

    [System.Serializable]
    private class JsonWeaponData
    {
        public string sprite;
        public float  nativeSpriteAngle = 135f;
    }

    [System.Serializable]
    private class JsonEffectsData
    {
        public string hitSpark;
        public string guard;
        public string parry;
        public string attackFail;
    }

    [System.Serializable]
    private class JsonVisualData
    {
        public string          spriteLibrary;
        public string          animatorController;
        public JsonWeaponData  weapon;
        public JsonEffectsData effects;
    }

    [System.Serializable]
    private class JsonUnitData
    {
        public string        typeName;
        public string        unitClass;
        public float[]       footprint;
        public int           engageDistance;
        public string[]      skills;
        public UnitStatsData stats;
        public JsonWeightData weight;
        public JsonVisualData visual;
        // 유닛 배치 시스템(2026-07-27 신규) 5.2장: 기본 유닛 인구수 점유량(2026-07-27 사용자 요청으로
        // 2→1 조정). JSON에 값이 없으면 JsonUtility가 이 기본값을 그대로 보존한다(heavyHitThreshold/
        // baseVisibility와 동일 관례).
        public int           populationCost = 1;
    }

    [System.Serializable]
    private class JsonUnitDatabase { public JsonUnitData[] units; }

    // SkillData를 JSON DTO로 바로 재사용하지 않는 이유: projectilePrefab/hitEffectPrefab이
    // GameObject 필드라 JsonUtility가 못 채운다. 이름 문자열로 받아뒀다가 ToSkillData()에서 해석한다.
    [System.Serializable]
    private class JsonSkillData
    {
        public string skillName;
        public float  baseDelayMs;
        public float  baseCooldown;
        public int    cooldownSlot;
        public bool   isProjectile;
        public string projectilePrefab;
        public float  projectileSpeed;
        public bool   isPiercing;
        public string hitEffectPrefab;
        public string hitShape;
        public int    hitRange, hitWidth, hitDepth;
        public int    threatRange, threatWidth, threatDepth;
        public float  damageMultiplier;
        public bool   hasStun;
        public float  stunDuration;
        public float  priorityBase;
        public float  priorityKillMultiplier;
        public float  priorityKillBonus;
        public float  priorityRangeThreshold;
        public float  priorityRangeBonus;
        
        public string skillArchetype;
        public int    multiHitCount;
        public float  effectDuration;
        public float  effectAmount;
        public float  explosionRadius;

        public SkillData ToSkillData() => new SkillData
        {
            skillName        = skillName,
            baseDelayMs      = baseDelayMs,
            baseCooldown     = baseCooldown,
            cooldownSlot     = cooldownSlot,
            isProjectile     = isProjectile,
            projectilePrefab = FindAssetByName<GameObject>(projectilePrefab),
            projectileSpeed  = projectileSpeed,
            isPiercing       = isPiercing,
            hitEffectPrefab  = FindAssetByName<GameObject>(hitEffectPrefab),
            hitShape         = hitShape,
            hitRange = hitRange, hitWidth = hitWidth, hitDepth = hitDepth,
            threatRange = threatRange, threatWidth = threatWidth, threatDepth = threatDepth,
            damageMultiplier = damageMultiplier,
            hasStun          = hasStun,
            stunDuration     = stunDuration,
            priorityBase     = priorityBase,
            priorityKillMultiplier = priorityKillMultiplier,
            priorityKillBonus      = priorityKillBonus,
            priorityRangeThreshold = priorityRangeThreshold,
            priorityRangeBonus     = priorityRangeBonus,
            skillArchetype   = skillArchetype,
            multiHitCount    = multiHitCount,
            effectDuration   = effectDuration,
            effectAmount     = effectAmount,
            explosionRadius  = explosionRadius,
        };
    }

    [System.Serializable]
    private class JsonSkillDatabase { public JsonSkillData[] skills; }

    [MenuItem("Tools/GrimArchive/JSON -> 유닛 프리팹 생성 (원클릭)")]
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
        foreach (var s in skillDb.skills) skillLookup[s.skillName] = s.ToSkillData();

        if (!AssetDatabase.IsValidFolder(OutputFolder))
            CreateFolderRecursive(OutputFolder);

        int created = 0;
        foreach (var u in unitDb.units)
        {
            GameObject root = new GameObject(u.typeName);
            var def = root.AddComponent<UnitVisualDefinition>();

            def.unitTypeName   = u.typeName;
            def.footprint      = (u.footprint != null && u.footprint.Length >= 2) ? new Vector2(u.footprint[0], u.footprint[1]) : Vector2.one;
            def.engageDistance = u.engageDistance;
            def.populationCost = u.populationCost;
            def.stats          = u.stats;

            def.skills = new List<SkillData>();
            foreach (var skillName in u.skills)
            {
                if (skillLookup.TryGetValue(skillName, out var sd))
                    def.skills.Add(sd);
                else
                    Debug.LogWarning($"[JsonToUnitPrefabConverter] '{u.typeName}'의 스킬 '{skillName}'을 skills.json에서 찾을 수 없습니다.");
            }

            if (u.weight != null)
            {
                def.isSpecialUnit     = u.weight.isSpecialUnit;
                def.isInterestTarget  = u.weight.isInterestTarget;
                def.baseInterest      = u.weight.baseInterest;
                def.baseDanger        = u.weight.baseDanger;
                def.heavyHitThreshold = u.weight.heavyHitThreshold;
                def.stealth           = u.weight.stealth;
                def.baseVisibility    = u.weight.baseVisibility;
            }

            if (u.visual?.effects != null)
            {
                def.hitSparkPrefab   = FindAssetByName<GameObject>(u.visual.effects.hitSpark);
                def.guardPrefab      = FindAssetByName<GameObject>(u.visual.effects.guard);
                def.parryPrefab      = FindAssetByName<GameObject>(u.visual.effects.parry);
                def.attackFailPrefab = FindAssetByName<GameObject>(u.visual.effects.attackFail);
            }

            // 스프라이트/애니메이션 작업을 바로 시작할 수 있도록 최소 자식 계층까지 만들어 둔다.
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.AddComponent<SpriteRenderer>().sortingOrder = 10;

#if UNITY_2022_2_OR_NEWER
            if (!string.IsNullOrEmpty(u.visual?.spriteLibrary))
            {
                var lib = visual.AddComponent<SpriteLibrary>();
                lib.spriteLibraryAsset = FindAssetByName<SpriteLibraryAsset>(u.visual.spriteLibrary);
                visual.AddComponent<SpriteResolver>();
            }
#endif

            // 선택 표시는 이제 UnitGenerate.EnsureSelectionMarker()가 런타임에 풋프린트 기준으로
            // 자동 생성한다(캐릭터 스프라이트와 무관) — 여기서 더 이상 "Outline" 자식을 굽지 않는다.

            if (u.visual?.weapon != null)
            {
                GameObject socket = new GameObject("WeaponSocket");
                socket.transform.SetParent(visual.transform);
                socket.transform.localPosition = Vector3.zero;
                socket.AddComponent<SpriteRenderer>().sprite = FindAssetByName<Sprite>(u.visual.weapon.sprite);

                var attachment = socket.AddComponent<WeaponAttachment>();
                var so = new SerializedObject(attachment);
                so.FindProperty("nativeSpriteAngle").floatValue = u.visual.weapon.nativeSpriteAngle;
                so.ApplyModifiedProperties();
            }

            if (!string.IsNullOrEmpty(u.visual?.animatorController))
            {
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = FindAssetByName<RuntimeAnimatorController>(u.visual.animatorController);
                root.AddComponent<AnimationEventVfxSpawner>();
            }

            string path = $"{OutputFolder}/{SanitizeFileName(u.typeName)}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            created++;

            Debug.Log($"[JsonToUnitPrefabConverter] 생성됨: {path} (스킬 {def.skills.Count}/{u.skills.Length}개 연결)");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[JsonToUnitPrefabConverter] 완료 — 프리팹 {created}개 생성됨 ({OutputFolder}). " +
                  "UnitSpriteManager.GetPrefab()이 파일명(타입명) 기준으로 자동 조회하므로 별도 등록은 필요 없다. " +
                  "이름으로 못 찾은 에셋은 위 경고 로그를 확인해서 units.json/skills.json의 이름 표기를 맞춰라.");
    }

    // 이름으로 프로젝트 전체에서 에셋을 찾는다. 스프라이트시트 내부 개별 스프라이트(예: 무기 스프라이트)처럼
    // 메인 에셋 파일명과 이름이 다른 서브 에셋까지 찾도록 LoadAllAssetRepresentationsAtPath도 확인한다.
    private static T FindAssetByName<T>(string name) where T : Object
    {
        if (string.IsNullOrEmpty(name)) return null;

        string typeFilter = typeof(T) == typeof(GameObject)                 ? "t:Prefab"
                           : typeof(T) == typeof(RuntimeAnimatorController) ? "t:AnimatorController"
                           : $"t:{typeof(T).Name}";

        foreach (var guid in AssetDatabase.FindAssets($"{typeFilter} {name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            foreach (var obj in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (obj is T typed && obj.name == name) return typed;

            var main = AssetDatabase.LoadAssetAtPath<T>(path);
            if (main != null && main.name == name) return main;
        }

        Debug.LogWarning($"[JsonToUnitPrefabConverter] 에셋을 찾을 수 없습니다: '{name}' ({typeof(T).Name})");
        return null;
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
