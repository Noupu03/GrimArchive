using UnityEngine;
using VContainer;
using VContainer.Unity;
using Haare.Client.Core.DI;
using GrimArchive.Wave;

// GameSession.Awake()가 예전에 직접 하던 AddComponent/FindObjectOfType 배선을 대체하는 DI 컴포지션
// 루트 — ssh.unity 씬에 이 컴포넌트가 붙은 GameObject가 하나 있어야 한다. CoreLifetimeScope를
// 상속하므로 DataManager/SceneService/CoreUIManager/GamePresenter는 부모가 등록해 여기선 다루지 않는다.
public class GameCompositionRoot : CoreLifetimeScope
{
    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Configure(IContainerBuilder builder)
    {
        base.Configure(builder);

        // 일반 Log는 콜스택이 5~7줄씩 붙어 콘솔이 지저분해지므로 Log만 스택 트레이스를 끈다
        // (Warning/Error는 원인 추적을 위해 유지).
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        // 이제는 GameSession.Awake()/Start()가 런타임에 직접 생성하던 것을 DI가 대체
        builder.RegisterComponentOnNewGameObject<InputManager>(Lifetime.Singleton, "InputManager");
        // UIManager/NoticeCenter — 다른 UI 패널처럼 [PanelAttribute] Haare ICustomPanel로 옮겨
        // GameUIPresenter.BootSequence가 생성을 담당한다(여기서 직접 배선하지 않음).

        // UnitGenerate: Update/OnGUI/인스펙터 데이터가 전혀 없는 순수 C# 클래스로 전환됨.
        builder.Register<UnitGenerate>(Lifetime.Singleton).AsSelf();

        // GameSession: 인스펙터 디버그 텍스처 뷰가 없어 씬 배치가 불필요한 NativeRoutine(순수 C#).
        builder.Register<GameSession>(Lifetime.Singleton).AsSelf().As<IOffenseQuery>();

        // UnitSpriteManager: 인스펙터 세팅 없이 항상 Resources.Load(Assets/Resources/Units/) 기반으로만
        // 동작하는 순수 C# 클래스.
        builder.Register<UnitSpriteManager>(Lifetime.Singleton).AsSelf();

        // VFXManager: Update/OnGUI/인스펙터 데이터가 전혀 없는 순수 이펙트 매니저라서 씬 배치가 불필요.
        builder.Register<VFXManager>(Lifetime.Singleton).AsSelf();

        // ThreatTileRenderer: Update/OnGUI/인스펙터 데이터 없는 순수 위협/시야 렌더러 — 씬 GameObject 불필요.
        builder.Register<ThreatTileRenderer>(Lifetime.Singleton).AsSelf();

        // PropagationDebugVisualizer: 07 소리/전파 시스템 임시 검증용 디버그 시각화(검증 끝나면 지워도
        // 됨) — ThreatTileRenderer와 동일한 순수 C# 클래스.
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

        // DoorSystem - split out of GameSession to keep it from growing further; resolves GameSession
        // lazily via IObjectResolver (same pattern as UnitRegistry).
        builder.Register<DoorSystem>(Lifetime.Singleton).AsSelf();

        // FogOfWarSystem - same pattern as DoorSystem (fog of war + torches bundled since torches
        // are timing-coupled to fog reveal).
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


