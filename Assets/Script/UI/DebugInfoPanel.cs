using System.Text;
using UnityEngine;
using VContainer;
using R3;
using Haare.Client.Routine;
using Haare.Client.UI;

// UIManager.OnGUI()의 DrawTopRightUI()/DrawSelectedUnitInfo()를 대체하는 Haare UGUI 패널.
// 프리팹은 Assets/Editor/HaareUISetup.cs("Tools/GrimArchive/Haare UI 셋업 생성")로 생성/배선된다.
[PanelAttribute("Prefabs/DebugInfoPanel")]
public class DebugInfoPanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    [SerializeField] private CustomText selectedUnitInfoText;
    [SerializeField] private CustomButton zoomInButton;
    [SerializeField] private CustomButton zoomOutButton;
    [SerializeField] private CustomSlider gameSpeedSlider;

    private InputManager _inputManager;
    private float _lastSliderValue = -1f;

    [Inject]
    public void Construct(InputManager inputManager)
    {
        _inputManager = inputManager;
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
        zoomInButton.Onclicked.Subscribe(_ => Zoom(-2f)).AddTo(disposables);
        zoomOutButton.Onclicked.Subscribe(_ => Zoom(2f)).AddTo(disposables);

        float initialSpeed = GameSession.Instance != null ? GameSession.Instance.currentGameSpeed : 1f;
        gameSpeedSlider.Setup(0.5f, 3f, initialSpeed);
        _lastSliderValue = initialSpeed;
    }

    private void Zoom(float delta)
    {
        if (Camera.main == null) return;
        Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize + delta, 5f, 50f);
    }

    protected override void UpdateProcess()
    {
        base.UpdateProcess();

        // CustomSlider엔 값 변경 이벤트가 없어 매 프레임 폴링해서 GameSession에 반영한다.
        if (GameSession.Instance != null && !Mathf.Approximately(gameSpeedSlider.Value, _lastSliderValue))
        {
            _lastSliderValue = gameSpeedSlider.Value;
            GameSession.Instance.currentGameSpeed = _lastSliderValue;
            if (!GameSession.Instance.isPaused)
                Time.timeScale = _lastSliderValue;
        }

        RefreshSelectedUnitInfo();
    }

    private void RefreshSelectedUnitInfo()
    {
        if (_inputManager == null || _inputManager.selectedUnit == null)
        {
            selectedUnitInfoText.SetupText("");
            return;
        }

        Unit u = _inputManager.selectedUnit;
        var sb = new StringBuilder();

        sb.AppendLine($"<b>이름:</b> {u.unitType.typeName}");
        sb.AppendLine($"<b>진영:</b> {(u is Human ? "인류" : "몬스터")}");
        sb.AppendLine();
        sb.AppendLine($"<b>HP:</b> {u.hp:F1}");
        if (u is Human)
        {
            sb.AppendLine($"<b>MP:</b> {u.mp:F1}");
            string panicStr = (u.mental < u.maxMental * 0.3f) ? " <color=red>공황</color>" : "";
            sb.AppendLine($"<b>정신력:</b> {u.mental:F1} / {u.maxMental:F1}{panicStr}");
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
}
