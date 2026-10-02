using UnityEngine;

// ============================================================================
// MapColorTheme.cs — 벽/바닥 스프라이트 색상 초기값 프리셋 (회의록 2026-09-27, 정림 요청).
// 런타임 조정 기능이 아니라 "같은 스프라이트로 다른 테마를 표현"하기 위한 생성 시점 틴트값이다.
// MapRandering.BuildTileCache가 Resources/MapColorTheme_Active를 로드해 Tile.color로 적용한다.
// 여러 개 만들어 보관해도 되고(Assets 아무 곳이나, CreateAssetMenu로 생성),
// Tools(new)/맵/타일 색상 테마 창에서 그중 하나를 활성 테마로 지정한다.
// ============================================================================
[CreateAssetMenu(fileName = "MapColorTheme", menuName = "GrimArchive/Map Color Theme")]
public class MapColorTheme : ScriptableObject
{
    public string themeName = "Default";
    public Color wallColor = Color.white;
    public Color floorColor = Color.white;
}
