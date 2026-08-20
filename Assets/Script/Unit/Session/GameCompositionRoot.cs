using UnityEngine;
using VContainer;
using VContainer.Unity;
using Haare.Client.Core.DI;
using GrimArchive.Wave;

// GameSession.Awake()�� �������� �ϴ� AddComponent/FindObjectOfType �輱�� ��ü�ϴ� DI �������� ��Ʈ.
// ssh.unity�� �� ������Ʈ�� ���� GameObject(��: "CompositionRoot")�� �ϳ� �־�� �Ѵ�.
//
// CoreLifetimeScope�� ����Ѵ� ? DataManager/SceneService/CoreUIManager(+ SceneUIManager)/GamePresenter��
// �θ�(CoreLifetimeScope.Configure)�� ������ֹǷ� ���⼭ �ٽ� ������� �ʴ´�. CoreUIManager ��������
// �θ��� protected _coreUIManagerPrefab �ʵ忡 �״�� ������(Assets/Editor/HaareUISetup.cs�� �輱).
public class GameCompositionRoot : CoreLifetimeScope
{
    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Configure(IContainerBuilder builder)
    {
        base.Configure(builder);

        // �Ϲ� Log�� �� �ٸ��� �ݽ����� 5~7�پ� ����پ �ܼ��� ���İ��� ����������
        // (LogHelper.Log(...) ȣ��� ���ñ��� ���� ����). Warning/Error�� ���� ����뿡 �ʿ��ϴ�
        // �״�� �ΰ�, ������ Log�� ���� Ʈ���̽��� ����.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        // ������ GameSession.Awake()/Start()�� ��Ÿ�ӿ� ���� �����ϴ� ����
        builder.RegisterComponentOnNewGameObject<InputManager>(Lifetime.Singleton, "InputManager");
        // UIManager/NoticeCenter (2026-08-20 UI 리팩토링, "모든 UI Haare 프레임워크에 편입") — 예전엔
        // 여기서 RegisterComponentOnNewGameObject로 직접 배선했는데, 다른 UI 패널들과 동일하게
        // [PanelAttribute] Haare ICustomPanel로 옮겨서 GameUIPresenter.BootSequence가 생성을 담당한다
        // (Assets/Script/Unit/Session/UIManager.cs, Assets/Script/UI/NoticeCenter.cs 참고).

        // UnitGenerate: Update/OnGUI/�ν����� �����Ͱ� ���� ���� ���� C# Ŭ������ ��ȯ��.
        builder.Register<UnitGenerate>(Lifetime.Singleton).AsSelf();

        // GameSession: �ν����� ����� �ؽ�ó �� ���� �� �� ��ġ�� ���ʿ����� NativeRoutine(���� C#)���� ��ȯ��.
        builder.Register<GameSession>(Lifetime.Singleton).AsSelf().As<IOffenseQuery>();

        // UnitSpriteManager: �ν����� ������ ���� ��� Resources.Load(Assets/Resources/Units/) ���
        // ���������� ��ȯ�Ǿ� ���� C# Ŭ������ ��.
        builder.Register<UnitSpriteManager>(Lifetime.Singleton).AsSelf();

        // VFXManager: Update/OnGUI/�ν����� �����Ͱ� ���� ���� ���� ���� ��Ŀ�����̶� �� ��ġ�� ���ʿ�.
        builder.Register<VFXManager>(Lifetime.Singleton).AsSelf();

        // ThreatTileRenderer: Update/OnGUI/�ν����� �����Ͱ� ���� ���� ���� ����/���� ��Ŀ�����̶�
        // �� GameObject�� �ʿ䰡 ���� ���� C# Ŭ������ ��ȯ��.
        builder.Register<ThreatTileRenderer>(Lifetime.Singleton).AsSelf();

        // PropagationDebugVisualizer: 07 소리/전파 시스템 임시 검증용 디버그 시각화 — ThreatTileRenderer와
        // 동일하게 GameObject 불필요한 순수 C# 클래스. 검증 끝나면 이 등록 줄만 지워도 됨.
        builder.Register<PropagationDebugVisualizer>(Lifetime.Singleton).AsSelf();

        // �� �����͸� ����/�����ϰ� ��Ÿ�ӿ� Ȱ���ϴ� ���� C# Ŭ����
        builder.Register<CreateMap>(Lifetime.Singleton).AsSelf();

        // ���� �׸��� ���� (�и���)
        builder.Register<UnitRegistry>(Lifetime.Singleton).AsSelf();

        // ������Ʈ ������ (�и���)
        builder.Register<ObjectSpawner>(Lifetime.Singleton).AsSelf();

        // ��Ƽ ���� (�и���)
        builder.Register<PartyService>(Lifetime.Singleton).AsSelf();

        // ���� �̺�Ʈ ���� (�и���)
        builder.Register<CombatEventService>(Lifetime.Singleton).AsSelf();

        // DoorSystem (2026-08-20, split out of GameSession to keep it from growing further) - lazily
        // resolves GameSession via IObjectResolver, same pattern as UnitRegistry.
        builder.Register<DoorSystem>(Lifetime.Singleton).AsSelf();

        // FogOfWarSystem (2026-08-20, same reason/pattern as DoorSystem - fog of war + torches, torches
        // are bundled in because they're timing-coupled to fog reveal).
        builder.Register<FogOfWarSystem>(Lifetime.Singleton).AsSelf();

        // �� ������ ���� - MonoBehaviour���� NativeRoutine���� ��ȯ
        builder.Register<MapRandering>(Lifetime.Singleton).AsSelf().As<IMapColorizer>();

        // ���潺 ���μ��� (DI)
        builder.Register<OffenseProcessor>(Lifetime.Singleton).AsSelf();
        builder.Register<DefenseProcessor>(Lifetime.Singleton).AsSelf();

        // ���̺� ������ - MonoBehaviour���� NativeRoutine���� ��ȯ
        builder.Register<WaveSpawner>(Lifetime.Singleton).AsSelf();

        // �η� ���̺� ���ɽ�Ʈ������ - Haare Framework NativeRoutine
        builder.Register<HumanWaveManager>(Lifetime.Singleton).AsSelf();

        // �� �Ŵ��� (MapRandering�� WaveSpawner�� �����Ͽ� ��Ÿ�� �̺�Ʈ ����)
        builder.Register<MapManager>(Lifetime.Singleton).AsSelf();

        // �߾� �ڿ� ������ - NativeRoutine (UniTask �ӽ� ä���� ����)
        builder.Register<ResourceManager>(Lifetime.Singleton).AsSelf();

        // ���๰ ������ - NativeRoutine (1Ÿ�� 1������Ʈ �� ������ ����ȭ ���)
        builder.Register<BuildingManager>(Lifetime.Singleton).AsSelf();

        // HumanKnowledgeBase: ��ǥ ����ġ 3��(���ص�/���赵/��̵�) ���� ������Ʈ�� ? ���� C#.
        builder.Register<HumanKnowledgeBase>(Lifetime.Singleton).AsSelf();

        // GameUIPresenter: Haare�� UIPresenter/RegisterEntryPoint ��θ� ������ Ÿ�� ������.
        // ���� �� LoadingFadePanel�� ���̵��ϸ� DebugInfoPanel/���� �г��� ����(UIManager.Start()�� �ϴ� ��).
        builder.RegisterEntryPoint<GameUIPresenter>();
        builder.RegisterEntryPoint<GameBootstrapper>();
    }

}


