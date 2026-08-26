using System;
using Haare.Client.Routine;
using Haare.Client.UI;
using R3;
using UnityEngine;

// 키 가이드 패널(2026-08-26, 사용자 요청 "타이틀과 esc에 키 가이드 항목 넣어줘. 실제 사용 키에 대한
// 가이드 설명 패널 뜨게 해주고" — rythoom 프로젝트의 PauseMenu "키 가이드" 버튼/KeyGuidePanel 참고).
// Title.unity(GameTitlePanel)와 ssh.unity(GameSettingsPanel) 양쪽에서 공유하는 단일 패널이다 — 실제
// 사용 키 목록은 이 게임의 유일한 입력 표면인 GameInputScheme.cs(Assets/Script/Unit/Session/)를 그대로
// 옮겨적은 것이라, 입력이 바뀌면 이 패널의 텍스트도 같이 갱신해야 한다(Assets/Editor/
// KeyGuidePanelSetup.cs가 실제 문구를 생성).
//
// 호출부가 "자기 화면을 잠깐 숨기고 이 패널을 띄운 뒤, 닫히면 다시 보여주는" 동일한 패턴을 쓴다
// (TitlePresenter.OpenKeyGuide / GameSettingsPanel.OpenKeyGuide 참고) — 그래서 이 패널 자신은 누가
// 열었는지 모르고, Open(onClosed)로 받은 콜백 하나만 닫을 때 호출한다.
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
