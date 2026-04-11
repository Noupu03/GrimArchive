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
	public Vector2 footprint;
}

// 인류 클래스
public class Knight : UnitType { public Knight() { typeName = "기사형"; footprint = new Vector2(1, 1); } }
public class ArcherType : UnitType { public ArcherType() { typeName = "궁수형"; footprint = new Vector2(1, 1); } }
public class Priest : UnitType { public Priest() { typeName = "사제형"; footprint = new Vector2(1, 1); } }
public class Commander : UnitType { public Commander() { typeName = "지휘관형"; footprint = new Vector2(1, 1); } }

// 몬스터 역할군
public class MeleeTank : UnitType { public MeleeTank() { typeName = "근접 탱커"; footprint = new Vector2(2, 2); } }
public class MeleeDealer : UnitType { public MeleeDealer() { typeName = "근접 딜러"; footprint = new Vector2(1, 1); } }
public class RangedSlow : UnitType { public RangedSlow() { typeName = "원거리 둔화"; footprint = new Vector2(1, 1); } }
public class RangedMental : UnitType { public RangedMental() { typeName = "원거리 정신"; footprint = new Vector2(1, 1); } }
public class Boss : UnitType { public Boss() { typeName = "보스"; footprint = new Vector2(3, 3); } }

public class ArtifactItem
{
	public Vector2Int position;
	public int floor;
	public GameObject visual;
	public bool isPickedUp;
}

public class FactionData
{
	// 동적 크기 맵 (0: 미탐색, 1: 바닥, 2: 벽) 타일맵 정보 공유
	public int[][,] discoveredMap;
	// 시야 내 발견된 적 유닛 데이터 공유
	public List<Unit> spottedEnemyUnits = new List<Unit>();
	public List<ArtifactItem> spottedArtifacts = new List<ArtifactItem>();

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
	public float mp = 0f;
	public float physicalAttack = 10f;
	public float physicalDefense = 0f;
	public float accuracy = 10f;
	public float evasion = 0f;
	public float magicalAttack = 0f;
	public float magicalDefense = 0f;
	public float spotting = 0f;
	public float leadership = 0f;
	public float walkSpeed = 3f;
	public float sprintSpeed = 4f;
	public float reaction = 1f; // 반응도
	public float physicalAttackSpeed = 10f;
	public float magicalAccuracy = 0f;
	public float magicalCastSpeed = 0f;
	public float statusResistance = 0f;
	public float dotResistance = 0f;
	public float mentalResistance = 0f;
	public float baseMental = 0f;
	public float currentMental = 0f;

	public float actionCooldown = 0f; // 턴 진행용 대기 시간
	public float attackCooldown = 0f; // 공격 쿨다운
	public float skillCooldown = 0f; // 스킬 쿨다운
	public bool isHitThisTurn = false; // 피격 여부
	public bool oneTimeReactUsed = false; // 피격 리액션 등 1회성 억제용
	
	public bool hasArtifact = false; // 유물 운반 여부
	public float interactionTimer = 0f;
	public float blockRate = 10f; // 임시 막기 확률

	public float GetEvasion() => hasArtifact ? evasion * 0.5f : evasion;
	public float GetBlockRate() => hasArtifact ? blockRate * 0.5f : blockRate;

	// 상태이상
	public float stunDuration = 0f;
	public float slowDuration = 0f;
	public float poisonDuration = 0f;
	public float burnDuration = 0f;

	// 이동 및 프레임워크
	public float currentSpeed = 0f;
	public float acceleration = 10f; // 임시 기본 가속도
	public bool isWaitState = false; // Spotting Broadcast에 의한 대기

	public Vector2Int? playerMoveTarget = null;
	public Unit playerAttackTarget = null;

	public Vector2Int position;
	public int currentFloor = 0; // 현재 유닛이 위치한 층 정보
	public Dir currentDir = Dir.DOWN; // 현재 바라보는 방향 (시야 기준)
	public static float ViewRadius = 30f; // 전역 시야 거리

	public void SetupStats()
	{
		if (unitType is Knight)
		{
			hp = 150; mp = 0; physicalAttack = 18; physicalDefense = 12;
			accuracy = 14; evasion = 6; magicalAttack = 0; magicalDefense = 6;
			spotting = 4; leadership = 8; walkSpeed = 3.0f; sprintSpeed = 4.2f;
			reaction = 10; physicalAttackSpeed = 12; magicalAccuracy = 0; magicalCastSpeed = 0;
			statusResistance = 14; dotResistance = 10; mentalResistance = 8; baseMental = 48;
		}
		else if (unitType is ArcherType)
		{
			hp = 90; mp = 0; physicalAttack = 14; physicalDefense = 5;
			accuracy = 22; evasion = 18; magicalAttack = 0; magicalDefense = 4;
			spotting = 5; leadership = 6; walkSpeed = 3.8f; sprintSpeed = 5.2f;
			reaction = 14; physicalAttackSpeed = 20; magicalAccuracy = 0; magicalCastSpeed = 0;
			statusResistance = 8; dotResistance = 8; mentalResistance = 6; baseMental = 45;
		}
		else if (unitType is Priest)
		{
			hp = 95; mp = 70; physicalAttack = 8; physicalDefense = 4;
			accuracy = 10; evasion = 8; magicalAttack = 16; magicalDefense = 8;
			spotting = 8; leadership = 18; walkSpeed = 3.2f; sprintSpeed = 4.4f;
			reaction = 11; physicalAttackSpeed = 10; magicalAccuracy = 18; magicalCastSpeed = 10;
			statusResistance = 10; dotResistance = 10; mentalResistance = 16; baseMental = 60;
		}
		else if (unitType is Commander)
		{
			hp = 120; mp = 20; physicalAttack = 14; physicalDefense = 9;
			accuracy = 16; evasion = 10; magicalAttack = 0; magicalDefense = 6;
			spotting = 10; leadership = 24; walkSpeed = 3.4f; sprintSpeed = 4.6f;
			reaction = 13; physicalAttackSpeed = 14; magicalAccuracy = 0; magicalCastSpeed = 0;
			statusResistance = 12; dotResistance = 12; mentalResistance = 18; baseMental = 55;
		}
		else if (unitType is MeleeTank)
		{
			hp = 165; mp = 0; physicalAttack = 16; physicalDefense = 13;
			accuracy = 12; evasion = 4; magicalAttack = 0; magicalDefense = 5;
			spotting = 6; leadership = 0; walkSpeed = 2.9f; sprintSpeed = 4.0f;
			reaction = 8; physicalAttackSpeed = 8; magicalAccuracy = 0; magicalCastSpeed = 0;
			statusResistance = 14; dotResistance = 12; mentalResistance = 0; baseMental = 0;
		}
		else if (unitType is MeleeDealer)
		{
			hp = 85; mp = 0; physicalAttack = 20; physicalDefense = 5;
			accuracy = 18; evasion = 12; magicalAttack = 0; magicalDefense = 4;
			spotting = 6; leadership = 0; walkSpeed = 3.9f; sprintSpeed = 5.4f;
			reaction = 12; physicalAttackSpeed = 16; magicalAccuracy = 0; magicalCastSpeed = 0;
			statusResistance = 8; dotResistance = 8; mentalResistance = 0; baseMental = 0;
		}
		else if (unitType is RangedSlow)
		{
			hp = 80; mp = 0; physicalAttack = 14; physicalDefense = 4;
			accuracy = 18; evasion = 10; magicalAttack = 0; magicalDefense = 4;
			spotting = 6; leadership = 0; walkSpeed = 3.5f; sprintSpeed = 4.8f;
			reaction = 11; physicalAttackSpeed = 14; magicalAccuracy = 0; magicalCastSpeed = 0;
			statusResistance = 8; dotResistance = 8; mentalResistance = 0; baseMental = 0;
		}
		else if (unitType is RangedMental)
		{
			hp = 70; mp = 40; physicalAttack = 0; physicalDefense = 3;
			accuracy = 10; evasion = 8; magicalAttack = 12; magicalDefense = 8;
			spotting = 8; leadership = 0; walkSpeed = 3.4f; sprintSpeed = 4.6f;
			reaction = 12; physicalAttackSpeed = 0; magicalAccuracy = 18; magicalCastSpeed = 14;
			statusResistance = 8; dotResistance = 8; mentalResistance = 0; baseMental = 0;
		}
		else if (unitType is Boss)
		{
			hp = 320; mp = 80; physicalAttack = 26; physicalDefense = 14;
			accuracy = 18; evasion = 6; magicalAttack = 20; magicalDefense = 12;
			spotting = 12; leadership = 0; walkSpeed = 3.2f; sprintSpeed = 5.0f;
			reaction = 14; physicalAttackSpeed = 10; magicalAccuracy = 16; magicalCastSpeed = 12;
			statusResistance = 18; dotResistance = 18; mentalResistance = 0; baseMental = 0;
		}

		currentMental = baseMental;
	}

	public void TakeDamage(float damage)
	{
		float prevHp = hp;
		hp -= damage;
		isHitThisTurn = true;
		if (UnitGenerate.Instance != null) UnitGenerate.Instance.TriggerHitEffect(this);

		if (hasArtifact && damage >= prevHp * 0.3f) DropArtifact();
	}

	public void TakePhysicalDamage(float rawDamage, Unit attacker)
	{
		float damage = Mathf.Max(1f, rawDamage - physicalDefense);
		TakeDamage(damage);
	}

	public void TakeMagicalDamage(float rawDamage, Unit attacker)
	{
		float damage = Mathf.Max(1f, rawDamage - magicalDefense);
		TakeDamage(damage);
	}

	public void TakeMentalDamage(float rawDamage, Unit attacker)
	{
		if (this is Human)
		{
			int prevStage = Mathf.FloorToInt(currentMental / (baseMental * 0.25f));
			currentMental -= rawDamage; // 정신력만 감소
			int currentStage = Mathf.FloorToInt(currentMental / (baseMental * 0.25f));
			isHitThisTurn = true;

			if (hasArtifact && currentStage < prevStage) DropArtifact();
		}
	}

	public void DropArtifact()
	{
		if (!hasArtifact) return;
		hasArtifact = false;
		interactionTimer = 0f;
		if (ArtifactManager.Instance != null) ArtifactManager.Instance.SpawnArtifact(position, currentFloor);
		Debug.Log($"{unitType.typeName}가 피격/공황으로 유물을 드롭했습니다!");
	}

	public void ApplyStun(float duration) { stunDuration = Mathf.Max(stunDuration, duration); }
	public void ApplySlow(float duration) { slowDuration = Mathf.Max(slowDuration, duration); }
	public void ApplyPoison(float duration) { poisonDuration = Mathf.Max(poisonDuration, duration); }
	public void ApplyBurn(float duration) { burnDuration = Mathf.Max(burnDuration, duration); }

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

		int w = (int)unitType.footprint.x;
		int h = (int)unitType.footprint.y;

		for (int dx = 0; dx < w; dx++)
		{
			for (int dy = 0; dy < h; dy++)
			{
				int targetX = pos.x + dx;
				int targetY = pos.y + dy;

				int cx = targetX / 8;
				int tx = targetX % 8;
				int cy = targetY / 8;
				int cyVal = targetY % 8;

				if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height) return false;

				Chunks c = floor.chunks[cx, cy];
				if (c.roomId == -1 || c.chunk == null) return false;

				if (c.chunk[tx, cyVal].name == "Wall") return false;

				// 다른 유닛 점유 여부 확인 (최적화: O(1) 캐싱 배열)
				if (GameSession.Instance != null && GameSession.Instance.unitGrid.TryGetValue(new Vector3Int(targetX, targetY, currentFloor), out Unit u))
				{
					if (u != null && u != this && u.hp > 0)
					{
						return false;
					}
				}
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

			// 유물 발견
			if (ArtifactManager.Instance != null)
			{
				foreach(var art in ArtifactManager.Instance.artifacts)
				{
					if (!art.isPickedUp && art.floor == currentFloor && art.position.x == x && art.position.y == y)
					{
						if (!myData.spottedArtifacts.Contains(art)) myData.spottedArtifacts.Add(art);
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
		if (stunDuration > 0f) return; // 기절 시 행동 불가
		brain.JudgeState(this);
	}

	public virtual void ExecuteAction()
	{
		if (stunDuration > 0f) return;
		brain.ExecuteAction(this);
	}

	public virtual void OnUpdate(float deltaTime)
	{
		if (stunDuration > 0f) stunDuration -= deltaTime;
		if (slowDuration > 0f) slowDuration -= deltaTime;
		
		if (poisonDuration > 0f)
		{
			poisonDuration -= deltaTime;
			hp -= 1f * deltaTime; // 매초 피해
		}
		
		if (burnDuration > 0f)
		{
			burnDuration -= deltaTime;
			hp -= 1f * deltaTime; // 매초 피해
		}
		
		if (attackCooldown > 0f) attackCooldown -= deltaTime;
		if (skillCooldown > 0f) skillCooldown -= deltaTime;
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

