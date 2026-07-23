using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class UnitRegistry
{
    public Dictionary<Vector3Int, Unit> unitGrid { get; private set; } = new Dictionary<Vector3Int, Unit>();

    private OffenseProcessor _offenseProcessor;
    private IObjectResolver _resolver;
    private GameSession _gameSession => _cachedGameSession ??= _resolver.Resolve<GameSession>();
    private GameSession _cachedGameSession;

    [Inject]
    public void Construct(OffenseProcessor offenseProcessor, IObjectResolver resolver)
    {
        _offenseProcessor = offenseProcessor;
        _resolver = resolver;
    }

    public void RegisterUnitPos(Unit u, Vector2Int pos)
    {
        if (u == null) return;
        
        int w = u.unitType != null ? (int)u.unitType.footprint.x : 1;
        int h = u.unitType != null ? (int)u.unitType.footprint.y : 1;
        
        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                unitGrid[new Vector3Int(pos.x + dx, pos.y + dy, u.currentFloor)] = u;
            }
        }
        
        if ((u.FactionBehavior is HumanFactionBehavior || u.FactionBehavior is PlayerMonsterBehavior) && _offenseProcessor != null && _offenseProcessor.currentOffenseRoom == null)
        {
            if (_gameSession != null && _gameSession.allRooms != null)
            {
                foreach (var room in _gameSession.allRooms)
                {
                    if (room.RoomFaction == FactionType.Wild && room.Bounds.Contains(pos))
                    {
                        Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, $"[오펜스 트리거] 플레이어가 야생 방({room.RoomName})에 물리적으로 진입했습니다.");
                        _offenseProcessor.StartOffense(room, u);
                        break;
                    }
                }
            }
        }
    }

    public void UnregisterUnitPos(Unit u, Vector2Int pos)
    {
        if (u == null) return;
        int w = u.unitType != null ? (int)u.unitType.footprint.x : 1;
        int h = u.unitType != null ? (int)u.unitType.footprint.y : 1;
        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                unitGrid.Remove(new Vector3Int(pos.x + dx, pos.y + dy, u.currentFloor));
            }
        }
    }
}
