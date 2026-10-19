using System;
using UnityEngine;
using Haare.Util.Logger;

// 후퇴·도주 시스템의 진입점(후퇴도주 문서 2026-10-13) — 설정·켜짐 스위치, 인류별 상태 질의, 관찰 틱. 실제 일은 역할별 static 클래스가 맡는다:
//   MentalSystem(정신력 변경 단일 진입점) · FleeSystem(개인 도주·조우 충격) · PartyRetreatSystem(파티 후퇴 전이·휴식·재출격) ·
//   RetreatMovement/RetreatBlockResolver(출구·거점 이동과 차단 대응) · RetreatExit(던전 밖 이탈) · RetreatBases(거점 후보·자리).
// 순수 판정은 MentalMath/FleeRollMath/PartyRetreatMath/EncounterShockMath/RetreatPathMath에 있다.
public static class RetreatSystem
{
	const float ObserveIntervalSeconds = 0.5f;

	// 테스트·debug 메뉴용 — null이 아니면 설정 에셋 대신 이 값을 쓴다. 기본은 켜짐(설정 에셋이 없어도 켜지고, 에셋의 kill-switch로만 끈다).
	public static bool? EnabledOverride;
	public static bool Enabled => EnabledOverride ?? (AIConfigLoader.Behavior?.retreatFleeSystemEnabled ?? true);

	// [0,1) 난수 — 테스트에서 교체한다.
	public static Func<float> Random01 = () => UnityEngine.Random.value;

	// ─────────────────────────── 설정값 (설정 에셋이 없으면 문서 기본값) ───────────────────────────
	public static float SprintMultiplier => AIConfigLoader.Behavior?.fleeSprintSpeedMultiplier ?? RetreatPathMath.DefaultSprintMultiplier;
	public static int DetourExtraTiles => AIConfigLoader.Behavior?.retreatDetourExtraTiles ?? RetreatPathMath.DefaultDetourExtraTiles;
	public static float RallyWaitSeconds => AIConfigLoader.Behavior?.retreatRallyWaitSeconds ?? RallySearchMath.DefaultWaitSeconds;
	public static float Gap2MentalDamage => AIConfigLoader.Behavior?.encounterGap2MentalDamage ?? EncounterShockMath.DefaultGap2MentalDamage;
	public static float PathCheckSeconds => AIConfigLoader.Behavior?.retreatPathCheckSeconds ?? 0.5f;
	public static int BaseArrivalRadius => AIConfigLoader.Behavior?.retreatBaseArrivalRadius ?? 2;
	public static float RestStragglerGraceSeconds => AIConfigLoader.Behavior?.retreatRestStragglerGraceSeconds ?? 10f;
	public static float ResortieSafeRoomMaxDanger => AIConfigLoader.Behavior?.resortieSafeRoomMaxDanger ?? 100f;
	public static bool OutsideRecoveryEnabled => AIConfigLoader.Behavior?.outsideRecoveryEnabled ?? true;

	// ─────────────────────────── 상태 질의 ───────────────────────────

	public static bool HasValidLeader(Human human)
		=> human.party != null && human.party.Leader != null && human.party.Leader.hp > 0f;

	// 지금 후퇴 중인가 — 개인 도주 > 개인 후퇴 > 파티 후퇴 순으로 유효한 모드를 돌려준다.
	public static RetreatMode GetEffectiveMode(Human human)
	{
		var state = human?.Retreat;
		if (state != null && state.Mode != RetreatMode.None) return state.Mode;
		return human?.party != null && human.party.Retreat.IsRetreating ? RetreatMode.PartyRetreat : RetreatMode.None;
	}

	// 이 틱에 이 인류가 후퇴 때문에 하는 행동. 개인 도주·개인 후퇴는 항상 이동(출구), 파티 후퇴는 명령을 받은 인원이 단계에 따라 이동 또는 거점 휴식이다. 던전 밖(0층)이면 해당 없음.
	public static RetreatActivity GetActivity(Human human)
	{
		if (!Enabled || human == null || human.hp <= 0f || human.currentFloor < 1) return RetreatActivity.None;
		var state = human.Retreat;
		if (state == null) return RetreatActivity.None;
		if (state.Mode == RetreatMode.PersonalFlee || state.Mode == RetreatMode.PersonalRetreat) return RetreatActivity.Move;

		var party = human.party;
		if (party == null || !party.Retreat.IsRetreating || !state.Ordered) return RetreatActivity.None;
		switch (party.Retreat.Phase)
		{
			case PartyRetreatPhase.Leaving: return RetreatActivity.Move;
			case PartyRetreatPhase.Moving:
			case PartyRetreatPhase.Resting: return state.AtBase ? RetreatActivity.Rest : RetreatActivity.Move;
			default: return RetreatActivity.None;
		}
	}

	// 후퇴 FSM 상태(전투 무시 이동)를 강제 배정할 유닛인가 — UnitFSM.SelectState가 부른다. 던전 입구 시퀀스 중에는 해당 없음.
	public static bool WantsRetreatState(Unit unit)
		=> unit is Human human && !human.isInDungeonEntranceSequence && GetActivity(human) == RetreatActivity.Move;

	// 후퇴·도주 이동이나 거점 휴식 중이라 리더의 평소 명령(집결·방 이동·문 찾기 추종·코어 보고 이동)을 받을 수 없는 상태인가 — 이 상태의 인류는 Tactical 대기 리프를 안 타므로(후퇴 이동은 RetreatFSMState, 휴식은 Rest 분기가 대기보다 앞) 명령을 줘도 헛돌고, 집결·진형 완료 판정만 상한 시간까지 붙잡는다. 명령을 배정하는 곳이 이 값으로 건너뛴다. 꺼져 있으면 항상 false.
	public static bool IsBoundToRetreat(Human human) => GetActivity(human) != RetreatActivity.None;

	// 리더가 도주 확정을 직접 확인했는가(명세서 6-2장 "도주 사실 확인" — 직접 확인·유효한 보고로 알게 된 도주자는 집결 대기 대상에서 제외, 부재·무응답만으로는 도주로 확정하지 않는다). 확인 이력은 도주 목격 정신력 감소 이력(WitnessedFlees)과 같다. 보고 경로는 없다.
	public static bool IsFleeKnownToLeader(Human member, Human leader)
		=> member?.Retreat != null && member.Retreat.IsFleeing && leader?.Retreat != null && leader != member && leader.Retreat.WitnessedFlees.Contains(member.name);

	// 후퇴·도주 이동 중이면 경계 상태의 이동 감속(75%)을 적용하지 않는다 — 전투에서 후퇴 상태로 강제 전환될 때 CombatFSMState.OnExit가 전투 후 경계 스윕을 새로 세팅하고, 후퇴 중에는 경계 리프(Tactical)가 안 돌아 그 경계가 해제되지 않아 도주자가 계속 느려진다.
	public static bool SuppressesAlertSlowdown(Unit unit) => unit is Human human && GetActivity(human) == RetreatActivity.Move;

	// 거점에서 쉬는 중인가 — 휴식 모션(애니메이션)·Tactical Rest 분기의 조건. 전투가 나면 Combat이 우선하지만 이 값은 유지된다.
	public static bool IsResting(Human human) => GetActivity(human) == RetreatActivity.Rest;

	// 실제로 회복이 일어나는가: 휴식 중이고 전투 상태가 아니며 인지한 적이 없을 때(명세서 2-1장: 전투 발생 → 휴식 회복 중단, 다시 쉴 수 있을 때 재개).
	public static bool IsRecovering(Human human)
		=> IsResting(human) && !(human.CurrentFsmStateIfCreated is CombatFSMState) && human.personalSpottedEnemies.Count == 0;

	// 정신력 단계의 전투 효과(공격력·선딜·방어 성공률·반응시간 배율, 후퇴도주 명세서 3-2장). 꺼져 있거나 인류가 아니면 효과 없음. 연결 지점: 공격력 = UnitFunction.TakePhysical/MagicalDamage(공격자 기준), 선딜 = SkillAction.BeginAttackCast, 방어 성공률 = DefenseSystem 회피·패링. 반응시간은 연결할 단일 지점이 없어 아직 미연결이다.
	public static MentalStageEffects GetStageEffects(Unit unit)
	{
		if (!Enabled || !(unit is Human human) || human.BaseStat.maxMental <= 0f) return MentalStageEffects.Neutral;
		return MentalMath.GetStageEffects(MentalMath.GetStage(human.BaseStat.mental));
	}

	// 방어 성공률 증감(단위: 확률 포인트, 예: 공황 −20 → −0.20)을 0~1 확률에 더하는 값.
	public static float DefenseSuccessDelta(Unit unit) => GetStageEffects(unit).DefenseSuccessDelta / 100f;

	// 파티 후퇴로 걸어 이동 중인 구성원(도주자 제외) — 공동 이동 속도(가장 느린 구성원)와 진형 유지의 대상.
	public static bool IsPartyRetreatMover(Unit unit)
		=> unit is Human human && human.Retreat != null && !human.Retreat.IsFleeing && GetActivity(human) == RetreatActivity.Move;

	// 좁은 통로 통과 순위(OccupancySystem.PassKeyOf가 쓴다) — 후퇴·도주 이동 중이 아니면 평소 입장 순위 그대로. 파티 후퇴는 평소의 정반대(사제형 → 궁수형 → 지휘관형 → 기사형), 개인 도주는 예상 탈출 시간이 짧은 순(동률이면 궁수형 → 사제형 → 지휘관형 → 기사형)이고 항상 일반 순위보다 먼저다(명세서 5-1장).
	public static int ResolvePassRank(Human human, int entryRank)
	{
		if (GetActivity(human) != RetreatActivity.Move) return entryRank;
		if (!human.Retreat.IsFleeing) return RetreatPathMath.PartyRetreatRank(entryRank);

		var move = human.Retreat.Move;
		int steps = move.DestinationFloor == human.currentFloor ? AIMovementHelper.ChebyshevDistance(human.position, move.Destination) : 9999;
		return RetreatPathMath.FleeRank(RetreatPathMath.EscapeSeconds(steps, human.AppliedWalkSpeed), entryRank);
	}

	// 이동 기본 속도에 후퇴 속도를 반영한다: 개인 도주는 달리기(걷기 × 배율), 나머지는 걷기(명세서 5-1장).
	public static float ResolveMoveSpeed(Human human, float baseSpeed)
		=> Enabled && human.Retreat != null && human.Retreat.IsFleeing && human.currentFloor >= 1
			? RetreatPathMath.SprintSpeed(baseSpeed, SprintMultiplier)
			: baseSpeed;

	// 휴식 회복 틱(UnitFunction.OnUpdate가 매 프레임 부른다) — 정신력은 휴식 중에만 +1/초 오른다(탐험 중 자연 회복 없음). HP 회복(재생력×2)은 OnUpdate의 기존 재생 줄이 RegenMultiplier로 처리한다.
	public static void TickRecovery(Human human, float deltaTime)
	{
		if (!Enabled || !IsRecovering(human)) return;
		MentalSystem.RecoverAtRest(human, deltaTime);
	}

	public static bool ShouldDoubleRegen(Human human) => Enabled && IsRecovering(human);

	// 던전 밖 회복 — 다음 침공에 합류하는 생환자를 거점 휴식 완료 기준(HP 60%·정신력 40%)까지 끌어올린다(문서 미정, 내부 판단). 회복하지 않으면 나간 그대로(HP 20% 이하·정신력 15% 이하) 합류해 평가 보류가 끝나자마자 같은 기준으로 다시 후퇴하는 순환이 된다. 이미 기준 이상이면 건드리지 않고, 올렸다면 로그를 남긴다.
	public static void RecoverOutsideDungeon(Human human)
	{
		if (!Enabled || !OutsideRecoveryEnabled || human == null || human.hp <= 0f) return;
		float hpBefore = human.hp;
		human.hp = PartyRetreatMath.OutsideRecoveryHp(hpBefore, human.maxHp);
		float mentalGain = MentalSystem.RecoverTo(human, PartyRetreatMath.OutsideRecoveryMental(human.BaseStat.mental));
		if (human.hp > hpBefore || mentalGain > 0f)
			LogHelper.Log(LogHelper.GAME, $"[후퇴] {human.name} 던전 밖 회복 — HP {hpBefore:F0}→{human.hp:F0}/{human.maxHp:F0}, 정신력 {MentalMath.ToRatioPercent(human.BaseStat.mental - mentalGain):F0}%→{MentalMath.ToRatioPercent(human.BaseStat.mental):F0}%");
	}

	// ─────────────────────────── 관찰·전이 틱 ───────────────────────────

	// 유닛 OnUpdate가 매 프레임 부르는 틱 — 꺼져 있으면 즉시 반환, 켜져 있어도 0.5초 스로틀. 파티 구동 유닛이 파티 후퇴 시작·단계 전이를 맡는다.
	public static void Tick(Human human, float now)
	{
		if (!Enabled || human == null || human.hp <= 0f) return;
		var state = human.Retreat;
		if (state == null || now < state.NextObserveTime) return;
		state.NextObserveTime = now + ObserveIntervalSeconds;

		EvaluatePersonalRetreat(human, now);
		FleeSystem.TickWitnessing(human);
		var party = human.party;
		if (party != null && party.RetreatDriver == human) PartyRetreatSystem.Tick(party, now);
	}

	// 리더 없는 인류의 개인 후퇴(명세서 1-4장): 진행 중인 후퇴·확정된 도주가 없을 때 개인 HP ≤20%면 시작. 파티 평균은 쓰지 않고 정신력 비율만으로는 시작하지 않는다. 시작한 뒤에는 HP가 회복돼도 출구 이동을 유지한다. 시작했으면 true.
	public static bool EvaluatePersonalRetreat(Human human, float now)
	{
		var state = human.Retreat;
		if (state == null || state.Mode != RetreatMode.None) return false; // 진행 중인 후퇴·확정된 도주는 유지
		if (human.currentFloor < 1) return false;                           // 던전 밖
		if (HasValidLeader(human)) return false;                           // 리더가 있으면 파티 평균 기준
		if (human.party != null && human.party.Retreat.IsRetreating) return false; // 리더가 죽어도 이미 시작한 파티 후퇴는 목적지·절차를 유지

		float hpPercent = PartyRetreatMath.PercentOf(human.hp, human.maxHp);
		if (!PartyRetreatMath.ShouldStartPersonalRetreat(hpPercent)) return false;

		state.Mode = RetreatMode.PersonalRetreat;
		state.ModeSince = now;
		state.Move.Reset();
		PartyRetreatSystem.InterruptActivities(human);
		LogHelper.Log(LogHelper.GAME, $"[후퇴] {human.name} 개인 후퇴 시작 — 리더 없음, HP {hpPercent:F1}% ≤ {PartyRetreatMath.PersonalRetreatHpPercent:F0}% (출구로 이동, 거점 휴식 없음)");
		SpeechSystem.Say(human, SpeechId.PersonalRetreat);
		return true;
	}
}
