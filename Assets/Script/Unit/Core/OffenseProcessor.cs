using System.Collections.Generic;
using Haare.Util.Logger;
using VContainer;

public class OffenseProcessor
{
    private readonly IOffenseQuery _query;
    private readonly IMapColorizer _colorizer;

    [Inject]
    public OffenseProcessor(IOffenseQuery query, IMapColorizer colorizer)
    {
        _query = query;
        _colorizer = colorizer;
    }

    public Room currentOffenseRoom { get; private set; }

    public void StartOffense(Room room, Unit playerUnit)
    {
        if (room.RoomFaction != FactionType.Wild) return;
        
        currentOffenseRoom = room;
        LogHelper.Log($"[오펜스 트리거] 시작: {room.RoomName} 방에 진입 발생");
    }

    public void UpdateProcess()
    {
        if (currentOffenseRoom == null) return;
        if (_query == null) return;

        bool hasPlayer = false;
        bool hasWild = false;

        var roomUnits = _query.GetUnitsInRoom(currentOffenseRoom.Bounds);
        foreach(var u in roomUnits)
        {
            if (u.FactionBehavior is PlayerMonsterBehavior) hasPlayer = true;
            if (u.FactionBehavior is WildMonsterBehavior) hasWild = true;
        }

        // 3. 패배 조건 (플레이어 전멸)
        if (!hasPlayer)
        {
            FailOffense();
            return;
        }

        // 2. 승리 조건
        if (currentOffenseRoom.Type == RoomType.Normal)
        {
            if (!hasWild) OnOffenseSuccess(currentOffenseRoom);
        }
        else if (currentOffenseRoom.Type == RoomType.Spawner)
        {
            if (!currentOffenseRoom.HasActiveSpawner && !hasWild)
            {
                OnOffenseSuccess(currentOffenseRoom);
            }
        }
    }

    private void FailOffense()
    {
        LogHelper.Log($"[오펜스] 패배: {currentOffenseRoom.RoomName}에서 플레이어 유닛이 전멸했습니다.");
        
        if (ResourceAccumulator.Instance != null)
        {
            ResourceAccumulator.Instance.ClearResourceB();
        }

        currentOffenseRoom = null;
    }

    private void OnOffenseSuccess(Room room)
    {
        LogHelper.Log($"[오펜스] 승리: {room.RoomName}의 몬스터를 모두 토벌했습니다.");
        
        if (ResourceAccumulator.Instance != null)
        {
            ResourceAccumulator.Instance.CommitResourceB();
        }

        room.RoomFaction = FactionType.Player;
        if (_colorizer != null)
        {
            // new Color(0.4f, 0.6f, 1f, 1f)을 위해 임시로 Color 객체 생성 (UnityEngine 네임스페이스 제거했으므로 UnityEngine.Color 명시)
            _colorizer.ChangeRoomColor(room, new UnityEngine.Color(0.4f, 0.6f, 1f, 1f));
        }

        currentOffenseRoom = null;
    }
}

