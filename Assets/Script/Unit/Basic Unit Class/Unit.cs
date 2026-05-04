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

//  인류 클래스
public class Knight : UnitType { public Knight() { typeName = "기사형"; footprint = new Vector2(1, 1); } }

// 몬스터 역할군
public class MeleeTank : UnitType { public MeleeTank() { typeName = "근접 탱커"; footprint = new Vector2(2, 2); } }

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

	public List<Unit> personalSpottedEnemies = new List<Unit>();

	public void SetupStats()
	{
		if (unitType is Knight)
		{
			hp = 150; mp = 0; physicalAttack = 18; physicalDefense = 12;
			accuracy = 84; evasion = 6; magicalAttack = 0; magicalDefense = 6;
			spotting = 4; leadership = 8; walkSpeed = 3.0f; sprintSpeed = 4.2f;
			reaction = 10; physicalAttackSpeed = 12; magicalAccuracy = 0; magicalCastSpeed = 0;
			statusResistance = 14; dotResistance = 10; mentalResistance = 8; baseMental = 48;
		}
		else if (unitType is MeleeTank)
		{
			hp = 165; mp = 0; physicalAttack = 16; physicalDefense = 13;
			accuracy = 82; evasion = 4; magicalAttack = 0; magicalDefense = 5;
			spotting = 6; leadership = 0; walkSpeed = 2.9f; sprintSpeed = 4.0f;
			reaction = 8; physicalAttackSpeed = 8; magicalAccuracy = 0; magicalCastSpeed = 0;
			statusResistance = 14; dotResistance = 12; mentalResistance = 0; baseMental = 0;
		}

		currentMental = baseMental;
	}

	public abstract void TakeDamage(float damage);
	public abstract void TakePhysicalDamage(float rawDamage, Unit attacker);
	public abstract void TakeMagicalDamage(float rawDamage, Unit attacker);
	public abstract void TakeMentalDamage(float rawDamage, Unit attacker);
	public abstract void DropArtifact();
	public abstract void ApplyStun(float duration);
	public abstract void ApplySlow(float duration);
	public abstract void ApplyPoison(float duration);
	public abstract void ApplyBurn(float duration);
	public abstract Vector2Int GetDirVector(Dir dir);
	public abstract bool CanMove(Vector2Int pos);
	public abstract void Move(Dir dir);
	public abstract void UpdateFOV(List<Unit> allUnits);
	
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

public class Human : UnitFunction
{
	public override void JudgeState()
	{
		base.JudgeState();
		// 인류 상태 판단 로직 추가
	}
}

public class Monster : UnitFunction
{
	public override void JudgeState()
	{
		base.JudgeState();
		// 몬스터 상태 판단 로직 추가
	}
}

