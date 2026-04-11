// ============================================================================
// CreateMap.Gizmos.cs — 에디터 기즈모 시각화
// ----------------------------------------------------------------------------
// 역할: Unity Editor Scene 뷰에서 맵 디버그 정보를 시각화.
//       방 영역 색상 표시(DrawGizmoRoomBounds), 통로 위치(DrawGizmoPassages),
//       계단 위치(DrawGizmoStairs), 방 라벨(DrawGizmoRoomLabels).
//       UNITY_EDITOR 전처리기로 빌드에서 제외된다.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public partial class CreateMap
{
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        if (map.floors == null || currentFloorIndex < 0 || currentFloorIndex >= map.floors.Length) return;

        Floor floor = map.floors[currentFloorIndex];
        if (floor.chunks == null) return;

        int w = floor.config.width;
        int h = floor.config.height;

        Vector3 origin = transform.position;

        // MapRandering의 계단 정렬 오프셋이 있으면 적용하여 타일맵과 Gizmo 좌표를 일치시킴
        var mr = Object.FindObjectOfType<MapRandering>();
        if (mr != null)
        {
            Transform childTilemap = mr.transform.Find($"F{currentFloorIndex}_Tilemap");
            if (childTilemap != null)
            {
                origin += childTilemap.localPosition;
            }
            else if (mr.floorOffsets != null && currentFloorIndex < mr.floorOffsets.Length)
            {
                Vector3Int offset = mr.floorOffsets[currentFloorIndex];
                origin += new Vector3(offset.x, offset.y, 0f);
            }
        }

        if (gizmoShowRoomBounds)
            DrawGizmoRoomBounds(ref floor, w, h, origin);

        if (gizmoShowPassages)
            DrawGizmoPassages(ref floor, w, h, origin);

        if (gizmoShowStairs)
            DrawGizmoStairs(ref floor, w, h, origin);

        if (gizmoShowRoomLabels)
            DrawGizmoRoomLabels(ref floor, w, h, origin);
    }

    void DrawGizmoRoomBounds(ref Floor floor, int w, int h, Vector3 origin)
    {
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                Vector3 center = origin + new Vector3(x * 8 + 4f, y * 8 + 4f, 0f);
                Vector3 size = new Vector3(8f, 8f, 0f);

                Color col = GetGizmoRoomColor(c.roomRole, c.roomId);
                col.a = 0.25f;
                Gizmos.color = col;
                Gizmos.DrawCube(center, size);

                col.a = 0.6f;
                Gizmos.color = col;
                Gizmos.DrawWireCube(center, size);
            }
        }
    }

    void DrawGizmoPassages(ref Floor floor, int w, int h, Vector3 origin)
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.9f);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomId < 0 || c.chunk == null) continue;

                if (x + 1 < w && floor.chunks[x + 1, y].roomId >= 0)
                {
                    int idA = c.roomId;
                    int idB = floor.chunks[x + 1, y].roomId;
                    if (idA != idB)
                    {
                        if (c.chunk[7, 3].name == "Floor" || c.chunk[7, 4].name == "Floor")
                        {
                            Vector3 passagePos = origin + new Vector3(x * 8 + 7.5f, y * 8 + 3.5f, 0f);
                            Gizmos.DrawCube(passagePos, new Vector3(1f, 2f, 0f));
                        }
                    }
                }

                if (y + 1 < h && floor.chunks[x, y + 1].roomId >= 0)
                {
                    int idA = c.roomId;
                    int idB = floor.chunks[x, y + 1].roomId;
                    if (idA != idB)
                    {
                        if (c.chunk[3, 7].name == "Floor" || c.chunk[4, 7].name == "Floor")
                        {
                            Vector3 passagePos = origin + new Vector3(x * 8 + 3.5f, y * 8 + 7.5f, 0f);
                            Gizmos.DrawCube(passagePos, new Vector3(2f, 1f, 0f));
                        }
                    }
                }
            }
        }
    }

    void DrawGizmoStairs(ref Floor floor, int w, int h, Vector3 origin)
    {
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.stairTargetFloor >= 0)
                {
                    Gizmos.color = c.stairIsOpen
                        ? new Color(0.3f, 0.5f, 1f, 0.9f)
                        : new Color(0.5f, 0.5f, 0.5f, 0.7f);
                    Vector3 stairCenter = origin + new Vector3(x * 8 + 3.5f, y * 8 + 3.5f, 0f);
                    Gizmos.DrawWireSphere(stairCenter, 1.5f);
                    Gizmos.DrawCube(stairCenter, new Vector3(2f, 2f, 0f));
                }
            }
        }
    }

    void DrawGizmoRoomLabels(ref Floor floor, int w, int h, Vector3 origin)
    {
        var roomFirstChunk = new Dictionary<int, (int x, int y)>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                int id = floor.chunks[x, y].roomId;
                if (id >= 0 && !roomFirstChunk.ContainsKey(id))
                    roomFirstChunk[id] = (x, y);
            }

        var labelStyle = new GUIStyle()
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        foreach (var kvp in roomFirstChunk)
        {
            int id = kvp.Key;
            var (cx, cy) = kvp.Value;
            Chunks c = floor.chunks[cx, cy];

            string roleChar = "";
            switch (c.roomRole)
            {
                case RoomRole.StartRoom:      roleChar = "S"; break;
                case RoomRole.BossRoom:       roleChar = "B"; break;
                case RoomRole.SubPurposeRoom: roleChar = "P"; break;
                case RoomRole.NormalRoom:     roleChar = "N"; break;
            }

            string label = $"{roleChar}{id}\nF{c.allowMaxFootprint}";
            Vector3 labelPos = origin + new Vector3(cx * 8 + 4f, cy * 8 + 4f, 0f);
            UnityEditor.Handles.Label(labelPos, label, labelStyle);
        }
    }

    static Color GetGizmoRoomColor(RoomRole role, int roomId)
    {
        switch (role)
        {
            case RoomRole.StartRoom:      return new Color(0.3f, 0.9f, 0.3f);
            case RoomRole.BossRoom:       return new Color(0.9f, 0.2f, 0.2f);
            case RoomRole.SubPurposeRoom: return new Color(0.9f, 0.7f, 0.2f);
            case RoomRole.NormalRoom:     return new Color(0.5f, 0.6f, 0.85f);
            default:
                return roomId >= 0
                    ? new Color(0.4f, 0.4f, 0.4f)
                    : new Color(0.15f, 0.15f, 0.15f);
        }
    }
#endif
}
