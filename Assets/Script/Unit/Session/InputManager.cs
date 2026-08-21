using System;
using UnityEngine;
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

	// 2026-08-21, 사용자 요청으로 "이동 및 공격" 토글을 롤백 — 우클릭 이동/공격은 다시 별도 토글 없이
	// 기본으로 항상 가능하다("명령 취소"/"집결 및 정지" 토글이 켜져 있을 때만 우클릭의 의미가 각각
	// 명령 취소/집결 후 정지로 바뀐다, 아래 Update() 우클릭 분기 참고).

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

	// =====================================================
	// BottomMenuBar(2026-08-20 UI 리뉴얼)가 호출하는 공개 API. 원래는 키보드 단축키(R/B/V/O/P/C)와
	// 같은 코드 경로를 공유했으나, 2026-08-21 입력 정리로 그 단축키들은 제거되고 이 버튼 호출만 남았다.
	// =====================================================
	public bool IsBuildPlacementActive => _buildPlacement != null && _buildPlacement.IsActive;
	public bool IsObjectPlacementActive => _objectPlacement != null && _objectPlacement.IsActive;
	public bool IsMonsterPlacementActive => _monsterPlacement != null && _monsterPlacement.IsActive;

	// 개별 서브모드 단위 상태(2026-08-20, 사용자 신고 "자원 생산 건물과 유닛 생산 건물이 다중 선택되어
	// 버리는 UI 버그") — BottomMenuBar가 설치/debug 서브메뉴 버튼을 각각 따로 하이라이트하고, 이미
	// 활성인 버튼을 다시 눌렀을 때만 취소하도록(재클릭 토글) 판단하는 데 쓴다.
	public bool IsUnitBuildModeActive => _buildPlacement != null && _buildPlacement.IsUnitBuildModeActive;
	public bool IsResourceBuildModeActive => _buildPlacement != null && _buildPlacement.IsResourceBuildModeActive;
	public bool IsObjectOnlyPlacementActive => _objectPlacement != null && _objectPlacement.IsObjectModeActive;
	public bool IsTrapPlacementActive => _objectPlacement != null && _objectPlacement.IsTrapModeActive;
	public bool IsCorePlacementActive => _objectPlacement != null && _objectPlacement.IsCoreModeActive;

	// 우클릭 취소를 없앤 대신(2026-08-20, 사용자 요청) BottomMenuBar가 "다른 메뉴로 전환" 또는 "같은
	// 서브 버튼 재클릭" 시점에 호출하는 단일 취소 진입점. 소집 배치(MonsterPlacementController)는
	// 별도의 토글 방식 진입/종료를 그대로 유지하므로 여기서 건드리지 않는다. 취소 notice도 (재클릭이든
	// 메뉴 전환이든) 이 한 곳에서만 띄워서 두 경로가 서로 다른 문구를 중복해서 띄우지 않게 한다
	// (2026-08-20, 사용자 요청 "메뉴를 통한 모드 클릭시 모두 notice로 설명이 뜨게").
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
		if (IsCorePlacementActive) return "코어";
		return null;
	}

	// 소집 배치(몬스터 배치 모드)는 자체 토글(Toggle)로만 열고 닫히므로 위 ExitActivePlacementMode의
	// 대상이 아니다 — BottomMenuBar가 "소집 배치" 버튼이 아닌 다른 상단 버튼을 눌렀을 때 이 모드도
	// 함께 취소하는 데 쓴다(2026-08-20, 사용자 요청 "소집 배치 메뉴도 다른 메뉴랑 중복되지 않게"). 진입/
	// 종료 안내 notice는 MonsterPlacementController가 이미 자체적으로(RefreshPlacementModeNotice) 담당.
	public void ExitMonsterPlacementModeIfActive()
	{
		if (_monsterPlacement != null && _monsterPlacement.IsActive)
			_monsterPlacement.Toggle();
	}

	// "명령" 상위 메뉴를 닫거나 다른 메뉴로 전환할 때 "명령 취소"/"집결 및 정지" 토글도 함께 꺼지도록
	// 하는 진입점(2026-08-20, 사용자 요청 "명령에서 상위 메뉴를 눌러 꺼버리면, 위에서 토글했던것들도
	// 취소되게"). "이동 및 공격"은 2026-08-21 롤백으로 더 이상 토글이 아니라 여기서 다룰 대상이 아니다.
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
	}

	// "명령 취소" 모드(2026-08-20 재설계, 사용자 요청 "명령 취소 로직을 바꿀게. 토글형으로 바꾸고, 해당
	// 유닛들을 선택 후 우클릭을 눌러 즉시 명령 취소되게 하자") — 예전엔 버튼을 누르는 즉시 "모든 유닛"의
	// 명령을 취소했는데(선택 여부 무관), 이제 토글이다: 토글을 켠 뒤 유닛을 선택하고 우클릭하면 그
	// 선택된 유닛들의 명령만 즉시 취소된다. 기본 우클릭(이동/공격)과 배타적으로 동작한다.
	public bool IsCancelCommandModeActive { get; private set; }

	public void SetCancelCommandModeActive(bool active)
	{
		IsCancelCommandModeActive = active;
		if (active) IsRallyHaltModeActive = false;
	}

	// "집결 및 정지" 모드(2026-08-20, 사용자 요청 "명령 메뉴에 '집결 및 정지' 모드를 넣어줘. 선택한
	// 유닛들을 우클릭을 통해 장소를 지정하면 해당 위치로 이동하고, 이동 후에는 '정지' 상태가 됨") —
	// 켠 뒤 유닛을 선택하고 우클릭하면 이동 명령이 나가고(기존 IssueMoveCommand 재사용), 도착하면
	// Unit.isHalted가 켜져 UnitFSM이 절대 해제되지 않는 "정지"(동상) 상태로 강제 고정한다(HaltFSMState.cs
	// 참고) — 새 직접 명령이나 "명령 취소"만 예외.
	public bool IsRallyHaltModeActive { get; private set; }

	public void SetRallyHaltModeActive(bool active)
	{
		IsRallyHaltModeActive = active;
		if (active) IsCancelCommandModeActive = false;
	}

	private void CancelSelectedUnitsCommands()
	{
		int count = 0;
		foreach (var u in selectedUnits)
		{
			if (u == null) continue;
			bool hasCommand = u.playerMoveTarget.HasValue || u.playerAttackTarget != null || u.playerInteractTarget.HasValue
				|| u.isManualMoveCommand || u.isHalted || u.pendingHaltOnArrival;
			if (!hasCommand) continue;

			u.playerMoveTarget = null;
			u.playerAttackTarget = null;
			u.playerInteractTarget = null;
			u.isManualMoveCommand = false;
			// "명령 해제"는 "정지"(동상) 상태를 풀 수 있는 두 예외 중 하나다(사용자 명시, 2026-08-20).
			u.isHalted = false;
			u.pendingHaltOnArrival = false;
			count++;
		}

		LogHelper.Log(LogHelper.GAME, $"명령 취소: 선택된 유닛 중 {count}기의 이동/공격 명령을 취소했습니다.");
		NoticeCenter.Instance?.PushMomentary($"선택한 유닛의 명령을 취소했습니다. ({count}기)", NoticeCenter.InfoColor);
	}

	public void EnterUnitBuildMode() { _objectPlacement.ExitMode(); _buildPlacement.EnterBuildMode(); }
	public void EnterResourceBuildMode() { _objectPlacement.ExitMode(); _buildPlacement.EnterResourceBuildMode(); }
	public void EnterObjectPlacementMode() { _buildPlacement.ExitMode(); _objectPlacement.EnterObjectMode(); }
	public void EnterTrapPlacementMode() { _buildPlacement.ExitMode(); _objectPlacement.EnterTrapMode(); }
	public void EnterCorePlacementMode() { _buildPlacement.ExitMode(); _objectPlacement.EnterCoreMode(); }

	public void ToggleMonsterPlacementMode()
	{
		bool inOtherPlaceMode = _buildPlacement.IsActive || _objectPlacement.IsActive;
		if (inOtherPlaceMode) return;
		_monsterPlacement.Toggle();
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
		if (!GameInputScheme.IsReady) return;

		int currentFloor = 1;
		Vector3 floorOffset =
			_unitGenerate != null
			? _unitGenerate.GetFloorOffset(currentFloor)
			: Vector3.zero;

		// 입력 정리(2026-08-21, 사용자 요청 "wasd, 마우스 휠, 스페이스바, 마우스 좌클릭 우클릭, 0123
		// 속도조절만 남기고 전부 없애줘" → 이후 "더블클릭과 ctrl+클릭은 있어야 해") — 배치 모드 진입
		// 단축키(R/B/V/O/P/C)는 제거했다(BottomMenuBar의 "소집 배치"/"설치"/"debug" 버튼이 이미 동일한
		// EnterXMode()/ToggleMonsterPlacementMode()를 호출해 마우스만으로도 기능 손실이 없다). Ctrl은
		// "선택 추가" 모디파이어로 남긴다(GameInputScheme.SelectAddHeld) — 마우스 좌/우클릭처럼 이
		// 게임의 핵심 선택 조작이라 없애면 다중 그룹 선택이 아예 불가능해진다.
		bool addHeld = GameInputScheme.SelectAddHeld;

		// 좌하단 패널 스택 구조 개선(2026-08-21) — _monsterPlacement의 다른 모든 진입점은 IsActive일
		// 때만 아래에서 호출되므로, 배치 모드를 완전히 나가는 프레임에는 이 컨트롤러의 어떤 메서드도
		// 더 이상 안 불려서 "안 보인다"는 보고 자체가 실행될 기회가 없었다(MonsterPlacementController.
		// ReportStackVisibility 주석 참고). IsActive 여부와 무관하게 매 프레임 무조건 호출해 즉시
		// 정리되게 한다.
		_monsterPlacement.ReportStackVisibility();

		if (_monsterPlacement.IsActive)
		{
			bool consumed = _monsterPlacement.Update(floorOffset, currentFloor);
			if (!consumed)
			{
				// 몬스터 선택 서브모드 — 기존 좌클릭 드래그/더블클릭/Ctrl+클릭 선택 로직만 재사용하고,
				// 우클릭 이동·게임 속도 단축키는 모두 건너뛴다(시간이 멈춰 있어야 함).
				UpdateSelectionDragAndClick(floorOffset, currentFloor, addHeld);
			}
			return;
		}

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

		// 선택(클릭/드래그 박스/더블클릭/Ctrl+추가)은 명령 모드와 무관하게 항상 가능하다 — "기본
		// 상태에서는 정보 조회만 가능"(문서 19줄)이 막는 건 실제 명령 발동(우클릭 이동)뿐이고, 선택
		// 자체까지 막으면 다중 선택으로 정보를 훑어보는 것조차 못 하게 된다(사용자 피드백, 2026-08-20:
		// "기본 상태에서도 드래그로 다중 선택 등은 가능해야지").
		UpdateSelectionDragAndClick(floorOffset, currentFloor, addHeld);

		bool rightClickOverUI = (BottomMenuBar.Instance != null && BottomMenuBar.Instance.IsMouseOverUI())
			|| (DebugInfoPanel.Instance != null && DebugInfoPanel.Instance.IsMouseOverUI());
		bool rightClickPressed = GameInputScheme.SecondaryDown && selectedUnits.Count > 0 && !rightClickOverUI;

		// =====================================================
		// 우클릭 (명령 취소) - "명령 취소" 토글이 켜져 있을 때는 우클릭이 이동 대신 선택된 유닛들의
		// 명령 취소로 동작한다(2026-08-20, 사용자 요청 "명령 취소 로직을... 토글형으로 바꾸고, 해당
		// 유닛들을 선택 후 우클릭을 눌러 즉시 명령 취소되게"). SetCancelCommandModeActive/
		// SetRallyHaltModeActive가 서로 배타로 관리하므로 이 분기와 아래 이동 분기가 동시에 걸릴 일은 없다.
		// =====================================================
		if (IsCancelCommandModeActive && rightClickPressed)
		{
			CancelSelectedUnitsCommands();
		}

		// =====================================================
		// 우클릭 (이동 / 공격) - 선택된 유닛 전원에게 명령. 2026-08-21 롤백(사용자 요청) — 별도 토글
		// 없이 기본으로 항상 동작한다. "집결 및 정지" 토글이 켜져 있으면 같은 우클릭이 도착 후
		// Unit.isHalted를 켜는 집결 및 정지 명령으로 바뀐다(markHaltOnArrival). "명령 취소" 토글이
		// 켜져 있을 때는 위 분기가 먼저 처리하므로 여기와 동시에 걸릴 일은 없다.
		// =====================================================
		else if (rightClickPressed)
		{
			IssueMoveCommand(floorOffset, currentFloor, markHaltOnArrival: IsRallyHaltModeActive);
		}

		HandleGameSpeedShortcuts();
	}

	// "이동 및 공격"과 "집결 및 정지"가 공유하는 우클릭 이동 명령 발동부(2026-08-20, 사용자 요청으로
	// "집결 및 정지" 모드를 추가하며 기존 이동 로직에서 분리) — markHaltOnArrival이 true면 도착 후
	// Unit.isHalted를 켜서 "정지"(동상) 상태로 고정한다(HaltFSMState.cs 참고). 이 명령 자체가 "정지"를
	// 풀 수 있는 두 예외 중 "플레이어 직접 명령"에 해당하므로, 대상 유닛이 기존에 정지 중이었더라도
	// 여기서 무조건 해제하고 새 명령을 부여한다.
	private void IssueMoveCommand(Vector3 floorOffset, int currentFloor, bool markHaltOnArrival)
	{
		Vector2 mousePos = GameInputScheme.PointerScreenPos;
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

			// 점령 관련(2026-07-27 신규): 플레이어(몬스터 진영) 이동 명령은 자신이 점령한 방과
			// 그 방과 Gate로 연결된 인접 방까지만 허용한다. 인류 명령은 테스트용이므로 이
			// 제한을 받지 않는다(사용자 확인).
			if (unit.IsPlayerMonsterFaction && _gameSession.cmap != null
				&& !_gameSession.cmap.CanPlayerCommandPosition(currentFloor, new Vector2Int(gridPos.x, gridPos.y)))
			{
				continue;
			}

			// "정지"(동상) 해제(2026-08-20) — 새 직접 명령은 정지 잠금을 풀 수 있는 예외다(사용자 명시).
			unit.isHalted = false;
			unit.playerMoveTarget = new Vector2Int(gridPos.x, gridPos.y);
			unit.isManualMoveCommand = true;
			unit.playerAttackTarget = null;
			unit.pendingHaltOnArrival = markHaltOnArrival;
			issuedCount++;
		}

		string commandLabel = markHaltOnArrival ? "집결 및 정지" : "일반 이동";
		LogHelper.Log(LogHelper.GAME, $"{commandLabel} 명령: {issuedCount}기 -> ({gridPos.x}, {gridPos.y})");
	}

	// =====================================================
	// 속도 / 일시정지 — 명령 모드 게이팅과 무관하게 항상 동작해야 해서 별도 메서드로 뺐다(2026-08-20).
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
		if (GameInputScheme.Speed0PressedThisFrame)
		{
			_gameSession.currentGameSpeed = 0.5f;
			if (!_gameSession.isPaused) Time.timeScale = 0.5f;
		}

		if (GameInputScheme.Speed1PressedThisFrame)
		{
			_gameSession.currentGameSpeed = 1f;
			if (!_gameSession.isPaused) Time.timeScale = 1f;
		}

		if (GameInputScheme.Speed2PressedThisFrame)
		{
			_gameSession.currentGameSpeed = 2f;
			if (!_gameSession.isPaused) Time.timeScale = 2f;
		}

		if (GameInputScheme.Speed3PressedThisFrame)
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
		if (GameInputScheme.PrimaryDown)
		{
			// UI(디버그 패널 등) 위에서 누른 클릭은 월드 선택으로 취급하지 않는다. BuildingControlPanel은
			// OnGUI(IMGUI)라 IsPointerOverGameObject()로 안 잡혀서 별도로 확인한다(사용자 신고, 2026-07-27
			// "유닛 생산 시설 버튼 클릭 시 UI가 닫혀버림"). 몬스터 배치 모드 패널도 동일 사유로 확인.
			bool overUI = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
				|| (BuildingControlPanel.Instance != null && BuildingControlPanel.Instance.IsMouseOverPanel())
				|| (_monsterPlacement.IsActive && _monsterPlacement.IsMouseOverPanel())
				|| (BottomMenuBar.Instance != null && BottomMenuBar.Instance.IsMouseOverUI())
				|| (DebugInfoPanel.Instance != null && DebugInfoPanel.Instance.IsMouseOverUI());

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

		MergeIntoSelection(nearby, addHeld);

		LogHelper.Log(LogHelper.GAME, $"더블클릭 선택: {origin.unitType.typeName} 근방 {selectedUnits.Count}기");
	}

	// DoBoxSelect/SelectNearbySameType이 공유하는 "후보 목록을 선택에 반영" 절차(2026-08-21 추출,
	// simplify 리뷰 지적 — 두 메서드가 이 8줄을 그대로 복붙하고 있었다). DoClickSelect의 단일 클릭
	// 분기는 addHeld일 때 "이미 선택돼 있으면 제거"라는 다른 의미(토글)라 여기 합치지 않는다.
	private void MergeIntoSelection(List<Unit> candidates, bool addHeld)
	{
		if (addHeld)
		{
			foreach (var u in candidates)
				if (!selectedUnits.Contains(u)) selectedUnits.Add(u);
		}
		else
		{
			selectedUnits.Clear();
			selectedUnits.AddRange(candidates);
		}
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
