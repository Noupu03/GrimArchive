using System.Collections.Generic;
using UnityEngine;

public static class AIMovementHelper
{
	// 계단 접근·착지 후보 탐색의 반경 상한 — 어떤 배치에서도 이 반경 안엔 걸을 수 있는 열린 타일이 있다고 본다(TryResolveUnoccupiedStairArrival/MoveToStairs 공유).
	public const int MaxStairSearchRadius = 10;

	// 체비셰프(8방향 격자) 거리 — 여러 FSM 상태가 각자 중복 구현하던 것을 통합.
	public static int ChebyshevDistance(Vector2Int a, Vector2Int b)
		=> Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

	// 인접(기본 반경 1칸, 대각 포함) 판정 — 위 거리 계산의 가장 흔한 용도.
	public static bool IsAdjacent(Vector2Int a, Vector2Int b, int radius = 1)
		=> ChebyshevDistance(a, b) <= radius;

	// FactionBehavior 타입을 나열하는 대신, 실제 방 제한 여부를 결정하는 MovementAlgorithm
	// (RoomConfinedMovement)을 직접 확인해 단일 기준으로 통일한다.
	public static bool IsRoomConfined(Unit unit) => unit.MovementAlgorithm is RoomConfinedMovement;

	// '알려진 적 공격 범위' 타일 전부를 회피 대상으로 모은다(04번 4장). 체비셰프 사각형 근사(IsRangedFormationRole의 정밀도와 동일)이며, 미확인 종은 빠진다(회피 안 함 ≠ 안전 확정).
	public static void ComputeKnownAttackRangeAvoidTiles(Unit self, HashSet<Vector2Int> result)
	{
		if (!(self is Human human) || human.Knowledge == null) return;

		foreach (var e in self.personalSpottedEnemies)
		{
			if (e == null || e.Health == null || e.Health.hp <= 0 || e.currentFloor != self.currentFloor) continue;
			if (e.unitType == null || !human.Memory.personalMap.TryGetKnownAttackRange(e.unitType.typeName, out int range)) continue;

			for (int dx = -range; dx <= range; dx++)
				for (int dy = -range; dy <= range; dy++)
					result.Add(new Vector2Int(e.position.x + dx, e.position.y + dy));
		}
	}

	// 원거리공격/근접·원거리지원 역할만 회피형 이동으로 승급한다(04번 4장; 근접탱커/DPS는 교전이 목적). 몬스터 전용 RoomConfinedMovement는 건드리지 않고, 이미 회피형이면 교체하지 않는다.
	public static void EnsureAvoidanceMovementForRole(Unit unit, CombatRole role)
	{
		bool needsAvoidance = role == CombatRole.RangedDps || role == CombatRole.RangedSupport || role == CombatRole.MeleeSupport;
		if (!needsAvoidance) return;
		if (unit.MovementAlgorithm is AttackRangeAvoidingMovement || unit.MovementAlgorithm is RoomConfinedMovement) return;
		if (unit.MovementAlgorithm is AStarMovement)
			unit.MovementAlgorithm = new AttackRangeAvoidingMovement();
	}

	// '노출/직행' 기준선 조회 전용 스크래치 인스턴스 — 유닛의 MovementAlgorithm과 별개로 이동 캐시를 오염시키지 않는 1회성 비교 질의에만 쓴다.
	private static readonly AStarMovement _exposedPathScratch = new AStarMovement();

	// 함정 통과 판단용 스크래치 — 함정을 무시(TrapMoveMode.Off)한 최단 경로를 재 유닛 자신의 전투 모드 탐색과 비교해 '함정 경로가 우회보다 빠른가'(v0.6 9-13)를 판정한다.
	private static readonly AStarMovement _trapPassScratch = new AStarMovement { TrapModeOverride = TrapMoveMode.Off };

	// 긴급 아군 보호로 함정을 밟고 지나가는 경우만 허용하는 예외(03번 9장 통과 판단표, 02번 9장): 함정 무시 최단 경로가 알려진 함정을 지나고, 우회보다 짧고, 보호 대상 HP ≤ 30%이며, 이동자 조건(기록 함정: 피해 후 HP ≥ 30% / 미기록: 현재 HP ≥ 60%)을 만족하면 한 걸음 간다(밟으면 TriggerTrapIfStepped가 피해). 개입하지 않으면 false.
	private static bool TryMoveThroughTrapsToProtect(Unit unit, Unit protect, Vector2Int destination)
	{
		if (!(unit is Human human) || protect == null || !TrapAvoidance.IsEnabled(unit)) return false;
		if (human.personalMap.KnownTrapTiles.Count == 0) return false;

		var traps = new List<TrapAvoidance.KnownTrap>();
		TrapAvoidance.CollectKnownTraps(human, traps);
		if (traps.Count == 0) return false;

		// 목적지는 보호 대상의 타일(점유됨)이다 — 경로 조회는 목표 점유를 막지 않아(RunQuerySearch) 그 타일까지 길이를 잴 수 있다.
		if (!_trapPassScratch.TryGetPathTiles(unit, destination, out List<Vector2Int> trapPath)) return false;

		float recordedDamage = 0f;
		int recordedCount = 0, unrecordedCount = 0;
		foreach (var t in traps)
		{
			if (!trapPath.Contains(t.Tile)) continue;
			if (t.Recorded) { recordedCount++; recordedDamage += t.DamageMax; }
			else unrecordedCount++;
		}
		if (recordedCount + unrecordedCount == 0) return false; // 그 경로가 함정을 안 지난다 — 일반 로직

		int detourSteps = int.MaxValue; // 우회가 없으면 함정 경로가 항상 빠르다
		if (unit.MovementAlgorithm is AStarMovement own && own.TryGetPathLength(unit, destination, out int detourLength, out _)) detourSteps = detourLength;

		var cfg = AIConfigLoader.Behavior;
		float recordedRatio = cfg?.trapRescueMinHpRatioRecorded ?? ExplorationMath.TrapAllyRescueMinHpRatioAfterHit;
		float unrecordedRatio = cfg?.trapRescueMinHpRatioUnrecorded ?? ExplorationMath.TrapAllyRescueUnrecordedMinCurrentHpRatio;
		bool allowed = true;
		if (recordedCount > 0)
			allowed &= ExplorationMath.CanPassTrapForProtect(true, protect.hp, protect.maxHp, unit.hp, unit.maxHp, recordedDamage, trapPath.Count, detourSteps, recordedRatio, unrecordedRatio);
		if (unrecordedCount > 0)
			allowed &= ExplorationMath.CanPassTrapForProtect(false, protect.hp, protect.maxHp, unit.hp, unit.maxHp, 0f, trapPath.Count, detourSteps, recordedRatio, unrecordedRatio);
		if (!allowed) return false;

		_trapPassScratch.ClearCache(); // 여러 유닛이 공유하는 스크래치라 이전 유닛의 이동 캐시를 쓰지 않게 한다
		if (!_trapPassScratch.TryGetNextStep(unit, destination, out Dir dir)) return false;
		Vector2Int before = unit.position;
		unit.Move(dir);
		return unit.position != before;
	}

	// 안전 경로(회피형)가 더 빠르면 그걸 쓰고, 느리면 노출(직행) 경로가 지나는 '알려진 공격범위' 구간의 예상 피해를 계산해 자기 HP 잔여율로 감수 여부를 정한다(02번 9항).
	public static bool MoveTowardsProtectTargetWithRiskCheck(Unit unit, Unit protect)
	{
		Vector2Int destination = protect.position;
		if (TryMoveThroughTrapsToProtect(unit, protect, destination)) return true;

		if (!(unit.MovementAlgorithm is AttackRangeAvoidingMovement safeAlgo))
			return MoveTowardsPos(unit, destination); // 회피형이 아니면 안전/노출 구분 자체가 무의미

		if (!_exposedPathScratch.TryGetPathTiles(unit, destination, out List<Vector2Int> exposedTiles))
			return MoveTowardsPos(unit, destination);

		if (safeAlgo.TryGetPathLength(unit, destination, out int safeSteps, out _) && safeSteps <= exposedTiles.Count)
			return MoveTowardsPos(unit, destination); // 안전 경로가 이미 더 빠르거나 같음 — 위험 감수 불필요

		bool estimable = TryEstimateExposureDamage(unit, exposedTiles, out float exposureDamage);
		float projectedRatio = (unit.hp - exposureDamage) / Mathf.Max(1f, unit.maxHp);

		if (CombatScoreMath.IsExposureRouteAllowed(estimable, projectedRatio))
		{
			_exposedPathScratch.ClearCache(); // 여러 유닛이 공유하는 스크래치라 이전 유닛의 이동 캐시를 쓰지 않게 한다
			if (!_exposedPathScratch.TryGetNextStep(unit, destination, out Dir dir)) return false;
			Vector2Int before = unit.position;
			unit.Move(dir);
			return unit.position != before;
		}
		return MoveTowardsPos(unit, destination); // 위험 과함 — 안전(회피) 경로 유지
	}

	// 노출 경로가 지나는 타일 중 '알려진 적 공격범위'와 겹치는 종마다 예상 스킬피해량(02번 9항)의 최댓값 1회분만 더한다(공격 횟수·도달시점이 불확실한 건 확정 피해처럼 안 더함). 자신의 방어력도 실제 데미지 파이프라인과 같이 반영해 1 이하로 안 내려가게 한다.
	// 반환값 = 추정 가능 여부(02번 9장): 노출 경로의 종에게 공격 스킬이 있는데 피해를 하나도 추정할 수 없으면 false — 호출부는 0 피해로 계산하지 말고 그 노출 경로를 제외해야 한다.
	private static bool TryEstimateExposureDamage(Unit unit, List<Vector2Int> exposedTiles, out float total)
	{
		total = 0f;
		if (!(unit is Human human) || human.Knowledge == null) return true;

		var countedSpecies = new HashSet<string>();
		foreach (var e in unit.personalSpottedEnemies)
		{
			if (e == null || e.Health == null || e.Health.hp <= 0 || e.currentFloor != unit.currentFloor || e.unitType == null) continue;
			string species = e.unitType.typeName;
			if (countedSpecies.Contains(species)) continue;
			if (!human.Memory.personalMap.TryGetKnownAttackRange(species, out int range)) continue;

			bool crosses = false;
			foreach (var t in exposedTiles) { if (ChebyshevDistance(t, e.position) <= range) { crosses = true; break; } }
			if (!crosses) continue;

			var skills = unit.Generate?.GetSkills(species);
			if (skills == null) { total = 0f; return false; } // 이 종의 스킬 정보를 얻을 수 없다 — 추정 불가

			float speciesMax = 0f;
			bool hasEnemySkill = false, anyDamageKnown = false;
			foreach (var s in skills)
			{
				if (s == null || s.Affinity != SkillAffinity.Enemy) continue;
				hasEnemySkill = true;
				if (!human.Knowledge.TryGetExpectedSkillDamage(human, species, s.SkillName, out int rawDmg)) continue;
				anyDamageKnown = true;

				float defense = SkillAction.IsMagicalDamage(s) ? unit.CombatStat.magicalDefense : unit.CombatStat.physicalDefense;
				float afterDefense = CombatScoreMath.DamageAfterDefense(rawDmg, defense);

				if (afterDefense > speciesMax) speciesMax = afterDefense;
			}
			if (hasEnemySkill && !anyDamageKnown) { total = 0f; return false; } // 피해를 하나도 모른다 — 추정 불가(02-07: 노출 경로 제외)
			total += speciesMax;
			countedSpecies.Add(species);
		}
		return true;
	}

	// 방 제한 유닛의 무작위 인접 이동 — NavigationFSMState.MoveRandomlyValid와 IdleFSMState가
	// 공유한다. anchor/radius로 배회 반경을 제한할 수 있고, forceRoomConfine=true면 MovementAlgorithm
	// 종류와 무관하게 방/문 제한을 강제한다(RoomConfinedMovement가 안 붙은 유닛도 대기 배회는 방 안에 묶기 위함).
	public static bool TryMoveRandomlyWithinRadius(Unit unit, Vector2Int? anchor = null, int radius = int.MaxValue, bool forceRoomConfine = false)
	{
		bool roomConfined = forceRoomConfine || IsRoomConfined(unit);
		Room myRoom = null;
		if (roomConfined && unit.Session?.roomGrid != null)
			unit.Session.roomGrid.TryGetValue(new Vector3Int(unit.position.x, unit.position.y, unit.currentFloor), out myRoom);

		int startOffset = Random.Range(0, 8);
		for (int i = 0; i < 8; i++)
		{
			Dir tryDir = (Dir)((startOffset + i) % 8);
			Vector2Int dirVec = unit.GetDirVector(tryDir);
			Vector2Int nextPos = unit.position + dirVec;
			if (!unit.CanMove(nextPos)) continue;
			// A*를 안 타는 배회도 알려진 활성 함정의 인접 1칸 구역에 들어가지 않는다.
			if (TrapAvoidance.BlocksGeneralStep(unit, nextPos)) continue;

			if (anchor.HasValue && ChebyshevDistance(nextPos, anchor.Value) > radius) continue;

			if (roomConfined && unit.Session != null)
			{
				if (unit.Session.IsDoorTile(new Vector3Int(nextPos.x, nextPos.y, unit.currentFloor)))
					continue;
				if (myRoom != null)
				{
					if (!unit.Session.roomGrid.TryGetValue(new Vector3Int(nextPos.x, nextPos.y, unit.currentFloor), out Room nextRoom)
						|| nextRoom != myRoom)
						continue;
				}
			}

			// Move()의 대각선 코너 커팅 방지 로직과 동일하게 먼저 검사한다.
			if (Mathf.Abs(dirVec.x) == 1 && Mathf.Abs(dirVec.y) == 1)
			{
				if (!unit.CanMove(unit.position + new Vector2Int(dirVec.x, 0)) ||
					!unit.CanMove(unit.position + new Vector2Int(0, dirVec.y))) continue;
			}
			unit.Move(tryDir);
			return true;
		}
		return false;
	}

	// 문 앞 통과 구간(05번 3장: 문 앞 2칸) 안인가 — tile 자신이나 체비셰프 거리 DoorClearance 미만 칸에 문 타일·게이트 문턱이 있으면 true(문이 파괴돼도 문턱은 남는다). 집결 자리 후보 제외에 쓰며 기준 타일만 본다(인류 전부 1×1).
	public static bool IsWithinDoorClearance(GameSession session, int floor, Vector2Int tile)
	{
		if (session == null) return false;
		int reach = PartyFormationMath.DoorClearance - 1;
		for (int dx = -reach; dx <= reach; dx++)
		{
			for (int dy = -reach; dy <= reach; dy++)
			{
				var p = new Vector3Int(tile.x + dx, tile.y + dy, floor);
				if (session.IsDoorTile(p) || session.TryGetGateKeyAt(p, out _)) return true;
			}
		}
		return false;
	}

	// 계단 도착(순간이동) 지점을 점유 없는 칸으로 고른다 — CanMove를 거치지 않는 순간이동(CrossStairs, HumanWaveManager 강제 이동/퇴각)이 모두 이 헬퍼를 거쳐야 한 좌표에 여러 유닛이 겹치지 않는다. 반경 1이 막히면 반경을 넓혀 찾고(FindDoorWaitSlot과 같은 방식), false는 MaxStairSearchRadius 안이 전부 불가능할 때뿐이다.
	public static bool TryResolveUnoccupiedStairArrival(GameSession session, int arrivalFloor, int fromFloor, out Vector2Int pos)
	{
		pos = Vector2Int.zero;
		if (session?.cmap == null) return false;

		for (int radius = 1; radius <= MaxStairSearchRadius; radius++)
		{
			if (!session.cmap.TryGetStairApproachCandidatesAtRadius(arrivalFloor, fromFloor, radius, out var candidates))
				continue; // 이 반경엔 걸을 수 있는 타일 자체가 없음(전부 벽/구조물) — 다음 반경 시도

			foreach (var c in candidates)
			{
				bool occupied = session.unitGrid.TryGetValue(new Vector3Int(c.x, c.y, arrivalFloor), out Unit occupant)
					&& occupant != null && occupant.hp > 0;
				if (!occupied) { pos = c; return true; }
			}
		}
		return false;
	}

	// 반환값: 실제로 한 칸이라도 다가갔으면 true, 목표 칸이 완전히 막혀 제자리에 머물렀으면 false —
	// 호출부(PlayerCommandFSMState)가 이 신호로 "길이 막혔다"를 판단해 목표를 재지정한다.
	public static bool MoveTowardsPos(Unit unit, Vector2Int targetPos)
	{
		// 가려던 자리를 같은 진영 유닛이 차지했다면 대기 vs 우회를 예상 도착시간으로 비교한다(점유 충돌 판단). true = 이번 주기는 대기(또는 비켜서기)로 썼다.
		if (OccupancySystem.TryHold(unit, targetPos)) return true;

		if (unit.MovementAlgorithm != null && unit.MovementAlgorithm.TryGetNextStep(unit, targetPos, out Dir nextDir))
		{
			// "방향을 받았다"가 아니라 "실제로 움직였다"를 반환한다 — A*와 Move()의 판정이 어긋나면(코너
			// 커팅 등) Move()가 조용히 실패할 수 있어, 그대로 true를 반환하면 stuckTurns가 리셋돼 영원히 얼어붙는다.
			Vector2Int before = unit.position;
			unit.Move(nextDir);
			return unit.position != before;
		}
		return false;
	}

	public static bool MoveTowardsTarget(Unit unit, Unit target)
		=> MoveTowardsPos(unit, target.position);

	// 목표 칸이 막혀 더 다가갈 수 없을 때, 바로 옆 8칸 중 실제로 갈 수 있는 가장 가까운 빈 칸을
	// 대신 반환한다(하나도 없으면 center 그대로 반환해 "재지정도 불가능"을 알림).
	public static Vector2Int FindNearbyOpenTile(Unit unit, Vector2Int center)
	{
		Vector2Int best = center;
		int bestDist = int.MaxValue;
		for (int dx = -1; dx <= 1; dx++)
		for (int dy = -1; dy <= 1; dy++)
		{
			if (dx == 0 && dy == 0) continue;
			Vector2Int cand = center + new Vector2Int(dx, dy);
			if (!unit.CanMove(cand)) continue;
			int dist = ChebyshevDistance(cand, unit.position);
			if (dist < bestDist) { bestDist = dist; best = cand; }
		}
		return best;
	}

	// observer의 개인 지도로 '아는'(PropagationSystem.KnowsDoor) 현재 방의 문 중 하나를 고른다(05번 1·8장). targetCenter가 있으면 그 방향에 가장 가까운 문(알려진 다음 이동 문), 없으면 observer에게 가장 가까운 문 — 방 그래프 최단경로 대신 좌표 거리 근사.
	public static bool TryFindKnownDoorInCurrentRoom(Human observer, Vector2? targetCenter, out Vector2Int doorPos, out int doorFloor)
	{
		doorPos = default; doorFloor = 0;
		if (observer == null || observer.currentRoom == null || GameSession.Instance == null) return false;

		Vector2 reference = targetCenter ?? (Vector2)observer.position;
		float bestDistSq = float.MaxValue;
		bool found = false;

		foreach (var obj in GameSession.Instance.objectGrid.Values)
		{
			if (obj == null || obj.Position.z != observer.currentFloor) continue;
			if (!observer.currentRoom.Bounds.Contains(new Vector2Int(obj.Position.x, obj.Position.y))) continue;
			if (obj.Tags == null || !obj.Tags.Contains(DoorSystem.DoorTag)) continue;
			if (!PropagationSystem.KnowsDoor(observer, obj)) continue;

			float distSq = (new Vector2(obj.Position.x, obj.Position.y) - reference).sqrMagnitude;
			if (distSq < bestDistSq)
			{
				bestDistSq = distSq;
				doorPos = new Vector2Int(obj.Position.x, obj.Position.y);
				doorFloor = obj.Position.z;
				found = true;
			}
		}
		return found;
	}

	// 문 대기는 문 바로 앞 통과 구간(체비셰프 1 이내)을 비우고 그 밖에서 기다린다(05번 3장). claimedSlots는 호출부가 파티 전체에서 공유해 자리가 안 겹치게 하고, 반경 5 안에서 못 찾으면 문 위치를 그대로 반환한다(겹치더라도 못 오는 것보다 낫다).
	public static Vector2Int FindDoorWaitSlot(Unit unit, Vector2Int doorPos, HashSet<Vector2Int> claimedSlots)
	{
		// 문이 속한 쪽 방 안에서만 고른다 — 반경 안이라는 이유만으로 닫힌 적 문 너머의 닿을 수 없는 타일을 뽑지 않게. 방을 못 구하거나 자리가 없으면 제한 없이 고른다.
		Room doorRoom = null;
		unit.Session?.roomGrid?.TryGetValue(new Vector3Int(doorPos.x, doorPos.y, unit.currentFloor), out doorRoom);
		// 문 앞 통과 구간을 비운 자리가 우선이고 문 주변이 좁아 없으면 통행 가능한 인접 지점으로 완화한다(05번 3장). 반경은 이 문 타일 하나 기준이라 1×2 묶음의 다른 칸·문턱은 구간 검사가 따로 본다.
		Vector2Int slot;
		if (doorRoom != null
			&& (TryFindDoorWaitSlot(unit, doorPos, claimedSlots, doorRoom, true, out slot) || TryFindDoorWaitSlot(unit, doorPos, claimedSlots, doorRoom, false, out slot)))
			return slot;
		return TryFindDoorWaitSlot(unit, doorPos, claimedSlots, null, true, out slot) || TryFindDoorWaitSlot(unit, doorPos, claimedSlots, null, false, out slot) ? slot : doorPos;
	}

	private static bool TryFindDoorWaitSlot(Unit unit, Vector2Int doorPos, HashSet<Vector2Int> claimedSlots, Room requiredRoom, bool clearPassage, out Vector2Int slot)
	{
		slot = default;
		for (int radius = 2; radius <= 5; radius++)
		{
			Vector2Int best = default;
			int bestDist = int.MaxValue;
			bool found = false;
			for (int dx = -radius; dx <= radius; dx++)
			for (int dy = -radius; dy <= radius; dy++)
			{
				if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue; // 이번 반경의 테두리만
				Vector2Int cand = doorPos + new Vector2Int(dx, dy);
				if (claimedSlots.Contains(cand)) continue;
				if (!unit.CanMove(cand)) continue;
				if (clearPassage && IsWithinDoorClearance(unit.Session, unit.currentFloor, cand)) continue;
				if (requiredRoom != null && !(unit.Session.roomGrid.TryGetValue(new Vector3Int(cand.x, cand.y, unit.currentFloor), out Room candRoom) && candRoom == requiredRoom)) continue;
				int dist = ChebyshevDistance(cand, unit.position);
				if (dist < bestDist) { bestDist = dist; best = cand; found = true; }
			}
			if (found)
			{
				claimedSlots.Add(best);
				slot = best;
				return true;
			}
		}
		return false;
	}

	// 유닛 자기 위치 기준 혼잡 판정 전용 — HasAnyStructurallyOpenNeighbor(unit, unit.position)는
	// center 자신이 항상 열려있어 무의미하므로 안 쓴다. 인접 8칸만(점유 무시) 확인해 판정한다.
	public static bool HasAnyStructurallyOpenAdjacentTile(Unit unit)
	{
		Vector2Int center = unit.position;
		for (int dx = -1; dx <= 1; dx++)
		for (int dy = -1; dy <= 1; dy++)
		{
			if (dx == 0 && dy == 0) continue;
			if (unit.CanMove(center + new Vector2Int(dx, dy), ignoreUnits: true)) return true;
		}
		return false;
	}

	// 적에게서 한 걸음 물러난다 — 이웃 8칸 중 적 반대 방향에 가장 가깝고 실제로 거리가 늘며 갈 수 있는 칸(벽·점유·방 제한·코너 커팅 통과)으로 간다(RangedSpacingMath.TryChooseRetreatStep). 예전엔 "적 반대편 N칸" 좌표를 A*로 향해, 그 좌표가 벽이면 가장 가까운 도달 노드(옆쪽일 수 있음)로 새 좌우로 떨었다. 물러날 칸이 없으면(몰림) false — 호출부가 그 자리에서 싸운다.
	public static bool MoveAwayFromTarget(Unit unit, Unit target)
	{
		Vector2Int pos = unit.position;
		bool CanStep(int dx, int dy)
		{
			Vector2Int next = pos + new Vector2Int(dx, dy);
			if (!unit.CanMove(next) || unit.IsRoomLeaveBlocked(next) || LeavesConfinedRoom(unit, next)) return false;
			// Move()의 코너 커팅 검사와 같다 — 대각선은 양옆 직교 칸도 갈 수 있어야 한다.
			if (dx != 0 && dy != 0 && (!unit.CanMove(pos + new Vector2Int(dx, 0)) || !unit.CanMove(pos + new Vector2Int(0, dy)))) return false;
			return true;
		}

		if (!RangedSpacingMath.TryChooseRetreatStep(pos.x, pos.y, target.position.x, target.position.y, CanStep, out int stepX, out int stepY)) return false;
		unit.Move(SkillAction.GetDirection8(new Vector2Int(stepX, stepY)));
		return unit.position != pos;
	}

	// 방 제한 몬스터(RoomConfinedMovement)는 방 경계를 A*(IsTileWalkable)로만 지킨다 — A*를 거치지 않는 한 걸음 후퇴가 방 밖·문 타일로 나가지 않게 같은 규칙을 직접 적용한다(플레이어 명령 중은 예외, RoomConfinedMovement와 동일).
	private static bool LeavesConfinedRoom(Unit unit, Vector2Int next)
	{
		if (!IsRoomConfined(unit) || unit.isManualMoveCommand || unit.Session?.roomGrid == null) return false;
		int floor = unit.currentFloor;
		if (unit.Session.IsDoorTile(new Vector3Int(next.x, next.y, floor))) return true;
		if (!unit.Session.roomGrid.TryGetValue(new Vector3Int(unit.position.x, unit.position.y, floor), out Room current)) return false;
		return !unit.Session.roomGrid.TryGetValue(new Vector3Int(next.x, next.y, floor), out Room nextRoom) || nextRoom != current;
	}

	// 03문서 6장 보호 포메이션 이동 로직 — backDistance<=1이면 근접, 더 크면 원거리 배치.
	public static void MoveToEscortSlot(Human human, float backDistance)
	{
		if (human.currentFormation == null || human.currentFormation.EscortTarget == null || !human.currentFormation.EscortTarget.IsInteracting)
		{
			Human target = human.FindDirectlyVisibleInteractingAlly();
			if (target == null) { human.currentFormation = null; return; }
			human.currentFormation = new FormationState { EscortTarget = target };
		}

		Human escortTarget = human.currentFormation.EscortTarget;
		Vector2Int slot = human.GetEscortSlotPosition(escortTarget, backDistance);
		MoveTowardsPos(human, slot);
	}
}
