using UnityEngine;

public interface IFactionBehavior
{
    bool IsEnemy(IFactionBehavior other);
    void OnUpdate(Unit unit);
    void OnDeath(Unit unit, Unit killer);
}
