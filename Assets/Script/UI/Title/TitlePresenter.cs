using Cysharp.Threading.Tasks;
using Haare.Client.Core.DI;
using Haare.Client.UI;
using Haare.Util.Logger;
using R3;
using UnityEngine;
using VContainer;

// Demo.TitleScene.DemoTitleUIPresenter와 동일한 구조 — 이 씬(Title.unity)의 진입점.
// GameTitlePanel이 로드되면 시작/설정/종료 버튼을 구독해서 실제 동작(씬 전환/종료)을 연결한다.
//
// 씬 전환은 Haare의 SceneService(Addressables 기반, 아직 이 프로젝트 실 전환에는 안 쓰인 미검증
// 경로 — Assets/Haare/Scripts/Util/AssetLoader/AssetPath.cs 주석 참고) 대신, ssh.unity를 그대로
// Build Settings에 등록해서 쓰는 평범한 SceneManager + SceneTransitionFade를 쓴다.
public class TitlePresenter : IPresenter
{
    [Inject] private SceneUIManager _sceneUiManager;

    public CompositeDisposable disposables { get; } = new CompositeDisposable();

    public void Dispose()
    {
        disposables.Dispose();
        LogHelper.Log(LogHelper.GAME, $"{GetType()} Disposed");
    }

    public void PostInitialize()
    {
        disposables.Add(
            _sceneUiManager.OnLoadedPanel.AsObservable()
                .Select(p => p as GameTitlePanel)
                .Where(p => p != null)
                .Subscribe(BindPanel));
    }

    private void BindPanel(GameTitlePanel panel)
    {
        if (panel.StartButton != null)
            panel.StartButton.Onclicked.AsObservable().Subscribe(_ => StartGame()).AddTo(disposables);

        if (panel.KeyGuideButton != null)
            panel.KeyGuideButton.Onclicked.AsObservable().Subscribe(_ => OpenKeyGuide(panel)).AddTo(disposables);

        if (panel.SettingsButton != null)
            panel.SettingsButton.Onclicked.AsObservable().Subscribe(_ => OpenSettings()).AddTo(disposables);

        if (panel.QuitButton != null)
            panel.QuitButton.Onclicked.AsObservable().Subscribe(_ => QuitGame()).AddTo(disposables);

        panel.OpenPanel();
    }

    private const string TitleSceneName = "Title";
    private const string GameSceneName = "ssh";

    // ssh 씬 자체의 초기화(맵 생성 등)가 진행되는 동안에도 화면이 잠깐 끊겨 보인다는 신고(사용자,
    // 2026-07-23 "씬 시작할때 검은색 점등 후에 시작해 — 이 사이 로딩 중에를 검은 오버레이로
    // 덮어버리자")에 따라 SceneTransitionFade가 로딩 구간 전체를 의도적인 검은 오버레이로 덮는다.
    private void StartGame()
    {
        LogHelper.Log(LogHelper.GAME, "[TitlePresenter] 게임 시작 -> ssh.unity 로드");
        SceneTransitionFade.EnsureInstance().LoadSceneWithCoverAsync(GameSceneName, TitleSceneName).Forget();
    }

    // 2026-08-26, 사용자 요청 "설정 버튼 누르면, 설정은 아직 미구현 상태입니다. 라는 창이 뜨도록
    // 해줘"(rythoom 프로젝트의 NoticeCenter.Push 참고) — 설정 패널 자체는 아직 없어 자리만 잡아둔다
    // (RealBioSearch 구조 이식 — 버튼 3종 배치는 동일하게 유지).
    private void OpenSettings()
    {
        NoticeCenter.Instance?.PushMomentary("설정은 아직 미구현 상태입니다.", NoticeCenter.WarningColor);
        LogHelper.Log(LogHelper.GAME, "[TitlePresenter] 설정 패널은 아직 준비되지 않았습니다.");
    }

    // 키 가이드(2026-08-26, 사용자 요청 "타이틀과 esc에 키 가이드 항목 넣어줘" — rythoom의
    // PauseMenu.OpenKeyGuide와 동일한 패턴: 타이틀 패널을 잠깐 숨기고 KeyGuidePanel을 띄운 뒤, 닫히면
    // 다시 타이틀 패널을 보여준다). GameSettingsPanel.OpenKeyGuide와 동일한 관례.
    private void OpenKeyGuide(GameTitlePanel panel)
    {
        panel.ClosePanel();
        KeyGuidePanel.Instance?.Open(() => panel.OpenPanel());
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
