using System.Collections.Generic;
using UnityEngine;

// 배치모드 OnGUI 화면. 기존 UI 규칙(GUIMenuStyleUtil·GUIMouseUtil·BottomLeftPanelStack, 새 프리팹 없음)을 따르고, 실제 시안은 별도 UI 작업이 정한다.
// 프로젝트에 미니맵이 없어 '미니맵 위' 요소(범위 ON/OFF·배치 순서 칸·공통 버튼)는 우하단 열에 쌓는다.
public sealed class PlacementModeUI
{
	const string StackId = "PlacementCards";
	const float Margin = 10f;
	const float CardWidth = 118f;
	const float CardHeight = 52f;
	const float RightColumnWidth = 172f;
	const float ButtonHeight = 34f;

	static readonly Color PanelCellColor = new Color(0.22f, 0.22f, 0.3f, 0.95f);
	static readonly Color SelectedColor = new Color(1f, 0.9f, 0.25f, 0.55f);
	static readonly string[] SlotNumbers = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16" };

	// GUIMouseUtil.IsPointerOverAnyPanel이 다른 패널과 같은 방식으로 조회한다.
	public static PlacementModeUI Active { get; private set; }

	readonly PlacementMode _mode;
	readonly GameSession _session;
	readonly PlacementWorldView _world;

	// 이 프레임에 그린 UI 사각형 — OnGUI가 Update보다 늦어 월드 클릭 판정은 직전 프레임 값을 읽는다.
	readonly List<Rect> _rects = new List<Rect>();

	GUIStyle _small, _centered;
	GUIStyle _roomLabel; // 방 위 패널 전용 — 글자 크기가 줌 배율을 따라 매 프레임 바뀐다(_small은 다른 UI와 공유).
	int _cardPage;
	Unit _cardDown;
	int _slotDown = -1;

	// 문자열은 값이 바뀔 때만 다시 만든다(OnGUI는 프레임당 여러 번 불린다).
	readonly Dictionary<Room, RoomText> _roomTexts = new Dictionary<Room, RoomText>();
	readonly Dictionary<Unit, CardText> _cardTexts = new Dictionary<Unit, CardText>();
	int _unplacedCount = -1;
	string _unplacedLabel = string.Empty;

	struct RoomText { public int Pop, Max, Incoming, Planned; public string PopText, PlanText; }
	struct CardText { public int Pop, Hp; public string Text; }

	public PlacementModeUI(PlacementMode mode, GameSession session)
	{
		_mode = mode;
		_session = session;
		_world = new PlacementWorldView(mode, session);
		Active = this;
	}

	// 하단 미배치 카드 영역(명세 1-2: 배치 유닛을 이 영역으로 끌면 저장 위치·방향만 해제). 카드가 하나도 없어 패널이 안 그려져도 같은 자리가 드롭 대상이다.
	// 오른쪽 열은 제외하고, 하단 메뉴 바가 차지한 높이(reservedBottom) 위의 가로 띠다.
	public static Rect UnplaceZoneRect(float screenWidth, float screenHeight, float reservedBottom)
	{
		float height = CardHeight + 28f;
		float width = Mathf.Max(0f, screenWidth - RightColumnWidth - Margin * 3f);
		return new Rect(Margin, screenHeight - reservedBottom - height, width, height);
	}

	static Rect CurrentUnplaceZone() => UnplaceZoneRect(Screen.width, Screen.height, BottomLeftPanelStack.GetReservedBottomHeight());

	public bool IsOverUnplaceZone() => GUIMouseUtil.IsMouseOverRect(CurrentUnplaceZone());

	public bool IsMouseOverUI()
	{
		if (!_mode.IsActive) return false;
		foreach (var r in _rects)
			if (GUIMouseUtil.IsMouseOverRect(r)) return true;
		return false;
	}

	void Reg(Rect r)
	{
		if (Event.current.type == EventType.Layout) _rects.Add(r);
	}

	public void Draw()
	{
		Event e = Event.current;
		if (e.type == EventType.Layout) _rects.Clear();
		if (!_mode.IsActive) { BottomLeftPanelStack.Report(StackId, false, 0f); return; }

		var defense = _session?.Defense;
		EnsureStyles();
		if (defense == null || !_world.BeginFrame(_small)) return;

		var selection = _mode.Selection;
		bool repaint = e.type == EventType.Repaint;
		if (repaint) _world.DrawOverlays(defense, selection, _centered);
		if (_mode.RotateOpen) _world.DrawRotateArrows(defense, selection, _centered, Reg);

		DrawRoomOverlays(defense);
		DrawBanner();
		DrawRightColumn(defense, selection);
		DrawUnplacedCards();
		DrawUnitList(defense);
		if (repaint) DrawDragBox();
		if (repaint) DrawUnplaceDropCue();
	}

	// 저장 위치의 유닛에서 시작한 드래그 중에는 하단 영역이 "여기에 놓으면 미배치"로 보이게 한다. 영역 위에서는 초록으로 바뀐다.
	void DrawUnplaceDropCue()
	{
		Unit dragged = _mode.DragStartUnit;
		if (!_mode.DragBoxActive || dragged == null) return;

		Rect zone = CurrentUnplaceZone();
		bool over = GUIMouseUtil.IsMouseOverRect(zone);
		PlacementGui.Fill(zone, over ? new Color(0.3f, 1f, 0.4f, 0.28f) : new Color(1f, 0.9f, 0.25f, 0.16f));
		GUIMenuStyleUtil.DrawButtonBorder(zone, over ? PlacementWorldView.ArrowGreen : Color.yellow);
		string name = dragged.unitType != null ? dragged.unitType.typeName : "?";
		GUI.Label(zone, over ? $"놓으면 {name} 미배치" : $"여기로 끌어 놓으면 {name} 미배치", _centered);
	}

	void EnsureStyles()
	{
		if (_small != null) return;
		_small = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, richText = true, wordWrap = false };
		_small.normal.textColor = Color.white;
		GUIMenuStyleUtil.LockStateColors(_small); // 마우스를 올려도 글자색이 변하지 않게(아래 두 스타일이 이 값을 복사한다)
		_centered = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
		_roomLabel = new GUIStyle(_small);
	}

	// ── 방 위: 정원과 [이동 적용]·[유닛] ──

	// 기준 크기(배율 1일 때의 픽셀) — 패널·글자·버튼이 모두 월드 배율(PlacementWorldView.WorldUiScale)만큼 같이 커지고 작아진다.
	const float RoomPanelWidth = 210f, RoomPanelHeight = 58f, RoomPanelGap = 6f;
	const int RoomLabelFontSize = 12, RoomButtonFontSize = 14;

	void DrawRoomOverlays(DefensePlacementSystem defense)
	{
		int floor = _mode.CurrentFloor;
		if (_session.allRooms == null || floor == 0) return;

		float s = _world.WorldUiScale;
		float w = RoomPanelWidth * s, h = RoomPanelHeight * s;
		_roomLabel.fontSize = Mathf.Max(6, Mathf.RoundToInt(RoomLabelFontSize * s));
		int buttonMax = Mathf.Max(6, Mathf.RoundToInt(RoomButtonFontSize * s));
		int buttonMin = Mathf.Max(5, Mathf.RoundToInt(9f * s));
		float buttonPad = 8f * s;

		foreach (var room in _session.allRooms)
		{
			if (room == null || room.Floor != floor || room.MaxPopulation <= 0) continue;
			if (room.RoomFaction != FactionType.Player || !room.FogRevealed) continue;

			Vector2 c = _world.RoomAnchor(room);
			var panel = new Rect(c.x - w * 0.5f, c.y - h - RoomPanelGap * s, w, h);
			// 화면과 겹치지 않으면 그리지 않는다 — 크게 확대하면 패널이 커서 앵커 기준으로는 판단할 수 없다.
			if (panel.xMax < 0f || panel.x > Screen.width || panel.yMax < 0f || panel.y > Screen.height) continue;

			RoomText text = GetRoomText(room, defense);
			Reg(panel); // 스케일이 이미 사각형에 들어가 있어(GUI.matrix 미사용) 마우스 판정용 화면 좌표와 같다
			GUIMenuStyleUtil.DrawPanelBox(panel);
			GUIMenuStyleUtil.DrawClippedLabel(new Rect(panel.x + 6f * s, panel.y + 2f * s, w - 12f * s, 16f * s), text.PopText, _roomLabel);
			GUIMenuStyleUtil.DrawClippedLabel(new Rect(panel.x + 6f * s, panel.y + 18f * s, w - 12f * s, 16f * s), text.PlanText, _roomLabel);

			var applyRect = new Rect(panel.x + 4f * s, panel.y + 36f * s, 100f * s, 20f * s);
			var listRect = new Rect(panel.x + 108f * s, panel.y + 36f * s, 98f * s, 20f * s);
			if (_mode.IsRoomEditable(room))
			{
				if (GUIMenuStyleUtil.DrawFlatButton(applyRect, "이동 적용", false, text.Planned > 0, buttonMax, buttonMin, buttonPad)) _mode.ApplyRoom(room);
			}
			else
			{
				GUIMenuStyleUtil.DrawClippedLabel(applyRect, "코어 수복 필요", _roomLabel, "#ffb060");
			}
			if (GUIMenuStyleUtil.DrawFlatButton(listRect, "유닛", _mode.UnitListRoom == room, true, buttonMax, buttonMin, buttonPad)) _mode.ToggleUnitList(room);
		}
	}

	RoomText GetRoomText(Room room, DefensePlacementSystem defense)
	{
		int pop = room.CurrentPopulation, incoming = defense.IncomingPopulation(room), planned = defense.PlannedPopulation(room);
		if (_roomTexts.TryGetValue(room, out RoomText t) && t.Pop == pop && t.Max == room.MaxPopulation && t.Incoming == incoming && t.Planned == planned) return t;
		t = new RoomText
		{
			Pop = pop, Max = room.MaxPopulation, Incoming = incoming, Planned = planned,
			PopText = RoomStatusText.PopulationCompact(room, incoming), PlanText = RoomStatusText.DefensePlan(room, planned),
		};
		_roomTexts[room] = t;
		return t;
	}

	void DrawBanner()
	{
		const float w = 360f, h = 26f;
		var r = new Rect((Screen.width - w) * 0.5f, 8f, w, h);
		Reg(r);
		GUIMenuStyleUtil.DrawPanelBox(r);
		GUI.Label(r, "배치모드 · 일시정지 중 — 우클릭/ESC: 닫기·선택 해제", _centered);
	}

	// ── 우하단 열 ──

	void DrawRightColumn(DefensePlacementSystem defense, PlacementSelection selection)
	{
		float x = Screen.width - RightColumnWidth - Margin;
		float y = Screen.height - BottomLeftPanelStack.GetReservedBottomHeight() - Margin - ButtonHeight;

		var rangeRect = new Rect(x, y, RightColumnWidth, ButtonHeight);
		Reg(rangeRect);
		if (GUIMenuStyleUtil.DrawFlatButton(rangeRect, _mode.RangeVisible ? "범위 표시 ON" : "범위 표시 OFF", _mode.RangeVisible)) _mode.SetRangeVisible(!_mode.RangeVisible);
		y -= 6f;

		DrawOrderSlots(selection, x, ref y);
		y -= 6f;

		bool hasSelection = selection.HasSelection;
		y -= ButtonHeight;
		if (ColumnButton(x, y, "이동 적용", false, hasSelection && _mode.IsFloorEditable)) _mode.ApplySelected();
		y -= ButtonHeight + 4f;
		if (ColumnButton(x, y, "미배치", false, hasSelection)) _mode.UnplaceSelected();
		y -= ButtonHeight + 4f;
		if (ColumnButton(x, y, "회전", _mode.RotateOpen, hasSelection)) _mode.ToggleRotate();

		// 층 전체가 코어 파괴 영향이면 숨긴다.
		if (_mode.IsFloorEditable)
		{
			y -= ButtonHeight + 10f;
			if (ColumnButton(x, y, "전체 이동 적용", false, defense.AnySavedOnFloor(_mode.CurrentFloor))) _mode.ApplyFloor();
		}
	}

	bool ColumnButton(float x, float y, string label, bool active, bool enabled)
	{
		var rect = new Rect(x, y, RightColumnWidth, ButtonHeight);
		Reg(rect);
		return GUIMenuStyleUtil.DrawFlatButton(rect, label, active, enabled);
	}

	// 아직 새 타일을 지정하지 않은 선택 인원 — 모습 위에 마우스를 두면 이름, 드래그하거나 A→B 클릭으로 순서를 바꾼다.
	void DrawOrderSlots(PlacementSelection selection, float x, ref float y)
	{
		const float slot = 34f, gap = 4f;
		int count = selection.Order.Count;
		int perRow = Mathf.Max(1, Mathf.FloorToInt((RightColumnWidth + gap) / (slot + gap)));
		int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)perRow));
		float height = 20f + rows * (slot + gap);
		var panel = new Rect(x, y - height, RightColumnWidth, height);
		y = panel.y;
		Reg(panel);
		GUIMenuStyleUtil.DrawPanelBox(panel);
		GUI.Label(new Rect(panel.x + 6f, panel.y + 1f, panel.width - 12f, 18f), count > 0 ? "배치 순서 (1번이 다음)" : "배치 순서 — 비어 있음", _small);

		Event e = Event.current;
		for (int i = 0; i < count; i++)
		{
			Unit u = selection.Order[i];
			var r = new Rect(panel.x + 6f + (i % perRow) * (slot + gap), panel.y + 20f + (i / perRow) * (slot + gap), slot, slot);
			PlacementGui.Fill(r, _mode.OrderSwapFirst == i ? SelectedColor : PanelCellColor);
			GUIMenuStyleUtil.DrawButtonBorder(r, i == 0 ? PlacementWorldView.ArrowGreen : Color.white);
			GUI.Label(new Rect(r.x, r.y + 2f, r.width, 16f), PlacementGui.ShortName(u), _centered);
			GUI.Label(new Rect(r.x, r.yMax - 15f, r.width, 14f), i < SlotNumbers.Length ? SlotNumbers[i] : (i + 1).ToString(), _centered);

			if (e.type == EventType.Repaint && r.Contains(e.mousePosition))
				GUI.Label(new Rect(r.x, r.y - 18f, 150f, 16f), "<color=#ffffa0>" + (u.unitType != null ? u.unitType.typeName : "?") + "</color>", _small);

			if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { _slotDown = i; e.Use(); }
			if (e.type == EventType.MouseUp && e.button == 0 && _slotDown >= 0 && r.Contains(e.mousePosition))
			{
				if (_slotDown == i) _mode.ClickOrderSlot(i);
				else _mode.SwapOrderSlots(_slotDown, i);
				_slotDown = -1;
				e.Use();
			}
		}
		if (e.type == EventType.MouseUp && e.button == 0) _slotDown = -1;
	}

	// ── 하단: 미배치 카드 ──

	void DrawUnplacedCards()
	{
		IReadOnlyList<Unit> unplaced = _mode.UnplacedUnits();
		if (unplaced.Count == 0) { BottomLeftPanelStack.Report(StackId, false, 0f); return; }

		float maxWidth = Mathf.Max(CardWidth + 80f, Screen.width - (RightColumnWidth + Margin * 2f) - 40f);
		int perPage = Mathf.Max(1, Mathf.FloorToInt((maxWidth - 120f) / (CardWidth + 4f)));
		int pages = Mathf.Max(1, Mathf.CeilToInt(unplaced.Count / (float)perPage));
		_cardPage = Mathf.Clamp(_cardPage, 0, pages - 1);
		int shown = Mathf.Min(perPage, unplaced.Count - _cardPage * perPage);
		float width = 120f + shown * (CardWidth + 4f);
		float height = CardHeight + 28f;

		float x = BottomLeftPanelStack.Report(StackId, true, width);
		var panel = new Rect(x, Screen.height - height - BottomLeftPanelStack.GetReservedBottomHeight(), width, height);
		Reg(panel);
		GUIMenuStyleUtil.DrawPanelBox(panel);
		GUI.Label(new Rect(panel.x + 6f, panel.y + 2f, 110f, 18f), UnplacedLabel(unplaced.Count), _small);

		if (pages > 1)
		{
			if (GUIMenuStyleUtil.DrawFlatButton(new Rect(panel.x + 6f, panel.y + 22f, 52f, 22f), "◀", false, _cardPage > 0)) _cardPage--;
			if (GUIMenuStyleUtil.DrawFlatButton(new Rect(panel.x + 62f, panel.y + 22f, 52f, 22f), "▶", false, _cardPage < pages - 1)) _cardPage++;
		}

		Event e = Event.current;
		for (int i = 0; i < shown; i++)
		{
			Unit u = unplaced[_cardPage * perPage + i];
			var r = new Rect(panel.x + 120f + i * (CardWidth + 4f), panel.y + 22f, CardWidth, CardHeight);
			bool selected = _mode.Selection.Selected.Contains(u);
			PlacementGui.Fill(r, selected ? SelectedColor : PanelCellColor);
			GUIMenuStyleUtil.DrawButtonBorder(r, selected ? Color.yellow : Color.white);
			GUIMenuStyleUtil.DrawClippedLabel(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, 18f), u.unitType != null ? u.unitType.typeName : "?", _small);
			GUIMenuStyleUtil.DrawClippedLabel(new Rect(r.x + 4f, r.y + 24f, r.width - 8f, 18f), CardLabel(u), _small);

			// 클릭 후 타일 선택 / 카드를 끌어 지도 위에 놓기
			if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { _cardDown = u; e.Use(); }
			if (e.type == EventType.MouseUp && e.button == 0 && _cardDown == u)
			{
				if (r.Contains(e.mousePosition)) _mode.SelectUnplaced(u, e.control);
				else _mode.DropUnplacedAt(u, GameInputScheme.PointerScreenPos);
				_cardDown = null;
				e.Use();
			}
		}
		if (e.type == EventType.MouseUp && e.button == 0) _cardDown = null;
	}

	string UnplacedLabel(int count)
	{
		if (count != _unplacedCount) { _unplacedCount = count; _unplacedLabel = $"미배치 {count}기"; }
		return _unplacedLabel;
	}

	string CardLabel(Unit u)
	{
		int pop = Mathf.Max(0, u.populationCost), hp = Mathf.CeilToInt(u.hp);
		if (_cardTexts.TryGetValue(u, out CardText t) && t.Pop == pop && t.Hp == hp) return t.Text;
		if (_cardTexts.Count > 64) _cardTexts.Clear(); // 사라진 유닛이 쌓이지 않게
		string text = $"인구 {pop} · HP {hp}";
		_cardTexts[u] = new CardText { Pop = pop, Hp = hp, Text = text };
		return text;
	}

	// ── 방별 [유닛] 명단 ──

	void DrawUnitList(DefensePlacementSystem defense)
	{
		Room room = _mode.UnitListRoom;
		if (room == null) return;

		List<Unit> units = defense.UnitsSavedInRoom(room.Floor, room.RoomId);
		const float w = 460f, rowH = 56f;
		float h = 30f + Mathf.Max(1, units.Count) * rowH;
		var panel = new Rect(Margin + 10f, 44f, w, Mathf.Min(h, Screen.height - 120f));
		Reg(panel);
		GUIMenuStyleUtil.DrawPanelBox(panel);
		GUIMenuStyleUtil.DrawClippedLabel(new Rect(panel.x + 8f, panel.y + 4f, w - 16f, 20f), $"{room.RoomName} — 방어 배치 유닛 {units.Count}기 (한 명을 누르면 그 유닛만 이동 적용)", _small);

		if (units.Count == 0)
		{
			GUI.Label(new Rect(panel.x + 8f, panel.y + 34f, w - 16f, 20f), "이 방에 방어 배치된 유닛이 없습니다.", _small);
			return;
		}

		bool editable = _mode.IsRoomEditable(room);
		for (int i = 0; i < units.Count; i++)
		{
			float ry = panel.y + 28f + i * rowH;
			if (ry + rowH > panel.yMax) break;
			DrawUnitRow(new Rect(panel.x + 6f, ry, w - 12f, rowH - 4f), units[i], editable);
		}
	}

	void DrawUnitRow(Rect row, Unit unit, bool editable)
	{
		UnitStatusLine line = _mode.DescribeUnit(unit);
		PlacementGui.Fill(row, new Color(0.18f, 0.18f, 0.24f, 0.9f));
		GUIMenuStyleUtil.DrawClippedLabel(new Rect(row.x + 4f, row.y + 1f, 130f, 18f), line.Name, _small);
		GUIMenuStyleUtil.DrawClippedLabel(new Rect(row.x + 4f, row.y + 19f, 160f, 18f), line.Location, _small);
		GUIMenuStyleUtil.DrawClippedLabel(new Rect(row.x + 170f, row.y + 1f, 176f, 18f), line.Activity, _small);

		bool hasNote = !string.IsNullOrEmpty(line.Note);
		if (line.Progress.HasValue)
		{
			var bar = new Rect(row.x + 170f, row.y + 24f, 120f, 8f);
			PlacementGui.Fill(bar, new Color(0.1f, 0.1f, 0.1f, 1f));
			PlacementGui.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(line.Progress.Value), bar.height), PlacementWorldView.ArrowGreen);
			// 게이지가 있어도 메모(진화 남은 시간·반복 작업 안내)는 아래 줄에 보여 준다 — 실패 사유가 있으면 그쪽이 우선이다.
			if (hasNote && string.IsNullOrEmpty(line.Failure))
				GUIMenuStyleUtil.DrawClippedLabel(new Rect(row.x + 4f, row.y + 36f, 340f, 16f), line.Note, _small, "#c8c8c8");
		}
		else if (hasNote)
		{
			GUIMenuStyleUtil.DrawClippedLabel(new Rect(row.x + 170f, row.y + 19f, 176f, 18f), line.Note, _small, "#c8c8c8");
		}
		if (!string.IsNullOrEmpty(line.Failure))
			GUIMenuStyleUtil.DrawClippedLabel(new Rect(row.x + 4f, row.y + 36f, 340f, 16f), line.Failure, _small, "#ff7070");

		// 마지막 유닛에게 지시해도 명단은 닫지 않는다.
		if (GUIMenuStyleUtil.DrawFlatButton(new Rect(row.xMax - 96f, row.y + 12f, 90f, 28f), "이동 적용", false, editable)) _mode.ApplyUnit(unit);
	}

	void DrawDragBox()
	{
		if (!_mode.DragBoxActive) return;
		Vector2 a = _mode.DragStartScreen, b = _mode.DragCurrentScreen;
		var r = new Rect(Mathf.Min(a.x, b.x), Screen.height - Mathf.Max(a.y, b.y), Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
		PlacementGui.Fill(r, new Color(0.3f, 1f, 0.3f, 0.15f));
		GUIMenuStyleUtil.DrawButtonBorder(r, new Color(0.3f, 1f, 0.3f, 0.9f));
	}
}
