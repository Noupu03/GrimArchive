using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// 인류(Human) 유닛 개인이 들고 있는 "지도" — 연산공식 문서 15~21장(타일/오브젝트/방 위험도·흥미도)의
// 실체. 개인 인지 정보라 Human.Memory.personalMap(Unit.cs)로만 보유하며, 진영 전체 지도나
// HumanKnowledgeBase 13장 전역 누적값은 다루지 않는다.
public class PersonalMapKnowledge
{
	// ─────────────────────────── 15장. 타일 위험도 ───────────────────────────
	private readonly Dictionary<Vector3Int, float> _tileDanger = new();
	private readonly Dictionary<Vector3Int, float> _tileSafetyElapsed = new();

	// ─────────────────────────── 16장. 타일 흥미도 확인 시간 (v0.7 (1) 개정판 신설) ───────────────────────────
	// 미탐사 기본 흥미도도 15장 위험도처럼 확인 시간을 거쳐야 0이 된다 — _tileDanger와 대칭 구조.
	private readonly Dictionary<Vector3Int, float> _tileInterest = new();
	private readonly Dictionary<Vector3Int, float> _tileInterestConfirmElapsed = new();

	// 이 유닛이 확인 시간을 흘려보내야 할 타일 목록 — KnownDangerTiles와 동일한 용도(매 프레임 순회 대상).
	public IEnumerable<Vector3Int> KnownInterestTiles => _tileInterest.Keys;

	// 매 프레임 호출 — 새 흥미 요소가 있으면 타이머를 리셋하고 없으면 단계별 확인 시간 후 제거한다.
	// TickTileSafety와 달리 threatPresent를 외부에서 받지 않고 자기완결적으로 판단한다.
	public void TickTileInterestConfirm(Vector3Int pos, float deltaTime)
	{
		if (!_tileInterest.TryGetValue(pos, out var interest) || interest <= 0f) return;

		string objId = GetObjectIdAtTile(pos);
		bool newInterestPresent = objId != null && _objectInterest.TryGetValue(objId, out var oi) && oi > 0f;
		if (newInterestPresent) { _tileInterestConfirmElapsed[pos] = 0f; return; }

		float elapsed = _tileInterestConfirmElapsed.GetValueOrDefault(pos) + deltaTime;

		var stage = WeightMath.GetInterestStage(Mathf.FloorToInt(interest));
		if (elapsed >= WeightMath.TileInterestCheckSeconds(stage))
		{
			// TickTileSafety와 동일한 이유로 0을 남기지 않고 기록 자체를 지운다(죽은 항목이 KnownInterestTiles
			// 순회 대상에 계속 쌓이는 것을 방지).
			_tileInterest.Remove(pos);
			_tileInterestConfirmElapsed.Remove(pos);
		}
		else
		{
			_tileInterestConfirmElapsed[pos] = elapsed;
		}
	}

	public float GetTileDanger(Vector3Int pos, bool explored)
	{
		float baseTileDanger = _tileDanger.TryGetValue(pos, out var v)
			? v
			: (explored ? WeightMath.ExploredSafeTileBaseDanger : WeightMath.UnexploredTileBaseDanger);

		// 15장(v0.7 (1) 개정판): 이 타일 위 오브젝트의 위험도를 항상 더한다 — 몬스터 기록 위험도
		// 유무와 무관한 별도 가산항(16장 오브젝트 흥미도 가산과 동일 구조).
		string objId = GetObjectIdAtTile(pos);
		float objectDanger = (objId != null && _objectDanger.TryGetValue(objId, out var od)) ? od : 0f;
		return WeightMath.ComposeTileDanger(baseTileDanger, objectDanger);
	}

	public void SetTileDangerFromUnit(Vector3Int pos, float unitFinalDanger)
	{
		_tileDanger[pos] = unitFinalDanger;
		_tileSafetyElapsed[pos] = 0f; // 위험 요소 갱신되면 안전 확인 타이머 리셋
	}

	// 이 유닛이 위험도를 기록해 둔 타일 목록 — 매 프레임 TickTileSafety를 돌릴 대상 읽기 전용 노출
	// (UnitFunction.OnUpdate에서 순회).
	public IEnumerable<Vector3Int> KnownDangerTiles => _tileDanger.Keys;

	// 매 프레임(또는 스캔 주기) 호출 — threatPresent가 false인 채로 단계별 안전확인시간이 지나면 0으로 감소.
	public void TickTileSafety(Vector3Int pos, bool threatPresent, float deltaTime)
	{
		if (!_tileDanger.TryGetValue(pos, out var danger) || danger <= 0f) return;
		if (threatPresent) { _tileSafetyElapsed[pos] = 0f; return; }

		float elapsed = _tileSafetyElapsed.GetValueOrDefault(pos) + deltaTime;

		var stage = WeightMath.GetDangerStage(Mathf.FloorToInt(danger));
		if (elapsed >= WeightMath.TileSafetyCheckSeconds(stage))
		{
			// 값을 0으로 남기지 않고 기록 자체를 지운다 — 안 지우면 죽은 항목이 KnownDangerTiles 등
			// 매 프레임 순회 대상에 계속 쌓인다.
			_tileDanger.Remove(pos);
			_tileSafetyElapsed.Remove(pos);
		}
		else
		{
			_tileSafetyElapsed[pos] = elapsed;
		}
	}

	// ─────────────────────────── 지형 밝히기 (벽/바닥) ───────────────────────────
	// GameSession의 FactionData.discoveredMap과 같은 정보를 인류 개인 기준으로 들고 있으며, UnitFunction.
	// CastRay가 시야가 지나가는 모든 타일마다 채운다(값 관례도 동일: 0=미탐색/1=바닥/2=벽).
	private readonly Dictionary<Vector3Int, int> _tileTerrain = new();
	// 층별 텍스처 캐시 — 매번 새 Texture2D를 만들면 낭비/누수가 생기므로, 층이 더러워졌을 때만 다시
	// 그리고 크기가 그대로면 재사용한다.
	private readonly Dictionary<int, Texture2D> _terrainTextures = new();
	private readonly HashSet<int> _dirtyTerrainFloors = new();

	// "가장 가까운 미탐사 타일 찾기"가 매번 전체 BFS를 돌지 않도록 _frontierTilesByFloor(미탐사지만
	// 밝혀진 바닥과 인접한 타일)만 층별로 유지해 그 안에서만 탐색한다. 대각선 인접은 제외 — 코너 커팅
	// 방지 규칙상 대각선 후보는 RevealTile이 결코 호출 안 되는 좀비로 남기 때문(직교 4방향만 사용).
	private readonly Dictionary<int, HashSet<Vector2Int>> _frontierTilesByFloor = new();

	private HashSet<Vector2Int> GetOrCreateFrontierSet(int floor)
	{
		if (!_frontierTilesByFloor.TryGetValue(floor, out var set))
		{
			set = new HashSet<Vector2Int>();
			_frontierTilesByFloor[floor] = set;
		}
		return set;
	}

	// 반환값: 이 타일을 처음 밝히는 것이면 true — 3-2장 E_EXPLORED_SAFE_TILE 이벤트가 발생하는 시점이
	// 정확히 이 "처음 밝혀지는 시점"이라, 호출부(UnitFunction.CastRay)가 이 값으로 로그를 남긴다.
	public bool RevealTile(Vector3Int pos, bool isWall)
	{
		bool isFirstReveal = !_tileTerrain.ContainsKey(pos);
		_tileTerrain[pos] = isWall ? 2 : 1;
		_dirtyTerrainFloors.Add(pos.z);

		// 프론티어 갱신은 "처음 밝히는 타일"에서만 한다(매번 재스캔하면 새 핫패스가 됨) — 이 타일은
		// 후보에서 빠지고, 바닥이면 직교 인접 4칸 중 미탐사 칸이 새 후보로 추가된다.
		if (isFirstReveal)
		{
			if (_frontierTilesByFloor.TryGetValue(pos.z, out var existingFrontier))
				existingFrontier.Remove(new Vector2Int(pos.x, pos.y));

			if (!isWall)
			{
				var frontier = GetOrCreateFrontierSet(pos.z);
				AddFrontierCandidateIfUnexplored(frontier, pos.x + 1, pos.y, pos.z);
				AddFrontierCandidateIfUnexplored(frontier, pos.x - 1, pos.y, pos.z);
				AddFrontierCandidateIfUnexplored(frontier, pos.x, pos.y + 1, pos.z);
				AddFrontierCandidateIfUnexplored(frontier, pos.x, pos.y - 1, pos.z);
			}
		}

		// 미탐사 타일 기본 위험도도 다른 기록 위험도와 동일하게 안전 확인 절차를 거쳐야 내려간다(이미
		// 더 높은 값이 기록돼 있으면 덮어쓰지 않음, 벽은 유닛이 설 수 없어 대상 제외).
		if (isFirstReveal && !isWall && !_tileDanger.ContainsKey(pos))
		{
			_tileDanger[pos] = WeightMath.UnexploredTileBaseDanger;
			_tileSafetyElapsed[pos] = 0f;
		}

		// 16장(v0.7 (1) 개정판): 미탐사 기본 흥미도도 처음 시야에 들어오는 순간 바로 0이 되지 않고
		// 확인 시간을 거친다(TickTileInterestConfirm이 감소시킴).
		if (isFirstReveal && !isWall && !_tileInterest.ContainsKey(pos))
		{
			_tileInterest[pos] = WeightMath.UnexploredTileBaseInterest;
			_tileInterestConfirmElapsed[pos] = 0f;
		}

		return isFirstReveal;
	}

	// pos가 벽(2)으로 기록돼 있으면 지워 미탐사(0) 상태로 되돌린다.
	public void ClearWallCache(Vector3Int pos)
	{
		if (_tileTerrain.TryGetValue(pos, out var terrain) && terrain == 2)
		{
			_tileTerrain.Remove(pos);
			_dirtyTerrainFloors.Add(pos.z);
		}
	}

	// RevealTile 전용 헬퍼 — (x,y,z)가 아직 미탐사(_tileTerrain에 없음)면 층별 프론티어 집합에 추가.
	private void AddFrontierCandidateIfUnexplored(HashSet<Vector2Int> frontier, int x, int y, int z)
	{
		if (!_tileTerrain.ContainsKey(new Vector3Int(x, y, z))) frontier.Add(new Vector2Int(x, y));
	}

	// 0=미탐색, 1=바닥, 2=벽 — discoveredMap과 동일한 값 관례.
	public int GetTileTerrain(Vector3Int pos) => _tileTerrain.GetValueOrDefault(pos, 0);

	public bool IsTileRevealed(Vector3Int pos) => _tileTerrain.ContainsKey(pos);

	// NavigationFSMState.RandomExplore 전용 — from과 가장 가까운 프론티어 타일을 직선거리 기준으로
	// 반환한다(실제 최단 경로는 아닐 수 있으나 호출부의 A* 실패 재시도가 흡수).
	public bool TryGetNearestFrontierTile(int floor, Vector2Int from, out Vector2Int nearest)
	{
		nearest = default;
		if (!_frontierTilesByFloor.TryGetValue(floor, out var frontier) || frontier.Count == 0)
			return false;

		float bestDistSq = float.MaxValue;
		bool found = false;
		foreach (var t in frontier)
		{
			float dx = t.x - from.x, dy = t.y - from.y;
			float distSq = dx * dx + dy * dy;
			if (distSq < bestDistSq)
			{
				bestDistSq = distSq;
				nearest = t;
				found = true;
			}
		}
		return found;
	}

	// 지형이 밝혀졌으면 explored를 직접 넘길 필요 없이 이 오버로드로 자동 판단한다(기존 오버로드는
	// 호환 위해 유지).
	public float GetTileDanger(Vector3Int pos) => GetTileDanger(pos, IsTileRevealed(pos));

	// 밝혀진 타일이 하나라도 있는 층 번호 목록 — 인스펙터가 층별로 텍스처를 그릴 때 순회용.
	public IEnumerable<int> KnownTerrainFloors => _tileTerrain.Keys.Select(k => k.z).Distinct().OrderBy(f => f);

	// GameSession.UpdateFactionTextures()와 같은 방식(흰색=바닥/회색=벽/검정=미탐색)으로 이 유닛이
	// 개인적으로 밝힌 지형만 그리며, 밝혀진 타일 경계 상자 안의 미탐사 칸은 검정으로 남는다.
	public Texture2D GetTerrainTexture(int floor)
	{
		var tilesOnFloor = _tileTerrain.Where(kv => kv.Key.z == floor).ToList();
		if (tilesOnFloor.Count == 0) return null;

		if (!_dirtyTerrainFloors.Contains(floor) && _terrainTextures.TryGetValue(floor, out var cached))
			return cached;

		int minX = tilesOnFloor.Min(kv => kv.Key.x);
		int maxX = tilesOnFloor.Max(kv => kv.Key.x);
		int minY = tilesOnFloor.Min(kv => kv.Key.y);
		int maxY = tilesOnFloor.Max(kv => kv.Key.y);
		int w = maxX - minX + 1;
		int h = maxY - minY + 1;

		if (!_terrainTextures.TryGetValue(floor, out var tex) || tex == null || tex.width != w || tex.height != h)
		{
			tex = new Texture2D(w, h) { filterMode = FilterMode.Point };
			_terrainTextures[floor] = tex;
		}

		var pixels = new Color[w * h];
		for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.black; // 경계 상자 안이지만 아직 못 밝힌 칸
		foreach (var kv in tilesOnFloor)
		{
			int px = kv.Key.x - minX;
			int py = kv.Key.y - minY;
			pixels[py * w + px] = kv.Value == 2 ? Color.gray : Color.white;
		}
		tex.SetPixels(pixels);
		tex.Apply();

		_dirtyTerrainFloors.Remove(floor);
		return tex;
	}

	// ─────────────────────────── 15장/16장/17장. 오브젝트 위치·위험도·흥미도 ───────────────────────────
	// InteractableObject(Assets/Script/Map/InteractableObject.cs)의 objectId(=Id)를 그대로 키로 쓴다.
	private readonly Dictionary<string, float> _objectBaseInterest = new();
	private readonly Dictionary<string, float> _objectInterest = new();
	private readonly Dictionary<string, float> _objectBaseDanger = new();
	private readonly Dictionary<string, float> _objectDanger = new();
	private readonly Dictionary<string, Vector3Int> _objectTile = new();

	public void RegisterObject(string objectId, Vector3Int tile, float baseDanger, float baseInterest, System.Collections.Generic.List<string> tags = null, DangerStage causerStage = DangerStage.Stage0)
	{
		_objectBaseDanger[objectId] = baseDanger;
		_objectDanger[objectId] = baseDanger;
		_objectBaseInterest[objectId] = baseInterest;
		_objectInterest[objectId] = baseInterest;
		_objectTile[objectId] = tile;

		if (tags != null)
		{
			// Tags는 "Object/Passable/Corpse"류 계층형 문자열이라 정확 일치가 아니라 부분 일치로 검사한다.
			if (tags.Any(t => t.Contains("WipeoutTrace")))
			{
				// 19장도 13-2장과 동일하게 원인 대상 위험도 단계 보정을 받는다.
				_objectInterest[objectId] = WeightMath.WipeoutTraceInterest(causerStage);
			}
			else if (tags.Any(t => t.Contains("Corpse")))
			{
				_objectInterest[objectId] = WeightMath.CorpseTraceInterest(causerStage);
			}
			// Loot 태그는 별도 이벤트 없음 (기본 오브젝트 처리)
		}
	}

	// 이 관찰자가 이 오브젝트를 이미 등록(발견)했는지 — 흥미도 수치로 추측하면 감쇠된 오브젝트를
	// 오판해 RegisterObject가 값을 기본값으로 되돌리는 버그가 생긴다.
	public bool IsObjectKnown(string objectId) => _objectTile.ContainsKey(objectId);

	// _objectTile은 objectId→tile 단방향 색인이라(오브젝트 수가 적어 역색인 없음), 타일→objectId
	// 조회는 호출 빈도가 낮은 확인 타이머/클릭 조회에서 선형 탐색으로 처리한다.
	public string GetObjectIdAtTile(Vector3Int pos)
		=> _objectTile.FirstOrDefault(kv => kv.Value == pos).Key;

	public float GetTileInterest(Vector3Int pos, bool explored, string objectIdAtTile)
	{
		// 16장(v0.7 (1) 개정판): 확인 시간이 아직 안 지나 _tileInterest에 기록이 남아있으면 그 값을
		// 우선 쓴다 — 방금 밝혀진 타일은 즉시 0이 아니라 여전히 기본 미탐사 흥미도(5)를 유지한다.
		float baseTileInterest = _tileInterest.TryGetValue(pos, out var recorded)
			? recorded
			: (explored ? WeightMath.ExploredTileBaseInterest : WeightMath.UnexploredTileBaseInterest);
		float objectInterest = (objectIdAtTile != null && _objectInterest.TryGetValue(objectIdAtTile, out var oi)) ? oi : 0f;
		// ComposeTileInterest는 2항 합산 함수라 오브젝트+유닛 흥미도를 미리 더해서 넘긴다.
		float unitInterest = GetUnitInterestAtTile(pos);
		return WeightMath.ComposeTileInterest(baseTileInterest, objectInterest + unitInterest);
	}

	// 16장 "유닛 흥미도" 항 — 이 관찰자가 그 타일에서 마지막으로 목격한 몬스터(들)의 흥미도 스냅샷 합
	// (_objectTile과 마찬가지로 역방향 색인 없이 선형 탐색).
	private float GetUnitInterestAtTile(Vector3Int pos)
	{
		float sum = 0f;
		foreach (var sighting in _monsterSightings.Values)
			if (sighting.Tile == pos) sum += sighting.InterestSnapshot;
		return sum;
	}

	// GetTileDanger(pos)와 동일한 이유의 편의 오버로드 — 지형 밝히기 기록으로 탐사 여부를 자동 판단.
	public float GetTileInterest(Vector3Int pos, string objectIdAtTile) => GetTileInterest(pos, IsTileRevealed(pos), objectIdAtTile);

	public void OnObjectInvestigated(string objectId)
	{
		if (_objectInterest.TryGetValue(objectId, out var v))
			_objectInterest[objectId] = WeightMath.ObjectInterestAfterInvestigate(v);
	}

	// 회수/파괴된 오브젝트는 흥미도/위험도가 0이 되고 20/21장 방 확인목록에서도 제거한다 — 안 지우면
	// 사라진 오브젝트의 옛 목격값이 방 위험도/흥미도에 계속 잡힌다.
	public void OnObjectCollected(string objectId)
	{
		_objectInterest[objectId] = 0f;
		_objectDanger[objectId] = 0f;
		ClearObjectFromRooms(objectId);
	}

	public void OnObjectDestroyed(string objectId)
	{
		_objectInterest[objectId] = 0f;
		_objectDanger[objectId] = 0f;
		ClearObjectFromRooms(objectId);
	}

	private void ClearObjectFromRooms(string objectId)
	{
		foreach (var room in _rooms.Values) room.ConfirmedObjects.Remove(objectId);
	}

	public void OnObjectDroppedByCarrierDeath(string objectId)
	{
		if (_objectBaseInterest.TryGetValue(objectId, out var baseInterest))
			_objectInterest[objectId] = WeightMath.ObjectInterestAfterDrop(baseInterest);
	}

	// ─────────────────────────── 03문서 9-2/9-3장. 함정 개인 지식(기록 여부/예상 성공률) ───────────────────────────
	// 기록 여부/예상 해제 성공률은 지도 기록 전반과 동일하게 관찰자 개인 소유다. "함정 정보 전파"
	// (9-3장)는 07_전파 문서가 없어 UnitFunction.BroadcastWitnessEvent와 동일한 근사로 구현한다.
	private readonly HashSet<string> _trapRecorded = new();
	private readonly Dictionary<string, float> _trapExpectedSuccessRate = new();

	public bool IsTrapRecorded(string trapObjectId) => _trapRecorded.Contains(trapObjectId);

	public float GetTrapExpectedSuccessRate(string trapObjectId) => _trapExpectedSuccessRate.GetValueOrDefault(trapObjectId, 0f);

	// 9-2장: 시도 결과로 관측된 예상 해제 성공률을 기록해 이후 판단(9-4장 50% 기준)에 쓴다 — 오차
	// 범위 축소 공식이 없어 "시도 = 기록 확정"으로 단순화했다.
	public void RecordTrapAttempt(string trapObjectId, float observedRate)
	{
		_trapRecorded.Add(trapObjectId);
		_trapExpectedSuccessRate[trapObjectId] = observedRate;
	}

	// ─────────────────────────── 신규. "지금 보고 있는 몬스터" 위치 기록 ───────────────────────────
	// 15장/18장 수치를 목격 시점 스냅샷으로 저장한다(실시간 조회 아닌 "마지막 확인 기록") — UnitFunction.
	// CastRay가 적 발견 시마다 호출하며, 24장의 "유닛 마지막 확인 위치"가 이 기록이다. RecordedInfoType으로
	// 정보 유형을 남겨 PriorityRank/ShouldReplace가 낮은 우선순위 정보로 안 덮이게 막는다.
	private class MonsterSighting
	{
		public Vector3Int Tile;
		public float DangerSnapshot;
		public float InterestSnapshot;
		public InfoType RecordedInfoType;
		// 전파 정보(PropagatedInfoRecord.LastKnownTimestamp)와 어느 쪽이 최신인지 비교할 때 쓴다
		// (PropagationSystem.GetLatestKnownPosition).
		public float Timestamp;
	}
	private readonly Dictionary<string, MonsterSighting> _monsterSightings = new();

	// monsterKey: target.name(인스턴스 식별자 — 위치는 개체별 정보라 HumanKnowledgeBase의 종/개체 누적
	// 키와 별개로 항상 인스턴스명 사용). infoType 기본값 DirectWitness는 유일한 호출부(CastRay)가 시야
	// 직접 목격이기 때문 — 다른 정보 유형이 갱신하려 들 때 아래 우선순위 게이트가 작동한다.
	public void ObserveMonster(string monsterKey, Vector3Int tile, float dangerSnapshot, float interestSnapshot, InfoType infoType = InfoType.DirectWitness)
	{
		// 24장 1~6번 규칙: 같은 기준(isLatest:true)으로 랭크를 매겨 비교하면 "직접 경험 > 직접 목격 >
		// 간접 파악", "같은 유형끼리는 더 최근 것"이 그대로 성립한다.
		if (_monsterSightings.TryGetValue(monsterKey, out var existing))
		{
			int candidateRank = WeightMath.PriorityRank(infoType, isLatest: true);
			int existingRank  = WeightMath.PriorityRank(existing.RecordedInfoType, isLatest: true);
			if (!WeightMath.ShouldReplace(candidateRank, existingRank, candidateIsNewer: true)) return;
		}

		_monsterSightings[monsterKey] = new MonsterSighting
		{
			Tile = tile,
			DangerSnapshot = dangerSnapshot,
			InterestSnapshot = interestSnapshot,
			RecordedInfoType = infoType,
			Timestamp = Time.time,
		};
		SetTileDangerFromUnit(tile, dangerSnapshot); // 15장: "적이 있거나 있었던 타일은 그 적의 기록 위험도를 가짐"
		// 시야에서 벗어나도 이 항목을 지우지 않는다 — 15장 "유닛 사라짐 → 기록 위험도 유지 →
		// 안전 확인 후 감소"를 그대로 따른다. 오래된 목격 정보를 치우는 것은 이번 스코프 밖.
	}

	public bool TryGetMonsterSighting(string monsterKey, out Vector3Int tile, out float danger, out float interest)
		=> TryGetMonsterSighting(monsterKey, out tile, out danger, out interest, out _);

	public bool TryGetMonsterSighting(string monsterKey, out Vector3Int tile, out float danger, out float interest, out float timestamp)
	{
		if (_monsterSightings.TryGetValue(monsterKey, out var s))
		{
			tile = s.Tile; danger = s.DangerSnapshot; interest = s.InterestSnapshot; timestamp = s.Timestamp;
			return true;
		}
		tile = default; danger = 0f; interest = 0f; timestamp = 0f;
		return false;
	}

	// ─────────────────────────── 20장/21장/7-1장. 방 위험도·흥미도 ───────────────────────────
	// "인류가 방에 들어가면 시야로 확인한 정보로 방 위험도/흥미도 추정값이 실시간 반영된다"(v3 문서
	// 7-1장) — UnitFunction.CastRay가 자동 연동하며, 아래 클래스/메서드가 그 실체다.
	public enum RoomExploreState { Unexplored, Exploring, Complete }

	private class RoomKnowledge
	{
		public RoomExploreState State = RoomExploreState.Unexplored;
		public bool IsBossRoom;
		// 유닛/오브젝트 키별 마지막 확인값으로 저장 — 누적(+=)이 아니라 키로 덮어쓰기만 한다(CastRay가
		// 매 프레임 같은 키로 다시 부르므로 누적형이면 값이 폭증한다).
		public readonly Dictionary<string, (float danger, float interest)> ConfirmedUnits = new();
		public readonly Dictionary<string, (float danger, float interest)> ConfirmedObjects = new();
		// 이 방에서 이 유닛(관찰자)이 지금까지 처음 밝힌 바닥 타일 수 — Exploring→Complete 판정용.
		public int RevealedFloorTiles;
	}
	private readonly Dictionary<int, RoomKnowledge> _rooms = new();

	private RoomKnowledge GetOrCreateRoom(int roomId, bool isBossRoom)
	{
		if (!_rooms.TryGetValue(roomId, out var r))
		{
			r = new RoomKnowledge { IsBossRoom = isBossRoom };
			_rooms[roomId] = r;
		}
		return r;
	}

	public void SetRoomExploreState(int roomId, bool isBossRoom, RoomExploreState state)
		=> GetOrCreateRoom(roomId, isBossRoom).State = state;

	// unitKey: 대상 유닛의 인스턴스 식별자(Unit.name) — 같은 유닛을 매 프레임 다시 불러도 최신값으로
	// 덮어쓸 뿐 중복 가산되지 않는다.
	public void ObserveUnitInRoom(int roomId, bool isBossRoom, string unitKey, float unitFinalDanger, float unitInterest)
		=> GetOrCreateRoom(roomId, isBossRoom).ConfirmedUnits[unitKey] = (unitFinalDanger, unitInterest);

	public void ObserveObjectInRoom(int roomId, bool isBossRoom, string objectKey, float objectDanger, float objectInterest)
		=> GetOrCreateRoom(roomId, isBossRoom).ConfirmedObjects[objectKey] = (objectDanger, objectInterest);

	// 20장 "인류는 방 전체 크기를 모른다"는 플레이어에게 진행률(%)을 노출하지 않는다는 뜻으로 해석 —
	// 완료 판정 자체는 내부적으로 totalFloorTilesInRoom(ground-truth)을 받아 누적, 도달 시 Complete로
	// 전환한다(비가역).
	public void ObserveRoomTileRevealed(int roomId, bool isBossRoom, int totalFloorTilesInRoom)
	{
		var r = GetOrCreateRoom(roomId, isBossRoom);
		if (r.State == RoomExploreState.Complete) return;

		if (r.State == RoomExploreState.Unexplored)
			r.State = RoomExploreState.Exploring;

		r.RevealedFloorTiles++;
		if (totalFloorTilesInRoom > 0 && r.RevealedFloorTiles >= totalFloorTilesInRoom)
			r.State = RoomExploreState.Complete;
	}

	public float GetRoomDanger(int roomId, bool isBossRoom)
	{
		var r = GetOrCreateRoom(roomId, isBossRoom);
		float confirmed = r.ConfirmedUnits.Values.Sum(v => v.danger) + r.ConfirmedObjects.Values.Sum(v => v.danger);
		return r.State switch
		{
			RoomExploreState.Unexplored => isBossRoom ? WeightMath.UnexploredBossRoomBaseDanger : WeightMath.UnexploredNormalRoomBaseDanger,
			RoomExploreState.Exploring => confirmed + (isBossRoom ? WeightMath.ExploringBossRoomFixedDanger : WeightMath.ExploringNormalRoomFixedDanger),
			RoomExploreState.Complete => confirmed,
			_ => 0f,
		};
	}

	public float GetRoomInterest(int roomId, bool isBossRoom)
	{
		var r = GetOrCreateRoom(roomId, isBossRoom);
		float confirmed = r.ConfirmedUnits.Values.Sum(v => v.interest) + r.ConfirmedObjects.Values.Sum(v => v.interest);
		return r.State switch
		{
			RoomExploreState.Unexplored => isBossRoom ? WeightMath.UnexploredBossRoomBaseInterest : WeightMath.UnexploredNormalRoomBaseInterest,
			RoomExploreState.Exploring => confirmed + (isBossRoom ? WeightMath.ExploringBossRoomFixedInterest : WeightMath.ExploringNormalRoomFixedInterest),
			RoomExploreState.Complete => confirmed,
			_ => 0f,
		};
	}

	// 방 탐사 상태를 조회 전용으로 노출 — 디버그 표시/외부 판단 로직에서 상태만 읽고 싶을 때 사용.
	public RoomExploreState GetRoomExploreState(int roomId) => _rooms.TryGetValue(roomId, out var r) ? r.State : RoomExploreState.Unexplored;

	// 이 유닛이 개인적으로 아는 방들의 합산값이다 — 22장 "던전 전체 위험도"와 달리 파티전멸/전멸흔적
	// 같은 진영 데이터는 포함하지 않는다(HumanKnowledgeBase 13장 소관).
	public float GetPersonalDungeonDanger() => _rooms.Keys.Sum(id => GetRoomDanger(id, _rooms[id].IsBossRoom));
	public float GetPersonalDungeonInterest() => _rooms.Keys.Sum(id => GetRoomInterest(id, _rooms[id].IsBossRoom));

	// ─────────────────────────── 디버그 표시 (인스펙터/로그용) ───────────────────────────
	public string BuildDebugSummary()
	{
		var sb = new StringBuilder();

		// 지형 밝히기는 칸 수가 금방 늘어나 텍스트 나열이 어려우므로 개수/층만 요약하고, 실제 모양은
		// GetTerrainTexture()로 그린 그림에서 확인한다.
		sb.AppendLine($"[지형 밝히기] {_tileTerrain.Count}칸 밝혀짐 (층: {string.Join(", ", KnownTerrainFloors)}) — 아래 텍스처 참고");

		sb.AppendLine($"[타일 위험도] {_tileDanger.Count}개");
		foreach (var kv in _tileDanger)
			sb.AppendLine($"  {kv.Key}: danger={kv.Value:0.##} (안전확인 경과 {_tileSafetyElapsed.GetValueOrDefault(kv.Key):0.#}s)");

		// 16장(v0.7 (1) 개정판): 확인 시간 중이라 아직 0으로 안 내려간 타일은 위 타일 위험도와
		// 동일하게 딕셔너리에 남아있다 — 그 목록을 그대로 보여준다.
		sb.AppendLine($"[타일 흥미도 확인중] {_tileInterest.Count}개 (기본값: 미탐사={WeightMath.UnexploredTileBaseInterest:0.##} / 탐사완료={WeightMath.ExploredTileBaseInterest:0.##})");
		foreach (var kv in _tileInterest)
			sb.AppendLine($"  {kv.Key}: interest={kv.Value:0.##} (확인 경과 {_tileInterestConfirmElapsed.GetValueOrDefault(kv.Key):0.#}s)");
		if (_objectTile.Count > 0)
		{
			foreach (var kv in _objectTile)
				sb.AppendLine($"  {kv.Value} (오브젝트 {kv.Key} 있음): 타일흥미도={GetTileInterest(kv.Value, kv.Key):0.##}");
		}

		sb.AppendLine($"[오브젝트] {_objectTile.Count}개");
		foreach (var kv in _objectTile)
		{
			float baseI = _objectBaseInterest.GetValueOrDefault(kv.Key);
			float curI = _objectInterest.GetValueOrDefault(kv.Key);
			float baseD = _objectBaseDanger.GetValueOrDefault(kv.Key);
			float curD = _objectDanger.GetValueOrDefault(kv.Key);
			sb.AppendLine($"  {kv.Key}: tile={kv.Value} interest={curI:0.##}(기본 {baseI:0.##}) danger={curD:0.##}(기본 {baseD:0.##})");
		}

		sb.AppendLine($"[몬스터 목격] {_monsterSightings.Count}개");
		foreach (var kv in _monsterSightings)
			sb.AppendLine($"  {kv.Key}: tile={kv.Value.Tile} danger={kv.Value.DangerSnapshot:0.##} interest={kv.Value.InterestSnapshot:0.##}");

		sb.AppendLine($"[방] {_rooms.Count}개 (개인 던전 위험도 {GetPersonalDungeonDanger():0.##} / 흥미도 {GetPersonalDungeonInterest():0.##})");
		foreach (var kv in _rooms)
			sb.AppendLine($"  room#{kv.Key} ({(kv.Value.IsBossRoom ? "보스방" : "일반")}, {kv.Value.State}, 바닥타일확인 {kv.Value.RevealedFloorTiles}칸, 확인유닛 {kv.Value.ConfirmedUnits.Count}/오브젝트 {kv.Value.ConfirmedObjects.Count}): danger={GetRoomDanger(kv.Key, kv.Value.IsBossRoom):0.##} interest={GetRoomInterest(kv.Key, kv.Value.IsBossRoom):0.##}");

		return sb.ToString();
	}
}
