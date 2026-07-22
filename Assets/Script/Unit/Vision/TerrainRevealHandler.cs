using UnityEngine;

public class TerrainRevealHandler : IVisionTileHandler
{
    public void Handle(Unit observer, IVisionContext context, Vector3Int tile, bool inPerceptionRange, float dist, Chunks chunk, Tile tileData)
    {
        bool tileIsWall = tileData.name == "Wall" || tileData.isStructureExist;
        
        if (observer is Human terrainObserver)
        {
            bool isFirstReveal = terrainObserver.personalMap.RevealTile(tile, tileIsWall);
            bool isBossRoom = chunk.roomRole == RoomRole.BossRoom;

            if (isFirstReveal && !tileIsWall)
            {
                int totalFloorTiles = context.Session.cmap.GetRoomFloorTileCount(tile.z, chunk.roomId);
                terrainObserver.personalMap.ObserveRoomTileRevealed(chunk.roomId, isBossRoom, totalFloorTiles);
            }
        }
    }
}
