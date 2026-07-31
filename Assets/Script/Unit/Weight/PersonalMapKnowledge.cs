using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// 인류(Human) 유닛 개인이 들고 있는 "지도" — 연산공식 문서 15~21장(타일/오브젝트/방 위험도·흥미도)의
// 실체. 예전에는 이 데이터가 HumanKnowledgeBase(전역 싱글턴)에 있었지만, 지도관련_정리 문서가
// 명시하듯 "인류 유닛별로 획득"되는 개인 인지 정보이므로 유닛 개인 소유로 옮겼다 — Human.Memory.personalMap
// (Unit.cs)로만 보유하고, Monster/base Unit에는 두지 않는다("지도는 인류만" 요구사항).
//
// 이번 라운드는 개인 지도 데이터 자체의 완성이 스코프다. 진영(전체) 지도는 만들지 않는다 —
// HumanKnowledgeBase의 13장(전멸 위험도) 관련 전역 누적값은 그대로 두고 건드리지 않았다.
public class PersonalMapKnowledge
{
	// ─────────────────────────── 15장. 타일 위험도 ───────────────────────────
	private readonly Dictionary<Vector3Int, float> _tileDanger = new();
	private readonly Dictionary<Vector3Int, float> _tileSafetyElapsed = new();

	// ─────────────────────────── 16장. 타일 흥미도 확인 시간 (v0.7 (1) 개정판 신설) ───────────────────────────
	// "미탐사 기본 흥미도 5"도 15장 위험도와 똑같이 시야 확인 후 흥미도 단계별 확인 시간을 거쳐야
	// 0(탐사완료)이 된다 — _tileDanger/_tileSafetyElapsed와 완전히 대칭 구조.
	private readonly Dictionary<Vector3Int, float> _tileInterest = new();
	private readonly Dictionary<Vector3Int, float> _tileInterestConfirmElapsed = new();

	// 이 유닛이 확인 시간을 흘려보내야 할 타일 목록 — KnownDangerTiles와 동일한 용도(매 프레임 순회 대상).
	public IEnumerable<Vector3Int> KnownInterestTiles => _tileInterest.Keys;

	// 매 프레임 호출 — 이 타일에 지금 흥미도>0인 오브젝트가 있으면(=새 흥미 요소 발견) 타이머를
	// 리셋하고, 없으면 단계별 확인 시간이 지난 뒤 0(제거)으로 감소시킨다. threatPresent를 외부
	// (GameSession)에서 받아야 하는 TickTileSafety와 달리, "이 타일에 흥미도 있는 오브젝트가
	// 있는가"는 이미 이 클래스가 들고 있는 _objectTile/_objectInterest만으로 판단 가능해 자기완결적이다.
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
			// TickTileSafety와 동일한 이유로 0을 남기지 않고 기록 자체를 지운다(죽은 항목이
			// KnownInterestTiles/매 프레임 순회 대상에 계속 쌓이는 것을 방지).
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

		// 15장(v0.7 (1) 개정판): 이 타일 위 오브젝트의 위험도를 항상 더한다 — 몬스터 기록 위험도가
		// 있든 없든(기본값이든) 상관없이 얹는 별도 가산항이다(16장의 오브젝트 흥미도 가산과 동일 구조).
		string objId = GetObjectIdAtTile(pos);
		float objectDanger = (objId != null && _objectDanger.TryGetValue(objId, out var od)) ? od : 0f;
		return WeightMath.ComposeTileDanger(baseTileDanger, objectDanger);
	}

	public void SetTileDangerFromUnit(Vector3Int pos, float unitFinalDanger)
	{
		_tileDanger[pos] = unitFinalDanger;
		_tileSafetyElapsed[pos] = 0f; // 위험 요소 갱신되면 안전 확인 타이머 리셋
	}

	// 이 유닛이 위험도를 기록해 둔 타일 목록 — 매 프레임 TickTileSafety를 돌릴 대상을 정하기 위한
	// 읽기 전용 노출(UnitFunction.OnUpdate에서 순회).
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
			// 안전 확정 — 값을 0으로 남겨두지 않고 기록 자체를 지운다. 0으로만 남기면
			// GetTileDanger(pos, explored:true) 결과는 똑같지만(둘 다 0), 안전 확인이 끝난
			// 타일이 KnownDangerTiles/디버그 목록에 죽은 항목으로 계속 쌓이고 매 프레임
			// TickTileSafety를 도는 대상에서도 안 빠진다.
			_tileDanger.Remove(pos);
			_tileSafetyElapsed.Remove(pos);
		}
		else
		{
			_tileSafetyElapsed[pos] = elapsed;
		}
	}

	// ─────────────────────────── 지형 밝히기 (벽/바닥) ───────────────────────────
	// GameSession의 FactionData.discoveredMap(0=미탐색/1=바닥/2=벽, 진영 전체 공유)과 같은 정보를
	// 인류 개인 기준으로도 들고 있는다 — UnitFunction.CastRay가 시야가 지나가는 모든 타일마다
	// (몬스터 발견 여부와 무관하게) 호출해서 채운다. 값 표기는 discoveredMap과 동일한 관례를 쓴다:
	// 딕셔너리에 없으면 미탐색(0), 있으면 1(바닥) 또는 2(벽).
	private readonly Dictionary<Vector3Int, int> _tileTerrain = new();
	// 층별 텍스처 캐시 — RevealTile()이 호출될 때마다 매번 새 Texture2D를 만들면 인스펙터가
	// 매 프레임 다시 그리는 동안 텍스처가 계속 새로 할당돼 낭비/누수가 생긴다. GameSession.
	// UpdateFactionTextures()와 동일하게 층이 더러워졌을 때만 다시 그리고, 크기가 그대로면
	// 기존 Texture2D를 재사용한다.
	private readonly Dictionary<int, Texture2D> _terrainTextures = new();
	private readonly HashSet<int> _dirtyTerrainFloors = new();

	// 2026-07-31 — "가장 가까운 미탐사 타일 찾기"(NavigationFSMState.FindNearestUnexploredTarget)가
	// 매번 이미 탐색된 영역 전체를 BFS로 훑어야 해서, 탐사가 진행될수록(explored 면적이 커질수록)
	// 호출 1번의 비용이 계속 늘어나는 문제가 있었다(프로파일러 확인, 84회 호출에 134ms). 원래도
	// "인류 유닛별 개인 지도"를 이 목적(탐사 경계 추적)까지 염두에 두고 설계한 것이었다(사용자 확인).
	// _frontierTilesByFloor는 "아직 미탐사(_tileTerrain에 없음)지만 이미 밝혀진 바닥 타일과 인접한"
	// 타일 집합을 층별로 나눠 들고 있는다 — RevealTile이 매번 갱신하므로, 목표를 고를 때는 이 집합
	// (탐사 경계 길이에 비례 = 면적보다 훨씬 작음)에서만 최근접 탐색을 하면 된다.
	//
	// 최초 구현(단일 HashSet<Vector3Int>, 층 구분 없이 전부 순회 후 z로 필터)이 오히려 프레임을
	// 더 깎아먹는 역효과를 낸 걸 프로파일러로 확인(단 1회 호출인데 self 20.80ms) — 두 가지 원인:
	// 1) 층을 옮겨도 이전 층의 미해결 항목이 안 비워져서 조회할 때마다 그것까지 전부 순회했다 →
	//    층별로 분리해 해당 층 집합만 순회하도록 해결(아래 GetOrCreateFrontierSet).
	// 2) 대각선 인접 칸까지 후보로 넣었더니, 이 코드베이스 전반의 "코너 커팅 방지" 규칙 때문에
	//    실제로는 결코 직접 RevealTile이 안 불리는(영원히 안 풀리는) 대각선 좀비 후보가 계속
	//    쌓였다 → 아래에서 직교 4방향만 후보로 추가하도록 축소해 해결.
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

	// 반환값: 이 타일을 처음 밝히는 것이면 true — 3-2장 E_EXPLORED_SAFE_TILE(탐사완료+안전확인
	// 타일 → 흥미도 0)이 실제로 발생하는 순간이 정확히 이 "처음 밝혀지는 시점"이라, 호출부
	// (UnitFunction.CastRay)가 이 값으로 그 이벤트를 로그로 남긴다.
	public bool RevealTile(Vector3Int pos, bool isWall)
	{
		bool isFirstReveal = !_tileTerrain.ContainsKey(pos);
		_tileTerrain[pos] = isWall ? 2 : 1;
		_dirtyTerrainFloors.Add(pos.z);

		// 프론티어 갱신은 "처음 밝히는 타일"에서만 의미가 있다(이미 밝혀진 타일을 다시 보는 건 매
		// UpdateFOV 패스마다 일어나는데, 그때마다 다시 스캔하면 오히려 새 핫패스가 된다) — 이 타일
		// 자체는 이제 탐사됐으니 프론티어 후보에서 빠지고, 바닥이면 직교 인접 4칸 중 아직 미탐사인
		// 칸들이 새 프론티어 후보로 추가된다(대각선 제외 — 위 필드 주석 참고).
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

		// 15장: "미탐사 타일 기본 위험도 2"도 다른 기록 위험도와 동일하게 안전 확인 절차를 거쳐야
		// 한다 — 처음 시야에 들어오는 순간 바로 0(탐사완료+안전확인)이 되는 게 아니라, 위험도
		// 2는 단계0(0~99)이므로 1초 동안 새 위험 요소가 없어야 비로소 0으로 내려간다. 이미 몬스터
		// 관측 등으로 더 높은 값이 기록돼 있다면 그 값을 덮어쓰지 않는다. 벽은 유닛이 서 있을 수
		// 없는 타일이라 대상에서 제외(그 결과는 그대로 GetTileDanger의 explored 분기가 담당).
		if (isFirstReveal && !isWall && !_tileDanger.ContainsKey(pos))
		{
			_tileDanger[pos] = WeightMath.UnexploredTileBaseDanger;
			_tileSafetyElapsed[pos] = 0f;
		}

		// 16장(v0.7 (1) 개정판): "미탐사 기본 흥미도 5"도 위와 동일하게 처음 시야에 들어오는
		// 순간 바로 0이 되지 않고 확인 시간을 거친다(TickTileInterestConfirm이 감소시킴).
		if (isFirstReveal && !isWall && !_tileInterest.ContainsKey(pos))
		{
			_tileInterest[pos] = WeightMath.UnexploredTileBaseInterest;
			_tileInterestConfirmElapsed[pos] = 0f;
		}

		return isFirstReveal;
	}

	// RevealTile 전용 헬퍼 — (x,y,z)가 아직 미탐사(_tileTerrain에 없음)면 층별 프론티어 집합에 추가.
	private void AddFrontierCandidateIfUnexplored(HashSet<Vector2Int> frontier, int x, int y, int z)
	{
		if (!_tileTerrain.ContainsKey(new Vector3Int(x, y, z))) frontier.Add(new Vector2Int(x, y));
	}

	// 0=미탐색, 1=바닥, 2=벽 — discoveredMap과 동일한 값 관례.
	public int GetTileTerrain(Vector3Int pos) => _tileTerrain.GetValueOrDefault(pos, 0);

	public bool IsTileRevealed(Vector3Int pos) => _tileTerrain.ContainsKey(pos);

	// NavigationFSMState.RandomExplore 전용 — 지정한 층에서 from과 가장 가까운 프론티어(미탐사 경계)
	// 타일을 반환한다. 층별로 분리된 집합(_frontierTilesByFloor)에서 해당 층 것만 선형 탐색하므로,
	// 예전 BFS처럼 explored 영역 전체를 다시 훑거나 다른 층의 누적 항목까지 훑을 필요가 없다.
	// 직선거리 기준 최근접이라 실제 이동 경로상 최단은 아닐 수 있지만(방/벽에 막힌 프론티어를 고를
	// 가능성), 그 경우는 호출부의 기존 A* 실패 처리(대상을 벽으로 표시하고 재시도)가 그대로 흡수한다.
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

	// 지형이 밝혀졌으면 "탐사 여부"를 직접 넘겨줄 필요 없이 이 오버로드로 자동 판단할 수 있다
	// (explored 파라미터를 직접 넘기는 기존 오버로드는 호환을 위해 그대로 남겨둔다).
	public float GetTileDanger(Vector3Int pos) => GetTileDanger(pos, IsTileRevealed(pos));

	// 밝혀진 타일이 하나라도 있는 층 번호 목록 — 인스펙터가 층별로 텍스처를 그릴 때 순회용.
	public IEnumerable<int> KnownTerrainFloors => _tileTerrain.Keys.Select(k => k.z).Distinct().OrderBy(f => f);

	// GameSession.UpdateFactionTextures()와 같은 방식(흰색=바닥, 회색=벽, 검정=미탐색)으로
	// 이 유닛이 개인적으로 밝힌 지형만 그려서 반환한다 — 텍스트로 칸마다 나열하는 대신 인스펙터에서
	// 그림 한 장으로 보기 위함. 밝혀진 타일들의 경계 상자만큼만 그리므로(진영 지도처럼 맵 전체
	// 크기를 미리 알 필요 없음), 그 상자 안에서 아직 안 밝힌 칸은 검정으로 남는다.
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
	// 2026-07-08: InteractableObject(Assets/Script/Map/InteractableObject.cs, 팀원 구현)가 실제
	// 오브젝트 엔티티로 게임에 들어왔다 — objectId(=InteractableObject.Id)를 그대로 키로 쓴다.
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
			// Tags는 "Object/Passable/Corpse"류 계층형 문자열이라 정확 일치가 아니라 부분 일치로
			// 검사해야 한다(2026-07-20 수정 — 기존 정확 일치 Contains("WipeoutTrace")/Contains("Corpse")는
			// 항상 false를 반환하는 잠재 버그였다).
			if (tags.Any(t => t.Contains("WipeoutTrace")))
			{
				_objectInterest[objectId] = WeightMath.WipeoutTraceBaseInterest;
			}
			else if (tags.Any(t => t.Contains("Corpse")))
			{
				_objectInterest[objectId] = WeightMath.CorpseTraceInterest(causerStage);
			}
			// Loot 태그는 별도 이벤트 없음 (기본 오브젝트 처리)
		}
	}

	// 이 관찰자가 이 오브젝트를 이미 등록(발견)한 적 있는지 — 호출부(CastRay)가 "처음 발견"
	// 여부를 판정할 때 흥미도 수치로 추측하지 않고 이 메서드로 직접 확인해야 한다. 수치 기반 추측
	// (예: 현재 흥미도<=5)은 base흥미도가 원래 낮은 오브젝트나, 조사로 흥미도가 낮게 감쇠된
	// 오브젝트를 "아직 못 본 것"으로 오판해 RegisterObject를 다시 불러 감쇠된 값을 기본값으로
	// 되돌려버리는 버그가 있었다(2026-07-08 발견 및 수정).
	public bool IsObjectKnown(string objectId) => _objectTile.ContainsKey(objectId);

	// _objectTile은 objectId→tile로만 색인돼 있어(오브젝트 수가 적어 역방향 색인을 따로 안 둠),
	// 타일→objectId 조회는 호출 빈도가 낮은 쪽(확인 타이머 틱, 클릭 조회)에서 선형 탐색으로 처리한다.
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
		// 16장(v0.7 (1) 개정판): "기본 탐사 흥미도 + 오브젝트, 유닛 흥미도" — 유닛 흥미도 항.
		// ComposeTileInterest는 2항 합산 함수라 오브젝트+유닛을 미리 더해서 넘긴다(문서 예시
		// 5+120=125가 그대로 성립하도록 함수 시그니처/기존 테스트는 안 건드림).
		float unitInterest = GetUnitInterestAtTile(pos);
		return WeightMath.ComposeTileInterest(baseTileInterest, objectInterest + unitInterest);
	}

	// 16장 "유닛 흥미도" 항 — 이 관찰자가 그 타일에서 마지막으로 목격한 몬스터(들)의 흥미도
	// 스냅샷 합. _monsterSightings도 오브젝트(_objectTile)와 마찬가지로 타일→키 역방향 색인이
	// 없어 선형 탐색으로 처리한다(목격 몬스터 수가 적어 호출 빈도 낮은 조회 쪽에서 감당).
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

	// 회수/파괴된 오브젝트는 흥미도/위험도 둘 다 0으로 사라지고, 20/21장 방 확인목록에서도
	// 제거한다 — 안 지우면 이미 사라진 오브젝트의 옛 목격값이 방 위험도/흥미도에 계속 잡힌다.
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
	// "기록 여부"와 "예상 해제 성공률"은 지도 기록 전반과 동일하게 관찰자(유닛) 개인 소유다(v3 문서
	// "함정/방어건물 상태" 기록도 6장/24장 규칙을 따른다는 함정_관련_요약.txt 24절 확인 결과와 일치).
	// 9-3장의 "함정 정보 전파"는 이 코드베이스에 07_전파 문서가 아직 없어, UnitFunction.
	// BroadcastWitnessEvent와 동일한 근사(그 순간 생존한 파티원 전원에게 직접 기록)로 구현한다.
	private readonly HashSet<string> _trapRecorded = new();
	private readonly Dictionary<string, float> _trapExpectedSuccessRate = new();

	public bool IsTrapRecorded(string trapObjectId) => _trapRecorded.Contains(trapObjectId);

	public float GetTrapExpectedSuccessRate(string trapObjectId) => _trapExpectedSuccessRate.GetValueOrDefault(trapObjectId, 0f);

	// 9-2장: "최초 해제 시도 이후 오차 범위를 포함한 예상 해제 성공률을 기록하며, 이해도가 증가하면
	// 오차 범위가 감소하고 실제 해제 성공률이 증가한다" — 시도 결과(성공/실패)로 관측된 성공률을
	// 기록해 이후 판단(9-4장 50% 기준)에 쓴다. observedRate는 ExplorationMath.TrapDisarmSuccessRate로
	// 계산한 "이 유닛 기준 예상값"을 그대로 넘기면 된다(실제 굴림 결과와 무관하게, "시도해봤다"는
	// 사실 자체가 그 순간의 예상치를 기록값으로 확정시킨다는 해석 — 9-2장이 오차 범위의 정확한
	// 축소 공식을 안 줘서, "시도 = 기록 확정"으로 단순화했다).
	public void RecordTrapAttempt(string trapObjectId, float observedRate)
	{
		_trapRecorded.Add(trapObjectId);
		_trapExpectedSuccessRate[trapObjectId] = observedRate;
	}

	// ─────────────────────────── 신규. "지금 보고 있는 몬스터" 위치 기록 ───────────────────────────
	// 15장/18장 수치(위험도/흥미도)를 목격 시점 스냅샷으로 저장한다 — 실시간 조회가 아니라
	// "마지막으로 확인한 기록"이라는 지도의 성격(지도관련_정리 문서 3-2장, 6-5장 실제값-기록값 차이)에
	// 맞춘 것이다. UnitFunction.CastRay가 적을 발견할 때마다 호출한다(유일하게 자동 연동되는 부분).
	//
	// 24장(위치 및 상태 기록 충돌 처리)이 명시하는 "유닛 마지막 확인 위치"의 실체가 바로 이 기록이다
	// — RecordedInfoType으로 어떤 정보 유형으로 들어온 기록인지 남겨서, PriorityRank/ShouldReplace로
	// 우선순위가 더 낮은 정보가 더 높은 정보를 덮어쓰지 못하게 막는다.
	private class MonsterSighting
	{
		public Vector3Int Tile;
		public float DangerSnapshot;
		public float InterestSnapshot;
		public InfoType RecordedInfoType;
	}
	private readonly Dictionary<string, MonsterSighting> _monsterSightings = new();

	// monsterKey: target.name (인스턴스 식별자 — 위치는 종별이 아니라 그 개체 하나에 대한 정보이므로
	// HumanKnowledgeBase의 종/개체 누적 키(ResolveTargetKey)와는 별개로 항상 인스턴스명을 쓴다).
	// infoType 기본값 DirectWitness — 지금 유일한 호출부(UnitFunction.CastRay)가 시야로 직접
	// 목격하는 경로라 항상 이 값. 나중에 소리/전파(간접 파악) 등 다른 정보 유형이 이 기록을 갱신
	// 하려 들 때 아래 우선순위 게이트가 그대로 작동한다.
	public void ObserveMonster(string monsterKey, Vector3Int tile, float dangerSnapshot, float interestSnapshot, InfoType infoType = InfoType.DirectWitness)
	{
		// 24장 1~6번 규칙: 방금 들어온 정보(candidate)는 자기 정보 유형 안에서 항상 "최신"이고
		// (지금 이 순간 발생했으므로), 기존 기록(existing)도 저장될 당시엔 "최신"이었다 — 두 값을
		// 같은 기준(isLatest:true)으로 랭크를 매겨 비교하면 "직접 경험은 직접 목격보다 우선"(1번),
		// "직접 목격은 간접 파악보다 우선"(2번), "같은 유형끼리는 더 최근 것"(3번, candidateIsNewer
		// 는 항상 true — 지금 쓰는 값이 곧 가장 최근 값이므로)이 그대로 성립한다.
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
		};
		SetTileDangerFromUnit(tile, dangerSnapshot); // 15장: "적이 있거나 있었던 타일은 그 적의 기록 위험도를 가짐"
		// 시야에서 벗어나도 이 항목을 지우지 않는다 — 15장 "유닛 사라짐 → 기록 위험도 유지 →
		// 안전 확인 후 감소"를 그대로 따른다. 오래된 목격 정보를 치우는 것은 이번 스코프 밖.
	}

	public bool TryGetMonsterSighting(string monsterKey, out Vector3Int tile, out float danger, out float interest)
	{
		if (_monsterSightings.TryGetValue(monsterKey, out var s))
		{
			tile = s.Tile; danger = s.DangerSnapshot; interest = s.InterestSnapshot;
			return true;
		}
		tile = default; danger = 0f; interest = 0f;
		return false;
	}

	// ─────────────────────────── 20장/21장/7-1장. 방 위험도·흥미도 ───────────────────────────
	// "인류가 방에 들어가면 시야로 확인한 정보를 통해 방 위험도/흥미도 추정값이 실시간으로 반영된다"
	// (v3 문서 7-1장) — 원래도 개인 인지 개념이었다.
	// 2026-07-08: CastRay 자동 연동 완료(UnitFunction.CastRay) — 아래 클래스/메서드가 그 실체.
	public enum RoomExploreState { Unexplored, Exploring, Complete }

	private class RoomKnowledge
	{
		public RoomExploreState State = RoomExploreState.Unexplored;
		public bool IsBossRoom;
		// 유닛/오브젝트 키(인스턴스명)별 마지막 확인값으로 저장 — 단순 += 누적이 아니다. 시야 안에
		// 계속 있는 몬스터는 CastRay가 매 프레임 같은 키로 다시 부르므로(ObserveMonster와 동일
		// 패턴), 누적형으로 두면 프레임마다 값이 폭증한다. "그 방에서 확인된 대상들의 최신 스냅샷
		// 합"이 되도록 키로 덮어쓰기만 한다.
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

	// 20장: "인류 유닛은 방 전체 크기를 모른다"는 문서 지시는 플레이어에게 진행률(%)을 노출하지
	// 않는다는 뜻으로 해석했다 — 완료 판정 자체는 게임 내부적으로(맵 생성 데이터 기준) 계산해야
	// Exploring→Complete 전환이 가능하므로, totalFloorTilesInRoom(그 방의 실제 바닥 타일 총수,
	// CreateMap.GetRoomFloorTileCount 등 맵 쪽 ground-truth)을 호출부가 넘겨준다.
	// Unexplored였다면 Exploring으로 전환하고, 처음 밝히는 바닥 타일마다 호출해 누적 카운트가
	// 총 타일 수에 도달하면 Complete로 전환한다(한 번 Complete가 되면 되돌리지 않음).
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

	// 이 유닛이 개인적으로 아는 방들의 합산값이다 — 22장의 "던전 전체 위험도"와 달리 파티전멸/
	// 전멸흔적 같은 진영 데이터는 포함하지 않는다(그건 HumanKnowledgeBase 13장 소관, 이번에 안 건드림).
	public float GetPersonalDungeonDanger() => _rooms.Keys.Sum(id => GetRoomDanger(id, _rooms[id].IsBossRoom));
	public float GetPersonalDungeonInterest() => _rooms.Keys.Sum(id => GetRoomInterest(id, _rooms[id].IsBossRoom));

	// ─────────────────────────── 디버그 표시 (인스펙터/로그용) ───────────────────────────
	public string BuildDebugSummary()
	{
		var sb = new StringBuilder();

		// 지형 밝히기는 칸 수가 금방 수백 단위로 늘어나 텍스트로 나열하면 인스펙터가 못 봐줄
		// 정도가 되므로, 여기서는 개수/층만 요약하고 실제 모양은 GetTerrainTexture()로 그린
		// 그림(인스펙터의 텍스처 영역)에서 확인한다.
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
