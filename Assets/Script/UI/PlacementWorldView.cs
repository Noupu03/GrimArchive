using System;
using System.Collections.Generic;
using UnityEngine;

// 배치모드 OnGUI 공용 그리기 도구.
public static class PlacementGui
{
	public static void Fill(Rect r, Color c)
	{
		Color prev = GUI.color;
		GUI.color = c;
		GUI.DrawTexture(r, Texture2D.whiteTexture);
		GUI.color = prev;
	}

	// 화살촉이 달린 선. GUI는 y가 아래로 늘어난다.
	public static void Arrow(Vector2 from, Vector2 to, Color c, float thickness)
	{
		float length = (to - from).magnitude;
		if (length < 1f) return;
		Matrix4x4 matrix = GUI.matrix;
		Color prev = GUI.color;
		GUIUtility.RotateAroundPivot(Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg, from);
		GUI.color = c;
		GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(from.x + length - 8f, from.y - 4f, 8f, 3f), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(from.x + length - 8f, from.y + 1f, 8f, 3f), Texture2D.whiteTexture);
		GUI.color = prev;
		GUI.matrix = matrix;
	}

	public static Vector2 DirGui(Dir d)
	{
		Vector2Int v = DefensePlacementMath.DirVector(d);
		return new Vector2(v.x, -v.y).normalized;
	}

	public static string ShortName(Unit u)
	{
		string n = u != null && u.unitType != null ? u.unitType.typeName : "?";
		return n.Length > 6 ? n.Substring(0, 6) : n;
	}
}

// 월드 표시 — 저장 위치 표현·홀로그램·예상 범위·회전 화살표. 월드 좌표를 화면 좌표로 바꿔 타일 사각형을 직접 칠한다.
public sealed class PlacementWorldView
{
	public const float ArrowButtonSize = 28f;

	// 방 위 패널처럼 월드에 붙은 UI의 배율 — 카메라 줌이 기준(RefOrthoSize)일 때 1, 확대하면 커지고 축소하면 작아진다.
	// 한계를 둔 것은 멀리 축소해도 글자가 읽히고 크게 확대해도 화면을 덮지 않게 하려는 값이다(기준·한계 모두 튜닝 대상).
	// 단계로 끊는 것은 줌하는 동안 버튼 스타일 캐시(GUIMenuStyleUtil)가 폭마다 쌓이지 않게 하려는 것이다.
	const float RefOrthoSize = 15f;
	const float MinUiScale = 0.4f, MaxUiScale = 3f, UiScaleStep = 0.05f;
	public float WorldUiScale { get; private set; } = 1f;

	static readonly Color SavedColor = new Color(0.25f, 0.55f, 1f, 0.42f);
	static readonly Color SelectedColor = new Color(1f, 0.9f, 0.25f, 0.55f);
	static readonly Color ReassignColor = new Color(1f, 0.25f, 0.2f, 0.55f);
	static readonly Color HologramOk = new Color(0.3f, 1f, 0.4f, 0.45f);
	static readonly Color HologramBad = new Color(1f, 0.2f, 0.2f, 0.5f);
	static readonly Color VisionColor = new Color(0.4f, 0.8f, 1f, 0.14f);
	static readonly Color AttackColor = new Color(1f, 0.55f, 0.2f, 0.22f);
	public static readonly Color ArrowGreen = new Color(0.25f, 0.85f, 0.3f, 1f);

	static readonly string[] ArrowLabels = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };

	readonly PlacementMode _mode;
	readonly GameSession _session;
	readonly HashSet<Vector2Int> _vision = new HashSet<Vector2Int>();
	readonly HashSet<Vector2Int> _attack = new HashSet<Vector2Int>();
	readonly Func<Vector2Int, bool> _isOpaque;

	Vector2 _origin;       // 현재 층 (0,0) 타일의 화면 좌표(좌하단 원점)
	Vector2 _pixelsPerTile;
	int _rangeFloor;
	GUIStyle _label;

	public PlacementWorldView(PlacementMode mode, GameSession session)
	{
		_mode = mode;
		_session = session;
		_isOpaque = p => BlocksSight(_rangeFloor, p);
	}

	// 프레임당 투영 두 번으로 타일→화면 변환을 정한다(타일마다 투영하지 않는다). 카메라가 없으면 false.
	public bool BeginFrame(GUIStyle labelStyle)
	{
		Camera cam = Camera.main;
		if (cam == null) return false;
		Vector3 offset = _mode.CurrentFloorOffset;
		Vector3 o = cam.WorldToScreenPoint(offset);
		Vector3 u = cam.WorldToScreenPoint(offset + new Vector3(1f, 1f, 0f));
		_origin = new Vector2(o.x, o.y);
		_pixelsPerTile = new Vector2(u.x - o.x, u.y - o.y);
		_label = labelStyle;

		// 타일 한 칸의 화면 크기를 기준 줌에서의 크기와 비교한다(직교 카메라면 RefOrthoSize / orthographicSize와 같다).
		float refPixelsPerTile = Screen.height / (2f * RefOrthoSize);
		float raw = refPixelsPerTile > 0f ? Mathf.Abs(_pixelsPerTile.y) / refPixelsPerTile : 1f;
		WorldUiScale = Mathf.Clamp(Mathf.Round(raw / UiScaleStep) * UiScaleStep, MinUiScale, MaxUiScale);
		return true;
	}

	public Rect TileRect(Vector2Int origin, Vector2Int size)
	{
		int w = Mathf.Max(1, size.x), h = Mathf.Max(1, size.y);
		float x = _origin.x + origin.x * _pixelsPerTile.x;
		float top = _origin.y + (origin.y + h) * _pixelsPerTile.y;
		return new Rect(x, Screen.height - top, w * _pixelsPerTile.x, h * _pixelsPerTile.y);
	}

	public Vector2 RoomAnchor(Room room)
	{
		Vector2 c = room.Bounds.center;
		return new Vector2(_origin.x + c.x * _pixelsPerTile.x, Screen.height - (_origin.y + c.y * _pixelsPerTile.y));
	}

	// Repaint에서만 부른다 — 입력을 받지 않는 표시.
	public void DrawOverlays(DefensePlacementSystem defense, PlacementSelection selection, GUIStyle centered)
	{
		int floor = _mode.CurrentFloor;
		DrawSavedMarkers(defense, selection, floor, centered);
		if (_mode.RangeVisible) DrawRanges(defense, selection, floor);
		DrawHologram(selection, centered);
	}

	void DrawSavedMarkers(DefensePlacementSystem defense, PlacementSelection selection, int floor, GUIStyle centered)
	{
		foreach (var record in defense.Records)
		{
			if (record.Floor != floor || !DefensePlacementSystem.IsManageable(record.Unit)) continue;
			Rect rect = TileRect(record.Tile, record.Unit.FootprintSize);
			bool selected = selection.Selected.Contains(record.Unit);

			PlacementGui.Fill(rect, record.NeedsReassign ? ReassignColor : (selected ? SelectedColor : SavedColor));
			GUIMenuStyleUtil.DrawButtonBorder(rect, selected ? Color.yellow : Color.white);
			GUI.Label(new Rect(rect.x, rect.center.y - 9f, rect.width, 18f), PlacementGui.ShortName(record.Unit), centered);
			Vector2 c = rect.center;
			PlacementGui.Arrow(c, c + PlacementGui.DirGui(record.Facing) * Mathf.Max(14f, rect.width * 0.45f), Color.white, 2f);

			if (record.NeedsReassign)
				GUI.Label(new Rect(rect.x - 30f, rect.y - 20f, rect.width + 60f, 18f), "<color=#ff6060>방어 위치 재지정 필요</color>", centered);
		}
	}

	// 선택한 저장 유닛과 다음 배치 인원의 마우스 홀로그램 기준. 불가 타일이면 그리지 않는다.
	void DrawRanges(DefensePlacementSystem defense, PlacementSelection selection, int floor)
	{
		foreach (var u in selection.Selected)
			if (defense.TryGetRecord(u, out var rec) && rec.Floor == floor && !rec.NeedsReassign)
				DrawRange(u, rec.Tile, rec.Facing, floor);
		if (_mode.HoverTile.HasValue && _mode.HoverBlock == PlacementBlock.None && selection.Next != null)
			DrawRange(selection.Next, _mode.HoverTile.Value, _mode.PendingFacing, floor);
	}

	void DrawRange(Unit unit, Vector2Int tile, Dir facing, int floor)
	{
		_rangeFloor = floor;
		Vector2Int fp = unit.FootprintSize;
		Vector2Int center = tile + new Vector2Int(Mathf.Max(0, fp.x - 1) / 2, Mathf.Max(0, fp.y - 1) / 2);

		_vision.Clear();
		int viewDistance = VisionMath.ViewDistance(unit.VisionStat != null ? unit.VisionStat.spotting : 0f);
		DefensePlacementMath.ExpectedVisionTiles(center, viewDistance, facing, _isOpaque, _vision);
		foreach (var t in _vision) PlacementGui.Fill(TileRect(t, Vector2Int.one), VisionColor);

		_attack.Clear();
		int range = unit.Generate != null && unit.unitType != null ? unit.Generate.GetEngageDistance(unit.unitType.typeName, 1) : 1;
		DefensePlacementMath.ExpectedAttackTiles(center, range, _isOpaque, _attack);
		foreach (var t in _attack)
			if (_vision.Contains(t)) PlacementGui.Fill(TileRect(t, Vector2Int.one), AttackColor);
	}

	// 벽 종류와 닫힌 문만 시야·공격선을 가린다. 일반 시설·건설 중 시설의 몸체는 가리지 않는다.
	bool BlocksSight(int floor, Vector2Int p)
	{
		CreateMap cmap = _session.cmap;
		if (cmap == null) return true;
		var key = new Vector3Int(p.x, p.y, floor);
		if (_session.objectGrid.TryGetValue(key, out InteractableObject obj) && DoorSystem.IsBarrier(obj))
			return !(obj.DoorManualOpen || obj.DoorAutoOpen);
		if (!_session.roomGrid.ContainsKey(key)) return true;
		return !cmap.IsStaticTileWalkable(floor, p) && (BuildingManager.Instance == null || BuildingManager.Instance.GetBuildingAt(key) == null);
	}

	void DrawHologram(PlacementSelection selection, GUIStyle centered)
	{
		Unit next = selection.Next;
		if (next == null || !_mode.HoverTile.HasValue || _mode.RotateOpen) return;

		Rect rect = TileRect(_mode.HoverTile.Value, next.FootprintSize);
		bool ok = _mode.HoverBlock == PlacementBlock.None;
		PlacementGui.Fill(rect, ok ? HologramOk : HologramBad);
		GUIMenuStyleUtil.DrawButtonBorder(rect, ok ? ArrowGreen : Color.red);
		GUI.Label(new Rect(rect.x, rect.center.y - 9f, rect.width, 18f), PlacementGui.ShortName(next), centered);
		Vector2 c = rect.center;
		PlacementGui.Arrow(c, c + PlacementGui.DirGui(_mode.PendingFacing) * Mathf.Max(14f, rect.width * 0.45f), Color.white, 2f);
		if (!ok) GUI.Label(new Rect(rect.xMax + 4f, rect.y, 220f, 18f), "<color=#ff7070>" + _mode.HoverBlock.ToKorean() + "</color>", _label);
	}

	// 회전 화살표 8개 — 기준 홀로그램 주위에만 둔다(기준 유닛이 화면 밖이어도). 버튼이라 모든 이벤트에서 부른다.
	public void DrawRotateArrows(DefensePlacementSystem defense, PlacementSelection selection, GUIStyle centered, Action<Rect> register)
	{
		GetRotateReference(defense, selection, out Vector2 center, out float radius);
		var dirs = DefensePlacementMath.AllDirs;
		for (int i = 0; i < dirs.Length; i++)
		{
			Vector2 p = center + PlacementGui.DirGui(dirs[i]) * radius;
			var rect = new Rect(p.x - ArrowButtonSize * 0.5f, p.y - ArrowButtonSize * 0.5f, ArrowButtonSize, ArrowButtonSize);
			register(rect);
			PlacementGui.Fill(rect, ArrowGreen);
			GUIMenuStyleUtil.DrawButtonBorder(rect, Color.white);
			if (GUI.Button(rect, ArrowLabels[i], centered)) _mode.ApplyRotation(dirs[i]);
		}
	}

	void GetRotateReference(DefensePlacementSystem defense, PlacementSelection selection, out Vector2 center, out float radius)
	{
		foreach (var u in selection.Selected)
		{
			if (!defense.TryGetRecord(u, out var rec) || rec.Floor != _mode.CurrentFloor) continue;
			Rect r = TileRect(rec.Tile, u.FootprintSize);
			center = r.center;
			radius = Mathf.Max(r.width, r.height) * 0.5f + 26f;
			return;
		}
		if (_mode.HoverTile.HasValue && selection.Next != null)
		{
			Rect r = TileRect(_mode.HoverTile.Value, selection.Next.FootprintSize);
			center = r.center;
			radius = Mathf.Max(r.width, r.height) * 0.5f + 26f;
			return;
		}
		center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
		radius = 60f;
	}
}
