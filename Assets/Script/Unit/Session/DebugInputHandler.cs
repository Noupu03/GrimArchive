using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using Haare.Util.Logger;

public class DebugInputHandler
{
    private IObjectResolver _resolver;
    private GameSession _gameSession => _cachedGameSession ??= _resolver.Resolve<GameSession>();
    private GameSession _cachedGameSession;
    private UnitGenerate _unitGenerate;

    [Inject]
    public void Construct(IObjectResolver resolver, UnitGenerate unitGenerate)
    {
        _resolver = resolver;
        _unitGenerate = unitGenerate;
    }

    public void HandleDebugInput()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.hKey.wasPressedThisFrame) OnKeyDown_H();
            if (Keyboard.current.kKey.wasPressedThisFrame) OnKeyDown_K();
            if (Keyboard.current.oKey.wasPressedThisFrame) OnKeyDown_O();
        }
    }

    public void OnKeyDown_H()
    {
        if (_unitGenerate == null) return;

        UnitType[] types = { new Knight() };
        Vector2Int[] offsets = { new Vector2Int(0, 0) };

        int floorIdx = 1;
        UnitType type = types[0];

        Vector2Int spawnPos = GetRandomStartRoomPos(type.footprint, floorIdx);

        for (int i = 0; i < types.Length; i++)
        {
            Vector2Int pos = spawnPos + offsets[i];

            if (!_unitGenerate.IsAreaClear(pos, types[i].footprint, floorIdx))
                pos = _unitGenerate.GetRandomFloorPos(types[i].footprint, floorIdx);

            Human human = _unitGenerate.GenerateUnitAtPos<Human>(types[i], pos, floorIdx);
            _gameSession.units.Add(human);

            _gameSession.RegisterUnitPos(human, human.position);
        }
    }

    private Vector2Int GetRandomStartRoomPos(Vector2 footprint, int floorIdx)
    {
        CreateMap mapGenerator = _gameSession.cmap;

        if (mapGenerator == null || mapGenerator.map.floors == null || floorIdx < 0 || floorIdx >= mapGenerator.map.floors.Length)
            return Vector2Int.zero;

        Floor floor = mapGenerator.map.floors[floorIdx];
        if (floor.chunks == null) return Vector2Int.zero;

        List<Vector2Int> candidates = new List<Vector2Int>();

        int chunkW = floor.config.width;
        int chunkH = floor.config.height;

        for (int cx = 0; cx < chunkW; cx++)
        {
            for (int cy = 0; cy < chunkH; cy++)
            {
                Chunks c = floor.chunks[cx, cy];
                if (c.roomRole != RoomRole.StartRoom || c.chunk == null) continue;

                for (int tx = 0; tx < 8; tx++)
                {
                    for (int ty = 0; ty < 8; ty++)
                    {
                        Vector2Int pos = new Vector2Int(cx * 8 + tx, cy * 8 + ty);

                        if (_unitGenerate.IsAreaClear(pos, footprint, floorIdx))
                            candidates.Add(pos);
                    }
                }
            }
        }

        if (candidates.Count == 0)
            return Vector2Int.zero;

        return candidates[Random.Range(0, candidates.Count)];
    }

    public void OnKeyDown_K()
    {
        if (_unitGenerate == null) return;

        UnitType[] types = { new Archer() };
        Vector2Int[] offsets = { new Vector2Int(0, 0) };

        int floorIdx = 1;
        UnitType type = types[0];

        Vector2Int spawnPos = GetRandomStartRoomPos(type.footprint, floorIdx);

        for (int i = 0; i < types.Length; i++)
        {
            Vector2Int pos = spawnPos + offsets[i];

            if (!_unitGenerate.IsAreaClear(pos, types[i].footprint, floorIdx))
                pos = _unitGenerate.GetRandomFloorPos(types[i].footprint, floorIdx);

            Human human = _unitGenerate.GenerateUnitAtPos<Human>(types[i], pos, floorIdx);
            human.FactionBehavior = new HumanFactionBehavior();
            _gameSession.units.Add(human);

            _gameSession.RegisterUnitPos(human, human.position);
            LogHelper.Log(LogHelper.GAME, $"Generated Archer (Human Faction) at Floor {human.currentFloor}, {human.position}");
        }
    }

    public void OnKeyDown_O()
    {
        if (_gameSession.cmap == null || _gameSession.cmap.map.floors == null) return;
        
        int floorIdx = 1;
        Vector2Int spawnPos = _unitGenerate.GetRandomFloorPos(Vector2.one, floorIdx);
        if (spawnPos == Vector2Int.zero) return;

        string objId = "InteractableObj_" + System.Guid.NewGuid().ToString().Substring(0, 4);
        Vector3Int gridPos = new Vector3Int(spawnPos.x, spawnPos.y, floorIdx);
        
        if (!_gameSession.objectGrid.ContainsKey(gridPos))
        {
            InteractableObject obj = new InteractableObject(objId, gridPos, 120f, 0f, new List<string> { "Object/Passable/Loot" });
            _gameSession.SpawnObject(obj, Color.magenta);
        }
    }
}
