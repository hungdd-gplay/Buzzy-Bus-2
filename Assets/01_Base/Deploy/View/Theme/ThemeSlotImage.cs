using UnityEngine;
using UnityEngine.UI;

namespace BaseProject.Theme
{
    /// <summary>
    /// View: gắn vào object có Image (UI) hoặc SpriteRenderer. Tự lấy ảnh theo slot của theme hiện tại
    /// và đổi lại khi theme đổi. Ảnh trang trí trong button: dùng cùng slot + subKey (vd "Decor_1").
    /// Theme không có ảnh cho slot -> giữ nguyên ảnh đang gán (ảnh concept mặc định trong scene).
    /// </summary>
    [DisallowMultipleComponent]
    public class ThemeSlotImage : MonoBehaviour
    {
        public ThemeSlotId slot = ThemeSlotId.None;
        [Tooltip("Hậu tố cho ảnh con, vd Decor_1 -> key Home_ButtonPlay_Decor_1. Để trống = ảnh chính.")]
        public string subKey;
        [Tooltip("Đổi kích thước theo ảnh mới (Image.SetNativeSize)")]
        public bool isSetNativeSize;

        private Image _image;
        private SpriteRenderer _spriteRenderer;

        public string Key => ThemeKeyUtility.GetKey(slot, subKey);

        private void Awake()
        {
            _image = GetComponent<Image>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            ThemeManager.OnThemeChanged += ApplyTheme;
            ApplyTheme();
        }

        private void OnDisable()
        {
            ThemeManager.OnThemeChanged -= ApplyTheme;
        }

        public void ApplyTheme()
        {
            if (slot == ThemeSlotId.None || ThemeManager.Instance == null)
                return;
            Sprite _sprite = ThemeManager.Instance.GetSprite(slot, subKey);
            if (_sprite == null)
                return;
            SetSprite(_sprite);
        }

        public void SetSprite(Sprite sprite)
        {
            if (_image != null)
            {
                _image.sprite = sprite;
                if (isSetNativeSize)
                    _image.SetNativeSize();
            }
            else if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = sprite;
            }
        }
    }
}
