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

    [Inject]
    public void Construct(InputManager inputManager, DataManager dataManager, GameSession gameSession)
    {
        _inputManager = inputManager;
        _dataManager = dataManager;
        _gameSession = gameSession;
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

        RefreshSelectedUnitInfo();
    }

    private void OnGUI()
    {
        if (_inputManager == null || _inputManager.selectedUnits.Count == 0) return;

        bool isMultiSelect = _inputManager.selectedUnits.Count > 1;
        string title = isMultiSelect
            ? $"Unit Status Test ({_inputManager.selectedUnits.Count}기 선택됨)"
            : "Unit Status Test";
        GUILayout.BeginArea(new Rect(Screen.width - 220, 10, 200, 150), title, GUI.skin.window);

        // 다수 선택 시엔 특정 유닛 하나를 편집하는 버튼들이 의미가 없어서 숨긴다 —
        // 아래 selectedUnitInfoText 쪽도 스탯 대신 선택된 유닛 목록만 보여준다(RefreshSelectedUnitInfo).
        if (!isMultiSelect)
        {
            Unit u = _inputManager.selectedUnit;

            if (GUILayout.Button("Add 10 EXP"))
            {
                u.exp += 10f;
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

        sb.AppendLine($"<b>이름:</b> {u.unitType.typeName}");
        sb.AppendLine($"<b>진영:</b> {(u is Human ? "인류" : "몬스터")}");
        sb.AppendLine($"<b>LV:</b> {u.level}");
        sb.AppendLine($"<b>EXP:</b> {u.exp:F1}");
        sb.AppendLine($"<b>킬 카운트:</b> {u.killCount}");
        sb.AppendLine();
        sb.AppendLine($"<b>HP:</b> {u.hp:F1}");
        if (u is Human humanObj)
        {
            sb.AppendLine($"<b>MP:</b> {humanObj.mp:F1}");
            string panicStr = (humanObj.mental < humanObj.maxMental * 0.3f) ? " <color=red>공황</color>" : "";
            sb.AppendLine($"<b>정신력:</b> {humanObj.mental:F1} / {humanObj.maxMental:F1}{panicStr}");
            
            if (humanObj.collectedObjects.Count > 0)
                sb.AppendLine($"<color=yellow><b>Collected Objects:</b> {humanObj.collectedObjects.Count}</color>");
            else
                sb.AppendLine($"<b>Collected Objects:</b> 0");

            sb.AppendLine();
            if (humanObj.party != null)
            {
                Party party = humanObj.party;
                string wipeStr = party.IsWiped ? " <color=red>(전멸)</color>" : (party.WaveEnded ? " <color=cyan>(웨이브 종료)</color>" : "");
                sb.AppendLine($"<b>파티:</b> {party.Name} ({party.Members.Count}명){wipeStr}");
                foreach (var member in party.Members)
                {
                    if (member == null) continue;
                    string color = member.hp <= 0 ? "red" : (member == u ? "yellow" : "white");
                    string self = member == u ? " ◀" : "";
                    sb.AppendLine($"  <color={color}>{member.unitType.typeName}: {member.hp:F0}/{member.maxHp:F0}{self}</color>");
                }
            }
            else
            {
                sb.AppendLine("<b>파티:</b> 없음");
            }
        }
        sb.AppendLine();
        sb.AppendLine($"<b>물리공격력:</b> {u.physicalAttack:F1}");
        sb.AppendLine($"<b>물리방어력:</b> {u.physicalDefense:F1}");
        sb.AppendLine($"<b>마법공격력:</b> {u.magicalAttack:F1}");
        sb.AppendLine($"<b>마법방어력:</b> {u.magicalDefense:F1}");
        sb.AppendLine();
        sb.AppendLine($"<b>이동속도:</b> {u.walkSpeed:F1}");
        sb.AppendLine();
        sb.AppendLine("<b>기본 능력치</b>");
        sb.AppendLine($"근력: {u.sterngth:F1}");
        sb.AppendLine($"내구: {u.Durability:F1}");
        sb.AppendLine($"민첩: {u.agility:F1}");
        sb.AppendLine($"집중: {u.concentration:F1}");
        sb.AppendLine($"마력: {u.MagicPower:F1}");
        sb.AppendLine($"저항: {u.resistance:F1}");
        sb.AppendLine($"감각: {u.sense:F1}");
        sb.AppendLine($"통솔: {u.leadership:F1}");
        sb.AppendLine($"<b>위치:</b> ({u.position.x}, {u.position.y}) F{u.currentFloor}");

        string statusStr = "";
        if (u.stunDuration > 0) statusStr += $"기절({u.stunDuration:F1}s) ";
        if (u.slowDuration > 0) statusStr += $"둔화({u.slowDuration:F1}s) ";
        if (u.poisonDuration > 0) statusStr += $"중독({u.poisonDuration:F1}s) ";
        if (u.burnDuration > 0) statusStr += $"화상({u.burnDuration:F1}s) ";
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
            string color = u.hp <= 0 ? "red" : (u is Human ? "white" : "yellow");
            sb.AppendLine($"<color={color}>{u.unitType.typeName} ({faction}) — {u.hp:F0}/{u.maxHp:F0}</color>");
        }

        return sb.ToString();
    }
}
