using UnityEngine;

public class PlayerMonsterBehavior : IFactionBehavior
{
    public bool IsEnemy(IFactionBehavior other)
    {
        return other is HumanFactionBehavior || other is WildMonsterBehavior;
    }

    public void OnUpdate(Unit unit) {}
    public void OnDeath(Unit unit, Unit killer) {}
    public void OnEnterRoom(Unit unit, Room room) {}
}
