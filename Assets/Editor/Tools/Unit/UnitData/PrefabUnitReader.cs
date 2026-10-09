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
    public sealed class PrefabSnapshot
    {
        public UnitDto Unit;                                         // 읽지 못하면 null
        public List<SkillDto> Skills = new List<SkillDto>();         // 이 유닛 프리팹이 실제로 들고 있는(유효) 스킬
        public List<string> Problems = new List<string>();
    }

    // 유닛 프리팹 → UnitDto. 프리팹이 "런타임의 진실"이라는 전제로, JSON 스키마가 다루는 모든 값을 읽어 온다.
    // unitClass는 프리팹에 없는 값이라 채우지 않는다(JSON에 이미 있는 값을 유지한다).
    public static class PrefabUnitReader
    {
        public const string Folder = "Assets/Prefabs/Units";

        public static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        public static string PathFor(string typeName) => $"{Folder}/{SanitizeFileName(typeName)}.prefab";

        public static PrefabSnapshot ReadAsset(string typeName)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PathFor(typeName));
            return asset == null ? null : ReadRoot(asset);
        }

        // Assets/Prefabs/Units 안의 프리팹이 가진 유닛 타입 이름들.
        public static List<string> ListPrefabTypeNames()
        {
            var names = new List<string>();
            if (!AssetDatabase.IsValidFolder(Folder)) return names;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path).Replace('\\', '/') != Folder) continue;
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                UnitVisualDefinition def = go.GetComponent<UnitVisualDefinition>();
                if (def == null) continue;
                names.Add(string.IsNullOrEmpty(def.unitTypeName) ? go.name : def.unitTypeName);
            }
            return names;
        }

        public static PrefabSnapshot ReadRoot(GameObject root)
        {
            var snap = new PrefabSnapshot();
            UnitVisualDefinition def = root.GetComponent<UnitVisualDefinition>();
            if (def == null)
            {
                snap.Problems.Add("UnitVisualDefinition 컴포넌트가 없습니다.");
                return snap;
            }

            var u = new UnitDto
            {
                typeName = string.IsNullOrEmpty(def.unitTypeName) ? root.name : def.unitTypeName,
                footprint = new[] { def.footprint.x, def.footprint.y },
                engageDistance = def.engageDistance,
                populationCost = def.populationCost,
                visualScaleIgnoresFootprint = def.visualScaleIgnoresFootprint,
                stats = UnitDataJson.Clone(def.stats ?? new UnitStatsData()),
                weight = new WeightDto
                {
                    isSpecialUnit = def.isSpecialUnit,
                    isInterestTarget = def.isInterestTarget,
                    baseInterest = def.baseInterest,
                    baseDanger = def.baseDanger,
                    heavyHitThreshold = def.heavyHitThreshold,
                    stealth = def.stealth,
                    baseVisibility = def.baseVisibility,
                },
                visual = new VisualDto(),
            };

            foreach (SkillData sd in def.skills ?? new List<SkillData>())
            {
                u.skills.Add(sd.skillName);
                snap.Skills.Add(ToDto(sd));
            }

            var fx = new EffectsDto
            {
                hitSpark = Ref(typeof(GameObject), def.hitSparkPrefab),
                bloodDrip = Ref(typeof(GameObject), def.bloodEffectPrefab),
                guard = Ref(typeof(GameObject), def.guardPrefab),
                parry = Ref(typeof(GameObject), def.parryPrefab),
                attackFail = Ref(typeof(GameObject), def.attackFailPrefab),
            };
            if (fx.hitSpark != null || fx.bloodDrip != null || fx.guard != null || fx.parry != null || fx.attackFail != null)
                u.visual.effects = fx;

            var death = new DeathDto
            {
                vfx = Ref(typeof(GameObject), def.deathVfxPrefab),
                corpseSprite = Ref(typeof(Sprite), def.corpseSprite),
            };
            if (death.vfx != null || death.corpseSprite != null) u.visual.death = death;

            Transform visual = root.transform.Find("Visual");
            if (visual != null)
            {
                SpriteLibrary lib = visual.GetComponent<SpriteLibrary>();
                if (lib != null && lib.spriteLibraryAsset != null)
                    u.visual.spriteLibrary = Ref(typeof(SpriteLibraryAsset), lib.spriteLibraryAsset);
                u.visual.weapon = ReadWeapon(visual);
            }
            u.visual.bossHands = ReadBossHands(root);

            snap.Unit = u;
            return snap;
        }

        private static string Ref(Type type, UObject obj) => UnitAssetRefs.NameOf(type, obj);

        public static SkillDto ToDto(SkillData s) => new SkillDto
        {
            skillName = s.skillName,
            baseDelayMs = s.baseDelayMs,
            baseCooldown = s.baseCooldown,
            cooldownSlot = s.cooldownSlot,
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
            isProjectile = s.isProjectile,
            projectilePrefab = Ref(typeof(GameObject), s.projectilePrefab),
            projectileSpeed = s.projectileSpeed,
            isPiercing = s.isPiercing,
            skillArchetype = s.skillArchetype,
            hitEffectPrefab = Ref(typeof(GameObject), s.hitEffectPrefab),
            effectAmount = s.effectAmount,
            explosionRadius = s.explosionRadius,
            effectDuration = s.effectDuration,
            multiHitCount = s.multiHitCount,
        };

        // ── 무기 ───────────────────────────────────────────────────────
        private static WeaponDto ReadWeapon(Transform visual)
        {
            Transform socket = visual.Find("WeaponSocket");
            if (socket == null) return null;
            SpriteRenderer sr = socket.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return null;        // 스프라이트 없는 빈 소켓은 "무기 없음"

            var weapon = new WeaponDto { sprite = Ref(typeof(Sprite), sr.sprite) };
            WeaponAttachment wa = socket.GetComponent<WeaponAttachment>();
            if (wa != null)
            {
                ReadAttachment(new SerializedObject(wa), out float native, out List<PoseDto> poses);
                weapon.nativeSpriteAngle = native;
                weapon.poses = WeaponDefaults.IsDefault(poses) ? null : poses;
            }
            return weapon;
        }

        internal static void ReadAttachment(SerializedObject so, out float nativeAngle, out List<PoseDto> poses)
        {
            nativeAngle = so.FindProperty("nativeSpriteAngle").floatValue;
            poses = new List<PoseDto>();
            SerializedProperty arr = so.FindProperty("poses");
            for (int i = 0; i < arr.arraySize; i++)
            {
                SerializedProperty el = arr.GetArrayElementAtIndex(i);
                Vector2 off = el.FindPropertyRelative("offset").vector2Value;
                poses.Add(new PoseDto
                {
                    direction = ((Dir)el.FindPropertyRelative("direction").intValue).ToString(),
                    offset = new[] { off.x, off.y },
                    targetAngle = el.FindPropertyRelative("targetAngle").floatValue,
                    sortingOrder = el.FindPropertyRelative("sortingOrder").intValue,
                });
            }
        }

        // ── 보스 골렘 손 ───────────────────────────────────────────────
        private static BossHandsDto ReadBossHands(GameObject root)
        {
            BossGolemHandController hc = root.GetComponent<BossGolemHandController>();
            if (hc == null) return null;

            var so = new SerializedObject(hc);
            return new BossHandsDto
            {
                left = ReadHand(so.FindProperty("leftHand").objectReferenceValue as Transform),
                right = ReadHand(so.FindProperty("rightHand").objectReferenceValue as Transform),
                slamTravelDuration = so.FindProperty("slamTravelDuration").floatValue,
                sweepTravelDuration = so.FindProperty("sweepTravelDuration").floatValue,
                clapTravelDuration = so.FindProperty("clapTravelDuration").floatValue,
                slamLiftHeight = so.FindProperty("slamLiftHeight").floatValue,
                clapApproachDistance = so.FindProperty("clapApproachDistance").floatValue,
            };
        }

        private static HandDto ReadHand(Transform t)
        {
            if (t == null) return null;
            SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
            return new HandDto
            {
                sprite = sr != null && sr.sprite != null ? Ref(typeof(Sprite), sr.sprite) : null,
                position = new[] { t.localPosition.x, t.localPosition.y },
                scale = new[] { t.localScale.x, t.localScale.y },
                sortingOrder = sr != null ? sr.sortingOrder : 11,
            };
        }

        // ── 스키마 드리프트 감지 ───────────────────────────────────────
        // 런타임 클래스에 필드가 새로 생겼는데 이 편집기가 모르면, 프리팹→JSON→프리팹을 거치며 조용히 사라진다.
        // 그런 필드를 찾아 알려 준다(편집기가 의도적으로 다루지 않는 것은 아래 목록에 이유와 함께 적는다).
        private static readonly HashSet<string> HandledDefinitionFields = new HashSet<string>
        {
            "unitTypeName", "footprint", "engageDistance", "populationCost", "visualScaleIgnoresFootprint", "stats", "skills",
            "hitSparkPrefab", "bloodEffectPrefab", "guardPrefab", "parryPrefab", "attackFailPrefab", "deathVfxPrefab", "corpseSprite",
            "isSpecialUnit", "isInterestTarget", "baseInterest", "baseDanger", "heavyHitThreshold", "stealth", "baseVisibility",
        };

        public static List<string> CheckSchemaParity()
        {
            var problems = new List<string>();
            var dtoSkillFields = new HashSet<string>(typeof(SkillDto).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name));
            foreach (FieldInfo f in typeof(SkillData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (!dtoSkillFields.Contains(f.Name))
                    problems.Add($"SkillData.{f.Name} 필드를 편집기가 모릅니다 — SkillDto/PrefabUnitReader/PrefabUnitWriter에 추가하지 않으면 사라집니다.");
            var runtimeSkillFields = new HashSet<string>(typeof(SkillData).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name));
            foreach (string name in dtoSkillFields)
                if (!runtimeSkillFields.Contains(name))
                    problems.Add($"SkillDto.{name} 필드가 SkillData에 없습니다.");

            foreach (FieldInfo f in typeof(UnitVisualDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (!HandledDefinitionFields.Contains(f.Name))
                    problems.Add($"UnitVisualDefinition.{f.Name} 필드를 편집기가 모릅니다 — 프리팹을 갱신해도 값은 보존되지만 JSON/창에서는 다룰 수 없습니다.");

            var statFields = new HashSet<string>(typeof(UnitStatsData).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name));
            if (statFields.Count != 20)
                problems.Add($"UnitStatsData 필드가 {statFields.Count}개입니다(편집기 작성 시점 20개) — 모든 필드는 자동으로 다뤄지지만 units.json에 새 키가 생깁니다.");
            return problems;
        }
    }

    // WeaponAttachment 컴포넌트의 기본값(방향별 포즈·기준각) — 코드에 적힌 초기값을 복제하지 않고 임시 컴포넌트에서 읽는다.
    public static class WeaponDefaults
    {
        private static List<PoseDto> _poses;
        private static float _nativeAngle;

        public static List<PoseDto> Poses { get { Ensure(); return _poses.Select(ClonePose).ToList(); } }
        public static float NativeAngle { get { Ensure(); return _nativeAngle; } }

        private static PoseDto ClonePose(PoseDto p) => new PoseDto
        {
            direction = p.direction,
            offset = (float[])p.offset.Clone(),
            targetAngle = p.targetAngle,
            sortingOrder = p.sortingOrder,
        };

        private static void Ensure()
        {
            if (_poses != null) return;
            var go = new GameObject("__WeaponDefaults", typeof(SpriteRenderer)) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                WeaponAttachment wa = go.AddComponent<WeaponAttachment>();
                PrefabUnitReader.ReadAttachment(new SerializedObject(wa), out _nativeAngle, out _poses);
            }
            finally { UObject.DestroyImmediate(go); }
        }

        public static bool IsDefault(List<PoseDto> poses)
        {
            Ensure();
            if (poses == null) return true;
            if (poses.Count != _poses.Count) return false;
            for (int i = 0; i < poses.Count; i++)
            {
                PoseDto a = poses[i], b = _poses[i];
                if (a.direction != b.direction || a.sortingOrder != b.sortingOrder) return false;
                if (Math.Abs(a.targetAngle - b.targetAngle) > 1e-3f) return false;
                if (Math.Abs(a.offset[0] - b.offset[0]) > 1e-3f || Math.Abs(a.offset[1] - b.offset[1]) > 1e-3f) return false;
            }
            return true;
        }
    }
}
