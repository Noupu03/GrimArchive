// ============================================================================
// WallAutoTileMath.cs — 벽 자동 타일 연결 순수 계산(부수효과 없음). MapRandering.RenderFloor가 8방향 인접 상태(+4방향 Floor 상태)를 넘겨 호출한다.
// ============================================================================

// 13종 벽 방향. TL/TR/BL/BR는 모서리가 존재하는 방향이고, Horizontal/Vertical은 벽 스프라이트가 상하/좌우 비대칭이라 Top/Bottom, Left/Right로 분화했다. FullySurrounded는 벽이 2칸 이상 두께라(CreateMap.TileWall.cs ApplyOuterWallThickness) 두꺼운 벽 안쪽 타일용 자리다.
public enum WallVariant
{
    HorizontalTop, HorizontalBottom,
    VerticalLeft, VerticalRight,
    OuterTL, OuterTR, OuterBL, OuterBR,
    InnerTL, InnerTR, InnerBL, InnerBR,
    FullySurrounded,
}

// 13종 방향이 공유하는 기준(0°) 스프라이트 형태 4종 + 방향 없는 내부 채움용 Solid 1종(형태별 기준 스프라이트 1개를 회전해 쓴다).
public enum WallShape { Horizontal, Vertical, Outer, Inner, Solid }

public static class WallAutoTileMath
{
    // 8방향 인접 상태로 13종 중 하나를 판정한다. 우선순위: 안쪽 모서리 > 바깥쪽 모서리 > 직선 > 예외(HorizontalTop 폴백).
    // isFloorN/S/E/W는 그 방향이 정확히 Floor 타입인지(미개척/다른 방/Stair는 false) — Vertical이 선택되는 조건이 이미 '동/서 둘 다 벽 아님'이라 벽 아님 여부만으로는 어느 쪽이 방 내부인지 가를 수 없어 필요하다.
    public static WallVariant SelectVariant(bool n, bool s, bool e, bool w, bool ne, bool nw, bool se, bool sw,
        bool isFloorN = false, bool isFloorS = false, bool isFloorE = false, bool isFloorW = false)
    {
        // 1) 안쪽 모서리 — 두 직교 방향이 막혀 있어도 그 사이 대각선이 비어 있으면 파인 코너.
        if (n && w && !nw) return WallVariant.InnerTL;
        if (n && e && !ne) return WallVariant.InnerTR;
        if (s && w && !sw) return WallVariant.InnerBL;
        if (s && e && !se) return WallVariant.InnerBR;

        // 2) 바깥쪽 모서리 — 인접한 두 직교 방향만 막혀 있고 나머지 두 방향은 완전히 비어 있는 전형적인 L자 코너.
        if (e && s && !n && !w) return WallVariant.OuterTL;
        if (w && s && !n && !e) return WallVariant.OuterTR;
        if (e && n && !s && !w) return WallVariant.OuterBL;
        if (w && n && !s && !e) return WallVariant.OuterBR;

        // 3) 완전히 막힘 — 4방향 전부 벽이고 코너 조건에도 안 걸린(대각선도 전부 막힌) 두꺼운 벽 안쪽 타일 — 방향 없는 채움용 스프라이트를 쓴다.
        if (n && s && e && w) return WallVariant.FullySurrounded;

        // 4) 직선(또는 Solid) — 코너/완전폐쇄가 아닌 나머지. 코너(1·2번)와 FullySurrounded(3번)는 벽 인접 기반 판정을 유지한다(Floor 인접 기반으로 바꾸면 두꺼운 벽에서 방 꼭짓점 타일이 카디널엔 Floor가 없고 대각선에만 있어 꼭짓점이 전부 Solid가 된다).
        // 이 자리만 Floor 인접 기반으로 판정한다 — 벽 인접 조건(세로 연결 + 가로 비연결)은 ApplyOuterWallThickness가 최소 2칸 두께를 보장해 e/w(n/s)가 거의 항상 true라 거의 모든 직선 벽이 Horizontal로 폴백했다. '정확히 한쪽에만 Floor가 있는 축'으로 방향을 정하면 반대쪽이 벽 두께든 미개척 공간이든 무관하다.
        // **어느 축에도 명확한 Floor 분기가 없으면(둘 다 Floor이거나 둘 다 아님) Horizontal 폴백이 아니라 Solid로 처리한다** — 두꺼운 벽이 방 Floor가 아니라 미개척 공간/층 경계와 맞닿을 때 방향 무늬가 반복돼 층 모서리 톱니처럼 보이던 원인이다.
        bool verticalFloorSplit = isFloorE != isFloorW;
        bool horizontalFloorSplit = isFloorN != isFloorS;
        if (verticalFloorSplit && !horizontalFloorSplit)
            return isFloorW ? WallVariant.VerticalRight : WallVariant.VerticalLeft;
        if (horizontalFloorSplit)
            return isFloorN ? WallVariant.HorizontalBottom : WallVariant.HorizontalTop;
        return WallVariant.FullySurrounded;
    }

    public static WallShape GetShape(WallVariant variant) => variant switch
    {
        WallVariant.HorizontalTop or WallVariant.HorizontalBottom => WallShape.Horizontal,
        WallVariant.VerticalLeft or WallVariant.VerticalRight => WallShape.Vertical,
        WallVariant.OuterTL or WallVariant.OuterTR or WallVariant.OuterBL or WallVariant.OuterBR => WallShape.Outer,
        WallVariant.FullySurrounded => WallShape.Solid,
        _ => WallShape.Inner,
    };

    // 참고 이미지(Assets/KakaoTalk_20260927_144913940.png)를 0°로 고정하고 TL→BL→BR→TR 순으로 90°씩 반시계 회전(Unity 2D 양의 Z회전)한다고 가정했다. **실물 아트로 아직 검증 안 됨** — 시계/반시계가 반대면 이 표만 뒤집는다. FullySurrounded는 방향이 없어 항상 0°.
    public static float GetRotationDegrees(WallVariant variant) => variant switch
    {
        WallVariant.HorizontalTop or WallVariant.VerticalLeft or WallVariant.OuterTL or WallVariant.InnerTL => 0f,
        WallVariant.OuterBL or WallVariant.InnerBL => 90f,
        WallVariant.HorizontalBottom or WallVariant.VerticalRight or WallVariant.OuterBR or WallVariant.InnerBR => 180f,
        WallVariant.OuterTR or WallVariant.InnerTR => 270f,
        _ => 0f,
    };

    // TileSpriteLibrary.spriteLib의 'Wall' 카테고리에서 찾을 라벨 — 형태별 5라벨(Wall_Horizontal/Wall_Vertical/Wall_Outer_TL/Wall_Inner_TL/Wall_Solid)만 두고 방향이 있는 나머지는 이 라벨을 회전시켜 만든다.
    public static string GetSpriteLibraryLabel(WallVariant variant) => GetShape(variant) switch
    {
        WallShape.Horizontal => "Wall_Horizontal",
        WallShape.Vertical => "Wall_Vertical",
        WallShape.Outer => "Wall_Outer_TL",
        WallShape.Solid => "Wall_Solid",
        _ => "Wall_Inner_TL",
    };
}
