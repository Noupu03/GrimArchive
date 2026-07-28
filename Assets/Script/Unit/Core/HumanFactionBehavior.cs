using UnityEngine;

public class HumanFactionBehavior : IFactionBehavior
{
    public bool IsEnemy(IFactionBehavior other)
    {
        return other is PlayerMonsterBehavior || other is WildMonsterBehavior;
    }

    public void OnUpdate(Unit unit) {}

    // 점령 전환/처치 보상 MVP(2026-07-27, 사용자 요청) — "플레이어 몬스터가 인간을 죽이면 돌 획득".
    // 플레이어 몬스터가 죽였을 때만 보상.
    public void OnDeath(Unit unit, Unit killer)
    {
        if (killer != null && killer.FactionBehavior is PlayerMonsterBehavior && ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource(ResourceType.Stone, ResourceManager.KillRewardStone);
            ResourceManager.ShowKillRewardText(killer, ResourceType.Stone, ResourceManager.KillRewardStone);
        }
    }

    public void OnEnterRoom(Unit unit, Room room) {}
}
