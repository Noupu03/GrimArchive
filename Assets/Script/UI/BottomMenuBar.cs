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

// UI 리뉴얼(2026-08-20, "Assets/문서/공식문서/데모 버전 기획서/08.19 최신/UI 리뉴얼.txt") — 화면
// 좌측 하단에 상시 표시되는 6개 카테고리(명령/설치/소집 배치/맵/도감/debug) 메뉴 바. 카테고리를
// 누르면 그 바로 위에 세로 버튼 목록(서브메뉴)이 뜨고, 다른 카테고리를 누르거나 같은 카테고리를
// 다시 누르면 닫힌다("스택형으로 쌓이고, 다른 메뉴 진입 혹은 비활성화시 사라짐").
//
// StatusInfoPanel/BuildingControlPanel/WaveGaugePanel과 동일한 이 프로젝트의 확립된 관례를 따른다 —
// [PanelAttribute] 패널은 빈 프리팹 껍데기(RectTransform+CanvasRenderer)만 있고 실제 그리기는
// OnGUI가 담당한다(Assets/Script/Editor/SetupWaveGaugePanel.cs 주석 참고). 판단 근거: 이 문서가
// 요구하는 "누르면 즉시 반응하는 버튼 목록"은 GUI.Button만으로 충분하고, 이미 이 패턴으로 구현된
// BuildingControlPanel/MonsterPlacementController의 버튼 패널과도 시각적으로 일관된다 — 완전히 새로운
// uGUI CustomButton 계층(Editor 프리팹 생성 스크립트 포함)을 추가로 만드는 것보다 훨씬 검증된 경로.
//
// UI 리뉴얼로 옮겨온 것들:
//   - 시야 표시/소리·전파 시각화 토글, 맵 저장/불러오기 (기존 DebugInfoPanel에서 이전)
//   - 오브젝트(O)/코어(C) 배치 진입 (기획 문서의 "설치" 메뉴엔 유닛/자원 생산 건물/함정 3개만 명시돼
//     있어, 테스트용 오브젝트/코어 배치는 debug 서브탭으로 분류 — 사용자 확인)
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

    // 2026-08-20 사용자 요청("메뉴를 통한 모드 클릭시 모두 notice로 설명이 뜨게" + "배치모드도 통일해서
    // 시간에 영향을 받지 않는 notice가 나타났다가 기존처럼 사라지는거로") — 처음엔 "지금 활성 모드"를
    // PushPersistent로 계속 띄워두는 방식이었는데, 이 요청에 맞춰 다른 notice들과 똑같이 클릭 시점에
    // 한 번 뜨고 몇 초 뒤 자동으로 사라지는 일반 Push 방식으로 통일했다(NoticeCenter는 Push/PushPersistent
    // 둘 다 Time.unscaledDeltaTime 기반이라 원래도 게임 정지 영향은 안 받는다 — 여기서 바뀐 건 "계속
    // 떠 있음" → "클릭 시 한 번 뜨고 사라짐"뿐). 실제 Push 호출은 각 모드 진입/취소 지점
    // (ToggleBuildSubMode/OnClickToggleCommandMode/InputManager.ExitActivePlacementMode 등)에 있다.

    private enum MenuCategory { None, Command, Build, Map, Encyclopedia, Debug }
    private MenuCategory _activeCategory = MenuCategory.None;

    // 사용자 요청(2026-08-20 "메뉴 UI와 누르면 나오는 버튼들 더 크게 해주고") — 전체적으로 확대.
    private const float BarHeight = 60f;
    private const float BarButtonWidth = 132f;
    private const float BarMarginX = 10f;
    private const float BarMarginY = 10f;
    private const float BarGap = 6f;

    // 사용자 요청(2026-08-20 "메뉴 하위의 버튼들은 크기 조금만 더 줄여줘. 기존크기와 지금 크기의
    // 중간정도") — 확대 전 원래 값(34/210)과 확대된 값(48/260)의 중간.
    private const float SubmenuButtonHeight = 41f;
    private const float SubmenuButtonWidth = 235f;
    private const float SubmenuGap = 6f;
    private const float SubmenuBottomGap = 8f;
    // debug 서브메뉴처럼 항목이 많아 세로로 다 못 쌓일 때 옆 열로 넘기는 데 쓰는 열 간격(2026-08-20,
    // 사용자 신고 "디버그 메뉴 버튼들 화면 넘침").
    private const float SubmenuColumnGap = 10f;

    // 버튼 폰트/흰 테두리 스타일은 GUIMenuStyleUtil로 옮겼다(2026-08-20, 사용자 요청 "정보 UI도
    // 메뉴와 동일한 스타일로" — DebugInfoPanel 탭 버튼과 완전히 같은 스타일을 공유하기 위함).

    // InputManager/BuildPlacementController 등이 "이 화면 좌표가 우리 바 위인가"를 물어볼 때 쓰는
    // 공개 API(BuildingControlPanel.IsMouseOverPanel과 동일 관례) — 안 그러면 바 버튼 클릭이 그대로
    // 월드 클릭(유닛 선택/건물 배치)으로도 처리돼 버린다.
    public bool IsMouseOverUI()
    {
        if (GUIMouseUtil.IsMouseOverRect(GetBarRect())) return true;
        return _activeCategory != MenuCategory.None && GUIMouseUtil.IsMouseOverRect(GetSubmenuBoundingRect());
    }

    // 다른 좌하단 UI(선택 유닛 정보 박스 등)가 "메뉴로 생성된 UI 위에 쌓이는" 방식으로 겹치지 않게
    // 배치하는 데 쓰는 공개 API(2026-08-20, 사용자 요청 "정보 UI랑 다른 UI 겹치지 않게 해줘. 메뉴로
    // 생성된 UI 위에 쌓이는 방식으로") — 지금 하단 바 + (열려 있으면) 서브메뉴가 화면 아래에서부터
    // 차지하는 총 높이를 반환한다. 서브메뉴가 열고 닫힐 때마다 값이 바뀌므로, 호출부는 매 프레임
    // 다시 물어봐서 자기 위치를 그 위로 다시 맞춰야 한다.
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
        int buttonCount = 6;
        float width = buttonCount * BarButtonWidth + (buttonCount - 1) * BarGap;
        return new Rect(BarMarginX, Screen.height - BarMarginY - BarHeight, width, BarHeight);
    }

    // 서브메뉴 항목이 화면 위로 넘치지 않고 세로로 쌓일 수 있는 최대 개수(=열 하나의 최대 행 수).
    // 이 값을 넘는 항목은 DrawVerticalSubmenu가 옆 열로 넘겨서 그린다(2026-08-20, 사용자 신고
    // "디버그 메뉴 버튼들 화면 넘침" — debug 서브메뉴가 헤더 포함 최대 20개 항목까지 쌓여서 저해상도
    // 화면에선 세로 한 줄로 다 못 들어갔다).
    private int GetMaxSubmenuRows()
    {
        float barY = Screen.height - BarMarginY - BarHeight;
        const float topMargin = 10f;
        float maxHeight = barY - SubmenuBottomGap - topMargin;
        if (maxHeight <= 0f) return 1;
        int rows = Mathf.FloorToInt((maxHeight + SubmenuGap) / (SubmenuButtonHeight + SubmenuGap));
        return Mathf.Max(1, rows);
    }

    // 지금 활성 카테고리가 그릴 "독립된 블록" 목록. 대부분은 블록 하나(세로로 쌓이다 화면 높이를
    // 넘으면 옆 열로 넘어감)지만, debug만 두 블록으로 나뉜다(2026-08-20, 사용자 요청 "디버그 메뉴
    // 넘침 문제도 해결 안됨... 소리 전파 시각화 부분을 옆으로 따로 떼서 옮겨봐" — 소리·전파 시각화
    // 토글 7개를 나머지 debug 항목들과 같은 세로줄에 쌓지 않고 별도 블록으로 옆에 그려서, 주 목록의
    // 높이 자체를 줄인다). 항목 리스트를 실제로 만드는 Build*Items()가 유일한 진실 공급원이라 개수를
    // 별도 상수로 손으로 맞춰둘 필요가 없다(예전엔 GetActiveSubmenuItemCount가 항목 수를 손으로 셌는데,
    // DrawXxxSubmenu가 바뀔 때마다 같이 안 바뀌면 마우스오버 판정 영역이 어긋나는 문제가 있었다).
    private List<List<SubmenuItem>> GetActiveSubmenuGroups()
    {
        var groups = new List<List<SubmenuItem>>();
        switch (_activeCategory)
        {
            case MenuCategory.Command: groups.Add(BuildCommandItems()); break;
            case MenuCategory.Build: groups.Add(BuildBuildItems()); break;
            case MenuCategory.Map: groups.Add(BuildMapItems()); break;
            case MenuCategory.Encyclopedia: groups.Add(BuildEncyclopediaItems()); break;
            case MenuCategory.Debug:
                var primary = BuildDebugPrimaryItems();
                if (primary.Count > 0) groups.Add(primary);
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
    }

    private void DrawBar()
    {
        Rect barRect = GetBarRect();
        float x = barRect.x;
        float y = barRect.y;

        DrawBarButton(ref x, y, "명령", _activeCategory == MenuCategory.Command, () => ToggleCategory(MenuCategory.Command));
        DrawBarButton(ref x, y, "설치", _activeCategory == MenuCategory.Build, () => ToggleCategory(MenuCategory.Build));
        // 소집 배치는 자기 자신의 onClick(OnClickDeploy)이 스스로 토글을 책임지므로, 아래 공용
        // "다른 버튼을 누르면 소집 배치를 취소" 규칙에서 제외한다(cancelMonsterPlacement: false) —
        // 안 그러면 이미 켜진 상태에서 다시 눌렀을 때 (취소 → 곧바로 재진입)이 되어 꺼지지 않는다.
        DrawBarButton(ref x, y, "소집 배치", _inputManager != null && _inputManager.IsMonsterPlacementActive, OnClickDeploy, cancelMonsterPlacement: false);
        // 맵/debug는 정보 열람·시각화 위주라 "명령"의 이동 및 공격 토글과 공존해도 된다(사용자 확인,
        // 2026-08-20 "맵,debug는 제외. 다른 메뉴 가도 상관없음") — 이 둘만 cancelCommandMode: false.
        DrawBarButton(ref x, y, "맵", _activeCategory == MenuCategory.Map, () => ToggleCategory(MenuCategory.Map), cancelCommandMode: false);
        // 도감은 아직 기능이 없지만(문서: "추후 추가될 기능인데, 버튼만 미리 두기"), 다른 메뉴들과
        // 동일하게 카테고리 토글 + 서브메뉴(버튼 1개, "구현 예정")로 동작한다(2026-08-20, 사용자 요청
        // "다른 메뉴들처럼 동작하게 만들어놓기" — notice로 안내하던 것 대신).
        DrawBarButton(ref x, y, "도감", _activeCategory == MenuCategory.Encyclopedia, () => ToggleCategory(MenuCategory.Encyclopedia));
        DrawBarButton(ref x, y, "debug", _activeCategory == MenuCategory.Debug, () => ToggleCategory(MenuCategory.Debug), cancelCommandMode: false);
    }

    private void DrawBarButton(ref float x, float y, string label, bool active, Action onClick,
        bool cancelMonsterPlacement = true, bool cancelCommandMode = true)
    {
        Rect rect = new Rect(x, y, BarButtonWidth, BarHeight);
        bool clicked = GUIMenuStyleUtil.DrawFlatButton(rect, label, active);
        if (clicked)
        {
            // 우클릭 취소를 없앤 대신(2026-08-20, 사용자 요청) 하단 바의 어떤 버튼을 누르든(다른
            // 카테고리로 전환/같은 카테고리 닫기/도감) 그 시점에 진행 중이던 건물·오브젝트 배치 모드는
            // 함께 취소된다 — "메뉴 바꾸기"로 취소하는 경로. 소집 배치(몬스터 배치 모드)도 같은 이유로
            // 다른 메뉴와 겹치지 않도록 함께 취소한다(사용자 요청, 2026-08-20 "소집 배치 메뉴도 다른
            // 메뉴랑 중복되지 않게"). "명령"의 이동 및 공격 토글도 마찬가지 — 메뉴를 닫거나 다른(맵/
            // debug 제외) 메뉴로 가면 함께 꺼진다(사용자 요청, 2026-08-20 "상위 메뉴를 눌러 꺼버리면,
            // 위에서 토글했던것들도 취소되게").
            _inputManager?.ExitActivePlacementMode();
            if (cancelMonsterPlacement) _inputManager?.ExitMonsterPlacementModeIfActive();
            if (cancelCommandMode) _inputManager?.CancelCommandModeIfActive();
            onClick();
        }
        x += BarButtonWidth + BarGap;
    }

    private void ToggleCategory(MenuCategory category)
    {
        _activeCategory = (_activeCategory == category) ? MenuCategory.None : category;
    }

    private void OnClickDeploy()
    {
        _activeCategory = MenuCategory.None; // 소집 배치는 서브메뉴 없이 즉시 토글(문서: "누르면 즉시 기존의 소집 배치 모드처럼 작동")
        _inputManager?.ToggleMonsterPlacementMode();
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

    // 도감 서브메뉴 — 아직 기능이 없어 비활성 버튼 1개("구현 예정")만 둔다(2026-08-20, 문서: "추후
    // 추가될 기능인데, 버튼만 미리 두기").
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
        // 주제별 구분용 헤더 행(2026-08-20, 사용자 요청 "debug의 버튼들을 주제별로 나눠줘") — 버튼이
        // 아니라 굵은 라벨만 그린다(클릭 불가, 테두리/배경 없음).
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

    // 바 위에 아래에서 위로 쌓이는 세로 버튼 목록 하나를 그린다. 헤더 항목은 버튼이 아니라 굵은
    // 라벨로만 그려서(주제 구분용) 나머지 버튼과 구분된다. startColumn은 이 블록을 몇 번째 열부터
    // 시작할지(다른 블록과 나란히 놓을 때 씀, GetActiveSubmenuGroups/DrawSubmenu 참고).
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
    // 명령 서브메뉴 — "명령 취소"(토글) + "이동 및 공격"(토글) + 예정 2개. 2026-08-20, 사용자 요청
    // "명령 취소 로직을 바꿀게. 토글형으로 바꾸고, 해당 유닛들을 선택 후 우클릭을 눌러 즉시 명령
    // 취소되게 하자" — 예전엔 눌러서 즉시 "모든 유닛"의 명령을 취소했는데, 이제 "이동 및 공격"과
    // 대칭 구조(토글 on → 선택 + 우클릭으로 발동)다. 두 토글은 InputManager 안에서 서로 배타로
    // 관리된다(SetCommandModeActive/SetCancelCommandModeActive).
    // =====================================================
    private List<SubmenuItem> BuildCommandItems()
    {
        return new List<SubmenuItem>
        {
            new SubmenuItem("명령 취소", _inputManager != null && _inputManager.IsCancelCommandModeActive, true, OnClickToggleCancelCommandMode),
            new SubmenuItem("이동 및 공격", _inputManager != null && _inputManager.IsCommandModeActive, true, OnClickToggleCommandMode),
            new SubmenuItem("(예정)", false, false, null),
            new SubmenuItem("(예정)", false, false, null),
        };
    }

    private void OnClickToggleCommandMode()
    {
        if (_inputManager == null) return;
        bool newState = !_inputManager.IsCommandModeActive;
        _inputManager.SetCommandModeActive(newState);
        NoticeCenter.Instance?.PushMomentary(
            newState ? "명령 모드 켜짐: 선택한 유닛에게 이동 명령을 내릴 수 있습니다." : "명령 모드 꺼짐: 정보 조회만 가능합니다.",
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

    // =====================================================
    // 설치 서브메뉴 — 유닛 생산 건물(B) / 자원 생산 건물(V) / 함정(P). 우클릭 취소를 없앤 대신(2026-08-20,
    // 사용자 요청) 각 버튼이 자기 모드일 때만 켜진 걸로 표시하고(예전엔 IsBuildPlacementActive 하나를
    // 셋이 공유해서 "자원 생산 건물"을 골라도 "유닛 생산 건물"까지 같이 켜진 것처럼 보이는 버그가
    // 있었다), 이미 켜진 버튼을 다시 누르면 취소한다 — 그래서 클릭 후에도 카테고리를 닫지 않고 계속
    // 열어둬서 같은 버튼을 바로 다시 누를 수 있게 한다.
    // =====================================================
    private List<SubmenuItem> BuildBuildItems()
    {
        bool unitActive = _inputManager != null && _inputManager.IsUnitBuildModeActive;
        bool resourceActive = _inputManager != null && _inputManager.IsResourceBuildModeActive;
        bool trapActive = _inputManager != null && _inputManager.IsTrapPlacementActive;

        return new List<SubmenuItem>
        {
            new SubmenuItem("유닛 생산 건물", unitActive, true, () => ToggleBuildSubMode(unitActive, () => _inputManager?.EnterUnitBuildMode(), "유닛 생산 건물")),
            new SubmenuItem("자원 생산 건물", resourceActive, true, () => ToggleBuildSubMode(resourceActive, () => _inputManager?.EnterResourceBuildMode(), "자원 생산 건물")),
            new SubmenuItem("함정", trapActive, true, () => ToggleBuildSubMode(trapActive, () => _inputManager?.EnterTrapPlacementMode(), "함정")),
        };
    }

    // wasActive가 이미 true였다면(=지금 클릭한 버튼이 이미 활성 상태였다면) 취소, 아니면 enterMode를
    // 호출해 그 모드로 진입/전환한다 — "설치"/"debug" 서브메뉴의 배치류 버튼들이 공유하는 재클릭 취소
    // 규칙(2026-08-20, 사용자 요청 "오직 메뉴 바꾸기 혹은 메뉴 다시 클릭으로 바꿀 수 있게"). 취소
    // notice는 InputManager.ExitActivePlacementMode가 담당(재클릭이든 메뉴 전환이든 같은 문구로
    // 통일) — 여기서는 "진입" notice만 띄운다(사용자 요청 "메뉴를 통한 모드 클릭시 모두 notice로").
    private void ToggleBuildSubMode(bool wasActive, Action enterMode, string label)
    {
        if (wasActive)
        {
            _inputManager?.ExitActivePlacementMode();
        }
        else
        {
            enterMode?.Invoke();
            NoticeCenter.Instance?.PushMomentary($"{label} 배치 모드 시작 (좌클릭: 설치, 취소: 메뉴 전환/재클릭)", NoticeCenter.InfoColor);
        }
    }

    // =====================================================
    // 맵 서브메뉴 — 층 버튼, 층별 카메라 시스템과 연동(CameraController.GoToFloor). 2026-08-20, 사용자
    // 요청 "플레이어에게 밝혀지지 않은 층은, 층 전환 메뉴에 아예 버튼이 뜨지 않게 해줘. 밝혀진 순간부터
    // 메뉴에 뜨도록" — 예전엔 4개 버튼을 항상 그리고 범위 밖(floorCount 이상)만 비활성화했는데, 이제
    // CameraController.IsFloorRevealed로 아직 안 밝혀진 층은 버튼 자체를 리스트에서 뺀다.
    // =====================================================
    private List<SubmenuItem> BuildMapItems()
    {
        var cam = CameraController.Instance;
        int currentFloor = cam != null ? cam.CurrentFloor : -1;
        int floorCount = 4;
        if (cam == null || !cam.TryGetFloorCount(out floorCount)) floorCount = 4;

        var items = new List<SubmenuItem>();
        for (int f = 0; f < floorCount; f++)
        {
            if (cam == null || !cam.IsFloorRevealed(f)) continue;

            int floorIndex = f; // 클로저 캡처
            items.Add(new SubmenuItem($"{floorIndex}층", floorIndex == currentFloor, true,
                () =>
                {
                    _activeCategory = MenuCategory.None;
                    CameraController.Instance?.GoToFloor(floorIndex);
                    NoticeCenter.Instance?.PushMomentary($"{floorIndex}층으로 카메라 전환", NoticeCenter.InfoColor);
                }));
        }
        return items;
    }

    // =====================================================
    // debug 서브메뉴 — 시야/소리·전파 시각화 토글, 오브젝트/코어 배치, 맵 저장/불러오기, 유닛 테스트
    // (전부 기존 DebugInfoPanel/InputManager 단축키·UI에서 이전). 2026-08-20, 사용자 요청 "debug의
    // 버튼들을 주제별로 나눠줘"로 헤더 그룹 구분을 도입했고, 이후 "디버그 메뉴 넘침... 소리 전파
    // 시각화 부분을 옆으로 따로 떼서 옮겨봐"에 맞춰 소리·전파 시각화(토글 7개, 항목이 가장 많음)만
    // 별도 블록(BuildDebugPropagationItems)으로 분리했다 — 나머지(시야/인지, 배치 테스트, 맵 저장/
    // 불러오기, 유닛 테스트)는 여전히 한 블록(BuildDebugPrimaryItems)에 쌓인다. GetActiveSubmenuGroups가
    // 이 두 블록을 옆으로 나란히 그려서, 주 목록 높이가 줄어들어 화면 상단(배속 표시 등)과 안 겹친다.
    // =====================================================
    private List<SubmenuItem> BuildDebugPrimaryItems()
    {
        var items = new List<SubmenuItem>();

        // 2026-08-20, 사용자 요청 "디버그 메뉴에서, 주제를 상단에 두고, 내용을 하단에 두는 방식으로
        // 바꿔줘" — DrawVerticalSubmenu는 바(하단 바 바로 위)에서부터 위로 쌓아 그리므로, 리스트의
        // 앞쪽 항목일수록 화면상 더 아래(바에 더 가까움)에 그려진다. 그래서 각 주제 블록마다 헤더를
        // 그 주제의 내용(버튼)들보다 뒤에 추가해야 화면에서는 헤더가 위, 내용이 그 아래로 보인다.
        if (_gameSession != null && _gameSession.unitGenerate != null)
        {
            bool visionOn = _gameSession.unitGenerate.ShowAllVisionRanges;
            items.Add(new SubmenuItem(visionOn ? "■ 시야 표시 (ON)" : "□ 시야 표시 (OFF)", visionOn, true,
                () => _gameSession.unitGenerate.ShowAllVisionRanges = !_gameSession.unitGenerate.ShowAllVisionRanges));
            items.Add(SubmenuItem.Header("시야 / 인지"));
        }

        bool objActive = _inputManager != null && _inputManager.IsObjectOnlyPlacementActive;
        bool coreActive = _inputManager != null && _inputManager.IsCorePlacementActive;
        items.Add(new SubmenuItem("오브젝트 배치", objActive, true, () => ToggleBuildSubMode(objActive, () => _inputManager?.EnterObjectPlacementMode(), "오브젝트")));
        items.Add(new SubmenuItem("코어 배치", coreActive, true, () => ToggleBuildSubMode(coreActive, () => _inputManager?.EnterCorePlacementMode(), "코어")));
        items.Add(SubmenuItem.Header("배치 테스트"));

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

    // "Unit Status Test" UI(2026-08-20, 사용자 요청 "unitstatustest UI도 debug에 옮기고")를 여기로
    // 이전 — DebugInfoPanel 우상단에 따로 떠 있던 EXP/Kill/LevelUp 테스트 버튼 3개.
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
