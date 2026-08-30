using UnityEngine;

// 함정 해제·코어 조사처럼 시간이 걸리는 상호작용의 진행률을 오브젝트 바로 아래 세계공간 막대로
// 표시한다. 카메라 위치/배율과 무관하게 항상 오브젝트 아래에 붙어야 해서 Screen Space Canvas 대신
// 자식 SpriteRenderer 두 장(배경/채움)으로 만든다. 성공/실패 결과 문구는 오브젝트 파괴와 동시에
// 사라지면 안 되므로 여기 자식으로 넣지 않고 UIManager.ShowFloatingTextAt으로 독립 표시한다.
public class ObjectProgressBarVisual : MonoBehaviour
{
	private const float BarWidth = 0.8f;
	private const float BarHeight = 0.12f;
	private const float BarWorldOffsetBelowBottom = 0.45f;

	// 배경/채움 스프라이트를 모든 인스턴스가 공유 — GameObject.Destroy가 SpriteRenderer의 Texture2D를
	// 해제하지 않아 파괴→재설치를 반복하는 문 오브젝트에서 텍스처가 누적되는 누수가 있었다.
	private static Sprite _sharedCenterPivotSprite;
	private static Sprite _sharedLeftPivotSprite;

	private SpriteRenderer _bg;
	private SpriteRenderer _fill;
	private float _fillBaseScaleX;
	private float _fillBaseScaleY;

	public void SetProgress(float progress01, bool visible)
	{
		EnsureBar();
		_bg.enabled = visible;
		_fill.enabled = visible;
		if (!visible) return;

		float clamped = Mathf.Clamp01(progress01);
		_fill.transform.localScale = new Vector3(_fillBaseScaleX * clamped, _fillBaseScaleY, 1f);
	}

	private void EnsureBar()
	{
		if (_bg != null) return;

		// 이 오브젝트의 localScale이 타일 1칸을 채우려고 스프라이트별로 제각각이라 그대로 자식에 물리면
		// 막대 크기가 종류마다 달라진다 — 부모 스케일의 역수를 곱해 항상 같은 월드 크기로 보이게 한다.
		Vector3 parentScale = transform.localScale;
		float invX = parentScale.x != 0f ? 1f / parentScale.x : 1f;
		float invY = parentScale.y != 0f ? 1f / parentScale.y : 1f;

		_bg = CreateBarSprite("ProgressBarBg", new Color(0f, 0f, 0f, 0.6f), 29, GetSharedSprite(centerPivot: true));
		_fill = CreateBarSprite("ProgressBarFill", new Color(1f, 0.85f, 0.1f, 0.95f), 30, GetSharedSprite(centerPivot: false));

		Vector3 belowCenter = new Vector3(0f, -BarWorldOffsetBelowBottom * invY, 0f);

		_bg.transform.localPosition = belowCenter;
		_bg.transform.localScale = new Vector3(BarWidth * invX, BarHeight * invY, 1f);

		_fillBaseScaleX = BarWidth * invX;
		_fillBaseScaleY = BarHeight * invY;
		_fill.transform.localPosition = belowCenter + new Vector3(-BarWidth * invX / 2f, 0f, 0f);
		_fill.transform.localScale = new Vector3(0f, _fillBaseScaleY, 1f);

		_bg.enabled = false;
		_fill.enabled = false;
	}

	private SpriteRenderer CreateBarSprite(string name, Color color, int sortingOrder, Sprite sprite)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(transform, false);
		var sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = sprite;
		sr.color = color;
		sr.sortingOrder = sortingOrder;
		return sr;
	}

	private static Sprite GetSharedSprite(bool centerPivot)
	{
		if (centerPivot)
			return _sharedCenterPivotSprite ??= CreateWhiteSprite(new Vector2(0.5f, 0.5f));
		return _sharedLeftPivotSprite ??= CreateWhiteSprite(new Vector2(0f, 0.5f));
	}

	private static Sprite CreateWhiteSprite(Vector2 pivot)
	{
		Texture2D tex = new Texture2D(4, 4);
		Color[] pixels = new Color[16];
		for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
		tex.SetPixels(pixels);
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, 4, 4), pivot, 4f);
	}
}
