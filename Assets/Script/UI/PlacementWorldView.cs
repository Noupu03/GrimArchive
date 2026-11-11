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

	// 두께가 있는 선분 하나(화살촉 없음) — 폴리라인 윤곽선용. GUI는 y가 아래로 늘어난다.
	public static void Line(Vector2 from, Vector2 to, Color c, float thickness)
	{
		float length = (to - from).magnitude;
		if (length < 0.5f) return;
		Matrix4x4 matrix = GUI.matrix;
		Color prev = GUI.color;
		GUIUtility.RotateAroundPivot(Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg, from);
		GUI.color = c;
		GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
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
	static readonly Color AttackColor = new Color(1f, 0.55f, 0.2f, 0.22f);
	public static readonly Color ArrowGreen = new Color(0.25f, 0.85f, 0.3f, 1f);
	static readonly Color NonPendingColor = new Color(0.55f, 0.85f, 1f, 0.9f);

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
		_isOpaque = IsOpaqueCached;
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
		_opaqueMemo.Clear();

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
		DrawManageableOutlines(floor);
		DrawSavedMarkers(defense, selection, floor, centered);
		if (_mode.RangeVisible) DrawRanges(defense, selection, floor);
		DrawHologram(selection, centered);
		DrawSelectedUnits(selection, floor, centered); // 다른 표시 위에 얹어 항상 보이게 한다
	}

	// 명세 1-1 표1 "조작 가능한 유닛·시설은 구별되는 색으로 보인다. 관리 가능한 종류는 지금 할 일이 없어도 선명하게 표시한다. 최대 HP인 고정 문도 같은 색을 유지한다."
	// 시설(청록)과 플레이어가 열고 닫을 수 있는 문(주황)에 윤곽과 옅은 채움을 씌운다. 문은 담당 코어가 가동 중인 층에서만 관리 대상이다.
	// 임시 표시다(문서: 최종 시안에서 색·밝기로 대체 가능). 기본 벽·보스방 계단의 반투명 처리는 하지 않는다.
	static readonly Color FacilityOutline = new Color(0.35f, 0.9f, 1f, 0.95f);
	static readonly Color DoorOutline = new Color(1f, 0.7f, 0.25f, 0.95f);
	readonly HashSet<InteractableObject> _drawnDoors = new HashSet<InteractableObject>();

	bool IsTileRevealed(int x, int y, int floor)
		=> _session.roomGrid.TryGetValue(new Vector3Int(x, y, floor), out Room room) && room != null && room.FogRevealed;

	void DrawManageableOutlines(int floor)
	{
		BuildingManager buildings = BuildingManager.Instance;
		if (buildings != null)
		{
			foreach (var b in buildings.AllBuildings)
			{
				if (b == null || b.IsDummy || b.Position.z != floor || !IsTileRevealed(b.Position.x, b.Position.y, floor)) continue;
				Rect r = TileRect(new Vector2Int(b.Position.x, b.Position.y), b.Footprint);
				PlacementGui.Fill(r, new Color(FacilityOutline.r, FacilityOutline.g, FacilityOutline.b, 0.10f));
				GUIMenuStyleUtil.DrawButtonBorder(r, FacilityOutline);
			}
		}

		if (!BuildingManager.AreCoreFunctionsActive(floor) || _session.objectGrid == null) return;
		_drawnDoors.Clear();
		foreach (var obj in _session.objectGrid.Values)
		{
			if (obj == null || obj.IsCollected || obj.Position.z != floor || obj.DoorOwnerFaction != FactionType.Player) continue;
			if (obj.Tags == null || !obj.Tags.Contains(DoorSystem.DoorTag) || !_drawnDoors.Add(obj)) continue; // 1×2 문은 칸마다 별칭으로 들어 있어 한 번만 그린다
			if (!IsTileRevealed(obj.Position.x, obj.Position.y, floor)) continue;

			int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
			foreach (var t in obj.AllTiles())
			{
				minX = Mathf.Min(minX, t.x); minY = Mathf.Min(minY, t.y);
				maxX = Mathf.Max(maxX, t.x); maxY = Mathf.Max(maxY, t.y);
			}
			if (minX > maxX) continue;
			Rect r = TileRect(new Vector2Int(minX, minY), new Vector2Int(maxX - minX + 1, maxY - minY + 1));
			PlacementGui.Fill(r, new Color(DoorOutline.r, DoorOutline.g, DoorOutline.b, 0.12f));
			GUIMenuStyleUtil.DrawButtonBorder(r, DoorOutline);
		}
	}

	// 배치 대상으로 고른 유닛을 실제 맵 위에서 강조한다 — 같은 종류가 여럿일 때 누구를 골랐는지 구분하려는 것.
	// 다음에 놓을 유닛(순서 1번)은 초록 펄스 + "▼ 다음", 나머지 순서 인원은 노란 테두리 + 번호,
	// 이미 놓았지만 선택은 남은 유닛(회전·미배치·이동 적용 대상)은 얇은 하늘색 테두리. 멀리 줌아웃해도 보이도록 최소 크기를 보장한다.
	void DrawSelectedUnits(PlacementSelection selection, int floor, GUIStyle centered)
	{
		if (!selection.HasSelection) return;

		const float MinBox = 26f;
		float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f); // 일시정지(timeScale≈0)에서도 움직이도록 unscaledTime
		foreach (var u in selection.Selected)
		{
			if (u == null || u.hp <= 0f || u.currentFloor != floor) continue; // 다른 층 유닛은 이 층 화면에 좌표가 없다

			int order = selection.Order.IndexOf(u);
			bool isNext = order == 0;
			bool pending = order >= 0;

			Rect body = TileRect(u.position, u.FootprintSize);
			float size = Mathf.Max(MinBox, Mathf.Max(body.width, body.height) + 8f);
			var box = new Rect(body.center.x - size * 0.5f, body.center.y - size * 0.5f, size, size);

			Color border = isNext ? Color.Lerp(ArrowGreen, Color.white, pulse * 0.6f) : (pending ? Color.yellow : NonPendingColor);
			if (pending) PlacementGui.Fill(box, isNext ? new Color(0.25f, 0.85f, 0.3f, 0.18f + 0.22f * pulse) : new Color(1f, 0.9f, 0.25f, 0.18f));
			GUIMenuStyleUtil.DrawButtonBorder(box, border);
			if (pending) GUIMenuStyleUtil.DrawButtonBorder(new Rect(box.x + 2f, box.y + 2f, box.width - 4f, box.height - 4f), border); // 두껍게

			if (!pending) continue;
			string text = isNext ? "<color=#60ff70>▼ 다음</color>" : "<color=#ffe84a>" + (order + 1) + "</color>";
			GUI.Label(new Rect(box.center.x - 40f, box.y - 18f, 80f, 16f), text, centered);
		}
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

		// 시야·인지 범위는 실제 유닛을 선택했을 때(UnitVisual.DrawVisionAndPerceptionRange)와 같은 부채꼴 윤곽선으로 그린다.
		// 원점은 실제 유닛 비주얼의 기준점(UnitGenerate.SyncVisual — 풋프린트 가로 중앙, 발밑 +0.05)이다. 몸체 중심이 아니다.
		DrawVisionOutline(unit, new Vector2(tile.x + fp.x * 0.5f, tile.y + 0.05f), facing);

		// 공격 범위는 예상 시야 타일(벽이 가리는 칸 제외) 안쪽만 타일로 칠한다 — 시야 타일 집합은 이 판정용으로 계산만 하고 칠하지 않는다.
		_vision.Clear();
		int viewDistance = VisionMath.ViewDistance(unit.VisionStat != null ? unit.VisionStat.spotting : 0f);
		DefensePlacementMath.ExpectedVisionTiles(center, viewDistance, facing, _isOpaque, _vision);

		_attack.Clear();
		int range = unit.Generate != null && unit.unitType != null ? unit.Generate.GetEngageDistance(unit.unitType.typeName, 1) : 1;
		DefensePlacementMath.ExpectedAttackTiles(center, range, _isOpaque, _attack);
		foreach (var t in _attack)
			if (_vision.Contains(t)) PlacementGui.Fill(TileRect(t, Vector2Int.one), AttackColor);
	}

	// UnitGenerate.RefreshSelectionVisual이 UnitVisual에 넘기는 값(반경·각도·방향)과 같은 기하를 월드 좌표로 만들어 화면에 선으로 그린다.
	// 색·굵기(월드 단위라 줌에 따라 변함)는 UnitVisual의 것을 그대로 쓴다. 실제 선택 표시와 달리 선은 벽 종류와 닫힌 문에서 잘린다 —
	// 배치모드 설계명세서 1-10장 "벽 종류와 닫힌 문만 시야와 공격선을 차단한다"(BlocksSight)를 예상 범위에 반영하기 위해서다.
	void DrawVisionOutline(Unit unit, Vector2 bodyCenter, Dir facing)
	{
		float spotting = unit.VisionStat != null ? unit.VisionStat.spotting : 0f;
		bool human = unit is Human;
		Vector2Int dv = DefensePlacementMath.DirVector(facing);
		Vector2 forward = dv == Vector2Int.zero ? Vector2.down : new Vector2(dv.x, dv.y);

		DrawConeOutline(bodyCenter, VisionMath.ViewDistance(spotting), VisionMath.BaseViewAngleDeg, forward, UnitVisual.VisionColorOf(human), UnitVisual.VisionLineWidth);
		DrawConeOutline(bodyCenter, VisionMath.AwarenessDistance(spotting), VisionMath.AwarenessAngle(spotting), forward, UnitVisual.PerceptionColorOf(human), UnitVisual.PerceptionLineWidth);
		if (unit.isSpecialUnit)
			DrawCircleOutline(bodyCenter, VisionMath.CircularPerceptionRadius(spotting), UnitVisual.CircularColorOf(human), UnitVisual.CircularLineWidth);
	}

	// 월드(타일 단위, y 위쪽) → OnGUI(y 아래쪽) 좌표. RoomAnchor와 같은 변환이다.
	Vector2 WorldToGui(Vector2 w)
		=> new Vector2(_origin.x + w.x * _pixelsPerTile.x, Screen.height - (_origin.y + w.y * _pixelsPerTile.y));

	float OutlineThickness(float worldWidth) => Mathf.Max(1.5f, worldWidth * Mathf.Abs(_pixelsPerTile.y)); // 너무 가늘면 사라지므로 최소 두께

	// 중심 → 호 시작 → 호 → 호 끝 → 중심 — UnitVisual.DrawCone(loop=true)과 같은 닫힌 부채꼴이되, 호의 각 점은 광선이 벽·닫힌 문을 만나는 곳에서 멈춘다.
	// 실제 표시(20분할)보다 촘촘하게 나눠야 벽 모서리를 따라 꺾인다 — 3도마다 한 줄, 최소 20분할.
	void DrawConeOutline(Vector2 origin, float radius, float angleDeg, Vector2 forward, Color color, float worldWidth)
	{
		if (radius <= 0f) return;
		int segments = Mathf.Clamp(Mathf.CeilToInt(angleDeg / 3f), 20, 120);
		float thickness = OutlineThickness(worldWidth);
		float start = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - angleDeg * 0.5f;

		Vector2 centerGui = WorldToGui(origin);
		Vector2 last = centerGui;
		for (int i = 0; i <= segments; i++)
		{
			float rad = (start + angleDeg * i / segments) * Mathf.Deg2Rad;
			var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
			Vector2 p = WorldToGui(origin + dir * ClipRay(origin, dir, radius));
			PlacementGui.Line(last, p, color, thickness);
			last = p;
		}
		PlacementGui.Line(last, centerGui, color, thickness);
	}

	void DrawCircleOutline(Vector2 origin, float radius, Color color, float worldWidth)
	{
		if (radius <= 0f) return;
		const int segments = 48;
		float thickness = OutlineThickness(worldWidth);

		Vector2 first = default, last = default;
		for (int i = 0; i < segments; i++)
		{
			float rad = 360f * i / segments * Mathf.Deg2Rad;
			var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
			Vector2 p = WorldToGui(origin + dir * ClipRay(origin, dir, radius));
			if (i == 0) first = p; else PlacementGui.Line(last, p, color, thickness);
			last = p;
		}
		PlacementGui.Line(last, first, color, thickness);
	}

	float ClipRay(Vector2 origin, Vector2 dir, float maxRadius)
		=> DefensePlacementMath.RayClipDistance(origin, dir, maxRadius, _isOpaque);

	// 같은 프레임 안에서는 타일의 시야 차단 여부가 바뀌지 않으므로 한 번만 계산한다(BeginFrame이 비운다).
	readonly Dictionary<Vector2Int, bool> _opaqueMemo = new Dictionary<Vector2Int, bool>();

	bool IsOpaqueCached(Vector2Int p)
	{
		if (_opaqueMemo.TryGetValue(p, out bool blocked)) return blocked;
		blocked = BlocksSight(_rangeFloor, p);
		_opaqueMemo[p] = blocked;
		return blocked;
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
