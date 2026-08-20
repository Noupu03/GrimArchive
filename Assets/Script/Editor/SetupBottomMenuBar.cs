using UnityEditor;

public class SetupBottomMenuBar
{
    [MenuItem("Tools/Setup BottomMenuBar")]
    public static void Setup() => EmptyHaarePanelSetup.CreateAndRegister<BottomMenuBar>("BottomMenuBar");
}
