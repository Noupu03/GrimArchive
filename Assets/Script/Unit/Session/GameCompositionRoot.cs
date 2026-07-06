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
        // 씬에 이미 배치되어 인스펙터 데이터를 들고 있는 서비스 (UnitManager, UnitSpriteManager 오브젝트)
        builder.RegisterComponentInHierarchy<GameSession>();
        builder.RegisterComponentInHierarchy<UnitSpriteManager>();
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

        // HumanKnowledgeBase: 대표 가중치 3종(이해도/위험도/흥미도) 전역 레지스트리 — 순수 C#.
        builder.Register<HumanKnowledgeBase>(Lifetime.Singleton).AsSelf();

        // Haare CoreUIManager: DebugInfoPanel 등 UGUI 패널을 담는 Canvas 루트.
        builder.RegisterComponentInNewPrefab(_coreUIManagerPrefab, Lifetime.Singleton)
            .DontDestroyOnLoad()
            .AsSelf();

        // Haare DataManager: NativeRoutine(순수 C# 클래스)이라 프리팹 없이 그냥 등록.
        builder.Register<DataManager>(Lifetime.Singleton).AsSelf();
    }
}
