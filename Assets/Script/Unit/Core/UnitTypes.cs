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

// Dir(8방향) 회전 유틸(2026-08-20) — PartyDeathSystem.cs와 TacticalFSMState.cs가 각자 거의 동일한
// private static Opposite/RotateCW를 중복 구현하고 있던 것을 통합했다(둘의 RotateCW 모듈러 안전
// 마진이 +8 vs +80으로 서로 달랐음 — 실제 호출값(steps -3~3) 범위에서는 둘 다 안전하지만 복붙 과정의
// drift였다).
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

// 2026-07-27 신규 — 모든 야생(Neutral) 방에 필수 배치되는 방 고정 몬스터(GameSession.
// SpawnWildRoomGuards 참고). 스킬은 근접 탱커와 동일, 스탯은 그보다 약하게(사용자 요청).
public class WildMonsterA : UnitType
{
	public WildMonsterA() { typeName = "야생 몬스터 A"; footprint = new Vector2(1, 1); }
}

// 보스용 골렘(2026-08-24, 기획 확정: "1층 보스방에 고정 소환, 야생 소속") — 기본 몬스터의 3배
// 풋프린트. GameSession.SpawnBossGolem이 1층 보스방 안 빈 좌표 하나에만 고정 배치한다(WildMonsterA처럼
// 야생 방마다 반복 배치되는 게 아니라 게임 전체에 한 마리).
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
// 플레이어블 8개 직업 (units.json 기준 — WaveSpawner.ResolveUnitType이 리플렉션으로 typeName을
// 매칭하므로 WaveData에서 이 이름들을 unitTypeName으로 쓰려면 여기 서브클래스가 있어야 한다.
// 실제 footprint는 어차피 UnitGenerate.SetupUnitVisual이 프리팹의 UnitVisualDefinition.footprint로
// 다시 덮어쓰므로 여기 값은 units.json과 맞춰두는 정도의 의미다.)
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
	// 최대 방어 성공률 (2026-08-24: 0.95 → 0.60. 회피/패링이 사실상 항상 성공해 전투가 끝나지 않았다)
	public const float MAX_DEFENSE_SUCCESS_RATE = 0.60f;
	// =======점멸=======
	// 점멸 사용 여부 (2026-08-24 사용자 요청으로 비활성화).
	// false면 DefenseSystem의 선제 반응 후보에서 점멸이 아예 빠지고 회피만 남는다.
	// 실행 로직(ExecuteEarlyReaction의 Blink 분기)은 그대로 남겨뒀으므로 true로 되돌리면 즉시 부활한다.
	public const bool ENABLE_BLINK = false;
	// 점멸 MP 소모 비율
	// (최대 MP의 30%)
	public const float BLINK_MP_COST_RATIO = 0.30f;
	// =======막기=======
	// 최소 피해 감소율
	public const float MIN_BLOCK_DAMAGE_REDUCTION = 0.01f;
	// 최대 피해 감소율 (2026-08-24: 1.00 → 0.50. 1.00은 "막으면 피해 0"이라 방어력이 어느 정도만
	// 있어도 계수 계산이 상한에 붙어 공격이 통째로 무효화됐다 — 전투가 끝나지 않던 주원인)
	public const float MAX_BLOCK_DAMAGE_REDUCTION = 0.50f;
}
