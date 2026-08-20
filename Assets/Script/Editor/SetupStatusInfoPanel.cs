using UnityEditor;

public class SetupStatusInfoPanel
{
    [MenuItem("Tools/Setup StatusInfoPanel")]
    public static void Setup() => EmptyHaarePanelSetup.CreateAndRegister<StatusInfoPanel>("StatusInfoPanel");
}
