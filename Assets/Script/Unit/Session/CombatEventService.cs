using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class CombatEventService
{
    // 3장: 처치 이벤트를 이해도/위험도에 반영. 인류가 몬스터를 처치하면 "직접 경험(SELF)"으로 연결.
    // 몬스터가 인류를 처치한 경우 죽은 본인은 기록을 남길 수 없으므로, 그 순간 생존한 다른 인류
    // 전원을 "직접 목격"으로 근사한다(실제 FOV 기반 목격 판정은 아직 없음 — 구현현황 문서에 근사임을 명시).
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
                if (witness == null || witness == attacker || !(witness is Human) || witness.Health.hp <= 0) continue;
                knowledge.RecordEvent(EventId.E_MONSTER_KILL_SEEN, witness, victim, InfoType.DirectWitness, incidentId);
            }
        }
        // else(인류가 몬스터에게 처치당한 경우)는 03문서 4-12~4-15장(파티원 사망 발견) 구현으로 대체됐다
        // — GameSession.RemoveDeadUnit → PartyDeathSystem.OnPartyMemberDied가 실제 시야 범위 기반으로
        // 직접 목격자를 가리고(E_HUMAN_KILL_SEEN), 그 자리에 없던 파티원은 시체 인지/조사 후에야
        // 사망 원인이 간접 확인(E_HUMAN_KILL_INDIRECT)된다.
    }
}
