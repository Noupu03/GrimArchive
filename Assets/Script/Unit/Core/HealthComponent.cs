using UnityEngine;

[System.Serializable]
public class HealthComponent : IUnitComponent
{
    private Unit _owner;
    
    public float maxHp = 100f;
    public float hp = 100f;
    public float maxMp = 0f;
    public float mp = 0f;

    
    public HealthComponent() { }

    public HealthComponent(Unit owner)
    {
        _owner = owner;
        // Temporary fallback to owner values if they existed before this component was loaded from save?
        // Let's just set defaults. They are initialized by UnitVisualDefinition.
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


