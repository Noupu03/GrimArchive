using UnityEngine;

public class UnitPerceptionHandler : IVisionTileHandler
{
    public void Handle(Unit observer, IVisionContext context, Vector3Int tile, bool inPerceptionRange, float dist, Chunks chunk, Tile tileData)
    {
        if (context.Session != null && context.Session.unitGrid.TryGetValue(tile, out Unit unit))
        {
            if (unit != null && unit != observer && unit.Health.hp > 0)
            {
                bool isEnemy = observer.IsEnemy(unit);
                if (isEnemy)
                {
                    if (inPerceptionRange)
                    {
                        PerceptionOutcome outcome = context.ResolveReachedTarget(unit, unit.GetFinalVisibility(), tile, dist, out bool firstTouch);

                        if (outcome == PerceptionOutcome.AccuratePerception)
                        {
                            context.AddPersonalSpottedEnemy(unit);

                            if (observer is Human human && observer.Knowledge != null)
                            {
                                float danger = observer.Knowledge.GetPersonalDanger(human, unit);
                                float interest = observer.Knowledge.GetPersonalInterest(human, unit);
                                human.Memory.personalMap.ObserveMonster(unit.name, tile, danger, interest);

                                human.Memory.personalMap.ObserveUnitInRoom(chunk.roomId, false, unit.name, danger, interest);
                                if (unit.unitType != null && unit.unitType.isBoss)
                                    human.Memory.personalMap.ConfirmBossRoom(chunk.roomId);
                            }
                        }
                    }
                    else if (!context.HasReachedPerceptionThisPass(unit) && !context.HasVisionOnlyNonEmptyTile(tile))
                    {
                        context.AddVisionOnlyNonEmptyTile(tile);
                    }
                }
            }
        }
    }
}
