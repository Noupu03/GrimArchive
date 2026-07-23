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
	private BuildingManager _buildingManager;
	private ResourceManager _resourceManager;

	// === 7단계: 빌드 모드 (고스트 프리팹) 상태 ===
	public bool isBuildMode = false;
	private GameObject ghostPrefab;
	private SpriteRenderer ghostRenderer;
	private ProductionRule currentBuildRule;
	private Sprite currentBuildSprite;

	// === 오브젝트(O)/함정(P)/몬스터(M) 배치 모드 — 빌드 모드와 같은 고스트 방식(2026-07-22/23, 사용자 요청) ===
	public bool isObjectPlaceMode = false;
	public bool isTrapPlaceMode = false;
	public bool isMonsterPlaceMode = false;
	private GameObject placeGhost;
	private SpriteRenderer placeGhostRenderer;

	[Inject]
	public void Construct(UnitGenerate unitGenerate, GameSession gameSession, BuildingManager buildingManager, ResourceManager resourceManager)
	{
		_unitGenerate = unitGenerate;
		_gameSession = gameSession;
		_buildingManager = buildingManager;
		_resourceManager = resourceManager;
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
		// 7단계: 빌드 모드 단축키 (임시 B키)
		// =====================================================
		if (Keyboard.current.bKey.wasPressedThisFrame)
		{
			EnterBuildMode();
		}

		if (isBuildMode)
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
		if (Keyboard.current.mKey.wasPressedThisFrame) EnterMonsterPlaceMode();

		if (isObjectPlaceMode || isTrapPlaceMode || isMonsterPlaceMode)
		{
			UpdatePlaceMode(floorOffset, currentFloor);
			return; // 배치 모드 중에는 유닛 선택 로직 스킵
		}

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
				unit.isManualMoveCommand = true;
				unit.playerAttackTarget = null;
			}

			LogHelper.Log(LogHelper.GAME,
				$"일반 이동 명령: {selectedUnits.Count}기 -> ({gridPos.x}, {gridPos.y})"
			);
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

		// 8단계: 건물 클릭 시 생산 명령 하달 및 원자적 자원 차감
		BuildingData bData = _buildingManager.GetBuildingAt(gridPos);
		if (bData != null)
		{
			if (!bData.IsProducing)
			{
				if (_resourceManager.TryConsumeResources(bData.Rule.costs))
				{
					bData.IsProducing = true;
					bData.ProductionProgress = 0f;
					LogHelper.Log(LogHelper.GAME, $"Started production at {gridPos} for {bData.Rule.targetUnitTypeName}");
				}
			}
			else
			{
				LogHelper.Log(LogHelper.GAME, $"Already producing at {gridPos}. Progress: {bData.ProductionProgress:F1}/{bData.Rule.productionTime:F1}");
			}
			return; // 건물 클릭 시 다른 유닛/허공 선택 로직 무시
		}

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
	// 빌드 모드 (고스트 프리팹 설치)
	// =====================================================
	private void EnterBuildMode()
	{
		if (isBuildMode) return;
		ExitPlaceMode(); // 오브젝트/함정 배치 모드와 동시에 켜지지 않게 한다
		isBuildMode = true;

		// 임시로 더미 생산 규칙 하나 생성
		currentBuildRule = ScriptableObject.CreateInstance<ProductionRule>();
		currentBuildRule.ruleId = "B_001";
		currentBuildRule.displayName = "Test Barracks";
		currentBuildRule.costs.Add(new ResourceCost { resourceType = ResourceType.Wood, amount = 20 });
		currentBuildRule.targetUnitTypeName = "Knight";

		// 건축물 전용 아트 스프라이트 배정(사용자 요청, 2026-07-23) — 이전엔 벽 타일(Tile_StoneWall)을
		// 임시로 재사용했다.
		currentBuildSprite = Resources.Load<Sprite>("obj/building");

		if (ghostPrefab == null)
		{
			ghostPrefab = new GameObject("GhostBuilding");
			ghostRenderer = ghostPrefab.AddComponent<SpriteRenderer>();
			ghostRenderer.sprite = currentBuildSprite;
			ghostRenderer.sortingOrder = 10;
		}
		ghostPrefab.SetActive(true);
		LogHelper.Log(LogHelper.GAME, "Entered Build Mode (Cost: 20 Wood)");
	}

	private void ExitBuildMode()
	{
		isBuildMode = false;
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

		// 8단계: 자원 원자적 차감 (TryConsumeResources)
		if (_resourceManager.TryConsumeResources(currentBuildRule.costs))
		{
			// 6단계: 설치 렌더링 및 등록
			_buildingManager.InstallBuilding(gridPos, currentBuildRule, currentBuildSprite);
			ExitBuildMode();
		}
	}

	// =====================================================
	// 오브젝트(O)/함정(P)/몬스터(M) 배치 모드 — 빌드 모드와 동일한 고스트 방식(2026-07-22/23, 사용자
	// 요청). 셋 다 고스트 하나를 공유하고, 모양/색만 종류에 따라 바꿔 쓴다.
	// =====================================================
	private void EnterObjectPlaceMode()
	{
		if (isObjectPlaceMode) return;
		ExitBuildMode();
		isTrapPlaceMode = false;
		isMonsterPlaceMode = false;
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
		isMonsterPlaceMode = false;
		isTrapPlaceMode = true;

		EnsurePlaceGhost();
		// 함정 아트 스프라이트 배정(사용자 요청, 2026-07-23) — 이전엔 세모 폴백 스프라이트를 썼다.
		placeGhostRenderer.sprite = Resources.Load<Sprite>("obj/trap");
		LogHelper.Log(LogHelper.GAME, $"함정 배치 모드 진입 (돌 {ResourceManager.TrapPlaceStoneCost}개 소모, 좌클릭: 생성, 우클릭: 취소)");
	}

	private void EnterMonsterPlaceMode()
	{
		if (isMonsterPlaceMode) return;
		ExitBuildMode();
		isObjectPlaceMode = false;
		isTrapPlaceMode = false;
		isMonsterPlaceMode = true;

		EnsurePlaceGhost();
		// 몬스터는 동그라미 스프라이트로 표시(함정의 세모와 구분) — UnitGenerate의 인류 폴백 원형
		// 생성 로직 재사용.
		placeGhostRenderer.sprite = _unitGenerate != null ? _unitGenerate.CreateCircleSprite(Color.white) : null;
		LogHelper.Log(LogHelper.GAME, $"몬스터 배치 모드 진입 (나무 {ResourceManager.MonsterPlaceWoodCost}개 소모, 좌클릭: 생성, 우클릭: 취소)");
	}

	private void ExitPlaceMode()
	{
		isObjectPlaceMode = false;
		isTrapPlaceMode = false;
		isMonsterPlaceMode = false;
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
				else if (isMonsterPlaceMode)
				{
					if (_resourceManager != null && _resourceManager.TryConsumeResource(ResourceType.Wood, ResourceManager.MonsterPlaceWoodCost))
					{
						_gameSession.SpawnPlayerMonsterAt(new Vector2Int(gridPos.x, gridPos.y), currentFloor);
						ExitPlaceMode();
					}
					else
					{
						LogHelper.Warning(LogHelper.GAME, $"나무가 부족하여 몬스터를 배치할 수 없습니다. (필요: {ResourceManager.MonsterPlaceWoodCost})");
					}
				}
			}
		}
	}
}
