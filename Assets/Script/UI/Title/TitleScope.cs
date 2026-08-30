using VContainer;
using VContainer.Unity;
using Haare.Client.UI;

// Title.unity의 DI 루트 — GameCompositionRoot(ssh.unity)와 달리 CoreLifetimeScope를 상속하지 않는다.
// 이 씬은 전역 서비스가 필요 없고 씬 로컬 SceneUIManager(TitleUIManager)만으로 충분하다.
public class TitleScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<TitleUIManager>().As<SceneUIManager>().AsSelf();
        builder.RegisterEntryPoint<TitlePresenter>();
    }
}
