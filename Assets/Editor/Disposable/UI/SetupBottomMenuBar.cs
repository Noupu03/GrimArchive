using UnityEditor;

public class SetupBottomMenuBar
{
    [MenuItem("Disposable/UI/빈 패널 프리팹/BottomMenuBar")]
    public static void Setup() => EmptyHaarePanelSetup.CreateAndRegister<BottomMenuBar>("BottomMenuBar");
}
