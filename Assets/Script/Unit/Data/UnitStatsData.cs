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
    public bool   isProjectile; // 투사체 스킬 여부
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
}
