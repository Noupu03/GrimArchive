using System.Collections.Generic;
using UnityEngine;

// 이동 모드 — 유닛 상태로 결정되고 한 번의 경로 탐색 동안 고정된다(TrapAvoidance.ResolveMode).
public enum TrapMoveMode
{
	Off,     // 함정을 무시한다(현행 동작): 인류가 아님·귀환 중(06 위기반응 문서 전까지)·설정 끔
	General, // 전투 아닌 자율 이동 — 알려진 활성 함정의 인접 1칸(3×3 구역) 진입 금지
	Combat,  // 전투 이동 — 안전한 우회를 먼저 보고, 알려진 피해 후 HP 50% 이상일 때만 함정 타일을 통과 후보로 삼는다
}

// 검증문서 03-13(03번 v0.12 9장·v0.6 9-6/9-10~9-13): "개인이 아는 활성 함정"을 이동 계층에 알리는 진입점. 이 파일 이전에는 이동 계층(AStarMovement/
// UnitFunction.CanMove)과 CombatFSMState가 함정을 전혀 몰라 함정 위치를 아는 유닛도 그대로 밟았다. 구역 = 함정 타일 + 체비셰프 1(ExplorationMath.
// TrapAvoidZoneRadius). 함정을 모르는 유닛에겐 구역이 없다(정보 격리) — 근거는 personalMap.KnownTrapTiles이고 "아직 존재하는가"만 실제 objectGrid로 확인한다.
public static class TrapAvoidance
{
	// 개인이 아는 활성 함정 하나.
	public struct KnownTrap
	{
		public string Id;
		public Vector2Int Tile;
		public bool Recorded;    // 이 유닛이 해제를 시도해 예상 성공률을 기록한 함정(=피해를 아는 함정으로 취급)
		public float DamageMax;  // 알려진 최대 예상 피해 — personalMap에 함정 피해 기록이 없어 실제 TrapDamageMax를 대용한다(InteractableObject 주석 참고)
		public bool Destroyable; // 함정 속성상 파괴 가능(TrapMaxHp > 0)하고 이 유닛에게 공격력이 있음
	}

	private const int EscapeSearchRadius = 5;
	// 구역 탈출을 못 하는 상태(사방이 막힘 등)에서 Tactical을 붙들고 얼어붙지 않게 잠시 탈출 시도를 끄는 시간(초).
	public const float EscapeSuppressSeconds = 5f;

	private static readonly List<KnownTrap> _scratchTraps = new List<KnownTrap>();
	private static readonly TrapMoveContext _sharedContext = new TrapMoveContext();

	public static bool IsEnabled(Unit unit)
	{
		if (!(unit is Human human) || human.hp <= 0f) return false;
		if (!(AIConfigLoader.Behavior?.trapAvoidanceEnabled ?? true)) return false;
		// 귀환·후퇴의 통과 조건은 06 위기반응 문서가 정한다 — 그 문서가 생기기 전까지는 현행(함정 무시) 동작을 유지한다.
		if (human.currentWait != null && human.currentWait.Reason == WaitReason.Retreating) return false;
		return true;
	}

	public static TrapMoveMode ResolveMode(Unit unit)
	{
		if (!IsEnabled(unit)) return TrapMoveMode.Off;
		return unit.CurrentFsmStateIfCreated is CombatFSMState ? TrapMoveMode.Combat : TrapMoveMode.General;
	}

	// 이 함정 대응을 하는 유닛(발견자·담당자)은 일반 모드에서 그 함정의 구역을 면제받는다 — 해제 위치가 인접 1칸이라 구역 안이기 때문이다.
	public static bool IsExemptResponder(Human human, string trapId)
		=> human.currentTrapInteraction != null && human.currentTrapInteraction.TrapObjectId == trapId;

	// 이 유닛이 아는 활성 함정을 into에 채운다(같은 층만). 세션이 있으면 실제로 아직 남아 있는 함정만 — 다른 유닛이 이미 해제·파괴한 함정에 계속 구역을
	// 두르지 않게 한다. 세션이 없으면(테스트) 아는 그대로 활성으로 본다.
	public static void CollectKnownTraps(Human human, List<KnownTrap> into)
	{
		into.Clear();
		var known = human.personalMap.KnownTrapTiles;
		if (known.Count == 0) return;

		var session = human.Session;
		foreach (var kv in known)
		{
			Vector3Int tile = kv.Value;
			if (tile.z != human.currentFloor) continue;

			var trap = new KnownTrap
			{
				Id = kv.Key,
				Tile = new Vector2Int(tile.x, tile.y),
				Recorded = human.personalMap.IsTrapRecorded(kv.Key),
			};
			if (session != null)
			{
				if (!session.objectGrid.TryGetValue(tile, out InteractableObject obj) || obj.Id != kv.Key || obj.IsCollected) continue;
				trap.DamageMax = obj.TrapDamageMax;
				trap.Destroyable = obj.TrapMaxHp > 0f && human.physicalAttack > 0f;
			}
			into.Add(trap);
		}
	}

	// 응답 상태 없이 알려진 활성 함정의 구역 안에 서 있는가(v0.6 9-6 "이미 범위 안에 있으면 빠져나온다"). FSM 상태와 무관한 기하 판정이다 —
	// 전투 중이면 어차피 Combat이 우선순위를 가져간다.
	public static bool NeedsZoneEscape(Unit unit)
	{
		if (!(unit is Human human) || !IsEnabled(unit)) return false;
		if (Time.time < human.trapZoneEscapeSuppressUntil) return false;
		CollectKnownTraps(human, _scratchTraps);
		foreach (var t in _scratchTraps)
			if (!IsExemptResponder(human, t.Id) && ExplorationMath.IsInTrapZone(unit.position, t.Tile)) return true;
		return false;
	}

	// 알려진 구역(응답자 면제 없음) 안의 타일인가 — 조사 후보처럼 "가려고 해도 갈 수 없는" 목표를 미리 거를 때 쓴다.
	public static bool IsInKnownZone(Unit unit, Vector2Int tile)
	{
		if (!(unit is Human human) || !IsEnabled(unit)) return false;
		CollectKnownTraps(human, _scratchTraps);
		foreach (var t in _scratchTraps)
			if (!IsExemptResponder(human, t.Id) && ExplorationMath.IsInTrapZone(tile, t.Tile)) return true;
		return false;
	}

	// A*를 타지 않는 무작위 배회 한 걸음이 일반 모드 회피 구역을 밟는가. 이미 구역 안에 있으면 막지 않는다(밖으로 나가는 걸음까지 막으면 안 된다).
	public static bool BlocksGeneralStep(Unit unit, Vector2Int nextPos)
	{
		var ctx = _sharedContext;
		ctx.Refresh(unit, null, null);
		return ctx.Active && ctx.BlocksTile(nextPos, nextPos);
	}

	// 구역 밖에서 가장 가까운 걸을 수 있는 비점유 타일을 BFS로 찾는다(함정 타일과 다른 구역 진입은 건너뛴다). 없으면 false.
	public static bool TryFindEscapeTile(Unit unit, out Vector2Int escape)
	{
		escape = default;
		if (!(unit is Human human)) return false;

		var traps = new List<KnownTrap>();
		CollectKnownTraps(human, traps);
		traps.RemoveAll(t => IsExemptResponder(human, t.Id));
		if (traps.Count == 0) return false;

		bool InAnyZone(Vector2Int p)
		{
			foreach (var t in traps) if (ExplorationMath.IsInTrapZone(p, t.Tile)) return true;
			return false;
		}
		bool OnAnyTrapTile(Vector2Int p)
		{
			foreach (var t in traps) if (t.Tile == p) return true;
			return false;
		}

		Vector2Int start = unit.position;
		var visited = new HashSet<Vector2Int> { start };
		var queue = new Queue<Vector2Int>();
		queue.Enqueue(start);

		while (queue.Count > 0)
		{
			Vector2Int cur = queue.Dequeue();
			for (int dx = -1; dx <= 1; dx++)
			for (int dy = -1; dy <= 1; dy++)
			{
				if (dx == 0 && dy == 0) continue;
				Vector2Int n = cur + new Vector2Int(dx, dy);
				if (!visited.Add(n)) continue;
				if (AIMovementHelper.ChebyshevDistance(start, n) > EscapeSearchRadius) continue;
				if (OnAnyTrapTile(n) || !unit.CanMove(n)) continue;
				if (!InAnyZone(n)) { escape = n; return true; }
				queue.Enqueue(n);
			}
		}
		return false;
	}
}

// 한 유닛의 한 번의 경로 탐색에 쓰는 함정 판정 상태 — AStarMovement가 인스턴스 하나를 들고 탐색 시작마다 Refresh한다(핫패스에서 할당하지 않는다).
// General: 함정 타일은 항상 막고(탐색 목표일 때만 도달 허용), 인접 1칸 구역은 막는다. 이미 구역 안에서 출발하면 그 구역은 막지 않고 큰 비용만 물려
// 가장 적게 밟고 나오게 한다. 자기 함정의 응답자는 그 함정의 구역(링)을 면제받는다. Combat: 링은 자유, 함정 타일은 통과 허용 조건을 만족하면 큰 비용,
// 아니면 막는다. Off: 아무것도 하지 않는다.
public sealed class TrapMoveContext
{
	public TrapMoveMode Mode { get; private set; } = TrapMoveMode.Off;
	public int Signature { get; private set; }

	private readonly List<TrapAvoidance.KnownTrap> _traps = new List<TrapAvoidance.KnownTrap>();
	private readonly HashSet<Vector2Int> _blocked = new HashSet<Vector2Int>();   // 항상 막는 타일(함정 타일)
	private readonly HashSet<Vector2Int> _hardRing = new HashSet<Vector2Int>();  // 일반 모드에서 막는 구역(링) 타일
	private readonly HashSet<Vector2Int> _softRing = new HashSet<Vector2Int>();  // 구역 안에서 출발할 때 비용만 물리는 링 타일
	private readonly HashSet<Vector2Int> _passCost = new HashSet<Vector2Int>();  // 전투 모드에서 통과가 허용된 함정 타일
	private Vector2Int? _exemptTile;

	public bool Active => Mode != TrapMoveMode.Off && (_blocked.Count > 0 || _hardRing.Count > 0 || _softRing.Count > 0 || _passCost.Count > 0);
	// 도달 실패를 "함정 때문인가"로 진단할 가치가 있는가(막는 타일이 있을 때만).
	public bool HasHardBlocks => Mode != TrapMoveMode.Off && (_blocked.Count > 0 || _hardRing.Count > 0);

	public void Refresh(Unit unit, TrapMoveMode? modeOverride, Vector2Int? exemptTrapTile)
	{
		_traps.Clear(); _blocked.Clear(); _hardRing.Clear(); _softRing.Clear(); _passCost.Clear();
		_exemptTile = exemptTrapTile;
		Mode = modeOverride ?? TrapAvoidance.ResolveMode(unit);
		Signature = (int)Mode;

		if (Mode == TrapMoveMode.Off || !(unit is Human human)) { Mode = TrapMoveMode.Off; Signature = 0; return; }

		TrapAvoidance.CollectKnownTraps(human, _traps);
		if (_traps.Count == 0) return;

		Vector2Int start = unit.position;
		float minRatio = AIConfigLoader.Behavior?.trapPassMinHpRatioAfterHit ?? ExplorationMath.TrapPassMinHpRatioAfterHit;

		unchecked
		{
			foreach (var t in _traps)
			{
				bool passable = false;
				if (Mode == TrapMoveMode.Combat)
				{
					passable = ExplorationMath.CanPassTrapInCombat(t.Recorded, unit.hp, unit.maxHp, t.DamageMax, minRatio);
					if (passable) _passCost.Add(t.Tile); else _blocked.Add(t.Tile);
				}
				else
				{
					_blocked.Add(t.Tile);
					bool exempt = (exemptTrapTile.HasValue && exemptTrapTile.Value == t.Tile) || TrapAvoidance.IsExemptResponder(human, t.Id);
					if (!exempt)
					{
						bool startInside = ExplorationMath.IsInTrapZone(start, t.Tile);
						var ring = startInside ? _softRing : _hardRing;
						for (int dx = -ExplorationMath.TrapAvoidZoneRadius; dx <= ExplorationMath.TrapAvoidZoneRadius; dx++)
						for (int dy = -ExplorationMath.TrapAvoidZoneRadius; dy <= ExplorationMath.TrapAvoidZoneRadius; dy++)
						{
							var p = new Vector2Int(t.Tile.x + dx, t.Tile.y + dy);
							if (p != t.Tile) ring.Add(p);
						}
						Signature = Signature * 31 + (startInside ? 2 : 1);
					}
					else Signature = Signature * 31 + 3;
				}
				Signature = Signature * 31 + t.Tile.x;
				Signature = Signature * 31 + t.Tile.y;
				Signature = Signature * 31 + (passable ? 1 : 0);
			}
			if (exemptTrapTile.HasValue) Signature = Signature * 31 + exemptTrapTile.Value.x * 7 + exemptTrapTile.Value.y;
		}
	}

	// 이 타일을 밟는 걸음이 막히는가. targetPos는 탐색 목표 — 명시적 면제(후보의 도착시간 조회처럼 함정 자체가 목표인 탐색)에서만 함정 타일 도달을 허용한다.
	public bool BlocksTile(Vector2Int tile, Vector2Int targetPos)
	{
		if (_blocked.Contains(tile))
			return !(_exemptTile.HasValue && _exemptTile.Value == tile && tile == targetPos);
		return _hardRing.Contains(tile);
	}

	public int ExtraCost(Vector2Int tile)
	{
		if (_passCost.Contains(tile)) return MovementMath.TrapPassExtraCost;
		if (_softRing.Contains(tile)) return MovementMath.TrapZoneEscapeExtraCost;
		return 0;
	}

	// 함정을 무시한 경로(offPath)가 이 컨텍스트에서 막히는 첫 타일 → 그 타일을 막는 함정. 도달 실패가 함정 때문인지 진단할 때 쓴다.
	public bool TryFindBlockingTrap(List<Vector2Int> offPath, out TrapAvoidance.KnownTrap blocking)
	{
		blocking = default;
		foreach (var tile in offPath)
		{
			if (!_blocked.Contains(tile) && !_hardRing.Contains(tile)) continue;
			foreach (var t in _traps)
			{
				bool hit = _blocked.Contains(tile) ? t.Tile == tile : ExplorationMath.IsInTrapZone(tile, t.Tile);
				if (hit) { blocking = t; return true; }
			}
		}
		return false;
	}
}
