using UnityEditor;

public class SetupStatusInfoPanel
{
    [MenuItem("Disposable/UI/빈 패널 프리팹/StatusInfoPanel")]
    public static void Setup() => EmptyHaarePanelSetup.CreateAndRegister<StatusInfoPanel>("StatusInfoPanel");
}
