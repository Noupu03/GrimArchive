using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Encyclopedia.UI
{
    /// <summary>
    /// 개별 도감 항목(슬롯)을 표시하는 UI 컴포넌트
    /// </summary>
    public class UI_EncyclopediaSlot : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private GameObject _lockOverlay;
        [SerializeField] private Button _button;

        private IEncyclopediaEntry _currentEntry;
        private UI_Encyclopedia _parentUI;

        private void Awake()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(OnClick);
            }
        }

        public void SetData(IEncyclopediaEntry entry, UI_Encyclopedia parentUI)
        {
            _currentEntry = entry;
            _parentUI = parentUI;
            
            if (entry.IsUnlocked)
            {
                if (_iconImage != null)
                {
                    _iconImage.sprite = entry.Icon;
                    _iconImage.color = Color.white;
                }
                if (_nameText != null) _nameText.text = entry.Name;
                if (_lockOverlay != null) _lockOverlay.SetActive(false);
            }
            else
            {
                // 미발견(잠금) 상태의 시각적 처리
                if (_iconImage != null)
                {
                    _iconImage.sprite = entry.Icon;
                    _iconImage.color = Color.black; // 실루엣 효과
                }
                if (_nameText != null) _nameText.text = "???";
                if (_lockOverlay != null) _lockOverlay.SetActive(true);
            }
        }

        private void OnClick()
        {
            if (_currentEntry != null && _parentUI != null)
            {
                _parentUI.OnSlotClicked(_currentEntry);
            }
        }
    }
}
