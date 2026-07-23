using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Haare.Util.Logger;
using Haare.Client.Routine;
using VContainer;
using Cysharp.Threading.Tasks;
using System.Threading;

public class MapRandering : NativeRoutine, IMapColorizer
{
    private CreateMap createMap;

    private Sprite wallSprite;
    private Sprite floorSprite;
    private Sprite stairSprite;

    public Tilemap[] floorTilemaps { get; private set; }
    public Vector3Int[] floorOffsets { get; private set; }

    private UnityEngine.Tilemaps.Tile wallTile;
    private UnityEngine.Tilemaps.Tile floorTile;
    private UnityEngine.Tilemaps.Tile stairTile;

    private const int ChunkSize = 8;
    private GameObject mapRoot;

    [Inject]
    public void Construct(CreateMap createMap)
    {
        this.createMap = createMap;
    }

    public override async UniTask Initialize(CancellationToken cts)
    {
        await base.Initialize(cts);
        // DoRandering() 호출은 MapManager가 맵 데이터를 준비한 뒤 명시적으로 호출하도록 제거됨
    }

    public void DoRandering(CreateMap targetMap = null) { if (targetMap != null) { this.createMap = targetMap; } BuildTileCache(); RenderAllFloors(); } private void _OldDoRanderingUnused()
    {
        BuildTileCache();
        RenderAllFloors();
    }

    void BuildTileCache()
    {
        if (wallSprite == null || floorSprite == null)
        {
            // 사용자가 지정한 각각의 텍스처 로드 (Resources 폴더 기준)
            wallSprite = Resources.Load<Sprite>("Tile_StoneWall");
            floorSprite = Resources.Load<Sprite>("FloorTexture");

            if (wallSprite == null || floorSprite == null)
            {
                LogHelper.Warning(LogHelper.GAME, "MapRandering: Resources 폴더에서 타일 이미지를 찾지 못해 임시 단색 이미지를 생성합니다.");
                
                if (wallSprite == null) wallSprite = CreateColorSprite(Color.gray);
                if (floorSprite == null) floorSprite = CreateColorSprite(Color.white);
            }
        }

        wallTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        wallTile.sprite = wallSprite;

        floorTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        floorTile.sprite = floorSprite;

        stairTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        stairTile.sprite = stairSprite != null ? stairSprite : CreateColorSprite(Color.yellow);
    }

    private Sprite CreateColorSprite(Color color)
    {
        Texture2D tex = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    public void RenderAllFloors()
    {
        if (createMap == null || createMap.map.floors == null || createMap.map.floors.Length == 0)
        {
            LogHelper.Error(LogHelper.GAME, "MapRandering: map 데이터가 없습니다.");
            return;
        }

        ClearExistingTilemaps();

        int floorCount = createMap.map.floors.Length;
        floorOffsets = ComputeStairAlignedOffsets();

        if (mapRoot == null)
        {
            mapRoot = new GameObject("MapRoot_Grid");
            mapRoot.AddComponent<Grid>();
        }

        floorTilemaps = new Tilemap[floorCount];

        for (int f = 0; f < floorCount; f++)
        {
            Floor floor = createMap.map.floors[f];
            if (floor.chunks == null) continue;

            GameObject tilemapObj = new GameObject($"F{f}_Tilemap");
            tilemapObj.transform.SetParent(mapRoot.transform, false);
            tilemapObj.transform.localPosition = new Vector3(floorOffsets[f].x, floorOffsets[f].y, 0f);

            Tilemap tilemap = tilemapObj.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObj.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = f;

            floorTilemaps[f] = tilemap;
            RenderFloor(tilemap, ref floor, f);
        }

        LogHelper.Log(LogHelper.GAME, $"MapRandering: 전체 {floorCount}개 Floor 렌더링 완료.");
    }

    void RenderFloor(Tilemap tilemap, ref Floor floor, int floorIdx)
    {
        int chunkCountX = floor.config.width;
        int chunkCountY = floor.config.height;
        int totalTiles = chunkCountX * ChunkSize * chunkCountY * ChunkSize;
        var positions = new Vector3Int[totalTiles];
        var tiles = new UnityEngine.Tilemaps.TileBase[totalTiles];
        int idx = 0;

        for (int cx = 0; cx < chunkCountX; cx++)
        {
            for (int cy = 0; cy < chunkCountY; cy++)
            {
                Chunks chunk = floor.chunks[cx, cy];
                if (chunk.chunk == null) continue;

                for (int tx = 0; tx < ChunkSize; tx++)
                {
                    for (int ty = 0; ty < ChunkSize; ty++)
                    {
                        string tileName = chunk.chunk[tx, ty].name;
                        UnityEngine.Tilemaps.TileBase tileBase = floorTile;
                        if (tileName == "Wall") tileBase = wallTile;
                        else if (tileName == "Stair") tileBase = stairTile;

                        positions[idx] = new Vector3Int(cx * ChunkSize + tx, cy * ChunkSize + ty, 0);
                        tiles[idx] = tileBase;
                        idx++;
                    }
                }
            }
        }

        if (idx < totalTiles)
        {
            var usedPositions = new Vector3Int[idx];
            var usedTiles = new UnityEngine.Tilemaps.TileBase[idx];
            System.Array.Copy(positions, usedPositions, idx);
            System.Array.Copy(tiles, usedTiles, idx);
            tilemap.SetTiles(usedPositions, usedTiles);
            LogHelper.Log(LogHelper.GAME, $"MapRandering: F{floorIdx}에 총 {idx}개의 타일을 배치했습니다. (일부 청크 비어있음)");
        }
        else
        {
            tilemap.SetTiles(positions, tiles);
            LogHelper.Log(LogHelper.GAME, $"MapRandering: F{floorIdx}에 총 {idx}개의 타일을 모두 꽉 채워 배치했습니다.");
        }
    }

    Vector3Int[] ComputeStairAlignedOffsets()
    {
        int floorCount = createMap.map.floors.Length;
        var offsets = new Vector3Int[floorCount];
        offsets[0] = Vector3Int.zero;

        if (floorCount > 1)
        {
            Vector2Int stairInF0 = FindStairTileCenter(ref createMap.map.floors[0], 1);
            Vector2Int stairInF1 = FindStairTileCenter(ref createMap.map.floors[1], 0);
            offsets[1] = new Vector3Int(offsets[0].x + stairInF0.x - stairInF1.x, offsets[0].y + stairInF0.y - stairInF1.y, 0);
        }
        if (floorCount > 2)
        {
            Vector2Int stairInF1 = FindStairTileCenter(ref createMap.map.floors[1], 2);
            Vector2Int stairInF2 = FindReturnStairTileCenter(ref createMap.map.floors[2], 1);
            offsets[2] = new Vector3Int(offsets[1].x + stairInF1.x - stairInF2.x, offsets[1].y + stairInF1.y - stairInF2.y, 0);
        }
        if (floorCount > 3)
        {
            Vector2Int stairInF2 = FindStairTileCenter(ref createMap.map.floors[2], 3);
            Vector2Int stairInF3 = FindReturnStairTileCenter(ref createMap.map.floors[3], 2);
            offsets[3] = new Vector3Int(offsets[2].x + stairInF2.x - stairInF3.x, offsets[2].y + stairInF2.y - stairInF3.y, 0);
        }

        return offsets;
    }

    Vector2Int FindStairTileCenter(ref Floor floor, int targetFloor)
    {
        int w = floor.config.width, h = floor.config.height;
        for (int cx = 0; cx < w; cx++)
            for (int cy = 0; cy < h; cy++)
                if (floor.chunks[cx, cy].stairTargetFloor == targetFloor)
                    return new Vector2Int(cx * ChunkSize + 3, cy * ChunkSize + 3);
        return Vector2Int.zero;
    }

    Vector2Int FindReturnStairTileCenter(ref Floor floor, int fromFloor)
    {
        int w = floor.config.width, h = floor.config.height;
        for (int cx = 0; cx < w; cx++)
            for (int cy = 0; cy < h; cy++)
                if (floor.chunks[cx, cy].stairTargetFloor == fromFloor)
                    return new Vector2Int(cx * ChunkSize + 3, cy * ChunkSize + 3);

        for (int cx = 0; cx < w; cx++)
            for (int cy = 0; cy < h; cy++)
                if (floor.chunks[cx, cy].stairTargetFloor == 0)
                    return new Vector2Int(cx * ChunkSize + 3, cy * ChunkSize + 3);

        return Vector2Int.zero;
    }

    void ClearExistingTilemaps()
    {
        floorTilemaps = null;
        if (mapRoot != null)
        {
            for (int i = mapRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = mapRoot.transform.GetChild(i);
                if (Application.isPlaying) Object.Destroy(child.gameObject);
                else Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    public void ShowFloor(int floorIndex)
    {
        if (floorTilemaps == null) return;
        for (int f = 0; f < floorTilemaps.Length; f++)
            if (floorTilemaps[f] != null) floorTilemaps[f].gameObject.SetActive(f == floorIndex);
    }

    public void ShowAllFloors()
    {
        if (floorTilemaps == null) return;
        for (int f = 0; f < floorTilemaps.Length; f++)
            if (floorTilemaps[f] != null) floorTilemaps[f].gameObject.SetActive(true);
    }

    public void ChangeRoomColor(Room room, Color color)
    {
        if (floorTilemaps == null || floorTilemaps.Length == 0) return;
        Tilemap tm = floorTilemaps[0]; // MVP: 0층 기준
        
        for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
        {
            for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                tm.SetTileFlags(pos, TileFlags.None);
                tm.SetColor(pos, color);
            }
        }
    }
}

