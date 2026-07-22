using UnityEngine;

public interface IVisionTileHandler
{
    void Handle(Unit observer, IVisionContext context, Vector3Int tile, bool inPerceptionRange, float dist, Chunks chunk, Tile tileData);
}
