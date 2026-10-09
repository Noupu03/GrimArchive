using System.Collections.Generic;

// 유닛 애니메이션 "자리"(슬롯) 목록. 값은 프리팹에 직렬화되므로 순서를 바꿔도 번호를 유지한다.
public enum AnimSlot
{
	Idle        = 0,
	Walk        = 1,
	Skill       = 2,   // 스킬명별로 하나씩 — SlotEntry.skillName이 키
	Hit         = 3,
	Stunned     = 4,
	Death       = 5,
	Dodge       = 6,
	Blink       = 7,
	Channel     = 8,   // 코어/문/함정 부수기
	Investigate = 9,
	DisarmTrap  = 10,
	Spawn       = 11,
	UseStairs   = 12,
	PickUp      = 13,
}

public enum AnimSlotKind
{
	Loop,     // 조건이 유지되는 동안 반복 (폴링으로 선택)
	OneShot,  // 이벤트로 시작해 끝까지 재생 후 복귀
	Hold,     // 끝까지 재생 후 마지막 프레임 유지 (사망)
}

public readonly struct AnimSlotInfo
{
	public readonly string       Label;
	public readonly string       Description;
	public readonly AnimSlotKind Kind;
	public readonly int          Priority;   // 클수록 우선. 낮은 원샷은 높은 원샷을 못 끊는다.
	public readonly bool         HumanOnly;

	public AnimSlotInfo(string label, string description, AnimSlotKind kind, int priority, bool humanOnly = false)
	{
		Label = label; Description = description; Kind = kind; Priority = priority; HumanOnly = humanOnly;
	}
}

public static class AnimSlotCatalog
{
	private static readonly Dictionary<AnimSlot, AnimSlotInfo> Table = new Dictionary<AnimSlot, AnimSlotInfo>
	{
		[AnimSlot.Death]       = new AnimSlotInfo("사망",       "유닛이 죽는 순간 1회 재생 후 마지막 프레임 유지. 이 슬롯에 클립이 있으면 클립 길이만큼 비주얼이 남고 시체는 연출 뒤에 나타난다.", AnimSlotKind.Hold,    100),
		[AnimSlot.Stunned]     = new AnimSlotInfo("기절",       "기절 상태(stunDuration > 0) 동안 반복. 사망을 뺀 모든 연출보다 우선한다.",                                 AnimSlotKind.Loop,    90),
		[AnimSlot.Dodge]       = new AnimSlotInfo("회피",       "위협을 피해 옆 칸으로 한 칸 이동할 때 1회.",                                                              AnimSlotKind.OneShot, 80),
		[AnimSlot.Blink]       = new AnimSlotInfo("점멸",       "위협을 피해 최대 4칸 순간이동할 때 1회.",                                                                  AnimSlotKind.OneShot, 80),
		[AnimSlot.Skill]       = new AnimSlotInfo("스킬",       "스킬 시전 시작 시 1회. 시전 시간(castMs)이 있으면 클립 전체가 그 시간에 맞춰 재생되고 타격 순간은 클립 끝이다. 즉발 스킬(0ms)은 1배속이며 타격은 이미 적용된 상태다.", AnimSlotKind.OneShot, 70),
		[AnimSlot.Hit]         = new AnimSlotInfo("피격",       "피해를 받는 순간 1회(짧게). 스킬·회피·점멸 연출은 끊지 못하고 걷기·대기·상호작용만 끊는다.",                  AnimSlotKind.OneShot, 60),
		[AnimSlot.Spawn]       = new AnimSlotInfo("등장",       "유닛이 생성되는 순간 1회.",                                                                                AnimSlotKind.OneShot, 50),
		[AnimSlot.UseStairs]   = new AnimSlotInfo("계단 이동",  "계단으로 층을 건너는 순간 1회.",                                                                            AnimSlotKind.OneShot, 50, true),
		[AnimSlot.PickUp]      = new AnimSlotInfo("전리품 회수", "조사를 마치고 전리품을 줍는 순간 1회.",                                                                     AnimSlotKind.OneShot, 50, true),
		[AnimSlot.Channel]     = new AnimSlotInfo("부수기",     "코어·문·함정을 부수는 동안 반복(대상을 바라본 채).",                                                       AnimSlotKind.Loop,    40),
		[AnimSlot.DisarmTrap]  = new AnimSlotInfo("함정 해제",  "함정을 해제하는 동안 반복.",                                                                                AnimSlotKind.Loop,    40, true),
		[AnimSlot.Investigate] = new AnimSlotInfo("조사",       "오브젝트를 조사하는 동안(약 8초) 반복. 피격·소리로 중단될 수 있다.",                                       AnimSlotKind.Loop,    40, true),
		[AnimSlot.Walk]        = new AnimSlotInfo("걷기",       "이동 중 반복(비주얼이 실제로 움직이는 동안).",                                                              AnimSlotKind.Loop,    20),
		[AnimSlot.Idle]        = new AnimSlotInfo("대기",       "아무 조건도 없을 때의 기본 반복.",                                                                          AnimSlotKind.Loop,    10),
	};

	// 슬롯 생성·인스펙터 표시 순서(스킬 슬롯은 SlotsFor가 유닛별로 뒤에 붙인다).
	private static readonly AnimSlot[] CommonOrder =
	{
		AnimSlot.Idle, AnimSlot.Walk, AnimSlot.Hit, AnimSlot.Stunned, AnimSlot.Death,
		AnimSlot.Dodge, AnimSlot.Blink, AnimSlot.Channel, AnimSlot.Spawn,
		AnimSlot.Investigate, AnimSlot.DisarmTrap, AnimSlot.UseStairs, AnimSlot.PickUp,
	};

	public static AnimSlotInfo Get(AnimSlot slot) => Table[slot];

	// 프리팹에 만들어 둘 (슬롯, 스킬명) 목록 — 인류 전용 슬롯은 인류만, 스킬 슬롯은 유닛이 가진 스킬 순서대로.
	public static List<(AnimSlot slot, string skillName)> SlotsFor(bool isHuman, IEnumerable<string> skillNames)
	{
		var list = new List<(AnimSlot, string)>();
		foreach (AnimSlot s in CommonOrder)
		{
			if (Table[s].HumanOnly && !isHuman) continue;
			list.Add((s, null));
		}
		if (skillNames != null)
		{
			var seen = new HashSet<string>();
			foreach (string name in skillNames)
				if (!string.IsNullOrEmpty(name) && seen.Add(name)) list.Add((AnimSlot.Skill, name));
		}
		return list;
	}

	// 직렬화 항목·런타임 조회 공용 키. 스킬 슬롯만 이름이 키에 들어간다.
	public static string KeyOf(AnimSlot slot, string skillName)
		=> slot == AnimSlot.Skill ? "Skill:" + (skillName ?? "") : slot.ToString();
}
