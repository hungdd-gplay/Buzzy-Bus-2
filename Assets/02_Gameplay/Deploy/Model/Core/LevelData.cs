using System.Collections.Generic;

namespace Buzzy.Core
{
    /// <summary>Một pixel (hành khách) của ảnh: toạ độ ô + mã màu.</summary>
    public struct PixelData
    {
        public int X, Y, Color;
        public PixelData(int x, int y, int color) { X = x; Y = y; Color = color; }
    }

    /// <summary>Dữ liệu một xe trong cột chờ.</summary>
    public struct CarData
    {
        public int Id, Seats, Color;
        /// <summary>Xe bị xích: chưa vào bến cho tới khi chìa cùng KeyColor được nhặt [thiết kế mục 12].</summary>
        public bool Locked;
        public int KeyColor;
        public CarData(int id, int seats, int color) { Id = id; Seats = seats; Color = color; Locked = false; KeyColor = 0; }
        public CarData(int id, int seats, int color, bool locked, int keyColor) { Id = id; Seats = seats; Color = color; Locked = locked; KeyColor = keyColor; }
    }

    /// <summary>Hình chữ nhật theo ô lưới (x, y là ô góc dưới-trái; y có thể = -1 với tường).</summary>
    public struct RectData
    {
        public int X, Y, W, H;
        public RectData(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        public bool Contains(int x, int y) { return x >= X && y >= Y && x < X + W && y < Y + H; }
    }

    /// <summary>Vùng băng (Frozen): pixel bên trong bị khoá tới khi Hits (đã chia hệ số) về 0.</summary>
    public struct FrozenData { public RectData Rect; public int RequiredHits; public FrozenData(RectData r, int hits) { Rect = r; RequiredHits = hits; } }

    /// <summary>Ô phát hành khách: Machine là thân máy, Area là vùng pixel sinh ra, Waves là màu từng đợt.</summary>
    public struct SpawnerData
    {
        public RectData Machine, Area; public int Facing; public int[] Waves;
        public SpawnerData(RectData m, RectData a, int facing, int[] waves) { Machine = m; Area = a; Facing = facing; Waves = waves; }
    }

    /// <summary>Chìa khoá 3x3 nằm trên bảng; mở các xe khoá có KeyColor trùng khi vùng quanh nó hết pixel.</summary>
    public struct KeyData { public RectData Rect; public int Color; public KeyData(RectData r, int color) { Rect = r; Color = color; } }

    /// <summary>Số liệu chỉnh được của mechanic (làm bằng dữ liệu để đổi dễ, xem design-decisions.md).</summary>
    public sealed class MechanicRules
    {
        /// <summary>Số "hit" gốc của Frozen chia hệ số này ra số pixel kề cần lấy.</summary>
        public double FrozenHitDivisor = 12;
        /// <summary>Pixel lấy được trong khoảng này (ô) quanh vùng băng mới tính là một hit.</summary>
        public int FrozenReach = 4;
        /// <summary>Số pixel tối đa Sky Grab nhấc mỗi lần.</summary>
        public int SkyGrabCount = 10;
        /// <summary>true: xe xích vẫn vào ô bến nhưng không nhận pixel tới khi có chìa; false: đứng chờ ở đầu cột, ô để trống.</summary>
        public bool LockedEntersSlot = false;
        /// <summary>Tỉ lệ pixel quanh chìa (vòng 5x5 bỏ 3x3) phải bị lấy thì chìa được nhặt.</summary>
        public double KeyClearFraction = 0.5;
    }

    /// <summary>
    /// Dữ liệu level thuần (POCO, không phụ thuộc JSON/Unity). Loader ngoài tự parse rồi gọi <see cref="Create"/>.
    /// Width/Height trong file là chỉ số lớn nhất (bao gồm) nên lưới thực là (Width+1) x (Height+1).
    /// </summary>
    public sealed class LevelData
    {
        public int Width, Height;
        public List<PixelData> Pixels = new List<PixelData>();
        /// <summary>Các cột xe, mỗi cột từ đầu cột (vào bến trước) đến cuối.</summary>
        public List<List<CarData>> Queues = new List<List<CarData>>();
        // Mechanic M3 (rỗng = level thuần).
        public List<RectData> Walls = new List<RectData>();
        public List<FrozenData> Frozen = new List<FrozenData>();
        public List<SpawnerData> Spawners = new List<SpawnerData>();
        public List<KeyData> Keys = new List<KeyData>();
        /// <summary>Ô pixel ẩn (Mystery): chưa lộ thì chưa lấy được.</summary>
        public List<PixelData> Mystery = new List<PixelData>();

        public int GridWidth { get { return Width + 1; } }
        public int GridHeight { get { return Height + 1; } }

        public static LevelData Create(int width, int height, IEnumerable<PixelData> pixels, IEnumerable<IEnumerable<CarData>> queues)
        {
            var d = new LevelData { Width = width, Height = height };
            d.Pixels.AddRange(pixels);
            foreach (var q in queues) d.Queues.Add(new List<CarData>(q));
            return d;
        }

        /// <summary>Tổng số chỗ xe theo màu.</summary>
        public Dictionary<int, int> SeatsByColor()
        {
            var r = new Dictionary<int, int>();
            foreach (var q in Queues) foreach (var c in q) { int v; r.TryGetValue(c.Color, out v); r[c.Color] = v + c.Seats; }
            return r;
        }

        /// <summary>Số pixel theo màu.</summary>
        public Dictionary<int, int> PixelsByColor()
        {
            var r = new Dictionary<int, int>();
            foreach (var p in Pixels) { int v; r.TryGetValue(p.Color, out v); r[p.Color] = v + 1; }
            return r;
        }
    }
}
