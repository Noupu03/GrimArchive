using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Haare.Util.Logger;

// 시설 건설 배치 모드(고스트 프리팹) 컨트롤러 — 명세서 v0.2 3·16항 기준(건설 명령 시 생명 에너지 선점 + 건설 예정지 생성, 현장 작업자 착공).
// 모드 간 배타 진입은 InputManager.Update()가 조율한다(ObjectPlacementController와 순환 참조를 피하기 위함).
public class BuildPlacementController
{
    public bool IsActive => _activeKind.HasValue;
    public FacilityKind? ActiveFacilityKind => _activeKind;

    public bool IsUnitBuildModeActive => _activeKind == FacilityKind.UnitProduction;
    public bool IsResourceBuildModeActive => _activeKind == FacilityKind.LifeEnergy;
    public bool IsRefineryBuildModeActive => _activeKind == FacilityKind.CorpseRefinery;
    public bool IsResearchBuildModeActive => _activeKind == FacilityKind.Research;

    private readonly BuildingManager _buildingManager;
    private readonly ResourceManager _resourceManager;
    private readonly PlacementGhost _ghost = new PlacementGhost("GhostBuilding");

    private FacilityKind? _activeKind;
    private List<ProductionRule> _currentProductionRules;
    private Sprite _currentBuildSprite;

    public BuildPlacementController(BuildingManager buildingManager, ResourceManager resourceManager)
    {
        _buildingManager = buildingManager;
        _resourceManager = resourceManager;
    }

    public void EnterBuildMode() => EnterFacilityMode(FacilityKind.UnitProduction);
    public void EnterResourceBuildMode() => EnterFacilityMode(FacilityKind.LifeEnergy);
    public void EnterRefineryBuildMode() => EnterFacilityMode(FacilityKind.CorpseRefinery);
    public void EnterResearchBuildMode() => EnterFacilityMode(FacilityKind.Research);

    public void EnterFacilityMode(FacilityKind kind)
    {
        if (_activeKind == kind) return;
        _activeKind = kind;

        if (kind == FacilityKind.UnitProduction)
            EnsureProductionRules();

        string spriteKey = kind == FacilityKind.UnitProduction
            ? AssetKeys.UnitBuildingSprite
            : AssetKeys.ResourceBuildingSprite;

        ShowBuildGhostAsync(spriteKey, () => _activeKind == kind).Forget();
        int cost = BuildingManager.GetBuildLifeEnergyCostForKind(kind);
        string name = BuildingManager.GetDisplayNameForKind(kind);
        LogHelper.Log(LogHelper.GAME, $"{name} 배치 모드 진입 (생명 에너지 {cost} 선점)");
    }

    // 스프라이트 로드가 끝나야 고스트가 보이고 설치할 수 있다 — 그 사이 다른 모드로 바뀌었으면 아무것도 하지 않는다.
    private async UniTaskVoid ShowBuildGhostAsync(string spriteKey, Func<bool> stillWanted)
    {
        _currentBuildSprite = null;
        Sprite sprite = await _ghost.ShowAsync(spriteKey, stillWanted);
        if (sprite != null) _currentBuildSprite = sprite;
    }

    public void ExitMode()
    {
        _activeKind = null;
        _ghost.Hide();
        LogHelper.Log(LogHelper.GAME, "배치 모드를 종료했습니다.");
    }

    private Vector2Int CurrentFootprint => _activeKind.HasValue
        ? BuildingManager.GetFootprintForKind(_activeKind.Value)
        : Vector2Int.one;

    public void Update(Vector3 floorOffset, int currentFloor)
    {
        if (!_activeKind.HasValue) return;

        Vector2 mousePos = GameInputScheme.PointerScreenPos;
        Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);
        Vector2Int footprint = CurrentFootprint;
        FacilityKind kind = _activeKind.Value;

        bool canInstall = _buildingManager.CheckInstallAt(gridPos, footprint, requireOwnership: true, kind: kind) == BuildBlock.None;
        _ghost.UpdatePosition(gridPos, floorOffset, canInstall, footprint);

        // "좌클릭은 선택, 우클릭은 실행" 관례에 따라 우클릭으로 건설 예약을 확정한다.
        if (GameInputScheme.SecondaryDown)
        {
            if (!GUIMouseUtil.IsPointerOverAnyPanel())
            {
                TryOrderBuilding(kind, gridPos);
            }
        }
    }

    private void EnsureProductionRules()
    {
        if (_currentProductionRules != null) return;
        _currentProductionRules = ProductionRule.CreateDefaultPlayerUnitRules();
    }

    private void TryOrderBuilding(FacilityKind kind, Vector3Int gridPos)
    {
        if (_currentBuildSprite == null) return;

        List<ProductionRule> rules = kind == FacilityKind.UnitProduction ? _currentProductionRules : null;
        if (_buildingManager.TryOrderConstruction(kind, gridPos, _currentBuildSprite, rules, out _, out string failMsg))
        {
            ExitMode();
            return;
        }

        if (!string.IsNullOrEmpty(failMsg))
        {
            LogHelper.Warning(LogHelper.GAME, failMsg);
            NoticeCenter.Instance?.PushMomentary(failMsg, NoticeCenter.WarningColor);
        }
    }
}
