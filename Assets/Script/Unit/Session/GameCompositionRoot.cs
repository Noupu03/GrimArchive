using UnityEngine;
using VContainer;
using VContainer.Unity;
using Haare.Client.Core.DI;
using GrimArchive.Wave;

// GameSession.Awake()가 수동으로 하던 AddComponent/FindObjectOfType 배선을 대체하는 DI 컴포지션 루트.
// ssh.unity에 이 컴포넌트가 붙은 GameObject(예: "CompositionRoot")가 하나 있어야 한다.
//
// CoreLifetimeScope를 상속한다 — DataManager/SceneService/CoreUIManager(+ SceneUIManager)/GamePresenter는
// 부모(CoreLifetimeScope.Configure)가 등록해주므로 여기서 다시 등록하지 않는다. CoreUIManager 프리팹은
// 부모의 protected _coreUIManagerPrefab 필드에 그대로 물린다(Assets/Editor/HaareUISetup.cs가 배선).
public class GameCompositionRoot : CoreLifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        base.Configure(builder);

        // 일반 Log는 한 줄마다 콜스택이 5~7줄씩 따라붙어서 콘솔이 순식간에 복잡해진다
        // (LogHelper.Log(...) 호출부 스택까지 전부 찍힘). Warning/Error는 실제 디버깅에 필요하니
        // 그대로 두고, 정보성 Log만 스택 트레이스를 끈다.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        // 기존에 GameSession.Awake()/Start()가 런타임에 동적 생성하던 서비스
        builder.RegisterComponentOnNewGameObject<InputManager>(Lifetime.Singleton, "InputManager");
        // UIManager는 OnGUI() 때문에 MonoBehaviour는 유지하지만 인스펙터 데이터가 없어 씬 배치가 불필요.
        builder.RegisterComponentOnNewGameObject<UIManager>(Lifetime.Singleton, "UIManager");

        // UnitGenerate: Update/OnGUI/인스펙터 데이터가 전혀 없는 순수 C# 클래스로 전환됨.
        builder.Register<UnitGenerate>(Lifetime.Singleton).AsSelf();

        // GameSession: 인스펙터 디버그 텍스처 뷰 제거 후 씬 배치가 불필요해져 NativeRoutine(순수 C#)으로 전환됨.
        builder.Register<GameSession>(Lifetime.Singleton).AsSelf();

        // UnitSpriteManager: 인스펙터 프리팹 매핑 대신 Resources.Load(Assets/Resources/Units/) 경로
        // 컨벤션으로 전환되어 순수 C# 클래스가 됨.
        builder.Register<UnitSpriteManager>(Lifetime.Singleton).AsSelf();

        // VFXManager: Update/OnGUI/인스펙터 데이터가 전혀 없는 순수 스폰 메커니즘이라 씬 배치가 불필요.
        builder.Register<VFXManager>(Lifetime.Singleton).AsSelf();

        // ThreatTileRenderer: Update/OnGUI/인스펙터 데이터가 전혀 없는 순수 스폰/렌더 메커니즘이라
        // 씬 GameObject일 필요가 없는 순수 C# 클래스로 전환됨.
        builder.Register<ThreatTileRenderer>(Lifetime.Singleton).AsSelf();

        // 맵 데이터를 저장/관리하고 런타임에 활용하는 순수 C# 클래스
        builder.Register<CreateMap>(Lifetime.Singleton).AsSelf();

        // 맵 렌더링 로직 - MonoBehaviour에서 NativeRoutine으로 전환
        builder.Register<MapRandering>(Lifetime.Singleton).AsSelf();

        // 웨이브 스포너 - MonoBehaviour에서 NativeRoutine으로 전환
        builder.Register<WaveSpawner>(Lifetime.Singleton).AsSelf();

        // 맵 매니저 (MapRandering과 WaveSpawner를 포함하여 런타임 이벤트 제공)
        builder.Register<MapManager>(Lifetime.Singleton).AsSelf();

        // HumanKnowledgeBase: 대표 가중치 3종(이해도/위험도/흥미도) 전역 레지스트리 — 순수 C#.
        builder.Register<HumanKnowledgeBase>(Lifetime.Singleton).AsSelf();

        // GameUIPresenter: Haare의 UIPresenter/RegisterEntryPoint 경로를 실제로 타는 진입점.
        // 부팅 시 LoadingFadePanel로 페이드하며 DebugInfoPanel/도감 패널을 띄운다(UIManager.Start()가 하던 일).
        builder.RegisterEntryPoint<GameUIPresenter>();
    }

    private void Start()
    {
        // 입력 이벤트를 게임 시작 직후부터 받기 위해 InputManager만 미리 생성합니다.
        // UIManager와 UI 에셋들은 호출되기 전까지 계속 생성되지 않습니다 (지연 로딩).
        Container.Resolve<InputManager>();
    }
}
