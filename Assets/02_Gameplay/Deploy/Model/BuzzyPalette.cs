using UnityEngine;

namespace Buzzy.Game
{
    /// <summary>Bảng màu ColorCode 0..20 lấy từ ColorConfig của game gốc (dòng V* màu pixel / xe).</summary>
    public static class BuzzyPalette
    {
        public static readonly Color32[] Colors =
        {
            new Color32(231, 45, 45, 255), // 0
            new Color32(255, 136, 9, 255), // 1
            new Color32(255, 201, 18, 255), // 2
            new Color32(135, 209, 42, 255), // 3
            new Color32(53, 173, 57, 255), // 4
            new Color32(95, 208, 255, 255), // 5
            new Color32(17, 79, 229, 255), // 6
            new Color32(145, 39, 246, 255), // 7
            new Color32(204, 64, 150, 255), // 8
            new Color32(54, 62, 75, 255), // 9
            new Color32(168, 44, 73, 255), // 10
            new Color32(168, 93, 75, 255), // 11
            new Color32(248, 137, 137, 255), // 12
            new Color32(50, 234, 193, 255), // 13
            new Color32(49, 125, 70, 255), // 14
            new Color32(62, 144, 255, 255), // 15
            new Color32(100, 84, 245, 255), // 16
            new Color32(253, 188, 144, 255), // 17
            new Color32(243, 100, 228, 255), // 18
            new Color32(206, 235, 255, 255), // 19
            new Color32(255, 255, 255, 255), // 20 (special)
        };

        public static Color32 Get(int colorCode)
        {
            if (colorCode < 0 || colorCode >= Colors.Length) return new Color32(255, 0, 255, 255);
            return Colors[colorCode];
        }
    }
}
