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
            _mapRandering.DoRandering(cmap);
        }

        // 웨이브 스폰 초기화 지시 (요청에 따라 비활성화 — 웨이브 스폰은 HumanWaveManager의
        // 타이머 흐름(0층 사전 스폰 → 계단 이동으로 목표 층 진입)이 전담한다. 여기서 즉시
        // SpawnWave()를 호출하면 맵이 세팅되자마자 목표 층에 몬스터/파티가 중복으로 미리
        // 생성된다 — 2026-07-23 실수로 재활성화됐던 것을 원복(사용자 신고, 2026-07-24).)
        // if (_waveSpawner != null) { _waveSpawner.SpawnWave(); }
        
        // 맵이 세팅되었음을 이벤트로 브로드캐스트 (MapView에서 구독)
        OnRuntimeMapDataUpdated?.Invoke(cmap);
        
        LogHelper.Log(LogHelper.GAME, "MapManager: 맵 렌더링 및 웨이브 스포너 초기화 완료, 브로드캐스트 전송.");
    }
}

