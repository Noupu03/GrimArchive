using UnityEngine;

public class WildMonsterBehavior : IFactionBehavior
{
    public bool IsEnemy(IFactionBehavior other) { return other is HumanFactionBehavior || other is PlayerMonsterBehavior; }

    public void OnUpdate(Unit unit)
    {
        // 야생 몬스터는 자신이 속한 방 내부에서만 행동 (방 밖 추격 안함)
    }

    public void OnDeath(Unit unit, Unit killer)
    {
        // 점령 전환/처치 보상 MVP(2026-07-27, 사용자 요청) — "몬스터가 야생 몬스터를 죽이면 나무 획득".
        // 플레이어 몬스터가 죽였을 때만 보상(야생끼리 서로 죽이는 경우 등은 대상 아님).
        if (killer != null && killer.FactionBehavior is PlayerMonsterBehavior && ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource(ResourceType.Wood, ResourceManager.KillRewardWood);
            ResourceManager.ShowKillRewardText(killer, ResourceType.Wood, ResourceManager.KillRewardWood);
        }
    }

    public void OnEnterRoom(Unit unit, Room room)
    {
        // 야생 유닛은 자동 방어 유닛으로 배치됨
    }
}
