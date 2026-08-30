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

    // 유닛끼리 절대 겹치지 않아야 한다 — 발자국(footprint) 칸 중 하나라도 이미 다른 살아있는 유닛이
    // 점유하고 있으면 어떤 칸도 등록하지 않고(부분 오염 방지) false를 반환한다. 호출부
    // (GameSession.ProcessUnitAction/Unit.ForceMove)는 실패 시 위치 변경 자체를 되돌린다.
    public bool RegisterUnitPos(Unit u, Vector2Int pos)
    {
        if (u == null) return false;

        int w = u.unitType != null ? (int)u.unitType.footprint.x : 1;
        int h = u.unitType != null ? (int)u.unitType.footprint.y : 1;

        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                Vector3Int key = new Vector3Int(pos.x + dx, pos.y + dy, u.currentFloor);
                if (unitGrid.TryGetValue(key, out Unit occupant) && occupant != null && occupant != u && occupant.hp > 0)
                    return false;
            }
        }

        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                unitGrid[new Vector3Int(pos.x + dx, pos.y + dy, u.currentFloor)] = u;
            }
        }

        if (_gameSession != null && _gameSession.roomGrid.TryGetValue(new Vector3Int(pos.x, pos.y, u.currentFloor), out Room currentRoom) && currentRoom != null)
        {
            if (u.FactionBehavior is PlayerMonsterBehavior && _offenseProcessor != null && currentRoom.RoomFaction == FactionType.Wild)
            {
                Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, $"[오펜스 트리거] 플레이어가 야생 방({currentRoom.RoomName})에 물리적으로 진입했습니다.");
                _offenseProcessor.TryStartOffense(currentRoom, u);
            }
            // 디펜스 자동 트리거(위 오펜스 트리거의 대칭) — 야생/인류 유닛이 플레이어 소유 방에
            // 물리적으로 등록(스폰 또는 이동)되면 즉시 디펜스 시작. RegisterUnitPos는 스폰도 포함,
            // roomGrid 기반(ProcessUnitAction)은 이동만 커버하므로 둘 다 이중 안전망으로 둔다.
            else if ((u.FactionBehavior is WildMonsterBehavior || u.FactionBehavior is HumanFactionBehavior) && _defenseProcessor != null && currentRoom.RoomFaction == FactionType.Player)
            {
                Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, $"[디펜스 트리거] 적대 유닛이 플레이어 방({currentRoom.RoomName})에 물리적으로 진입했습니다.");
                _defenseProcessor.TryStartDefense(currentRoom, u);
            }
        }

        return true;
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
