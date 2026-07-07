using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// 인류(Human) 유닛 개인이 들고 있는 "지도" — 연산공식 문서 15~21장(타일/오브젝트/방 위험도·흥미도)의
// 실체. 예전에는 이 데이터가 HumanKnowledgeBase(전역 싱글턴)에 있었지만, 지도관련_정리 문서가
// 명시하듯 "인류 유닛별로 획득"되는 개인 인지 정보이므로 유닛 개인 소유로 옮겼다 — Human.personalMap
// (Unit.cs)로만 보유하고, Monster/base Unit에는 두지 않는다("지도는 인류만" 요구사항).
//
// 이번 라운드는 개인 지도 데이터 자체의 완성이 스코프다. 진영(전체) 지도는 만들지 않는다 —
// HumanKnowledgeBase의 13장(전멸 위험도) 관련 전역 누적값은 그대로 두고 건드리지 않았다.
public class PersonalMapKnowledge
{
	// ─────────────────────────── 15장. 타일 위험도 ───────────────────────────
	private readonly Dictionary<Vector3Int, float> _tileDanger = new();
	private readonly Dictionary<Vector3Int, float> _tileSafetyElapsed = new();

	public float GetTileDanger(Vector3Int pos, bool explored)
	{
		if (_tileDanger.TryGetValue(pos, out var v)) return v;
		return explored ? WeightMath.ExploredSafeTileBaseDanger : WeightMath.UnexploredTileBaseDanger;
	}

	public void SetTileDangerFromUnit(Vector3Int pos, float unitFinalDanger)
	{
		_tileDanger[pos] = unitFinalDanger;
		_tileSafetyElapsed[pos] = 0f; // 위험 요소 갱신되면 안전 확인 타이머 리셋
	}

	// 매 프레임(또는 스캔 주기) 호출 — threatPresent가 false인 채로 단계별 안전확인시간이 지나면 0으로 감소.
	public void TickTileSafety(Vector3Int pos, bool threatPresent, float deltaTime)
	{
		if (!_tileDanger.TryGetValue(pos, out var danger) || danger <= 0f) return;
		if (threatPresent) { _tileSafetyElapsed[pos] = 0f; return; }

		float elapsed = _tileSafetyElapsed.GetValueOrDefault(pos) + deltaTime;
		_tileSafetyElapsed[pos] = elapsed;

		var stage = WeightMath.GetDangerStage(Mathf.FloorToInt(danger));
		if (elapsed >= WeightMath.TileSafetyCheckSeconds(stage))
			_tileDanger[pos] = 0f;
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

	public void RevealTile(Vector3Int pos, bool isWall)
	{
		_tileTerrain[pos] = isWall ? 2 : 1;
		_dirtyTerrainFloors.Add(pos.z);
	}

	// 0=미탐색, 1=바닥, 2=벽 — discoveredMap과 동일한 값 관례.
	public int GetTileTerrain(Vector3Int pos) => _tileTerrain.GetValueOrDefault(pos, 0);

	public bool IsTileRevealed(Vector3Int pos) => _tileTerrain.ContainsKey(pos);

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

	// ─────────────────────────── 16장/17장. 오브젝트 위치·흥미도 ───────────────────────────
	// 오브젝트/트랩/방어건물 엔티티가 아직 게임에 없어(가중치 구현현황 문서 참고), objectId를 임의
	// 문자열 키로 받는 범용 API로 둔다 — 실제 오브젝트 시스템이 생기면 이 유닛이 오브젝트를 보는
	// 시점(예: CastRay의 시야 판정)에서 이 메서드들을 호출하면 그대로 연동된다.
	private readonly Dictionary<string, float> _objectBaseInterest = new();
	private readonly Dictionary<string, float> _objectInterest = new();
	private readonly Dictionary<string, Vector3Int> _objectTile = new();

	public void RegisterObjectInterest(string objectId, Vector3Int tile, float baseInterest)
	{
		_objectBaseInterest[objectId] = baseInterest;
		_objectInterest[objectId] = baseInterest;
		_objectTile[objectId] = tile;
	}

	public float GetTileInterest(Vector3Int pos, bool explored, string objectIdAtTile)
	{
		float baseTileInterest = explored ? WeightMath.ExploredTileBaseInterest : WeightMath.UnexploredTileBaseInterest;
		float objectInterest = (objectIdAtTile != null && _objectInterest.TryGetValue(objectIdAtTile, out var oi)) ? oi : 0f;
		return WeightMath.ComposeTileInterest(baseTileInterest, objectInterest);
	}

	// GetTileDanger(pos)와 동일한 이유의 편의 오버로드 — 지형 밝히기 기록으로 탐사 여부를 자동 판단.
	public float GetTileInterest(Vector3Int pos, string objectIdAtTile) => GetTileInterest(pos, IsTileRevealed(pos), objectIdAtTile);

	public void OnObjectInvestigated(string objectId)
	{
		if (_objectInterest.TryGetValue(objectId, out var v))
			_objectInterest[objectId] = WeightMath.ObjectInterestAfterInvestigate(v);
	}

	public void OnObjectCollected(string objectId) => _objectInterest[objectId] = 0f;
	public void OnObjectDestroyed(string objectId) => _objectInterest[objectId] = 0f;

	public void OnObjectDroppedByCarrierDeath(string objectId)
	{
		if (_objectBaseInterest.TryGetValue(objectId, out var baseInterest))
			_objectInterest[objectId] = WeightMath.ObjectInterestAfterDrop(baseInterest);
	}

	// ─────────────────────────── 신규. "지금 보고 있는 몬스터" 위치 기록 ───────────────────────────
	// 15장/18장 수치(위험도/흥미도)를 목격 시점 스냅샷으로 저장한다 — 실시간 조회가 아니라
	// "마지막으로 확인한 기록"이라는 지도의 성격(지도관련_정리 문서 3-2장, 6-5장 실제값-기록값 차이)에
	// 맞춘 것이다. UnitFunction.CastRay가 적을 발견할 때마다 호출한다(유일하게 자동 연동되는 부분).
	private class MonsterSighting
	{
		public Vector3Int Tile;
		public float DangerSnapshot;
		public float InterestSnapshot;
	}
	private readonly Dictionary<string, MonsterSighting> _monsterSightings = new();

	// monsterKey: target.name (인스턴스 식별자 — 위치는 종별이 아니라 그 개체 하나에 대한 정보이므로
	// HumanKnowledgeBase의 종/개체 누적 키(ResolveTargetKey)와는 별개로 항상 인스턴스명을 쓴다).
	public void ObserveMonster(string monsterKey, Vector3Int tile, float dangerSnapshot, float interestSnapshot)
	{
		_monsterSightings[monsterKey] = new MonsterSighting
		{
			Tile = tile,
			DangerSnapshot = dangerSnapshot,
			InterestSnapshot = interestSnapshot,
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
	// (v3 문서 7-1장) — 원래도 개인 인지 개념이었다. API는 준비하되, 방이 보스방인지 등 맵 데이터
	// 확인이 더 필요해 CastRay 자동 연동은 이번 라운드에서 하지 않는다(지도 구현현황 문서 참고).
	public enum RoomExploreState { Unexplored, Exploring, Complete }

	private class RoomKnowledge
	{
		public RoomExploreState State = RoomExploreState.Unexplored;
		public bool IsBossRoom;
		public float ConfirmedUnitDanger, ConfirmedObjectDanger;
		public float ConfirmedUnitInterest, ConfirmedObjectInterest;
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

	public void ObserveUnitInRoom(int roomId, bool isBossRoom, float unitFinalDanger, float unitInterest)
	{
		var r = GetOrCreateRoom(roomId, isBossRoom);
		r.ConfirmedUnitDanger += unitFinalDanger;
		r.ConfirmedUnitInterest += unitInterest;
	}

	public void ObserveObjectInRoom(int roomId, bool isBossRoom, float objectDanger, float objectInterest)
	{
		var r = GetOrCreateRoom(roomId, isBossRoom);
		r.ConfirmedObjectDanger += objectDanger;
		r.ConfirmedObjectInterest += objectInterest;
	}

	public float GetRoomDanger(int roomId, bool isBossRoom)
	{
		var r = GetOrCreateRoom(roomId, isBossRoom);
		return r.State switch
		{
			RoomExploreState.Unexplored => isBossRoom ? WeightMath.UnexploredBossRoomBaseDanger : WeightMath.UnexploredNormalRoomBaseDanger,
			RoomExploreState.Exploring => r.ConfirmedUnitDanger + r.ConfirmedObjectDanger + (isBossRoom ? WeightMath.ExploringBossRoomFixedDanger : WeightMath.ExploringNormalRoomFixedDanger),
			RoomExploreState.Complete => r.ConfirmedUnitDanger + r.ConfirmedObjectDanger,
			_ => 0f,
		};
	}

	public float GetRoomInterest(int roomId, bool isBossRoom)
	{
		var r = GetOrCreateRoom(roomId, isBossRoom);
		return r.State switch
		{
			RoomExploreState.Unexplored => isBossRoom ? WeightMath.UnexploredBossRoomBaseInterest : WeightMath.UnexploredNormalRoomBaseInterest,
			RoomExploreState.Exploring => r.ConfirmedUnitInterest + r.ConfirmedObjectInterest + (isBossRoom ? WeightMath.ExploringBossRoomFixedInterest : WeightMath.ExploringNormalRoomFixedInterest),
			RoomExploreState.Complete => r.ConfirmedUnitInterest + r.ConfirmedObjectInterest,
			_ => 0f,
		};
	}

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

		sb.AppendLine($"[오브젝트] {_objectTile.Count}개");
		foreach (var kv in _objectTile)
		{
			float baseI = _objectBaseInterest.GetValueOrDefault(kv.Key);
			float curI = _objectInterest.GetValueOrDefault(kv.Key);
			sb.AppendLine($"  {kv.Key}: tile={kv.Value} interest={curI:0.##}(기본 {baseI:0.##})");
		}

		sb.AppendLine($"[몬스터 목격] {_monsterSightings.Count}개");
		foreach (var kv in _monsterSightings)
			sb.AppendLine($"  {kv.Key}: tile={kv.Value.Tile} danger={kv.Value.DangerSnapshot:0.##} interest={kv.Value.InterestSnapshot:0.##}");

		sb.AppendLine($"[방] {_rooms.Count}개 (개인 던전 위험도 {GetPersonalDungeonDanger():0.##} / 흥미도 {GetPersonalDungeonInterest():0.##})");
		foreach (var kv in _rooms)
			sb.AppendLine($"  room#{kv.Key} ({(kv.Value.IsBossRoom ? "보스방" : "일반")}, {kv.Value.State}): danger={GetRoomDanger(kv.Key, kv.Value.IsBossRoom):0.##} interest={GetRoomInterest(kv.Key, kv.Value.IsBossRoom):0.##}");

		return sb.ToString();
	}
}
