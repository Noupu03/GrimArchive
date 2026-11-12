using System.Collections.Generic;
using UnityEngine;
using Haare.Client.Routine;
using Haare.Client.UI;
using VContainer;

// 시설 및 오브젝트(코어·문·함정·시체·분해 패키지·방 라벨) 통합 조작 UI — 명세서 v0.2 반영.
// 패널 높이는 내용에 맞춰 늘고 줄며(화면의 70%까지, 넘치면 스크롤), 모든 글자는 칸 안에서 줄어들거나 …로 잘려 밖으로 나가지 않는다.
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
    private float _contentHeight; // 지난 Repaint에서 잰 내용 높이 - 패널 높이를 정한다

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

    // 시설 정보창이 닫히거나 다른 대상으로 바뀌면 그 시설의 위치·방향 선택(이동·위치 변경·회전)을 끝낸다 - 안 끝내면 창이 없는 채로 미리보기만 남는다.
    private void CancelPlacementIfLeaving(BuildingData next)
    {
        if (_current != null && _current != next) BuildPlacementController.Current?.CancelModesFor(_current);
    }

    public void ClosePanel()
    {
        CancelPlacementIfLeaving(null);
        gameObject.SetActive(false);
        _current = null;
        _currentObject = null;
        _currentRoom = null;
    }

    public void BindEvent() { }

    public void ShowForBuilding(BuildingData building)
    {
        CancelPlacementIfLeaving(building);
        _current = building;
        _currentObject = null;
        _currentRoom = null;
        _scrollPos = Vector2.zero;
        OpenPanel();
    }

    public void ShowForObject(InteractableObject obj)
    {
        CancelPlacementIfLeaving(null);
        _currentObject = obj;
        _current = null;
        _currentRoom = null;
        _scrollPos = Vector2.zero;
        OpenPanel();
    }

    public void ShowForRoom(Room room)
    {
        CancelPlacementIfLeaving(null);
        _currentRoom = room;
        _current = null;
        _currentObject = null;
        _scrollPos = Vector2.zero;
        OpenPanel();
    }

    // 시설·문은 작업자 목록 행이 들어가므로 넓게, 방·그 밖의 오브젝트는 좁게 연다.
    private const int NarrowWidth = 320;
    private const int WideWidth = 380;
    private const float MinPanelHeight = 96f;
    private const float PanelPaddingY = 8f;
    private const float HeaderHeight = 24f;
    private const float FitSlack = 12f; // 내용이 딱 맞을 때 스크롤바가 깜빡이지 않게 둔 여유
    private const string StackId = "BuildingControl";

    private int PanelWidth => _current != null || IsDoorObject(_currentObject) ? WideWidth : NarrowWidth;

    private static bool IsDoorObject(InteractableObject obj)
        => obj != null && obj.Tags != null && obj.Tags.Contains(DoorSystem.DoorTag);

    private Rect GetPanelRect()
    {
        float width = PanelWidth;
        float reserved = BottomLeftPanelStack.GetReservedBottomHeight();
        float maxHeight = Mathf.Max(MinPanelHeight, Screen.height * 0.7f - reserved);
        float height = Mathf.Clamp(_contentHeight + HeaderHeight + PanelPaddingY * 2f + FitSlack, MinPanelHeight, maxHeight);
        float x = BottomLeftPanelStack.Report(StackId, HasContent, width);
        return new Rect(x, Screen.height - height - reserved, width, height);
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

        GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + PanelPaddingY, rect.width - 24f, rect.height - PanelPaddingY * 2f));
        DrawHeader();

        _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
        GUILayout.BeginVertical();
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
        GUILayout.EndVertical();
        GUIMenuStyleUtil.MeasureContent(ref _contentHeight);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ── 공용 조각 ──

    private static readonly GUILayoutOption[] H24 = { GUILayout.Height(24f) };

    private static void Body(string text) => GUILayout.Label(text, GUIMenuStyleUtil.CompactBodyLabelStyle);
    private static void Caption(string text) => GUILayout.Label(text, GUIMenuStyleUtil.CaptionLabelStyle);
    private static void Warn(string text) => GUILayout.Label(text, GUIMenuStyleUtil.WarningLabelStyle);

    private static bool Btn(string label, bool active = false, bool interactable = true)
        => GUIMenuStyleUtil.DrawFlatButtonLayout(label, active, interactable, H24);

    private string GetPanelTitle()
    {
        if (_currentRoom != null) return _currentRoom.RoomName;
        if (_currentObject != null) return GetObjectDisplayName(_currentObject);
        if (_current == null) return string.Empty;
        if (_current.IsDummy) return "더미 건물 (디버그)";
        return _current.UpgradeTier > 0 ? $"{_current.DisplayName} +{_current.UpgradeTier}" : _current.DisplayName;
    }

    private void DrawHeader()
    {
        Rect row = GUILayoutUtility.GetRect(0f, HeaderHeight, GUILayout.ExpandWidth(true));
        const float closeWidth = 46f;
        var closeRect = new Rect(row.xMax - closeWidth, row.y + 1f, closeWidth, row.height - 2f);
        bool close = GUIMenuStyleUtil.DrawFlatButton(closeRect, "닫기", false, true, 14, 11, 8f);
        GUIMenuStyleUtil.DrawClippedLabel(new Rect(row.x, row.y, row.width - closeWidth - 6f, row.height), GetPanelTitle(), GUIMenuStyleUtil.HeaderLabelStyle);
        if (close) ClosePanel();
    }

    // 한 줄 행: 왼쪽 글자(넘치면 …) + 오른쪽 끝부터 붙는 작은 버튼들. 버튼을 먼저 그리고 남은 폭에 글자를 그린다.
    private struct ButtonRow
    {
        public Rect Row;
        public float Right;
    }

    private static ButtonRow BeginButtonRow()
    {
        Rect row = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
        return new ButtonRow { Row = row, Right = row.xMax };
    }

    private static bool RowButton(ref ButtonRow row, string label, float width, bool enabled = true)
    {
        var rect = new Rect(row.Right - width, row.Row.y, width, row.Row.height);
        row.Right -= width + 2f;
        return GUIMenuStyleUtil.DrawFlatButton(rect, label, false, enabled, 16, 11, 6f);
    }

    private static void RowLabel(ButtonRow row, string text)
        => GUIMenuStyleUtil.DrawClippedLabel(new Rect(row.Row.x, row.Row.y, Mathf.Max(0f, row.Right - row.Row.x - 2f), row.Row.height), text, GUIMenuStyleUtil.LineLabelStyle);

    // ── 시설 통합 표시 ──

    private void DrawBuildingInfo(BuildingData b)
    {
        if (b.IsDummy)
        {
            Body("기능 없음 — 건물 판정(타일 점유)만 있는 디버그용 오브젝트입니다.");
            return;
        }

        bool coreActive = BuildingManager.AreCoreFunctionsActive(b.Position.z);
        DrawBuildingSummary(b, coreActive);

        if (b.ActiveFieldTask != FacilityFieldTaskKind.None)
        {
            DrawActiveFieldTaskSection(b);
            DrawWorkerList(b);
            return;
        }

        DrawIdleMaintenanceButtons(b);
        GUILayout.Space(4f);

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

        DrawWorkerList(b);
    }

    // 체력·작업자 수·손상 효율은 한 줄, 가동 정지 사유와 상태는 그 아래에 둔다.
    private void DrawBuildingSummary(BuildingData b, bool coreActive)
    {
        string line = $"체력 {b.CurrentHp:F0} / {b.MaxHp:F0}";
        if (_buildingManager != null && _buildingManager.DoesBuildingAcceptWorkers(b))
            line += $" · 작업자 {_buildingManager.CountAssignedWorkers(b)}/{BuildingManager.GetWorkerCap(b)}";
        if (b.IsFormallyInstalled && b.CurrentHp < b.MaxHp - 1e-3f)
            line += $" · 손상 효율 {b.DamageEfficiency * 100.0:F0}%";
        Body(line);

        if (b.IsFormallyInstalled)
        {
            if (!coreActive)
                Warn("※ 코어 파괴로 가동 정지 — 코어 수복 대기");
            else if (b.FieldWorkActive)
                Warn("※ 수리·강화·철거·분해 작업 중이라 가동 정지");
        }
        if (!string.IsNullOrEmpty(b.WaitReason))
            Body($"상태: {b.WaitReason}");
    }

    // ── 작업자 목록(건설 명세 1-2) — 해당 층 아군을 방별·작업 속도 순으로 보인다. 행을 누르면 추가하고, 배정된 행을 다시 누르면 그 한 명만 해제한다 ──
    private bool _showWorkerList = true;

    // 후보 목록은 Layout 이벤트에서만 0.25초마다 다시 만든다 - OnGUI는 프레임당 여러 번 불리고, Layout과 Repaint 사이에 컨트롤 수가 바뀌면 IMGUI가 예외를 낸다.
    private List<WorkerCandidate> _workerCandidates = new List<WorkerCandidate>();
    private BuildingData _workerCandidatesFor;
    private float _nextWorkerCandidatesAt;

    private List<WorkerCandidate> GetWorkerCandidates(BuildingData b)
    {
        bool targetChanged = _workerCandidatesFor != b;
        if (targetChanged || (Event.current.type == EventType.Layout && Time.unscaledTime >= _nextWorkerCandidatesAt))
        {
            _workerCandidates = _buildingManager.BuildWorkerCandidates(b);
            _workerCandidatesFor = b;
            _nextWorkerCandidatesAt = Time.unscaledTime + 0.25f;
        }
        return _workerCandidates;
    }

    private List<DoorWorkerCandidate> _doorCandidates = new List<DoorWorkerCandidate>();
    private DoorExpansionTask _doorCandidatesFor;
    private float _nextDoorCandidatesAt;

    private List<DoorWorkerCandidate> GetDoorWorkerCandidates(DoorExpansionTask task)
    {
        bool targetChanged = _doorCandidatesFor != task;
        if (targetChanged || (Event.current.type == EventType.Layout && Time.unscaledTime >= _nextDoorCandidatesAt))
        {
            _doorCandidates = _buildingManager.BuildDoorWorkerCandidates(task);
            _doorCandidatesFor = task;
            _nextDoorCandidatesAt = Time.unscaledTime + 0.25f;
        }
        return _doorCandidates;
    }

    private void DrawWorkerList(BuildingData b)
    {
        if (_buildingManager == null || !_buildingManager.DoesBuildingAcceptWorkers(b)) return;

        GUILayout.Space(4f);
        int assigned = _buildingManager.CountAssignedWorkers(b);
        string taskName = BuildingManager.DescribeTaskName(b);
        if (Btn($"{(_showWorkerList ? "▼" : "▶")} 작업자 {assigned}/{BuildingManager.GetWorkerCap(b)}"))
            _showWorkerList = !_showWorkerList;
        if (!_showWorkerList) return;

        Caption("행을 눌러 배정·해제 · 아군을 고른 뒤 우클릭해도 배정됩니다");

        string internalState = _buildingManager.DescribeInternalState(b);
        if (!string.IsNullOrEmpty(internalState)) Body(internalState);

        if (b.ActiveFieldTask == FacilityFieldTaskKind.Construction && b.WaitReason == "공간 부족")
            Warn("공간이 없어 건설할 수 없습니다.");

        if (assigned > 0 && b.ActiveFieldTask != FacilityFieldTaskKind.None)
            Caption($"남은 {taskName} 시간 {_buildingManager.EstimateSecondsWithWorker(b, null):F0}초 (이동·대기 제외, 현재 협력 속도)");

        List<WorkerCandidate> list = GetWorkerCandidates(b);
        if (list.Count == 0)
            Body("이 층에 배정할 수 있는 아군이 없습니다.");

        string lastRoom = null;
        for (int i = 0; i < list.Count; i++)
        {
            WorkerCandidate c = list[i];
            if (c.RoomName != lastRoom)
            {
                GUILayout.Label($"<color=#9fd0ff>▸ {c.RoomName}</color>", GUIMenuStyleUtil.CompactBodyLabelStyle);
                lastRoom = c.RoomName;
            }
            DrawWorkerRow(b, c, taskName);
        }
    }

    private const float CombatSettingWidth = 64f;

    // 한 명 = 두 줄 행(이름·현재 하는 일 / 속도·시간 또는 선택 불가 사유). 내부 작업자는 오른쪽에 전투 대응 설정 버튼이 붙는다.
    private void DrawWorkerRow(BuildingData b, WorkerCandidate c, string taskName)
    {
        string name = c.Unit.unitType != null ? c.Unit.unitType.typeName : c.Unit.name;
        bool selectable = c.BlockReason == null;
        bool busyElsewhere = c.BusyElsewhere && !c.IsAssigned; // 다른 일을 하는 중 - 배정하면 그 일에서 빠진다(건설 명세 1-2-표2)

        string detail;
        if (!selectable)
        {
            detail = $"선택 불가: {c.BlockReason}";
        }
        else
        {
            string time = c.IsAssigned
                ? $"진행 {_buildingManager.GetTaskProgress01(b) * 100f:F0}%"
                : $"{taskName} {c.EstimatedSeconds:F0}초";
            detail = $"속도 x{c.Speed:F1} · {time}";
            if (c.QueuedCount > 0) detail += $" · 이후 {c.QueuedCount}건 · 총 {c.QueueTotalSeconds:F0}초";
            if (busyElsewhere) detail = $"{c.Activity}에서 빠짐 · {detail}";
        }

        Rect row = GUILayoutUtility.GetRect(0f, GUIMenuStyleUtil.ListRowHeight, GUILayout.ExpandWidth(true));
        float mainWidth = c.ShowCombatSetting ? row.width - CombatSettingWidth - 4f : row.width;
        var mainRect = new Rect(row.x, row.y, mainWidth, row.height);
        if (GUIMenuStyleUtil.DrawListRow(mainRect, $"{(c.IsAssigned ? "● " : "○ ")}{name}", busyElsewhere ? "다른 작업 중" : c.Activity, detail, c.IsAssigned, selectable && GUI.enabled, !selectable, busyElsewhere))
        {
            _buildingManager.ToggleWorkerFromList(c.Unit, b);
            _nextWorkerCandidatesAt = 0f; // 다음 Layout에서 바로 갱신한다
        }

        if (c.ShowCombatSetting)
        {
            // 내부 작업자의 전투 대응 설정(건설 명세 1-6-표1): 대응 = 현재 방의 전투 인지·시설 피격 시 밖으로 나와 전투, 무시 = 내부 작업 계속.
            var settingRect = new Rect(row.xMax - CombatSettingWidth, row.y, CombatSettingWidth, row.height);
            string setLabel = c.Setting == InternalCombatSetting.Respond ? "전투 대응" : "전투 무시";
            if (GUIMenuStyleUtil.DrawFlatButton(settingRect, setLabel, false, true, 16, 11, 6f))
                c.Unit.internalCombatSetting = c.Setting == InternalCombatSetting.Respond ? InternalCombatSetting.Ignore : InternalCombatSetting.Respond;
        }
        GUILayout.Space(2f);
    }

    private void DrawActiveFieldTaskSection(BuildingData b)
    {
        GUILayout.Space(2f);
        switch (b.ActiveFieldTask)
        {
            case FacilityFieldTaskKind.Construction:
            case FacilityFieldTaskKind.Upgrade:
            {
                var s = b.ActiveConstructionOrUpgrade;
                string title = b.ActiveFieldTask == FacilityFieldTaskKind.Construction ? "건설 진행 중" : "업그레이드 진행 중";
                double pct = s != null ? s.Progress01 * 100.0 : 0.0;
                string waitStart = s != null && !s.HasStartedWork ? " · 작업자 도착·부지 비움 대기" : string.Empty;
                Body($"[{title}] {pct:F0}%{waitStart}");

                GUILayout.BeginHorizontal();
                if (b.ActiveFieldTask == FacilityFieldTaskKind.Construction && _buildingManager != null && _buildingManager.CanMovePlan(b) && Btn("위치 변경"))
                    BuildPlacementController.Current?.EnterPlanMoveMode(b);
                if (b.ActiveFieldTask == FacilityFieldTaskKind.Construction && s != null && s.HasStartedWork && b.ActiveRelocation == null && Btn("이동(미완성)"))
                    BuildPlacementController.Current?.EnterRelocationMode(b);
                if (Btn("취소(환급)"))
                    _buildingManager?.CancelFacilityConstructionOrUpgrade(b);
                GUILayout.EndHorizontal();
                break;
            }
            case FacilityFieldTaskKind.Repair:
            {
                var r = b.ActiveRepair;
                double pct = r != null ? r.Progress01 * 100.0 : 0.0;
                Body($"[수리 진행 중] {pct:F0}%");
                if (r != null && r.IsPausedWaitingDiffApproval)
                {
                    Warn($"추가 피해 발생 — 차액 {r.UnapprovedDiffAmount} 생명 에너지 승인 필요");
                    if (Btn($"추가 수리비 지불 ({r.UnapprovedDiffAmount})"))
                        _buildingManager?.TryApproveFacilityRepairDiff(b);
                }
                GUILayout.BeginHorizontal();
                if (Btn("강화로 교체"))
                    _buildingManager?.TryStartFacilityUpgrade(b);
                if (Btn("취소(환급)"))
                    _buildingManager?.CancelFacilityRepair(b);
                GUILayout.EndHorizontal();
                break;
            }
            case FacilityFieldTaskKind.Demolish:
            {
                double pct = b.DemolishRequiredSeconds > 0.0 ? (b.DemolishCompletedSeconds / b.DemolishRequiredSeconds) * 100.0 : 100.0;
                Body($"[철거 진행 중] {pct:F0}%");
                if (Btn("철거 취소"))
                    _buildingManager?.CancelFacilityDemolish(b);
                break;
            }
            case FacilityFieldTaskKind.Relocation:
            {
                var rel = b.ActiveRelocation;
                bool rotationOnly = rel != null && rel.IsRotationOnly; // 같은 자리에서 방향만 바꾸는 회전
                string stage = b.ReinstallBodyPlaced ? "재설치" : (rel != null && rel.IsDismantleFinished ? "운반" : "분해");
                string destination = rotationOnly ? string.Empty : $" → ({rel?.TargetDestination.x},{rel?.TargetDestination.y})";
                Body($"[{(rotationOnly ? "회전" : "이동")} · {stage} 진행 중] {_buildingManager.GetTaskProgress01(b) * 100f:F0}%{destination} · 정면 {ConstructionRules.FrontDirectionName(b.PendingRotation)}");
                if (b.ReinstallBodyPlaced)
                    Caption($"재설치 체력 {b.CurrentHp:F0}/{b.MaxHp:F0} · 완료 시 남은 비율을 최대 HP에 적용");

                GUILayout.BeginHorizontal();
                if (Btn(rotationOnly ? "방향 변경" : "목적지·방향 변경"))
                {
                    if (rotationOnly) BuildPlacementController.Current?.EnterRotateMode(b);
                    else BuildPlacementController.Current?.EnterRelocationMode(b);
                }
                if (!b.ReinstallBodyPlaced && !(rel != null && rel.IsDismantleFinished) && Btn("취소(환급)"))
                    _buildingManager?.CancelFacilityRelocation(b);
                GUILayout.EndHorizontal();
                break;
            }
        }
    }

    // 수리는 손상됐을 때만, 회전은 방향이 의미 있는 시설만 누를 수 있다. 강화·철거·이동은 코어가 파괴된 층에서도 가능하다(건설 명세 1-7) - 조건은 각 시도 함수가 확인한다.
    private void DrawIdleMaintenanceButtons(BuildingData b)
    {
        if (!b.IsFormallyInstalled || _buildingManager == null) return;

        bool damaged = b.CurrentHp < b.MaxHp - 1e-3f;
        bool rotatable = ConstructionRules.RotationMatters(b.Kind, b.Footprint);
        GUILayout.BeginHorizontal();
        if (Btn("수리", false, damaged))
            _buildingManager.TryStartFacilityRepair(b);
        if (Btn("강화"))
            _buildingManager.TryStartFacilityUpgrade(b);
        if (Btn("철거"))
            _buildingManager.TryStartFacilityDemolish(b);
        if (Btn("이동"))
            BuildPlacementController.Current?.EnterRelocationMode(b);
        if (Btn("회전", false, rotatable))
            BuildPlacementController.Current?.EnterRotateMode(b); // 정면 방향을 버튼으로 고른다 - 제자리 회전도 작업자가 분해·재설치로 수행한다(무상)
        GUILayout.EndHorizontal();
        if (!rotatable) Caption("정사각형 시설이라 회전할 방향이 없습니다");
    }

    private void DrawLifeEnergyFacilityBody(BuildingData b)
    {
        double pct = b.LifeEnergyCycle.Progress01 * 100.0;
        Body($"생산 주기 {pct:F0}% · 기본 +{b.LifeEnergyCycle.BasePayout:F0} / {b.LifeEnergyCycle.BaseCycleDurationSeconds:F0}초");
        if (_buildingManager != null)
            Caption($"생명 에너지 시설 {_buildingManager.ResourceBuildingCount}개 · 작업자를 넣으면 주기가 짧아지고 산출이 늘어납니다");
    }

    private void DrawUnitProductionFacilityBody(BuildingData b, bool coreActive)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"묶음 수량: {_batchCount}", GUIMenuStyleUtil.CompactBodyLabelStyle, GUILayout.Width(96f));
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
            GUI.enabled = prev && b.CanTakeReservations && roomSecured; // 코어 파괴 중에도 예약 가능(건설 명세 1-7-표1)
            for (int i = 0; i < b.AvailableRules.Count; i++)
            {
                ProductionRule rule = b.AvailableRules[i];
                UnitProductionSpec spec = rule.ToSpec();
                string costText = BuildCostText(rule.costs);
                if (Btn($"{rule.displayName} ×{_batchCount} ({costText}/기)"))
                {
                    ProductionEnqueueBlock block = b.ProductionQueue.TryEnqueue(
                        spec,
                        _batchCount,
                        _resourceManager.Accounts,
                        _resourceManager.IsSoulShardSpendUnlocked,
                        isSpeciesUnlocked: true,
                        isRoomSecured: roomSecured,
                        isCoreValid: true, // 새 생산 예약은 코어 파괴 중에도 가능 - 실제 생산은 가동 조건 재검사(건설 명세 1-7-표1)
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

        GUILayout.Space(4f);
        var entries = b.ProductionQueue.Entries;
        Body($"생산 대기열 ({entries.Count}건)");

        for (int i = 0; i < entries.Count; i++)
        {
            ProductionQueueEntry e = entries[i];
            double pct = e.ActiveProgress01 * 100.0;
            string stateLabel = e.IsBlockedWaitingSpawnUI
                ? FacilityProductionQueue.WaitingSpawnStatusText
                : (i == 0 && e.HasActiveUnit ? $"{pct:F0}%" : "대기");

            ButtonRow row = BeginButtonRow();
            bool cancel = RowButton(ref row, "취소", 44f);
            bool down = RowButton(ref row, "▼", 28f);
            bool up = RowButton(ref row, "▲", 28f);
            bool minus = RowButton(ref row, "-1", 30f);
            RowLabel(row, $"{i + 1}. {e.Spec.DisplayName} ×{e.TotalRemainingUnits} · {stateLabel}");

            if (e.IsBlockedWaitingSpawnUI)
                Caption($"└ {FacilityProductionQueue.WaitingSpawnReasonText}");

            if (minus)
                b.ProductionQueue.TryReduceWaitingCount(e.EntryId, 1, _resourceManager?.Accounts);
            if (up && i > 0 && _resourceManager != null)
                b.ProductionQueue.TryMoveEntry(e.EntryId, i - 1, _resourceManager.Accounts, _resourceManager.IsSoulShardSpendUnlocked);
            if (down && i + 1 < entries.Count && _resourceManager != null)
                b.ProductionQueue.TryMoveEntry(e.EntryId, i + 1, _resourceManager.Accounts, _resourceManager.IsSoulShardSpendUnlocked);
            if (cancel && _resourceManager != null)
            {
                b.ProductionQueue.CancelEntry(e.EntryId, _resourceManager.Accounts);
                break;
            }
        }
    }

    private void DrawCorpseRefineryFacilityBody(BuildingData b)
    {
        CorpseRefineryState refState = b.Refinery;
        Body($"용량: 보관 {refState.StoredCorpseCount} + 예약 {refState.ReservedIncomingCount} / 최대 {refState.MaxStorageSlots} (여유 {refState.AvailableSlots})");

        if (refState.ActiveRefiningCorpse != null)
        {
            double pct = refState.ActiveRefineProgress01 * 100.0;
            Body($"자동 정제 중: {refState.ActiveRefiningCorpse.DisplayName} ({pct:F0}%)");
        }
        else
        {
            Body("자동 정제 대기 중 (시체를 우클릭 회수해 입고)");
        }

        if (!refState.IsDraining)
        {
            if (Btn("전체 비우기 시작"))
                _buildingManager?.StartRefineryDrain(b);
        }
        else
        {
            Warn("※ 전체 비우기 진행 중 (신규 입고 차단)");
            if (Btn("전체 비우기 해제"))
                _buildingManager?.CancelRefineryDrain(b);
        }

        if (refState.WaitingCorpses.Count > 0)
        {
            GUILayout.Space(4f);
            Body($"대기 시체 ({refState.WaitingCorpses.Count}구)");
            for (int i = 0; i < refState.WaitingCorpses.Count; i++)
            {
                CorpseRecord c = refState.WaitingCorpses[i];
                ButtonRow row = BeginButtonRow();
                bool eject = RowButton(ref row, "반출", 48f);
                RowLabel(row, $"{c.DisplayName} (부패 {c.Decay01 * 100f:F0}%)");
                if (eject)
                {
                    _buildingManager?.EjectSingleWaitingCorpse(b, c.CorpseId);
                    break;
                }
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
            ButtonRow row = BeginButtonRow();
            bool cancel = RowButton(ref row, "취소(환급)", 78f);
            RowLabel(row, $"선두: {act.Definition.DisplayName} ({pct:F0}%)");
            if (cancel)
                b.ResearchQueue.CancelManually(act.Definition.ResearchId, _resourceManager.Accounts);
        }

        for (int idx = 1; idx < entries.Count; idx++)
        {
            var w = entries[idx];
            ButtonRow row = BeginButtonRow();
            bool cancel = RowButton(ref row, "취소", 44f);
            bool down = RowButton(ref row, "▼", 28f);
            bool up = RowButton(ref row, "▲", 28f);
            RowLabel(row, $"대기 {idx}. {w.Definition.DisplayName}");

            if (up)
            {
                if (!b.ResearchQueue.TryMoveEntry(w.Definition.ResearchId, idx - 1, out string moveErr) && !string.IsNullOrEmpty(moveErr))
                    NoticeCenter.Instance?.PushMomentary(moveErr, NoticeCenter.WarningColor);
            }
            if (down && idx + 1 < entries.Count)
            {
                if (!b.ResearchQueue.TryMoveEntry(w.Definition.ResearchId, idx + 1, out string moveErr) && !string.IsNullOrEmpty(moveErr))
                    NoticeCenter.Instance?.PushMomentary(moveErr, NoticeCenter.WarningColor);
            }
            if (cancel)
            {
                b.ResearchQueue.CancelManually(w.Definition.ResearchId, _resourceManager.Accounts);
                break;
            }
        }

        GUILayout.Space(4f);
        Body("연구 목록");
        foreach (var kv in ledger.Catalog)
        {
            ResearchDefinition def = kv.Value;
            if (ledger.IsCompleted(def.ResearchId))
            {
                Caption($"✓ {def.DisplayName} (완료)");
                continue;
            }

            bool preservedPaid = ledger.HasPreservedPayment(def.ResearchId);
            string costText = preservedPaid ? "재개 무료(지불 보존)" : $"생명 {def.BaseLifeEnergyCost:F0}, 영혼 {def.BaseSoulShardCost:F0}";
            bool prev = GUI.enabled;
            GUI.enabled = prev && b.CanTakeReservations && !ledger.IsQueuedAnywhere(def.ResearchId); // 코어 파괴 중에도 연구 선택 가능(건설 명세 1-7-표1)
            if (Btn($"{def.DisplayName} ({costText})"))
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
        if (session == null)
        {
            Body($"{room.Floor}층");
            return;
        }

        Body($"{room.Floor}층 · 소유 진영 {RoomStatusText.FactionName(room.RoomFaction)}");
        Body(RoomStatusText.Population(room, DefensePlacementSystem.Instance != null ? DefensePlacementSystem.Instance.IncomingPopulation(room) : 0));
        Body($"방 확보: {RoomStatusText.ClearText(session, room)}");
        Body($"인류 거점: {RoomStatusText.BaseText(session, room)}");

        string core = RoomStatusText.CoreText(session, room);
        if (core.Length > 0) Body($"코어: {core}");
    }

    // ── 월드 오브젝트 정보 ──

    private void DrawObjectInfo(InteractableObject obj)
    {
        Body($"위치 ({obj.Position.x}, {obj.Position.y}) · {obj.Position.z}층");

        string stats = null;
        if (TryGetObjectHp(obj, out float hp, out float maxHp))
            stats = $"체력 {hp:F0} / {maxHp:F0}";
        FactionType? owner = GetObjectOwnerFaction(obj);
        if (owner != null)
            stats = stats == null ? $"소유 진영 {owner.Value}" : $"{stats} · 소유 진영 {owner.Value}";
        if (stats != null) Body(stats);

        if (obj.Tags != null && obj.Tags.Contains("Object/Passable/Loot"))
        {
            string state = obj.IsCollected ? "회수됨" : obj.IsInvestigated ? "조사 완료(회수 대기)" : "미조사";
            Body($"상태: {state}");
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
        Body($"원본: {c.DisplayName} ({c.OriginFaction})");
        if (!c.CanBeRefined)
        {
            Body("부패도 100% — 정제 불가 (장애물로 잔존)");
        }
        else
        {
            var reward = CorpseRefineMath.CalculateReward(c);
            Body($"부패도 {c.Decay01 * 100f:F0}% · 예상 정제 보상 생명 에너지 {reward.LifeEnergy:F0}, 영혼 파편 {reward.TotalSoulShard:F0}");
            Caption("보관시설에 넣으면 부패가 멈춥니다 · 아군을 고른 뒤 우클릭해 회수·운반");
        }

        if (obj.CoexistingObjects != null && obj.CoexistingObjects.Count > 0)
            Caption($"같은 타일 추가 보존 대상 {obj.CoexistingObjects.Count}개");
    }

    private void DrawDismantledPackageInfo(InteractableObject obj)
    {
        BuildingData b = obj.DismantledBuildingPayload;
        Body($"분해된 시설: {b.DisplayName} (1타일 패키지)");
        if (b.ActiveRelocation != null)
            Body($"목적지: ({b.ActiveRelocation.TargetDestination.x}, {b.ActiveRelocation.TargetDestination.y})");
        Caption("아군을 고른 뒤 우클릭해 운반·재설치 (추가 비용 0)");
    }

    private void DrawDoorControls(InteractableObject door)
    {
        GameSession session = GameSession.Instance;
        if (session == null) return;

        Body($"상태: {(door.DoorManualOpen || door.DoorAutoOpen ? "열려 있음(누구나 통과)" : "닫힘(보유 진영만 통과)")}");

        // 일시정지 중엔 문이 움직이지 않으므로 지시를 예약하고, 문 앞·뒤 유닛 같은 상황은 재개 때 확인한다.
        bool paused = session.isPaused;
        bool hasPending = session.TryGetPendingDoorOrder(door, out bool pendingOpen);
        bool effectiveOpen = paused && hasPending ? pendingOpen : door.DoorManualOpen;
        bool wantOpen = !effectiveOpen;
        DoorControlBlock block = paused ? session.CheckDoorReservation(door, wantOpen) : session.CheckDoorControl(door, wantOpen);
        if (block == DoorControlBlock.NotPlayerDoor)
        {
            Caption(DoorControlRules.Describe(block));
            return;
        }
        if (paused && hasPending)
            Body($"예약됨: {(pendingOpen ? "열기" : "닫기")} — 시간을 재개하면 적용됩니다");

        bool prevEnabled = GUI.enabled;
        GUI.enabled = prevEnabled && block == DoorControlBlock.None;
        if (Btn(wantOpen ? "문 열기" : "문 닫기"))
        {
            if (paused) session.ReserveDoorOpen(door, wantOpen);
            else session.TrySetDoorOpen(door, wantOpen);
        }
        GUI.enabled = prevEnabled;

        string reason = DoorControlRules.Describe(block);
        if (reason.Length > 0) Warn(reason);

        DrawDoorExpansion(door);
    }

    // ── 문 확장(건설 명세 2-9) — 플레이어 소유 문의 양 끝 벽을 넓힌다. 비용은 등록 때 선점하고 첫 실제 작업에서 차감하며 그때 문이 열린다 ──
    private void DrawDoorExpansion(InteractableObject door)
    {
        if (_buildingManager == null) return;
        GUILayout.Space(4f);
        DoorExpansionTask task = _buildingManager.GetDoorExpansionOf(door);
        if (task == null)
        {
            string block = _buildingManager.GetDoorExpansionBlockReason(door, out _);
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && block == null;
            if (Btn($"문 확장 (생명 에너지 {BuildingManager.DoorExpansionCost})"))
            {
                if (!_buildingManager.TryRegisterDoorExpansion(door, out string why)) NoticeCenter.Instance?.PushMomentary(why, NoticeCenter.WarningColor);
            }
            GUI.enabled = prevEnabled;
            if (block != null) Warn($"문 확장 불가: {block}");
            return;
        }

        DoorExpansionSession s = task.Session;
        string wait = string.IsNullOrEmpty(task.WaitReason) ? "진행 중" : $"대기: {task.WaitReason}";
        Body($"[문 확장 {s.CurrentWidth}→{s.TargetWidth}] {s.Progress01 * 100.0:F0}% · 양 끝 {(s.IsEndDone(0) ? "완료" : "작업")}/{(s.IsEndDone(1) ? "완료" : "작업")} · {wait}");
        Caption(s.HasStartedWork ? "문이 열려 있습니다 · 한 명이 한쪽을 마치고 반대쪽으로 가서 끝낼 수 있습니다" : "작업자가 도착해 작업이 시작되면 비용이 차감되고 문이 열립니다");

        Body($"담당 작업자 {_buildingManager.CountDoorWorkers(task)}/{WorkSpeedMath.MaxWorkersPerTask} · 아군을 고른 뒤 문을 우클릭해도 배정됩니다");
        int shown = 0;
        foreach (DoorWorkerCandidate c in GetDoorWorkerCandidates(task))
        {
            if (++shown > 12) break;
            string name = c.Unit.unitType != null ? c.Unit.unitType.typeName : c.Unit.name;
            bool selectable = c.BlockReason == null;
            bool busyElsewhere = c.BusyElsewhere && !c.IsAssigned;
            string detail = !selectable ? $"선택 불가: {c.BlockReason}" : busyElsewhere ? $"{c.Activity}에서 빠짐 · 속도 x{c.Speed:F1}" : $"속도 x{c.Speed:F1}";

            Rect row = GUILayoutUtility.GetRect(0f, GUIMenuStyleUtil.ListRowHeight, GUILayout.ExpandWidth(true));
            if (GUIMenuStyleUtil.DrawListRow(row, $"{(c.IsAssigned ? "● " : "○ ")}{name}", busyElsewhere ? "다른 작업 중" : c.RoomName, detail, c.IsAssigned, selectable && GUI.enabled, !selectable, busyElsewhere))
            {
                _buildingManager.ToggleDoorWorker(c.Unit, task);
                _nextDoorCandidatesAt = 0f;
            }
            GUILayout.Space(2f);
        }

        if (Btn(s.HasStartedWork ? "문 확장 취소 (비용 환급)" : "문 확장 취소 (선점 해제)"))
            _buildingManager.CancelDoorExpansion(task);
    }

    private void DrawCoreControls(InteractableObject obj)
    {
        CoreSystem coreSystem = GameSession.Instance?.Core;
        if (coreSystem == null || !coreSystem.TryGetCoreAt(obj.Position, out Room room, out InteractableObject core)) return;

        CoreStatus status = CoreMath.StatusOf(core.CoreEverActivated, core.CoreDestroyed);
        Body($"코어 상태: {CoreMath.Describe(status)}");

        if (!core.CoreDestroyed)
        {
            if (core.CoreEverActivated) return;
            CoreActivateBlock block = coreSystem.CheckActivate(room, core);
            if (block == CoreActivateBlock.None)
            {
                if (Btn("완전 활성화"))
                    coreSystem.TryActivate(core.Position);
            }
            else
            {
                Warn($"활성화 불가: {CoreMath.Describe(block)}");
            }
            return;
        }

        Warn("이 층의 생산·자원·연구·정제와 문 제어가 정지 중입니다.");

        CoreRestoreSession activeRestore = coreSystem.GetActiveRestoreAt(core.Position);
        if (activeRestore != null)
        {
            Body($"수복 진행률 {activeRestore.Progress01 * 100.0:F0}% (수복 체력 {activeRestore.RestoreObjectCurrentHp:F0}/{activeRestore.RestoreObjectMaxHp:F0})");
            Caption("아군을 고른 뒤 코어를 우클릭해 작업자를 배정하세요");

            if (activeRestore.IsPausedWaitingDiffApproval)
            {
                Warn($"추가 피해 발생 — 차액 {activeRestore.UnapprovedDiffAmount} 생명 에너지 승인 필요");
                if (Btn($"추가 수복비 지불 ({activeRestore.UnapprovedDiffAmount})"))
                    coreSystem.TryApproveRestoreExtraDiff(core.Position);
            }

            if (Btn("코어 수복 취소 (지불액 환급)"))
                coreSystem.CancelRestoreManually(core.Position);
            return;
        }

        bool hasSalvage = coreSystem.HasAvailableSalvageInCoreRoom(room, out _);
        if (hasSalvage)
        {
            string toggleText = _useCoreSalvageToggle ? "■ 코어 잔해 1개 사용 (-50% 감면)" : "□ 코어 잔해 사용 안 함";
            if (Btn(toggleText, active: _useCoreSalvageToggle))
                _useCoreSalvageToggle = !_useCoreSalvageToggle;
        }

        bool applySalvage = hasSalvage && _useCoreSalvageToggle;
        int neededCost = EconomyMath.CeilCost(applySalvage ? CoreSystem.RestoreLifeEnergyCost * 0.5 : CoreSystem.RestoreLifeEnergyCost);
        CoreRestoreBlock restoreBlock = coreSystem.CheckRestore(room, core, useSalvage: applySalvage);
        if (restoreBlock == CoreRestoreBlock.None)
        {
            if (Btn($"코어 수복 등록 (생명 에너지 {neededCost})"))
                coreSystem.TryBeginRestoreOrder(core.Position, applySalvage, out _);
        }
        else
        {
            Warn($"수복 불가: {CoreMath.Describe(restoreBlock)} (필요 생명 에너지 {neededCost})");
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
            string kr = costs[i].resourceType == ResourceType.SoulShard ? "영혼" : "생명";
            sb.Append(kr).Append(' ').Append(costs[i].amount);
        }
        return sb.ToString();
    }
}
