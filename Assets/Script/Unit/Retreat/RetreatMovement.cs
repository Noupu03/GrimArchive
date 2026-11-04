using UnityEngine;

// 후퇴·도주 이동 한 틱(후퇴도주 명세서 5장) — RetreatFSMState의 리프가 부른다. 목적지(거점 휴식 자리/던전 출구)로 걷거나 달리고, 0.5초마다 기준 경로와 실제 경로를 비교해 막히면 막는 요소에 따라 대응한다:
//   문 = 상대 진영 문은 자동 개방 없이 가능한 파괴 / 적 = 통과에 필요한 만큼만 상대(전멸까지 기다리지 않음) / 함정 = 우회 우선 → 파괴 → 예상 피해를 알고 통과 후 HP > 0일 때만 강행 / 그 밖 = 목적지를 유지한 채 길 정보를 기다린다.
// 후퇴·도주 중에는 소리 추적·새 수집·중간 휴식을 위해 정지하지 않고(Tactical 분기를 타지 않는다), 전투 중인 방도 통과 조건만 맞으면 경유한다(Combat 상태로 넘어가지 않는다).
public static class RetreatMovement
{
	// 우회·파괴 모두 안 되는 함정을 강행 통과할 때 쓰는 스크래치(함정 무시). 여러 유닛이 공유해 매번 캐시를 비운 뒤 쓴다(AIMovementHelper 긴급 보호 통과와 같은 방식).
	static readonly AStarMovement _forcePass = new AStarMovement { TrapModeOverride = TrapMoveMode.Off };

	// 목적지에 닿을 길이 아예 없다고 판정된 뒤 다음 점검까지의 간격(초) — 평소 점검 간격(0.5초)보다 길게 둔다.
	const float UnreachableRecheckSeconds = 2f;

	public static BTStatus Step(Human h)
	{
		h.GrantRoomLeave(); // 후퇴·도주 이동은 방 이탈 금지의 예외 — 리더 없는 개인 후퇴·도주는 방을 떠나야 한다(CLAUDE.md "공황·도주 문서가 나오면 예외를 재고")
		var move = h.Retreat.Move;
		if (!TryResolveDestination(h, move)) return BTStatus.Running; // 갈 곳을 아직 모른다(예: 이 층에 출구 계단 접근 타일이 없음) — 정보를 기다린다

		if (move.DestinationKind == RetreatDestination.Exit && RetreatExit.IsAtStairs(h)) return RetreatExit.StepCross(h);
		if (move.DestinationKind == RetreatDestination.Base && h.position == move.Destination) return BTStatus.Running; // 휴식 자리에 섰다 — 도착 판정·전환은 PartyRetreatSystem이 한다

		float now = Time.time;
		if (now >= move.NextPathCheckTime)
		{
			move.NextPathCheckTime = now + RetreatSystem.PathCheckSeconds;
			var verdict = RetreatBlockResolver.Evaluate(h, move.Destination, move);
			if (!verdict.Blocked) EndBlock(h, move);
			else UpdateBlock(h, move, verdict, now);
			// 닿을 곳이 아예 없는 상태는 매번 전체 맵을 훑는 탐색 세 번(실제·기준·최근접 추적)이라, 여러 명이 동시에 이 상태면 0.5초마다 부하가 쌓인다 — 길 정보가 바뀔 일이 드물어 점검 간격을 늘린다.
			if (verdict.Blocked && verdict.Kind == RetreatBlockKind.Unreachable) move.NextPathCheckTime = now + UnreachableRecheckSeconds;
		}

		if (move.Block != RetreatBlockKind.None)
		{
			BTStatus? blockStatus = RunBlock(h, move, now);
			if (blockStatus.HasValue) return blockStatus.Value;
		}
		return StepAlong(h, move);
	}

	// ─────────────────────────── 목적지 ───────────────────────────

	static RetreatDestination DecideKind(Human h)
	{
		var rs = h.Retreat;
		if (rs.Mode == RetreatMode.PersonalFlee || rs.Mode == RetreatMode.PersonalRetreat) return RetreatDestination.Exit;
		var st = h.party?.Retreat;
		if (st != null && st.Destination == RetreatDestination.Base && st.BaseRoom != null && st.BaseRoom.Floor == h.currentFloor) return RetreatDestination.Base;
		return RetreatDestination.Exit; // 다른 층 거점은 지원하지 않는다 — 그 층의 출구로 간다
	}

	static bool TryResolveDestination(Human h, RetreatMoveState move)
	{
		float now = Time.time;
		RetreatDestination kind = DecideKind(h);
		bool unchanged = kind == move.DestinationKind && move.DestinationFloor == h.currentFloor;
		if (unchanged && now < move.NextDestinationRefreshTime) return true;

		Vector2Int tile;
		if (kind == RetreatDestination.Exit)
		{
			if (!RetreatExit.TryResolveExitTile(h, out tile)) return false;
		}
		else
		{
			Room room = h.party.Retreat.BaseRoom;
			if (!RetreatBases.EnsureRestSlot(h, room, h.party.Retreat.ClaimedRestSlots))
			{
				if (!RetreatBases.TryGetRoomAnchorTile(h, room, out tile)) return false;
			}
			else tile = h.Retreat.RestSlot.Value;
		}

		if (!unchanged || move.Destination != tile)
		{
			if (!unchanged) { move.ResetSegment(); move.ClearBlock(); }
			move.StuckTurns = 0;
		}
		move.DestinationKind = kind;
		move.DestinationFloor = h.currentFloor;
		move.Destination = tile;
		move.NextDestinationRefreshTime = now + 1f;
		return true;
	}

	// ─────────────────────────── 한 걸음 ───────────────────────────

	static BTStatus StepAlong(Human h, RetreatMoveState move)
	{
		Vector2Int before = h.position;
		bool acted; // 이번 틱을 이동 시도·대기로 썼는가
		if (move.ForcePassTrap)
		{
			_forcePass.ClearCache(); // 공유 스크래치라 이전 유닛의 이동 캐시를 쓰지 않게 한다
			acted = _forcePass.TryGetNextStep(h, move.Destination, out Dir dir);
			if (acted)
			{
				h.Move(dir);
				acted = h.position != before; // 대기 개념이 없으니 걸음이 안 나가면 막힌 것이다
				if (!acted) AIMovementHelper.LearnBumpedWalls(h, dir);
			}
		}
		else
		{
			acted = AIMovementHelper.MoveTowardsPos(h, move.Destination); // 점유 대기(OccupancySystem.TryHold)도 true를 돌려준다
		}

		// "걸었다"는 반환값이 아니라 실제 위치 변화로 판정한다 — 점유 대기 틱을 걸은 칸으로 세면 우회 구간에서 기다리기만 해도 한도(6칸)가 소진된다.
		if (h.position != before)
		{
			move.StuckTurns = 0;
			if (move.SegmentBaseline >= 0) move.SegmentWalked++; // 돌아가는 구간에서 이미 걸은 거리 — 한도 계산에 합산된다(R13)
		}
		else if (!acted) { move.StuckTurns++; RelocateOccupiedRestSlot(h, move); } // 대기는 막힘이 아니다
		return BTStatus.Running;
	}

	// 거점 휴식 자리를 다른 유닛이 차지해 걸음이 계속 실패하면 비어 있는 자리로 바꾼다(후퇴도주 검증 D4) — 점유된 목적지 칸은 A*가 닿을 수 없고, 도착 반경 밖의 낙오자는 영영 휴식 대상(AtBase)이 되지 못한다.
	const int OccupiedSlotStuckTurns = 3;

	static void RelocateOccupiedRestSlot(Human h, RetreatMoveState move)
	{
		if (move.DestinationKind != RetreatDestination.Base || move.StuckTurns < OccupiedSlotStuckTurns) return;
		var session = h.Session;
		var room = h.party?.Retreat.BaseRoom;
		if (session == null || room == null) return;

		var key = new Vector3Int(move.Destination.x, move.Destination.y, h.currentFloor);
		if (!session.unitGrid.TryGetValue(key, out Unit occupant) || occupant == null || occupant == h || occupant.hp <= 0f) return;
		if (!RetreatBases.ReassignRestSlot(h, room, h.party.Retreat.ClaimedRestSlots)) return;

		move.NextDestinationRefreshTime = 0f; // 다음 틱에 새 자리로 목적지를 갱신한다(TryResolveDestination)
		move.StuckTurns = 0;
	}

	// ─────────────────────────── 차단 대응 ───────────────────────────

	static void EndBlock(Human h, RetreatMoveState move)
	{
		if (move.Block == RetreatBlockKind.None) return;
		h.ClearAttackObjectTarget();
		move.ClearBlock();
	}

	// 같은 종류·같은 대상이면 진행 중인 대응을 유지하고, 바뀌었으면 새 대응으로 교체한다.
	static void UpdateBlock(Human h, RetreatMoveState move, RetreatBlockResolver.Verdict v, float now)
	{
		bool same = move.Block == v.Kind
			&& (v.Kind != RetreatBlockKind.Door && v.Kind != RetreatBlockKind.Trap || move.BlockObjectPos == v.ObjectPos)
			&& (v.Kind != RetreatBlockKind.Enemy || move.BlockEnemy == v.Enemy);
		if (same) return;

		EndBlock(h, move);
		move.Block = v.Kind;
		move.BlockObjectPos = v.ObjectPos;
		move.BlockEnemy = v.Enemy;
		move.BlockSince = now;
	}

	// 반환이 null이면 이 대응이 이번 틱의 행동을 정하지 못한 것(일반 이동으로 이어 간다).
	static BTStatus? RunBlock(Human h, RetreatMoveState move, float now)
	{
		switch (move.Block)
		{
			case RetreatBlockKind.Door: return RunDoor(h, move);
			case RetreatBlockKind.Enemy: return RunEnemy(h, move);
			case RetreatBlockKind.Trap: return RunTrap(h, move);
			default: return null; // Unreachable — 목적지를 유지하고 일반 이동을 시도하며 다음 점검에서 길 정보를 다시 본다
		}
	}

	// 닫힌 상대 진영 문 — 자동 개방 없이 부순다(후퇴·도주라는 이유로 열어 주지 않는다, 명세서 5-3장). 인접하면 채널링(UnitFunction.OnUpdate가 피해), 아니면 가장 가까운 문 타일 곁으로 접근한다.
	static BTStatus? RunDoor(Human h, RetreatMoveState move)
	{
		var session = h.Session;
		if (session == null || !session.objectGrid.TryGetValue(move.BlockObjectPos, out InteractableObject door) || door.DoorHp <= 0f || !session.IsBlockedByClosedDoor(move.BlockObjectPos, h))
		{
			EndBlock(h, move);
			move.NextPathCheckTime = 0f; // 문이 사라졌다 — 바로 경로를 다시 본다
			return null;
		}

		if (door.IsAdjacentTo(h.position))
		{
			h.SetAttackObjectTarget(move.BlockObjectPos);
			return BTStatus.Running;
		}

		h.ClearAttackObjectTarget();
		Vector2Int near = door.NearestTileTo(h.position);
		if (AIMovementHelper.MoveTowardsPos(h, near)) { move.StuckTurns = 0; return BTStatus.Running; }

		if (++move.StuckTurns >= (AIConfigLoader.Behavior?.waitStuckTurnLimit ?? 4)) // 문 곁에 못 붙는다 — 이 대응을 접고 다음 점검에서 다시 판단한다
		{
			EndBlock(h, move);
			move.StuckTurns = 0;
		}
		return BTStatus.Running;
	}

	// 길을 막는 적 — 전멸까지 기다리지 않고 통과에 필요한 만큼만 그 적을 상대한다. 적이 사라지거나 경로가 열리면(다음 점검) 대응이 끝난다.
	static BTStatus? RunEnemy(Human h, RetreatMoveState move)
	{
		var enemy = move.BlockEnemy;
		if (enemy == null || enemy.hp <= 0f || enemy.currentFloor != h.currentFloor)
		{
			EndBlock(h, move);
			move.NextPathCheckTime = 0f;
			return null;
		}
		return CombatFSMState.EngageBlocker(h, enemy);
	}

	// 우회 불가 함정 — 가능한 파괴를 먼저, 안 되면 예상 피해를 알고 통과 후 HP > 0일 때만 강행한다. 둘 다 안 되면 목적지를 유지한 채 대기한다. 일반 함정 해제는 새로 시작하지 않는다.
	static BTStatus? RunTrap(Human h, RetreatMoveState move)
	{
		var session = h.Session;
		if (session == null || !session.objectGrid.TryGetValue(move.BlockObjectPos, out InteractableObject trap) || trap.IsCollected)
		{
			EndBlock(h, move);
			move.NextPathCheckTime = 0f;
			return null;
		}

		bool destroyable = trap.TrapMaxHp > 0f && h.physicalAttack > 0f;
		if (destroyable)
		{
			var pos2D = new Vector2Int(move.BlockObjectPos.x, move.BlockObjectPos.y);
			if (AIMovementHelper.IsAdjacent(h.position, pos2D))
			{
				h.SetAttackObjectTarget(move.BlockObjectPos);
				return BTStatus.Running;
			}
			h.ClearAttackObjectTarget();
			if (AIMovementHelper.MoveTowardsPos(h, pos2D)) return BTStatus.Running;
			return null;
		}

		float? knownDamage = h.personalMap.IsTrapRecorded(trap.Id) ? trap.TrapDamageMax : (float?)null;
		move.ForcePassTrap = RetreatPathMath.CanForcePassTrap(h.hp, knownDamage);
		return null; // ForcePassTrap이 켜졌으면 StepAlong이 함정을 무시한 경로로 간다, 아니면 일반 이동만 시도한다(다음 점검에서 재판단)
	}
}
