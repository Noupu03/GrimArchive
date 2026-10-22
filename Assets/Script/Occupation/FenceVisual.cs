using UnityEngine;

// 목책 임시 스프라이트 — 전용 아트가 생기기 전까지 쓰는 자리표시자(코드로 그린 16×16 픽셀)이고 한 번만 만들어 모든 목책이 공유한다(텍스처 누수 방지).
// 뾰족한 나무 말뚝 네 개를 가로대 두 줄로 묶었고 말뚝 끝은 인류 진영 파랑이다. 칸 하나 크기이며 목책 묶음(1×2)은 칸마다 한 장씩 그린다.
public static class FenceVisual
{
	private const int Size = 16;
	private const float PixelsPerTile = 16f;

	private static Sprite _sprite;

	public static Sprite GetSprite()
	{
		if (_sprite != null) return _sprite;

		var wood = new Color32(168, 118, 64, 255);
		var woodDark = new Color32(104, 68, 34, 255);
		var rail = new Color32(206, 160, 96, 255);
		var cap = new Color32(64, 140, 255, 255);
		var clear = new Color32(0, 0, 0, 0);

		var pixels = new Color32[Size * Size];
		for (int y = 0; y < Size; y++)
		{
			for (int x = 0; x < Size; x++)
			{
				pixels[y * Size + x] = Pixel(x, y, wood, woodDark, rail, cap, clear);
			}
		}

		var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
		texture.SetPixels32(pixels);
		texture.Apply();
		return _sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerTile);
	}

	// 말뚝은 폭 3칸(x % 4 = 1~3)짜리 네 개이고 사이 한 칸(x % 4 = 0)은 틈이다. 가로대는 y = 4·9 두 줄이 틈까지 이어진다.
	private static Color32 Pixel(int x, int y, Color32 wood, Color32 woodDark, Color32 rail, Color32 cap, Color32 clear)
	{
		if (y == 4 || y == 9) return rail;             // 가로대 — 말뚝 사이 틈까지 끊기지 않는다

		int column = x % 4;
		if (column == 0) return clear;                 // 말뚝 사이 틈
		int post = x / 4;                              // 0~3번째 말뚝
		int top = 13 - (post % 2);                     // 이웃한 말뚝 높이를 어긋나게 해 울타리 윤곽에 리듬을 준다
		if (y < 1 || y > top) return clear;

		if (y == top) return column == 2 ? cap : clear;                   // 뾰족한 끝 — 한 칸짜리 파란 촉
		if (y == top - 1) return column == 2 ? cap : wood;                // 끝 아래 한 줄은 촉이 이어지고 양옆은 나무
		return column == 3 ? woodDark : wood;                             // 몸통 — 오른쪽 한 줄은 그늘
	}
}
