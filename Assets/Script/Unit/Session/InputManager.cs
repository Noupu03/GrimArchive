using System;
using UnityEngine;
using System.Collections.Generic;
using VContainer;
using Haare.Util.Logger;

// 입력 라우팅 + 유닛 선택/이동/공격의 핵심 로직을 담당한다. 배치 모드 2종(빌드/오브젝트·함정·문
// 재설치)은 각자 별도 컨트롤러로 분리돼 있고, 이 클래스는 그 둘의 진입/배타 처리를 조율하며 일반
// 선택, 우클릭 이동/공격, 게임 속도 제어만 담당한다.
public class InputManager : MonoBehaviour
{
	// 하위 호환용 — 실제 저장소는 _selectedUnits이고, 이 프로퍼티는 그 목록의 첫 번째 유닛을 가리킨다.
	public Unit selectedUnit
	{
		get => _selectedUnits.Count > 0 ? _selectedUnits[0] : null;
		set
		{
			_selectedUnits.Clear();
			if (value != null) _selectedUnits.Add(value);
		}
	}

	// 외부(UI/UnitGenerate)는 항상 읽기만 하고, 실제 추가/제거/비우기는 이 클래스 안에서만 일어난다.
	private readonly List<Unit> _selectedUnits = new List<Unit>();
	public IReadOnlyList<Unit> selectedUnits => _selectedUnits;
	// 매 프레임 호출되는 UnitGenerate의 선택 표시 갱신이 IReadOnlyList 너머로 LINQ Contains(박싱)를 타지 않게 하는 헬퍼.
	public bool IsUnitSelected(Unit u) => _selectedUnits.Contains(u);
	public Action OnSelectionChanged;

	// 우클릭 이동/공격은 별도 토글 없이 기본으로 항상 가능하다("명령 취소"/"집결 및 정지" 토글이
	// 켜져 있을 때만 우클릭의 의미가 각각 바뀐다, 아래 Update() 참고).

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
	// DoClickSelect(건물 클릭 시 조작 패널 표시)에서 직접 쓴다.
	private BuildingManager _buildingManager;

	// 배치 모드 2종 컨트롤러 — Construct()에서 직접 생성해 소유(DI 싱글턴 아님). 서로 다른 배치
	// 모드끼리의 배타 진입은 이 클래스의 Update()가 조율한다.
	private BuildPlacementController _buildPlacement;
	private ObjectPlacementController _objectPlacement;

	[Inject]
	public void Construct(UnitGenerate unitGenerate, GameSession gameSession, BuildingManager buildingManager, ResourceManager resourceManager)
	{
		_unitGenerate = unitGenerate;
		_gameSession = gameSession;
		_buildingManager = buildingManager;

		_buildPlacement = new BuildPlacementController(buildingManager, resourceManager);
		_objectPlacement = new ObjectPlacementController(gameSession, unitGenerate, resourceManager, buildingManager);
	}

	// =====================================================
	// BottomMenuBar가 호출하는 공개 API — 예전 키보드 단축키는 제거되고 이 버튼 호출만 남았다.
	// =====================================================
	public bool IsBuildPlacementActive => _buildPlacement != null && _buildPlacement.IsActive;
	public bool IsObjectPlacementActive => _objectPlacement != null && _objectPlacement.IsActive;

	// 개별 서브모드 단위 상태 — BottomMenuBar가 서브메뉴 버튼을 각각 따로 하이라이트하고 재클릭 토글을 판단하는 데 쓴다.
	public bool IsUnitBuildModeActive => _buildPlacement != null && _buildPlacement.IsUnitBuildModeActive;
	public bool IsResourceBuildModeActive => _buildPlacement != null && _buildPlacement.IsResourceBuildModeActive;
	public bool IsObjectOnlyPlacementActive => _objectPlacement != null && _objectPlacement.IsObjectModeActive;
	public bool IsTrapPlacementActive => _objectPlacement != null && _objectPlacement.IsTrapModeActive;
	public bool IsDoorRepairPlacementActive => _objectPlacement != null && _objectPlacement.IsDoorRepairModeActive;
	// 2026-08-24 debug 전용 — 바닥 타일을 벽으로 전환하는 모드 / 함정 무제한 설치 토글.
	public bool IsWallConvertPlacementActive => _objectPlacement != null && _objectPlacement.IsWallConvertModeActive;
	// 더미 건물 배치 모드(debug 전용) — displayName으로 어느 더미 건물 버튼인지 구분한다.
	public bool IsDummyBuildingModeActive(string displayName)
		=> _objectPlacement != null && _objectPlacement.IsDummyBuildingModeActive && _objectPlacement.ActiveDummyBuildingName == displayName;
	public bool DebugUnlimitedTrapPlacement
	{
		get => _objectPlacement != null && _objectPlacement.DebugUnlimitedTrapPlacement;
		set { if (_objectPlacement != null) _objectPlacement.DebugUnlimitedTrapPlacement = value; }
	}
	// "모든 유닛 선택 가능" debug 토글 — IsSelectableUnit의 진영/안개 제한을 우회해 선택/정보열람만
	// 허용한다. 공격/이동 등 실행 계열 명령은 이 토글과 무관하게 기존 진영 규칙을 따른다.
	public bool DebugSelectAllUnits { get; set; }

	// BottomMenuBar가 "다른 메뉴로 전환"/"같은 서브 버튼 재클릭" 시점에 호출하는 단일 취소 진입점 —
	// 취소 notice도 이 한 곳에서만 띄워 중복 문구를 막는다.
	public void ExitActivePlacementMode()
	{
		string label = GetActivePlacementModeLabel();
		if (label == null) return;

		_buildPlacement?.ExitMode();
		_objectPlacement?.ExitMode();
		NoticeCenter.Instance?.PushMomentary($"{label} 배치 모드 취소", NoticeCenter.InfoColor);
	}

	private string GetActivePlacementModeLabel()
	{
		if (IsUnitBuildModeActive) return "유닛 생산 건물";
		if (IsResourceBuildModeActive) return "자원 생산 건물";
		if (IsTrapPlacementActive) return "함정";
		if (IsObjectOnlyPlacementActive) return "오브젝트";
		if (IsDoorRepairPlacementActive) return "문 재설치";
		if (IsWallConvertPlacementActive) return "벽 변환";
		if (_objectPlacement != null && _objectPlacement.IsDummyBuildingModeActive) return _objectPlacement.ActiveDummyBuildingName;
		return null;
	}

	// "명령" 상위 메뉴를 닫거나 다른 메뉴로 전환할 때 명령 관련 토글 3종을 함께 끄는 진입점.
	public void CancelCommandModeIfActive()
	{
		if (IsCancelCommandModeActive)
		{
			SetCancelCommandModeActive(false);
			NoticeCenter.Instance?.PushMomentary("명령 취소 모드 꺼짐.", NoticeCenter.InfoColor);
		}
		if (IsRallyHaltModeActive)
		{
			SetRallyHaltModeActive(false);
			NoticeCenter.Instance?.PushMomentary("집결 및 정지 모드 꺼짐.", NoticeCenter.InfoColor);
		}
		if (IsStandGroundModeActive)
		{
			SetStandGroundModeActive(false);
			NoticeCenter.Instance?.PushMomentary("제자리 공격 모드 꺼짐.", NoticeCenter.InfoColor);
		}
	}

	// "명령 취소" 모드 — 토글을 켠 뒤 유닛을 선택하고 우클릭하면 선택된 유닛들의 명령만 즉시 취소된다.
	public bool IsCancelCommandModeActive { get; private set; }

	public void SetCancelCommandModeActive(bool active)
	{
		IsCancelCommandModeActive = active;
		if (active) { IsRallyHaltModeActive = false; IsStandGroundModeActive = false; }
	}

	// "집결 및 정지" 모드 — 켠 뒤 이동 명령을 내리면 도착 시 isHalted가 켜져 "정지" 상태로 강제 고정한다.
	public bool IsRallyHaltModeActive { get; private set; }

	public void SetRallyHaltModeActive(bool active)
	{
		IsRallyHaltModeActive = active;
		if (active) { IsCancelCommandModeActive = false; IsStandGroundModeActive = false; }
	}

	// "제자리 공격" 모드 — "집결 및 정지"와 동일한 이동 파이프라인을 재사용하되, 도착 후 isHalted 대신 isStandGroundAttack을 켠다.
	public bool IsStandGroundModeActive { get; private set; }

	public void SetStandGroundModeActive(bool active)
	{
		IsStandGroundModeActive = active;
		if (active) { IsCancelCommandModeActive = false; IsRallyHaltModeActive = false; }
	}

	private void CancelSelectedUnitsCommands()
	{
		int count = 0;
		foreach (var u in selectedUnits)
		{
			if (u == null) continue;
			if (!u.HasActivePlayerCommand()) continue;

			u.ClearPlayerCommand();
			count++;
		}

		LogHelper.Log(LogHelper.GAME, $"명령 취소: 선택된 유닛 중 {count}기의 이동/공격 명령을 취소했습니다.");
		NoticeCenter.Instance?.PushMomentary($"선택한 유닛의 명령을 취소했습니다. ({count}기)", NoticeCenter.InfoColor);
	}

	public void EnterUnitBuildMode() { _objectPlacement.ExitMode(); _buildPlacement.EnterBuildMode(); }
	public void EnterResourceBuildMode() { _objectPlacement.ExitMode(); _buildPlacement.EnterResourceBuildMode(); }
	public void EnterObjectPlacementMode() { _buildPlacement.ExitMode(); _objectPlacement.EnterObjectMode(); }
	public void EnterTrapPlacementMode() { _buildPlacement.ExitMode(); _objectPlacement.EnterTrapMode(); }
	public void EnterDoorRepairPlacementMode() { _buildPlacement.ExitMode(); _objectPlacement.EnterDoorRepairMode(); }
	// 2026-08-24 debug 전용.
	public void EnterWallConvertPlacementMode() { _buildPlacement.ExitMode(); _objectPlacement.EnterWallConvertMode(); }
	// 2026-08-25 debug 전용 — resourcePath는 Resources.Load 경로, displayName은 표시 이름.
	public void EnterDummyBuildingPlacementMode(string resourcePath, string displayName) { _buildPlacement.ExitMode(); _objectPlacement.EnterDummyBuildingMode(resourcePath, displayName); }

	private bool IsPointInFootprint(Vector3Int pos, Unit u)
	{
		return u != null && u.ContainsPos(pos.x, pos.y);
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

	// 선택/정보열람 가능 여부(플레이어 유닛만, 안개 속은 불가) — 클릭/드래그박스/더블클릭 선택이 이 한 곳을 거친다.
	private bool IsSelectableUnit(Unit u)
	{
		if (u == null || u.Health.hp <= 0) return false;
		if (DebugSelectAllUnits) return true;
		if (!u.IsPlayerMonsterFaction) return false;
		if (IsPositionHiddenByFog(u.position, u.currentFloor)) return false;
		return true;
	}

	// Room.FogRevealed 기반 판정 — 어느 방에도 속하지 않는 칸은 roomGrid에 없으므로 조회 실패도 안개로 취급한다.
	private bool IsPositionHiddenByFog(Vector2Int pos, int floor)
	{
		if (_gameSession == null || _gameSession.roomGrid == null) return true;
		if (!_gameSession.roomGrid.TryGetValue(new Vector3Int(pos.x, pos.y, floor), out Room room) || room == null)
			return true;
		return !room.FogRevealed;
	}

	void Update()
	{
		if (_gameSession == null) return;
		if (!GameInputScheme.IsReady) return;

		// Unity는 실제 파괴를 프레임 끝에 처리하므로, 사망 후 Destroy된 유닛이 선택 목록에 남아 있으면
		// 소비처가 MissingReferenceException을 던질 수 있어 매 프레임 시작 시점에 선제적으로 정리한다.
		if (_selectedUnits.RemoveAll(u => u == null) > 0)
			OnSelectionChanged?.Invoke();

		int currentFloor = 1;
		Vector3 floorOffset =
			_unitGenerate != null
			? _unitGenerate.GetFloorOffset(currentFloor)
			: Vector3.zero;

		// 배치 모드 진입 단축키는 제거했다(BottomMenuBar 버튼이 동일 기능 제공). Ctrl은 "선택 추가" 모디파이어로 남긴다.
		bool addHeld = GameInputScheme.SelectAddHeld;

		if (_buildPlacement.IsActive)
		{
			_buildPlacement.Update(floorOffset, currentFloor);
			return; // 빌드 모드 중에는 유닛 선택 로직 스킵
		}

		if (_objectPlacement.IsActive)
		{
			_objectPlacement.Update(floorOffset, currentFloor);
			return; // 배치 모드 중에는 유닛 선택 로직 스킵
		}

		// 선택(클릭/드래그 박스/더블클릭/Ctrl+추가)은 명령 모드와 무관하게 항상 가능하다 — 막히는 건 실제 명령 발동뿐이다.
		UpdateSelectionDragAndClick(floorOffset, currentFloor, addHeld);

		bool rightClickOverUI = (BottomMenuBar.Instance != null && BottomMenuBar.Instance.IsMouseOverUI())
			|| (DebugInfoPanel.Instance != null && DebugInfoPanel.Instance.IsMouseOverUI());
		bool rightClickPressed = GameInputScheme.SecondaryDown && selectedUnits.Count > 0 && !rightClickOverUI;

		// =====================================================
		// 우클릭 (명령 취소) - "명령 취소" 토글이 켜져 있을 때는 우클릭이 명령 취소로 동작한다.
		// 각 모드 토글이 서로 배타로 관리되므로 이 분기와 아래 이동 분기가 동시에 걸릴 일은 없다.
		// =====================================================
		if (IsCancelCommandModeActive && rightClickPressed)
		{
			CancelSelectedUnitsCommands();
		}

		// =====================================================
		// 우클릭 (공격 / 이동) - 선택된 유닛 전원에게 명령, 별도 토글 없이 기본으로 항상 동작한다.
		// 좌클릭=선택 전용, 우클릭=실행 전용 원칙에 따라 클릭 위치에 공격 가능한 대상이 있으면 공격을,
		// 없으면 이동을 ExecuteRightClickCommand로 위임한다.
		// =====================================================
		else if (rightClickPressed)
		{
			ExecuteRightClickCommand(floorOffset, currentFloor, markHaltOnArrival: IsRallyHaltModeActive, markStandGroundOnArrival: IsStandGroundModeActive);
		}

		HandleGameSpeedShortcuts();
	}

	// 우클릭 실행 디스패처 — 클릭 위치를 보고 적 유닛 공격 → 코어/문 공격 → 이동 순서로 판단해
	// 실행한다. 이동 예약 토글이 켜져 있어도 공격 가능한 대상이 있으면 공격이 우선한다.
	private void ExecuteRightClickCommand(Vector3 floorOffset, int currentFloor, bool markHaltOnArrival, bool markStandGroundOnArrival)
	{
		Vector2 mousePos = GameInputScheme.PointerScreenPos;
		Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);

		// 1. 적 유닛 공격
		Unit targetUnit = FindUnitAtGridPos(gridPos, currentFloor);
		if (targetUnit != null && targetUnit.Health.hp > 0)
		{
			bool anyAttacked = false;
			foreach (var selUnit in selectedUnits)
			{
				if (selUnit == null || selUnit.Health.hp <= 0) continue;

				bool isEnemy =
					(selUnit is Monster && targetUnit is Human) ||
					(selUnit is Human && targetUnit is Monster);
				if (!isEnemy) continue;

				selUnit.SetAttackCommand(targetUnit);
				anyAttacked = true;
			}

			if (anyAttacked)
			{
				LogHelper.Log(LogHelper.GAME, $"공격 명령: {selectedUnits.Count}기 -> {targetUnit.unitType.typeName}");
				return;
			}
		}

		// 2. 오브젝트(코어/문) 공격 — 유닛 공격 대상을 못 찾았을 때만 확인한다.
		if (_gameSession.objectGrid.ContainsKey(gridPos))
		{
			bool anyIssued = false;
			foreach (var selUnit in selectedUnits)
			{
				if (selUnit == null || selUnit.Health.hp <= 0) continue;
				if (!PlayerCommandFSMState.IsPendingObjectAttackValid(selUnit, gridPos)) continue;

				// SetObjectAttackCommand가 내부적으로 isManualMoveCommand를 켜서 방 경계·문 타일 제한을
				// 우회시킨다 — 없으면 2*2 통로에서 자기 진영 문 타일조차 walkable에서 제외돼 멈춰버린다.
				selUnit.SetObjectAttackCommand(gridPos);
				anyIssued = true;
			}

			if (anyIssued)
			{
				LogHelper.Log(LogHelper.GAME, $"오브젝트 공격 명령: {selectedUnits.Count}기 -> {gridPos}");
				return;
			}
		}

		// 3. 공격 대상이 없으면 기존 이동 명령.
		IssueMoveCommand(floorOffset, currentFloor, markHaltOnArrival, markStandGroundOnArrival);
	}

	// "이동 및 공격"/"집결 및 정지"/"제자리 공격"이 공유하는 우클릭 이동 명령 발동부 — markHaltOnArrival/
	// markStandGroundOnArrival이 true면 도착 후 각각 isHalted/isStandGroundAttack을 켜서 강제 고정한다.
	private void IssueMoveCommand(Vector3 floorOffset, int currentFloor, bool markHaltOnArrival, bool markStandGroundOnArrival)
	{
		Vector2 mousePos = GameInputScheme.PointerScreenPos;
		Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);

		// 목적지 방의 잔여 인구수를 먼저 확인, 초과하면 선택된 유닛 전체의 이동 명령을 취소한다
		// (일부만 자동 이동시키는 기능은 없음). 인구수는 플레이어 진영 몬스터만 포함.
		_gameSession.roomGrid.TryGetValue(new Vector3Int(gridPos.x, gridPos.y, currentFloor), out Room destRoom);
		int incomingPopulation = 0;
		if (destRoom != null)
		{
			var selectedSet = new HashSet<Unit>(selectedUnits);
			foreach (var u in selectedUnits)
				if (u != null && u.Health.hp > 0 && u.IsPlayerMonsterFaction && u.currentRoom != destRoom)
					incomingPopulation += u.populationCost;

			// Room.CurrentPopulation은 "이미 도착한" 유닛만 세므로, 빠르게 연달아 이동시키면 도착 전
			// 다음 명령이 정원 검사를 통과할 수 있다 — 이동 중인 다른 유닛의 인구수도 미리 반영한다.
			foreach (var u in _gameSession.units)
			{
				if (u == null || selectedSet.Contains(u) || u.Health.hp <= 0 || !u.IsPlayerMonsterFaction) continue;
				if (u.currentRoom == destRoom || !u.isManualMoveCommand || !u.playerMoveTarget.HasValue) continue;
				Vector2Int pendingTarget = u.playerMoveTarget.Value;
				if (_gameSession.roomGrid.TryGetValue(new Vector3Int(pendingTarget.x, pendingTarget.y, u.currentFloor), out Room pendingRoom)
					&& pendingRoom == destRoom)
				{
					incomingPopulation += u.populationCost;
				}
			}
		}
		bool populationOk = destRoom == null || destRoom.CurrentPopulation + incomingPopulation <= destRoom.MaxPopulation;

		if (!populationOk)
		{
			LogHelper.Warning(LogHelper.GAME,
				$"목적지 방({destRoom.RoomName}) 인구수 초과로 이동 명령을 취소합니다. " +
				$"(현재 {destRoom.CurrentPopulation} + 이동 {incomingPopulation} > 최대 {destRoom.MaxPopulation})");
			NoticeCenter.Instance?.PushMomentary(
				$"{destRoom.RoomName} 인구수 초과로 이동 명령을 취소합니다. ({destRoom.CurrentPopulation}+{incomingPopulation}/{destRoom.MaxPopulation})",
				NoticeCenter.WarningColor);
			return;
		}

		int issuedCount = 0;
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

			// 이동 명령 도달성은 점령 여부가 아닌 "도달 가능 여부" 기준이다 — 통행 가능한 문만 거쳐
			// 도달 가능한 방이면 전부 허용한다. 인류 명령은 테스트용이라 제한 없음.
			if (unit.IsPlayerMonsterFaction
				&& !_gameSession.CanFactionReachRoom(FactionType.Player, currentFloor, unit.currentRoom?.RoomId ?? -1, destRoom?.RoomId ?? -1))
			{
				continue;
			}

			// "정지"(동상)/"제자리 공격" 해제 — 새 직접 명령은 두 강제 상태 모두를 풀 수 있는 예외다.
			unit.SetMoveCommand(new Vector2Int(gridPos.x, gridPos.y), markHaltOnArrival, markStandGroundOnArrival);
			issuedCount++;
		}

		string commandLabel = markHaltOnArrival ? "집결 및 정지" : markStandGroundOnArrival ? "제자리 공격" : "일반 이동";
		LogHelper.Log(LogHelper.GAME, $"{commandLabel} 명령: {issuedCount}기 -> ({gridPos.x}, {gridPos.y})");
	}

	// =====================================================
	// 속도 / 일시정지 — 명령 모드 게이팅과 무관하게 항상 동작해야 해서 별도 메서드로 뺐다.
	// =====================================================
	private void HandleGameSpeedShortcuts()
	{
		if (GameInputScheme.PausePressedThisFrame)
		{
			_gameSession.isPaused = !_gameSession.isPaused;
			Time.timeScale = _gameSession.isPaused
				? 0.0001f
				: _gameSession.currentGameSpeed;
		}

		// ESC는 space바와 동일하게 정지 상태로 만들고 설정 패널을 띄운다 — 일시정지/재개 처리는 GameSettingsPanel이 직접 들고 있다.
		if (GameInputScheme.EscapePressedThisFrame)
		{
			GameSettingsPanel.Instance?.Toggle();
		}

		if (GameInputScheme.Speed05PressedThisFrame)
		{
			_gameSession.currentGameSpeed = 0.5f;
			if (!_gameSession.isPaused) Time.timeScale = 0.5f;
		}

		if (GameInputScheme.Speed10PressedThisFrame)
		{
			_gameSession.currentGameSpeed = 1f;
			if (!_gameSession.isPaused) Time.timeScale = 1f;
		}

		if (GameInputScheme.Speed15PressedThisFrame)
		{
			_gameSession.currentGameSpeed = 1.5f;
			if (!_gameSession.isPaused) Time.timeScale = 1.5f;
		}

		if (GameInputScheme.Speed20PressedThisFrame)
		{
			_gameSession.currentGameSpeed = 2f;
			if (!_gameSession.isPaused) Time.timeScale = 2f;
		}
	}

	// =====================================================
	// 좌클릭 드래그 박스/클릭 선택 (더블클릭, 드래그, 컨트롤+클릭 모두 지원)
	// =====================================================
	private void UpdateSelectionDragAndClick(Vector3 floorOffset, int currentFloor, bool addHeld)
	{
		// =====================================================
		// 좌클릭 - 드래그 시작
		// =====================================================
		if (GameInputScheme.PrimaryDown)
		{
			// UI 위에서 누른 클릭은 월드 선택으로 취급하지 않는다. BuildingControlPanel은 OnGUI(IMGUI)라 IsPointerOverGameObject()로 안 잡혀서 별도로 확인한다.
			bool overUI = GUIMouseUtil.IsPointerOverAnyPanel()
				|| (BuildingControlPanel.Instance != null && BuildingControlPanel.Instance.IsMouseOverPanel());

			if (!overUI)
			{
				_isMouseDown = true;
				_dragBoxActive = false;
				_dragStartScreenPos = GameInputScheme.PointerScreenPos;
				_dragCurrentScreenPos = _dragStartScreenPos;
			}
		}

		// =====================================================
		// 좌클릭 - 드래그 중 (박스 갱신)
		// =====================================================
		if (_isMouseDown && GameInputScheme.PrimaryHeld)
		{
			_dragCurrentScreenPos = GameInputScheme.PointerScreenPos;

			if (!_dragBoxActive &&
				Vector2.Distance(_dragCurrentScreenPos, _dragStartScreenPos) >= DragThresholdPixels)
			{
				_dragBoxActive = true;
			}
		}

		// =====================================================
		// 좌클릭 - 뗌 (드래그였으면 박스 선택, 아니면 기존 클릭 선택/공격)
		// =====================================================
		if (_isMouseDown && GameInputScheme.PrimaryUp)
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
	// 클릭 선택 (드래그 없이 뗀 경우) — 좌클릭은 오직 선택만 담당한다. 실행 계열 명령은 전부 우클릭으로 옮겼다.
	// =====================================================
	private void DoClickSelect(Vector2 screenPos, Vector3 floorOffset, int currentFloor, bool addHeld)
	{
		Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(screenPos, floorOffset, currentFloor);

		// 건물 클릭 시 즉시 생산하는 대신 조작 UI를 띄운다. 실제 생산 큐잉/자원 차감은 BuildingControlPanel에서 처리.
		BuildingData bData = _buildingManager.GetBuildingAt(gridPos);
		if (bData != null)
		{
			BuildingControlPanel.Instance?.ShowForBuilding(bData);
			return; // 건물 클릭 시 다른 유닛/허공 선택 로직 무시
		}

		// 건물이 아닌 다른 곳(유닛/허공)을 클릭하면 열려 있던 건물 패널은 닫는다.
		BuildingControlPanel.Instance?.ClosePanel();

		// 유닛 클릭이면 선택 처리 — 플레이어 유닛만 선택/정보열람 가능하다. 다른 진영 유닛이나 안개에
		// 가려진 유닛을 클릭하면 아무것도 없었던 것처럼 오브젝트 → 허공 클릭 순으로 계속 판정한다.
		Unit clickedUnit = FindUnitAtGridPos(gridPos, currentFloor);
		if (clickedUnit != null && !IsSelectableUnit(clickedUnit))
			clickedUnit = null;

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
				if (!_selectedUnits.Remove(clickedUnit))
					_selectedUnits.Add(clickedUnit);
			}
			else
			{
				_selectedUnits.Clear();
				_selectedUnits.Add(clickedUnit);
			}

			LogHelper.Log(LogHelper.GAME, $"선택: {clickedUnit.unitType.typeName} (총 {selectedUnits.Count}기)");
			return;
		}

		// 오브젝트(코어/문/함정/전리품/시체/전멸흔적) 클릭 시 정보 패널 표시 — 유닛도 건물도 아닌
		// 위치에 오브젝트가 있을 때만 확인한다. 안개에 가려진 오브젝트는 허공 클릭 처리로 넘어간다.
		if (_gameSession.objectGrid.TryGetValue(gridPos, out InteractableObject clickedObj)
			&& !IsPositionHiddenByFog(new Vector2Int(gridPos.x, gridPos.y), currentFloor))
		{
			BuildingControlPanel.Instance?.ShowForObject(clickedObj);
			return;
		}

		// 허공 클릭 → 선택 해제 (Ctrl 중이면 기존 선택 유지)
		if (!addHeld)
			_selectedUnits.Clear();
	}

	// =====================================================
	// 드래그 박스 선택 — 플레이어 유닛만 선택 가능(IsSelectableUnit, 진영+안개 필터).
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
			if (!IsSelectableUnit(u)) continue;

			float fw = u.unitType != null ? u.unitType.footprint.x : 1f;
			float fh = u.unitType != null ? u.unitType.footprint.y : 1f;
			float cx = u.position.x + fw / 2f;
			float cy = u.position.y + fh / 2f;

			if (cx >= minX && cx <= maxX && cy >= minY && cy <= maxY)
				boxed.Add(u);
		}

		MergeIntoSelection(boxed, addHeld);

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
			if (!IsSelectableUnit(u)) continue;
			// UnitType은 스폰마다 새로 만들어지는 순수 C# 인스턴스라 참조 비교로는 "같은 유형"을 못
			// 잡는다 — typeName 문자열로 비교해야 한다.
			if (u.unitType.typeName != origin.unitType.typeName) continue;

			float dx = (u.position.x + u.unitType.footprint.x / 2f) - (origin.position.x + origin.unitType.footprint.x / 2f);
			float dy = (u.position.y + u.unitType.footprint.y / 2f) - (origin.position.y + origin.unitType.footprint.y / 2f);

			if (dx * dx + dy * dy <= radiusSq)
				nearby.Add(u);
		}

		MergeIntoSelection(nearby, addHeld);

		LogHelper.Log(LogHelper.GAME, $"더블클릭 선택: {origin.unitType.typeName} 근방 {selectedUnits.Count}기");
	}

	// DoBoxSelect/SelectNearbySameType이 공유하는 "후보 목록을 선택에 반영" 절차 — DoClickSelect의
	// 단일 클릭 분기는 addHeld일 때 토글 의미가 달라 합치지 않는다.
	private void MergeIntoSelection(List<Unit> candidates, bool addHeld)
	{
		if (addHeld)
		{
			foreach (var u in candidates)
				if (!_selectedUnits.Contains(u)) _selectedUnits.Add(u);
		}
		else
		{
			_selectedUnits.Clear();
			_selectedUnits.AddRange(candidates);
		}
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
			NoticeCenter.DrawRectBorder(r, 2f);

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
}
