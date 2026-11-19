using System.Collections.Generic;
using System.Linq;
using Buzzy.Core;
using Xunit;

/// <summary>Góp ý 11: luật 4 booster theo clip gốc (visual-check B1, B3, B5, B7; design-decisions 4–6).</summary>
public class BoosterTests
{
    static LevelData Small(int w, int h, IEnumerable<PixelData> px, params CarData[][] cols)
    {
        return LevelData.Create(w, h, px, cols.Select(c => (IEnumerable<CarData>)c));
    }

    static IEnumerable<PixelData> Rect(int x0, int y0, int x1, int y1, int color)
    {
        for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) yield return new PixelData(x, y, color);
    }

    // ---------- VIP Bus (B1) ----------
    [Fact]
    public void VipBusFill_TakesExactlyFreeSeats_IncludingEnclosed()
    {
        // 3x3 đặc màu 0, xe 5 chỗ: lấy 5 pixel (kể cả ô giữa bị bao kín nếu cụm chọn thế), còn 4 trên ảnh; Full ngay; đáp hết thì xe rời, xe kế xuống.
        var d = Small(2, 2, Rect(0, 0, 2, 2, 0), new[] { new CarData(1, 5, 0), new CarData(2, 4, 0) });
        var g = new BusGame(d);
        var r = g.VipBusFill(0);
        Assert.True(r.Matched);
        Assert.Equal(5, r.Pixels.Count);
        Assert.Equal(4, g.Board.Count);
        Assert.True(g.Slots[0].IsFull);
        Assert.Equal(1, g.Slots[0].Id);          // chưa rời: pixel còn đang bay
        g.ArriveAll(r);
        Assert.Equal(2, g.Slots[0].Id);          // rời sau khi đáp đủ
        Assert.Equal(GameStatus.Playing, g.Status);
    }

    [Fact]
    public void VipBusFill_GhostSeatsWhenNotEnoughPixels()
    {
        // chỉ 2 pixel màu 0, xe 4 chỗ: 2 bay thật, 2 chỗ "ma" ghi thẳng → xe vẫn rời khi 2 pixel đáp; ảnh rỗng → thắng.
        var d = Small(1, 0, Rect(0, 0, 1, 0, 0), new[] { new CarData(1, 4, 0) });
        var g = new BusGame(d);
        var r = g.VipBusFill(0);
        Assert.Equal(2, r.Pixels.Count);
        Assert.Equal(2, g.Slots[0].Boarded);
        Assert.True(g.Slots[0].IsFull);
        g.ArriveAll(r);
        Assert.Equal(GameStatus.Won, g.Status);
    }

    [Fact]
    public void VipBusFill_NoPixelOfColor_DepartsImmediately()
    {
        var d = Small(1, 0, Rect(0, 0, 1, 0, 1), new[] { new CarData(1, 3, 0), new CarData(2, 2, 1) });
        var g = new BusGame(d);
        int departed = 0; g.CarDeparted += (c, s) => departed++;
        var r = g.VipBusFill(0);
        Assert.True(r.Matched);
        Assert.Empty(r.Pixels);
        Assert.Equal(1, departed);
        Assert.Equal(2, g.Slots[0].Id);
        Assert.False(g.VipBusFill(5).Matched);    // ô sai
    }

    [Fact]
    public void FillPick_BiggestClusterFirst_ThenOuterLayerRule()
    {
        // màu 0: cụm trái 2x2 (4 pixel) + cụm phải 1x2 (2 pixel), giữa là màu 1. Xe 3 chỗ: luật 0 lấy 3 pixel đều từ cụm trái.
        var px = Rect(0, 0, 1, 1, 0).Concat(Rect(2, 0, 2, 1, 1)).Concat(Rect(3, 0, 3, 1, 0));
        var d = Small(3, 1, px, new[] { new CarData(1, 3, 0) });
        var g = new BusGame(d);
        var r = g.VipBusFill(0);
        Assert.Equal(3, r.Pixels.Count);
        Assert.All(r.Pixels, p => Assert.True(p.X <= 1));
        // luật 1 (lớp ngoài như chạm): vẫn đúng 3 pixel, tổng pixel màu 0 còn lại 3
        var g2 = new BusGame(d); g2.Boosters.FillPick = 1;
        var r2 = g2.VipBusFill(0);
        Assert.Equal(3, r2.Pixels.Count);
        Assert.Equal(3, g2.Board.CountColor(0));
    }

    [Fact]
    public void VipBus_OldInstantVersion_StillWorksForRevive()
    {
        var d = Small(3, 0, Rect(0, 0, 3, 0, 0), new[] { new CarData(1, 4, 0), new CarData(2, 4, 0) });
        var g = new BusGame(d);
        Assert.True(g.VipBus(0));
        Assert.Equal(0, g.Board.Count);
        Assert.Equal(2, g.Slots[0].Id);
    }

    // ---------- Sky Grab (B3) ----------
    [Fact]
    public void SkyGrab_AreaAllColors_MatchedGoToCar_RestVanish()
    {
        // 5x5: hàng y<=2 màu 0, hàng y>=3 màu 1. Xe đỗ: ô 0 màu 0 (50 chỗ); không có xe màu 1.
        var px = Rect(0, 0, 4, 2, 0).Concat(Rect(0, 3, 4, 4, 1));
        var d = Small(4, 4, px, new[] { new CarData(1, 50, 0) });
        var g = new BusGame(d);
        var r = g.SkyGrab(2.0, 2.0);                // bán kính 2,5 quanh (2,2)
        Assert.True(r.Matched);
        int expect = 0; for (int x = 0; x <= 4; x++) for (int y = 0; y <= 4; y++) if ((x - 2) * (x - 2) + (y - 2) * (y - 2) <= 6.25) expect++;
        Assert.Equal(expect, r.Count);
        Assert.Single(r.ToCars);
        Assert.Equal(1, r.ToCars[0].Car.Id);
        Assert.All(r.ToCars[0].Pixels, p => Assert.Equal(0, p.Color));
        Assert.All(r.Vanished, p => Assert.Equal(1, p.Color));
        Assert.True(r.Vanished.Count > 0);
        Assert.Equal(25 - expect, g.Board.Count);
        Assert.Equal(r.ToCars[0].Pixels.Count, g.Slots[0].Reserved);
        g.ArriveAll(r);
        Assert.Equal(r.ToCars[0].Pixels.Count, g.Slots[0].Boarded);
        Assert.Equal(0, g.InFlight);
    }

    [Fact]
    public void SkyGrab_UnmatchedStay_WhenRuleOff_AndEmptyAreaNoMatch()
    {
        var px = Rect(0, 0, 4, 2, 0).Concat(Rect(0, 3, 4, 4, 1));
        var d = Small(4, 4, px, new[] { new CarData(1, 50, 0) });
        var g = new BusGame(d); g.Boosters.SkyGrabVanishUnmatched = false;
        var r = g.SkyGrab(2.0, 4.0);
        Assert.True(r.Matched);
        Assert.Empty(r.Vanished);
        Assert.Equal(10, g.Board.CountColor(1));    // màu 1 ở lại
        Assert.False(g.SkyGrab(40.0, 40.0).Matched); // ngoài ảnh
    }

    [Fact]
    public void SkyGrab_RespectsFreeSeats()
    {
        var d = Small(4, 4, Rect(0, 0, 4, 4, 0), new[] { new CarData(1, 3, 0) });
        var g = new BusGame(d);
        var r = g.SkyGrab(2.0, 2.0);
        Assert.Equal(3, r.ToCars[0].Pixels.Count);
        Assert.True(r.Vanished.Count > 0);           // phần dư biến mất (luật mặc định)
        Assert.True(g.Slots[0].IsFull);
    }

    // ---------- Magic Wand (B5) ----------
    [Fact]
    public void Shuffle_PicksColorWithMostPixels_AndKeepsAllCars()
    {
        // màu 2 có 20 pixel, màu 0 / 1 mỗi màu 2. Đỗ: (màu0, màu1, màu2); cột chờ có xe màu 2.
        var px = Rect(0, 0, 9, 1, 2).Concat(Rect(0, 2, 1, 2, 0)).Concat(Rect(2, 2, 3, 2, 1));
        var d = Small(9, 2, px,
            new[] { new CarData(1, 5, 0), new CarData(4, 5, 2), new CarData(7, 5, 0) },
            new[] { new CarData(2, 5, 1), new CarData(5, 5, 2) },
            new[] { new CarData(3, 5, 2), new CarData(6, 5, 1) });
        var g = new BusGame(d);
        var all = new HashSet<int>(); foreach (var c in g.Slots) all.Add(c.Id); foreach (var q in g.Queues) foreach (var c in q) all.Add(c.Id);
        var r = g.Shuffle();
        Assert.True(r.Ok);
        Assert.All(g.Slots, c => Assert.Equal(2, c.Color));
        var after = new List<int>(); foreach (var c in g.Slots) after.Add(c.Id); foreach (var q in g.Queues) foreach (var c in q) after.Add(c.Id);
        Assert.Equal(all.Count, after.Count);
        Assert.True(all.SetEquals(after));                       // không mất / không nhân đôi xe
        Assert.Equal(2, g.Queues[0].Count); Assert.Single(g.Queues[1]); Assert.Single(g.Queues[2]);   // số xe mỗi cột không đổi (xe đỗ cũ điền vào chỗ trống)
        Assert.Equal(GameStatus.Playing, g.Status);
    }

    [Fact]
    public void Shuffle_CountsExposedPixelsOnly_Rule3a()
    {
        // chốt 3a: màu A khối 7x7 (49 pixel, chỉ 24 lộ); màu B dải 20x2 (40 pixel, lộ cả 40). Đếm "lấy được" thì A thắng, đếm "lộ" thì B thắng.
        const int A = 0, B = 1;
        var px = Rect(0, 0, 6, 6, A).Concat(Rect(0, 8, 19, 9, B));
        var d = Small(20, 10, px,
            new[] { new CarData(1, 5, A), new CarData(4, 5, B), new CarData(5, 5, B) },
            new[] { new CarData(2, 5, A), new CarData(6, 5, B) },
            new[] { new CarData(3, 5, A), new CarData(7, 5, A) });
        var g = new BusGame(d);
        var all = new HashSet<int>(); foreach (var c in g.Slots) all.Add(c.Id); foreach (var q in g.Queues) foreach (var c in q) all.Add(c.Id);
        var r = g.Shuffle();
        Assert.True(r.Ok);
        Assert.All(g.Slots, c => Assert.Equal(B, c.Color));             // 3 xe B (40 pixel lộ) thay 3 xe A (49 pixel nhưng chỉ 24 lộ)
        var after = new List<int>(); foreach (var c in g.Slots) after.Add(c.Id); foreach (var q in g.Queues) foreach (var c in q) after.Add(c.Id);
        Assert.True(all.SetEquals(after) && all.Count == after.Count);
        Assert.Equal(2, g.Queues[0].Count); Assert.Single(g.Queues[1]); Assert.Single(g.Queues[2]);
        Assert.All(g.Queues[0], c => Assert.Equal(A, c.Color));           // xe A cũ về chỗ xe B rời khỏi cột
    }

    [Fact]
    public void Shuffle_SkipsCarWithPixelsInFlight_AndRandomRuleKeepsCars()
    {
        var px = Rect(0, 0, 9, 1, 2).Concat(Rect(0, 2, 1, 2, 0));
        var d = Small(9, 2, px,
            new[] { new CarData(1, 2, 0), new CarData(4, 5, 2) },
            new[] { new CarData(2, 5, 2), new CarData(5, 5, 2) },
            new[] { new CarData(3, 5, 2), new CarData(6, 5, 2) });
        var g = new BusGame(d);
        var tap = g.Tap(0);                       // xe 1 đang nhận pixel: không được đổi
        Assert.True(tap.Matched);
        var r = g.Shuffle();
        Assert.True(r.Ok);
        Assert.Equal(1, g.Slots[0].Id);
        Assert.Null(r.NewSlots[0]);
        var g2 = new BusGame(d); g2.Boosters.WandPick = 1;
        var ids = new HashSet<int>(); foreach (var c in g2.Slots) ids.Add(c.Id); foreach (var q in g2.Queues) foreach (var c in q) ids.Add(c.Id);
        g2.Shuffle();
        var ids2 = new List<int>(); foreach (var c in g2.Slots) ids2.Add(c.Id); foreach (var q in g2.Queues) foreach (var c in q) ids2.Add(c.Id);
        Assert.True(ids.SetEquals(ids2)); Assert.Equal(ids.Count, ids2.Count);
    }

    // ---------- Bus Claw (B7) ----------
    [Fact]
    public void Claw_TakesAnyQueueCar_FillsOnLane_ThenDeparts()
    {
        // cột 0: xe 1 (đỗ, màu 0), chờ [xe 2 màu 1 (2 chỗ), xe 3 màu 1 (1 chỗ)]; ảnh có 3 pixel màu 1. Kẹp xe 3 (vị trí 1).
        var d = Small(2, 0, Rect(0, 0, 2, 0, 1), new[] { new CarData(1, 2, 0), new CarData(2, 2, 1), new CarData(3, 1, 1) });
        var g = new BusGame(d);
        Car gone = null; g.LaneCarDeparted += c => gone = c;
        var r = g.Claw(0, 1);
        Assert.True(r.Ok);
        Assert.Equal(3, r.Car.Id);
        Assert.Single(r.Pixels);
        Assert.Contains(r.Car, g.LaneCars);
        Assert.Single(g.Queues[0]); Assert.Equal(2, g.Queues[0].Peek().Id);
        Assert.Equal(1, g.Slots[0].Id);                    // ô không đổi
        Assert.Equal(2, g.Board.Count);
        g.ArriveAll(r);
        Assert.Same(r.Car, gone);
        Assert.Empty(g.LaneCars);
        Assert.Equal(0, g.InFlight);
    }

    [Fact]
    public void Claw_NoPixels_DepartsAtOnce_AndRefusesLockedOrBadPos()
    {
        var d = Small(1, 0, Rect(0, 0, 1, 0, 0), new[] { new CarData(1, 2, 0), new CarData(2, 2, 5), new CarData(3, 1, 0, true, 9) });
        var g = new BusGame(d);
        Car gone = null; g.LaneCarDeparted += c => gone = c;
        var r = g.Claw(0, 0);                              // xe 2 màu 5: không có pixel → rời ngay (chỗ ma)
        Assert.True(r.Ok); Assert.Empty(r.Pixels); Assert.Same(r.Car, gone); Assert.Empty(g.LaneCars);
        Assert.False(g.Claw(0, 0).Ok);                     // còn lại xe 3 xích
        Assert.False(g.Claw(1, 0).Ok);                     // cột rỗng
        Assert.False(g.Claw(0, 7).Ok);
    }
}
