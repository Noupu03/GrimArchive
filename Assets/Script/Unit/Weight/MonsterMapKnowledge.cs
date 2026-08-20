using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// 몬스터(Monster) 유닛 개인이 들고 있는 "지도"(2026-08-20, 사용자 요청 "몬스터들도 개인 지도는 있어야
// 한다. 단순 함정과 지형 기록용.(가중치 X)") — Human.Memory.personalMap(PersonalMapKnowledge)의
// 몬스터용 축소판. PersonalMapKnowledge는 연산공식 문서 15~21장(타일/오브젝트/방 위험도·흥미도)
// 전체를 구현한 무거운 클래스라, 몬스터에게 그대로 재사용하면 "가중치 X"라는 요구와 어긋나고 안 쓰는
// 위험도/흥미도/방 추적 인프라까지 끌고 들어온다 — 그래서 지형 밝히기 + 함정 위치 기록, 딱 두 가지만
// 담는 별도의 가벼운 클래스로 새로 만들었다. 인간의 것과 달리 위험도/흥미도/오브젝트 흥미도/방 상태/
// 몬스터 목격 기록은 전혀 없다.
//
// 판단 근거(문서 없음, 사용자 확인 필요 시 재검토):
//   - 적용 대상: 플레이어 몬스터/야생 몬스터 구분 없이 Monster 전체(모든 유닛이 이미 MemoryComponent를
//     갖고 있어 자연스럽게 확장, Unit.cs OnEnable 참고).
//   - 갱신 시점: 인류의 PersonalMapKnowledge와 동일하게 UnitFunction.CastRay의 시야 처리(ProcessTile)
//     에서 자동으로 채워진다 — 별도 트리거를 새로 만들지 않고 기존 시야 파이프라인에 편승.
//   - 함정 기록: 인류의 "예상 해제 성공률"(9-2장, 해제 시도 기반)은 몬스터가 함정을 해제하지 않으므로
//     불필요 — 오브젝트 ID+위치만 기록하는 단순 존재 확인.
public class MonsterMapKnowledge
{
	// ─────────────────────────── 지형 밝히기 (벽/바닥) ───────────────────────────
	// PersonalMapKnowledge와 동일한 값 관례: 0=미탐색, 1=바닥, 2=벽.
	private readonly Dictionary<Vector3Int, int> _tileTerrain = new();
	private readonly Dictionary<int, Texture2D> _terrainTextures = new();
	private readonly HashSet<int> _dirtyTerrainFloors = new();

	// 반환값: 이 타일을 처음 밝히는 것이면 true(호출부가 필요하면 쓸 수 있게 PersonalMapKnowledge.
	// RevealTile과 동일한 시그니처를 유지 — 지금은 아무도 반환값을 안 쓰지만 나중 확장 대비).
	public bool RevealTile(Vector3Int pos, bool isWall)
	{
		bool isFirstReveal = !_tileTerrain.ContainsKey(pos);
		_tileTerrain[pos] = isWall ? 2 : 1;
		_dirtyTerrainFloors.Add(pos.z);
		return isFirstReveal;
	}

	public int GetTileTerrain(Vector3Int pos) => _tileTerrain.GetValueOrDefault(pos, 0);
	public bool IsTileRevealed(Vector3Int pos) => _tileTerrain.ContainsKey(pos);
	public IEnumerable<int> KnownTerrainFloors => _tileTerrain.Keys.Select(k => k.z).Distinct().OrderBy(f => f);

	// PersonalMapKnowledge.GetTerrainTexture와 동일한 방식(흰색=바닥, 회색=벽, 검정=미탐색) — 인스펙터에서
	// 같은 형태로 보이도록 그림을 그리는 로직을 그대로 맞췄다(사용자 요청 "인간 지도 인스펙터에 쓰는것처럼").
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
		for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.black;
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

	// ─────────────────────────── 함정 위치 기록 (단순 존재 확인, 가중치 없음) ───────────────────────────
	private readonly Dictionary<string, Vector3Int> _knownTraps = new();

	public void RecordTrap(string trapObjectId, Vector3Int tile) => _knownTraps[trapObjectId] = tile;
	public bool IsTrapKnown(string trapObjectId) => _knownTraps.ContainsKey(trapObjectId);
	public IReadOnlyDictionary<string, Vector3Int> KnownTraps => _knownTraps;

	// ─────────────────────────── 디버그 표시 (인스펙터용) ───────────────────────────
	public string BuildDebugSummary()
	{
		var sb = new StringBuilder();
		sb.AppendLine($"[지형 밝히기] {_tileTerrain.Count}칸 밝혀짐 (층: {string.Join(", ", KnownTerrainFloors)}) — 아래 텍스처 참고");

		sb.AppendLine($"[함정 기록] {_knownTraps.Count}개");
		foreach (var kv in _knownTraps)
			sb.AppendLine($"  {kv.Key}: tile={kv.Value}");

		return sb.ToString();
	}
}
