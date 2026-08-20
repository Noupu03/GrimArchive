using UnityEngine;
using Haare.Client.Routine;
using Haare.Client.UI;
using Cysharp.Threading.Tasks;
using GrimArchive.Wave;
using VContainer;

[PanelAttribute("Prefabs/StatusInfoPanel")]
public class StatusInfoPanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    private GameSession _gameSession;

    // 2026-08-20 — DebugInfoPanel/BuildingControlPanel과 동일한 [Inject] Construct 패턴으로 교체.
    // 예전엔 FindAnyObjectByType<GameCompositionRoot>().Container.Resolve<GameSession>()로 직접
    // 서비스 로케이터를 썼는데, GameUIPresenter가 이미 _resolver를 넘겨 LoadPanel하므로 다른 패널과
    // 동일하게 정상 주입받을 수 있다.
    [Inject]
    public void Construct(GameSession gameSession)
    {
        _gameSession = gameSession;
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        panel = gameObject;
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    public void BindEvent()
    {
    }

    // DebugInfoPanel의 우상단 UI(시야 표시 토글 y10~50, 선택 유닛 정보창)와 겹치지 않도록 이 박스는
    // y50부터 시작해서 충분한 높이(300)까지만 쓰고, DebugInfoPanel 쪽을 그 아래(y360~)로 내렸다
    // (사용자 요청 "UI 배치들 겹치지 않게 정리", 2026-07-23).
    private const int PanelWidth = 260;
    private const int PanelY = 50;
    private const int PanelHeight = 300;

    private void OnGUI()
    {
        // 프리팹 UI가 깨졌거나 롤백되어 날아갔을 때를 대비해 OnGUI로 무조건 화면에 띄움
        GUILayout.BeginArea(new Rect(Screen.width - PanelWidth - 10, PanelY, PanelWidth, PanelHeight), GUI.skin.box);
        GUILayout.Label("<size=14><b>[ 오팬스 현황 ]</b></size>");

        if (HumanWaveManager.Instance != null)
            GUILayout.Label($"다음 인류 웨이브: {HumanWaveManager.Instance.cooldownTimer:F1}초");

        if (ResourceManager.Instance != null)
        {
            GUILayout.Label($"보유 나무(Wood): {ResourceManager.Instance.GetResourceAmount(ResourceType.Wood)}");
            GUILayout.Label($"보유 돌(Stone): {ResourceManager.Instance.GetResourceAmount(ResourceType.Stone)}");
        }

        if (_gameSession != null && _gameSession.OffenseProcessor != null && _gameSession.OffenseProcessor.currentOffenseRoom != null)
        {
            GUI.color = Color.red;
            GUILayout.Label($"!!! 현재 오팬스 진행 중 !!!\n위치: {_gameSession.OffenseProcessor.currentOffenseRoom.RoomName}");
            GUI.color = Color.white;
        }

        GUILayout.Space(6);
        GUILayout.Label("<size=14><b>[ 자원 사용 안내 ]</b></size>");
        GUILayout.Label($"B키: 유닛 생산 건물 배치 (돌 {ResourceManager.UnitBuildingStoneCost}개 소모)");
        GUILayout.Label($"V키: 자원 생산 건물 배치 (돌 {ResourceManager.ResourceBuildingStoneCost}개 소모)");
        GUILayout.Label($"P키: 함정 배치 (돌 {ResourceManager.TrapPlaceStoneCost}개 소모)");
        GUILayout.Label("");

        GUILayout.EndArea();
    }
}
