using System;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using VContainer;
using Haare.Util.Logger;

// 입력 라우팅 + 유닛 선택/이동/공격의 핵심 로직을 담당한다. 배치 모드 2종(빌드/오브젝트·함정·문
// 재설치)은 각자 별도 컨트롤러(BuildPlacementController/ObjectPlacementController, 전부 이 폴더)로
// 분리돼 있다(2026-08-20, InputManager 비대화 방지) — 이 클래스는 그 두 컨트롤러의 진입/배타 처리를
// 조율(Update()/OnGUI() 위임)하고, 일반 선택(드래그 박스/클릭/더블클릭)과 우클릭 이동/공격, 게임
// 속도 제어라는 "입력 관리자 본연의 책임"만 갖는다. 몬스터 배치 프리셋(R키 방 단위 배치 모드)은
// 기초문서.md 피드백(2026-08-22, "배치 모드 완전 제거")으로 폐기됐다 — 대신 "제자리 공격" 명령
// 토글(IsStandGroundModeActive)이 그 자리를 대신한다(아래 참고).
public class InputManager : MonoBehaviour
{
	// 하위 호환용: 기존 코드는 "선택된 유닛 1기"를 이렇게 참조한다.
	// 실제 저장소는 _selectedUnits이고, 이 프로퍼티는 그 목록의 첫 번째 유닛을 가리킨다.
	public Unit selectedUnit
	{
		get => _selectedUnits.Count > 0 ? _selectedUnits[0] : null;
		set
		{
			_selectedUnits.Clear();
			if (value != null) _selectedUnits.Add(value);
		}
	}

	// 캡슐화(2026-08-22 리팩토링): 외부(UI/UnitGenerate)는 항상 읽기만 하고, 실제 추가/제거/비우기는
	// 전부 이 클래스 안(선택/드래그/더블클릭 로직)에서만 일어난다 — 그 불변식을 타입으로 강제한다.
	private readonly List<Unit> _selectedUnits = new List<Unit>();
	public IReadOnlyList<Unit> selectedUnits => _selectedUnits;
	// List<Unit>.Contains(무할당)을 그대로 쓰기 위한 헬퍼 — 매 프레임 호출되는 UnitGenerate의 선택 표시
	// 갱신(RefreshSelectionVisual)이 IReadOnlyList 너머로 LINQ Contains(열거자 박싱)를 타지 않게 한다.
	public bool IsUnitSelected(Unit u) => _selectedUnits.Contains(u);
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

	// 배치 모드 2종 컨트롤러(2026-08-20 분리) — 전부 이 Construct()에서 InputManager가 직접 생성해
	// 소유한다(VContainer 등록 대상 아님 — InputManager별 상태를 갖는 협력 객체라 DI 싱글턴으로 둘
	// 이유가 없다). 서로 다른 배치 모드끼리의 배타 진입(예: 빌드 모드 진입 시 오브젝트 배치 모드 종료)은
	// 컨트롤러끼리 직접 참조하지 않고 이 클래스의 Update()가 조율한다(순환 참조 방지).
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
	// BottomMenuBar(2026-08-20 UI 리뉴얼)가 호출하는 공개 API. 원래는 키보드 단축키(R/B/V/O/P/C)와
	// 같은 코드 경로를 공유했으나, 2026-08-21 입력 정리로 그 단축키들은 제거되고 이 버튼 호출만 남았다.
	// =====================================================
	public bool IsBuildPlacementActive => _buildPlacement != null && _buildPlacement.IsActive;
	public bool IsObjectPlacementActive => _objectPlacement != null && _objectPlacement.IsActive;

	// 개별 서브모드 단위 상태(2026-08-20, 사용자 신고 "자원 생산 건물과 유닛 생산 건물이 다중 선택되어
	// 버리는 UI 버그") — BottomMenuBar가 설치/debug 서브메뉴 버튼을 각각 따로 하이라이트하고, 이미
	// 활성인 버튼을 다시 눌렀을 때만 취소하도록(재클릭 토글) 판단하는 데 쓴다.
	public bool IsUnitBuildModeActive => _buildPlacement != null && _buildPlacement.IsUnitBuildModeActive;
	public bool IsResourceBuildModeActive => _buildPlacement != null && _buildPlacement.IsResourceBuildModeActive;
	public bool IsObjectOnlyPlacementActive => _objectPlacement != null && _objectPlacement.IsObjectModeActive;
	public bool IsTrapPlacementActive => _objectPlacement != null && _objectPlacement.IsTrapModeActive;
	public bool IsDoorRepairPlacementActive => _objectPlacement != null && _objectPlacement.IsDoorRepairModeActive;
	// 2026-08-24 debug 전용 — 바닥 타일을 벽으로 전환하는 모드 / 함정 무제한 설치 토글.
	public bool IsWallConvertPlacementActive => _objectPlacement != null && _objectPlacement.IsWallConvertModeActive;
	// 더미 건물 배치 모드(2026-08-25 debug 전용) — displayName으로 어느 더미 건물 버튼인지 구분한다
	// (같은 부류 버튼 두 개를 BottomMenuBar가 각각 따로 하이라이트해야 하므로).
	public bool IsDummyBuildingModeActive(string displayName)
		=> _objectPlacement != null && _objectPlacement.IsDummyBuildingModeActive && _objectPlacement.ActiveDummyBuildingName == displayName;
	public bool DebugUnlimitedTrapPlacement
	{
		get => _objectPlacement != null && _objectPlacement.DebugUnlimitedTrapPlacement;
		set { if (_objectPlacement != null) _objectPlacement.DebugUnlimitedTrapPlacement = value; }
	}
	// "모든 유닛 선택 가능" debug 토글(2026-08-24, 사용자 요청) — IsSelectableUnit의 진영/안개 제한을
	// 우회해 인류·야생 몬스터·안개 속 유닛까지 클릭/드래그박스/더블클릭으로 선택하고 정보를 열람할 수
	// 있게 한다. 실행 계열 명령(공격/이동)은 이 토글과 무관하게 기존 진영 규칙(예: 자기 유닛만 이동
	// 명령 가능)을 그대로 따른다 — 이 토글은 어디까지나 "선택/정보열람" 게이트만 완화한다.
	public bool DebugSelectAllUnits { get; set; }

	// 우클릭 취소를 없앤 대신(2026-08-20, 사용자 요청) BottomMenuBar가 "다른 메뉴로 전환" 또는 "같은
	// 서브 버튼 재클릭" 시점에 호출하는 단일 취소 진입점. 취소 notice도 (재클릭이든 메뉴 전환이든) 이
	// 한 곳에서만 띄워서 두 경로가 서로 다른 문구를 중복해서 띄우지 않게 한다(2026-08-20, 사용자 요청
	// "메뉴를 통한 모드 클릭시 모두 notice로 설명이 뜨게").
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

	// "명령" 상위 메뉴를 닫거나 다른 메뉴로 전환할 때 "명령 취소"/"집결 및 정지"/"제자리 공격" 토글도
	// 함께 꺼지도록 하는 진입점(2026-08-20, 사용자 요청 "명령에서 상위 메뉴를 눌러 꺼버리면, 위에서
	// 토글했던것들도 취소되게"). "이동 및 공격"은 2026-08-21 롤백으로 더 이상 토글이 아니라 여기서
	// 다룰 대상이 아니다.
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

	// "명령 취소" 모드(2026-08-20 재설계, 사용자 요청 "명령 취소 로직을 바꿀게. 토글형으로 바꾸고, 해당
	// 유닛들을 선택 후 우클릭을 눌러 즉시 명령 취소되게 하자") — 예전엔 버튼을 누르는 즉시 "모든 유닛"의
	// 명령을 취소했는데(선택 여부 무관), 이제 토글이다: 토글을 켠 뒤 유닛을 선택하고 우클릭하면 그
	// 선택된 유닛들의 명령만 즉시 취소된다. 기본 우클릭(이동/공격)과 배타적으로 동작한다.
	public bool IsCancelCommandModeActive { get; private set; }

	public void SetCancelCommandModeActive(bool active)
	{
		IsCancelCommandModeActive = active;
		if (active) { IsRallyHaltModeActive = false; IsStandGroundModeActive = false; }
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
		if (active) { IsCancelCommandModeActive = false; IsStandGroundModeActive = false; }
	}

	// "제자리 공격" 모드(기초문서.md 피드백, 2026-08-22 — R키 몬스터 배치 모드 폐기를 대체, 사용자 결정
	// "명령 항목에 하나 추가... 이 명령을 넣어두면, 제자리에서 절대 이동하지 않고 공격만 함") — "집결
	// 및 정지"와 동일한 이동 파이프라인을 재사용하되, 도착 후 isHalted 대신 isStandGroundAttack을 켠다
	// (StandGroundAttackFSMState.cs 참고).
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

	// 선택/정보열람 가능 여부(2026-08-24, 사용자 요청 "플레이어 유닛만 선택 가능하게... 안개에 있는
	// 오브젝트와 유닛들 또한 선택 불가") — 클릭/드래그박스/더블클릭 선택 전부가 이 한 곳을 거친다.
	// 공격 대상 탐색(ExecuteRightClickCommand의 FindUnitAtGridPos 호출)은 "선택"이 아니라 별개의
	// 명령 판정이라 이 필터를 타지 않는다.
	private bool IsSelectableUnit(Unit u)
	{
		if (u == null || u.Health.hp <= 0) return false;
		if (DebugSelectAllUnits) return true;
		if (!u.IsPlayerMonsterFaction) return false;
		if (IsPositionHiddenByFog(u.position, u.currentFloor)) return false;
		return true;
	}

	// Room.FogRevealed 기반 판정(UnitGenerate.SyncVisual의 hiddenByFog와 동일한 조회 패턴) — 그 좌표가
	// 속한 방이 아직 안개에 덮여 있으면 true. 어느 방에도 속하지 않는 칸(빈 청크)은 항상 영구 안개라
	// roomGrid에 아예 없으므로, 조회 실패도 안개로 취급한다.
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

		// 사망 후 Destroy된 유닛이 선택 목록에 계속 남는 문제 예방(2026-08-24, 사용자 요청 "사망시
		// destroy 처리 잘 되게") — GameSession.RemoveDeadUnit이 Destroy(u)를 호출해도 InputManager가
		// 따로 들고 있는 _selectedUnits에서는 자동으로 안 빠진다. Unity는 실제 파괴를 프레임 끝에
		// 처리하므로 다음 프레임부터 u==null이 true가 되는데, 그때까지 목록에 남아있으면
		// DebugInfoPanel 등 소비처가 파괴된 참조의 멤버(u.unitType 등)에 접근하다가
		// MissingReferenceException을 던질 수 있다 — 매 프레임 시작 시점에 선제적으로 정리한다.
		if (_selectedUnits.RemoveAll(u => u == null) > 0)
			OnSelectionChanged?.Invoke();

		int currentFloor = 1;
		Vector3 floorOffset =
			_unitGenerate != null
			? _unitGenerate.GetFloorOffset(currentFloor)
			: Vector3.zero;

		// 입력 정리(2026-08-21, 사용자 요청 "wasd, 마우스 휠, 스페이스바, 마우스 좌클릭 우클릭, 0123
		// 속도조절만 남기고 전부 없애줘" → 이후 "더블클릭과 ctrl+클릭은 있어야 해") — 배치 모드 진입
		// 단축키(R/B/V/O/P/C)는 제거했다(BottomMenuBar의 "설치"/"debug" 버튼이 이미 동일한 EnterXMode()를
		// 호출해 마우스만으로도 기능 손실이 없다). Ctrl은 "선택 추가" 모디파이어로 남긴다
		// (GameInputScheme.SelectAddHeld) — 마우스 좌/우클릭처럼 이 게임의 핵심 선택 조작이라 없애면
		// 다중 그룹 선택이 아예 불가능해진다.
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
		// 우클릭 (공격 / 이동) - 선택된 유닛 전원에게 명령. 2026-08-21 롤백(사용자 요청) — 별도 토글
		// 없이 기본으로 항상 동작한다. "집결 및 정지"/"제자리 공격" 토글이 켜져 있으면 같은 우클릭이
		// 도착 후 각각 Unit.isHalted/isStandGroundAttack을 켜는 명령으로 바뀐다. "명령 취소" 토글이
		// 켜져 있을 때는 위 분기가 먼저 처리하므로 여기와 동시에 걸릴 일은 없다. 2026-08-22(사용자
		// 요청 "좌클릭은 선택, 우클릭은 실행으로 두자") — 클릭 위치에 공격 가능한 대상(적 유닛/코어/문)이
		// 있으면 공격을, 없으면 기존 이동을 발동하도록 ExecuteRightClickCommand로 위임한다.
		// =====================================================
		else if (rightClickPressed)
		{
			ExecuteRightClickCommand(floorOffset, currentFloor, markHaltOnArrival: IsRallyHaltModeActive, markStandGroundOnArrival: IsStandGroundModeActive);
		}

		HandleGameSpeedShortcuts();
	}

	// 우클릭 실행 디스패처(2026-08-22, 사용자 요청 "좌클릭은 선택, 우클릭은 실행으로 두자. 설치나
	// 명령 전반 모두 포함") — 이전에는 좌클릭(DoClickSelect)이 유닛/오브젝트 공격을, 우클릭
	// (IssueMoveCommand)이 이동만 담당해 둘이 분리돼 있었다(그 결과 "우클릭으로는 문을 공격할 수
	// 없다"는 사용자 신고가 나왔다). 이제는 우클릭 하나가 클릭 위치를 보고 적 유닛 공격 → 코어/문
	// 공격 → 이동 순서로 판단해 실행한다. "집결 및 정지"/"제자리 공격" 예약 토글이 켜져 있어도 클릭
	// 위치에 공격 가능한 대상이 있으면 공격이 우선한다(이동 예약 토글은 이동할 때만 의미가 있으므로).
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

				// 2*2 통로(플레이어 문 2칸 + 야생 문 2칸)에서 야생 문 공격 명령을 내리면 플레이어
				// 몬스터가 자기 진영 문 앞에서 멈춰버리는 버그(사용자 신고, 2026-08-22) — 원인은 이
				// 플래그가 false라 RoomConfinedMovement가 "플레이어 명령 중이 아님"으로 보고 자기
				// 진영 문 타일조차 walkable에서 제외했기 때문이다(방 제한 규칙 "오직 플레이어의
				// 명령에 의해서만 다른 방으로 이동 가능"의 예외 조건). SetObjectAttackCommand가 내부적으로
				// isManualMoveCommand를 true로 켜서 일반 이동 명령과 동일하게 방 경계·문 타일 제한을 우회시킨다 —
				// 상대 진영 문 타일 자체는 여전히 IsBlockedByClosedDoor(진영 불일치)가 막으므로, 자기 문 위까지만
				// 접근해 인접 공격하게 된다.
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

	// "이동 및 공격"/"집결 및 정지"/"제자리 공격"이 공유하는 우클릭 이동 명령 발동부(2026-08-20, 사용자
	// 요청으로 "집결 및 정지" 모드를 추가하며 기존 이동 로직에서 분리, 2026-08-22 "제자리 공격" 추가) —
	// markHaltOnArrival/markStandGroundOnArrival이 true면 도착 후 각각 Unit.isHalted/
	// isStandGroundAttack을 켜서 강제 상태로 고정한다(HaltFSMState.cs/StandGroundAttackFSMState.cs
	// 참고). 이 명령 자체가 그 강제 상태들을 풀 수 있는 예외 중 "플레이어 직접 명령"에 해당하므로,
	// 대상 유닛이 기존에 그 상태였더라도 여기서 무조건 해제하고 새 명령을 부여한다.
	private void IssueMoveCommand(Vector3 floorOffset, int currentFloor, bool markHaltOnArrival, bool markStandGroundOnArrival)
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
			var selectedSet = new HashSet<Unit>(selectedUnits);
			foreach (var u in selectedUnits)
				if (u != null && u.Health.hp > 0 && u.IsPlayerMonsterFaction && u.currentRoom != destRoom)
					incomingPopulation += u.populationCost;

			// 광클 대응(2026-08-23, 사용자 신고 "가끔 광클하면 맵 제한 유닛 이상으로 유닛을 넣을 수
			// 있어") — Room.CurrentPopulation은 "이미 그 방에 물리적으로 도착한" 유닛만 센다. 서로 다른
			// 부대를 빠르게 연달아 같은 방으로 이동시키면, 먼저 명령받은 부대가 아직 도착 전이라
			// CurrentPopulation에 반영되지 않은 채로 다음 명령의 검사를 통과해버려(둘 다 그 순간엔 여유가
			// 있어 보임) 결과적으로 도착 인원 합이 정원을 넘길 수 있었다. 이미 이 방으로 이동 중인(선택되지
			// 않은) 다른 유닛들의 인구수도 여유분에서 미리 빼서, 아직 도착하지 않은 "예약된" 인구까지
			// 반영한다.
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

			// 이동 명령 도달성(2026-07-27 신규, 2026-08-24 점령 기준→도달 기준으로 변경) — 예전엔
			// "자신이 점령한 방과 그 바로 옆(1홉)"까지만 허용해서, 문을 다 부숴놔도 중간 방들을
			// 하나하나 점령해야만 그 너머로 이동 명령을 낼 수 있었다(사용자 신고, "지금 점령하지
			// 않으면 다음 방으로 지나갈 수가 없는 현상"). 이제는 점령 여부와 무관하게, 지금 있는
			// 방에서 통행 가능한 문(파괴됐거나 자기 진영 소유)만 거쳐 도달 가능한 방이면 전부
			// 허용한다(GameSession.CanFactionReachRoom). 인류 명령은 테스트용이므로 이 제한을
			// 받지 않는다(사용자 확인).
			if (unit.IsPlayerMonsterFaction
				&& !_gameSession.CanFactionReachRoom(FactionType.Player, currentFloor, unit.currentRoom?.RoomId ?? -1, destRoom?.RoomId ?? -1))
			{
				continue;
			}

			// "정지"(동상)/"제자리 공격" 해제(2026-08-20/2026-08-22) — 새 직접 명령은 두 강제 상태
			// 모두를 풀 수 있는 예외다(사용자 명시).
			unit.SetMoveCommand(new Vector2Int(gridPos.x, gridPos.y), markHaltOnArrival, markStandGroundOnArrival);
			issuedCount++;
		}

		string commandLabel = markHaltOnArrival ? "집결 및 정지" : markStandGroundOnArrival ? "제자리 공격" : "일반 이동";
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
			// UI(디버그 패널 등) 위에서 누른 클릭은 월드 선택으로 취급하지 않는다. BuildingControlPanel은
			// OnGUI(IMGUI)라 IsPointerOverGameObject()로 안 잡혀서 별도로 확인한다(사용자 신고, 2026-07-27
			// "유닛 생산 시설 버튼 클릭 시 UI가 닫혀버림").
			bool overUI = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
				|| (BuildingControlPanel.Instance != null && BuildingControlPanel.Instance.IsMouseOverPanel())
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
	// 클릭 선택 (드래그 없이 뗀 경우) — 좌클릭은 오직 선택만 담당한다(2026-08-22, 사용자 요청 "어떤
	// 기능이든 좌클릭은 선택, 우클릭은 실행으로 두자"). 공격/이동/오브젝트 공격 등 실행 계열 명령은
	// 전부 우클릭(ExecuteRightClickCommand)으로 옮겼다.
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

		// 유닛 클릭이면 선택 처리 — 플레이어 유닛만 선택/정보열람 가능하다(2026-08-24, 사용자 요청
		// "플레이어 유닛만 선택 가능하게... 다른 유닛들은 선택 불가. 정보 열람도 불가"). 다른 진영
		// 유닛이나 안개에 가려진 유닛을 클릭하면 그 자리에 아무것도 없었던 것처럼(오브젝트 → 허공
		// 클릭 순으로) 계속 판정한다.
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

		// 오브젝트(코어/문/함정/전리품/시체/전멸흔적) 클릭 시 정보 패널 표시(2026-08-22 후속 피드백,
		// 사용자 요청 "모든 오브젝트는 이제 클릭을 통해 정보를 볼 수 있어(건물처럼)") — 유닛도 건물도
		// 아닌 위치에 오브젝트가 있을 때만 확인한다(유닛 선택이 항상 우선). 안개에 가려진 오브젝트는
		// 선택/정보열람 불가(2026-08-24, 사용자 요청) — 못 찾은 것처럼 허공 클릭 처리로 넘어간다.
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
	// 드래그 박스 선택 — 플레이어 유닛만 선택 가능(2026-08-24, 사용자 요청). 예전엔 "진영 제한 없음"
	// 이었으나 이제 IsSelectableUnit(플레이어 진영 + 안개 미적용)만 통과한다.
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
}
