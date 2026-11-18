using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Buzzy.Core;
using Xunit;

/// <summary>M4: LevelValidator trên 300 level JSON gốc (không cần Unity). Báo cáo ghi ra bin/level-report.md.</summary>
public class ValidatorTests
{
    static string LevelDir
    {
        get
        {
            var e = Environment.GetEnvironmentVariable("BUZZY_LEVELS");
            return string.IsNullOrEmpty(e) ? @"C:/Users/PC/Contacts/Downloads/Buzzy Bus Resource/Buzzy Bus/Levels" : e;
        }
    }

    /// <summary>Lý do ngoài phạm vi bản đầu đọc thẳng từ JSON (importer Unity ghi cùng nội dung vào BuzzyLevel.outOfScope).</summary>
    public static string OutOfScopeReason(int n)
    {
        var path = Path.Combine(LevelDir, string.Format("level_{0:000}.json", n));
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var ld = doc.RootElement.GetProperty("LevelData");
        var why = new List<string>();
        if (ld.GetProperty("PixelImageData").GetProperty("Elevators").GetArrayLength() > 0) why.Add("elevator");
        if (ld.TryGetProperty("QueueBoardGatePairs", out var g) && g.ValueKind == JsonValueKind.Array && g.GetArrayLength() > 0) why.Add("cổng đổi cột");
        bool two = false;
        foreach (var q in ld.GetProperty("CarQueueGroup").GetProperty("CarQueues").EnumerateArray())
            foreach (var c in q.GetProperty("Cars").EnumerateArray())
                if (c.TryGetProperty("SecondSeats", out var s) && s.ValueKind == JsonValueKind.Number && s.GetInt32() > 0) two = true;
        if (two) why.Add("xe 2 màu");
        return string.Join(";", why);
    }

    static int Tag(int n)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(LevelDir, string.Format("level_{0:000}.json", n))));
        return doc.RootElement.TryGetProperty("levelTag", out var t) ? t.GetInt32() : 0;
    }

    // Level trong phạm vi mà luật [thiết kế] hiện tại không giải được bằng chạm thuần (cần booster): xem README_Assumptions.md.
    static readonly int[] KnownHard = { 38, 77, 78, 145 };

    [Fact]
    public void Validator_AllLevels_ReportAndKnownHard()
    {
        if (!Directory.Exists(LevelDir)) return;   // máy khác không có bộ level gốc
        var opt = new ValidatorOptions { Tries = 100, Seed = 1 };
        var checks = new List<LevelCheck>();
        for (int n = 1; n <= 300; n++) checks.Add(LevelValidator.Check(MechanicTests.LoadFull(n), n, Tag(n), OutOfScopeReason(n), new MechanicRules(), opt));
        var md = LevelValidator.Markdown(checks, opt);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "level-report.md"), md);
        Console.WriteLine(md.Substring(0, Math.Min(1200, md.Length)));

        Assert.Equal(300, checks.Count);
        Assert.Empty(checks.Where(c => c.InScope && c.Errors.Count > 0).Select(c => c.Index + ": " + string.Join("; ", c.Errors)));
        var unsolvable = checks.Where(c => c.InScope && !c.Solvable).Select(c => c.Index).ToArray();
        Assert.Equal(KnownHard, unsolvable);
        // level khó (tag 1, 2) phải có tỉ lệ thắng ngẫu nhiên thấp hơn level thường (plan.md mục 4)
        double Avg(int tag) { var l = checks.Where(c => c.InScope && c.Tag == tag).ToList(); return l.Count == 0 ? 1 : l.Average(c => c.WinRate); }
        Assert.True(Avg(1) <= Avg(0) && Avg(2) <= Avg(0), string.Format("tỉ lệ thắng TB: tag0 {0:0.00} tag1 {1:0.00} tag2 {2:0.00}", Avg(0), Avg(1), Avg(2)));
    }

    [Fact]
    public void Validator_SchemaErrors()
    {
        // pixel nhiều hơn chỗ: lỗi; xe khoá không có chìa: lỗi; ghế thừa không ô phát: cảnh báo
        var px = new List<PixelData>(); for (int x = 0; x < 5; x++) px.Add(new PixelData(x, 0, 3));
        var d = LevelData.Create(4, 0, px, new[] { (IEnumerable<CarData>)new[] { new CarData(1, 4, 3) }, new CarData[0], new CarData[0] });
        var c = LevelValidator.Check(d, 999, 0, "", new MechanicRules(), new ValidatorOptions { Tries = 5 });
        Assert.Contains(c.Errors, e => e.Contains("pixel >"));

        var d2 = LevelData.Create(4, 0, px, new[] { (IEnumerable<CarData>)new[] { new CarData(1, 9, 3, true, 7) }, new CarData[0], new CarData[0] });
        var c2 = LevelValidator.Check(d2, 998, 0, "", new MechanicRules(), new ValidatorOptions { Tries = 5 });
        Assert.Contains(c2.Errors, e => e.Contains("không có chìa"));
        Assert.Contains(c2.Warnings, w => w.Contains("ghế thừa"));

        var d3 = LevelData.Create(4, 0, px, new[] { (IEnumerable<CarData>)new[] { new CarData(1, 5, 3) }, new CarData[0], new CarData[0] });
        var c3 = LevelValidator.Check(d3, 997, 0, "", new MechanicRules(), new ValidatorOptions { Tries = 5 });
        Assert.True(c3.Ok && c3.Solvable && c3.Taps == 1 && c3.WinRate == 1);
    }
}
