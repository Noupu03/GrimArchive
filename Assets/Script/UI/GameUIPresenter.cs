using Cysharp.Threading.Tasks;
using Haare.Client.Core.DI;
using UnityEngine;

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

            int debugPanelId = await _coreUIManager.LoadPanel<DebugInfoPanel>(_resolver, null, false, false);
            _coreUIManager.RentPanel<DebugInfoPanel>(debugPanelId).OpenPanel();

            try
            {
                int statusPanelId = await _coreUIManager.LoadPanel<StatusInfoPanel>(_resolver, null, false, false);
                _coreUIManager.RentPanel<StatusInfoPanel>(statusPanelId).OpenPanel();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UI] StatusInfoPanel 로드 실패: {e.Message}");
            }

            try
            {
                await _coreUIManager.LoadPanel<Game.Encyclopedia.UI.UI_Encyclopedia>(_resolver, null, false, false);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UI] UI_Encyclopedia 로드 실패: {e.Message}");
            }

            await FadeOut();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameUIPresenter] BootSequence Exception: {ex}");
            try
            {
                await FadeOut();
            }
            catch { }
        }
    }
}
