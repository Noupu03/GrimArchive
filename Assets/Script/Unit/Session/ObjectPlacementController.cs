using UnityEngine;
using UnityEngine.EventSystems;
using Haare.Util.Logger;

// 오브젝트(O)/함정(P)/문 재설치 배치 모드 — 빌드 모드와 동일한 고스트 방식(2026-07-22/23, 사용자
// 요청). 셋 다 고스트 하나를 공유하고, 모양/색만 종류에 따라 바꿔 쓴다. 몬스터(M) 배치 모드는
// 건축물·자원·유닛 생산 MVP(2026-07-27)로 대체되어 삭제됐고, 그 뒤에 남아있던 테스트용 코어(C)
// 배치도 코어 전면 개편(기초문서.md 피드백, 2026-08-22 — 모든 방이 항상 코어를 가짐)으로 제거됐다.
// 문 재설치 모드는 그 자리에 2026-08-22 신규 추가됐다.
//
// InputManager 비대화를 막기 위해 InputManager.cs에서 분리했다(2026-08-20, BuildPlacementController
// 와 동일한 리팩토링). 모드 간 배타 진입(빌드 모드 종료 등)은 BuildPlacementController를 직접
// 참조하지 않고 InputManager.Update()가 조율한다.
public class ObjectPlacementController
{
    public bool IsActive => _isObjectPlaceMode || _isTrapPlaceMode || _isDoorRepairMode;
    // BottomMenuBar의 개별 서브버튼(오브젝트/함정/문 재설치) 하이라이트·토글용(2026-08-20) —
    // BuildPlacementController.IsUnitBuildModeActive/IsResourceBuildModeActive와 동일한 이유.
    public bool IsObjectModeActive => _isObjectPlaceMode;
    public bool IsTrapModeActive => _isTrapPlaceMode;
    public bool IsDoorRepairModeActive => _isDoorRepairMode;

    private readonly GameSession _gameSession;
    private readonly UnitGenerate _unitGenerate;
    private readonly ResourceManager _resourceManager;
    private readonly PlacementGhost _ghost = new PlacementGhost("PlacementGhost");

    private bool _isObjectPlaceMode;
    private bool _isTrapPlaceMode;
    private bool _isDoorRepairMode;

    private Sprite _coreSprite;
    private Sprite _trapSprite;
    private Sprite _doorOpenSprite;

    private Sprite CoreSprite => _coreSprite ??= Resources.Load<Sprite>("obj/core");
    private Sprite TrapSprite => _trapSprite ??= Resources.Load<Sprite>("obj/trap");
    private Sprite DoorOpenSprite => _doorOpenSprite ??= Resources.Load<Sprite>("obj/door_open");

    public ObjectPlacementController(GameSession gameSession, UnitGenerate unitGenerate, ResourceManager resourceManager)
    {
        _gameSession = gameSession;
        _unitGenerate = unitGenerate;
        _resourceManager = resourceManager;
    }

    public void EnterObjectMode()
    {
        if (_isObjectPlaceMode) return;
        _isTrapPlaceMode = false;
        _isDoorRepairMode = false;
        _isObjectPlaceMode = true;

        // 코어(루팅 오브젝트) 아트 스프라이트 배정(사용자 요청, 2026-07-23) — 이전엔 벽 타일을 임시로 썼다.
        _ghost.Show(CoreSprite);
        LogHelper.Log(LogHelper.GAME, "오브젝트 배치 모드 진입 (좌클릭: 생성)");
    }

    public void EnterTrapMode()
    {
        if (_isTrapPlaceMode) return;
        _isObjectPlaceMode = false;
        _isDoorRepairMode = false;
        _isTrapPlaceMode = true;

        // 함정 아트 스프라이트 배정(사용자 요청, 2026-07-23) — 이전엔 세모 폴백 스프라이트를 썼다.
        _ghost.Show(TrapSprite);
        LogHelper.Log(LogHelper.GAME, $"함정 배치 모드 진입 (돌 {ResourceManager.TrapPlaceStoneCost}개 소모, 좌클릭: 생성)");
    }

    // 문도 방어건물화(기초문서.md 피드백, 2026-08-22) — 파괴된 문을 원래 게이트 자리에만 재설치.
    public void EnterDoorRepairMode()
    {
        if (_isDoorRepairMode) return;
        _isObjectPlaceMode = false;
        _isTrapPlaceMode = false;
        _isDoorRepairMode = true;

        _ghost.Show(DoorOpenSprite);
        LogHelper.Log(LogHelper.GAME, $"문 재설치 모드 진입 (돌 {ResourceManager.DoorRepairStoneCost}개 소모, 파괴된 문 자리만 선택 가능, 우클릭: 설치)");
    }

    public void ExitMode()
    {
        _isObjectPlaceMode = false;
        _isTrapPlaceMode = false;
        _isDoorRepairMode = false;
        _ghost.Hide();
    }

    public void Update(Vector3 floorOffset, int currentFloor)
    {
        Vector2 mousePos = GameInputScheme.PointerScreenPos;
        Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);

        // 이미 다른 오브젝트가 있거나(objectGrid) 벽/유닛으로 막혀있으면(IsAreaClear) 놓을 수 없다.
        // 문 재설치 모드는 그 대신 "원래 게이트(복도) 자리"인지만 확인한다(기초문서.md 피드백,
        // 2026-08-22 "복도 자리에만 설치 가능") — 자유로운 위치 배치가 아니다.
        bool canPlace = _gameSession != null && _unitGenerate != null
            && !_gameSession.objectGrid.ContainsKey(gridPos)
            && (_isDoorRepairMode ? _gameSession.IsRepairableDoorTile(gridPos)
                                  : _unitGenerate.IsAreaClear(new Vector2Int(gridPos.x, gridPos.y), Vector2.one, currentFloor));

        _ghost.UpdatePosition(gridPos, floorOffset, canPlace);

        // 우클릭 취소는 없앴다(2026-08-20, 사용자 요청) — BuildPlacementController.Update()와 동일한
        // 이유. 취소는 BottomMenuBar에서 다른 메뉴로 전환하거나 같은 서브 버튼을 다시 눌러야 한다.
        // 설치 확정 입력은 2026-08-22(사용자 요청 "좌클릭은 선택, 우클릭은 실행으로 두자")로 좌클릭에서
        // 우클릭으로 옮겼다.
        if (GameInputScheme.SecondaryDown && canPlace)
        {
            bool overUI = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                || (BottomMenuBar.Instance != null && BottomMenuBar.Instance.IsMouseOverUI())
                || (DebugInfoPanel.Instance != null && DebugInfoPanel.Instance.IsMouseOverUI());
            if (!overUI)
            {
                if (_isObjectPlaceMode)
                {
                    _gameSession.SpawnLootObjectAt(gridPos);
                    ExitMode();
                }
                else if (_isTrapPlaceMode)
                {
                    // 돌 자원이 부족하면 배치를 취소하지 않고 모드를 유지 — 자원을 모은 뒤 같은 위치에
                    // 다시 시도할 수 있게 한다(배치 모드 자체는 메뉴 전환/재클릭으로만 취소).
                    if (_resourceManager != null && _resourceManager.TryConsumeResource(ResourceType.Stone, ResourceManager.TrapPlaceStoneCost))
                    {
                        _gameSession.SpawnTrapAt(gridPos);
                        ExitMode();
                    }
                    else
                    {
                        LogHelper.Warning(LogHelper.GAME, $"돌이 부족하여 함정을 배치할 수 없습니다. (필요: {ResourceManager.TrapPlaceStoneCost})");
                    }
                }
                else if (_isDoorRepairMode)
                {
                    // 자원 소모 재설치(2026-08-22 사용자 요청 "자원을 소모해서 설치하게 다시 바꿔줘")
                    // — 함정과 동일하게, 자원이 부족하면 배치를 취소하지 않고 모드만 유지한다.
                    if (_resourceManager != null && _resourceManager.TryConsumeResource(ResourceType.Stone, ResourceManager.DoorRepairStoneCost))
                    {
                        _gameSession.RebuildDoorAt(gridPos);
                        ExitMode();
                    }
                    else
                    {
                        LogHelper.Warning(LogHelper.GAME, $"돌이 부족하여 문을 재설치할 수 없습니다. (필요: {ResourceManager.DoorRepairStoneCost})");
                    }
                }
            }
        }
    }
}
