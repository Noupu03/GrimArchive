using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.UI;
using Haare.Util.Logger;

// Demo.TitleScene.DemoTitleUIManager와 동일한 역할 — 이 씬(Title.unity)에 국한된 SceneUIManager.
// 부팅 시 GameTitlePanel을 로드해서 화면에 띄운다.
public class TitleUIManager : SceneUIManager
{
    public override async UniTask Initialize(CancellationToken cts)
    {
        await base.Initialize(cts);

        // NoticeCenter — Title 씬은 GameSession 등 전역 서비스가 없는 경량 씬(TitleScope.cs)이라
        // 여기서 직접 로드해야 한다. Addressables Player Content 미빌드 시 LoadPanel이 던지는
        // InvalidKeyException이 Initialize() 전체를 중단시켜 GameTitlePanel도 못 뜨는 걸 막기 위해
        // GameUIPresenter.BootSequence와 동일한 try/catch 관례를 맞춘다.
        try
        {
            int noticeCenterId = await LoadPanel<NoticeCenter>(null, false, false);
            RentPanel<NoticeCenter>(noticeCenterId).OpenPanel();
        }
        catch (System.Exception e)
        {
            LogHelper.Error(LogHelper.GAME, $"[UI] NoticeCenter 로드 실패: {e.Message}");
        }

        int titlePanelID = await LoadPanel<GameTitlePanel>(null, false, true);
        var panel = RentPanel<GameTitlePanel>(titlePanelID);
        panel.uiManager = this;
        panel.BindEvent();

        // KeyGuidePanel — 평소엔 닫아 두고 TitlePresenter.OpenKeyGuide가 연다. GameTitlePanel보다
        // 나중에 로드해야(마지막 sibling) 열렸을 때 그 위를 덮는다.
        try
        {
            int keyGuidePanelId = await LoadPanel<KeyGuidePanel>(null, false, false);
            RentPanel<KeyGuidePanel>(keyGuidePanelId).ClosePanel();
        }
        catch (System.Exception e)
        {
            LogHelper.Error(LogHelper.GAME, $"[UI] KeyGuidePanel 로드 실패: {e.Message}");
        }
    }
}
