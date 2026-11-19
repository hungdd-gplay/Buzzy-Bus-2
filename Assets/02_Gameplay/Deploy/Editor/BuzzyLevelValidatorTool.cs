using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Buzzy.Core;

namespace Buzzy.Game.EditorTools
{
    /// <summary>
    /// M4: chạy LevelValidator trên BuzzyLevels.asset, ghi bảng độ khó vào từng level của asset + báo cáo Markdown.
    /// -executeMethod Buzzy.Game.EditorTools.BuzzyLevelValidatorTool.Run ; môi trường: BUZZY_REPORT (đường dẫn .md, mặc định .game/research/level-report.md), BUZZY_TRIES.
    /// ImportAndValidate: nhập lại 300 level rồi kiểm, trong 1 lần mở Unity.
    /// </summary>
    public static class BuzzyLevelValidatorTool
    {
        const string DefaultReport = ".game/research/level-report.md";

        [MenuItem("Tools/Buzzy Bus/Validate Levels")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<BuzzyLevelDatabase>(BuzzyLevelImporter.AssetPath);
            if (db == null) throw new Exception("Chưa có " + BuzzyLevelImporter.AssetPath + " (chạy Import Levels)");
            string rep = Environment.GetEnvironmentVariable("BUZZY_REPORT"); if (string.IsNullOrEmpty(rep)) rep = DefaultReport;
            string tr = Environment.GetEnvironmentVariable("BUZZY_TRIES");
            var opt = new ValidatorOptions(); if (!string.IsNullOrEmpty(tr)) opt.Tries = int.Parse(tr);
            var rules = BuzzyStaticBuilder.LoadOrCreateConfig().ToMechanicRules();   // M6: luật mechanic là dữ liệu
            var checks = new List<LevelCheck>();
            foreach (var lv in db.levels)
            {
                var c = LevelValidator.Check(lv.ToCore(), lv.index, lv.tag, lv.outOfScope, rules, opt);
                checks.Add(c);
                lv.solvable = c.Solvable; lv.winRate = (float)c.WinRate; lv.taps = c.Taps; lv.difficultyBand = c.DifficultyBand;
                lv.validation = string.Join("; ", c.Errors.ToArray());
            }
            EditorUtility.SetDirty(db); AssetDatabase.SaveAssets();
            var md = LevelValidator.Markdown(checks, opt);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(rep)));
            File.WriteAllText(rep, md);
            int inScope = 0, solvable = 0, errors = 0;
            foreach (var c in checks) if (c.InScope) { inScope++; if (c.Solvable) solvable++; if (c.Errors.Count > 0) errors++; }
            Debug.Log("[BuzzyLevelValidator] " + checks.Count + " level: trong phạm vi " + inScope + ", giải được " + solvable + ", lỗi dữ liệu " + errors + " -> " + rep);
        }

        public static void ImportAndValidate()
        {
            BuzzyLevelImporter.ImportAll(BuzzyLevelImporter.DefaultSource);
            Run();
        }
    }
}
