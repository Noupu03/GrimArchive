using System;
using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 배치모드 컨트롤러 — 게임을 일시정지하고 방어 위치·방향과 이동 적용을 편집한다. InputManager가 소유하고 활성일 때 Update를 위임한다.
//
// ⚠️ 이 모드 안에서만 좌클릭으로 배치한다(일반 화면은 좌=선택, 우=실행). 우클릭·ESC는 회전 UI → 인원 목록 → 선택 순으로 닫고, ESC만 마지막에 모드를 나간다.
// 진입하면 자동 일시정지하고 종료해도 정지를 유지한다.
public sealed class PlacementMode
{
	const float DragThresholdPixels = 6f;
	const float DoubleClickSeconds = 0.3f;
	const string RangePrefKey = "GrimArchive.PlacementRangeVisible";

	readonly GameSession _session;
	readonly UnitGenerate _unitGenerate;
	readonly BuildingManager _buildingManager;
	readonly Func<bool> _isConstructionPreviewActive;
	readonly Action _closeConstructionPreview;

	public PlacementModeUI UI { get; }
	public bool IsActive { get; private set; }
	public PlacementSelectionStore Store { get; } = new PlacementSelectionStore();

	public bool RotateOpen { get; private set; }
	public Room UnitListRoom { get; private set; }
	public Dir PendingFacing { get; private set; } = Dir.DOWN;
	public bool RangeVisible { get; private set; } = true;

	// 마우스 홀로그램(다음 배치 인원)의 타일과 불가 사유.
	public Vector2Int? HoverTile { get; private set; }
	public PlacementBlock HoverBlock { get; private set; }

	public bool DragBoxActive { get; private set; }
	// 드래그를 저장 위치의 유닛 위에서 시작했으면 그 유닛 — 하단 카드 영역에서 놓으면 박스 선택 대신 그 유닛만 미배치로 돌린다(명세 1-2).
	public Unit DragStartUnit { get; private set; }
	public Vector2 DragStartScreen { get; private set; }
	public Vector2 DragCurrentScreen { get; private set; }
	bool _mouseDown;

	Unit _lastClickedUnit;
	float _lastClickTime = -999f;
	int _lastFloor = int.MinValue; // 층 전환 감지 — 모드에 들어올 때마다 다시 잡는다(Enter가 초기화)
	public int OrderSwapFirst { get; private set; } = -1;

	// 마지막 이동 적용에서 유닛별로 막힌 사유 — 인원 목록이 보여 준다.
	readonly Dictionary<Unit, PlacementBlock> _failures = new Dictionary<Unit, PlacementBlock>();

	readonly List<Unit> _unplaced = new List<Unit>();
	int _unplacedFrame = -1;

	public PlacementMode(GameSession session, UnitGenerate unitGenerate, BuildingManager buildingManager, Func<bool> isConstructionPreviewActive, Action closeConstructionPreview)
	{
		_session = session;
		_unitGenerate = unitGenerate;
		_buildingManager = buildingManager;
		_isConstructionPreviewActive = isConstructionPreviewActive;
		_closeConstructionPreview = closeConstructionPreview;
		UI = new PlacementModeUI(this, session);
		RangeVisible = LoadRangeVisible();
	}

	public int CurrentFloor => CameraController.Instance != null ? CameraController.Instance.CurrentFloor : 1; // InputManager의 floor=1 고정값은 쓰지 않는다
	public PlacementSelection Selection => Store.For(CurrentFloor);
	public Vector3 CurrentFloorOffset => _unitGenerate != null ? _unitGenerate.GetFloorOffset(CurrentFloor) : Vector3.zero;
	DefensePlacementSystem Defense => _session?.Defense;

	// ── 진입·종료 ──

	public void Toggle()
	{
		if (IsActive) Exit();
		else Enter();
	}

	public void Enter()
	{
		if (IsActive || _session == null) return;
		IsActive = true;

		_closeConstructionPreview?.Invoke(); // 미확정 미리보기만 닫는다. 확정한 계획·배정은 유지된다.
		BuildingControlPanel.Instance?.ClosePanel();
		_session.isPaused = true;
		Time.timeScale = 0.0001f;

		ResetTransient();
		_lastFloor = int.MinValue;
		NoticeCenter.Instance?.PushMomentary("배치모드 — 위치를 저장하고 [이동 적용] 뒤 시간을 재개하면 걷습니다.", NoticeCenter.InfoColor);
		LogHelper.Log(LogHelper.GAME, "[배치모드] 진입 — 자동 일시정지");
	}

	// 종료해도 일시정지는 유지하고, 저장한 위치·성공한 명령은 되돌리지 않는다.
	public void Exit()
	{
		if (!IsActive) return;
		IsActive = false;
		Store.ClearAll();
		ResetTransient();
		_failures.Clear();
		LogHelper.Log(LogHelper.GAME, "[배치모드] 종료 — 일시정지 유지");
	}

	void ResetTransient()
	{
		RotateOpen = false;
		UnitListRoom = null;
		HoverTile = null;
		HoverBlock = PlacementBlock.None;
		DragBoxActive = false;
		DragStartUnit = null;
		_mouseDown = false;
		OrderSwapFirst = -1;
		_lastClickedUnit = null;
	}

	// ── 입력 ──

	public void Update()
	{
		if (!IsActive) return;

		if (!_session.isPaused) // 다른 경로가 풀어도 편집 중엔 시간이 흐르지 않게 한다
		{
			_session.isPaused = true;
			Time.timeScale = 0.0001f;
		}

		// 실제 층 전환은 인원 목록을 닫는다(명세 1-7: "유닛·오브젝트 선택, 실제 층 전환 등 다른 행동을 하면 닫고 그 입력을 처리한다").
		int floor = CurrentFloor;
		if (_lastFloor != floor)
		{
			if (_lastFloor != int.MinValue) { UnitListRoom = null; RotateOpen = false; OrderSwapFirst = -1; }
			_lastFloor = floor;
		}

		Selection.Prune();
		PruneFailures();
		if (Defense == null) return;

		// 모드를 유지한 채 [건설]로 미리보기가 켜지면 그쪽이 마우스를 쓴다. ESC는 미리보기만 닫는다.
		if (_isConstructionPreviewActive != null && _isConstructionPreviewActive())
		{
			HoverTile = null;
			if (GameInputScheme.EscapePressedThisFrame) _closeConstructionPreview?.Invoke();
			return;
		}

		if (GameInputScheme.EscapePressedThisFrame) { CloseCurrentStep(exitIfNothingLeft: true); return; }
		if (GameInputScheme.SecondaryDown) { CloseCurrentStep(exitIfNothingLeft: false); return; }

		UpdateRotateKey();
		UpdateHover();
		UpdateMouse();
	}

	// R: 배치 중이면 다음에 놓을 방향을, 이미 놓은 유닛을 골랐으면 그 유닛들의 방향을 시계 방향으로 45도 돌린다. 방향 버튼 UI가 열려 있으면 그 버튼으로 고른다.
	void UpdateRotateKey()
	{
		if (RotateOpen || !GameInputScheme.RotatePressedThisFrame) return;
		if (Selection.Next != null)
		{
			PendingFacing = DefensePlacementMath.NextClockwise(PendingFacing);
			return;
		}
		foreach (Unit u in Selection.Selected)
		{
			if (!Defense.TryGetRecord(u, out var record) || record.Floor != CurrentFloor) continue;
			ApplyRotation(DefensePlacementMath.NextClockwise(record.Facing)); // 첫 유닛 기준으로 한 칸 돌려 선택 전원에 적용한다
			return;
		}
	}

	public void CloseCurrentStep(bool exitIfNothingLeft)
	{
		OrderSwapFirst = -1;
		if (RotateOpen) { RotateOpen = false; return; }
		if (UnitListRoom != null) { UnitListRoom = null; return; }
		// 열려 있는 시설·오브젝트·방 정보 UI도 현재 조작 단계다 — 임시 선택을 풀거나 모드를 나가기 전에 먼저 닫는다(명세 1-1).
		if (BuildingControlPanel.Instance != null && BuildingControlPanel.Instance.IsOpen) { BuildingControlPanel.Instance.ClosePanel(); return; }
		if (Selection.HasSelection) { Selection.Clear(); return; }
		if (exitIfNothingLeft) Exit();
	}

	void UpdateHover()
	{
		HoverTile = null;
		HoverBlock = PlacementBlock.None;
		Unit next = Selection.Next;
		if (next == null || RotateOpen || IsPointerOverUI()) return;

		Vector3Int grid = ScreenGridUtil.ScreenToGridPos(GameInputScheme.PointerScreenPos, CurrentFloorOffset, CurrentFloor);
		var tile = new Vector2Int(grid.x, grid.y);
		HoverTile = tile;
		HoverBlock = Defense.CheckSave(next, CurrentFloor, tile);
	}

	void UpdateMouse()
	{
		if (GameInputScheme.PrimaryDown && !IsPointerOverUI())
		{
			_mouseDown = true;
			DragBoxActive = false;
			DragStartScreen = DragCurrentScreen = GameInputScheme.PointerScreenPos;
			Vector3Int startGrid = ScreenGridUtil.ScreenToGridPos(DragStartScreen, CurrentFloorOffset, CurrentFloor);
			DragStartUnit = FindPlacedUnitAt(new Vector2Int(startGrid.x, startGrid.y));
		}

		if (_mouseDown && GameInputScheme.PrimaryHeld)
		{
			DragCurrentScreen = GameInputScheme.PointerScreenPos;
			if (!DragBoxActive && Vector2.Distance(DragCurrentScreen, DragStartScreen) >= DragThresholdPixels) DragBoxActive = true;
		}

		if (_mouseDown && GameInputScheme.PrimaryUp)
		{
			if (DragBoxActive)
			{
				if (DragStartUnit != null && UI.IsOverUnplaceZone()) { Unplace(DragStartUnit); OrderSwapFirst = -1; } // 그 유닛만 하단 미배치로(명세 1-2)
				else DoBoxSelect(DragStartScreen, DragCurrentScreen, GameInputScheme.SelectAddHeld);
			}
			else DoClick(GameInputScheme.PointerScreenPos, GameInputScheme.SelectAddHeld);
			_mouseDown = false;
			DragBoxActive = false;
			DragStartUnit = null;
		}
	}

	public bool IsPointerOverUI()
		=> UI.IsMouseOverUI()
			|| GUIMouseUtil.IsPointerOverAnyPanel()
			|| (BuildingControlPanel.Instance != null && BuildingControlPanel.Instance.IsMouseOverPanel());

	// ── 선택·배치 ──

	// 저장 위치에 보이는 유닛 표현이 박스 안에 들어오면 선택한다. 영역 안의 건물은 포함하지 않는다.
	void DoBoxSelect(Vector2 startScreen, Vector2 endScreen, bool additive)
	{
		Vector3 offset = CurrentFloorOffset;
		Vector3 a = ScreenGridUtil.ScreenToWorldPoint(startScreen) - offset;
		Vector3 b = ScreenGridUtil.ScreenToWorldPoint(endScreen) - offset;
		float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x), minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);

		var boxed = new List<Unit>();
		foreach (var record in Defense.Records)
		{
			if (record.Floor != CurrentFloor || !DefensePlacementSystem.IsManageable(record.Unit)) continue;
			Vector2 c = DefensePlacementMath.BodyCenter(record.Tile, record.Unit.FootprintSize);
			if (c.x >= minX && c.x <= maxX && c.y >= minY && c.y <= maxY) boxed.Add(record.Unit);
		}

		Selection.SelectGroup(boxed, new Vector2(a.x, a.y), CenterOf, additive);
		if (boxed.Count > 0) BuildingControlPanel.Instance?.ClosePanel();
		OrderSwapFirst = -1;
		UnitListRoom = null;
	}

	Vector2 CenterOf(Unit u)
		=> DefensePlacementMath.BodyCenter(Defense.TryGetRecord(u, out var r) ? r.Tile : u.position, u.FootprintSize);

	void DoClick(Vector2 screenPos, bool additive)
	{
		if (RotateOpen) return; // 회전 중 빈 곳 좌클릭은 방향 확정도 배치도 아니다

		Vector3Int grid = ScreenGridUtil.ScreenToGridPos(screenPos, CurrentFloorOffset, CurrentFloor);
		var tile = new Vector2Int(grid.x, grid.y);

		Unit hit = FindPlacedUnitAt(tile);
		if (hit != null) { ClickPlacedUnit(hit, additive); return; }

		// 시설·오브젝트를 선택하면 그 층의 배치 순서와 유닛 선택이 끝난다. 저장한 위치·발행한 명령은 유지된다.
		var key = new Vector3Int(tile.x, tile.y, CurrentFloor);
		BuildingData building = _buildingManager != null ? _buildingManager.GetBuildingAt(key) : null;
		if (building != null)
		{
			SelectFacilityInstead(() => BuildingControlPanel.Instance?.ShowForBuilding(building));
			return;
		}
		if (_session.objectGrid.TryGetValue(key, out InteractableObject obj) && IsRevealed(tile))
		{
			SelectFacilityInstead(() => BuildingControlPanel.Instance?.ShowForObject(obj));
			return;
		}

		// 순서가 비었으면(마지막 유닛 뒤) 빈 타일을 눌러도 아무것도 안 한다 — 새로 배치하려면 대상을 다시 선택한다.
		Unit next = Selection.Next;
		if (next != null) PlaceNext(next, tile);
	}

	void ClickPlacedUnit(Unit hit, bool additive)
	{
		bool isDouble = hit == _lastClickedUnit && (Time.unscaledTime - _lastClickTime) <= DoubleClickSeconds;
		_lastClickedUnit = isDouble ? null : hit;
		_lastClickTime = Time.unscaledTime;
		if (isDouble) { Unplace(hit); return; }

		if (additive) Selection.Toggle(hit);
		else Selection.SelectOnly(hit);
		BuildingControlPanel.Instance?.ClosePanel();
		UnitListRoom = null;
		OrderSwapFirst = -1;
	}

	void SelectFacilityInstead(Action showPanel)
	{
		Selection.Clear();
		UnitListRoom = null;
		showPanel();
	}

	void PlaceNext(Unit unit, Vector2Int tile)
	{
		if (Defense.TrySave(unit, CurrentFloor, tile, PendingFacing, out PlacementBlock block))
		{
			Selection.OnPlaced(unit);
			OrderSwapFirst = -1;
			return;
		}
		HoverBlock = block;
		NoticeCenter.Instance?.PushMomentary($"{unit.unitType?.typeName}: {block.ToKorean()}", NoticeCenter.WarningColor);
	}

	// Room.FogRevealed 기준 — InputManager와 같다.
	bool IsRevealed(Vector2Int tile)
		=> _session.roomGrid.TryGetValue(new Vector3Int(tile.x, tile.y, CurrentFloor), out Room room) && room != null && room.FogRevealed;

	Unit FindPlacedUnitAt(Vector2Int tile)
	{
		foreach (var record in Defense.Records)
		{
			if (record.Floor != CurrentFloor || !DefensePlacementSystem.IsManageable(record.Unit)) continue;
			if (DefensePlacementMath.BodiesOverlap(record.Tile, record.Unit.FootprintSize, tile, Vector2Int.one)) return record.Unit;
		}
		return null;
	}

	// ── UI 명령 ──

	// 하단 카드 클릭 — 선택하고 순서 맨 앞에 둔다. 이어서 타일을 누르면 배치된다.
	public void SelectUnplaced(Unit unit, bool additive)
	{
		if (unit == null) return;
		if (additive) Selection.Toggle(unit);
		else Selection.SelectOnly(unit);
		BuildingControlPanel.Instance?.ClosePanel();
		UnitListRoom = null;
		RotateOpen = false;
	}

	// 카드를 끌어 지도 위에서 놓았다 — 그 타일에 바로 배치한다.
	public void DropUnplacedAt(Unit unit, Vector2 screenPos)
	{
		if (unit == null || IsPointerOverUI()) { SelectUnplaced(unit, false); return; }
		Vector3Int grid = ScreenGridUtil.ScreenToGridPos(screenPos, CurrentFloorOffset, CurrentFloor);
		Selection.SelectOnly(unit);
		PlaceNext(unit, new Vector2Int(grid.x, grid.y));
	}

	public void Unplace(Unit unit)
	{
		if (unit == null) return;
		Defense.Unplace(unit);
		Selection.Remove(unit); // 하단 카드로 돌아간다
	}

	public void UnplaceSelected()
	{
		foreach (var u in new List<Unit>(Selection.Selected)) Unplace(u);
	}

	public void ToggleRotate()
	{
		if (!Selection.HasSelection) return;
		RotateOpen = !RotateOpen;
		OrderSwapFirst = -1;
	}

	// 선택 인원 전체와 다음 배치에 적용한다. 순서와 현재 행동은 그대로다.
	public void ApplyRotation(Dir facing)
	{
		PendingFacing = facing;
		foreach (var u in Selection.Selected) Defense.SetFacing(u, facing);
		RotateOpen = false;
	}

	public void SetRangeVisible(bool visible)
	{
		RangeVisible = visible;
		try
		{
			PlayerPrefs.SetInt(RangePrefKey, visible ? 1 : 0);
			PlayerPrefs.Save();
		}
		catch (Exception) { /* 저장이 막힌 환경 — 이번 실행에서만 유지 */ }
	}

	static bool LoadRangeVisible()
	{
		try { return PlayerPrefs.GetInt(RangePrefKey, 1) != 0; }
		catch (Exception) { return true; }
	}

	public void ClickOrderSlot(int index)
	{
		if (OrderSwapFirst < 0) { OrderSwapFirst = index; return; }
		int first = OrderSwapFirst;
		OrderSwapFirst = -1;
		if (first != index) Selection.Swap(first, index);
	}

	public void SwapOrderSlots(int a, int b)
	{
		if (Selection.Swap(a, b)) OrderSwapFirst = -1;
	}

	public void ToggleUnitList(Room room)
	{
		UnitListRoom = UnitListRoom == room ? null : room;
		RotateOpen = false;
	}

	// ── 이동 적용 ──

	public void ApplyRoom(Room room)
	{
		if (room != null) RunApply(Defense.UnitsSavedInRoom(room.Floor, room.RoomId));
	}

	public void ApplyFloor() => RunApply(Defense.UnitsSavedOnFloor(CurrentFloor));
	public void ApplySelected() => RunApply(new List<Unit>(Selection.Selected));

	public void ApplyUnit(Unit unit)
	{
		if (unit != null) RunApply(new List<Unit> { unit });
	}

	void RunApply(List<Unit> targets)
	{
		if (targets.Count == 0) return;
		foreach (var u in targets) _failures.Remove(u);
		DefenseApplyResult result = Defense.ApplyMoves(targets);
		foreach (var f in result.Failed) _failures[f.unit] = f.reason;
		NoticeCenter.Instance?.PushMomentary(result.Summary(), result.Failed.Count > 0 ? NoticeCenter.WarningColor : NoticeCenter.InfoColor);
	}

	void PruneFailures()
	{
		if (_failures.Count == 0) return;
		List<Unit> dead = null;
		foreach (var u in _failures.Keys)
			if (u == null || u.hp <= 0f) (dead ??= new List<Unit>()).Add(u);
		if (dead != null)
			foreach (var u in dead) _failures.Remove(u);
	}

	// ── 조회(UI) ──

	// 하단 카드 — 방어 위치가 없는 생존 아군 몬스터. 프레임당 한 번만 모은다.
	public IReadOnlyList<Unit> UnplacedUnits()
	{
		if (_unplacedFrame == Time.frameCount) return _unplaced;
		_unplaced.Clear();
		if (_session?.units != null)
			foreach (var u in _session.units)
				if (DefensePlacementSystem.IsManageable(u) && !Defense.IsPlaced(u)) _unplaced.Add(u);
		_unplacedFrame = Time.frameCount;
		return _unplaced;
	}

	public bool IsRoomEditable(Room room) => room != null && BuildingManager.AreCoreFunctionsActive(room.Floor);
	public bool IsFloorEditable => BuildingManager.AreCoreFunctionsActive(CurrentFloor);

	public UnitStatusLine DescribeUnit(Unit unit)
	{
		Defense.TryGetRecord(unit, out DefenseRecord record);
		PlacementBlock? failure = _failures.TryGetValue(unit, out PlacementBlock reason) ? reason : (PlacementBlock?)null;
		return PlacementUnitStatus.Describe(unit, record, failure);
	}
}
