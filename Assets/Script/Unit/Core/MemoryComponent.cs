using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class MemoryComponent : IUnitComponent
{
    private Unit _owner;
    
    public PersonalMapKnowledge personalMap = new PersonalMapKnowledge();
    public List<string> collectedObjects = new List<string>();

    
    public MemoryComponent() { }

    public MemoryComponent(Unit owner)
    {
        _owner = owner;
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


