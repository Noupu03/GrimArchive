using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

[System.Serializable]
public class WildBaseSpawnerComponent : IUnitComponent
{
    private Unit _owner;
    private Room _targetRoom;
    private float _spawnInterval = 5f; // 5초마다 생성
    private const int _maxUnits = 6;   // MVP 4.2: 최대 야생 유닛 수 제한
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
        
        Debug.Log($"[WildBaseSpawnerComponent] 야생 거점 스포너 모듈이 부착되었습니다. (부착된 유닛 체력: {_owner.Health.hp})");
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

        // MVP 4.2: 최대 개체 수 도달 시 생성 중단
        var roomUnits = _owner.Session.GetUnitsInRoom(_targetRoom.Bounds);
        int wildCount = 0;
        foreach (var u in roomUnits)
            if (u.FactionBehavior is WildMonsterBehavior) wildCount++;
        if (wildCount >= _maxUnits) return;

        UnitType monsterType = new MeleeTank();
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
        
        Debug.Log($"[WildBaseSpawnerComponent] 거점에서 야생 몬스터를 {spawnPos} 좌표에 생성했습니다!");
    }

    public void OnDespawn()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        if (_targetRoom != null)
            _targetRoom.HasActiveSpawner = false;

        Debug.Log("[WildBaseSpawnerComponent] 거점 스포너 모듈이 파괴되었습니다!");
    }
}


