using System.Collections.Generic;
using UnityEngine;

// 기초 시설(인류 문·목책)을 새로 설치하는 동안만 보이는 청사진 — 문 줄과 목책 줄의 칸마다 점선 청사진을 깔고 진행도만큼 아래에서 채운다. 설치가 끝나 실제 문·목책이 서면 번쩍이며 사라진다(코어 점령 v0.2 12장에 연출 규정이 없어 연출 판단).
// 오브젝트 로직이 없는 순수 비주얼이라 이동·인지에 영향을 주지 않는다. 스프라이트는 BaseSiteVisual의 청사진 자리표시자를 공유한다. 문은 인류 파랑, 목책은 나무색이다.
public sealed class BaseGateBlueprint : MonoBehaviour
{
	private const int OutlineOrder = 3;     // 바닥 장식 — 방 윤곽선(1) 위, 오브젝트(5) 아래 (BaseSiteVisual 청사진과 같다)
	private const int FillOrder = 4;
	private const float FinishSeconds = 0.5f;

	private static readonly Color DoorTint = new Color(0.45f, 0.75f, 1f, 1f);
	private static readonly Color FenceTint = new Color(1f, 0.78f, 0.42f, 1f);

	private struct Cell
	{
		public SpriteRenderer Outline;
		public SpriteRenderer Fill;
		public Color Tint;
	}

	private readonly List<Cell> _cells = new List<Cell>();
	private float _progress;
	private bool _finishing;
	private float _finishT;

	// doorCenters / fenceCenters = 칸마다 월드 중심(층 오프셋 포함).
	public static BaseGateBlueprint Create(Transform parent, IReadOnlyList<Vector3> doorCenters, IReadOnlyList<Vector3> fenceCenters)
	{
		var go = new GameObject("BaseGateBlueprint");
		if (parent != null) go.transform.SetParent(parent);
		var blueprint = go.AddComponent<BaseGateBlueprint>();
		for (int i = 0; i < doorCenters.Count; i++) blueprint.AddCell(doorCenters[i], DoorTint);
		for (int i = 0; i < fenceCenters.Count; i++) blueprint.AddCell(fenceCenters[i], FenceTint);
		return blueprint;
	}

	private void AddCell(Vector3 center, Color tint)
	{
		var cell = new GameObject("Cell");
		cell.transform.SetParent(transform, false);
		cell.transform.position = center;

		var outline = CreateChild(cell.transform, "Outline", OutlineOrder, BaseSiteVisual.GetBlueprintSprite(0), Vector3.zero);
		outline.color = tint;

		var fill = CreateChild(cell.transform, "Fill", FillOrder, BaseSiteVisual.GetFillSprite(), new Vector3(0f, -0.5f, 0f));
		fill.color = new Color(tint.r, tint.g, tint.b, 0.3f);
		fill.transform.localScale = new Vector3(1f, 0f, 1f);

		_cells.Add(new Cell { Outline = outline, Fill = fill, Tint = tint });
	}

	private static SpriteRenderer CreateChild(Transform parent, string childName, int sortingOrder, Sprite sprite, Vector3 localPosition)
	{
		var go = new GameObject(childName);
		go.transform.SetParent(parent, false);
		go.transform.localPosition = localPosition;
		var sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = sprite;
		sr.sortingOrder = sortingOrder;
		return sr;
	}

	public void SetVisible(bool visible)
	{
		if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
	}

	// 진행도 0~1 — 칸마다 채움이 아래에서 위로 오른다.
	public void SetProgress(float progress01)
	{
		if (_finishing) return;
		_progress = Mathf.Clamp01(progress01);
		foreach (var cell in _cells)
			cell.Fill.transform.localScale = new Vector3(1f, _progress, 1f);
	}

	// 청사진을 걷는다. celebrate면 채움이 가득 찬 채 옅어지며 사라지고 스스로 파괴된다(안개 속처럼 보이지 않는 곳은 곧바로 파괴).
	public void Finish(bool celebrate)
	{
		if (!celebrate || !gameObject.activeInHierarchy) { Destroy(gameObject); return; }
		if (_finishing) return;
		_finishing = true;
		_finishT = 0f;
		foreach (var cell in _cells)
			cell.Fill.transform.localScale = Vector3.one;
	}

	private void Update()
	{
		if (_finishing)
		{
			_finishT += Time.deltaTime;
			float t = Mathf.Clamp01(_finishT / FinishSeconds);
			foreach (var cell in _cells)
			{
				cell.Outline.color = new Color(cell.Tint.r, cell.Tint.g, cell.Tint.b, (1f - t) * 0.9f);
				cell.Fill.color = new Color(cell.Tint.r, cell.Tint.g, cell.Tint.b, (1f - t) * 0.3f);
			}
			if (t >= 1f) Destroy(gameObject);
			return;
		}

		float pulse = 0.75f + 0.2f * Mathf.Sin(Time.time * 2.2f);
		foreach (var cell in _cells)
			cell.Outline.color = new Color(cell.Tint.r, cell.Tint.g, cell.Tint.b, pulse);
	}
}
