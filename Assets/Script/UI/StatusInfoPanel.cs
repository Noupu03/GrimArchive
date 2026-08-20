using UnityEngine;
using Haare.Client.Routine;
using Haare.Client.UI;

[PanelAttribute("Prefabs/StatusInfoPanel")]
public class StatusInfoPanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

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

    // UI 리뉴얼(2026-08-20, 사용자 요청 "우측 상단의 오펜스 현황, 자원 사용 안내 전부 지우고, 그냥
    // 보유 자원만 표시") — 오펜스 현황(다음 웨이브 타이머/오펜스 진행 상태)과 자원 사용 안내(B/V/P
    // 키 비용 설명)를 제거하고 보유 자원 표시만 남긴다. 패널 높이도 그만큼 줄였다.
    private const int PanelWidth = 220;
    private const int PanelY = 50;
    private const int PanelHeight = 70;

    private void OnGUI()
    {
        // 프리팹 UI가 깨졌거나 롤백되어 날아갔을 때를 대비해 OnGUI로 무조건 화면에 띄움
        GUILayout.BeginArea(new Rect(Screen.width - PanelWidth - 10, PanelY, PanelWidth, PanelHeight), GUI.skin.box);

        if (ResourceManager.Instance != null)
        {
            GUILayout.Label($"보유 나무(Wood): {ResourceManager.Instance.GetResourceAmount(ResourceType.Wood)}");
            GUILayout.Label($"보유 돌(Stone): {ResourceManager.Instance.GetResourceAmount(ResourceType.Stone)}");
        }

        GUILayout.EndArea();
    }
}
