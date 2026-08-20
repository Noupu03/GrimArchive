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

    private const float BarHeight = 44f;
    private const float BarButtonWidth = 96f;
    private const float BarMarginX = 10f;
    private const float BarMarginY = 10f;
    private const float BarGap = 4f;

    private const float SubmenuButtonHeight = 34f;
    private const float SubmenuButtonWidth = 210f;
    private const float SubmenuGap = 4f;
    private const float SubmenuBottomGap = 6f;

    private static readonly Color ActiveColor = new Color(0.25f, 0.75f, 1f, 1f);
    private static readonly Color InactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
    private static readonly Color DisabledColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);

    // InputManager/BuildPlacementController 등이 "이 화면 좌표가 우리 바 위인가"를 물어볼 때 쓰는
    // 공개 API(BuildingControlPanel.IsMouseOverPanel과 동일 관례) — 안 그러면 바 버튼 클릭이 그대로
    // 월드 클릭(유닛 선택/건물 배치)으로도 처리돼 버린다.
    public bool IsMouseOverUI()
    {
        if (GUIMouseUtil.IsMouseOverRect(GetBarRect())) return true;
        return _activeCategory != MenuCategory.None && GUIMouseUtil.IsMouseOverRect(GetSubmenuRect(GetActiveSubmenuItemCount()));
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
            int itemCount = GetActiveSubmenuItemCount();
            height += SubmenuBottomGap + itemCount * SubmenuButtonHeight + Mathf.Max(0, itemCount - 1) * SubmenuGap;
        }
        return height;
    }

    private Rect GetBarRect()
    {
        int buttonCount = 6;
        float width = buttonCount * BarButtonWidth + (buttonCount - 1) * BarGap;
        return new Rect(BarMarginX, Screen.height - BarMarginY - BarHeight, width, BarHeight);
    }

    private Rect GetSubmenuRect(int itemCount)
    {
        float barY = Screen.height - BarMarginY - BarHeight;
        float height = itemCount * SubmenuButtonHeight + Mathf.Max(0, itemCount - 1) * SubmenuGap;
        return new Rect(BarMarginX, barY - SubmenuBottomGap - height, SubmenuButtonWidth, height);
    }

    // IsMouseOverUI가 실제 그려질 서브메뉴 버튼 개수를 정확히 알아야 마우스오버 판정 영역이 실제
    // 버튼 목록과 어긋나지 않는다(DrawXxxSubmenu들이 만드는 항목 수와 그대로 맞춰둔다).
    private int GetActiveSubmenuItemCount()
    {
        switch (_activeCategory)
        {
            case MenuCategory.Command: return 4;
            case MenuCategory.Build: return 3;
            case MenuCategory.Map: return 4;
            case MenuCategory.Encyclopedia: return 1;
            case MenuCategory.Debug:
                int count = 4; // 오브젝트 배치/코어 배치/맵 저장/맵 불러오기
                if (_gameSession != null && _gameSession.unitGenerate != null) count += 1;
                if (_propagationDebugVisualizer != null) count += 7;
                return count;
            default: return 0;
        }
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
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = active ? ActiveColor : InactiveColor;
        if (GUI.Button(new Rect(x, y, BarButtonWidth, BarHeight), label))
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
        GUI.backgroundColor = prev;
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

    private void DrawSubmenu()
    {
        switch (_activeCategory)
        {
            case MenuCategory.Command: DrawCommandSubmenu(); break;
            case MenuCategory.Build: DrawBuildSubmenu(); break;
            case MenuCategory.Map: DrawMapSubmenu(); break;
            case MenuCategory.Encyclopedia: DrawEncyclopediaSubmenu(); break;
            case MenuCategory.Debug: DrawDebugSubmenu(); break;
        }
    }

    // 도감 서브메뉴 — 아직 기능이 없어 비활성 버튼 1개("구현 예정")만 둔다(2026-08-20, 문서: "추후
    // 추가될 기능인데, 버튼만 미리 두기").
    private void DrawEncyclopediaSubmenu()
    {
        DrawVerticalSubmenu(new List<SubmenuItem> { new SubmenuItem("구현 예정", false, false, null) });
    }

    private struct SubmenuItem
    {
        public string label;
        public bool active;
        public bool interactable;
        public Action onClick;

        public SubmenuItem(string label, bool active, bool interactable, Action onClick)
        {
            this.label = label;
            this.active = active;
            this.interactable = interactable;
            this.onClick = onClick;
        }
    }

    // 바 위에 아래에서 위로 쌓이는 세로 버튼 목록 하나를 그린다.
    private void DrawVerticalSubmenu(List<SubmenuItem> items)
    {
        Rect barRect = GetBarRect();
        float x = barRect.x;
        float y = barRect.y - SubmenuBottomGap - SubmenuButtonHeight;

        foreach (var item in items)
        {
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = !item.interactable ? DisabledColor : (item.active ? ActiveColor : InactiveColor);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = item.interactable;

            if (GUI.Button(new Rect(x, y, SubmenuButtonWidth, SubmenuButtonHeight), item.label))
                item.onClick?.Invoke();

            GUI.enabled = wasEnabled;
            GUI.backgroundColor = prev;

            y -= (SubmenuButtonHeight + SubmenuGap);
        }
    }

    // =====================================================
    // 명령 서브메뉴 — "명령 취소"(단일 클릭, 실행 중인 모든 명령 취소) + "이동 및 공격"(토글) + 예정 2개
    // =====================================================
    private void DrawCommandSubmenu()
    {
        var items = new List<SubmenuItem>
        {
            new SubmenuItem("명령 취소", false, true, () => _inputManager?.CancelAllCommands()),
            new SubmenuItem("이동 및 공격", _inputManager != null && _inputManager.IsCommandModeActive, true, OnClickToggleCommandMode),
            new SubmenuItem("(예정)", false, false, null),
            new SubmenuItem("(예정)", false, false, null),
        };
        DrawVerticalSubmenu(items);
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

    // =====================================================
    // 설치 서브메뉴 — 유닛 생산 건물(B) / 자원 생산 건물(V) / 함정(P). 우클릭 취소를 없앤 대신(2026-08-20,
    // 사용자 요청) 각 버튼이 자기 모드일 때만 켜진 걸로 표시하고(예전엔 IsBuildPlacementActive 하나를
    // 셋이 공유해서 "자원 생산 건물"을 골라도 "유닛 생산 건물"까지 같이 켜진 것처럼 보이는 버그가
    // 있었다), 이미 켜진 버튼을 다시 누르면 취소한다 — 그래서 클릭 후에도 카테고리를 닫지 않고 계속
    // 열어둬서 같은 버튼을 바로 다시 누를 수 있게 한다.
    // =====================================================
    private void DrawBuildSubmenu()
    {
        bool unitActive = _inputManager != null && _inputManager.IsUnitBuildModeActive;
        bool resourceActive = _inputManager != null && _inputManager.IsResourceBuildModeActive;
        bool trapActive = _inputManager != null && _inputManager.IsTrapPlacementActive;

        var items = new List<SubmenuItem>
        {
            new SubmenuItem("유닛 생산 건물", unitActive, true, () => ToggleBuildSubMode(unitActive, () => _inputManager?.EnterUnitBuildMode(), "유닛 생산 건물")),
            new SubmenuItem("자원 생산 건물", resourceActive, true, () => ToggleBuildSubMode(resourceActive, () => _inputManager?.EnterResourceBuildMode(), "자원 생산 건물")),
            new SubmenuItem("함정", trapActive, true, () => ToggleBuildSubMode(trapActive, () => _inputManager?.EnterTrapPlacementMode(), "함정")),
        };
        DrawVerticalSubmenu(items);
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
    // 맵 서브메뉴 — 0/1/2/3층 버튼, 층별 카메라 시스템과 연동(CameraController.GoToFloor)
    // =====================================================
    private void DrawMapSubmenu()
    {
        var cam = CameraController.Instance;
        int currentFloor = cam != null ? cam.CurrentFloor : -1;
        int floorCount = 4;
        if (cam == null || !cam.TryGetFloorCount(out floorCount)) floorCount = 4;

        var items = new List<SubmenuItem>();
        for (int f = 0; f < 4; f++)
        {
            int floorIndex = f; // 클로저 캡처
            bool interactable = floorIndex < floorCount;
            items.Add(new SubmenuItem($"{floorIndex}층", floorIndex == currentFloor, interactable,
                () =>
                {
                    _activeCategory = MenuCategory.None;
                    CameraController.Instance?.GoToFloor(floorIndex);
                    NoticeCenter.Instance?.PushMomentary($"{floorIndex}층으로 이동", NoticeCenter.InfoColor);
                }));
        }
        DrawVerticalSubmenu(items);
    }

    // =====================================================
    // debug 서브메뉴 — 시야/소리·전파 시각화 토글, 오브젝트/코어 배치, 맵 저장/불러오기(전부 기존
    // DebugInfoPanel/InputManager 단축키에서 이전)
    // =====================================================
    private void DrawDebugSubmenu()
    {
        var items = new List<SubmenuItem>();

        if (_gameSession != null && _gameSession.unitGenerate != null)
        {
            bool visionOn = _gameSession.unitGenerate.ShowAllVisionRanges;
            items.Add(new SubmenuItem(visionOn ? "■ 시야 표시 (ON)" : "□ 시야 표시 (OFF)", visionOn, true,
                () => _gameSession.unitGenerate.ShowAllVisionRanges = !_gameSession.unitGenerate.ShowAllVisionRanges));
        }

        if (_propagationDebugVisualizer != null)
        {
            var v = _propagationDebugVisualizer;
            AddPropagationToggle(items, "전파 범위", () => v.ShowPropagationRange, val => v.ShowPropagationRange = val);
            AddPropagationToggle(items, "이동음", () => v.ShowMovement, val => v.ShowMovement = val);
            AddPropagationToggle(items, "공격 실행음", () => v.ShowAttackExecution, val => v.ShowAttackExecution = val);
            AddPropagationToggle(items, "피격 발생 공격음", () => v.ShowHitImpact, val => v.ShowHitImpact = val);
            AddPropagationToggle(items, "피격 비명", () => v.ShowHitScream, val => v.ShowHitScream = val);
            AddPropagationToggle(items, "사망음", () => v.ShowDeath, val => v.ShowDeath = val);
            AddPropagationToggle(items, "함정 작동음", () => v.ShowTrapActivation, val => v.ShowTrapActivation = val);
        }

        bool objActive = _inputManager != null && _inputManager.IsObjectOnlyPlacementActive;
        bool coreActive = _inputManager != null && _inputManager.IsCorePlacementActive;
        items.Add(new SubmenuItem("오브젝트 배치", objActive, true, () => ToggleBuildSubMode(objActive, () => _inputManager?.EnterObjectPlacementMode(), "오브젝트")));
        items.Add(new SubmenuItem("코어 배치", coreActive, true, () => ToggleBuildSubMode(coreActive, () => _inputManager?.EnterCorePlacementMode(), "코어")));
        items.Add(new SubmenuItem("맵 저장", false, true, () => SaveMapAsync().Forget()));
        items.Add(new SubmenuItem("맵 불러오기", false, true, () => LoadMapAsync().Forget()));

        DrawVerticalSubmenu(items);
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
