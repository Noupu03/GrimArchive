using UnityEditor;

public class SetupUIManager
{
    [MenuItem("Disposable/UI/빈 패널 프리팹/UIManager")]
    public static void Setup() => EmptyHaarePanelSetup.CreateAndRegister<UIManager>("UIManager");
}
