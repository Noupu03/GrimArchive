using System.Collections.Generic;
using UnityEngine;

// 03문서 4-12~4-15장(2026-07-27 신규) 구현부 — 순수 계산이 아니라 Human/Party/Knowledge를 직접
// 건드리는 호출부라 WeightMath/ExplorationMath 같은 순수함수 모음과 달리 부수효과가 있다. 호출 지점:
//   - GameSession.RemoveDeadUnit: 인류 사망 시 OnPartyMemberDied(사망 순간 직접 목격 처리)
//   - UnitFunction.CastRay(오브젝트 인지 블록): OnCorpseDiscovered(나중에 시체를 발견한 경우)
//   - TacticalFSMState.InvestigatePerform: OnCorpseInvestigated(시체 조사로 사망 원인 확인)
//   - UnitFunction.CastRay(유닛 인지 블록): OnDeathSearchSpotted(원인미상 수색 중 몬스터 정확 인지)
// 2026-07-31: 07_전파·소리·간접입력 구현으로 전파(PropagateFrom)는 실제 전파 조건(같은 공간+카리스마
// 기반 전파 범위)을 쓴다. "사망 순간 직접 목격" 판정(TryConfirmCauseByWitness 등)은 전파가 아니라
// 시야 기반 목격이라 VisionMath.ViewDistance(spotting)를 그대로 유지한다(성격이 다름 — 혼동 주의).
// 2026-08-06(검증 중 발견): 07문서 8장 "사망 정보는 일반적인 비전투 전파 조건보다 사망 정보 규칙을
// 우선한다" + 03문서 4-12장 "현재 상태와 탐색 반응 우선순위에 관계없이 즉시 처리한다" — 그래서
// PropagateFrom/TickOngoingPropagation은 CanPropagate(비전투 조건 포함)가 아니라
// PropagationSystem.InPropagationRange(공간+범위만, 7-1/7-2와 동일 패턴)를 쓴다. 전투 중인 대표
// 발견자나 수신자도 사망 정보 자체는 즉시 주고받는다 — 막히는 건 "경계 수색으로 전환"뿐(그건 현재
// 행동 우선순위를 그대로 따름, TriggerUnknownCauseSearch의 CanJoinDeathSearch가 담당).
public static class PartyDeathSystem
{
	// ─────────────────────────── 4-12/4-14장: 사망 순간 직접 목격 ───────────────────────────
	// GameSession.RemoveDeadUnit이 인류 사망 시(시체 오브젝트 생성 직후) 호출한다. dead는 아직
	// Destroy되지 않은 상태(이 프레임 끝에 Destroy)라 위치/방향/lastAttacker를 안전하게 읽을 수 있다.
	public static void OnPartyMemberDied(Human dead, string corpseObjectId)
	{
		var party = dead.party;
		if (party == null) return;
		if (party.DeathRecords.ContainsKey(corpseObjectId)) return;

		var record = new PartyDeathRecord
		{
			DeadUnitName = dead.name,
			DeathPosition = new Vector3Int(dead.position.x, dead.position.y, dead.currentFloor),
			DeadFacing = dead.currentDir,
			CauseMonster = dead.lastAttacker,
			CauseTrap = dead.lastTrapAttacker,
			IncidentId = System.Guid.NewGuid().ToString(),
		};
		party.DeathRecords[corpseObjectId] = record;

		Human representative = null;
		float repDist = float.MaxValue;

		foreach (var m in party.Members)
		{
			if (m == null || m == dead || m.hp <= 0) continue;
			float d = Vector2Int.Distance(m.position, dead.position);
			if (d > VisionMath.ViewDistance(m.spotting)) continue; // 사망 순간 시야 밖 — 목격 아님

			if (d < repDist) { repDist = d; representative = m; }
			ApplyDeathInfo(record, m, mentalDelta: -ExplorationMath.DeathDirectDiscoveryMentalLoss, isDirectDiscovery: true);
			TryConfirmCauseByWitness(record, m);
			TryConfirmTrapCause(record);
		}

		if (representative != null)
			PropagateFrom(party, record, dead, representative);

		if (!record.CauseConfirmed)
			TriggerUnknownCauseSearch(party, record, corpseObjectId);
	}

	// ─────────────────────────── 4-12/4-13장: 나중에 시체를 발견한 경우 ───────────────────────────
	// UnitFunction.CastRay가 시체(Corpse+Human 태그)를 처음 정확 인지한 순간(firstTouch &&
	// AccuratePerception) 호출한다.
	public static void OnCorpseDiscovered(Human discoverer, InteractableObject corpse)
	{
		if (corpse == null || !corpse.Tags.Contains("Human")) return; // 몬스터 시체는 이 시스템 대상 아님
		var party = ResolveOwnerParty(discoverer, corpse);
		if (party == null) return;
		if (!party.DeathRecords.TryGetValue(corpse.Id, out var record)) return; // 정상 흐름이면 항상 존재
		if (record.InfoKnownUnits.Contains(discoverer.name)) return; // 이미 앎(직접목격 등) — 중복 없음

		ApplyDeathInfo(record, discoverer, mentalDelta: -ExplorationMath.DeathDirectDiscoveryMentalLoss, isDirectDiscovery: true);
		TryConfirmCauseByWitness(record, discoverer);
		TryConfirmTrapCause(record);
		PropagateFrom(party, record, deadUnitPositionOwner: null, representative: discoverer);

		if (!record.CauseConfirmed && !record.UnknownCauseSearchTriggered)
			TriggerUnknownCauseSearch(party, record, corpse.Id);
	}

	// ─────────────────────────── 5-2장: 시체 조사로 사망 원인 확인 ───────────────────────────
	// TacticalFSMState.InvestigatePerform이 Corpse+Human 오브젝트의 조사를 완료한 직후 호출한다.
	public static void OnCorpseInvestigated(Human investigator, InteractableObject corpse)
	{
		if (corpse == null || !corpse.Tags.Contains("Human")) return;
		var party = ResolveOwnerParty(investigator, corpse);
		if (party == null) return;
		if (!party.DeathRecords.TryGetValue(corpse.Id, out var record)) return;
		if (record.CauseConfirmed) return;
		if (TryConfirmTrapCause(record)) return; // 4-14장: 함정 원인은 위험도 이벤트 없이 확인만 된다.
		if (record.CauseMonster == null || record.CauseMonster.hp <= 0) return;

		record.CauseConfirmed = true;
		ApplyDangerOnce(record, investigator, EventId.E_HUMAN_KILL_INDIRECT, InfoType.Indirect);
	}

	// ─────────────────────────── 4-15장: 원인미상 수색 중 몬스터 정확 인지 ───────────────────────────
	// UnitFunction.CastRay가 유닛을 정확 인지한 직후(AddPersonalSpottedEnemy와 같은 지점) 호출한다.
	public static void OnDeathSearchSpotted(Human observer, Unit spotted)
	{
		var alert = observer.currentAlertSearch;
		if (alert == null || !alert.IsDeathSearch || alert.DeathRecordCorpseId == null) return;
		var party = observer.party;
		if (party == null || !party.DeathRecords.TryGetValue(alert.DeathRecordCorpseId, out var record))
		{
			observer.currentAlertSearch = null;
			return;
		}

		if (!record.CauseConfirmed && record.CauseMonster == spotted && spotted != null && spotted.hp > 0)
		{
			record.CauseConfirmed = true;
			ApplyDangerOnce(record, observer, EventId.E_HUMAN_KILL_INDIRECT, InfoType.Indirect);
		}
		// 원인 몬스터든 다른 적이든, 정확 인지 즉시 전투 상태 우선순위(CombatFSMState)가 자연히
		// 넘겨받으므로 경계 수색 레코드는 여기서 정리만 해준다(12-2장 "전투" 전환).
		observer.currentAlertSearch = null;
	}

	// ─────────────────────────── 내부 헬퍼 ───────────────────────────

	// 시체의 OwnerPartyId로 원본 파티를 찾는다(발견자가 그 파티 소속이 아닌 경우까지 대비 —
	// 지금 게임엔 웨이브당 인류 파티가 보통 하나뿐이라 discoverer.party와 같은 경우가 대부분이지만,
	// 다른 파티가 발견하는 미래 시나리오에도 안전하도록 GameSession.parties에서 직접 찾는다).
	private static Party ResolveOwnerParty(Human discoverer, InteractableObject corpse)
	{
		if (string.IsNullOrEmpty(corpse.OwnerPartyId)) return discoverer.party;
		if (discoverer.party != null && discoverer.party.Id == corpse.OwnerPartyId) return discoverer.party;
		if (discoverer.Session == null) return discoverer.party;
		foreach (var p in discoverer.Session.parties)
			if (p != null && p.Id == corpse.OwnerPartyId) return p;
		return discoverer.party;
	}

	private static void ApplyDeathInfo(PartyDeathRecord record, Human unit, float mentalDelta, bool isDirectDiscovery)
	{
		if (record.InfoKnownUnits.Contains(unit.name)) return;
		record.InfoKnownUnits.Add(unit.name);
		unit.BaseStat.mental += mentalDelta; // mentalDelta는 음수(-10 또는 -5)
	}

	private static void TryConfirmCauseByWitness(PartyDeathRecord record, Human witness)
	{
		if (record.CauseConfirmed || record.CauseMonster == null || record.CauseMonster.hp <= 0) return;
		if (Vector2Int.Distance(witness.position, record.CauseMonster.position) > VisionMath.ViewDistance(witness.spotting)) return;

		record.CauseConfirmed = true;
		ApplyDangerOnce(record, witness, EventId.E_HUMAN_KILL_SEEN, InfoType.DirectWitness);
	}

	// 4-14장: 함정이 원인으로 확인되면(직접 목격/전파/조사 — 호출부가 이미 "확인 가능한 상황"임을
	// 보장) 원인 확인만 처리한다. 이 이벤트 테이블(E_HUMAN_KILL_SEEN/INDIRECT)은 "사망 원인이
	// 몬스터로 확인되면"에만 적용되므로 함정 원인은 DangerApplied를 건드리지 않는다 — 확인됐으니
	// 4-15장 원인미상 수색만 더 이상 트리거되지 않게 막는 역할이다. 반환값은 "이 호출로 확정됐는지"라
	// 호출부가 몬스터 확인 로직으로 이어갈지 판단하는 데 쓴다.
	private static bool TryConfirmTrapCause(PartyDeathRecord record)
	{
		if (record.CauseConfirmed || record.CauseTrap == null) return false;
		record.CauseConfirmed = true;
		return true;
	}

	private static void ApplyDangerOnce(PartyDeathRecord record, Human observer, EventId eventId, InfoType infoType)
	{
		if (record.DangerApplied || record.CauseMonster == null || record.CauseMonster.hp <= 0 || observer.Knowledge == null) return;
		observer.Knowledge.RecordEvent(eventId, observer, record.CauseMonster, infoType, record.IncidentId);
		record.DangerApplied = true;
	}

	// 사망 시점(deadUnitPositionOwner!=null, 죽은 유닛 본인은 제외)과 발견 시점(null) 호출을 함께
	// 지원한다 — 둘 다 "대표 발견자의 전파 범위 안 파티원에게 최초 전파(-5)"라는 동일 로직이라 하나로 묶었다.
	private static void PropagateFrom(Party party, PartyDeathRecord record, Human deadUnitPositionOwner, Human representative)
	{
		if (representative == null) return;
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0 || m == representative || (deadUnitPositionOwner != null && m == deadUnitPositionOwner)) continue;
			if (record.InfoKnownUnits.Contains(m.name)) continue;
			if (!PropagationSystem.InPropagationRange(representative, m)) continue;
			ApplyDeathInfo(record, m, mentalDelta: -ExplorationMath.DeathPropagationMentalLoss, isDirectDiscovery: false);
		}
	}

	// 07문서 9장(정보 동기화)/03문서 4-13장(2026-07-31 신규): 최초 전파 시점의 스냅샷 범위 체크뿐이던
	// 것을 실제 지속 재전파로 교체 — UnitFunction.OnUpdate의 기존 0.1초 틱에서 인류마다 호출한다. 아직
	// 정보를 모르는 파티원이, 이미 아는 파티원("정보 보유자")의 전파 범위 안으로 나중에 들어오면 그
	// 시점에 정보를 받는다.
	public static void TickOngoingPropagation(Human human)
	{
		var party = human.party;
		if (party == null || party.DeathRecords.Count == 0) return;

		foreach (var record in party.DeathRecords.Values)
		{
			if (record.InfoKnownUnits.Contains(human.name)) continue;

			foreach (var carrier in party.Members)
			{
				if (carrier == null || carrier == human || carrier.hp <= 0) continue;
				if (!record.InfoKnownUnits.Contains(carrier.name)) continue;
				if (!PropagationSystem.InPropagationRange(carrier, human)) continue;
				ApplyDeathInfo(record, human, mentalDelta: -ExplorationMath.DeathPropagationMentalLoss, isDirectDiscovery: false);
				break;
			}
		}
	}

	// ─────────────────────────── 4-15장: 원인미상 파티원 사망 수색 트리거 ───────────────────────────
	private static void TriggerUnknownCauseSearch(Party party, PartyDeathRecord record, string corpseObjectId)
	{
		if (record.UnknownCauseSearchTriggered) return;
		record.UnknownCauseSearchTriggered = true;

		Dir front = record.DeadFacing;
		Dir[] slots = { front, Opposite(front), RotateCW(front, -2), RotateCW(front, 2) };
		int slotIndex = 0;
		Vector2Int origin = new Vector2Int(record.DeathPosition.x, record.DeathPosition.y);

		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0 || !record.InfoKnownUnits.Contains(m.name)) continue;
			if (!CanJoinDeathSearch(m)) continue; // 4-15장 표: 전투/도주/조사/함정/포메이션 유지 유닛은 불참
			m.currentAlertSearch = new AlertSearchState
			{
				IsDeathSearch = true,
				DeathSearchOrigin = origin,
				AssignedSearchDir = slots[slotIndex % slots.Length],
				DeathRecordCorpseId = corpseObjectId,
			};
			slotIndex++;
		}
	}

	// 4-15장 표: 일반 탐색/대기/집결 이동 중인 유닛만 참여, 전투/도주후퇴/조사/함정해제/다른 포메이션은
	// 현재 행동 유지(정보·정신력만 적용, 위에서 이미 처리됨). 도주·후퇴는 06문서 부재로 해당 상태 자체가
	// 없어 자동으로 충족된다(기존 관례).
	private static bool CanJoinDeathSearch(Human h)
	{
		if (h.currentTrapInteraction != null) return false;
		if (h.currentInvestigation != null) return false;
		if (h.currentFormation != null) return false;
		if (h.personalSpottedEnemies.Count > 0) return false; // 전투 중 근사(명시적 전투 상태 플래그 부재)
		return true;
	}

	private static Dir Opposite(Dir d) => (Dir)(((int)d + 4) % 8);
	private static Dir RotateCW(Dir d, int steps) => (Dir)(((int)d + steps + 8) % 8);
}
