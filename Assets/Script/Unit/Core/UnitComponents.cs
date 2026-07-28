using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct UnitStatusEffects
{
    public float stunDuration;
    public float slowDuration;
    public float poisonDuration;
    public float burnDuration;
}

[Serializable]
public struct UnitCombatState
{
    public float physicalAttackSpeed;
    public float magicalCastSpeed;
    public float actionCooldown;
    public float[] skillCooldowns;
    public bool isHitThisTurn;
    public bool oneTimeReactUsed;
    public float currentReactionWindow;
    public float evadeCooldown;
    public bool isCastingAttack;
    public float castTimer;
    public bool suppressHitVFX;
    public float currentAttackAngle;
    public float currentSpeed;
    public float acceleration;
    public bool isWaitState;
}

[Serializable]
public struct UnitPerceptionState
{
    public float stealth;
    public float baseVisibility;
    public float attackVisibilityBoostTimer;
    
    public Dictionary<object, PerceptionRecord> perceptionRecords;
    public int alertRecordCount;
    public List<Vector3Int> visionOnlyNonEmptyTiles;
    public List<ThreatTileData> detectedThreats;
    public List<Unit> personalSpottedEnemies;
}

[Serializable]
public struct UnitAIWeightState
{
    // Future AI weight properties here
}
