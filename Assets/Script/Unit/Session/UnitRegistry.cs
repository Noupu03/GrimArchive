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

    // 2026-08-22 사용자 신고 "난전중 겹침... 어떤 상황에서도 유닛끼리는 겹쳐지면 안돼" — 예전엔
    // 무조건 덮어써서, 이미 다른 유닛이 등록된 칸에 또 다른 유닛을 등록하면(스폰/이동/텔레포트 등
    // 호출부가 사전에 점유 확인을 빠뜨리거나 놓치면) 그 칸의 등록이 뒤 호출로 조용히 덮어써지고
    // 원래 유닛은 unitGrid 상 "존재하지 않는" 상태가 되면서도 화면상 위치(position 필드)는 그대로
    // 남아 — 다른 유닛이 그 자리를 빈 칸으로 오판해 걸어 들어오는 식으로 겹침이 발생했다. 이제
    // 반환값(bool)으로 성공 여부를 알려준다 — 발자국(footprint) 칸 중 하나라도 이미 다른 살아있는
    // 유닛이 점유하고 있으면 어떤 칸도 등록하지 않고(부분 오염 방지) false를 반환한다. 호출부
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
