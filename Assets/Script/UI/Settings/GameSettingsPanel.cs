using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Client.UI;
using R3;
using UnityEngine;
using VContainer;

// ESC 설정 패널 — Title.unity의 GameTitlePanel과 같은 시각 스타일을 쓰는 전체화면 오버레이. 설정
// 항목은 아직 없고 재개/타이틀로 나가기 버튼만 동작한다. Title.unity의 "설정" 버튼은 별개 — Title
// 씬엔 GameSession이 없어 이 패널을 띄울 수 없으므로 NoticeCenter 토스트로만 안내한다.
[PanelAttribute("Prefabs/GrimArchive_SettingsPanel")]
public class GameSettingsPanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    // InputManager가 ESC 입력마다 바로 참조할 수 있도록(BuildingControlPanel.Instance와 동일 관례).
    public static GameSettingsPanel Instance { get; private set; }

    [SerializeField] public CustomButton ResumeButton;
    [SerializeField] public CustomButton KeyGuideButton;
    [SerializeField] public CustomButton QuitToTitleButton;

    private GameSession _gameSession;

    public bool IsOpen { get; private set; }

    [Inject]
    public void Construct(GameSession gameSession)
    {
        _gameSession = gameSession;
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

    public void BindEvent()
    {
        if (ResumeButton != null)
            ResumeButton.Onclicked.AsObservable().Subscribe(_ => Close());

        if (KeyGuideButton != null)
            KeyGuideButton.Onclicked.AsObservable().Subscribe(_ => OpenKeyGuide());

        if (QuitToTitleButton != null)
            QuitToTitleButton.Onclicked.AsObservable().Subscribe(_ => ExitToTitle());
    }

    // 게임은 이미 ESC로 일시정지된 상태라 pause/timeScale은 건드리지 않는다. 이 패널을 잠깐 숨기고
    // KeyGuidePanel을 띄운 뒤 닫히면 다시 보여준다(TitlePresenter.OpenKeyGuide와 동일한 관례).
    private void OpenKeyGuide()
    {
        ClosePanel();
        KeyGuidePanel.Instance?.Open(() => OpenPanel());
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    // 스페이스바 일시정지(GameInputScheme.PausePressedThisFrame 처리부)와 동일한 Time.timeScale
    // 규칙을 공유한다 — ESC로 열든 재개 버튼으로 닫든 항상 이 경로 하나만 거친다.
    public void Open()
    {
        OpenPanel();
        IsOpen = true;
        if (_gameSession != null)
        {
            _gameSession.isPaused = true;
            Time.timeScale = 0.0001f;
        }
    }

    public void Close()
    {
        ClosePanel();
        IsOpen = false;
        if (_gameSession != null)
        {
            _gameSession.isPaused = false;
            Time.timeScale = _gameSession.currentGameSpeed;
        }
    }

    private void ExitToTitle()
    {
        IsOpen = false;
        ClosePanel();
        GameExitFlow.ReturnToTitle(_gameSession);
    }
}
