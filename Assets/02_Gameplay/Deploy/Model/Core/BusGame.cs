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
    public sealed class BusGame
    {
        public const int SlotCount = 3;
        public readonly Board Board;
        public readonly Car[] Slots = new Car[SlotCount];
        /// <summary>Ô bị rào vĩnh viễn (cột tương ứng hết xe).</summary>
        public readonly bool[] Barrier = new bool[SlotCount];
        public readonly Queue<Car>[] Queues = new Queue<Car>[SlotCount];
        /// <summary>Vị trí ô bến theo toạ độ lưới, dùng để tính "gần xe nhất".</summary>
        public readonly double[] SlotX = new double[SlotCount], SlotY = new double[SlotCount];
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
                Slots[slot] = Queues[slot].Dequeue(); if (CarEntered != null) CarEntered(Slots[slot], slot);
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
            return c == Board.Empty ? TapResult.NoMatch : Tap(c);
        }

        /// <summary>
        /// Tap theo màu: chọn xe trong ô cùng màu có Free lớn nhất (hoà: ô nhỏ hơn), rồi giữ chỗ và gỡ khỏi Board
        /// tối đa Free pixel lộ (lớp ngoài, gần ô bến). Không có xe/pixel khớp thì NoMatch.
        /// </summary>
        public TapResult Tap(int color)
        {
            int best = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                var car = Slots[i];
                if (car != null && !car.Locked && car.Color == color && car.Free > 0 && (best < 0 || car.Free > Slots[best].Free)) best = i;
            }
            if (best < 0) return TapResult.NoMatch;
            var picked = Board.PickExposed(color, Slots[best].Free, SlotX[best], SlotY[best]);
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

        /// <summary>
        /// Bus Claw: kẹp xe ở vị trí <paramref name="pos"/> (0 = đầu cột) của cột <paramref name="queue"/> thả vào ô bến.
        /// Ưu tiên ô đang chờ (trống); không có thì đổi chỗ với một xe đang đỗ chưa nhận pixel nào (xe đó về chỗ của xe kẹp trong hàng chờ). [thiết kế mục 6]
        /// </summary>
        public bool Claw(int queue, int pos)
        {
            if (queue < 0 || queue >= SlotCount || pos < 0 || pos >= Queues[queue].Count) return false;
            var list = new List<Car>(Queues[queue]);
            var car = list[pos];
            if (car.Locked) return false;
            int dest = -1;
            if (IsWaiting(queue)) dest = queue;
            else for (int i = 0; i < SlotCount && dest < 0; i++) if (IsWaiting(i)) dest = i;
            Car displaced = null;
            if (dest < 0)
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    var c = Slots[i];
                    if (c != null && !c.Locked && c.Reserved == 0 && c.Boarded == 0 && (dest < 0 || i == queue)) dest = i;
                }
                if (dest < 0) return false;
                displaced = Slots[dest];
            }
            if (displaced != null) list[pos] = displaced; else list.RemoveAt(pos);
            Queues[queue].Clear(); foreach (var c in list) Queues[queue].Enqueue(c);
            Slots[dest] = car;
            if (displaced != null && CarDisplaced != null) CarDisplaced(displaced, queue, pos);
            if (CarClawed != null) CarClawed(car, queue, pos, dest);
            if (displaced == null && list.Count == 0 && IsWaiting(queue)) Refill(queue);   // cột vừa hết xe: rào ô của nó
            return true;
        }

        /// <summary>Sky Grab: nhấc tối đa Rules.SkyGrabCount pixel màu <paramref name="color"/> (kể cả băng / ẩn) vào xe cùng màu đang đỗ.</summary>
        public TapResult SkyGrab(int color)
        {
            int best = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                var car = Slots[i];
                if (car != null && !car.Locked && car.Color == color && car.Free > 0 && (best < 0 || car.Free > Slots[best].Free)) best = i;
            }
            if (best < 0) return TapResult.NoMatch;
            var picked = Board.PickExposed(color, Math.Min(Rules.SkyGrabCount, Slots[best].Free), SlotX[best], SlotY[best], true);
            if (picked.Count == 0) return TapResult.NoMatch;
            foreach (var p in picked) Board.Remove(p.X, p.Y);
            Slots[best].Reserved += picked.Count; InFlight += picked.Count;
            Process(picked);
            return new TapResult { Matched = true, Car = Slots[best], Slot = best, Pixels = picked };
        }

        /// <summary>
        /// Magic Wand: xe đỗ ở ô <paramref name="slot"/> (chưa nhận pixel nào) đổi sang màu <paramref name="color"/>:
        /// đổi cả màu lẫn số chỗ với xe chờ sớm nhất cùng màu đó, nên tổng chỗ mỗi màu không đổi.
        /// </summary>
        public bool MagicWand(int slot, int color)
        {
            var car = Slots[slot];
            if (car == null || car.Locked || car.Color == color || car.Reserved != 0 || car.Boarded != 0 || Board.CountColor(color) == 0) return false;
            Car other = null;
            foreach (var q in Queues) { foreach (var c in q) if (!c.Locked && c.Color == color) { other = c; break; } if (other != null) break; }
            if (other == null) return false;
            int old = car.Color;
            car.SwapIdentity(other);
            if (CarRecolored != null) { CarRecolored(car, old); CarRecolored(other, color); }
            return true;
        }

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
            var rnd = new Random(seed);
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
                    int c = cols[rnd.Next(cols.Count)];
                    var r = g.Tap(c);
                    if (!r.Matched) break;
                    order.Add(c); g.ArriveAll(r);
                }
                if (g.Status == GameStatus.Won) return order;
            }
            return null;
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
