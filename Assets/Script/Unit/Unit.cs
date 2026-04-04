using UnityEngine;
using System.Collections.Generic;

public enum Dir
{
    UP,
    UP_RIGHT,
    RIGHT,
    DOWN_RIGHT,
    DOWN,
    DOWN_LEFT,
    LEFT,
    UP_LEFT
}

public enum UnitState
{
    EXPLORE,
    CAUTIOUS,
    SPOTTING,
    ENGAGE,
    WAIT,
    TEST_RANDOM_MOVE_6
}

public abstract class UnitType
{
    public string typeName;
    public float unitSize;
    public object details;
}

public class Warrior : UnitType { public Warrior() { typeName = "전사"; unitSize = 1.0f; } }
public class Scout : UnitType { public Scout() { typeName = "정찰병"; unitSize = 1.0f; } }
public class Archer : UnitType { public Archer() { typeName = "궁수"; unitSize = 1.0f; } }
public class Goblin : UnitType { public Goblin() { typeName = "고블린"; unitSize = 0.8f; } }
public class Orc : UnitType { public Orc() { typeName = "오크"; unitSize = 2f; } }

public class FactionData
{
    // 128x128 크기 맵 (0: 미탐색, 1: 바닥, 2: 벽) 타일맵 정보 공유
    public int[,] discoveredMap = new int[128, 128];
    // 시야 내 발견된 적 유닛 데이터 공유
    public List<Unit> spottedEnemyUnits = new List<Unit>();

    public void ClearSpottedUnits()
    {
        spottedEnemyUnits.Clear();
    }
}

public abstract class Unit : ScriptableObject
{
    public static FactionData humanFactionData = new FactionData();
    public static FactionData monsterFactionData = new FactionData();

    public UnitType unitType;
    public UnitState currentState = UnitState.TEST_RANDOM_MOVE_6;

    public float str;
    public float dex;
    public float con;
    public float @int;
    public float wis;
    public float cha;

    public Vector2Int position;
    public Dir currentDir = Dir.DOWN; // 현재 바라보는 방향 (시야 기준)


    #region 기능함수들
    public Vector2Int GetDirVector(Dir dir)
    {
        switch (dir)
        {
            case Dir.UP: return new Vector2Int(0, 1);
            case Dir.UP_RIGHT: return new Vector2Int(1, 1);
            case Dir.RIGHT: return new Vector2Int(1, 0);
            case Dir.DOWN_RIGHT: return new Vector2Int(1, -1);
            case Dir.DOWN: return new Vector2Int(0, -1);
            case Dir.DOWN_LEFT: return new Vector2Int(-1, -1);
            case Dir.LEFT: return new Vector2Int(-1, 0);
            case Dir.UP_LEFT: return new Vector2Int(-1, 1);
            default: return Vector2Int.zero;
        }
    }

    public bool CanMove(Vector2Int pos)//움직일 수 있는지 판단하는 함수
	{
        int cx = pos.x / 8;
        int tx = pos.x % 8;
        int cy = pos.y / 8;
        int ty = pos.y % 8;

        if (cx < 0 || cx >= 16 || cy < 0 || cy >= 16) return false;

        CreateMap cmap = FindObjectOfType<CreateMap>();
        if (cmap == null || cmap.map.session == null) return false;

        Chunks c = cmap.map.session[cx, cy];
        if (c.roomId == -1 || c.chunk == null) return false;

        if (c.chunk[tx, ty].name == "Wall") return false;

        return true;
    }

    public void Move(Dir dir)//움직이는 함수
    {
        currentDir = dir; // 이동 방향으로 시야 방향 갱신
        Vector2Int v = GetDirVector(dir);
        Vector2Int nextPos = position + v;

        if (CanMove(nextPos))
        {
            position = nextPos;
        }
    }

    public void UpdateFOV(List<Unit> allUnits)//시야 업데이트 함수
    {
        FactionData myData = this is Human ? humanFactionData : monsterFactionData;
        Vector2 forward = GetDirVector(currentDir);
        if (forward == Vector2.zero) forward = Vector2.down;

        CreateMap cmap = FindObjectOfType<CreateMap>();
        if (cmap == null || cmap.map.session == null) return;

        float viewRadius = 128f;
        float fovAngle = 160f;

        // 1. 공용 타일맵 데이터 갱신
        for (int dx = -128; dx <= 128; dx++)
        {
            for (int dy = -128; dy <= 128; dy++)
            {
                Vector2Int targetPos = position + new Vector2Int(dx, dy);
                if (targetPos.x < 0 || targetPos.x >= 128 || targetPos.y < 0 || targetPos.y >= 128) continue;

                float dist = Vector2.Distance(position, targetPos);
                if (dist > viewRadius) continue;

                Vector2 dirToTarget = ((Vector2)targetPos - (Vector2)position).normalized;
                float angle = Vector2.Angle(forward, dirToTarget);

                if (angle <= fovAngle / 2f || dist < 0.5f) // 부채꼴 시야 내 확인
                {
                    int cx = targetPos.x / 8;
                    int tx = targetPos.x % 8;
                    int cy = targetPos.y / 8;
                    int ty = targetPos.y % 8;

                    if (cx >= 0 && cx < 16 && cy >= 0 && cy < 16)
                    {
                        Chunks c = cmap.map.session[cx, cy];
                        if (c.roomId != -1 && c.chunk != null)
                        {
                            string tileName = c.chunk[tx, ty].name;
                            myData.discoveredMap[targetPos.x, targetPos.y] = tileName == "Wall" ? 2 : 1;
                        }
                    }
                }
            }
        }

        // 2. 다른 유닛 데이터 공용 데이터에 갱신
        foreach (var unit in allUnits)
        {
            if (unit == null || unit == this) continue;

            bool isEnemy = (this is Human && unit is Monster) || (this is Monster && unit is Human);
            if (isEnemy)
            {
                float dist = Vector2.Distance(position, unit.position);
                if (dist <= viewRadius)
                {
                    Vector2 dirToTarget = ((Vector2)unit.position - (Vector2)position).normalized;
                    float angle = Vector2.Angle(forward, dirToTarget);
                    if (angle <= fovAngle / 2f || dist < 0.5f)
                    {
                        if (!myData.spottedEnemyUnits.Contains(unit))
                        {
                            myData.spottedEnemyUnits.Add(unit); // 적 발견 갱신
                        }
                    }
                }
            }
        }
    }

    public abstract void JudgeState();//미구현.판단 함수

    public virtual void ExecuteAction()//행동 즉시 실행. 여기에 if 늘리면 enum 값에 따라 행동 늘어남
	{
        if (currentState == UnitState.TEST_RANDOM_MOVE_6)
        {
			TEST_RANDOM_MOVE_6_ExecuteRandomMove();
        }
    }
    #endregion
	#region 상태를 나타내는 함수들/////중요!! 여기에 함수들만 추가하고 enum에 넣으면 상태 늘어남
	protected void TEST_RANDOM_MOVE_6_ExecuteRandomMove()
	{
		Dir randomDir = (Dir)Random.Range(0, 8);
		Move(randomDir);
	}
	#endregion
}

public class Human : Unit
{
    public override void JudgeState()
    {
        // 인류 상태 판단 로직
    }
}

public class Monster : Unit
{
    public override void JudgeState()
    {
        // 몬스터 상태 판단 로직
    }
}

