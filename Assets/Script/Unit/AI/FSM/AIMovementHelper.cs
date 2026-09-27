using System.Collections.Generic;
using UnityEngine;

public static class AIMovementHelper
{
	// 계단 접근·착지 후보 탐색의 반경 상한 — 어떤 방 배치에서도 이 반경 안엔 걸을 수 있는 열린
	// 타일이 있다고 본다(TryResolveUnoccupiedStairArrival/NavigationFSMState.MoveToStairs 공유).
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

	// 04번 문서 4장: "알려진 적 공격 범위"에 해당하는 타일 전부를 회피 대상으로 모은다. 체비셰프
	// 사각형(회전 히트박스 아님)으로 근사 — IsRangedFormationRole이 이미 스칼라 HitRange만으로
	// 원거리/근접을 가르는 것과 동일한 정밀도 수준. 미확인 종은 자연히 빠진다(회피 안 함 ≠ 안전 확정,
	// 문서가 "모르는 부분을 안전하다고 단정하지 않는다"고 명시).
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

	// 04번 문서 4장: 원거리공격/근접·원거리지원 역할만 회피형 이동으로 승급한다(근접탱커/DPS는 교전
	// 자체가 목적이라 제외). RoomConfinedMovement(몬스터 전용, 04장 표의 "몬스터" 행으로 별도 관리)는
	// 건드리지 않는다 — 이미 회피형이면 중복 교체하지 않는다.
	public static void EnsureAvoidanceMovementForRole(Unit unit, CombatRole role)
	{
		bool needsAvoidance = role == CombatRole.RangedDps || role == CombatRole.RangedSupport || role == CombatRole.MeleeSupport;
		if (!needsAvoidance) return;
		if (unit.MovementAlgorithm is AttackRangeAvoidingMovement || unit.MovementAlgorithm is RoomConfinedMovement) return;
		if (unit.MovementAlgorithm is AStarMovement)
			unit.MovementAlgorithm = new AttackRangeAvoidingMovement();
	}

	// "노출/직행" 기준선 조회 전용 스크래치 인스턴스 — 유닛 자신의 MovementAlgorithm(회피형일 수 있음)과
	// 별개로, 이동 캐시를 오염시키지 않는 1회성 비교 질의에만 쓴다.
	private static readonly AStarMovement _exposedPathScratch = new AStarMovement();

	// 02번 9번 항목: 안전 경로(회피형)가 이미 더 빠르면 그냥 그걸 쓰고, 안전 경로가 느리면 노출(직행)
	// 경로가 지나는 "알려진 공격범위" 구간의 예상 피해를 계산해 자기 HP 잔여율로 감수 여부를 판정한다.
	public static bool MoveTowardsProtectTargetWithRiskCheck(Unit unit, Vector2Int destination)
	{
		if (!(unit.MovementAlgorithm is AttackRangeAvoidingMovement safeAlgo))
			return MoveTowardsPos(unit, destination); // 회피형이 아니면 안전/노출 구분 자체가 무의미

		if (!_exposedPathScratch.TryGetPathTiles(unit, destination, out List<Vector2Int> exposedTiles))
			return MoveTowardsPos(unit, destination);

		if (safeAlgo.TryGetPathLength(unit, destination, out int safeSteps, out _) && safeSteps <= exposedTiles.Count)
			return MoveTowardsPos(unit, destination); // 안전 경로가 이미 더 빠르거나 같음 — 위험 감수 불필요

		float exposureDamage = EstimateExposureDamage(unit, exposedTiles);
		float projectedRatio = (unit.hp - exposureDamage) / Mathf.Max(1f, unit.maxHp);

		if (projectedRatio >= CombatScoreMath.ProtectApproachDamageRiskHpFloor)
		{
			if (!_exposedPathScratch.TryGetNextStep(unit, destination, out Dir dir)) return false;
			Vector2Int before = unit.position;
			unit.Move(dir);
			return unit.position != before;
		}
		return MoveTowardsPos(unit, destination); // 위험 과함 — 안전(회피) 경로 유지
	}

	// 노출 경로가 지나는 타일 중 "알려진 적 공격범위"와 겹치는 종마다, 그 종의 예상 스킬피해량(02번 9번
	// 항목) 중 최댓값 1회분만 더한다 — "공격 횟수·도달시점 불확실한 건 확정피해처럼 안 더한다"(원문).
	// 피해를 추정할 수 없는 스킬(TryGetExpectedSkillDamage 실패)은 자연히 합산에서 빠진다. 원문이 명시한
	// "자신의 알려진 방어·감소 효과"도 반영한다 — 실제 데미지 파이프라인(UnitFunction.
	// TakePhysicalDamage/TakeMagicalDamage)과 동일하게 방어력을 뺀 뒤 1 이하로 안 내려가게 한다.
	// (2026-09-27 수정: 처음엔 방어력을 안 빼고 raw 값을 그대로 썼음 — 사용자 지적으로 발견.)
	private static float EstimateExposureDamage(Unit unit, List<Vector2Int> exposedTiles)
	{
		if (!(unit is Human human) || human.Knowledge == null) return 0f;

		float total = 0f;
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

			float speciesMax = 0f;
			var skills = unit.Generate?.GetSkills(species);
			if (skills != null)
			{
				foreach (var s in skills)
				{
					if (s == null || s.Affinity != SkillAffinity.Enemy) continue;
					if (!human.Knowledge.TryGetExpectedSkillDamage(human, species, s.SkillName, out int rawDmg)) continue;

					// HumanKnowledgeBase.SeedSkillDamageEstimates의 _magicalSkillArchetypes와 동일한
					// 물리/마법 분류(GroundAoE/Curse만 마법) — 여긴 SkillAction 인스턴스라 타입으로 판정.
					bool isMagical = s is SkillAction_GroundAoE || s is SkillAction_Curse;
					float defense = isMagical ? unit.CombatStat.magicalDefense : unit.CombatStat.physicalDefense;
					float afterDefense = Mathf.Max(1f, rawDmg - defense);

					if (afterDefense > speciesMax) speciesMax = afterDefense;
				}
			}
			total += speciesMax;
			countedSpecies.Add(species);
		}
		return total;
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

	// 계단 도착(순간이동) 지점을 점유 없는 칸으로 고른다. CanMove를 거치지 않는 순간이동성 이동
	// (CrossStairs, HumanWaveManager 강제 이동/퇴각)이 전부 이 헬퍼를 거쳐야 한 좌표에 여러 유닛이
	// 겹쳐 텔레포트되는 걸 막는다. 반경 1이 막혀 있거나 꽉 찼으면 반경을 넓혀가며 계속 찾는다
	// (FindDoorWaitSlot과 동일 이디엄) — false는 MaxStairSearchRadius 안 전체가 불가능할 때뿐.
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

	// 05번 문서 3장: 문 대기는 문 바로 앞 통과 구간(체비셰프 거리 1 이내)을 비우고 그 밖에서 기다린다.
	// claimedSlots는 호출부가 파티 전체에 걸쳐 공유해 자리가 안 겹치게 한다. 반경 5 안에서도 못
	// 찾으면(좁은 통로 등) 문 위치 그대로 반환한다 — 겹치더라도 완전히 못 오는 것보다 낫다는 폴백.
	public static Vector2Int FindDoorWaitSlot(Unit unit, Vector2Int doorPos, HashSet<Vector2Int> claimedSlots)
	{
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
				int dist = ChebyshevDistance(cand, unit.position);
				if (dist < bestDist) { bestDist = dist; best = cand; found = true; }
			}
			if (found)
			{
				claimedSlots.Add(best);
				return best;
			}
		}
		return doorPos;
	}

	// 명령 포기 오판 방지 — 벽/닫힌 문 때문인지 다른 유닛이 잠깐 몰려 막힌 것뿐인지 구분하려고
	// CanMove(ignoreUnits: true)로 점유 무시하고 지형만 재확인한다(후자를 오판하면 명령이 영구히 취소됨).
	public static bool HasAnyStructurallyOpenNeighbor(Unit unit, Vector2Int center)
	{
		if (unit.CanMove(center, ignoreUnits: true)) return true;
		for (int dx = -1; dx <= 1; dx++)
		for (int dy = -1; dy <= 1; dy++)
		{
			if (dx == 0 && dy == 0) continue;
			if (unit.CanMove(center + new Vector2Int(dx, dy), ignoreUnits: true)) return true;
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

	public static void MoveAwayFromTarget(Unit unit, Unit target, float desiredDist)
	{
		Vector2 away = (Vector2)(unit.position - target.position);
		if (away == Vector2.zero) away = Vector2.right;

		float maxComp = Mathf.Max(Mathf.Abs(away.x), Mathf.Abs(away.y));
		Vector2 scaled = away / maxComp;

		int targetDist = Mathf.RoundToInt(desiredDist);
		Vector2Int retreatPos = target.position + new Vector2Int(
			Mathf.RoundToInt(scaled.x * targetDist),
			Mathf.RoundToInt(scaled.y * targetDist)
		);
		MoveTowardsPos(unit, retreatPos);
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
