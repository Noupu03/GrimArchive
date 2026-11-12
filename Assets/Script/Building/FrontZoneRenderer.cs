using System.Collections.Generic;
using UnityEngine;

// 생산시설 정면 출현 구역을 타일마다 반투명 사각형으로 보여 준다(건설 명세 2-10·2-13). 새 프리팹 없이 코드로 만든다.
public sealed class FrontZoneRenderer
{
	private readonly string _name;
	private GameObject _root;
	private readonly List<SpriteRenderer> _tiles = new List<SpriteRenderer>();
	private RectInt _shown;
	private int _shownFloor = int.MinValue;
	private Color _shownColor;

	public FrontZoneRenderer(string name) { _name = name; }

	public void Show(RectInt zone, int floor, Vector3 floorOffset, Color color)
	{
		if (_root == null) _root = new GameObject(_name);
		if (!_root.activeSelf) _root.SetActive(true);
		if (_shownFloor == floor && _shown.Equals(zone) && _shownColor == color && _tiles.Count == zone.width * zone.height) return;

		_shown = zone;
		_shownFloor = floor;
		_shownColor = color;

		int count = zone.width * zone.height;
		while (_tiles.Count < count)
		{
			var go = new GameObject("Tile");
			go.transform.SetParent(_root.transform, false);
			var sr = go.AddComponent<SpriteRenderer>();
			sr.sprite = WorldWhiteSprite.Get();
			sr.sortingOrder = 4; // 시설(6) 아래, 바닥 위
			_tiles.Add(sr);
		}

		int idx = 0;
		for (int x = 0; x < zone.width; x++)
		{
			for (int y = 0; y < zone.height; y++)
			{
				SpriteRenderer sr = _tiles[idx++];
				sr.gameObject.SetActive(true);
				sr.color = color;
				sr.transform.position = new Vector3(zone.x + x + 0.5f, zone.y + y + 0.5f, 0f) + floorOffset;
				sr.transform.localScale = new Vector3(0.94f, 0.94f, 1f);
			}
		}
		for (; idx < _tiles.Count; idx++) _tiles[idx].gameObject.SetActive(false);
	}

	public void Hide()
	{
		if (_root != null && _root.activeSelf) _root.SetActive(false);
	}

	public void Destroy()
	{
		if (_root != null) Object.Destroy(_root);
		_root = null;
		_tiles.Clear();
	}
}
