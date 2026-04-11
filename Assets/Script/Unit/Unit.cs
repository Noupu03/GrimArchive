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
	// 동적 크기 맵 (0: 미탐색, 1: 바닥, 2: 벽) 타일맵 정보 공유
	public int[][,] discoveredMap;
	// 시야 내 발견된 적 유닛 데이터 공유
	public List<Unit> spottedEnemyUnits = new List<Unit>();

	public FactionData()
	{
		// 기본적으로 빈 배열로 두거나, InitMap에서 초기화
		discoveredMap = new int[0][,];
	}

	public void InitMap(CreateMap cmap)
	{
		if (cmap == null || cmap.map.floors == null) return;
		int floorCount = cmap.map.floors.Length;
		discoveredMap = new int[floorCount][,];
		for (int i = 0; i < floorCount; i++)
		{
			int w = cmap.map.floors[i].config.width * 8;
			int h = cmap.map.floors[i].config.height * 8;
			discoveredMap[i] = new int[w, h];
		}
	}

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
	public int currentFloor = 0; // 현재 유닛이 위치한 층 정보
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
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null) ? GameSession.Instance.cmap : FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null) return false;
		if (currentFloor < 0 || currentFloor >= cmap.map.floors.Length) return false;

		Floor floor = cmap.map.floors[currentFloor];
		if (floor.chunks == null) return false;

		int cx = pos.x / 8;
		int tx = pos.x % 8;
		int cy = pos.y / 8;
		int cyVal = pos.y % 8;

		if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height) return false;

		Chunks c = floor.chunks[cx, cy];
		if (c.roomId == -1 || c.chunk == null) return false;

		if (c.chunk[tx, cyVal].name == "Wall") return false;

		// 다른 유닛 점유 여부 확인 (최적화: O(1) 캐싱 배열)
		if (GameSession.Instance != null && GameSession.Instance.unitGrid.TryGetValue(new Vector3Int(pos.x, pos.y, currentFloor), out Unit u))
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

		Floor floor = cmap.map.floors[currentFloor];
		if (floor.chunks == null) return;
		int mapWidth = floor.config.width * 8;
		int mapHeight = floor.config.height * 8;

		while (dist <= maxRadius)
		{
			if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) break;

			int cx = x / 8;
			int tx = x % 8;
			int cy = y / 8;
			int ty = y % 8;

			if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height) break;

			Chunks c = floor.chunks[cx, cy];
			if (c.roomId == -1 || c.chunk == null) break;

			Tile tile = c.chunk[tx, ty];
			myData.discoveredMap[currentFloor][x, y] = tile.name == "Wall" ? 2 : 1;

			// 유닛 발견 (O(1) 캐싱 검색 적용)
			if (GameSession.Instance != null && GameSession.Instance.unitGrid.TryGetValue(new Vector3Int(x, y, currentFloor), out Unit unit))
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

	private GoapBrain _brain;
	public GoapBrain brain { get { if (_brain == null) _brain = new GoapBrain(); return _brain; } }

	public virtual void JudgeState()
	{
		brain.JudgeState(this);
	}

	public virtual void ExecuteAction()
	{
		brain.ExecuteAction(this);
	}
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

