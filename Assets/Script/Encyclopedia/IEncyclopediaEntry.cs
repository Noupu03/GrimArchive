using UnityEngine;

namespace Game.Encyclopedia
{
    /// <summary>
    /// 단일 도감 항목의 규격을 정의하는 인터페이스.
    /// 몬스터, 인물, 아이템 등 다양한 종류의 도감 데이터가 이를 구현하게 하여
    /// 시스템과 UI가 특정 데이터 타입에 종속되지 않도록 의존성을 역전시킵니다.
    /// </summary>
    public interface IEncyclopediaEntry
    {
        string Id { get; }
        
        /// <summary>
        /// "Monster", "Human", "Item" 등 도감의 종류를 구분하기 위한 카테고리
        /// </summary>
        string Category { get; }
        
        string Name { get; }
        string Description { get; }
        Sprite Icon { get; }
        
        bool IsUnlocked { get; set; }
    }
}
