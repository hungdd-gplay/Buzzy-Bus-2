using System;
using System.Collections.Generic;
using System.Linq;
using Buzzy.Core;
using Xunit;

/// <summary>M6: in trạng thái bế tắc của level khó (chạy khi đặt BUZZY_DIAG=38,77,...). Không chạy mặc định.</summary>
public class DiagnoseTests
{
    [Fact]
    public void Diagnose_Deadlock()
    {
        var env = Environment.GetEnvironmentVariable("BUZZY_DIAG"); if (string.IsNullOrEmpty(env)) return;
        foreach (var s in env.Split(','))
        {
            int n = int.Parse(s); var lv = MechanicTests.LoadFull(n);
            var g = new BusGame(lv, null, new MechanicRules());
            var rnd = new Random(1); int taps = 0;
            while (g.Status == GameStatus.Playing && taps < 100000)
            {
                var cols = new List<int>();
                foreach (var car in g.Slots) if (car != null && !car.Locked && car.Free > 0 && g.Board.HasExposed(car.Color) && !cols.Contains(car.Color)) cols.Add(car.Color);
                if (cols.Count == 0) break;
                int code; var r = Solver.RandomTap(g, cols[rnd.Next(cols.Count)], rnd, out code); if (!r.Matched) break; g.ArriveAll(r); taps++;
            }
            Console.WriteLine("=== Lv." + n + " status=" + g.Status + " taps=" + taps + " pixelLeft=" + g.Board.Count);
            Console.WriteLine("frozen: " + string.Join(" | ", lv.Frozen.Select((f, i) => "rect(" + f.Rect.X + "," + f.Rect.Y + "," + f.Rect.W + "x" + f.Rect.H + ") hits=" + f.RequiredHits + " left=" + g.Board.FrozenLeft[i])));
            Console.WriteLine("mystery: " + lv.Mystery.Count + " keys: " + lv.Keys.Count + " walls: " + lv.Walls.Count + " spawners: " + lv.Spawners.Count);
            for (int i = 0; i < g.Slots.Length; i++) { var c = g.Slots[i]; Console.WriteLine("slot " + i + ": " + (c == null ? "trống" : "màu " + c.Color + " free " + c.Free + (c.Locked ? " KHÓA" : "")) + " ; cột còn " + g.Queues[i].Count + " xe: " + string.Join(",", g.Queues[i].Select(q => q.Color + (q.Locked ? "L" : "")))); }
            var byColor = new Dictionary<int, int[]>();
            for (int x = 0; x < g.Board.W; x++) for (int y = 0; y < g.Board.H; y++)
            {
                int col = g.Board.ColorAt(x, y); if (col == Board.Empty) continue;
                if (!byColor.ContainsKey(col)) byColor[col] = new int[4];
                byColor[col][0]++; if (g.Board.IsFrozen(x, y)) byColor[col][1]++; if (g.Board.IsHidden(x, y)) byColor[col][2]++; if (g.Board.IsExposed(x, y) && g.Board.Takeable(x, y)) byColor[col][3]++;
            }
            foreach (var kv in byColor.OrderBy(k => k.Key)) Console.WriteLine("màu " + kv.Key + ": còn " + kv.Value[0] + " (băng " + kv.Value[1] + ", ẩn " + kv.Value[2] + ", lấy được " + kv.Value[3] + ")");
            int seatsLeft = g.Slots.Where(c => c != null).Sum(c => c.Free) + g.Queues.Sum(q => q.Sum(c => c.Seats));
            Console.WriteLine("chỗ còn (bến + cột): " + seatsLeft);
            var variants = new Dictionary<string, MechanicRules> {
                { "gốc", new MechanicRules() },
                { "mysteryTakeable", new MechanicRules { MysteryBlocks = false } },
                { "frozenAny/1", new MechanicRules { FrozenReach = -1, FrozenHitDivisor = 1 } },
                { "frozenAny/2", new MechanicRules { FrozenReach = -1, FrozenHitDivisor = 2 } },
                { "frozenReach8/6", new MechanicRules { FrozenReach = 8, FrozenHitDivisor = 6 } },
            };
            foreach (var kv in variants) foreach (var sp in new[] { 0.0, 0.7 })
            {
                int wins; List<int> best; var t0 = DateTime.Now;
                Solver.Sample(lv, kv.Value, null, 200, 1, out wins, out best, false, sp);
                Console.WriteLine("luật " + kv.Key + " smart " + sp + ": thắng " + wins + "/200, chạm ngắn nhất " + (best == null ? "-" : best.Count.ToString()) + ", " + (DateTime.Now - t0).TotalSeconds.ToString("0.0") + "s");
            }
        }
    }

    /// <summary>M7: in bản đồ "có đường ra" (IsReachable) lúc đầu level: BUZZY_REACH=16,4</summary>
    [Fact]
    public void Diagnose_Reach()
    {
        var env = Environment.GetEnvironmentVariable("BUZZY_REACH"); if (string.IsNullOrEmpty(env)) return;
        foreach (var s in env.Split(','))
        {
            int n = int.Parse(s); var lv = MechanicTests.LoadFull(n);
            var g = new BusGame(lv, null, new MechanicRules()); var b = g.Board;
            int reach = 0, exp = 0, take = 0;
            for (int x = 0; x < b.W; x++) for (int y = 0; y < b.H; y++) { if (b.ColorAt(x, y) == Board.Empty) continue; if (b.IsReachable(x, y)) reach++; if (b.IsExposed(x, y)) exp++; if (b.Takeable(x, y)) take++; }
            Console.WriteLine("=== Lv." + n + " " + b.W + "x" + b.H + " pixel " + b.Count + " lộ " + exp + " có đường ra " + reach + " takeable " + take + " walls " + lv.Walls.Count);
            for (int y = b.H - 1; y >= 0; y--)
            {
                var sb = new System.Text.StringBuilder();
                for (int x = 0; x < b.W; x++) sb.Append(b.IsWall(x, y) ? 'W' : b.ColorAt(x, y) == Board.Empty ? '.' : b.IsReachable(x, y) ? 'R' : '#');
                Console.WriteLine(sb.ToString());
            }
            foreach (var c in g.Slots) if (c != null) Console.WriteLine("slot màu " + c.Color + " free " + c.Free + " GetExposed " + b.GetExposed(c.Color).Count);
        }
    }

    /// <summary>M7: đếm level bế tắc với luật 1b (ExposedOnly=true, flood cùng màu): BUZZY_RULE1B=1</summary>
    [Fact]
    public void Diagnose_Rule1b_Solvable()
    {
        if (Environment.GetEnvironmentVariable("BUZZY_RULE1B") != "1") return;
        var bad = new List<int>(); var rules = new MechanicRules { ExposedOnly = true };
        for (int n = 1; n <= 300; n++) if (!MechanicTests.OutOfScope(n) && !MechanicTests.RandomSolve(MechanicTests.LoadFull(n), rules, 100, n)) bad.Add(n);
        Console.WriteLine("LUẬT 1b BẾ TẮC (" + bad.Count + "): " + string.Join(",", bad));
    }
}
