using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

// GameCompositionRoot 컨테이너 빌드 직후 GameDataLoader의 비동기 로딩을 시작하는 진입점.
public class GameDataBootstrap : IAsyncStartable
{
    public UniTask StartAsync(CancellationToken cancellation)
    {
        return GameDataLoader.LoadAsync(cancellation);
    }
}
