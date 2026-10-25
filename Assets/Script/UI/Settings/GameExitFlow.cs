using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Client.UI;
using UnityEngine;

// 게임(ssh) → 타이틀 복귀 — ESC 설정 패널의 "타이틀로 나가기"와 패배 화면의 "타이틀로 이동"이 같은 정리 순서를 쓴다.
public static class GameExitFlow
{
    public const string TitleSceneName = "Title";
    public const string GameSceneName = "ssh";

    // TitlePresenter.StartGame과 동일한 SceneTransitionFade 경로. Time.timeScale은 씬 전환 후에도 유지되는 전역 값이라
    // 명시적으로 1f로 되돌리지 않으면 일시정지 배속이 Title에 그대로 남는다.
    public static void ReturnToTitle(GameSession gameSession)
    {
        if (gameSession != null) gameSession.isPaused = false;
        Time.timeScale = 1f;

        // CoreUIManager는 DontDestroyOnLoad로 등록돼 ssh 씬을 언로드해도 살아남고 게임 UI 패널들이
        // 그 하위 계층이라 함께 Title 화면 위에 남으므로 GameObject 자체를 파괴해 정리한다
        // (ssh.unity 재진입 시 GameCompositionRoot.Configure()가 새로 만들어준다).
        var coreUIManagers = Object.FindObjectsByType<CoreUIManager>(FindObjectsSortMode.None);
        foreach (var coreUIManager in coreUIManagers)
            Object.Destroy(coreUIManager.gameObject);

        SceneTransitionFade.EnsureInstance().LoadSceneWithCoverAsync(TitleSceneName, GameSceneName).Forget();
    }
}
