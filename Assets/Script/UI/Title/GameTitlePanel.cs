using Haare.Client.Routine;
using Haare.Client.UI;
using UnityEngine;

// RealBioSearch(외부 참고 프로젝트)의 타이틀 씬 구조(배경+제목+시작/설정/종료 버튼+버전 표기)를
// 이 프로젝트의 Haare DI 패턴(Assets/Haare/Demo/Script/UI/TitlePanel.cs와 동일한 스캐폴딩)으로 이식한
// 패널. 실제 배경/텍스트/버튼 배치는 Assets/Editor/TitleSceneSetup.cs가 코드로 생성한 프리팹
// (Prefabs/GrimArchive_TitlePanel)에 있다.
[PanelAttribute("Prefabs/GrimArchive_TitlePanel")]
public class GameTitlePanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    [SerializeField] public CustomButton StartButton;
    [SerializeField] public CustomButton KeyGuideButton;
    [SerializeField] public CustomButton SettingsButton;
    [SerializeField] public CustomButton QuitButton;

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        panel = gameObject;
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    // 버튼 클릭 로직은 TitlePresenter가 소유한다(Onclicked 구독) — 이 패널은 시각적 표시/상태만 담당.
    public void BindEvent()
    {
    }
}
