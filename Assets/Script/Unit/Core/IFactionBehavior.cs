using UnityEngine;

public interface IFactionBehavior
{
    void OnUpdate(Unit unit);
    void OnDeath(Unit unit, Unit killer);
    void OnEnterRoom(Unit unit, Room room);
}
