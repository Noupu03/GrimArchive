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
        // 사용자의 요청에 따라 동적 계산 대신 임의의 숫자를 하드코딩하여 보상(자원 B) 결정
        int rewardAmount = 10;

        Debug.Log($"{unit.name} (Wild Monster) 사망. 고정 보상(자원 B): {rewardAmount} 누적.");
        if (ResourceAccumulator.Instance != null)
        {
            ResourceAccumulator.Instance.AccumulateResourceB(rewardAmount);
        }
    }

    public void OnEnterRoom(Unit unit, Room room)
    {
        // 야생 유닛은 자동 방어 유닛으로 배치됨
    }
}
