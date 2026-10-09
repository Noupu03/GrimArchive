using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// 유닛 데이터 편집기(Tools(new)/유닛/유닛 데이터 편집기)의 JSON 스키마 DTO — units.json / skills.json의 한 줄 한 줄과
// 1:1로 대응한다. 순수 C#(UnityEngine 에셋 API 없음)이라 Unity 없이도 콘솔 하니스로 검증할 수 있다.
//
// 에셋 참조는 전부 "이름 문자열"로 저장한다. 이름이 겹치는 에셋만 "이름#GUID"로 적는다(UnitAssetRefs).
// 필드 선언 순서가 곧 JSON 키 순서다(원본 파일의 순서와 맞춰 둠 — 바꾸면 git diff가 커진다).
namespace UnitDataTool
{
    public enum RefKind { Prefab, Sprite, SpriteLibrary }

    // 이 string 필드는 에셋 이름(또는 "이름#GUID")이다 — 창이 ObjectField로 그린다.
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class AssetRefAttribute : Attribute
    {
        public readonly RefKind Kind;
        public AssetRefAttribute(RefKind kind) { Kind = kind; }
    }

    // 비어 있을 수 있는(null이면 JSON에 안 쓰는) 하위 블록 — 창이 [추가]/[제거] 버튼을 붙인다.
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OptionalBlockAttribute : Attribute { }

    public class UnitsFileDto { public List<UnitDto> units = new List<UnitDto>(); }
    public class SkillsFileDto { public List<SkillDto> skills = new List<SkillDto>(); }

    public class UnitDto
    {
        public string typeName;
        public string unitClass;
        public float[] footprint = { 1f, 1f };
        public int engageDistance;
        public int populationCost = 1;
        public bool visualScaleIgnoresFootprint;        // false면 JSON에 쓰지 않는다
        public List<string> skills = new List<string>();
        // 이 유닛에서만 전역 스킬 정의와 다른 필드만 담는다: { "스킬명": { "baseCooldown": 5, ... } }
        // 문자열 필드를 ""로 두면 "이 유닛은 비어 있음"(예: 피격 이펙트 없음)이라는 뜻이다.
        public Dictionary<string, JObject> skillOverrides = new Dictionary<string, JObject>();
        public UnitStatsData stats = new UnitStatsData();
        public WeightDto weight = new WeightDto();
        public VisualDto visual = new VisualDto();

        public bool ShouldSerializevisualScaleIgnoresFootprint() => visualScaleIgnoresFootprint;
        public bool ShouldSerializeskillOverrides() => skillOverrides != null && skillOverrides.Count > 0;
    }

    public class WeightDto
    {
        public bool isSpecialUnit;
        public bool isInterestTarget;
        public float baseInterest;
        public float baseDanger;
        public float heavyHitThreshold = 10f;
        public float stealth;
        public float baseVisibility = 100f;
    }

    public class VisualDto
    {
        [AssetRef(RefKind.SpriteLibrary)] public string spriteLibrary;
        // 폐기된 필드(애니메이션은 UnitAnimationDriver 슬롯으로 대체) — 옛 JSON을 읽을 때 오류가 나지 않게만 받아 두고 쓰지 않는다.
        public string animatorController;
        [OptionalBlock] public WeaponDto weapon;
        [OptionalBlock] public EffectsDto effects;
        [OptionalBlock] public DeathDto death;
        [OptionalBlock] public BossHandsDto bossHands;

        public bool ShouldSerializeanimatorController() => false;
    }

    public class WeaponDto
    {
        [AssetRef(RefKind.Sprite)] public string sprite;
        public float nativeSpriteAngle = 135f;
        // null이면 WeaponAttachment 컴포넌트의 기본 포즈. 기본값과 다를 때만 JSON에 쓴다.
        public List<PoseDto> poses;
    }

    public class PoseDto
    {
        public string direction = "UP";                 // Dir 이름 (UP / UP_LEFT / LEFT / DOWN_LEFT / DOWN)
        public float[] offset = { 0f, 0f };
        public float targetAngle;
        public int sortingOrder = 10;
    }

    public class EffectsDto
    {
        [AssetRef(RefKind.Prefab)] public string hitSpark;
        [AssetRef(RefKind.Prefab)] public string bloodDrip;
        [AssetRef(RefKind.Prefab)] public string guard;
        [AssetRef(RefKind.Prefab)] public string parry;
        [AssetRef(RefKind.Prefab)] public string attackFail;
    }

    public class DeathDto
    {
        [AssetRef(RefKind.Prefab)] public string vfx;
        [AssetRef(RefKind.Sprite)] public string corpseSprite;
    }

    public class BossHandsDto
    {
        [OptionalBlock] public HandDto left;
        [OptionalBlock] public HandDto right;
        public float slamTravelDuration = 0.35f;
        public float sweepTravelDuration = 0.6f;
        public float clapTravelDuration = 0.3f;
        public float slamLiftHeight = 1.5f;
        public float clapApproachDistance = 1.5f;
    }

    public class HandDto
    {
        [AssetRef(RefKind.Sprite)] public string sprite;
        public float[] position = { 0f, 0f };
        public float[] scale = { 1f, 1f };
        public int sortingOrder = 11;
    }

    // SkillData(런타임 클래스)와 필드 이름이 같다. 에셋 참조(projectilePrefab/hitEffectPrefab)만 이름 문자열이다.
    // 선택 필드(투사체·아키타입 등)는 기본값(0/false/빈 문자열)이면 JSON에 쓰지 않는다 — 원본 skills.json의 관례.
    public class SkillDto
    {
        public string skillName;
        public float baseDelayMs;
        public float baseCooldown;
        public int cooldownSlot;
        public string hitShape;
        public int hitRange;
        public int hitWidth;
        public int hitDepth;
        public int threatRange;
        public int threatWidth;
        public int threatDepth;
        public float damageMultiplier;
        public bool hasStun;
        public float stunDuration;
        public float priorityBase;
        public float priorityKillMultiplier;
        public float priorityKillBonus;
        public float priorityRangeThreshold;
        public float priorityRangeBonus;

        // ── 선택 필드 ──
        public bool isProjectile;
        [AssetRef(RefKind.Prefab)] public string projectilePrefab;
        public float projectileSpeed;
        public bool isPiercing;
        public string skillArchetype;
        [AssetRef(RefKind.Prefab)] public string hitEffectPrefab;
        public float effectAmount;
        public float explosionRadius;
        public float effectDuration;
        public int multiHitCount;
    }
}
