using UnityEngine;
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
    public bool IsActive => _isObjectPlaceMode || _isTrapPlaceMode || _isDoorRepairMode || _isWallConvertMode || _isDummyBuildingMode;
    // BottomMenuBar의 개별 서브버튼(오브젝트/함정/문 재설치) 하이라이트·토글용(2026-08-20) —
    // BuildPlacementController.IsUnitBuildModeActive/IsResourceBuildModeActive와 동일한 이유.
    public bool IsObjectModeActive => _isObjectPlaceMode;
    public bool IsTrapModeActive => _isTrapPlaceMode;
    public bool IsDoorRepairModeActive => _isDoorRepairMode;
    // 2026-08-24 debug 전용 — 바닥 타일을 벽으로 전환하는 모드(BottomMenuBar debug 서브메뉴).
    public bool IsWallConvertModeActive => _isWallConvertMode;
    // 더미 건물 배치 모드(2026-08-25 debug 전용, 사용자 요청 "기존에 쓰던 두 스프라이트는 디버그용
    // 툴에다가 배치할건데... 아무런 기능도 하지 않는, 건물 판정만 있는 더미 건물") — 어느 더미 건물이
    // 켜져 있는지는 표시용 이름(ActiveDummyBuildingName)으로 구분한다(같은 그룹 버튼 두 개를
    // BottomMenuBar가 각각 따로 하이라이트해야 하므로).
    public bool IsDummyBuildingModeActive => _isDummyBuildingMode;
    public string ActiveDummyBuildingName => _dummyBuildingDisplayName;

    // 2026-08-24 debug 전용 토글(사용자 요청 "함정을 무제한 설치하는 기능을 debug에 넣어줘") — 켜져
    // 있으면 돌 자원을 소모하지 않고, 배치 후에도 모드를 유지해 연속으로 계속 배치할 수 있다.
    public bool DebugUnlimitedTrapPlacement;

    private readonly GameSession _gameSession;
    private readonly UnitGenerate _unitGenerate;
    private readonly ResourceManager _resourceManager;
    private readonly BuildingManager _buildingManager;
    private readonly PlacementGhost _ghost = new PlacementGhost("PlacementGhost");

    private bool _isObjectPlaceMode;
    private bool _isTrapPlaceMode;
    private bool _isDoorRepairMode;
    private bool _isWallConvertMode;
    private bool _isDummyBuildingMode;

    private Sprite _coreSprite;
    private Sprite _trapSprite;
    private Sprite _doorOpenSprite;
    private Sprite _wallSprite;
    private Sprite _dummyBuildingSprite;
    private string _dummyBuildingDisplayName;

    private Sprite CoreSprite => SpriteCache.GetOrLoad(ref _coreSprite, "obj/core");
    private Sprite TrapSprite => SpriteCache.GetOrLoad(ref _trapSprite, "obj/trap");
    private Sprite DoorOpenSprite => SpriteCache.GetOrLoad(ref _doorOpenSprite, "obj/door_open");
    // MapRandering.BuildTileCache와 동일한 리소스 경로 — 실제 벽 타일과 같은 아트로 미리보기.
    private Sprite WallSprite => SpriteCache.GetOrLoad(ref _wallSprite, "Tile_StoneWall");

    public ObjectPlacementController(GameSession gameSession, UnitGenerate unitGenerate, ResourceManager resourceManager, BuildingManager buildingManager)
    {
        _gameSession = gameSession;
        _unitGenerate = unitGenerate;
        _resourceManager = resourceManager;
        _buildingManager = buildingManager;
    }

    public void EnterObjectMode()
    {
        if (_isObjectPlaceMode) return;
        _isTrapPlaceMode = false;
        _isDoorRepairMode = false;
        _isWallConvertMode = false;
        _isDummyBuildingMode = false;
        _isObjectPlaceMode = true;

        // 코어(루팅 오브젝트) 아트 스프라이트 배정(사용자 요청, 2026-07-23) — 이전엔 벽 타일을 임시로 썼다.
        _ghost.Show(CoreSprite);
        LogHelper.Log(LogHelper.GAME, "오브젝트 배치 모드 진입 (우클릭: 생성)");
    }

    public void EnterTrapMode()
    {
        if (_isTrapPlaceMode) return;
        _isObjectPlaceMode = false;
        _isDoorRepairMode = false;
        _isWallConvertMode = false;
        _isDummyBuildingMode = false;
        _isTrapPlaceMode = true;

        // 함정 아트 스프라이트 배정(사용자 요청, 2026-07-23) — 이전엔 세모 폴백 스프라이트를 썼다.
        _ghost.Show(TrapSprite);
        string costNotice = DebugUnlimitedTrapPlacement ? "debug 무제한 설치 — 자원 소모 없음" : $"돌 {ResourceManager.TrapPlaceStoneCost}개 소모";
        LogHelper.Log(LogHelper.GAME, $"함정 배치 모드 진입 ({costNotice}, 우클릭: 생성)");
    }

    // 2026-08-24 debug 전용 — 바닥 타일을 벽으로 전환하는 모드 진입.
    public void EnterWallConvertMode()
    {
        if (_isWallConvertMode) return;
        _isObjectPlaceMode = false;
        _isTrapPlaceMode = false;
        _isDoorRepairMode = false;
        _isDummyBuildingMode = false;
        _isWallConvertMode = true;

        _ghost.Show(WallSprite);
        LogHelper.Log(LogHelper.GAME, "debug 벽 변환 모드 진입 (우클릭: 바닥 타일을 벽으로 전환)");
    }

    // 문도 방어건물화(기초문서.md 피드백, 2026-08-22) — 파괴된 문을 원래 게이트 자리에만 재설치.
    public void EnterDoorRepairMode()
    {
        if (_isDoorRepairMode) return;
        _isObjectPlaceMode = false;
        _isTrapPlaceMode = false;
        _isWallConvertMode = false;
        _isDummyBuildingMode = false;
        _isDoorRepairMode = true;

        _ghost.Show(DoorOpenSprite);
        LogHelper.Log(LogHelper.GAME, $"문 재설치 모드 진입 (돌 {ResourceManager.DoorRepairStoneCost}개 소모, 파괴된 문 자리만 선택 가능, 우클릭: 설치)");
    }

    // 더미 건물 배치 모드(2026-08-25 debug 전용, 사용자 요청 "기존에 쓰던 두 스프라이트는 디버그용
    // 툴에다가 배치할건데... 아무런 기능도 하지 않는, 건물 판정만 있는 더미 건물") — resourcePath는
    // Resources.Load 경로("obj/building"/"obj/resource_building", 건물 개편 전 유닛/자원 생산 건물이
    // 쓰던 스프라이트), displayName은 BuildingControlPanel/로그에 쓰일 표시 이름이다. 같은 그룹의 다른
    // 더미 스프라이트로 전환할 때도(이미 이 모드여도) 다시 진입시켜 스프라이트/이름을 갱신한다.
    public void EnterDummyBuildingMode(string resourcePath, string displayName)
    {
        _isObjectPlaceMode = false;
        _isTrapPlaceMode = false;
        _isDoorRepairMode = false;
        _isWallConvertMode = false;
        _isDummyBuildingMode = true;

        _dummyBuildingSprite = Resources.Load<Sprite>(resourcePath);
        _dummyBuildingDisplayName = displayName;
        _ghost.Show(_dummyBuildingSprite);
        LogHelper.Log(LogHelper.GAME, $"더미 건물({displayName}) 배치 모드 진입 (기능 없음, 건물 판정만, 우클릭: 설치)");
    }

    public void ExitMode()
    {
        _isObjectPlaceMode = false;
        _isTrapPlaceMode = false;
        _isDoorRepairMode = false;
        _isWallConvertMode = false;
        _isDummyBuildingMode = false;
        _ghost.Hide();
    }

    public void Update(Vector3 floorOffset, int currentFloor)
    {
        Vector2 mousePos = GameInputScheme.PointerScreenPos;
        Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);

        // 이미 다른 오브젝트가 있거나(objectGrid) 벽/유닛으로 막혀있으면(IsAreaClear) 놓을 수 없다.
        // 문 재설치 모드는 그 대신 "원래 게이트(복도) 자리"인지만(기초문서.md 피드백, 2026-08-22
        // "복도 자리에만 설치 가능"), 벽 변환 모드(2026-08-24 debug)는 "아직 벽이 아닌 타일"인지만,
        // 더미 건물 모드(2026-08-25 debug)는 BuildingManager.CanInstallAt(다른 건물과 동일한 건물
        // 판정 규칙)인지만 확인한다 — 다 자유로운 위치 배치가 아니다.
        bool canPlace = _gameSession != null && _unitGenerate != null
            && !_gameSession.objectGrid.ContainsKey(gridPos)
            && (_isDoorRepairMode ? _gameSession.IsRepairableDoorTile(gridPos)
                : _isWallConvertMode ? _gameSession.IsFloorTileConvertibleToWall(gridPos)
                // 더미 건물은 점령 여부 무관, 바닥 타일이면 어디든 설치 가능(2026-08-25 사용자 요청).
                : _isDummyBuildingMode ? (_buildingManager != null && _buildingManager.CanInstallAt(gridPos, Vector2Int.one, requireOwnership: false))
                : _unitGenerate.IsAreaClear(new Vector2Int(gridPos.x, gridPos.y), Vector2.one, currentFloor));

        _ghost.UpdatePosition(gridPos, floorOffset, canPlace);

        // 우클릭 취소는 없앴다(2026-08-20, 사용자 요청) — BuildPlacementController.Update()와 동일한
        // 이유. 취소는 BottomMenuBar에서 다른 메뉴로 전환하거나 같은 서브 버튼을 다시 눌러야 한다.
        // 설치 확정 입력은 2026-08-22(사용자 요청 "좌클릭은 선택, 우클릭은 실행으로 두자")로 좌클릭에서
        // 우클릭으로 옮겼다.
        if (GameInputScheme.SecondaryDown && canPlace)
        {
            if (!GUIMouseUtil.IsPointerOverAnyPanel())
            {
                if (_isObjectPlaceMode)
                {
                    _gameSession.SpawnLootObjectAt(gridPos);
                    ExitMode();
                }
                else if (_isTrapPlaceMode)
                {
                    // debug 무제한 설치(2026-08-24)면 자원 소모 자체를 건너뛰고, 배치 후에도 모드를
                    // 유지해 계속 이어서 설치할 수 있게 한다. 평소엔 돌 자원이 부족하면 배치를 취소하지
                    // 않고 모드를 유지 — 자원을 모은 뒤 같은 위치에 다시 시도할 수 있게 한다.
                    if (DebugUnlimitedTrapPlacement)
                    {
                        _gameSession.SpawnTrapAt(gridPos);
                    }
                    else
                    {
                        PlacementResourceHelper.OnConsumeResult(
                            _resourceManager != null && _resourceManager.TryConsumeResource(ResourceType.Stone, ResourceManager.TrapPlaceStoneCost),
                            () => { _gameSession.SpawnTrapAt(gridPos); ExitMode(); },
                            $"돌이 부족하여 함정을 배치할 수 없습니다. (필요: {ResourceManager.TrapPlaceStoneCost})");
                    }
                }
                else if (_isWallConvertMode)
                {
                    // debug 도구라 배치 후에도 모드를 유지 — 연속으로 여러 타일을 벽으로 바꿀 수 있다.
                    _gameSession.DebugConvertFloorTileToWall(gridPos);
                }
                else if (_isDoorRepairMode)
                {
                    // 자원 소모 재설치(2026-08-22 사용자 요청 "자원을 소모해서 설치하게 다시 바꿔줘")
                    // — 함정과 동일하게, 자원이 부족하면 배치를 취소하지 않고 모드만 유지한다.
                    PlacementResourceHelper.OnConsumeResult(
                        _resourceManager != null && _resourceManager.TryConsumeResource(ResourceType.Stone, ResourceManager.DoorRepairStoneCost),
                        () => { _gameSession.RebuildDoorAt(gridPos); ExitMode(); },
                        $"돌이 부족하여 문을 재설치할 수 없습니다. (필요: {ResourceManager.DoorRepairStoneCost})");
                }
                else if (_isDummyBuildingMode)
                {
                    // debug 도구라 자원 소모 없이, 배치 후에도 모드를 유지해 연속으로 여러 개 설치할 수
                    // 있다(바닥 → 벽 변환 모드와 동일한 관례).
                    _buildingManager?.InstallDummyBuilding(gridPos, _dummyBuildingSprite, _dummyBuildingDisplayName);
                }
            }
        }
    }
}
