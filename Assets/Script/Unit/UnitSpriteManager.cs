using UnityEngine;
using System.Collections.Generic;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

public class UnitSpriteManager : MonoBehaviour
{
    public static UnitSpriteManager Instance { get; private set; }

    [System.Serializable]
    public class UnitTypeSpriteLibrary
    {
        public string unitTypeName;
#if UNITY_2022_2_OR_NEWER
        public SpriteLibraryAsset spriteLibraryAsset;
#endif
        [Header("Animation Clips")]
        public AnimationClip idleClip;
        public AnimationClip walkClip;
    }

    public List<UnitTypeSpriteLibrary> unitTypeSpriteLibraryMap = new List<UnitTypeSpriteLibrary>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

#if UNITY_2022_2_OR_NEWER
    public SpriteLibraryAsset GetSpriteLibrary(UnitType unitType)
    {
        if (unitType == null) return null;
        foreach (var lib in unitTypeSpriteLibraryMap)
            if (lib.unitTypeName == unitType.typeName && lib.spriteLibraryAsset != null)
                return lib.spriteLibraryAsset;
        Debug.LogWarning($"스프라이트 라이브러리를 찾을 수 없습니다: {unitType.typeName}");
        return null;
    }
#else
    public SpriteLibraryAsset GetSpriteLibrary(UnitType unitType)
    {
        Debug.LogError("Sprite Library는 Unity 2022.2 이상에서 지원됩니다.");
        return null;
    }
#endif

    public bool TryGetAnimationClips(string unitTypeName, out AnimationClip idle, out AnimationClip walk)
    {
        foreach (var lib in unitTypeSpriteLibraryMap)
        {
            if (lib.unitTypeName == unitTypeName)
            {
                idle = lib.idleClip;
                walk = lib.walkClip;
                return idle != null && walk != null;
            }
        }
        idle = walk = null;
        return false;
    }

    public static void GetSpriteLabelForDirection(Dir direction, out string category, out string label, out bool flipX)
    {
        category = "Direction";
        flipX = false;
        switch (direction)
        {
            case Dir.UP:        label = "Up";       break;
            case Dir.DOWN:      label = "Down";     break;
            case Dir.LEFT:      label = "Left";     break;
            case Dir.RIGHT:     label = "Left"; flipX = true; break;
            case Dir.UP_LEFT:   label = "UpLeft";   break;
            case Dir.UP_RIGHT:  label = "UpLeft"; flipX = true; break;
            case Dir.DOWN_LEFT: label = "DownLeft"; break;
            case Dir.DOWN_RIGHT:label = "DownLeft"; flipX = true; break;
            default:            label = "Down";     break;
        }
    }
}
