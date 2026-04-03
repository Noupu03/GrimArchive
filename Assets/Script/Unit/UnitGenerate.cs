using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class UnitGenerate : MonoBehaviour
{
    public static UnitGenerate Instance;

    void Awake()
    {
        Instance = this;
    }

    public T GenerateUnitAtRandomFloor<T>(UnitType unitType) where T : Unit
    {
        Vector2Int pos = GetRandomFloorPos();
        GameObject go = new GameObject($"{unitType.typeName}_{pos.x}_{pos.y}");

        // For visibility
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        // Assign a default sprite if possible, user will replace it later.

        T unit = go.AddComponent<T>();
        unit.unitType = unitType;
        unit.currentState = UnitState.TEST_RANDOM_MOVE_6;
        unit.position = pos;
        go.transform.position = new Vector3(pos.x, pos.y, 0);
        return unit;
    }

    public Vector2Int GetRandomFloorPos()
    {
        CreateMap cmap = FindObjectOfType<CreateMap>();
        if (cmap == null || cmap.map.session == null) return Vector2Int.zero;

        // Try getting a random floor position
        for (int i = 0; i < 1000; i++)
        {
            int cx = Random.Range(0, 16);
            int cy = Random.Range(0, 16);
            Chunks c = cmap.map.session[cx, cy];
            if (c.roomId != -1 && c.chunk != null)
            {
                int tx = Random.Range(0, 8);
                int ty = Random.Range(0, 8);
                if (c.chunk[tx, ty].name != "Wall")
                {
                    return new Vector2Int(cx * 8 + tx, cy * 8 + ty);
                }
            }
        }
        return Vector2Int.zero; // default fallback
    }
}

public class GameSession : MonoBehaviour
{
    public List<Unit> units = new List<Unit>();
    private float updateTimer = 0f;

    void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.hKey.wasPressedThisFrame)
            {
                OnKeyDown_H();
            }

            if (Keyboard.current.mKey.wasPressedThisFrame)
            {
                OnKeyDown_M();
            }
        }

        updateTimer += Time.deltaTime;
        if (updateTimer >= 2f)
        {
            updateTimer -= 2f;
            ProcessTurn();
        }
    }

    public void OnKeyDown_H()
    {
        if (UnitGenerate.Instance == null) return;

        UnitType[] types = { new Warrior(), new Scout(), new Archer() };
        UnitType selection = types[Random.Range(0, types.Length)];

        Human human = UnitGenerate.Instance.GenerateUnitAtRandomFloor<Human>(selection);
        units.Add(human);
        Debug.Log($"Generated Human: {selection.typeName} at {human.position}");
    }

    public void OnKeyDown_M()
    {
        if (UnitGenerate.Instance == null) return;

        UnitType[] types = { new Goblin(), new Orc() };
        UnitType selection = types[Random.Range(0, types.Length)];

        Monster monster = UnitGenerate.Instance.GenerateUnitAtRandomFloor<Monster>(selection);
        units.Add(monster);
        Debug.Log($"Generated Monster: {selection.typeName} at {monster.position}");
    }

    public void ProcessTurn()
    {
        foreach (var u in units)
        {
            if (u != null)
            {
                u.ExecuteAction();
            }
        }
    }
}
