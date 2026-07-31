using System.Collections.Generic;
using UnityEngine;

// 07_전파·소리·간접입력 구현부(부수효과 있는 호출부) — PartyDeathSystem/TrapPartySystem과 동일 성격.
// PropagationMath(순수 계산)와 PropagatedInfoRecord/PendingSoundReaction/JoinCombatWaitState(저장소)를
// 실제 게임 루프(UnitFunction.OnUpdate/Move/RecordHitWeightEvent, GameSession.RemoveDeadUnit,
// TacticalFSMState.TrapPass, CombatFSMState.GetPriority)에 연결한다.
//
// 08(리더·명령)/09(목표·경로)/06(전투반응·기습) 문서가 아직 없어 위임된 부분(발견자가 리더에게 실제로
// 이동해 전파하는 물리적 단계, 실제 합류 최대 대기 5초 초과 후 처리, 반응 후보의 정교한 선택 로직 등)은
// CLAUDE.md 관례대로 가장 단순한 기본값으로 스텁 처리한다 — 각 지점에 근거를 남긴다.
public static class PropagationSystem
{
	// ═══════════════════════════ 소리 이벤트 버스 ═══════════════════════════

	private class SoundEvent
	{
		public SoundType Type;
		public Vector2Int Position;
		public int FloorIndex;
		public int RoomId;
		public Unit Source; // 이동음의 "자신·아군 이동음 제외" 판정에 사용. 함정 등은 null.
		public float CreatedAtTime;
	}

	private static readonly List<SoundEvent> _activeSounds = new List<SoundEvent>();

	// 14장: 이동/공격 실행/피격/사망/함정 작동 시 각각 호출한다. session이 null이거나 맵 조회에
	// 실패하면(방 밖 등) 조용히 무시한다 — 소리는 항상 공간 안에서만 유효하다(12장).
	public static void EmitSound(GameSession session, SoundType type, Vector2Int position, int floorIndex, Unit source)
	{
		if (session == null || session.cmap == null) return;
		int roomId = session.cmap.GetRoomIdAt(floorIndex, position);
		if (roomId < 0) return;

		PruneExpiredSounds();
		_activeSounds.Add(new SoundEvent
		{
			Type = type,
			Position = position,
			FloorIndex = floorIndex,
			RoomId = roomId,
			Source = source,
			CreatedAtTime = Time.time,
		});
	}

	private static void PruneExpiredSounds()
	{
		for (int i = _activeSounds.Count - 1; i >= 0; i--)
		{
			if (Time.time - _activeSounds[i].CreatedAtTime > PropagationMath.SoundValidSeconds)
				_activeSounds.RemoveAt(i);
		}
	}

	// ═══════════════════════════ 소리 감지·선택 (16장) ═══════════════════════════

	// UnitFunction.OnUpdate의 기존 0.1초 틱에서 인류에 한해 호출한다(몬스터는 07문서 16-6장 "역할군
	// AI가 감지 결과를 해석"이 담당해야 하는데 그런 역할군 AI 자체가 아직 없어, 몬스터 청각 계산은
	// PropagationMath.SoundDetectionRange(isMonster:true)로 값만 준비해 두고 실제 소비는 스텁으로
	// 남긴다 — VisionMath.NonEmptyTileTempWeight와 동일한 전례).
	public static void TickSoundPerception(Human human)
	{
		if (human.Session == null || human.Session.cmap == null) return;
		if (IsSoundUnresponsive(human)) return;

		PruneExpiredSounds();
		int myRoomId = human.Session.cmap.GetRoomIdAt(human.currentFloor, human.position);
		if (myRoomId < 0) return;

		bool isAlert = human.Perception.IsAlert;
		SoundEvent best = null;
		int bestRank = int.MaxValue;
		float bestDist = float.MaxValue;

		foreach (var ev in _activeSounds)
		{
			if (ev.FloorIndex != human.currentFloor) continue;
			if (ev.Source == human) continue;
			// 16-2장: 자신의 이동음과 모든 아군의 일반 이동음은 반응 대상에서 제외(다른 소리 종류는
			// 대상 제외 규칙이 없다 — 전투 관련 소리는 출처와 무관하게 긴급하기 때문).
			if (ev.Type == SoundType.Movement && ev.Source is Human) continue;
			if (!PropagationMath.SameSpace(myRoomId, ev.RoomId)) continue;

			float dist = Vector2Int.Distance(human.position, ev.Position);
			int detectRange = PropagationMath.SoundDetectionRange(ev.Type, human.spotting, isAlert, isMonster: false);
			if (dist > detectRange) continue;

			int rank = PropagationMath.SoundPriorityRank(ev.Type);
			if (best == null || PropagationMath.ShouldReplaceSound(rank, dist, bestRank, bestDist))
			{
				best = ev; bestRank = rank; bestDist = dist;
			}
		}
		if (best == null) return;

		var pending = human.Propagation.PendingSound;
		if (pending != null && !pending.ResponseStarted && Time.time <= pending.ValidUntilTime)
		{
			int curRank = PropagationMath.SoundPriorityRank(pending.Type);
			float curDist = Vector2Int.Distance(human.position, pending.SourcePosition);
			if (!PropagationMath.ShouldReplaceSound(bestRank, bestDist, curRank, curDist)) return; // 7-2장: 기존 유지
		}
		else if (pending != null && pending.ResponseStarted)
		{
			// 7-2장: "높은 우선순위 소리로 교체되면 이전 확인 대상은 폐기하며 이후 다시 돌아가지
			// 않는다" — 이미 확인 행동(currentAlertSearch)이 진행 중이어도 더 급한 소리면 그 자리에서
			// 폐기하고 교체한다(그래야 아래에서 새로 세팅하는 PendingSound가 다음 HasAlert 호출 때
			// 막히지 않고 바로 승격된다).
			int curRank = PropagationMath.SoundPriorityRank(pending.Type);
			if (bestRank >= curRank) return;
			if (human.currentAlertSearch != null && human.currentAlertSearch.IsSoundResponse)
				human.currentAlertSearch = null;
		}

		human.Propagation.PendingSound = new PendingSoundReaction
		{
			Type = best.Type,
			SourcePosition = best.Position,
			HasEstimatedArea = PropagationMath.HasEstimatedArea(best.Type),
			EstimatedAreaCenter = new Vector3Int(best.Position.x, best.Position.y, best.FloorIndex),
			EstimatedAreaRadius = PropagationMath.EstimatedAreaRadius(best.Type),
			DetectedAtTime = Time.time,
			ValidUntilTime = Time.time + PropagationMath.SoundValidSeconds,
			ResponseStarted = false,
		};

		// 함정 작동음이 아니면(=전투 관련 소리/이동음) 현재 행동을 중단시킬 수 있다(16-4장) — 그 판단은
		// TacticalFSMState의 트랩 분기 게이팅이 담당하고, 여기서는 곧바로 승격을 시도한다(막혀 있으면
		// 실패하고 트랩 분기가 끝나는 시점에 HasAlert가 다시 시도한다).
		TryPromotePendingSoundToAlert(human);
	}

	// 16-4/16-5/10장: 소리에 아예 반응하지 않는 상태 — 파티 목표·코어 상호작용 당사자, 전투 진입 합류
	// 대기 중, 이미 전투 목표가 있는 경우(16-5장 "현재 공격 목표가 있으면 소리로 반응하지 않는다").
	private static bool IsSoundUnresponsive(Human human)
	{
		if (human.personalSpottedEnemies.Count > 0) return true;
		if (human.currentJoinCombatWait != null) return true;
		if (human.currentCoreInteraction != null && human.currentCoreInteraction.Active) return true;
		return false;
	}

	// currentAlertSearch가 비어있고 아직 시작 안 한 유효한 PendingSound가 있으면 그 자리에서 경계
	// 상태로 승격시킨다. TacticalFSMState.HasAlert(Alert 분기 조건)가 지연 호출한다 — 상위 분기(함정
	// 대응 등)가 이미 처리 중이면 애초에 이 지점까지 오지 않으므로, 함정작동음처럼 "현재 행동을 유지"
	// 시키는 소리는 자연히 그 행동이 끝난 뒤에야 여기 도달해 승격된다(16-4장).
	public static bool TryPromotePendingSoundToAlert(Human human)
	{
		if (human.currentAlertSearch != null) return human.currentAlertSearch.IsSoundResponse;
		var pending = human.Propagation.PendingSound;
		if (pending == null || pending.ResponseStarted) return false;
		if (Time.time > pending.ValidUntilTime) { human.Propagation.PendingSound = null; return false; }

		human.currentAlertSearch = new AlertSearchState
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

	// ═══════════════════════════ 일반 전파 조건 (6장) ═══════════════════════════

	public static int GetPropagationRange(Human human) => PropagationMath.PropagationRange(human.BaseStat.charisma);

	// 공간 판정(같은 방/통로)+전파 범위만 확인한다(6장 "비전투" 조건 제외) — 7-1장의 "적을 정확 인지한
	// 발견자가 정보를 전달"하는 경우, 발견자는 그 인지 자체 때문에 personalSpottedEnemies가 이미 채워져
	// 있어(합류 대기 중이라 아직 전투로는 안 넘어갔을 뿐) 일반적인 "비전투" 판정을 그대로 쓸 수 없다 —
	// StartJoinCombatWait가 이 헬퍼를 직접 쓰고 수신자 쪽 비전투 조건만 별도로 확인한다.
	private static bool InPropagationRange(Human sender, Human receiver)
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
		return PropagationMath.TryGetSpaceDistance(sender.position, receiver.position, range,
			p => cmap.IsStaticTileWalkable(floor, p), out _);
	}

	// 6장: 발신자·수신자 모두 비전투 + 같은 공간(방/통로) + 전파 범위 안. "정보 미보유/구버전" 조건은
	// 호출부(각 정보 저장소)가 판단한다.
	public static bool CanPropagate(Human sender, Human receiver)
	{
		if (sender != null && sender.personalSpottedEnemies.Count > 0) return false;
		if (receiver != null && receiver.personalSpottedEnemies.Count > 0) return false;
		return InPropagationRange(sender, receiver);
	}

	// ═══════════════════════════ 상호작용 정보·보호 포메이션 (10장) ═══════════════════════════

	// 조사/함정 해제/코어 조사가 실제로 시작되는 시점(TacticalFSMState의 각 Perform 리프)에 1회 호출한다.
	// 이 전파를 직접 받은 파티원만 보호 포메이션 참여 자격을 얻는다 — "상호작용 유닛을 시야에서 직접
	// 확인한 것만으로는 참여하지 않는다"(10장)가 기존 03문서 6-1장의 시야 기반 근사를 대체한다.
	public static void NotifyInteractionStarted(Human interactingUnit)
	{
		if (interactingUnit.party == null) return;
		foreach (var m in interactingUnit.party.Members)
		{
			if (m == null || m == interactingUnit || m.hp <= 0) continue;
			if (!CanPropagate(interactingUnit, m)) continue;
			m.Propagation.NotifiedActiveInteractions.Add(interactingUnit);
		}
	}

	public static bool HasReceivedInteractionNotice(Human observer, Unit interactingUnit)
		=> observer.Propagation.NotifiedActiveInteractions.Contains(interactingUnit);

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
		if (m.currentCoreInteraction != null && m.currentCoreInteraction.Active) return false;
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
