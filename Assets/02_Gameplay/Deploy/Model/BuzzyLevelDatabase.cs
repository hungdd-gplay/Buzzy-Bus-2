using System;
using System.Collections.Generic;
using UnityEngine;
using Buzzy.Core;

namespace Buzzy.Game
{
    [Serializable] public struct BuzzyPixel { public int x, y, color; }
    [Serializable] public struct BuzzyCar { public int id, seats, color; public bool locked; public int keyColor; }
    [Serializable] public struct BuzzyRect { public int x, y, w, h; public BuzzyRect(int x, int y, int w, int h) { this.x = x; this.y = y; this.w = w; this.h = h; } }
    [Serializable] public struct BuzzyFrozen { public BuzzyRect rect; public int hits; }
    [Serializable] public struct BuzzyKey { public BuzzyRect rect; public int color; }
    [Serializable] public struct BuzzySpawner { public BuzzyRect machine, area; public int facing; public List<int> waves; }
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
        /// <summary>Level có mechanic ngoài phạm vi bản đầu: xe 2 màu, Elevator, cổng đổi cột (importer ghi cảnh báo).</summary>
        public string warnings;
        // Mechanic M3 (rỗng = level thuần)
        public List<BuzzyRect> walls = new List<BuzzyRect>();
        public List<BuzzyFrozen> frozen = new List<BuzzyFrozen>();
        public List<BuzzyKey> keys = new List<BuzzyKey>();
        public List<BuzzySpawner> spawners = new List<BuzzySpawner>();
        public List<Vector2Int> mystery = new List<Vector2Int>();

        public bool HasMechanics { get { return walls.Count + frozen.Count + keys.Count + spawners.Count + mystery.Count > 0; } }
        static RectData R(BuzzyRect r) { return new RectData(r.x, r.y, r.w, r.h); }

        public LevelData ToCore()
        {
            var px = new List<PixelData>(pixels.Count);
            foreach (var p in pixels) px.Add(new PixelData(p.x, p.y, p.color));
            var qs = new List<IEnumerable<CarData>>();
            foreach (var q in queues)
            {
                var cs = new List<CarData>();
                foreach (var c in q.cars) cs.Add(new CarData(c.id, c.seats, c.color, c.locked, c.keyColor));
                qs.Add(cs);
            }
            var d = LevelData.Create(width, height, px, qs);
            foreach (var w in walls) d.Walls.Add(R(w));
            foreach (var f in frozen) d.Frozen.Add(new FrozenData(R(f.rect), f.hits));
            foreach (var k in keys) d.Keys.Add(new KeyData(R(k.rect), k.color));
            foreach (var s in spawners) d.Spawners.Add(new SpawnerData(R(s.machine), R(s.area), s.facing, s.waves.ToArray()));
            foreach (var m in mystery) d.Mystery.Add(new PixelData(m.x, m.y, -1));
            return d;
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
