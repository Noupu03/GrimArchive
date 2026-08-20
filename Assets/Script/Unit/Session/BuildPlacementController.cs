using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);

        _ghost.UpdatePosition(gridPos, floorOffset, _buildingManager.CanInstallAt(gridPos));

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            ExitMode();
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
