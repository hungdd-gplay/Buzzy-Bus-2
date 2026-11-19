using System;
using System.Collections.Generic;

namespace Buzzy.Core
{
    /// <summary>Một xe. Seats = tổng chỗ; Reserved = chỗ đã giữ (pixel đã xuất phát); Boarded = pixel đã lên xe.</summary>
    public sealed class Car
    {
        public readonly int Id;
        public int Seats { get; private set; }
        public int Color { get; private set; }
        public int Reserved, Boarded;
        /// <summary>Xe bị xích (chưa vào bến) tới khi chìa KeyColor được nhặt.</summary>
        public bool Locked;
        public readonly int KeyColor;
        public Car(CarData d) { Id = d.Id; Color = d.Color; Seats = d.Seats; Locked = d.Locked; KeyColor = d.KeyColor; }
        public void Recolor(int color) { Color = color; }
        /// <summary>Đổi cả màu lẫn số chỗ với xe khác (Magic Wand): tổng chỗ mỗi màu giữ nguyên nên level vẫn thắng được.</summary>
        public void SwapIdentity(Car o) { int c = Color, s = Seats; Color = o.Color; Seats = o.Seats; o.Color = c; o.Seats = s; }
        /// <summary>Số người còn phải lên (hiển thị trên xe).</summary>
        public int Remaining { get { return Seats - Boarded; } }
        /// <summary>Số chỗ còn có thể giữ thêm. 0 = hiện bóng "Full".</summary>
        public int Free { get { return Seats - Reserved; } }
        public bool IsFull { get { return Free <= 0; } }
    }

    /// <summary>Kết quả một lần Tap. Matched=false là NoMatch (UI rung).</summary>
    public sealed class TapResult
    {
        public bool Matched;
        public Car Car;
        public int Slot = -1;
        public List<PixelData> Pixels = new List<PixelData>();
        public static readonly TapResult NoMatch = new TapResult();
    }

    public enum GameStatus { Playing, Won, Deadlock }

    /// <summary>Luật lõi: 3 ô bến + 3 cột xe + Board. Không phụ thuộc engine.</summary>
    public sealed partial class BusGame
    {
        public const int SlotCount = 3;
        public readonly Board Board;
        public readonly Car[] Slots = new Car[SlotCount];
        /// <summary>Ô bị rào vĩnh viễn (cột tương ứng hết xe).</summary>
        public readonly bool[] Barrier = new bool[SlotCount];
        public readonly Queue<Car>[] Queues = new Queue<Car>[SlotCount];
        /// <summary>Vị trí ô bến theo toạ độ lưới, dùng để tính "gần xe nhất".</summary>
        public readonly double[] SlotX = new double[SlotCount], SlotY = new double[SlotCount];
        /// <summary>Xe ở ô còn đang xuống (view chưa báo đỗ xong); chỉ có tác dụng khi Rules.TapWhileArriving = false. Solver / tự chơi không gọi MarkParked nên coi như luôn chạm được.</summary>
        public readonly bool[] Arriving = new bool[3];
        public void MarkParked(int slot) { if (slot >= 0 && slot < 3) Arriving[slot] = false; }

        /// <summary>Số pixel đang bay (đã Tap, chưa Arrive).</summary>
        public int InFlight { get; private set; }

        public event Action<Car, int> CarEntered;      // xe vào ô
        public event Action<Car, int> CarDeparted;     // xe rời bến
        public event Action<int> BarrierPlaced;        // ô bị rào
        public event Action<Car, PixelData> PixelArrived;
        // M3
        public event Action<int> FrozenBroken;                 // chỉ số vùng băng vỡ
        public event Action<PixelData> MysteryRevealed;        // ô ẩn vừa lộ
        public event Action<PixelData> PixelSpawned;           // pixel mới do ô phát sinh ra
        public event Action<int, int> KeyCollected;            // (chỉ số chìa, màu)
        public event Action<Car> CarUnlocked;                  // xe hết xích
        public event Action<Car, int, int, int> CarClawed;     // (xe, cột chờ, vị trí trong cột, ô bến) — Bus Claw
        public event Action<Car, int, int> CarDisplaced;       // (xe bị đẩy về hàng chờ, cột, vị trí) — Bus Claw khi không có ô trống
        public event Action<Car, int> CarRecolored;            // (xe, màu cũ) — Magic Wand
        public event Action<List<PixelData>> PixelsVanished;   // pixel biến mất tức thì (VIP Bus)
        public readonly MechanicRules Rules;

        /// <summary>slotPos null = mặc định: 3 ô chia đều theo chiều ngang, nằm dưới lưới (y = -3). [giả định]</summary>
        public BusGame(LevelData level, double[][] slotPos = null, MechanicRules rules = null)
        {
            Rules = rules ?? new MechanicRules();
            Board = Board.FromLevel(level, Rules);
            for (int i = 0; i < SlotCount; i++)
            {
                Queues[i] = new Queue<Car>();
                if (i < level.Queues.Count) foreach (var c in level.Queues[i]) Queues[i].Enqueue(new Car(c));
                if (slotPos != null) { SlotX[i] = slotPos[i][0]; SlotY[i] = slotPos[i][1]; }
                else { SlotX[i] = Board.W * (i + 0.5) / SlotCount; SlotY[i] = -3; }
            }
            for (int i = 0; i < SlotCount; i++) Refill(i);
        }

        private void Refill(int slot)
        {
            if (Queues[slot].Count > 0)
            {
                if (!Rules.LockedEntersSlot && Queues[slot].Peek().Locked) { Slots[slot] = null; return; }   // chế độ cũ: xe xích đứng đầu cột, ô chờ chìa
                Slots[slot] = Queues[slot].Dequeue(); Arriving[slot] = true; if (CarEntered != null) CarEntered(Slots[slot], slot);
            }
            else { Slots[slot] = null; Barrier[slot] = true; if (BarrierPlaced != null) BarrierPlaced(slot); }
        }

        /// <summary>Ô trống nhưng chưa rào: đang chờ xe (xe xích đứng đầu cột, hoặc vừa kẹp xe đi).</summary>
        public bool IsWaiting(int slot) { return Slots[slot] == null && !Barrier[slot]; }

        // Sau khi gỡ pixel: cập nhật mechanic (băng, ô ẩn, phát đợt, chìa) và phát event.
        private void Process(List<PixelData> removed)
        {
            var ch = Board.Apply(removed);
            if (!ch.Any) return;
            foreach (int f in ch.FrozenBroken) if (FrozenBroken != null) FrozenBroken(f);
            foreach (var r in ch.Revealed) if (MysteryRevealed != null) MysteryRevealed(r);
            foreach (var sp in ch.Spawned) if (PixelSpawned != null) PixelSpawned(sp);
            foreach (int k in ch.KeysCollected)
            {
                int kc = Board.KeyList[k].Color;
                if (KeyCollected != null) KeyCollected(k, kc);
                foreach (var q in Queues) foreach (var car in q)
                    if (car.Locked && car.KeyColor == kc) { car.Locked = false; if (CarUnlocked != null) CarUnlocked(car); }
                foreach (var car in Slots)
                    if (car != null && car.Locked && car.KeyColor == kc) { car.Locked = false; if (CarUnlocked != null) CarUnlocked(car); }
            }
            if (ch.KeysCollected.Count > 0)
                for (int i = 0; i < SlotCount; i++) if (IsWaiting(i)) Refill(i);
        }

        /// <summary>Tap theo pixel: dùng màu của pixel đó (ô rỗng = NoMatch).</summary>
        public TapResult Tap(int x, int y)
        {
            int c = Board.ColorAt(x, y);
            return c == Board.Empty ? TapResult.NoMatch : TapAt(c, x, y);
        }

        /// <summary>
        /// Tap theo màu: chọn xe trong ô cùng màu có Free lớn nhất (hoà: ô nhỏ hơn), rồi giữ chỗ và gỡ khỏi Board
        /// tối đa Free pixel lộ (lớp ngoài, gần ô bến). Không có xe/pixel khớp thì NoMatch.
        /// </summary>
        public TapResult Tap(int color) { return TapAt(color, double.NaN, double.NaN); }

        /// <summary>Tap theo màu kèm điểm chạm (toạ độ lưới): khi Rules.OrderFromTapPoint, thứ tự trong lớp tính theo điểm chạm thay vì ô bến (luật 1c).</summary>
        public TapResult TapAt(int color, double tx, double ty)
        {
            int best = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                var car = Slots[i];
                if (car != null && !car.Locked && car.Color == color && car.Free > 0 && (Rules.TapWhileArriving || !Arriving[i]) && (best < 0 || car.Free > Slots[best].Free)) best = i;
            }
            if (best < 0) return TapResult.NoMatch;
            if (Rules.OrderFromTapPoint && double.IsNaN(tx))
            {   // không có điểm chạm (solver / tự chơi theo màu): chạm vào pixel lộ gần ô bến nhất
                double bd = double.MaxValue;
                foreach (var p in Board.GetExposed(color))
                {
                    if (!Board.IsReachable(p.X, p.Y)) continue;
                    double d = (p.X - SlotX[best]) * (p.X - SlotX[best]) + (p.Y - SlotY[best]) * (p.Y - SlotY[best]);
                    if (d < bd) { bd = d; tx = p.X; ty = p.Y; }
                }
            }
            bool fromTap = Rules.OrderFromTapPoint && !double.IsNaN(tx);
            var picked = Board.PickExposed(color, Slots[best].Free, fromTap ? tx : SlotX[best], fromTap ? ty : SlotY[best], false, fromTap);   // 1c: BFS từ điểm chạm
            if (picked.Count == 0) return TapResult.NoMatch;
            foreach (var p in picked) Board.Remove(p.X, p.Y);
            Slots[best].Reserved += picked.Count;
            InFlight += picked.Count;
            Process(picked);
            return new TapResult { Matched = true, Car = Slots[best], Slot = best, Pixels = picked };
        }

        // ---------------- Booster [thiết kế mục 4-6] ----------------

        /// <summary>VIP Bus: xoá ngay xe đang đỗ ở ô <paramref name="slot"/>; pixel còn thiếu của nó biến mất, xe rời, xe kế xuống.</summary>
        public bool VipBus(int slot)
        {
            var car = Slots[slot];
            if (car == null || car.Reserved != car.Boarded) return false;   // còn pixel đang bay tới xe
            var picked = Board.PickExposed(car.Color, car.Seats - car.Boarded, SlotX[slot], SlotY[slot], true);
            foreach (var p in picked) Board.Remove(p.X, p.Y);
            car.Boarded = car.Seats; car.Reserved = car.Seats;
            if (PixelsVanished != null) PixelsVanished(picked);
            Process(picked);
            if (CarDeparted != null) CarDeparted(car, slot);
            Refill(slot);
            return true;
        }

        // Claw / SkyGrab / MagicWand (góp ý 11, B3–B8): xem BusGameBoosters.cs (partial). VipBus(int) ở trên giữ cho Revive; booster dùng VipBusFill.

        /// <summary>Một pixel bay tới xe: Boarded+1; đủ chỗ thì xe rời, xe kế xuống (hoặc rào).</summary>
        public void Arrive(Car car, PixelData p)
        {
            InFlight--; car.Boarded++;
            if (PixelArrived != null) PixelArrived(car, p);
            if (car.Boarded < car.Seats) return;
            int slot = Array.IndexOf(Slots, car);
            if (slot < 0) return;
            if (CarDeparted != null) CarDeparted(car, slot);
            Refill(slot);
        }

        /// <summary>Tiện cho test/solver: toàn bộ pixel của kết quả Tap đáp xuống.</summary>
        public void ArriveAll(TapResult r) { foreach (var p in r.Pixels) Arrive(r.Car, p); }

        /// <summary>Thắng = Board hết pixel. Thua (THIẾT KẾ, không phải luật quan sát): bế tắc.</summary>
        public GameStatus Status
        {
            get
            {
                if (Board.Count == 0 && !Board.HasPendingWaves) return GameStatus.Won;
                // [thiết kế] bế tắc: mọi ô có xe hoặc rào, không pixel đang bay, không xe nào chạm được pixel cùng màu đang lộ.
                if (InFlight == 0 && !CanTapAny()) return GameStatus.Deadlock;
                return GameStatus.Playing;
            }
        }

        /// <summary>Có xe nào trong ô còn chỗ và có pixel cùng màu đang lộ không.</summary>
        public bool CanTapAny() { return TappableColor() != null; }

        /// <summary>Màu chạm được (xe còn chỗ + pixel lộ), ưu tiên xe có Free lớn nhất; null nếu không có.</summary>
        public int? TappableColor()
        {
            Car best = null;
            foreach (var car in Slots)
                if (car != null && !car.Locked && car.Free > 0 && Board.HasExposed(car.Color) && (best == null || car.Free > best.Free)) best = car;
            return best == null ? (int?)null : best.Color;
        }
    }

    /// <summary>Tự giải greedy để test.</summary>
    public static class Solver
    {
        /// <summary>Mã bước chạm: &lt; 100 = chỉ màu; ≥ 100 = màu + ô chạm (luật 1c). Dùng cho FindOrder / tự chơi.</summary>
        public static int Encode(int color, int x, int y) { return x < 0 ? color : color + 100 * (1 + x + 1000 * y); }
        public static void Decode(int code, out int color, out int x, out int y)
        {
            if (code < 100) { color = code; x = y = -1; return; }
            color = code % 100; int rest = code / 100 - 1; x = rest % 1000; y = rest / 1000;
        }
        public static TapResult TapCode(BusGame g, int code) { int c, x, y; Decode(code, out c, out x, out y); return x < 0 ? g.Tap(c) : g.TapAt(c, x, y); }

        /// <summary>Chạm màu màu tại một pixel LỘ ngẫu nhiên của màu đó (như người chơi chạm bừa vào cụm); luật cũ thì chạm theo màu.</summary>
        public static TapResult RandomTap(BusGame g, int color, Random rnd, out int code)
        {
            code = color;
            if (!g.Rules.OrderFromTapPoint) return g.Tap(color);
            var surf = new List<PixelData>();
            foreach (var p in g.Board.GetExposed(color)) if (g.Board.IsReachable(p.X, p.Y)) surf.Add(p);
            if (surf.Count == 0) return g.Tap(color);
            var q = surf[rnd.Next(surf.Count)]; code = Encode(color, q.X, q.Y);
            return g.TapAt(color, q.X, q.Y);
        }

        /// <summary>Lặp: chọn màu chạm được, Tap, pixel đáp ngay. Trả Won hoặc Deadlock.</summary>
        public static GameStatus Greedy(LevelData level, double[][] slotPos = null)
        {
            return Greedy(new BusGame(level, slotPos));
        }

        /// <summary>
        /// Tìm thứ tự chạm màu thắng bằng chọn ngẫu nhiên có khởi động lại (cố định seed). Trả null nếu không tìm được.
        /// Dùng cho bộ tự chơi (level có mechanic mà greedy thuần bế tắc) và kiểm 300 level.
        /// </summary>
        public static List<int> FindOrder(LevelData level, MechanicRules rules = null, double[][] slotPos = null, int tries = 200, int seed = 1)
        {
            int wins; List<int> best;
            Sample(level, rules, slotPos, tries, seed, out wins, out best, true);
            return best;
        }

        /// <summary>
        /// Chơi ngẫu nhiên `tries` ván (cố định seed): đếm số ván thắng (độ khó cho validator M4) và giữ thứ tự chạm ngắn nhất.
        /// stopAtFirstWin = true: dừng ngay ván thắng đầu (nhanh, cho bộ tự chơi).
        /// </summary>
        public static void Sample(LevelData level, MechanicRules rules, double[][] slotPos, int tries, int seed, out int wins, out List<int> best, bool stopAtFirstWin = false)
        {
            Sample(level, rules, slotPos, tries, seed, out wins, out best, stopAtFirstWin, 0.0);
        }

        /// <summary>
        /// M6: như Sample, thêm xác suất <paramref name="smartProb"/> mỗi lượt chọn màu theo <see cref="Unblock"/> (giải cứu xe kẹt vì băng / ẩn,
        /// ưu tiên cột còn nhiều xe) thay vì ngẫu nhiên. smartProb = 0 → ngẫu nhiên thuần (độ khó M4); 0.7 → bộ giải dự phòng cho level khó.
        /// </summary>
        public static void Sample(LevelData level, MechanicRules rules, double[][] slotPos, int tries, int seed, out int wins, out List<int> best, bool stopAtFirstWin, double smartProb)
        {
            var rnd = new Random(seed);
            wins = 0; best = null;
            for (int t = 0; t < tries; t++)
            {
                var g = new BusGame(level, slotPos, rules);
                var order = new List<int>();
                int guard = 100000;
                while (g.Status == GameStatus.Playing && guard-- > 0)
                {
                    var cols = new List<int>();
                    foreach (var car in g.Slots)
                        if (car != null && !car.Locked && car.Free > 0 && g.Board.HasExposed(car.Color) && !cols.Contains(car.Color)) cols.Add(car.Color);
                    if (cols.Count == 0) break;
                    int c = smartProb > 0 && rnd.NextDouble() < smartProb ? Unblock(g, cols) : cols[rnd.Next(cols.Count)];
                    int code; var r = RandomTap(g, c, rnd, out code); c = code;
                    if (!r.Matched) break;
                    order.Add(c); g.ArriveAll(r);
                }
                if (g.Status == GameStatus.Won)
                {
                    wins++;
                    if (best == null || order.Count < best.Count) best = order;
                    if (stopAtFirstWin) return;
                }
            }
        }

        /// <summary>
        /// Chọn màu trong <paramref name="cols"/> giúp ván đi tiếp: điểm = 10 × số pixel lấy được của màu đó kề ô ẩn / trong tầm băng
        /// đang chặn một xe kẹt (xe đỗ còn chỗ nhưng không có pixel lấy được) + số xe còn lại trong cột của xe nhận màu đó (giữ 3 cột đều).
        /// </summary>
        public static int Unblock(BusGame g, List<int> cols)
        {
            var stuck = new HashSet<int>();
            foreach (var car in g.Slots) if (car != null && !car.Locked && car.Free > 0 && !g.Board.HasExposed(car.Color)) stuck.Add(car.Color);
            var b = g.Board; int reach = g.Rules.FrozenReach;
            int bestC = cols[0]; double bestS = double.MinValue;
            foreach (int c in cols)
            {
                double score = 0;
                for (int i = 0; i < BusGame.SlotCount; i++) { var car = g.Slots[i]; if (car != null && car.Color == c && car.Free > 0) score = Math.Max(score, g.Queues[i].Count); }
                if (stuck.Count > 0)
                {
                    foreach (var p in b.GetExposed(c))
                    {
                        bool help = false;
                        for (int d = 0; d < 4 && !help; d++)
                        {
                            int nx = p.X + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = p.Y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                            if (b.IsHidden(nx, ny) && stuck.Contains(b.ColorAt(nx, ny))) help = true;
                        }
                        for (int f = 0; f < b.FrozenAreas.Count && !help; f++)
                        {
                            if (b.FrozenLeft[f] <= 0) continue; var r = b.FrozenAreas[f].Rect;
                            if (r.Contains(p.X, p.Y) || p.X < r.X - reach || p.X >= r.X + r.W + reach || p.Y < r.Y - reach || p.Y >= r.Y + r.H + reach) continue;
                            for (int x = r.X; x < r.X + r.W && !help; x++) for (int y = r.Y; y < r.Y + r.H && !help; y++) if (stuck.Contains(b.ColorAt(x, y))) help = true;
                        }
                        if (help) score += 10;
                    }
                }
                if (score > bestS) { bestS = score; bestC = c; }
            }
            return bestC;
        }

        public static GameStatus Greedy(BusGame g)
        {
            int guard = 1000000;
            while (g.Status == GameStatus.Playing && guard-- > 0)
            {
                int? c = g.TappableColor();
                if (c == null) break;
                var r = g.Tap(c.Value);
                if (!r.Matched) break;
                g.ArriveAll(r);
            }
            return g.Status;
        }
    }
}
