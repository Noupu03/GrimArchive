using UnityEngine;

[System.Serializable]
public class CombatStatComponent : IUnitComponent
{
    private Unit _owner;
    
    public float physicalAttack = 10f;
    public float magicalAttack = 0f;
    public float physicalDefense = 0f;
    public float magicalDefense = 0f;
    
    // 공격 관련 (원래 Unit에 있던것)
    public float criticalChance = 0f;
    public float attackspeed = 0f;

    
    public CombatStatComponent() { }

    public CombatStatComponent(Unit owner)
    {
        _owner = owner;
        // 임시로 Unit의 기존 필드값을 복사
        // removed
        // removed
        // removed
        // removed
        // removed
        // removed
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


