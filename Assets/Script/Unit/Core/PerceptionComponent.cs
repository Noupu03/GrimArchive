using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PerceptionComponent : IUnitComponent
{
    private Unit _owner;
    
    public UnitPerceptionState State = new UnitPerceptionState { 
        perceptionRecords = new Dictionary<object, PerceptionRecord>(),
        visionOnlyNonEmptyTiles = new List<Vector3Int>(),
        detectedThreats = new List<ThreatTileData>(),
        personalSpottedEnemies = new List<Unit>()
    };

    public int AlertRecordCount { get => State.alertRecordCount; set => State.alertRecordCount = value; }
    public bool IsAlert => AlertRecordCount > 0;

    
    public PerceptionComponent() { }

    public PerceptionComponent(Unit owner)
    {
        _owner = owner;
    }

    public void NotifyPerceptionSuspiciousChanged(bool wasSuspicious, bool nowSuspicious)
    {
        if (wasSuspicious == nowSuspicious) return;
        AlertRecordCount += nowSuspicious ? 1 : -1;
        if (AlertRecordCount < 0) AlertRecordCount = 0;
    }

    public void RemovePerceptionRecord(object key)
    {
        if (State.perceptionRecords.TryGetValue(key, out var record))
        {
            NotifyPerceptionSuspiciousChanged(record.PendingSuspiciousInvestigation, false);
            State.perceptionRecords.Remove(key);
        }
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


