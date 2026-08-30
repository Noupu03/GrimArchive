using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

// 07_전파·소리·간접입력 구현부(부수효과 있는 호출부, PartyDeathSystem/TrapPartySystem과 동일 성격) —
// PropagationMath(순수 계산)/저장소 클래스들을 실제 게임 루프에 연결한다. 리더·명령/목표·경로/전투반응
// 문서가 없어 위임된 부분(물리적 전파 이동, 합류 5초 초과 처리 등)은 가장 단순한 기본값으로 스텁 처리한다.
public static class PropagationSystem
{
	// ═══════════════════════════ 소리 이벤트 ═══════════════════════════
	// 소리는 발생한 순간에만 존재하는 1회성 사건이다 — EmitSound 호출 시점에 범위 스캔까지 끝내 그 순간
	// 조건을 만족한 감지자에게만 통지하므로 나중에 들어온 유닛은 못 받는다. 07-A 7-3장의 "유효시간 5초"는
	// 소리의 수명이 아니라 감지자별 확인 행동 유예시간(UniTask 타이머로 개별 적용)이다.
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

	// 공개 Observable — 실제 게임 로직(PendingSound 갱신)은 정적 생성자에서 내부적으로 구독하므로
	// 다른 구독자(디버그 시각화 등) 존재 여부와 무관하게 핵심 동작은 항상 보장된다.
	public static readonly Subject<SoundPerceivedEvent> OnSoundPerceived = new Subject<SoundPerceivedEvent>();

	// OnSoundPerceived와 달리 아무도 감지하지 못했어도 무조건 1회 발행된다 — 게임 로직은 쓰지 않고
	// PropagationDebugVisualizer가 소리 종류별 범위 원을 그리는 디버그 전용 채널이다.
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
	// EmitSound가 session.units 전체 대신 소리 발생 방의 유닛만 스캔하도록 좁힌다. GameSession.
	// RegisterUnitPos/UnregisterUnitPos가 이 인덱스도 함께 유지해 별도 폴링이 필요 없다. roomId는
	// 층마다 재사용될 수 있어 (층, roomId)를 키로 쓴다. 감지는 인류/몬스터 공통이지만 전파는 인류 전용.
	private static readonly Dictionary<(int Floor, int RoomId), HashSet<Unit>> _listenersByRoom = new Dictionary<(int, int), HashSet<Unit>>();
	private static readonly Dictionary<Unit, (int Floor, int RoomId)> _listenerRoomKey = new Dictionary<Unit, (int, int)>();
	private static readonly List<Unit> _scanBuffer = new List<Unit>();

	// GameSession.RegisterUnitPos가 유닛을 등록/이동시킬 때마다 호출한다. roomId < 0(맵 밖 등)이면
	// 인덱스에서 빠진다 — 그 상태로는 어차피 소리를 주고받을 공간 판정 자체가 성립하지 않는다.
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

	// 14장: 이동/공격 실행/피격/사망/함정 작동 시 각각 호출한다. session/맵 조회 실패 시 무시.
	// attacker/isHeavyHit/incidentId는 피격 발생 공격음·피격 비명 전용(E_HIT_HEAVY_INDIRECT 연결용) —
	// 다른 소리 종류는 기본값 그대로 넘기면 된다.
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

		// 이 스캔 도중 인덱스가 바뀔 일은 없지만(핸들러가 방을 옮기는 로직을 안 건드림), 방어적으로
		// 스냅샷해서 순회한다 — 재사용 버퍼라 매 호출 GC 없음.
		_scanBuffer.Clear();
		_scanBuffer.AddRange(candidates);

		foreach (var listener in _scanBuffer)
		{
			if (listener == null || listener.hp <= 0 || listener == source) continue;
			if (listener.currentFloor != floorIndex) continue; // 인덱스 정합성 방어 — 이론상 항상 참
			// 16-2장: 자신/아군의 일반 이동음은 제외한다(전투 관련 소리는 예외 없음). "아군"은 종족
			// 단위로 근사하되 서로 다른 진영의 이동음은 감지 대상으로 남긴다.
			bool sourceIsHuman = source is Human;
			bool listenerIsHuman = listener is Human;
			if (type == SoundType.Movement && sourceIsHuman == listenerIsHuman) continue;
			if (!listener.CanPerceive) continue; // 기절 등 인지 판정 불가 상태 — 못 듣는다고 근사
			if (IsSoundUnresponsive(listener)) continue;

			bool isAlert = listener.Perception.IsAlert;
			int detectRange = PropagationMath.SoundDetectionRange(type, listener.spotting, isAlert, isMonster: !listenerIsHuman);
			// 07-A 1장: 소리도 전파처럼 벽 우회 최단경로를 써야 한다 — 우회가 게이트를 스치지 않도록
			// GetRoomIdAt==roomId로 같은 공간 안에서만 허용한다.
			bool reachable = PropagationMath.TryGetSpaceDistance(position, listener.position, detectRange,
				p => cmap.IsStaticTileWalkable(floorIndex, p) && cmap.GetRoomIdAt(floorIndex, p) == roomId, out _);
			if (!reachable) continue;

			OnSoundPerceived.OnNext(new SoundPerceivedEvent(listener, type, position, floorIndex, source, attacker, isHeavyHit, incidentId));
		}
	}

	// ═══════════════════════════ 소리 감지 반응 (16장) ═══════════════════════════

	// OnSoundPerceived 내부 구독자 — 감지 시 정확히 1회 호출되며(매 틱 재평가 없음), 여러 소리가
	// 순서대로 발생해도 "지금 저장된 PendingSound"와 비교해 가장 급한 소리만 남는다.
	private static void HandleSoundPerceived(SoundPerceivedEvent e)
	{
		Unit listener = e.Observer;
		if (listener == null || listener.hp <= 0) return;

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

		// 함정 작동음이 아니면 현재 행동을 중단시킬 수 있다(16-4장) — 트랩 분기 게이팅에 막히면
		// 그 분기가 끝난 뒤 HasAlert가 다시 시도한다. 트랩 대응은 인류 전용이라 몬스터는 게이팅 없음.
		TryPromotePendingSoundToAlert(listener);

		// 07-A 7-3장: 확인 행동을 5초 안에 시작하지 못하면 유예시간이 소멸한다 — 감지 순간 기준 명시적 타이머.
		ExpireAfterDelay(listener, reaction).Forget();
	}

	private static async UniTaskVoid ExpireAfterDelay(Unit listener, PendingSoundReaction reaction)
	{
		await UniTask.Delay(TimeSpan.FromSeconds(PropagationMath.SoundValidSeconds));
		if (listener == null || listener.hp <= 0) return;
		// 그 사이 이미 확인 행동을 시작했거나(ResponseStarted) 더 급한 소리로 완전히 교체됐으면
		// (참조가 더 이상 이 reaction이 아니면) 손대지 않는다.
		if (listener.Propagation.PendingSound == reaction && !reaction.ResponseStarted)
			listener.Propagation.PendingSound = null;
	}

	// 16-4/16-5/10장: 소리에 반응하지 않는 상태 — 이미 전투 목표가 있으면(인류/몬스터 공통) 무시하고,
	// 전투 합류 대기 중(인류 전용 상태)도 무시한다.
	private static bool IsSoundUnresponsive(Unit unit)
	{
		if (unit.personalSpottedEnemies.Count > 0) return true;
		if (unit is Human human)
		{
			if (human.currentJoinCombatWait != null) return true;
		}
		return false;
	}

	// currentAlertSearch가 비어있고 유효한 PendingSound가 있으면 경계 상태로 승격시킨다.
	// TacticalFSMState.HasAlert가 호출하며, 함정작동음처럼 현재 행동을 유지시키는 소리는 그 행동이
	// 끝난 뒤에야 승격된다(16-4장). Unit 공통 필드라 인류/몬스터 모두 적용된다.
	public static bool TryPromotePendingSoundToAlert(Unit unit)
	{
		if (unit.currentAlertSearch != null) return unit.currentAlertSearch.IsSoundResponse;
		var pending = unit.Propagation.PendingSound;
		if (pending == null || pending.ResponseStarted) return false;
		if (Time.time > pending.ValidUntilTime) { unit.Propagation.PendingSound = null; return false; }

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

	// 16-4장: 함정 대응(해제/대기 전 단계 전부)을 중단시킬 수 있는 소리가 대기 중인지 — TacticalFSMState가
	// 함정 분기 전체를 감싸는 조건으로 쓴다. 함정 작동음은 현재 행동을 유지시키므로 여기서 제외한다.
	public static bool HasPendingInterruptingSound(Human human)
	{
		if (human.currentAlertSearch != null && human.currentAlertSearch.IsSoundResponse) return true;
		var pending = human.Propagation.PendingSound;
		if (pending == null || pending.ResponseStarted) return false;
		if (Time.time > pending.ValidUntilTime) return false;
		return pending.Type != SoundType.TrapActivation;
	}

	// ═══════════════════════════ 피격 간접 확인 (E_HIT_HEAVY_INDIRECT) ═══════════════════════════
	// 07-A 8-2장: 추정 지역 접근 후 인지 판정 시점(TacticalFSMState.SoundAreaApproach)에서 호출한다.
	// "원인을 정확 인지"는 그 순간 공격자가 정확 인지 상태인지로 근사한다(UpdateFOV가 채운 기록만 조회).
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
	// UnitFunction.CastRay가 Corpse+Monster 태그를 처음 정확 인지하는 순간 호출한다. 몬스터는 Destroy되어
	// Unit 참조로 RecordEvent를 못 부르므로 GameSession.RemoveDeadUnit이 미리 스냅샷해 둔 종/개체 키를 쓴다.
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
	// 일반 비전투 판정을 못 쓰는 경우(7-1장)와 사망 정보가 비전투 조건보다 우선하는 예외(8장)에서
	// CanPropagate 대신 이 헬퍼를 재사용한다.
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
		// 07-A 1-2장: 우회 경로는 같은 공간 안에서만 유효하다 — senderRoom(=receiverRoom)과 같은
		// 방의 타일만 후보로 남겨 게이트 너머로 새는 것을 막는다.
		return PropagationMath.TryGetSpaceDistance(sender.position, receiver.position, range,
			p => cmap.IsStaticTileWalkable(floor, p) && cmap.GetRoomIdAt(floor, p) == senderRoom, out _);
	}

	// 6장: 발신자·수신자 모두 비전투 + 같은 공간(방/통로) + 전파 범위 안. "정보 미보유/구버전" 조건은
	// 호출부(각 정보 저장소)가 판단한다.
	public static bool CanPropagate(Human sender, Human receiver)
	{
		if (sender != null && sender.personalSpottedEnemies.Count > 0) return false;
		if (receiver != null && receiver.personalSpottedEnemies.Count > 0) return false;
		return InPropagationRange(sender, receiver);
	}

	// ═══════════════════════════ 공격받은 사실의 전파 예외 (7-2장) ═══════════════════════════

	// 7-2장: 적을 정확 인지하기 전에 공격받으면 발신자 상태 조건의 예외로 "공격받은 사실"+"공격 방향"을
	// 1회 전파한다. 발신자(피해자)는 이 사건 때문에 비전투 조건을 못 만족할 수 있어 CanPropagate 대신
	// InPropagationRange(공간+범위만)를 쓰고, 수신자 쪽 비전투 조건만 IsSoundUnresponsive로 확인한다.
	public static void PropagateAttackedFact(Human victim, Vector2Int? attackerPosition)
	{
		if (victim?.party == null) return;
		foreach (var m in victim.party.Members)
		{
			if (m == null || m == victim || m.hp <= 0) continue;
			if (IsSoundUnresponsive(m)) continue;
			if (!InPropagationRange(victim, m)) continue;
			if (m.currentAlertSearch != null) continue; // 더 급한 상태(이미 반응 중)는 덮어쓰지 않는다.

			m.currentAlertSearch = new AlertSearchState { TargetPosition = attackerPosition };
		}
	}

	// ═══════════════════════════ 상호작용 정보·보호 포메이션 (10장) ═══════════════════════════

	// 10장의 중복 참여 방지는 "같은 상호작용 인스턴스"를 전제로 한다 — Unit 단위 영구 기록이면 나중에
	// 시작한 다른 상호작용에도 옛 알림이 잘못 유효해질 수 있어, State 참조 자체를 인스턴스 토큰으로 쓴다.
	private static object GetInteractionToken(Unit unit)
	{
		if (unit.currentTrapInteraction != null) return unit.currentTrapInteraction;
		if (unit is Human h)
		{
			if (h.currentInvestigation != null) return h.currentInvestigation;
		}
		return null;
	}

	// 조사/함정 해제가 실제로 시작되는 시점에 1회 호출한다. 이 전파를 직접 받은 파티원만 보호 포메이션
	// 참여 자격을 얻는다(10장) — 03문서 6-1장의 시야 기반 근사를 대체.
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
		if (human.Knowledge == null || enemy == null || enemy.unitType == null) return false;
		// 16-3장: 긴급 소리(피격/사망음) 확인 중 적을 정확 인지하면 거리·위험도와 무관하게 합류 대기를 건너뛴다.
		if (human.currentAlertSearch != null && human.currentAlertSearch.IsUrgentSoundApproach) return false;
		int dist = Mathf.RoundToInt(Vector2Int.Distance(human.position, enemy.position));
		DangerStage? stage = human.Knowledge.GetDangerStage(enemy.unitType.typeName, enemy.isSpecialUnit ? enemy.name : null, enemy.BaseStat.baseDanger);
		return PropagationMath.RequiresJoinWait(stage, dist);
	}

	// 7-1장: 적을 정확 인지 + 합류가 필요한 상황 — 전파 가능한 파티원에게 적 정보를 1회 전달하고,
	// 합류 가능한 아군이 있으면 그 아군을 합류자로 지정한다.
	public static void StartJoinCombatWait(Human discoverer, Unit enemy)
	{
		if (discoverer.currentJoinCombatWait != null) return;
		discoverer.currentJoinCombatWait = new JoinCombatWaitState { TargetEnemy = enemy, IsDiscoverer = true };

		if (discoverer.party == null) return;

		Human responder = null;
		foreach (var m in discoverer.party.Members)
		{
			if (m == null || m == discoverer || m.hp <= 0) continue;
			// 수신자만 비전투 조건을 확인한다 — 발견자는 방금 이 적을 인지했기 때문에
			// personalSpottedEnemies가 이미 채워져 있다(InPropagationRange 주석 참고).
			if (m.personalSpottedEnemies.Count > 0) continue;
			if (!InPropagationRange(discoverer, m)) continue;

			RecordEnemySighting(m, enemy, discoverer.position);
			if (responder == null && CanRespondToJoinRequest(m)) responder = m;
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
	// 직접 정보와 전파 정보는 저장소를 계속 분리한다(3-4장). "지금 어디로 알고 있나"는 24장
	// PriorityRank(직접 우선)가 아니라 13장 "더 최신 확인 정보로 갱신" 규칙만 순수 비교한다.
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

		// 9-3장: 대기 중 적이 먼저 공격하면 위험도·합류 조건을 무시하고 즉시 전투로 전환한다.
		if (human.isHitThisTurn) { human.currentJoinCombatWait = null; return; }

		if (wait.IsDiscoverer)
		{
			if (!wait.ResponseReceived)
			{
				wait.ResponseWaitTimer += deltaTime;
				if (wait.ResponseWaitTimer >= PropagationMath.JoinResponseWaitSeconds)
					human.currentJoinCombatWait = null; // 2초 내 응답 없음 — 발견자 단독 전투 시작
				return;
			}

			wait.ActualJoinWaitTimer += deltaTime;
			bool arrived = human.party != null && HasResponderArrived(human, wait.TargetEnemy);
			// 07-A 11장: 5초 초과 후 처리는 09_목표·이동경로 문서 몫이라 미정 — 스텁으로 대기를 끝내고 단독 전투 시작.
			if (arrived || wait.ActualJoinWaitTimer >= PropagationMath.ActualJoinMaxWaitSeconds)
			{
				ReleaseResponders(human, wait.TargetEnemy);
				human.currentJoinCombatWait = null;
			}
		}
		// 합류자 쪽은 실제 이동만 TacticalFSMState.JoinCombatWaitPerform이 담당하고, 정리는 발견자의
		// Tick이 도착을 감지했을 때 ReleaseResponders가 함께 처리한다.
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
