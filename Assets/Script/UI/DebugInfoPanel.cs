using System.Text;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Client.Routine;
using Haare.Client.UI;

// UIManager.OnGUI()의 DrawTopRightUI()/DrawSelectedUnitInfo()를 대체하는 Haare UGUI 패널(프리팹은
// Assets/Editor/HaareUISetup.cs로 생성/배선) — 시야/전파 시각화 토글과 맵 저장/불러오기 버튼은 BottomMenuBar
// debug 서브탭으로 옮겨져 이 패널은 선택 유닛 정보만 담당한다. 마우스 휠 줌은 CameraController 전담이므로 건드리지 말 것.
[PanelAttribute("Prefabs/DebugInfoPanel")]
public class DebugInfoPanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    // BuildingControlPanel.Instance와 동일 관례 — InputManager가 마우스가 이 정보창 위에 있는지
    // 물어볼 때 쓴다(안 그러면 정보창 클릭이 월드 클릭으로도 처리돼 선택이 풀린다).
    public static DebugInfoPanel Instance { get; private set; }

    [SerializeField] private CustomText selectedUnitInfoText;
    // 이 박스(InfoBox)의 RectTransform을 들고 있다가, 하단 메뉴 바가 차지하는 높이
    // (BottomMenuBar.GetReservedBottomLeftHeight, 서브메뉴 열림에 따라 매 프레임 바뀜)만큼 매 프레임
    // 위로 밀어 올려서 겹치지 않게 한다.
    [SerializeField] private RectTransform infoBoxRect;

    private InputManager _inputManager;

    // 좌하단 패널 스택(2026-08-21) 등록용 ID — BottomLeftPanelStack.Report 참고.
    private const string StackId = "DebugInfo";

    // "기본 정보"/"세부 스탯"/"장비"/"스킬" 탭(림월드 캐릭터창 참고) — 장비는 시스템 자체가 없어
    // "구현 예정" 문구만 보여준다. 스킬 탭은 진실의 원천인 프리팹(UnitGenerate.GetSkills)에서
    // 실제 장착된 스킬 목록을 그대로 읽는다.
    private enum InfoTab { Basic, Stats, Equipment, Skills }
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
    // 동일 관례) — 정보 박스 + 탭 버튼(있으면)을 모두 포함한다.
    public bool IsMouseOverUI()
    {
        if (!TryGetVisibleInfoBoxRect(out Rect rect)) return false;
        return GUIMouseUtil.IsMouseOverRect(rect);
    }

    // 다른 OnGUI 패널이 이 정보창과 실제로 겹치는지 판정할 때 쓴다. 이 박스는 uGUI Canvas라 OnGUI 실행
    // 순서로 겹침을 못 바꾸므로(Canvas가 항상 먼저 그려짐), 겹치는 패널 쪽이 자기 자신을 안 그리는
    // 방식으로 우선순위를 준다(IsMouseOverUI와 동일한 사각형 계산 재사용).
    public bool TryGetVisibleInfoBoxRect(out Rect rect)
    {
        rect = default;
        if (infoBoxRect == null || _inputManager == null || _inputManager.selectedUnits.Count == 0) return false;
        if (!infoBoxRect.gameObject.activeInHierarchy) return false;

        bool tabsVisible = _inputManager.selectedUnits.Count == 1;
        float extraTop = tabsVisible ? (TabHeight + TabGap) : 0f;

        float left = infoBoxRect.anchoredPosition.x;
        float width = infoBoxRect.sizeDelta.x;
        float height = infoBoxRect.sizeDelta.y + extraTop;
        float top = Screen.height - (infoBoxRect.anchoredPosition.y + infoBoxRect.sizeDelta.y + extraTop);

        rect = new Rect(left, top, width, height);
        return true;
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

        // InfoBox는 선택된 유닛이 있을 때만 보이게 한다(예전엔 OpenPanel() 이후 항상 SetActive(true)).
        if (infoBoxRect != null) infoBoxRect.gameObject.SetActive(currentCount > 0);
    }

    protected override void UpdateProcess()
    {
        base.UpdateProcess();

        RepositionInfoBoxAboveBottomMenu();
    }

    // 좌하단 패널 스택 — 보이는 동안 매 프레임 자기 폭을 보고하고 시작 X를 받아온다. 등록 순서
    // 기반이라 다른 패널이 새로 나타나도 이 박스가 이미 떠 있었다면 위치가 바뀌지 않는다.
    private void RepositionInfoBoxAboveBottomMenu()
    {
        if (infoBoxRect == null) return;

        bool visible = _inputManager != null && _inputManager.selectedUnits.Count > 0;
        float x = BottomLeftPanelStack.Report(StackId, visible, infoBoxRect.sizeDelta.x);
        infoBoxRect.anchoredPosition = new Vector2(x, BottomLeftPanelStack.GetReservedBottomHeight());
    }

    private void OnGUI()
    {
        DrawInfoBoxBorder();
        DrawInfoTabs();
    }

    // infoBoxRect는 uGUI Image라 GUIMenuStyleUtil을 직접 못 쓰지만, anchoredPosition/sizeDelta를
    // OnGUI 화면 좌표로 변환해 그 위에 테두리만 겹쳐 그려 하단 메뉴 바와 같은 스타일로 통일한다.
    private void DrawInfoBoxBorder()
    {
        if (infoBoxRect == null || !infoBoxRect.gameObject.activeInHierarchy) return;

        float left = infoBoxRect.anchoredPosition.x;
        float width = infoBoxRect.sizeDelta.x;
        float height = infoBoxRect.sizeDelta.y;
        float top = Screen.height - (infoBoxRect.anchoredPosition.y + height);

        GUIMenuStyleUtil.DrawButtonBorder(new Rect(left, top, width, height));
    }

    // 정보 박스 바로 위에 탭 버튼 4개를 그린다. 유닛을 정확히 1기 선택했을 때만 의미가 있다.
    // TabCount에 맞춰 박스 폭 기준으로 버튼 폭을 동적으로 계산한다(고정폭이면 박스 밖으로 넘침).
    private const float TabHeight = 40f;
    private const float TabGap = 6f;
    private const int TabCount = 4;

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
        DrawInfoTabButton(boxX + (tabWidth + TabGap) * 3, y, tabWidth, TabHeight, "스킬", InfoTab.Skills);
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

    // BottomMenuBar의 debug 메뉴("Unit Status Test" 버튼들)가 스탯을 바꾼 뒤 화면 텍스트를 즉시
    // 갱신하려고 호출하는 공개 진입점.
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
            InfoTab.Skills => BuildSkillsTabText(u),
            _ => BuildDetailedStatsTabText(u),
        };
        selectedUnitInfoText.SetupText(text);
    }

    // "기본 정보" 탭 — 이름/진영/LV/EXP/킬카운트만.
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

    // 림월드 캐릭터창의 "장비" 탭 참고 — 장비 시스템 자체가 아직 없어서 슬롯 자리만 보여주고 전부
    // "구현 예정"으로 표시한다.
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

    // "스킬" 탭 — 진실의 원천은 skills.json이 아니라 프리팹이므로 UnitGenerate.GetSkills(런타임에
    // 프리팹에서 조립된 실제 SkillAction 목록)를 그대로 읽는다. 이름/사거리/쿨다운만 표시.
    private string BuildSkillsTabText(Unit u)
    {
        var sb = new StringBuilder();
        var skills = u.Generate != null ? u.Generate.GetSkills(u.unitType.typeName) : null;

        if (skills == null || skills.Count == 0)
        {
            sb.AppendLine("<color=grey>(보유 스킬 없음)</color>");
            return sb.ToString();
        }

        foreach (var s in skills)
        {
            if (s == null) continue;
            sb.AppendLine($"<b>{s.SkillName}</b>");
            sb.AppendLine($"사거리: {s.HitRange} / 쿨다운: {s.DefaultBaseCooldown:F1}초");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // "세부 스탯" 탭 — 근력/내구/민첩/집중/마력/저항/감각/통솔만.
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
    private string BuildMultiSelectListText(IReadOnlyList<Unit> units)
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
