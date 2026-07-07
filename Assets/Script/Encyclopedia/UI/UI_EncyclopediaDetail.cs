using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Encyclopedia.UI
{
    /// <summary>
    /// 슬롯을 클릭했을 때 나타나는 상세 정보 창 UI
    /// </summary>
    public class UI_EncyclopediaDetail : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private GameObject _contentRoot; // 정보가 없을 때 숨길 루트 컨테이너

        public void ShowDetails(IEncyclopediaEntry entry)
        {
            if (entry == null)
            {
                Clear();
                return;
            }

            if (_contentRoot != null) _contentRoot.SetActive(true);

            if (entry.IsUnlocked)
            {
                if (_iconImage != null)
                {
                    _iconImage.sprite = entry.Icon;
                    _iconImage.color = Color.white;
                }
                if (_nameText != null) _nameText.text = entry.Name;
                if (_descriptionText != null) _descriptionText.text = entry.Description;
            }
            else
            {
                if (_iconImage != null)
                {
                    _iconImage.sprite = entry.Icon;
                    _iconImage.color = Color.black; // 실루엣 효과
                }
                if (_nameText != null) _nameText.text = "???";
                if (_descriptionText != null) _descriptionText.text = "아직 발견하지 못한 항목입니다.";
            }
        }

        public void Clear()
        {
            if (_contentRoot != null) _contentRoot.SetActive(false);
            if (_iconImage != null) _iconImage.sprite = null;
            if (_nameText != null) _nameText.text = "";
            if (_descriptionText != null) _descriptionText.text = "";
        }
    }
}
