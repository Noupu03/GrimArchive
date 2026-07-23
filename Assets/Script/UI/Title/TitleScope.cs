using VContainer;
using VContainer.Unity;
using Haare.Client.UI;

// Demo.TitleScene.DemoTitleScope와 동일한 구조 — Title.unity의 DI 루트.
// GameCompositionRoot(ssh.unity)와 달리 CoreLifetimeScope를 상속하지 않는다: 이 씬은 DataManager/
// CoreUIManager 같은 전역 서비스가 필요 없고(씬 전환은 SceneManager.LoadScene 직행), 씬 로컬
// SceneUIManager(TitleUIManager)만으로 타이틀 패널을 띄우면 충분하다.
public class TitleScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<TitleUIManager>().As<SceneUIManager>().AsSelf();
        builder.RegisterEntryPoint<TitlePresenter>();
    }
}
