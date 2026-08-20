using UnityEditor;

public class SetupUIManager
{
    [MenuItem("Tools/Setup UIManager")]
    public static void Setup() => EmptyHaarePanelSetup.CreateAndRegister<UIManager>("UIManager");
}
