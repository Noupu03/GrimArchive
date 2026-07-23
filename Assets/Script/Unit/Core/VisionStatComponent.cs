using UnityEngine;

[System.Serializable]
public class VisionStatComponent : IUnitComponent
{
    private Unit _owner;
    
    public float stealth = 0f;
    public float spotting = 0f;
    public float baseVisibility = 100f;
    public float attackVisibilityBoostTimer = 0f;

    
    public VisionStatComponent() { }

    public VisionStatComponent(Unit owner)
    {
        _owner = owner;
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


