using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Buzzy.Core;
using Xunit;

/// <summary>M3: mechanic (Mystery, Frozen, Walls, Spawner, Key + Locked) và booster; chạy cả 300 level thật.</summary>
public class MechanicTests
{
    static string LevelDir
    {
        get
        {
            var e = Environment.GetEnvironmentVariable("BUZZY_LEVELS");
            return string.IsNullOrEmpty(e) ? @"C:/Users/PC/Contacts/Downloads/Buzzy Bus Resource/Buzzy Bus/Levels" : e;
        }
    }

    static int I(JsonElement e, string n, int d = 0) { return e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : d; }
    static RectData R(JsonElement e, string x = "x", string y = "y", string w = "Width", string h = "Height") { return new RectData(I(e, x), I(e, y), I(e, w), I(e, h)); }

    public static LevelData LoadFull(int n)
    {
        var path = Path.Combine(LevelDir, string.Format("level_{0:000}.json", n));
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var ld = doc.RootElement.GetProperty("LevelData");
        var img = ld.GetProperty("PixelImageData");
        var pixels = img.GetProperty("Pixels").EnumerateArray().Select(p => new PixelData(I(p, "x"), I(p, "y"), I(p, "ColorCode")));
        var queues = ld.GetProperty("CarQueueGroup").GetProperty("CarQueues").EnumerateArray()
            .Select(q => q.GetProperty("Cars").EnumerateArray()
                .Select(c => new CarData(I(c, "ID"), I(c, "Seats"), I(c, "ColorCode"), I(c, "IsLocked") != 0, I(c, "KeyColorCode"))).ToList()).ToList();
        var d = LevelData.Create(I(img, "Width"), I(img, "Height"), pixels, queues);
        foreach (var w in img.GetProperty("Walls").EnumerateArray()) d.Walls.Add(R(w));
        foreach (var f in img.GetProperty("FrozenAreas").EnumerateArray()) d.Frozen.Add(new FrozenData(R(f), I(f, "RequiredHits")));
        foreach (var k in img.GetProperty("Keys").EnumerateArray()) d.Keys.Add(new KeyData(R(k), I(k, "ColorCode")));
        foreach (var s in img.GetProperty("Spawners").EnumerateArray())
            d.Spawners.Add(new SpawnerData(R(s), R(s, "SpawnAreaX", "SpawnAreaY", "SpawnAreaWidth", "SpawnAreaHeight"), I(s, "Facing"),
                s.GetProperty("WaveColors").EnumerateArray().Select(v => v.GetInt32()).ToArray()));
        foreach (var m in img.GetProperty("SurprisePixels").GetProperty("Pixels").EnumerateArray()) d.Mystery.Add(new PixelData(I(m, "X"), I(m, "Y"), -1));
        return d;
    }

    static LevelData Small(int w, int h, IEnumerable<PixelData> px, params CarData[][] cols)
    {
        return LevelData.Create(w, h, px, cols.Select(c => (IEnumerable<CarData>)c));
    }

    static IEnumerable<PixelData> Rect(int x0, int y0, int x1, int y1, int color)
    {
        for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) yield return new PixelData(x, y, color);
    }

    // ---------- Mystery ----------
    [Fact]
    public void Mystery_HiddenUntilExposed()
    {
        // khối 5x5 màu 0, 9 ô giữa là Mystery
        var d = Small(4, 4, Rect(0, 0, 4, 4, 0), new[] { new CarData(1, 25, 0) });
        for (int x = 1; x <= 3; x++) for (int y = 1; y <= 3; y++) d.Mystery.Add(new PixelData(x, y, -1));
        var g = new BusGame(d);
        Assert.True(g.Board.IsHidden(2, 2));
        Assert.False(g.Board.Takeable(2, 2));
        Assert.Equal(16, g.Board.GetExposed(0).Count);   // chỉ viền
        Assert.Equal(GameStatus.Won, Solver.Greedy(d));  // lấy viền thì lõi lộ ra, lấy tiếp được
    }

    // ---------- Walls ----------
    [Fact]
    public void Walls_DoNotExposeNeighbours_ButBlockDepth()
    {
        var d = Small(2, 0, Rect(0, 0, 2, 0, 0), new[] { new CarData(1, 3, 0) });
        d.Walls.Add(new RectData(-1, -1, 5, 1)); d.Walls.Add(new RectData(-1, 1, 5, 1));   // tường trên + dưới
        var g = new BusGame(d);
        Assert.False(g.Board.IsExposed(1, 0));   // ô giữa chỉ kề hai pixel + hai tường
        Assert.True(g.Board.IsExposed(0, 0));    // đầu trái kề ô rỗng x=-1 (tường chỉ che trên/dưới)
    }

    // ---------- Frozen ----------
    [Fact]
    public void Frozen_PixelsLockedUntilHitsReached()
    {
        // 6x1 pixel màu 0 ở x 0..5 (tự do) + vùng băng 2x1 ở x 6..7 (màu 1). Hits=24 / 12 = 2.
        var px = Rect(0, 0, 5, 0, 0).Concat(Rect(6, 0, 7, 0, 1)).ToList();
        var d = Small(7, 0, px, new[] { new CarData(1, 6, 0), new CarData(2, 2, 1) });
        d.Frozen.Add(new FrozenData(new RectData(6, 0, 2, 1), 24));
        var g = new BusGame(d);
        Assert.True(g.Board.IsFrozen(6, 0));
        Assert.Empty(g.Board.GetExposed(1));
        var broken = new List<int>(); g.FrozenBroken += i => broken.Add(i);
        var r = g.Tap(0); g.ArriveAll(r);   // lấy 6 pixel màu 0 (>= 2 hit gần vùng băng)
        Assert.Equal(new[] { 0 }, broken);
        Assert.False(g.Board.IsFrozen(6, 0));
        Assert.Equal(2, g.Board.GetExposed(1).Count);
    }

    // ---------- Spawner ----------
    [Fact]
    public void Spawner_RefillsAreaWhenCleared()
    {
        var px = Rect(0, 0, 1, 0, 0).ToList();
        var d = Small(3, 2, px, new[] { new CarData(1, 2, 0), new CarData(2, 4, 5) });
        d.Spawners.Add(new SpawnerData(new RectData(0, 2, 2, 1), new RectData(2, 0, 2, 2), 0, new[] { 5 }));
        var g = new BusGame(d);
        Assert.Equal(2 + 4, g.Board.Count);            // đợt 0 (4 ô, màu 5) đổ ngay
        var spawned = new List<PixelData>(); g.PixelSpawned += p => spawned.Add(p);
        Assert.Equal(GameStatus.Won, Solver.Greedy(g));
        Assert.True(g.Board.Count == 0 && !g.Board.HasPendingWaves);
    }

    [Fact]
    public void Spawner_SecondWaveAppearsOnlyAfterAreaEmpty()
    {
        var d = Small(3, 1, Rect(0, 0, 0, 0, 0), new[] { new CarData(1, 1, 0), new CarData(2, 2, 5), new CarData(3, 2, 6) });
        d.Spawners.Add(new SpawnerData(new RectData(0, 3, 1, 1), new RectData(1, 0, 2, 1), 0, new[] { 5, 6 }));
        var g = new BusGame(d);
        Assert.Equal(1 + 2, g.Board.Count);
        Assert.True(g.Board.HasPendingWaves);
        Assert.Equal(GameStatus.Won, Solver.Greedy(g));
    }

    // ---------- Key + Locked ----------
    [Fact]
    public void LockedCar_WaitsForKey_ThenEnters()
    {
        // cột 0: xe 1 (màu 0, 4 chỗ, xích, key màu 7). Chìa ở (5,0) 1x1 giả lập 3x3 nhỏ; vòng quanh là pixel màu 1.
        var px = Rect(0, 0, 3, 0, 0).Concat(new[] { new PixelData(4, 0, 1) }).ToList();
        var d = Small(6, 0, px,
            new[] { new CarData(1, 4, 0, true, 7) },
            new[] { new CarData(2, 1, 1) });
        d.Keys.Add(new KeyData(new RectData(5, 0, 1, 1), 7));
        var g = new BusGame(d, null, new MechanicRules { LockedEntersSlot = false });
        Assert.Null(g.Slots[0]); Assert.True(g.IsWaiting(0));
        Assert.Equal(GameStatus.Playing, g.Status);
        var unlocked = new List<Car>(); g.CarUnlocked += c => unlocked.Add(c);
        var r = g.Tap(1); g.ArriveAll(r);               // lấy pixel màu 1 quanh chìa (4,0)
        Assert.Single(unlocked);
        Assert.NotNull(g.Slots[0]); Assert.Equal(1, g.Slots[0].Id);
        Assert.Equal(GameStatus.Won, Solver.Greedy(g));
    }

    [Fact]
    public void LockedCar_OccupiesSlot_ButTakesNoPixels_UntilKey()
    {
        var px = Rect(0, 0, 3, 0, 0).Concat(new[] { new PixelData(4, 0, 1) }).ToList();
        var d = Small(6, 0, px, new[] { new CarData(1, 4, 0, true, 7) }, new[] { new CarData(2, 1, 1) });
        d.Keys.Add(new KeyData(new RectData(5, 0, 1, 1), 7));
        var g = new BusGame(d, null, new MechanicRules { LockedEntersSlot = true });
        Assert.NotNull(g.Slots[0]); Assert.True(g.Slots[0].Locked);
        Assert.False(g.Tap(0).Matched);                 // xe xích: chạm màu 0 không nhận
        var r = g.Tap(1); g.ArriveAll(r);               // lấy pixel quanh chìa
        Assert.False(g.Slots[0].Locked);
        Assert.Equal(GameStatus.Won, Solver.Greedy(g));
    }

    // ---------- Booster ----------
    [Fact]
    public void VipBus_ClearsParkedCar_AndRemovesItsPixels()
    {
        var d = Small(3, 0, Rect(0, 0, 3, 0, 0), new[] { new CarData(1, 4, 0), new CarData(2, 4, 0) });
        var g = new BusGame(d);
        Assert.True(g.VipBus(0));
        Assert.Equal(0, g.Board.Count);
        Assert.Equal(GameStatus.Won, g.Status);
        Assert.Equal(2, g.Slots[0].Id);
    }

    [Fact]
    public void VipBus_RefusesWhileFlying()
    {
        var d = Small(3, 0, Rect(0, 0, 3, 0, 0), new[] { new CarData(1, 4, 0) });
        var g = new BusGame(d);
        var r = g.Tap(0);
        Assert.False(g.VipBus(0));
    }

    [Fact]
    public void Claw_MovesQueuedCarToEmptySlot_OrRefuses()
    {
        var d = Small(2, 0, Rect(0, 0, 2, 0, 0),
            new[] { new CarData(1, 1, 0), new CarData(3, 1, 0) },
            new[] { new CarData(2, 1, 0) });
        var g = new BusGame(d);
        Assert.True(g.Claw(0, 0));     // không có ô trống: đổi chỗ với xe đang đỗ ở ô 0 (chưa nhận pixel)
        Assert.Equal(3, g.Slots[0].Id);
        Assert.Equal(1, g.Queues[0].Peek().Id);   // xe đỗ cũ về chỗ xe kẹp trong hàng chờ
        var tap = g.Tap(0); Assert.True(tap.Matched);
        g.Claw(0, 0);
        Assert.Equal(3, g.Slots[0].Id);   // xe 3 đã nhận pixel: không bị đẩy
    }

    [Fact]
    public void Claw_UsesWaitingSlot()
    {
        // cột 0 đầu cột xe xích -> ô 0 chờ; xe 2 sau nó bị kẹp thả vào ô 0.
        var d = Small(1, 0, Rect(0, 0, 1, 0, 0),
            new[] { new CarData(1, 1, 0, true, 9), new CarData(2, 2, 0) },
            new[] { new CarData(3, 1, 1) });
        var g = new BusGame(d, null, new MechanicRules { LockedEntersSlot = false });
        Assert.True(g.IsWaiting(0));
        Assert.True(g.Claw(0, 1));
        Assert.Equal(2, g.Slots[0].Id);
        Assert.Single(g.Queues[0]);
    }

    [Fact]
    public void SkyGrab_TakesUpToTenIncludingHidden()
    {
        var d = Small(4, 4, Rect(0, 0, 4, 4, 0), new[] { new CarData(1, 25, 0) });
        for (int x = 1; x <= 3; x++) for (int y = 1; y <= 3; y++) d.Mystery.Add(new PixelData(x, y, -1));
        var g = new BusGame(d);
        var r = g.SkyGrab(0);
        Assert.True(r.Matched);
        Assert.Equal(10, r.Pixels.Count);
        Assert.Equal(15, g.Board.Count);
    }

    [Fact]
    public void MagicWand_RecolorsCar_AndSwapsWithQueuedCar()
    {
        var d = Small(3, 1, Rect(0, 0, 1, 1, 0).Concat(Rect(2, 0, 3, 0, 1)).Concat(Rect(2, 1, 2, 1, 1)).Concat(Rect(0, 2, 2, 2, 2)),
            new[] { new CarData(1, 4, 0), new CarData(2, 3, 1) },
            new[] { new CarData(3, 3, 2), new CarData(4, 3, 1) });
        var g = new BusGame(d);
        // xe 3 màu 2 (3 chỗ) đỗ ở ô 1; xe chờ màu 1 sớm nhất là xe 4 (cột 1)
        Assert.True(g.MagicWand(0, 1));              // xe 1 (màu 0, 4 chỗ) <-> xe chờ màu 1
        Assert.Equal(1, g.Slots[0].Color);
        Assert.Equal(3, g.Slots[0].Seats);
        Assert.Equal(0, g.Queues[0].Peek().Color);   // xe chờ nhận màu 0 và 4 chỗ
        Assert.Equal(4, g.Queues[0].Peek().Seats);
        Assert.False(g.MagicWand(0, 1));             // cùng màu: từ chối
    }

    // Level có mechanic ngoài phạm vi bản đầu: xe 2 màu, Elevator, cổng đổi cột.
    static bool OutOfScope(int n)
    {
        var path = Path.Combine(LevelDir, string.Format("level_{0:000}.json", n));
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var ld = doc.RootElement.GetProperty("LevelData");
        if (ld.GetProperty("PixelImageData").GetProperty("Elevators").GetArrayLength() > 0) return true;
        if (ld.TryGetProperty("QueueBoardGatePairs", out var g) && g.ValueKind == JsonValueKind.Array && g.GetArrayLength() > 0) return true;
        foreach (var q in ld.GetProperty("CarQueueGroup").GetProperty("CarQueues").EnumerateArray())
            foreach (var c in q.GetProperty("Cars").EnumerateArray()) if (I(c, "SecondSeats") > 0) return true;
        return false;
    }

    // Giải ngẫu nhiên có khởi động lại: mỗi bước chọn ngẫu nhiên một màu chạm được (không chỉ xe nhiều chỗ nhất).
    static bool RandomSolve(LevelData d, MechanicRules rules, int tries, int seed)
    {
        var rnd = new Random(seed);
        for (int t = 0; t < tries; t++)
        {
            var g = new BusGame(d, null, rules);
            int guard = 100000;
            while (g.Status == GameStatus.Playing && guard-- > 0)
            {
                var cols = new List<int>();
                foreach (var car in g.Slots) if (car != null && car.Free > 0 && g.Board.HasExposed(car.Color) && !cols.Contains(car.Color)) cols.Add(car.Color);
                if (cols.Count == 0) break;
                var r = g.Tap(cols[rnd.Next(cols.Count)]);
                if (!r.Matched) break;
                g.ArriveAll(r);
            }
            if (g.Status == GameStatus.Won) return true;
        }
        return false;
    }

    // Level trong phạm vi mà luật [thiết kế] hiện tại không giải được bằng chạm thuần (cần booster Sky Grab / VIP Bus):
    // 38, 145 (Mystery); 77, 78 (Frozen). Ghi ở README_Assumptions.md. Thêm level bế tắc mới thì test này đỏ.
    static readonly int[] KnownHard = { 38, 77, 78, 145 };

    [Fact]
    public void InScopeLevels_SolvableByRandomRestarts_ExceptKnownHard()
    {
        var bad = new List<int>();
        for (int n = 1; n <= 300; n++) if (!OutOfScope(n) && !RandomSolve(LoadFull(n), new MechanicRules(), 100, n)) bad.Add(n);
        Console.WriteLine("BẾ TẮC: " + string.Join(",", bad));
        Assert.Equal(KnownHard, bad.ToArray());
    }

    // ---------- Toàn bộ 300 level ----------
    [Fact]
    public void AllLevels_Report()
    {
        var lines = new List<string>(); int dead = 0, won = 0, oos = 0;
        for (int n = 1; n <= 300; n++)
        {
            var d = LoadFull(n);
            var g = new BusGame(d);
            var st = Solver.Greedy(g);
            if (OutOfScope(n)) { oos++; continue; }
            if (st == GameStatus.Won) won++; else { dead++; lines.Add(string.Format("L{0}: {1} (còn {2} pixel; băng {3}, chìa {4}, ô phát {5}, ẩn {6})", n, st, g.Board.Count, d.Frozen.Count, d.Keys.Count, d.Spawners.Count, d.Mystery.Count)); }
        }
        lines.Insert(0, string.Format("trong phạm vi: thắng {0}, bế tắc {1}; ngoài phạm vi (xe 2 màu / Elevator / cổng): {2}", won, dead, oos));
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "solve_report.txt"), lines);
        Console.WriteLine(string.Join("\n", lines.Take(60)));
        Assert.True(true);
    }
}
