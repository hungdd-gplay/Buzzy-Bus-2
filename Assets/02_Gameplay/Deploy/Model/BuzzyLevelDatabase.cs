using System;
using System.Collections.Generic;
using UnityEngine;
using Buzzy.Core;

namespace Buzzy.Game
{
    [Serializable] public struct BuzzyPixel { public int x, y, color; }
    [Serializable] public struct BuzzyCar { public int id, seats, color; }
    [Serializable] public class BuzzyQueue { public List<BuzzyCar> cars = new List<BuzzyCar>(); }

    /// <summary>Một level đã nhập từ JSON gốc (importer tạo, không sửa tay).</summary>
    [Serializable]
    public class BuzzyLevel
    {
        public int index;
        /// <summary>0 thường, 1 khó, 2 siêu khó (levelTag của game gốc).</summary>
        public int tag;
        /// <summary>Width/Height là chỉ số lớn nhất (bao gồm); lưới thực = (Width+1) x (Height+1).</summary>
        public int width, height;
        public List<BuzzyPixel> pixels = new List<BuzzyPixel>();
        public List<BuzzyQueue> queues = new List<BuzzyQueue>();
        /// <summary>Level có mechanic ngoài phạm vi bản đầu (importer ghi cảnh báo).</summary>
        public string warnings;

        public LevelData ToCore()
        {
            var px = new List<PixelData>(pixels.Count);
            foreach (var p in pixels) px.Add(new PixelData(p.x, p.y, p.color));
            var qs = new List<IEnumerable<CarData>>();
            foreach (var q in queues)
            {
                var cs = new List<CarData>();
                foreach (var c in q.cars) cs.Add(new CarData(c.id, c.seats, c.color));
                qs.Add(cs);
            }
            return LevelData.Create(width, height, px, qs);
        }
    }

    /// <summary>Database 300 level nhập từ Levels/level_*.json (menu Tools/Buzzy Bus/Import Levels).</summary>
    [CreateAssetMenu(menuName = "Buzzy Bus/Level Database", fileName = "BuzzyLevels")]
    public class BuzzyLevelDatabase : ScriptableObject
    {
        public List<BuzzyLevel> levels = new List<BuzzyLevel>();

        public BuzzyLevel Get(int index)
        {
            for (int i = 0; i < levels.Count; i++) if (levels[i].index == index) return levels[i];
            return null;
        }
    }
}
