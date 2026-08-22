using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Haare.Util.Logger;

// 건축물·자원·유닛 생산 MVP(2026-07-27) — 빌드 모드(고스트 프리팹) 컨트롤러. B키=유닛 생산 건물,
// V키=자원 생산 건물. 같은 고스트/스프라이트를 공유하고 플래그로만 구분한다.
//
// InputManager 비대화를 막기 위해 InputManager.cs에서 분리했다(2026-08-20) — 몬스터 배치 모드
// (MonsterPlacementController)를 먼저 분리하면서 발견한 것과 같은 문제(기존 빌드/오브젝트 배치
// 모드도 전부 InputManager 안에 그대로 있었음)를 같은 방식으로 정리한 것. 모드 간 배타 진입(예:
// 빌드 모드 진입 시 오브젝트 배치 모드 종료)은 이 클래스가 직접 하지 않고 InputManager.Update()가
// 조율한다 — ObjectPlacementController와 서로를 직접 참조하지 않게(순환 참조 방지) 하기 위함.
public class BuildPlacementController
{
    public bool IsActive => _isBuildMode || _isResourceBuildMode;
    // BottomMenuBar가 "유닛 생산 건물"/"자원 생산 건물" 서브버튼을 각각 따로 하이라이트/토글하는 데
    // 쓴다(2026-08-20, 사용자 신고 "자원 생산 건물과 유닛 생산 건물이 다중 선택되어버리는 UI 버그" —
    // 이전엔 합쳐진 IsActive만 있어서 둘 다 항상 같이 켜진 것처럼 보였다).
    public bool IsUnitBuildModeActive => _isBuildMode;
    public bool IsResourceBuildModeActive => _isResourceBuildMode;

    private readonly BuildingManager _buildingManager;
    private readonly ResourceManager _resourceManager;
    private readonly PlacementGhost _ghost = new PlacementGhost("GhostBuilding");

    private bool _isBuildMode;
    private bool _isResourceBuildMode;
    private List<ProductionRule> _currentProductionRules;
    private Sprite _currentBuildSprite;

    public BuildPlacementController(BuildingManager buildingManager, ResourceManager resourceManager)
    {
        _buildingManager = buildingManager;
        _resourceManager = resourceManager;
    }

    public void EnterBuildMode()
    {
        if (_isBuildMode) return;
        _isResourceBuildMode = false;
        _isBuildMode = true;

        EnsureProductionRules();
        _currentBuildSprite = Resources.Load<Sprite>("obj/building");
        _ghost.Show(_currentBuildSprite);
        LogHelper.Log(LogHelper.GAME, $"유닛 생산 건물 배치 모드 진입 (돌 {ResourceManager.UnitBuildingStoneCost} 소모)");
    }

    public void EnterResourceBuildMode()
    {
        if (_isResourceBuildMode) return;
        _isBuildMode = false;
        _isResourceBuildMode = true;

        // 사용자 요청(2026-07-27) — V키(자원 생산 건물)는 별도 스프라이트(obj/resource_building)를 쓴다.
        // B키(유닛 생산 건물)는 기존 obj/building 그대로.
        _currentBuildSprite = Resources.Load<Sprite>("obj/resource_building");
        _ghost.Show(_currentBuildSprite);
        LogHelper.Log(LogHelper.GAME, $"자원 생산 건물 배치 모드 진입 (돌 {ResourceManager.ResourceBuildingStoneCost} 소모)");
    }

    public void ExitMode()
    {
        _isBuildMode = false;
        _isResourceBuildMode = false;
        _ghost.Hide();
        LogHelper.Log(LogHelper.GAME, "Exited Build Mode");
    }

    public void Update(Vector3 floorOffset, int currentFloor)
    {
        Vector2 mousePos = GameInputScheme.PointerScreenPos;
        Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);

        _ghost.UpdatePosition(gridPos, floorOffset, _buildingManager.CanInstallAt(gridPos));

        // 우클릭 취소는 없앴다(2026-08-20, 사용자 요청 "우클릭 취소 없애고, 오직 메뉴 바꾸기 혹은 메뉴
        // 다시 클릭으로 바꿀 수 있게") — 취소는 BottomMenuBar에서 다른 메뉴로 전환하거나 같은 서브
        // 버튼을 다시 눌러야만 가능하다(InputManager.ExitActivePlacementMode 경유). 설치 확정 입력은
        // 2026-08-22(사용자 요청 "좌클릭은 선택, 우클릭은 실행으로 두자. 설치나 명령 전반 모두 포함")로
        // 좌클릭에서 우클릭으로 옮겼다.
        if (GameInputScheme.SecondaryDown)
        {
            bool overUI = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                || (BottomMenuBar.Instance != null && BottomMenuBar.Instance.IsMouseOverUI())
                || (DebugInfoPanel.Instance != null && DebugInfoPanel.Instance.IsMouseOverUI());
            if (!overUI)
            {
                TryInstallBuilding(gridPos);
            }
        }
    }

    // 플레이어(몬스터 진영) 생산 건물이 실제로 뽑을 수 있는 유닛 목록을 준비한다. GameSession의 시작방
    // 자동 배치와 같은 팩토리(ProductionRule.CreateDefaultPlayerUnitRules)를 써서 두 경로가 어긋나지
    // 않게 한다.
    private void EnsureProductionRules()
    {
        if (_currentProductionRules != null) return;
        _currentProductionRules = ProductionRule.CreateDefaultPlayerUnitRules();
    }

    private void TryInstallBuilding(Vector3Int gridPos)
    {
        if (!_buildingManager.CanInstallAt(gridPos))
        {
            LogHelper.Warning(LogHelper.GAME, "장애물이 있거나 설치할 수 없는 지형입니다.");
            NoticeCenter.Instance?.PushMomentary("장애물이 있거나 설치할 수 없는 지형입니다.", NoticeCenter.WarningColor);
            return;
        }

        if (_isResourceBuildMode)
        {
            if (_resourceManager.TryConsumeResource(ResourceType.Stone, ResourceManager.ResourceBuildingStoneCost))
            {
                _buildingManager.InstallResourceBuilding(gridPos, _currentBuildSprite);
                ExitMode();
            }
            else
            {
                LogHelper.Warning(LogHelper.GAME, $"돌이 부족하여 자원 생산 건물을 지을 수 없습니다. (필요: {ResourceManager.ResourceBuildingStoneCost})");
            }
            return;
        }

        if (_resourceManager.TryConsumeResource(ResourceType.Stone, ResourceManager.UnitBuildingStoneCost))
        {
            _buildingManager.InstallProductionBuilding(gridPos, _currentProductionRules, _currentBuildSprite);
            ExitMode();
        }
        else
        {
            LogHelper.Warning(LogHelper.GAME, $"돌이 부족하여 유닛 생산 건물을 지을 수 없습니다. (필요: {ResourceManager.UnitBuildingStoneCost})");
        }
    }
}
