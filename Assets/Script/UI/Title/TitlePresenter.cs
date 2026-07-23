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
// Build Settings에 등록해서 쓰는 평범한 SceneManager.LoadSceneAsync() + SceneTransitionFade를 쓴다.
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

        if (panel.SettingsButton != null)
            panel.SettingsButton.Onclicked.AsObservable().Subscribe(_ => OpenSettings()).AddTo(disposables);

        if (panel.QuitButton != null)
            panel.QuitButton.Onclicked.AsObservable().Subscribe(_ => QuitGame()).AddTo(disposables);

        panel.OpenPanel();
    }

    private void StartGame()
    {
        LogHelper.Log(LogHelper.GAME, "[TitlePresenter] 게임 시작 -> ssh.unity 로드");
        // 동기 LoadScene 한 줄로는 씬 전환 프레임에 카메라가 끊겨 화면이 검게 번쩍였다(사용자 신고,
        // 2026-07-23) — SceneTransitionFade가 로드 전후로 페이드인/아웃해서 그 프레임을 가린다.
        SceneTransitionFade.EnsureInstance().LoadSceneWithFade("ssh");
    }

    private void OpenSettings()
    {
        // 설정 패널이 아직 없어 자리만 잡아둔다(RealBioSearch 구조 이식 — 버튼 3종 배치는 동일하게 유지).
        LogHelper.Log(LogHelper.GAME, "[TitlePresenter] 설정 패널은 아직 준비되지 않았습니다.");
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
