using UnityEngine;
using System.Collections.Generic;

public enum PartyGoal { Sweep, Recovery,Exploration }

[System.Serializable]
public class Party
{
    public string partyName;
    public PartyGoal partyGoal;
    public List<Unit> members = new List<Unit>();
    public Unit leader;
    public float leaderlessShockTimer = 0f;

    public void UpdateLeader()
    {
        members.RemoveAll(u => u == null || u.hp <= 0);

        if (leader != null && (leader.hp <= 0 || !members.Contains(leader)))
        {
            leader = null;
            leaderlessShockTimer = 10f;
            Debug.Log($"[{partyName}] 지휘관(리더) 사망! Leaderless Shock 적용 (10초)");
        }
        else if (leader == null && leaderlessShockTimer <= 0f)
        {
            // 리더(지휘관) 선출: 20 이상인 단위체 대상 (현재로선 지휘관형뿐)
            Unit bestLeader = null;
            float maxLeadership = -1f;
            foreach (var u in members)
            {
                if (u.leadership >= 20f && u.leadership > maxLeadership)
                {
                    maxLeadership = u.leadership;
                    bestLeader = u;
                }
            }
            if (bestLeader != null) leader = bestLeader;
        }

        if (leaderlessShockTimer > 0f)
            leaderlessShockTimer -= Time.deltaTime;
    }
}