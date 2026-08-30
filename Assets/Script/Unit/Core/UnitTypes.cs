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

// Dir(8방향) 회전 유틸 — PartyDeathSystem.cs/TacticalFSMState.cs가 각자 중복 구현하던
// Opposite/RotateCW를 통합했다.
public static class DirUtil
{
	public static Dir Opposite(Dir d) => (Dir)(((int)d + 4) % 8);
	public static Dir RotateCW(Dir d, int steps) => (Dir)(((int)d + steps + 80) % 8);
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

public class HumanBaseType : UnitType
{
    public HumanBaseType() { typeName = "인간"; footprint = new Vector2(1, 1); }
}

// 몬스터 역할군
public class MeleeTank : UnitType
{
	public MeleeTank() { typeName = "근접 탱커"; footprint = new Vector2(1, 1); }
}

public class DollKnight : UnitType
{
	public DollKnight() { typeName = "인형 기사"; footprint = new Vector2(1, 1); }
}

public class GnoleA : UnitType
{
	public GnoleA() { typeName = "놀"; footprint = new Vector2(1, 1); }
}

public class GoblinHoodA : UnitType
{
	public GoblinHoodA() { typeName = "고블린 후드"; footprint = new Vector2(1, 1); }
}

public class WildBaseType : UnitType
{
	public WildBaseType() { typeName = "야생 거점"; footprint = new Vector2(2, 2); }
}

// 모든 야생(Neutral) 방에 필수 배치되는 방 고정 몬스터(GameSession.SpawnWildRoomGuards 참고) —
// 스킬은 근접 탱커와 동일, 스탯은 그보다 약하다.
public class WildMonsterA : UnitType
{
	public WildMonsterA() { typeName = "야생 몬스터 A"; footprint = new Vector2(1, 1); }
}

// 보스용 골렘 — 1층 보스방에 고정 소환되는 야생 소속 유닛, 기본 몬스터의 3배 풋프린트.
// GameSession.SpawnBossGolem이 1층 보스방 안 빈 좌표 하나에만 배치한다(WildMonsterA처럼 야생 방마다
// 반복 배치되는 게 아니라 게임 전체에 한 마리).
public class BossGolem : UnitType
{
	public BossGolem() { typeName = "보스 골렘"; footprint = new Vector2(3, 3); }
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

// ----------------------------------------------------
// 플레이어블 8개 직업(units.json 기준) — WaveSpawner.ResolveUnitType이 리플렉션으로 typeName을
// 매칭하므로 서브클래스가 있어야 하며, footprint는 참고용일 뿐 실제 값은 프리팹이 덮어쓴다.
// ----------------------------------------------------
public class Warrior : UnitType
{
    public Warrior() { typeName = "전사"; footprint = new Vector2(1, 1); }
}

public class Rogue : UnitType
{
    public Rogue() { typeName = "도적"; footprint = new Vector2(1, 1); }
}

public class Mage : UnitType
{
    public Mage() { typeName = "마법사"; footprint = new Vector2(1, 1); }
}

public class Priest : UnitType
{
    public Priest() { typeName = "사제"; footprint = new Vector2(1, 1); }
}

public class Paladin : UnitType
{
    public Paladin() { typeName = "성기사"; footprint = new Vector2(1, 1); }
}

public class Shaman : UnitType
{
    public Shaman() { typeName = "주술사"; footprint = new Vector2(1, 1); }
}

public class Monk : UnitType
{
    public Monk() { typeName = "무도가"; footprint = new Vector2(1, 1); }
}

public class Bard : UnitType
{
    public Bard() { typeName = "음유시인"; footprint = new Vector2(1, 1); }
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
	// 최대 방어 성공률 — 너무 높으면 회피/패링이 사실상 항상 성공해 전투가 끝나지 않는다.
	public const float MAX_DEFENSE_SUCCESS_RATE = 0.60f;
	// =======점멸=======
	// 점멸 비활성화 — false면 선제 반응 후보에서 점멸이 빠지고 회피만 남는다(실행 로직은 남아있어
	// true로 되돌리면 즉시 부활).
	public const bool ENABLE_BLINK = false;
	// 점멸 MP 소모 비율
	// (최대 MP의 30%)
	public const float BLINK_MP_COST_RATIO = 0.30f;
	// =======막기=======
	// 최소 피해 감소율
	public const float MIN_BLOCK_DAMAGE_REDUCTION = 0.01f;
	// 최대 피해 감소율 — 1.00까지 허용하면 방어력이 조금만 있어도 계수가 상한에 붙어 공격이
	// 무효화된다(전투가 안 끝나던 주원인).
	public const float MAX_BLOCK_DAMAGE_REDUCTION = 0.50f;
}
