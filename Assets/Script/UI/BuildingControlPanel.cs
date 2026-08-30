using System.Collections.Generic;
using UnityEngine;
using Haare.Client.Routine;
using Haare.Client.UI;
using VContainer;

// B/V키로 지은 건물을 클릭하면 뜨는 조작 UI. DebugInfoPanel/StatusInfoPanel과 동일한 관례 —
// [PanelAttribute]로 등록된 얇은 UGUI 프리팹 껍데기 + 실제 인터랙션은 OnGUI로 그린다.
// 코어/문/함정/전리품/시체/전멸흔적 등 InteractableObject 전반의 클릭 정보 표시도 이 클래스가 겸한다 —
// 새 프리팹 GUID를 발급받을 수 없는 환경이라 _current(건물)/_currentObject(오브젝트) 두 상태를
// 상호 배타로 관리해 확장했다.
[PanelAttribute("Prefabs/BuildingControlPanel")]
public class BuildingControlPanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    // InputManager가 건물 클릭 시 바로 참조할 수 있도록(ResourceManager.Instance 등과 동일 관례).
    public static BuildingControlPanel Instance { get; private set; }

    private ResourceManager _resourceManager;
    private BuildingManager _buildingManager;
    private BuildingData _current;
    private InteractableObject _currentObject;

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

    // OnGUI 쪽 null 체크 뒤에서만 Report를 부르면 패널이 닫히는 순간의 보고가 누락돼 BottomLeftPanelStack
    // 정리가 몇 프레임 지연된다 — UpdateProcess는 상태와 무관하게 매 프레임 돌므로 여기서 직접 보고한다.
    protected override void UpdateProcess()
    {
        base.UpdateProcess();
        BottomLeftPanelStack.Report(StackId, _current != null || _currentObject != null, PanelWidth);
    }

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
    }

    public void BindEvent() { }

    public void ShowForBuilding(BuildingData building)
    {
        _current = building;
        _currentObject = null;
        OpenPanel();
    }

    // InputManager가 유닛도 건물도 아닌 오브젝트(코어/문/함정/전리품/시체/전멸흔적)를 클릭했을 때 호출한다.
    public void ShowForObject(InteractableObject obj)
    {
        _currentObject = obj;
        _current = null;
        OpenPanel();
    }

    private const int PanelWidth = 260;
    private const int PanelHeight = 260;

    // 좌하단 패널 스택 — 보이는 동안 매 프레임 자기 폭을 보고하고 시작 X를 받아온다(등록 순서 기반 정렬).
    private const string StackId = "BuildingControl";

    private Rect GetPanelRect()
    {
        float x = BottomLeftPanelStack.Report(StackId, _current != null || _currentObject != null, PanelWidth);
        float y = Screen.height - PanelHeight - BottomLeftPanelStack.GetReservedBottomHeight();
        return new Rect(x, y, PanelWidth, PanelHeight);
    }

    // OnGUI(IMGUI)는 UGUI의 EventSystem.IsPointerOverGameObject()로 감지가 안 돼 패널 안 버튼 클릭이
    // InputManager의 월드 클릭으로도 처리되던 문제 — 좌클릭 처리 전에 패널 영역 위인지 먼저 확인한다.
    public bool IsMouseOverPanel()
    {
        if (_current == null && _currentObject == null) return false;
        return GUIMouseUtil.IsMouseOverRect(GetPanelRect());
    }

    // 다른 OnGUI 패널이 이 패널과 실제로 겹치는지 판정할 때 쓴다.
    public bool TryGetVisibleRect(out Rect rect)
    {
        rect = default;
        if (_current == null && _currentObject == null) return false;
        rect = GetPanelRect();
        return true;
    }

    // 기본 Unity GUI 스킨 대신 GUIMenuStyleUtil을 쓴다. 항목 개수가 가변적이라 DrawFlatButtonLayout(GUILayout 래퍼)을 쓴다.
    private void OnGUI()
    {
        if (_current == null && _currentObject == null) return;

        // StatusInfoPanel/DebugInfoPanel과 안 겹치도록 좌하단 유닛 정보 박스 오른쪽으로 옮겨 띄운다.
        Rect rect = GetPanelRect();
        GUIMenuStyleUtil.DrawPanelBox(rect);

        GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 8, rect.width - 24, rect.height - 16));

        if (_currentObject != null)
        {
            DrawObjectInfo(_currentObject);
        }
        else if (_current.IsDummy)
        {
            // 디버그용 더미 건물 — 생산/자원 UI 없이 안내 문구만 표시한다.
            GUILayout.Label("[ 더미 건물 (디버그) ]", GUIMenuStyleUtil.LabelStyle);
            GUILayout.Label("기능 없음 — 건물 판정(타일 점유)만 있는 디버그용 오브젝트입니다.", GUIMenuStyleUtil.BodyLabelStyle);
        }
        else if (_current.IsResourceBuilding)
        {
            GUILayout.Label("[ 자원 생산 시설 ]", GUIMenuStyleUtil.LabelStyle);
            GUILayout.Label("5초마다 Wood/Stone이 자동으로 증가합니다.", GUIMenuStyleUtil.BodyLabelStyle);
            GUILayout.Label($"다음 증가까지: {BuildingManager.ResourceTickInterval - _current.ResourceTickTimer:F1}초", GUIMenuStyleUtil.BodyLabelStyle);
            if (_buildingManager != null)
            {
                GUILayout.Label($"생산 건물 {_buildingManager.ResourceBuildingCount}개로 인해, 현재 생산량 초당 {_buildingManager.CurrentResourceProductionPerSecond:F1}개", GUIMenuStyleUtil.BodyLabelStyle);
            }
        }
        else
        {
            GUILayout.Label("[ 유닛 생산 시설 ]", GUIMenuStyleUtil.LabelStyle);

            if (_current.AvailableRules != null)
            {
                foreach (var rule in _current.AvailableRules)
                {
                    string costText = BuildCostText(rule.costs);
                    if (GUIMenuStyleUtil.DrawFlatButtonLayout($"{rule.displayName} ({costText})", options: new[] { GUILayout.Height(28f) }))
                    {
                        if (_resourceManager != null && _resourceManager.TryConsumeResources(rule.costs))
                        {
                            _current.Queue.Enqueue(rule);
                        }
                    }
                }
            }

            GUILayout.Space(6);
            GUILayout.Label($"대기열 ({_current.Queue.Count})", GUIMenuStyleUtil.BodyLabelStyle);

            int index = 0;
            ProductionRule toCancel = null;
            foreach (var queued in _current.Queue)
            {
                string progressText = "";
                if (index == 0 && _current.IsProducing)
                {
                    // 인구수 초과로 진행이 멈춰있으면 진행중 대신 중지됨을 표시한다.
                    progressText = _current.WaitingForRoomSpace
                        ? $" - 중지됨(방 인원 초과) {_current.ProductionProgress:F1}/{queued.productionTime:F1}s"
                        : $" - 진행중 {_current.ProductionProgress:F1}/{queued.productionTime:F1}s";
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{index + 1}. {queued.displayName}{progressText}", GUIMenuStyleUtil.BodyLabelStyle);
                // 취소해도 자원은 환불되지 않는다(문서 10장 "생산 취소 및 자원 환불" 제외 범위).
                if (GUIMenuStyleUtil.DrawFlatButtonLayout("취소", options: new[] { GUILayout.Width(50f), GUILayout.Height(24f) }))
                {
                    toCancel = queued;
                }
                GUILayout.EndHorizontal();
                index++;
            }

            if (toCancel != null)
            {
                RemoveFirstFromQueue(_current, toCancel);
            }
        }

        GUILayout.Space(6);
        if (GUIMenuStyleUtil.DrawFlatButtonLayout("닫기", options: new[] { GUILayout.Height(28f) }))
        {
            ClosePanel();
        }

        GUILayout.EndArea();
    }

    // 모든 InteractableObject 공통 정보 표시 — 체력이 있으면 체력, 진영 소유 가능하면 소유 진영을 표기한다.
    private void DrawObjectInfo(InteractableObject obj)
    {
        GUILayout.Label($"[ {GetObjectDisplayName(obj)} ]", GUIMenuStyleUtil.LabelStyle);
        GUILayout.Label($"위치: ({obj.Position.x}, {obj.Position.y}) / {obj.Position.z}층", GUIMenuStyleUtil.BodyLabelStyle);

        if (TryGetObjectHp(obj, out float hp, out float maxHp))
        {
            GUILayout.Label($"체력: {hp:F0} / {maxHp:F0}", GUIMenuStyleUtil.BodyLabelStyle);
        }

        FactionType? owner = GetObjectOwnerFaction(obj);
        if (owner != null)
        {
            GUILayout.Label($"소유 진영: {owner.Value}", GUIMenuStyleUtil.BodyLabelStyle);
        }

        if (obj.Tags != null && obj.Tags.Contains("Object/Passable/Loot"))
        {
            string state = obj.IsCollected ? "회수됨" : obj.IsInvestigated ? "조사 완료(회수 대기)" : "미조사";
            GUILayout.Label($"상태: {state}", GUIMenuStyleUtil.BodyLabelStyle);
        }
    }

    private static string GetObjectDisplayName(InteractableObject obj)
    {
        if (obj.Tags == null) return "오브젝트";
        if (obj.Tags.Contains(DoorSystem.DoorTag)) return "문";
        if (obj.Tags.Contains(GameSession.CoreTag)) return "코어";
        if (obj.Tags.Contains("Object/Building/Passable/Trap")) return "함정";
        if (obj.Tags.Contains("Object/Passable/Corpse"))
            return obj.Tags.Contains("Human") ? "인류 시체" : "몬스터 시체";
        if (obj.Tags.Contains("Object/Passable/WipeoutTrace")) return "전멸 흔적";
        if (obj.Tags.Contains("Object/Passable/Loot")) return "전리품";
        return "오브젝트";
    }

    // 체력이 있는 오브젝트(코어/문/함정)만 true를 반환한다 — 그 외는 체력 개념이 없다.
    private static bool TryGetObjectHp(InteractableObject obj, out float hp, out float maxHp)
    {
        if (obj.Tags != null && obj.Tags.Contains(DoorSystem.DoorTag) && obj.DoorMaxHp > 0f)
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

    // 진영 소유 가능한 오브젝트(코어/문)만 값을 반환한다. 코어는 속한 방의 Room.RoomFaction을 따르지만,
    // 문은 방 소유권과 분리된 고정값(DoorOwnerFaction, 파괴+재설치로만 변경)을 쓴다.
    private static FactionType? GetObjectOwnerFaction(InteractableObject obj)
    {
        if (obj.Tags == null) return null;
        if (obj.Tags.Contains(DoorSystem.DoorTag)) return obj.DoorOwnerFaction;
        if (!obj.Tags.Contains(GameSession.CoreTag) || GameSession.Instance == null) return null;
        return GameSession.Instance.roomGrid.TryGetValue(obj.Position, out Room room) && room != null
            ? room.RoomFaction
            : (FactionType?)null;
    }

    private static string BuildCostText(List<ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0) return "무료";
        if (costs.Count == 1) return $"{costs[0].resourceType} {costs[0].amount}";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < costs.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(costs[i].resourceType).Append(' ').Append(costs[i].amount);
        }
        return sb.ToString();
    }

    // Queue<T>는 임의 위치 제거를 지원하지 않아 취소 대상만 뺀 새 큐로 다시 만든다. 생산 진행 중인
    // (맨 앞) 항목이 취소되면 진행도도 함께 리셋한다.
    private static void RemoveFirstFromQueue(BuildingData building, ProductionRule toRemove)
    {
        bool removedFront = building.IsProducing && building.Queue.Count > 0 && ReferenceEquals(building.Queue.Peek(), toRemove);

        var remaining = new Queue<ProductionRule>();
        bool removedOnce = false;
        foreach (var item in building.Queue)
        {
            if (!removedOnce && ReferenceEquals(item, toRemove))
            {
                removedOnce = true;
                continue;
            }
            remaining.Enqueue(item);
        }
        building.Queue = remaining;

        if (removedFront)
        {
            building.IsProducing = false;
            building.ProductionProgress = 0f;
        }
    }
}
