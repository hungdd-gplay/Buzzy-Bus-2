using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaseProject.EditorTools.ImageLayoutScanner
{
    public enum ImageScaleMode
    {
        Fixed = 0,      // Dùng 1 tỉ lệ cố định
        AutoSearch = 1  // Tự dò trong khoảng [scaleMin, scaleMax]
    }

    /// <summary>
    /// Cấu hình quét (Model). Được lưu vào EditorPrefs dạng JSON.
    /// Tỉ lệ (scale) = kích thước thành phần TRONG ảnh demo / kích thước file ảnh thành phần.
    /// Ví dụ demo 540x960 chụp từ thiết kế 1080x1920 -> scale = 0.5.
    /// </summary>
    [Serializable]
    public class ImageLayoutScanSetting
    {
        [Tooltip("Điểm khớp tối thiểu (0..1). 1 = giống hệt. Mặc định 0.92 (đã thử với JPG + co giãn). Ra thừa thì tăng, thiếu thì giảm.")]
        public float thresholdMatch = 0.92f;
        [Tooltip("Số điểm ảnh mẫu lấy từ mỗi ảnh thành phần để quét nhanh.")]
        public int maxSamplePerTemplate = 400;
        [Tooltip("Số lần xuất hiện tối đa của 1 ảnh thành phần trong ảnh demo.")]
        public int maxMatchPerTemplate = 30;
        [Tooltip("Pixel có alpha >= ngưỡng này mới dùng để so khớp (bỏ qua viền trong suốt).")]
        public float alphaOpaque = 0.9f;
        [Tooltip("Ảnh thành phần có diện tích >= tỉ lệ này so với ảnh demo được coi là ảnh lớn (nền, khung).")]
        public float ratioAreaLargeTemplate = 0.3f;
        [Tooltip("Ảnh lớn không khớp được do bị che: cho phép bỏ qua tối đa tỉ lệ pixel này khi so khớp lại.")]
        public float occlusionAllowLargeTemplate = 0.6f;
        [Tooltip("Hai kết quả của cùng 1 ảnh chồng nhau quá tỉ lệ này thì chỉ giữ cái tốt hơn.")]
        public float iouOverlapSameTemplate = 0.3f;
        [Header("Tỉ lệ")]
        public ImageScaleMode scaleMode = ImageScaleMode.Fixed;
        public float scaleFixed = 1f;
        public float scaleMin = 0.5f;
        public float scaleMax = 1.5f;
        public float scaleStep = 0.05f;
        [Tooltip("Tự dò: khi đã khớp chắc chắn 1 ảnh, các ảnh sau chỉ dò quanh tỉ lệ đó (nhanh hơn nhiều).")]
        public bool isLockScaleAfterFirstMatch = true;
        [Tooltip("Chiều rộng bản thiết kế gốc (dùng để tính nhanh tỉ lệ cố định = rộng demo / rộng thiết kế).")]
        public int widthDesignReference = 1080;
        [Tooltip("Build UI theo kích thước gốc của ảnh thành phần (chia cho tỉ lệ), thay vì kích thước trong ảnh demo.")]
        public bool isBuildNativeSize = true;

        public bool isUseGpu = true;
        public bool isIncludeSubFolder = true;
        public bool isAutoConvertToSprite = true;
        public bool isNestChildInParent = true;
        public bool isAddDemoAsReference = true;
        public bool isRaycastTarget = false;
    }

    /// <summary>
    /// Dữ liệu 1 ảnh thành phần đã đọc pixel.
    /// </summary>
    public class ImageTemplateData
    {
        public string pathAsset;
        public string nameTemplate;
        public int width;
        public int height;
        public Color32[] pixels;
        public List<int> opaqueIndices = new List<int>();
        public Vector2Int[] samplePositions;
        public Color32[] sampleColors;
        public bool isLowDetail;
        /// <summary>Tỉ lệ của bản này so với file gốc (1 = gốc).</summary>
        public float scale = 1f;
        /// <summary>Ảnh gốc nếu đây là bản đã co giãn, null nếu chính là ảnh gốc.</summary>
        public ImageTemplateData templateSource;

        public int Area => width * height;
        public ImageTemplateData Source => templateSource ?? this;
    }

    /// <summary>
    /// 1 vị trí tìm thấy ảnh thành phần trong ảnh demo.
    /// Toạ độ rect tính theo pixel, gốc ở góc DƯỚI-TRÁI của ảnh demo (giống Texture2D của Unity).
    /// </summary>
    public class ImageMatchData
    {
        public ImageTemplateData template;
        public RectInt rect;
        public float score;
        public bool isSelected = true;
        public int indexParent = -1;
        public int depth;

        /// <summary>Tỉ lệ thành phần / demo của kết quả này.</summary>
        public float Scale => template.scale;
        public Vector2 Center => new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
        public int Area => rect.width * rect.height;
    }

    /// <summary>
    /// Kết quả 1 lần quét.
    /// </summary>
    public class ImageLayoutScanResult
    {
        public string pathDemo;
        public int widthDemo;
        public int heightDemo;
        public List<ImageMatchData> matches = new List<ImageMatchData>();
        public List<string> warnings = new List<string>();
        public double timeScanSeconds;
        public bool isUsedGpu;
        /// <summary>Tỉ lệ phổ biến (trung vị) của các kết quả.</summary>
        public float scaleDominant = 1f;
        public int numTemplateScanned;
    }
}
