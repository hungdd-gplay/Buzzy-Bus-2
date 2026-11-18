using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Buzzy.Game.EditorTools
{
    /// <summary>Nhập Levels/level_*.json của game gốc thành BuzzyLevelDatabase (đúng schema, giữ độ khó + hình pixel).</summary>
    public static class BuzzyLevelImporter
    {
        public const string DefaultSource = @"C:\Users\PC\Contacts\Downloads\Buzzy Bus Resource\Buzzy Bus\Levels";
        public const string AssetPath = "Assets/02_Gameplay/Res/Levels/BuzzyLevels.asset";

        // DTO cho JsonUtility: tên trường khớp đúng khoá JSON, khoá không khai báo bị bỏ qua.
        [Serializable] class JCar { public int ID, Seats, ColorCode, IsLocked, KeyColorCode, SecondSeats, SecondColorCode; }
        [Serializable] class JQueue { public List<JCar> Cars; }
        [Serializable] class JQueueGroup { public List<JQueue> CarQueues; }
        [Serializable] class JPixel { public int x, y, ColorCode; }
        [Serializable] class JRect { public int x, y, Width, Height; }
        [Serializable] class JFrozen { public int x, y, Width, Height, RequiredHits; }
        [Serializable] class JKey { public int x, y, Width, Height, ColorCode; }
        [Serializable] class JSpawner { public int x, y, Width, Height, SpawnAreaX, SpawnAreaY, SpawnAreaWidth, SpawnAreaHeight, Facing; public List<int> WaveColors; }
        [Serializable] class JCell { public int X, Y; }
        [Serializable] class JSurprise { public List<JCell> Pixels; }
        [Serializable] class JImage
        {
            public int Width, Height; public List<JPixel> Pixels;
            public List<JRect> Walls; public List<JFrozen> FrozenAreas; public List<JKey> Keys; public List<JSpawner> Spawners; public JSurprise SurprisePixels;
        }
        [Serializable] class JLevelData { public JQueueGroup CarQueueGroup; public JImage PixelImageData; }
        [Serializable] class JRoot { public int levelIndex, levelTag; public JLevelData LevelData; }

        [MenuItem("Tools/Buzzy Bus/Import Levels")]
        public static void ImportMenu() { ImportAll(DefaultSource); }

        /// <summary>Dùng với -executeMethod Buzzy.Game.EditorTools.BuzzyLevelImporter.ImportBatch</summary>
        public static void ImportBatch() { ImportAll(DefaultSource); }

        public static void ImportAll(string dir)
        {
            if (!Directory.Exists(dir)) { Debug.LogError("Không thấy thư mục level: " + dir); return; }
            var files = Directory.GetFiles(dir, "level_*.json");
            Array.Sort(files, StringComparer.Ordinal);
            var db = ScriptableObject.CreateInstance<BuzzyLevelDatabase>();
            int warn = 0;
            foreach (var f in files)
            {
                var root = JsonUtility.FromJson<JRoot>(File.ReadAllText(f));
                if (root == null || root.LevelData == null || root.LevelData.PixelImageData == null) { Debug.LogWarning("Bỏ qua (parse lỗi): " + f); continue; }
                var lv = new BuzzyLevel
                {
                    index = root.levelIndex, tag = root.levelTag,
                    width = root.LevelData.PixelImageData.Width, height = root.LevelData.PixelImageData.Height
                };
                foreach (var p in root.LevelData.PixelImageData.Pixels) lv.pixels.Add(new BuzzyPixel { x = p.x, y = p.y, color = p.ColorCode });
                foreach (var q in root.LevelData.CarQueueGroup.CarQueues)
                {
                    var bq = new BuzzyQueue();
                    foreach (var c in q.Cars)
                    {
                        bq.cars.Add(new BuzzyCar { id = c.ID, seats = c.Seats, color = c.ColorCode, locked = c.IsLocked != 0, keyColor = c.KeyColorCode });
                        if (c.SecondSeats != 0 && !(lv.warnings ?? "").Contains("xe 2 màu")) lv.warnings += "xe 2 màu;";
                    }
                    lv.queues.Add(bq);
                }
                // mechanic M3: Mystery, Frozen, Walls, Spawner, Key (+ xe khóa) chạy được
                var img = root.LevelData.PixelImageData;
                if (img.Walls != null) foreach (var w in img.Walls) lv.walls.Add(new BuzzyRect(w.x, w.y, w.Width, w.Height));
                if (img.FrozenAreas != null) foreach (var fz in img.FrozenAreas) lv.frozen.Add(new BuzzyFrozen { rect = new BuzzyRect(fz.x, fz.y, fz.Width, fz.Height), hits = fz.RequiredHits });
                if (img.Keys != null) foreach (var k in img.Keys) lv.keys.Add(new BuzzyKey { rect = new BuzzyRect(k.x, k.y, k.Width, k.Height), color = k.ColorCode });
                if (img.Spawners != null) foreach (var sp in img.Spawners)
                    lv.spawners.Add(new BuzzySpawner { machine = new BuzzyRect(sp.x, sp.y, sp.Width, sp.Height), area = new BuzzyRect(sp.SpawnAreaX, sp.SpawnAreaY, sp.SpawnAreaWidth, sp.SpawnAreaHeight), facing = sp.Facing, waves = sp.WaveColors ?? new List<int>() });
                if (img.SurprisePixels != null && img.SurprisePixels.Pixels != null) foreach (var m in img.SurprisePixels.Pixels) lv.mystery.Add(new Vector2Int(m.X, m.Y));
                // ngoài phạm vi bản đầu: Elevator (Pixel Vault), cổng đổi cột (khoá thô)
                string raw = File.ReadAllText(f);
                if (raw.Contains("\"Elevators\": [") && !raw.Contains("\"Elevators\": []")) lv.warnings += "elevator;";
                if (raw.Contains("\"QueueBoardGatePairs\": [") && !raw.Contains("\"QueueBoardGatePairs\": []")) lv.warnings += "cổng đổi cột;";
                if (!string.IsNullOrEmpty(lv.warnings)) warn++;
                db.levels.Add(lv);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            AssetDatabase.CreateAsset(db, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[BuzzyLevelImporter] Nhập " + db.levels.Count + " level (" + warn + " level ngoài phạm vi: xe 2 màu / Elevator / cổng đổi cột) -> " + AssetPath);
        }
    }
}
