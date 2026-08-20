using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Haare.Util.Logger;

// 오브젝트(O)/함정(P) 배치 모드 — 빌드 모드와 동일한 고스트 방식(2026-07-22/23, 사용자 요청). 둘 다
// 고스트 하나를 공유하고, 모양/색만 종류에 따라 바꿔 쓴다. 몬스터(M) 배치 모드는 건축물·자원·유닛
// 생산 MVP(2026-07-27)로 완전히 대체되어 삭제됨. 코어(C) 배치는 03문서 7-3장(2026-07-27 신규)
// 테스트용으로 같은 방식에 얹혀 있다.
//
// InputManager 비대화를 막기 위해 InputManager.cs에서 분리했다(2026-08-20, BuildPlacementController
// 와 동일한 리팩토링). 모드 간 배타 진입(빌드 모드 종료 등)은 BuildPlacementController를 직접
// 참조하지 않고 InputManager.Update()가 조율한다.
public class ObjectPlacementController
{
    public bool IsActive => _isObjectPlaceMode || _isTrapPlaceMode || _isCorePlaceMode;

    private readonly GameSession _gameSession;
    private readonly UnitGenerate _unitGenerate;
    private readonly ResourceManager _resourceManager;
    private readonly PlacementGhost _ghost = new PlacementGhost("PlacementGhost");

    private bool _isObjectPlaceMode;
    private bool _isTrapPlaceMode;
    private bool _isCorePlaceMode;

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
        _isCorePlaceMode = false;
        _isObjectPlaceMode = true;

        // 코어(루팅 오브젝트) 아트 스프라이트 배정(사용자 요청, 2026-07-23) — 이전엔 벽 타일을 임시로 썼다.
        _ghost.Show(Resources.Load<Sprite>("obj/core"));
        LogHelper.Log(LogHelper.GAME, "오브젝트 배치 모드 진입 (좌클릭: 생성, 우클릭: 취소)");
    }

    public void EnterTrapMode()
    {
        if (_isTrapPlaceMode) return;
        _isObjectPlaceMode = false;
        _isCorePlaceMode = false;
        _isTrapPlaceMode = true;

        // 함정 아트 스프라이트 배정(사용자 요청, 2026-07-23) — 이전엔 세모 폴백 스프라이트를 썼다.
        _ghost.Show(Resources.Load<Sprite>("obj/trap"));
        LogHelper.Log(LogHelper.GAME, $"함정 배치 모드 진입 (돌 {ResourceManager.TrapPlaceStoneCost}개 소모, 좌클릭: 생성, 우클릭: 취소)");
    }

    // 03문서 7-3장(2026-07-27 신규) 테스트용 — 자원 소모 없이 즉시 배치(리더 전용 조사 흐름 검증 목적).
    public void EnterCoreMode()
    {
        if (_isCorePlaceMode) return;
        _isObjectPlaceMode = false;
        _isTrapPlaceMode = false;
        _isCorePlaceMode = true;

        _ghost.Show(Resources.Load<Sprite>("obj/core"));
        LogHelper.Log(LogHelper.GAME, "코어 배치 모드 진입 (테스트용, 좌클릭: 생성, 우클릭: 취소)");
    }

    public void ExitMode()
    {
        _isObjectPlaceMode = false;
        _isTrapPlaceMode = false;
        _isCorePlaceMode = false;
        _ghost.Hide();
    }

    public void Update(Vector3 floorOffset, int currentFloor)
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);

        // 이미 다른 오브젝트가 있거나(objectGrid) 벽/유닛으로 막혀있으면(IsAreaClear) 놓을 수 없다.
        bool canPlace = _gameSession != null && _unitGenerate != null
            && !_gameSession.objectGrid.ContainsKey(gridPos)
            && _unitGenerate.IsAreaClear(new Vector2Int(gridPos.x, gridPos.y), Vector2.one, currentFloor);

        _ghost.UpdatePosition(gridPos, floorOffset, canPlace);

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            ExitMode();
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && canPlace)
        {
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
            {
                if (_isObjectPlaceMode)
                {
                    _gameSession.SpawnLootObjectAt(gridPos);
                    ExitMode();
                }
                else if (_isTrapPlaceMode)
                {
                    // 돌 자원이 부족하면 배치를 취소하지 않고 모드를 유지 — 자원을 모은 뒤 같은 위치에
                    // 다시 시도할 수 있게 한다(배치 모드 자체는 우클릭으로만 취소).
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
                else if (_isCorePlaceMode)
                {
                    _gameSession.SpawnCoreAt(gridPos);
                    ExitMode();
                }
            }
        }
    }
}
