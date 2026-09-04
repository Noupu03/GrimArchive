using UnityEngine;

// 03문서 9장 구현부 — 함정 발견 시 발견자 혼자 처리하지 않고, 파티 중 함정과 가장 가까운 1명을 실제
// 담당(선정 유닛)으로 뽑는다(2026-09-04: 해제 성공률 비교 대신 거리 단일 기준으로 단순화).
// 나머지 파티원은 정보만 기록한 채 기존 행동을 유지한다.
public static class TrapPartySystem
{
	// ─────────────────────────── 9-1~9-3장: 함정 최초 발견 ───────────────────────────
	public static void OnTrapDiscovered(Human discoverer, InteractableObject trapObj)
	{
		var party = discoverer.party;
		if (party == null)
		{
			// 파티 없는 단독 유닛(테스트 등) — 예전처럼 발견 즉시 자기 자신이 처리.
			if (discoverer.currentTrapInteraction == null)
				discoverer.currentTrapInteraction = new TrapInteractionState { TrapObjectId = trapObj.Id, TrapPosition = trapObj.Position };
			return;
		}
		if (party.TrapCoordinations.ContainsKey(trapObj.Id)) return; // 이미 다른 파티원이 먼저 발견해 처리 중

		var coord = new TrapPartyCoordination
		{
			TrapObjectId = trapObj.Id,
			TrapPosition = trapObj.Position,
			DiscovererName = discoverer.name,
		};
		party.TrapCoordinations[trapObj.Id] = coord;

		// 9-3장 전파는 07문서 6장 일반 전파 조건(비전투+같은 공간+전파 범위)을 따른다 — 발견자는 항상
		// 알고, 조건을 만족하는 파티원만 함께 안다(비선정 유닛이 위치를 알아야 9-6장 "인접 1칸 회피"가 성립).
		foreach (var m in party.Members)
		{
			if (m == null || m.hp <= 0) continue;
			bool receivesInfo = m == discoverer || PropagationSystem.CanPropagate(discoverer, m);
			if (!receivesInfo) continue;

			if (!m.personalMap.IsObjectKnown(trapObj.Id))
				m.personalMap.RegisterObject(trapObj.Id, trapObj.Position, trapObj.BaseDanger, trapObj.BaseInterest, trapObj.Tags, trapObj.CauserStage);

			// 9-6장: 인접 1칸에 있던 비발견 유닛은 즉시 안전 타일로 물러난다(발견자/해제 담당은 그
			// 자리로 가야 하므로 제외).
			if (m == discoverer) continue;
			Vector2Int trapPos2D = new Vector2Int(trapObj.Position.x, trapObj.Position.y);
			if (m.currentTrapInteraction == null && Mathf.Max(Mathf.Abs(m.position.x - trapPos2D.x), Mathf.Abs(m.position.y - trapPos2D.y)) <= 1)
			{
				StepAwayFromTrap(m, trapPos2D);
			}
		}

		// 9-3장: 발견 유닛이 파티 내에서 함정과 가장 가까우면 2초 응답 없이 즉시 해제 담당으로 확정한다.
		// 2026-09-04(사용자 요청): 선정 기준을 해제 성공률 비교에서 "가장 가까운 1명"으로 단순화.
		if (IsClosestPartyMember(discoverer, party, trapObj.Position))
		{
			coord.SelectedUnitName = discoverer.name;
			coord.SelectionLocked = true;
			discoverer.currentTrapInteraction = new TrapInteractionState
			{
				TrapObjectId = trapObj.Id,
				TrapPosition = trapObj.Position,
				JoinWaitElapsed = true,
				AutoConfirmed = true,
				IsSelectedDisarmer = true,
				SelectedUnitName = discoverer.name,
			};
		}
		else
		{
			discoverer.currentTrapInteraction = new TrapInteractionState { TrapObjectId = trapObj.Id, TrapPosition = trapObj.Position };
		}
	}

	// 07문서 9장 재전파 — 최초 발견 시 전파 범위 밖이었던 파티원도 나중에 범위 안으로 들어오면 함정
	// 정보를 받는다(PartyDeathSystem.TickOngoingPropagation과 동일 패턴).
	public static void TickOngoingPropagation(Human human)
	{
		var party = human.party;
		if (party == null || party.TrapCoordinations.Count == 0 || human.Session == null) return;

		foreach (var coord in party.TrapCoordinations.Values)
		{
			if (human.personalMap.IsObjectKnown(coord.TrapObjectId)) continue;

			foreach (var carrier in party.Members)
			{
				if (carrier == null || carrier == human || carrier.hp <= 0) continue;
				if (!carrier.personalMap.IsObjectKnown(coord.TrapObjectId)) continue;
				if (!PropagationSystem.CanPropagate(carrier, human)) continue;
				if (!human.Session.objectGrid.TryGetValue(coord.TrapPosition, out var trapObj)) break;

				human.personalMap.RegisterObject(trapObj.Id, trapObj.Position, trapObj.BaseDanger, trapObj.BaseInterest, trapObj.Tags, trapObj.CauserStage);
				break;
			}
		}
	}

	private static void StepAwayFromTrap(Human unit, Vector2Int trapPos2D)
	{
		Vector2Int away = unit.position - trapPos2D;
		if (away == Vector2Int.zero) away = new Vector2Int(1, 0);
		Vector2Int stepTarget = unit.position + new Vector2Int(System.Math.Sign(away.x), System.Math.Sign(away.y));
		AIMovementHelper.MoveTowardsPos(unit, stepTarget);
	}

	// ─────────────────────────── 9-2장: 최근접 유닛 판정 ───────────────────────────
	// 2026-09-04(사용자 요청): 해제 시도자 선정을 성공률 비교 대신 "가장 가까운 1명"으로 단순화.
	private static bool IsClosestPartyMember(Human discoverer, Party party, Vector3Int trapPos)
	{
		Vector2Int trapPos2D = new Vector2Int(trapPos.x, trapPos.y);
		float discovererDist = Vector2Int.Distance(discoverer.position, trapPos2D);
		foreach (var m in party.Members)
		{
			if (m == null || m == discoverer || m.hp <= 0) continue;
			if (Vector2Int.Distance(m.position, trapPos2D) < discovererDist) return false;
		}
		return true;
	}

	// ─────────────────────────── 9-2~9-4장: 2초 경과/기록함정 확정 시점의 실제 선정 ───────────────────────────
	// UnitFunction.OnUpdate가 discoverer의 trap.JoinWaitElapsed가 false→true로 바뀌는 바로 그 프레임에
	// (AutoConfirmed가 아닌 경우에만) 호출한다.
	public static void ResolveSelection(Human discoverer, TrapInteractionState trap)
	{
		var party = discoverer.party;
		if (party == null) { trap.IsSelectedDisarmer = true; return; }
		if (!party.TrapCoordinations.TryGetValue(trap.TrapObjectId, out var coord)) { trap.IsSelectedDisarmer = true; return; }
		if (coord.SelectionLocked)
		{
			trap.SelectedUnitName = coord.SelectedUnitName;
			trap.IsSelectedDisarmer = coord.SelectedUnitName == discoverer.name;
			return;
		}

		// 9-2장: 전파 범위(07문서 6장, PropagationSystem.CanPropagate) 안의 모든 유닛 중 함정 대응으로
		// 전환 가능한 유닛만 대상으로 가장 가까운 1명을 선정한다.
		// 2026-09-04(사용자 요청): 성공률 비교 대신 거리(EstimateEta) 단일 기준으로 단순화.
		Human best = discoverer;
		float bestEta = EstimateEta(discoverer, trap.TrapPosition);

		foreach (var m in party.Members)
		{
			if (m == null || m == discoverer || m.hp <= 0) continue;
			if (!PropagationSystem.CanPropagate(discoverer, m)) continue;
			if (!CanSwitchToTrapResponse(m)) continue;

			float eta = EstimateEta(m, trap.TrapPosition);
			if (eta < bestEta)
			{
				best = m; bestEta = eta;
			}
		}

		coord.SelectedUnitName = best.name;
		coord.SelectionLocked = true;
		trap.SelectedUnitName = best.name;
		trap.IsSelectedDisarmer = best == discoverer;

		if (best != discoverer)
		{
			// 9-7장 미도착 유예 판정의 기준 ETA를 선정 확정 시점에 고정한다(매 틱 재계산하면 안 됨).
			trap.SelectedUnitInitialEta = bestEta;

			if (best.currentTrapInteraction == null)
				best.currentTrapInteraction = new TrapInteractionState { TrapObjectId = trap.TrapObjectId, TrapPosition = trap.TrapPosition };
			best.currentTrapInteraction.JoinWaitElapsed = true;
			best.currentTrapInteraction.IsSelectedDisarmer = true;
			best.currentTrapInteraction.SelectedUnitName = best.name;
		}
	}

	private static bool CanSwitchToTrapResponse(Human m)
	{
		if (m.currentTrapInteraction != null) return false; // 이미 다른 함정 대응 중
		if (m.currentInvestigation != null) return false;
		if (m.currentFormation != null) return false;
		if (m.personalSpottedEnemies.Count > 0) return false; // 전투 중 근사(명시적 전투 상태 플래그 부재)
		return true;
	}

	// 9-7장: A* 전체 경로 대신 직선거리로 ETA를 근사한다(선정 비교/미도착 판정용 추정치라 정밀할 필요 없음).
	private static float EstimateEta(Human m, Vector3Int trapPos)
	{
		float dist = Vector2Int.Distance(m.position, new Vector2Int(trapPos.x, trapPos.y));
		float speed = Mathf.Max(0.01f, m.BaseStat.walkSpeed);
		return dist / speed;
	}

	// ─────────────────────────── 9-7장: 발견 유닛이 선정 유닛의 도착을 기다리는 동안 ───────────────────────────
	public static void TickWaitingForSelectedUnit(Human discoverer, TrapInteractionState trap, float deltaTime)
	{
		if (trap.SelectedUnitName == null || trap.IsSelectedDisarmer || trap.SearchingForSelectedUnit) return;

		var party = discoverer.party;
		Human selected = party?.Members.Find(m => m != null && m.name == trap.SelectedUnitName);

		if (selected == null || selected.hp <= 0)
		{
			// 선정 유닛이 이미 죽었거나(사망 사건은 PartyDeathSystem이 별도 처리) 파티에서 사라짐 —
			// 마지막으로 알려진 위치(없으면 함정 위치)로 즉시 찾아 나선다.
			trap.SearchingForSelectedUnit = true;
			trap.SelectedUnitLastKnownPos ??= new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
			return;
		}

		trap.SelectedUnitLastKnownPos = selected.position;
		Vector2Int trapPos2D = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		if (Vector2Int.Distance(selected.position, trapPos2D) <= 1f) return; // 이미 도착 — 대기 계속

		// 매 틱 EstimateEta를 새로 구하면 정상 접근 중에도 ETA가 계속 줄어 "실종"으로 오판정되므로,
		// ResolveSelection이 고정해둔 SelectedUnitInitialEta를 그대로 쓴다.
		float eta = trap.SelectedUnitInitialEta >= 0f ? trap.SelectedUnitInitialEta : EstimateEta(selected, trap.TrapPosition);
		trap.MissingUnitTimer += deltaTime;
		if (trap.MissingUnitTimer >= eta + ExplorationMath.TrapSelectedUnitLateGraceSeconds)
			trap.SearchingForSelectedUnit = true;
	}

	// TrapSearchForMissingUnit이 목적지 도착 시 호출 — 선정을 리셋해 2초 응답부터 다시 시작한다(9-7장).
	public static void RestartSelection(Human discoverer, TrapInteractionState trap)
	{
		if (discoverer.party != null && discoverer.party.TrapCoordinations.TryGetValue(trap.TrapObjectId, out var coord))
		{
			coord.SelectionLocked = false;
			coord.SelectedUnitName = null;
		}
		trap.SearchingForSelectedUnit = false;
		trap.IsSelectedDisarmer = false;
		trap.SelectedUnitName = null;
		trap.JoinWaitElapsed = false;
		trap.JoinWaitTimer = 0f;
		trap.MissingUnitTimer = 0f;
		trap.SelectedUnitInitialEta = -1f;
	}
}
