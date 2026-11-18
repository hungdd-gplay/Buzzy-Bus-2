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
        public CarData(int id, int seats, int color) { Id = id; Seats = seats; Color = color; }
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
