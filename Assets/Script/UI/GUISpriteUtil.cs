using UnityEngine;

// OnGUI(IMGUI)에서 스프라이트 시트의 서브스프라이트 하나를 지정한 화면 Rect에 맞춰 그리는 공용
// 헬퍼(2026-08-20) — WaveGaugePanel(웨이브 게이지/파티 아이콘)과 InputManager(몬스터 배치 실루엣)가
// 각자 동일한 UV 슬라이싱 로직을 중복 구현했던 것을 통합했다.
public static class GUISpriteUtil
{
    public static void Draw(Sprite sprite, Rect screenRect)
    {
        Texture2D tex = sprite.texture;
        Rect r = sprite.rect;
        Rect uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        GUI.DrawTextureWithTexCoords(screenRect, tex, uv);
    }
}
