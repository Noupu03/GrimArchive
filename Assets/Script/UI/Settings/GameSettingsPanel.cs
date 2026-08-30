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

    private const string TitleSceneName = "Title";
    private const string GameSceneName = "ssh";

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

    // TitlePresenter.StartGame과 동일한 SceneTransitionFade 경로. Time.timeScale은 씬 전환 후에도
    // 유지되는 전역 값이라 명시적으로 1f로 되돌리지 않으면 일시정지 배속이 Title에 그대로 남는다.
    private void ExitToTitle()
    {
        IsOpen = false;
        ClosePanel();
        if (_gameSession != null) _gameSession.isPaused = false;
        Time.timeScale = 1f;

        // CoreUIManager는 DontDestroyOnLoad로 등록돼 ssh 씬을 언로드해도 살아남고 게임 UI 패널들이
        // 그 하위 계층이라 함께 Title 화면 위에 남으므로 GameObject 자체를 파괴해 정리한다
        // (ssh.unity 재진입 시 GameCompositionRoot.Configure()가 새로 만들어준다).
        var coreUIManagers = FindObjectsByType<CoreUIManager>(FindObjectsSortMode.None);
        foreach (var coreUIManager in coreUIManagers)
            Destroy(coreUIManager.gameObject);

        SceneTransitionFade.EnsureInstance().LoadSceneWithCoverAsync(TitleSceneName, GameSceneName).Forget();
    }
}
