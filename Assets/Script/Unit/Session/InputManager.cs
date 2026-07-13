using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using VContainer;
using Haare.Util.Logger;

public class InputManager : MonoBehaviour
{
	// 하위 호환용: 기존 코드는 "선택된 유닛 1기"를 이렇게 참조한다.
	// 실제 저장소는 selectedUnits이고, 이 프로퍼티는 그 목록의 첫 번째 유닛을 가리킨다.
	public Unit selectedUnit
	{
		get => selectedUnits.Count > 0 ? selectedUnits[0] : null;
		set
		{
			selectedUnits.Clear();
			if (value != null) selectedUnits.Add(value);
		}
	}

	public List<Unit> selectedUnits = new List<Unit>();

	// 드래그 박스(스타크래프트식) 관련 상태
	private const float DragThresholdPixels = 6f;
	private bool _isMouseDown;
	private bool _dragBoxActive;
	private Vector2 _dragStartScreenPos;
	private Vector2 _dragCurrentScreenPos;

	// 더블클릭 판정(같은 유닛을 짧은 시간 안에 다시 클릭) 관련 상태
	private const float DoubleClickTimeThreshold = 0.3f;
	// "근방" 판정 반경(타일 기준). 화면에 보이는 전체가 아니라 클릭한 유닛 주변만 잡는다.
	private const float SameTypeNearbyRadius = 15f;
	private Unit _lastClickedUnit;
	private float _lastClickTime = -999f;

	private UnitGenerate _unitGenerate;
	private GameSession _gameSession;

	[Inject]
	public void Construct(UnitGenerate unitGenerate, GameSession gameSession)
	{
		_unitGenerate = unitGenerate;
		_gameSession = gameSession;
	}

	private bool IsPointInFootprint(Vector3Int pos, Unit u)
	{
		if (u == null || u.unitType == null) return false;

		int w = (int)u.unitType.footprint.x;
		int h = (int)u.unitType.footprint.y;

		return (pos.x >= u.position.x && pos.x < u.position.x + w &&
				pos.y >= u.position.y && pos.y < u.position.y + h);
	}

	private Unit FindUnitAtGridPos(Vector3Int gridPos, int currentFloor)
	{
		foreach (var u in _gameSession.units)
		{
			if (u == null || u.hp <= 0) continue;
			if (u.currentFloor != currentFloor) continue;

			if (IsPointInFootprint(gridPos, u))
				return u;
		}
		return null;
	}

	private Vector3 ScreenToWorldPoint(Vector2 screenPos)
	{
		return Camera.main.ScreenToWorldPoint(
			new Vector3(screenPos.x, screenPos.y, Mathf.Abs(Camera.main.transform.position.z))
		);
	}

	private Vector3Int ScreenToGridPos(Vector2 screenPos, Vector3 floorOffset, int currentFloor)
	{
		Vector3 localPoint = ScreenToWorldPoint(screenPos) - floorOffset;

		return new Vector3Int(
			Mathf.FloorToInt(localPoint.x),
			Mathf.FloorToInt(localPoint.y),
			currentFloor
		);
	}

	void Update()
	{
		if (_gameSession == null) return;
		if (Keyboard.current == null || Mouse.current == null) return;

		int currentFloor = 1;
		Vector3 floorOffset =
			_unitGenerate != null
			? _unitGenerate.GetFloorOffset(currentFloor)
			: Vector3.zero;

		// Ctrl = "기존 선택에 추가" (클릭/드래그/더블클릭 공통).
		bool addHeld = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;

		// =====================================================
		// 좌클릭 - 드래그 시작
		// =====================================================
		if (Mouse.current.leftButton.wasPressedThisFrame)
		{
			// UI(디버그 패널 등) 위에서 누른 클릭은 월드 선택으로 취급하지 않는다.
			bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

			if (!overUI)
			{
				_isMouseDown = true;
				_dragBoxActive = false;
				_dragStartScreenPos = Mouse.current.position.ReadValue();
				_dragCurrentScreenPos = _dragStartScreenPos;
			}
		}

		// =====================================================
		// 좌클릭 - 드래그 중 (박스 갱신)
		// =====================================================
		if (_isMouseDown && Mouse.current.leftButton.isPressed)
		{
			_dragCurrentScreenPos = Mouse.current.position.ReadValue();

			if (!_dragBoxActive &&
				Vector2.Distance(_dragCurrentScreenPos, _dragStartScreenPos) >= DragThresholdPixels)
			{
				_dragBoxActive = true;
			}
		}

		// =====================================================
		// 좌클릭 - 뗌 (드래그였으면 박스 선택, 아니면 기존 클릭 선택/공격)
		// =====================================================
		if (_isMouseDown && Mouse.current.leftButton.wasReleasedThisFrame)
		{
			if (_dragBoxActive)
			{
				DoBoxSelect(_dragStartScreenPos, _dragCurrentScreenPos, floorOffset, currentFloor, addHeld);
			}
			else
			{
				DoClickSelect(_dragCurrentScreenPos, floorOffset, currentFloor, addHeld);
			}

			_isMouseDown = false;
			_dragBoxActive = false;
		}

		// =====================================================
		// 우클릭 (이동) - 선택된 유닛 전원에게 명령
		// =====================================================
		if (Mouse.current.rightButton.wasPressedThisFrame && selectedUnits.Count > 0)
		{
			Vector2 mousePos = Mouse.current.position.ReadValue();
			Vector3Int gridPos = ScreenToGridPos(mousePos, floorOffset, currentFloor);

			// 방(Room) 병합 로직 (B안: 그리드 연동)
			if (_gameSession.roomGrid.TryGetValue(gridPos, out Room targetRoom))
			{
				if (targetRoom.IsCombatActive)
				{
					LogHelper.Log(LogHelper.GAME, "해당 방은 전투 중이므로 이동할 수 없습니다.");
					return;
				}

				int totalCostToMove = 0;
				foreach (var unit in selectedUnits)
				{
					if (unit == null || unit.hp <= 0) continue;
					if (unit is Human) continue; // 인간은 배치 인원수 검사에서 제외
					if (unit.CurrentRoom != targetRoom) totalCostToMove += unit.populationCost;
				}

				if (!targetRoom.CanAcceptPopulation(totalCostToMove))
				{
					LogHelper.Log(LogHelper.GAME, "대상 방의 수용 가능 인구수를 초과하여 이동 명령이 취소되었습니다.");
					return;
				}

				foreach (var unit in selectedUnits)
				{
					if (unit == null || unit.hp <= 0) continue;
					unit.playerInteractTarget = null;
					unit.playerAttackTarget = null;
					
					if (unit.CurrentRoom != targetRoom)
					{
						unit.IssueRoomMoveCommand(targetRoom, new Vector2Int(gridPos.x, gridPos.y));
					}
					else
					{
						// 이미 같은 방에 있다면 일반 그리드 이동 처리
						unit.playerMoveTarget = new Vector2Int(gridPos.x, gridPos.y);
					}
				}

				LogHelper.Log(LogHelper.GAME, $"방 이동 명령: {selectedUnits.Count}기 -> {targetRoom.RoomName} ({gridPos.x}, {gridPos.y})");
			}
			else
			{
				foreach (var unit in selectedUnits)
				{
					if (unit == null || unit.hp <= 0) continue;

					unit.playerInteractTarget = null;

					if (unit is Human && _gameSession.objectGrid.TryGetValue(gridPos, out InteractableObject obj))
					{
						if (!obj.IsCollected)
						{
							unit.playerInteractTarget = gridPos;
						}
					}

					unit.playerMoveTarget = new Vector2Int(gridPos.x, gridPos.y);
					unit.playerAttackTarget = null;
				}

				LogHelper.Log(LogHelper.GAME,
					$"일반 이동 명령: {selectedUnits.Count}기 -> ({gridPos.x}, {gridPos.y})"
				);
			}
		}

		// =====================================================
		// 속도 / 일시정지 (그대로 유지)
		// =====================================================
		if (Keyboard.current.spaceKey.wasPressedThisFrame)
		{
			_gameSession.isPaused = !_gameSession.isPaused;
			Time.timeScale = _gameSession.isPaused
				? 0.0001f
				: _gameSession.currentGameSpeed;
		}
		if (Keyboard.current.digit0Key.wasPressedThisFrame)
		{
			_gameSession.currentGameSpeed = 0.5f;
			if (!_gameSession.isPaused) Time.timeScale = 0.5f;
		}

		if (Keyboard.current.digit1Key.wasPressedThisFrame)
		{
			_gameSession.currentGameSpeed = 1f;
			if (!_gameSession.isPaused) Time.timeScale = 1f;
		}

		if (Keyboard.current.digit2Key.wasPressedThisFrame)
		{
			_gameSession.currentGameSpeed = 2f;
			if (!_gameSession.isPaused) Time.timeScale = 2f;
		}

		if (Keyboard.current.digit3Key.wasPressedThisFrame)
		{
			_gameSession.currentGameSpeed = 3f;
			if (!_gameSession.isPaused) Time.timeScale = 3f;
		}
	}

	// =====================================================
	// 클릭 선택 / 공격 (드래그 없이 뗀 경우)
	// =====================================================
	private void DoClickSelect(Vector2 screenPos, Vector3 floorOffset, int currentFloor, bool addHeld)
	{
		Vector3Int gridPos = ScreenToGridPos(screenPos, floorOffset, currentFloor);

		// 1. 유닛 클릭이면 최우선으로 선택 처리 (진영 제한 없음, 기존 동작 유지)
		Unit clickedUnit = FindUnitAtGridPos(gridPos, currentFloor);

		if (clickedUnit != null)
		{
			bool isDoubleClick =
				clickedUnit == _lastClickedUnit &&
				(Time.unscaledTime - _lastClickTime) <= DoubleClickTimeThreshold;

			_lastClickedUnit = clickedUnit;
			_lastClickTime = Time.unscaledTime;

			if (isDoubleClick)
			{
				// 세 번째 클릭이 다시 더블클릭으로 판정되는 것을 막는다.
				_lastClickedUnit = null;
				SelectNearbySameType(clickedUnit, currentFloor, addHeld);
				return;
			}

			if (addHeld)
			{
				// Ctrl+클릭: 이미 선택돼 있으면 선택 해제, 아니면 추가
				if (!selectedUnits.Remove(clickedUnit))
					selectedUnits.Add(clickedUnit);
			}
			else
			{
				selectedUnits.Clear();
				selectedUnits.Add(clickedUnit);
			}

			LogHelper.Log(LogHelper.GAME, $"선택: {clickedUnit.unitType.typeName} (총 {selectedUnits.Count}기)");
			return;
		}

		// 2. 공격 처리 (선택된 유닛이 있을 때만, 기존 단일 선택 로직을 다중 선택으로 확장)
		if (selectedUnits.Count > 0)
		{
			foreach (var unit in _gameSession.units)
			{
				if (unit == null || unit.hp <= 0) continue;
				if (unit.currentFloor != currentFloor) continue;
				if (selectedUnits.Contains(unit)) continue;

				if (IsPointInFootprint(gridPos, unit))
				{
					bool anyAttacked = false;

					foreach (var selUnit in selectedUnits)
					{
						if (selUnit == null || selUnit.hp <= 0) continue;

						bool isEnemy =
							(selUnit is Monster && unit is Human) ||
							(selUnit is Human && unit is Monster);

						if (isEnemy)
						{
							selUnit.playerAttackTarget = unit;
							selUnit.playerMoveTarget = null;
							anyAttacked = true;
						}
					}

					if (anyAttacked)
					{
						LogHelper.Log(LogHelper.GAME,
							$"공격 명령: {selectedUnits.Count}기 -> {unit.unitType.typeName}"
						);
						return;
					}
				}
			}
		}

		// 3. 허공 클릭 → 선택 해제 (Ctrl 중이면 기존 선택 유지)
		if (!addHeld)
			selectedUnits.Clear();
	}

	// =====================================================
	// 드래그 박스 선택 (진영 제한 없음 — 인류/몬스터 둘 다 드래그로 선택하고 조종할 수 있다)
	// =====================================================
	private void DoBoxSelect(Vector2 startScreenPos, Vector2 endScreenPos, Vector3 floorOffset, int currentFloor, bool addHeld)
	{
		Vector3 worldA = ScreenToWorldPoint(startScreenPos) - floorOffset;
		Vector3 worldB = ScreenToWorldPoint(endScreenPos) - floorOffset;

		float minX = Mathf.Min(worldA.x, worldB.x);
		float maxX = Mathf.Max(worldA.x, worldB.x);
		float minY = Mathf.Min(worldA.y, worldB.y);
		float maxY = Mathf.Max(worldA.y, worldB.y);

		var boxed = new List<Unit>();

		foreach (var u in _gameSession.units)
		{
			if (u == null || u.hp <= 0) continue;
			if (u.currentFloor != currentFloor) continue;

			float cx = u.position.x + u.unitType.footprint.x / 2f;
			float cy = u.position.y + u.unitType.footprint.y / 2f;

			if (cx >= minX && cx <= maxX && cy >= minY && cy <= maxY)
				boxed.Add(u);
		}

		if (addHeld)
		{
			foreach (var u in boxed)
				if (!selectedUnits.Contains(u)) selectedUnits.Add(u);
		}
		else
		{
			selectedUnits.Clear();
			selectedUnits.AddRange(boxed);
		}

		if (selectedUnits.Count > 0)
			LogHelper.Log(LogHelper.GAME, $"드래그 선택: {selectedUnits.Count}기");
	}

	// =====================================================
	// 더블클릭 선택 (스타크래프트식: 같은 유형 유닛을 근방에서 한 번에 선택)
	// =====================================================
	private void SelectNearbySameType(Unit origin, int currentFloor, bool addHeld)
	{
		var nearby = new List<Unit>();
		float radiusSq = SameTypeNearbyRadius * SameTypeNearbyRadius;

		foreach (var u in _gameSession.units)
		{
			if (u == null || u.hp <= 0) continue;
			if (u.currentFloor != currentFloor) continue;
			// UnitType은 ScriptableObject 에셋 공유가 아니라 스폰마다 new Knight() 식으로 새로 만들어지는
			// 순수 C# 인스턴스라(GameSession.cs 스폰 코드 참고) 참조 비교(==)로는 "같은 유형"을 못 잡는다
			// (자기 자신 말고는 전부 다른 인스턴스라 항상 실패) — typeName 문자열로 비교해야 한다.
			if (u.unitType.typeName != origin.unitType.typeName) continue;

			float dx = (u.position.x + u.unitType.footprint.x / 2f) - (origin.position.x + origin.unitType.footprint.x / 2f);
			float dy = (u.position.y + u.unitType.footprint.y / 2f) - (origin.position.y + origin.unitType.footprint.y / 2f);

			if (dx * dx + dy * dy <= radiusSq)
				nearby.Add(u);
		}

		if (addHeld)
		{
			foreach (var u in nearby)
				if (!selectedUnits.Contains(u)) selectedUnits.Add(u);
		}
		else
		{
			selectedUnits.Clear();
			selectedUnits.AddRange(nearby);
		}

		LogHelper.Log(LogHelper.GAME, $"더블클릭 선택: {origin.unitType.typeName} 근방 {selectedUnits.Count}기");
	}

	// =====================================================
	// 드래그 박스 및 임시 UI 시각화
	// =====================================================
	void OnGUI()
	{
		if (_dragBoxActive)
		{
			Rect r = GetGUIRect(_dragStartScreenPos, _dragCurrentScreenPos);
			Color prevColor = GUI.color;

			GUI.color = new Color(0.3f, 1f, 0.3f, 0.15f);
			GUI.DrawTexture(r, Texture2D.whiteTexture);

			GUI.color = new Color(0.3f, 1f, 0.3f, 0.9f);
			DrawRectBorder(r, 2f);

			GUI.color = prevColor;
		}

		// [임시 UI] 방 기준 왼쪽 위에 인구수 및 최대 인원 텍스트 렌더링
		if (_gameSession != null && _gameSession.roomGrid != null)
		{
			GUIStyle style = new GUIStyle();
			style.fontSize = 20; // 텍스트를 크게
			style.fontStyle = FontStyle.Bold;
			style.alignment = TextAnchor.UpperLeft; // 왼쪽 위 정렬

			HashSet<Room> drawnRooms = new HashSet<Room>();

			foreach (var kvp in _gameSession.roomGrid)
			{
				Room room = kvp.Value;
				if (room == null || drawnRooms.Contains(room)) continue;
				drawnRooms.Add(room);

				// 방 생성 시 계산해 둔 TopLeftWorldPos (최소 X, 최대 Y)
				Vector3 screenPos = Camera.main.WorldToScreenPoint(room.TopLeftWorldPos);

				// 카메라 앞에 있을 때만 렌더링
				if (screenPos.z > 0)
				{
					// Screen.height에서 빼주어 OnGUI 좌표계로 변환
					float guiY = Screen.height - screenPos.y;
					Rect labelRect = new Rect(screenPos.x, guiY, 200, 40);

					// 인구수 표시 텍스트
					style.normal.textColor = room.CurrentPopulation > room.MaxPopulation ? Color.red : Color.green;
					GUI.Label(labelRect, $"인구수: {room.CurrentPopulation} / {room.MaxPopulation}", style);
				}
			}
		}
	}

	// Mouse.current.position은 좌하단 원점, OnGUI는 좌상단 원점이라 Y를 뒤집어야 한다.
	private Rect GetGUIRect(Vector2 a, Vector2 b)
	{
		float xMin = Mathf.Min(a.x, b.x);
		float xMax = Mathf.Max(a.x, b.x);
		float yMin = Screen.height - Mathf.Max(a.y, b.y);
		float yMax = Screen.height - Mathf.Min(a.y, b.y);

		return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
	}

	private void DrawRectBorder(Rect r, float thickness)
	{
		GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, thickness), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(r.xMin, r.yMin, thickness, r.height), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), Texture2D.whiteTexture);
	}
}
