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
        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };

        public int Count { get; private set; }

        public Board(int w, int h, IEnumerable<PixelData> pixels)
        {
            W = w; H = h; _c = new int[w, h]; _depth = new int[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) _c[x, y] = Empty;
            foreach (var p in pixels) if (InBounds(p.X, p.Y) && _c[p.X, p.Y] == Empty) { _c[p.X, p.Y] = p.Color; Count++; }
            ComputeDepth();
        }

        public static Board FromLevel(LevelData d) { return new Board(d.GridWidth, d.GridHeight, d.Pixels); }

        public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }
        public int ColorAt(int x, int y) { return InBounds(x, y) ? _c[x, y] : Empty; }

        /// <summary>Pixel "lộ" nếu có ô kề 4 hướng rỗng hoặc nằm ngoài lưới.</summary>
        public bool IsExposed(int x, int y)
        {
            if (ColorAt(x, y) == Empty) return false;
            for (int i = 0; i < 4; i++) if (ColorAt(x + Dx[i], y + Dy[i]) == Empty) return true;
            return false;
        }

        /// <summary>Lấy pixel làm rỗng ô.</summary>
        public bool Remove(int x, int y)
        {
            if (ColorAt(x, y) == Empty) return false;
            _c[x, y] = Empty; Count--; return true;
        }

        public int CountColor(int color)
        {
            int n = 0;
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (_c[x, y] == color) n++;
            return n;
        }

        public bool HasExposed(int color) { return GetExposed(color).Count > 0; }

        /// <summary>
        /// Các pixel lấy được của một màu (chưa sắp xếp). Luật đã chốt với người dùng: pixel nằm sâu bên trong VẪN bay được,
        /// "lớp ngoài + gần xe nhất" chỉ là THỨ TỰ lấy. Chặn lấy (Frozen, Wall, Mystery...) sẽ lọc ở đây ở mốc M3.
        /// Tên GetExposed giữ lại để không vỡ API.
        /// </summary>
        public List<PixelData> GetExposed(int color)
        {
            var r = new List<PixelData>();
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
                if (_c[x, y] == color) r.Add(new PixelData(x, y, color));
            return r;
        }

        /// <summary>
        /// Chọn tối đa <paramref name="max"/> pixel của màu: lớp ngoài cùng (độ sâu ban đầu nhỏ) trước,
        /// rồi gần (sx, sy) nhất (vị trí ô bến, toạ độ lưới), cuối cùng theo (y, x) để ổn định. Không xoá pixel.
        /// </summary>
        public List<PixelData> PickExposed(int color, int max, double sx, double sy)
        {
            var list = GetExposed(color);
            list.Sort((a, b) =>
            {
                int c = _depth[a.X, a.Y].CompareTo(_depth[b.X, b.Y]);
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

        // BFS nhiều nguồn từ ô rỗng/ngoài lưới: lớp ngoài = 1.
        private void ComputeDepth()
        {
            var q = new Queue<int>();
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
            {
                if (_c[x, y] == Empty) continue;
                bool edge = false;
                for (int i = 0; i < 4; i++) if (ColorAt(x + Dx[i], y + Dy[i]) == Empty) edge = true;
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
