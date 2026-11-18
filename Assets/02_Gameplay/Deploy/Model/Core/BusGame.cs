using System;
using System.Collections.Generic;

namespace Buzzy.Core
{
    /// <summary>Một xe. Seats = tổng chỗ; Reserved = chỗ đã giữ (pixel đã xuất phát); Boarded = pixel đã lên xe.</summary>
    public sealed class Car
    {
        public readonly int Id, Color, Seats;
        public int Reserved, Boarded;
        public Car(CarData d) { Id = d.Id; Color = d.Color; Seats = d.Seats; }
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

        /// <summary>slotPos null = mặc định: 3 ô chia đều theo chiều ngang, nằm dưới lưới (y = -3). [giả định]</summary>
        public BusGame(LevelData level, double[][] slotPos = null)
        {
            Board = Board.FromLevel(level);
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
            if (Queues[slot].Count > 0) { Slots[slot] = Queues[slot].Dequeue(); if (CarEntered != null) CarEntered(Slots[slot], slot); }
            else { Slots[slot] = null; Barrier[slot] = true; if (BarrierPlaced != null) BarrierPlaced(slot); }
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
                if (car != null && car.Color == color && car.Free > 0 && (best < 0 || car.Free > Slots[best].Free)) best = i;
            }
            if (best < 0) return TapResult.NoMatch;
            var picked = Board.PickExposed(color, Slots[best].Free, SlotX[best], SlotY[best]);
            if (picked.Count == 0) return TapResult.NoMatch;
            foreach (var p in picked) Board.Remove(p.X, p.Y);
            Slots[best].Reserved += picked.Count;
            InFlight += picked.Count;
            return new TapResult { Matched = true, Car = Slots[best], Slot = best, Pixels = picked };
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
                if (Board.Count == 0) return GameStatus.Won;
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
                if (car != null && car.Free > 0 && Board.HasExposed(car.Color) && (best == null || car.Free > best.Free)) best = car;
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
