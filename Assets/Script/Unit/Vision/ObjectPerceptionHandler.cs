using System.Linq;
using UnityEngine;

public class ObjectPerceptionHandler : IVisionTileHandler
{
    public void Handle(Unit observer, IVisionContext context, Vector3Int tile, bool inPerceptionRange, float dist, Chunks chunk, Tile tileData)
    {
        if (observer is Human terrainObserver)
        {
            if (context.Session != null && context.Session.objectGrid.TryGetValue(tile, out InteractableObject obj))
            {
                if (!obj.IsCollected && !terrainObserver.GetComponent<MemoryComponent>().personalMap.IsObjectKnown(obj.Id))
                {
                    if (inPerceptionRange)
                    {
                        float objVisibility = VisionMath.ResolveObjectVisibility(obj.BaseVisibility, obj.Tags);
                        PerceptionOutcome outcome = context.ResolveReachedTarget(obj.Id, objVisibility, tile, dist, out bool firstTouch);

                        if (firstTouch && outcome == PerceptionOutcome.AccuratePerception)
                        {
                            terrainObserver.GetComponent<MemoryComponent>().personalMap.RegisterObject(obj.Id, obj.Position, obj.BaseDanger, obj.BaseInterest, obj.Tags, obj.CauserStage);

                            bool isBossRoom = chunk.roomRole == RoomRole.BossRoom;
                            terrainObserver.GetComponent<MemoryComponent>().personalMap.ObserveObjectInRoom(chunk.roomId, isBossRoom, obj.Id, obj.BaseDanger, obj.BaseInterest);

                            if (obj.Tags.Any(t => t.Contains("WipeoutTrace")) && !string.IsNullOrEmpty(obj.TraceId))
                            {
                                terrainObserver.Knowledge?.OnWipeoutTraceReflected(obj.TraceId);
                            }
                        }
                    }
                    else if (!context.HasReachedPerceptionThisPass(obj.Id) && !context.HasVisionOnlyNonEmptyTile(tile))
                    {
                        context.AddVisionOnlyNonEmptyTile(tile);
                    }
                }
            }
        }
    }
}
