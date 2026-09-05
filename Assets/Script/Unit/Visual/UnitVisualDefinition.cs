using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 유닛 타입별 프리팹의 루트에 붙는 컴포넌트.
// 스탯/스킬/이펙트를 인스펙터에서 직접 편집한다 (스프라이트/애니메이션은 프리팹 자식 계층의
// SpriteLibrary/SpriteResolver/UnitAnimationController 컴포넌트에서 직접 편집).
public class UnitVisualDefinition : MonoBehaviour
{
    [Header("유닛 식별")]
    public string unitTypeName;
    public Vector2 footprint = Vector2.one;
    public int engageDistance = 2;
    [Tooltip("유닛 배치 시스템(2026-07-27 신규) 5.2장: 방 인구수 점유량 - 기본 유닛 1")]
    public int populationCost = 1;
    [Tooltip("2026-08-24 신규(보스 골렘 대응) — footprint(예: [3,3])는 그대로 인구수·충돌·타일 점유 등 " +
             "게임플레이 판정에 계속 쓰이지만, 원본 스프라이트 아트 자체가 이미 그 배율로 그려져 있어서 " +
             "UnitGenerate.SetupUnitVisual이 루트 오브젝트에 footprint만큼 추가로 곱하는 시각적 확대는 " +
             "건너뛰어야 하는 유닛에 켠다. 켜지 않으면(기본값) 기존과 동일하게 footprint만큼 시각적으로도 " +
             "커진다 — 일반 유닛은 그대로 두면 된다.")]
    public bool visualScaleIgnoresFootprint = false;

    [Header("스탯")]
    public UnitStatsData stats = new UnitStatsData();

    [Header("스킬")]
    public List<SkillData> skills = new List<SkillData>();

    [Header("이펙트")]
    public GameObject hitSparkPrefab;
    [Tooltip("피격 추가 이펙트(2026-08-24 신규, 예: 피 튀김) — hitSparkPrefab을 대체하지 않고 같이 " +
             "재생된다(피격 시 매번 함께 스폰). 회피/막기 등 실제 피해가 없을 때(suppressHitVFX)는 " +
             "hitSparkPrefab과 마찬가지로 재생되지 않는다. 비워두면 추가 효과 없이 기존과 동일.")]
    public GameObject bloodEffectPrefab;
    public GameObject guardPrefab;
    public GameObject parryPrefab;
    public GameObject attackFailPrefab;
    [Tooltip("사망 연출(2026-08-24 신규) — 유닛이 죽는 즉시 1회 재생되는 VFX. 비워두면 사망 VFX 없이 넘어간다.")]
    public GameObject deathVfxPrefab;
    // deathSprite(사망 포즈로 잠깐 고정하던 중간 단계용 스프라이트) 필드는 2026-08-24 삭제 —
    // 사용자 요청으로 "죽는 즉시 시체 스프라이트로 전환"으로 바뀌면서 그 중간 단계 자체가 없어졌다
    // (UnitGenerate.PlayDeathVisual/GameSession.RemoveDeadUnit 참고). 13개 유닛 프리팹 전부 이 필드가
    // 미설정(null) 상태였어서 삭제로 잃는 데이터가 없었다.
    [Tooltip("사망 연출(2026-08-24 신규) — 사망 판정 즉시(deathVfxPrefab과 동시에) 바닥에 나타나는 " +
             "시체 오브젝트의 스프라이트. 캐릭터별로 지정 가능 — 비워두면 기존 공용 시체 스프라이트" +
             "(obj/colapse)로 폴백한다.")]
    public Sprite corpseSprite;

    [Header("가중치 시스템 (이해도/위험도/흥미도)")]
    [Tooltip("보스/네메시스 등 종별+개별 이해도를 함께 쓰는 특수 유닛인지 (연산공식 문서 7-1장)")]
    public bool isSpecialUnit = false;
    [Tooltip("IsInterestTarget 플래그 - 켜져있으면 이해도 상승에 따른 흥미도 감소식을 적용하지 않음 (18장)")]
    public bool isInterestTarget = false;
    [Tooltip("유닛 기본 흥미도 (6-3장)")]
    public float baseInterest = 0f;
    [Tooltip("대상 기본 위험도 (12-1장 최종 위험도 = 기본 + 종별누적 + 개별누적)")]
    public float baseDanger = 0f;
    [Tooltip("\"일정 피해량 이상\" 판정 기준값 - 이 값 미만의 피해는 위험도/이해도 이벤트를 발생시키지 않음 (3장)")]
    public float heavyHitThreshold = 10f;

    [Header("시야-인지-반응 시스템 (01_시야·인지범위·가시성)")]
    [Tooltip("은신 - 최종 가시성을 낮추는 세부 스탯 (01장 10절)")]
    public float stealth = 0f;
    [Tooltip("대상 기본 가시성 - 일반 유닛 100, 은신형/특수 유닛은 낮게 설정 (01장 9절)")]
    public float baseVisibility = 100f;

    public void ApplyStatsTo(Unit unit)
    {
        if (unit == null) return;

        unit.isSpecialUnit = isSpecialUnit;
        unit.isInterestTarget = isInterestTarget;
        unit.populationCost = populationCost;
        unit.BaseStat.baseInterest = baseInterest;
        unit.BaseStat.baseDanger = baseDanger;
        unit.BaseStat.heavyHitThreshold = heavyHitThreshold;
        unit.VisionStat.stealth = stealth;
        unit.VisionStat.baseVisibility = baseVisibility;

        unit.Health.maxHp = stats.maxHp; unit.Health.hp = stats.maxHp;
        unit.Health.maxMp = stats.maxMp; unit.Health.mp = stats.maxMp;
        var combatStats = unit.CombatStat; if(combatStats != null) { combatStats.physicalAttack = stats.physicalAttack;
        combatStats.magicalAttack = stats.magicalAttack;
        combatStats.physicalDefense = stats.physicalDefense;
        combatStats.magicalDefense = stats.magicalDefense;
        unit.BaseStat.HPRegen = stats.HPRegen;
        combatStats.attackspeed = stats.attackspeed;
        unit.BaseStat.walkSpeed = stats.walkSpeed;
        unit.BaseStat.reaction = stats.reaction;
        combatStats.criticalChance = stats.criticalChance; }
        unit.BaseStat.cooltimeReduction = stats.cooltimeReduction;
        unit.BaseStat.statusResistance = stats.statusResistance;
        unit.BaseStat.maxMental = stats.maxMental;
        unit.BaseStat.mental = stats.mental;
        unit.VisionStat.spotting = stats.spotting;
        unit.BaseStat.leadershipRange = stats.leadershipRange;
        unit.BaseStat.charisma = stats.charisma;
        unit.CombatState.State.physicalAttackSpeed = stats.physicalAttackSpeed;
        unit.CombatState.State.magicalCastSpeed    = stats.magicalCastSpeed;

        if (unit.unitType != null) unit.unitType.footprint = footprint;
    }

    public List<SkillAction> BuildSkillActions()
    {
        var list = new List<SkillAction>();
        foreach (var sd in skills)
        {
            switch (sd.skillArchetype)
            {
                case "GroundAoE": list.Add(new SkillAction_GroundAoE(sd)); break;
                case "Backstab":  list.Add(new SkillAction_Backstab(sd)); break;
                case "Fireball":  list.Add(new SkillAction_Fireball(sd)); break;
                case "Heal":      list.Add(new SkillAction_Heal(sd)); break;
                case "Shield":    list.Add(new SkillAction_Shield(sd)); break;
                case "Curse":     list.Add(new SkillAction_Curse(sd)); break;
                case "MultiHit":  list.Add(new SkillAction_MultiHit(sd)); break;
                case "PartyBuff": list.Add(new SkillAction_PartyBuff(sd)); break;
                case "GolemSlam":  list.Add(new SkillAction_GolemSlam(sd)); break;
                case "GolemSweep": list.Add(new SkillAction_GolemSweep(sd)); break;
                case "GolemClap":  list.Add(new SkillAction_GolemClap(sd)); break;
                default:
                    // 아키타입이 비어있는 건 정상(몬스터 기본 스킬 등)이지만, 값이 있는데 여기로 떨어졌다면
                    // 오타이거나 위 case 목록에 빠진 것이다 — 조용히 Generic으로 폴백하면 스킬 하나가
                    // 통째로 근접 평타처럼 동작하면서도 아무 흔적이 안 남는다(2026-08-23 파이어볼 사례).
                    if (!string.IsNullOrEmpty(sd.skillArchetype))
                        Debug.LogWarning($"[UnitVisualDefinition] '{unitTypeName}'의 스킬 '{sd.skillName}': " +
                                         $"알 수 없는 skillArchetype '{sd.skillArchetype}' — Generic으로 폴백합니다.");

                    if (sd.isProjectile)
                        list.Add(new SkillAction_Projectile(sd, sd.projectilePrefab));
                    else
                        list.Add(new SkillAction_Generic(sd));
                    break;
            }
        }
        return list;
    }

#if UNITY_EDITOR
    [System.Serializable]
    private class UnitsJsonWrapper { public List<UnitJsonNode> units; }
    [System.Serializable]
    private class UnitJsonNode {
        public string typeName;
        public int[] footprint;
        public int engageDistance;
        public List<string> skills;
        public UnitStatsData stats;
    }
    // projectilePrefab/hitEffectPrefab이 GameObject라 JsonUtility가 못 채우므로(JsonToUnitPrefabConverter와 동일 이유) 이름 문자열로 받아 FindAssetByName으로 해석한다.
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

    // JsonToUnitPrefabConverter.FindAssetByName와 동일한 로직 — 서브 에셋(스프라이트시트 내부 등)까지 이름으로 찾는다.
    private static T FindAssetByName<T>(string name) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(name)) return null;

        string typeFilter = typeof(T) == typeof(GameObject) ? "t:Prefab"
                           : typeof(T) == typeof(RuntimeAnimatorController) ? "t:AnimatorController"
                           : $"t:{typeof(T).Name}";

        foreach (var guid in UnityEditor.AssetDatabase.FindAssets($"{typeFilter} {name}"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);

            foreach (var obj in UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (obj is T typed && obj.name == name) return typed;

            var main = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (main != null && main.name == name) return main;
        }

        LogHelper.Warning(LogHelper.GAME, $"[UnitVisualDefinition] 에셋을 찾을 수 없습니다: '{name}' ({typeof(T).Name})");
        return null;
    }

    [System.Serializable]
    private class SkillsJsonWrapper { public List<JsonSkillData> skills; }

    [ContextMenu("Load Data From JSON (units.json / skills.json)")]
    public void LoadDataFromJson()
    {
        if (string.IsNullOrEmpty(unitTypeName))
        {
            LogHelper.Error(LogHelper.GAME, "Unit Type Name이 없습니다. (예: 아처형)");
            return;
        }

        string unitsPath = System.IO.Path.Combine(Application.dataPath, "Data", "units.json");
        string skillsPath = System.IO.Path.Combine(Application.dataPath, "Data", "skills.json");

        if (!System.IO.File.Exists(unitsPath) || !System.IO.File.Exists(skillsPath))
        {
            LogHelper.Error(LogHelper.GAME, "Data 폴더에 units.json 또는 skills.json이 없습니다.");
            return;
        }

        string unitsJson = System.IO.File.ReadAllText(unitsPath);
        string skillsJson = System.IO.File.ReadAllText(skillsPath);

        UnitsJsonWrapper unitsData = JsonUtility.FromJson<UnitsJsonWrapper>(unitsJson);
        SkillsJsonWrapper skillsData = JsonUtility.FromJson<SkillsJsonWrapper>(skillsJson);

        if (unitsData == null || unitsData.units == null) return;

        UnitJsonNode targetNode = null;
        foreach (var node in unitsData.units)
        {
            if (node.typeName == this.unitTypeName)
            {
                targetNode = node;
                break;
            }
        }

        if (targetNode == null)
        {
            LogHelper.Error(LogHelper.GAME, $"{unitTypeName} 데이터를 units.json에서 찾을 수 없습니다.");
            return;
        }

        // 스탯 및 기본 정보 덮어쓰기
        if (targetNode.footprint != null && targetNode.footprint.Length >= 2)
            this.footprint = new Vector2(targetNode.footprint[0], targetNode.footprint[1]);
        this.engageDistance = targetNode.engageDistance;
        this.stats = targetNode.stats;

        // 스킬 연결
        this.skills.Clear();
        if (targetNode.skills != null && skillsData != null && skillsData.skills != null)
        {
            foreach (var skillName in targetNode.skills)
            {
                foreach (var sd in skillsData.skills)
                {
                    if (sd.skillName == skillName)
                    {
                        this.skills.Add(sd.ToSkillData());
                        break;
                    }
                }
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
        LogHelper.Log(LogHelper.GAME, $"[{unitTypeName}] 데이터 JSON 불러오기 완료!");
    }
#endif
}
