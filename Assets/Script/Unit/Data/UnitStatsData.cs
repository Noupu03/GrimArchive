using UnityEngine;

[System.Serializable]
public class UnitStatsData
{
    public float maxHp, maxMp;
    public float physicalAttack, magicalAttack;
    public float physicalDefense, magicalDefense;
    public float HPRegen, attackspeed, walkSpeed, reaction;
    public float criticalChance, cooltimeReduction, statusResistance;
    public float maxMental, mental, spotting, leadershipRange, charisma;
    public float physicalAttackSpeed, magicalCastSpeed;
}

[System.Serializable]
public class SkillData
{
    public string skillName;
    public float  baseDelayMs;
    public float  baseCooldown;
    public int    cooldownSlot;
    
    [Header("투사체 전용 설정")]
    public bool       isProjectile; // 투사체 스킬 여부
    public GameObject projectilePrefab; // 커스텀 투사체 프리팹
    public float      projectileSpeed;  // 투사체 비행 속도
    public bool       isPiercing;       // 관통 여부
    public GameObject hitEffectPrefab;  // 적중 시 생성할 이펙트 프리팹
    
    // canHit 사전 검사용 히트박스
    public string hitShape;
    public int    hitRange, hitWidth, hitDepth;
    // 실제 위협 타일 히트박스
    public int    threatRange, threatWidth, threatDepth;
    public float  damageMultiplier;
    public bool   hasStun;
    public float  stunDuration;
    public float  priorityBase;
    public float  priorityKillMultiplier;
    public float  priorityKillBonus;
    public float  priorityRangeThreshold;
    public float  priorityRangeBonus;

    [Header("확장 스킬 설정 (테스트용)")]
    public string skillArchetype; // "GroundAoE", "Backstab", "Heal", "Shield", "Curse", "MultiHit", "PartyBuff",
                                   // "GolemSlam", "GolemSweep", "GolemClap"(보스 골렘 전용) 등
    public int    multiHitCount;
    public float  effectDuration;
    public float  effectAmount;
    public float  explosionRadius;
}
