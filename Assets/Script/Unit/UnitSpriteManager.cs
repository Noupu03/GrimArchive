using UnityEngine;
using System.Collections.Generic;

public class UnitSpriteManager : MonoBehaviour
{
    public static UnitSpriteManager Instance { get; private set; }

    [System.Serializable]
    public class UnitTypeSpriteSet
    {
        public string unitTypeName;  // "기사형", "근접 탱커" 등
        public Sprite spriteUp;
        public Sprite spriteDown;
        public Sprite spriteLeft;
        public Sprite spriteUpLeft;
        public Sprite spriteDownLeft;
    }

    public List<UnitTypeSpriteSet> unitTypeSpriteMap = new List<UnitTypeSpriteSet>();

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
    /// 유닛 유형과 방향에 따른 스프라이트를 반환합니다.
    /// flipX를 통해 좌우 반전 여부를 반환합니다.
    /// </summary>
    public Sprite GetSprite(UnitType unitType, Dir direction, out bool flipX)
    {
        flipX = false;

        if (unitType == null)
            return null;

        // 유닛 타입 이름으로 스프라이트 세트 찾기
        UnitTypeSpriteSet spriteSet = null;
        foreach (var set in unitTypeSpriteMap)
        {
            if (set.unitTypeName == unitType.typeName)
            {
                spriteSet = set;
                break;
            }
        }

        if (spriteSet == null)
        {
            Debug.LogWarning($"스프라이트 세트를 찾을 수 없습니다: {unitType.typeName}");
            return null;
        }

        // 방향에 따라 스프라이트 반환
        switch (direction)
        {
            case Dir.UP:
                return spriteSet.spriteUp;

            case Dir.DOWN:
                return spriteSet.spriteDown;

            case Dir.LEFT:
                return spriteSet.spriteLeft;

            case Dir.RIGHT:
                flipX = true;
                return spriteSet.spriteLeft;

            case Dir.UP_LEFT:
                return spriteSet.spriteUpLeft;

            case Dir.UP_RIGHT:
                flipX = true;
                return spriteSet.spriteUpLeft;

            case Dir.DOWN_LEFT:
                return spriteSet.spriteDownLeft;

            case Dir.DOWN_RIGHT:
                flipX = true;
                return spriteSet.spriteDownLeft;

            default:
                return null;
        }
    }
}
