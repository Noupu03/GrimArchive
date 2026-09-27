// ============================================================================
// WallAutoTileMath.cs — 벽 자동 타일 연결 순수 계산 함수 (회의록 2026-09-27, 정림 전달안 기반).
// 부수효과 없음, MapRandering.RenderFloor가 8방향 인접 상태를 넘겨 호출한다.
// ============================================================================

// 10종 벽 스프라이트 형태. TL/TR/BL/BR는 "모서리가 존재하는 방향"(회의록 방향 규칙 그대로).
public enum WallVariant
{
    Horizontal, Vertical,
    OuterTL, OuterTR, OuterBL, OuterBR,
    InnerTL, InnerTR, InnerBL, InnerBR,
}

public static class WallAutoTileMath
{
    // 8방향 인접 상태(N/S/E/W + 대각선)로 10종 중 하나를 판정한다.
    // 우선순위: 안쪽 모서리 > 바깥쪽 모서리 > 직선 > 예외(Horizontal 폴백) — 회의록 "우선순위" 절 그대로.
    public static WallVariant SelectVariant(bool n, bool s, bool e, bool w, bool ne, bool nw, bool se, bool sw)
    {
        // 1) 안쪽 모서리 — 두 직교 방향이 막혀 있어도 그 사이 대각선이 비어 있으면 파인 코너.
        // (회의록 예시: "위쪽과 왼쪽에 벽이 있어도 좌상단 대각선이 비어 있으면 안쪽 모서리")
        if (n && w && !nw) return WallVariant.InnerTL;
        if (n && e && !ne) return WallVariant.InnerTR;
        if (s && w && !sw) return WallVariant.InnerBL;
        if (s && e && !se) return WallVariant.InnerBR;

        // 2) 바깥쪽 모서리 — 인접한 두 직교 방향만 막혀 있고 나머지 두 방향은 완전히 비어 있는 전형적인 L자 코너.
        if (e && s && !n && !w) return WallVariant.OuterTL;
        if (w && s && !n && !e) return WallVariant.OuterTR;
        if (e && n && !s && !w) return WallVariant.OuterBL;
        if (w && n && !s && !e) return WallVariant.OuterBR;

        // 3) 직선 — 세로축 연결이 있고 가로축 연결이 없을 때만 Vertical, 그 외(순수 가로/양쪽 모두/고립 타일
        // 등 10종 밖의 예외 상태)는 전부 Horizontal로 폴백한다(회의록 "예외 상태 처리").
        if ((n || s) && !(e || w)) return WallVariant.Vertical;
        return WallVariant.Horizontal;
    }

    // TileSpriteLibrary.spriteLib의 "Wall" 카테고리에서 찾을 라벨 이름 — 회의록에 명시된 이름 그대로
    // 고정한다(정림이 실제 아트를 채울 때 이 문자열과 정확히 일치하는 라벨을 만들어야 함).
    public static string GetSpriteLibraryLabel(WallVariant variant) => variant switch
    {
        WallVariant.Horizontal => "Wall_Horizontal",
        WallVariant.Vertical => "Wall_Vertical",
        WallVariant.OuterTL => "Wall_Outer_TL",
        WallVariant.OuterTR => "Wall_Outer_TR",
        WallVariant.OuterBL => "Wall_Outer_BL",
        WallVariant.OuterBR => "Wall_Outer_BR",
        WallVariant.InnerTL => "Wall_Inner_TL",
        WallVariant.InnerTR => "Wall_Inner_TR",
        WallVariant.InnerBL => "Wall_Inner_BL",
        WallVariant.InnerBR => "Wall_Inner_BR",
        _ => "Wall_Horizontal",
    };
}
