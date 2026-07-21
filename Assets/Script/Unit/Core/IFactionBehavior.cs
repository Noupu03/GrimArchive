using UnityEngine;

public interface IFactionBehavior
{
    bool IsEnemy(IFactionBehavior other);
    void OnUpdate(Unit unit);
    void OnDeath(Unit unit, Unit killer);
    void OnEnterRoom(Unit unit, Room room);
}
