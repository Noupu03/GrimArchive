using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

// 07_전파·소리·간접입력 구현부(부수효과 있는 호출부) — PropagationMath(순수 계산)를 게임 루프에
// 연결한다. 없는 문서(리더·명령/목표·경로/전투반응)가 위임한 부분(물리적 전파 이동, 합류 대기 초과 처리 등)은 가장 단순한 기본값으로 스텁 처리한다.
public static class PropagationSystem
{
	// ═══════════════════════════ 소리 이벤트 ═══════════════════════════
	// 소리는 발생한 순간에만 존재하는 1회성 사건이다 — EmitSound 호출 시점에 범위 스캔까지 끝내 그
	// 순간 조건을 만족한 감지자에게만 통지한다(나중에 들어온 유닛은 못 받음). "유효시간"은 소리
	// 수명이 아니라 감지자별 확인 행동 유예시간(UniTask 타이머로 개별 적용)이다.
	public readonly struct SoundPerceivedEvent
	{
		// 07문서 1장: 소리 감지는 인류/몬스터 공통이라 Observer는 Unit이다(전파 자체는 인류 전용).
		public readonly Unit Observer;
		public readonly SoundType Type;
		public readonly Vector2Int Position;
		public readonly int FloorIndex;
		public readonly Unit Victim; // 피격 발생 공격음/피격 비명일 때만 의미 있음(= 피격당한 유닛)
		public readonly Unit Attacker;
		public readonly bool IsHeavyHit;
		public readonly string IncidentId;

		public SoundPerceivedEvent(Unit observer, SoundType type, Vector2Int position, int floorIndex,
			Unit victim, Unit attacker, bool isHeavyHit, string incidentId)
		{
			Observer = observer;
			Type = type;
			Position = position;
			FloorIndex = floorIndex;
			Victim = victim;
			Attacker = attacker;
			IsHeavyHit = isHeavyHit;
			IncidentId = incidentId;
		}
	}

	// 공개 Observable — 실제 게임 로직(PendingSound 갱신)은 정적 생성자에서 내부적으로 구독해 다른 구독자 존재 여부와 무관하게 핵심 동작이 보장된다.
	public static readonly Subject<SoundPerceivedEvent> OnSoundPerceived = new Subject<SoundPerceivedEvent>();

	// OnSoundPerceived와 달리 아무도 감지 못 했어도 무조건 1회 발행된다 — PropagationDebugVisualizer 전용 디버그 채널.
	public readonly struct SoundEmittedEvent
	{
		public readonly SoundType Type;
		public readonly Vector2Int Position;
		public readonly int FloorIndex;
		public readonly int RangeTiles; // 이 소리가 도달하는 기준 반경(14장 소리별 기본 범위)

		public SoundEmittedEvent(SoundType type, Vector2Int position, int floorIndex, int rangeTiles)
		{
			Type = type;
			Position = position;
			FloorIndex = floorIndex;
			RangeTiles = rangeTiles;
		}
	}
	public static readonly Subject<SoundEmittedEvent> OnSoundEmitted = new Subject<SoundEmittedEvent>();

	static PropagationSystem()
	{
		OnSoundPerceived.Subscribe(HandleSoundPerceived);
	}

	// ═══════════════════════════ 방별 소리 감지자 인덱스 (스캔 최적화) ═══════════════════════════
	// EmitSound가 session.units 전체 대신 소리 발생 방의 유닛만 스캔하도록 좁힌다(GameSession.
	// RegisterUnitPos/UnregisterUnitPos가 함께 유지해 별도 폴링 불필요). roomId는 층마다 재사용돼 (층, roomId)를 키로 쓴다.
	private static readonly Dictionary<(int Floor, int RoomId), HashSet<Unit>> _listenersByRoom = new Dictionary<(int, int), HashSet<Unit>>();
	private static readonly Dictionary<Unit, (int Floor, int RoomId)> _listenerRoomKey = new Dictionary<Unit, (int, int)>();
	private static readonly List<Unit> _scanBuffer = new List<Unit>();

	// GameSession.RegisterUnitPos가 유닛 등록/이동 시마다 호출한다. roomId < 0(맵 밖 등)이면 인덱스에서 빠진다.
	public static void UpdateListenerRoomIndex(Unit unit, int floorIndex, int roomId)
	{
		if (unit == null) return;
		var newKey = (floorIndex, roomId);
		if (_listenerRoomKey.TryGetValue(unit, out var oldKey))
		{
			if (oldKey == newKey) return;
			if (_listenersByRoom.TryGetValue(oldKey, out var oldSet)) oldSet.Remove(unit);
		}
		if (roomId < 0) { _listenerRoomKey.Remove(unit); return; }

		if (!_listenersByRoom.TryGetValue(newKey, out var set))
		{
			set = new HashSet<Unit>();
			_listenersByRoom[newKey] = set;
		}
		set.Add(unit);
		_listenerRoomKey[unit] = newKey;
	}

	// GameSession.UnregisterUnitPos(사망/제거 시)가 호출한다.
	public static void RemoveFromRoomIndex(Unit unit)
	{
		if (unit == null) return;
		if (_listenerRoomKey.TryGetValue(unit, out var key))
		{
			if (_listenersByRoom.TryGetValue(key, out var set)) set.Remove(unit);
			_listenerRoomKey.Remove(unit);
		}
	}

	// 14장: 이동/공격 실행/피격/사망/함정 작동 시 각각 호출한다. attacker/isHeavyHit/incidentId는
	// 피격 발생 공격음·피격 비명 전용(E_HIT_HEAVY_INDIRECT 연결용) — 다른 소리 종류는 기본값을 넘긴다.
	public static void EmitSound(GameSession session, SoundType type, Vector2Int position, int floorIndex, Unit source,
		Unit attacker = null, bool isHeavyHit = false, string incidentId = null)
	{
		if (session == null || session.cmap == null) return;
		CreateMap cmap = session.cmap;
		int roomId = cmap.GetRoomIdAt(floorIndex, position);
		if (roomId < 0) return;

		// 감지 성공 여부와 무관하게 "소리가 발생했다" 자체는 항상 알린다(디버그 시각화 전용).
		OnSoundEmitted.OnNext(new SoundEmittedEvent(type, position, floorIndex, PropagationMath.SoundBaseRange(type)));

		if (!_listenersByRoom.TryGetValue((floorIndex, roomId), out var candidates) || candidates.Count == 0) return;

		// 스캔 도중 인덱스가 바뀔 일은 없지만 방어적으로 스냅샷해서 순회한다 — 재사용 버퍼라 매 호출 GC 없음.
		_scanBuffer.Clear();
		_scanBuffer.AddRange(candidates);

		foreach (var listener in _scanBuffer)
		{
			if (listener == null || listener.hp <= 0 || listener == source) continue;
			if (listener.currentFloor != floorIndex) continue; // 인덱스 정합성 방어 — 이론상 항상 참
			// 16-2장: 자신/아군의 일반 이동음은 제외한다(전투 관련 소리는 예외 없음). "아군"은 종족 단위로 근사한다.
			bool sourceIsHuman = source is Human;
			bool listenerIsHuman = listener is Human;
			if (type == SoundType.Movement && sourceIsHuman == listenerIsHuman) continue;
			if (!listener.CanPerceive) continue; // 기절 등 인지 판정 불가 상태 — 못 듣는다고 근사
			if (IsSoundUnresponsive(listener, type)) continue;

			bool isAlert = listener.Perception.IsAlert;
			int detectRange = PropagationMath.SoundDetectionRange(type, listener.spotting, isAlert, isMonster: !listenerIsHuman);
			// 07-A 1장: 소리도 전파처럼 벽 우회 최단경로를 써야 한다 — GetRoomIdAt==roomId로 같은 공간 안에서만 허용해 게이트 밖으로 새지 않게 한다.
			bool reachable = PropagationMath.TryGetSpaceDistance(position, listener.position, detectRange,
				p => cmap.IsStaticTileWalkable(floorIndex, p) && cmap.GetRoomIdAt(floorIndex, p) == roomId, out _);
			if (!reachable) continue;

			OnSoundPerceived.OnNext(new SoundPerceivedEvent(listener, type, position, floorIndex, source, attacker, isHeavyHit, incidentId));
		}
	}

	// ═══════════════════════════ 소리 감지 반응 (16장) ═══════════════════════════

	// OnSoundPerceived 내부 구독자 — 감지 시 1회만 호출되며(매 틱 재평가 없음), "지금 저장된 PendingSound"와 비교해 가장 급한 소리만 남는다.
	private static void HandleSoundPerceived(SoundPerceivedEvent e)
	{
		Unit listener = e.Observer;
		if (listener == null || listener.hp <= 0) return;
		DiscardInterruptedSoundReaction(listener);

		int rank = PropagationMath.SoundPriorityRank(e.Type);
		float dist = Vector2Int.Distance(listener.position, e.Position);

		var pending = listener.Propagation.PendingSound;
		if (pending != null && !pending.ResponseStarted && Time.time <= pending.ValidUntilTime)
		{
			int curRank = PropagationMath.SoundPriorityRank(pending.Type);
			float curDist = Vector2Int.Distance(listener.position, pending.SourcePosition);
			// 7-2장 타이브레이크: 순위·거리가 같으면 현재 시야 방향(currentDir)에 더 가까운 소리를 우선한다.
			int candidateDirStep = VisionMath.DirStepDistance(SkillAction.GetDirection8(e.Position - listener.position), listener.currentDir);
			int curDirStep = VisionMath.DirStepDistance(SkillAction.GetDirection8(pending.SourcePosition - listener.position), listener.currentDir);
			if (!PropagationMath.ShouldReplaceSound(rank, dist, curRank, curDist, candidateDirStep, curDirStep)) return; // 7-2장: 기존 유지
		}
		else if (pending != null && pending.ResponseStarted)
		{
			// 7-2장: 이미 확인 행동이 진행 중이어도 더 급한 소리면 그 자리에서 폐기하고 교체한다.
			int curRank = PropagationMath.SoundPriorityRank(pending.Type);
			if (rank >= curRank) return;
			if (listener.currentAlertSearch != null && listener.currentAlertSearch.IsSoundResponse)
				listener.currentAlertSearch = null;
		}

		var reaction = new PendingSoundReaction
		{
			Type = e.Type,
			SourcePosition = e.Position,
			HasEstimatedArea = PropagationMath.HasEstimatedArea(e.Type),
			EstimatedAreaCenter = new Vector3Int(e.Position.x, e.Position.y, e.FloorIndex),
			EstimatedAreaRadius = PropagationMath.EstimatedAreaRadius(e.Type),
			DetectedAtTime = Time.time,
			ValidUntilTime = Time.time + PropagationMath.SoundValidSeconds,
			ResponseStarted = false,
			Victim = e.Victim,
			Attacker = e.Attacker,
			IsHeavyHit = e.IsHeavyHit,
			IncidentId = e.IncidentId,
		};
		listener.Propagation.PendingSound = reaction;

		// 함정 작동음이 아니면 현재 행동을 중단시킬 수 있다(16-4장) — 트랩 분기 게이팅에 막히면 그
		// 분기가 끝난 뒤 HasAlert가 재시도한다(트랩 대응은 인류 전용이라 몬스터는 게이팅 없음).
		TryPromotePendingSoundToAlert(listener);

		// 07-A 7-3장: 확인 행동을 5초 안에 시작하지 못하면 유예시간이 소멸한다 — 감지 순간 기준 명시적 타이머.
		ExpireAfterDelay(listener, reaction).Forget();
	}

	private static async UniTaskVoid ExpireAfterDelay(Unit listener, PendingSoundReaction reaction)
	{
		await UniTask.Delay(TimeSpan.FromSeconds(PropagationMath.SoundValidSeconds));
		if (listener == null || listener.hp <= 0) return;
		// 그 사이 이미 확인 행동을 시작했거나 더 급한 소리로 완전히 교체됐으면(참조 불일치) 손대지 않는다.
		if (listener.Propagation.PendingSound == reaction && !reaction.ResponseStarted)
			listener.Propagation.PendingSound = null;
	}

	// 16-4/16-5/10장: 소리에 반응하지 않는 상태 — 전투 합류 대기 중(인류 전용)이면 완전 무시. 검증문서
	// 03-04(03번 문서 4장 "전투 중 소리 반응"): 전투 상태(personalSpottedEnemies 있음)라도 공격 목표
	// (CombatTargeting.AttackTarget)가 없으면 다음 대상을 확인할 수 있게 교전음·이동음은 듣게 한다 —
	// 함정음은 공격 목표 유무와 무관하게 전투 중엔 계속 제외.
	private static bool IsSoundUnresponsive(Unit unit, SoundType type)
	{
		if (unit is Human human)
		{
			if (human.currentJoinCombatWait != null) return true;
		}
		if (unit.personalSpottedEnemies.Count > 0)
		{
			if (unit.CombatTargeting.AttackTarget != null) return true;
			if (type == SoundType.TrapActivation) return true;
		}
		return false;
	}

	// 검증문서 03-16: 확인 행동을 시작한(ResponseStarted) 소리 기록은 그 소리 경계(IsSoundResponse)가 실제로 진행 중일
	// 때만 "반응 중"이다. 전투 진입(CombatFSMState.OnEnter)·조사·사망 수색 등이 경계만 지우고 덮어쓰면 기록이 남아,
	// 같은/낮은 순위의 새 소리를 영구히 무시하게 된다(사망음·피격 비명이면 이후 모든 소리) — 그런 기록은 중단된 반응이므로 버린다.
	private static void DiscardInterruptedSoundReaction(Unit unit)
	{
		var pending = unit.Propagation.PendingSound;
		if (pending == null || !pending.ResponseStarted) return;
		if (unit.currentAlertSearch != null && unit.currentAlertSearch.IsSoundResponse) return;
		unit.Propagation.PendingSound = null;
	}

	// currentAlertSearch가 비어있고 유효한 PendingSound가 있으면 경계 상태로 승격시킨다. TacticalFSMState.
	// HasAlert가 호출하며, 함정작동음처럼 현재 행동을 유지시키는 소리는 그 행동이 끝난 뒤에야 승격된다(16-4장).
	public static bool TryPromotePendingSoundToAlert(Unit unit)
	{
		DiscardInterruptedSoundReaction(unit);
		if (unit.currentAlertSearch != null) return unit.currentAlertSearch.IsSoundResponse;
		var pending = unit.Propagation.PendingSound;
		if (pending == null || pending.ResponseStarted) return false;
		if (Time.time > pending.ValidUntilTime) { unit.Propagation.PendingSound = null; return false; }
		// 검증문서 03-04: 여기서 승격을 막지 않으면, BT 우선순위상 Alert가 Investigate/Wait/CoreAttack/
		// DoorAttack보다 먼저라 그 아래 카테고리들의 자체 "이 소리 무시" 판정(CanInvestigate의
		// HasPendingInterruptingSound 등)이 실행될 기회조차 없이 Alert가 먼저 가로챈다 — TrapResponse만
		// Alert보다 앞이라 이 문제에서 자유롭다(IsTrapResponseBlockedBySound가 별도로 담당).
		if (IsSoundReactionSuppressed(unit, pending.Type)) return false;

		unit.currentAlertSearch = new AlertSearchState
		{
			IsSoundResponse = true,
			SoundKind = pending.Type,
			TargetPosition = pending.HasEstimatedArea
				? new Vector2Int(pending.EstimatedAreaCenter.x, pending.EstimatedAreaCenter.y)
				: pending.SourcePosition,
			SoundHasEstimatedArea = pending.HasEstimatedArea,
			SoundEstimatedAreaRadius = pending.EstimatedAreaRadius,
			// 16-3장: 피격 발생 공격음·피격 비명·사망음만 감속 없이 정상 이동속도로 접근한다.
			IsUrgentSoundApproach = pending.Type == SoundType.HitImpact || pending.Type == SoundType.HitScream || pending.Type == SoundType.Death,
		};
		pending.ResponseStarted = true;
		return true;
	}

	// 검증문서 03-04(03번 문서 4장 표): "파티 목표·코어 상호작용 당사자"는 전투관련소리·이동음·
	// 함정작동음 구분 없이 전부 반응하지 않는다(둘 다 "현재 행동 유지"). "일반 조사"는 함정작동음만
	// 무시하고 그 외 소리엔 정상적으로 중단된다 — 함정 해제·대응(TrapResponse)은 BT 우선순위 자체가
	// Alert보다 높아 여기서 다룰 필요가 없다(IsTrapResponseBlockedBySound가 전담). 보호 포메이션 참여
	// 분기는 Formation.enabled=false(2026-09-04 사용자 결정)로 여전히 비활성이라 포함하지 않았다 —
	// 재활성화 논의 시 함께 추가할 것.
	private static bool IsSoundReactionSuppressed(Unit unit, SoundType type)
	{
		// 코어/문 공격 채널링 중(인류·플레이어 몬스터 공통 필드) — 모든 소리 무시.
		if (unit.currentAttackObjectTarget.HasValue) return true;

		if (unit is Human human)
		{
			// 파티 목표 상호작용(회수 파티+Loot, 검증문서 03-01의 IsPartyGoalTarget) 당사자 — 모든 소리 무시.
			if (human.currentInvestigation?.IsPartyGoalTarget == true) return true;
			// 코어 보고 이동 중은 03번 1장 48~49행의 별도 행(보고 이동)을 따른다 — 이동음·함정음만 무시하고
			// 전투음은 대응한 뒤 남은 보고를 재개한다(검증문서 03-14). 이동음까지 허용하면 SoundMoveReact가
			// 2초간 보고 이동을 멈춰 세운다.
			if (human.currentWait?.Reason == WaitReason.ReportingCoreToLeader)
				return type == SoundType.Movement || type == SoundType.TrapActivation;
			// 일반 조사(파티 목표 아님) 중엔 함정작동음만 무시한다.
			if (type == SoundType.TrapActivation && human.currentInvestigation != null) return true;
		}
		return false;
	}

	// 16-4장: 함정 대응을 중단시킬 수 있는 소리가 대기 중인지 — TacticalFSMState가 함정 분기 전체를
	// 감싸는 조건으로 쓴다. 함정 작동음은 현재 행동을 유지시키므로 여기서 제외한다.
	public static bool HasPendingInterruptingSound(Human human)
	{
		if (human.currentAlertSearch != null && human.currentAlertSearch.IsSoundResponse) return true;
		var pending = human.Propagation.PendingSound;
		if (pending == null || pending.ResponseStarted) return false;
		if (Time.time > pending.ValidUntilTime) return false;
		return pending.Type != SoundType.TrapActivation;
	}

	// ═══════════════════════════ 피격 간접 확인 (E_HIT_HEAVY_INDIRECT) ═══════════════════════════
	// 07-A 8-2장: 추정 지역 접근 후 인지 판정 시점(TacticalFSMState.SoundAreaApproach)에서 호출 —
	// "원인을 정확 인지"는 그 순간 공격자의 정확 인지 상태로 근사한다(UpdateFOV가 채운 기록만 조회).
	public static bool IsAccuratelyPerceived(Human observer, Unit target)
		=> target != null && observer.Perception.State.perceptionRecords.TryGetValue(target, out var record)
			&& record.Outcome == PerceptionOutcome.AccuratePerception;

	public static void TryConfirmIndirectHit(Human human, AlertSearchState alert)
	{
		if (alert == null || (alert.SoundKind != SoundType.HitImpact && alert.SoundKind != SoundType.HitScream)) return;
		var pending = human.Propagation.PendingSound;
		if (pending == null || !pending.IsHeavyHit || pending.Victim == null || pending.Attacker == null) return;
		if (pending.Victim.hp <= 0 || pending.Attacker.hp <= 0) return; // 둘 다 그 사이 사망하면 다른 경로(사망음/시체)로 넘어간다
		if (human.Knowledge == null || !IsAccuratelyPerceived(human, pending.Attacker)) return;

		human.Knowledge.RecordEvent(EventId.E_HIT_HEAVY_INDIRECT, human, pending.Attacker, InfoType.Indirect, pending.IncidentId);
	}

	// ═══════════════════════════ 몬스터 처치 간접 확인 (E_MONSTER_KILL_INDIRECT) ═══════════════════════════
	// UnitFunction.CastRay가 Corpse+Monster 태그를 처음 정확 인지하는 순간 호출 — 몬스터는 Destroy되어
	// Unit 참조로 RecordEvent를 못 부르므로 미리 스냅샷해 둔 종/개체 키를 쓴다.
	public static void OnMonsterCorpseDiscovered(Human discoverer, InteractableObject corpse)
	{
		if (corpse == null || !corpse.Tags.Contains("Monster") || !corpse.MonsterKilledByHuman) return;
		if (discoverer.Knowledge == null || string.IsNullOrEmpty(corpse.MonsterSpeciesKey)) return;

		discoverer.Knowledge.RecordEventByKey(EventId.E_MONSTER_KILL_INDIRECT, discoverer,
			corpse.MonsterIsSpecialUnit ? corpse.MonsterIndividualKey : corpse.MonsterSpeciesKey,
			corpse.MonsterIsSpecialUnit, InfoType.Indirect, corpse.Id);
	}

	// ═══════════════════════════ 일반 전파 조건 (6장) ═══════════════════════════

	public static int GetPropagationRange(Human human) => PropagationMath.PropagationRange(human.BaseStat.charisma);

	// 공간 판정(같은 방/통로)+전파 범위만 확인한다(6장 "비전투" 조건 제외) — 발견자가 이미 적을 인지해
	// 비전투 판정을 못 쓰는 경우(7-1장)나 사망 정보가 비전투 조건보다 우선하는 예외(8장)에서 재사용한다.
	public static bool InPropagationRange(Human sender, Human receiver)
	{
		if (sender == null || receiver == null || sender.hp <= 0 || receiver.hp <= 0) return false;
		if (sender.currentFloor != receiver.currentFloor) return false;
		if (sender.Session?.cmap == null) return false;

		int senderRoom = sender.Session.cmap.GetRoomIdAt(sender.currentFloor, sender.position);
		int receiverRoom = sender.Session.cmap.GetRoomIdAt(receiver.currentFloor, receiver.position);
		if (!PropagationMath.SameSpace(senderRoom, receiverRoom)) return false;

		int range = GetPropagationRange(sender);
		CreateMap cmap = sender.Session.cmap;
		int floor = sender.currentFloor;
		// 07-A 1-2장: 우회 경로는 같은 공간 안에서만 유효 — senderRoom과 같은 방의 타일만 후보로 남겨 게이트 너머로 새는 것을 막는다.
		// (문이 열려 있거나 파괴됐어도 예외 없음 — 알려진 한계, PropagationMath.SameSpace 주석/검증문서 00-08 참고)
		return PropagationMath.TryGetSpaceDistance(sender.position, receiver.position, range,
			p => cmap.IsStaticTileWalkable(floor, p) && cmap.GetRoomIdAt(floor, p) == senderRoom, out _);
	}

	// 6장: 발신자·수신자 모두 비전투 + 같은 공간 + 전파 범위 안. "정보 미보유/구버전" 조건은 호출부(각 정보 저장소)가 판단한다.
	public static bool CanPropagate(Human sender, Human receiver)
	{
		if (sender != null && sender.personalSpottedEnemies.Count > 0) return false;
		if (receiver != null && receiver.personalSpottedEnemies.Count > 0) return false;
		return InPropagationRange(sender, receiver);
	}

	// 01번 문서 9장/검증문서 01-09 2번: 일반 오브젝트(조사·회수) 발견 정보의 지속 재전파 —
	// PartyDeathSystem/TrapPartySystem.TickOngoingPropagation과 동일 패턴(최초 전파 시점 스냅샷이
	// 아니라 매 틱 재확인해, 그 순간 놓친 파티원도 나중에 전파 범위 안으로 들어오면 받게 한다).
	public static void TickOngoingObjectPropagation(Human human)
	{
		var party = human.party;
		if (party == null || human.Session == null) return;

		if (party.KnownInvestigatedObjects.Count > 0)
		{
			foreach (var kv in party.KnownInvestigatedObjects)
			{
				string objectId = kv.Key;
				if (human.personalMap.IsObjectKnown(objectId)) continue;

				foreach (var carrier in party.Members)
				{
					if (carrier == null || carrier == human || carrier.hp <= 0) continue;
					if (!carrier.personalMap.IsObjectKnown(objectId)) continue;
					if (!CanPropagate(carrier, human)) continue;
					if (!human.Session.objectGrid.TryGetValue(kv.Value, out var obj)) break;

					human.personalMap.RegisterObject(obj.Id, obj.Position, obj.BaseDanger, obj.BaseInterest, obj.Tags, obj.CauserStage);
					human.personalMap.OnObjectInvestigated(obj.Id);
					break;
				}
			}
		}

		if (party.KnownCollectedObjectIds.Count > 0)
		{
			foreach (var objectId in party.KnownCollectedObjectIds)
			{
				if (human.personalMap.IsKnownCollected(objectId)) continue;

				foreach (var carrier in party.Members)
				{
					if (carrier == null || carrier == human || carrier.hp <= 0) continue;
					if (!carrier.personalMap.IsKnownCollected(objectId)) continue;
					if (!CanPropagate(carrier, human)) continue;

					human.personalMap.OnObjectCollected(objectId);
					break;
				}
			}
		}

		// 검증문서 03-02: 파티 목표 합류 정보 지속 재전파 — 위 KnownInvestigatedObjects 블록과 동일
		// 패턴이지만 OnObjectInvestigated는 호출하지 않는다(아직 완료가 아니라 "존재와 위치를 앎"만
		// 필요 — 등록되는 순간 FindInvestigateTarget이 자유로운 이 유닛에게 자연히 합류 후보로 제시한다).
		if (party.PartyGoalJoinTargets.Count > 0)
		{
			foreach (var kv in party.PartyGoalJoinTargets)
			{
				string objectId = kv.Key;
				if (human.personalMap.IsObjectKnown(objectId)) continue;

				foreach (var carrier in party.Members)
				{
					if (carrier == null || carrier == human || carrier.hp <= 0) continue;
					if (!carrier.personalMap.IsObjectKnown(objectId)) continue;
					if (!CanPropagate(carrier, human)) continue;
					if (!human.Session.objectGrid.TryGetValue(kv.Value, out var obj)) break;

					human.personalMap.RegisterObject(obj.Id, obj.Position, obj.BaseDanger, obj.BaseInterest, obj.Tags, obj.CauserStage);
					break;
				}
			}
		}

		// 검증문서 05-06(05번 7장 408·454~456줄): 발견한 문도 코어·함정과 같은 지속 재전파 대상이다 — 알게 된다는 것은 개인 지도 등록뿐이고 방 이동 명령은 여전히 리더 결정이다.
		if (party.KnownDoorObjects.Count > 0) PropagateKnownDoors(human, party);

		// 검증문서 03-03: 코어 위치·발견 내용의 지속 재전파 — 새 Party 필드 없이 기존
		// LeaderKnownCorePosition을 그대로 재사용한다(코어가 처리되면 Party.TryStartRally가 스스로
		// null로 지워 전파도 자연히 멈춘다 — 이미 처리된 코어를 뒤늦게 알려줄 필요가 없어 적절하다).
		if (party.LeaderKnownCorePosition.HasValue
			&& human.Session.objectGrid.TryGetValue(party.LeaderKnownCorePosition.Value, out var core)
			&& !human.personalMap.IsObjectKnown(core.Id))
		{
			foreach (var carrier in party.Members)
			{
				if (carrier == null || carrier == human || carrier.hp <= 0) continue;
				if (!carrier.personalMap.IsObjectKnown(core.Id)) continue;
				if (!CanPropagate(carrier, human)) continue;

				human.personalMap.RegisterObject(core.Id, core.Position, core.BaseDanger, core.BaseInterest, core.Tags, core.CauserStage);
				break;
			}
		}
	}

	private static readonly List<Human> _doorCarrierScratch = new List<Human>();
	private static readonly List<string> _staleDoorScratch = new List<string>();

	// 파티가 확인한 문(Party.KnownDoorObjects) 중 human이 아직 모르는 문을, 그 문을 아는 파티원과 일반 전파 조건(CanPropagate)이 성립하면 human의 개인 지도에 등록한다.
	// 전파 조건을 만족하는 파티원은 문마다가 아니라 한 번만 구한다(같은 공간 BFS라 문 수만큼 반복하면 비싸다). 파괴돼 사라진 문은 원장에서 정리한다.
	private static void PropagateKnownDoors(Human human, Party party)
	{
		_doorCarrierScratch.Clear();
		_staleDoorScratch.Clear();
		bool carriersBuilt = false;

		foreach (var kv in party.KnownDoorObjects)
		{
			if (!human.Session.objectGrid.TryGetValue(kv.Value, out var door) || door.IsCollected) { _staleDoorScratch.Add(kv.Key); continue; }
			if (KnowsDoor(human, door)) continue;

			if (!carriersBuilt)
			{
				carriersBuilt = true;
				foreach (var carrier in party.Members)
				{
					if (carrier == null || carrier == human || carrier.hp <= 0) continue;
					if (CanPropagate(carrier, human)) _doorCarrierScratch.Add(carrier);
				}
			}
			foreach (var carrier in _doorCarrierScratch)
			{
				if (!KnowsDoor(carrier, door)) continue;
				human.personalMap.RegisterObject(door.Id, door.Position, door.BaseDanger, door.BaseInterest, door.Tags, door.CauserStage);
				break;
			}
		}
		foreach (var id in _staleDoorScratch) party.KnownDoorObjects.Remove(id);
	}

	// 문을 "아는가" — 오브젝트로 등록했거나(전파받은 경우 포함) 문 타일을 시야로 확인했다(인지 판정과 무관). AIMovementHelper.TryFindKnownDoorInCurrentRoom(리더의 다음 이동 문 후보)이 같은 기준을 쓴다.
	public static bool KnowsDoor(Human h, InteractableObject door)
		=> h.personalMap.IsObjectKnown(door.Id) || h.personalMap.IsTileRevealed(door.Position);

	// ═══════════════════════════ 공격받은 사실의 전파 예외 (7-2장) ═══════════════════════════

	// 7-2장: 적을 정확 인지하기 전에 공격받으면 상태 조건 예외로 "공격받은 사실"+"공격 방향"을 1회
	// 전파한다. 발신자(피해자)는 비전투 조건을 못 만족할 수 있어 CanPropagate 대신 InPropagationRange를 쓴다.
	public static void PropagateAttackedFact(Human victim, Vector2Int? attackerPosition)
	{
		if (victim?.party == null) return;
		foreach (var m in victim.party.Members)
		{
			if (m == null || m == victim || m.hp <= 0) continue;
			// 소리 타입 자체는 없는 직접 정보라 함정음 특수 취급과 무관한 임의의 non-trap 타입(피격
			// 관련이라 HitImpact)을 넘긴다 — "공격 목표 있으면 무시, 없으면 허용" 규칙만 적용된다.
			if (IsSoundUnresponsive(m, SoundType.HitImpact)) continue;
			if (!InPropagationRange(victim, m)) continue;
			if (m.currentAlertSearch != null) continue; // 더 급한 상태(이미 반응 중)는 덮어쓰지 않는다.

			m.currentAlertSearch = new AlertSearchState { TargetPosition = attackerPosition, IsAttackDirectionSearch = true };
		}
	}

	// ═══════════════════════════ 상호작용 정보·보호 포메이션 (10장) ═══════════════════════════

	// 10장의 중복 참여 방지는 "같은 상호작용 인스턴스"를 전제로 한다 — Unit 단위 영구 기록이면 나중에
	// 시작한 다른 상호작용에도 옛 알림이 잘못 유효해질 수 있어 State 참조 자체를 인스턴스 토큰으로 쓴다.
	private static object GetInteractionToken(Unit unit)
	{
		if (unit.currentTrapInteraction != null) return unit.currentTrapInteraction;
		if (unit is Human h)
		{
			if (h.currentInvestigation != null) return h.currentInvestigation;
		}
		return null;
	}

	// 조사/함정 해제가 실제로 시작되는 시점에 1회 호출 — 이 전파를 직접 받은 파티원만 보호 포메이션 참여 자격을 얻는다(10장).
	public static void NotifyInteractionStarted(Human interactingUnit)
	{
		if (interactingUnit.party == null) return;
		object token = GetInteractionToken(interactingUnit);
		if (token == null) return; // 호출 시점엔 항상 있어야 하지만(막 시작한 상호작용) 방어적으로.
		foreach (var m in interactingUnit.party.Members)
		{
			if (m == null || m == interactingUnit || m.hp <= 0) continue;
			if (!CanPropagate(interactingUnit, m)) continue;
			m.Propagation.NotifiedInteractionTokens[interactingUnit] = token;
		}
	}

	// 검증문서 03-02(03번 문서 3번 항목): 파티 목표 오브젝트 상호작용이 시작되는 순간 "합류 정보"를
	// 전파한다 — 위 NotifyInteractionStarted(포메이션 참여 토큰)와는 별개 목적이라 나란히 둔다. 같은
	// 틱에 전파 범위 안인 파티원은 즉시 personalMap에 등록되고(다음 틱부터 FindInvestigateTarget이
	// 자연히 후보로 인식해 합류 이동을 시작한다), 범위 밖이었던 파티원은 party.PartyGoalJoinTargets에
	// 남겨 TickOngoingObjectPropagation이 나중에 채워준다. "유닛당 1회만 재전파"는 IsObjectKnown 체크로,
	// "재전파"(수신자가 다시 전파)는 아래 TickOngoingObjectPropagation의 carrier 루프가 등록 여부만
	// 보고 원발견자와 수신자를 구분하지 않는 것으로 자연히 만족된다.
	public static void NotifyPartyGoalInteractionStarted(Human interactingUnit, InteractableObject obj)
	{
		if (interactingUnit.party == null) return;
		interactingUnit.party.PartyGoalJoinTargets[obj.Id] = obj.Position;
		foreach (var m in interactingUnit.party.Members)
		{
			if (m == null || m == interactingUnit || m.hp <= 0) continue;
			if (m.personalMap.IsObjectKnown(obj.Id)) continue; // 유닛당 1회만
			if (!CanPropagate(interactingUnit, m)) continue;
			m.personalMap.RegisterObject(obj.Id, obj.Position, obj.BaseDanger, obj.BaseInterest, obj.Tags, obj.CauserStage);
		}
	}

	// 검증문서 03-03(03번 문서 3장 138줄): "집결 해제는 같은 방·같은 파티 전체에 전달하지만 코어
	// 위치와 발견 내용은 일반 전파 조건으로 전달한다" — Party.OnLeaderLearnsCore의 집결 해제(같은
	// 방 무조건 예외 있음)와 별도로, 코어 "정보" 자체는 CanPropagate만으로 게이트한다. 발견자는
	// UnitFunction.CastRay의 AccuratePerception 시점(:645)에 이미 personalMap에 등록돼 있으므로,
	// 여기선 리더(보고로 알게 된 경우 아직 모를 수 있음)와 그 순간 범위 안인 나머지 파티원만 새로
	// 등록한다 — 위 NotifyPartyGoalInteractionStarted와 동일한 즉시 전파 패턴.
	public static void NotifyCoreLocationKnown(Human leader, InteractableObject core)
	{
		if (leader.party == null || core == null) return;
		if (!leader.personalMap.IsObjectKnown(core.Id))
			leader.personalMap.RegisterObject(core.Id, core.Position, core.BaseDanger, core.BaseInterest, core.Tags, core.CauserStage);
		foreach (var m in leader.party.Members)
		{
			if (m == null || m == leader || m.hp <= 0) continue;
			if (m.personalMap.IsObjectKnown(core.Id)) continue;
			if (!CanPropagate(leader, m)) continue;
			m.personalMap.RegisterObject(core.Id, core.Position, core.BaseDanger, core.BaseInterest, core.Tags, core.CauserStage);
		}
	}

	// 저장된 토큰이 현재 인스턴스와 같은 참조일 때만 유효 — 상호작용 종료나 새 인스턴스 교체 시 자동 무효화.
	public static bool HasReceivedInteractionNotice(Human observer, Unit interactingUnit)
	{
		if (!observer.Propagation.NotifiedInteractionTokens.TryGetValue(interactingUnit, out var token) || token == null)
			return false;
		return ReferenceEquals(token, GetInteractionToken(interactingUnit));
	}

	// ═══════════════════════════ 전투 진입 합류 판정 (07문서 7장 / 07-A 9장) ═══════════════════════════

	public static bool ShouldDeferForJoinWait(Human human, Unit enemy)
	{
		// 현재 문서 세트가 삭제한 합류 대기(검증 06-01) — AIBehaviorConfig.joinCombatWaitEnabled가 꺼져 있으면(기본) 적을 정확 인지하는 즉시 전투한다.
		if (!(AIConfigLoader.Behavior?.joinCombatWaitEnabled ?? false)) return false;
		if (human.Knowledge == null || enemy == null || enemy.unitType == null) return false;
		// 9-3장: 이 적은 이미 한 번 응답 대기 타임아웃으로 포기한 적 — 다시 대기하지 않고 곧장 전투.
		if (enemy == human.joinWaitGiveUpTarget) return false;
		// 16-3장: 긴급 소리(피격/사망음) 확인 중 적을 정확 인지하면 거리·위험도와 무관하게 합류 대기를 건너뛴다.
		// 03번 v0.6 4-7: 전파받은 적 위치로 접근하는 유닛도 다른 유닛을 기다리지 않고 기록 위치로 직접 접근한다.
		if (human.currentAlertSearch != null && (human.currentAlertSearch.IsUrgentSoundApproach || human.currentAlertSearch.IsIndirectEnemyApproach)) return false;
		int dist = Mathf.RoundToInt(Vector2Int.Distance(human.position, enemy.position));
		DangerStage? stage = human.Knowledge.GetDangerStage(enemy.unitType.typeName, enemy.isSpecialUnit ? enemy.name : null, enemy.BaseStat.baseDanger);
		return PropagationMath.RequiresJoinWait(stage, dist);
	}

	// 검증문서 03-07(03번 문서 6장 "전파 가능한 적 정보는 이동·공격과 병행하여 전달한다"): 합류 대기
	// 필요 여부와 무관하게 적을 정확 인지한 모든 경우에 적용되는 순수 전파 — StartJoinCombatWait(합류
	// 대기가 필요한 경우 전용)와 CombatFSMState의 즉시전투 경로(합류 대기 불필요)가 공유한다.
	// RecordEnemySighting은 "더 최신 정보만 갱신"하는 멱등 함수라 매 틱 반복 호출해도 안전하다.
	public static void PropagateEnemySighting(Human discoverer, Unit enemy)
	{
		if (discoverer.party == null) return;
		foreach (var m in discoverer.party.Members)
		{
			if (m == null || m == discoverer || m.hp <= 0) continue;
			if (!InPropagationRange(discoverer, m)) continue;
			// 발견자가 정확 인지한 "적의" 마지막 확인 위치를 전한다(예전엔 발견자 자신의 위치를 넘겨, 전파받은 적 위치로 접근하면
			// 엉뚱한 곳으로 갔다 — 03번 v0.6 4-7).
			RecordEnemySighting(m, enemy, enemy.position);
		}
	}

	// 7-1장: 적을 정확 인지 + 합류가 필요한 상황 — 전파 가능한 파티원에게 적 정보를 전달하고, 합류 가능한 아군이 있으면 그 아군을 합류자로 지정한다.
	public static void StartJoinCombatWait(Human discoverer, Unit enemy)
	{
		if (discoverer.currentJoinCombatWait != null) return;
		discoverer.currentJoinCombatWait = new JoinCombatWaitState { TargetEnemy = enemy, IsDiscoverer = true };

		if (discoverer.party == null) return;

		PropagateEnemySighting(discoverer, enemy);

		Human responder = null;
		foreach (var m in discoverer.party.Members)
		{
			if (m == null || m == discoverer || m.hp <= 0) continue;
			// 합류자 지정은 순수 전파와 별개 조건 — 수신자가 비전투 상태여야 한다(발견자는 방금
			// 인지해 personalSpottedEnemies가 이미 채워져 있어 이 조건을 확인할 필요가 없다).
			if (m.personalSpottedEnemies.Count > 0) continue;
			if (!InPropagationRange(discoverer, m)) continue;
			if (responder == null && CanRespondToJoinRequest(m)) { responder = m; break; }
		}

		if (responder == null) return;
		discoverer.currentJoinCombatWait.ResponseReceived = true;
		responder.currentJoinCombatWait = new JoinCombatWaitState
		{
			TargetEnemy = enemy,
			IsDiscoverer = false,
			RallyTarget = discoverer.position,
		};
	}

	private static bool CanRespondToJoinRequest(Human m)
	{
		if (m.currentJoinCombatWait != null) return false;
		if (m.currentTrapInteraction != null) return false;
		if (m.currentInvestigation != null) return false;
		if (m.personalSpottedEnemies.Count > 0) return false;
		return true;
	}

	// 03번 v0.6 4-7(간접 인지 후 접근, 검증 갭 정리): 다른 파티원에게 전파받은 적 위치(PropagatedInfo)가 있고 이 유닛에게 다른
	// 우선 행동이 없으면 그 위치로 일반 탐색 상태·일반 이동속도로 접근한다. "어떤 상황에서 이 접근을 목표로 고르는가"는 새 문서
	// 세트에 없어(목표설정·파티 문서로 미뤄짐) 사용자 승인 하에 "비전투이고 다른 우선 행동이 없는 파티원이 가장 최근 전파 정보로
	// 접근"으로 근사했다. UnitFunction.OnUpdate 0.1초 틱이 호출한다.
	private static readonly List<object> _staleInfoKeys = new List<object>();

	public static void TickIndirectEnemyApproach(Human human)
	{
		var infos = human.Propagation.PropagatedInfo;
		if (infos.Count == 0) return;

		float now = Time.time;
		float maxAge = AIConfigLoader.Behavior?.indirectEnemyInfoMaxAgeSeconds ?? 30f;

		// 대상이 죽었거나 사라졌거나 낡은 기록은 정리하고, 접근할 가장 최근 기록을 고른다.
		Unit bestEnemy = null;
		PropagatedInfoRecord best = null;
		_staleInfoKeys.Clear();
		foreach (var kv in infos)
		{
			var enemy = kv.Key as Unit;
			var rec = kv.Value;
			if (enemy == null || enemy.hp <= 0 || !ExplorationMath.IsIndirectInfoFresh(now, rec.LastKnownTimestamp, maxAge))
			{
				_staleInfoKeys.Add(kv.Key);
				continue;
			}
			if (rec.LastKnownTile.z != human.currentFloor) continue;
			if (best == null || rec.LastKnownTimestamp > best.LastKnownTimestamp) { best = rec; bestEnemy = enemy; }
		}
		foreach (var key in _staleInfoKeys) infos.Remove(key);
		if (best == null || !CanStartIndirectEnemyApproach(human)) return;

		human.currentAlertSearch = new AlertSearchState
		{
			TargetPosition = new Vector2Int(best.LastKnownTile.x, best.LastKnownTile.y),
			IsIndirectEnemyApproach = true,
			IndirectEnemy = bestEnemy,
		};
	}

	// 접근을 시작해도 되는 상태 — 전투·이미 시작한 상호작용·집결/공동 이동/보고/귀환 대기·직접 명령·던전 입구 시퀀스가 우선한다
	// (03번 1장 50줄: 집결·공동 이동 중의 적 발견은 전투에만 대응하고 개인 탐색으로 흩어지지 않는다).
	private static bool CanStartIndirectEnemyApproach(Human human)
	{
		if (human.party == null || human.personalSpottedEnemies.Count > 0) return false;
		if (human.currentAlertSearch != null || human.currentWait != null || human.currentInvestigation != null) return false;
		if (human.currentTrapInteraction != null || human.currentJoinCombatWait != null) return false;
		if (human.pendingCoreReportPos.HasValue) return false; // 코어 보고 의무가 우선
		if (human.isInDungeonEntranceSequence || human.pendingStairTargetFloor.HasValue) return false;
		if (human.playerAttackTarget != null || (human.playerMoveTarget.HasValue && human.isManualMoveCommand)) return false;
		return true;
	}

	private static void RecordEnemySighting(Human receiver, Unit enemy, Vector2Int lastKnownPos)
	{
		if (receiver.Propagation.PropagatedInfo.TryGetValue(enemy, out var existing))
		{
			int candRank = WeightMath.PriorityRank(InfoType.Indirect, isLatest: true);
			int existRank = WeightMath.PriorityRank(existing.SourceInfoType, isLatest: true);
			if (!WeightMath.ShouldReplace(candRank, existRank, candidateIsNewer: true)) return;
		}
		receiver.Propagation.PropagatedInfo[enemy] = new PropagatedInfoRecord
		{
			LastKnownTile = new Vector3Int(lastKnownPos.x, lastKnownPos.y, enemy.currentFloor),
			LastKnownTimestamp = Time.time,
			SourceInfoType = InfoType.Indirect,
		};
	}

	// ═══════════════════════════ 최신 위치 판단 (07문서 13장 / 07-A 3장) ═══════════════════════════
	// 직접 정보와 전파 정보는 저장소를 계속 분리한다(3-4장) — "지금 어디로 알고 있나"는 24장
	// PriorityRank(직접 우선)가 아니라 13장 "더 최신 확인 정보로 갱신" 규칙만으로 비교한다.
	public static bool GetLatestKnownPosition(Human observer, Unit target, out Vector3Int tile, out float timestamp)
	{
		bool hasDirect = observer.personalMap.TryGetMonsterSighting(target.name, out var directTile, out _, out _, out var directTime);
		bool hasPropagated = observer.Propagation.PropagatedInfo.TryGetValue(target, out var propagated);

		if (hasDirect && (!hasPropagated || !PropagationMath.ShouldUpdateLastKnownPosition(propagated.LastKnownTimestamp, directTime)))
		{
			tile = directTile; timestamp = directTime; return true;
		}
		if (hasPropagated)
		{
			tile = propagated.LastKnownTile; timestamp = propagated.LastKnownTimestamp; return true;
		}
		tile = default; timestamp = default; return false;
	}

	// UnitFunction.OnUpdate가 매 프레임(currentAlertSearch와 동일 패턴) 호출한다.
	public static void TickJoinCombatWait(Human human, float deltaTime)
	{
		var wait = human.currentJoinCombatWait;
		if (wait == null) return;

		if (wait.TargetEnemy == null || wait.TargetEnemy.hp <= 0) { human.currentJoinCombatWait = null; return; }

		// 9-3장: 대기 중 적이 먼저 공격하면 위험도·합류 조건을 무시하고 즉시 전투로 전환한다. 이 적에
		// 대해서는 다시 대기하지 않도록 표시해둬야 다음 틱에 같은 조건으로 대기가 재시작되지 않는다.
		if (human.isHitThisTurn) { human.joinWaitGiveUpTarget = wait.TargetEnemy; human.currentJoinCombatWait = null; return; }

		if (wait.IsDiscoverer)
		{
			if (!wait.ResponseReceived)
			{
				wait.ResponseWaitTimer += deltaTime;
				if (wait.ResponseWaitTimer >= PropagationMath.JoinResponseWaitSeconds)
				{
					// 응답 시간 초과 — 발견자 단독 전투 시작. 이 적에 한해 재대기를 막아둬야
					// 다음 틱 CombatFSMState.GetPriority가 같은 조건으로 대기를 재시작하지 않는다.
					human.joinWaitGiveUpTarget = wait.TargetEnemy;
					human.currentJoinCombatWait = null;
				}
				return;
			}

			wait.ActualJoinWaitTimer += deltaTime;
			bool arrived = human.party != null && HasResponderArrived(human, wait.TargetEnemy);
			// 07-A 11장: 대기 초과 후 처리는 09_목표·이동경로 문서 몫이라 미정 — 스텁으로 대기를 끝내고 단독 전투 시작.
			// 도착 성공이든 타임아웃이든 이 적에 대해서는 재대기를 막아 곧장 전투로 넘어가게 한다.
			if (arrived || wait.ActualJoinWaitTimer >= PropagationMath.ActualJoinMaxWaitSeconds)
			{
				ReleaseResponders(human, wait.TargetEnemy);
				human.joinWaitGiveUpTarget = wait.TargetEnemy;
				human.currentJoinCombatWait = null;
			}
		}
		// 합류자 쪽 실제 이동은 TacticalFSMState.JoinCombatWaitPerform이 담당하고, 정리는 발견자 Tick의 ReleaseResponders가 함께 처리한다.
	}

	private static bool HasResponderArrived(Human discoverer, Unit enemy)
	{
		foreach (var m in discoverer.party.Members)
		{
			if (m == null || m == discoverer || m.hp <= 0) continue;
			if (m.currentJoinCombatWait == null || m.currentJoinCombatWait.IsDiscoverer) continue;
			if (m.currentJoinCombatWait.TargetEnemy != enemy) continue;
			if (Vector2Int.Distance(m.position, discoverer.position) <= PropagationMath.ImmediateCombatRangeTiles) return true;
		}
		return false;
	}

	private static void ReleaseResponders(Human discoverer, Unit enemy)
	{
		if (discoverer.party == null) return;
		foreach (var m in discoverer.party.Members)
		{
			if (m == null || m == discoverer) continue;
			if (m.currentJoinCombatWait != null && !m.currentJoinCombatWait.IsDiscoverer && m.currentJoinCombatWait.TargetEnemy == enemy)
				m.currentJoinCombatWait = null;
		}
	}
}
