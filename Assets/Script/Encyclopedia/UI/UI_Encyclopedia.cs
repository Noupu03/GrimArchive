using UnityEngine;
using System.Collections.Generic;
using Haare.Client.Routine;
using Haare.Client.UI;
using VContainer;

namespace Game.Encyclopedia.UI
{
    /// <summary>
    /// 하레 프레임워크 기반의 도감 패널
    /// </summary>
    [PanelAttribute("Prefabs/UI/EncyclopediaPanel")]
    public class UI_Encyclopedia : MonoRoutine, ICustomPanel
    {
        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        [Header("UI References")]
        [SerializeField] private Transform _contentContainer;
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private UI_EncyclopediaDetail _detailPanel;

        [Header("Settings")]
        [Tooltip("비워두면 모든 카테고리 표시, 특정 카테고리를 입력하면 해당 도감만 표시")]
        [SerializeField] private string _filterCategory = "";
        
        [Tooltip("도감 UI를 켜고 끌 단축키를 목록에서 선택하세요.")]
        [SerializeField] private UnityEngine.InputSystem.Key _toggleKey = UnityEngine.InputSystem.Key.Tab;

        private IEncyclopediaSystem _encyclopediaSystem;
        private List<UI_EncyclopediaSlot> _spawnedSlots = new List<UI_EncyclopediaSlot>();
        private UnityEngine.InputSystem.InputAction _toggleAction;

        protected override void Constructor()
        {
            base.Constructor();
        }

        private void TogglePanel()
        {
            Debug.Log($"<color=cyan>[UI_Encyclopedia]</color> 토글 키 입력 감지! 현재 활성 상태: {gameObject.activeSelf}");
            if (gameObject.activeSelf) 
                ClosePanel();
            else 
                OpenPanel();
        }

        // 프리팹이 Instantiate 될 때 최초 1회 실행됨 (비활성화 되기 직전)
        private void OnEnable()
        {
            if (_toggleAction == null)
            {
                string keyPath = $"<Keyboard>/{_toggleKey.ToString()}";
                _toggleAction = new UnityEngine.InputSystem.InputAction(binding: keyPath);
                _toggleAction.performed += _ => TogglePanel();
                _toggleAction.Enable();
                Debug.Log($"<color=cyan>[UI_Encyclopedia]</color> 단축키({keyPath}) 구독 완료 (OnEnable).");
            }
        }

        public void BindEvent()
        {
            Debug.Log($"<color=cyan>[UI_Encyclopedia]</color> BindEvent 호출됨! (단축키: {_toggleKey.ToString()})");
            
            // 의존성 수동 연결 (EncyclopediaManager가 아직 DI 컨테이너에 등록되지 않은 경우 싱글톤 사용)
            if (_encyclopediaSystem == null && EncyclopediaManager.Instance != null)
            {
                _encyclopediaSystem = EncyclopediaManager.Instance;
            }

            if (_encyclopediaSystem != null)
            {
                _encyclopediaSystem.OnEntryUnlocked -= HandleEntryUnlocked;
                _encyclopediaSystem.OnEntryUnlocked += HandleEntryUnlocked;
            }
            else
            {
                Debug.LogWarning("[UI_Encyclopedia] IEncyclopediaSystem를 찾을 수 없습니다.");
            }
        }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;

            if (_detailPanel != null)
            {
                _detailPanel.Clear();
            }

            RefreshUI();
        }

        public void ClosePanel()
        {
            gameObject.SetActive(false);
        }

        private void HandleEntryUnlocked(string id)
        {
            if (gameObject.activeSelf)
            {
                RefreshUI();
            }
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

            // 필터에 따라 데이터 가져오기
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

        private void OnDestroy()
        {
            if (_toggleAction != null)
            {
                _toggleAction.Disable();
                _toggleAction.Dispose();
            }

            if (_encyclopediaSystem != null)
            {
                _encyclopediaSystem.OnEntryUnlocked -= HandleEntryUnlocked;
            }
        }
    }
}
