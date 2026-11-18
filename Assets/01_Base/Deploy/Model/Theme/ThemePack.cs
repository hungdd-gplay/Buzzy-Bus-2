using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaseProject.Theme
{
    [Serializable]
    public class ThemeSpriteEntry
    {
        public string key;
        public Sprite sprite;
    }

    /// <summary>
    /// Model: bộ ảnh của 1 theme (concept mặc định hoặc 1 sự kiện).
    /// Được dựng tự động bởi Theme Manager (Editor) từ thư mục ảnh; có thể đóng gói vào AssetBundle.
    /// </summary>
    [CreateAssetMenu(fileName = "ThemePack", menuName = "Base Project/Theme/Theme Pack")]
    public class ThemePack : ScriptableObject
    {
        public ThemeEventId eventId = ThemeEventId.Default;
        public string displayName;

        [Header("Ảnh theo key (tên file)")]
        public List<ThemeSpriteEntry> entries = new List<ThemeSpriteEntry>();

        [Header("BG Gameplay (Gameplay_BG_01, Gameplay_BG_02...)")]
        public List<Sprite> gameplayBackgrounds = new List<Sprite>();
        public ThemeBackgroundMode backgroundMode = ThemeBackgroundMode.ByLevelOrder;
        [Tooltip("Chế độ ByLevelRange: mỗi BG dùng cho bao nhiêu level liên tiếp")]
        public int levelPerBackground = 10;

        [NonSerialized] private Dictionary<string, Sprite> _dictSprite;

        public Sprite GetSprite(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            if (_dictSprite == null)
                _BuildCache();
            _dictSprite.TryGetValue(key.ToLowerInvariant(), out var _sprite);
            return _sprite;
        }

        public Sprite GetSprite(ThemeSlotId slot, string subKey = null)
        {
            return GetSprite(ThemeKeyUtility.GetKey(slot, subKey));
        }

        /// <summary>Chọn BG gameplay theo level (level bắt đầu từ 1).</summary>
        public Sprite GetGameplayBackground(int level)
        {
            if (gameplayBackgrounds == null || gameplayBackgrounds.Count == 0)
                return GetSprite(ThemeSlotId.Gameplay_BG);

            int _count = gameplayBackgrounds.Count;
            int _levelIndex = Mathf.Max(0, level - 1);
            switch (backgroundMode)
            {
                case ThemeBackgroundMode.Random:
                    return gameplayBackgrounds[UnityEngine.Random.Range(0, _count)];
                case ThemeBackgroundMode.ByLevelRange:
                    return gameplayBackgrounds[(_levelIndex / Mathf.Max(1, levelPerBackground)) % _count];
                default:
                    return gameplayBackgrounds[_levelIndex % _count];
            }
        }

        public bool HasGameplayBackground => gameplayBackgrounds != null && gameplayBackgrounds.Count > 0;

        public void ClearCache()
        {
            _dictSprite = null;
        }

        private void _BuildCache()
        {
            _dictSprite = new Dictionary<string, Sprite>();
            foreach (var _entry in entries)
            {
                if (_entry == null || string.IsNullOrEmpty(_entry.key) || _entry.sprite == null)
                    continue;
                _dictSprite[_entry.key.ToLowerInvariant()] = _entry.sprite;
            }
        }

        private void OnValidate()
        {
            _dictSprite = null;
        }
    }
}
