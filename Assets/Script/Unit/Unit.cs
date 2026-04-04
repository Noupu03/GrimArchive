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
    public static float ViewRadius = 30f; // 전역 시야 거리


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

    private void CastRay(FactionData myData, CreateMap cmap, Vector2Int startPos, float angleRad, float maxRadius, List<Unit> allUnits)//시야 레이캐스트
    {
        Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

        float rayPosX = startPos.x + 0.5f;
        float rayPosY = startPos.y + 0.5f;

        int x = startPos.x;
        int y = startPos.y;

        int stepX = dir.x > 0 ? 1 : (dir.x < 0 ? -1 : 0);
        int stepY = dir.y > 0 ? 1 : (dir.y < 0 ? -1 : 0);

        float tMaxX = dir.x != 0 ? Mathf.Abs(((dir.x > 0 ? x + 1 : x) - rayPosX) / dir.x) : float.PositiveInfinity;
        float tMaxY = dir.y != 0 ? Mathf.Abs(((dir.y > 0 ? y + 1 : y) - rayPosY) / dir.y) : float.PositiveInfinity;

        float tDeltaX = dir.x != 0 ? Mathf.Abs(1f / dir.x) : float.PositiveInfinity;
        float tDeltaY = dir.y != 0 ? Mathf.Abs(1f / dir.y) : float.PositiveInfinity;

        float dist = 0f;

        while (dist <= maxRadius)
        {
            if (x < 0 || x >= 128 || y < 0 || y >= 128) break;

            int cx = x / 8;
            int tx = x % 8;
            int cy = y / 8;
            int ty = y % 8;

            if (cx < 0 || cx >= 16 || cy < 0 || cy >= 16) break;

            Chunks c = cmap.map.session[cx, cy];
            if (c.roomId == -1 || c.chunk == null) break;

            Tile tile = c.chunk[tx, ty];
            myData.discoveredMap[x, y] = tile.name == "Wall" ? 2 : 1;

            // 유닛 발견
            foreach (var unit in allUnits)
            {
                if (unit == null || unit == this) continue;
                if (unit.position.x == x && unit.position.y == y)
                {
                    bool isEnemy = (this is Human && unit is Monster) || (this is Monster && unit is Human);
                    if (isEnemy && !myData.spottedEnemyUnits.Contains(unit))
                    {
                        myData.spottedEnemyUnits.Add(unit);
                    }
                }
            }

            // 가시성 체크 (본인 위치 제외)
            if (x != startPos.x || y != startPos.y)
            {
                int vis = tile.visibility;

                // visibility 데이터가 설정되지 않은 맵을 위한 예외처리
                if (vis == 0 && tile.name != "Wall") vis = 100;
                if (tile.name == "Wall") vis = 0;

                if (vis <= 0) break; // 시야 즉시 차단
                if (vis < 100)
                {
                    // visibility 확률에 따른 시야 통과 여부 검사
                    if (Random.Range(0, 100) >= vis)
                    {
                        break; // 시야 차단 막힘
                    }
                }
            }

            // 다음 타일 이동
            if (tMaxX < tMaxY)
            {
                dist = tMaxX;
                tMaxX += tDeltaX;
                x += stepX;
            }
            else
            {
                dist = tMaxY;
                tMaxY += tDeltaY;
                y += stepY;
            }
        }
    }

    public void UpdateFOV(List<Unit> allUnits)//시야 업데이트 함수
    {
        FactionData myData = this is Human ? humanFactionData : monsterFactionData;
        Vector2 forward = GetDirVector(currentDir);
        if (forward == Vector2.zero) forward = Vector2.down;

        CreateMap cmap = FindObjectOfType<CreateMap>();
        if (cmap == null || cmap.map.session == null) return;

        float fovAngle = 160f;

        float centerAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;

        // 방사형 레이캐스트(DDA 알고리즘) 적용, 가시성 확률(visibility) 반영
        int numRays = 800; // 충분히 촘촘한 레이 수 설정하여 누락 타일 방지

        for (int i = 0; i <= numRays; i++)
        {
            float angle = centerAngle - (fovAngle / 2f) + (fovAngle * i / numRays);
            float rad = angle * Mathf.Deg2Rad;
            CastRay(myData, cmap, position, rad, ViewRadius, allUnits);
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

