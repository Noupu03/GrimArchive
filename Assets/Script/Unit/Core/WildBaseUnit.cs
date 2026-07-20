using UnityEngine;
using System.Collections.Generic;

// 야생 거점(Spawner)을 물리적인 유닛으로 취급하기 위한 클래스
public class WildBaseUnit : Monster
{
    private float spawnTimer = 0f;
    private float spawnInterval = 5f; // 5초마다 생성
    private Room targetRoom;

    public void InitializeBase(Room room)
    {
        targetRoom = room;
        FactionBehavior = new WildMonsterBehavior();
        unitType = new WildBaseType();
        
        hp = 500f; // 튼튼한 체력
        maxHp = 500f;
        walkSpeed = 0f; // 이동 불가

        if (targetRoom != null)
        {
            targetRoom.HasActiveSpawner = true;
        }

        Debug.Log($"[WildBaseUnit] 야생 거점이 생성되었습니다. (체력: {hp})");
    }

    public override void OnUpdate(float deltaTime)
    {
        // 살아있을 때만 스폰 타이머 작동 (GOAP AI는 돌리지 않음)
        if (hp > 0 && targetRoom != null)
        {
            spawnTimer += deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                SpawnMonster();
            }
        }
    }

    private void SpawnMonster()
    {
        if (GameSession.Instance == null || targetRoom == null || GameSession.Instance.unitGenerate == null) return;

        // 거점과 동일한 방 영역 내 무작위 좌표 중 빈 공간(벽이 아닌 곳) 찾기
        UnitType monsterType = new MeleeTank();
        Vector2Int spawnPos = targetRoom.GetRandomPosInRoom();
        int attempts = 0;
        
        while (!GameSession.Instance.unitGenerate.IsAreaClear(spawnPos, monsterType.footprint, this.currentFloor) && attempts < 20)
        {
            spawnPos = targetRoom.GetRandomPosInRoom();
            attempts++;
        }
        
        // MeleeTank 몬스터 생성 (거점형 몬스터 알고리즘)
        Monster monster = GameSession.Instance.unitGenerate.GenerateUnitAtPos<Monster>(monsterType, spawnPos, this.currentFloor);
        monster.FactionBehavior = new WildMonsterBehavior();
        monster.MovementAlgorithm = new RoomConfinedMovement();
        
        GameSession.Instance.units.Add(monster);
        GameSession.Instance.RegisterUnitPos(monster, monster.position);
        targetRoom.AddUnit(monster);
        
        Debug.Log($"[WildBaseUnit] 거점에서 야생 몬스터를 {spawnPos} 좌표에 생성했습니다!");
    }

    public GameObject debugVisual;

    private void OnDestroy()
    {
        // 유닛 파괴(사망) 시
        if (targetRoom != null)
        {
            targetRoom.HasActiveSpawner = false;
        }
        
        if (debugVisual != null)
        {
            Destroy(debugVisual);
        }
        
        Debug.Log("[WildBaseUnit] 거점이 파괴되었습니다!");
    }
}
