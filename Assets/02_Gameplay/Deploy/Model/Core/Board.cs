using System;
using System.Collections.Generic;

namespace Buzzy.Core
{
    /// <summary>Lưới pixel theo màu. Ô rỗng = -1.</summary>
    public sealed class Board
    {
        public const int Empty = -1;
        public readonly int W, H;
        private readonly int[,] _c;
        private readonly int[,] _depth; // độ sâu ban đầu (1 = lớp ngoài cùng), tính 1 lần lúc dựng
        private readonly bool[,] _open;  // M7: ô rỗng thông ra ngoài lưới (qua ô rỗng kề 4 hướng); tính lại khi bảng đổi
        private bool _openDirty = true;
        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };

        public int Count { get; private set; }

        // --- mechanic M3 ---
        private readonly HashSet<long> _walls = new HashSet<long>();
        private readonly int[,] _frozenId;          // -1 = không băng
        private readonly bool[,] _mystery, _revealed;
        public readonly List<FrozenData> FrozenAreas = new List<FrozenData>();
        public readonly List<int> FrozenLeft = new List<int>();
        public readonly List<SpawnerData> SpawnerList = new List<SpawnerData>();
        public readonly List<int> SpawnWave = new List<int>();   // chỉ số đợt kế tiếp của mỗi ô phát
        public readonly List<KeyData> KeyList = new List<KeyData>();
        public readonly List<bool> KeyDone = new List<bool>();
        private readonly List<int> _keyRing = new List<int>();   // số pixel quanh chìa lúc đầu
        public readonly List<RectData> WallList = new List<RectData>();
        private readonly MechanicRules _rules;

        public Board(int w, int h, IEnumerable<PixelData> pixels, LevelData level = null, MechanicRules rules = null)
        {
            W = w; H = h; _c = new int[w, h]; _depth = new int[w, h]; _open = new bool[w, h];
            _frozenId = new int[w, h]; _mystery = new bool[w, h]; _revealed = new bool[w, h];
            _rules = rules ?? new MechanicRules();
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) { _c[x, y] = Empty; _frozenId[x, y] = -1; }
            foreach (var p in pixels) if (InBounds(p.X, p.Y) && _c[p.X, p.Y] == Empty) { _c[p.X, p.Y] = p.Color; Count++; }
            if (level != null)
            {
                foreach (var r in level.Walls)
                {
                    WallList.Add(r);
                    for (int i = 0; i < r.W; i++) for (int j = 0; j < r.H; j++) _walls.Add(Key(r.X + i, r.Y + j));
                }
                foreach (var f in level.Frozen)
                {
                    int id = FrozenAreas.Count; FrozenAreas.Add(f);
                    FrozenLeft.Add(Math.Max(1, (int)Math.Ceiling(f.RequiredHits / _rules.FrozenHitDivisor)));
                    for (int i = 0; i < f.Rect.W; i++) for (int j = 0; j < f.Rect.H; j++)
                        if (InBounds(f.Rect.X + i, f.Rect.Y + j)) _frozenId[f.Rect.X + i, f.Rect.Y + j] = id;
                }
                foreach (var m in level.Mystery) if (InBounds(m.X, m.Y)) _mystery[m.X, m.Y] = true;
                foreach (var k in level.Keys) { KeyList.Add(k); KeyDone.Add(false); }
                foreach (var sp in level.Spawners) { SpawnerList.Add(sp); SpawnWave.Add(0); }
            }
            ComputeDepth();
            if (level != null)
            {
                for (int i = 0; i < KeyList.Count; i++) _keyRing.Add(RingPixels(KeyList[i].Rect));
                for (int i = 0; i < SpawnerList.Count; i++) TrySpawn(i, null);
                UpdateReveal(new BoardChanges());   // ô ẩn kề ô rỗng ngay từ đầu thì coi là đã lộ
            }
        }

        public static Board FromLevel(LevelData d, MechanicRules rules = null) { return new Board(d.GridWidth, d.GridHeight, d.Pixels, d, rules); }

        private static long Key(int x, int y) { return ((long)(x + 1000) << 20) | (long)(y + 1000); }
        public bool IsWall(int x, int y) { return _walls.Contains(Key(x, y)); }
        public bool IsFrozen(int x, int y) { return InBounds(x, y) && _frozenId[x, y] >= 0 && FrozenLeft[_frozenId[x, y]] > 0; }
        public int FrozenIdAt(int x, int y) { return InBounds(x, y) ? _frozenId[x, y] : -1; }
        public bool IsMystery(int x, int y) { return InBounds(x, y) && _mystery[x, y]; }
        public bool IsHidden(int x, int y) { return IsMystery(x, y) && !_revealed[x, y] && _c[x, y] != Empty; }

        /// <summary>Pixel lấy được bằng chạm: có mặt, không bị băng, không còn ẩn.</summary>
        public bool Takeable(int x, int y) { return ColorAt(x, y) != Empty && !IsFrozen(x, y) && (!_rules.MysteryBlocks || !IsHidden(x, y)); }

        /// <summary>Ô rỗng thật (không phải tường): chỉ ô như vậy mới làm pixel kề "lộ".</summary>
        private bool OpenAt(int x, int y) { return ColorAt(x, y) == Empty && !IsWall(x, y); }

        public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }
        public int ColorAt(int x, int y) { return InBounds(x, y) ? _c[x, y] : Empty; }

        /// <summary>Pixel "lộ" nếu có ô kề 4 hướng rỗng hoặc nằm ngoài lưới.</summary>
        public bool IsExposed(int x, int y)
        {
            if (ColorAt(x, y) == Empty) return false;
            for (int i = 0; i < 4; i++) if (OpenAt(x + Dx[i], y + Dy[i])) return true;
            return false;
        }

        /// <summary>M7 (1a): pixel có đường ra ngoài: kề ô ngoài lưới hoặc kề ô rỗng thông ra ngoài qua chuỗi ô rỗng (hành khách đi bộ ra được).
        /// Ô rỗng bị bao kín (lỗ trong ảnh) không tính.</summary>
        public bool IsReachable(int x, int y)
        {
            if (ColorAt(x, y) == Empty) return false;
            if (_openDirty) ComputeOpen();
            for (int i = 0; i < 4; i++) { int nx = x + Dx[i], ny = y + Dy[i]; if (!InBounds(nx, ny)) return true; if (_open[nx, ny]) return true; }
            return false;
        }

        // BFS từ mép lưới qua ô rỗng (không tường)
        private void ComputeOpen()
        {
            _openDirty = false; var q = new Queue<int>();
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
            {
                _open[x, y] = false;
                if (OpenAt(x, y) && (x == 0 || y == 0 || x == W - 1 || y == H - 1)) { _open[x, y] = true; q.Enqueue(x * H + y); }
            }
            while (q.Count > 0)
            {
                int v = q.Dequeue(); int x = v / H, y = v % H;
                for (int i = 0; i < 4; i++) { int nx = x + Dx[i], ny = y + Dy[i]; if (InBounds(nx, ny) && !_open[nx, ny] && OpenAt(nx, ny)) { _open[nx, ny] = true; q.Enqueue(nx * H + ny); } }
            }
        }

        /// <summary>Lấy pixel làm rỗng ô.</summary>
        public bool Remove(int x, int y)
        {
            if (ColorAt(x, y) == Empty) return false;
            _c[x, y] = Empty; Count--; _openDirty = true; return true;
        }

        public int CountColor(int color)
        {
            int n = 0;
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (_c[x, y] == color) n++;
            return n;
        }

        public bool HasExposed(int color) { return GetExposed(color).Count > 0; }

        /// <summary>
        /// Các pixel lấy được của một màu (chưa sắp xếp). M7 (góp ý 04, 1a): mặc định chỉ pixel có đường ra ngoài (IsReachable);
        /// luật cũ (pixel sâu bên trong vẫn lấy, lớp ngoài chỉ là thứ tự) giữ bằng MechanicRules.ExposedOnly = false.
        /// Chặn lấy (Frozen, Mystery...) lọc ở Takeable.
        /// </summary>
        public List<PixelData> GetExposed(int color) { return Collect(color, false); }

        /// <summary>Mọi pixel của màu, kể cả băng / ẩn (Sky Grab, VIP Bus).</summary>
        public List<PixelData> GetAll(int color) { return Collect(color, true); }

        private List<PixelData> Collect(int color, bool any)
        {
            var r = new List<PixelData>();
            if (!any && _rules.ExposedOnly) return CollectReachable(color);
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
                if (_c[x, y] == color && (any || Takeable(x, y))) r.Add(new PixelData(x, y, color));
            return r;
        }

        /// <summary>
        /// Luật 1b (M7, góp ý 04, design-decisions 19): pixel màu X đi được nếu có đường ra mép lưới qua ô rỗng HOẶC qua pixel cùng màu X
        /// lấy được (cả khối "chảy" ra trong một lần chạm, clip Lv.2); chỉ pixel bị màu khác / băng / tường bao kín mới ở lại.
        /// Flood từ các pixel X có IsReachable qua láng giềng 4 hướng cùng màu X lấy được.
        /// </summary>
        private List<PixelData> CollectReachable(int color)
        {
            var r = new List<PixelData>(); var seen = new bool[W, H]; var q = new Queue<int>();
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
                if (_c[x, y] == color && Takeable(x, y) && IsReachable(x, y)) { seen[x, y] = true; q.Enqueue(x * H + y); }
            while (q.Count > 0)
            {
                int v = q.Dequeue(); int x = v / H, y = v % H; r.Add(new PixelData(x, y, color));
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + Dx[i], ny = y + Dy[i];
                    if (InBounds(nx, ny) && !seen[nx, ny] && _c[nx, ny] == color && Takeable(nx, ny)) { seen[nx, ny] = true; q.Enqueue(nx * H + ny); }
                }
            }
            return r;
        }

        /// <summary>
        /// Chọn tối đa <paramref name="max"/> pixel của màu: lớp ngoài cùng (độ sâu ban đầu nhỏ) trước,
        /// rồi gần (sx, sy) nhất (vị trí ô bến, toạ độ lưới), cuối cùng theo (y, x) để ổn định. Không xoá pixel.
        /// </summary>
        public List<PixelData> PickExposed(int color, int max, double sx, double sy, bool any = false, bool bfsFromPoint = false)
        {
            var list = Collect(color, any);
            if (bfsFromPoint) return PickByBfs(list, max, sx, sy);
            var depth = CurrentDepth();   // M7 góp ý 05: lớp tính theo bảng HIỆN TẠI (pixel đã lộ ra sau nhiều lần lấy đứng trước), không theo độ sâu ban đầu
            list.Sort((a, b) =>
            {
                int c = depth[a.X, a.Y].CompareTo(depth[b.X, b.Y]);
                if (c != 0) return c;
                c = Dist2(a, sx, sy).CompareTo(Dist2(b, sx, sy));
                if (c != 0) return c;
                c = a.Y.CompareTo(b.Y);
                return c != 0 ? c : a.X.CompareTo(b.X);
            });
            if (list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }

        private static double Dist2(PixelData p, double sx, double sy) { double dx = p.X - sx, dy = p.Y - sy; return dx * dx + dy * dy; }

        /// <summary>Thay đổi mechanic sau một lần lấy pixel (để BusGame / View phát event).</summary>
        public sealed class BoardChanges
        {
            public List<int> FrozenBroken = new List<int>();
            public List<PixelData> Revealed = new List<PixelData>();
            public List<PixelData> Spawned = new List<PixelData>();
            public List<int> KeysCollected = new List<int>();
            public bool Any { get { return FrozenBroken.Count + Revealed.Count + Spawned.Count + KeysCollected.Count > 0; } }
        }

        /// <summary>Gọi sau khi gỡ pixel: tính hit băng, phát đợt mới, lộ ô ẩn, nhặt chìa.</summary>
        public BoardChanges Apply(List<PixelData> removed)
        {
            var ch = new BoardChanges();
            int reach = _rules.FrozenReach;
            for (int i = 0; i < FrozenAreas.Count; i++)
            {
                if (FrozenLeft[i] <= 0) continue;
                var r = FrozenAreas[i].Rect;
                foreach (var p in removed)
                {
                    if (r.Contains(p.X, p.Y)) continue;
                    if (reach < 0 || (p.X >= r.X - reach && p.X < r.X + r.W + reach && p.Y >= r.Y - reach && p.Y < r.Y + r.H + reach))
                        if (--FrozenLeft[i] <= 0) { ch.FrozenBroken.Add(i); break; }
                }
            }
            for (int i = 0; i < SpawnerList.Count; i++) TrySpawn(i, ch);
            UpdateReveal(ch);
            for (int i = 0; i < KeyList.Count; i++)
            {
                if (KeyDone[i]) continue;
                // chìa nhặt được khi đủ tỉ lệ pixel quanh nó đã bị lấy (KeyClearFraction; 1 = hết sạch)
                int left = RingPixels(KeyList[i].Rect);
                if (left <= _keyRing[i] * (1.0 - _rules.KeyClearFraction) + 1e-9) { KeyDone[i] = true; ch.KeysCollected.Add(i); }
            }
            return ch;
        }

        private int RingPixels(RectData r)
        {
            int n = 0;
            for (int x = r.X - 1; x <= r.X + r.W; x++) for (int y = r.Y - 1; y <= r.Y + r.H; y++)
                if (!r.Contains(x, y) && ColorAt(x, y) != Empty) n++;
            return n;
        }

        private void UpdateReveal(BoardChanges ch)
        {
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
                if (_mystery[x, y] && !_revealed[x, y] && _c[x, y] != Empty && IsExposed(x, y))
                { _revealed[x, y] = true; ch.Revealed.Add(new PixelData(x, y, _c[x, y])); }
        }

        // Ô phát: khi vùng Area rỗng thì đổ đầy đợt kế tiếp (màu Waves[k]).
        private void TrySpawn(int i, BoardChanges ch)
        {
            var sp = SpawnerList[i]; int k = SpawnWave[i];
            if (sp.Waves == null || k >= sp.Waves.Length) return;
            var a = sp.Area;
            for (int x = a.X; x < a.X + a.W; x++) for (int y = a.Y; y < a.Y + a.H; y++) if (ColorAt(x, y) != Empty) return;
            int color = sp.Waves[k]; SpawnWave[i] = k + 1;
            for (int x = a.X; x < a.X + a.W; x++) for (int y = a.Y; y < a.Y + a.H; y++)
            {
                if (!InBounds(x, y)) continue;
                _c[x, y] = color; _depth[x, y] = 1; Count++; _openDirty = true;
                if (ch != null) ch.Spawned.Add(new PixelData(x, y, color));
            }
        }

        /// <summary>Còn đợt phát chưa đổ (chưa thắng dù bảng đang rỗng).</summary>
        public bool HasPendingWaves
        {
            get { for (int i = 0; i < SpawnerList.Count; i++) if (SpawnerList[i].Waves != null && SpawnWave[i] < SpawnerList[i].Waves.Length) return true; return false; }
        }

        /// <summary>Luật 1c (phiên 3c đề xuất, chờ chốt): thứ tự = BFS từ pixel gần điểm chạm nhất qua cạnh 4 hướng giữa các pixel trong tập lấy được ("đào hầm" từ chỗ chạm,
        /// không bóc lớp); pixel không nối được xếp sau theo khoảng cách. Lấy tối đa max.</summary>
        private List<PixelData> PickByBfs(List<PixelData> list, int max, double sx, double sy)
        {
            if (list.Count == 0) return list;
            var inSet = new Dictionary<int, PixelData>(); foreach (var p in list) inSet[p.X * H + p.Y] = p;
            PixelData start = list[0]; double bd = double.MaxValue;
            foreach (var p in list) { double d = Dist2(p, sx, sy); if (d < bd || (d == bd && (p.Y < start.Y || (p.Y == start.Y && p.X < start.X)))) { bd = d; start = p; } }
            var order = new List<PixelData>(); var seen = new HashSet<int> { start.X * H + start.Y }; var q = new Queue<PixelData>(); q.Enqueue(start);
            while (q.Count > 0 && order.Count < max)
            {
                var p = q.Dequeue(); order.Add(p);
                for (int i = 0; i < 4; i++)
                {
                    int k = (p.X + Dx[i]) * H + (p.Y + Dy[i]); PixelData n;
                    if (InBounds(p.X + Dx[i], p.Y + Dy[i]) && inSet.TryGetValue(k, out n) && seen.Add(k)) q.Enqueue(n);
                }
            }
            if (order.Count < max)
            {
                var rest = new List<PixelData>(); foreach (var p in list) if (!seen.Contains(p.X * H + p.Y)) rest.Add(p);
                rest.Sort((a, b) => { int c = Dist2(a, sx, sy).CompareTo(Dist2(b, sx, sy)); if (c != 0) return c; c = a.Y.CompareTo(b.Y); return c != 0 ? c : a.X.CompareTo(b.X); });
                foreach (var p in rest) { if (order.Count >= max) break; order.Add(p); }
            }
            return order;
        }

        /// <summary>Độ sâu theo bảng hiện tại: 1 = kề ô rỗng / ngoài lưới, tăng dần vào trong (BFS nhiều nguồn). Pixel không nối với lớp ngoài: giữ 0 → đẩy về cuối bằng int.MaxValue.</summary>
        public int[,] CurrentDepth()
        {
            var d = new int[W, H]; var q = new Queue<int>();
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
            {
                if (_c[x, y] == Empty) continue;
                for (int i = 0; i < 4; i++) if (OpenAt(x + Dx[i], y + Dy[i])) { d[x, y] = 1; q.Enqueue(x * H + y); break; }
            }
            while (q.Count > 0)
            {
                int v = q.Dequeue(); int x = v / H, y = v % H;
                for (int i = 0; i < 4; i++) { int nx = x + Dx[i], ny = y + Dy[i]; if (InBounds(nx, ny) && _c[nx, ny] != Empty && d[nx, ny] == 0) { d[nx, ny] = d[x, y] + 1; q.Enqueue(nx * H + ny); } }
            }
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (_c[x, y] != Empty && d[x, y] == 0) d[x, y] = int.MaxValue;
            return d;
        }

        // BFS nhiều nguồn từ ô rỗng/ngoài lưới: lớp ngoài = 1 (độ sâu ban đầu, dùng cho hiển thị / chẩn đoán).
        private void ComputeDepth()
        {
            var q = new Queue<int>();
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
            {
                if (_c[x, y] == Empty) continue;
                bool edge = false;
                for (int i = 0; i < 4; i++) if (OpenAt(x + Dx[i], y + Dy[i])) edge = true;
                if (edge) { _depth[x, y] = 1; q.Enqueue(x * H + y); }
            }
            while (q.Count > 0)
            {
                int v = q.Dequeue(); int x = v / H, y = v % H;
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + Dx[i], ny = y + Dy[i];
                    if (InBounds(nx, ny) && _c[nx, ny] != Empty && _depth[nx, ny] == 0) { _depth[nx, ny] = _depth[x, y] + 1; q.Enqueue(nx * H + ny); }
                }
            }
        }
    }
}
