using System.Collections.Generic;
using UnityEngine;

public class OffenseProcessor
{
    private static OffenseProcessor _instance;
    public static OffenseProcessor Instance
    {
        get
        {
            if (_instance == null) _instance = new OffenseProcessor();
            return _instance;
        }
    }

    public Room currentOffenseRoom { get; private set; }

    public void StartOffense(Room room, Unit playerUnit)
    {
        if (room.RoomFaction != FactionType.Wild) return;
        
        currentOffenseRoom = room;
        Debug.Log($"[오펜스 트리거] 시작: {room.RoomName} 방에 진입 발생");
    }

    public void UpdateProcess()
    {
        if (currentOffenseRoom == null) return;
        if (GameSession.Instance == null) return;

        bool hasPlayer = false;
        bool hasWild = false;

        foreach(var u in GameSession.Instance.units)
        {
            if (currentOffenseRoom.Bounds.Contains(u.position))
            {
                if (u.FactionBehavior is PlayerMonsterBehavior) hasPlayer = true;
                if (u.FactionBehavior is WildMonsterBehavior) hasWild = true;
            }
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
        Debug.Log($"[오펜스] 패배: {currentOffenseRoom.RoomName}에서 플레이어 유닛이 전멸했습니다.");
        
        if (ResourceAccumulator.Instance != null)
        {
            ResourceAccumulator.Instance.ClearResourceB();
        }

        currentOffenseRoom = null;
    }

    private void OnOffenseSuccess(Room room)
    {
        Debug.Log($"[오펜스] 승리: {room.RoomName}의 몬스터를 모두 토벌했습니다.");
        
        if (ResourceAccumulator.Instance != null)
        {
            ResourceAccumulator.Instance.CommitResourceB();
        }

        room.RoomFaction = FactionType.Player;
        if (GameSession.Instance != null && GameSession.Instance.mapRandering != null)
        {
            GameSession.Instance.mapRandering.ChangeRoomColor(room, new Color(0.4f, 0.6f, 1f, 1f));
        }

        currentOffenseRoom = null;
    }
}

