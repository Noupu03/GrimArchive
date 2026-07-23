using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class PartyService
{
    public List<Party> parties { get; private set; } = new List<Party>();

    private ObjectSpawner _objectSpawner;

    [Inject]
    public void Construct(ObjectSpawner objectSpawner)
    {
        _objectSpawner = objectSpawner;
    }

    public Party CreateParty(string name, List<Human> members)
    {
        var party = new Party(System.Guid.NewGuid().ToString(), name);
        foreach (var m in members)
        {
            if (m == null) continue;
            party.Members.Add(m);
            m.GetComponent<PartyComponent>().party = party;

            m.Knowledge?.InitializeNewUnitPersonalInfo(m);
        }
        parties.Add(party);
        return party;
    }

    public void CheckPartyWaveState(Unit deadUnit)
    {
        var knowledge = deadUnit.Knowledge;
        if (knowledge == null) return;

        if (deadUnit is Human deadHuman && deadHuman.GetComponent<PartyComponent>().party != null)
        {
            var party = deadHuman.GetComponent<PartyComponent>().party;
            if (party.WaveEnded || !party.IsWiped) return;

            party.WaveEnded = true;
            knowledge.OnPartyWipeout();

            Unit causer = deadHuman.lastAttacker;
            DangerStage causerStage = DangerStage.Stage0;
            if (causer != null)
                causerStage = knowledge.GetDangerStage(causer.unitType.typeName, causer.isSpecialUnit ? causer.name : null, causer.GetComponent<BaseStatComponent>().baseDanger);
            string traceId = knowledge.RegisterWipeoutTrace(causerStage);

            string objId = "Wipeout_" + System.Guid.NewGuid().ToString().Substring(0, 4);
            Vector3Int gridPos = new Vector3Int(deadHuman.position.x, deadHuman.position.y, deadHuman.currentFloor);
            List<string> tags = new List<string> { "Object/Passable/WipeoutTrace" };
            InteractableObject wipeoutObj = new InteractableObject(objId, gridPos, WeightMath.WipeoutTraceBaseInterest, 0f, tags, causerStage, traceId);
            _objectSpawner?.SpawnObject(wipeoutObj, Color.black);
        }
        else if (deadUnit is Monster deadMonster)
        {
            foreach (var party in parties)
            {
                if (party.WaveEnded || !party.WaveMonsters.Contains(deadMonster) || !party.IsWaveCleared) continue;

                party.WaveEnded = true;
                var survivors = party.GetSurvivors();
                knowledge.OnWaveEnd(survivors);
            }
        }
    }
}
