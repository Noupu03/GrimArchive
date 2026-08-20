using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using VContainer;
using Haare.Util.Logger;

// 입력 라우팅 + 유닛 선택/이동/공격의 핵심 로직을 담당한다. 배치 모드 3종(빌드/오브젝트·함정·코어/
// 몬스터)은 각자 별도 컨트롤러(BuildPlacementController/ObjectPlacementController/
// MonsterPlacementController, 전부 이 폴더)로 분리돼 있다(2026-08-20, InputManager 비대화 방지) —
// 이 클래스는 그 세 컨트롤러의 진입/배타 처리를 조율(Update()/OnGUI() 위임)하고, 일반 선택(드래그
// 박스/클릭/더블클릭)과 우클릭 이동/공격, 게임 속도 제어라는 "입력 관리자 본연의 책임"만 갖는다.
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
	public Action OnSelectionChanged;

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
	// DoClickSelect(건물 클릭 시 조작 패널 표시)에서 직접 쓴다 — BuildPlacementController도 별도로
	// 자기 몫의 BuildingManager 참조를 갖는다(같은 싱글턴, 서로 다른 책임의 두 소비처).
	private BuildingManager _buildingManager;

	// 배치 모드 3종 컨트롤러(2026-08-20 분리) — 전부 이 Construct()에서 InputManager가 직접 생성해
	// 소유한다(VContainer 등록 대상 아님 — InputManager별 상태를 갖는 협력 객체라 DI 싱글턴으로 둘
	// 이유가 없다). 서로 다른 배치 모드끼리의 배타 진입(예: 빌드 모드 진입 시 오브젝트 배치 모드 종료)은
	// 컨트롤러끼리 직접 참조하지 않고 이 클래스의 Update()가 조율한다(순환 참조 방지).
	private BuildPlacementController _buildPlacement;
	private ObjectPlacementController _objectPlacement;
	private MonsterPlacementController _monsterPlacement;

	[Inject]
	public void Construct(UnitGenerate unitGenerate, GameSession gameSession, BuildingManager buildingManager, ResourceManager resourceManager, UnitSpriteManager unitSpriteManager)
	{
		_unitGenerate = unitGenerate;
		_gameSession = gameSession;
		_buildingManager = buildingManager;

		_buildPlacement = new BuildPlacementController(buildingManager, resourceManager);
		_objectPlacement = new ObjectPlacementController(gameSession, unitGenerate, resourceManager);
		_monsterPlacement = new MonsterPlacementController(gameSession, unitGenerate, unitSpriteManager, selectedUnits);
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
			if (u == null || u.Health.hp <= 0) continue;
			if (u.currentFloor != currentFloor) continue;

			if (IsPointInFootprint(gridPos, u))
				return u;
		}
		return null;
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
		// 플레이어 몬스터 배치 모드(2026-08-19 신규) — R키. 다른 배치 모드(빌드/오브젝트/함정/코어)와
		// 배타적으로 동작한다 — 그쪽 모드 중엔 R을 무시하고, 이 모드 중엔 그쪽 단축키를 모두 막는다.
		// =====================================================
		bool inOtherPlaceMode = _buildPlacement.IsActive || _objectPlacement.IsActive;
		if (Keyboard.current.rKey.wasPressedThisFrame && !inOtherPlaceMode)
		{
			_monsterPlacement.Toggle();
		}

		if (_monsterPlacement.IsActive)
		{
			bool consumed = _monsterPlacement.Update(floorOffset, currentFloor);
			if (!consumed)
			{
				// 몬스터 선택 서브모드 — 기존 좌클릭 드래그/더블클릭/Ctrl+클릭 선택 로직만 재사용하고,
				// 우클릭 이동·빌드/배치 단축키·게임 속도 단축키는 모두 건너뛴다(시간이 멈춰 있어야 함).
				UpdateSelectionDragAndClick(floorOffset, currentFloor, addHeld);
			}
			return;
		}

		// =====================================================
		// 건축물·자원·유닛 생산 MVP(2026-07-27) — 빌드 모드 단축키. B=유닛 생산 건물, V=자원 생산 건물.
		// M키(구 몬스터 즉시 배치)는 이 MVP로 완전히 대체되어 삭제됨.
		// =====================================================
		if (Keyboard.current.bKey.wasPressedThisFrame)
		{
			_objectPlacement.ExitMode(); // 오브젝트/함정/코어 배치 모드와 동시에 켜지지 않게 한다
			_buildPlacement.EnterBuildMode();
		}
		if (Keyboard.current.vKey.wasPressedThisFrame)
		{
			_objectPlacement.ExitMode();
			_buildPlacement.EnterResourceBuildMode();
		}

		if (_buildPlacement.IsActive)
		{
			_buildPlacement.Update(floorOffset, currentFloor);
			return; // 빌드 모드 중에는 유닛 선택 로직 스킵
		}

		// =====================================================
		// 오브젝트(O)/함정(P) 배치 모드 — B키(빌드 모드)와 동일한 방식(고스트 스프라이트가 마우스를
		// 따라다니다 좌클릭한 위치에 생성, 우클릭으로 취소)으로 원하는 위치를 직접 골라서 놓는다
		// (사용자 요청, 2026-07-22 — 예전엔 O/P가 GameSession.HandleDebugInput에서 즉시 무작위 위치에
		// 스폰했음).
		// =====================================================
		if (Keyboard.current.oKey.wasPressedThisFrame) { _buildPlacement.ExitMode(); _objectPlacement.EnterObjectMode(); }
		if (Keyboard.current.pKey.wasPressedThisFrame) { _buildPlacement.ExitMode(); _objectPlacement.EnterTrapMode(); }
		if (Keyboard.current.cKey.wasPressedThisFrame) { _buildPlacement.ExitMode(); _objectPlacement.EnterCoreMode(); }

		if (_objectPlacement.IsActive)
		{
			_objectPlacement.Update(floorOffset, currentFloor);
			return; // 배치 모드 중에는 유닛 선택 로직 스킵
		}

		UpdateSelectionDragAndClick(floorOffset, currentFloor, addHeld);

		// =====================================================
		// 우클릭 (이동) - 선택된 유닛 전원에게 명령
		// =====================================================
		if (Mouse.current.rightButton.wasPressedThisFrame && selectedUnits.Count > 0)
		{
			Vector2 mousePos = Mouse.current.position.ReadValue();
			Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);

			// 유닛 배치 시스템(2026-07-27 신규) 4.1/5.3장: 목적지 방의 잔여 인구수를 먼저 확인한다.
			// 초과하면 선택된 유닛 전체의 이동 명령을 취소한다 — 일부만 자동으로 이동시키는 기능은
			// 제공하지 않는다(문서 5.3장, "전체 이동 명령 취소" + "직접 선택 대상을 조정해 재시도").
			// 클릭 위치가 어느 방에도 속하지 않으면(예: 방 경계 밖) 검사를 건너뛴다. 사용자 요청·정정
			// (2026-07-27): 인구수는 "플레이어 진영 몬스터"만 포함 — 인류와 야생 몬스터는 둘 다 제외.
			_gameSession.roomGrid.TryGetValue(new Vector3Int(gridPos.x, gridPos.y, currentFloor), out Room destRoom);
			int incomingPopulation = 0;
			if (destRoom != null)
			{
				foreach (var u in selectedUnits)
					if (u != null && u.Health.hp > 0 && u.IsPlayerMonsterFaction && u.currentRoom != destRoom)
						incomingPopulation += u.populationCost;
			}
			bool populationOk = destRoom == null || destRoom.CurrentPopulation + incomingPopulation <= destRoom.MaxPopulation;

			if (!populationOk)
			{
				LogHelper.Warning(LogHelper.GAME,
					$"목적지 방({destRoom.RoomName}) 인구수 초과로 이동 명령을 취소합니다. " +
					$"(현재 {destRoom.CurrentPopulation} + 이동 {incomingPopulation} > 최대 {destRoom.MaxPopulation})");
			}
			else
			{
			foreach (var unit in selectedUnits)
			{
				if (unit == null || unit.Health.hp <= 0) continue;

				unit.playerInteractTarget = null;

				if (unit is Human && _gameSession.objectGrid.TryGetValue(gridPos, out InteractableObject obj))
				{
					if (!obj.IsCollected)
					{
						unit.playerInteractTarget = gridPos;
					}
				}

				// 점령 관련(2026-07-27 신규): 플레이어(몬스터 진영) 이동 명령은 자신이 점령한 방과
				// 그 방과 Gate로 연결된 인접 방까지만 허용한다. 인류 명령은 테스트용이므로 이
				// 제한을 받지 않는다(사용자 확인).
				if (unit.IsPlayerMonsterFaction && _gameSession.cmap != null
					&& !_gameSession.cmap.CanPlayerCommandPosition(currentFloor, new Vector2Int(gridPos.x, gridPos.y)))
				{
					continue;
				}

				unit.playerMoveTarget = new Vector2Int(gridPos.x, gridPos.y);
				unit.isManualMoveCommand = true;
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
	// 좌클릭 드래그 박스/클릭 선택 — Update()의 일반 흐름과 몬스터 배치 모드의 "몬스터 선택" 서브모드
	// (2026-08-19 신규)가 공유한다(사용자 요청 "몬스터를 선택하고(더블클릭, 드래그, 컨트롤+클릭, 단일
	// 선택 무관.)" — 기존 선택 로직을 그대로 재사용).
	// =====================================================
	private void UpdateSelectionDragAndClick(Vector3 floorOffset, int currentFloor, bool addHeld)
	{
		// =====================================================
		// 좌클릭 - 드래그 시작
		// =====================================================
		if (Mouse.current.leftButton.wasPressedThisFrame)
		{
			// UI(디버그 패널 등) 위에서 누른 클릭은 월드 선택으로 취급하지 않는다. BuildingControlPanel은
			// OnGUI(IMGUI)라 IsPointerOverGameObject()로 안 잡혀서 별도로 확인한다(사용자 신고, 2026-07-27
			// "유닛 생산 시설 버튼 클릭 시 UI가 닫혀버림"). 몬스터 배치 모드 패널도 동일 사유로 확인.
			bool overUI = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
				|| (BuildingControlPanel.Instance != null && BuildingControlPanel.Instance.IsMouseOverPanel())
				|| (_monsterPlacement.IsActive && _monsterPlacement.IsMouseOverPanel());

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
	}

	// =====================================================
	// 클릭 선택 / 공격 (드래그 없이 뗀 경우)
	// =====================================================
	private void DoClickSelect(Vector2 screenPos, Vector3 floorOffset, int currentFloor, bool addHeld)
	{
		Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(screenPos, floorOffset, currentFloor);

		// 건축물·자원·유닛 생산 MVP(2026-07-27) — 건물 클릭 시 즉시 생산하는 대신 조작 UI를 띄운다
		// (스타크래프트 참고, 사용자 요청). 실제 생산 큐잉/자원 차감은 BuildingControlPanel에서 처리.
		BuildingData bData = _buildingManager.GetBuildingAt(gridPos);
		if (bData != null)
		{
			BuildingControlPanel.Instance?.ShowForBuilding(bData);
			return; // 건물 클릭 시 다른 유닛/허공 선택 로직 무시
		}

		// 건물이 아닌 다른 곳(유닛/허공)을 클릭하면 열려 있던 건물 패널은 닫는다.
		BuildingControlPanel.Instance?.ClosePanel();

		// 1. 유닛 클릭이면 최우선으로 선택 처리 (진영 제한 없음, 기존 동작 유지)
		Unit clickedUnit = FindUnitAtGridPos(gridPos, currentFloor);

		if (clickedUnit != null && !_monsterPlacement.IsUnitSelectable(clickedUnit))
		{
			// 배치 모드 몬스터 선택 서브모드: 선택된 방 밖 유닛 클릭은 완전히 무시(선택도 공격도 안 함).
			return;
		}

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
				if (unit == null || unit.Health.hp <= 0) continue;
				if (unit.currentFloor != currentFloor) continue;
				if (selectedUnits.Contains(unit)) continue;

				if (IsPointInFootprint(gridPos, unit))
				{
					bool anyAttacked = false;

					foreach (var selUnit in selectedUnits)
					{
						if (selUnit == null || selUnit.Health.hp <= 0) continue;

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
		Vector3 worldA = ScreenGridUtil.ScreenToWorldPoint(startScreenPos) - floorOffset;
		Vector3 worldB = ScreenGridUtil.ScreenToWorldPoint(endScreenPos) - floorOffset;

		float minX = Mathf.Min(worldA.x, worldB.x);
		float maxX = Mathf.Max(worldA.x, worldB.x);
		float minY = Mathf.Min(worldA.y, worldB.y);
		float maxY = Mathf.Max(worldA.y, worldB.y);

		var boxed = new List<Unit>();

		foreach (var u in _gameSession.units)
		{
			if (u == null || u.Health.hp <= 0) continue;
			if (u.currentFloor != currentFloor) continue;
			if (!_monsterPlacement.IsUnitSelectable(u)) continue;

			float fw = u.unitType != null ? u.unitType.footprint.x : 1f;
			float fh = u.unitType != null ? u.unitType.footprint.y : 1f;
			float cx = u.position.x + fw / 2f;
			float cy = u.position.y + fh / 2f;

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
			if (u == null || u.Health.hp <= 0) continue;
			if (u.currentFloor != currentFloor) continue;
			if (!_monsterPlacement.IsUnitSelectable(u)) continue;
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
		if (_monsterPlacement == null) return; // Construct() 이전(주입 완료 전) 프레임 방어

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

		if (_monsterPlacement.IsActive)
		{
			_monsterPlacement.DrawGUI();
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
