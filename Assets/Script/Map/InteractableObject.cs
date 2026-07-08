using UnityEngine;
using System;

[Serializable]
public class InteractableObject
{
    public string Id;
    public Vector3Int Position;
    public float BaseInterest;
    public bool IsCollected;

    public InteractableObject(string id, Vector3Int position, float baseInterest)
    {
        Id = id;
        Position = position;
        BaseInterest = baseInterest;
        IsCollected = false;
    }
}
