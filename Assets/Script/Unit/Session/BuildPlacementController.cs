using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Haare.Util.Logger;

// 시설 건설 배치 모드(고스트 프리팹) 컨트롤러 — 명세서 v0.2 3·16항 기준(건설 명령 시 생명 에너지 선점 + 건설 예정지 생성, 현장 작업자 착공).
// 모드 간 배타 진입은 InputManager.Update()가 조율한다(ObjectPlacementController와 순환 참조를 피하기 위함).
public class BuildPlacementController
{
    public bool IsActive => _activeKind.HasValue || _movingPlan != null || _relocating != null || _rotating != null;

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
        ClearModeState();
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

    // 위치·방향 선택 모드를 모두 끝낸다 - 새 모드로 들어갈 때도 먼저 부른다(안 끝내면 먼저 열려 있던 모드가 입력을 계속 가져간다).
    private void ClearModeState()
    {
        _activeKind = null;
        _movingPlan = null;
        _relocating = null;
        _rotating = null;
        _rotateHover = -1;
        _zoneGhost.Hide();
    }

    // 시설 정보창이 닫히거나 다른 대상으로 바뀌면 그 시설을 향한 위치·방향 선택(이동·위치 변경·회전)을 끝낸다.
    public void CancelModesFor(BuildingData b)
    {
        if (b != null && (_relocating == b || _movingPlan == b || _rotating == b)) ExitMode();
    }

    public void ExitMode()
    {
        ClearModeState();
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
        ClearModeState();
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
    private BuildingData _rotating;   // 제자리 회전의 정면 방향을 고르는 중인 시설
    private int _rotateHover = -1;    // 마우스가 올라간 방향 버튼(회전 값), 없으면 -1
    private readonly bool[] _rotateOk = new bool[4];
    private static readonly Color ZoneOkColor = new Color(0.35f, 0.85f, 0.55f, 0.3f);
    private static readonly Color ZoneBadColor = new Color(0.95f, 0.25f, 0.25f, 0.3f);

    // 시설 이동(위치 변경·제자리 회전)의 목적지 지정 - 분해·운반·재설치는 작업자가 맡는다. 이미 이동 중이면 목적지(와 방향)만 바꾼다(건설 명세 2-6·2-7-표2).
    public void EnterRelocationMode(BuildingData b)
    {
        if (b == null) return;
        bool changing = b.ActiveRelocation != null;
        bool unfinished = !b.IsFormallyInstalled && b.ActiveFieldTask == FacilityFieldTaskKind.Construction
            && b.ActiveConstructionOrUpgrade != null && b.ActiveConstructionOrUpgrade.HasStartedWork;
        if (!changing && !unfinished && !(b.IsFormallyInstalled && b.ActiveFieldTask == FacilityFieldTaskKind.None)) return;

        ClearModeState();
        _buildingManager.EndContinuousSession();
        _relocating = b;
        _relocRotation = changing ? b.PendingRotation : b.Rotation;
        BuildingData captured = b;
        ShowBuildGhostAsync(BuildingSpriteKey(b.Kind), () => _relocating == captured).Forget();
        string rotateHint = ConstructionRules.RotationMatters(b.Kind, b.Footprint) ? "R: 정면 방향 회전, " : string.Empty;
        NoticeCenter.Instance?.PushMomentary($"{b.DisplayName}의 새 위치를 우클릭으로 지정하세요. ({rotateHint}ESC: 취소)", NoticeCenter.QuestCompleteColor);
    }

    private void UpdateRelocation(Vector3 floorOffset, int currentFloor)
    {
        BuildingData b = _relocating;
        if (b == null) return;
        if (b.ActiveRelocation == null && !(b.IsFormallyInstalled || b.ActiveFieldTask == FacilityFieldTaskKind.Construction)) { ExitMode(); return; }

        bool rotatable = ConstructionRules.RotationMatters(b.Kind, b.Footprint); // 정사각형·정면 없는 시설은 R이 아무 일도 하지 않는다
        if (rotatable && RotatePressedThisFrame()) _relocRotation = ConstructionRules.NormalizeRotation(_relocRotation + 1);

        Vector2Int fp = ConstructionRules.RotatedFootprint(b.Footprint, _relocRotation - b.Rotation);
        Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(GameInputScheme.PointerScreenPos, floorOffset, currentFloor);
        bool ok = _buildingManager.CheckInstallAt(gridPos, fp, requireOwnership: true, kind: b.Kind, ignore: b, rotation: _relocRotation) == BuildBlock.None;
        _ghost.UpdatePosition(gridPos, floorOffset, ok, fp);
        // 방향이 눈에 보이도록 생산시설은 정면 출현 구역을 미리 보여 준다(건설 미리보기와 같다).
        if (b.Kind == FacilityKind.UnitProduction)
            _zoneGhost.Show(ConstructionRules.FrontZoneRect(new Vector2Int(gridPos.x, gridPos.y), fp, _relocRotation, BuildingManager.FrontZoneDepth), gridPos.z, floorOffset, ok ? ZoneOkColor : ZoneBadColor);
        else _zoneGhost.Hide();

        if (GameInputScheme.SecondaryDown && !GUIMouseUtil.IsPointerOverAnyPanel())
        {
            bool success = b.ActiveRelocation != null
                ? _buildingManager.TryChangeRelocationDestination(b, gridPos, _relocRotation)
                : _buildingManager.TryStartFacilityRelocation(b, gridPos, _relocRotation);
            if (success) ExitMode();
        }
    }

    // ── 제자리 회전: 정면 방향 버튼을 시설 둘레에 띄워 원하는 방향을 직접 고른다(배치모드의 유닛 회전과 같은 방식) ──
    // 방향 값은 0=아래, 1=왼쪽, 2=위, 3=오른쪽이다. 고르면 분해·재설치를 작업자가 맡는다(건설 명세 2-6, 방향을 정하는 입력은 명세에 없어 이 방식으로 정했다).
    private static readonly string[] RotateArrowLabels = { "↓", "←", "↑", "→" };

    public void EnterRotateMode(BuildingData b)
    {
        if (b == null || !ConstructionRules.RotationMatters(b.Kind, b.Footprint) || !_buildingManager.CanRotateNow(b)) return;
        ClearModeState();
        _buildingManager.EndContinuousSession();
        _ghost.Hide();
        _rotating = b;
        NoticeCenter.Instance?.PushMomentary($"{b.DisplayName}의 정면 방향을 고르세요. (ESC·우클릭: 취소)", NoticeCenter.QuestCompleteColor);
    }

    private static int FacingOf(BuildingData b) => b.ActiveRelocation != null ? b.PendingRotation : b.Rotation;

    // 시설 중심(OnGUI 좌표)과 방향 버튼을 놓을 반지름 - 카메라가 없으면 false.
    private bool TryGetRotateAnchor(BuildingData b, out Vector2 center, out float radius)
    {
        center = default;
        radius = 0f;
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 world = new Vector3(b.Position.x + b.Footprint.x / 2f, b.Position.y + b.Footprint.y / 2f, 0f) + _buildingManager.GetFloorOffset(b.Position.z);
        Vector3 sp = cam.WorldToScreenPoint(world);
        float pixelsPerTile = Mathf.Abs(cam.WorldToScreenPoint(world + Vector3.right).x - sp.x);
        center = new Vector2(sp.x, Screen.height - sp.y);
        radius = Mathf.Max(b.Footprint.x, b.Footprint.y) * 0.5f * pixelsPerTile + 30f;
        return true;
    }

    private static Rect RotateArrowRect(Vector2 center, float radius, int rotation)
    {
        Vector2Int d = ConstructionRules.FrontDirection(rotation);
        Vector2 p = center + new Vector2(d.x, -d.y) * radius;
        return new Rect(p.x - 14f, p.y - 14f, 28f, 28f);
    }

    private void UpdateRotate()
    {
        BuildingData b = _rotating;
        if (!_buildingManager.CanRotateNow(b) || GameInputScheme.SecondaryDown) { ExitMode(); return; }

        int current = FacingOf(b);
        bool anchored = TryGetRotateAnchor(b, out Vector2 center, out float radius);
        _rotateHover = -1;
        for (int r = 0; r < 4; r++)
        {
            Vector2Int fp = ConstructionRules.RotatedFootprint(b.Footprint, r - b.Rotation);
            _rotateOk[r] = r != current && _buildingManager.CheckInstallAt(b.Position, fp, requireOwnership: true, kind: b.Kind, ignore: b, rotation: r) == BuildBlock.None;
            if (anchored && _rotateHover < 0 && GUIMouseUtil.IsMouseOverRect(RotateArrowRect(center, radius, r))) _rotateHover = r;
        }

        // 방향 버튼에 마우스를 올리면 그 방향의 정면 출현 구역을 미리 보여 준다(미리보기는 즉시 돌린다 - 건설 명세 2-6).
        if (_rotateHover >= 0 && b.Kind == FacilityKind.UnitProduction)
        {
            Vector2Int fp = ConstructionRules.RotatedFootprint(b.Footprint, _rotateHover - b.Rotation);
            RectInt zone = ConstructionRules.FrontZoneRect(new Vector2Int(b.Position.x, b.Position.y), fp, _rotateHover, BuildingManager.FrontZoneDepth);
            _zoneGhost.Show(zone, b.Position.z, _buildingManager.GetFloorOffset(b.Position.z), _rotateOk[_rotateHover] ? ZoneOkColor : ZoneBadColor);
        }
        else _zoneGhost.Hide();
    }

    private void OnRotateChosen(BuildingData b, int rotation)
    {
        bool ok = b.ActiveRelocation != null
            ? _buildingManager.TryChangeRelocationDestination(b, b.Position, rotation)
            : _buildingManager.TryStartFacilityRelocation(b, b.Position, rotation);
        if (ok) ExitMode();
    }

    // 방향 선택 버튼과 방향 안내 - InputManager.OnGUI가 매 이벤트마다 부른다(버튼이라 모든 이벤트에서 같은 순서로 그린다).
    public void DrawOverlay()
    {
        if (_rotating != null)
        {
            DrawRotateButtons(_rotating);
            return;
        }
        if (_relocating != null && ConstructionRules.RotationMatters(_relocating.Kind, _relocating.Footprint)) DrawFacingHint(_relocRotation);
        else if (_activeKind.HasValue && ConstructionRules.RotationMatters(_activeKind.Value, CurrentFootprint)) DrawFacingHint(_buildRotation);
    }

    private void DrawRotateButtons(BuildingData b)
    {
        if (!TryGetRotateAnchor(b, out Vector2 center, out float radius)) return;
        int current = FacingOf(b);
        for (int r = 0; r < 4; r++)
        {
            bool isCurrent = r == current;
            // 현재 방향은 파랗게 보이되 눌러도 아무 일이 없고, 설치할 수 없는 방향은 회색이라 누를 수 없다.
            if (GUIMenuStyleUtil.DrawFlatButton(RotateArrowRect(center, radius, r), RotateArrowLabels[r], isCurrent, isCurrent || _rotateOk[r], 18, 12, 2f) && _rotateOk[r])
            {
                OnRotateChosen(b, r);
                return;
            }
        }
        DrawFloatingCaption(new Vector2(center.x, center.y - radius - 40f), $"정면 {ConstructionRules.FrontDirectionName(current)} — 바꿀 방향을 고르세요 (ESC·우클릭: 취소)", 330f);
    }

    // 이동·건설 미리보기에서 지금 정면 방향을 마우스 옆에 알려 준다.
    private static void DrawFacingHint(int rotation)
    {
        Vector2 m = GameInputScheme.PointerScreenPos;
        DrawFloatingCaption(new Vector2(m.x + 130f, Screen.height - m.y + 30f), $"정면 {ConstructionRules.FrontDirectionName(rotation)} · R: 시계 방향 회전", 240f);
    }

    private static void DrawFloatingCaption(Vector2 center, string text, float width)
    {
        var rect = new Rect(center.x - width * 0.5f, center.y - 11f, width, 22f);
        GUIMenuStyleUtil.DrawPanelBox(rect);
        GUIMenuStyleUtil.DrawClippedLabel(new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height), text, GUIMenuStyleUtil.LineLabelStyle);
    }

    public void Update(Vector3 floorOffset, int currentFloor)
    {
        if (_rotating != null) { UpdateRotate(); return; }
        if (_relocating != null) { UpdateRelocation(floorOffset, currentFloor); return; }
        if (_movingPlan != null) { UpdatePlanMove(floorOffset, currentFloor); return; }
        if (!_activeKind.HasValue) return;

        Vector2 mousePos = GameInputScheme.PointerScreenPos;
        Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, floorOffset, currentFloor);
        FacilityKind kind = _activeKind.Value;
        if (RotatePressedThisFrame() && ConstructionRules.RotationMatters(kind, CurrentFootprint)) _buildRotation = ConstructionRules.NormalizeRotation(_buildRotation + 1);
        Vector2Int footprint = ConstructionRules.RotatedFootprint(CurrentFootprint, _buildRotation);

        bool canInstall = _buildingManager.CheckInstallAt(gridPos, footprint, requireOwnership: true, kind: kind, rotation: _buildRotation) == BuildBlock.None;
        _ghost.UpdatePosition(gridPos, floorOffset, canInstall, footprint);
        // 생산시설은 정면 출현 구역까지 함께 보여 건설이 필요한 전체 영역을 알린다(건설 명세 2-13).
        if (kind == FacilityKind.UnitProduction)
            _zoneGhost.Show(ConstructionRules.FrontZoneRect(new Vector2Int(gridPos.x, gridPos.y), footprint, _buildRotation, BuildingManager.FrontZoneDepth), gridPos.z, floorOffset, canInstall ? ZoneOkColor : ZoneBadColor);
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
