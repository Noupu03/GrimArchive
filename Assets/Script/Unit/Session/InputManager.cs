using System;
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
	private BuildingManager _buildingManager;
	private ResourceManager _resourceManager;
	private UnitSpriteManager _unitSpriteManager;

	// === 건축물·자원·유닛 생산 MVP(2026-07-27) — 빌드 모드(고스트 프리팹) 상태. B키=유닛 생산 건물,
	// V키(신규)=자원 생산 건물. 같은 고스트/스프라이트를 공유하고 플래그로만 구분한다. ===
	public bool isBuildMode = false;
	public bool isResourceBuildMode = false;
	private GameObject ghostPrefab;
	private SpriteRenderer ghostRenderer;
	private List<ProductionRule> currentProductionRules;
	private Sprite currentBuildSprite;

	// === 오브젝트(O)/함정(P) 배치 모드 — 빌드 모드와 같은 고스트 방식(2026-07-22/23, 사용자 요청) ===
	public bool isObjectPlaceMode = false;
	public bool isTrapPlaceMode = false;
	// 03문서 7-3장(2026-07-27 신규) 테스트용 — O/P/M과 동일한 고스트 배치 관례.
	public bool isCorePlaceMode = false;
	private GameObject placeGhost;
	private SpriteRenderer placeGhostRenderer;

	// =====================================================
	// 플레이어 몬스터 배치 모드(2026-08-19 신규, "몬스터 배치 프리셋 프로그래머 지시서") — R키.
	// 웨이브 대기 중(HumanWaveManager.WaveState.Idle)에만 진입 가능. 시간을 멈추고, 점령한 방을
	// 단위로 선택 → 몬스터 선택/타일 선택 두 서브모드를 오가며 몬스터별 디펜스 시작 위치를 지정한다.
	// 실제 이동은 이 모드 안에서 일어나지 않는다(GameSession.ApplyMonsterDefenseStartPositions가
	// 0층 인류 사전 스폰 시점에 대신 처리).
	// =====================================================
	public bool isMonsterPlacementMode = false;

	private enum PlacementSubMode { MonsterSelect, TileSelect }
	private PlacementSubMode _placementSubMode = PlacementSubMode.MonsterSelect;
	private Room _placementSelectedRoom;
	private readonly List<Unit> _placementQueue = new List<Unit>();
	private bool _placementWasPausedBefore;
	private bool _placementHasLastPaintedTile;
	private Vector3Int _placementLastPaintedTile;

	// 타일 선택 서브모드 중 흐릿하게 만든 유닛 스프라이트의 원래 알파값 — 서브모드를 벗어나거나
	// 모드를 종료할 때 정확히 복원하기 위해 보관한다.
	private readonly Dictionary<SpriteRenderer, float> _placementDimmedUnitAlphas = new Dictionary<SpriteRenderer, float>();

	private readonly Dictionary<string, Sprite> _placementIconCache = new Dictionary<string, Sprite>();

	// 방 강조 방식(2026-08-19 재수정, 사용자 피드백 "방선택을 색상 변경 말고 다른 방법으로 하자. 색상은
	// 이미 점령 여부에서 사용하고 있잖아") — 방 타일 색(MapRandering.ChangeRoomColor, 점령 표시 전용
	// 슬롯)은 전혀 건드리지 않고, 그 위에 겹치지 않는 별도의 화면 테두리 선으로만 강조한다. 선택된
	// 몬스터는 이미 존재하는 유닛 선택 표시(UnitGenerate.EnsureSelectionMarker, 발밑 링)가 대신하므로
	// 몬스터 선택 서브모드에는 별도 강조가 필요 없다.
	private static readonly Color PlacementEligibleBorderColor = new Color(0.3f, 1f, 0.55f, 0.9f);
	private static readonly Color PlacementSelectedBorderColor = new Color(1f, 0.92f, 0.25f, 1f);
	private const float PlacementEligibleBorderThickness = 3f;
	private const float PlacementSelectedBorderThickness = 4f;
	private const float PlacementDimUnitAlphaFactor = 0.2f; // 타일 선택 중 유닛 스프라이트 알파 배율

	private const float PlacementPanelWidth = 230f;
	private const float PlacementPanelHeight = 280f;
	// 오른쪽 상단에 이미 DebugInfoPanel/StatusInfoPanel이 y10~510 구간을 꽉 채우고 있어(사용자 요청
	// "생성되는 UI들 기존 UI와 겹치지 않는지 체크해줘") 그 아래로 내려서 겹치지 않게 고정한다.
	private const float PlacementPanelTopY = 530f;

	[Inject]
	public void Construct(UnitGenerate unitGenerate, GameSession gameSession, BuildingManager buildingManager, ResourceManager resourceManager, UnitSpriteManager unitSpriteManager)
	{
		_unitGenerate = unitGenerate;
		_gameSession = gameSession;
		_buildingManager = buildingManager;
		_resourceManager = resourceManager;
		_unitSpriteManager = unitSpriteManager;
	}

	private bool IsPointInFootprint(Vector3Int pos, Unit u)
	{
		if (u == null || u.unitType == null) return false;

		int w = (int)u.unitType.footprint.x;
		int h = (int)u.unitType.footprint.y;

		return (pos.x >= u.position.x && pos.x < u.position.x + w &&
				pos.y >= u.position.y && pos.y < u.position.y + h);
	}

	// 몬스터 배치 프리셋(2026-08-19, 사용자 요청 "몬스터 선택 진입 시, 선택된 방 안에서만 몬스터를
	// 선택할 수 있게 해줘. 지금 모든 유닛 다 선택됨") — 배치 모드의 몬스터 선택 서브모드에서는 선택된
	// 방 안에 있는 유닛만 선택 대상으로 허용한다. 배치 모드가 아니거나(일반 플레이) 타일 선택
	// 서브모드거나 아직 방을 안 골랐으면 항상 통과(기존 동작 그대로).
	private bool IsUnitSelectableInPlacementMode(Unit u)
	{
		if (!isMonsterPlacementMode || _placementSubMode != PlacementSubMode.MonsterSelect || _placementSelectedRoom == null)
			return true;

		return _gameSession.roomGrid.TryGetValue(new Vector3Int(u.position.x, u.position.y, u.currentFloor), out Room r)
			&& r == _placementSelectedRoom;
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
		// 플레이어 몬스터 배치 모드(2026-08-19 신규) — R키. 다른 배치 모드(빌드/오브젝트/함정/코어)와
		// 배타적으로 동작한다 — 그쪽 모드 중엔 R을 무시하고, 이 모드 중엔 그쪽 단축키를 모두 막는다.
		// =====================================================
		bool inOtherPlaceMode = isBuildMode || isResourceBuildMode || isObjectPlaceMode || isTrapPlaceMode || isCorePlaceMode;
		if (Keyboard.current.rKey.wasPressedThisFrame && !inOtherPlaceMode)
		{
			ToggleMonsterPlacementMode();
		}

		if (isMonsterPlacementMode)
		{
			bool consumed = UpdateMonsterPlacementMode(floorOffset, currentFloor);
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
			EnterBuildMode();
		}
		if (Keyboard.current.vKey.wasPressedThisFrame)
		{
			EnterResourceBuildMode();
		}

		if (isBuildMode || isResourceBuildMode)
		{
			UpdateBuildMode(floorOffset, currentFloor);
			return; // 빌드 모드 중에는 유닛 선택 로직 스킵
		}

		// =====================================================
		// 오브젝트(O)/함정(P) 배치 모드 — B키(빌드 모드)와 동일한 방식(고스트 스프라이트가 마우스를
		// 따라다니다 좌클릭한 위치에 생성, 우클릭으로 취소)으로 원하는 위치를 직접 골라서 놓는다
		// (사용자 요청, 2026-07-22 — 예전엔 O/P가 GameSession.HandleDebugInput에서 즉시 무작위 위치에
		// 스폰했음).
		// =====================================================
		if (Keyboard.current.oKey.wasPressedThisFrame) EnterObjectPlaceMode();
		if (Keyboard.current.pKey.wasPressedThisFrame) EnterTrapPlaceMode();
		if (Keyboard.current.cKey.wasPressedThisFrame) EnterCorePlaceMode();

		if (isObjectPlaceMode || isTrapPlaceMode || isCorePlaceMode)
		{
			UpdatePlaceMode(floorOffset, currentFloor);
			return; // 배치 모드 중에는 유닛 선택 로직 스킵
		}

		UpdateSelectionDragAndClick(floorOffset, currentFloor, addHeld);

		// =====================================================
		// 우클릭 (이동) - 선택된 유닛 전원에게 명령
		// =====================================================
		if (Mouse.current.rightButton.wasPressedThisFrame && selectedUnits.Count > 0)
		{
			Vector2 mousePos = Mouse.current.position.ReadValue();
			Vector3Int gridPos = ScreenToGridPos(mousePos, floorOffset, currentFloor);

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
				|| (isMonsterPlacementMode && IsMouseOverPlacementPanel());

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
		Vector3Int gridPos = ScreenToGridPos(screenPos, floorOffset, currentFloor);

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

		if (clickedUnit != null && !IsUnitSelectableInPlacementMode(clickedUnit))
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
		Vector3 worldA = ScreenToWorldPoint(startScreenPos) - floorOffset;
		Vector3 worldB = ScreenToWorldPoint(endScreenPos) - floorOffset;

		float minX = Mathf.Min(worldA.x, worldB.x);
		float maxX = Mathf.Max(worldA.x, worldB.x);
		float minY = Mathf.Min(worldA.y, worldB.y);
		float maxY = Mathf.Max(worldA.y, worldB.y);

		var boxed = new List<Unit>();

		foreach (var u in _gameSession.units)
		{
			if (u == null || u.Health.hp <= 0) continue;
			if (u.currentFloor != currentFloor) continue;
			if (!IsUnitSelectableInPlacementMode(u)) continue;

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
			if (!IsUnitSelectableInPlacementMode(u)) continue;
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

		if (isMonsterPlacementMode)
		{
			int currentFloor = 1;
			Vector3 floorOffset = _unitGenerate != null ? _unitGenerate.GetFloorOffset(currentFloor) : Vector3.zero;

			if (_placementSelectedRoom == null)
			{
				DrawEligibleRoomBorders(floorOffset);
			}
			else
			{
				DrawSelectedRoomBorder(floorOffset);
				if (_placementSubMode == PlacementSubMode.TileSelect) DrawTileSelectGrid(floorOffset);
			}

			DrawPlacementSilhouettes(floorOffset, currentFloor);
			DrawMonsterPlacementUI();
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

	// =====================================================
	// 빌드 모드 (고스트 프리팹 설치) — B=유닛 생산 건물, V=자원 생산 건물(건축물·자원·유닛 생산 MVP,
	// 2026-07-27). 두 모드가 고스트 오브젝트/스프라이트를 공유하고 플래그로만 갈린다.
	// =====================================================
	private void EnterBuildMode()
	{
		if (isBuildMode) return;
		ExitPlaceMode(); // 오브젝트/함정 배치 모드와 동시에 켜지지 않게 한다
		isResourceBuildMode = false;
		isBuildMode = true;

		EnsureProductionRules();
		EnsureGhost();
		LogHelper.Log(LogHelper.GAME, $"유닛 생산 건물 배치 모드 진입 (돌 {ResourceManager.UnitBuildingStoneCost} 소모)");
	}

	private void EnterResourceBuildMode()
	{
		if (isResourceBuildMode) return;
		ExitPlaceMode();
		isBuildMode = false;
		isResourceBuildMode = true;

		EnsureGhost();
		LogHelper.Log(LogHelper.GAME, $"자원 생산 건물 배치 모드 진입 (돌 {ResourceManager.ResourceBuildingStoneCost} 소모)");
	}

	// 플레이어(몬스터 진영) 생산 건물이 실제로 뽑을 수 있는 유닛 목록을 준비한다. GameSession의 시작방
	// 자동 배치와 같은 팩토리(ProductionRule.CreateDefaultPlayerUnitRules)를 써서 두 경로가 어긋나지
	// 않게 한다.
	private void EnsureProductionRules()
	{
		if (currentProductionRules != null) return;

		currentProductionRules = ProductionRule.CreateDefaultPlayerUnitRules();
	}

	private void EnsureGhost()
	{
		// 사용자 요청(2026-07-27) — V키(자원 생산 건물)는 별도 스프라이트(obj/resource_building)를 쓴다.
		// B키(유닛 생산 건물)는 기존 obj/building 그대로.
		currentBuildSprite = isResourceBuildMode
			? Resources.Load<Sprite>("obj/resource_building")
			: Resources.Load<Sprite>("obj/building");

		if (ghostPrefab == null)
		{
			ghostPrefab = new GameObject("GhostBuilding");
			ghostRenderer = ghostPrefab.AddComponent<SpriteRenderer>();
			ghostRenderer.sortingOrder = 10;
		}
		ghostRenderer.sprite = currentBuildSprite;
		ghostPrefab.SetActive(true);
	}

	private void ExitBuildMode()
	{
		isBuildMode = false;
		isResourceBuildMode = false;
		if (ghostPrefab != null) ghostPrefab.SetActive(false);
		LogHelper.Log(LogHelper.GAME, "Exited Build Mode");
	}

	private void UpdateBuildMode(Vector3 floorOffset, int currentFloor)
	{
		Vector2 mousePos = Mouse.current.position.ReadValue();
		Vector3Int gridPos = ScreenToGridPos(mousePos, floorOffset, currentFloor);

		// 고스트 프리팹 위치 갱신
		if (ghostPrefab != null)
		{
			ghostPrefab.transform.position = new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, 0f) + floorOffset;

			if (_buildingManager.CanInstallAt(gridPos))
				ghostRenderer.color = new Color(0f, 1f, 0f, 0.5f); // 설치 가능 (녹색 반투명)
			else
				ghostRenderer.color = new Color(1f, 0f, 0f, 0.5f); // 설치 불가 (빨간색 반투명)
		}

		if (Mouse.current.rightButton.wasPressedThisFrame)
		{
			ExitBuildMode();
			return;
		}

		if (Mouse.current.leftButton.wasPressedThisFrame)
		{
			if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
			{
				TryInstallBuilding(gridPos);
			}
		}
	}

	private void TryInstallBuilding(Vector3Int gridPos)
	{
		if (!_buildingManager.CanInstallAt(gridPos))
		{
			LogHelper.Warning(LogHelper.GAME, "장애물이 있거나 설치할 수 없는 지형입니다.");
			return;
		}

		if (isResourceBuildMode)
		{
			if (_resourceManager.TryConsumeResource(ResourceType.Stone, ResourceManager.ResourceBuildingStoneCost))
			{
				_buildingManager.InstallResourceBuilding(gridPos, currentBuildSprite);
				ExitBuildMode();
			}
			else
			{
				LogHelper.Warning(LogHelper.GAME, $"돌이 부족하여 자원 생산 건물을 지을 수 없습니다. (필요: {ResourceManager.ResourceBuildingStoneCost})");
			}
			return;
		}

		if (_resourceManager.TryConsumeResource(ResourceType.Stone, ResourceManager.UnitBuildingStoneCost))
		{
			_buildingManager.InstallProductionBuilding(gridPos, currentProductionRules, currentBuildSprite);
			ExitBuildMode();
		}
		else
		{
			LogHelper.Warning(LogHelper.GAME, $"돌이 부족하여 유닛 생산 건물을 지을 수 없습니다. (필요: {ResourceManager.UnitBuildingStoneCost})");
		}
	}

	// =====================================================
	// 오브젝트(O)/함정(P) 배치 모드 — 빌드 모드와 동일한 고스트 방식(2026-07-22/23, 사용자 요청). 둘 다
	// 고스트 하나를 공유하고, 모양/색만 종류에 따라 바꿔 쓴다. 몬스터(M) 배치 모드는 건축물·자원·유닛
	// 생산 MVP(2026-07-27)로 완전히 대체되어 삭제됨.
	// =====================================================
	private void EnterObjectPlaceMode()
	{
		if (isObjectPlaceMode) return;
		ExitBuildMode();
		isTrapPlaceMode = false;
		isCorePlaceMode = false;
		isObjectPlaceMode = true;

		EnsurePlaceGhost();
		// 코어(루팅 오브젝트) 아트 스프라이트 배정(사용자 요청, 2026-07-23) — 이전엔 벽 타일을 임시로 썼다.
		placeGhostRenderer.sprite = Resources.Load<Sprite>("obj/core");
		LogHelper.Log(LogHelper.GAME, "오브젝트 배치 모드 진입 (좌클릭: 생성, 우클릭: 취소)");
	}

	private void EnterTrapPlaceMode()
	{
		if (isTrapPlaceMode) return;
		ExitBuildMode();
		isObjectPlaceMode = false;
		isCorePlaceMode = false;
		isTrapPlaceMode = true;

		EnsurePlaceGhost();
		// 함정 아트 스프라이트 배정(사용자 요청, 2026-07-23) — 이전엔 세모 폴백 스프라이트를 썼다.
		placeGhostRenderer.sprite = Resources.Load<Sprite>("obj/trap");
		LogHelper.Log(LogHelper.GAME, $"함정 배치 모드 진입 (돌 {ResourceManager.TrapPlaceStoneCost}개 소모, 좌클릭: 생성, 우클릭: 취소)");
	}

	// 03문서 7-3장(2026-07-27 신규) 테스트용 — 자원 소모 없이 즉시 배치(리더 전용 조사 흐름 검증 목적).
	private void EnterCorePlaceMode()
	{
		if (isCorePlaceMode) return;
		ExitBuildMode();
		isObjectPlaceMode = false;
		isTrapPlaceMode = false;
		isCorePlaceMode = true;

		EnsurePlaceGhost();
		placeGhostRenderer.sprite = Resources.Load<Sprite>("obj/core");
		LogHelper.Log(LogHelper.GAME, "코어 배치 모드 진입 (테스트용, 좌클릭: 생성, 우클릭: 취소)");
	}

	private void ExitPlaceMode()
	{
		isObjectPlaceMode = false;
		isTrapPlaceMode = false;
		isCorePlaceMode = false;
		if (placeGhost != null) placeGhost.SetActive(false);
	}

	private void EnsurePlaceGhost()
	{
		if (placeGhost == null)
		{
			placeGhost = new GameObject("PlacementGhost");
			placeGhostRenderer = placeGhost.AddComponent<SpriteRenderer>();
			placeGhostRenderer.sortingOrder = 10;
		}
		placeGhost.SetActive(true);
	}

	private void UpdatePlaceMode(Vector3 floorOffset, int currentFloor)
	{
		Vector2 mousePos = Mouse.current.position.ReadValue();
		Vector3Int gridPos = ScreenToGridPos(mousePos, floorOffset, currentFloor);

		// 이미 다른 오브젝트가 있거나(objectGrid) 벽/유닛으로 막혀있으면(IsAreaClear) 놓을 수 없다.
		bool canPlace = _gameSession != null && _unitGenerate != null
			&& !_gameSession.objectGrid.ContainsKey(gridPos)
			&& _unitGenerate.IsAreaClear(new Vector2Int(gridPos.x, gridPos.y), Vector2.one, currentFloor);

		if (placeGhost != null)
		{
			placeGhost.transform.position = new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, 0f) + floorOffset;
			placeGhostRenderer.color = canPlace
				? new Color(0f, 1f, 0f, 0.5f)  // 배치 가능 (녹색 반투명)
				: new Color(1f, 0f, 0f, 0.5f); // 배치 불가 (빨간색 반투명)
		}

		if (Mouse.current.rightButton.wasPressedThisFrame)
		{
			ExitPlaceMode();
			return;
		}

		if (Mouse.current.leftButton.wasPressedThisFrame && canPlace)
		{
			if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
			{
				if (isObjectPlaceMode)
				{
					_gameSession.SpawnLootObjectAt(gridPos);
					ExitPlaceMode();
				}
				else if (isTrapPlaceMode)
				{
					// 돌 자원이 부족하면 배치를 취소하지 않고 모드를 유지 — 자원을 모은 뒤 같은 위치에
					// 다시 시도할 수 있게 한다(배치 모드 자체는 우클릭으로만 취소).
					if (_resourceManager != null && _resourceManager.TryConsumeResource(ResourceType.Stone, ResourceManager.TrapPlaceStoneCost))
					{
						_gameSession.SpawnTrapAt(gridPos);
						ExitPlaceMode();
					}
					else
					{
						LogHelper.Warning(LogHelper.GAME, $"돌이 부족하여 함정을 배치할 수 없습니다. (필요: {ResourceManager.TrapPlaceStoneCost})");
					}
				}
				else if (isCorePlaceMode)
				{
					_gameSession.SpawnCoreAt(gridPos);
					ExitPlaceMode();
				}
			}
		}
	}

	// =====================================================
	// 플레이어 몬스터 배치 모드(2026-08-19 신규) — 진입/종료
	// =====================================================
	private void ToggleMonsterPlacementMode()
	{
		if (!isMonsterPlacementMode)
		{
			// 웨이브가 시작되기 전(대기 중)에만 사용 가능 — 웨이브를 클리어하고 다음 웨이브를
			// 기다리는 동안(다시 Idle)도 재활성화된다(사용자 요청, 2026-08-19).
			var wm = GrimArchive.Wave.HumanWaveManager.Instance;
			if (wm != null && wm.currentState != GrimArchive.Wave.WaveState.Idle)
			{
				LogHelper.Warning(LogHelper.GAME, "웨이브 진행 중에는 몬스터 배치 모드를 사용할 수 없습니다.");
				return;
			}

			isMonsterPlacementMode = true;
			selectedUnits.Clear();
			_placementSubMode = PlacementSubMode.MonsterSelect;
			_placementSelectedRoom = null;
			_placementQueue.Clear();

			// 게임 시간을 멈춘다(사용자 요청 "게임의 시간이 멈춰") — 스페이스바 일시정지와 동일한
			// 메커니즘 재사용, 기존 일시정지 상태를 기억해뒀다가 종료 시 그대로 복원한다.
			_placementWasPausedBefore = _gameSession.isPaused;
			_gameSession.isPaused = true;
			Time.timeScale = 0.0001f;

			LogHelper.Log(LogHelper.GAME, "플레이어 몬스터 배치 모드 진입 — 초록 테두리로 표시된, 점령한 방을 선택하세요.");
			RefreshPlacementModeNotice();
		}
		else
		{
			ExitMonsterPlacementMode();
		}
	}

	private void ExitMonsterPlacementMode()
	{
		isMonsterPlacementMode = false;

		RestoreAllUnitDim();
		_placementSelectedRoom = null;
		_placementSubMode = PlacementSubMode.MonsterSelect;
		_placementQueue.Clear();
		selectedUnits.Clear();

		// 이 모드는 몬스터들의 행동 상태에 아무런 영향을 주지 않는다(사용자 요청) — 여기서 이동 명령을
		// 내리지 않는다. defenseStartPosition만 남아있고, 실제 이동은 0층 인류 사전 스폰 시점에
		// GameSession.ApplyMonsterDefenseStartPositions가 담당한다.
		_gameSession.isPaused = _placementWasPausedBefore;
		Time.timeScale = _gameSession.isPaused ? 0.0001f : _gameSession.currentGameSpeed;

		// 사용자 요청(2026-08-19) "배치모드 완전히 종료 시에만 사라지게" — 진행 단계별 안내 알림은
		// 배치 모드가 완전히 끝날 때만 지운다(중간 단계 전환에서는 PushPersistent가 알아서 교체).
		NoticeCenter.Instance?.Remove(PlacementNoticeKey);

		LogHelper.Log(LogHelper.GAME, "플레이어 몬스터 배치 모드 종료.");
	}

	private void SelectPlacementRoom(Room room)
	{
		_placementSelectedRoom = room;
		_placementSubMode = PlacementSubMode.MonsterSelect;
		_placementQueue.Clear();
		selectedUnits.Clear();
		RefreshPlacementUnitDim(); // 방 안 몬스터 강조(방 밖 유닛은 흐리게) — 이전 방에서 타일 선택 중이었어도 재계산됨
		RefreshPlacementModeNotice();

		LogHelper.Log(LogHelper.GAME, $"배치 대상 방 선택: {room.RoomName}");
	}

	// 몬스터 선택 서브모드로 전환 — 패널 버튼에서 호출.
	private void SwitchToMonsterSelectSubMode()
	{
		if (_placementSubMode == PlacementSubMode.MonsterSelect) return;
		_placementSubMode = PlacementSubMode.MonsterSelect;
		RefreshPlacementUnitDim();
		RefreshPlacementModeNotice();
	}

	// 배치 모드 진행 단계 안내(2026-08-19 신규, 사용자 요청 "이 세 알림은 현재 무슨 배치를 사용하고
	// 있는가에 따라 지워졌다가 새로 생기고, 배치모드 완전히 종료 시에만 사라지게") — 방 미선택/몬스터
	// 선택/타일 선택 세 단계에 맞는 문구로 같은 key를 계속 갱신한다(NoticeCenter.PushPersistent가
	// 같은 key의 기존 알림을 지우고 새로 넣는다). 자동 만료되지 않으므로 ExitMonsterPlacementMode가
	// 명시적으로 Remove할 때까지 계속 떠 있는다.
	private const string PlacementNoticeKey = "MonsterPlacementMode";

	private void RefreshPlacementModeNotice()
	{
		string text;
		if (_placementSelectedRoom == null)
			text = "배치모드 : 배치모드를 실행할 점령된 방을 선택하세요.";
		else if (_placementSubMode == PlacementSubMode.MonsterSelect)
			text = "배치모드 : 배치를 사용할 유닛들을 선택하고, \"타일 선택\"으로 전환해주세요.";
		else
			text = "배치모드 : 배치할 타일을 선택하고 R키를 눌러 배치모드를 종료하세요.";

		NoticeCenter.Instance?.PushPersistent(PlacementNoticeKey, text, NoticeCenter.InfoColor);
	}

	// =====================================================
	// 배치 모드 유닛 흐림/강조 통합(2026-08-19 수정, 사용자 요청 "몬스터 선택 상태에서 방 안의
	// 몬스터들을 강조 처리해줘. 강조 방식은 자유롭게") — 기존 "타일 선택 중엔 전부 흐리게" 대신,
	// 서브모드별로 다른 대상을 흐리게 해서 나머지가 상대적으로 강조돼 보이게 한다:
	//   몬스터 선택: 선택된 방 밖(=선택 불가능한) 유닛만 흐리게 → 방 안 몬스터가 도드라짐
	//   타일 선택:   전부 흐리게(기존 그대로) → 타일/격자가 도드라짐
	// 상태가 바뀔 때마다(방 선택/서브모드 전환) 이 함수 하나만 부르면 된다 — 항상 먼저 전부
	// 복원한 뒤 현재 상태에 맞는 대상만 다시 흐리게 계산해서 적용한다.
	// =====================================================
	private void RefreshPlacementUnitDim()
	{
		RestoreAllUnitDim();
		if (!isMonsterPlacementMode || _placementSelectedRoom == null) return;

		if (_placementSubMode == PlacementSubMode.TileSelect)
			DimUnitsMatching(_ => true);
		else
			DimUnitsMatching(u => !IsUnitSelectableInPlacementMode(u)); // 선택 가능(=방 안) 유닛만 강조 유지
	}

	private void DimUnitsMatching(System.Func<Unit, bool> shouldDim)
	{
		foreach (var unit in _gameSession.units)
		{
			if (unit == null || unit.currentFloor != 1 || !shouldDim(unit)) continue;

			Transform visualRoot = _unitGenerate?.GetVisualTransform(unit);
			if (visualRoot == null) continue;

			foreach (var sr in visualRoot.GetComponentsInChildren<SpriteRenderer>(true))
			{
				if (sr == null || _placementDimmedUnitAlphas.ContainsKey(sr)) continue;
				_placementDimmedUnitAlphas[sr] = sr.color.a;
				Color c = sr.color;
				c.a *= PlacementDimUnitAlphaFactor;
				sr.color = c;
			}
		}
	}

	// 지금까지 흐리게 만든 유닛 스프라이트를 전부 원래 알파값으로 되돌린다.
	private void RestoreAllUnitDim()
	{
		foreach (var kvp in _placementDimmedUnitAlphas)
		{
			if (kvp.Key == null) continue;
			Color c = kvp.Key.color;
			c.a = kvp.Value;
			kvp.Key.color = c;
		}
		_placementDimmedUnitAlphas.Clear();
	}

	// =====================================================
	// 몬스터 선택 → 타일 선택 전환 시 대기열 구성
	// =====================================================
	private void EnterTileSelectSubMode()
	{
		foreach (var unit in selectedUnits)
		{
			if (unit == null || unit.Health.hp <= 0) continue;
			if (!(unit is Monster) || !unit.IsPlayerMonsterFaction) continue;
			if (_placementQueue.Contains(unit)) continue;

			_placementQueue.Add(unit);
		}

		selectedUnits.Clear();
		_placementSubMode = PlacementSubMode.TileSelect;
		_placementHasLastPaintedTile = false;

		RefreshPlacementUnitDim();
		RefreshPlacementModeNotice();
	}

	// =====================================================
	// 배치 모드 입력 라우팅 — true를 반환하면 이번 프레임 입력을 이미 소비한 것(일반 선택 로직으로
	// 새지 않게 막는다). 몬스터 선택 서브모드일 때만 false를 반환해 기존 좌클릭 선택 로직을 그대로
	// 태운다.
	// =====================================================
	private bool UpdateMonsterPlacementMode(Vector3 floorOffset, int currentFloor)
	{
		// Phase 1: 방 미선택 — 좌클릭으로 점령한 방을 선택한다.
		if (_placementSelectedRoom == null)
		{
			if (Mouse.current.leftButton.wasPressedThisFrame
				&& (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
			{
				Vector2 mousePos = Mouse.current.position.ReadValue();
				Vector3Int gridPos = ScreenToGridPos(mousePos, floorOffset, currentFloor);

				if (_gameSession.roomGrid.TryGetValue(new Vector3Int(gridPos.x, gridPos.y, currentFloor), out Room room)
					&& room.RoomFaction == FactionType.Player)
				{
					SelectPlacementRoom(room);
				}
			}
			return true;
		}

		// 패널(오른쪽 중앙) 위 클릭은 OnGUI 버튼이 직접 처리 — 여기서는 아무 것도 하지 않는다.
		if (IsMouseOverPlacementPanel())
		{
			_placementHasLastPaintedTile = false;
			return true;
		}

		if (_placementSubMode == PlacementSubMode.MonsterSelect)
		{
			return false; // 기존 좌클릭 선택 로직(UpdateSelectionDragAndClick)을 그대로 재사용
		}

		// TileSelect — 좌클릭/드래그로 선택된 방 안 타일에 대기열 앞머리 유닛을 순서대로 배치한다.
		HandleTilePlacementInput(floorOffset, currentFloor);
		return true;
	}

	private void HandleTilePlacementInput(Vector3 floorOffset, int currentFloor)
	{
		if (!Mouse.current.leftButton.isPressed)
		{
			_placementHasLastPaintedTile = false;
			return;
		}

		Vector2 mousePos = Mouse.current.position.ReadValue();
		Vector3Int gridPos = ScreenToGridPos(mousePos, floorOffset, currentFloor);

		// 드래그로 같은 칸을 여러 프레임 지나가도 한 번만 소모한다.
		if (_placementHasLastPaintedTile && gridPos == _placementLastPaintedTile) return;

		_placementLastPaintedTile = gridPos;
		_placementHasLastPaintedTile = true;

		TryPlaceQueuedUnitAt(gridPos, currentFloor);
	}

	// 스택상 앞에 있는(=대기열 맨 앞) 유닛부터 순서대로 배치한다(사용자 요청 "동일 종류 유닛들끼리
	// 순서대로 배치함").
	private void TryPlaceQueuedUnitAt(Vector3Int gridPos, int currentFloor)
	{
		if (_placementSelectedRoom == null) return;

		if (!_gameSession.roomGrid.TryGetValue(new Vector3Int(gridPos.x, gridPos.y, currentFloor), out Room clickedRoom)
			|| clickedRoom != _placementSelectedRoom)
		{
			return;
		}

		// 사용자 요청(2026-08-19) "배치후 다시 클릭해서 지울 수 있게 해줘" — 이미 이 타일에 배치된
		// 유닛이 있으면 배치를 취소하고 대기열 맨 앞으로 되돌린다(바로 다시 배치할 수 있게).
		Unit placed = FindUnitPlacedAt(gridPos, currentFloor);
		if (placed != null)
		{
			placed.defenseStartPosition = null;
			if (!_placementQueue.Contains(placed)) _placementQueue.Insert(0, placed);

			LogHelper.Log(LogHelper.GAME,
				$"배치 취소: {placed.unitType?.typeName} ({gridPos.x},{gridPos.y}) — 대기열로 복귀 (대기열 {_placementQueue.Count}기)");
			return;
		}

		if (_placementQueue.Count == 0) return;

		// 사용자 요청(2026-08-19) "유닛이 지나갈 수 있는 건물이나 벽 등의 타일도 선택 가능하니까 이거
		// 막아줘" — 벽·건물(둘 다 Tile.isStructureExist로 통일 관리됨, UnitFunction.CanMove와 동일
		// 기준)이 있는 타일은 배치 대상에서 제외한다. 취소(위 분기)는 이미 지정된 위치를 지우는 것뿐
		// 이라 이 검사 대상이 아니다.
		if (_gameSession.cmap == null || !_gameSession.cmap.IsStaticTileWalkable(currentFloor, new Vector2Int(gridPos.x, gridPos.y)))
		{
			return;
		}

		Unit unit = _placementQueue[0];
		_placementQueue.RemoveAt(0);
		if (unit == null || unit.Health.hp <= 0) return;

		unit.defenseStartPosition = new Vector2Int(gridPos.x, gridPos.y);

		LogHelper.Log(LogHelper.GAME,
			$"배치 지정: {unit.unitType?.typeName} -> ({gridPos.x},{gridPos.y}) (남은 대기열 {_placementQueue.Count}기)");
	}

	private Unit FindUnitPlacedAt(Vector3Int gridPos, int currentFloor)
	{
		foreach (var unit in _gameSession.units)
		{
			if (unit == null || unit.Health.hp <= 0) continue;
			if (unit.currentFloor != currentFloor) continue;
			if (!unit.IsPlayerMonsterFaction || !unit.defenseStartPosition.HasValue) continue;

			Vector2Int p = unit.defenseStartPosition.Value;
			if (p.x == gridPos.x && p.y == gridPos.y) return unit;
		}
		return null;
	}

	// =====================================================
	// 배치 모드 UI(오른쪽 중앙) — 몬스터 선택/타일 선택 버튼 + 남은 배치 대기열 목록
	// =====================================================
	private Rect GetPlacementPanelRect()
		=> new Rect(Screen.width - PlacementPanelWidth - 16f, PlacementPanelTopY, PlacementPanelWidth, PlacementPanelHeight);

	private bool IsMouseOverPlacementPanel()
	{
		if (_placementSelectedRoom == null || Mouse.current == null) return false;
		Vector2 mp = Mouse.current.position.ReadValue();
		Vector2 guiPos = new Vector2(mp.x, Screen.height - mp.y);
		return GetPlacementPanelRect().Contains(guiPos);
	}

	private void DrawMonsterPlacementUI()
	{
		// 방 미선택 안내 문구는 NoticeCenter가 담당한다(모드 진입 시 ToggleMonsterPlacementMode에서
		// 1회 Push, 2026-08-19 "이 문구들을 시스템화" 요청) — 여기서는 방이 선택된 뒤의 패널만 그린다.
		if (_placementSelectedRoom == null) return;

		GUILayout.BeginArea(GetPlacementPanelRect(), GUI.skin.box);
		GUILayout.Label($"<b>[ 몬스터 배치: {_placementSelectedRoom.RoomName} ]</b>");
		GUILayout.Space(4);

		GUILayout.BeginHorizontal();
		string monsterLabel = _placementSubMode == PlacementSubMode.MonsterSelect ? "[몬스터 선택]" : "몬스터 선택";
		string tileLabel = _placementSubMode == PlacementSubMode.TileSelect ? "[타일 선택]" : "타일 선택";
		if (GUILayout.Button(monsterLabel)) SwitchToMonsterSelectSubMode();
		if (GUILayout.Button(tileLabel) && _placementSubMode != PlacementSubMode.TileSelect) EnterTileSelectSubMode();
		GUILayout.EndHorizontal();

		GUILayout.Space(6);

		if (_placementSubMode == PlacementSubMode.MonsterSelect)
		{
			GUILayout.Label($"선택된 몬스터: {selectedUnits.Count}기");
			GUILayout.Label("더블클릭/드래그/Ctrl+클릭으로\n배치할 몬스터를 고르세요.");
		}
		else
		{
			GUILayout.Label("남은 배치 대기열:");
			foreach (var group in GetPlacementQueueGroups())
				GUILayout.Label($"{group.typeName} X{group.count}");
			if (_placementQueue.Count == 0)
				GUILayout.Label("(없음)");

			GUILayout.Space(6);
			GUILayout.Label("방 안 타일을 클릭/드래그해\n순서대로 배치하세요.");
		}

		GUILayout.EndArea();
	}

	private struct PlacementQueueGroup { public string typeName; public int count; }

	private List<PlacementQueueGroup> GetPlacementQueueGroups()
	{
		var groups = new List<PlacementQueueGroup>();
		foreach (var unit in _placementQueue)
		{
			if (unit == null || unit.unitType == null) continue;
			string name = unit.unitType.typeName;

			int idx = groups.FindIndex(g => g.typeName == name);
			if (idx >= 0)
			{
				var g = groups[idx];
				g.count++;
				groups[idx] = g;
			}
			else
			{
				groups.Add(new PlacementQueueGroup { typeName = name, count = 1 });
			}
		}
		return groups;
	}

	// =====================================================
	// 배치된 유닛 실루엣(사용자 요청 "배치하면 유닛 실루엣이 보임") — 이번 세션에서 새로 지정한
	// 것뿐 아니라, defenseStartPosition이 이미 저장돼 있는 모든 플레이어 몬스터를 대상으로 한다 —
	// 배치 모드를 다시 켰을 때 기존 계획도 그대로 보여야 하므로.
	// =====================================================
	private void DrawPlacementSilhouettes(Vector3 floorOffset, int currentFloor)
	{
		if (Camera.main == null) return;

		foreach (var unit in _gameSession.units)
		{
			if (unit == null || unit.Health.hp <= 0) continue;
			if (unit.currentFloor != currentFloor) continue;
			if (!unit.IsPlayerMonsterFaction || !unit.defenseStartPosition.HasValue) continue;

			Sprite icon = GetPlacementUnitIcon(unit.unitType?.typeName);
			if (icon == null) continue;

			Vector2Int pos = unit.defenseStartPosition.Value;
			Vector2 footprint = unit.unitType != null && unit.unitType.footprint.x > 0f && unit.unitType.footprint.y > 0f
				? unit.unitType.footprint
				: Vector2.one;

			// 사용자 신고(2026-08-19) "실루엣이 카메라 스케일 기반인것 같은데, 월드 기반으로 바꿔줘 —
			// 카메라 배율 달라지면 배율 이상하게 변한다" — 고정 픽셀 크기 대신 유닛 풋프린트(월드
			// 단위) 두 모서리를 각각 투영해서 화면 Rect를 만든다. 이러면 카메라 줌에 맞춰 실제 타일
			// 크기와 동일하게 자연스럽게 스케일된다.
			float aspect = icon.rect.width > 0f ? icon.rect.height / icon.rect.width : 1f;
			float worldWidth = footprint.x;
			float worldHeight = worldWidth * aspect;

			Vector3 worldCenter = new Vector3(pos.x + footprint.x * 0.5f, pos.y + footprint.y * 0.5f, 0f) + floorOffset;
			Vector3 worldBottomLeft = worldCenter - new Vector3(worldWidth * 0.5f, worldHeight * 0.5f, 0f);
			Vector3 worldTopRight   = worldCenter + new Vector3(worldWidth * 0.5f, worldHeight * 0.5f, 0f);

			Rect r = WorldRectToGUIRect(worldBottomLeft, worldTopRight);
			if (r.width <= 0f || r.height <= 0f) continue;

			Color prev = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, 0.5f); // 반투명 실루엣
			DrawSpriteOnGUI(icon, r);
			GUI.color = prev;
		}
	}

	// 월드 공간 사각형(대각선 두 점)을 화면(OnGUI) 좌표계 Rect로 투영한다 — WorldToScreenPoint 기반이라
	// 카메라 줌/배율이 바뀌어도 실제 타일 크기에 맞춰 자연스럽게 스케일된다.
	private static Rect WorldRectToGUIRect(Vector3 worldBottomLeft, Vector3 worldTopRight)
	{
		Vector3 screenBL = Camera.main.WorldToScreenPoint(worldBottomLeft);
		Vector3 screenTR = Camera.main.WorldToScreenPoint(worldTopRight);
		if (screenBL.z <= 0 || screenTR.z <= 0) return default;

		float guiX = screenBL.x;
		float guiYTop = Screen.height - screenTR.y;
		float width = screenTR.x - screenBL.x;
		float height = screenTR.y - screenBL.y;

		return new Rect(guiX, guiYTop, width, height);
	}

	// =====================================================
	// 방 강조(2026-08-19 재수정, 사용자 피드백 "방선택을 색상 변경 말고 다른 방법으로 하자. 색상은
	// 이미 점령 여부에서 사용하고 있잖아") — 방 타일 자체의 색(점령 표시 전용)은 전혀 건드리지 않고,
	// 그 위에 화면 테두리 선만 그려서 강조한다. Phase 1(방 미선택)에서는 선택 가능한(점령한) 방 전부에
	// 테두리를, Phase 2(방 선택 후)에서는 선택된 방 하나에만 더 굵은 테두리를 그린다.
	// =====================================================
	private void DrawEligibleRoomBorders(Vector3 floorOffset)
	{
		if (_gameSession.allRooms == null) return;

		Color prev = GUI.color;
		GUI.color = PlacementEligibleBorderColor;

		foreach (var room in _gameSession.allRooms)
		{
			if (room == null || room.Floor != 1 || room.RoomFaction != FactionType.Player) continue;
			DrawRoomBorder(room, floorOffset, PlacementEligibleBorderThickness);
		}

		GUI.color = prev;
	}

	private void DrawSelectedRoomBorder(Vector3 floorOffset)
	{
		if (_placementSelectedRoom == null) return;

		Color prev = GUI.color;
		GUI.color = PlacementSelectedBorderColor;
		DrawRoomBorder(_placementSelectedRoom, floorOffset, PlacementSelectedBorderThickness);
		GUI.color = prev;
	}

	private void DrawRoomBorder(Room room, Vector3 floorOffset, float thicknessPx)
	{
		RectInt b = room.Bounds;
		Vector3 bl = new Vector3(b.xMin, b.yMin, 0f) + floorOffset;
		Vector3 br = new Vector3(b.xMax, b.yMin, 0f) + floorOffset;
		Vector3 tl = new Vector3(b.xMin, b.yMax, 0f) + floorOffset;
		Vector3 tr = new Vector3(b.xMax, b.yMax, 0f) + floorOffset;

		DrawWorldLineOnGUI(bl, br, thicknessPx);
		DrawWorldLineOnGUI(tl, tr, thicknessPx);
		DrawWorldLineOnGUI(bl, tl, thicknessPx);
		DrawWorldLineOnGUI(br, tr, thicknessPx);
	}

	// =====================================================
	// 타일 선택 서브모드 격자 강조(사용자 요청 "타일 선택 모드에서 타일의 격자 더 잘보이도록
	// 강조해줘") — 선택된 방 범위 안의 타일 경계선을 또렷한 색으로 그린다. 카메라가 회전하지 않는
	// (고정 탑다운) 전제로, 월드 공간의 수직/수평선을 화면 좌표로 그대로 투영한다.
	// =====================================================
	private void DrawTileSelectGrid(Vector3 floorOffset)
	{
		if (Camera.main == null || _placementSelectedRoom == null) return;

		RectInt b = _placementSelectedRoom.Bounds;
		Color prev = GUI.color;
		GUI.color = new Color(1f, 1f, 0.4f, 0.9f);

		for (int x = b.xMin; x <= b.xMax; x++)
		{
			DrawWorldLineOnGUI(new Vector3(x, b.yMin, 0f) + floorOffset, new Vector3(x, b.yMax, 0f) + floorOffset, 2f);
		}
		for (int y = b.yMin; y <= b.yMax; y++)
		{
			DrawWorldLineOnGUI(new Vector3(b.xMin, y, 0f) + floorOffset, new Vector3(b.xMax, y, 0f) + floorOffset, 2f);
		}

		GUI.color = prev;
	}

	private static void DrawWorldLineOnGUI(Vector3 worldA, Vector3 worldB, float thicknessPx)
	{
		Vector3 sA = Camera.main.WorldToScreenPoint(worldA);
		Vector3 sB = Camera.main.WorldToScreenPoint(worldB);
		if (sA.z <= 0 || sB.z <= 0) return;

		float ax = sA.x, ay = Screen.height - sA.y;
		float bx = sB.x, by = Screen.height - sB.y;

		if (Mathf.Approximately(ax, bx))
		{
			float top = Mathf.Min(ay, by);
			GUI.DrawTexture(new Rect(ax - thicknessPx * 0.5f, top, thicknessPx, Mathf.Abs(by - ay)), Texture2D.whiteTexture);
		}
		else
		{
			float left = Mathf.Min(ax, bx);
			GUI.DrawTexture(new Rect(left, ay - thicknessPx * 0.5f, Mathf.Abs(bx - ax), thicknessPx), Texture2D.whiteTexture);
		}
	}

	private Sprite GetPlacementUnitIcon(string unitTypeName)
	{
		if (string.IsNullOrEmpty(unitTypeName)) return null;
		if (_placementIconCache.TryGetValue(unitTypeName, out var cached)) return cached;

		Sprite icon = null;
		GameObject prefab = _unitSpriteManager?.GetPrefab(unitTypeName);
		Transform visual = prefab != null ? prefab.transform.Find("Visual") : null;
		SpriteRenderer sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
		if (sr != null) icon = sr.sprite;

		_placementIconCache[unitTypeName] = icon;
		return icon;
	}

	// 스프라이트 시트에서 서브스프라이트 하나를 지정한 화면 Rect에 맞춰 그린다(WaveGaugePanel.
	// DrawSprite와 동일한 기법).
	private static void DrawSpriteOnGUI(Sprite sprite, Rect screenRect)
	{
		Texture2D tex = sprite.texture;
		Rect r = sprite.rect;
		Rect uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
		GUI.DrawTextureWithTexCoords(screenRect, tex, uv);
	}
}
