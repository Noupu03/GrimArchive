using UnityEngine;

public class UnitPerceptionHandler : IVisionTileHandler
{
    public void Handle(Unit observer, IVisionContext context, Vector3Int tile, bool inPerceptionRange, float dist, Chunks chunk, Tile tileData)
    {
        if (context.Session != null && context.Session.unitGrid.TryGetValue(tile, out Unit unit))
        {
            if (unit != null && unit != observer && unit.GetComponent<HealthComponent>().hp > 0)
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
                                human.GetComponent<MemoryComponent>().personalMap.ObserveMonster(unit.name, tile, danger, interest);

                                bool isBossRoom = chunk.roomRole == RoomRole.BossRoom;
                                human.GetComponent<MemoryComponent>().personalMap.ObserveUnitInRoom(chunk.roomId, isBossRoom, unit.name, danger, interest);
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
