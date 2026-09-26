using UnityEngine;

public class TerrainRevealHandler : IVisionTileHandler
{
    public void Handle(Unit observer, IVisionContext context, Vector3Int tile, bool inPerceptionRange, float dist, Chunks chunk, Tile tileData)
    {
        bool tileIsWall = tileData.name == "Wall" || tileData.isStructureExist;
        
        if (observer is Human terrainObserver)
        {
            bool isFirstReveal = terrainObserver.Memory.personalMap.RevealTile(tile, tileIsWall);

            if (isFirstReveal && !tileIsWall)
            {
                int totalFloorTiles = context.Session.cmap.GetRoomFloorTileCount(tile.z, chunk.roomId);
                // 01-10: 타일 공개만으로는 보스방으로 기록하지 않는다(ConfirmBossRoom만이 승격시킴).
                terrainObserver.Memory.personalMap.ObserveRoomTileRevealed(chunk.roomId, false, totalFloorTiles, terrainObserver);
                // 01번 문서 7-2장: 파티 전체 시야 합산(검증문서 01-06-4 1번).
                terrainObserver.party?.OnTileRevealedInRoom(chunk.roomId, new Vector2Int(tile.x, tile.y), totalFloorTiles);
            }
        }
    }
}
