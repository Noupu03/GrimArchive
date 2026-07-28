using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;

public class ObjectSpawner
{
    public Dictionary<Vector3Int, InteractableObject> objectGrid { get; private set; } = new Dictionary<Vector3Int, InteractableObject>();
    private Dictionary<InteractableObject, GameObject> objectVisuals = new Dictionary<InteractableObject, GameObject>();

    private MapRandering _mapRandering;

    [Inject]
    public void Construct(MapRandering mapRandering)
    {
        _mapRandering = mapRandering;
    }

    public void SpawnObject(InteractableObject obj, Color color)
    {
        if (objectGrid.ContainsKey(obj.Position)) return;
        
        objectGrid[obj.Position] = obj;
        LogHelper.Log(LogHelper.GAME, $"Generated {obj.Id} at Floor {obj.Position.z}, {new Vector2Int(obj.Position.x, obj.Position.y)} with Tags: [{string.Join(", ", obj.Tags)}]");

        GameObject visual = new GameObject(obj.Id);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        
        Texture2D tex = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        sr.sprite = sprite;
        sr.sortingOrder = 5;
        
        Vector3 offset = Vector3.zero;
        if (_mapRandering != null)
        {
            if (_mapRandering.floorOffsets != null && obj.Position.z >= 0 && obj.Position.z < _mapRandering.floorOffsets.Length)
            {
                offset = _mapRandering.floorOffsets[obj.Position.z];
            }
            
            GameObject childTilemap = GameObject.Find($"F{obj.Position.z}_Tilemap");
            if (childTilemap != null)
            {
                visual.transform.SetParent(childTilemap.transform);
            }
        }
        
        visual.transform.position = new Vector3(obj.Position.x + 0.5f, obj.Position.y + 0.5f, 0f) + offset;
        visual.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        
        objectVisuals[obj] = visual;
    }

    public void CollectObject(Vector3Int pos)
    {
        if (objectGrid.ContainsKey(pos))
        {
            var obj = objectGrid[pos];
            obj.IsCollected = true;
            objectGrid.Remove(pos);

            if (objectVisuals.TryGetValue(obj, out GameObject visual))
            {
                GameObject.Destroy(visual);
                objectVisuals.Remove(obj);
            }
        }
    }
}
