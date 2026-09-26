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
                if (!obj.IsCollected && !terrainObserver.Memory.personalMap.IsObjectKnown(obj.Id))
                {
                    if (inPerceptionRange)
                    {
                        float objVisibility = VisionMath.ResolveObjectVisibility(obj.BaseVisibility, obj.Tags);
                        PerceptionOutcome outcome = context.ResolveReachedTarget(obj.Id, objVisibility, tile, dist, out bool firstTouch);

                        if (firstTouch && outcome == PerceptionOutcome.AccuratePerception)
                        {
                            terrainObserver.Memory.personalMap.RegisterObject(obj.Id, obj.Position, obj.BaseDanger, obj.BaseInterest, obj.Tags, obj.CauserStage);

                            // 01-10: 오브젝트 관찰만으로는 보스방으로 기록하지 않는다(ConfirmBossRoom만이 승격시킴).
                            terrainObserver.Memory.personalMap.ObserveObjectInRoom(chunk.roomId, false, obj.Id, obj.BaseDanger, obj.BaseInterest);

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
