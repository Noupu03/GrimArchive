using UnityEngine;
using Haare.Client.Routine;
using Haare.Client.UI;
using Cysharp.Threading.Tasks;
using GrimArchive.Wave;

[PanelAttribute("Prefabs/StatusInfoPanel")]
public class StatusInfoPanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    [SerializeField] private TMPro.TextMeshProUGUI waveTimerText;
    [SerializeField] private TMPro.TextMeshProUGUI resourceWoodText;
    [SerializeField] private TMPro.TextMeshProUGUI resourceStoneText;
    [SerializeField] private TMPro.TextMeshProUGUI resourceGoldText;
    [SerializeField] private TMPro.TextMeshProUGUI resourceBText;

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

    private void Update()
    {
        if (HumanWaveManager.Instance != null && waveTimerText != null)
        {
            waveTimerText.text = $"다음 인류 웨이브: {HumanWaveManager.Instance.cooldownTimer:F1}초";
        }

        if (ResourceManager.Instance != null)
        {
            if (resourceWoodText != null) resourceWoodText.text = $"Wood: {ResourceManager.Instance.GetResourceAmount(ResourceType.Wood)}";
            if (resourceStoneText != null) resourceStoneText.text = $"Stone: {ResourceManager.Instance.GetResourceAmount(ResourceType.Stone)}";
            if (resourceGoldText != null) resourceGoldText.text = $"Gold: {ResourceManager.Instance.GetResourceAmount(ResourceType.Gold)}";
        }

        if (ResourceAccumulator.Instance != null && resourceBText != null)
        {
            resourceBText.text = $"임시 누적 자원(B): {ResourceAccumulator.Instance.AccumulatedResourceB}";
        }
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
        GUILayout.Label("<size=14><b>[ 오팬스 현황 (자동 복구 UI) ]</b></size>");

        if (HumanWaveManager.Instance != null)
            GUILayout.Label($"다음 인류 웨이브: {HumanWaveManager.Instance.cooldownTimer:F1}초");

        if (ResourceManager.Instance != null)
        {
            GUILayout.Label($"보유 나무(Wood): {ResourceManager.Instance.GetResourceAmount(ResourceType.Wood)}");
            GUILayout.Label($"보유 돌(Stone): {ResourceManager.Instance.GetResourceAmount(ResourceType.Stone)}");
            GUI.color = Color.cyan;
            GUILayout.Label($"오펜스 획득 보상(Resource B): {ResourceManager.Instance.GetResourceAmount(ResourceType.OffenseReward)}");
            GUI.color = Color.white;
        }

        if (ResourceAccumulator.Instance != null)
        {
            GUI.color = Color.yellow;
            GUILayout.Label($"오팬스 임시 누적 보상: {ResourceAccumulator.Instance.AccumulatedResourceB}");
            GUI.color = Color.white;
        }

        if (OffenseProcessor.Instance != null && OffenseProcessor.Instance.currentOffenseRoom != null)
        {
            GUI.color = Color.red;
            GUILayout.Label($"!!! 현재 오팬스 진행 중 !!!\n위치: {OffenseProcessor.Instance.currentOffenseRoom.RoomName}");
            GUI.color = Color.white;
        }

        GUILayout.Space(6);
        GUILayout.Label("<size=14><b>[ 자원 사용 안내 ]</b></size>");
        GUILayout.Label($"M키: 몬스터 배치 (나무 {ResourceManager.MonsterPlaceWoodCost}개 소모)");
        GUILayout.Label($"P키: 함정 배치 (돌 {ResourceManager.TrapPlaceStoneCost}개 소모)");
        GUILayout.Label("O키: 루팅 오브젝트 배치 (무료)");

        GUILayout.EndArea();
    }
}
