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

        // NoticeCenter(2026-08-26, 사용자 요청 "설정 버튼 누르면, 설정은 아직 미구현 상태입니다.
        // 라는 창이 뜨도록 해줘" — rythoom 프로젝트의 NoticeCenter.Push 참고) — ssh 씬은 이미
        // GameUIPresenter가 부팅 시 로드하지만, Title 씬은 GameSession 등 전역 서비스가 없는
        // 경량 씬(TitleScope.cs 참고)이라 여기서 직접 로드해야 한다. NoticeCenter는 아무것도
        // 주입받지 않는 순수 UI라 ssh 쪽과 완전히 같은 방식으로 재사용 가능하다.
        //
        // try/catch로 감싼 이유(2026-08-26, 빌드 테스트 중 발견) — Addressables Player Content를
        // 빌드하지 않은 상태로 실행하면 이 LoadPanel이 InvalidKeyException을 던지는데, 감싸지
        // 않으면 그 예외가 Initialize() 전체를 중단시켜 바로 아래 GameTitlePanel도 못 뜨고 완전히
        // 검은 화면만 남는다(사용자 신고 "빌드파일 실행하면 검은색 화면밖에 안나오는데?"). 근본
        // 원인은 Addressables 빌드 누락이라 그것부터 고쳐야 하지만, NoticeCenter 하나가 어떤
        // 이유로든 실패해도 타이틀 화면 자체는 뜨도록 GameUIPresenter.BootSequence와 동일한
        // try/catch 관례를 맞춘다.
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

        // KeyGuidePanel(2026-08-26, 사용자 요청 "타이틀과 esc에 키 가이드 항목 넣어줘") — 평소엔
        // 닫아 두고 TitlePresenter.OpenKeyGuide가 연다. GameTitlePanel보다 나중에 로드해야(마지막
        // sibling) 열렸을 때 그 위를 덮는다(GameUIPresenter.BootSequence의 GameSettingsPanel 로드
        // 순서와 동일한 이유).
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
