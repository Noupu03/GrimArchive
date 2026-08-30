using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// 몬스터(Monster) 유닛 개인이 들고 있는 "지도" — PersonalMapKnowledge의 몬스터용 축소판. 위험도/
// 흥미도/방 추적은 빼고 지형 밝히기+함정 위치 기록(존재 확인만, 해제 안 함)만 담는다. 문서 없이 자체
// 판단으로 채운 부분 — 플레이어/야생 구분 없이 Monster 전체에 적용되고 CastRay 시야 처리에서 자동 갱신된다.
public class MonsterMapKnowledge
{
	// ─────────────────────────── 지형 밝히기 (벽/바닥) ───────────────────────────
	// PersonalMapKnowledge와 동일한 값 관례: 0=미탐색, 1=바닥, 2=벽.
	private readonly Dictionary<Vector3Int, int> _tileTerrain = new();
	private readonly Dictionary<int, Texture2D> _terrainTextures = new();
	private readonly HashSet<int> _dirtyTerrainFloors = new();

	// 반환값: 이 타일을 처음 밝히는 것이면 true(PersonalMapKnowledge.RevealTile과 시그니처 통일, 확장 대비).
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

	// PersonalMapKnowledge.GetTerrainTexture와 동일한 방식(흰색=바닥, 회색=벽, 검정=미탐색)으로 그린다.
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
