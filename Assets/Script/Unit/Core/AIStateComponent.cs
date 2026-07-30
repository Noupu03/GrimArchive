using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class AIStateComponent : IUnitComponent
{
    private Unit _owner;
    
    public UnitAIWeightState AIWeightState;
    public HashSet<Unit> reactedAttackers = new HashSet<Unit>();
    public HashSet<Unit> unitsReactingToMe = new HashSet<Unit>();
    public float currentReactionWindow = 0f;
    public ThreatTileData reactingThreat = null;
    public Unit reactingAttacker = null;
    public System.Action pendingAttack;
    public System.Action pendingCastUpdate;
    public System.Action pendingVFX;
    public ThreatTileData currentThreat;

    
    public AIStateComponent() { }

    public AIStateComponent(Unit owner)
    {
        _owner = owner;
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


