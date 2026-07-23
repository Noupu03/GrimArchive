using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class CombatEventService
{
    // 대표 가중치 3종 연산공식 문서 3장: 처치 이벤트를 이해도/위험도에 반영.
    // 인류가 몬스터를 처치한 경우는 "직접 경험(SELF)"으로 바로 연결한다.
    // 몬스터가 인류를 처치한 경우, 죽은 본인은 정보를 남길 수 없으므로 그 순간 생존해 있는
    // 다른 인류 전원이 "직접 목격"한 것으로 근사 처리한다 — 실제 FOV 기반 목격 판정(그 인류가
    // 정말 그 자리를 보고 있었는지)은 아직 없어서 근사임을 구현현황 문서에 남긴다.
    public void RecordKillWeightEvent(Unit victim, List<Unit> units)
    {
        Unit attacker = victim.lastAttacker;
        if (attacker == null) return;

        var knowledge = attacker.Knowledge;
        if (knowledge == null) return;

        bool victimIsHuman = victim is Human;
        bool attackerIsHuman = attacker is Human;
        if (victimIsHuman == attackerIsHuman) return;

        string incidentId = System.Guid.NewGuid().ToString();

        if (!victimIsHuman)
        {
            knowledge.RecordEvent(EventId.E_MONSTER_KILL_SELF, attacker, victim, InfoType.DirectExperience, incidentId);

            // 처치한 본인 외에 그 순간 생존해 있는 다른 인류도 "직접 목격"한 것으로 근사(위와 동일한 근사).
            foreach (var witness in units)
            {
                if (witness == null || witness == attacker || !(witness is Human) || witness.GetComponent<HealthComponent>().hp <= 0) continue;
                knowledge.RecordEvent(EventId.E_MONSTER_KILL_SEEN, witness, victim, InfoType.DirectWitness, incidentId);
            }
        }
        else
        {
            foreach (var witness in units)
            {
                if (witness == null || witness == victim || !(witness is Human) || witness.GetComponent<HealthComponent>().hp <= 0) continue;
                knowledge.RecordEvent(EventId.E_HUMAN_KILL_SEEN, witness, attacker, InfoType.DirectWitness, incidentId);
            }
        }
    }
}
