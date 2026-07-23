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
    public float leadershipRange = 0f;
    public float charisma = 0f;
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


