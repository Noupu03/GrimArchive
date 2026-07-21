using UnityEngine;

public class HumanFactionBehavior : IFactionBehavior
{
    public bool IsEnemy(IFactionBehavior other)
    {
        return other is PlayerMonsterBehavior || other is WildMonsterBehavior;
    }

    public void OnUpdate(Unit unit) {}
    public void OnDeath(Unit unit, Unit killer) {}
    public void OnEnterRoom(Unit unit, Room room) {}
}
