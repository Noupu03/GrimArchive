using UnityEditor;

public class SetupWaveGaugePanel
{
    [MenuItem("Disposable/UI/빈 패널 프리팹/WaveGaugePanel")]
    public static void Setup() => EmptyHaarePanelSetup.CreateAndRegister<WaveGaugePanel>("WaveGaugePanel");
}
