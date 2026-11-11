// 리더 명령 설계명세서 v0.15(2026-11-01) 공용 열거형. 순수 판정은 LeaderMath/CommandSelectionMath, 호출부는 *System, 상태는 LeaderComponent/Party.Command.cs.

// 전술 명령 종류. 집결·문 개방·파괴·방 이동·후퇴는 쿨타임이 없는 기본 지휘라 여기에 넣지 않는다(7장).
public enum TacticalCommand
{
	None,
	FocusAttack,          // 집중 공격(현재 통솔력 20+)
	Strike,               // 강공격(40+) — 현재 공격 대상 유지, 고위력 공격 기술 우선
	Suppress,             // 제압(40+) — 지정 적 CC, 그 적을 공격하는 아군 공격 버프 우선
	Protect,              // 보호(40+) — 각자 개인 보호 기준으로 치료·보호 버프·위협 적 CC 우선
	SuperiorFocusAttack,  // 상위 공동 공격(60+, 상위 지휘관) — 여러 소속 파티가 적 하나를 우선
	ObjectFocus,          // 통로 차단물 공동 파괴 지정(12-3장) — 집중 공격의 대상이 오브젝트인 변형
	SignatureSkill,       // 80/100 고유 지휘 스킬 — 문서 미정(쿨타임 연동 틀만)
}

// 행동 우선 명령 3종(강공격·제압·보호) — 5초 동안 기술 선택 순서를 바꾼다. TacticalCommand의 부분집합.
public static class TacticalCommandUtil
{
	public static bool IsActionPriority(this TacticalCommand c) => c == TacticalCommand.Strike || c == TacticalCommand.Suppress || c == TacticalCommand.Protect;
	public static bool IsFocus(this TacticalCommand c) => c == TacticalCommand.FocusAttack || c == TacticalCommand.SuperiorFocusAttack || c == TacticalCommand.ObjectFocus;

	public static string ToKorean(this TacticalCommand c) => c switch
	{
		TacticalCommand.FocusAttack         => "집중 공격",
		TacticalCommand.Strike              => "강공격",
		TacticalCommand.Suppress            => "제압",
		TacticalCommand.Protect             => "보호",
		TacticalCommand.SuperiorFocusAttack => "상위 공동 공격",
		TacticalCommand.ObjectFocus         => "차단물 공동 파괴",
		TacticalCommand.SignatureSkill      => "고유 지휘 스킬",
		_                                   => c.ToString(),
	};
}

// 인류 파티 컨셉 — 임무(PartyType)와 다른 축이다. 버프 종류·위협 배율·행동 우선 명령 순서를 정한다.
public enum PartyConcept
{
	Adventurer, // 초반 모험가
	Rogue,      // 초반 도적
	Mercenary,  // 용병 — 효과 수치 미정
	Army,       // 군대 — 효과 수치 미정
	Hero,       // 용사 — 효과 수치 미정
	Vagrant,    // 부랑자 — 리더 없는 초기 독립 유닛
}

public static class PartyConceptUtil
{
	public static string ToKorean(this PartyConcept c) => c switch
	{
		PartyConcept.Adventurer => "모험가",
		PartyConcept.Rogue      => "도적",
		PartyConcept.Mercenary  => "용병",
		PartyConcept.Army       => "군대",
		PartyConcept.Hero       => "용사",
		PartyConcept.Vagrant    => "부랑자",
		_                       => c.ToString(),
	};
}

// 지휘 상실 사유(17장). 던전 밖 정상 이탈은 문서에 규정이 없어 승계 대기 없이 기존처럼 즉시 재선정한다(Party.OnMemberExited) — 사유로 세지 않는다.
public enum LeaderLossReason
{
	Death,
	Flee,
}

// 지휘 권한: 일반 리더(자기 파티) / 상위 지휘관(같은 원정의 여러 파티).
public enum CommandAuthority
{
	Leader,
	Superior,
}
