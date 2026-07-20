using UnityEngine;

public class PlayerUnitBehavior : IFactionBehavior
{
    public void OnUpdate(Unit unit)
    {
        // 플레이어 유닛의 이동 및 교전 업데이트
    }

    public void OnDeath(Unit unit, Unit killer)
    {
        Debug.Log($"{unit.name} (Player) 사망.");
    }

    public void OnEnterRoom(Unit unit, Room room)
    {
        // 야생 방 진입 시 오펜스 개입 시작
        if (room != null && room.RoomFaction == FactionType.Wild)
        {
            if (OffenseProcessor.Instance != null)
            {
                OffenseProcessor.Instance.StartOffense(room, unit);
            }
        }
    }
}
