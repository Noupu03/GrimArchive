// ...existing code...
using UnityEngine;
using System.Collections.Generic;

public class PartyController : MonoBehaviour
{
    public static PartyController Instance;
    
    public Unit leader;
    public float leaderlessShockTimer = 0f;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (GameSession.Instance == null) return;

        UpdateLeader();

        if (leaderlessShockTimer > 0f)
        {
            leaderlessShockTimer -= Time.deltaTime;
        }
    }

    private void UpdateLeader()
    {
        bool leaderAlive = false;
        
        List<Unit> humans = new List<Unit>();
        foreach (var u in GameSession.Instance.units)
        {
            if (u is Human && u.hp > 0)
            {
                humans.Add(u);
                if (u == leader) leaderAlive = true;
            }
        }

        if (!leaderAlive && leader != null)
        {
            // 리더 사망 시 재선출 방지 및 10초 쇼크
            leader = null;
            leaderlessShockTimer = 10f;
            Debug.Log("리더가 사망했습니다! Leaderless Shock 적용 (10초)");
        }
        else if (leader == null && leaderlessShockTimer <= 0f)
        {
            // 리더가 아예 없는 초기 상태라면 선출
            Unit bestLeader = null;
            float maxLeadership = -1f;
            
            foreach(var u in humans)
            {
                if (u.leadership >= 20f && u.leadership > maxLeadership)
                {
                    maxLeadership = u.leadership;
                    bestLeader = u;
                }
            }
            if (bestLeader != null) leader = bestLeader;
        }
    }

    public void HandleSpottingBroadcast(Unit spotter, Unit target)
    {
        if (leaderlessShockTimer > 0f) return; // 쇼크 상태면 Broadcast 무효
        
        // 같은 층의 모든 아군에게 전달하여 Wait 상태 진입
        foreach (var u in GameSession.Instance.units)
        {
            if (u is Human && u.hp > 0 && u.currentFloor == spotter.currentFloor && u != spotter)
            {
                u.isWaitState = true;
                Debug.Log($"{u.unitType.typeName}가 {spotter.unitType.typeName}의 Broadcast를 수신하고 대기 상태 진입.");
            }
        }
    }
}
// ...existing code...