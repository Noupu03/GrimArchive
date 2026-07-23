using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class StatusEffectsComponent : IUnitComponent
{
    private Unit _owner;
    
    public UnitStatusEffects State;

    
    public StatusEffectsComponent() { }

    public StatusEffectsComponent(Unit owner)
    {
        _owner = owner;
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


