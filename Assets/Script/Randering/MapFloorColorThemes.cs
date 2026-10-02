using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// MapFloorColorThemes.cs — 층별 색상 테마 배정표 (2026-09-27, 사용자 요청으로 단일 활성 테마에서
// 층별 배정으로 확장). Resources/MapColorTheme_FloorAssignments 경로에 단일 인스턴스로 저장되고,
// Tools(new)/맵/타일 색상 테마 창(MapColorThemeWindow)이 편집한다. MapRandering이 층마다
// GetThemeForFloor(floorIndex)로 조회해 그 층의 Tile.color에 적용한다(런타임 조정 아님, 맵 생성
// 시점에만 적용 — MapColorTheme.cs 참고).
// ============================================================================
public class MapFloorColorThemes : ScriptableObject
{
    [Serializable]
    public class FloorEntry
    {
        public int floorIndex;
        public MapColorTheme theme;
    }

    // 층별로 명시 배정이 없을 때 쓰는 테마 — 이것도 없으면 흰색(원본 스프라이트 그대로).
    public MapColorTheme defaultTheme;
    public List<FloorEntry> floorThemes = new List<FloorEntry>();

    // 최종 적용값 — 명시 배정이 있으면 그것, 없으면 defaultTheme(그것도 없으면 null → 흰색).
    public MapColorTheme GetThemeForFloor(int floorIndex)
    {
        MapColorTheme explicit_ = GetExplicitThemeForFloor(floorIndex);
        return explicit_ != null ? explicit_ : defaultTheme;
    }

    // 층 배정 UI가 "이 층에 명시적으로 지정된 게 있는지"만 보여줄 때 쓴다(기본값과 구분).
    public MapColorTheme GetExplicitThemeForFloor(int floorIndex)
    {
        if (floorThemes == null) return null;
        foreach (var entry in floorThemes)
            if (entry != null && entry.floorIndex == floorIndex && entry.theme != null)
                return entry.theme;
        return null;
    }
}
