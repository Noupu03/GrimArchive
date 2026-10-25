using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;
using Haare.Util.Logger;

[System.Serializable]
public class WildBaseSpawnerComponent : IUnitComponent
{
    private Unit _owner;
    private Room _targetRoom;
    private float _spawnInterval = 5f; // 5초마다 생성
    private const int _maxUnits = 6;   // MVP 4.2: 방 최대 인구수가 없는 방에서만 쓰는 예전 상한
    // 이 거점이 만드는 몬스터(MeleeTank)의 인구수 — Unit.populationCost 기본값이다. 생성 전에 정원을 확인해야 해서 종류가 늘면 UnitType 조회로 바꾼다(BuildingManager.HasRoomForProduction과 같은 단순화).
    private const int NewMonsterPopulationCost = 1;
    private CancellationTokenSource _cts;

    
    public WildBaseSpawnerComponent() { }

    public WildBaseSpawnerComponent(Unit owner, Room room)
    {
        _owner = owner;
        _targetRoom = room;

        if (_targetRoom != null)
        {
            _targetRoom.HasActiveSpawner = true;
        }
        
        _cts = new CancellationTokenSource();
        // HAARE 프레임워크 (Native Routine): UniTask 기반 스폰 루프 실행
        SpawnLoop(_cts.Token).Forget();
        
        LogHelper.Log(LogHelper.GAME, $"[WildBaseSpawnerComponent] 야생 거점 스포너 모듈이 부착되었습니다. (부착된 유닛 체력: {_owner.Health.hp})");
    }

    private async UniTaskVoid SpawnLoop(CancellationToken token)
    {
        // Native Routine: Mono Update() 대신 비동기 대기
        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(_spawnInterval), cancellationToken: token);
            
            // 살아있을 때만 스폰 타이머 작동 (GOAP AI는 돌리지 않음)
            if (_owner == null || _owner.Health.hp <= 0)
            {
                break; // 거점 파괴 시 루프 종료
            }

            if (_targetRoom != null)
            {
                SpawnMonster();
            }
        }
    }

    public void OnUpdate(float deltaTime)
    {
        // Native Routine(UniTask) 기반으로 변경되어 더 이상 Mono Routine 방식의 deltaTime 누적을 사용하지 않음.
    }

    private void SpawnMonster()
    {
        if (_owner.Session == null || _targetRoom == null || _owner.Session.unitGenerate == null) return;

        // 방이 야생 소유가 아니면(플레이어가 클리어해 확보했거나 인류가 거점을 세움) 자동 생성을 멈춘다(코어 점령 v0.2 11-1장).
        if (_targetRoom.RoomFaction != FactionType.Wild) return;

        // 방 정원 규칙(코어 점령 v0.2 11-1장) — 방 안의 야생 생존 인구수와 새로 생성할 인구수의 합이 방 최대 인구수를 넘지 않을 때만 생성한다. 죽은 야생 몬스터는 생존 인구수에서 빠지고(시체는 hp 0), 같은 방의 여러 거점을 합산하려고 방 전체의 야생 유닛을 센다.
        // 방 최대 인구수가 아직 정해지지 않은 방(0)은 예전 MVP 상한(_maxUnits마리)을 쓴다.
        UnitType monsterType = new MeleeTank();
        var roomUnits = _owner.Session.GetUnitsInRoom(_targetRoom.Bounds);
        int wildPopulation = 0;
        foreach (var u in roomUnits)
            if (u.hp > 0f && u.FactionBehavior is WildMonsterBehavior && !(u.unitType is WildBaseType)) wildPopulation += u.populationCost;
        if (!WildSpawnMath.CanSpawn(wildPopulation, NewMonsterPopulationCost, _targetRoom.MaxPopulation, _maxUnits)) return;

        Vector2Int spawnPos = _targetRoom.GetRandomPosInRoom();
        int attempts = 0;
        
        while (!_owner.Session.unitGenerate.IsAreaClear(spawnPos, monsterType.footprint, _owner.currentFloor) && attempts < 20)
        {
            spawnPos = _targetRoom.GetRandomPosInRoom();
            attempts++;
        }
        
        Monster monster = _owner.Session.unitGenerate.GenerateUnitAtPos<Monster>(monsterType, spawnPos, _owner.currentFloor);
        monster.FactionBehavior = new WildMonsterBehavior();
        monster.MovementAlgorithm = new RoomConfinedMovement();
        monster.summonPosition = spawnPos; // IdleFSMState 배회 기준점(소환 위치)

        _owner.Session.units.Add(monster);
        _owner.Session.RegisterUnitPos(monster, monster.position);
        _targetRoom.AddUnit(monster);
        
        LogHelper.Log(LogHelper.GAME, $"[WildBaseSpawnerComponent] 거점에서 야생 몬스터를 {spawnPos} 좌표에 생성했습니다!");
    }

    public void OnDespawn()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        if (_targetRoom != null)
            _targetRoom.HasActiveSpawner = false;

        LogHelper.Log(LogHelper.GAME, "[WildBaseSpawnerComponent] 거점 스포너 모듈이 파괴되었습니다!");
    }
}

// 야생 생성 정원 판정(순수) — 코어 점령 v0.2 11-1장·20-2장: 방 안의 생존 인구수와 새로 생성할 인구수의 합이 방 최대 인구수를 넘지 않을 때만 생성한다.
public static class WildSpawnMath
{
    // 예: 방 정원 10, 생존 9 → 인구수 1 생성은 가능, 2는 불가. 방 최대 인구수가 0 이하(미설정)이면 fallbackCapacity를 정원으로 쓴다.
    public static bool CanSpawn(int livingPopulation, int newPopulation, int roomMaxPopulation, int fallbackCapacity)
    {
        int capacity = roomMaxPopulation > 0 ? roomMaxPopulation : fallbackCapacity;
        return livingPopulation + newPopulation <= capacity;
    }
}


