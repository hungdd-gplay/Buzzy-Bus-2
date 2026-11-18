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
        [Serializable] class JCar { public int ID, Seats, ColorCode, IsLocked, SecondSeats, SecondColorCode; }
        [Serializable] class JQueue { public List<JCar> Cars; }
        [Serializable] class JQueueGroup { public List<JQueue> CarQueues; }
        [Serializable] class JPixel { public int x, y, ColorCode; }
        [Serializable] class JImage { public int Width, Height; public List<JPixel> Pixels; }
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
                        bq.cars.Add(new BuzzyCar { id = c.ID, seats = c.Seats, color = c.ColorCode });
                        if (c.IsLocked != 0) lv.warnings += "xe khóa;";
                        if (c.SecondSeats != 0) lv.warnings += "xe 2 màu;";
                    }
                    lv.queues.Add(bq);
                }
                // mechanic chỉ phát hiện bằng khoá thô (Walls, Spawners, Elevators...) — chi tiết ở M3/M4
                string raw = File.ReadAllText(f);
                if (raw.Contains("\"Walls\": [") && !raw.Contains("\"Walls\": []")) lv.warnings += "walls;";
                if (raw.Contains("\"Spawners\": [") && !raw.Contains("\"Spawners\": []")) lv.warnings += "spawner;";
                if (raw.Contains("\"Elevators\": [") && !raw.Contains("\"Elevators\": []")) lv.warnings += "elevator;";
                if (!string.IsNullOrEmpty(lv.warnings)) warn++;
                db.levels.Add(lv);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            AssetDatabase.CreateAsset(db, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[BuzzyLevelImporter] Nhập " + db.levels.Count + " level (" + warn + " level có mechanic cần xử lý ở M3/M4) -> " + AssetPath);
        }
    }
}
