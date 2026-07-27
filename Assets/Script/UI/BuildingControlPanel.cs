using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Haare.Client.Routine;
using Haare.Client.UI;
using VContainer;

// 건축물·자원·유닛 생산 MVP(2026-07-27) — B/V키로 지은 건물을 클릭하면 뜨는 조작 UI(스타크래프트 참고,
// 사용자 요청). DebugInfoPanel/StatusInfoPanel과 동일한 관례 — [PanelAttribute]로 등록된 얇은 UGUI
// 프리팹 껍데기 + 실제 인터랙션은 OnGUI로 그린다(이 프로젝트에서 이미 검증된 패턴).
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

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        panel = gameObject;
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        _current = null;
    }

    public void BindEvent() { }

    public void ShowForBuilding(BuildingData building)
    {
        _current = building;
        OpenPanel();
    }

    private const int PanelWidth = 260;
    private const int PanelHeight = 260;
    // DebugInfoPanel의 좌하단 선택 유닛 정보 박스(Assets/Editor/HaareUISetup.cs CreateDebugInfoPanelPrefab
    // — anchoredPosition(10,10), sizeDelta(240,480), 즉 x:10~250 구간을 차지)와 겹친다는 사용자 신고
    // (2026-07-27)로 그 오른쪽으로 옮겼다.
    private const int PanelX = 260;

    private Rect GetPanelRect() => new Rect(PanelX, Screen.height - PanelHeight - 10, PanelWidth, PanelHeight);

    // 사용자 신고(2026-07-27) "유닛 생산 시설 버튼 클릭 시 UI가 닫혀버림" — OnGUI(IMGUI)는 UGUI의
    // EventSystem.IsPointerOverGameObject()로 감지가 안 돼서, 패널 안 버튼을 클릭해도 그 클릭이 그대로
    // InputManager의 월드 클릭으로도 처리돼 "건물이 아닌 곳 클릭 → 패널 닫기" 분기를 타 버렸다.
    // InputManager가 좌클릭을 월드 입력으로 처리하기 전에 이 패널 영역 위인지 먼저 확인하도록 노출한다.
    public bool IsMouseOverPanel()
    {
        if (_current == null || Mouse.current == null) return false;
        Vector2 screenPos = Mouse.current.position.ReadValue(); // 화면 좌표(y=0이 아래)
        Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y); // OnGUI 좌표(y=0이 위)
        return GetPanelRect().Contains(guiPos);
    }

    private void OnGUI()
    {
        if (_current == null) return;

        // StatusInfoPanel(우상단)/DebugInfoPanel(우측 상단 버튼, 좌하단 유닛 정보)과 안 겹치도록
        // 좌하단에서 유닛 정보 박스 오른쪽(PanelX)으로 옮겨 띄운다.
        GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);

        if (_current.IsResourceBuilding)
        {
            GUILayout.Label("<size=14><b>[ 자원 생산 시설 ]</b></size>");
            GUILayout.Label("5초마다 Wood/Stone이 자동으로 증가합니다.");
            GUILayout.Label($"다음 증가까지: {BuildingManager.ResourceTickInterval - _current.ResourceTickTimer:F1}초");
            if (_buildingManager != null)
            {
                GUILayout.Label($"생산 건물 {_buildingManager.ResourceBuildingCount}개로 인해, 현재 생산량 초당 {_buildingManager.CurrentResourceProductionPerSecond:F1}개");
            }
        }
        else
        {
            GUILayout.Label("<size=14><b>[ 유닛 생산 시설 ]</b></size>");

            if (_current.AvailableRules != null)
            {
                foreach (var rule in _current.AvailableRules)
                {
                    string costText = BuildCostText(rule.costs);
                    if (GUILayout.Button($"{rule.displayName} ({costText})"))
                    {
                        if (_resourceManager != null && _resourceManager.TryConsumeResources(rule.costs))
                        {
                            _current.Queue.Enqueue(rule);
                        }
                    }
                }
            }

            GUILayout.Space(6);
            GUILayout.Label($"<b>대기열 ({_current.Queue.Count})</b>");

            int index = 0;
            ProductionRule toCancel = null;
            foreach (var queued in _current.Queue)
            {
                string progressText = index == 0 && _current.IsProducing
                    ? $" - 진행중 {_current.ProductionProgress:F1}/{queued.productionTime:F1}s"
                    : "";

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{index + 1}. {queued.displayName}{progressText}");
                // 취소해도 자원은 환불되지 않는다(문서 10장 "생산 취소 및 자원 환불" 제외 범위).
                if (GUILayout.Button("취소", GUILayout.Width(40)))
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
        if (GUILayout.Button("닫기"))
        {
            ClosePanel();
        }

        GUILayout.EndArea();
    }

    private static string BuildCostText(List<ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0) return "무료";
        var parts = new List<string>();
        foreach (var c in costs) parts.Add($"{c.resourceType} {c.amount}");
        return string.Join(", ", parts);
    }

    // Queue<T>는 임의 위치 제거를 지원하지 않아, 취소 대상만 뺀 새 큐로 다시 만든다(대기열 길이가
    // 짧아 성능은 문제되지 않음). 생산 진행 중인(맨 앞) 항목이 취소되면 진행도도 함께 리셋한다.
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
