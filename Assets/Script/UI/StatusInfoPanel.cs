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
}
