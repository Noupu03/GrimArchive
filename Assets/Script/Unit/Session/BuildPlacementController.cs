using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Haare.Util.Logger;

// 시설 건설 배치 모드(고스트 프리팹) 컨트롤러 — 명세서 v0.2 3·16항 기준(건설 명령 시 생명 에너지 선점 + 건설 예정지 생성, 현장 작업자 착공).
// 모드 간 배타 진입은 InputManager.Update()가 조율한다(ObjectPlacementController와 순환 참조를 피하기 위함).
public class BuildPlacementController
{
    public bool IsActive => _activeKind.HasValue || _movingPlan != null || _relocating != null;

    // 건설 패널(홀로그램 선택 UI)이 [위치 변경]을 요청할 때 쓰는 접근점 - BuildingControlPanel.Instance와 같은 관례.
    public static BuildPlacementController Current { get; private set; }
    public FacilityKind? ActiveFacilityKind => _activeKind;

    public bool IsUnitBuildModeActive => _activeKind == FacilityKind.UnitProduction;
    public bool IsResourceBuildModeActive => _activeKind == FacilityKind.LifeEnergy;
    public bool IsRefineryBuildModeActive => _activeKind == FacilityKind.CorpseRefinery;
    public bool IsResearchBuildModeActive => _activeKind == FacilityKind.Research;

    private readonly BuildingManager _buildingManager;
    private readonly ResourceManager _resourceManager;
    private readonly PlacementGhost _ghost = new PlacementGhost("GhostBuilding");

    private FacilityKind? _activeKind;
    private int _buildRotation; // 새 건설 미리보기의 방향(R로 90도 회전) - 생산시설은 정면 출현 구역이 이 방향을 따른다
    private readonly FrontZoneRenderer _zoneGhost = new FrontZoneRenderer("GhostFrontZone");
    private List<ProductionRule> _currentProductionRules;
    private Sprite _currentBuildSprite;

    public BuildPlacementController(BuildingManager buildingManager, ResourceManager resourceManager)
    {
        _buildingManager = buildingManager;
        _resourceManager = resourceManager;
        Current = this;
    }

    public void EnterBuildMode() => EnterFacilityMode(FacilityKind.UnitProduction);
    public void EnterResourceBuildMode() => EnterFacilityMode(FacilityKind.LifeEnergy);
    public void EnterRefineryBuildMode() => EnterFacilityMode(FacilityKind.CorpseRefinery);
    public void EnterResearchBuildMode() => EnterFacilityMode(FacilityKind.Research);

    public void EnterFacilityMode(FacilityKind kind)
    {
        if (_activeKind == kind) return;
        _movingPlan = null;
        _buildRotation = 0;
        _activeKind = kind;
        _buildingManager.BeginContinuousSession(); // 같은 종류의 미리보기를 유지하며 계속 건설을 지정한다(건설 명세 2-1)

        if (kind == FacilityKind.UnitProduction)
            EnsureProductionRules();

        string spriteKey = BuildingSpriteKey(kind);

        ShowBuildGhostAsync(spriteKey, () => _activeKind == kind).Forget();
        int cost = BuildingManager.GetBuildLifeEnergyCostForKind(kind);
        string name = BuildingManager.GetDisplayNameForKind(kind);
        LogHelper.Log(LogHelper.GAME, $"{name} 배치 모드 진입 (생명 에너지 {cost} 선점)");
    }

    private static string BuildingSpriteKey(FacilityKind kind)
        => kind == FacilityKind.UnitProduction ? AssetKeys.UnitBuildingSprite : AssetKeys.ResourceBuildingSprite;

    private static bool RotatePressedThisFrame()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.rKey.wasPressedThisFrame;
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
        _movingPlan = null;
        _relocating = null;
        _zoneGhost.Hide();
        _buildingManager.EndContinuousSession(); // 미리보기만 끝나고 이미 확정한 계획·담당자는 유지된다
        _ghost.Hide();
        LogHelper.Log(LogHelper.GAME, "배치 모드를 종료했습니다.");
    }

    private Vector2Int CurrentFootprint => _activeKind.HasValue
        ? BuildingManager.GetFootprintForKind(_activeKind.Value)
        : Vector2Int.one;

    private BuildingData _movingPlan;

    // 시작 전 건설 계획(홀로그램)의 위치 변경 - 새 위치 검사에 성공하면 계획·예약을 옮기고 배정 작업자의 목적지를 갱신한다(건설 명세 2-1-표1).
    public void EnterPlanMoveMode(BuildingData plan)
    {
        if (!_buildingManager.CanMovePlan(plan)) return;
        _activeKind = null;
        _buildingManager.EndContinuousSession();
        _movingPlan = plan;
        BuildingData captured = plan;
        ShowBuildGhostAsync(BuildingSpriteKey(plan.Kind), () => _movingPlan == captured).Forget();
        NoticeCenter.Instance?.PushMomentary($"{plan.DisplayName}의 새 위치를 우클릭으로 지정하세요. (ESC로 취소)", NoticeCenter.QuestCompleteColor);
    }

    private void UpdatePlanMove(Vector3 floorOffset, int currentFloor)
    {
        if (!_buildingManager.CanMovePlan(_movingPlan)) { ExitMode(); return; }

        Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(GameInputScheme.PointerScreenPos, floorOffset, currentFloor);
        bool ok = _buildingManager.CheckInstallAt(gridPos, _movingPlan.Footprint, requireOwnership: true, kind: _movingPlan.Kind, ignore: _movingPlan) == BuildBlock.None;
        _ghost.UpdatePosition(gridPos, floorOffset, ok, _movingPlan.Footprint);

        if (GameInputScheme.SecondaryDown && !GUIMouseUtil.IsPointerOverAnyPanel())
        {
            if (_buildingManager.TryMovePlan(_movingPlan, gridPos)) ExitMode();
        }
    }

    private BuildingData _relocating;
    private int _relocRotation;

    // 시설 이동(위치 변경·제자리 회전)의 목적지 지정 - 분해·운반·재설치는 작업자가 맡는다. 이미 이동 중이면 목적지(와 방향)만 바꾼다(건설 명세 2-6·2-7-표2).
    public void EnterRelocationMode(BuildingData b)
    {
        if (b == null) return;
        bool changing = b.ActiveRelocation != null;
        bool unfinished = !b.IsFormallyInstalled && b.ActiveFieldTask == FacilityFieldTaskKind.Construction
            && b.ActiveConstructionOrUpgrade != null && b.ActiveConstructionOrUpgrade.HasStartedWork;
        if (!changing && !unfinished && !(b.IsFormallyInstalled && b.ActiveFieldTask == FacilityFieldTaskKind.None)) return;

        _activeKind = null;
        _movingPlan = null;
        _buildingManager.EndContinuousSession();
        _relocating = b;
        _relocRotation = changing ? b.PendingRotation : b.Rotation;
        BuildingData captured = b;
        ShowBuildGhostAsync(BuildingSpriteKey(b.Kind), () => _relocating == captured).Forget();
        NoticeCenter.Instance?.PushMomentary($"{b.DisplayName}의 새 위치를 우클릭으로 지정하세요. (R: 90도 회전, ESC: 취소)", NoticeCenter.QuestCompleteColor);
    }

    private void UpdateRelocation(Vector3 floorOffset, int currentFloor)
    {
        BuildingData b = _relocating;
        if (b == null) return;
        if (b.ActiveRelocation == null && !(b.IsFormallyInstalled || b.ActiveFieldTask == FacilityFieldTaskKind.Construction)) { ExitMode(); return; }

        if (RotatePressedThisFrame()) _relocRotation = ConstructionRules.NormalizeRotation(_relocRotation + 1);

        Vector2Int fp = ConstructionRules.RotatedFootprint(b.Footprint, _relocRotation - b.Rotation);
        Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(GameInputScheme.PointerScreenPos, floorOffset, currentFloor);
        bool ok = _buildingManager.CheckInstallAt(gridPos, fp, requireOwnership: true, kind: b.Kind, ignore: b) == BuildBlock.None;
        _ghost.UpdatePosition(gridPos, floorOffset, ok, fp);

        if (GameInputScheme.SecondaryDown && !GUIMouseUtil.IsPointerOverAnyPanel())
        {
            bool success = b.ActiveRelocation != null
                ? _buildingManager.TryChangeRelocationDestination(b, gridPos, _relocRotation)
                : _buildingManager.TryStartFacilityRelocation(b, gridPos, _relocRotation);
            if (success) ExitMode();
        }
    }

    public void Update(Vector3 floorOffset, int currentFloor)
    {
        if (_relocating != null) { UpdateRelocation(floorOffset, currentFloor); return; }
        if (_movingPlan != null) { UpdatePlanMove(floorOffset, currentFloor); return; }
        if (!_activeKind.HasValue) return;

        Vector2 mousePos = GameInputScheme.PointerScreenPos;
        Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);
        FacilityKind kind = _activeKind.Value;
        if (RotatePressedThisFrame()) _buildRotation = ConstructionRules.NormalizeRotation(_buildRotation + 1);
        Vector2Int footprint = ConstructionRules.RotatedFootprint(CurrentFootprint, _buildRotation);

        bool canInstall = _buildingManager.CheckInstallAt(gridPos, footprint, requireOwnership: true, kind: kind, rotation: _buildRotation) == BuildBlock.None;
        _ghost.UpdatePosition(gridPos, floorOffset, canInstall, footprint);
        // 생산시설은 정면 출현 구역까지 함께 보여 건설이 필요한 전체 영역을 알린다(건설 명세 2-13).
        if (kind == FacilityKind.UnitProduction)
            _zoneGhost.Show(ConstructionRules.FrontZoneRect(new Vector2Int(gridPos.x, gridPos.y), footprint, _buildRotation, BuildingManager.FrontZoneDepth), gridPos.z, floorOffset, canInstall ? new Color(0.35f, 0.85f, 0.55f, 0.3f) : new Color(0.95f, 0.25f, 0.25f, 0.3f));
        else _zoneGhost.Hide();

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
        if (_buildingManager.TryOrderConstruction(kind, gridPos, _currentBuildSprite, rules, out _, out string failMsg, _buildRotation))
        {
            return; // 미리보기를 유지해 Shift 없이 계속 지정한다 - ESC나 다른 UI 선택이 끝낸다(건설 명세 2-1)
        }

        if (!string.IsNullOrEmpty(failMsg))
        {
            LogHelper.Warning(LogHelper.GAME, failMsg);
            NoticeCenter.Instance?.PushMomentary(failMsg, NoticeCenter.WarningColor);
        }
    }
}
