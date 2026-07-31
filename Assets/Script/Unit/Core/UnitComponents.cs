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
    // 07문서 17장(2026-07-31 신규): 이번 공격의 형태 — 피격 대상이 공격자를 정확 인지하지 못했을 때
    // 방향 정보를 얻을 수 있는지 판정하는 데 쓰인다(SkillAction.BeginAttackCast가 세팅).
    public AttackShape lastAttackShape;
}

[Serializable]
public struct UnitPerceptionState
{
    public float stealth;
    public float baseVisibility;
    public float attackVisibilityBoostTimer;
    
    public Dictionary<object, PerceptionRecord> perceptionRecords;
    public int alertRecordCount;
    public HashSet<Vector3Int> visionOnlyNonEmptyTiles;
    public List<ThreatTileData> detectedThreats;
    public HashSet<Unit> personalSpottedEnemies;
}

[Serializable]
public struct UnitAIWeightState
{
    // Future AI weight properties here
}
