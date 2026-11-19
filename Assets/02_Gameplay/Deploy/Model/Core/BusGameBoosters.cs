using System;
using System.Collections.Generic;

namespace Buzzy.Core
{
    /// <summary>
    /// Số luật booster (góp ý 11, quan sát clip V4 Lv.4 / V5 Lv.8, 13, 16; visual-check B1–B9). View chép từ BuzzyLayoutConfig.
    /// Phần clip không trả lời được (câu hỏi 1–4 góp ý 11) để làm dữ liệu, mặc định = đề xuất.
    /// </summary>
    public sealed class BoosterRules
    {
        /// <summary>B3 (chốt 2a): bán kính vùng hút của Sky Grab (ô lưới). Clip: ~25 pixel ≈ bán kính 2,5.</summary>
        public double SkyGrabRadius = 2.5;
        /// <summary>B3 (người dùng chốt 1a): pixel trong vùng mà không có xe đỗ cùng màu còn chỗ (không xe / xe đầy) → true = biến mất luôn, false = ở lại.</summary>
        public bool SkyGrabVanishUnmatched = true;
        /// <summary>B3: hút cả pixel dưới băng / ẩn không (clip không có băng trong vùng).</summary>
        public bool SkyGrabTakesFrozen = false;
        /// <summary>B5 (chốt 3a): 0 = 3 xe đỗ mới là xe có màu nhiều pixel LỘ nhất (Board.IsExposed, lấy được); 1 = ngẫu nhiên.</summary>
        public int WandPick = 0;
        /// <summary>B1 (chốt 4a): thứ tự lấy pixel khi đổ đầy (VIP / Claw): 0 = cụm lớn nhất trước, không cần lộ; 1 = lớp ngoài như chạm.</summary>
        public int FillPick = 0;
        /// <summary>Hạt giống cho WandPick = 1.</summary>
        public int Seed = 20261119;
    }

    /// <summary>Kết quả Sky Grab: pixel bay về từng xe (số trên xe giảm) + pixel biến mất.</summary>
    public sealed class SkyGrabResult
    {
        public bool Matched;
        public readonly List<TapResult> ToCars = new List<TapResult>();
        public readonly List<PixelData> Vanished = new List<PixelData>();
        public int Count { get { int n = Vanished.Count; foreach (var r in ToCars) n += r.Pixels.Count; return n; } }
    }

    /// <summary>Kết quả Bus Claw: xe kẹp khỏi cột chờ (đứng ở làn, không chiếm ô) + pixel đổ đầy.</summary>
    public sealed class ClawResult
    {
        public bool Ok;
        public Car Car;
        public int Queue, Pos;
        public List<PixelData> Pixels = new List<PixelData>();
    }

    /// <summary>Kết quả Magic Wand: xe đỗ cũ / mới từng ô (null = ô đó không đổi).</summary>
    public sealed class ShuffleResult
    {
        public bool Ok;
        public readonly Car[] OldSlots = new Car[BusGame.SlotCount], NewSlots = new Car[BusGame.SlotCount];
    }

    /// <summary>Luật 4 booster theo clip gốc (góp ý 11). Design-decisions 4–6 ghi lại theo đây.</summary>
    public sealed partial class BusGame
    {
        public BoosterRules Boosters = new BoosterRules();
        /// <summary>Xe do Bus Claw thả xuống làn: không ở ô, không ở cột; rời khi đủ người.</summary>
        public readonly List<Car> LaneCars = new List<Car>();
        public event Action<Car> LaneCarDeparted;      // xe ở làn (Claw) đủ người rời
        public event Action<ShuffleResult> CarsShuffled; // Magic Wand xong (view xếp lại xe)

        // ---------------- đổ đầy (VIP B1, Claw B7) ----------------

        /// <summary>
        /// B1 VIP Bus: "Clear a bus instantly!": đúng số chỗ còn trống của xe đỗ ở ô <paramref name="slot"/> pixel màu xe rời ảnh bay lên xe
        /// (kể cả pixel bị bao kín), Full hiện ngay; thiếu pixel thì phần thiếu coi như đã lên để xe vẫn rời. Trả Matched=false nếu không có xe.
        /// Xe rời qua Arrive như chạm thường. (Hàm VipBus(int) cũ giữ cho Revive.)
        /// </summary>
        public TapResult VipBusFill(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return TapResult.NoMatch;
            var car = Slots[slot];
            if (car == null || car.Locked || car.Free <= 0) return TapResult.NoMatch;
            var picked = Fill(car, SlotX[slot], SlotY[slot]);
            if (car.Boarded >= car.Seats && car.Reserved == car.Boarded) { if (CarDeparted != null) CarDeparted(car, slot); Refill(slot); }   // không còn pixel nào: rời ngay
            return new TapResult { Matched = true, Car = car, Slot = slot, Pixels = picked };
        }

        /// <summary>Giữ chỗ + gỡ khỏi ảnh tối đa Free pixel màu xe; phần thiếu ghi thẳng vào Boarded (ma).</summary>
        private List<PixelData> Fill(Car car, double sx, double sy)
        {
            int need = car.Free;
            var picked = PickForFill(car.Color, need, sx, sy);
            foreach (var p in picked) Board.Remove(p.X, p.Y);
            car.Reserved += picked.Count; InFlight += picked.Count;
            int ghost = need - picked.Count;
            if (ghost > 0) { car.Reserved += ghost; car.Boarded += ghost; }
            if (picked.Count > 0) Process(picked);
            return picked;
        }

        /// <summary>Thứ tự lấy khi đổ đầy (BoosterRules.FillPick): 0 = cụm 4-kề lớn nhất trước (trong cụm: gần xe trước); 1 = lớp ngoài như chạm (mọi pixel).</summary>
        public List<PixelData> PickForFill(int color, int max, double sx, double sy)
        {
            if (max <= 0) return new List<PixelData>();
            var all = Board.GetExposed(color);   // lấy được (không băng / không ẩn nếu MysteryBlocks); IsReachable không áp cho booster
            if (Rules.ExposedOnly) { all.Clear(); for (int x = 0; x < Board.W; x++) for (int y = 0; y < Board.H; y++) if (Board.ColorAt(x, y) == color && Board.Takeable(x, y)) all.Add(new PixelData(x, y, color)); }
            if (Boosters.FillPick == 1)
            {
                all.Sort((a, b) => { int c = D2(a, sx, sy).CompareTo(D2(b, sx, sy)); return c != 0 ? c : (a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X)); });
                var outer = Board.PickExposed(color, max, sx, sy, false);
                var set = new HashSet<long>(); var r = new List<PixelData>();
                foreach (var p in outer) if (set.Add(p.X * 100000L + p.Y)) r.Add(p);
                foreach (var p in all) if (r.Count < max && set.Add(p.X * 100000L + p.Y)) r.Add(p);
                if (r.Count > max) r.RemoveRange(max, r.Count - max);
                return r;
            }
            // cụm 4-kề
            var comp = new Dictionary<long, int>(); var groups = new List<List<PixelData>>();
            var lookup = new HashSet<long>(); foreach (var p in all) lookup.Add(p.X * 100000L + p.Y);
            foreach (var p in all)
            {
                long k0 = p.X * 100000L + p.Y; if (comp.ContainsKey(k0)) continue;
                var g = new List<PixelData>(); var st = new Stack<PixelData>(); st.Push(p); comp[k0] = groups.Count;
                while (st.Count > 0)
                {
                    var q = st.Pop(); g.Add(q);
                    int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
                    for (int d = 0; d < 4; d++)
                    {
                        long k = (q.X + dx[d]) * 100000L + (q.Y + dy[d]);
                        if (lookup.Contains(k) && !comp.ContainsKey(k)) { comp[k] = groups.Count; st.Push(new PixelData(q.X + dx[d], q.Y + dy[d], color)); }
                    }
                }
                groups.Add(g);
            }
            groups.Sort((a, b) => b.Count.CompareTo(a.Count));
            var res = new List<PixelData>();
            foreach (var g in groups)
            {
                g.Sort((a, b) => { int c = D2(a, sx, sy).CompareTo(D2(b, sx, sy)); return c != 0 ? c : (a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X)); });
                foreach (var p in g) { if (res.Count >= max) return res; res.Add(p); }
            }
            return res;
        }

        private static double D2(PixelData p, double sx, double sy) { double dx = p.X - sx, dy = p.Y - sy; return dx * dx + dy * dy; }

        // ---------------- Sky Grab (B3) ----------------

        /// <summary>
        /// B3 Sky Grab: "Clear all passenger in the selected area!": mọi pixel lấy được trong bán kính Boosters.SkyGrabRadius quanh điểm chạm
        /// (toạ độ lưới, có thể lẻ) rời ảnh; màu có xe đỗ còn chỗ thì bay về xe đó (tới hết chỗ), phần còn lại biến mất (SkyGrabVanishUnmatched).
        /// Matched=false khi vùng không có pixel.
        /// </summary>
        public SkyGrabResult SkyGrab(double gx, double gy)
        {
            var res = new SkyGrabResult();
            double r2 = Boosters.SkyGrabRadius * Boosters.SkyGrabRadius;
            var inArea = new List<PixelData>();
            int x0 = (int)Math.Floor(gx - Boosters.SkyGrabRadius), x1 = (int)Math.Ceiling(gx + Boosters.SkyGrabRadius);
            int y0 = (int)Math.Floor(gy - Boosters.SkyGrabRadius), y1 = (int)Math.Ceiling(gy + Boosters.SkyGrabRadius);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                if (!Board.InBounds(x, y)) continue;
                int c = Board.ColorAt(x, y); if (c == Board.Empty) continue;
                if (!Boosters.SkyGrabTakesFrozen && !Board.Takeable(x, y)) continue;
                double dx = x - gx, dy = y - gy; if (dx * dx + dy * dy > r2) continue;
                inArea.Add(new PixelData(x, y, c));
            }
            if (inArea.Count == 0) return res;
            inArea.Sort((a, b) => { int c = D2(a, gx, gy).CompareTo(D2(b, gx, gy)); return c != 0 ? c : (a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X)); });
            var removed = new List<PixelData>();
            var perCar = new Dictionary<Car, TapResult>();
            foreach (var p in inArea)
            {
                int best = -1;
                for (int i = 0; i < SlotCount; i++)
                {
                    var car = Slots[i];
                    if (car != null && !car.Locked && car.Color == p.Color && car.Free > 0 && (best < 0 || car.Free > Slots[best].Free)) best = i;
                }
                if (best >= 0)
                {
                    var car = Slots[best];
                    TapResult tr; if (!perCar.TryGetValue(car, out tr)) { tr = new TapResult { Matched = true, Car = car, Slot = best }; perCar[car] = tr; res.ToCars.Add(tr); }
                    tr.Pixels.Add(p); car.Reserved++; InFlight++;
                }
                else if (Boosters.SkyGrabVanishUnmatched) res.Vanished.Add(p);
                else continue;
                Board.Remove(p.X, p.Y); removed.Add(p);
            }
            if (removed.Count == 0) return res;
            Process(removed);
            res.Matched = true;
            return res;
        }

        // ---------------- Magic Wand (B5) ----------------

        /// <summary>
        /// B5 Magic Wand: "Shuffle the buses!": chọn lại 3 xe đỗ từ toàn bộ xe (ô + cột chờ, không xích, không đang nhận pixel);
        /// xe đỗ cũ về đúng vị trí trong cột của xe được chọn. WandPick 0 (chốt 3a): xe có màu nhiều pixel LỘ nhất (mỗi lần chọn trừ số chỗ xe đó
        /// khỏi quỹ màu để không dồn 3 xe vào màu ít pixel); 1: ngẫu nhiên. Ok=false nếu không đổi được xe nào.
        /// </summary>
        public ShuffleResult Shuffle()
        {
            var res = new ShuffleResult();
            // vị trí có thể đổi: ô đỗ (xe không xích, không có pixel đang bay) + mọi xe cột chờ không xích
            var movableSlots = new List<int>();
            for (int i = 0; i < SlotCount; i++) { var c = Slots[i]; if (c != null && !c.Locked && c.Reserved == c.Boarded && c.Boarded < c.Seats) movableSlots.Add(i); }
            var pool = new List<Car>(); var origin = new Dictionary<Car, int[]>();   // origin: {-1, slot} hoặc {col, pos}
            foreach (int i in movableSlots) { pool.Add(Slots[i]); origin[Slots[i]] = new[] { -1, i }; }
            for (int c = 0; c < SlotCount; c++) { int k = 0; foreach (var car in Queues[c]) { if (!car.Locked) { pool.Add(car); origin[car] = new[] { c, k }; } k++; } }
            if (movableSlots.Count == 0 || pool.Count <= movableSlots.Count) return res;
            // quỹ pixel theo màu = số pixel lộ (chốt 3a)
            var budget = new Dictionary<int, int>();
            foreach (var car in pool) if (!budget.ContainsKey(car.Color)) budget[car.Color] = CountExposed(car.Color);
            var rnd = new Random(Boosters.Seed + Board.Count);
            var chosen = new List<Car>();
            foreach (int slot in movableSlots)
            {
                Car best = null; double bestScore = double.MinValue;
                foreach (var car in pool)
                {
                    if (chosen.Contains(car)) continue;
                    double score;
                    if (Boosters.WandPick == 1) score = rnd.NextDouble();
                    else
                    {
                        int b = budget[car.Color];
                        score = Math.Min(b, car.Remaining) * 1000.0 + b - (origin[car][0] < 0 ? 0.5 : 0) ;   // đủ pixel cho xe trước; còn dư nhiều hơn tốt; ưu tiên xe cột chờ khi hoà
                    }
                    if (score > bestScore) { bestScore = score; best = car; }
                }
                chosen.Add(best);
                if (Boosters.WandPick != 1) budget[best.Color] = Math.Max(0, budget[best.Color] - best.Remaining);
            }
            // nếu kết quả giống hệt hiện tại (mọi xe chọn đang ở đúng ô) thì hoán vị tối thiểu: không làm gì
            bool same = true; for (int i = 0; i < movableSlots.Count; i++) if (Slots[movableSlots[i]] != chosen[i]) same = false;
            if (same) return res;
            // xếp lại: xe đỗ cũ (không được chọn lại) điền vào chỗ cột chờ mà xe được chọn bỏ lại
            var displaced = new List<Car>(); foreach (int i in movableSlots) if (!chosen.Contains(Slots[i])) displaced.Add(Slots[i]);
            var holes = new List<int[]>(); foreach (var car in chosen) if (origin[car][0] >= 0) holes.Add(origin[car]);
            var lists = new List<Car>[SlotCount]; for (int c = 0; c < SlotCount; c++) lists[c] = new List<Car>(Queues[c]);
            for (int h = 0; h < holes.Count; h++) lists[holes[h][0]][holes[h][1]] = displaced[h];
            for (int c = 0; c < SlotCount; c++) { Queues[c].Clear(); foreach (var car in lists[c]) Queues[c].Enqueue(car); }
            for (int i = 0; i < movableSlots.Count; i++) { int s = movableSlots[i]; res.OldSlots[s] = Slots[s]; res.NewSlots[s] = chosen[i]; Slots[s] = chosen[i]; }
            res.Ok = true;
            if (CarsShuffled != null) CarsShuffled(res);
            return res;
        }

        /// <summary>Số pixel màu đang lộ (kề ô rỗng / mép) và lấy được: quỹ chọn xe của Magic Wand (chốt 3a).</summary>
        private int CountExposed(int color)
        {
            int n = 0;
            for (int x = 0; x < Board.W; x++) for (int y = 0; y < Board.H; y++) if (Board.ColorAt(x, y) == color && Board.Takeable(x, y) && Board.IsExposed(x, y)) n++;
            return n;
        }

        // ---------------- Bus Claw (B7) ----------------

        /// <summary>
        /// B7 Bus Claw: "Fill the selected bus instantly!": xe ở vị trí <paramref name="pos"/> của cột <paramref name="queue"/> (xe bất kỳ, không xích)
        /// rời cột (xe sau dồn lên), đứng ở làn trên ô bến (LaneCars), được đổ đầy ngay như VIP; đủ người → LaneCarDeparted (qua ArriveLane).
        /// Không cần ô trống. Cột vừa hết xe mà ô đang chờ → rào.
        /// </summary>
        public ClawResult Claw(int queue, int pos)
        {
            var res = new ClawResult { Queue = queue, Pos = pos };
            if (queue < 0 || queue >= SlotCount || pos < 0 || pos >= Queues[queue].Count) return res;
            var list = new List<Car>(Queues[queue]);
            var car = list[pos];
            if (car.Locked) return res;
            list.RemoveAt(pos);
            Queues[queue].Clear(); foreach (var c in list) Queues[queue].Enqueue(c);
            LaneCars.Add(car);
            res.Car = car; res.Ok = true;
            res.Pixels = Fill(car, SlotX[Math.Min(SlotCount - 1, Math.Max(0, queue))], SlotY[queue]);
            if (list.Count == 0 && IsWaiting(queue)) Refill(queue);
            if (car.Boarded >= car.Seats && car.Reserved == car.Boarded) LaneDone(car);
            return res;
        }

        /// <summary>Pixel tới xe ở làn (Claw): như Arrive, đủ người thì xe rời (LaneCarDeparted).</summary>
        public void ArriveLane(Car car, PixelData p)
        {
            Arrive(car, p);
            if (car.Boarded >= car.Seats && LaneCars.Contains(car)) LaneDone(car);
        }

        private void LaneDone(Car car)
        {
            LaneCars.Remove(car);
            if (LaneCarDeparted != null) LaneCarDeparted(car);
        }

        /// <summary>Tiện cho test: mọi pixel Sky Grab / Claw đáp xuống.</summary>
        public void ArriveAll(SkyGrabResult r) { foreach (var tr in r.ToCars) ArriveAll(tr); }
        public void ArriveAll(ClawResult r) { foreach (var p in r.Pixels) ArriveLane(r.Car, p); }
    }
}
