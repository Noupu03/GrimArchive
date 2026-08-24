using System;
using System.Collections.Generic;
using UnityEngine;
using Haare.Scripts.Client.Data;

// ========================================================================
// 맵 직렬화 유틸리티
// ========================================================================
// Unity JsonUtility는 다차원 배열(Tile[,], Chunks[,])을 지원하지 않으므로
// 1차원 래퍼 클래스를 사용하여 JSON 변환합니다.
//
// 사용법:
//   string json = MapSerializer.ToJson(map);
//   Map restored = MapSerializer.FromJson(json);
// ========================================================================

public static class MapSerializer
{
    // ── 직렬화: Map → JSON 문자열 ──
    public static string ToJson(Map map, bool prettyPrint = false)
    {
        var dto = MapToDto(map);
        return JsonUtility.ToJson(dto, prettyPrint);
    }

    // ── 역직렬화: JSON 문자열 → Map ──
    public static Map FromJson(string json)
    {
        var dto = JsonUtility.FromJson<MapDto>(json);
        return DtoToMap(dto);
    }

    // ── Map → DTO 변환 ──
    public static MapDto MapToDto(Map map)
    {
        var dto = new MapDto();

        if (map.floors == null)
        {
            dto.floors = new FloorDto[0];
            return dto;
        }

        dto.floors = new FloorDto[map.floors.Length];

        for (int f = 0; f < map.floors.Length; f++)
        {
            Floor floor = map.floors[f];
            var floorDto = new FloorDto();
            floorDto.config = floor.config;

            int w = floor.config.width;
            int h = floor.config.height;
            floorDto.chunksWidth = w;
            floorDto.chunksHeight = h;

            if (floor.chunks != null)
            {
                floorDto.chunks = new ChunksDto[w * h];
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        Chunks c = floor.chunks[x, y];
                        var cDto = new ChunksDto();
                        cDto.landform = c.landform;
                        cDto.roomId = c.roomId;
                        cDto.roomName = c.roomName;
                        cDto.roomRole = c.roomRole;
                        cDto.floorId = c.floorId;
                        cDto.occupationState = c.occupationState;
                        cDto.stairTargetFloor = c.stairTargetFloor;
                        cDto.allowMaxFootprint = c.allowMaxFootprint;
                        cDto.stairIsOpen = c.stairIsOpen;
                        cDto.stairHumanOnly = c.stairHumanOnly;

                        if (c.chunk != null)
                        {
                            int cs = c.chunk.GetLength(0);
                            cDto.tiles = new Tile[cs * cs];
                            for (int tx = 0; tx < cs; tx++)
                                for (int ty = 0; ty < cs; ty++)
                                    cDto.tiles[tx * cs + ty] = c.chunk[tx, ty];
                        }

                        floorDto.chunks[x * h + y] = cDto;
                    }
                }
            }
            else
            {
                floorDto.chunks = new ChunksDto[0];
            }

            // gates 변환
            if (floor.gates != null)
                floorDto.gates = floor.gates.ToArray();
            else
                floorDto.gates = new Gate[0];

            dto.floors[f] = floorDto;
        }

        return dto;
    }

    // ── DTO → Map 변환 ──
    public static Map DtoToMap(MapDto dto)
    {
        var map = new Map();

        if (dto.floors == null || dto.floors.Length == 0)
        {
            map.floors = new Floor[0];
            return map;
        }

        map.floors = new Floor[dto.floors.Length];

        for (int f = 0; f < dto.floors.Length; f++)
        {
            FloorDto floorDto = dto.floors[f];
            var floor = new Floor();
            floor.config = floorDto.config;

            int w = floorDto.chunksWidth;
            int h = floorDto.chunksHeight;

            if (floorDto.chunks != null && floorDto.chunks.Length == w * h)
            {
                floor.chunks = new Chunks[w, h];
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        ChunksDto cDto = floorDto.chunks[x * h + y];
                        var c = new Chunks();
                        c.landform = cDto.landform;
                        c.roomId = cDto.roomId;
                        c.roomName = cDto.roomName;
                        c.roomRole = cDto.roomRole;
                        c.floorId = cDto.floorId;
                        c.occupationState = cDto.occupationState;
                        c.stairTargetFloor = cDto.stairTargetFloor;
                        c.allowMaxFootprint = cDto.allowMaxFootprint;
                        c.stairIsOpen = cDto.stairIsOpen;
                        c.stairHumanOnly = cDto.stairHumanOnly;

                        // 청크 크기가 층별 설정값이 된 뒤(2026-08-23, 맵 1.5배 확장)로는 64 고정 대신
                        // 배열 길이의 정수 제곱근으로 실제 청크 크기를 역산한다 — floor.config는 바로
                        // 위에서 이미 복원됐으므로 floorDto.config.chunkSize를 그대로 믿어도 되지만,
                        // 저장 당시의 실제 배열 길이와 항상 정확히 일치시키기 위해 배열 자체에서 구한다.
                        if (cDto.tiles != null && cDto.tiles.Length > 0)
                        {
                            int cs = Mathf.RoundToInt(Mathf.Sqrt(cDto.tiles.Length));
                            if (cs * cs == cDto.tiles.Length)
                            {
                                c.chunk = new Tile[cs, cs];
                                for (int tx = 0; tx < cs; tx++)
                                    for (int ty = 0; ty < cs; ty++)
                                        c.chunk[tx, ty] = cDto.tiles[tx * cs + ty];
                            }
                        }

                        floor.chunks[x, y] = c;
                    }
                }
            }

            // gates 복원
            if (floorDto.gates != null && floorDto.gates.Length > 0)
                floor.gates = new System.Collections.Generic.List<Gate>(floorDto.gates);
            else
                floor.gates = new System.Collections.Generic.List<Gate>();

            map.floors[f] = floor;
        }

        return map;
    }

    // ================================================================
    // DTO 클래스 (JSON 직렬화용 1차원 래퍼)
    // ================================================================

    [Serializable]
    public class MapDto : IData
    {
        public FloorDto[] floors;
    }

    [Serializable]
    public class FloorDto
    {
        public FloorConfig config;
        public int chunksWidth;
        public int chunksHeight;
        public ChunksDto[] chunks;
        public Gate[] gates;
    }

    [Serializable]
    public class ChunksDto
    {
        public Tile[] tiles;  // 청크 크기(cs)×cs, tx*cs+ty 순서 — cs는 층별 FloorConfig.chunkSize.
        public int landform;
        public int roomId;
        public string roomName;
        public RoomRole roomRole;
        public int floorId;
        public OccupationState occupationState;
        public int stairTargetFloor;
        public int allowMaxFootprint;
        public bool stairIsOpen;
        public bool stairHumanOnly;
    }
}
 