using UnityEngine;

public class WildMonsterBehavior : IFactionBehavior
{
    public void OnUpdate(Unit unit)
    {
        // 야생 몬스터는 자신이 속한 방 내부에서만 활동 (방 밖 추격 안함)
    }

    public void OnDeath(Unit unit, Unit killer)
    {
        Debug.Log($"{unit.name} (Wild Monster) 사망. 오펜스 자원 B 누적.");
        if (ResourceAccumulator.Instance != null)
        {
            ResourceAccumulator.Instance.AccumulateResourceB(10); // 테스트용 수치
        }
    }

    public void OnEnterRoom(Unit unit, Room room)
    {
        // 야생 유닛은 자동 방어 유닛으로 배치됨
    }
}
