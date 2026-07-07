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

public abstract class UnitType
{
	public string typeName;
	public Vector2 footprint;
}

// 인류 클래스
public class Knight : UnitType
{
	public Knight() { typeName = "기사형"; footprint = new Vector2(1, 1); }
}

// 몬스터 역할군
public class MeleeTank : UnitType
{
	public MeleeTank() { typeName = "근접 탱커"; footprint = new Vector2(1, 1); }
}


// ----------------------------------------------------
// 원거리 유닛 (투사체 사용)
// ----------------------------------------------------
public class Archer : UnitType
{
    public Archer() 
    { 
        typeName = "아처형"; 
        footprint = new Vector2(1, 1); 
    }
}
// 공통 전투 상수 정의
public static class CombatConstants
{
	// =======반응 시간 (ms)=======
	// AI 기본 반응 시간
	public const float BASE_REACTION_TIME_MS = 250f;
	// 최소 / 최대 반응 시간
	public const float MIN_REACTION_TIME_MS  = 100f;
	public const float MAX_REACTION_TIME_MS  = 500f;
	// =======방어 행동 준비 시간 (ms)=======
	// 막기 준비 시간
	public const float BLOCK_PREPARE_TIME_MS = 80f;
	// 회피 준비 시간
	public const float DODGE_PREPARE_TIME_MS = 120f;
	// 점멸 준비 시간
	public const float BLINK_PREPARE_TIME_MS = 180f;
	// 패링 준비 시간
	public const float PARRY_PREPARE_TIME_MS = 100f;
	// =======방어 성공률=======
	// 최소 방어 성공률
	public const float MIN_DEFENSE_SUCCESS_RATE = 0.05f;
	// 최대 방어 성공률
	public const float MAX_DEFENSE_SUCCESS_RATE = 0.95f;
	// =======점멸=======
	// 점멸 MP 소모 비율
	// (최대 MP의 30%)
	public const float BLINK_MP_COST_RATIO = 0.30f;
	// =======막기=======
	// 최소 피해 감소율
	public const float MIN_BLOCK_DAMAGE_REDUCTION = 0.01f;
	// 최대 피해 감소율
	public const float MAX_BLOCK_DAMAGE_REDUCTION = 1.00f;
}
