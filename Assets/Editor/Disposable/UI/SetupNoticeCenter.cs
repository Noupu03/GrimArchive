using UnityEditor;

public class SetupNoticeCenter
{
    [MenuItem("Disposable/UI/빈 패널 프리팹/NoticeCenter")]
    public static void Setup() => EmptyHaarePanelSetup.CreateAndRegister<NoticeCenter>("NoticeCenter");
}
