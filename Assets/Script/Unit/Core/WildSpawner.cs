using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class WildSpawner : MonoBehaviour
{
    public Room targetRoom;
    public int maxUnits = 5;
    public float spawnInterval = 3f;
    
    private int currentSpawned = 0;
    private CancellationTokenSource cts;

    public void Initialize(Room room)
    {
        targetRoom = room;
        if (targetRoom != null) targetRoom.HasActiveSpawner = true;
        cts = new CancellationTokenSource();
        SpawnLoop(cts.Token).Forget();
        Debug.Log($"[WildSpawner] 야생 거점이 가동되었습니다. (최대 {maxUnits}마리, {spawnInterval}초 주기)");
    }

    private async UniTaskVoid SpawnLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && currentSpawned < maxUnits)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(spawnInterval), cancellationToken: token);
            if (GameSession.Instance == null || GameSession.Instance.unitGenerate == null) continue;

            UnitType selection = new MeleeTank();
            Vector2Int spawnPos = targetRoom != null ? targetRoom.GetRandomPosInRoom() : new Vector2Int(0, 0);
            Monster monster = GameSession.Instance.unitGenerate.GenerateUnitAtPos<Monster>(selection, spawnPos, 1);
            
            // HAARE 프레임워크 (Native Routine): 생성된 유닛에 야생 알고리즘 주입
            monster.FactionBehavior = new WildMonsterBehavior();
            
            GameSession.Instance.units.Add(monster);
            GameSession.Instance.RegisterUnitPos(monster, monster.position);
            
            if (targetRoom != null) targetRoom.AddUnit(monster);
            
            currentSpawned++;
            Debug.Log($"[거점형] 야생 몬스터 스폰 ({currentSpawned}/{maxUnits}) 위치: {monster.position}");
        }
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        cts?.Dispose();
        if (targetRoom != null) targetRoom.HasActiveSpawner = false;
        Debug.Log("[WildSpawner] 거점이 파괴되어 몬스터 생성이 중단되었습니다.");
    }
}
