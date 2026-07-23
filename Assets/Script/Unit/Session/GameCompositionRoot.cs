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
        // UIManager�� OnGUI() ������ MonoBehaviour�� ���������� �ν����� �����Ͱ� ���� �� ��ġ�� ���ʿ�.
        builder.RegisterComponentOnNewGameObject<UIManager>(Lifetime.Singleton, "UIManager");

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

        // ����� �Է� �ڵ鷯 (�и���)
        builder.Register<DebugInputHandler>(Lifetime.Singleton).AsSelf();

        // �� ������ ���� - MonoBehaviour���� NativeRoutine���� ��ȯ
        builder.Register<MapRandering>(Lifetime.Singleton).AsSelf().As<IMapColorizer>();

        // ���潺 ���μ��� (DI)
        builder.Register<OffenseProcessor>(Lifetime.Singleton).AsSelf();

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


