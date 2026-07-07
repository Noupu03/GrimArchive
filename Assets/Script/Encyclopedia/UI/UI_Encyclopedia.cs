using UnityEngine;
using System.Collections.Generic;

namespace Game.Encyclopedia.UI
{
    /// <summary>
    /// 도감 UI 전체를 관리하는 메인 클래스.
    /// 구체적인 매니저 클래스(EncyclopediaManager)에 의존하지 않고 
    /// IEncyclopediaSystem 인터페이스를 통해 통신합니다.
    /// </summary>
    public class UI_Encyclopedia : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform _contentContainer;
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private UI_EncyclopediaDetail _detailPanel;

        [Header("Settings")]
        [Tooltip("비워두면 모든 카테고리 표시, 특정 카테고리를 입력하면 해당 도감만 표시")]
        [SerializeField] private string _filterCategory = "";

        private IEncyclopediaSystem _encyclopediaSystem;
        private List<UI_EncyclopediaSlot> _spawnedSlots = new List<UI_EncyclopediaSlot>();

        /// <summary>
        /// 의존성 주입(Dependency Injection)을 위한 초기화 메서드.
        /// 외부(혹은 Bootstrapper)에서 이 UI에 시스템 인터페이스를 주입해 줍니다.
        /// </summary>
        public void Initialize(IEncyclopediaSystem system, string categoryFilter = "")
        {
            if (_encyclopediaSystem != null)
            {
                _encyclopediaSystem.OnEntryUnlocked -= HandleEntryUnlocked;
            }

            _encyclopediaSystem = system;
            
            if (!string.IsNullOrEmpty(categoryFilter))
            {
                _filterCategory = categoryFilter;
            }

            if (_encyclopediaSystem != null)
            {
                // 시스템에서 발생하는 이벤트를 구독하여 느슨하게 결합됨
                _encyclopediaSystem.OnEntryUnlocked += HandleEntryUnlocked;
                RefreshUI();
            }
        }

        private void Start()
        {
            // 의존성이 외부에서 주입되지 않았을 경우, Singleton Fallback 처리
            if (_encyclopediaSystem == null && EncyclopediaManager.Instance != null)
            {
                Initialize(EncyclopediaManager.Instance, _filterCategory);
            }
            else if (_encyclopediaSystem == null)
            {
                Debug.LogWarning("[UI_Encyclopedia] IEncyclopediaSystem가 주입되지 않았습니다.");
            }

            if (_detailPanel != null)
            {
                _detailPanel.Clear();
            }
        }

        private void OnDestroy()
        {
            if (_encyclopediaSystem != null)
            {
                _encyclopediaSystem.OnEntryUnlocked -= HandleEntryUnlocked;
            }
        }

        private void HandleEntryUnlocked(string id)
        {
            RefreshUI();
        }

        public void RefreshUI()
        {
            if (_encyclopediaSystem == null) return;

            // 기존 슬롯 제거
            foreach (var slot in _spawnedSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            _spawnedSlots.Clear();

            // 필터에 따라 데이터 가져오기 (인터페이스를 통해서만 통신)
            List<IEncyclopediaEntry> entriesToShow;
            if (string.IsNullOrEmpty(_filterCategory))
            {
                entriesToShow = _encyclopediaSystem.GetAllEntries();
            }
            else
            {
                entriesToShow = _encyclopediaSystem.GetEntriesByCategory(_filterCategory);
            }

            // 슬롯 생성 및 데이터 세팅
            foreach (var entry in entriesToShow)
            {
                GameObject slotGO = Instantiate(_slotPrefab, _contentContainer);
                if (slotGO.TryGetComponent<UI_EncyclopediaSlot>(out var slot))
                {
                    slot.SetData(entry, this);
                    _spawnedSlots.Add(slot);
                }
            }
        }

        public void OnSlotClicked(IEncyclopediaEntry entry)
        {
            if (_detailPanel != null)
            {
                _detailPanel.ShowDetails(entry);
            }
        }
    }
}
