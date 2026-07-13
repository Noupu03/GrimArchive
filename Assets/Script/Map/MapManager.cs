using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using Haare.Client.Routine;
using Haare.Util.Logger;
using GrimArchive.Wave;

public class MapManager : NativeRoutine
{
    private CreateMap _createMap;
    private MapRandering _mapRandering;
    private WaveSpawner _waveSpawner;
    
    public MapRandering mapRandering => _mapRandering;
    public WaveSpawner waveSpawner => _waveSpawner;

    // 런타임에 맵이 세팅되었음을 외부(MapView 등)에 알리는 이벤트
    public static event Action<CreateMap> OnRuntimeMapDataUpdated;

    [Inject]
    public void Construct(CreateMap createMap, MapRandering mapRandering, WaveSpawner waveSpawner)
    {
        _createMap = createMap;
        _mapRandering = mapRandering;
        _waveSpawner = waveSpawner;
    }

    public override async UniTask Initialize(CancellationToken cts)
    {
        await base.Initialize(cts);
        LogHelper.Log(LogHelper.GAME, "MapManager: Initialize.");
    }

    public void SetupAndVisualizeMap(CreateMap cmap)
    {
        if (cmap == null || cmap.map.floors == null)
        {
            LogHelper.Error(LogHelper.GAME, "MapManager: 전달받은 CreateMap 데이터가 유효하지 않습니다.");
            return;
        }

        // 맵 시각화(렌더링) 지시
        if (_mapRandering != null)
        {
            _mapRandering.DoRandering();
        }

        // 웨이브 스폰 초기화 지시 (요청에 따라 비활성화)
        // if (_waveSpawner != null)
        // {
        //     _waveSpawner.SpawnWave();
        // }
        
        // 맵이 세팅되었음을 이벤트로 브로드캐스트 (MapView에서 구독)
        OnRuntimeMapDataUpdated?.Invoke(cmap);
        
        LogHelper.Log(LogHelper.GAME, "MapManager: 맵 렌더링 및 웨이브 스포너 초기화 완료, 브로드캐스트 전송.");
    }
}
