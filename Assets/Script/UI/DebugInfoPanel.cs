using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using R3;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Client.UI;
using Haare.Scripts.Client.Data;
using Haare.Util.Loader;
using Haare.Util.Logger;

// UIManager.OnGUI()의 DrawTopRightUI()/DrawSelectedUnitInfo()를 대체하는 Haare UGUI 패널.
// 프리팹은 Assets/Editor/HaareUISetup.cs("Tools/GrimArchive/Haare UI 셋업 생성")로 생성/배선된다.
[PanelAttribute("Prefabs/DebugInfoPanel")]
public class DebugInfoPanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    [SerializeField] private CustomText selectedUnitInfoText;
    [SerializeField] private CustomButton saveButton;
    [SerializeField] private CustomButton loadButton;

    private const float ScrollZoomSpeed = 0.02f;

    private InputManager _inputManager;
    private DataManager _dataManager;
    private GameSession _gameSession;
    private PropagationDebugVisualizer _propagationDebugVisualizer;

    [Inject]
    public void Construct(InputManager inputManager, DataManager dataManager, GameSession gameSession, PropagationDebugVisualizer propagationDebugVisualizer)
    {
        _inputManager = inputManager;
        _dataManager = dataManager;
        _gameSession = gameSession;
        _propagationDebugVisualizer = propagationDebugVisualizer;
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
        // 프리팹이 스크립트보다 오래돼서(Tools > GrimArchive > Haare UI 셋업 생성 재실행 전) 참조가
        // 비어있는 경우 하나가 null이어도 나머지 바인딩까지 통째로 죽지 않도록 방어적으로 처리한다.
        if (saveButton == null || loadButton == null)
        {
            LogHelper.Error(LogHelper.GAME,
                "DebugInfoPanel 필드가 비어 있습니다. Tools > GrimArchive > Haare UI 셋업 생성을 다시 실행해서 프리팹을 갱신하세요.");
        }

        if (saveButton != null) saveButton.Onclicked.Subscribe(_ => SaveMapAsync().Forget()).AddTo(disposables);
        if (loadButton != null) loadButton.Onclicked.Subscribe(_ => LoadMapAsync().Forget()).AddTo(disposables);
    }

    private void Zoom(float delta)
    {
        if (Camera.main == null) return;
        Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize + delta, 5f, 50f);
    }

    // 유닛 상태는 저장 대상이 아님 — 맵(층/청크/타일/점령 상태)만 저장/복원한다.
    private async UniTaskVoid SaveMapAsync()
    {
        var cmap = _gameSession != null ? _gameSession.cmap : null;
        if (cmap == null) return;

        var dto = MapSerializer.MapToDto(cmap.map);
        await _dataManager.SaveData<MapSaveModel, MapSerializer.MapDto>(null, dto);
        LogHelper.Log(LogHelper.GAME, "맵 저장 완료 (Save/map.json)");
    }

    private async UniTaskVoid LoadMapAsync()
    {
        var cmap = _gameSession != null ? _gameSession.cmap : null;
        if (cmap == null) return;

        if (!AssetLoader.Exists("Save/map.json"))
        {
            LogHelper.Warning(LogHelper.GAME, "저장된 맵이 없습니다.");
            return;
        }

        var model = await _dataManager.GetModel<MapSaveModel>();
        if (model == null) return;

        cmap.ApplyMap(model.Map);
        LogHelper.Log(LogHelper.GAME, "맵 불러오기 완료");
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
    }

    protected override void UpdateProcess()
    {
        base.UpdateProcess();

        // 줌인/줌아웃 버튼 대신 마우스 휠로 카메라 줌 조절.
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0f))
                Zoom(-scroll * ScrollZoomSpeed);
        }
    }

    private void OnGUI()
    {
        DrawVisionToggle();
        DrawPropagationToggle();

        if (_inputManager == null || _inputManager.selectedUnits.Count == 0) return;

        bool isMultiSelect = _inputManager.selectedUnits.Count > 1;
        string title = isMultiSelect
            ? $"Unit Status Test ({_inputManager.selectedUnits.Count}기 선택됨)"
            : "Unit Status Test";
        // StatusInfoPanel의 우상단 박스(y50~350)와 안 겹치도록 그 아래에서 시작한다
        // (사용자 요청 "UI 배치들 겹치지 않게 정리", 2026-07-23).
        GUILayout.BeginArea(new Rect(Screen.width - 220, 360, 200, 150), title, GUI.skin.window);

        // 다수 선택 시엔 특정 유닛 하나를 편집하는 버튼들이 의미가 없어서 숨긴다 —
        // 아래 selectedUnitInfoText 쪽도 스탯 대신 선택된 유닛 목록만 보여준다(RefreshSelectedUnitInfo).
        if (!isMultiSelect)
        {
            Unit u = _inputManager.selectedUnit;

            if (GUILayout.Button("Add 10 EXP"))
            {
                u.BaseStat.exp += 10f;
                RefreshSelectedUnitInfo();
            }

            if (GUILayout.Button("Add 1 Kill"))
            {
                u.killCount += 1;
                RefreshSelectedUnitInfo();
            }

            if (GUILayout.Button("Level Up"))
            {
                u.level += 1;
                RefreshSelectedUnitInfo();
            }
        }

        GUILayout.EndArea();
    }

    // 우측 상단, 선택 상태와 무관하게 항상 보이는 전역 토글 — 켜면 모든 유닛의 시야 범위(연한
    // 색)/인지 범위(진한 색)가 진영별 색(인류 파랑 계열/몬스터 빨강 계열)으로 동시에 표시된다
    // (UnitGenerate.ShowAllVisionRanges, UnitVisual 참고).
    private void DrawVisionToggle()
    {
        if (_gameSession == null || _gameSession.unitGenerate == null) return;

        GUILayout.BeginArea(new Rect(Screen.width - 220, 10, 200, 40));
        bool current = _gameSession.unitGenerate.ShowAllVisionRanges;
        if (GUILayout.Button($"시야 표시: {(current ? "켜짐" : "꺼짐")}"))
        {
            _gameSession.unitGenerate.ShowAllVisionRanges = !current;
        }
        GUILayout.EndArea();
    }

    // 항목별로 켜고 끌 수 있다(PropagationDebugVisualizer, 07 소리·전파 시스템 임시 검증용).
    // 2026-08-05: 사용자 요청으로 단일 on/off 버튼을 색 표 기준별 개별 토글로 분리하고, 버튼 배경색을
    // 켜짐/꺼짐에 따라 뚜렷하게 다르게 칠해 상태가 한눈에 보이게 했다(전에는 텍스트만 바뀌어서 눈에
    // 잘 안 띈다는 지적을 받음). 켜짐일 땐 실제 원 색과 같은 색으로 칠해 표와 바로 대응되게 한다.
    // 위치는 원래 우측 상단(시야 표시 토글 바로 아래)에 뒀었는데, StatusInfoPanel(오펜스 현황/자원
    // 사용 안내, x:Screen.width-270~Screen.width-10, y:50~350)과 그대로 겹친다는 지적을 받아 좌측
    // 상단으로 옮겼다 — UIManager.DrawTopLeftUI가 y10~100(FPS 카운터+게임 속도 표시)만 쓰고 나머지
    // 좌측 상단 UI(유물 생성 모드/파티 상태 등)는 전부 주석 처리돼 죽어 있어서 y110부터는 비어 있다.
    // 화면 하단 좌측(x10~250, BuildingControlPanel 왼쪽)의 선택 유닛 정보 UGUI 프리팹과도 세로로
    // 충분히 떨어져 있어(그쪽은 화면 하단에서 위로 480px만 차지) 겹치지 않는다.
    private const int PanelX = 10;
    private const int PanelY = 110;
    private static readonly Color OffButtonColor = new Color(0.4f, 0.4f, 0.4f);

    private void DrawPropagationToggle()
    {
        if (_propagationDebugVisualizer == null) return;
        var v = _propagationDebugVisualizer;

        GUILayout.BeginArea(new Rect(PanelX, PanelY, 220, 240), GUI.skin.box);
        GUILayout.Label("<b>소리/전파 시각화</b>");
        DrawColorToggle("전파 범위", ref v.ShowPropagationRange, new Color(0.2f, 0.8f, 1f));
        DrawColorToggle("이동음", ref v.ShowMovement, new Color(0.6f, 0.6f, 0.6f));
        DrawColorToggle("공격 실행음", ref v.ShowAttackExecution, new Color(1f, 0.6f, 0f));
        DrawColorToggle("피격 발생 공격음", ref v.ShowHitImpact, new Color(1f, 0.2f, 0.2f));
        DrawColorToggle("피격 비명", ref v.ShowHitScream, new Color(1f, 0f, 0.6f));
        DrawColorToggle("사망음", ref v.ShowDeath, Color.white);
        DrawColorToggle("함정 작동음", ref v.ShowTrapActivation, new Color(1f, 1f, 0f));
        GUILayout.EndArea();
    }

    // label 앞에 켜짐/꺼짐 표시 문자를 붙이고, 버튼 배경색을 켜짐이면 그 항목의 실제 원 색(진하게),
    // 꺼짐이면 회색으로 칠한다 — 텍스트만으로 상태를 구분해야 했던 예전 버튼보다 훨씬 눈에 띈다.
    private static void DrawColorToggle(string label, ref bool state, Color onColor)
    {
        Color prevColor = GUI.backgroundColor;
        GUI.backgroundColor = state ? onColor : OffButtonColor;
        if (GUILayout.Button(state ? $"■ {label} (ON)" : $"□ {label} (OFF)"))
            state = !state;
        GUI.backgroundColor = prevColor;
    }

    private void RefreshSelectedUnitInfo()
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
        var sb = new StringBuilder();

        // 2026-07-27 사용자 요청 — 파티 리더를 이름 옆 별표로 구분(어느 유닛이 7-3장 코어 조사 등을
        // 담당하는 리더인지 클릭만으로 알 수 있게).
        bool isLeader = u is Human leaderCheck && leaderCheck.party != null && leaderCheck.party.Leader == leaderCheck;
        sb.AppendLine($"<b>이름:</b> {u.unitType.typeName}{(isLeader ? " <color=yellow>★(리더)</color>" : "")}");
        sb.AppendLine($"<b>진영:</b> {(u.IsHumanFaction ? "인류" : (u.IsPlayerMonsterFaction ? "플레이어 몬스터" : "야생 몬스터"))}");
        sb.AppendLine($"<b>LV:</b> {u.level}");
        sb.AppendLine($"<b>EXP:</b> {u.BaseStat.exp:F1}");
        sb.AppendLine($"<b>킬 카운트:</b> {u.killCount}");
        sb.AppendLine();
        sb.AppendLine($"<b>HP:</b> {u.Health.hp:F1}");
        if (u is Human humanObj)
        {
            sb.AppendLine($"<b>MP:</b> {humanObj.Health.mp:F1}");
            string panicStr = (humanObj.BaseStat.mental < humanObj.BaseStat.maxMental * 0.3f) ? " <color=red>공황</color>" : "";
            sb.AppendLine($"<b>정신력:</b> {humanObj.BaseStat.mental:F1} / {humanObj.BaseStat.maxMental:F1}{panicStr}");
            
            if (humanObj.Memory.collectedObjects.Count > 0)
                sb.AppendLine($"<color=yellow><b>Collected Objects:</b> {humanObj.Memory.collectedObjects.Count}</color>");
            else
                sb.AppendLine($"<b>Collected Objects:</b> 0");

            sb.AppendLine();
            if (humanObj.UnitParty.party != null)
            {
                Party party = humanObj.UnitParty.party;
                string wipeStr = party.IsWiped ? " <color=red>(전멸)</color>" : (party.WaveEnded ? " <color=cyan>(웨이브 종료)</color>" : "");
                sb.AppendLine($"<b>파티:</b> {party.Name} ({party.Members.Count}명){wipeStr}");
                foreach (var member in party.Members)
                {
                    if (member == null) continue;
                    string color = member.Health.hp <= 0 ? "red" : (member == u ? "yellow" : "white");
                    string self = member == u ? " ◀" : "";
                    string leaderMark = member == party.Leader ? " ★" : "";
                    sb.AppendLine($"  <color={color}>{member.unitType.typeName}{leaderMark}: {member.Health.hp:F0}/{member.Health.maxHp:F0}{self}</color>");
                }
            }
            else
            {
                sb.AppendLine("<b>파티:</b> 없음");
            }
        }
        sb.AppendLine();
        sb.AppendLine($"<b>물리공격력:</b> {u.CombatStat.physicalAttack:F1}");
        sb.AppendLine($"<b>물리방어력:</b> {u.CombatStat.physicalDefense:F1}");
        sb.AppendLine($"<b>마법공격력:</b> {u.CombatStat.magicalAttack:F1}");
        sb.AppendLine($"<b>마법방어력:</b> {u.CombatStat.magicalDefense:F1}");
        sb.AppendLine();
        sb.AppendLine($"<b>이동속도:</b> {u.BaseStat.walkSpeed:F1}");
        sb.AppendLine();
        sb.AppendLine("<b>기본 능력치</b>");
        sb.AppendLine($"근력: {u.BaseStat.sterngth:F1}");
        sb.AppendLine($"내구: {u.BaseStat.Durability:F1}");
        sb.AppendLine($"민첩: {u.BaseStat.agility:F1}");
        sb.AppendLine($"집중: {u.concentration:F1}");
        sb.AppendLine($"마력: {u.MagicPower:F1}");
        sb.AppendLine($"저항: {u.resistance:F1}");
        sb.AppendLine($"감각: {u.BaseStat.sense:F1}");
        sb.AppendLine($"통솔: {u.leadership:F1}");
        sb.AppendLine($"<b>위치:</b> ({u.position.x}, {u.position.y}) F{u.currentFloor}");

        string statusStr = "";
        if (u.StatusEffects.State.stunDuration > 0) statusStr += $"기절({u.StatusEffects.State.stunDuration:F1}s) ";
        if (u.StatusEffects.State.slowDuration > 0) statusStr += $"둔화({u.StatusEffects.State.slowDuration:F1}s) ";
        if (u.StatusEffects.State.poisonDuration > 0) statusStr += $"중독({u.StatusEffects.State.poisonDuration:F1}s) ";
        if (u.StatusEffects.State.burnDuration > 0) statusStr += $"화상({u.StatusEffects.State.burnDuration:F1}s) ";
        if (statusStr != "")
            sb.AppendLine($"<color=red>상태이상: {statusStr}</color>");

        selectedUnitInfoText.SetupText(sb.ToString());
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
