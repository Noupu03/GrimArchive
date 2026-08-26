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

        // NoticeCenter(2026-08-26, 사용자 요청 "설정 버튼 누르면, 설정은 아직 미구현 상태입니다.
        // 라는 창이 뜨도록 해줘" — rythoom 프로젝트의 NoticeCenter.Push 참고) — ssh 씬은 이미
        // GameUIPresenter가 부팅 시 로드하지만, Title 씬은 GameSession 등 전역 서비스가 없는
        // 경량 씬(TitleScope.cs 참고)이라 여기서 직접 로드해야 한다. NoticeCenter는 아무것도
        // 주입받지 않는 순수 UI라 ssh 쪽과 완전히 같은 방식으로 재사용 가능하다.
        int noticeCenterId = await LoadPanel<NoticeCenter>(null, false, false);
        RentPanel<NoticeCenter>(noticeCenterId).OpenPanel();

        int titlePanelID = await LoadPanel<GameTitlePanel>(null, false, true);
        var panel = RentPanel<GameTitlePanel>(titlePanelID);
        panel.uiManager = this;
        panel.BindEvent();
    }
}
