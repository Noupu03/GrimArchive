using System.Collections.Generic;
using UnityEngine;
using Haare.Client.Routine;
using Haare.Client.UI;
using VContainer;

// 시설 및 오브젝트(코어·문·함정·시체·분해 패키지·방 라벨) 통합 조작 UI — 명세서 v0.2 반영.
[PanelAttribute("Prefabs/BuildingControlPanel")]
public class BuildingControlPanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    public static BuildingControlPanel Instance { get; private set; }

    private ResourceManager _resourceManager;
    private BuildingManager _buildingManager;
    private BuildingData _current;
    private InteractableObject _currentObject;
    private Room _currentRoom;

    private int _batchCount = 1;
    private bool _useCoreSalvageToggle = true;
    private Vector2 _scrollPos;

    private bool HasContent => _current != null || _currentObject != null || _currentRoom != null;

    // 시설·오브젝트·방 정보 중 무엇이든 열려 있는가 — 배치모드의 ESC/우클릭이 시설 UI를 먼저 닫을 때 쓴다.
    public bool IsOpen => HasContent;

    [Inject]
    public void Construct(ResourceManager resourceManager, BuildingManager buildingManager)
    {
        _resourceManager = resourceManager;
        _buildingManager = buildingManager;
    }

    protected override void Constructor()
    {
        base.Constructor();
        Instance = this;
    }

    protected override void UpdateProcess()
    {
        base.UpdateProcess();
        if (_currentObject != null && !IsObjectStillAlive(_currentObject))
            ClosePanel();
        if (_current != null && (_buildingManager == null || !ContainsBuilding(_buildingManager.AllBuildings, _current)))
            ClosePanel();
        BottomLeftPanelStack.Report(StackId, HasContent, PanelWidth);
    }

    private static bool ContainsBuilding(IReadOnlyList<BuildingData> list, BuildingData target)
    {
        if (list == null || target == null) return false;
        for (int i = 0; i < list.Count; i++)
            if (list[i] == target) return true;
        return false;
    }

    private static bool IsObjectStillAlive(InteractableObject obj)
        => GameSession.Instance != null
        && GameSession.Instance.objectGrid.TryGetValue(obj.Position, out var found)
        && found == obj;

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        panel = gameObject;
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        _current = null;
        _currentObject = null;
        _currentRoom = null;
    }

    public void BindEvent() { }

    public void ShowForBuilding(BuildingData building)
    {
        _current = building;
        _currentObject = null;
        _currentRoom = null;
        _scrollPos = Vector2.zero;
        OpenPanel();
    }

    public void ShowForObject(InteractableObject obj)
    {
        _currentObject = obj;
        _current = null;
        _currentRoom = null;
        _scrollPos = Vector2.zero;
        OpenPanel();
    }

    public void ShowForRoom(Room room)
    {
        _currentRoom = room;
        _current = null;
        _currentObject = null;
        _scrollPos = Vector2.zero;
        OpenPanel();
    }

    private const int PanelWidth = 320;
    private const int PanelHeight = 420;
    private const string StackId = "BuildingControl";

    private Rect GetPanelRect()
    {
        float x = BottomLeftPanelStack.Report(StackId, HasContent, PanelWidth);
        float y = Screen.height - PanelHeight - BottomLeftPanelStack.GetReservedBottomHeight();
        return new Rect(x, y, PanelWidth, PanelHeight);
    }

    public bool IsMouseOverPanel()
    {
        if (!HasContent) return false;
        return GUIMouseUtil.IsMouseOverRect(GetPanelRect());
    }

    public bool TryGetVisibleRect(out Rect rect)
    {
        rect = default;
        if (!HasContent) return false;
        rect = GetPanelRect();
        return true;
    }

    private void OnGUI()
    {
        if (!HasContent) return;

        Rect rect = GetPanelRect();
        GUIMenuStyleUtil.DrawPanelBox(rect);

        GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 8, rect.width - 24, rect.height - 16));
        _scrollPos = GUILayout.BeginScrollView(_scrollPos);

        if (_currentRoom != null)
        {
            DrawRoomInfo(_currentRoom);
        }
        else if (_currentObject != null)
        {
            DrawObjectInfo(_currentObject);
        }
        else if (_current != null)
        {
            DrawBuildingInfo(_current);
        }

        GUILayout.Space(8);
        if (GUIMenuStyleUtil.DrawFlatButtonLayout("닫기", options: new[] { GUILayout.Height(28f) }))
        {
            ClosePanel();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ── 시설 통합 표시 ──

    private void DrawBuildingInfo(BuildingData b)
    {
        if (b.IsDummy)
        {
            GUILayout.Label("[ 더미 건물 (디버그) ]", GUIMenuStyleUtil.LabelStyle);
            GUILayout.Label("기능 없음 — 건물 판정(타일 점유)만 있는 디버그용 오브젝트입니다.", GUIMenuStyleUtil.BodyLabelStyle);
            return;
        }

        string tierSuffix = b.UpgradeTier > 0 ? $" +{b.UpgradeTier}" : string.Empty;
        GUILayout.Label($"[ {b.DisplayName}{tierSuffix} ]", GUIMenuStyleUtil.LabelStyle);
        GUILayout.Label($"체력: {b.CurrentHp:F0} / {b.MaxHp:F0}", GUIMenuStyleUtil.BodyLabelStyle);

        int workers = _buildingManager != null ? _buildingManager.CountAssignedWorkers(b) : 0;
        if (_buildingManager != null && _buildingManager.DoesBuildingAcceptWorkers(b))
            GUILayout.Label($"투입 작업자: {workers} / {WorkSpeedMath.MaxWorkersPerTask} (아군 선택 후 우클릭)", GUIMenuStyleUtil.BodyLabelStyle);

        bool coreActive = BuildingManager.AreCoreFunctionsActive(b.Position.z);
        if (!coreActive)
            GUILayout.Label("※ 담당 코어가 파괴되어 일반 가동이 정지 중입니다.", GUIMenuStyleUtil.BodyLabelStyle);
        else if (b.IsFormallyInstalled && b.CurrentHp < b.MaxHp - 1e-3f && b.ActiveFieldTask != FacilityFieldTaskKind.Repair)
            GUILayout.Label("※ 시설 손상으로 일반 가동이 정지 중입니다 (최대 HP 수리 필요).", GUIMenuStyleUtil.BodyLabelStyle);

        if (b.ActiveFieldTask != FacilityFieldTaskKind.None)
        {
            DrawActiveFieldTaskSection(b);
            return;
        }

        DrawIdleMaintenanceButtons(b, coreActive);
        GUILayout.Space(6);

        switch (b.Kind)
        {
            case FacilityKind.LifeEnergy:
                DrawLifeEnergyFacilityBody(b);
                break;
            case FacilityKind.UnitProduction:
                DrawUnitProductionFacilityBody(b, coreActive);
                break;
            case FacilityKind.CorpseRefinery:
                DrawCorpseRefineryFacilityBody(b);
                break;
            case FacilityKind.Research:
                DrawResearchFacilityBody(b, coreActive);
                break;
        }
    }

    private void DrawActiveFieldTaskSection(BuildingData b)
    {
        GUILayout.Space(4);
        switch (b.ActiveFieldTask)
        {
            case FacilityFieldTaskKind.Construction:
            case FacilityFieldTaskKind.Upgrade:
            {
                var s = b.ActiveConstructionOrUpgrade;
                string title = b.ActiveFieldTask == FacilityFieldTaskKind.Construction ? "건설 진행 중" : "업그레이드 진행 중";
                double pct = s != null ? s.Progress01 * 100.0 : 0.0;
                string waitStart = s != null && !s.HasStartedWork ? " (작업자 도착 및 부지 비움 대기)" : string.Empty;
                GUILayout.Label($"[{title}] {pct:F0}%{waitStart}", GUIMenuStyleUtil.BodyLabelStyle);
                if (GUIMenuStyleUtil.DrawFlatButtonLayout("작업 취소 (비용 환급)", options: new[] { GUILayout.Height(26f) }))
                    _buildingManager?.CancelFacilityConstructionOrUpgrade(b);
                break;
            }
            case FacilityFieldTaskKind.Repair:
            {
                var r = b.ActiveRepair;
                double pct = r != null ? r.Progress01 * 100.0 : 0.0;
                GUILayout.Label($"[수리 진행 중] {pct:F0}%", GUIMenuStyleUtil.BodyLabelStyle);
                if (r != null && r.IsPausedWaitingDiffApproval)
                {
                    GUILayout.Label($"추가 피해 발생 — 차액 {r.UnapprovedDiffAmount} 생명 에너지 승인 필요", GUIMenuStyleUtil.BodyLabelStyle);
                    if (GUIMenuStyleUtil.DrawFlatButtonLayout($"추가 수리비 지불 ({r.UnapprovedDiffAmount})", options: new[] { GUILayout.Height(26f) }))
                        _buildingManager?.TryApproveFacilityRepairDiff(b);
                }
                if (GUIMenuStyleUtil.DrawFlatButtonLayout("수리 취소 (지불액 환급)", options: new[] { GUILayout.Height(26f) }))
                    _buildingManager?.CancelFacilityRepair(b);
                break;
            }
            case FacilityFieldTaskKind.Demolish:
            {
                double pct = b.DemolishRequiredSeconds > 0.0 ? (b.DemolishCompletedSeconds / b.DemolishRequiredSeconds) * 100.0 : 100.0;
                GUILayout.Label($"[철거 진행 중] {pct:F0}%", GUIMenuStyleUtil.BodyLabelStyle);
                if (GUIMenuStyleUtil.DrawFlatButtonLayout("철거 취소", options: new[] { GUILayout.Height(26f) }))
                    _buildingManager?.CancelFacilityDemolish(b);
                break;
            }
            case FacilityFieldTaskKind.Relocation:
            {
                var rel = b.ActiveRelocation;
                double pct = rel != null && rel.DismantleRequiredSeconds > 0.0
                    ? (rel.DismantleCompletedSeconds / rel.DismantleRequiredSeconds) * 100.0
                    : 0.0;
                GUILayout.Label($"[이동용 분해 진행 중] {pct:F0}% → ({rel?.TargetDestination.x},{rel?.TargetDestination.y})", GUIMenuStyleUtil.BodyLabelStyle);
                if (GUIMenuStyleUtil.DrawFlatButtonLayout("분해 취소 (비용 환급)", options: new[] { GUILayout.Height(26f) }))
                    _buildingManager?.CancelFacilityRelocation(b);
                break;
            }
        }
    }

    private void DrawIdleMaintenanceButtons(BuildingData b, bool coreActive)
    {
        if (!b.IsFormallyInstalled || _buildingManager == null) return;

        bool damaged = b.CurrentHp < b.MaxHp - 1e-3f;
        GUILayout.BeginHorizontal();
        bool prev = GUI.enabled;
        GUI.enabled = prev && coreActive && damaged;
        if (GUIMenuStyleUtil.DrawFlatButtonLayout("수리", options: new[] { GUILayout.Height(24f) }))
            _buildingManager.TryStartFacilityRepair(b);

        GUI.enabled = prev && coreActive;
        if (GUIMenuStyleUtil.DrawFlatButtonLayout("강화", options: new[] { GUILayout.Height(24f) }))
            _buildingManager.TryStartFacilityUpgrade(b);

        if (GUIMenuStyleUtil.DrawFlatButtonLayout("철거", options: new[] { GUILayout.Height(24f) }))
            _buildingManager.TryStartFacilityDemolish(b);

        if (GUIMenuStyleUtil.DrawFlatButtonLayout("분해이동", options: new[] { GUILayout.Height(24f) }))
        {
            if (_buildingManager.TryFindNearbyRelocationDestination(b, out Vector3Int dest))
                _buildingManager.TryStartFacilityRelocation(b, dest);
            else
                NoticeCenter.Instance?.PushMomentary("같은 방 안에 이동할 빈 부지가 없습니다.", NoticeCenter.WarningColor);
        }
        GUI.enabled = prev;
        GUILayout.EndHorizontal();
    }

    private void DrawLifeEnergyFacilityBody(BuildingData b)
    {
        double pct = b.LifeEnergyCycle.Progress01 * 100.0;
        GUILayout.Label($"생산 주기 게이지: {pct:F0}% (기본 {b.LifeEnergyCycle.BasePayout:F0} / {b.LifeEnergyCycle.BaseCycleDurationSeconds:F0}초)", GUIMenuStyleUtil.BodyLabelStyle);
        GUILayout.Label("작업자 투입 시 공동 작업 배율만큼 주기가 단축되고 산출량 보너스가 가중 합산됩니다.", GUIMenuStyleUtil.BodyLabelStyle);
        if (_buildingManager != null)
            GUILayout.Label($"정식 설치된 생명 에너지 시설 수: {_buildingManager.ResourceBuildingCount}개", GUIMenuStyleUtil.BodyLabelStyle);
    }

    private void DrawUnitProductionFacilityBody(BuildingData b, bool coreActive)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"묶음 수량: {_batchCount}", GUIMenuStyleUtil.BodyLabelStyle, GUILayout.Width(96f));
        if (GUIMenuStyleUtil.DrawFlatButtonLayout("-10", options: new[] { GUILayout.Width(38f), GUILayout.Height(22f) })) _batchCount = Mathf.Clamp(_batchCount - 10, 1, 99);
        if (GUIMenuStyleUtil.DrawFlatButtonLayout("-1", options: new[] { GUILayout.Width(34f), GUILayout.Height(22f) })) _batchCount = Mathf.Clamp(_batchCount - 1, 1, 99);
        if (GUIMenuStyleUtil.DrawFlatButtonLayout("+1", options: new[] { GUILayout.Width(34f), GUILayout.Height(22f) })) _batchCount = Mathf.Clamp(_batchCount + 1, 1, 99);
        if (GUIMenuStyleUtil.DrawFlatButtonLayout("+10", options: new[] { GUILayout.Width(38f), GUILayout.Height(22f) })) _batchCount = Mathf.Clamp(_batchCount + 10, 1, 99);
        GUILayout.EndHorizontal();

        bool roomSecured = true;
        if (GameSession.Instance != null && GameSession.Instance.roomGrid.TryGetValue(b.Position, out Room facRoom) && facRoom != null)
            roomSecured = facRoom.RoomFaction == FactionType.Player;

        if (b.AvailableRules != null && _resourceManager != null)
        {
            bool prev = GUI.enabled;
            GUI.enabled = prev && coreActive && b.IsOperational && roomSecured;
            for (int i = 0; i < b.AvailableRules.Count; i++)
            {
                ProductionRule rule = b.AvailableRules[i];
                UnitProductionSpec spec = rule.ToSpec();
                string costText = BuildCostText(rule.costs);
                if (GUIMenuStyleUtil.DrawFlatButtonLayout($"{rule.displayName} ×{_batchCount} ({costText}/기)", options: new[] { GUILayout.Height(28f) }))
                {
                    ProductionEnqueueBlock block = b.ProductionQueue.TryEnqueue(
                        spec,
                        _batchCount,
                        _resourceManager.Accounts,
                        _resourceManager.IsSoulShardSpendUnlocked,
                        isSpeciesUnlocked: true,
                        isRoomSecured: roomSecured,
                        isCoreValid: coreActive,
                        facilityFloor: b.Position.z,
                        reserveCostMultiplier: 1.0,
                        out _,
                        out string failMsg);

                    if (block != ProductionEnqueueBlock.None && !string.IsNullOrEmpty(failMsg))
                        NoticeCenter.Instance?.PushMomentary(failMsg, NoticeCenter.WarningColor);
                }
            }
            GUI.enabled = prev;
        }

        GUILayout.Space(6);
        var entries = b.ProductionQueue.Entries;
        GUILayout.Label($"생산 대기열 ({entries.Count}건)", GUIMenuStyleUtil.BodyLabelStyle);

        for (int i = 0; i < entries.Count; i++)
        {
            ProductionQueueEntry e = entries[i];
            double pct = e.ActiveProgress01 * 100.0;
            string stateLabel = e.IsBlockedWaitingSpawnUI
                ? FacilityProductionQueue.WaitingSpawnStatusText
                : (i == 0 && e.HasActiveUnit ? $"{pct:F0}%" : "대기");

            GUILayout.Label($"{i + 1}. {e.Spec.DisplayName} (남은 {e.TotalRemainingUnits}기) — {stateLabel}", GUIMenuStyleUtil.BodyLabelStyle);
            if (e.IsBlockedWaitingSpawnUI)
                GUILayout.Label($"  └ {FacilityProductionQueue.WaitingSpawnReasonText}", GUIMenuStyleUtil.BodyLabelStyle);

            GUILayout.BeginHorizontal();
            if (GUIMenuStyleUtil.DrawFlatButtonLayout("-1", options: new[] { GUILayout.Width(36f), GUILayout.Height(22f) }))
                b.ProductionQueue.TryReduceWaitingCount(e.EntryId, 1, _resourceManager?.Accounts);
            if (GUIMenuStyleUtil.DrawFlatButtonLayout("▲", options: new[] { GUILayout.Width(32f), GUILayout.Height(22f) }) && i > 0 && _resourceManager != null)
                b.ProductionQueue.TryMoveEntry(e.EntryId, i - 1, _resourceManager.Accounts, _resourceManager.IsSoulShardSpendUnlocked);
            if (GUIMenuStyleUtil.DrawFlatButtonLayout("▼", options: new[] { GUILayout.Width(32f), GUILayout.Height(22f) }) && i + 1 < entries.Count && _resourceManager != null)
                b.ProductionQueue.TryMoveEntry(e.EntryId, i + 1, _resourceManager.Accounts, _resourceManager.IsSoulShardSpendUnlocked);
            if (GUIMenuStyleUtil.DrawFlatButtonLayout("취소", options: new[] { GUILayout.Width(52f), GUILayout.Height(22f) }) && _resourceManager != null)
            {
                b.ProductionQueue.CancelEntry(e.EntryId, _resourceManager.Accounts);
                break;
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawCorpseRefineryFacilityBody(BuildingData b)
    {
        CorpseRefineryState refState = b.Refinery;
        GUILayout.Label($"용량: 보관 {refState.StoredCorpseCount} + 예약 {refState.ReservedIncomingCount} / 최대 {refState.MaxStorageSlots} (여유 {refState.AvailableSlots})", GUIMenuStyleUtil.BodyLabelStyle);

        if (refState.ActiveRefiningCorpse != null)
        {
            double pct = refState.ActiveRefineProgress01 * 100.0;
            GUILayout.Label($"자동 정제 중: {refState.ActiveRefiningCorpse.DisplayName} ({pct:F0}%)", GUIMenuStyleUtil.BodyLabelStyle);
        }
        else
        {
            GUILayout.Label("자동 정제 대기 중 (시체를 우클릭 회수해 입고)", GUIMenuStyleUtil.BodyLabelStyle);
        }

        if (!refState.IsDraining)
        {
            if (GUIMenuStyleUtil.DrawFlatButtonLayout("전체 비우기 시작", options: new[] { GUILayout.Height(26f) }))
                _buildingManager?.StartRefineryDrain(b);
        }
        else
        {
            GUILayout.Label("※ 전체 비우기 진행 중 (신규 입고 차단)", GUIMenuStyleUtil.BodyLabelStyle);
            if (GUIMenuStyleUtil.DrawFlatButtonLayout("전체 비우기 해제", options: new[] { GUILayout.Height(26f) }))
                _buildingManager?.CancelRefineryDrain(b);
        }

        if (refState.WaitingCorpses.Count > 0)
        {
            GUILayout.Space(4);
            GUILayout.Label($"대기 시체 ({refState.WaitingCorpses.Count}구)", GUIMenuStyleUtil.BodyLabelStyle);
            for (int i = 0; i < refState.WaitingCorpses.Count; i++)
            {
                CorpseRecord c = refState.WaitingCorpses[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{c.DisplayName} (부패 {c.Decay01 * 100f:F0}%)", GUIMenuStyleUtil.BodyLabelStyle);
                if (GUIMenuStyleUtil.DrawFlatButtonLayout("반출", options: new[] { GUILayout.Width(48f), GUILayout.Height(22f) }))
                {
                    _buildingManager?.EjectSingleWaitingCorpse(b, c.CorpseId);
                    break;
                }
                GUILayout.EndHorizontal();
            }
        }
    }

    private void DrawResearchFacilityBody(BuildingData b, bool coreActive)
    {
        if (b.ResearchQueue == null || _resourceManager == null) return;
        GlobalResearchLedger ledger = _resourceManager.ResearchLedger;
        var entries = b.ResearchQueue.Entries;

        if (entries.Count > 0)
        {
            var act = entries[0];
            double pct = ledger.GetProgress01(act.Definition.ResearchId) * 100.0;
            GUILayout.Label($"선두 항목: {act.Definition.DisplayName} ({pct:F0}%)", GUIMenuStyleUtil.BodyLabelStyle);
            if (GUIMenuStyleUtil.DrawFlatButtonLayout("선두 연구 취소 (환급)", options: new[] { GUILayout.Height(24f) }))
                b.ResearchQueue.CancelManually(act.Definition.ResearchId, _resourceManager.Accounts);
        }

        for (int idx = 1; idx < entries.Count; idx++)
        {
            var w = entries[idx];
            GUILayout.BeginHorizontal();
            GUILayout.Label($"대기 {idx}. {w.Definition.DisplayName}", GUIMenuStyleUtil.BodyLabelStyle);
            if (GUIMenuStyleUtil.DrawFlatButtonLayout("▲", options: new[] { GUILayout.Width(30f), GUILayout.Height(22f) }))
            {
                if (!b.ResearchQueue.TryMoveEntry(w.Definition.ResearchId, idx - 1, out string moveErr) && !string.IsNullOrEmpty(moveErr))
                    NoticeCenter.Instance?.PushMomentary(moveErr, NoticeCenter.WarningColor);
            }
            if (GUIMenuStyleUtil.DrawFlatButtonLayout("▼", options: new[] { GUILayout.Width(30f), GUILayout.Height(22f) }) && idx + 1 < entries.Count)
            {
                if (!b.ResearchQueue.TryMoveEntry(w.Definition.ResearchId, idx + 1, out string moveErr) && !string.IsNullOrEmpty(moveErr))
                    NoticeCenter.Instance?.PushMomentary(moveErr, NoticeCenter.WarningColor);
            }
            if (GUIMenuStyleUtil.DrawFlatButtonLayout("취소", options: new[] { GUILayout.Width(48f), GUILayout.Height(22f) }))
            {
                b.ResearchQueue.CancelManually(w.Definition.ResearchId, _resourceManager.Accounts);
                break;
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6);
        GUILayout.Label("연구 목록", GUIMenuStyleUtil.BodyLabelStyle);
        foreach (var kv in ledger.Catalog)
        {
            ResearchDefinition def = kv.Value;
            if (ledger.IsCompleted(def.ResearchId))
            {
                GUILayout.Label($"✓ {def.DisplayName} (완료)", GUIMenuStyleUtil.BodyLabelStyle);
                continue;
            }

            bool preservedPaid = ledger.HasPreservedPayment(def.ResearchId);
            string costText = preservedPaid ? "재개 무료(지불 보존)" : $"생명 {def.BaseLifeEnergyCost:F0}, 영혼 {def.BaseSoulShardCost:F0}";
            bool prev = GUI.enabled;
            GUI.enabled = prev && coreActive && b.IsOperational && !ledger.IsQueuedAnywhere(def.ResearchId);
            if (GUIMenuStyleUtil.DrawFlatButtonLayout($"{def.DisplayName} ({costText})", options: new[] { GUILayout.Height(26f) }))
            {
                bool reqFloorActivated = !def.RequiredActivatedFloor.HasValue
                    || (GameSession.Instance?.Core?.IsFloorCoreEverActivated(def.RequiredActivatedFloor.Value) ?? false);

                ResearchEnqueueBlock block = b.ResearchQueue.TryEnqueue(
                    def,
                    _resourceManager.Accounts,
                    _resourceManager.IsSoulShardSpendUnlocked,
                    reqFloorActivated,
                    reserveCostMultiplier: 1.0,
                    out string failMsg);

                if (block != ResearchEnqueueBlock.None && !string.IsNullOrEmpty(failMsg))
                    NoticeCenter.Instance?.PushMomentary(failMsg, NoticeCenter.WarningColor);
            }
            GUI.enabled = prev;
        }
    }

    // ── 방 정보 ──

    private void DrawRoomInfo(Room room)
    {
        GameSession session = GameSession.Instance;
        GUILayout.Label($"[ {room.RoomName} ]", GUIMenuStyleUtil.LabelStyle);
        GUILayout.Label($"{room.Floor}층", GUIMenuStyleUtil.BodyLabelStyle);
        if (session == null) return;

        GUILayout.Label($"소유 진영: {RoomStatusText.FactionName(room.RoomFaction)}", GUIMenuStyleUtil.BodyLabelStyle);
        GUILayout.Label(RoomStatusText.Population(room, DefensePlacementSystem.Instance != null ? DefensePlacementSystem.Instance.IncomingPopulation(room) : 0), GUIMenuStyleUtil.BodyLabelStyle);
        GUILayout.Label($"방 확보: {RoomStatusText.ClearText(session, room)}", GUIMenuStyleUtil.BodyLabelStyle);
        GUILayout.Label($"인류 거점: {RoomStatusText.BaseText(session, room)}", GUIMenuStyleUtil.BodyLabelStyle);

        string core = RoomStatusText.CoreText(session, room);
        if (core.Length > 0) GUILayout.Label($"코어: {core}", GUIMenuStyleUtil.BodyLabelStyle);
    }

    // ── 월드 오브젝트 정보 ──

    private void DrawObjectInfo(InteractableObject obj)
    {
        GUILayout.Label($"[ {GetObjectDisplayName(obj)} ]", GUIMenuStyleUtil.LabelStyle);
        GUILayout.Label($"위치: ({obj.Position.x}, {obj.Position.y}) / {obj.Position.z}층", GUIMenuStyleUtil.BodyLabelStyle);

        if (TryGetObjectHp(obj, out float hp, out float maxHp))
            GUILayout.Label($"체력: {hp:F0} / {maxHp:F0}", GUIMenuStyleUtil.BodyLabelStyle);

        FactionType? owner = GetObjectOwnerFaction(obj);
        if (owner != null)
            GUILayout.Label($"소유 진영: {owner.Value}", GUIMenuStyleUtil.BodyLabelStyle);

        if (obj.Tags != null && obj.Tags.Contains("Object/Passable/Loot"))
        {
            string state = obj.IsCollected ? "회수됨" : obj.IsInvestigated ? "조사 완료(회수 대기)" : "미조사";
            GUILayout.Label($"상태: {state}", GUIMenuStyleUtil.BodyLabelStyle);
        }

        if (obj.CorpseEconomyData != null)
            DrawCorpseInfo(obj);
        if (obj.DismantledBuildingPayload != null)
            DrawDismantledPackageInfo(obj);
        if (obj.Tags != null && obj.Tags.Contains(GameSession.CoreTag))
            DrawCoreControls(obj);
        if (obj.Tags != null && obj.Tags.Contains(DoorSystem.DoorTag))
            DrawDoorControls(obj);
    }

    private void DrawCorpseInfo(InteractableObject obj)
    {
        CorpseRecord c = obj.CorpseEconomyData;
        GUILayout.Label($"원본: {c.DisplayName} ({c.OriginFaction})", GUIMenuStyleUtil.BodyLabelStyle);
        if (!c.CanBeRefined)
        {
            GUILayout.Label("부패도: 100% — 정제 불가 (장애물로 잔존)", GUIMenuStyleUtil.BodyLabelStyle);
        }
        else
        {
            var reward = CorpseRefineMath.CalculateReward(c);
            GUILayout.Label($"부패도: {c.Decay01 * 100f:F0}% (보관시설 입고 시 정지)", GUIMenuStyleUtil.BodyLabelStyle);
            GUILayout.Label($"예상 정제 보상: 생명 에너지 {reward.LifeEnergy:F0}, 영혼 파편 {reward.TotalSoulShard:F0}", GUIMenuStyleUtil.BodyLabelStyle);
            GUILayout.Label("※ 아군 몬스터 선택 후 우클릭하여 시체 회수·운반", GUIMenuStyleUtil.BodyLabelStyle);
        }

        if (obj.CoexistingObjects != null && obj.CoexistingObjects.Count > 0)
            GUILayout.Label($"같은 타일 추가 보존 대상: {obj.CoexistingObjects.Count}개", GUIMenuStyleUtil.BodyLabelStyle);
    }

    private void DrawDismantledPackageInfo(InteractableObject obj)
    {
        BuildingData b = obj.DismantledBuildingPayload;
        GUILayout.Label($"분해된 시설: {b.DisplayName} (1타일 패키지)", GUIMenuStyleUtil.BodyLabelStyle);
        if (b.ActiveRelocation != null)
            GUILayout.Label($"목적지: ({b.ActiveRelocation.TargetDestination.x}, {b.ActiveRelocation.TargetDestination.y})", GUIMenuStyleUtil.BodyLabelStyle);
        GUILayout.Label("※ 아군 몬스터 선택 후 우클릭하여 운반·재설치 (추가 비용 0)", GUIMenuStyleUtil.BodyLabelStyle);
    }

    private void DrawDoorControls(InteractableObject door)
    {
        GameSession session = GameSession.Instance;
        if (session == null) return;

        GUILayout.Label($"상태: {(door.DoorManualOpen || door.DoorAutoOpen ? "열려 있음(누구나 통과)" : "닫힘(보유 진영만 통과)")}", GUIMenuStyleUtil.BodyLabelStyle);

        // 일시정지 중엔 문이 움직이지 않으므로 지시를 예약하고, 문 앞·뒤 유닛 같은 상황은 재개 때 확인한다.
        bool paused = session.isPaused;
        bool hasPending = session.TryGetPendingDoorOrder(door, out bool pendingOpen);
        bool effectiveOpen = paused && hasPending ? pendingOpen : door.DoorManualOpen;
        bool wantOpen = !effectiveOpen;
        DoorControlBlock block = paused ? session.CheckDoorReservation(door, wantOpen) : session.CheckDoorControl(door, wantOpen);
        if (block == DoorControlBlock.NotPlayerDoor)
        {
            GUILayout.Label(DoorControlRules.Describe(block), GUIMenuStyleUtil.BodyLabelStyle);
            return;
        }
        if (paused && hasPending)
            GUILayout.Label($"예약됨: {(pendingOpen ? "열기" : "닫기")} — 시간을 재개하면 적용됩니다", GUIMenuStyleUtil.BodyLabelStyle);

        bool prevEnabled = GUI.enabled;
        GUI.enabled = prevEnabled && block == DoorControlBlock.None;
        if (GUIMenuStyleUtil.DrawFlatButtonLayout(wantOpen ? "문 열기" : "문 닫기", options: new[] { GUILayout.Height(28f) }))
        {
            if (paused) session.ReserveDoorOpen(door, wantOpen);
            else session.TrySetDoorOpen(door, wantOpen);
        }
        GUI.enabled = prevEnabled;

        string reason = DoorControlRules.Describe(block);
        if (reason.Length > 0) GUILayout.Label(reason, GUIMenuStyleUtil.BodyLabelStyle);
    }

    private void DrawCoreControls(InteractableObject obj)
    {
        CoreSystem coreSystem = GameSession.Instance?.Core;
        if (coreSystem == null || !coreSystem.TryGetCoreAt(obj.Position, out Room room, out InteractableObject core)) return;

        CoreStatus status = CoreMath.StatusOf(core.CoreEverActivated, core.CoreDestroyed);
        GUILayout.Label($"코어 상태: {CoreMath.Describe(status)}", GUIMenuStyleUtil.BodyLabelStyle);

        if (!core.CoreDestroyed)
        {
            if (core.CoreEverActivated) return;
            CoreActivateBlock block = coreSystem.CheckActivate(room, core);
            if (block == CoreActivateBlock.None)
            {
                if (GUIMenuStyleUtil.DrawFlatButtonLayout("완전 활성화", options: new[] { GUILayout.Height(28f) }))
                    coreSystem.TryActivate(core.Position);
            }
            else
            {
                GUILayout.Label($"활성화 불가: {CoreMath.Describe(block)}", GUIMenuStyleUtil.BodyLabelStyle);
            }
            return;
        }

        GUILayout.Label("이 층의 건설·생산·문 제어가 정지 중입니다.", GUIMenuStyleUtil.BodyLabelStyle);

        CoreRestoreSession activeRestore = coreSystem.GetActiveRestoreAt(core.Position);
        if (activeRestore != null)
        {
            GUILayout.Label($"수복 진행률: {activeRestore.Progress01 * 100.0:F0}% (수복 체력 {activeRestore.RestoreObjectCurrentHp:F0}/{activeRestore.RestoreObjectMaxHp:F0})", GUIMenuStyleUtil.BodyLabelStyle);
            GUILayout.Label("※ 아군 몬스터 선택 후 코어를 우클릭해 작업자를 배정하세요.", GUIMenuStyleUtil.BodyLabelStyle);

            if (activeRestore.IsPausedWaitingDiffApproval)
            {
                GUILayout.Label($"추가 피해 발생 — 차액 {activeRestore.UnapprovedDiffAmount} 생명 에너지 승인 필요", GUIMenuStyleUtil.BodyLabelStyle);
                if (GUIMenuStyleUtil.DrawFlatButtonLayout($"추가 수복비 지불 ({activeRestore.UnapprovedDiffAmount})", options: new[] { GUILayout.Height(26f) }))
                    coreSystem.TryApproveRestoreExtraDiff(core.Position);
            }

            if (GUIMenuStyleUtil.DrawFlatButtonLayout("코어 수복 취소 (지불액 환급)", options: new[] { GUILayout.Height(26f) }))
                coreSystem.CancelRestoreManually(core.Position);
            return;
        }

        bool hasSalvage = coreSystem.HasAvailableSalvageInCoreRoom(room, out _);
        if (hasSalvage)
        {
            string toggleText = _useCoreSalvageToggle ? "■ 코어 잔해 1개 사용 (-50% 감면)" : "□ 코어 잔해 사용 안 함";
            if (GUIMenuStyleUtil.DrawFlatButtonLayout(toggleText, active: _useCoreSalvageToggle, options: new[] { GUILayout.Height(24f) }))
                _useCoreSalvageToggle = !_useCoreSalvageToggle;
        }

        bool applySalvage = hasSalvage && _useCoreSalvageToggle;
        int neededCost = EconomyMath.CeilCost(applySalvage ? CoreSystem.RestoreLifeEnergyCost * 0.5 : CoreSystem.RestoreLifeEnergyCost);
        CoreRestoreBlock restoreBlock = coreSystem.CheckRestore(room, core, useSalvage: applySalvage);
        if (restoreBlock == CoreRestoreBlock.None)
        {
            if (GUIMenuStyleUtil.DrawFlatButtonLayout($"코어 수복 등록 (생명 에너지 {neededCost})", options: new[] { GUILayout.Height(28f) }))
                coreSystem.TryBeginRestoreOrder(core.Position, applySalvage, out _);
        }
        else
        {
            GUILayout.Label($"수복 불가: {CoreMath.Describe(restoreBlock)} (필요 생명 에너지 {neededCost})", GUIMenuStyleUtil.BodyLabelStyle);
        }
    }

    private static string GetObjectDisplayName(InteractableObject obj)
    {
        if (obj.DismantledBuildingPayload != null) return "분해된 시설 패키지";
        if (obj.Tags == null) return "오브젝트";
        if (obj.Tags.Contains(DoorSystem.DoorTag)) return "문";
        if (obj.Tags.Contains(DoorSystem.FenceTag)) return "목책";
        if (obj.Tags.Contains(GameSession.CoreTag)) return "코어";
        if (obj.Tags.Contains("Object/Building/Passable/Trap")) return "함정";
        if (obj.Tags.Contains("Object/Passable/Corpse"))
            return obj.Tags.Contains("Human") ? "인류 시체" : "몬스터 시체";
        if (obj.Tags.Contains("Object/Passable/WipeoutTrace")) return "전멸 흔적";
        if (obj.Tags.Contains("Object/Passable/Loot")) return "전리품";
        if (obj.Tags.Contains(CoreSalvage.Tag)) return "코어 잔해";
        return "오브젝트";
    }

    private static bool TryGetObjectHp(InteractableObject obj, out float hp, out float maxHp)
    {
        if (DoorSystem.IsBarrier(obj) && obj.DoorMaxHp > 0f)
        {
            hp = obj.DoorHp; maxHp = obj.DoorMaxHp; return true;
        }
        if (obj.Tags != null && obj.Tags.Contains(GameSession.CoreTag) && obj.CoreMaxHp > 0f)
        {
            hp = obj.CoreHp; maxHp = obj.CoreMaxHp; return true;
        }
        if (obj.Tags != null && obj.Tags.Contains("Object/Building/Passable/Trap") && obj.TrapMaxHp > 0f)
        {
            hp = obj.TrapHp; maxHp = obj.TrapMaxHp; return true;
        }
        hp = 0f; maxHp = 0f; return false;
    }

    private static FactionType? GetObjectOwnerFaction(InteractableObject obj)
    {
        if (obj.Tags == null) return null;
        if (DoorSystem.IsBarrier(obj)) return obj.DoorOwnerFaction;
        if (!obj.Tags.Contains(GameSession.CoreTag) || GameSession.Instance == null) return null;
        return GameSession.Instance.roomGrid.TryGetValue(obj.Position, out Room room) && room != null
            ? room.RoomFaction
            : (FactionType?)null;
    }

    private static string BuildCostText(List<ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0) return "무료";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < costs.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            string kr = costs[i].resourceType == ResourceType.SoulShard ? "영혼 파편" : "생명 에너지";
            sb.Append(kr).Append(' ').Append(costs[i].amount);
        }
        return sb.ToString();
    }
}
