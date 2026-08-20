using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class UnitRegistry
{
    public Dictionary<Vector3Int, Unit> unitGrid { get; private set; } = new Dictionary<Vector3Int, Unit>();

    private OffenseProcessor _offenseProcessor;
    private DefenseProcessor _defenseProcessor;
    private IObjectResolver _resolver;
    private GameSession _gameSession => _cachedGameSession ??= _resolver.Resolve<GameSession>();
    private GameSession _cachedGameSession;

    [Inject]
    public void Construct(OffenseProcessor offenseProcessor, DefenseProcessor defenseProcessor, IObjectResolver resolver)
    {
        _offenseProcessor = offenseProcessor;
        _defenseProcessor = defenseProcessor;
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
        
        if (u.FactionBehavior is PlayerMonsterBehavior && _offenseProcessor != null)
        {
            if (_gameSession != null && _gameSession.allRooms != null)
            {
                foreach (var room in _gameSession.allRooms)
                {
                    if (room.RoomFaction == FactionType.Wild && room.Bounds.Contains(pos))
                    {
                        Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, $"[오펜스 트리거] 플레이어가 야생 방({room.RoomName})에 물리적으로 진입했습니다.");
                        _offenseProcessor.TryStartOffense(room, u);
                        break;
                    }
                }
            }
        }

        // 디펜스 자동 트리거(2026-08-20, 위 오펜스 트리거의 대칭) — 야생/인류 유닛이 플레이어 소유
        // 방에 물리적으로 등록(스폰 또는 이동)되면 즉시 디펜스 시작. GameSession.ProcessUnitAction의
        // TryTriggerDefenseForUnit(roomGrid 기반, 이동 시점)과 함께 스폰 시점까지 커버하는 이중 안전망 —
        // 오펜스 쪽과 동일한 이유(RegisterUnitPos는 스폰도 포함, roomGrid 기반은 이동만 커버)로 둘 다 둔다.
        if ((u.FactionBehavior is WildMonsterBehavior || u.FactionBehavior is HumanFactionBehavior) && _defenseProcessor != null)
        {
            if (_gameSession != null && _gameSession.allRooms != null)
            {
                foreach (var room in _gameSession.allRooms)
                {
                    if (room.RoomFaction == FactionType.Player && room.Bounds.Contains(pos))
                    {
                        Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, $"[디펜스 트리거] 적대 유닛이 플레이어 방({room.RoomName})에 물리적으로 진입했습니다.");
                        _defenseProcessor.TryStartDefense(room, u);
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
