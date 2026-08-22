using System.Collections.Generic;
using UnityEngine;
using Haare.Client.Routine;
using Haare.Client.UI;
using VContainer;

// 건축물·자원·유닛 생산 MVP(2026-07-27) — B/V키로 지은 건물을 클릭하면 뜨는 조작 UI(스타크래프트 참고,
// 사용자 요청). DebugInfoPanel/StatusInfoPanel과 동일한 관례 — [PanelAttribute]로 등록된 얇은 UGUI
// 프리팹 껍데기 + 실제 인터랙션은 OnGUI로 그린다(이 프로젝트에서 이미 검증된 패턴).
//
// 오브젝트 정보 조회 겸용(2026-08-22 후속 피드백, 사용자 요청 "모든 오브젝트는 이제 클릭을 통해
// 정보를 볼 수 있어(건물처럼)") — 코어/문/함정/전리품/시체/전멸흔적 등 InteractableObject 전반의
// 클릭 정보 표시도 이 클래스가 겸한다. 새 프리팹을 만들려면 Unity 에디터에서 GUID를 새로 발급받아야
// 하는데 이 환경에서는 에디터 실행이 불가능해 안전하게 만들 수 없다 — 이미 있는 이 패널(내용은 전부
// OnGUI라 프리팹 자체는 빈 껍데기, DebugInfoPanel의 "시야 표시" 토글 재사용 전례와 동일한 이유)의
// _current(건물)/_currentObject(오브젝트) 두 상태를 상호 배타로 관리해 확장했다.
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

    // 좌하단 패널 스택 구조 개선(2026-08-21, 사용자 요청 "빈틈이 안 당겨져" 재확인 + "구조 개선 필요한
    // 부분 있는지 체크") — 예전엔 OnGUI/IsMouseOverPanel이 `if (_current == null) return;`으로 먼저
    // 걸러진 뒤에야 GetPanelRect()(=Report 호출부)를 불러서, 패널이 닫히는 순간 "안 보인다"는 보고
    // 자체가 실행되지 않았다(BottomLeftPanelStack의 프레임 스윕이 안전망으로 뒤늦게 정리해주긴
    // 했지만 최대 1~2프레임 지연). UpdateProcess는 _current 상태와 무관하게 매 프레임 무조건
    // 실행되므로, 여기서 직접 보고하면 패널이 닫히는 바로 그 프레임에 즉시 정리된다 — 스윕은 이제
    // 이 보고를 놓치는 다른 소비자를 위한 이중 안전망일 뿐, 이 패널은 더 이상 거기 의존하지 않는다.
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

    // 오브젝트 정보 조회(2026-08-22 후속 피드백) — InputManager가 유닛도 건물도 아닌 오브젝트
    // (코어/문/함정/전리품/시체/전멸흔적)를 클릭했을 때 호출한다.
    public void ShowForObject(InteractableObject obj)
    {
        _currentObject = obj;
        _current = null;
        OpenPanel();
    }

    private const int PanelWidth = 260;
    private const int PanelHeight = 260;

    // 좌하단 패널 스택(2026-08-21, 사용자 요청 "유닛 정보 UI와 같은 방식으로... 스택형태로 좌우로
    // 쌓이게" → "새 창이 왼쪽으로 오는 형태가 아니라 오른쪽으로 늘어나는 형태로") — 지금 보이는 동안
    // 매 프레임 자기 폭을 보고하고 시작 X를 받아온다. 등록 순서 기반이라(BottomLeftPanelStack 주석
    // 참고) 이 패널이 먼저 떠 있었다면(예: 유닛을 아직 하나도 선택 안 한 채 건물부터 클릭) 나중에
    // 유닛 정보 UI가 나타나도 이 패널 위치는 그대로고, 유닛 정보 UI가 오른쪽에 새로 붙는다.
    private const string StackId = "BuildingControl";

    private Rect GetPanelRect()
    {
        float x = BottomLeftPanelStack.Report(StackId, _current != null || _currentObject != null, PanelWidth);
        float y = Screen.height - PanelHeight - BottomLeftPanelStack.GetReservedBottomHeight();
        return new Rect(x, y, PanelWidth, PanelHeight);
    }

    // 사용자 신고(2026-07-27) "유닛 생산 시설 버튼 클릭 시 UI가 닫혀버림" — OnGUI(IMGUI)는 UGUI의
    // EventSystem.IsPointerOverGameObject()로 감지가 안 돼서, 패널 안 버튼을 클릭해도 그 클릭이 그대로
    // InputManager의 월드 클릭으로도 처리돼 "건물이 아닌 곳 클릭 → 패널 닫기" 분기를 타 버렸다.
    // InputManager가 좌클릭을 월드 입력으로 처리하기 전에 이 패널 영역 위인지 먼저 확인하도록 노출한다.
    public bool IsMouseOverPanel()
    {
        if (_current == null && _currentObject == null) return false;
        return GUIMouseUtil.IsMouseOverRect(GetPanelRect());
    }

    // UI 스타일 통일(2026-08-21, 사용자 요청 "건물 선택시 정보 UI... 다른 메뉴들과 동일한 스타일로") —
    // 기본 Unity GUI 스킨(GUI.skin.box/Button/Label) 대신 BottomMenuBar/StatusInfoPanel과 같은
    // GUIMenuStyleUtil(어두운 패널 박스 + 흰 테두리 + 굵은 흰 글씨 + 채우기형 버튼)을 쓴다. 항목 개수가
    // 가변적인 목록(생산 가능 목록/대기열)이라 BottomMenuBar처럼 손으로 Rect를 계산하는 대신
    // GUIMenuStyleUtil.DrawFlatButtonLayout(GUILayout 흐름 안에서 같은 버튼 스타일을 그리는 래퍼)을 쓴다.
    private void OnGUI()
    {
        if (_current == null && _currentObject == null) return;

        // StatusInfoPanel(우상단)/DebugInfoPanel(우측 상단 버튼, 좌하단 유닛 정보)과 안 겹치도록
        // 좌하단에서 유닛 정보 박스 오른쪽(PanelX)으로 옮겨 띄운다.
        Rect rect = GetPanelRect();
        GUIMenuStyleUtil.DrawPanelBox(rect);

        GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 8, rect.width - 24, rect.height - 16));

        if (_currentObject != null)
        {
            DrawObjectInfo(_currentObject);
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
                    // 인구수 초과로 진행이 멈춰있으면(2026-07-28, 사용자 요청) 진행중 대신 중지됨을
                    // 표시한다 — BuildingManager.UpdateProcess가 이 동안 진행도를 안 늘려서 실제로도
                    // 값이 정지해 있다(예: 0.0/1.0s에서 그대로).
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

    // 오브젝트 정보 표시(2026-08-22 후속 피드백, 사용자 요청 "그 정보에는 만약 체력이 있는 오브젝트면
    // 체력 표기, 만약 진영 소유 가능한 오브젝트면 오브젝트 어디 진영 소유인지 표기, 해당 오브젝트의
    // 기본적인 정보 표기 등") — 코어/문/함정/전리품/시체/전멸흔적 등 모든 InteractableObject 공통.
    // 순수 조회용이라 버튼은 아래 공용 "닫기"뿐이다.
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

    // 체력이 있는 오브젝트(코어/문/함정)만 true를 반환한다 — 그 외(전리품/시체/전멸흔적)는 체력 개념이
    // 없으므로 hp/maxHp를 표시하지 않는다.
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

    // 진영 소유 가능한 오브젝트(코어/문)만 값을 반환한다. 코어는 자신이 물리적으로 속한 방의
    // Room.RoomFaction을 그대로 따르지만, 문은 2026-08-22부터 방 소유권과 분리된 고정값
    // (InteractableObject.DoorOwnerFaction — 점령으로 안 바뀌고 파괴+재설치로만 바뀜)을 쓴다.
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
