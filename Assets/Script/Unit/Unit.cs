using UnityEngine;

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

public abstract class Unit : ScriptableObject
{
    public UnitType unitType;
    public UnitState currentState = UnitState.TEST_RANDOM_MOVE_6;

    public float str;
    public float dex;
    public float con;
    public float @int;
    public float wis;
    public float cha;

    public Vector2Int position;

    
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
        Vector2Int v = GetDirVector(dir);
        Vector2Int nextPos = position + v;

        if (CanMove(nextPos))
        {
            position = nextPos;
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

