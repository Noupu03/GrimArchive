using UnityEditor;

public class SetupNoticeCenter
{
    [MenuItem("Tools/Setup NoticeCenter")]
    public static void Setup() => EmptyHaarePanelSetup.CreateAndRegister<NoticeCenter>("NoticeCenter");
}
