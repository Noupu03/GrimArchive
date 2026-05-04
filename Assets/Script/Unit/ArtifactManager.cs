using UnityEngine;
using System.Collections.Generic;

public class ArtifactManager : MonoBehaviour
{
    /*public static ArtifactManager Instance;
    public List<ArtifactItem> artifacts = new List<ArtifactItem>();
    public bool artifactPlacementMode = false;

    void Awake()
    {
        Instance = this;
    }

    public void SpawnArtifact(Vector2Int pos, int floorIdx)
    {
        ArtifactItem art = new ArtifactItem { position = pos, floor = floorIdx, isPickedUp = false };

        GameObject go = new GameObject($"Artifact_{pos.x}_{pos.y}");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.yellow);
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0,0,1,1), new Vector2(0.5f,0.5f), 1f);
        sr.sortingOrder = 8; // 유닛(10) 밑

        go.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0);
        if (UnitGenerate.Instance != null)
        {
            go.transform.position += UnitGenerate.Instance.GetFloorOffset_Public(floorIdx);
        }
        art.visual = go;
        artifacts.Add(art);
        Debug.Log($"유물이 ({pos.x}, {pos.y}) 타일에 생성되었습니다.");
    }

    public void PickupArtifact(ArtifactItem art, Unit u)
    {
        art.isPickedUp = true;
        if (art.visual != null) Destroy(art.visual);
        u.hasArtifact = true;
        u.interactionTimer = 0f;
        Debug.Log($"{u.unitType.typeName}가 유물을 획득했습니다! (이동속도 저하, 빗나감/막기 저하, 공격 불가)");
    }
    */
}
