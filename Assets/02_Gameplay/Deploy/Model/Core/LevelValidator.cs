using System;
using System.Collections.Generic;
using System.Text;

namespace Buzzy.Core
{
    /// <summary>Ngưỡng xếp độ khó theo tỉ lệ thắng khi chơi ngẫu nhiên `[thiết kế]` (design-decisions.md mục 14): dữ liệu, không hằng số rải trong code.</summary>
    public sealed class ValidatorOptions
    {
        /// <summary>Số ván chơi ngẫu nhiên mỗi level (tỉ lệ thắng = độ khó).</summary>
        public int Tries = 100;
        public int Seed = 1;
        /// <summary>Tỉ lệ thắng ngẫu nhiên dưới mức này = "khó" (tương ứng levelTag 1).</summary>
        public double HardWinRate = 0.5;
        /// <summary>Dưới mức này = "siêu khó" (levelTag 2).</summary>
        public double SuperHardWinRate = 0.2;
    }

    /// <summary>Kết quả kiểm 1 level: lỗi dữ liệu, mechanic, giải được hay không, độ khó.</summary>
    public sealed class LevelCheck
    {
        public int Index, Tag;
        public int GridW, GridH, PixelCount, CarCount, SeatTotal;
        public string Mechanics = "";
        /// <summary>Lý do ngoài phạm vi bản đầu (xe 2 màu / Elevator / cổng đổi cột); rỗng = trong phạm vi.</summary>
        public string OutOfScope = "";
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        /// <summary>Có ít nhất 1 ván ngẫu nhiên thắng.</summary>
        public bool Solvable;
        /// <summary>Số lần chạm của ván thắng ngắn nhất (0 nếu không thắng).</summary>
        public int Taps;
        /// <summary>Tỉ lệ ván ngẫu nhiên thắng (0..1).</summary>
        public double WinRate;
        /// <summary>0 thường, 1 khó, 2 siêu khó theo ValidatorOptions.</summary>
        public int DifficultyBand;

        public bool InScope { get { return OutOfScope.Length == 0; } }
        public bool Ok { get { return Errors.Count == 0 && (!InScope || Solvable); } }
    }

    /// <summary>
    /// Kiểm dữ liệu level + giải thử bằng luật lõi (M4). Không phụ thuộc engine; chạy được trong test, Unity Editor, tool dòng lệnh.
    /// </summary>
    public static class LevelValidator
    {
        public static LevelCheck Check(LevelData d, int index, int tag, string outOfScope = "", MechanicRules rules = null, ValidatorOptions opt = null)
        {
            opt = opt ?? new ValidatorOptions();
            var c = new LevelCheck { Index = index, Tag = tag, GridW = d.GridWidth, GridH = d.GridHeight, PixelCount = d.Pixels.Count, OutOfScope = outOfScope ?? "" };
            foreach (var q in d.Queues) foreach (var car in q) { c.CarCount++; c.SeatTotal += car.Seats; }
            c.Mechanics = MechanicList(d);
            Schema(d, c);
            if (c.InScope && d.Queues.Count == BusGame.SlotCount)   // lỗi dữ liệu khác vẫn giải thử để biết có thắng được không
            {
                int wins; List<int> best;
                Solver.Sample(d, rules, null, opt.Tries, opt.Seed, out wins, out best);
                c.Solvable = best != null; c.Taps = best == null ? 0 : best.Count;
                c.WinRate = opt.Tries > 0 ? (double)wins / opt.Tries : 0;
                c.DifficultyBand = !c.Solvable || c.WinRate < opt.SuperHardWinRate ? 2 : c.WinRate < opt.HardWinRate ? 1 : 0;
            }
            return c;
        }

        public static string MechanicList(LevelData d)
        {
            var m = new List<string>();
            if (d.Mystery.Count > 0) m.Add("Mystery " + d.Mystery.Count);
            if (d.Frozen.Count > 0) m.Add("Frozen " + d.Frozen.Count);
            if (d.Walls.Count > 0) m.Add("Walls " + d.Walls.Count);
            if (d.Spawners.Count > 0) m.Add("Spawner " + d.Spawners.Count);
            if (d.Keys.Count > 0) m.Add("Key " + d.Keys.Count);
            int locked = 0; foreach (var q in d.Queues) foreach (var car in q) if (car.Locked) locked++;
            if (locked > 0) m.Add("Locked " + locked);
            return string.Join(", ", m.ToArray());
        }

        // Lỗi dữ liệu: làm game không chạy / không thắng được. Cảnh báo: bất thường nhưng luật vẫn xử lý được.
        static void Schema(LevelData d, LevelCheck c)
        {
            if (d.Queues.Count != BusGame.SlotCount) c.Errors.Add("số cột xe = " + d.Queues.Count + " (cần " + BusGame.SlotCount + ")");
            var seen = new HashSet<long>();
            foreach (var p in d.Pixels)
            {
                if (p.X < 0 || p.Y < 0 || p.X >= d.GridWidth || p.Y >= d.GridHeight) c.Errors.Add("pixel ngoài lưới (" + p.X + "," + p.Y + ")");
                if (!seen.Add(p.X * 100000L + p.Y)) c.Errors.Add("pixel trùng ô (" + p.X + "," + p.Y + ")");
            }
            if (d.Pixels.Count == 0 && d.Spawners.Count == 0) c.Errors.Add("không có pixel");
            foreach (var q in d.Queues) foreach (var car in q)
            {
                if (car.Seats <= 0) c.Errors.Add("xe #" + car.Id + " số chỗ " + car.Seats);
                if (car.Locked && !HasKey(d, car.KeyColor)) c.Errors.Add("xe #" + car.Id + " bị khoá màu " + car.KeyColor + " nhưng không có chìa");
            }
            foreach (var k in d.Keys) if (!InGrid(d, k.Rect)) c.Errors.Add("chìa ngoài lưới");
            foreach (var f in d.Frozen) { if (!InGrid(d, f.Rect)) c.Errors.Add("vùng băng ngoài lưới"); if (f.RequiredHits <= 0) c.Warnings.Add("vùng băng số 0"); }
            // tường được phép nằm ngoài lưới (dữ liệu gốc đóng khung ảnh ở hàng/cột -1 và W/H): không kiểm
            foreach (var s in d.Spawners) if (!InGrid(d, s.Area)) c.Warnings.Add("vùng phát sinh ngoài lưới");

            // cân bằng ghế / pixel theo màu (ô phát sinh ra pixel lặp lại nên ghế thừa là bình thường)
            var seats = d.SeatsByColor(); var px = d.PixelsByColor();
            var colors = new HashSet<int>(seats.Keys); colors.UnionWith(px.Keys);
            int over = 0, under = 0;
            foreach (var col in colors)
            {
                int s, p; seats.TryGetValue(col, out s); px.TryGetValue(col, out p);
                bool spawned = false; foreach (var sp in d.Spawners) foreach (var w in sp.Waves) if (w == col) spawned = true;
                if (p > s) { under += p - s; c.Errors.Add("màu " + col + ": " + p + " pixel > " + s + " chỗ (không thể hết pixel)"); }
                else if (s > p && !spawned) over += s - p;
            }
            if (over > 0) c.Warnings.Add("ghế thừa " + over + " (không có ô phát sinh màu đó)");
        }

        static bool HasKey(LevelData d, int color) { foreach (var k in d.Keys) if (k.Color == color) return true; return false; }
        static bool InGrid(LevelData d, RectData r) { return r.X >= 0 && r.Y >= 0 && r.W > 0 && r.H > 0 && r.X + r.W <= d.GridWidth && r.Y + r.H <= d.GridHeight; }

        /// <summary>Bảng Markdown: tóm tắt theo tag + từng level. Dùng cho .game/research/level-report.md.</summary>
        public static string Markdown(IList<LevelCheck> checks, ValidatorOptions opt)
        {
            var sb = new StringBuilder();
            int inScope = 0, solvable = 0, errors = 0, oos = 0;
            var byTag = new Dictionary<int, List<LevelCheck>>();
            foreach (var c in checks)
            {
                if (!c.InScope) { oos++; continue; }
                inScope++; if (c.Solvable) solvable++; if (c.Errors.Count > 0) errors++;
                List<LevelCheck> l; if (!byTag.TryGetValue(c.Tag, out l)) byTag[c.Tag] = l = new List<LevelCheck>(); l.Add(c);
            }
            sb.AppendLine("# Báo cáo level (M4) — sinh bởi LevelValidator, không sửa tay");
            sb.AppendLine();
            sb.AppendLine("- Tổng " + checks.Count + " level · trong phạm vi " + inScope + " · ngoài phạm vi " + oos + " (xe 2 màu / Elevator / cổng đổi cột)");
            sb.AppendLine("- Trong phạm vi: giải được " + solvable + "/" + inScope + " bằng chạm ngẫu nhiên " + opt.Tries + " ván; lỗi dữ liệu " + errors);
            sb.AppendLine("- Độ khó = tỉ lệ thắng khi chơi ngẫu nhiên `[thiết kế]`: khó < " + opt.HardWinRate + ", siêu khó < " + opt.SuperHardWinRate + " (ValidatorOptions)");
            sb.AppendLine();
            sb.AppendLine("## Theo levelTag của game gốc (0 thường, 1 khó, 2 siêu khó)");
            sb.AppendLine("| Tag | Số level | Tỉ lệ thắng TB | Chạm TB | Pixel TB | Band tính ra 0/1/2 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            var tags = new List<int>(byTag.Keys); tags.Sort();
            foreach (var t in tags)
            {
                var l = byTag[t]; double wr = 0, taps = 0, px = 0; int b0 = 0, b1 = 0, b2 = 0;
                foreach (var c in l) { wr += c.WinRate; taps += c.Taps; px += c.PixelCount; if (c.DifficultyBand == 0) b0++; else if (c.DifficultyBand == 1) b1++; else b2++; }
                sb.AppendLine("| " + t + " | " + l.Count + " | " + (wr / l.Count).ToString("0.00") + " | " + (taps / l.Count).ToString("0") + " | " + (px / l.Count).ToString("0") + " | " + b0 + "/" + b1 + "/" + b2 + " |");
            }
            sb.AppendLine();
            sb.AppendLine("## Level không giải được / lỗi (trong phạm vi)");
            bool any = false;
            foreach (var c in checks) if (c.InScope && !c.Ok) { any = true; sb.AppendLine("- L" + c.Index + " (tag " + c.Tag + "; " + c.Mechanics + "): " + (c.Solvable ? "" : "không thắng ván nào; ") + string.Join("; ", c.Errors.ToArray())); }
            if (!any) sb.AppendLine("- (không có)");
            sb.AppendLine();
            sb.AppendLine("## Từng level");
            sb.AppendLine("| Lv | Tag | Lưới | Pixel | Xe | Chỗ | Mechanic | Phạm vi | Thắng % | Chạm | Band | Ghi chú |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var c in checks)
            {
                string note = string.Join("; ", c.Errors.ToArray()); if (c.Warnings.Count > 0) note += (note.Length > 0 ? "; " : "") + string.Join("; ", c.Warnings.ToArray());
                sb.AppendLine("| " + c.Index + " | " + c.Tag + " | " + c.GridW + "x" + c.GridH + " | " + c.PixelCount + " | " + c.CarCount + " | " + c.SeatTotal + " | " + c.Mechanics + " | " + (c.InScope ? "trong" : "ngoài: " + c.OutOfScope) + " | " + (c.InScope ? (c.WinRate * 100).ToString("0") : "-") + " | " + (c.InScope ? c.Taps.ToString() : "-") + " | " + (c.InScope ? c.DifficultyBand.ToString() : "-") + " | " + note + " |");
            }
            return sb.ToString();
        }
    }
}
