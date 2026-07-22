using System.Collections.Generic;
using UnityEngine;

public interface IVisionContext
{
    GameSession Session { get; }
    
    PerceptionOutcome ResolveReachedTarget(object key, float targetVisibility, Vector3Int tile, float currentDist, out bool firstTouch);
    bool HasReachedPerceptionThisPass(object key);
    
    bool HasVisionOnlyNonEmptyTile(Vector3Int tile);
    void AddVisionOnlyNonEmptyTile(Vector3Int tile);
    
    void AddPersonalSpottedEnemy(Unit unit);
}
