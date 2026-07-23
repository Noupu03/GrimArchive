using UnityEngine;

public interface ITargetable
{
    Vector2Int TargetPosition { get; }
    int TargetFloor { get; }
    float TargetHp { get; set; }
    FactionData TargetFactionData { get; }
    void TakePhysicalDamage(float rawDamage, ITargetable attacker);
    void TakeMagicalDamage(float rawDamage, ITargetable attacker);
    void TakeMentalDamage(float rawDamage, ITargetable attacker);
    void ApplyDirectDamage(ITargetable attacker, float multiplier = 1f);
}
