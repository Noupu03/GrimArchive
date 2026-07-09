using UnityEngine;
using VContainer;
using VContainer.Unity;
using Haare.Client.UI;
using Haare.Scripts.Client.Data;

// GameSession.Awake()가 수동으로 하던 AddComponent/FindObjectOfType 배선을 대체하는 DI 컴포지션 루트.
// ssh.unity에 이 컴포넌트가 붙은 GameObject(예: "CompositionRoot")가 하나 있어야 한다.
public class GameCompositionRoot : LifetimeScope
{
    // Assets/Editor/HaareUISetup.cs("Tools/GrimArchive/Haare UI 셋업 생성")가 생성한 CoreCanvas 프리팹.
    [SerializeField] private CoreUIManager _coreUIManagerPrefab;

    protected override void Configure(IContainerBuilder builder)
    {
        // 일반 Log는 한 줄마다 콜스택이 5~7줄씩 따라붙어서 콘솔이 순식간에 복잡해진다
        // (LogHelper.Log(...) 호출부 스택까지 전부 찍힘). Warning/Error는 실제 디버깅에 필요하니
        // 그대로 두고, 정보성 Log만 스택 트레이스를 끈다.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        // 씬에 이미 배치되어 인스펙터 데이터를 들고 있는 서비스
        builder.RegisterComponentInHierarchy<VFXManager>();

        // 기존에 GameSession.Awake()/Start()가 런타임에 동적 생성하던 서비스
        // ThreatTileRenderer는 이제 Resources.Load로 스프라이트 라이브러리를 가져오므로
        // 인스펙터 할당이 필요 없어 다시 동적 생성으로 되돌렸다.
        builder.RegisterComponentOnNewGameObject<InputManager>(Lifetime.Singleton, "InputManager");
        builder.RegisterComponentOnNewGameObject<ThreatTileRenderer>(Lifetime.Singleton, "ThreatTileRenderer");
        // UIManager는 OnGUI() 때문에 MonoBehaviour는 유지하지만 인스펙터 데이터가 없어 씬 배치가 불필요.
        builder.RegisterComponentOnNewGameObject<UIManager>(Lifetime.Singleton, "UIManager");

        // UnitGenerate: Update/OnGUI/인스펙터 데이터가 전혀 없는 순수 C# 클래스로 전환됨.
        builder.Register<UnitGenerate>(Lifetime.Singleton).AsSelf();

        // GameSession: 인스펙터 디버그 텍스처 뷰 제거 후 씬 배치가 불필요해져 NativeRoutine(순수 C#)으로 전환됨.
        builder.Register<GameSession>(Lifetime.Singleton).AsSelf();

        // UnitSpriteManager: 인스펙터 프리팹 매핑 대신 Resources.Load(Assets/Resources/Units/) 경로
        // 컨벤션으로 전환되어 순수 C# 클래스가 됨.
        builder.Register<UnitSpriteManager>(Lifetime.Singleton).AsSelf();

        // HumanKnowledgeBase: 대표 가중치 3종(이해도/위험도/흥미도) 전역 레지스트리 — 순수 C#.
        builder.Register<HumanKnowledgeBase>(Lifetime.Singleton).AsSelf();

        // Haare CoreUIManager: DebugInfoPanel 등 UGUI 패널을 담는 Canvas 루트.
        builder.RegisterComponentInNewPrefab(_coreUIManagerPrefab, Lifetime.Singleton)
            .DontDestroyOnLoad()
            .AsSelf();

        // Haare DataManager: NativeRoutine(순수 C# 클래스)이라 프리팹 없이 그냥 등록.
        builder.Register<DataManager>(Lifetime.Singleton).AsSelf();
    }

    private void Start()
    {
        // 입력 이벤트를 게임 시작 직후부터 받기 위해 InputManager만 미리 생성합니다.
        // UIManager와 UI 에셋들은 호출되기 전까지 계속 생성되지 않습니다 (지연 로딩).
        Container.Resolve<InputManager>();
    }
}
