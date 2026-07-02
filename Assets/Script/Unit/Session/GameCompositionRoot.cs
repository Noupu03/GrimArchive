using VContainer;
using VContainer.Unity;

// GameSession.Awake()가 수동으로 하던 AddComponent/FindObjectOfType 배선을 대체하는 DI 컴포지션 루트.
// ssh.unity에 이 컴포넌트가 붙은 GameObject(예: "CompositionRoot")가 하나 있어야 한다.
public class GameCompositionRoot : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // 씬에 이미 배치되어 인스펙터 데이터를 들고 있는 서비스 (UnitManager, UnitSpriteManager 오브젝트)
        builder.RegisterComponentInHierarchy<GameSession>();
        builder.RegisterComponentInHierarchy<UnitGenerate>();
        builder.RegisterComponentInHierarchy<UnitSpriteManager>();
        builder.RegisterComponentInHierarchy<VFXManager>();
        builder.RegisterComponentInHierarchy<UIManager>();

        // 기존에 GameSession.Awake()/Start()가 런타임에 동적 생성하던 서비스
        builder.RegisterComponentOnNewGameObject<InputManager>(Lifetime.Singleton, "InputManager");
        builder.RegisterComponentOnNewGameObject<ThreatTileRenderer>(Lifetime.Singleton, "ThreatTileRenderer");

        // 게임 데이터(skills/units) 로딩을 컨테이너 빌드 직후 비동기로 시작
        builder.RegisterEntryPoint<GameDataBootstrap>(Lifetime.Singleton).As<IAsyncStartable>();
    }
}
