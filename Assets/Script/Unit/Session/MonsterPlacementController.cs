using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Haare.Util.Logger;
using GrimArchive.Wave;

// 플레이어 몬스터 배치 모드(2026-08-19, "몬스터 배치 프리셋 프로그래머 지시서") — R키. 웨이브 대기
// 중(HumanWaveManager.WaveState.Idle)에만 진입 가능. 시간을 멈추고, 점령한 방을 단위로 선택 →
// 몬스터 선택/타일 선택 두 서브모드를 오가며 몬스터별 디펜스 시작 위치를 지정한다. 실제 이동은 이
// 모드 안에서 일어나지 않는다(MonsterDefensePlacementSystem.ApplyDefenseStartPositions가 0층 인류
// 사전 스폰 시점에 대신 처리).
//
// InputManager 비대화를 막기 위해 InputManager.cs에서 분리했다(2026-08-20). selectedUnits는
// InputManager가 소유한 리스트를 그대로 참조로 받아 공유한다(더블클릭/드래그/Ctrl+클릭 같은 일반
// 선택 로직은 여전히 InputManager 쪽에 있고, 이 클래스는 그 결과 목록만 읽고/비운다). 일반 선택
// 로직 쪽에서 이 모드의 방 필터를 적용해야 하는 지점(IsUnitSelectable)만 InputManager가 이 클래스에
// 역으로 물어본다 — 그 외에는 InputManager → 이 클래스 단방향 호출만 있다.
public class MonsterPlacementController
{
    public bool IsActive { get; private set; }

    private enum PlacementSubMode { MonsterSelect, TileSelect }

    private readonly GameSession _gameSession;
    private readonly UnitGenerate _unitGenerate;
    private readonly UnitSpriteManager _unitSpriteManager;
    private readonly List<Unit> _selectedUnits;

    private PlacementSubMode _placementSubMode = PlacementSubMode.MonsterSelect;
    private Room _placementSelectedRoom;
    private readonly List<Unit> _placementQueue = new List<Unit>();
    private bool _placementWasPausedBefore;
    private bool _placementHasLastPaintedTile;
    private Vector3Int _placementLastPaintedTile;

    // 타일 선택 서브모드 중 흐릿하게 만든 유닛 스프라이트의 원래 알파값 — 서브모드를 벗어나거나
    // 모드를 종료할 때 정확히 복원하기 위해 보관한다.
    private readonly Dictionary<SpriteRenderer, float> _placementDimmedUnitAlphas = new Dictionary<SpriteRenderer, float>();

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

    public MonsterPlacementController(GameSession gameSession, UnitGenerate unitGenerate, UnitSpriteManager unitSpriteManager, List<Unit> selectedUnits)
    {
        _gameSession = gameSession;
        _unitGenerate = unitGenerate;
        _unitSpriteManager = unitSpriteManager;
        _selectedUnits = selectedUnits;
    }

    // 몬스터 배치 프리셋(2026-08-19, 사용자 요청 "몬스터 선택 진입 시, 선택된 방 안에서만 몬스터를
    // 선택할 수 있게 해줘. 지금 모든 유닛 다 선택됨") — 배치 모드의 몬스터 선택 서브모드에서는 선택된
    // 방 안에 있는 유닛만 선택 대상으로 허용한다. 배치 모드가 아니거나(일반 플레이) 타일 선택
    // 서브모드거나 아직 방을 안 골랐으면 항상 통과(기존 동작 그대로). InputManager의 일반 클릭/드래그/
    // 더블클릭 선택 로직이 이 메서드를 필터로 사용한다.
    public bool IsUnitSelectable(Unit u)
    {
        if (!IsActive || _placementSubMode != PlacementSubMode.MonsterSelect || _placementSelectedRoom == null)
            return true;

        return _gameSession.roomGrid.TryGetValue(new Vector3Int(u.position.x, u.position.y, u.currentFloor), out Room r)
            && r == _placementSelectedRoom;
    }

    public bool IsMouseOverPanel()
    {
        if (_placementSelectedRoom == null) return false;
        return GUIMouseUtil.IsMouseOverRect(GetPlacementPanelRect());
    }

    // =====================================================
    // 진입/종료
    // =====================================================
    public void Toggle()
    {
        if (!IsActive)
        {
            // 웨이브가 시작되기 전(대기 중)에만 사용 가능 — 웨이브를 클리어하고 다음 웨이브를
            // 기다리는 동안(다시 Idle)도 재활성화된다(사용자 요청, 2026-08-19).
            var wm = HumanWaveManager.Instance;
            if (wm != null && wm.currentState != WaveState.Idle)
            {
                LogHelper.Warning(LogHelper.GAME, "웨이브 진행 중에는 몬스터 배치 모드를 사용할 수 없습니다.");
                return;
            }

            IsActive = true;
            _selectedUnits.Clear();
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
            ExitMode();
        }
    }

    private void ExitMode()
    {
        IsActive = false;

        RestoreAllUnitDim();
        _placementSelectedRoom = null;
        _placementSubMode = PlacementSubMode.MonsterSelect;
        _placementQueue.Clear();
        _selectedUnits.Clear();

        // 이 모드는 몬스터들의 행동 상태에 아무런 영향을 주지 않는다(사용자 요청) — 여기서 이동 명령을
        // 내리지 않는다. defenseStartPosition만 남아있고, 실제 이동은 0층 인류 사전 스폰 시점에
        // MonsterDefensePlacementSystem.ApplyDefenseStartPositions가 담당한다.
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
        _selectedUnits.Clear();
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
    // 같은 key의 기존 알림을 지우고 새로 넣는다). 자동 만료되지 않으므로 ExitMode가 명시적으로
    // Remove할 때까지 계속 떠 있는다.
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
        if (!IsActive || _placementSelectedRoom == null) return;

        if (_placementSubMode == PlacementSubMode.TileSelect)
            DimUnitsMatching(_ => true);
        else
            DimUnitsMatching(u => !IsUnitSelectable(u)); // 선택 가능(=방 안) 유닛만 강조 유지
    }

    private void DimUnitsMatching(Func<Unit, bool> shouldDim)
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
        foreach (var unit in _selectedUnits)
        {
            if (unit == null || unit.Health.hp <= 0) continue;
            if (!(unit is Monster) || !unit.IsPlayerMonsterFaction) continue;
            if (_placementQueue.Contains(unit)) continue;

            _placementQueue.Add(unit);
        }

        _selectedUnits.Clear();
        _placementSubMode = PlacementSubMode.TileSelect;
        _placementHasLastPaintedTile = false;

        RefreshPlacementUnitDim();
        RefreshPlacementModeNotice();
    }

    // =====================================================
    // 배치 모드 입력 라우팅 — true를 반환하면 이번 프레임 입력을 이미 소비한 것(InputManager의 일반
    // 선택 로직으로 새지 않게 막는다). 몬스터 선택 서브모드일 때만 false를 반환해 InputManager가
    // 기존 좌클릭 선택 로직을 그대로 태우게 한다.
    // =====================================================
    public bool Update(Vector3 floorOffset, int currentFloor)
    {
        // Phase 1: 방 미선택 — 좌클릭으로 점령한 방을 선택한다.
        if (_placementSelectedRoom == null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);

                if (_gameSession.roomGrid.TryGetValue(new Vector3Int(gridPos.x, gridPos.y, currentFloor), out Room room)
                    && room.RoomFaction == FactionType.Player)
                {
                    SelectPlacementRoom(room);
                }
            }
            return true;
        }

        // 패널(오른쪽 중앙) 위 클릭은 OnGUI 버튼이 직접 처리 — 여기서는 아무 것도 하지 않는다.
        if (IsMouseOverPanel())
        {
            _placementHasLastPaintedTile = false;
            return true;
        }

        if (_placementSubMode == PlacementSubMode.MonsterSelect)
        {
            return false; // InputManager의 기존 좌클릭 선택 로직(UpdateSelectionDragAndClick)을 그대로 재사용
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
        Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);

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
    // OnGUI 그리기 진입점 — InputManager.OnGUI()가 IsActive일 때만 호출한다.
    // =====================================================
    public void DrawGUI()
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

    // =====================================================
    // 배치 모드 UI(오른쪽 중앙) — 몬스터 선택/타일 선택 버튼 + 남은 배치 대기열 목록
    // =====================================================
    private Rect GetPlacementPanelRect()
        => new Rect(Screen.width - PlacementPanelWidth - 16f, PlacementPanelTopY, PlacementPanelWidth, PlacementPanelHeight);

    private void DrawMonsterPlacementUI()
    {
        // 방 미선택 안내 문구는 NoticeCenter가 담당한다(모드 진입 시 Toggle에서 1회 Push, 2026-08-19
        // "이 문구들을 시스템화" 요청) — 여기서는 방이 선택된 뒤의 패널만 그린다.
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
            GUILayout.Label($"선택된 몬스터: {_selectedUnits.Count}기");
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

            Sprite icon = _unitSpriteManager?.GetIcon(unit.unitType?.typeName);
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
            GUISpriteUtil.Draw(icon, r);
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
}
