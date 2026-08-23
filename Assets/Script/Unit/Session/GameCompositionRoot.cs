using UnityEngine;
using VContainer;
using VContainer.Unity;
using Haare.Client.Core.DI;
using GrimArchive.Wave;

// GameSession.Awake()가 예전엔 직접 하던 AddComponent/FindObjectOfType 배선을 대체하는 DI 컴포지션 루트.
// ssh.unity 씬 이 컴포넌트가 붙은 GameObject(예: "CompositionRoot")가 하나 있어야 한다.
//
// CoreLifetimeScope를 상속한다 — DataManager/SceneService/CoreUIManager(+ SceneUIManager)/GamePresenter는
// 부모(CoreLifetimeScope.Configure)가 등록해주므로 여기서 다시 등록하지 않는다. CoreUIManager 프리팹도
// 부모의 protected _coreUIManagerPrefab 필드에 그대로 배선됨(Assets/Editor/HaareUISetup.cs가 배선).
public class GameCompositionRoot : CoreLifetimeScope
{
    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Configure(IContainerBuilder builder)
    {
        base.Configure(builder);

        // 일반 Log는 다른 로그와 달리 콜스택이 5~7줄씩 따라붙어서 콘솔이 지저분해지므로
        // (LogHelper.Log(...) 호출부 스택까지는 필요 없음). Warning/Error는 원인 추적에 필요하니
        // 그대로 두고, Log만 스택 트레이스를 끈다.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        // 이제는 GameSession.Awake()/Start()가 런타임에 직접 생성하던 것을 DI가 대체
        builder.RegisterComponentOnNewGameObject<InputManager>(Lifetime.Singleton, "InputManager");
        // UIManager/NoticeCenter (2026-08-20 UI 리팩토링, "모든 UI Haare 프레임워크에 편입") — 예전엔
        // 여기서 RegisterComponentOnNewGameObject로 직접 배선했는데, 다른 UI 패널들과 동일하게
        // [PanelAttribute] Haare ICustomPanel로 옮겨서 GameUIPresenter.BootSequence가 생성을 담당한다
        // (Assets/Script/Unit/Session/UIManager.cs, Assets/Script/UI/NoticeCenter.cs 참고).

        // UnitGenerate: Update/OnGUI/인스펙터 데이터가 전혀 없는 순수 C# 클래스로 전환됨.
        builder.Register<UnitGenerate>(Lifetime.Singleton).AsSelf();

        // GameSession: 인스펙터 디버그 텍스처 뷰 제거 후 씬 배치가 불필요해져서 NativeRoutine(순수 C#)으로 전환됨.
        builder.Register<GameSession>(Lifetime.Singleton).AsSelf().As<IOffenseQuery>();

        // UnitSpriteManager: 인스펙터 세팅 없이 항상 Resources.Load(Assets/Resources/Units/) 기반
        // 정적으로만 동작되어 순수 C# 클래스로 됨.
        builder.Register<UnitSpriteManager>(Lifetime.Singleton).AsSelf();

        // VFXManager: Update/OnGUI/인스펙터 데이터가 전혀 없는 순수 이펙트 매니저라서 씬 배치가 불필요.
        builder.Register<VFXManager>(Lifetime.Singleton).AsSelf();

        // ThreatTileRenderer: Update/OnGUI/인스펙터 데이터가 전혀 없는 순수 위협/시야 렌더러라서
        // 씬 GameObject가 필요가 없는 순수 C# 클래스로 전환됨.
        builder.Register<ThreatTileRenderer>(Lifetime.Singleton).AsSelf();

        // PropagationDebugVisualizer: 07 소리/전파 시스템 임시 검증용 디버그 시각화 — ThreatTileRenderer와
        // 동일하게 GameObject 불필요한 순수 C# 클래스. 검증 끝나면 이 등록 줄만 지워도 됨.
        builder.Register<PropagationDebugVisualizer>(Lifetime.Singleton).AsSelf();

        // 맵 데이터를 생성/관리하고 런타임에 활용하는 순수 C# 클래스
        builder.Register<CreateMap>(Lifetime.Singleton).AsSelf();

        // 유닛 그리드 관리 (분리됨)
        builder.Register<UnitRegistry>(Lifetime.Singleton).AsSelf();

        // 오브젝트 스포너 (분리됨)
        builder.Register<ObjectSpawner>(Lifetime.Singleton).AsSelf();

        // 파티 서비스 (분리됨)
        builder.Register<PartyService>(Lifetime.Singleton).AsSelf();

        // 전투 이벤트 서비스 (분리됨)
        builder.Register<CombatEventService>(Lifetime.Singleton).AsSelf();

        // DoorSystem (2026-08-20, split out of GameSession to keep it from growing further) - lazily
        // resolves GameSession via IObjectResolver, same pattern as UnitRegistry.
        builder.Register<DoorSystem>(Lifetime.Singleton).AsSelf();

        // FogOfWarSystem (2026-08-20, same reason/pattern as DoorSystem - fog of war + torches, torches
        // are bundled in because they're timing-coupled to fog reveal).
        builder.Register<FogOfWarSystem>(Lifetime.Singleton).AsSelf();

        // 맵 렌더링 담당 - MonoBehaviour에서 NativeRoutine으로 전환됨
        builder.Register<MapRandering>(Lifetime.Singleton).AsSelf().As<IMapColorizer>();

        // 공방 프로세서 (DI)
        builder.Register<OffenseProcessor>(Lifetime.Singleton).AsSelf();
        builder.Register<DefenseProcessor>(Lifetime.Singleton).AsSelf();

        // 웨이브 스포너 - MonoBehaviour에서 NativeRoutine으로 전환됨
        builder.Register<WaveSpawner>(Lifetime.Singleton).AsSelf();

        // 인류 웨이브 오케스트레이터 - Haare Framework NativeRoutine
        builder.Register<HumanWaveManager>(Lifetime.Singleton).AsSelf();

        // 맵 매니저 (MapRandering과 WaveSpawner를 조율하여 런타임 이벤트 연결)
        builder.Register<MapManager>(Lifetime.Singleton).AsSelf();

        // 중앙 자원 관리자 - NativeRoutine (UniTask 임시 채굴량 계산)
        builder.Register<ResourceManager>(Lifetime.Singleton).AsSelf();

        // 건물 관리자 - NativeRoutine (1타일 1오브젝트 룰 검증/배치 담당)
        builder.Register<BuildingManager>(Lifetime.Singleton).AsSelf();

        // HumanKnowledgeBase: 대표 가중치 3종(이해도/위험도/흥미도) 전역 레지스트리 — 순수 C#.
        builder.Register<HumanKnowledgeBase>(Lifetime.Singleton).AsSelf();

        // GameUIPresenter: Haare의 UIPresenter/RegisterEntryPoint 경로를 따르는 진입 지점.
        // 부팅 시 LoadingFadePanel로 페이드하며 DebugInfoPanel/도감 패널을 로드(UIManager.Start()가 하던 일).
        builder.RegisterEntryPoint<GameUIPresenter>();
        builder.RegisterEntryPoint<GameBootstrapper>();
    }

}


