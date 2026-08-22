using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

// 07_전파·소리·간접입력 구현부(부수효과 있는 호출부) — PartyDeathSystem/TrapPartySystem과 동일 성격.
// PropagationMath(순수 계산)와 PropagatedInfoRecord/PendingSoundReaction/JoinCombatWaitState(저장소)를
// 실제 게임 루프(UnitFunction.Move/RecordHitWeightEvent, GameSession.RemoveDeadUnit,
// TacticalFSMState.TrapPass, CombatFSMState.GetPriority)에 연결한다.
//
// 08(리더·명령)/09(목표·경로)/06(전투반응·기습) 문서가 아직 없어 위임된 부분(발견자가 리더에게 실제로
// 이동해 전파하는 물리적 단계, 실제 합류 최대 대기 5초 초과 후 처리, 반응 후보의 정교한 선택 로직 등)은
// CLAUDE.md 관례대로 가장 단순한 기본값으로 스텁 처리한다 — 각 지점에 근거를 남긴다.
public static class PropagationSystem
{
	// ═══════════════════════════ 소리 이벤트 (2026-08-05 재설계) ═══════════════════════════
	// 사용자 피드백: 소리는 "발생한 그 순간"에만 존재하는 1회성 사건이다 — 예전 구현처럼 5초 동안
	// _activeSounds 목록에 남아 있다가 나중에 범위 안으로 걸어 들어온 유닛까지 뒤늦게 주워듣는 건
	// 기획 의도와도, 최적화 관점에서도(모든 인류가 0.1초마다 활성 소리 목록 전체를 훑는 상시 폴링)
	// 어긋난다. 07-A 7-3장의 "일반 소리 유효시간 5초"는 소리 자체의 수명이 아니라 "그 순간 감지한
	// 인류 개인이 확인 행동을 시작할 수 있는 유예시간"이었다.
	//
	// 그래서 이제 EmitSound가 호출된 그 자리에서 범위 스캔까지 끝내고, 그 순간 조건을 만족한 감지자에게만
	// R3 이벤트(OnSoundPerceived)로 통지한다 — 나중에 범위 안으로 들어온 유닛은 이 사건 자체를 아예
	// 못 받는다(실제 소리처럼 순간적). 5초 유예시간은 이벤트를 받은 각 감지자의 PendingSound에 대해서만
	// UniTask 타이머로 개별 적용된다(ExpireAfterDelay) — 다른 시스템의 틱 호출 유무에 기대지 않고
	// 감지 시점 자체를 기준으로 명시적으로 만료시킨다.
	//
	// 범위 스캔 방식 자체는 아래 "방별 소리 감지자 인덱스"(2026-08-05 도입, 2026-08-06 몬스터까지 확장)로
	// 이미 좁혀져 있다 — 최종 판정(거리 비교)은 그 후보군 안에서 좌표 수학(Vector2Int.Distance) 그대로
	// 쓴다(공간 분할은 "후보를 줄이는" 역할이지 "거리 계산 자체를 대체"하지 않는다).
	public readonly struct SoundPerceivedEvent
	{
		// 2026-08-06: 07문서 1장 "소리 감지: 인류/몬스터 모두 적용"에 맞춰 Human 고정에서 Unit으로
		// 일반화했다 — 몬스터도 감지자가 될 수 있다(전파는 여전히 인류 전용, 아래 별도 함수들 참고).
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

	// 다른 시스템(디버그 시각화/로그/향후 UI 등)도 "누가 방금 무슨 소리를 들었는지"를 자유롭게 구독할
	// 수 있도록 공개 Observable로 노출한다. 실제 게임 로직(PendingSound 갱신)은 이 클래스가 정적
	// 생성자에서 내부적으로 구독해 처리한다 — 다른 구독자가 있든 없든 핵심 동작은 항상 보장된다.
	public static readonly Subject<SoundPerceivedEvent> OnSoundPerceived = new Subject<SoundPerceivedEvent>();

	// "소리가 실제로 발생한 사건" 자체를 알리는 이벤트 — OnSoundPerceived와 달리 아무도 그 소리를
	// 감지하지 못했어도(범위 밖/인덱스 문제/이미 다른 걸 보고 있어서 등) 무조건 1회 발행된다. 게임
	// 로직은 이 이벤트를 쓰지 않는다(감지 여부와 무관하게 "소리가 남" 자체를 보여주고 싶은 디버그
	// 시각화 전용 — PropagationDebugVisualizer가 이걸 구독해 소리 종류별 범위 원을 그린다).
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

	// ═══════════════════════════ 방별 소리 감지자 인덱스 (스캔 최적화, 2026-08-05 / 2026-08-06 몬스터 확장) ═══════════════════════════
	// EmitSound가 매번 session.units(던전 전체 유닛) 전체를 순회하던 것을 "그 소리가 발생한 방에 있는
	// 유닛"으로만 좁힌다 — 사용자 자문 결과 "던전에 여러 파티/방이 동시에 활동하면 전체 순회가 O(N²)로
	// 커진다"는 근거로 결정. GameSession.RegisterUnitPos/UnregisterUnitPos(스폰/이동/사망 시 이미
	// 호출되는 기존 유닛 그리드 관리 지점)가 그대로 이 인덱스도 함께 유지해준다 — 별도의 매 틱 폴링이
	// 필요 없다. roomId는 층마다 번호가 재사용될 수 있어(CreateMap.GetRoomOccupationState가 floorIndex를
	// 별도로 요구하는 것과 동일한 이유) (층, roomId) 조합을 키로 쓴다.
	// 2026-08-06: 07문서 1장 "소리 감지: 인류/몬스터 모두 적용" 검증 중 몬스터가 이 인덱스에 아예
	// 등록되지 않아 EmitSound의 스캔 대상이 될 수 없던 갭을 발견해 Human 전용에서 Unit 공통으로
	// 일반화했다(전파/재전파는 여전히 인류 전용 — 아래 "일반 전파 조건" 섹션은 손대지 않음).
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

	// 14장: 이동/공격 실행/피격/사망/함정 작동 시 각각 호출한다. session이 null이거나 맵 조회에
	// 실패하면(방 밖 등) 조용히 무시한다 — 소리는 항상 공간 안에서만 유효하다(12장).
	// attacker/isHeavyHit/incidentId: 피격 발생 공격음/피격 비명에서만 쓰는 선택적 메타데이터
	// (E_HIT_HEAVY_INDIRECT 연결용) — 다른 소리 종류는 기본값(null/false/null) 그대로 넘기면 된다.
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
			// 16-2장: "자신의 이동음과 모든 아군의 일반 이동음은 반응 대상에서 제외"(다른 소리 종류는
			// 대상 제외 규칙이 없다 — 전투 관련 소리는 출처와 무관하게 긴급하기 때문). 몬스터가 감지자로
			// 추가되면서(2026-08-06) "아군"을 종족 단위로 근사한다 — 인류는 인류의 이동음을, 몬스터는
			// 몬스터의 이동음을 서로 반응 대상에서 제외하되, 서로 다른 진영의 이동음(=상대 진영 접근)은
			// 여전히 감지 대상으로 남긴다.
			bool sourceIsHuman = source is Human;
			bool listenerIsHuman = listener is Human;
			if (type == SoundType.Movement && sourceIsHuman == listenerIsHuman) continue;
			if (!listener.CanPerceive) continue; // 기절 등 인지 판정 불가 상태 — 못 듣는다고 근사
			if (IsSoundUnresponsive(listener)) continue;

			bool isAlert = listener.Perception.IsAlert;
			int detectRange = PropagationMath.SoundDetectionRange(type, listener.spotting, isAlert, isMonster: !listenerIsHuman);
			// 07-A 1장 "전파·소리 공통 공간 거리 판정" — 소리도 전파와 동일하게 벽 우회 최단경로를 써야
			// 한다(직선거리만 쓰면 방 안 장애물(isStructureExist)을 무시하게 됨, 2026-08-06 검증 중 발견).
			// _listenersByRoom로 이미 같은 방 후보만 남아있지만, 우회 경로 자체가 게이트를 스치지 않도록
			// GetRoomIdAt(floorIndex,p)==roomId로 같은 공간 안에서만 우회하도록 제한한다.
			bool reachable = PropagationMath.TryGetSpaceDistance(position, listener.position, detectRange,
				p => cmap.IsStaticTileWalkable(floorIndex, p) && cmap.GetRoomIdAt(floorIndex, p) == roomId, out _);
			if (!reachable) continue;

			OnSoundPerceived.OnNext(new SoundPerceivedEvent(listener, type, position, floorIndex, source, attacker, isHeavyHit, incidentId));
		}
	}

	// ═══════════════════════════ 소리 감지 반응 (16장) ═══════════════════════════

	// OnSoundPerceived 내부 구독자 — EmitSound가 스캔한 각 인류에 대해 정확히 1회 호출된다(예전
	// TickSoundPerception처럼 유효한 5초 동안 매 틱 반복 재평가하지 않는다). 우선순위 비교 로직은
	// 그대로 유지 — 같은 공격에서 공격 실행음→피격 발생 공격음→비명이 순서대로 발생해도(각각
	// EmitSound 호출 = 각각 이 핸들러 호출) 매번 "지금 저장된 PendingSound"와 비교해 최종적으로
	// 가장 급한 소리가 남는다.
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
			// 7-2장 마지막 타이브레이크: 순위·거리가 모두 같으면 현재 시야 방향(currentDir)에 더 가까운
			// 소리를 우선한다. 두 지점 모두 관찰자와 정확히 같은 위치인 근접 사건이면 방향이 무의미하므로
			// GetDirection8이 기본 방향을 돌려줘도 안전하다(둘 다 같은 값이라 타이브레이크에 영향 없음).
			int candidateDirStep = VisionMath.DirStepDistance(SkillAction.GetDirection8(e.Position - listener.position), listener.currentDir);
			int curDirStep = VisionMath.DirStepDistance(SkillAction.GetDirection8(pending.SourcePosition - listener.position), listener.currentDir);
			if (!PropagationMath.ShouldReplaceSound(rank, dist, curRank, curDist, candidateDirStep, curDirStep)) return; // 7-2장: 기존 유지
		}
		else if (pending != null && pending.ResponseStarted)
		{
			// 7-2장: "높은 우선순위 소리로 교체되면 이전 확인 대상은 폐기하며 이후 다시 돌아가지
			// 않는다" — 이미 확인 행동(currentAlertSearch)이 진행 중이어도 더 급한 소리면 그 자리에서
			// 폐기하고 교체한다(그래야 아래에서 새로 세팅하는 PendingSound가 다음 HasAlert 호출 때
			// 막히지 않고 바로 승격된다).
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

		// 함정 작동음이 아니면(=전투 관련 소리/이동음) 현재 행동을 중단시킬 수 있다(16-4장) — 그 판단은
		// TacticalFSMState의 트랩 분기 게이팅이 담당하고, 여기서는 곧바로 승격을 시도한다(막혀 있으면
		// 실패하고 트랩 분기가 끝나는 시점에 HasAlert가 다시 시도한다). 트랩 대응 자체는 인류 전용이라
		// 몬스터는 이 게이팅 없이 바로 승격된다.
		TryPromotePendingSoundToAlert(listener);

		// 07-A 7-3장: 확인 행동을 5초 안에 시작하지 못하면 이 유예시간 자체가 소멸한다. 다른 시스템의
		// 틱 호출이 우연히 이 인지 판정 지점을 다시 지나가길 기다리지 않고(사용자 요청), 감지된 이
		// 순간을 기준으로 명시적 타이머를 건다.
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

	// 16-4/16-5/10장: 소리에 아예 반응하지 않는 상태 — 이미 전투 목표가 있는 경우(16-5장 "현재 공격
	// 목표가 있으면 소리로 반응하지 않는다", 인류/몬스터 공통 규칙)는 모든 유닛에 적용하고, 전투 진입
	// 합류 대기 중은 인류 전용 상태라 Human일 때만 확인한다(몬스터는 해당 필드 자체가 없다 — 16-6장
	// "몬스터는 소리 감지와 방향·추정 지역 획득까지만 공통 규칙을 사용한다").
	private static bool IsSoundUnresponsive(Unit unit)
	{
		if (unit.personalSpottedEnemies.Count > 0) return true;
		if (unit is Human human)
		{
			if (human.currentJoinCombatWait != null) return true;
		}
		return false;
	}

	// currentAlertSearch가 비어있고 아직 시작 안 한 유효한 PendingSound가 있으면 그 자리에서 경계
	// 상태로 승격시킨다. TacticalFSMState.HasAlert(Alert 분기 조건)가 지연 호출한다 — 상위 분기(함정
	// 대응 등)가 이미 처리 중이면 애초에 이 지점까지 오지 않으므로, 함정작동음처럼 "현재 행동을 유지"
	// 시키는 소리는 자연히 그 행동이 끝난 뒤에야 여기 도달해 승격된다(16-4장).
	// 2026-08-06: Human 고정에서 Unit으로 일반화 — currentAlertSearch/Propagation 모두 base Unit
	// 소유라 로직 변경 없이 그대로 몬스터에도 적용된다. TacticalFSMState.HasAlert가 인류/몬스터 구분
	// 없이 호출한다(HasAlert 자체가 어느 유닛에 대해 호출되는지는 UnitFSM이 이미 결정해 둔 뒤다).
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
	// 07-A 8-2장: 추정 지역 접근 후 인지 판정 1회 시점(TacticalFSMState.SoundAreaApproach)에서 호출한다.
	// "원인을 정확 인지" = 그 순간 공격자(몬스터)를 정확 인지 상태로 판정받았는지로 근사한다(별도
	// 재판정을 새로 굴리지 않고 UnitFunction.ForceReidentifyAttacker/UpdateFOV가 이미 채워둔 기록을
	// 조회만 한다 — IsCurrentlyIdentified와 동일 패턴). 가중치 구현현황 문서 "다음 우선순위 4번"/
	// 시야인지반응 구현현황 문서 "다음 우선순위 1번"이 가리키던 마지막 배선이다.
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
	// UnitFunction.CastRay의 기존 오브젝트 발견 파이프라인이 Corpse+Monster 태그를 처음 정확 인지하는
	// 순간(firstTouch && AccuratePerception, PartyDeathSystem.OnCorpseDiscovered와 동일 지점) 호출한다.
	// 몬스터는 죽으면 Destroy되어 Unit 참조로 직접 RecordEvent를 부를 수 없으므로(target.name 등 Unity
	// 네이티브 프로퍼티 접근이 안전하지 않음), GameSession.RemoveDeadUnit이 Destroy 전에 InteractableObject
	// 시체에 스냅샷해 둔 종/개체 키를 그대로 쓴다 — HumanKnowledgeBase.RecordEventByKey 참고.
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

	// 공간 판정(같은 방/통로)+전파 범위만 확인한다(6장 "비전투" 조건 제외) — 7-1장의 "적을 정확 인지한
	// 발견자가 정보를 전달"하는 경우, 발견자는 그 인지 자체 때문에 personalSpottedEnemies가 이미 채워져
	// 있어(합류 대기 중이라 아직 전투로는 안 넘어갔을 뿐) 일반적인 "비전투" 판정을 그대로 쓸 수 없다 —
	// StartJoinCombatWait가 이 헬퍼를 직접 쓰고 수신자 쪽 비전투 조건만 별도로 확인한다.
	// 8장: 사망 정보(PartyDeathSystem)가 "일반적인 비전투 전파 조건보다 사망 정보 규칙을 우선한다"는
	// 예외를 적용할 때도 이 공간+범위 판정만 재사용한다 — CanPropagate처럼 비전투 조건까지 강제하지
	// 않는다(2026-08-06 검증 중 발견, 03문서 4-12장 "현재 상태에 관계없이 즉시 처리"와 동일 근거로
	// public 승격).
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
		// 07-A 1-2장: 우회 경로는 "같은 공간 안에서"만 유효하다 — IsStaticTileWalkable만으로는 구조적
		// 통행 가능 여부만 볼 뿐 방 소속을 안 봐서, 우회 경로가 게이트를 지나 옆방/통로를 스치는 경우까지
		// 허용해버릴 수 있었다. senderRoom(=receiverRoom, 위에서 이미 SameSpace로 확인됨)과 같은 방의
		// 타일만 후보로 남긴다(2026-08-06 검증 중 발견).
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

	// 7-2장: "적을 정확 인지하기 전에 공격받은 경우 일반 전파의 발신자 상태 조건에 대한 예외로 정보를
	// 1회 전파할 수 있다" — 전달 내용은 "자신이 공격받았다는 사실" + "공격 형태상 확인 가능한 공격
	// 방향"(attackerPosition, 없으면 방향도 모름) 둘뿐이다(공격자를 이미 정확 인지했다면 그 정보도
	// 포함되지만, 이 메서드는 UnitFunction.RecordHitWeightEvent의 "공격자 미인지" 분기에서만 호출돼
	// 그 경우는 애초에 해당하지 않는다). "이 예외는 발신자의 상태 조건만 예외 처리하며, 수신자의
	// 상태·동일 공간·전파 범위 조건은 일반 전파 규칙을 따른다" — 그래서 CanPropagate(양쪽 비전투 확인)
	// 대신 InPropagationRange(공간+범위만)를 쓰고 수신자 쪽 비전투 조건만 IsSoundUnresponsive로 별도
	// 확인한다(7-1장 StartJoinCombatWait와 동일 패턴 — 발견자/피해자는 이 사건 자체 때문에 비전투
	// 조건을 만족 못 할 수 있어 일반 CanPropagate를 못 쓴다).
	// 수신자 반응은 "이후 상태 전환과 공격 방향에 따른 행동은 17을 따른다"(7-2장) 그대로 — 17장은
	// 발신자(피해자)와 수신자를 구분하지 않으므로, 피해자 본인이 받는 것과 동일한 모양의 일반
	// AlertSearchState(03문서 4-10~4-12장 미식별 공격 수색, 15초 워치독 포함)를 그대로 재사용한다.
	// 소리 반응(IsSoundResponse)과는 다른 채널이라 소리 전용 필드는 세팅하지 않는다(07문서 2-1장:
	// "전파는 시스템상 소리가 아니다").
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

	// 10장 마지막 문단(2026-08-06 수정): "재전파 수신자는... 같은 대상에 대한 중복 조사·해제 목표를
	// 선택하지 않는다"는 "같은 상호작용 인스턴스"를 전제로 한다 — 예전엔 Unit 단위 영구 기록이라, A가
	// 상호작용1을 끝내고 완전히 다른 상호작용2를 나중에(범위 밖에서) 시작해도 옛 알림이 그대로 유효해
	// 잘못 참여자격을 줄 수 있었다(07-31본이 이미 지적한 갭). currentTrapInteraction/currentInvestigation
	// 은 상호작용을 새로 시작할 때마다 TacticalFSMState가 `new ...State`로 교체하므로, 그 참조 자체를
	// "이번 인스턴스"의 토큰으로 쓰면 별도 ID 체계 없이 인스턴스를 구분할 수 있다.
	private static object GetInteractionToken(Unit unit)
	{
		if (unit.currentTrapInteraction != null) return unit.currentTrapInteraction;
		if (unit is Human h)
		{
			if (h.currentInvestigation != null) return h.currentInvestigation;
		}
		return null;
	}

	// 조사/함정 해제가 실제로 시작되는 시점(TacticalFSMState의 각 Perform 리프)에 1회 호출한다.
	// 이 전파를 직접 받은 파티원만 보호 포메이션 참여 자격을 얻는다 — "상호작용 유닛을 시야에서 직접
	// 확인한 것만으로는 참여하지 않는다"(10장)가 기존 03문서 6-1장의 시야 기반 근사를 대체한다.
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

	// 저장된 토큰이 상호작용 유닛의 "지금 이 순간" 인스턴스와 여전히 같은 참조일 때만 유효 — 상호작용이
	// 끝나 인스턴스가 사라지거나(token != null인데 현재 token이 null) 완전히 새 인스턴스로 교체되면
	// (참조 불일치) 자동으로 무효화된다.
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
		// 16-3장: 피격 발생 공격음/피격 비명/사망음을 확인하던 중 적을 정확 인지하면 거리·위험도와
		// 무관하게 즉시 전투에 합류한다 — 합류 대기 자체를 건너뛴다.
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
	// 직접 정보(PersonalMapKnowledge.MonsterSighting)와 전파 정보(PropagatedInfo)는 저장소 자체는
	// 계속 분리한다(3장 "직접 정보/전파 정보" 구분, 4장 "전파받은 정보가 구체적이어도 직접 인지
	// 결과로 취급 안 함"). 다만 "이 대상 지금 어디로 알고 있나"를 판단할 때는 24장 PriorityRank
	// (직접이 간접보다 항상 우선, 오래됐어도)가 아니라 13장 자체의 규칙 — "더 최신의 확인 정보로
	// 갱신, 더 오래된 정보는 유지"만 순수 비교한다(사용자 결정, 2026-08-06). 두 저장소 다 없으면
	// false, 하나만 있으면 그 값, 둘 다 있으면 ShouldUpdateLastKnownPosition(07-A 3장)으로 비교한다.
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
			// 07-A 11장: 실제 합류 최대 대기 5초 초과 후 행동은 09_목표·이동경로 문서 몫이라 아직
			// 미정 — 안전한 기본값으로 대기를 끝내고 단독 전투를 시작한다(스텁, 구현현황 문서에 명시).
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
