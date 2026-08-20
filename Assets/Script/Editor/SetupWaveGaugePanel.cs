using UnityEditor;

public class SetupWaveGaugePanel
{
    [MenuItem("Tools/Setup WaveGaugePanel")]
    public static void Setup() => EmptyHaarePanelSetup.CreateAndRegister<WaveGaugePanel>("WaveGaugePanel");
}
