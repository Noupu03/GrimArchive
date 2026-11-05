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

    // 우측 상단 보유 자원 표시(명세서 v0.2 2·3항): 내림 정수 표기 및 선점분 괄호 병기("1000 (-800)").
    private const int PanelWidth = 340;
    private const int PanelY = 50;
    private const int PanelHeight = 104;

    private void OnGUI()
    {
        Rect rect = new Rect(Screen.width - PanelWidth - 10, PanelY, PanelWidth, PanelHeight);
        GUIMenuStyleUtil.DrawPanelBox(rect);

        GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 8, rect.width - 24, rect.height - 16));

        ResourceManager rm = ResourceManager.Instance;
        if (rm != null)
        {
            GUILayout.Label($"생명 에너지: {rm.FormatResourceDisplay(ResourceType.LifeEnergy)}", GUIMenuStyleUtil.LabelStyle);
            GUILayout.Label($"영혼 파편: {rm.FormatResourceDisplay(ResourceType.SoulShard)}", GUIMenuStyleUtil.LabelStyle);
            if (!rm.IsSoulShardSpendUnlocked)
                GUILayout.Label("※ 첫 코어 완전 활성화 후 영혼 파편 사용 해금", GUIMenuStyleUtil.BodyLabelStyle);
        }

        GUILayout.EndArea();
    }
}
