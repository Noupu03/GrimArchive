using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PartyComponent : IUnitComponent
{
    private Unit _owner;
    
    public Party party;

    
    public PartyComponent() { }

    public PartyComponent(Unit owner)
    {
        _owner = owner;
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


