using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Client.UI;
using Haare.Scripts.Client.Data;
using Haare.Util.Loader;
using Haare.Util.Logger;

// 좌측 하단 상시 카테고리(명령/설치/도감/debug) 메뉴 바 — 카테고리 클릭 시 위에 서브메뉴가 뜨고
// 재클릭/전환 시 닫힌다. "맵" 카테고리만 별도 좌측 중앙 패널(DrawFloorPanel)로 분리돼 있다.
// [PanelAttribute]는 빈 프리팹 껍데기이고 실제 그리기는 OnGUI가 담당한다(다른 커스텀 패널과 동일
// 관례). 오브젝트/코어 배치는 기획 문서의 "설치" 메뉴에 없어 debug 서브탭으로 분류했다.
[PanelAttribute("Prefabs/BottomMenuBar")]
public class BottomMenuBar : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    // InputManager/BuildPlacementController 등 정적 접근이 필요한 쪽(BuildingControlPanel.Instance와
    // 동일 관례)이 "지금 마우스가 이 바/서브메뉴 위에 있는가"를 물어볼 때 쓴다.
    public static BottomMenuBar Instance { get; private set; }

    private InputManager _inputManager;
    private GameSession _gameSession;
    private DataManager _dataManager;
    private PropagationDebugVisualizer _propagationDebugVisualizer;

    [Inject]
    public void Construct(InputManager inputManager, GameSession gameSession, DataManager dataManager, PropagationDebugVisualizer propagationDebugVisualizer)
    {
        _inputManager = inputManager;
        _gameSession = gameSession;
        _dataManager = dataManager;
        _propagationDebugVisualizer = propagationDebugVisualizer;
    }

    protected override void Constructor()
    {
        base.Constructor();
        Instance = this;

        // 층 변경 패널을 다른 UI보다 뒤로 깔기 위한 별도 컴포넌트(FloorPanelOverlay.cs 주석 참고) —
        // 새 프리팹/GUID 없이 이 GameObject에 런타임으로 붙인다.
        if (gameObject.GetComponent<FloorPanelOverlay>() == null)
            gameObject.AddComponent<FloorPanelOverlay>();
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        panel = gameObject;
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    public void BindEvent() { }

    // 모드 진입/전환 안내는 전부 NoticeCenter.Push(클릭 시 한 번 뜨고 자동 소멸) 방식으로 통일돼
    // 있다 — PushPersistent로 "지금 활성 모드"를 계속 띄워두지 않는다.

    private enum MenuCategory { None, Command, Build, Encyclopedia, Debug }
    private MenuCategory _activeCategory = MenuCategory.None;

    private const float BarHeight = 60f;
    private const float BarButtonWidth = 132f;
    private const float BarMarginX = 10f;
    private const float BarMarginY = 10f;
    private const float BarGap = 6f;

    private const float SubmenuButtonHeight = 41f;
    private const float SubmenuButtonWidth = 235f;
    private const float SubmenuGap = 6f;
    private const float SubmenuBottomGap = 8f;
    // debug 서브메뉴처럼 항목이 많아 세로로 다 못 쌓일 때 옆 열로 넘기는 데 쓰는 열 간격.
    private const float SubmenuColumnGap = 10f;

    // 층 이동 상시 패널(DrawFloorPanel) 전용 버튼 크기 — 서브메뉴 버튼보다 작게 별도 상수를 둔다.
    private const float FloorPanelButtonWidth = 90f;
    private const float FloorPanelButtonHeight = 28f;
    private const float FloorPanelGap = 4f;

    // 버튼 폰트/흰 테두리 스타일은 GUIMenuStyleUtil로 옮겼다 — DebugInfoPanel 탭 버튼과 스타일을
    // 공유하기 위함.

    // "이 화면 좌표가 바 위인가"를 물어보는 공개 API(BuildingControlPanel.IsMouseOverPanel과 동일
    // 관례) — 안 그러면 바 버튼 클릭이 월드 클릭(유닛 선택/건물 배치)으로도 처리된다.
    public bool IsMouseOverUI()
    {
        if (GUIMouseUtil.IsMouseOverRect(GetBarRect())) return true;
        if (GUIMouseUtil.IsMouseOverRect(GetFloorPanelRect())) return true;
        return _activeCategory != MenuCategory.None && GUIMouseUtil.IsMouseOverRect(GetSubmenuBoundingRect());
    }

    // 다른 좌하단 UI가 메뉴 UI와 겹치지 않게 쌓일 수 있도록, 하단 바+서브메뉴가 차지하는 총 높이를
    // 반환한다. 서브메뉴 개폐에 따라 값이 바뀌므로 호출부는 매 프레임 다시 물어봐야 한다.
    public float GetReservedBottomLeftHeight()
    {
        float height = BarMarginY + BarHeight;
        if (_activeCategory != MenuCategory.None)
        {
            float submenuHeight = GetSubmenuBoundingRect().height;
            if (submenuHeight > 0f) height += SubmenuBottomGap + submenuHeight;
        }
        return height;
    }

    private Rect GetBarRect()
    {
        // "맵" 버튼이 상시 좌측 패널(DrawFloorPanel)로 빠지면서 5개로 줄었다.
        int buttonCount = 5;
        float width = buttonCount * BarButtonWidth + (buttonCount - 1) * BarGap;
        return new Rect(BarMarginX, Screen.height - BarMarginY - BarHeight, width, BarHeight);
    }

    // 서브메뉴 항목이 화면 위로 넘치지 않고 세로로 쌓일 수 있는 최대 개수(=열 하나의 최대 행 수) —
    // 이 값을 넘으면 DrawVerticalSubmenu가 옆 열로 넘겨서 그린다.
    private int GetMaxSubmenuRows()
    {
        float barY = Screen.height - BarMarginY - BarHeight;
        const float topMargin = 10f;
        float maxHeight = barY - SubmenuBottomGap - topMargin;
        if (maxHeight <= 0f) return 1;
        int rows = Mathf.FloorToInt((maxHeight + SubmenuGap) / (SubmenuButtonHeight + SubmenuGap));
        return Mathf.Max(1, rows);
    }

    // 지금 활성 카테고리가 그릴 "독립된 블록" 목록. debug는 항목이 많아 소리·전파 시각화 토글을 별도
    // 블록으로 분리해 목록 높이를 줄였다. Build*Items()가 유일한 진실 공급원이라 개수를 별도 상수로
    // 맞춰둘 필요가 없다 — 손으로 세면 목록이 바뀔 때 마우스오버 판정 영역이 어긋난다.
    private List<List<SubmenuItem>> GetActiveSubmenuGroups()
    {
        var groups = new List<List<SubmenuItem>>();
        switch (_activeCategory)
        {
            case MenuCategory.Command: groups.Add(BuildCommandItems()); break;
            case MenuCategory.Build: groups.Add(BuildBuildItems()); break;
            case MenuCategory.Encyclopedia: groups.Add(BuildEncyclopediaItems()); break;
            case MenuCategory.Debug:
                var primary = BuildDebugPrimaryItems();
                if (primary.Count > 0) groups.Add(primary);
                var secondary = BuildDebugSecondaryItems();
                if (secondary.Count > 0) groups.Add(secondary);
                var propagation = BuildDebugPropagationItems();
                if (propagation.Count > 0) groups.Add(propagation);
                break;
        }
        return groups;
    }

    // 지금 활성 카테고리의 모든 블록을 합친 전체 차지 영역(마우스오버 판정/다른 UI가 피할 예약 높이에
    // 쓰인다) — 블록마다 필요한 열 수는 이어 붙이고, 높이는 그중 가장 긴 열 기준으로 잡는다.
    private Rect GetSubmenuBoundingRect()
    {
        var groups = GetActiveSubmenuGroups();
        if (groups.Count == 0) return new Rect(0, 0, 0, 0);

        int maxRows = GetMaxSubmenuRows();
        int totalColumns = 0;
        int rowsUsed = 0;
        foreach (var group in groups)
        {
            if (group.Count == 0) continue;
            totalColumns += Mathf.CeilToInt((float)group.Count / maxRows);
            rowsUsed = Mathf.Max(rowsUsed, Mathf.Min(group.Count, maxRows));
        }
        if (totalColumns == 0) return new Rect(0, 0, 0, 0);

        float barY = Screen.height - BarMarginY - BarHeight;
        float height = rowsUsed * SubmenuButtonHeight + Mathf.Max(0, rowsUsed - 1) * SubmenuGap;
        float width = totalColumns * SubmenuButtonWidth + Mathf.Max(0, totalColumns - 1) * SubmenuColumnGap;
        return new Rect(BarMarginX, barY - SubmenuBottomGap - height, width, height);
    }

    private void OnGUI()
    {
        DrawBar();
        DrawSubmenu();
        // DrawFloorPanel()은 더 이상 여기서 안 부른다 — FloorPanelOverlay(낮은 실행 순서로 먼저
        // 그려짐)가 대신 호출해서, 겹칠 때 다른 UI가 층 패널을 덮도록 한다(위 Constructor 주석 참고).
    }

    private void DrawBar()
    {
        Rect barRect = GetBarRect();
        float x = barRect.x;
        float y = barRect.y;

        DrawBarButton(ref x, y, "명령", _activeCategory == MenuCategory.Command, () => ToggleCategory(MenuCategory.Command));
        DrawBarButton(ref x, y, "설치", _activeCategory == MenuCategory.Build, () => ToggleCategory(MenuCategory.Build));
        // debug는 정보 열람·시각화 위주라 "명령"의 다른 토글들과 공존 가능 — 유일한 예외. 도감은 아직
        // 기능이 없지만 카테고리 토글 + 서브메뉴(버튼 1개, "구현 예정")로 동작한다.
        DrawBarButton(ref x, y, "도감", _activeCategory == MenuCategory.Encyclopedia, () => ToggleCategory(MenuCategory.Encyclopedia));
        DrawBarButton(ref x, y, "debug", _activeCategory == MenuCategory.Debug, () => ToggleCategory(MenuCategory.Debug), cancelCommandMode: false);
    }

    private void DrawBarButton(ref float x, float y, string label, bool active, Action onClick,
        bool cancelCommandMode = true)
    {
        Rect rect = new Rect(x, y, BarButtonWidth, BarHeight);
        bool clicked = GUIMenuStyleUtil.DrawFlatButton(rect, label, active);
        if (clicked)
        {
            // 하단 바의 어떤 버튼을 누르든 진행 중이던 건물·오브젝트 배치 모드는 함께 취소된다 —
            // 우클릭 취소가 없는 대신 "메뉴 바꾸기"가 취소 경로다.
            _inputManager?.ExitActivePlacementMode();
            if (cancelCommandMode) _inputManager?.CancelCommandModeIfActive();
            onClick();
        }
        x += BarButtonWidth + BarGap;
    }

    private void ToggleCategory(MenuCategory category)
    {
        _activeCategory = (_activeCategory == category) ? MenuCategory.None : category;
    }

    // 지금 활성 카테고리의 블록들을 순서대로 그린다 — 블록마다 자기가 실제로 쓴 열 수만큼 다음 블록의
    // 시작 열을 밀어서, 블록끼리 겹치지 않고 옆으로 나란히 놓이게 한다.
    private void DrawSubmenu()
    {
        var groups = GetActiveSubmenuGroups();
        int maxRows = GetMaxSubmenuRows();
        int columnOffset = 0;
        foreach (var group in groups)
        {
            DrawVerticalSubmenu(group, columnOffset);
            columnOffset += Mathf.CeilToInt((float)group.Count / maxRows);
        }
    }

    // 도감 서브메뉴 — 아직 기능이 없어 비활성 버튼 1개("구현 예정")만 둔다.
    private List<SubmenuItem> BuildEncyclopediaItems()
    {
        return new List<SubmenuItem> { new SubmenuItem("구현 예정", false, false, null) };
    }

    private struct SubmenuItem
    {
        public string label;
        public bool active;
        public bool interactable;
        public Action onClick;
        // 주제별 구분용 헤더 행 — 버튼이 아니라 굵은 라벨만 그린다(클릭 불가, 테두리/배경 없음).
        public bool isHeader;

        public SubmenuItem(string label, bool active, bool interactable, Action onClick)
        {
            this.label = label;
            this.active = active;
            this.interactable = interactable;
            this.onClick = onClick;
            this.isHeader = false;
        }

        public static SubmenuItem Header(string label) => new SubmenuItem(label, false, false, null) { isHeader = true };
    }

    // 바 위에 아래에서 위로 쌓이는 세로 버튼 목록 하나를 그린다. 헤더 항목은 굵은 라벨로만 그려
    // 구분한다. startColumn은 이 블록의 시작 열(다른 블록과 나란히 놓을 때 씀).
    private void DrawVerticalSubmenu(List<SubmenuItem> items, int startColumn = 0)
    {
        Rect barRect = GetBarRect();
        int maxRows = GetMaxSubmenuRows();

        for (int i = 0; i < items.Count; i++)
        {
            // 한 열에 다 못 쌓일 만큼 항목이 많으면(debug 서브메뉴) 옆 열로 넘겨서 그린다 — 화면 위로
            // 넘치는 대신 옆으로 늘어난다.
            int col = startColumn + i / maxRows;
            int row = i % maxRows;
            float x = barRect.x + col * (SubmenuButtonWidth + SubmenuColumnGap);
            float y = barRect.y - SubmenuBottomGap - SubmenuButtonHeight - row * (SubmenuButtonHeight + SubmenuGap);

            var item = items[i];
            Rect rect = new Rect(x, y, SubmenuButtonWidth, SubmenuButtonHeight);

            if (item.isHeader)
            {
                Color prevColor = GUI.color;
                GUI.color = Color.white;
                GUI.Label(rect, item.label, HeaderStyle);
                GUI.color = prevColor;
            }
            else
            {
                if (GUIMenuStyleUtil.DrawFlatButton(rect, item.label, item.active, item.interactable))
                    item.onClick?.Invoke();
            }
        }
    }

    private GUIStyle _headerStyle;
    private GUIStyle HeaderStyle
    {
        get
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = GUIMenuStyleUtil.ButtonFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
            }
            return _headerStyle;
        }
    }

    // =====================================================
    // 명령 서브메뉴 — "제자리 공격"/"명령 취소"/"집결 및 정지" 세 토글은 InputManager에서 서로 배타로
    // 관리된다(Set*ModeActive). 기본 우클릭 이동/공격은 토글 없는 기본 동작이라 항목이 없다 —
    // 나머지 토글이 모두 꺼져 있으면 항상 이동/공격이 나간다.
    // =====================================================
    private List<SubmenuItem> BuildCommandItems()
    {
        return new List<SubmenuItem>
        {
            new SubmenuItem("제자리 공격", _inputManager != null && _inputManager.IsStandGroundModeActive, true, OnClickToggleStandGroundMode),
            new SubmenuItem("명령 취소", _inputManager != null && _inputManager.IsCancelCommandModeActive, true, OnClickToggleCancelCommandMode),
            new SubmenuItem("집결 및 정지", _inputManager != null && _inputManager.IsRallyHaltModeActive, true, OnClickToggleRallyHaltMode),
        };
    }

    // R키 몬스터 배치 모드 폐기를 대체 — "명령 취소"/"집결 및 정지"와 동일한 토글 패턴.
    private void OnClickToggleStandGroundMode()
    {
        if (_inputManager == null) return;
        bool newState = !_inputManager.IsStandGroundModeActive;
        _inputManager.SetStandGroundModeActive(newState);
        NoticeCenter.Instance?.PushMomentary(
            newState
                ? "제자리 공격 모드 켜짐: 유닛을 선택하고 우클릭하면 그 위치에서 이동 없이 사거리 내 적만 공격합니다(새 명령/명령 취소 전까지 해제 불가)."
                : "제자리 공격 모드 꺼짐.",
            NoticeCenter.InfoColor);
    }

    private void OnClickToggleCancelCommandMode()
    {
        if (_inputManager == null) return;
        bool newState = !_inputManager.IsCancelCommandModeActive;
        _inputManager.SetCancelCommandModeActive(newState);
        NoticeCenter.Instance?.PushMomentary(
            newState ? "명령 취소 모드 켜짐: 유닛을 선택하고 우클릭하면 그 유닛들의 명령이 즉시 취소됩니다." : "명령 취소 모드 꺼짐.",
            NoticeCenter.InfoColor);
    }

    private void OnClickToggleRallyHaltMode()
    {
        if (_inputManager == null) return;
        bool newState = !_inputManager.IsRallyHaltModeActive;
        _inputManager.SetRallyHaltModeActive(newState);
        NoticeCenter.Instance?.PushMomentary(
            newState
                ? "집결 및 정지 모드 켜짐: 유닛을 선택하고 우클릭하면 그 위치로 이동한 뒤 정지 상태가 됩니다(새 명령/명령 취소 전까지 해제 불가)."
                : "집결 및 정지 모드 꺼짐.",
            NoticeCenter.InfoColor);
    }

    // =====================================================
    // 설치 서브메뉴 — 유닛 생산 건물(B) / 자원 생산 건물(V) / 함정(P) / 문 재설치(자원 소모). 각
    // 버튼은 자기 모드 전용 플래그로만 활성 표시된다(공유 플래그를 쓰면 다른 모드도 켜진 것처럼
    // 보임). 이미 켜진 버튼을 다시 누르면 취소하고, 클릭 후에도 카테고리는 닫지 않는다.
    // =====================================================
    private List<SubmenuItem> BuildBuildItems()
    {
        bool unitActive = _inputManager != null && _inputManager.IsUnitBuildModeActive;
        bool resourceActive = _inputManager != null && _inputManager.IsResourceBuildModeActive;
        bool trapActive = _inputManager != null && _inputManager.IsTrapPlacementActive;
        // 문 재설치 — 원래 debug 서브메뉴(BuildDebugPrimaryItems)에 있던 걸 여기로 이전.
        bool doorRepairActive = _inputManager != null && _inputManager.IsDoorRepairPlacementActive;

        return new List<SubmenuItem>
        {
            new SubmenuItem("유닛 생산 건물", unitActive, true, () => ToggleBuildSubMode(unitActive, () => _inputManager?.EnterUnitBuildMode(), "유닛 생산 건물")),
            new SubmenuItem("자원 생산 건물", resourceActive, true, () => ToggleBuildSubMode(resourceActive, () => _inputManager?.EnterResourceBuildMode(), "자원 생산 건물")),
            new SubmenuItem("함정", trapActive, true, () => ToggleBuildSubMode(trapActive, () => _inputManager?.EnterTrapPlacementMode(), "함정")),
            new SubmenuItem("문 재설치", doorRepairActive, true, () => ToggleBuildSubMode(doorRepairActive, () => _inputManager?.EnterDoorRepairPlacementMode(), "문 재설치")),
        };
    }

    // wasActive면(재클릭) 취소, 아니면 enterMode로 진입 — "설치"/"debug" 서브메뉴의 배치류 버튼이
    // 공유하는 재클릭 취소 규칙. 취소 notice는 InputManager.ExitActivePlacementMode가 담당한다.
    private void ToggleBuildSubMode(bool wasActive, Action enterMode, string label)
    {
        if (wasActive)
        {
            _inputManager?.ExitActivePlacementMode();
        }
        else
        {
            enterMode?.Invoke();
            NoticeCenter.Instance?.PushMomentary($"{label} 배치 모드 시작 (우클릭: 설치, 취소: 메뉴 전환/재클릭)", NoticeCenter.InfoColor);
        }
    }

    // =====================================================
    // 층 이동 상시 패널 — 화면 좌측 중앙에 항상 떠 있는 층 버튼 목록, CameraController.GoToFloor와
    // 연동. 카테고리 토글 없이 항상 그려지는 독립 패널이다. IsFloorRevealed로 아직 안 밝혀진 층은
    // 버튼 자체를 리스트에서 뺀다.
    // =====================================================
    private Rect GetFloorPanelRect()
    {
        int revealedCount = CountRevealedFloors();
        if (revealedCount == 0) return new Rect(0f, 0f, 0f, 0f);

        float height = revealedCount * FloorPanelButtonHeight + (revealedCount - 1) * FloorPanelGap;
        float y = (Screen.height - height) * 0.5f;
        return new Rect(BarMarginX, y, FloorPanelButtonWidth, height);
    }

    private static int CountRevealedFloors()
    {
        var cam = CameraController.Instance;
        int floorCount = 4;
        if (cam == null || !cam.TryGetFloorCount(out floorCount)) floorCount = 4;

        int count = 0;
        for (int f = 0; f < floorCount; f++)
            if (cam != null && cam.IsFloorRevealed(f)) count++;
        return count;
    }

    // FloorPanelOverlay가 호출한다(다른 UI보다 뒤로 깔기 위해 실행 순서가 이른 별도 컴포넌트로
    // 분리 — 위 Constructor/FloorPanelOverlay.cs 주석 참고).
    public void DrawFloorPanel()
    {
        var cam = CameraController.Instance;
        int currentFloor = cam != null ? cam.CurrentFloor : -1;
        int floorCount = 4;
        if (cam == null || !cam.TryGetFloorCount(out floorCount)) floorCount = 4;

        Rect panelRect = GetFloorPanelRect();
        if (panelRect.height <= 0f) return;

        // 다른 UI와 실제로 겹치면 이번 프레임엔 층 패널을 그리지 않는다 — DebugInfoPanel(uGUI
        // Canvas)은 OnGUI보다 먼저 그려지는 별개 렌더 패스라 실행 순서만으로는 못 가릴 수 있어
        // 겹치는 쪽이 양보한다.
        if (DebugInfoPanel.Instance != null && DebugInfoPanel.Instance.TryGetVisibleInfoBoxRect(out Rect infoRect) && panelRect.Overlaps(infoRect))
            return;
        if (BuildingControlPanel.Instance != null && BuildingControlPanel.Instance.TryGetVisibleRect(out Rect buildRect) && panelRect.Overlaps(buildRect))
            return;
        // 하단 바 자신의 서브메뉴(debug 등)도 같은 클래스 안에서 그려져 실행 순서 트릭이 적용되지
        // 않으므로 같은 방식으로 직접 겹침을 확인한다.
        if (_activeCategory != MenuCategory.None && panelRect.Overlaps(GetSubmenuBoundingRect()))
            return;

        float y = panelRect.y;
        for (int f = 0; f < floorCount; f++)
        {
            if (cam == null || !cam.IsFloorRevealed(f)) continue;

            // 0층은 "던전입구"로 표시 — 그 외 층은 "N층".
            string label = f == 0 ? "던전입구" : $"{f}층";
            Rect rect = new Rect(panelRect.x, y, FloorPanelButtonWidth, FloorPanelButtonHeight);
            // 상시 패널로 빠지면서 버튼 자체가 항상 보이는 즉시 피드백이라, 클릭할 때마다 뜨던 notice는 뺐다.
            if (GUIMenuStyleUtil.DrawFlatButton(rect, label, f == currentFloor))
            {
                CameraController.Instance?.GoToFloor(f);
            }
            y += FloorPanelButtonHeight + FloorPanelGap;
        }
    }

    // =====================================================
    // debug 서브메뉴 — 시야/소리·전파 시각화, 오브젝트/코어 배치, 맵 저장/불러오기, 유닛 테스트.
    // 항목이 많아 헤더로 주제별 구분하고 Primary/Secondary/Propagation 세 블록으로 나눠 옆으로
    // 나란히 그린다(한 열이 화면 높이를 넘지 않도록).
    // =====================================================
    private List<SubmenuItem> BuildDebugPrimaryItems()
    {
        var items = new List<SubmenuItem>();

        // DrawVerticalSubmenu는 바로부터 위로 쌓아 그리므로 리스트 앞쪽 항목일수록 화면상 아래에
        // 그려진다 — 헤더를 그 주제 내용(버튼)들보다 뒤에 추가해야 화면에서 헤더가 위로 보인다.
        if (_gameSession != null && _gameSession.unitGenerate != null)
        {
            bool visionOn = _gameSession.unitGenerate.ShowAllVisionRanges;
            items.Add(new SubmenuItem(visionOn ? "■ 시야 표시 (ON)" : "□ 시야 표시 (OFF)", visionOn, true,
                () => _gameSession.unitGenerate.ShowAllVisionRanges = !_gameSession.unitGenerate.ShowAllVisionRanges));
            items.Add(SubmenuItem.Header("시야 / 인지"));

            // 유닛 머리 위 현재 FSM 상태 라벨 on/off — "시야 표시"와 동일 관례지만 주제가 달라(시야/
            // 인지가 아니라 FSM 상태 HUD) 별도 헤더로 분리.
            bool statusLabelOn = _gameSession.unitGenerate.ShowUnitStatusLabels;
            items.Add(new SubmenuItem(statusLabelOn ? "■ 유닛 상태 표시 (ON)" : "□ 유닛 상태 표시 (OFF)", statusLabelOn, true,
                () => _gameSession.unitGenerate.ShowUnitStatusLabels = !_gameSession.unitGenerate.ShowUnitStatusLabels));
            items.Add(SubmenuItem.Header("유닛 상태 HUD"));
        }

        bool objActive = _inputManager != null && _inputManager.IsObjectOnlyPlacementActive;
        items.Add(new SubmenuItem("오브젝트 배치", objActive, true, () => ToggleBuildSubMode(objActive, () => _inputManager?.EnterObjectPlacementMode(), "오브젝트")));

        // 함정 무제한 설치 — 자원 소모 없이 연속 배치할 수 있는 debug 토글. 배치 모드 자체
        // (EnterTrapPlacementMode)는 "설치" 메뉴에 그대로 두고, 여기서는 무제한 여부만 켜고 끈다.
        if (_inputManager != null)
        {
            bool unlimitedTrapOn = _inputManager.DebugUnlimitedTrapPlacement;
            items.Add(new SubmenuItem(unlimitedTrapOn ? "■ 함정 무제한 설치 (ON)" : "□ 함정 무제한 설치 (OFF)", unlimitedTrapOn, true,
                () => _inputManager.DebugUnlimitedTrapPlacement = !_inputManager.DebugUnlimitedTrapPlacement));

            // 모든 유닛 선택 가능 — 플레이어 유닛만 선택 가능하게 제한한 InputManager.IsSelectableUnit
            // (진영/안개 게이트)을 debug에서만 우회하는 토글.
            bool selectAllOn = _inputManager.DebugSelectAllUnits;
            items.Add(new SubmenuItem(selectAllOn ? "■ 모든 유닛 선택 가능 (ON)" : "□ 모든 유닛 선택 가능 (OFF)", selectAllOn, true,
                () => _inputManager.DebugSelectAllUnits = !_inputManager.DebugSelectAllUnits));
        }

        // 바닥 타일 → 벽 전환 — 다른 배치 모드들과 동일한 패턴(우클릭으로 실행, 재클릭/메뉴 전환으로 취소).
        bool wallConvertActive = _inputManager != null && _inputManager.IsWallConvertPlacementActive;
        items.Add(new SubmenuItem("바닥 → 벽 변환", wallConvertActive, true,
            () => ToggleBuildSubMode(wallConvertActive, () => _inputManager?.EnterWallConvertPlacementMode(), "벽 변환")));

        items.Add(SubmenuItem.Header("배치 테스트"));

        return items;
    }

    // debug 서브메뉴가 세로로 너무 길어지지 않도록 BuildDebugPrimaryItems와 이 블록을 주제 경계
    // 기준으로 나눠 항상 별도 열에 그린다 — BuildDebugPropagationItems와 동일한 관례.
    private List<SubmenuItem> BuildDebugSecondaryItems()
    {
        var items = new List<SubmenuItem>();

        // 더미 건물 배치 — 건물 스프라이트/크기 개편으로 더 이상 쓰지 않게 된 예전 유닛/자원 생산
        // 건물 스프라이트를, 기능 없이 건물 판정만 있는 1x1 더미 건물로 재활용.
        if (_inputManager != null)
        {
            const string dummy1Name = "더미 건물 (구 유닛 생산형)";
            bool dummy1Active = _inputManager.IsDummyBuildingModeActive(dummy1Name);
            items.Add(new SubmenuItem("더미 건물 (구 유닛 생산형)", dummy1Active, true,
                () => ToggleBuildSubMode(dummy1Active, () => _inputManager?.EnterDummyBuildingPlacementMode("obj/building", dummy1Name), dummy1Name)));

            const string dummy2Name = "더미 건물 (구 자원 생산형)";
            bool dummy2Active = _inputManager.IsDummyBuildingModeActive(dummy2Name);
            items.Add(new SubmenuItem("더미 건물 (구 자원 생산형)", dummy2Active, true,
                () => ToggleBuildSubMode(dummy2Active, () => _inputManager?.EnterDummyBuildingPlacementMode("obj/resource_building", dummy2Name), dummy2Name)));
        }
        items.Add(SubmenuItem.Header("더미 건물 (기능 없음, 건물 판정만)"));

        items.Add(new SubmenuItem("맵 저장", false, true, () => SaveMapAsync().Forget()));
        items.Add(new SubmenuItem("맵 불러오기", false, true, () => LoadMapAsync().Forget()));
        items.Add(SubmenuItem.Header("맵 저장 / 불러오기"));

        bool singleSelected = _inputManager != null && _inputManager.selectedUnits.Count == 1;
        items.Add(new SubmenuItem("EXP +10", false, singleSelected, () => AdjustSelectedUnit(u => u.BaseStat.exp += 10f)));
        items.Add(new SubmenuItem("Kill +1", false, singleSelected, () => AdjustSelectedUnit(u => u.killCount += 1)));
        items.Add(new SubmenuItem("Level Up", false, singleSelected, () => AdjustSelectedUnit(u => u.level += 1)));
        items.Add(SubmenuItem.Header("유닛 테스트"));

        return items;
    }

    private List<SubmenuItem> BuildDebugPropagationItems()
    {
        var items = new List<SubmenuItem>();
        if (_propagationDebugVisualizer == null) return items;

        var v = _propagationDebugVisualizer;
        AddPropagationToggle(items, "전파 범위", () => v.ShowPropagationRange, val => v.ShowPropagationRange = val);
        AddPropagationToggle(items, "이동음", () => v.ShowMovement, val => v.ShowMovement = val);
        AddPropagationToggle(items, "공격 실행음", () => v.ShowAttackExecution, val => v.ShowAttackExecution = val);
        AddPropagationToggle(items, "피격 발생 공격음", () => v.ShowHitImpact, val => v.ShowHitImpact = val);
        AddPropagationToggle(items, "피격 비명", () => v.ShowHitScream, val => v.ShowHitScream = val);
        AddPropagationToggle(items, "사망음", () => v.ShowDeath, val => v.ShowDeath = val);
        AddPropagationToggle(items, "함정 작동음", () => v.ShowTrapActivation, val => v.ShowTrapActivation = val);
        items.Add(SubmenuItem.Header("소리 · 전파 시각화"));
        return items;
    }

    // "Unit Status Test" UI를 여기로 이전 — DebugInfoPanel 우상단에 따로 떠 있던 EXP/Kill/LevelUp
    // 테스트 버튼 3개.
    private void AdjustSelectedUnit(Action<Unit> apply)
    {
        if (_inputManager == null || _inputManager.selectedUnits.Count != 1) return;
        apply(_inputManager.selectedUnit);
        DebugInfoPanel.Instance?.RefreshSelectedUnitInfo();
    }

    private static void AddPropagationToggle(List<SubmenuItem> items, string label, Func<bool> getter, Action<bool> setter)
    {
        bool on = getter();
        items.Add(new SubmenuItem(on ? $"■ {label} (ON)" : $"□ {label} (OFF)", on, true, () => setter(!getter())));
    }

    // 유닛 상태는 저장 대상이 아님 — 맵(층/청크/타일/점령 상태)만 저장/복원한다(DebugInfoPanel에서 이전).
    private async UniTaskVoid SaveMapAsync()
    {
        var cmap = _gameSession != null ? _gameSession.cmap : null;
        if (cmap == null) return;

        var dto = MapSerializer.MapToDto(cmap.map);
        await _dataManager.SaveData<MapSaveModel, MapSerializer.MapDto>(null, dto);
        LogHelper.Log(LogHelper.GAME, "맵 저장 완료 (Save/map.json)");
        NoticeCenter.Instance?.PushMomentary("맵 저장 완료", NoticeCenter.InfoColor);
    }

    private async UniTaskVoid LoadMapAsync()
    {
        var cmap = _gameSession != null ? _gameSession.cmap : null;
        if (cmap == null) return;

        if (!AssetLoader.Exists("Save/map.json"))
        {
            LogHelper.Warning(LogHelper.GAME, "저장된 맵이 없습니다.");
            NoticeCenter.Instance?.PushMomentary("저장된 맵이 없습니다.", NoticeCenter.WarningColor);
            return;
        }

        var model = await _dataManager.GetModel<MapSaveModel>();
        if (model == null) return;

        cmap.ApplyMap(model.Map);
        LogHelper.Log(LogHelper.GAME, "맵 불러오기 완료");
        NoticeCenter.Instance?.PushMomentary("맵 불러오기 완료", NoticeCenter.InfoColor);
    }
}
