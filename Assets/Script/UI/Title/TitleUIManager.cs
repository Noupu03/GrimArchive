using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.UI;

// Demo.TitleScene.DemoTitleUIManager와 동일한 역할 — 이 씬(Title.unity)에 국한된 SceneUIManager.
// 부팅 시 GameTitlePanel을 로드해서 화면에 띄운다.
public class TitleUIManager : SceneUIManager
{
    public override async UniTask Initialize(CancellationToken cts)
    {
        await base.Initialize(cts);

        int titlePanelID = await LoadPanel<GameTitlePanel>(null, false, true);
        var panel = RentPanel<GameTitlePanel>(titlePanelID);
        panel.uiManager = this;
        panel.BindEvent();
    }
}
