using UnityEngine;
using UnityEngine.Tilemaps;

public class MapRandering : MonoBehaviour
{
    [Header("참조")]
    public CreateMap createMap;
    public Tilemap tilemap;

    [Header("스프라이트 (Assets/Asset/Atras.png 슬라이스)")]
    // Atras_0 → Wall, Atras_1 → Floor
    public Sprite wallSprite;
    public Sprite floorSprite;

    // 생성된 TileBase 캐시
    private UnityEngine.Tilemaps.Tile wallTile;
    private UnityEngine.Tilemaps.Tile floorTile;

    // 청크 수 (16x16), 청크 당 타일 수 (8x8)
    private const int ChunkCount = 16;
    private const int ChunkSize  = 8;

    void Start()
    {
        //DoRandering();
    }

    public void DoRandering()
    {
        BuildTileCache();
        RenderMap();
    }

    // TileBase 오브젝트를 미리 생성하여 반복 생성 비용 제거
    void BuildTileCache()
    {
        wallTile        = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        wallTile.sprite = wallSprite;

        floorTile        = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        floorTile.sprite = floorSprite;
    }

    // 맵 데이터 전체를 Tilemap에 렌더링
    public void RenderMap()
    {
        if (createMap == null || tilemap == null)
        {
            Debug.LogError("MapRandering: createMap 또는 tilemap이 연결되지 않았습니다.");
            return;
        }

        Map map = createMap.map;
        if (map.session == null)
        {
            Debug.LogError("MapRandering: map.session이 null입니다. CreateMap이 먼저 실행되었는지 확인하세요.");
            return;
        }

        tilemap.ClearAllTiles();

        // 배치 처리로 드로우콜 최소화
        int totalTiles  = ChunkCount * ChunkSize;
        var positions   = new Vector3Int[totalTiles * totalTiles];
        var tiles       = new UnityEngine.Tilemaps.TileBase[totalTiles * totalTiles];
        int idx         = 0;

        for (int cx = 0; cx < ChunkCount; cx++)
        {
            for (int cy = 0; cy < ChunkCount; cy++)
            {
                Chunks chunk = map.session[cx, cy];

                // chunk 배열이 null이면 렌더링할 수 없으므로 건너뜀
                if (chunk.chunk == null)
                    continue;

                for (int tx = 0; tx < ChunkSize; tx++)
                {
                    for (int ty = 0; ty < ChunkSize; ty++)
                    {
                        // 월드 타일 좌표: 청크 위치 × 청크 크기 + 청크 내 위치
                        int worldX = cx * ChunkSize + tx;
                        int worldY = cy * ChunkSize + ty;

                        string tileName = chunk.chunk[tx, ty].name;
                        UnityEngine.Tilemaps.TileBase tileBase = tileName == "Wall" ? (UnityEngine.Tilemaps.TileBase)wallTile : floorTile;

                        positions[idx] = new Vector3Int(worldX, worldY, 0);
                        tiles[idx]     = tileBase;
                        idx++;
                    }
                }
            }
        }

        // SetTiles로 한 번에 배치 (개별 SetTile 반복보다 훨씬 빠름)
        tilemap.SetTiles(
            System.Array.ConvertAll(positions, p => p),
            System.Array.ConvertAll(tiles,     t => t)
        );

        // 실제 배치된 타일 수만 처리
        var usedPositions = new Vector3Int[idx];
        var usedTiles     = new UnityEngine.Tilemaps.TileBase[idx];
        System.Array.Copy(positions, usedPositions, idx);
        System.Array.Copy(tiles,     usedTiles,     idx);

        tilemap.ClearAllTiles();
        tilemap.SetTiles(usedPositions, usedTiles);

        Debug.Log($"MapRandering: {idx}개 타일 렌더링 완료.");
    }
}
