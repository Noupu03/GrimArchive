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

    // 우측 상단은 보유 자원 표시만 남긴다 — 오펜스 현황(다음 웨이브 타이머/진행 상태)과 자원 사용
    // 안내(B/V/P 키 비용 설명)는 제거. GUIMenuStyleUtil의 어두운 배경 박스 + 흰 테두리 + 굵은 큰
    // 글씨로 통일. 합산 초당 증가량은 여기 안 넣는다 — BuildingControlPanel이 건물 클릭 시 표시한다.
    private const int PanelWidth = 300;
    private const int PanelY = 50;
    private const int PanelHeight = 90;

    private void OnGUI()
    {
        // 프리팹 UI가 깨졌거나 롤백되어 날아갔을 때를 대비해 OnGUI로 무조건 화면에 띄움
        Rect rect = new Rect(Screen.width - PanelWidth - 10, PanelY, PanelWidth, PanelHeight);
        GUIMenuStyleUtil.DrawPanelBox(rect);

        GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 8, rect.width - 24, rect.height - 16));

        if (ResourceManager.Instance != null)
        {
            GUILayout.Label($"보유 나무(Wood): {ResourceManager.Instance.GetResourceAmount(ResourceType.Wood)}", GUIMenuStyleUtil.LabelStyle);
            GUILayout.Label($"보유 돌(Stone): {ResourceManager.Instance.GetResourceAmount(ResourceType.Stone)}", GUIMenuStyleUtil.LabelStyle);
        }

        GUILayout.EndArea();
    }
}
