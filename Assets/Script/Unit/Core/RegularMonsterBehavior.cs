using UnityEngine;

public class RegularMonsterBehavior : IFactionBehavior
{
    public void OnUpdate(Unit unit)
    {
        // 일반 몬스터의 행동 패턴 (플레이어 추적, 디펜스 목적)
    }

    public void OnDeath(Unit unit, Unit killer)
    {
        Debug.Log($"{unit.name} (Regular Monster) 사망. 자원 A 지급 (디펜스).");
        // 자원 A 즉시 지급 연동 예정
    }

    public void OnEnterRoom(Unit unit, Room room)
    {
        // 일반 몬스터의 방 진입 처리
    }
}
