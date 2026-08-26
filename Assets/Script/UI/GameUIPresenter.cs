using Cysharp.Threading.Tasks;
using Haare.Client.Core.DI;
using UnityEngine;
using Haare.Util.Logger;

public class GameUIPresenter : UIPresenter
{
    public override void PostInitialize()
    {
        base.PostInitialize();
        BootSequence().Forget();
    }

    private async UniTask BootSequence()
    {
        try
        {
            await FadeIn();

            // UI 리팩토링(2026-08-20, "모든 UI Haare 프레임워크에 편입") — NoticeCenter/UIManager는
            // 다른 모든 패널·게임플레이 코드(Unit.UI 등)가 기대는 가장 기초적인 상시 오버레이라
            // 맨 먼저 띄운다.
            try
            {
                int noticeCenterId = await _coreUIManager.LoadPanel<NoticeCenter>(_resolver, null, false, false);
                _coreUIManager.RentPanel<NoticeCenter>(noticeCenterId).OpenPanel();
            }
            catch (System.Exception e)
            {
                LogHelper.Error(LogHelper.GAME, $"[UI] NoticeCenter 로드 실패: {e.Message}");
            }

            try
            {
                int uiManagerId = await _coreUIManager.LoadPanel<UIManager>(_resolver, null, false, false);
                _coreUIManager.RentPanel<UIManager>(uiManagerId).OpenPanel();
            }
            catch (System.Exception e)
            {
                LogHelper.Error(LogHelper.GAME, $"[UI] UIManager 로드 실패: {e.Message}");
            }

            int debugPanelId = await _coreUIManager.LoadPanel<DebugInfoPanel>(_resolver, null, false, false);
            _coreUIManager.RentPanel<DebugInfoPanel>(debugPanelId).OpenPanel();

            try
            {
                int statusPanelId = await _coreUIManager.LoadPanel<StatusInfoPanel>(_resolver, null, false, false);
                _coreUIManager.RentPanel<StatusInfoPanel>(statusPanelId).OpenPanel();
            }
            catch (System.Exception e)
            {
                LogHelper.Error(LogHelper.GAME, $"[UI] StatusInfoPanel 로드 실패: {e.Message}");
            }

            try
            {
                await _coreUIManager.LoadPanel<Game.Encyclopedia.UI.UI_Encyclopedia>(_resolver, null, false, false);
            }
            catch (System.Exception e)
            {
                LogHelper.Error(LogHelper.GAME, $"[UI] UI_Encyclopedia 로드 실패: {e.Message}");
            }

            // 웨이브 시각화(2026-08-19 신규) — 화면 상단 웨이브 게이지, 항상 켜져 있어야 하므로
            // StatusInfoPanel과 동일하게 부팅 즉시 OpenPanel한다.
            try
            {
                int waveGaugePanelId = await _coreUIManager.LoadPanel<WaveGaugePanel>(_resolver, null, false, false);
                _coreUIManager.RentPanel<WaveGaugePanel>(waveGaugePanelId).OpenPanel();
            }
            catch (System.Exception e)
            {
                LogHelper.Error(LogHelper.GAME, $"[UI] WaveGaugePanel 로드 실패: {e.Message}");
            }

            // 건축물·자원·유닛 생산 MVP(2026-07-27) — 건물 클릭 시에만 뜨는 패널이라 평소엔 닫아 둔다.
            try
            {
                int buildingPanelId = await _coreUIManager.LoadPanel<BuildingControlPanel>(_resolver, null, false, false);
                _coreUIManager.RentPanel<BuildingControlPanel>(buildingPanelId).ClosePanel();
            }
            catch (System.Exception e)
            {
                LogHelper.Error(LogHelper.GAME, $"[UI] BuildingControlPanel 로드 실패: {e.Message}");
            }

            // UI 리뉴얼(2026-08-20) — 좌하단 하단 메뉴 바, WaveGaugePanel처럼 항상 켜져 있어야 한다.
            try
            {
                int bottomMenuPanelId = await _coreUIManager.LoadPanel<BottomMenuBar>(_resolver, null, false, false);
                _coreUIManager.RentPanel<BottomMenuBar>(bottomMenuPanelId).OpenPanel();
            }
            catch (System.Exception e)
            {
                LogHelper.Error(LogHelper.GAME, $"[UI] BottomMenuBar 로드 실패: {e.Message}");
            }

            // ESC 설정 패널(2026-08-26) — 평소엔 닫아 두고 InputManager가 ESC 입력 시 연다. 다른 모든
            // UGUI 패널 뒤에 로드해야(마지막 sibling) 열렸을 때 그 위를 덮는다.
            try
            {
                int settingsPanelId = await _coreUIManager.LoadPanel<GameSettingsPanel>(_resolver, null, false, false);
                _coreUIManager.RentPanel<GameSettingsPanel>(settingsPanelId).ClosePanel();
            }
            catch (System.Exception e)
            {
                LogHelper.Error(LogHelper.GAME, $"[UI] GameSettingsPanel 로드 실패: {e.Message}");
            }

            // 키 가이드 패널(2026-08-26, 사용자 요청 "타이틀과 esc에 키 가이드 항목 넣어줘") — 평소엔
            // 닫아 두고 GameSettingsPanel의 "키 가이드" 버튼이 연다. GameSettingsPanel보다도 나중에
            // 로드해야(마지막 sibling) 설정 패널 위에 뜬다.
            try
            {
                int keyGuidePanelId = await _coreUIManager.LoadPanel<KeyGuidePanel>(_resolver, null, false, false);
                _coreUIManager.RentPanel<KeyGuidePanel>(keyGuidePanelId).ClosePanel();
            }
            catch (System.Exception e)
            {
                LogHelper.Error(LogHelper.GAME, $"[UI] KeyGuidePanel 로드 실패: {e.Message}");
            }

            await FadeOut();
        }
        catch (System.Exception ex)
        {
            LogHelper.Error(LogHelper.GAME, $"[GameUIPresenter] BootSequence Exception: {ex}");
            try
            {
                await FadeOut();
            }
            catch { }
        }
    }
}
