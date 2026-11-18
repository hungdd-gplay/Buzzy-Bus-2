using UnityEngine;
using UnityEngine.UI;

namespace BaseProject.Theme
{
    /// <summary>
    /// View: BG gameplay lấy từ danh sách Gameplay_BG_xx của theme hiện tại.
    /// Cách chọn (Random / theo thứ tự level / theo khoảng level) cấu hình trong ThemePack.
    /// Level mặc định lấy từ UseProfile.CurrentLevel; gameplay mới có thể gọi SetLevel(level).
    /// </summary>
    [DisallowMultipleComponent]
    public class ThemeGameplayBackground : MonoBehaviour
    {
        [Tooltip("Tự lấy level từ UseProfile.CurrentLevel khi bật")]
        public bool isUseCurrentLevel = true;
        [Tooltip("Ảnh co giãn phủ kín màn hình (giữ tỉ lệ, cắt phần thừa) - chỉ áp dụng cho UI Image")]
        public bool isEnvelopeParent = true;

        private Image _image;
        private SpriteRenderer _spriteRenderer;
        private int _level = 1;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (isUseCurrentLevel)
                _level = Mathf.Max(1, UseProfile.CurrentLevel);
            ThemeManager.OnThemeChanged += ApplyTheme;
            ApplyTheme();
        }

        private void OnDisable()
        {
            ThemeManager.OnThemeChanged -= ApplyTheme;
        }

        /// <summary>Đổi BG theo level (gọi khi vào level mới).</summary>
        public void SetLevel(int level)
        {
            _level = Mathf.Max(1, level);
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            if (ThemeManager.Instance == null)
                return;
            Sprite _sprite = ThemeManager.Instance.GetGameplayBackground(_level);
            if (_sprite == null)
                return;

            if (_image != null)
            {
                _image.sprite = _sprite;
                if (isEnvelopeParent)
                    _FitEnvelope(_image.rectTransform, _sprite);
            }
            else if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = _sprite;
            }
        }

        private static void _FitEnvelope(RectTransform rectTransform, Sprite sprite)
        {
            var _parent = rectTransform.parent as RectTransform;
            if (_parent == null || sprite.rect.height <= 0)
                return;
            Vector2 _sizeParent = _parent.rect.size;
            float _ratioSprite = sprite.rect.width / sprite.rect.height;
            float _ratioParent = _sizeParent.y > 0 ? _sizeParent.x / _sizeParent.y : _ratioSprite;
            Vector2 _size = _ratioParent > _ratioSprite
                ? new Vector2(_sizeParent.x, _sizeParent.x / _ratioSprite)
                : new Vector2(_sizeParent.y * _ratioSprite, _sizeParent.y);
            rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = _size;
            rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
