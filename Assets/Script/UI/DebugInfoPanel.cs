using System.Text;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Client.Routine;
using Haare.Client.UI;

// UIManager.OnGUI()의 DrawTopRightUI()/DrawSelectedUnitInfo()를 대체하는 Haare UGUI 패널.
// 프리팹은 Assets/Editor/HaareUISetup.cs("Tools/GrimArchive/Haare UI 셋업 생성")로 생성/배선된다.
// UI 리뉴얼(2026-08-20) — 시야/전파 시각화 토글과 맵 저장/불러오기 버튼은 하단 메뉴 "debug" 서브탭
// (BottomMenuBar)으로 옮겨졌다. 이 패널은 이제 선택 유닛 정보 표시만 담당한다. 마우스 휠 줌은
// CameraController가 유일하게 담당한다(2026-08-21, 사용자 신고 "입력 시스템 개선 여지 체크" —
// 이 패널이 별도로 Camera.main.orthographicSize를 매 프레임 건드려 CameraController의 줌과 이중으로
// 겹쳐 적용되던 버그를 여기서 제거해 해결했다).
[PanelAttribute("Prefabs/DebugInfoPanel")]
public class DebugInfoPanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    // BuildingControlPanel.Instance/BottomMenuBar.Instance와 동일 관례 — InputManager가 "지금
    // 마우스가 이 정보창(박스+탭 버튼) 위에 있는가"를 물어볼 때 쓴다(2026-08-20, 사용자 신고 "세부
    // 스탯에서 장비 클릭하면 화면이 사라져버려" — 이 정보창 위 클릭이 InputManager의 월드 클릭으로도
    // 처리돼 선택이 풀리면서 정보 텍스트가 빈 문자열이 돼 버렸던 문제).
    public static DebugInfoPanel Instance { get; private set; }

    [SerializeField] private CustomText selectedUnitInfoText;
    // UI 리뉴얼(2026-08-20, 사용자 요청 "정보 UI랑 다른 UI 겹치지 않게, 메뉴로 생성된 UI 위에 쌓이는
    // 방식으로") — 이 박스(InfoBox)의 RectTransform을 직접 들고 있다가, 하단 메뉴 바가 지금 차지하고
    // 있는 높이(BottomMenuBar.GetReservedBottomLeftHeight, 서브메뉴 열림에 따라 매 프레임 바뀜)만큼
    // 매 프레임 위로 밀어 올려서 겹치지 않게 한다.
    [SerializeField] private RectTransform infoBoxRect;

    // 사용자 요청(2026-08-20 "정보 UI 밑으로 메뉴 바로 위에 오게 딱 붙여줘") — 여백 없이 밀착.
    private const float InfoBoxBottomGap = 0f;

    private InputManager _inputManager;

    // "기본 정보"/"세부 스탯"/"장비" 탭(2026-08-20, 사용자 요청, 림월드 캐릭터창 참고) — 장비는 아직
    // 시스템 자체가 없어 자리만 만들고 "구현 예정" 문구만 보여준다. 각 탭은 사용자가 명시한 필드만
    // 보여준다(기본 정보: 이름/진영/LV/EXP/킬카운트, 세부 스탯: 근력/내구/민첩/집중/마력/저항/감각/
    // 통솔 — 그 외 HP/MP/정신력/파티/전투스탯/이동속도/위치/상태이상 등은 전부 표시 안 함).
    private enum InfoTab { Basic, Stats, Equipment }
    private InfoTab _currentTab = InfoTab.Basic;

    [Inject]
    public void Construct(InputManager inputManager)
    {
        _inputManager = inputManager;
    }

    protected override void Constructor()
    {
        base.Constructor();
        Instance = this;
    }

    // InputManager가 월드 클릭 처리 전에 확인하는 공개 API(BuildingControlPanel.IsMouseOverPanel과
    // 동일 관례) — 정보 박스 + 탭 버튼(있으면)을 모두 포함한다. 우상단 "Unit Status Test" 창은
    // debug 메뉴로 옮겨져(BottomMenuBar) 여기서 더는 확인하지 않는다.
    public bool IsMouseOverUI()
    {
        if (infoBoxRect == null || _inputManager == null || _inputManager.selectedUnits.Count == 0) return false;

        bool tabsVisible = _inputManager.selectedUnits.Count == 1;
        float extraTop = tabsVisible ? (TabHeight + TabGap) : 0f;

        float left = infoBoxRect.anchoredPosition.x;
        float width = infoBoxRect.sizeDelta.x;
        float height = infoBoxRect.sizeDelta.y + extraTop;
        float top = Screen.height - (infoBoxRect.anchoredPosition.y + infoBoxRect.sizeDelta.y + extraTop);

        return GUIMouseUtil.IsMouseOverRect(new Rect(left, top, width, height));
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

    public void BindEvent()
    {
    }

    private int _lastSelectionCount = -1;
    private Unit _lastSelectedUnit = null;

    private void Update()
    {
        if (_inputManager == null) return;

        int currentCount = _inputManager.selectedUnits.Count;
        Unit currentFirst = currentCount > 0 ? _inputManager.selectedUnits[0] : null;

        if (currentCount != _lastSelectionCount || currentFirst != _lastSelectedUnit)
        {
            _lastSelectionCount = currentCount;
            _lastSelectedUnit = currentFirst;
            RefreshSelectedUnitInfo();
        }

        // 사용자 신고(2026-08-20) "정보창 배경이 계속 떠있잖아? 정보 열람할때만 뜨게" — InfoBox는
        // OpenPanel() 이후로는 항상 SetActive(true)인 채였다. 선택된 유닛이 있을 때만 보이게 한다.
        if (infoBoxRect != null) infoBoxRect.gameObject.SetActive(currentCount > 0);
    }

    protected override void UpdateProcess()
    {
        base.UpdateProcess();

        RepositionInfoBoxAboveBottomMenu();
    }

    private void RepositionInfoBoxAboveBottomMenu()
    {
        if (infoBoxRect == null) return;

        float reserved = BottomMenuBar.Instance != null ? BottomMenuBar.Instance.GetReservedBottomLeftHeight() : 0f;
        Vector2 pos = infoBoxRect.anchoredPosition;
        infoBoxRect.anchoredPosition = new Vector2(pos.x, InfoBoxBottomGap + reserved);
    }

    private void OnGUI()
    {
        DrawInfoBoxBorder();
        DrawInfoTabs();
    }

    // 사용자 요청(2026-08-20 "세부 정보창 전체에 하얀색 테두리도 그려주고") — infoBoxRect는 uGUI Image라
    // 다른 메뉴 UI들처럼 GUIMenuStyleUtil을 직접 못 쓰지만, IsMouseOverUI가 이미 하듯 anchoredPosition/
    // sizeDelta를 OnGUI 화면 좌표로 변환해서 그 위에 테두리만 겹쳐 그린다 — 하단 메뉴 바/탭과 같은
    // 흰 테두리 스타일로 통일.
    private void DrawInfoBoxBorder()
    {
        if (infoBoxRect == null || !infoBoxRect.gameObject.activeInHierarchy) return;

        float left = infoBoxRect.anchoredPosition.x;
        float width = infoBoxRect.sizeDelta.x;
        float height = infoBoxRect.sizeDelta.y;
        float top = Screen.height - (infoBoxRect.anchoredPosition.y + height);

        GUIMenuStyleUtil.DrawButtonBorder(new Rect(left, top, width, height));
    }

    // 정보 박스(InfoBox) 바로 위에 "기본 정보"/"세부 스탯"/"장비" 탭 버튼 3개를 그린다. 유닛을 정확히
    // 1기 선택했을 때만 의미가 있다(다중 선택/미선택 시엔 탭 없이 기존 목록/빈 텍스트 그대로). 탭이
    // 3개로 늘어나서 박스 폭에 맞춰 버튼 폭을 동적으로 계산한다(고정폭이면 박스 밖으로 넘침).
    // 크기/폰트/테두리는 GUIMenuStyleUtil로 하단 메뉴 바와 동일한 스타일을 쓴다(2026-08-20, 사용자
    // 요청 "정보 UI도 메뉴와 동일한 스타일로").
    private const float TabHeight = 40f;
    private const float TabGap = 6f;
    private const int TabCount = 3;

    private void DrawInfoTabs()
    {
        if (infoBoxRect == null || _inputManager == null || _inputManager.selectedUnits.Count != 1) return;

        float boxX = infoBoxRect.anchoredPosition.x;
        float boxWidth = infoBoxRect.sizeDelta.x;
        float boxTopY = Screen.height - (infoBoxRect.anchoredPosition.y + infoBoxRect.sizeDelta.y);
        float y = boxTopY - TabHeight - TabGap;
        float tabWidth = (boxWidth - (TabCount - 1) * TabGap) / TabCount;

        DrawInfoTabButton(boxX, y, tabWidth, TabHeight, "기본 정보", InfoTab.Basic);
        DrawInfoTabButton(boxX + (tabWidth + TabGap) * 1, y, tabWidth, TabHeight, "세부 스탯", InfoTab.Stats);
        DrawInfoTabButton(boxX + (tabWidth + TabGap) * 2, y, tabWidth, TabHeight, "장비", InfoTab.Equipment);
    }


    private void DrawInfoTabButton(float x, float y, float w, float h, string label, InfoTab tab)
    {
        Rect rect = new Rect(x, y, w, h);
        bool clicked = GUIMenuStyleUtil.DrawFlatButton(rect, label, _currentTab == tab);

        if (clicked && _currentTab != tab)
        {
            _currentTab = tab;
            RefreshSelectedUnitInfo();
        }
    }

    // BottomMenuBar의 debug 메뉴(옮겨진 "Unit Status Test" 버튼들)가 스탯을 바꾼 뒤 화면 텍스트를
    // 즉시 갱신하려고 호출하는 공개 진입점(2026-08-20).
    public void RefreshSelectedUnitInfo()
    {
        if (selectedUnitInfoText == null) return;

        if (_inputManager == null || _inputManager.selectedUnits.Count == 0)
        {
            selectedUnitInfoText.SetupText("");
            return;
        }

        // 다수 선택 시엔 스탯 대신 선택된 유닛 목록만 보여준다.
        if (_inputManager.selectedUnits.Count > 1)
        {
            selectedUnitInfoText.SetupText(BuildMultiSelectListText(_inputManager.selectedUnits));
            return;
        }

        Unit u = _inputManager.selectedUnit;
        string text = _currentTab switch
        {
            InfoTab.Basic => BuildBasicInfoTabText(u),
            InfoTab.Equipment => BuildEquipmentTabText(u),
            _ => BuildDetailedStatsTabText(u),
        };
        selectedUnitInfoText.SetupText(text);
    }

    // "기본 정보" 탭(2026-08-20, 사용자 명시) — 이름/진영/LV/EXP/킬카운트만.
    private string BuildBasicInfoTabText(Unit u)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<b>이름:</b> {u.unitType.typeName}");
        sb.AppendLine($"<b>진영:</b> {(u.IsHumanFaction ? "인류" : (u.IsPlayerMonsterFaction ? "플레이어 몬스터" : "야생 몬스터"))}");
        sb.AppendLine($"<b>LV:</b> {u.level}");
        sb.AppendLine($"<b>EXP:</b> {u.BaseStat.exp:F1}");
        sb.AppendLine($"<b>킬 카운트:</b> {u.killCount}");
        return sb.ToString();
    }

    // 림월드 캐릭터창의 "장비" 탭 참고(2026-08-20, 사용자 요청) — 장비 시스템 자체가 아직 없어서
    // 슬롯 자리만 보여주고 전부 "구현 예정"으로 표시한다.
    private string BuildEquipmentTabText(Unit u)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<b>이름:</b> {u.unitType.typeName}");
        sb.AppendLine();
        sb.AppendLine("<b>장비</b>");
        sb.AppendLine("<color=grey>(구현 예정 — 아직 장비 시스템이 없습니다)</color>");
        sb.AppendLine();
        sb.AppendLine("무기: -");
        sb.AppendLine("방어구: -");
        sb.AppendLine("장신구: -");
        return sb.ToString();
    }

    // "세부 스탯" 탭(2026-08-20, 사용자 명시) — 근력/내구/민첩/집중/마력/저항/감각/통솔만.
    private string BuildDetailedStatsTabText(Unit u)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"근력: {u.BaseStat.sterngth:F1}");
        sb.AppendLine($"내구: {u.BaseStat.Durability:F1}");
        sb.AppendLine($"민첩: {u.BaseStat.agility:F1}");
        sb.AppendLine($"집중: {u.concentration:F1}");
        sb.AppendLine($"마력: {u.MagicPower:F1}");
        sb.AppendLine($"저항: {u.resistance:F1}");
        sb.AppendLine($"감각: {u.BaseStat.sense:F1}");
        sb.AppendLine($"통솔: {u.leadership:F1}");
        return sb.ToString();
    }

    // 다수 선택 시 스탯 대신 보여줄 목록. 스탯 대신 "무엇이 선택돼 있는지"만 한눈에 보이면 되므로
    // 유닛별 상세 능력치는 넣지 않는다.
    private string BuildMultiSelectListText(List<Unit> units)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<b>선택됨: {units.Count}기</b>");
        sb.AppendLine();

        foreach (var u in units)
        {
            if (u == null) continue;

            string faction = u is Human ? "인류" : "몬스터";
            string color = u.Health.hp <= 0 ? "red" : (u is Human ? "white" : "yellow");
            string leaderMark = u is Human hMulti && hMulti.party != null && hMulti.party.Leader == hMulti ? " ★" : "";
            sb.AppendLine($"<color={color}>{u.unitType.typeName}{leaderMark} ({faction}) — {u.Health.hp:F0}/{u.Health.maxHp:F0}</color>");
        }

        return sb.ToString();
    }
}
