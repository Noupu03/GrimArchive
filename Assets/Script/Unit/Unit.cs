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
public class Wolf : UnitType { public Wolf() { typeName = "늑대"; unitSize = 1.2f; } }

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

	// 전투 관련 속성
    public float hp = 100f;
    public float attackPower = 10f;
    public float reaction = 1f; // 반응도
    public float actionCooldown = 0f; // 턴 진행용 대기 시간
    public bool isHitThisTurn = false; // 피격 여부
    public bool oneTimeReactUsed = false; // 피격 리액션 등 1회성 억제용

    public Vector2Int position;
    public Dir currentDir = Dir.DOWN; // 현재 바라보는 방향 (시야 기준)
    public static float ViewRadius = 30f; // 전역 시야 거리

    public void SetupStats()
    {
        if (unitType is Archer)
        {
            hp = 80f; attackPower = 20f; reaction = 1.0f;
        }
        else if (unitType is Wolf)
        {
            hp = 70f; attackPower = 35f; reaction = 0.4f;
        }
        // 기타 유닛 스탯 생략 (기본값)
    }

    public void TakeDamage(float damage)
    {
        hp -= damage;
        isHitThisTurn = true;
    }


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
		int cyVal = pos.y % 8;

		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null) ? GameSession.Instance.cmap : FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null) return false;

		Floor floor = cmap.GetCurrentFloor();
		if (floor.chunks == null) return false;
		if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height) return false;

		Chunks c = floor.chunks[cx, cy];
		if (c.roomId == -1 || c.chunk == null) return false;

		if (c.chunk[tx, cyVal].name == "Wall") return false;

		// 다른 유닛 점유 여부 확인 (최적화: O(1) 캐싱 배열)
		if (GameSession.Instance != null && GameSession.Instance.unitGrid.TryGetValue(pos, out Unit u))
		{
			if (u != null && u != this && u.hp > 0)
			{
				return false;
			}
		}

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

		Floor floor = cmap.GetCurrentFloor();
		if (floor.chunks == null) return;
		int worldMaxX = floor.config.width * 8;
		int worldMaxY = floor.config.height * 8;

		while (dist <= maxRadius)
		{
			if (x < 0 || x >= worldMaxX || y < 0 || y >= worldMaxY) break;

			int cx = x / 8;
			int tx = x % 8;
			int cy = y / 8;
			int ty = y % 8;

			if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height) break;

			Chunks c = floor.chunks[cx, cy];
            if (c.roomId == -1 || c.chunk == null) break;

            Tile tile = c.chunk[tx, ty];
            myData.discoveredMap[x, y] = tile.name == "Wall" ? 2 : 1;

            // 유닛 발견 (O(1) 캐싱 검색 적용)
            if (GameSession.Instance != null && GameSession.Instance.unitGrid.TryGetValue(new Vector2Int(x, y), out Unit unit))
            {
                if (unit != null && unit != this && unit.hp > 0)
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

		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null) ? GameSession.Instance.cmap : FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null) return;

		float fovAngle = 160f;

		float centerAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;

		// 방사형 레이캐스트 최적화 적용 (800 -> 72)
		int numRays = 72; // 최적화: 시야각 누락되지 않는 선에서 최대한 감소

		for (int i = 0; i <= numRays; i++)
        {
            float angle = centerAngle - (fovAngle / 2f) + (fovAngle * i / numRays);
            float rad = angle * Mathf.Deg2Rad;
            CastRay(myData, cmap, position, rad, ViewRadius, allUnits);
        }
    }
	#endregion

	#region GOAP
	// ==========================================
	// GOAP Architecture
	// ==========================================
	public class GoapState : Dictionary<string, bool> { }

	public abstract class GoapGoal
	{
		public string Name;
		public GoapState DesiredState = new GoapState();
		public abstract float GetPriority(Unit unit);
	}

	public abstract class GoapAction
	{
		public string ActionName;
		public float Cost = 1f;
		public GoapState Preconditions = new GoapState();
		public GoapState Effects = new GoapState();

		public void AddPrecondition(string key, bool value) => Preconditions[key] = value;
		public void AddEffect(string key, bool value) => Effects[key] = value;

		public abstract bool IsValid(Unit unit);
		public abstract void Execute(Unit unit);
	}

	// Goals
	public class Goal_DefeatEnemy : GoapGoal
	{
		public Goal_DefeatEnemy() { Name = "DefeatEnemy"; DesiredState["enemyAlive"] = false; }
		public override float GetPriority(Unit unit)
		{
			FactionData myData = unit is Human ? humanFactionData : monsterFactionData;
			foreach (var enemy in myData.spottedEnemyUnits)
			{
				if (enemy != null && enemy.hp > 0) return 80f; // 적이 보이면 가중치 80 부여. 우선운위 높음
			}
			return 0f;
		}
	}

	public class Goal_Explore : GoapGoal
	{
		public Goal_Explore() { Name = "Explore"; DesiredState["explored"] = true; }
		public override float GetPriority(Unit unit) => 10f; // 기본 목표 우선순위 가중치 10임 낮음. 아직 미구현
	}

	// Actions
	public class Action_RandomExplore : GoapAction
	{
		public Action_RandomExplore()
		{
			ActionName = "RandomExplore";
			AddEffect("explored", true);
		}
		public override bool IsValid(Unit unit) => true;
		public override void Execute(Unit unit)
		{
			unit.TEST_RANDOM_MOVE_6_ExecuteRandomMove();
		}
	}

	public class Action_EngageEnemy : GoapAction
	{
		public Action_EngageEnemy()
		{
			ActionName = "EngageEnemy";
			AddPrecondition("enemyVisible", true);
			AddEffect("enemyAlive", false);
		}
		public override bool IsValid(Unit unit) => true;
		public override void Execute(Unit unit)
		{
			unit.ENGAGE_Execute();
		}
	}

	protected List<GoapGoal> availableGoals;
	protected List<GoapAction> availableActions;
	protected GoapAction currentPlannedAction;

	public virtual void JudgeState()// GOAP 목표 갱신 및 플래닝
	{
		if (availableGoals == null)
			availableGoals = new List<GoapGoal> { new Goal_DefeatEnemy(), new Goal_Explore() };
		if (availableActions == null)
			availableActions = new List<GoapAction> { new Action_RandomExplore(), new Action_EngageEnemy() };

		// 1. 최고 우선순위 목표 선정
		GoapGoal bestGoal = null;
		float highestPriority = -1f;

		foreach (var goal in availableGoals)
		{
			float priority = goal.GetPriority(this);
			if (priority > highestPriority)
			{
				highestPriority = priority;
				bestGoal = goal;
			}
		}

		// 2. 현재 월드 상태(WorldState) 수집
		GoapState worldState = new GoapState();
		FactionData myData = this is Human ? humanFactionData : monsterFactionData;
		bool enemyVisible = myData.spottedEnemyUnits.Exists(e => e != null && e.hp > 0);
		worldState["enemyVisible"] = enemyVisible;
		worldState["isHit"] = isHitThisTurn;

		// 3. 플래닝 (가장 단순한 1-step 매칭)
		currentPlannedAction = null;
		float lowestCost = float.MaxValue;

		if (bestGoal != null)
		{
			foreach (var action in availableActions)
			{
				if (!action.IsValid(this)) continue;

				// Effect가 Goal의 DesiredState를 만족시키는지 확인
				bool fulfillsGoal = false;
				foreach (var eff in action.Effects)
				{
					if (bestGoal.DesiredState.ContainsKey(eff.Key) && bestGoal.DesiredState[eff.Key] == eff.Value)
					{
						fulfillsGoal = true;
						break;
					}
				}

				if (fulfillsGoal && action.Cost < lowestCost)
				{
					// Precondition 체크
					bool meetsPreconditions = true;
					foreach (var pre in action.Preconditions)
					{
						if (!worldState.ContainsKey(pre.Key) || worldState[pre.Key] != pre.Value)
						{
							meetsPreconditions = false;
							break;
						}
					}

					if (meetsPreconditions)
					{
						currentPlannedAction = action;
						lowestCost = action.Cost;
					}
				}
			}
		}

		if (!enemyVisible) oneTimeReactUsed = false;
	}

	public virtual void ExecuteAction()//행동 실행
	{
		if (currentPlannedAction != null)
		{
			currentPlannedAction.Execute(this);
		}
		else
		{
			// 계획 실패 시 기본 행동
			TEST_RANDOM_MOVE_6_ExecuteRandomMove();
		}

		isHitThisTurn = false; // 턴 시작/종료시 피격 플래그 리셋
	}
	#endregion

	#region 가장 큰 단위 상태를 나타내는 함수들
	public void TEST_RANDOM_MOVE_6_ExecuteRandomMove()
	{
		Dir randomDir = (Dir)Random.Range(0, 8);
		Move(randomDir);
	}

	public void ENGAGE_Execute()
	{
		if (unitType is Archer)
		{
			ENGAGE_Archer();
		}
		else if (unitType is Wolf)
		{
			ENGAGE_Wolf();
		}
		else
		{
			ENGAGE_Default();
		}
	}

	#endregion

	#region 공통 행동들

	protected Unit GetClosestEnemy(out float minDist)//가장 가까운 적 유닛 획득
	{
		FactionData myData = this is Human ? humanFactionData : monsterFactionData;
		Unit target = null;
		minDist = float.MaxValue;

		foreach (var enemy in myData.spottedEnemyUnits)
		{
			if (enemy == null || enemy.hp <= 0) continue;
			float d = Vector2Int.Distance(position, enemy.position);
			if (d < minDist)
			{
				minDist = d;
				target = enemy;
			}
		}
		return target;
	}

	protected void MoveTowardsTarget(Unit target)//타켓을 향해 이동
	{
		Vector2Int diff = target.position - position;
		int dx = diff.x == 0 ? 0 : (diff.x > 0 ? 1 : -1);
		int dy = diff.y == 0 ? 0 : (diff.y > 0 ? 1 : -1);

		foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
		{
			if (GetDirVector(d) == new Vector2Int(dx, dy))
			{
				Move(d);
				break;
			}
		}
	}
	#endregion

	#region Engage 상태 유닛타입별 분류

	protected void ENGAGE_Archer()
	{
		// 피격 리액션이 우선
		if (isHitThisTurn)
		{
			// 궁수 피격 처리: 뒤로 1칸 대각선 이동
			Dir runDir = (Dir)Mathf.Repeat((int)currentDir + 4 + Random.Range(-1, 2), 8);
			Move(runDir);
			return;
		}

		Unit target = GetClosestEnemy(out float minDist);
		if (target == null) return;

		// 공격 범위 체크
		if (minDist <= 5.5f)
		{
			target.TakeDamage(attackPower);
			Debug.Log($"{unitType.typeName}가 {target.unitType.typeName}을 공격해 {attackPower} 피해를 입힘");
		}
		else
		{
			MoveTowardsTarget(target);
		}
	}

	protected void ENGAGE_Wolf()
	{
		// 피격 리액션이 우선
		if (isHitThisTurn && !oneTimeReactUsed)
		{
			// 늑대 피격 처리: 1회 대각선 전진 (적 방향 구현 필요시 구체화)
			oneTimeReactUsed = true;
			Move((Dir)Random.Range(0, 8)); // 임시 전진
			return;
		}

		Unit target = GetClosestEnemy(out float minDist);
		if (target == null) return;

		// 공격 범위 체크
		if (minDist <= 1.5f)
		{
			target.TakeDamage(attackPower);
			Debug.Log($"{unitType.typeName}가 {target.unitType.typeName}을 공격해 {attackPower} 피해를 입힘");
		}
		else
		{
			MoveTowardsTarget(target);
		}
	}

	protected void ENGAGE_Default()
	{
		Unit target = GetClosestEnemy(out float minDist);
		if (target == null) return;

		if (minDist <= 1.5f)
		{
			target.TakeDamage(attackPower);
			Debug.Log($"{unitType.typeName}가 {target.unitType.typeName}을 공격해 {attackPower} 피해를 입힘");
		}
		else
		{
			MoveTowardsTarget(target);
		}
	}
	#endregion
}

public class Human : Unit
{
	public override void JudgeState()
	{
		base.JudgeState();
		// 인류 상태 판단 로직 추가
	}
}

public class Monster : Unit
{
	public override void JudgeState()
	{
		base.JudgeState();
		// 몬스터 상태 판단 로직 추가
	}
}

