using Cysharp.Threading.Tasks;
using Haare.Client.Core.DI;

// Haare의 UIPresenter/RegisterEntryPoint 경로를 실제로 타는 이 프로젝트의 진입점.
// 기존 UIManager.Start()가 하던 "부팅 시 DebugInfoPanel/도감 패널 프리로딩"을 이쪽으로 옮기고,
// 그 앞뒤로 LoadingFadePanel 페이드를 씌운다(GameCompositionRoot.Configure에서 RegisterEntryPoint로 등록).
//
// UIPresenter.OpenPanelWithFade<T>()는 안 쓴다 — 내부에서 T를 IsStack:true로 로드해서
// SceneUIManager.ClosePanel<LoadingFadePanel>()이 "스택 맨 위인지" 검사할 때 T한테 밀려 실패하고,
// 페이드 패널이 투명한 채로 화면에 남아 클릭을 계속 가로채는 문제가 있다(SceneUiManager.ClosePanel<T>
// 참고). 그래서 DebugInfoPanel/도감 패널은 IsStack:false로 직접 로드해 스택에 안 올리고,
// LoadingFadePanel만 스택 맨 위를 유지하게 해서 FadeOut()의 ClosePanel이 정상 동작하게 한다.
public class GameUIPresenter : UIPresenter
{
    public override void PostInitialize()
    {
        base.PostInitialize();
        BootSequence().Forget();
    }

    private async UniTask BootSequence()
    {
        await FadeIn();

        int debugPanelId = await _coreUIManager.LoadPanel<DebugInfoPanel>(_resolver, null, false, false);
        _coreUIManager.RentPanel<DebugInfoPanel>(debugPanelId).OpenPanel();

        await _coreUIManager.LoadPanel<Game.Encyclopedia.UI.UI_Encyclopedia>(_resolver, null, false, false);

        await FadeOut();
    }
}
