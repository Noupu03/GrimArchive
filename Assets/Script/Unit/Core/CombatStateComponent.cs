using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class CombatStateComponent : IUnitComponent
{
    private Unit _owner;
    
    public UnitCombatState State = new UnitCombatState { skillCooldowns = new float[4] };

    
    public CombatStateComponent() { }

    public CombatStateComponent(Unit owner)
    {
        _owner = owner;
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


