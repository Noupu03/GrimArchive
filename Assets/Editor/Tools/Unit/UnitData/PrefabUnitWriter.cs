using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UObject = UnityEngine.Object;

namespace UnitDataTool
{
    // UnitDto(+유효 스킬) → 유닛 프리팹. 예전 컨버터와 달리 새로 만들어 덮어쓰지 않고 기존 프리팹을 열어 "JSON 스키마가
    // 소유한 값"만 갱신한다. 그래서 스키마에 없는 것(애니메이션 클립, 리그, 인형 줄·십자가 같은 장식, Outline 등)은
    // 그대로 남는다. 규칙:
    //  · 스키마에 있는 값은 JSON이 주인이다 — JSON에 없으면(기본값이면) 프리팹 값도 비운다.
    //  · 에셋 참조를 찾지 못하면 기존 프리팹 값을 유지하고 경고한다(찾지 못해 비워 버리는 사고를 막는다).
    //  · 스프라이트 라이브러리·무기·보스 손 "블록 자체"가 JSON에 없으면 컴포넌트/오브젝트를 지우지 않고 둔다
    //    (무기만은 스프라이트를 비운다 — 빈 소켓은 남는다).
    public static class PrefabUnitWriter
    {
        public sealed class ApplyResult
        {
            public string TypeName;
            public string Path;
            public bool Created;
            public string Error;
            public List<string> Warnings = new List<string>();
        }

        // 메모리 상의 루트에 값을 반영한다(저장은 호출자 몫). 실제 적용·검증(드라이런)이 같은 코드를 쓴다.
        public static void ApplyToRoot(GameObject root, UnitDto u, IReadOnlyList<SkillDto> effectiveSkills, List<string> warnings)
        {
            UnitVisualDefinition def = root.GetComponent<UnitVisualDefinition>();
            if (def == null) def = root.AddComponent<UnitVisualDefinition>();

            def.unitTypeName = u.typeName;
            def.footprint = new Vector2(At(u.footprint, 0, 1f), At(u.footprint, 1, 1f));
            def.engageDistance = u.engageDistance;
            def.populationCost = u.populationCost;
            def.visualScaleIgnoresFootprint = u.visualScaleIgnoresFootprint;

            if (def.stats == null) def.stats = new UnitStatsData();
            foreach (FieldInfo f in typeof(UnitStatsData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                f.SetValue(def.stats, f.GetValue(u.stats ?? new UnitStatsData()));

            List<SkillData> oldSkills = def.skills ?? new List<SkillData>();
            var newSkills = new List<SkillData>();
            foreach (SkillDto s in effectiveSkills)
                newSkills.Add(ToSkillData(s, oldSkills.FirstOrDefault(x => x.skillName == s.skillName), warnings, u.typeName));
            def.skills = newSkills;

            WeightDto w = u.weight ?? new WeightDto();
            def.isSpecialUnit = w.isSpecialUnit;
            def.isInterestTarget = w.isInterestTarget;
            def.baseInterest = w.baseInterest;
            def.baseDanger = w.baseDanger;
            def.heavyHitThreshold = w.heavyHitThreshold;
            def.stealth = w.stealth;
            def.baseVisibility = w.baseVisibility;

            EffectsDto fx = u.visual != null ? u.visual.effects : null;
            def.hitSparkPrefab = Keep<GameObject>(fx != null ? fx.hitSpark : null, def.hitSparkPrefab, warnings, "hitSpark");
            def.bloodEffectPrefab = Keep<GameObject>(fx != null ? fx.bloodDrip : null, def.bloodEffectPrefab, warnings, "bloodDrip");
            def.guardPrefab = Keep<GameObject>(fx != null ? fx.guard : null, def.guardPrefab, warnings, "guard");
            def.parryPrefab = Keep<GameObject>(fx != null ? fx.parry : null, def.parryPrefab, warnings, "parry");
            def.attackFailPrefab = Keep<GameObject>(fx != null ? fx.attackFail : null, def.attackFailPrefab, warnings, "attackFail");

            DeathDto death = u.visual != null ? u.visual.death : null;
            def.deathVfxPrefab = Keep<GameObject>(death != null ? death.vfx : null, def.deathVfxPrefab, warnings, "death.vfx");
            def.corpseSprite = Keep<Sprite>(death != null ? death.corpseSprite : null, def.corpseSprite, warnings, "death.corpseSprite");

            Transform visual = root.transform.Find("Visual");
            if (visual == null)
            {
                var go = new GameObject("Visual");
                go.transform.SetParent(root.transform, false);
                go.AddComponent<SpriteRenderer>().sortingOrder = 10;
                visual = go.transform;
            }

            ApplySpriteLibrary(visual, u.visual != null ? u.visual.spriteLibrary : null, warnings);
            ApplyWeapon(visual, u.visual != null ? u.visual.weapon : null, warnings);
            ApplyBossHands(root, visual, u.visual != null ? u.visual.bossHands : null, warnings);

            // 애니메이션 슬롯 목록을 스킬 목록에 맞춘다(이미 꽂은 클립은 보존된다).
            AnimationSlotSync.ApplyTo(root, u.unitClass == "Human", u.skills, u.typeName);
            if (root.GetComponent<AnimationEventVfxSpawner>() == null) root.AddComponent<AnimationEventVfxSpawner>();
        }

        // 실제 적용: 프리팹을 열어(없으면 새로 만들어) 값을 반영하고 저장한 뒤 Addressables에 등록한다.
        public static ApplyResult Apply(UnitDto u, IReadOnlyList<SkillDto> effectiveSkills)
        {
            var result = new ApplyResult { TypeName = u.typeName, Path = PrefabUnitReader.PathFor(u.typeName) };
            try
            {
                EnsureFolder(PrefabUnitReader.Folder);
                bool exists = File.Exists(result.Path);
                GameObject root = exists ? PrefabUtility.LoadPrefabContents(result.Path) : new GameObject(u.typeName);
                try
                {
                    ApplyToRoot(root, u, effectiveSkills, result.Warnings);
                    PrefabUtility.SaveAsPrefabAsset(root, result.Path);
                }
                finally
                {
                    if (exists) PrefabUtility.UnloadPrefabContents(root);
                    else UObject.DestroyImmediate(root);
                }
                result.Created = !exists;
                AddressablesEntryUtil.EnsureEntry(result.Path, AssetKeys.Unit(PrefabUnitReader.SanitizeFileName(u.typeName)), AssetKeys.UnitsLabel);
            }
            catch (Exception e)
            {
                result.Error = e.Message;
                Debug.LogException(e);
            }
            return result;
        }

        // 검증용: 저장하지 않고 적용 → 다시 읽기. JSON을 적용한 결과를 프리팹 쪽에서 읽었을 때 JSON과 같아야 한다.
        public static PrefabSnapshot DryRun(UnitDto u, IReadOnlyList<SkillDto> effectiveSkills, List<string> warnings)
        {
            string path = PrefabUnitReader.PathFor(u.typeName);
            bool exists = File.Exists(path);
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(u.typeName);
            try
            {
                ApplyToRoot(root, u, effectiveSkills, warnings);
                return PrefabUnitReader.ReadRoot(root);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else UObject.DestroyImmediate(root);
            }
        }

        // ── 스프라이트 라이브러리 ──────────────────────────────────────
        private static void ApplySpriteLibrary(Transform visual, string reference, List<string> warnings)
        {
            if (string.IsNullOrEmpty(reference)) return;      // 라이브러리를 JSON에서 비웠다고 컴포넌트를 지우지는 않는다
            var asset = UnitAssetRefs.Resolve<SpriteLibraryAsset>(reference, out string problem);
            if (problem != null) warnings.Add("spriteLibrary: " + problem);
            if (asset == null) return;

            SpriteLibrary lib = visual.GetComponent<SpriteLibrary>();
            if (lib == null) lib = visual.gameObject.AddComponent<SpriteLibrary>();
            if (visual.GetComponent<SpriteResolver>() == null) visual.gameObject.AddComponent<SpriteResolver>();
            if (lib.spriteLibraryAsset != asset) lib.spriteLibraryAsset = asset;
        }

        // ── 무기 소켓 ─────────────────────────────────────────────────
        private static void ApplyWeapon(Transform visual, WeaponDto w, List<string> warnings)
        {
            Transform socket = visual.Find("WeaponSocket");
            if (w == null)
            {
                // 무기가 JSON에 없으면 보이는 무기만 치운다(빈 소켓·포즈 튜닝은 남겨 둔다).
                if (socket != null)
                {
                    SpriteRenderer existing = socket.GetComponent<SpriteRenderer>();
                    if (existing != null && existing.sprite != null) existing.sprite = null;
                }
                return;
            }

            if (socket == null)
            {
                var go = new GameObject("WeaponSocket");
                go.transform.SetParent(visual, false);
                socket = go.transform;
            }
            SpriteRenderer sr = socket.GetComponent<SpriteRenderer>();
            if (sr == null) sr = socket.gameObject.AddComponent<SpriteRenderer>();
            WeaponAttachment wa = socket.GetComponent<WeaponAttachment>();
            if (wa == null) wa = socket.gameObject.AddComponent<WeaponAttachment>();

            sr.sprite = Keep<Sprite>(w.sprite, sr.sprite, warnings, "weapon.sprite");

            var so = new SerializedObject(wa);
            so.FindProperty("nativeSpriteAngle").floatValue = w.nativeSpriteAngle;
            List<PoseDto> poses = w.poses ?? WeaponDefaults.Poses;      // poses가 없으면 컴포넌트 기본 포즈
            SerializedProperty arr = so.FindProperty("poses");
            arr.arraySize = poses.Count;
            for (int i = 0; i < poses.Count; i++)
            {
                SerializedProperty el = arr.GetArrayElementAtIndex(i);
                if (!Enum.TryParse(poses[i].direction, true, out Dir dir))
                {
                    warnings.Add($"weapon.poses[{i}]: 알 수 없는 방향 '{poses[i].direction}' → UP으로 처리");
                    dir = Dir.UP;
                }
                el.FindPropertyRelative("direction").intValue = (int)dir;
                el.FindPropertyRelative("offset").vector2Value = new Vector2(At(poses[i].offset, 0, 0f), At(poses[i].offset, 1, 0f));
                el.FindPropertyRelative("targetAngle").floatValue = poses[i].targetAngle;
                el.FindPropertyRelative("sortingOrder").intValue = poses[i].sortingOrder;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 보스 골렘 손 ──────────────────────────────────────────────
        private static void ApplyBossHands(GameObject root, Transform visual, BossHandsDto h, List<string> warnings)
        {
            if (h == null) return;     // 손 블록이 JSON에 없으면 기존 손을 지우지 않는다

            Transform left = EnsureHand(visual, "LeftHand", h.left, warnings);
            Transform right = EnsureHand(visual, "RightHand", h.right, warnings);

            BossGolemHandController hc = root.GetComponent<BossGolemHandController>();
            if (hc == null) hc = root.AddComponent<BossGolemHandController>();
            var so = new SerializedObject(hc);
            if (left != null) so.FindProperty("leftHand").objectReferenceValue = left;
            if (right != null) so.FindProperty("rightHand").objectReferenceValue = right;
            so.FindProperty("slamTravelDuration").floatValue = h.slamTravelDuration;
            so.FindProperty("sweepTravelDuration").floatValue = h.sweepTravelDuration;
            so.FindProperty("clapTravelDuration").floatValue = h.clapTravelDuration;
            so.FindProperty("slamLiftHeight").floatValue = h.slamLiftHeight;
            so.FindProperty("clapApproachDistance").floatValue = h.clapApproachDistance;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform EnsureHand(Transform visual, string name, HandDto d, List<string> warnings)
        {
            Transform t = visual.Find(name);
            if (d == null) return t;

            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(visual, false);
                t = go.transform;
            }
            SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = Keep<Sprite>(d.sprite, sr.sprite, warnings, name + ".sprite");
            sr.sortingOrder = d.sortingOrder;
            t.localPosition = new Vector3(At(d.position, 0, 0f), At(d.position, 1, 0f), t.localPosition.z);
            float scaleZ = Mathf.Approximately(t.localScale.z, 0f) ? 1f : t.localScale.z;
            t.localScale = new Vector3(At(d.scale, 0, 1f), At(d.scale, 1, 1f), scaleZ);
            return t;
        }

        // ── 보조 ──────────────────────────────────────────────────────
        private static float At(float[] a, int i, float fallback) => a != null && a.Length > i ? a[i] : fallback;

        // 참조 문자열 → 에셋. 비어 있으면 null(비움), 못 찾으면 기존 값을 유지하고 경고한다.
        private static T Keep<T>(string reference, T existing, List<string> warnings, string label) where T : UObject
        {
            if (string.IsNullOrEmpty(reference)) return null;
            T resolved = UnitAssetRefs.Resolve<T>(reference, out string problem);
            if (problem != null) warnings.Add($"{label}: {problem}");
            return resolved != null ? resolved : existing;
        }

        private static SkillData ToSkillData(SkillDto s, SkillData old, List<string> warnings, string unit)
        {
            string label = unit + "/" + s.skillName;
            return new SkillData
            {
                skillName = s.skillName,
                baseDelayMs = s.baseDelayMs,
                baseCooldown = s.baseCooldown,
                cooldownSlot = s.cooldownSlot,
                isProjectile = s.isProjectile,
                projectilePrefab = Keep<GameObject>(s.projectilePrefab, old != null ? old.projectilePrefab : null, warnings, label + ".projectilePrefab"),
                projectileSpeed = s.projectileSpeed,
                isPiercing = s.isPiercing,
                hitEffectPrefab = Keep<GameObject>(s.hitEffectPrefab, old != null ? old.hitEffectPrefab : null, warnings, label + ".hitEffectPrefab"),
                hitShape = s.hitShape,
                hitRange = s.hitRange,
                hitWidth = s.hitWidth,
                hitDepth = s.hitDepth,
                threatRange = s.threatRange,
                threatWidth = s.threatWidth,
                threatDepth = s.threatDepth,
                damageMultiplier = s.damageMultiplier,
                hasStun = s.hasStun,
                stunDuration = s.stunDuration,
                priorityBase = s.priorityBase,
                priorityKillMultiplier = s.priorityKillMultiplier,
                priorityKillBonus = s.priorityKillBonus,
                priorityRangeThreshold = s.priorityRangeThreshold,
                priorityRangeBonus = s.priorityRangeBonus,
                skillArchetype = s.skillArchetype,
                multiHitCount = s.multiHitCount,
                effectDuration = s.effectDuration,
                effectAmount = s.effectAmount,
                explosionRadius = s.explosionRadius,
            };
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
