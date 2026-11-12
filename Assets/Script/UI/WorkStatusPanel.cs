using System.Collections.Generic;
using UnityEngine;

// 일반 화면 하단 [작업 현황] — 층을 고르고 방별로 건설·수리·강화·철거·시설 이동의 진행/멈춘 일과 대기 사유를 본다(건설 명세 1-8).
// 항목을 누르면 현장의 시설 UI가 열려 같은 화면에서 다시 배정할 수 있다. 새 프리팹 없이 BottomMenuBar가 런타임에 붙인다(IMGUI).
public class WorkStatusPanel : MonoBehaviour
{
	public static WorkStatusPanel Instance { get; private set; }

	public bool IsOpen { get; private set; }

	private const string StackId = "WorkStatus";
	private const int PanelWidth = 380;
	private const int PanelHeight = 360;

	private int _floor = 1;
	private Vector2 _scroll;
	private readonly List<WorkStatusRow> _rows = new List<WorkStatusRow>();
	private float _nextRefreshAt;

	private void Awake() => Instance = this;

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
		float x = BottomLeftPanelStack.Report(StackId, IsOpen, PanelWidth);
		float y = Screen.height - PanelHeight - BottomLeftPanelStack.GetReservedBottomHeight();
		return new Rect(x, y, PanelWidth, PanelHeight);
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
		GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 8, rect.width - 24, rect.height - 16));

		GUILayout.BeginHorizontal();
		GUILayout.Label($"[ 작업 현황 · {_floor}층 ]", GUIMenuStyleUtil.LabelStyle);
		GUILayout.FlexibleSpace();
		if (GUIMenuStyleUtil.DrawFlatButtonLayout("◀", options: new[] { GUILayout.Width(28f), GUILayout.Height(22f) })) { _floor = Mathf.Max(0, _floor - 1); _nextRefreshAt = 0f; }
		if (GUIMenuStyleUtil.DrawFlatButtonLayout("▶", options: new[] { GUILayout.Width(28f), GUILayout.Height(22f) })) { _floor = _floor + 1; _nextRefreshAt = 0f; }
		GUILayout.EndHorizontal();

		_scroll = GUILayout.BeginScrollView(_scroll);
		if (_rows.Count == 0)
			GUILayout.Label("진행 중이거나 멈춘 작업이 없습니다.", GUIMenuStyleUtil.BodyLabelStyle);

		string lastRoom = null;
		for (int i = 0; i < _rows.Count; i++)
		{
			WorkStatusRow r = _rows[i];
			if (r.RoomName != lastRoom)
			{
				GUILayout.Label($"▸ {r.RoomName}", GUIMenuStyleUtil.LabelStyle);
				lastRoom = r.RoomName;
			}

			string wait = string.IsNullOrEmpty(r.Wait) ? "진행 중" : $"대기: {r.Wait}";
			// 남은 시간에는 이동·대기를 넣지 않는다(건설 명세 1-8).
			string line = r.Internal
				? $"{r.Task} · {r.Target}\n    한 주기 {r.Progress * 100f:F0}% · 담당 {r.Assigned}명 중 시설 안 {r.Working}명 · {(string.IsNullOrEmpty(r.Wait) ? "가동 중" : "대기: " + r.Wait)}"
				: $"{r.Task} · {r.Target}\n    {r.Progress * 100f:F0}% · 남은 작업 {r.RemainingSeconds:F0}초 · 담당 {r.Assigned}명 중 작업 {r.Working}명 · {wait}";
			if (GUIMenuStyleUtil.DrawFlatButtonLayout(line, options: new[] { GUILayout.Height(40f) }))
				if (r.Door != null) BuildingControlPanel.Instance?.ShowForObject(r.Door);
					else BuildingControlPanel.Instance?.ShowForBuilding(r.Building);
		}
		GUILayout.EndScrollView();

		if (GUIMenuStyleUtil.DrawFlatButtonLayout("닫기", options: new[] { GUILayout.Height(26f) })) Toggle();
		GUILayout.EndArea();
	}
}
