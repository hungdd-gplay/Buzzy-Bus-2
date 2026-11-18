using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Buzzy.Core;
using Xunit;

public class CoreTests
{
    const string LevelDir = @"C:/Users/PC/Contacts/Downloads/Buzzy Bus Resource/Buzzy Bus/Levels";

    static LevelData Load(int n)
    {
        var path = Path.Combine(LevelDir, string.Format("level_{0:000}.json", n));
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var ld = doc.RootElement.GetProperty("LevelData");
        var img = ld.GetProperty("PixelImageData");
        var pixels = img.GetProperty("Pixels").EnumerateArray()
            .Select(p => new PixelData(p.GetProperty("x").GetInt32(), p.GetProperty("y").GetInt32(), p.GetProperty("ColorCode").GetInt32()));
        var queues = ld.GetProperty("CarQueueGroup").GetProperty("CarQueues").EnumerateArray()
            .Select(q => q.GetProperty("Cars").EnumerateArray()
                .Select(c => new CarData(c.GetProperty("ID").GetInt32(), c.GetProperty("Seats").GetInt32(), c.GetProperty("ColorCode").GetInt32()))
                .ToList()).ToList();
        return LevelData.Create(img.GetProperty("Width").GetInt32(), img.GetProperty("Height").GetInt32(), pixels, queues);
    }

    static LevelData Small(int w, int h, IEnumerable<PixelData> px, params CarData[][] cols)
    {
        return LevelData.Create(w, h, px, cols.Select(c => (IEnumerable<CarData>)c));
    }

    static IEnumerable<PixelData> Rect(int x0, int y0, int x1, int y1, int color)
    {
        for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) yield return new PixelData(x, y, color);
    }

    static void AssertSeatsMatchPixels(LevelData d)
    {
        var s = d.SeatsByColor(); var p = d.PixelsByColor();
        Assert.Equal(p.OrderBy(k => k.Key).ToList(), s.OrderBy(k => k.Key).ToList());
    }

    [Fact]
    public void Level001_LoadsCorrectly()
    {
        var d = Load(1);
        Assert.Equal(246, d.Pixels.Count);
        Assert.Equal(3, d.Queues.Count);
        Assert.Equal(6, d.Queues.Sum(q => q.Count));
        AssertSeatsMatchPixels(d);
    }

    [Fact]
    public void Level002_LoadsCorrectly()
    {
        var d = Load(2);
        Assert.Equal(324, d.Pixels.Count);
        Assert.Equal(24, d.GridWidth);
        Assert.Equal(20, d.GridHeight);
        AssertSeatsMatchPixels(d);
    }

    [Fact]
    public void Tap_ColorWithoutCar_IsNoMatch()
    {
        var d = Small(2, 0, new[] { new PixelData(0, 0, 0), new PixelData(1, 0, 5) },
            new[] { new CarData(0, 1, 0) }, new[] { new CarData(1, 1, 0) }, new[] { new CarData(2, 1, 0) });
        var g = new BusGame(d);
        var r = g.Tap(5);
        Assert.False(r.Matched);
        Assert.Equal(0, g.InFlight);
        Assert.Equal(2, g.Board.Count);
        Assert.False(g.Tap(9, 9).Matched); // ô ngoài lưới
    }

    [Fact]
    public void Tap_ClusterLargerThanSeats_TakesExactlySeats()
    {
        var d = Small(2, 2, Rect(0, 0, 2, 2, 0),
            new[] { new CarData(0, 4, 0) }, new CarData[0], new CarData[0]);
        var g = new BusGame(d);
        var r = g.Tap(1, 1); // chạm giữa cụm: dùng màu pixel
        Assert.True(r.Matched);
        Assert.Equal(4, r.Pixels.Count);
        Assert.Equal(5, g.Board.Count);
        Assert.Equal(4, r.Car.Reserved);
        Assert.Equal(0, r.Car.Boarded);   // chưa Arrive
        Assert.Equal(4, r.Car.Remaining);
        Assert.True(r.Car.IsFull);
        Assert.False(g.Tap(0).Matched);   // xe đã Full: không giữ thêm
    }

    [Fact]
    public void PickOrder_OuterLayerFirst_ThenNearest()
    {
        // lưới 5x3 đặc: viền độ sâu 1, hàng giữa x=1..3 độ sâu 2
        var b = new Board(5, 3, Rect(0, 0, 4, 2, 0));
        b.Remove(2, 2); // lộ (2,1) với độ sâu 2
        // luật đã chốt: pixel sâu bên trong VẪN lấy được; lớp ngoài (11 pixel viền) đứng trước 3 pixel lớp trong,
        // và trong lớp trong thì (2,1) gần ô bến nhất nên đứng đầu nhóm.
        var all = b.PickExposed(0, 100, 2, 1);
        Assert.Equal(14, all.Count);
        Assert.All(all.Take(11), p => Assert.True(p.Y != 1 || p.X == 0 || p.X == 4));
        Assert.Equal(new PixelData(2, 1, 0), all[11]);
        // trong lớp ngoài: gần ô bến trước
        var two = b.PickExposed(0, 2, 0, -3);
        Assert.Equal(new[] { 0, 1 }, two.Select(p => p.X).ToArray());
        Assert.All(two, p => Assert.Equal(0, p.Y));
        // pixel kẹt bên trong không "lộ" (chỉ ảnh hưởng hiển thị / thứ tự, không chặn lấy)
        Assert.False(b.IsExposed(1, 1));
        Assert.False(b.IsExposed(3, 1));
    }

    [Fact]
    public void Exposed_OutsideGridCountsAsEmpty()
    {
        var b = new Board(3, 3, Rect(0, 0, 2, 2, 1));
        Assert.True(b.IsExposed(0, 0));
        Assert.False(b.IsExposed(1, 1));
    }

    [Fact]
    public void FullCar_Departs_NextCarEnters_EmptyColumnGetsBarrier()
    {
        var d = Small(3, 0, new[] { new PixelData(0, 0, 0), new PixelData(1, 0, 0), new PixelData(2, 0, 1), new PixelData(3, 0, 2) },
            new[] { new CarData(10, 2, 0), new CarData(11, 1, 1) },
            new[] { new CarData(12, 1, 2) },
            new CarData[0]);
        var g = new BusGame(d);
        Assert.False(g.Barrier[0]);
        Assert.True(g.Barrier[2]);      // cột 2 không có xe: rào ngay từ đầu
        var departed = new List<int>();
        g.CarDeparted += (c, s) => departed.Add(c.Id);
        var r = g.Tap(0);
        Assert.Equal(10, g.Slots[0].Id); // đã giữ chỗ nhưng chưa lên -> chưa rời
        g.ArriveAll(r);
        Assert.Equal(11, g.Slots[0].Id); // xe kế xuống
        Assert.Equal(new[] { 10 }, departed);
        g.ArriveAll(g.Tap(2));           // cột 1 hết xe
        Assert.Null(g.Slots[1]);
        Assert.True(g.Barrier[1]);
        g.ArriveAll(g.Tap(1));
        Assert.True(g.Barrier[0]);
        Assert.Equal(GameStatus.Won, g.Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Greedy_SolvesSampleLevels(int n)
    {
        Assert.Equal(GameStatus.Won, Solver.Greedy(Load(n)));
    }

    [Fact]
    public void Deadlock_Detected_OnTinyLevel()
    {
        // 3 xe đỗ màu 0/1/2 nhưng ảnh chỉ còn màu 3 -> bế tắc ngay
        var d = Small(1, 0, new[] { new PixelData(0, 0, 3), new PixelData(1, 0, 3) },
            new[] { new CarData(0, 1, 0) }, new[] { new CarData(1, 1, 1) }, new[] { new CarData(2, 1, 2) });
        var g = new BusGame(d);
        Assert.Equal(GameStatus.Deadlock, g.Status);
        Assert.Equal(GameStatus.Deadlock, Solver.Greedy(d));
    }

    [Fact]
    public void NotDeadlock_WhileFlying_OrWhenTappable()
    {
        var d = Small(1, 0, new[] { new PixelData(0, 0, 0), new PixelData(1, 0, 0) },
            new[] { new CarData(0, 2, 0) }, new CarData[0], new CarData[0]);
        var g = new BusGame(d);
        Assert.Equal(GameStatus.Playing, g.Status);
        var r = g.Tap(0);
        Assert.Equal(GameStatus.Won, g.Status); // board hết pixel
        g.ArriveAll(r);
    }
}
