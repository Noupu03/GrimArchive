using System;
using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 개인 도주와 몬스터 직접 조우 충격(후퇴도주 문서 3~4장)의 부수효과 호출부. 순수 판정은 FleeRollMath/EncounterShockMath에 있다.
public static class FleeSystem
{
	// 이동 단계·UI가 구독하는 확정 통지.
	public static event Action<Human, FleeReason> OnFleeConfirmed;

	// 판정 순서(명세서 3-3장): ① 사건이 만든 실제 감소를 이미 적용 ② 사망 우선, 생존 유닛이 −100이면 즉시 도주 확정 ③ 공황 구간 실제 감소량을 누적해 5마다 1회 판정 ④ 버티면 다음 확률 +10%p. 회복은 다음 확률·미사용 감소량을 초기화한다(회복 그 자체로 판정하지 않는다).
	public static void OnMentalChanged(Human human, float before, float after)
	{
		var state = human.Retreat;
		if (state == null) return;

		if (after > before)
		{
			state.FleeRoll.OnRecovered();
			return;
		}
		if (after >= before || human.hp <= 0f || state.FleeRoll.Confirmed) return;
		SayOnStageWorsened(human, before, after);

		if (MentalMath.ReachedFloor(after))
		{
			ConfirmFlee(human, FleeReason.MentalFloor);
			return;
		}
		var outcome = FleeRollMath.Advance(state.FleeRoll, MentalMath.PanicZoneDecrease(before, after), RetreatSystem.Random01);
		if (outcome.Rolls > 0 && !outcome.Fled) SpeechSystem.Say(human, SpeechId.FleeSurvived); // 연출 — 도주 판정을 버텨냈다
		if (outcome.Fled) ConfirmFlee(human, FleeReason.RollSucceeded);
	}

	// 연출 — 정신력 단계가 악화되는 순간(긴장·공포·공황 진입)의 혼잣말. 같은 단계 안의 하락에는 말하지 않는다.
	static void SayOnStageWorsened(Human human, float before, float after)
	{
		MentalStage stage = MentalMath.GetStage(after);
		if (stage == MentalMath.GetStage(before)) return;
		switch (stage)
		{
			case MentalStage.Tension: SpeechSystem.Say(human, SpeechId.MentalTension); break;
			case MentalStage.Fear: SpeechSystem.Say(human, SpeechId.MentalFear); break;
			case MentalStage.Panic: SpeechSystem.Say(human, SpeechId.MentalPanic); break;
		}
	}

	// 개인 도주를 확정한다 — 한 번 확정되면 정신력 회복·리더 명령으로 취소하지 않고, 확정 즉시 파티 후퇴·휴식 평균에서 제외된다(Party.CollectRetreatAverageMembers). 리더에게 도주 사실을 자동 전달하지 않는다. 코어 잔해를 들고 있었다면 현재 타일에 내려놓는다(R41). 진행 중이던 조사·대기·함정 대응은 접는다(출구 이동이 집결·휴식·리더 명령보다 우선). 기절 중이면 이동은 기절이 끝난 뒤 시작한다(Unit.JudgeState가 stunDuration 동안 FSM을 막는다).
	public static void ConfirmFlee(Human human, FleeReason reason)
	{
		var state = human?.Retreat;
		if (state == null || state.IsFleeing) return;

		state.Mode = RetreatMode.PersonalFlee;
		state.ModeSince = Time.time;
		state.FleeRoll.Confirmed = true;
		state.Move.Reset();
		PartyRetreatSystem.InterruptActivities(human);
		human.Session?.Core?.Salvage.OnCarrierFled(human);
		LogHelper.Log(LogHelper.GAME, $"[후퇴] {human.name} 개인 도주 확정 — {Describe(reason)}, 정신력 {human.BaseStat.mental:F1}, 판정 {state.FleeRoll.TotalRolls}회");
		SpeechSystem.Say(human, reason == FleeReason.EncounterShock ? SpeechId.EncounterFleeMax : SpeechId.FleeConfirmed); // 연출 — 명세서 4-3·7-3장
		NotifyWitnesses(human);
		OnFleeConfirmed?.Invoke(human, reason);
	}

	static string Describe(FleeReason reason)
	{
		switch (reason)
		{
			case FleeReason.RollSucceeded: return "공황 구간 감소 판정 통과";
			case FleeReason.MentalFloor: return "정신력 −100 도달";
			case FleeReason.EncounterShock: return "몬스터 조우 충격(직전 공황)";
			default: return reason.ToString();
		}
	}

	// 도주가 확정된 순간 그 파티원을 직접 보고 있는 파티원은 정신력이 줄어든다(최초 직접 확인). 나중에 보게 되는 파티원은 TickWitnessing이 처리하고, 못 본 파티원은 모른다(숨은 위치·도주 사실을 리더가 자동으로 알지 못한다).
	static void NotifyWitnesses(Human fleeing)
	{
		var party = fleeing.party;
		if (party == null) return;
		foreach (var m in party.Members)
			if (m != null && m != fleeing && m.hp > 0f && m.visiblePartyMembers.Contains(fleeing))
				MentalSystem.OnFleeWitnessed(m, fleeing);
	}

	// 관찰 틱: 도주 중인 파티원을 이번에 처음 직접 본 인류의 정신력 감소(사건당 1회 집합이 중복을 막는다).
	public static void TickWitnessing(Human witness)
	{
		var party = witness.party;
		if (party == null || witness.visiblePartyMembers.Count == 0) return;
		foreach (var seen in witness.visiblePartyMembers)
			if (seen != null && seen != witness && seen.hp > 0f && seen.Retreat != null && seen.Retreat.IsFleeing)
				MentalSystem.OnFleeWitnessed(witness, seen);
	}

	// ─────────────────────────── 몬스터 직접 조우 충격 ───────────────────────────

	// 인류가 몬스터를 정확 인지(직접 시야·인지)한 순간의 호출 — 소리·보고·함정 확인·관찰자는 이 호출 지점이 아니므로 발동하지 않는다(R26). 기록된 몬스터 위험도는 이 인류의 개인 기록(GetPersonalDanger)이다.
	public static void OnMonsterSpotted(Human human, Unit monster)
	{
		if (!RetreatSystem.Enabled || EncounterShockMath.LevelToResponseStage == null) return; // Lv→대응 단계 표가 확정되기 전에는 격차를 계산하지 못한다 — 위험도 조회도 하지 않는다
		if (human == null || !(monster is Monster) || human.Retreat == null || human.Knowledge == null || monster.unitType == null) return;
		string key = monster.name; // 몬스터 개체 단위 이력 — 같은 종의 다른 개체는 따로 판정한다(명세서 4-3장)
		if (human.Retreat.Encounters.HandledMonsters.Contains(key)) return; // 정확 인지는 시야 패스마다 반복되므로 이미 판정한 개체는 위험도 조회도 건너뛴다
		float danger = human.Knowledge.GetPersonalDanger(human, monster);
		EvaluateEncounter(human, new[] { new KeyValuePair<string, float>(key, danger) });
	}

	// 직접 확인한 몬스터들의 (개체 키, 기록된 위험도)로 조우 충격을 판정해 적용한다. Lv→대응 단계 표(성장 설계)가 없으면 격차를 계산하지 못해 아무 일도 하지 않는다(EncounterShockMath.LevelToResponseStage). 적용한 효과를 돌려준다.
	public static EncounterShock EvaluateEncounter(Human human, IEnumerable<KeyValuePair<string, float>> seenMonsterDangers)
	{
		if (!RetreatSystem.Enabled || human == null || human.hp <= 0f || human.Retreat == null) return EncounterShock.None;

		var gaps = new List<KeyValuePair<string, int>>();
		foreach (var seen in seenMonsterDangers)
		{
			if (!EncounterShockMath.TryComputeGap(seen.Value, human.level, out int gap)) return EncounterShock.None; // 표 미확정 — 임의 계산 금지
			gaps.Add(new KeyValuePair<string, int>(seen.Key, gap));
		}

		var shock = human.Retreat.Encounters.Evaluate(gaps, human.BaseStat.mental, RetreatSystem.Gap2MentalDamage);
		ApplyEncounterShock(human, shock);
		return shock;
	}

	// 확정된 조우 충격 효과를 적용한다. 격차 +2는 일반 감소 보정을 받는 정신력 감소이고, +3 이상은 기절 1초와 −71/−100 강제 설정(−100이면 도주 즉시 확정, 이동은 기절 뒤)이다.
	public static void ApplyEncounterShock(Human human, in EncounterShock shock)
	{
		switch (shock.Kind)
		{
			case EncounterShockKind.MentalDamage:
				MentalSystem.ApplyChange(human, -shock.MentalDamage);
				SpeechSystem.Say(human, SpeechId.EncounterGap2);
				break;
			case EncounterShockKind.StunAndPanic:
			case EncounterShockKind.StunAndMax:
				// ApplyStun은 상태이상 가중치 이벤트를 남기므로 쓰지 않고 지속시간만 직접 건다.
				human.StatusEffects.State.stunDuration = Mathf.Max(human.StatusEffects.State.stunDuration, shock.StunSeconds);
				if (shock.Kind == EncounterShockKind.StunAndMax) ConfirmFlee(human, FleeReason.EncounterShock); // −100 설정이 낳는 MentalFloor 확정보다 먼저 — 원인을 조우 충격으로 남긴다
				MentalSystem.SetForced(human, shock.SetMentalTo);
				if (shock.Kind == EncounterShockKind.StunAndPanic) SpeechSystem.Say(human, SpeechId.EncounterPanic); // StunAndMax의 "도망쳐야 해!"는 ConfirmFlee가 말한다
				break;
		}
	}

	// 전투·추적이 끝나 조우 묶음이 종료됨(CombatFSMState.OnExit) — 개체 처리 이력은 유지한다.
	public static void EndEncounterBundle(Human human) => human?.Retreat?.Encounters.EndEncounterBundle();

	// 실제 던전 밖 이탈 후 다음 침공 — 도주 확률 50%·잔여 감소량 0·조우 충격 이력·도주 확정·후퇴 상태를 새로 시작한다. 새 방 진입·리더 교체로는 초기화하지 않는다.
	public static void ResetForNewInvasion(Human human) => human?.Retreat?.ResetForNewInvasion();
}
