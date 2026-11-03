using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 집중 공격 지정을 받은 파티원 개인의 수행(리더 명령 11-1·12장): 지정 적을 공격 목표로 우선하고, 놓치면 마지막 확인 위치를 최대 5초 추적하며, 개인별 종료 조건으로 끝난다.
// 집중 공격 전체에 고정된 유지시간은 없다 — 쿨타임 10초·상위 쿨타임 15초·행동 우선 명령 5초는 종료 시간이 아니다. 종료한 지정을 근거로 이전 추적을 자동 재개하지 않는다.
// 호출 지점: UnitFunction.OnUpdate(Tick), CombatFSMState.SelectAttackTarget(TryGetFocusTarget), TacticalFSMState의 FocusPursuit 분기(NeedsPursuit·PursuitStep).
public static class FocusAttackSystem
{
	private const float TickIntervalSeconds = 0.1f;
	private const int PursuitStuckTurnLimit = 4; // 마지막 위치로 가는 이동이 이만큼 연속으로 진전 없으면 이 파티원의 지정을 끝낸다 (내부 판단 — combatChaseStuckTurnLimit와 같은 값)
	private const float PathBlockedLimitSeconds = 3f; // 접근 제한 거리 때문에 허용 경로가 없는 상태가 이만큼 이어지면 모든 허용 방법이 불가능하다고 본다 (내부 판단)

	// 매 틱 호출 — 개인 상태(시야 확인·놓침 시계·종료 조건)를 갱신한다. 받은 명령이 없으면 한 줄 판정으로 끝난다.
	public static void Tick(Human human) => Tick(human, Time.time);

	// 시각을 주입할 수 있는 형태(테스트용 — 배치 모드에서는 Time.time이 흐르지 않는다). 동작은 같다.
	public static void Tick(Human human, float now)
	{
		var state = human?.LeaderState;
		if (state == null || (state.GeneralFocus == null && state.SuperiorFocus == null && state.Action == null)) return;

		if (now < state.NextFocusTick) return;
		state.NextFocusTick = now + TickIntervalSeconds;

		if (state.Action != null && !state.Action.IsActive(now)) state.Action = null; // 행동 우선 명령 5초 적용 종료

		if (state.SuperiorFocus != null && TickOrder(human, state.SuperiorFocus, now)) state.SuperiorFocus = null;
		if (state.GeneralFocus != null && TickOrder(human, state.GeneralFocus, now)) state.GeneralFocus = null;
		// 상위 명령이 끝났으면 그때까지 끝나지 않은 일반 명령만 다시 수행한다(16-1-표1) — 일반 쪽은 위에서 같은 틱에 종료 조건을 이미 확인했다.
		if (state.SuperiorFocus == null && state.GeneralFocus != null) state.GeneralFocus.Suspended = false;
	}

	// 한 지정의 개인 상태를 갱신한다. 끝났으면 true를 돌려준다(호출부가 비운다).
	private static bool TickOrder(Human human, FocusOrder order, float now)
	{
		if (RetreatSystem.IsBoundToRetreat(human)) return End(human, order, "후퇴·도주 전환");
		if (!IsIssuerStillCommanding(human, order)) return End(human, order, "명령한 지휘자의 지휘 상실");

		if (order.IsObject)
			return !IsObjectTargetValid(human, order.ObjectTarget.Value) && End(human, order, "차단물이 사라짐");

		var target = order.Target;
		if (target == null || target.hp <= 0f || !human.IsEnemy(target)) return End(human, order, "대상 사망·비적대 전환");

		bool seen = human.personalSpottedEnemies.Contains(target);
		if (seen)
		{
			// 직접 보고 있다 — 추적은 필요 없고 기존 교전 규칙으로 공격한다. 보고만으로는 기한이 늘지 않는다(아래 else).
			order.EverSeen = true;
			order.LostAt = -1f;
			order.ArrivedAt = -1f;
			order.PursuitStuckTurns = 0;
			order.LastKnownPosition = target.position;
			return false;
		}

		if (order.EverSeen && order.LostAt < 0f) order.LostAt = now; // 직접 보던 적을 방금 놓쳤다 — 이때부터 최대 5초
		if (HasOtherVisibleEnemy(human, target)) return End(human, order, "다른 적과 교전"); // 그 파티원만 추적·지정을 즉시 끝낸다(다른 파티원은 유지, 자동 재개 없음)
		if (LeaderMath.PursuitRemaining(order.PursuitStart, now) <= 0f) return End(human, order, "추적 5초 종료");
		if (order.ArrivedAt >= 0f && now - order.ArrivedAt >= LeaderMath.ConfirmWaitSeconds(order.PursuitStart, order.ArrivedAt) - 1e-4f) return End(human, order, "마지막 위치 확인 대기 종료(최대 3초, 전체 5초 안)");
		return false;
	}

	private static bool HasOtherVisibleEnemy(Human human, Unit exclude)
	{
		foreach (var e in human.personalSpottedEnemies)
			if (e != null && e != exclude && e.hp > 0f && e.currentFloor == human.currentFloor) return true;
		return false;
	}

	private static bool IsIssuerStillCommanding(Human human, FocusOrder order)
	{
		if (!LeaderCommandSettings.Enabled) return false;
		if (order.Authority == CommandAuthority.Superior) return ExpeditionCommandSystem.IsSuperiorCommanding(order.IssuerName);
		var party = human.party;
		return party != null && party.Leader != null && party.Leader.hp > 0f && party.Leader.name == order.IssuerName;
	}

	private static bool End(Human human, FocusOrder order, string reason)
	{
		if (order.IsObject) human.ClearAttackObjectTarget(); // 차단물 파괴 채널링을 접는다
		LogHelper.Log(LogHelper.GAME, $"[지휘] {human.name}의 {order.Command.ToKorean()} 지정 종료 — {reason}(지정: {(order.IsObject ? "차단물" : order.Target != null ? order.Target.name : "대상 소멸")})");
		return true;
	}

	// 이 파티원의 지정을 끝낸다(다른 파티원의 지정은 유지한다). SelectAttackTarget 같은 호출부가 즉시 종료해야 할 때 쓴다.
	public static void EndFocus(Human human, FocusOrder order, string reason)
	{
		var state = human.LeaderState;
		End(human, order, reason);
		if (state.SuperiorFocus == order) state.SuperiorFocus = null;
		if (state.GeneralFocus == order) state.GeneralFocus = null;
		if (state.SuperiorFocus == null && state.GeneralFocus != null) state.GeneralFocus.Suspended = false;
	}

	// ─────────────────────────── 전투 대상 선정 연결 ───────────────────────────

	// CombatFSMState.SelectAttackTarget이 부른다 — 지정 적이 지금 후보에 있으면 개인 대상 변경 이동 한도·점수 비교 없이 그 적을 공격 대상으로 우선한다(11-1장). 긴급 보호·치료는 호출부가 이미 먼저 처리했다.
	// 후보에 없으면: 시야에서 놓친 채 다른 적과 교전하게 된 것이므로 이 파티원의 지정을 끝낸다(12-2-표1). 후보가 비는 경우(전투 아님)는 호출부에 오지 않는다.
	public static bool TryGetFocusTarget(Human human, List<Unit> candidates, out Unit target)
	{
		target = null;
		if (!LeaderCommandSettings.Enabled) return false;
		var order = human.LeaderState?.EffectiveFocus;
		if (order == null || order.IsObject || order.Target == null) return false;

		if (candidates.Contains(order.Target)) { target = order.Target; return true; }
		if (candidates.Count > 0) EndFocus(human, order, human.personalSpottedEnemies.Contains(order.Target) ? "공격 위치·경로를 확인할 수 없음(추격 불가)" : "다른 적과 교전");
		return false;
	}

	// ─────────────────────────── 대상을 놓친 뒤 추적 ───────────────────────────

	// Tactical의 FocusPursuit 분기 조건 — 지정 적을 못 보고, 다른 교전이 없고, 추적 기한이 남았다. 통로 차단물 공동 파괴 지정(ObjectFocus)이 유효하면 그 파괴 행동도 이 분기가 맡는다.
	public static bool NeedsPursuit(Unit unit)
	{
		if (!LeaderCommandSettings.Enabled || !(unit is Human human)) return false;
		var order = human.LeaderState?.EffectiveFocus;
		if (order != null && order.IsObject) return IsObjectTargetValid(human, order.ObjectTarget.Value);
		if (order == null || order.Target == null || order.Target.hp <= 0f) return false;
		if (human.personalSpottedEnemies.Contains(order.Target) || HasOtherVisibleEnemy(human, order.Target)) return false;
		return LeaderMath.PursuitRemaining(order.PursuitStart, Time.time) > 0f;
	}

	// 마지막 확인 위치로 이동한다. 도착하면 제자리에서 직접 확인을 기다린다(대기 최대 3초는 전체 5초 안에 포함 — Tick이 종료한다). 한 칸도 못 다가가는 상태가 이어지면 이 파티원의 지정을 끝낸다.
	public static BTStatus PursuitStep(Human human)
	{
		var order = human.LeaderState?.EffectiveFocus;
		if (order == null) return BTStatus.Failure;
		if (order.IsObject) return ObjectStep(human, order);

		Vector2Int goal = order.LastKnownPosition;
		if (order.ArrivedAt < 0f)
		{
			if (AIMovementHelper.ChebyshevDistance(human.position, goal) <= 1 || human.currentFloor < 1)
			{
				order.ArrivedAt = Time.time; // 도착 — 직접 확인 대기 시작
			}
			else
			{
				int before = AIMovementHelper.ChebyshevDistance(human.position, goal);
				AIMovementHelper.MoveTowardsPos(human, goal);
				if (AIMovementHelper.ChebyshevDistance(human.position, goal) < before) order.PursuitStuckTurns = 0;
				else if (++order.PursuitStuckTurns >= PursuitStuckTurnLimit)
				{
					EndFocus(human, order, "마지막 위치로 갈 수 없음");
					return BTStatus.Failure;
				}
				return BTStatus.Running;
			}
		}

		// 도착해 확인을 기다리는 동안 마지막 위치 쪽을 본다.
		Vector2Int diff = goal - human.position;
		if (diff != Vector2Int.zero)
		{
			human.currentDir = SkillAction.GetDirection8(diff);
			human.Generate?.UpdateUnitSpriteForDirection(human);
		}
		return BTStatus.Running;
	}

	// ─────────────────────────── 통로 차단물 ───────────────────────────

	// 차단물 공동 파괴 지정을 받은 파티원의 한 걸음 — 차단물 인접 1칸으로 이동한 뒤 기존 오브젝트 채널링(Unit.SetAttackObjectTarget → UnitFunction.OnUpdate의 고정 초당 피해)으로 부순다.
	// 개인이 통로를 확보하는 동안에도 기존 적 지정은 이 지정에 교체되어 있다(12-3장: 리더가 차단물로 직접 목표를 바꾸면 이전 적 지정을 교체, 제거 뒤 자동 복원 없음).
	public static BTStatus ObjectStep(Human human, FocusOrder order)
	{
		var session = human.Session;
		Vector3Int pos = order.ObjectTarget.Value;
		if (session == null || !session.objectGrid.TryGetValue(pos, out InteractableObject obj) || obj == null) return BTStatus.Failure;

		if (obj.IsAdjacentTo(human.position))
		{
			human.SetAttackObjectTarget(pos);
			Vector2Int diff = obj.NearestTileTo(human.position) - human.position;
			if (diff != Vector2Int.zero)
			{
				human.currentDir = SkillAction.GetDirection8(diff);
				human.Generate?.UpdateUnitSpriteForDirection(human);
			}
			order.PursuitStuckTurns = 0;
			return BTStatus.Running;
		}

		human.ClearAttackObjectTarget();
		Vector2Int near = obj.NearestTileTo(human.position);
		int before = AIMovementHelper.ChebyshevDistance(human.position, near);
		AIMovementHelper.MoveTowardsPos(human, near);
		if (AIMovementHelper.ChebyshevDistance(human.position, near) < before) order.PursuitStuckTurns = 0;
		else if (++order.PursuitStuckTurns >= PursuitStuckTurnLimit)
		{
			EndFocus(human, order, "차단물에 다가갈 수 없음");
			return BTStatus.Failure;
		}
		return BTStatus.Running;
	}

	// 전투 상태(적이 보이는 중)에서도 차단물 지정은 이 파티원의 행동이다 — 피격 중이 아니면 CombatFSMState.ExecuteCombat이 먼저 이 걸음을 확인한다.
	public static bool TryObjectFocusStep(Human human)
	{
		if (!LeaderCommandSettings.Enabled || human.isHitThisTurn) return false;
		var order = human.LeaderState?.EffectiveFocus;
		if (order == null || !order.IsObject || !IsObjectTargetValid(human, order.ObjectTarget.Value)) return false;
		return ObjectStep(human, order) == BTStatus.Running;
	}

	public static bool IsObjectTargetValid(Unit viewer, Vector3Int pos)
	{
		var session = viewer?.Session;
		if (session == null || !session.objectGrid.TryGetValue(pos, out InteractableObject obj) || obj == null || obj.IsCollected) return false;
		return obj.DoorHp > 0f || obj.TrapHp > 0f; // 체력이 있는 차단물(문·목책·함정)만 파괴 대상이다 — 시체는 HP 개념이 없어 제외
	}

	// ─────────────────────────── 접근 제한 거리 ───────────────────────────

	// 11-1장: 이 위치에 섰을 때 "다른 근접 적"과의 점유 타일 중심 최소 직선거리가 설정값 이상인가. 선택한 공격 대상은 검사에서 제외하고, 이 유닛이 근접 역할이고 알려진 위치가 있는 다른 적만 본다(시야 밖 실제 위치는 쓰지 않는다).
	// 설정값 0은 이 추가 제한을 쓰지 않는다. 이 거리를 지켜도 실제 적 공격을 받지 않는다고 보장하지 않는다.
	public static bool ApproachLimitAllows(Human human, Vector2Int position, Unit attackTarget)
	{
		float limit = LeaderCommandSettings.ApproachLimit;
		if (limit <= 0f) return true;
		if (CombatScoreMath.ResolveCombatRole(human.unitType) == CombatRole.RangedDps || CombatScoreMath.ResolveCombatRole(human.unitType) == CombatRole.RangedSupport) return true; // 근접 역할에만 적용(원거리는 적 사거리와 무관하게 거리 유지가 따로 있다)

		var mine = TileCenters(position, human.FootprintSize);
		float min = float.PositiveInfinity;
		foreach (var e in human.personalSpottedEnemies)
		{
			if (e == null || e == attackTarget || e.hp <= 0f || e.currentFloor != human.currentFloor) continue;
			if (CombatScoreMath.ResolveCombatRole(e.unitType) == CombatRole.RangedDps || CombatScoreMath.ResolveCombatRole(e.unitType) == CombatRole.RangedSupport) continue; // 근접 적만
			min = Mathf.Min(min, LeaderMath.MinCenterDistance(mine, TileCenters(e.position, e.FootprintSize)));
		}
		return LeaderMath.PassesApproachLimit(min, limit);
	}

	private static List<Vector2> TileCenters(Vector2Int anchor, Vector2Int footprint)
	{
		var list = new List<Vector2>(footprint.x * footprint.y);
		for (int dx = 0; dx < footprint.x; dx++)
			for (int dy = 0; dy < footprint.y; dy++)
				list.Add(new Vector2(anchor.x + dx + 0.5f, anchor.y + dy + 0.5f));
		return list;
	}

	// CombatFSMState.ChaseTarget이 집중 공격 대상을 향해 걷기 직전에 묻는다 — 지금 가는 길(공격 위치 포함)의 모든 점유 위치가 접근 제한 거리를 지키면 true.
	// 어긴 칸이 있으면 이 걸음은 내딛지 않고(false) 막힘 시계를 돌린다 — 3초 이어지면 NoteApproachBlocked가 이 파티원의 지정만 끝낸다. 이동 알고리즘(A*)이 후보 경로를 걸러 주지는 못해, 다른 허용 경로를 찾는 대신 "현재 최단 경로가 허용되는가"로 근사한다(사거리·시야·점유 판정은 기존 이동이 그대로 맡는다).
	// 적용 대상은 집중 공격 지정 적을 쫓는 근접 역할뿐이다 — 설정값 0, 지정 없음, 다른 대상 추격, 원거리 역할, 전체 길을 아직 모르는 경우는 제한하지 않는다(길을 모르는 것을 수행 불가로 확정하지 않는다).
	public static bool AllowsChaseStep(Human human, Unit target) => AllowsChaseStep(human, target, Time.time);

	public static bool AllowsChaseStep(Human human, Unit target, float now)
	{
		if (human == null || target == null || !LeaderCommandSettings.Enabled || LeaderCommandSettings.ApproachLimit <= 0f) return true;
		var order = human.LeaderState?.EffectiveFocus;
		if (order == null || order.IsObject || order.Target != target) return true;
		var role = CombatScoreMath.ResolveCombatRole(human.unitType);
		if (role == CombatRole.RangedDps || role == CombatRole.RangedSupport) return true;

		if (!(human.MovementAlgorithm is AStarMovement astar) || !astar.TryGetPathTiles(human, target.position, out List<Vector2Int> path) || path.Count == 0)
		{
			NoteApproachBlocked(human, order, false, now); // 가는 길을 아직 모른다 — 불가능으로 확정하지 않는다
			return true;
		}
		foreach (var tile in path)
		{
			if (tile == target.position) continue; // 대상 자신의 자리는 거리 검사에서 제외한다(선택한 공격 대상은 제외)
			if (ApproachLimitAllows(human, tile, target)) continue;
			NoteApproachBlocked(human, order, true, now);
			return false;
		}
		NoteApproachBlocked(human, order, false, now);
		return true;
	}

	// 접근 제한 거리 때문에 허용 위치·경로를 못 찾는 상태가 이어지는지 기록한다 — 한동안 이어지면 모든 허용 방법이 불가능하다고 보고 이 파티원의 지정만 끝낸다(다른 파티원은 유지, 전체 길을 아직 모르는 것은 불가능으로 확정하지 않는다).
	public static void NoteApproachBlocked(Human human, FocusOrder order, bool blocked) => NoteApproachBlocked(human, order, blocked, Time.time);

	public static void NoteApproachBlocked(Human human, FocusOrder order, bool blocked, float now)
	{
		if (!blocked) { order.PathBlockedSince = -1f; return; }
		if (order.PathBlockedSince < 0f) order.PathBlockedSince = now;
		else if (now - order.PathBlockedSince >= PathBlockedLimitSeconds) EndFocus(human, order, "접근 제한 거리를 지키는 허용 위치·경로가 없음");
	}
}
