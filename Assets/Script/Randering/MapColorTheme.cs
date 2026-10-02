using UnityEngine;

// ============================================================================
// MapColorTheme.cs — 벽/바닥 스프라이트 색상 초기값 프리셋. 런타임 조정이 아니라 '같은 스프라이트로 다른 테마'를 표현하는 생성 시점 틴트값이며, 여러 개 만들어 보관(CreateAssetMenu)하고 Tools(new)의 '맵 타일 색상 테마' 창에서 층별로 배정한다(MapFloorColorThemes).
// ============================================================================
[CreateAssetMenu(fileName = "MapColorTheme", menuName = "GrimArchive/Map Color Theme")]
public class MapColorTheme : ScriptableObject
{
    public string themeName = "Default";
    public Color wallColor = Color.white;
    public Color floorColor = Color.white;
}
