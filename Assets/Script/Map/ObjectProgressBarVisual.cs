using UnityEngine;

// 03문서 9-7/9-8장/7-3장(2026-07-27 추가) — 함정 해제·코어 조사처럼 시간이 걸리는 상호작용의 진행률을
// 오브젝트 바로 아래 세계공간 막대로 표시한다(원래 함정 전용이었다가 코어 조사에도 재사용하며 이름을
// 일반화함). UnitVisual의 world-space TextMesh/LineRenderer와 같은 이유로 Screen Space Canvas 대신
// 이 오브젝트 트랜스폼의 자식 SpriteRenderer 두 장(배경/채움)으로 만든다 — 카메라 위치/배율과
// 무관하게 항상 오브젝트 아래에 붙어 있어야 하기 때문이다. 성공/실패/완료 결과 문구는 오브젝트
// 파괴와 동시에 사라지면 안 되므로 여기 자식으로 넣지 않고, UIManager.ShowFloatingTextAt으로 독립
// world-space 텍스트를 따로 띄운다(TacticalFSMState.TrapDisarmPerform/CoreInvestigatePerform 참고).
public class ObjectProgressBarVisual : MonoBehaviour
{
	private const float BarWidth = 0.8f;
	private const float BarHeight = 0.12f;
	private const float BarWorldOffsetBelowBottom = 0.45f;

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

		// 이 오브젝트(트랩/코어 스프라이트) 자신의 localScale이 타일 1칸을 채우려고 스프라이트별로
		// 제각각 스케일돼 있다(GameSession.SpawnObject 참고) — 그대로 자식에 물리면 막대가 스프라이트
		// 종류마다 다르게 늘어나 보이므로, 부모 스케일의 역수를 곱해 항상 같은 월드 크기로 보이게 한다
		// (UnitVisual.EnsureBelowLabel과 동일한 이유/관례).
		Vector3 parentScale = transform.localScale;
		float invX = parentScale.x != 0f ? 1f / parentScale.x : 1f;
		float invY = parentScale.y != 0f ? 1f / parentScale.y : 1f;

		_bg = CreateBarSprite("ProgressBarBg", new Color(0f, 0f, 0f, 0.6f), 29, new Vector2(0.5f, 0.5f));
		_fill = CreateBarSprite("ProgressBarFill", new Color(1f, 0.85f, 0.1f, 0.95f), 30, new Vector2(0f, 0.5f));

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

	private SpriteRenderer CreateBarSprite(string name, Color color, int sortingOrder, Vector2 pivot)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(transform, false);
		var sr = go.AddComponent<SpriteRenderer>();

		Texture2D tex = new Texture2D(4, 4);
		Color[] pixels = new Color[16];
		for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
		tex.SetPixels(pixels);
		tex.Apply();
		sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), pivot, 4f);
		sr.color = color;
		sr.sortingOrder = sortingOrder;
		return sr;
	}
}
