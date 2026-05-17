using UnityEngine;

public class OutlineSpriteSync : MonoBehaviour
{
    private SpriteRenderer outlineSr;
    private SpriteRenderer sourceSr;

    public void Init(SpriteRenderer source)
    {
        outlineSr = GetComponent<SpriteRenderer>();
        sourceSr = source;
    }

    void LateUpdate()
    {
        if (outlineSr == null || sourceSr == null) return;
        if (outlineSr.sprite == sourceSr.sprite && outlineSr.flipX == sourceSr.flipX) return;

        outlineSr.sprite = sourceSr.sprite;
        outlineSr.flipX = sourceSr.flipX;
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (outlineSr.sprite == null) return;

        // 스프라이트 피벗이 중심이 아닐 때 1.2x 스케일이 한쪽으로 솟는 문제 보정.
        // 아웃라인의 시각적 중심 = localPos + center * scale
        // 원본의 시각적 중심 = center * 1.0
        // 두 중심이 일치하려면: localPos = -center * (scale - 1)
        Vector2 center = outlineSr.sprite.bounds.center;
        if (outlineSr.flipX) center.x = -center.x;
        float sx = transform.localScale.x;
        float sy = transform.localScale.y;
        transform.localPosition = new Vector3(-center.x * (sx - 1f), -center.y * (sy - 1f), 0f);
    }
}
