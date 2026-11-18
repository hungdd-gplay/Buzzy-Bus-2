using System;
using System.Collections.Generic;
using System.Linq;

namespace BaseProject.Theme
{
    /// <summary>
    /// Quy ước tên ảnh (key) của theme.
    /// key = tên slot (vd Home_ButtonPlay) hoặc tên slot + "_" + subKey (vd Home_ButtonPlay_Decor_1, Gameplay_BG_03).
    /// </summary>
    public static class ThemeKeyUtility
    {
        private static string[] _slotNamesSortedByLength;

        public static string GetKey(ThemeSlotId slot, string subKey = null)
        {
            return string.IsNullOrEmpty(subKey) ? slot.ToString() : slot + "_" + subKey;
        }

        public static ThemeCategory GetCategory(ThemeSlotId slot)
        {
            int _id = (int)slot;
            if (_id >= 300) return ThemeCategory.Gameplay;
            if (_id >= 200) return ThemeCategory.Home;
            return ThemeCategory.Loading;
        }

        /// <summary>Tách tên file thành slot + subKey. Trả về false nếu không khớp slot nào.</summary>
        public static bool TryParseKey(string key, out ThemeSlotId slot, out string subKey)
        {
            slot = ThemeSlotId.None;
            subKey = null;
            if (string.IsNullOrEmpty(key))
                return false;

            if (_slotNamesSortedByLength == null)
            {
                _slotNamesSortedByLength = Enum.GetNames(typeof(ThemeSlotId))
                    .Where(n => n != nameof(ThemeSlotId.None))
                    .OrderByDescending(n => n.Length)
                    .ToArray();
            }

            foreach (var _name in _slotNamesSortedByLength)
            {
                if (string.Equals(key, _name, StringComparison.OrdinalIgnoreCase))
                {
                    slot = (ThemeSlotId)Enum.Parse(typeof(ThemeSlotId), _name);
                    return true;
                }
                if (key.StartsWith(_name + "_", StringComparison.OrdinalIgnoreCase))
                {
                    slot = (ThemeSlotId)Enum.Parse(typeof(ThemeSlotId), _name);
                    subKey = key.Substring(_name.Length + 1);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Key có phải 1 BG gameplay trong danh sách (Gameplay_BG hoặc Gameplay_BG_xx).</summary>
        public static bool IsGameplayBackgroundKey(string key)
        {
            return TryParseKey(key, out var _slot, out _) && _slot == ThemeSlotId.Gameplay_BG;
        }

        /// <summary>Danh sách tên file chuẩn để designer đặt tên ảnh.</summary>
        public static List<string> GetAllKeyNames()
        {
            return Enum.GetNames(typeof(ThemeSlotId)).Where(n => n != nameof(ThemeSlotId.None)).ToList();
        }
    }
}
