using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace BaseProject.EditorTools.ImageLayoutScanner
{
    /// <summary>
    /// Controller: đọc pixel, so khớp ảnh thành phần trong ảnh demo (GPU / CPU), lọc kết quả,
    /// dựng quan hệ cha-con và sinh UI (Canvas + Image) đúng vị trí ảnh demo (tỉ lệ 1:1).
    /// </summary>
    public static class ImageLayoutScanController
    {
        private const string _NAME_COMPUTE_SHADER = "ImageLayoutTemplateMatch";
        private const string _NAME_KERNEL = "CSMatchTemplate";
        private const int _MAX_PIXEL_VERIFY = 20000;
        private const float _COARSE_THRESHOLD_OFFSET = 0.03f;
        private const float _IOU_DUPLICATE_OTHER_TEMPLATE = 0.9f;
        private const float _RELAX_THRESHOLD_COARSE_SCALE = 0.08f;
        private const int _FINE_DIVISION = 5;
        private const int _TOLERANCE_SIZE_FIT_DEMO = 2;
        private const float _RATIO_DETAIL_OCCLUSION_MIN = 0.5f;

        #region Scan

        /// <summary>
        /// Quét toàn bộ ảnh thành phần trong ảnh demo.
        /// onProgress(message, progress01) trả về true nếu người dùng bấm Cancel.
        /// </summary>
        public static ImageLayoutScanResult Scan(string pathDemo, List<string> lsPathTemplate, ImageLayoutScanSetting setting, Func<string, float, bool> onProgress)
        {
            var _result = new ImageLayoutScanResult { pathDemo = pathDemo };
            var _stopwatch = Stopwatch.StartNew();
            _cacheScaled.Clear();

            Color32[] _demoPixels = LoadPixels(pathDemo, out int _demoWidth, out int _demoHeight);
            if (_demoPixels == null)
            {
                _result.warnings.Add("Không đọc được ảnh demo: " + pathDemo);
                return _result;
            }
            _result.widthDemo = _demoWidth;
            _result.heightDemo = _demoHeight;

            // 1) Đọc ảnh thành phần
            var _lsTemplate = new List<ImageTemplateData>();
            for (int i = 0; i < lsPathTemplate.Count; i++)
            {
                if (onProgress != null && onProgress("Đọc ảnh thành phần " + Path.GetFileName(lsPathTemplate[i]), (float)i / lsPathTemplate.Count * 0.1f))
                    return _result;
                var _template = _LoadTemplate(lsPathTemplate[i], setting, _result.warnings);
                if (_template == null)
                    continue;
                _lsTemplate.Add(_template);
            }
            // Ảnh lớn quét trước (nền, khung) -> ảnh nhỏ sau (nút, icon)
            _lsTemplate.Sort((a, b) => b.Area.CompareTo(a.Area));

            // 2) Chuẩn bị GPU
            ComputeShader _shader = setting.isUseGpu ? _FindComputeShader() : null;
            bool _isGpu = _shader != null && SystemInfo.supportsComputeShaders;
            if (setting.isUseGpu && !_isGpu)
                _result.warnings.Add("Không dùng được compute shader, chuyển sang quét bằng CPU.");
            _result.isUsedGpu = _isGpu;

            ComputeBuffer _demoBuffer = null;
            uint[] _demoPacked = _PackPixels(_demoPixels);
            if (_isGpu)
            {
                _demoBuffer = new ComputeBuffer(_demoPacked.Length, sizeof(uint));
                _demoBuffer.SetData(_demoPacked);
            }

            bool _isCancel = false;
            try
            {
                // 3) Quét từng ảnh thành phần theo từng tỉ lệ
                float? _scaleLocked = null;
                for (int i = 0; i < _lsTemplate.Count; i++)
                {
                    var _source = _lsTemplate[i];
                    int _indexTemplate = i;
                    Func<float, string, bool> _onProgressScale = (scale, step) =>
                        onProgress != null && onProgress($"{step} '{_source.nameTemplate}' x{scale:0.###} ({_indexTemplate + 1}/{_lsTemplate.Count})",
                            0.1f + 0.85f * _indexTemplate / _lsTemplate.Count);

                    // Tự dò (chưa khoá): dò thô với ngưỡng nới lỏng để tìm tỉ lệ gần đúng, sau đó dò tinh với ngưỡng thật
                    bool _isRefine = setting.scaleMode == ImageScaleMode.AutoSearch && _scaleLocked == null;
                    float _thresholdCoarse = _isRefine ? setting.thresholdMatch - _RELAX_THRESHOLD_COARSE_SCALE : setting.thresholdMatch;
                    List<float> _lsScale = _GetScalesToScan(setting, _scaleLocked);

                    // Bước 1: so khớp chặt (sai khác trung bình trên mọi điểm mẫu)
                    var _lsFound = _ScanScales(_source, _lsScale, _thresholdCoarse, false, setting, _demoPixels, _demoPacked,
                        _demoWidth, _demoHeight, _isGpu ? _shader : null, _demoBuffer, _onProgressScale, ref _isCancel);
                    // Bước 2: ảnh lớn (nền, khung) không tìm thấy vì bị che -> so khớp chịu che khuất (CPU)
                    bool _isOcclusion = false;
                    if (!_isCancel && _lsFound.Count == 0)
                    {
                        _lsFound = _ScanScales(_source, _lsScale, _thresholdCoarse, true, setting, _demoPixels, _demoPacked,
                            _demoWidth, _demoHeight, null, null, _onProgressScale, ref _isCancel);
                        _isOcclusion = _lsFound.Count > 0;
                    }
                    // Bước 3: dò tinh quanh tỉ lệ tốt nhất
                    if (!_isCancel && _isRefine && _lsFound.Count > 0)
                    {
                        float _scaleBest = _lsFound.OrderByDescending(m => m.score).First().Scale;
                        _lsFound = _ScanScales(_source, _GetScalesFine(setting, _scaleBest, 5), setting.thresholdMatch, _isOcclusion, setting,
                            _demoPixels, _demoPacked, _demoWidth, _demoHeight, _isOcclusion || !_isGpu ? null : _shader, _demoBuffer,
                            _onProgressScale, ref _isCancel);
                    }
                    if (_isCancel)
                        break;
                    if (_isOcclusion && _lsFound.Count > 0)
                        _result.warnings.Add($"'{_source.nameTemplate}' bị che nhiều, đã khớp ở chế độ chịu che khuất. Hãy kiểm tra lại vị trí.");

                    // Gộp kết quả giữa các tỉ lệ: cùng vị trí chỉ giữ tỉ lệ có điểm cao nhất
                    var _lsMerged = _SuppressOverlap(_lsFound, setting.iouOverlapSameTemplate, setting.maxMatchPerTemplate);
                    foreach (var _match in _lsMerged)
                    {
                        if (_IsDuplicateOfOtherTemplate(_result.matches, _match))
                            continue;
                        _result.matches.Add(_match);
                    }

                    // Khoá tỉ lệ theo ảnh khớp chắc chắn đầu tiên (bỏ qua ảnh 1 màu dễ khớp nhầm)
                    if (setting.scaleMode == ImageScaleMode.AutoSearch && setting.isLockScaleAfterFirstMatch
                        && _scaleLocked == null && _lsMerged.Count > 0 && !_source.isLowDetail)
                    {
                        _scaleLocked = _lsMerged[0].Scale;
                        _result.warnings.Add($"Đã khoá tỉ lệ x{_scaleLocked:0.###} theo '{_source.nameTemplate}'. Các ảnh sau chỉ dò quanh tỉ lệ này.");
                    }
                    _result.numTemplateScanned++;
                }
            }
            finally
            {
                _demoBuffer?.Release();
            }

            // 4) Quan hệ cha - con và thứ tự hiển thị
            _BuildHierarchy(_result.matches, setting.isNestChildInParent);
            _result.scaleDominant = _GetScaleDominant(_result.matches);
            _cacheScaled.Clear();

            _stopwatch.Stop();
            _result.timeScanSeconds = _stopwatch.Elapsed.TotalSeconds;
            return _result;
        }

        private static ImageTemplateData _LoadTemplate(string pathAsset, ImageLayoutScanSetting setting, List<string> warnings)
        {
            Color32[] _pixels = LoadPixels(pathAsset, out int _width, out int _height);
            if (_pixels == null)
            {
                warnings.Add("Không đọc được: " + pathAsset);
                return null;
            }

            var _template = new ImageTemplateData
            {
                pathAsset = pathAsset,
                nameTemplate = Path.GetFileNameWithoutExtension(pathAsset),
                width = _width,
                height = _height,
                pixels = _pixels
            };

            if (!_BuildOpaqueAndSamples(_template, setting))
            {
                warnings.Add($"Bỏ qua '{_template.nameTemplate}': ảnh trong suốt hoàn toàn.");
                return null;
            }
            if (_template.isLowDetail)
                warnings.Add($"'{_template.nameTemplate}' gần như 1 màu, dễ khớp nhầm. Nên tăng ngưỡng hoặc loại khỏi danh sách.");
            return _template;
        }

        /// <summary>Tìm pixel đục và lấy điểm mẫu. Trả về false nếu ảnh trong suốt hoàn toàn.</summary>
        private static bool _BuildOpaqueAndSamples(ImageTemplateData template, ImageLayoutScanSetting setting)
        {
            var _pixels = template.pixels;
            template.opaqueIndices.Clear();
            byte _alphaMin = (byte)Mathf.Clamp(Mathf.RoundToInt(setting.alphaOpaque * 255f), 1, 255);
            for (int i = 0; i < _pixels.Length; i++)
            {
                if (_pixels[i].a >= _alphaMin)
                    template.opaqueIndices.Add(i);
            }
            if (template.opaqueIndices.Count == 0)
            {
                // Ảnh toàn bán trong suốt -> hạ ngưỡng
                for (int i = 0; i < _pixels.Length; i++)
                    if (_pixels[i].a >= 128) template.opaqueIndices.Add(i);
            }
            if (template.opaqueIndices.Count == 0)
                return false;
            _BuildSamples(template, Mathf.Max(16, setting.maxSamplePerTemplate));
            return true;
        }

        /// <summary>Lấy các điểm mẫu rải đều trên vùng đục của ảnh thành phần.</summary>
        private static void _BuildSamples(ImageTemplateData template, int maxSample)
        {
            var _indices = template.opaqueIndices;
            int _count = Mathf.Min(maxSample, _indices.Count);
            template.samplePositions = new Vector2Int[_count];
            template.sampleColors = new Color32[_count];

            float _sumLuma = 0, _sumLumaSqr = 0;
            for (int i = 0; i < _count; i++)
            {
                // Rải đều theo thứ tự pixel đục + lệch nhẹ để tránh trùng hàng
                long _pick = (long)i * _indices.Count / _count;
                int _index = _indices[(int)_pick];
                var _color = template.pixels[_index];
                template.samplePositions[i] = new Vector2Int(_index % template.width, _index / template.width);
                template.sampleColors[i] = _color;

                float _luma = 0.299f * _color.r + 0.587f * _color.g + 0.114f * _color.b;
                _sumLuma += _luma;
                _sumLumaSqr += _luma * _luma;
            }
            float _mean = _sumLuma / _count;
            float _variance = _sumLumaSqr / _count - _mean * _mean;
            template.isLowDetail = _variance < 9f; // độ lệch chuẩn độ sáng < 3
        }

        private static float[] _MatchGpu(ComputeShader shader, ComputeBuffer demoBuffer, int demoWidth, ImageTemplateData template, int resultWidth, int resultHeight)
        {
            int _kernel = shader.FindKernel(_NAME_KERNEL);
            int _sampleCount = template.samplePositions.Length;
            var _samples = new int[_sampleCount * 4];
            for (int i = 0; i < _sampleCount; i++)
            {
                _samples[i * 4 + 0] = template.samplePositions[i].x;
                _samples[i * 4 + 1] = template.samplePositions[i].y;
                _samples[i * 4 + 2] = unchecked((int)_PackColor(template.sampleColors[i]));
                _samples[i * 4 + 3] = 0;
            }

            var _sampleBuffer = new ComputeBuffer(_sampleCount, sizeof(int) * 4);
            var _scoreBuffer = new ComputeBuffer(resultWidth * resultHeight, sizeof(float));
            try
            {
                _sampleBuffer.SetData(_samples);
                shader.SetBuffer(_kernel, "_DemoPixels", demoBuffer);
                shader.SetBuffer(_kernel, "_Samples", _sampleBuffer);
                shader.SetBuffer(_kernel, "_Scores", _scoreBuffer);
                shader.SetInt("_DemoWidth", demoWidth);
                shader.SetInt("_SampleCount", _sampleCount);
                shader.SetInt("_ResultWidth", resultWidth);
                shader.SetInt("_ResultHeight", resultHeight);
                shader.Dispatch(_kernel, Mathf.CeilToInt(resultWidth / 8f), Mathf.CeilToInt(resultHeight / 8f), 1);

                var _scores = new float[resultWidth * resultHeight];
                _scoreBuffer.GetData(_scores);
                return _scores;
            }
            finally
            {
                _sampleBuffer.Release();
                _scoreBuffer.Release();
            }
        }

        private static float[] _MatchCpu(uint[] demoPacked, int demoWidth, ImageTemplateData template, int resultWidth, int resultHeight, float thresholdCoarse)
        {
            int _sampleCount = template.samplePositions.Length;
            var _offsets = new int[_sampleCount];
            var _colors = new int[_sampleCount * 3];
            for (int i = 0; i < _sampleCount; i++)
            {
                _offsets[i] = template.samplePositions[i].y * demoWidth + template.samplePositions[i].x;
                _colors[i * 3 + 0] = template.sampleColors[i].r;
                _colors[i * 3 + 1] = template.sampleColors[i].g;
                _colors[i * 3 + 2] = template.sampleColors[i].b;
            }
            float _maxDiffTotal = _sampleCount * 765f;
            // Vượt quá mức này thì chắc chắn không đạt ngưỡng -> dừng sớm
            float _maxDiffAllowed = (1f - thresholdCoarse) * _maxDiffTotal;
            var _scores = new float[resultWidth * resultHeight];

            Parallel.For(0, resultHeight, y =>
            {
                for (int x = 0; x < resultWidth; x++)
                {
                    int _baseIndex = y * demoWidth + x;
                    float _sumDiff = 0;
                    bool _isFail = false;
                    for (int i = 0; i < _sampleCount; i++)
                    {
                        uint _demoColor = demoPacked[_baseIndex + _offsets[i]];
                        _sumDiff += Math.Abs((int)(_demoColor & 255u) - _colors[i * 3])
                                  + Math.Abs((int)((_demoColor >> 8) & 255u) - _colors[i * 3 + 1])
                                  + Math.Abs((int)((_demoColor >> 16) & 255u) - _colors[i * 3 + 2]);
                        if (_sumDiff > _maxDiffAllowed)
                        {
                            _isFail = true;
                            break;
                        }
                    }
                    _scores[y * resultWidth + x] = _isFail ? 0f : 1f - _sumDiff / _maxDiffTotal;
                }
            });
            return _scores;
        }

        /// <summary>Quét 1 ảnh thành phần (đã co giãn) trên ảnh demo, trả về các vị trí đạt ngưỡng.</summary>
        private static List<ImageMatchData> _FindMatchesForTemplate(ImageTemplateData template, ImageLayoutScanSetting setting, float threshold,
            Color32[] demoPixels, uint[] demoPacked, int demoWidth, int demoHeight, ComputeShader shaderGpu, ComputeBuffer demoBuffer, bool isOcclusionMode)
        {
            int _resultWidth = demoWidth - template.width + 1;
            int _resultHeight = demoHeight - template.height + 1;
            float _ratioKeep = isOcclusionMode ? Mathf.Clamp01(1f - setting.occlusionAllowLargeTemplate) : 1f;

            float[] _scores;
            if (isOcclusionMode)
                _scores = _MatchCpuTrimmed(demoPacked, demoWidth, template, _resultWidth, _resultHeight, _ratioKeep);
            else if (shaderGpu != null)
                _scores = _MatchGpu(shaderGpu, demoBuffer, demoWidth, template, _resultWidth, _resultHeight);
            else
                _scores = _MatchCpu(demoPacked, demoWidth, template, _resultWidth, _resultHeight, threshold - _COARSE_THRESHOLD_OFFSET);

            var _lsResult = new List<ImageMatchData>();
            foreach (var _match in _PickBestMatches(_scores, _resultWidth, _resultHeight, template, setting, threshold))
            {
                // Xác minh lại bằng toàn bộ pixel đục của ảnh thành phần
                _match.score = _ScoreFull(demoPixels, demoWidth, template, _match.rect.x, _match.rect.y, _ratioKeep);
                if (_match.score >= threshold)
                    _lsResult.Add(_match);
            }
            return _lsResult;
        }

        #region Scale

        /// <summary>Quét 1 ảnh gốc ở nhiều tỉ lệ, trả về tất cả vị trí đạt ngưỡng.</summary>
        private static List<ImageMatchData> _ScanScales(ImageTemplateData source, List<float> lsScale, float threshold, bool isOcclusionMode,
            ImageLayoutScanSetting setting, Color32[] demoPixels, uint[] demoPacked, int demoWidth, int demoHeight,
            ComputeShader shaderGpu, ComputeBuffer demoBuffer, Func<float, string, bool> onProgressScale, ref bool isCancel)
        {
            var _lsFound = new List<ImageMatchData>();
            foreach (float _scale in lsScale)
            {
                if (onProgressScale != null && onProgressScale(_scale, isOcclusionMode ? "Quét (bị che)" : "Quét"))
                {
                    isCancel = true;
                    break;
                }
                var _scaled = _GetScaledTemplate(source, _scale, setting, demoWidth, demoHeight);
                if (!_IsFitDemo(_scaled, demoWidth, demoHeight))
                    continue;
                if (isOcclusionMode && _scaled.Area < setting.ratioAreaLargeTemplate * demoWidth * demoHeight)
                    continue;
                _lsFound.AddRange(_FindMatchesForTemplate(_scaled, setting, threshold, demoPixels, demoPacked, demoWidth, demoHeight,
                    isOcclusionMode ? null : shaderGpu, isOcclusionMode ? null : demoBuffer, isOcclusionMode));
            }
            return _lsFound;
        }

        /// <summary>Các tỉ lệ dò tinh quanh 1 tỉ lệ: scale ± k * (bước / 5).</summary>
        private static List<float> _GetScalesFine(ImageLayoutScanSetting setting, float scaleCenter, int numStepEachSide)
        {
            float _fine = Mathf.Max(0.002f, setting.scaleStep / _FINE_DIVISION);
            var _lsScale = new List<float> { scaleCenter };
            for (int k = 1; k <= numStepEachSide; k++)
            {
                _lsScale.Add((float)Math.Round(scaleCenter - k * _fine, 4));
                _lsScale.Add((float)Math.Round(scaleCenter + k * _fine, 4));
            }
            _lsScale.RemoveAll(v => v <= 0.01f);
            return _lsScale;
        }

        private static bool _IsFitDemo(ImageTemplateData template, int demoWidth, int demoHeight)
        {
            return template != null && template.width >= 2 && template.height >= 2
                && template.width <= demoWidth && template.height <= demoHeight;
        }

        /// <summary>Danh sách tỉ lệ cần quét cho 1 ảnh.</summary>
        private static List<float> _GetScalesToScan(ImageLayoutScanSetting setting, float? scaleLocked)
        {
            var _lsScale = new List<float>();
            if (setting.scaleMode == ImageScaleMode.Fixed)
            {
                _lsScale.Add(Mathf.Max(0.01f, setting.scaleFixed));
                return _lsScale;
            }

            float _step = Mathf.Max(0.005f, setting.scaleStep);
            float _min = Mathf.Max(0.01f, Mathf.Min(setting.scaleMin, setting.scaleMax));
            float _max = Mathf.Max(setting.scaleMin, setting.scaleMax);
            if (scaleLocked.HasValue)
            {
                // Đã khoá: chỉ dò tinh quanh tỉ lệ đã khoá (±2 bước nhỏ)
                return _GetScalesFine(setting, scaleLocked.Value, 2);
            }
            // Ưu tiên quét từ tỉ lệ 1 (hoặc gần nhất) ra 2 phía
            for (float _scale = _min; _scale <= _max + 0.0001f; _scale += _step)
                _lsScale.Add((float)Math.Round(_scale, 4));
            _lsScale.Sort((a, b) => Math.Abs(a - 1f).CompareTo(Math.Abs(b - 1f)));
            return _lsScale;
        }

        /// <summary>Tạo (hoặc lấy từ cache) bản co giãn của ảnh thành phần.</summary>
        private static ImageTemplateData _GetScaledTemplate(ImageTemplateData source, float scale, ImageLayoutScanSetting setting, int demoWidth, int demoHeight)
        {
            if (Mathf.Abs(scale - 1f) < 0.0001f)
                return source;
            int _width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int _height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            // Sai số làm tròn: ảnh lớn hơn demo tối đa 2px (thường là ảnh nền full màn) -> ép về đúng kích thước demo
            if (_width > demoWidth && _width - demoWidth <= _TOLERANCE_SIZE_FIT_DEMO) _width = demoWidth;
            if (_height > demoHeight && _height - demoHeight <= _TOLERANCE_SIZE_FIT_DEMO) _height = demoHeight;
            string _key = source.pathAsset + "|" + _width + "x" + _height;
            if (_cacheScaled.TryGetValue(_key, out var _cached))
                return _cached;

            var _scaled = new ImageTemplateData
            {
                pathAsset = source.pathAsset,
                nameTemplate = source.nameTemplate,
                width = _width,
                height = _height,
                pixels = _ResizePixels(source.pixels, source.width, source.height, _width, _height),
                scale = scale,
                templateSource = source
            };
            _BuildOpaqueAndSamples(_scaled, setting);
            _cacheScaled[_key] = _scaled;
            return _scaled;
        }

        private static readonly Dictionary<string, ImageTemplateData> _cacheScaled = new Dictionary<string, ImageTemplateData>();

        /// <summary>
        /// Co giãn ảnh: thu nhỏ dùng lọc trung bình vùng (giống cách ảnh demo bị thu nhỏ),
        /// phóng to dùng nội suy song tuyến. Màu được nhân alpha trước khi trộn để viền không bị tối.
        /// </summary>
        private static Color32[] _ResizePixels(Color32[] source, int widthSource, int heightSource, int widthTarget, int heightTarget)
        {
            var _result = new Color32[widthTarget * heightTarget];
            float _ratioX = (float)widthSource / widthTarget;
            float _ratioY = (float)heightSource / heightTarget;
            int _subX = Mathf.Clamp(Mathf.CeilToInt(_ratioX), 1, 8);
            int _subY = Mathf.Clamp(Mathf.CeilToInt(_ratioY), 1, 8);

            Parallel.For(0, heightTarget, y =>
            {
                for (int x = 0; x < widthTarget; x++)
                {
                    float _r = 0, _g = 0, _b = 0, _a = 0;
                    for (int sy = 0; sy < _subY; sy++)
                    {
                        float _fy = (y + (sy + 0.5f) / _subY) * _ratioY - 0.5f;
                        for (int sx = 0; sx < _subX; sx++)
                        {
                            float _fx = (x + (sx + 0.5f) / _subX) * _ratioX - 0.5f;
                            _SampleBilinear(source, widthSource, heightSource, _fx, _fy, ref _r, ref _g, ref _b, ref _a);
                        }
                    }
                    float _count = _subX * _subY;
                    _a /= _count;
                    if (_a > 0.0001f)
                    {
                        // Bỏ nhân alpha
                        _r /= _count * _a; _g /= _count * _a; _b /= _count * _a;
                    }
                    _result[y * widthTarget + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(_r), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(_g), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(_b), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(_a * 255f), 0, 255));
                }
            });
            return _result;
        }

        private static void _SampleBilinear(Color32[] source, int width, int height, float fx, float fy, ref float r, ref float g, ref float b, ref float a)
        {
            fx = Mathf.Clamp(fx, 0, width - 1);
            fy = Mathf.Clamp(fy, 0, height - 1);
            int _x0 = (int)fx, _y0 = (int)fy;
            int _x1 = Math.Min(_x0 + 1, width - 1), _y1 = Math.Min(_y0 + 1, height - 1);
            float _tx = fx - _x0, _ty = fy - _y0;
            _Accumulate(source[_y0 * width + _x0], (1 - _tx) * (1 - _ty), ref r, ref g, ref b, ref a);
            _Accumulate(source[_y0 * width + _x1], _tx * (1 - _ty), ref r, ref g, ref b, ref a);
            _Accumulate(source[_y1 * width + _x0], (1 - _tx) * _ty, ref r, ref g, ref b, ref a);
            _Accumulate(source[_y1 * width + _x1], _tx * _ty, ref r, ref g, ref b, ref a);
        }

        private static void _Accumulate(Color32 color, float weight, ref float r, ref float g, ref float b, ref float a)
        {
            float _alpha = color.a / 255f * weight;
            r += color.r * _alpha;
            g += color.g * _alpha;
            b += color.b * _alpha;
            a += _alpha;
        }

        /// <summary>Gộp kết quả (nhiều tỉ lệ) của cùng 1 ảnh: điểm cao giữ lại, vị trí chồng lấn bị loại.</summary>
        private static List<ImageMatchData> _SuppressOverlap(List<ImageMatchData> lsMatch, float iouMax, int maxCount)
        {
            lsMatch.Sort((a, b) => b.score.CompareTo(a.score));
            var _lsResult = new List<ImageMatchData>();
            foreach (var _match in lsMatch)
            {
                bool _isOverlap = false;
                foreach (var _accepted in _lsResult)
                {
                    if (_IntersectionOverUnion(_accepted.rect, _match.rect) > iouMax || _IsContainEachOtherSameSource(_accepted, _match))
                    {
                        _isOverlap = true;
                        break;
                    }
                }
                if (_isOverlap)
                    continue;
                _lsResult.Add(_match);
                if (_lsResult.Count >= maxCount)
                    break;
            }
            return _lsResult;
        }

        /// <summary>2 tỉ lệ khác nhau của cùng 1 ảnh, cái này nằm trọn trong cái kia và cùng tâm -> trùng.</summary>
        private static bool _IsContainEachOtherSameSource(ImageMatchData a, ImageMatchData b)
        {
            if (Mathf.Abs(a.Scale - b.Scale) < 0.0001f)
                return false;
            if (!_IsContain(a.rect, b.rect) && !_IsContain(b.rect, a.rect))
                return false;
            float _distance = Vector2.Distance(a.Center, b.Center);
            return _distance <= Mathf.Max(a.rect.width, a.rect.height, b.rect.width, b.rect.height) * 0.15f;
        }

        private static float _GetScaleDominant(List<ImageMatchData> lsMatch)
        {
            if (lsMatch.Count == 0)
                return 1f;
            var _scales = lsMatch.Select(m => m.Scale).OrderBy(v => v).ToList();
            return _scales[_scales.Count / 2];
        }

        public static void ClearCacheScaled()
        {
            _cacheScaled.Clear();
        }

        #endregion

        /// <summary>So khớp chịu che khuất: chỉ tính trung bình trên phần điểm mẫu khớp tốt nhất (ratioKeep).</summary>
        private static float[] _MatchCpuTrimmed(uint[] demoPacked, int demoWidth, ImageTemplateData template, int resultWidth, int resultHeight, float ratioKeep)
        {
            int _sampleCount = template.samplePositions.Length;
            int _keep = Mathf.Max(1, Mathf.RoundToInt(_sampleCount * ratioKeep));
            var _offsets = new int[_sampleCount];
            for (int i = 0; i < _sampleCount; i++)
                _offsets[i] = template.samplePositions[i].y * demoWidth + template.samplePositions[i].x;
            var _colors = template.sampleColors;
            var _scores = new float[resultWidth * resultHeight];

            Parallel.For(0, resultHeight, () => new int[_sampleCount], (y, _, diffs) =>
            {
                for (int x = 0; x < resultWidth; x++)
                {
                    int _baseIndex = y * demoWidth + x;
                    for (int i = 0; i < _sampleCount; i++)
                    {
                        uint _demoColor = demoPacked[_baseIndex + _offsets[i]];
                        diffs[i] = Math.Abs((int)(_demoColor & 255u) - _colors[i].r)
                                 + Math.Abs((int)((_demoColor >> 8) & 255u) - _colors[i].g)
                                 + Math.Abs((int)((_demoColor >> 16) & 255u) - _colors[i].b);
                    }
                    Array.Sort(diffs);
                    long _sum = 0;
                    for (int i = 0; i < _keep; i++)
                        _sum += diffs[i];
                    _scores[y * resultWidth + x] = 1f - _sum / (_keep * 765f);
                }
                return diffs;
            }, _ => { });
            return _scores;
        }

        /// <summary>Lấy các vị trí điểm cao nhất, loại các vị trí chồng nhau (non-maximum suppression).</summary>
        private static List<ImageMatchData> _PickBestMatches(float[] scores, int resultWidth, int resultHeight, ImageTemplateData template, ImageLayoutScanSetting setting, float threshold)
        {
            float _thresholdCoarse = threshold - _COARSE_THRESHOLD_OFFSET;
            var _candidates = new List<int>();
            for (int i = 0; i < scores.Length; i++)
            {
                if (scores[i] >= _thresholdCoarse)
                    _candidates.Add(i);
            }
            _candidates.Sort((a, b) => scores[b].CompareTo(scores[a]));

            int _maxCandidateCheck = Mathf.Max(1, setting.maxMatchPerTemplate) * 400;
            var _lsMatch = new List<ImageMatchData>();
            for (int c = 0; c < _candidates.Count && c < _maxCandidateCheck; c++)
            {
                int _index = _candidates[c];
                var _rect = new RectInt(_index % resultWidth, _index / resultWidth, template.width, template.height);
                bool _isOverlap = false;
                foreach (var _accepted in _lsMatch)
                {
                    if (_IntersectionOverUnion(_accepted.rect, _rect) > setting.iouOverlapSameTemplate)
                    {
                        _isOverlap = true;
                        break;
                    }
                }
                if (_isOverlap)
                    continue;
                _lsMatch.Add(new ImageMatchData { template = template, rect = _rect, score = scores[_index] });
                if (_lsMatch.Count >= setting.maxMatchPerTemplate)
                    break;
            }
            return _lsMatch;
        }

        /// <summary>
        /// Điểm khớp dùng toàn bộ pixel đục (tối đa _MAX_PIXEL_VERIFY pixel).
        /// ratioKeep &lt; 1: chỉ tính phần pixel khớp tốt nhất (chế độ chịu che khuất).
        /// </summary>
        private static float _ScoreFull(Color32[] demoPixels, int demoWidth, ImageTemplateData template, int posX, int posY, float ratioKeep)
        {
            var _indices = template.opaqueIndices;
            int _step = Mathf.Max(1, _indices.Count / _MAX_PIXEL_VERIFY);
            int _count = (_indices.Count + _step - 1) / _step;
            var _diffs = new int[_count];
            var _lumas = new float[_count];
            int _n = 0;
            for (int i = 0; i < _indices.Count && _n < _count; i += _step, _n++)
            {
                int _index = _indices[i];
                int _tx = _index % template.width;
                int _ty = _index / template.width;
                var _t = template.pixels[_index];
                var _d = demoPixels[(posY + _ty) * demoWidth + posX + _tx];
                _diffs[_n] = Math.Abs(_t.r - _d.r) + Math.Abs(_t.g - _d.g) + Math.Abs(_t.b - _d.b);
                _lumas[_n] = 0.299f * _t.r + 0.587f * _t.g + 0.114f * _t.b;
            }

            if (ratioKeep >= 1f)
            {
                double _sumAll = 0;
                for (int i = 0; i < _n; i++)
                    _sumAll += _diffs[i];
                return (float)(1.0 - _sumAll / (_n * 765.0));
            }

            // Chế độ chịu che khuất: chỉ giữ phần pixel khớp tốt nhất
            var _order = new int[_n];
            for (int i = 0; i < _n; i++) _order[i] = i;
            var _keys = new int[_n];
            Array.Copy(_diffs, _keys, _n);
            Array.Sort(_keys, _order);
            int _keep = Mathf.Max(1, Mathf.RoundToInt(_n * ratioKeep));

            // Phần pixel giữ lại phải còn đủ chi tiết, tránh khớp nhầm khi chỉ còn lại vùng 1 màu
            if (_GetStdDev(_lumas, null, _n) > 0.0001f && _GetStdDev(_lumas, _order, _keep) < _RATIO_DETAIL_OCCLUSION_MIN * _GetStdDev(_lumas, null, _n))
                return 0f;

            double _sumDiff = 0;
            for (int i = 0; i < _keep; i++)
                _sumDiff += _keys[i];
            return (float)(1.0 - _sumDiff / (_keep * 765.0));
        }

        private static float _GetStdDev(float[] values, int[] order, int count)
        {
            double _sum = 0, _sumSqr = 0;
            for (int i = 0; i < count; i++)
            {
                float _value = order != null ? values[order[i]] : values[i];
                _sum += _value;
                _sumSqr += _value * _value;
            }
            double _mean = _sum / count;
            return (float)Math.Sqrt(Math.Max(0, _sumSqr / count - _mean * _mean));
        }

        private static bool _IsDuplicateOfOtherTemplate(List<ImageMatchData> lsAccepted, ImageMatchData match)
        {
            foreach (var _accepted in lsAccepted)
            {
                if (_accepted.template.Source == match.template.Source)
                    continue;
                if (_IntersectionOverUnion(_accepted.rect, match.rect) >= _IOU_DUPLICATE_OTHER_TEMPLATE)
                    return true; // 2 ảnh thành phần giống nhau -> giữ ảnh đã quét trước
            }
            return false;
        }

        /// <summary>Gán cha = ảnh nhỏ nhất bao trọn ảnh này. Danh sách được sắp: cha trước con, lớn trước nhỏ.</summary>
        private static void _BuildHierarchy(List<ImageMatchData> lsMatch, bool isNest)
        {
            lsMatch.Sort((a, b) =>
            {
                int _compare = b.Area.CompareTo(a.Area);
                return _compare != 0 ? _compare : b.score.CompareTo(a.score);
            });
            for (int i = 0; i < lsMatch.Count; i++)
            {
                lsMatch[i].indexParent = -1;
                lsMatch[i].depth = 0;
                if (!isNest)
                    continue;
                int _bestParent = -1;
                for (int j = 0; j < i; j++)
                {
                    if (lsMatch[j].Area <= lsMatch[i].Area)
                        continue;
                    if (!_IsContain(lsMatch[j].rect, lsMatch[i].rect))
                        continue;
                    if (_bestParent < 0 || lsMatch[j].Area < lsMatch[_bestParent].Area)
                        _bestParent = j;
                }
                lsMatch[i].indexParent = _bestParent;
                lsMatch[i].depth = _bestParent >= 0 ? lsMatch[_bestParent].depth + 1 : 0;
            }
        }

        private static bool _IsContain(RectInt outer, RectInt inner)
        {
            const int _TOLERANCE = 1;
            return inner.xMin >= outer.xMin - _TOLERANCE && inner.yMin >= outer.yMin - _TOLERANCE
                && inner.xMax <= outer.xMax + _TOLERANCE && inner.yMax <= outer.yMax + _TOLERANCE;
        }

        private static float _IntersectionOverUnion(RectInt a, RectInt b)
        {
            int _xMin = Mathf.Max(a.xMin, b.xMin);
            int _yMin = Mathf.Max(a.yMin, b.yMin);
            int _xMax = Mathf.Min(a.xMax, b.xMax);
            int _yMax = Mathf.Min(a.yMax, b.yMax);
            if (_xMax <= _xMin || _yMax <= _yMin)
                return 0f;
            float _intersect = (float)(_xMax - _xMin) * (_yMax - _yMin);
            float _union = (float)a.width * a.height + (float)b.width * b.height - _intersect;
            return _intersect / _union;
        }

        #endregion

        #region Pixel IO

        /// <summary>
        /// Đọc pixel gốc của ảnh (không bị nén / thu nhỏ bởi Import Settings).
        /// PNG/JPG đọc trực tiếp từ file; định dạng khác đọc qua RenderTexture.
        /// Gốc toạ độ: góc dưới-trái.
        /// </summary>
        public static Color32[] LoadPixels(string pathAsset, out int width, out int height)
        {
            width = height = 0;
            string _extension = Path.GetExtension(pathAsset).ToLowerInvariant();
            if ((_extension == ".png" || _extension == ".jpg" || _extension == ".jpeg") && File.Exists(pathAsset))
            {
                var _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (_texture.LoadImage(File.ReadAllBytes(pathAsset)))
                    {
                        width = _texture.width;
                        height = _texture.height;
                        return _texture.GetPixels32();
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(_texture);
                }
            }
            return _LoadPixelsByBlit(pathAsset, out width, out height);
        }

        private static Color32[] _LoadPixelsByBlit(string pathAsset, out int width, out int height)
        {
            width = height = 0;
            var _source = AssetDatabase.LoadAssetAtPath<Texture2D>(pathAsset);
            if (_source == null)
                return null;
            width = _source.width;
            height = _source.height;
            var _renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var _previous = RenderTexture.active;
            var _readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(_source, _renderTexture);
                RenderTexture.active = _renderTexture;
                _readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                _readable.Apply();
                return _readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = _previous;
                RenderTexture.ReleaseTemporary(_renderTexture);
                UnityEngine.Object.DestroyImmediate(_readable);
            }
        }

        private static uint[] _PackPixels(Color32[] pixels)
        {
            var _packed = new uint[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                _packed[i] = _PackColor(pixels[i]);
            return _packed;
        }

        private static uint _PackColor(Color32 color)
        {
            return color.r | ((uint)color.g << 8) | ((uint)color.b << 16) | ((uint)color.a << 24);
        }

        private static ComputeShader _FindComputeShader()
        {
            foreach (var _guid in AssetDatabase.FindAssets(_NAME_COMPUTE_SHADER + " t:ComputeShader"))
            {
                var _shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(AssetDatabase.GUIDToAssetPath(_guid));
                if (_shader != null)
                    return _shader;
            }
            return null;
        }

        /// <summary>Danh sách ảnh trong thư mục (bỏ qua chính ảnh demo).</summary>
        public static List<string> FindTemplatePaths(string pathFolder, bool isIncludeSubFolder, string pathDemoExclude)
        {
            var _result = new List<string>();
            if (string.IsNullOrEmpty(pathFolder) || !AssetDatabase.IsValidFolder(pathFolder))
                return _result;
            string _folderNormalized = pathFolder.TrimEnd('/');
            foreach (var _guid in AssetDatabase.FindAssets("t:Texture2D", new[] { _folderNormalized }))
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                if (_path == pathDemoExclude)
                    continue;
                if (!isIncludeSubFolder && Path.GetDirectoryName(_path).Replace('\\', '/') != _folderNormalized)
                    continue;
                if (!_result.Contains(_path))
                    _result.Add(_path);
            }
            _result.Sort(StringComparer.OrdinalIgnoreCase);
            return _result;
        }

        #endregion

        #region Build UI

        /// <summary>
        /// Sinh UI: Root (kích thước = ảnh demo) + Image cho từng kết quả, đúng vị trí, tỉ lệ 1:1.
        /// Đặt dưới object đang chọn nếu nó nằm trong Canvas, nếu không thì tạo Canvas mới.
        /// </summary>
        public static GameObject BuildUI(ImageLayoutScanResult result, ImageLayoutScanSetting setting, Transform transformSelected)
        {
            Vector2 _sizeUI = GetSizeUI(result, setting);
            Transform _parent = _GetOrCreateParentInCanvas(transformSelected, Mathf.RoundToInt(_sizeUI.x), Mathf.RoundToInt(_sizeUI.y));
            string _nameDemo = Path.GetFileNameWithoutExtension(result.pathDemo);

            var _root = BuildLayout(result, setting, _parent, "Layout_" + _nameDemo, setting.isAddDemoAsReference);
            Undo.RegisterCreatedObjectUndo(_root, "Build Layout From Image");
            Selection.activeGameObject = _root;
            EditorGUIUtility.PingObject(_root);
            return _root;
        }

        /// <summary>Hệ số đổi từ pixel demo sang pixel UI: build theo kích thước gốc của ảnh thành phần thì chia cho tỉ lệ.</summary>
        public static float GetFactorDemoToUI(ImageLayoutScanResult result, ImageLayoutScanSetting setting)
        {
            return setting.isBuildNativeSize && result.scaleDominant > 0.0001f ? 1f / result.scaleDominant : 1f;
        }

        /// <summary>Kích thước UI tương ứng toàn bộ ảnh demo.</summary>
        public static Vector2 GetSizeUI(ImageLayoutScanResult result, ImageLayoutScanSetting setting)
        {
            float _factor = GetFactorDemoToUI(result, setting);
            return new Vector2(result.widthDemo * _factor, result.heightDemo * _factor);
        }

        /// <summary>
        /// Dựng cây UI (Image lồng nhau theo quan hệ cha-con) dưới parent. Không tạo Canvas.
        /// Dùng chung cho Build UI và Build Popup.
        /// </summary>
        public static GameObject BuildLayout(ImageLayoutScanResult result, ImageLayoutScanSetting setting, Transform parent, string nameRoot, bool isAddDemoReference)
        {
            var _lsMatch = result.matches;
            float _factor = GetFactorDemoToUI(result, setting);
            float _widthUI = result.widthDemo * _factor;
            float _heightUI = result.heightDemo * _factor;

            var _root = CreateUIObject(nameRoot, parent);
            var _rootRect = (RectTransform)_root.transform;
            _rootRect.sizeDelta = new Vector2(_widthUI, _heightUI);
            _rootRect.anchoredPosition = Vector2.zero;
            var _rootCenter = new Vector2(result.widthDemo * 0.5f, result.heightDemo * 0.5f);

            if (isAddDemoReference)
            {
                var _spriteDemo = GetSprite(result.pathDemo, setting.isAutoConvertToSprite);
                if (_spriteDemo != null)
                {
                    var _reference = CreateUIObject("_DemoReference (xoá khi xong)", _root.transform);
                    var _image = _reference.AddComponent<Image>();
                    _image.sprite = _spriteDemo;
                    _image.color = new Color(1f, 1f, 1f, 0.35f);
                    _image.raycastTarget = false;
                    var _rect = (RectTransform)_reference.transform;
                    _rect.sizeDelta = new Vector2(_widthUI, _heightUI);
                    _rect.anchoredPosition = Vector2.zero;
                }
            }

            var _createdObjects = new GameObject[_lsMatch.Count];
            var _countName = new Dictionary<string, int>();
            for (int i = 0; i < _lsMatch.Count; i++)
            {
                var _match = _lsMatch[i];
                if (!_match.isSelected)
                    continue;

                // Tìm cha gần nhất còn được chọn
                int _indexParent = _match.indexParent;
                while (_indexParent >= 0 && _createdObjects[_indexParent] == null)
                    _indexParent = _lsMatch[_indexParent].indexParent;
                Transform _transformParent = _indexParent >= 0 ? _createdObjects[_indexParent].transform : _root.transform;
                Vector2 _parentCenter = _indexParent >= 0 ? _lsMatch[_indexParent].Center : _rootCenter;

                string _name = _match.template.nameTemplate;
                _countName.TryGetValue(_name, out int _count);
                _countName[_name] = _count + 1;
                if (_count > 0)
                    _name += "_" + _count;

                var _go = CreateUIObject(_name, _transformParent);
                var _imageMatch = _go.AddComponent<Image>();
                _imageMatch.sprite = GetSprite(_match.template.pathAsset, setting.isAutoConvertToSprite);
                _imageMatch.raycastTarget = setting.isRaycastTarget;
                var _rectMatch = (RectTransform)_go.transform;
                // Build kích thước gốc: dùng đúng kích thước file ảnh (không méo dù mỗi ảnh có tỉ lệ hơi khác)
                _rectMatch.sizeDelta = setting.isBuildNativeSize
                    ? new Vector2(_match.template.Source.width, _match.template.Source.height)
                    : new Vector2(_match.rect.width, _match.rect.height);
                _rectMatch.anchoredPosition = (_match.Center - _parentCenter) * _factor;
                _createdObjects[i] = _go;
            }
            return _root;
        }

        public static GameObject CreateUIObject(string name, Transform parent)
        {
            var _go = new GameObject(name, typeof(RectTransform));
            _go.layer = LayerMask.NameToLayer("UI");
            var _rect = (RectTransform)_go.transform;
            _rect.SetParent(parent, false);
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.localScale = Vector3.one;
            return _go;
        }

        private static Transform _GetOrCreateParentInCanvas(Transform transformSelected, int widthDemo, int heightDemo)
        {
            if (transformSelected != null && transformSelected.GetComponentInParent<Canvas>() != null)
                return transformSelected;

            var _canvasGo = new GameObject("Canvas_Layout", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(_canvasGo, "Create Canvas Layout");
            _canvasGo.layer = LayerMask.NameToLayer("UI");
            var _canvas = _canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var _scaler = _canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(widthDemo, heightDemo);
            _scaler.matchWidthOrHeight = 0.5f;

            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var _eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(_eventSystem, "Create EventSystem");
            }
            return _canvasGo.transform;
        }

        /// <summary>Lấy Sprite của ảnh. Nếu ảnh chưa ở dạng Sprite và cho phép, tự đổi Texture Type sang Sprite.</summary>
        public static Sprite GetSprite(string pathAsset, bool isAutoConvert)
        {
            var _sprite = AssetDatabase.LoadAllAssetsAtPath(pathAsset).OfType<Sprite>().FirstOrDefault();
            if (_sprite != null || !isAutoConvert)
                return _sprite;

            var _importer = AssetImporter.GetAtPath(pathAsset) as TextureImporter;
            if (_importer == null)
                return null;
            _importer.textureType = TextureImporterType.Sprite;
            _importer.spriteImportMode = SpriteImportMode.Single;
            _importer.alphaIsTransparency = true;
            _importer.mipmapEnabled = false;
            _importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(pathAsset).OfType<Sprite>().FirstOrDefault();
        }

        #endregion
    }
}
