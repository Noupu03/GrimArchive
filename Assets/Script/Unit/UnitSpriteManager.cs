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
        public string unitTypeName;  // "기사형", "근접 탱커" 등
#if UNITY_2022_2_OR_NEWER
        public SpriteLibraryAsset spriteLibraryAsset;
#endif
    }

    public List<UnitTypeSpriteLibrary> unitTypeSpriteLibraryMap = new List<UnitTypeSpriteLibrary>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 유닛 유형에 해당하는 SpriteLibraryAsset을 반환합니다.
    /// </summary>
#if UNITY_2022_2_OR_NEWER
    public SpriteLibraryAsset GetSpriteLibrary(UnitType unitType)
    {
        if (unitType == null)
            return null;

        // 유닛 타입 이름으로 스프라이트 라이브러리 찾기
        foreach (var lib in unitTypeSpriteLibraryMap)
        {
            if (lib.unitTypeName == unitType.typeName && lib.spriteLibraryAsset != null)
            {
                return lib.spriteLibraryAsset;
            }
        }

        Debug.LogWarning($"스프라이트 라이브러리를 찾을 수 없습니다: {unitType.typeName}");
        return null;
    }

    /// <summary>
    /// 주어진 방향에 대한 Category와 Label을 반환합니다.
    /// </summary>
    public static void GetSpriteLabelForDirection(Dir direction, out string category, out string label, out bool flipX)
    {
        category = "Direction";
        flipX = false;

        switch (direction)
        {
            case Dir.UP:
                label = "Up";
                break;

            case Dir.DOWN:
                label = "Down";
                break;

            case Dir.LEFT:
                label = "Left";
                break;

            case Dir.RIGHT:
                label = "Left";
                flipX = true;
                break;

            case Dir.UP_LEFT:
                label = "UpLeft";
                break;

            case Dir.UP_RIGHT:
                label = "UpLeft";
                flipX = true;
                break;

            case Dir.DOWN_LEFT:
                label = "DownLeft";
                break;

            case Dir.DOWN_RIGHT:
                label = "DownLeft";
                flipX = true;
                break;

            default:
                label = "Down";
                break;
        }
    }
#else
    public SpriteLibraryAsset GetSpriteLibrary(UnitType unitType)
    {
        Debug.LogError("Sprite Library는 Unity 2022.2 이상에서 지원됩니다.");
        return null;
    }

    public static void GetSpriteLabelForDirection(Dir direction, out string category, out string label, out bool flipX)
    {
        category = "Direction";
        label = "Down";
        flipX = false;
        Debug.LogError("Sprite Library는 Unity 2022.2 이상에서 지원됩니다.");
    }
#endif
}
