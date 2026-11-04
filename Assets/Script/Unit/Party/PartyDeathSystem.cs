using System.Collections.Generic;
using UnityEngine;

// Human/Party/Knowledge를 직접 건드리는 부수효과 호출부. 호출 지점: GameSession.RemoveDeadUnit(직접
// 목격), UnitFunction.CastRay(나중에 시체 발견), TacticalFSMState.InvestigatePerform(조사로 원인 확인).
// "사망 순간 직접 목격"은 전파가 아니라 시야 기반이라 VisionMath.ViewDistance를 쓴다(PropagateFrom의
// 전파 범위 조건과 혼동 주의). 사망 정보는 전투 중에도 즉시 주고받으며, 막히는 건 CanJoinDeathSearch가
// 담당하는 "경계 수색 전환"뿐이다.
public static class PartyDeathSystem
{
	// ─────────────────────────── 4-12/4-14장: 사망 순간 직접 목격 ───────────────────────────
	// GameSession.RemoveDeadUnit이 인류 사망 시 호출한다. dead는 아직 Destroy 전이라(이 프레임 끝에
	// Destroy) 위치/방향/lastAttacker를 안전하게 읽을 수 있다.
	public static void OnPartyMemberDied(Human dead, string corpseObjectId) => OnPartyMemberDied(dead, corpseObjectId, dead.Session?.parties);

	// parties: 세션의 파티 목록(없으면 null) — 상위 지휘관 사망을 같은 원정의 다른 파티에 전파할 때만 쓴다. 테스트가 세션 없이 목록을 직접 넘긴다.
	internal static void OnPartyMemberDied(Human dead, string corpseObjectId, IReadOnlyList<Party> parties)
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
			// 지휘 상실 처리(GameSession.CheckPartyWaveState)보다 먼저 불리므로 아직 Leader가 죽은 유닛이다. 상위 지휘관의 사망도 같은 충격이다(18-1장 "자기 리더 또는 자신에게 적용되는 상위 지휘관") — 상위 지휘관 사망은 같은 원정의 다른 소속 파티에도 전파된다(CollectAffectedMembers).
			DeadWasLeader = LeaderCommandSettings.Enabled && (party.Leader == dead || (dead.LeaderState != null && dead.LeaderState.IsSuperiorCommander)),
			DeadWasSuperior = LeaderCommandSettings.Enabled && dead.LeaderState != null && dead.LeaderState.IsSuperiorCommander,
			OwnerParty = party,
		};
		party.DeathRecords[corpseObjectId] = record;

		Human representative = null;
		float repDist = float.MaxValue;

		var affected = new List<Human>();
		CollectAffectedMembers(record, parties, affected);
		foreach (var m in affected)
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
			PropagateFrom(record, parties, dead, representative);

		if (!record.CauseConfirmed)
			TriggerUnknownCauseSearch(party, record, corpseObjectId);
	}

	// ─────────────────────────── 4-12/4-13장: 나중에 시체를 발견한 경우 ───────────────────────────
	// UnitFunction.CastRay가 시체(Corpse+Human 태그)를 처음 정확 인지한 순간 호출한다.
	public static void OnCorpseDiscovered(Human discoverer, InteractableObject corpse) => OnCorpseDiscovered(discoverer, corpse, discoverer.Session?.parties);

	internal static void OnCorpseDiscovered(Human discoverer, InteractableObject corpse, IReadOnlyList<Party> parties)
	{
		if (corpse == null || !corpse.Tags.Contains("Human")) return; // 몬스터 시체는 이 시스템 대상 아님
		var party = ResolveOwnerParty(discoverer, corpse, parties);
		if (party == null) return;
		if (!party.DeathRecords.TryGetValue(corpse.Id, out var record)) return; // 정상 흐름이면 항상 존재
		if (record.InfoKnownUnits.Contains(discoverer.name)) return; // 이미 앎(직접목격 등) — 중복 없음

		ApplyDeathInfo(record, discoverer, mentalDelta: -ExplorationMath.DeathDirectDiscoveryMentalLoss, isDirectDiscovery: true);
		TryConfirmCauseByWitness(record, discoverer);
		TryConfirmTrapCause(record);
		PropagateFrom(record, parties, deadUnitPositionOwner: null, representative: discoverer);

		if (!record.CauseConfirmed && !record.UnknownCauseSearchTriggered)
			TriggerUnknownCauseSearch(party, record, corpse.Id);
	}

	// ─────────────────────────── 5-2장: 시체 조사로 사망 원인 확인 ───────────────────────────
	// TacticalFSMState.InvestigatePerform이 Corpse+Human 오브젝트의 조사를 완료한 직후 호출한다.
	public static void OnCorpseInvestigated(Human investigator, InteractableObject corpse)
	{
		if (corpse == null || !corpse.Tags.Contains("Human")) return;
		var party = ResolveOwnerParty(investigator, corpse, investigator.Session?.parties);
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
		// 정확 인지 즉시 전투 상태 우선순위(CombatFSMState)가 자연히 넘겨받으므로 여기선 레코드만 정리한다.
		observer.currentAlertSearch = null;
	}

	// ─────────────────────────── 내부 헬퍼 ───────────────────────────

	// 시체의 OwnerPartyId로 원본 파티를 찾는다 — 발견자가 그 파티 소속이 아닌 경우까지 대비해 GameSession.parties에서 직접 찾는다.
	private static Party ResolveOwnerParty(Human discoverer, InteractableObject corpse, IReadOnlyList<Party> parties)
	{
		if (string.IsNullOrEmpty(corpse.OwnerPartyId)) return discoverer.party;
		if (discoverer.party != null && discoverer.party.Id == corpse.OwnerPartyId) return discoverer.party;
		if (parties == null) return discoverer.party;
		foreach (var p in parties)
			if (p != null && p.Id == corpse.OwnerPartyId) return p;
		return discoverer.party;
	}

	// 이 사망 사건의 정보를 주고받는 사람들 — 죽은 유닛의 파티원 전체, 상위 지휘관 사망이면 같은 원정의 다른 소속 파티원까지(16-2·18-1장 "자신에게 적용되는 상위 지휘관"). 점령 파티와 이미 끝난 파티는 원정이 아니다.
	private static void CollectAffectedMembers(PartyDeathRecord record, IReadOnlyList<Party> parties, List<Human> into)
	{
		into.Clear();
		var owner = record.OwnerParty;
		if (owner == null) return;
		foreach (var m in owner.Members) if (m != null) into.Add(m);
		if (!record.DeadWasSuperior || parties == null) return;
		foreach (var p in parties)
		{
			if (p == null || p == owner || p.WaveEnded || !ExpeditionCommandSystem.InSameExpedition(owner, p)) continue;
			foreach (var m in p.Members) if (m != null) into.Add(m);
		}
	}

	// 리더 사망 효과(정신력 −20/−10·능력치 충격)가 이 사람에게 적용되는가 — 죽은 리더의 파티원이거나, 상위 지휘관이었다면 같은 원정의 소속 파티원이다. 관련 없는 다른 파티는 일반 사실 정보만 기록한다(18-2장): 시체를 봐도 일반 사망 감소(−10)뿐이고 충격은 없다.
	internal static bool LeaderDeathApplies(PartyDeathRecord record, Human receiver)
	{
		if (!record.DeadWasLeader || receiver == null || receiver.party == null || record.OwnerParty == null) return false;
		if (receiver.party == record.OwnerParty) return true;
		return record.DeadWasSuperior && ExpeditionCommandSystem.InSameExpedition(record.OwnerParty, receiver.party);
	}

	private static void ApplyDeathInfo(PartyDeathRecord record, Human unit, float mentalDelta, bool isDirectDiscovery)
	{
		if (record.InfoKnownUnits.Contains(unit.name)) return;
		record.InfoKnownUnits.Add(unit.name);
		// 리더 사망은 일반 사망 감소에 더하지 않고 기본 감소량을 대체한다 — 직접 확인 −20 / 전달받음 −10, 이후 기존 정신력 보정(ApplyChange)을 적용한다(18-2장).
		bool leaderEffect = LeaderDeathApplies(record, unit);
		if (leaderEffect) mentalDelta = -LeaderMath.LeaderDeathMentalLoss(isDirectDiscovery);
		MentalSystem.ApplyChange(unit, mentalDelta); // mentalDelta는 음수 — 정신력 변경 단일 진입점
		if (leaderEffect) DeathShockSystem.Apply(unit); // 능력치 −20%·10초 — 처음 안 때부터 파티원별로 따로 센다(17-표1·18-1장)
		if (isDirectDiscovery && !leaderEffect) SpeechSystem.Say(unit, SpeechId.AllyDeathSeen); // 연출 — 직접 확인한 사람만 반응한다(전파 수신은 말하지 않음). 리더 사망은 사망 순간 가장 가까운 파티원 한 명이 알리므로(LeaderSuccessionSystem.OnLeaderLost) 발견자마다 따로 외치지 않는다
	}

	private static void TryConfirmCauseByWitness(PartyDeathRecord record, Human witness)
	{
		if (record.CauseConfirmed || record.CauseMonster == null || record.CauseMonster.hp <= 0) return;
		if (Vector2Int.Distance(witness.position, record.CauseMonster.position) > VisionMath.ViewDistance(witness.spotting)) return;

		record.CauseConfirmed = true;
		ApplyDangerOnce(record, witness, EventId.E_HUMAN_KILL_SEEN, InfoType.DirectWitness);
	}

	// 함정 원인은 확인만 처리한다 — E_HUMAN_KILL_SEEN/INDIRECT는 "사망 원인이 몬스터로 확인되면"에만 적용되므로 DangerApplied는 건드리지 않는다.
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

	// 사망 시점과 발견 시점 호출을 함께 지원한다 — 둘 다 "대표 발견자의 전파 범위 안 파티원에게 최초 전파"라는 동일 로직이라 하나로 묶었다.
	private static void PropagateFrom(PartyDeathRecord record, IReadOnlyList<Party> parties, Human deadUnitPositionOwner, Human representative)
	{
		if (representative == null) return;
		var affected = new List<Human>();
		CollectAffectedMembers(record, parties, affected);
		foreach (var m in affected)
		{
			if (m == null || m.hp <= 0 || m == representative || (deadUnitPositionOwner != null && m == deadUnitPositionOwner)) continue;
			if (record.InfoKnownUnits.Contains(m.name)) continue;
			if (!PropagationSystem.InPropagationRange(representative, m)) continue;
			ApplyDeathInfo(record, m, mentalDelta: -ExplorationMath.DeathPropagationMentalLoss, isDirectDiscovery: false);
		}
	}

	// 최초 전파 시점 스냅샷이 아니라 지속 재전파 — UnitFunction.OnUpdate 틱마다 인류마다 호출해, 아직
	// 모르는 파티원이 나중에 전파 범위 안으로 들어오면 그때 정보를 받게 한다.
	// 자기 파티의 사망 기록에 더해, 같은 원정의 다른 파티에서 상위 지휘관이 죽은 기록도 받는다(그 사망은 원정 전체에 적용된다).
	public static void TickOngoingPropagation(Human human) => TickOngoingPropagation(human, human.Session?.parties);

	private static readonly List<Human> _carrierScratch = new List<Human>(); // 틱마다 인류마다 불리므로 목록을 재사용한다

	internal static void TickOngoingPropagation(Human human, IReadOnlyList<Party> parties)
	{
		var party = human.party;
		if (party == null) return;

		if (party.DeathRecords.Count > 0)
			foreach (var record in party.DeathRecords.Values) TryReceiveFromCarrier(human, record, parties);

		if (parties == null) return;
		foreach (var other in parties)
		{
			if (other == null || other == party || other.DeathRecords.Count == 0 || !ExpeditionCommandSystem.InSameExpedition(other, party)) continue;
			foreach (var record in other.DeathRecords.Values)
				if (record.DeadWasSuperior) TryReceiveFromCarrier(human, record, parties);
		}
	}

	private static void TryReceiveFromCarrier(Human human, PartyDeathRecord record, IReadOnlyList<Party> parties)
	{
		if (record.InfoKnownUnits.Contains(human.name)) return;

		CollectAffectedMembers(record, parties, _carrierScratch);
		foreach (var carrier in _carrierScratch)
		{
			if (carrier == null || carrier == human || carrier.hp <= 0) continue;
			if (!record.InfoKnownUnits.Contains(carrier.name)) continue;
			if (!PropagationSystem.InPropagationRange(carrier, human)) continue;
			ApplyDeathInfo(record, human, mentalDelta: -ExplorationMath.DeathPropagationMentalLoss, isDirectDiscovery: false);
			break;
		}
	}

	// ─────────────────────────── 4-15장: 원인미상 파티원 사망 수색 트리거 ───────────────────────────
	private static void TriggerUnknownCauseSearch(Party party, PartyDeathRecord record, string corpseObjectId)
	{
		if (record.UnknownCauseSearchTriggered) return;
		record.UnknownCauseSearchTriggered = true;

		Dir front = record.DeadFacing;
		Dir[] slots = { front, DirUtil.Opposite(front), DirUtil.RotateCW(front, -2), DirUtil.RotateCW(front, 2) };
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

	// 일반 탐색/대기/집결 이동 중인 유닛만 참여, 전투/조사/함정해제/다른 포메이션은 현재 행동을 유지한다.
	private static bool CanJoinDeathSearch(Human h)
	{
		if (h.currentTrapInteraction != null) return false;
		if (h.currentInvestigation != null) return false;
		if (h.currentFormation != null) return false;
		if (RetreatSystem.IsBoundToRetreat(h)) return false; // 4-15장 표: 도주 유닛은 불참 — 후퇴·도주 이동 중에는 추가 경계를 위해 멈추지 않고(후퇴도주 5-1장) 거점 휴식 중에도 수색으로 흩어지지 않는다
		if (h.personalSpottedEnemies.Count > 0) return false; // 전투 중 근사(명시적 전투 상태 플래그 부재)
		return true;
	}
}
