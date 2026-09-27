// ============================================================================
// WallAutoTileMath.cs — 벽 자동 타일 연결 순수 계산 함수 (회의록 2026-09-27, 정림 전달안 기반).
// 부수효과 없음, MapRandering.RenderFloor가 8방향 인접 상태(+4방향 Floor 상태)를 넘겨 호출한다.
// ============================================================================

// 13종 벽 방향. TL/TR/BL/BR는 "모서리가 존재하는 방향"(회의록 방향 규칙 그대로).
// Horizontal/Vertical은 2026-09-27 후속 회의록 요청으로 Top/Bottom, Left/Right로 분화됐다 —
// 예전엔 방향 미분화 단일 케이스였는데, 벽 스프라이트가 상하/좌우 비대칭이라 구분이 필요해졌다.
// FullySurrounded는 같은 날 밤 추가 요청(정림: "모든 면이 막혀있는 타일... 걔네용 자리 한 칸") —
// GrimArchive의 벽이 실제로는 2칸 이상 두께라(CreateMap.TileWall.cs의 ApplyOuterWallThickness),
// 두꺼운 벽 안쪽 타일 다수가 이 케이스에 해당하는데 예전엔 전부 HorizontalTop으로 잘못 분류됐었다.
public enum WallVariant
{
    HorizontalTop, HorizontalBottom,
    VerticalLeft, VerticalRight,
    OuterTL, OuterTR, OuterBL, OuterBR,
    InnerTL, InnerTR, InnerBL, InnerBR,
    FullySurrounded,
}

// 13종 방향이 공유하는 기준(0°) 스프라이트 형태 — 회의록 후속 요청(2026-09-27 5:33) "형태별 기준
// 스프라이트 1개 + 회전"의 그 "형태" 4종 + 방향 없는 내부 채움용 Solid 1종.
public enum WallShape { Horizontal, Vertical, Outer, Inner, Solid }

public static class WallAutoTileMath
{
    // 8방향 인접 상태(N/S/E/W + 대각선)로 13종 중 하나를 판정한다.
    // 우선순위: 안쪽 모서리 > 바깥쪽 모서리 > 직선 > 예외(HorizontalTop 폴백) — 회의록 "우선순위" 절 그대로.
    // isFloorN/S/E/W: 그 방향이 정확히 Floor 타입 타일인지(벽 아님과는 다르다 — 미개척/다른방/Stair는
    // false) — 직선 구간의 상하/좌우 방향(어느 쪽이 방 내부인지) 판정에 필요하다. Vertical이 선택되는
    // 조건 자체가 "동/서 둘 다 벽 아님"이라, 벽 아님 여부만으로는 방향을 못 가른다.
    public static WallVariant SelectVariant(bool n, bool s, bool e, bool w, bool ne, bool nw, bool se, bool sw,
        bool isFloorN = false, bool isFloorS = false, bool isFloorE = false, bool isFloorW = false)
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

        // 3) 완전히 막힘 — 4방향 전부 벽이고(위 1·2번 코너 조건에 전부 안 걸렸다는 뜻은 대각선도 전부
        // 막혀 있다는 뜻) 두꺼운 벽 안쪽 깊숙한 타일. 2026-09-27 후속 요청(정림: "모든 면이 막혀있는
        // 타일... 자리 한 칸") — 방향성이 없는 채움용 스프라이트를 쓴다.
        if (n && s && e && w) return WallVariant.FullySurrounded;

        // 4) 직선(또는 Solid) — 코너/완전폐쇄가 아닌 나머지. 코너(1·2번)와 FullySurrounded(3번)는
        // 원래 벽-인접 기반 판정 그대로 둔다 — 실제로는 문제없이 잘 동작하고 있었다(2026-09-27 밤,
        // 사용자 확인: "방의 꼭짓점 부분까지 solid 처리했더라" — Floor 인접 기반으로 코너까지 다시
        // 판정하려던 시도를 되돌림. 두꺼운 벽에서는 방의 진짜 꼭짓점 타일도 카디널 방향엔 Floor가 없고
        // 대각선에만 있어서, "카디널에 Floor 없으면 Solid"로 코너까지 덮어버리면 꼭짓점이 전부 Solid가
        // 되는 부작용이 있었다).
        // 이 4번 자리(코너도 FullySurrounded도 아닌 나머지)만 Floor 인접 기반으로 판정한다. 예전엔
        // "세로축(n|s) 연결 + 가로축(e|w) 비연결"이라는 순수 벽-인접 조건으로 Vertical을 골랐는데,
        // CreateMap.TileWall.cs의 ApplyOuterWallThickness가 StartRoom/BossRoom을 뺀 모든 방 벽에
        // `minGuaranteed = Mathf.Max(thkMin, 2)`로 최소 2칸 두께를 보장해서 e/w(또는 n/s)가 거의 항상
        // true라, 이 조건이 사실상 거의 만족되지 않아 거의 모든 직선 벽이 Horizontal로 잘못
        // 폴백하고 있었다. 대신 "정확히 한쪽에만 Floor가 있는 축"으로 방향을 정한다 — 반대쪽이 벽
        // 두께든 미개척 공간(층 경계 바깥 등)이든 무관하게 판정 가능하다. **어느 축에도 명확한 Floor
        // 분기가 없으면(둘 다 Floor거나 둘 다 아니면) Horizontal 기본 폴백이 아니라 Solid로 처리한다**
        // (2026-09-27 밤 — 여기가 실제 "층 모서리 톱니"의 원인이었다: 두꺼운 벽이 방 Floor가 아니라
        // 미개척 공간/층 경계와 맞닿을 때 옛 FullySurrounded 조건(4방향 전부 벽)을 못 만족해 Horizontal로
        // 폴백하며 방향성 있는 무늬가 반복돼 톱니처럼 보였다 — 진짜 방 코너와는 다른 자리).
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

    // 참고 이미지(Assets/KakaoTalk_20260927_144913940.png — "왼쪽위 바깥모서리, 가로면 위쪽, 세로면
    // 왼쪽" 원화)를 0°로 고정하고, TL→BL→BR→TR 순으로 90°씩 반시계 회전(Unity 2D 기본 카메라 기준
    // 양의 Z회전=반시계)한다고 가정했다. **아직 실물 아트로 검증 안 됨** — 시계/반시계가 반대로
    // 보이면 이 표만 뒤집으면 된다(2026-09-27, 알려진 한계로 기록).
    // FullySurrounded는 방향성이 없는 채움용이라 회전 자체가 무의미 — 항상 0°.
    public static float GetRotationDegrees(WallVariant variant) => variant switch
    {
        WallVariant.HorizontalTop or WallVariant.VerticalLeft or WallVariant.OuterTL or WallVariant.InnerTL => 0f,
        WallVariant.OuterBL or WallVariant.InnerBL => 90f,
        WallVariant.HorizontalBottom or WallVariant.VerticalRight or WallVariant.OuterBR or WallVariant.InnerBR => 180f,
        WallVariant.OuterTR or WallVariant.InnerTR => 270f,
        _ => 0f,
    };

    // TileSpriteLibrary.spriteLib의 "Wall" 카테고리에서 찾을 라벨 이름 — 2026-09-27 후속 요청으로
    // 방향별 10라벨에서 형태별 4라벨(+같은 날 밤 Solid 1라벨 추가, 총 5라벨)로 축소됐다
    // (Wall_Horizontal/Wall_Vertical/Wall_Outer_TL/Wall_Inner_TL/Wall_Solid만 남음 — 방향이 있는
    // 나머지는 이 라벨들을 회전시켜 만든다).
    public static string GetSpriteLibraryLabel(WallVariant variant) => GetShape(variant) switch
    {
        WallShape.Horizontal => "Wall_Horizontal",
        WallShape.Vertical => "Wall_Vertical",
        WallShape.Outer => "Wall_Outer_TL",
        WallShape.Solid => "Wall_Solid",
        _ => "Wall_Inner_TL",
    };
}
