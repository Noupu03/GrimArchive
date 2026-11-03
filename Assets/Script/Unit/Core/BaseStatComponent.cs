using UnityEngine;

[System.Serializable]
public class BaseStatComponent : IUnitComponent
{
    private Unit _owner;
    
    public float agility = 0f;
    public float sense = 0f;
    public float sterngth = 0f; // typo kept from original
    public float Durability = 0f;
    public float walkSpeed = 3f;
    public float reaction = 1f;
    public float statusResistance = 0f;
    public float leadershipRange = 0f; // 사용 안 함 — 통솔력은 leadership으로 단일화됐다(리더 명령 22-표1). 프리팹에 직렬화돼 있어 필드만 남긴다
    public float charisma = 0f;        // 사용 안 함(위와 같음)
    public float leadership = 0f;      // 기본 통솔력 0~100 — Assets/Data/leader_commands.json(LeaderCommandTable)에서 UnitVisualDefinition.ApplyStatsTo가 채운다. 현재 통솔력은 Unit.LeaderState.CurrentLeadership
    public float exp = 0f;
    public float baseDanger = 0f;
    public float baseInterest = 0f;
    public float heavyHitThreshold = 10f;
    public float HPRegen = 0f;
    public float cooltimeReduction = 0f;
    public float maxMental = 0f;
    public float mental = 0f;

    
    public BaseStatComponent() { }

    public BaseStatComponent(Unit owner)
    {
        _owner = owner;
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


