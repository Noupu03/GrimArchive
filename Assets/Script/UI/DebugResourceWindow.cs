using System.Globalization;
using UnityEngine;

// debug 메뉴의 "자원 조절" 떠 있는 창 — 나무·돌 보유량을 슬라이더 드래그, 숫자 직접 입력, 증감 칩으로 바꾼다(옛 고정 단계 버튼 목록을 대체).
// 이 프로젝트의 디버그/메뉴 UI와 같은 OnGUI 방식이라 새 프리팹이 필요 없다. BottomMenuBar.Constructor가 FloorPanelOverlay처럼 런타임에 같은 GameObject에 붙인다.
// GUI.depth를 낮춰 다른 OnGUI 패널보다 위에 그리고 입력도 먼저 받는다 — 창 위의 마우스 이벤트는 마지막에 Use()해서 아래 패널의 버튼이 같이 눌리지 않게 한다.
// 숫자 입력칸에 포커스가 있는 동안은 GameInputScheme.TextInputFocused로 게임 키보드 단축키(1~4 배속·WASD 등)를 막는다.
public sealed class DebugResourceWindow : MonoBehaviour
{
	private const float Width = 440f;
	private const float Pad = 14f;
	private const float TitleHeight = 40f;
	private const float CardHeight = 136f;
	private const float CardGap = 10f;
	private const float FooterHeight = 38f;
	private const float WindowHeight = TitleHeight + Pad + CardHeight * 2f + CardGap + Pad + FooterHeight + Pad;

	// 슬라이더 범위와 눈금 — 건설·생산·함정 비용(100~300)을 다루기 좋은 0~2000, 10 단위. 더 큰 값은 숫자 입력이나 +1000 칩으로 정한다(값이 범위를 넘으면 손잡이는 오른쪽 끝에 붙는다).
	private const int SliderMax = 2000;
	private const int SliderStep = 10;
	private const int SliderTicks = 4;

	private static readonly ResourceType[] Types = { ResourceType.Wood, ResourceType.Stone };
	private static readonly string[] Labels = { "나무", "돌" };
	private static readonly Color[] Accents = { new Color(0.86f, 0.62f, 0.32f, 1f), new Color(0.62f, 0.74f, 0.9f, 1f) };
	private static readonly Color CardColor = new Color(1f, 1f, 1f, 0.05f);
	private static readonly Color TitleColor = new Color(1f, 1f, 1f, 0.08f);
	private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.16f);
	private static readonly Color FaintLine = new Color(1f, 1f, 1f, 0.3f);
	private static readonly Color InputColor = new Color(0f, 0f, 0f, 0.35f);

	private bool _open;
	private bool _placed;
	private bool _clearFocus;
	private Rect _rect = new Rect(0f, 0f, Width, WindowHeight);
	private Vector2 _dragOffset;
	private readonly string[] _input = { string.Empty, string.Empty };
	private int _focusedField = -1;
	private int _selectAllField = -1; // 포커스가 막 간 입력칸 — 다음 그리기에서 값을 전체 선택한다
	private bool _skipCommit;

	private GUIStyle _titleStyle, _subtitleStyle, _nameStyle, _valueStyle, _inputStyle;

	public bool IsOpen => _open;

	// BottomMenuBar.IsMouseOverUI가 합쳐 부른다 — 창 위 클릭이 월드 클릭(선택·이동 명령)으로 새지 않게.
	public bool IsMouseOver() => _open && GUIMouseUtil.IsMouseOverRect(_rect);

	public void Toggle()
	{
		_open = !_open;
		if (!_open) ReleaseTextFocus();
		_clearFocus = true;
	}

	private void Close()
	{
		_open = false;
		ReleaseTextFocus();
		_clearFocus = true;
	}

	private void OnDisable() => ReleaseTextFocus();

	private void ReleaseTextFocus()
	{
		_focusedField = -1;
		GameInputScheme.TextInputFocused = false;
	}

	private void OnGUI()
	{
		if (!_open) return;
		GUI.depth = -1000;
		EnsureStyles();
		EnsurePlaced();

		Event e = Event.current;
		if (_clearFocus) { GUI.FocusControl(null); _clearFocus = false; }
		// 창 밖을 누르면 숫자 입력칸의 포커스를 푼다 — 안 그러면 월드를 클릭해도 키보드가 계속 막힌다.
		if (e.type == EventType.MouseDown && !_rect.Contains(e.mousePosition)) GUI.FocusControl(null);

		GUI.skin.settings.cursorColor = Color.white;
		GUI.skin.settings.selectionColor = new Color(0.05f, 0.45f, 1f, 0.5f);

		GUIMenuStyleUtil.DrawPanelBox(_rect);
		if (!DrawTitle(e)) return; // 닫기를 눌렀다

		ResourceManager rm = ResourceManager.Instance;
		if (rm == null)
		{
			GUI.Label(new Rect(_rect.x + Pad, _rect.y + TitleHeight + Pad, _rect.width - Pad * 2f, 30f), "자원 관리자를 찾을 수 없습니다.", GUIMenuStyleUtil.BodyLabelStyle);
			return;
		}

		float y = _rect.y + TitleHeight + Pad;
		for (int i = 0; i < Types.Length; i++)
		{
			DrawCard(i, new Rect(_rect.x + Pad, y, _rect.width - Pad * 2f, CardHeight), rm, e);
			y += CardHeight + CardGap;
		}
		DrawFooter(new Rect(_rect.x + Pad, y + Pad - CardGap, _rect.width - Pad * 2f, FooterHeight), rm);

		if (e.type == EventType.Repaint) TrackFocus(rm);
		if (e.isMouse && _rect.Contains(e.mousePosition)) e.Use(); // 아래 패널의 버튼이 같은 클릭을 받지 않게
	}

	// ── 제목 줄(드래그로 창 이동 + 닫기) — 닫기를 눌러 창이 닫혔으면 false ──
	private bool DrawTitle(Event e)
	{
		var title = new Rect(_rect.x, _rect.y, _rect.width, TitleHeight);
		Fill(title, TitleColor);
		GUI.Label(new Rect(title.x + Pad, title.y, 120f, title.height), "자원 조절", _titleStyle);
		GUI.Label(new Rect(title.x + Pad + 100f, title.y + 2f, 160f, title.height), "debug", _subtitleStyle);

		if (GUIMenuStyleUtil.DrawFlatButton(new Rect(title.xMax - 36f, title.y + 6f, 28f, 28f), "X", false))
		{
			Close();
			return false;
		}

		int id = GUIUtility.GetControlID(FocusType.Passive);
		var dragArea = new Rect(title.x, title.y, title.width - 44f, title.height);
		switch (e.GetTypeForControl(id))
		{
			case EventType.MouseDown:
				if (e.button == 0 && dragArea.Contains(e.mousePosition))
				{
					GUIUtility.hotControl = id;
					_dragOffset = e.mousePosition - _rect.position;
					e.Use();
				}
				break;
			case EventType.MouseDrag:
				if (GUIUtility.hotControl == id)
				{
					_rect.position = e.mousePosition - _dragOffset;
					ClampToScreen();
					e.Use();
				}
				break;
			case EventType.MouseUp:
				if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
				break;
		}
		return true;
	}

	// ── 자원 한 종류의 카드: 이름·현재값 / 슬라이더 / 숫자 입력 + 증감 칩 ──
	private void DrawCard(int index, Rect card, ResourceManager rm, Event e)
	{
		ResourceType type = Types[index];
		Color accent = Accents[index];
		int amount = rm.GetResourceAmount(type);

		Fill(card, CardColor);
		GUIMenuStyleUtil.DrawButtonBorder(card, FaintLine);

		float x = card.x + 14f;
		float w = card.width - 28f;

		Fill(new Rect(x, card.y + 14f, 5f, 26f), accent);
		GUI.Label(new Rect(x + 14f, card.y + 8f, 120f, 38f), Labels[index], _nameStyle);
		_valueStyle.normal.textColor = accent;
		GUI.Label(new Rect(x + 100f, card.y + 6f, w - 100f, 42f), amount.ToString("N0", CultureInfo.InvariantCulture), _valueStyle);

		DrawSlider(new Rect(x, card.y + 54f, w, 24f), amount, accent, rm, type, e);

		float rowY = card.y + 92f;
		const float inputWidth = 104f;
		DrawInput(index, new Rect(x, rowY, inputWidth, 32f), accent, rm, e);

		float chipsX = x + inputWidth + 8f;
		float chipWidth = (x + w - chipsX - 6f * 3f) / 4f;
		string[] chipLabels = { "0", "-100", "+100", "+1000" };
		for (int c = 0; c < chipLabels.Length; c++)
		{
			var chip = new Rect(chipsX + c * (chipWidth + 6f), rowY, chipWidth, 32f);
			if (!GUIMenuStyleUtil.DrawFlatButton(chip, chipLabels[c], false)) continue;
			GUI.FocusControl(null);
			switch (c)
			{
				case 0: rm.DebugSetResource(type, 0); break;
				case 1: rm.DebugAdjustResource(type, -100); break;
				case 2: rm.DebugAdjustResource(type, 100); break;
				default: rm.DebugAdjustResource(type, 1000); break;
			}
		}
	}

	// 가는 트랙 + 채움 + 손잡이로 그린 슬라이더 — Unity 기본 슬라이더 스킨 대신 이 프로젝트 패널과 같은 평면 스타일. 드래그 중에는 값이 바로 반영된다.
	private void DrawSlider(Rect r, int amount, Color accent, ResourceManager rm, ResourceType type, Event e)
	{
		int id = GUIUtility.GetControlID(FocusType.Passive);
		var track = new Rect(r.x, r.center.y - 3f, r.width, 6f);
		float t = Mathf.Clamp01(amount / (float)SliderMax);

		for (int k = 0; k <= SliderTicks; k++)
			Fill(new Rect(track.x + track.width * k / SliderTicks - 0.5f, track.yMax + 3f, 1f, 5f), FaintLine);
		Fill(track, TrackColor);
		Fill(new Rect(track.x, track.y, track.width * t, track.height), accent);

		bool dragging = GUIUtility.hotControl == id;
		float knobHalfWidth = dragging ? 9f : 7f;
		var knob = new Rect(track.x + track.width * t - knobHalfWidth, r.center.y - 11f, knobHalfWidth * 2f, 22f);
		Fill(knob, Color.white);
		GUIMenuStyleUtil.DrawButtonBorder(knob, accent);

		var hit = new Rect(r.x - 10f, r.y - 2f, r.width + 20f, r.height + 10f);
		switch (e.GetTypeForControl(id))
		{
			case EventType.MouseDown:
				if (e.button == 0 && hit.Contains(e.mousePosition))
				{
					GUI.FocusControl(null);
					GUIUtility.hotControl = id;
					rm.DebugSetResource(type, SliderValueAt(track, e.mousePosition.x));
					e.Use();
				}
				break;
			case EventType.MouseDrag:
				if (dragging)
				{
					rm.DebugSetResource(type, SliderValueAt(track, e.mousePosition.x));
					e.Use();
				}
				break;
			case EventType.MouseUp:
				if (dragging) { GUIUtility.hotControl = 0; e.Use(); }
				break;
		}
	}

	private static int SliderValueAt(Rect track, float mouseX)
	{
		float t = Mathf.Clamp01((mouseX - track.x) / track.width);
		return Mathf.RoundToInt(t * SliderMax / SliderStep) * SliderStep;
	}

	// 숫자 직접 입력 — 숫자만 받고, Enter나 포커스를 잃을 때 적용한다(Esc는 취소). 포커스가 없으면 항상 현재 값을 보여 준다.
	private void DrawInput(int index, Rect rect, Color accent, ResourceManager rm, Event e)
	{
		string controlName = "DebugResInput" + index;
		bool focused = GUI.GetNameOfFocusedControl() == controlName;
		if (!focused) _input[index] = rm.GetResourceAmount(Types[index]).ToString(CultureInfo.InvariantCulture);

		if (focused && e.type == EventType.KeyDown)
		{
			if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
			{
				Commit(index, rm);
				_skipCommit = true; // 방금 적용했으니 포커스가 풀릴 때 또 적용하지 않는다
				GUI.FocusControl(null);
				e.Use();
			}
			else if (e.keyCode == KeyCode.Escape)
			{
				_skipCommit = true;
				GUI.FocusControl(null);
				e.Use();
			}
		}

		Fill(rect, InputColor);
		GUIMenuStyleUtil.DrawButtonBorder(rect, focused ? accent : FaintLine);
		GUI.SetNextControlName(controlName);
		string edited = GUI.TextField(rect, _input[index], 7, _inputStyle);
		if (edited != _input[index]) _input[index] = DigitsOnly(edited);

		// 입력칸에 막 포커스가 갔으면 값을 전체 선택해 바로 덮어쓸 수 있게 한다(이미 포커스가 있는 채로 다시 누르면 커서 위치를 옮길 수 있게 한 번만).
		if (_selectAllField == index && focused && GUIUtility.keyboardControl != 0)
		{
			((TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl)).SelectAll();
			_selectAllField = -1;
		}
	}

	private void Commit(int index, ResourceManager rm)
	{
		if (int.TryParse(_input[index], NumberStyles.None, CultureInfo.InvariantCulture, out int value))
			rm.DebugSetResource(Types[index], value);
	}

	// 입력칸 포커스 변화를 추적한다 — 포커스를 잃으면 입력값을 적용하고, 포커스가 있는 동안은 게임 키보드 단축키를 막는다(Repaint마다 한 번).
	private void TrackFocus(ResourceManager rm)
	{
		string focusedName = GUI.GetNameOfFocusedControl();
		int now = -1;
		for (int i = 0; i < Types.Length; i++)
			if (focusedName == "DebugResInput" + i) now = i;

		if (_focusedField != -1 && now != _focusedField)
		{
			if (!_skipCommit) Commit(_focusedField, rm);
			_skipCommit = false;
		}
		if (now != -1 && now != _focusedField) _selectAllField = now;
		_focusedField = now;
		GameInputScheme.TextInputFocused = now != -1;
	}

	private static string DigitsOnly(string text)
	{
		var chars = new char[text.Length];
		int n = 0;
		foreach (char c in text)
			if (c >= '0' && c <= '9') chars[n++] = c;
		return new string(chars, 0, n);
	}

	// ── 아래쪽 한 번에 조절 ──
	private void DrawFooter(Rect row, ResourceManager rm)
	{
		float w = (row.width - 8f * 2f) / 3f;
		if (GUIMenuStyleUtil.DrawFlatButton(new Rect(row.x, row.y, w, row.height), $"초기값 {ResourceManager.InitialResourceAmount}", false))
		{
			GUI.FocusControl(null);
			rm.DebugResetResources();
		}
		if (GUIMenuStyleUtil.DrawFlatButton(new Rect(row.x + w + 8f, row.y, w, row.height), "모두 +1000", false))
		{
			GUI.FocusControl(null);
			foreach (var type in Types) rm.DebugAdjustResource(type, 1000);
		}
		if (GUIMenuStyleUtil.DrawFlatButton(new Rect(row.x + (w + 8f) * 2f, row.y, w, row.height), "모두 0", false))
		{
			GUI.FocusControl(null);
			foreach (var type in Types) rm.DebugSetResource(type, 0);
		}
	}

	// ── 배치·스타일 ──
	private void EnsurePlaced()
	{
		if (!_placed)
		{
			// 우측 상단 보유 자원 표시(StatusInfoPanel) 아래쪽에서 시작한다 — 드래그로 옮길 수 있다.
			_rect = new Rect(Screen.width - Width - 24f, 120f, Width, WindowHeight);
			_placed = true;
		}
		ClampToScreen();
	}

	private void ClampToScreen()
	{
		_rect.x = Mathf.Clamp(_rect.x, 0f, Mathf.Max(0f, Screen.width - _rect.width));
		_rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, Screen.height - _rect.height));
	}

	private static void Fill(Rect r, Color color)
	{
		Color prev = GUI.color;
		GUI.color = color;
		GUI.DrawTexture(r, Texture2D.whiteTexture);
		GUI.color = prev;
	}

	private void EnsureStyles()
	{
		if (_titleStyle != null) return;

		_titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
		_titleStyle.normal.textColor = Color.white;

		_subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
		_subtitleStyle.normal.textColor = new Color(1f, 1f, 1f, 0.45f);

		_nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
		_nameStyle.normal.textColor = Color.white;

		_valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };

		// 기본 텍스트 필드 스킨 배경(탁한 회색 이미지)은 버리고 위에서 직접 그린 바탕 위에 글자만 얹는다.
		_inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, padding = new RectOffset(6, 10, 0, 0) };
		foreach (var state in new[] { _inputStyle.normal, _inputStyle.hover, _inputStyle.active, _inputStyle.focused, _inputStyle.onNormal, _inputStyle.onHover, _inputStyle.onActive, _inputStyle.onFocused })
		{
			state.background = null;
			state.textColor = Color.white;
		}
	}
}
