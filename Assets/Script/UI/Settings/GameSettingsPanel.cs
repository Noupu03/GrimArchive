using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Client.UI;
using R3;
using UnityEngine;
using VContainer;

// ESC 설정 패널(2026-08-26, 사용자 요청 "ssh 씬에서 esc를 누르면, 게임이 space 바를 누른 것처럼 정지
// 상태가 되고, 옵션 panel이 뜨게 해줘" → 이후 "옵션이라는 말, 설정으로 통일해") — Title.unity의
// GameTitlePanel(Assets/Script/UI/Title/)과 같은 시각 스타일(Assets/Editor/SettingsPanelSetup.cs가
// TitleSceneSetup.cs의 배경색/글자색/버튼 스타일을 그대로 재사용해서 생성)을 쓰는 전체화면 오버레이.
// 설정 항목 자체는 아직 없고(사용자 확인: "옵션의 기능들은 아직 미구현이니 타이틀처럼 화면만 두고"),
// 실제로 동작하는 버튼은 재개/타이틀로 나가기 둘뿐이다. BuildingControlPanel.Instance와 동일한 관례로
// GameUIPresenter가 부팅 시 미리 로드해서 닫아 두고(Assets/Script/UI/GameUIPresenter.cs), InputManager가
// ESC 입력마다 이 static Instance를 통해 Toggle()을 호출한다(Assets/Script/Unit/Session/InputManager.cs의
// HandleGameSpeedShortcuts). Title.unity의 "설정" 버튼(TitlePresenter.OpenSettings)은 이 패널과는
// 별개다 — Title 씬은 GameSession이 없어 이 패널을 띄울 수 없으므로 NoticeCenter 토스트로만 안내한다.
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

    // 키 가이드(2026-08-26, 사용자 요청 "타이틀과 esc에 키 가이드 항목 넣어줘") — 게임은 이미 ESC로
    // 일시정지된 상태라 여기서 pause/timeScale을 다시 건드릴 필요는 없다. 이 패널을 잠깐 숨기고
    // KeyGuidePanel을 띄운 뒤, 닫히면 다시 이 패널을 보여준다(TitlePresenter.OpenKeyGuide와 동일한
    // 관례).
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

    // TitlePresenter.StartGame과 동일한 SceneTransitionFade 경로. Time.timeScale은 씬을 넘어가도
    // 유지되는 전역 값이라, 여기서 명시적으로 1f로 되돌리지 않으면 Title.unity가 일시정지 배속(0.0001f)
    // 그대로 남아 다음 "시작" 때까지 영향을 준다.
    private void ExitToTitle()
    {
        IsOpen = false;
        ClosePanel();
        if (_gameSession != null) _gameSession.isPaused = false;
        Time.timeScale = 1f;

        // ssh 게임 UI 정리(2026-08-26, 사용자 신고 "타이틀 화면 돌아가면, 게임에서 사용하던 UI들도 다
        // 없는 깔끔한 상태로 타이틀로 돌아가야지. 지금 타이틀 가도 다 남아있어") — CoreUIManager(Haare
        // 프레임워크, Assets/Haare/Scripts/Client/DI/Container/CoreLifetimeScope.cs)는
        // .DontDestroyOnLoad()로 등록돼 ssh 씬을 언로드해도 살아남는다. NoticeCenter/UIManager/
        // DebugInfoPanel/StatusInfoPanel/WaveGaugePanel/BuildingControlPanel/BottomMenuBar/이 설정
        // 패널 자신까지 지금까지 로드된 모든 게임 UI 패널이 전부 그 하위 계층이라 같이 남아 Title 화면
        // 위에 계속 그려지고 있었다 — CoreUIManager GameObject 자체를 파괴해서 전부 함께 정리한다.
        // ssh.unity가 나중에 다시 로드되면 GameCompositionRoot.Configure()가 새 CoreUIManager를
        // 처음부터 다시 만들어준다.
        // FindObjectsByType(전체 순회)로 — 혹시 이전 테스트 세션에서 이미 중복 생성된 CoreUIManager가
        // 남아있어도(이 정리 로직이 생기기 전에는 나갈 때마다 하나씩 새로 쌓였을 수 있다) 전부 함께 치운다.
        var coreUIManagers = FindObjectsByType<CoreUIManager>(FindObjectsSortMode.None);
        foreach (var coreUIManager in coreUIManagers)
            Destroy(coreUIManager.gameObject);

        SceneTransitionFade.EnsureInstance().LoadSceneWithCoverAsync(TitleSceneName, GameSceneName).Forget();
    }
}
