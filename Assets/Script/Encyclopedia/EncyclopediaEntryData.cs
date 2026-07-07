using UnityEngine;

namespace Game.Encyclopedia
{
    /// <summary>
    /// IEncyclopediaEntry를 구현하는 ScriptableObject 기반 데이터.
    /// 기획자가 유니티 에디터 상에서 몬스터나 인물 데이터를 쉽게 생성하고 관리할 수 있도록 합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEncyclopediaData", menuName = "Encyclopedia/Entry Data")]
    public class EncyclopediaEntryData : ScriptableObject, IEncyclopediaEntry
    {
        [Header("Identity")]
        [SerializeField] private string _id;
        
        [Tooltip("도감 종류 구분: 예) Monster, Human, Item")]
        [SerializeField] private string _category = "Monster";
        
        [Header("Display Info")]
        [SerializeField] private string _entryName;
        
        [TextArea(3, 10)]
        [SerializeField] private string _description;
        [SerializeField] private Sprite _icon;
        
        [Header("State")]
        [SerializeField] private bool _isUnlocked;

        // 인터페이스 프로퍼티 구현
        public string Id => _id;
        public string Category => _category;
        public string Name => _entryName;
        public string Description => _description;
        public Sprite Icon => _icon;
        
        public bool IsUnlocked 
        { 
            get => _isUnlocked; 
            set => _isUnlocked = value; 
        }
    }
}
