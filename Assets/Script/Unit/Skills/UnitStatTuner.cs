using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 인스펙터에서 유닛 타입별 세부 능력치를 조정하는 컴포넌트.
/// 빈 GameObject에 붙이고 에디터의 "유닛 타입 자동 감지" 버튼으로 목록을 채운다.
/// </summary>
public class UnitStatTuner : MonoBehaviour
{
    [System.Serializable]
    public class UnitStatEntry
    {
        public string unitTypeName;

        [Header("기본 전투")]
        public float maxHp           = 100f;
        public float maxMp           = 0f;
        public float physicalAttack  = 10f;
        public float magicalAttack   = 0f;
        public float physicalDefense = 0f;
        public float magicalDefense  = 0f;

        [Header("전투 스탯")]
        public float HPRegen           = 0f;
        public float attackspeed       = 100f;
        public float walkSpeed         = 3f;
        public float reaction          = 100f;
        public float criticalChance    = 0f;
        public float cooltimeReduction = 0f;
        public float statusResistance  = 0f;

        [Header("정신 / 감지")]
        public float maxMental       = 0f;
        public float mental          = 0f;
        public float spotting        = 0f;
        public float leadershipRange = 0f;
        public float charisma        = 0f;

        [Header("공격 속도")]
        public float physicalAttackSpeed = 10f;
        public float magicalCastSpeed    = 0f;
    }

    public List<UnitStatEntry> entries = new();

    void Awake() => Apply();

    public void Apply()
    {
        UnitStatOverride.Clear();
        foreach (var e in entries)
            UnitStatOverride.Register(e.unitTypeName, ToStatEntry(e));

        // 런타임 즉시 적용: 이미 생성된 유닛에도 반영
        if (Application.isPlaying && GameSession.Instance != null)
        {
            foreach (var unit in GameSession.Instance.units)
            {
                if (unit != null)
                    UnitStatOverride.ApplyRuntime(unit);
            }
        }
    }

    static UnitStatOverride.StatEntry ToStatEntry(UnitStatEntry e) => new UnitStatOverride.StatEntry
    {
        maxHp              = e.maxHp,
        maxMp              = e.maxMp,
        physicalAttack     = e.physicalAttack,
        magicalAttack      = e.magicalAttack,
        physicalDefense    = e.physicalDefense,
        magicalDefense     = e.magicalDefense,
        HPRegen            = e.HPRegen,
        attackspeed        = e.attackspeed,
        walkSpeed          = e.walkSpeed,
        reaction           = e.reaction,
        criticalChance     = e.criticalChance,
        cooltimeReduction  = e.cooltimeReduction,
        statusResistance   = e.statusResistance,
        maxMental          = e.maxMental,
        mental             = e.mental,
        spotting           = e.spotting,
        leadershipRange    = e.leadershipRange,
        charisma           = e.charisma,
        physicalAttackSpeed = e.physicalAttackSpeed,
        magicalCastSpeed   = e.magicalCastSpeed,
    };
}
