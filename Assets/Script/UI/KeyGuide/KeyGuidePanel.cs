using System;
using Haare.Client.Routine;
using Haare.Client.UI;
using R3;
using UnityEngine;

// 키 가이드 패널 — Title.unity(GameTitlePanel)와 ssh.unity(GameSettingsPanel)가 공유하는 단일 패널.
// 키 목록은 GameInputScheme.cs를 그대로 옮긴 것이라 입력이 바뀌면 텍스트도 갱신해야 한다
// (KeyGuidePanelSetup.cs가 문구 생성). 호출부가 자기 화면을 숨기고 이 패널을 띄운 뒤 닫히면 다시
// 보여주는 패턴을 쓰며, 이 패널 자신은 누가 열었는지 모르고 Open(onClosed) 콜백만 닫을 때 호출한다.
[PanelAttribute("Prefabs/GrimArchive_KeyGuidePanel")]
public class KeyGuidePanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    public static KeyGuidePanel Instance { get; private set; }

    [SerializeField] public CustomButton CloseButton;

    private Action _onClosed;

    protected override void Constructor()
    {
        base.Constructor();
        Instance = this;
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        panel = gameObject;
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    public void BindEvent()
    {
        if (CloseButton != null)
            CloseButton.Onclicked.AsObservable().Subscribe(_ => Close());
    }

    public void Open(Action onClosed = null)
    {
        _onClosed = onClosed;
        OpenPanel();
    }

    public void Close()
    {
        ClosePanel();
        var callback = _onClosed;
        _onClosed = null;
        callback?.Invoke();
    }
}
