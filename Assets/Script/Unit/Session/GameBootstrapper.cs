using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Haare.Client.Core;
using Haare.Util.Logger;
using GrimArchive.Wave;

public class GameBootstrapper : IAsyncStartable
{
    private readonly IObjectResolver _resolver;

    [Inject]
    public GameBootstrapper(IObjectResolver resolver)
    {
        _resolver = resolver;
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        LogHelper.Log(LogHelper.FRAMEWORK, "GameBootstrapper: Waiting for Haare Processor...");
        
        await Processor.WaitForCreation();
        await UniTask.WaitUntil(() => Processor.Instance != null && Processor.Instance.isInitialized, cancellationToken: cancellation);

        LogHelper.Log(LogHelper.FRAMEWORK, "GameBootstrapper: Processor ready. Resolving all game systems...");

        // 1단계: MapRandering, WaveSpawner를 먼저 Resolve해서 NativeRoutine 생성 및 Processor 등록 시작
        var mapRandering   = _resolver.Resolve<MapRandering>();
        var waveSpawner    = _resolver.Resolve<WaveSpawner>();
        var humanWaveMgr   = _resolver.Resolve<HumanWaveManager>();

        // 2단계: MapManager Resolve - 위의 의존성들이 이미 생성되어 있으므로 property inject 즉시 가능
        var mapManager     = _resolver.Resolve<MapManager>();

        // 3단계: MapManager의 NativeRoutine Initialize 완료 대기
        await UniTask.WaitUntil(() => mapManager.isInitialized, cancellationToken: cancellation);
        LogHelper.Log(LogHelper.FRAMEWORK, "GameBootstrapper: MapManager initialized.");

        // 4단계: InputManager Resolve (MonoBehaviour 생성)
        var inputManager = _resolver.Resolve<InputManager>();

        // 5단계: GameSession Resolve - _mapManager property inject가 이미 완료된 상태
        var gameSession = _resolver.Resolve<GameSession>();

        // 6단계: GameSession Initialize 완료 대기 (맵 로드 + 렌더링 + 웨이브 스폰 포함)
        await UniTask.WaitUntil(() => gameSession.isInitialized, cancellationToken: cancellation);

        LogHelper.Log(LogHelper.FRAMEWORK, $"GameBootstrapper: All systems ready. GameSession: {gameSession != null}, MapManager: {mapManager != null}");
    }
}
