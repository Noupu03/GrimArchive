using System.Collections.Generic;
using UnityEngine;

// 일반 화면 하단 [작업 현황] — 층을 고르고 방별로 건설·수리·강화·철거·시설 이동의 진행/멈춘 일과 대기 사유를 본다(건설 명세 1-8).
// 항목을 누르면 현장의 시설 UI가 열려 같은 화면에서 다시 배정할 수 있다. 새 프리팹 없이 BottomMenuBar가 런타임에 붙인다(IMGUI).
// 패널 높이는 내용에 맞춰 늘고 줄며(화면의 70%까지, 넘치면 스크롤), 모든 글자는 칸 안에서 …로 줄어 밖으로 나가지 않는다.
public class WorkStatusPanel : MonoBehaviour
{
	public static WorkStatusPanel Instance { get; private set; }

	public bool IsOpen { get; private set; }

	private const string StackId = "WorkStatus";
	private const int PanelWidth = 380;
	private const float MinPanelHeight = 110f;
	private const float PanelPaddingY = 8f;
	private const float HeaderHeight = 24f;
	private const float FitSlack = 12f; // 내용이 딱 맞을 때 스크롤바가 깜빡이지 않게 둔 여유

	private int _floor = 1;
	private Vector2 _scroll;
	private float _contentHeight; // 지난 Repaint에서 잰 내용 높이 - 패널 높이를 정한다
	private readonly List<WorkStatusRow> _rows = new List<WorkStatusRow>();
	private float _nextRefreshAt;

	private void Awake() => Instance = this;

	private void OnDestroy()
	{
		if (Instance == this) Instance = null;
	}

	public void Toggle()
	{
		IsOpen = !IsOpen;
		if (IsOpen)
		{
			_floor = CameraController.Instance != null ? CameraController.Instance.CurrentFloor : 1;
			_nextRefreshAt = 0f;
		}
		BottomLeftPanelStack.Report(StackId, IsOpen, PanelWidth);
	}

	private void Update()
	{
		BottomLeftPanelStack.Report(StackId, IsOpen, PanelWidth);
	}

	private Rect GetRect()
	{
		float reserved = BottomLeftPanelStack.GetReservedBottomHeight();
		float maxHeight = Mathf.Max(MinPanelHeight, Screen.height * 0.7f - reserved);
		float height = Mathf.Clamp(_contentHeight + HeaderHeight + PanelPaddingY * 2f + FitSlack, MinPanelHeight, maxHeight);
		float x = BottomLeftPanelStack.Report(StackId, IsOpen, PanelWidth);
		return new Rect(x, Screen.height - height - reserved, PanelWidth, height);
	}

	public bool IsMouseOverPanel() => IsOpen && GUIMouseUtil.IsMouseOverRect(GetRect());

	private void OnGUI()
	{
		if (!IsOpen) return;

		// 행 목록은 0.25초마다만 다시 만든다(OnGUI는 프레임당 여러 번 불린다).
		if (Time.unscaledTime >= _nextRefreshAt && Event.current.type == EventType.Layout)
		{
			_nextRefreshAt = Time.unscaledTime + 0.25f;
			_rows.Clear();
			if (BuildingManager.Instance != null) _rows.AddRange(BuildingManager.Instance.BuildWorkStatusRows(_floor));
		}

		Rect rect = GetRect();
		GUIMenuStyleUtil.DrawPanelBox(rect);
		GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + PanelPaddingY, rect.width - 24f, rect.height - PanelPaddingY * 2f));

		DrawHeader();

		_scroll = GUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
		GUILayout.BeginVertical();
		if (_rows.Count == 0)
			GUILayout.Label("진행 중이거나 멈춘 작업이 없습니다.", GUIMenuStyleUtil.CompactBodyLabelStyle);

		string lastRoom = null;
		for (int i = 0; i < _rows.Count; i++)
		{
			WorkStatusRow r = _rows[i];
			if (r.RoomName != lastRoom)
			{
				GUILayout.Label($"<color=#9fd0ff>▸ {r.RoomName}</color>", GUIMenuStyleUtil.CompactBodyLabelStyle);
				lastRoom = r.RoomName;
			}
			DrawRow(r);
		}
		GUILayout.EndVertical();
		GUIMenuStyleUtil.MeasureContent(ref _contentHeight);
		GUILayout.EndScrollView();
		GUILayout.EndArea();
	}

	// 제목 + 층 이동(◀ ▶) + 닫기. 글자 폭이 아니라 칸 폭으로 배치해 제목이 길어도 버튼을 밀어내지 않는다.
	private void DrawHeader()
	{
		Rect row = GUILayoutUtility.GetRect(0f, HeaderHeight, GUILayout.ExpandWidth(true));
		const float closeWidth = 46f;
		const float arrowWidth = 28f;
		float right = row.xMax;

		bool close = GUIMenuStyleUtil.DrawFlatButton(new Rect(right - closeWidth, row.y + 1f, closeWidth, row.height - 2f), "닫기", false, true, 14, 11, 8f);
		right -= closeWidth + 6f;
		bool next = GUIMenuStyleUtil.DrawFlatButton(new Rect(right - arrowWidth, row.y + 1f, arrowWidth, row.height - 2f), "▶", false, true, 14, 11, 6f);
		right -= arrowWidth + 2f;
		bool prev = GUIMenuStyleUtil.DrawFlatButton(new Rect(right - arrowWidth, row.y + 1f, arrowWidth, row.height - 2f), "◀", false, true, 14, 11, 6f);
		right -= arrowWidth + 6f;
		GUIMenuStyleUtil.DrawClippedLabel(new Rect(row.x, row.y, Mathf.Max(0f, right - row.x), row.height), $"작업 현황 · {_floor}층", GUIMenuStyleUtil.HeaderLabelStyle);

		if (prev) { _floor = Mathf.Max(0, _floor - 1); _nextRefreshAt = 0f; }
		if (next) { _floor = _floor + 1; _nextRefreshAt = 0f; }
		if (close) Toggle();
	}

	// 한 작업 = 두 줄 행(작업·대상 + 진행률 / 남은 시간·담당 인원·대기 사유). 대기 사유가 있으면 설명 줄을 경고색으로 보인다.
	private void DrawRow(WorkStatusRow r)
	{
		// 남은 시간에는 이동·대기를 넣지 않는다(건설 명세 1-8).
		string wait = string.IsNullOrEmpty(r.Wait) ? "진행 중" : $"대기: {r.Wait}";
		string left = $"{r.Task} · {r.Target}";
		string right;
		string detail;
		if (r.Internal)
		{
			right = $"한 주기 {r.Progress * 100f:F0}%";
			detail = $"담당 {r.Assigned}명 중 시설 안 {r.Working}명 · {(string.IsNullOrEmpty(r.Wait) ? "가동 중" : "대기: " + r.Wait)}";
		}
		else
		{
			right = $"{r.Progress * 100f:F0}%";
			detail = $"남은 {r.RemainingSeconds:F0}초 · 담당 {r.Assigned}명 중 작업 {r.Working}명 · {wait}";
		}

		Rect row = GUILayoutUtility.GetRect(0f, GUIMenuStyleUtil.ListRowHeight, GUILayout.ExpandWidth(true));
		if (GUIMenuStyleUtil.DrawListRow(row, left, right, detail, false, true, !string.IsNullOrEmpty(r.Wait)))
		{
			if (r.Door != null) BuildingControlPanel.Instance?.ShowForObject(r.Door);
			else BuildingControlPanel.Instance?.ShowForBuilding(r.Building);
		}
		GUILayout.Space(2f);
	}
}
