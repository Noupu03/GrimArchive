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
    // 키 비용 설명)를 제거하고 보유 자원 표시만 남긴다.
    // 이후 사용자 요청(2026-08-20 "우상단의 자원 보유량도 글자 크기 좀 키워주고, 메뉴 스타일로") —
    // GUIMenuStyleUtil의 어두운 배경 박스 + 흰 테두리 + 굵은 큰 글씨로 통일, 그만큼 패널도 키웠다.
    // 합산 초당 증가량은 여기 안 넣는다(2026-08-24 사용자 요청 "우상단 UI에는 없애고, 건물 정보에서만
    // 보게 두자") — BuildingControlPanel이 건물 클릭 시 이미 표시한다.
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
