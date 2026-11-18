using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BaseProject.EditorTools.ImageLayoutScanner
{
    /// <summary>
    /// View: cửa sổ Editor để chọn ảnh demo + thư mục ảnh thành phần, quét, xem trước và sinh UI.
    /// Menu: Tools/Base Project/Image Layout Scanner
    /// </summary>
    public class ImageLayoutScannerWindow : EditorWindow
    {
        private const string _KEY_PREFS_SETTING = "BaseProject.ImageLayoutScanner.Setting";
        private const string _KEY_PREFS_DEMO = "BaseProject.ImageLayoutScanner.Demo";
        private const string _KEY_PREFS_FOLDER = "BaseProject.ImageLayoutScanner.Folder";
        private const string _KEY_PREFS_POPUP = "BaseProject.ImageLayoutScanner.Popup";
        private const float _WIDTH_PANEL_LEFT = 380f;

        private ImageLayoutScanSetting _setting = new ImageLayoutScanSetting();
        private Texture2D _textureDemo;
        private DefaultAsset _folderTemplate;
        private ImageLayoutScanResult _result;
        private int _indexHighlight = -1;
        private Vector2 _scrollLeft;
        private bool _isFoldoutSetting = true;
        private PopupBuildSetting _settingPopup = new PopupBuildSetting();
        private bool _isFoldoutPopup = true;

        [MenuItem("Tools/Base Project/Image Layout Scanner")]
        public static void OpenWindow()
        {
            var _window = GetWindow<ImageLayoutScannerWindow>("Image Layout Scanner");
            _window.minSize = new Vector2(820, 520);
        }

        private void OnEnable()
        {
            string _json = EditorPrefs.GetString(_KEY_PREFS_SETTING, string.Empty);
            if (!string.IsNullOrEmpty(_json))
                JsonUtility.FromJsonOverwrite(_json, _setting);
            string _jsonPopup = EditorPrefs.GetString(_KEY_PREFS_POPUP, string.Empty);
            if (!string.IsNullOrEmpty(_jsonPopup))
                JsonUtility.FromJsonOverwrite(_jsonPopup, _settingPopup);
            _textureDemo = AssetDatabase.LoadAssetAtPath<Texture2D>(EditorPrefs.GetString(_KEY_PREFS_DEMO, string.Empty));
            _folderTemplate = AssetDatabase.LoadAssetAtPath<DefaultAsset>(EditorPrefs.GetString(_KEY_PREFS_FOLDER, string.Empty));
        }

        private void OnDisable()
        {
            _SavePrefs();
        }

        private void _SavePrefs()
        {
            EditorPrefs.SetString(_KEY_PREFS_SETTING, JsonUtility.ToJson(_setting));
            EditorPrefs.SetString(_KEY_PREFS_POPUP, JsonUtility.ToJson(_settingPopup));
            EditorPrefs.SetString(_KEY_PREFS_DEMO, _textureDemo != null ? AssetDatabase.GetAssetPath(_textureDemo) : string.Empty);
            EditorPrefs.SetString(_KEY_PREFS_FOLDER, _folderTemplate != null ? AssetDatabase.GetAssetPath(_folderTemplate) : string.Empty);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(_WIDTH_PANEL_LEFT));
            _scrollLeft = EditorGUILayout.BeginScrollView(_scrollLeft);
            _DrawInput();
            _DrawScale();
            _DrawSetting();
            _DrawButtons();
            _DrawPopup();
            _DrawResultList();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            _DrawPreview();
            EditorGUILayout.EndHorizontal();
        }

        #region Panel trái

        private void _DrawInput()
        {
            EditorGUILayout.LabelField("Đầu vào", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _textureDemo = (Texture2D)EditorGUILayout.ObjectField("Ảnh demo", _textureDemo, typeof(Texture2D), false);
            _folderTemplate = (DefaultAsset)EditorGUILayout.ObjectField("Thư mục ảnh thành phần", _folderTemplate, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                _result = null;
                if (_textureDemo != null)
                    _settingPopup.namePopup = PopupBuilderController.ToPascalCase(PopupBuilderController.SplitNameToTokens(_textureDemo.name)) + "Box";
                _SavePrefs();
            }

            if (_folderTemplate != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_folderTemplate)))
                EditorGUILayout.HelpBox("Hãy kéo vào 1 THƯ MỤC chứa các ảnh thành phần.", MessageType.Warning);
            EditorGUILayout.Space(4);
        }

        private void _DrawScale()
        {
            EditorGUILayout.LabelField("Tỉ lệ thành phần / demo", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _setting.scaleMode = (ImageScaleMode)EditorGUILayout.EnumPopup(new GUIContent("Chế độ", "Fixed: 1 tỉ lệ cố định. AutoSearch: tự dò trong khoảng"), _setting.scaleMode);

            if (_setting.scaleMode == ImageScaleMode.Fixed)
            {
                _setting.scaleFixed = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Tỉ lệ", "Kích thước thành phần trong demo / kích thước file ảnh thành phần"), _setting.scaleFixed), 0.05f, 10f);
                EditorGUILayout.BeginHorizontal();
                _setting.widthDesignReference = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Rộng thiết kế gốc", "Ví dụ thiết kế 1080x1920 -> 1080"), _setting.widthDesignReference));
                GUI.enabled = _textureDemo != null;
                if (GUILayout.Button(new GUIContent("Tính", "Tỉ lệ = chiều rộng ảnh demo / chiều rộng thiết kế gốc"), GUILayout.Width(50)))
                {
                    int _widthDemo = _GetDemoWidthRaw();
                    if (_widthDemo > 0)
                        _setting.scaleFixed = (float)System.Math.Round((double)_widthDemo / _setting.widthDesignReference, 4);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox(_GetScaleDescription(_setting.scaleFixed), MessageType.None);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Khoảng dò");
                _setting.scaleMin = EditorGUILayout.FloatField(_setting.scaleMin, GUILayout.Width(50));
                EditorGUILayout.MinMaxSlider(ref _setting.scaleMin, ref _setting.scaleMax, 0.1f, 3f);
                _setting.scaleMax = EditorGUILayout.FloatField(_setting.scaleMax, GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
                _setting.scaleMin = Mathf.Clamp(_setting.scaleMin, 0.05f, 10f);
                _setting.scaleMax = Mathf.Clamp(_setting.scaleMax, _setting.scaleMin, 10f);
                _setting.scaleStep = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Bước dò", "Nhỏ hơn = chính xác hơn nhưng chậm hơn"), _setting.scaleStep), 0.005f, 1f);
                _setting.isLockScaleAfterFirstMatch = EditorGUILayout.Toggle(new GUIContent("Khoá tỉ lệ sau khi khớp", "Ảnh đầu tiên khớp chắc chắn -> các ảnh sau chỉ dò quanh tỉ lệ đó"), _setting.isLockScaleAfterFirstMatch);
                int _numScale = Mathf.FloorToInt((_setting.scaleMax - _setting.scaleMin) / _setting.scaleStep + 0.0001f) + 1;
                EditorGUILayout.HelpBox($"Dò {_numScale} mức tỉ lệ cho mỗi ảnh{(_setting.isLockScaleAfterFirstMatch ? " (đến khi khoá được tỉ lệ)" : "")}.", MessageType.None);
            }
            _setting.isBuildNativeSize = EditorGUILayout.Toggle(new GUIContent("Build theo kích thước gốc", "Bật: UI dùng đúng kích thước file ảnh thành phần, layout phóng theo tỉ lệ. Tắt: UI đúng kích thước trong ảnh demo."), _setting.isBuildNativeSize);
            if (EditorGUI.EndChangeCheck())
                _SavePrefs();
            EditorGUILayout.Space(4);
        }

        private static string _GetScaleDescription(float scale)
        {
            if (Mathf.Abs(scale - 1f) < 0.0001f)
                return "x1: thành phần trong demo đúng bằng kích thước file ảnh.";
            return scale < 1f
                ? $"x{scale:0.###}: demo nhỏ hơn thiết kế {1f / scale:0.##} lần (ảnh 100px hiện {100 * scale:0.#}px trong demo)."
                : $"x{scale:0.###}: demo lớn hơn thiết kế {scale:0.##} lần (ảnh 100px hiện {100 * scale:0.#}px trong demo).";
        }

        private int _GetDemoWidthRaw()
        {
            if (_result != null && _result.widthDemo > 0)
                return _result.widthDemo;
            ImageLayoutScanController.LoadPixels(AssetDatabase.GetAssetPath(_textureDemo), out int _width, out _);
            return _width;
        }

        private void _DrawSetting()
        {
            _isFoldoutSetting = EditorGUILayout.Foldout(_isFoldoutSetting, "Cấu hình", true);
            if (!_isFoldoutSetting)
                return;
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            _setting.thresholdMatch = EditorGUILayout.Slider(new GUIContent("Ngưỡng khớp", "Mặc định 0.92. Ra thừa (khớp nhầm) thì tăng, ra thiếu thì giảm"), _setting.thresholdMatch, 0.5f, 1f);
            _setting.maxSamplePerTemplate = EditorGUILayout.IntSlider(new GUIContent("Số điểm mẫu / ảnh", "Càng nhiều càng chính xác nhưng chậm hơn"), _setting.maxSamplePerTemplate, 32, 2048);
            _setting.maxMatchPerTemplate = EditorGUILayout.IntSlider(new GUIContent("Tối đa vị trí / ảnh"), _setting.maxMatchPerTemplate, 1, 200);
            _setting.alphaOpaque = EditorGUILayout.Slider(new GUIContent("Alpha tối thiểu", "Bỏ qua viền mờ / trong suốt của ảnh thành phần"), _setting.alphaOpaque, 0.1f, 1f);
            _setting.iouOverlapSameTemplate = EditorGUILayout.Slider(new GUIContent("Cho phép chồng (cùng ảnh)"), _setting.iouOverlapSameTemplate, 0f, 0.9f);
            _setting.ratioAreaLargeTemplate = EditorGUILayout.Slider(new GUIContent("Ảnh lớn khi chiếm ≥", "Ảnh nền / khung: tỉ lệ diện tích so với ảnh demo"), _setting.ratioAreaLargeTemplate, 0.05f, 1f);
            _setting.occlusionAllowLargeTemplate = EditorGUILayout.Slider(new GUIContent("Ảnh lớn bị che tối đa", "Ảnh lớn không khớp được sẽ thử lại, bỏ qua tỉ lệ pixel bị che này"), _setting.occlusionAllowLargeTemplate, 0f, 0.9f);
            _setting.isUseGpu = EditorGUILayout.Toggle(new GUIContent("Dùng GPU (compute shader)"), _setting.isUseGpu);
            _setting.isIncludeSubFolder = EditorGUILayout.Toggle("Quét cả thư mục con", _setting.isIncludeSubFolder);
            _setting.isNestChildInParent = EditorGUILayout.Toggle(new GUIContent("Lồng con vào cha", "Ảnh nằm trọn trong ảnh khác sẽ thành object con"), _setting.isNestChildInParent);
            _setting.isAutoConvertToSprite = EditorGUILayout.Toggle(new GUIContent("Tự đổi ảnh sang Sprite", "Đổi Texture Type sang Sprite (2D and UI) nếu cần"), _setting.isAutoConvertToSprite);
            _setting.isAddDemoAsReference = EditorGUILayout.Toggle(new GUIContent("Thêm ảnh demo làm nền mờ", "Để so sánh, xoá khi xong"), _setting.isAddDemoAsReference);
            _setting.isRaycastTarget = EditorGUILayout.Toggle("Raycast Target", _setting.isRaycastTarget);
            if (EditorGUI.EndChangeCheck())
                _SavePrefs();
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        private void _DrawButtons()
        {
            bool _isReady = _textureDemo != null && _folderTemplate != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_folderTemplate));
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _isReady;
            if (GUILayout.Button("Scan", GUILayout.Height(28)))
                _RunScan();
            GUI.enabled = _isReady && _result != null && _result.matches.Count > 0;
            if (GUILayout.Button("Build UI", GUILayout.Height(28)))
                _RunBuild();
            GUI.enabled = _result != null;
            if (GUILayout.Button("Clear", GUILayout.Height(28), GUILayout.Width(60)))
            {
                _result = null;
                _indexHighlight = -1;
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (Selection.activeTransform != null && Selection.activeTransform.GetComponentInParent<Canvas>() != null)
                EditorGUILayout.HelpBox($"UI sẽ được tạo dưới: {Selection.activeTransform.name}", MessageType.None);
            else
                EditorGUILayout.HelpBox("Chưa chọn object trong Canvas, sẽ tạo Canvas mới (Reference Resolution = kích thước ảnh demo).", MessageType.None);
        }

        private void _DrawPopup()
        {
            EditorGUILayout.Space(4);
            _isFoldoutPopup = EditorGUILayout.Foldout(_isFoldoutPopup, "Tạo Popup (BaseBox) từ kết quả quét", true, EditorStyles.foldoutHeader);
            if (_isFoldoutPopup)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.indentLevel++;
                _settingPopup.namePopup = EditorGUILayout.TextField(new GUIContent("Tên popup / class", "Ví dụ: DailyRewardBox"), _settingPopup.namePopup);
                _settingPopup.folderScript = _DrawFolderField("Thư mục script", _settingPopup.folderScript);
                _settingPopup.folderPrefab = _DrawFolderField("Thư mục prefab (Resources)", _settingPopup.folderPrefab);
                _settingPopup.matchWidthOrHeight = EditorGUILayout.Slider(new GUIContent("Match W/H", "CanvasScaler: 0 = theo rộng, 1 = theo cao"), _settingPopup.matchWidthOrHeight, 0f, 1f);

                _settingPopup.isAddDim = EditorGUILayout.Toggle("Nền tối (Dim)", _settingPopup.isAddDim);
                if (_settingPopup.isAddDim)
                {
                    EditorGUI.indentLevel++;
                    _settingPopup.colorDim = EditorGUILayout.ColorField("Màu", _settingPopup.colorDim);
                    _settingPopup.isCloseOnClickDim = EditorGUILayout.Toggle("Bấm ra ngoài để đóng", _settingPopup.isCloseOnClickDim);
                    EditorGUI.indentLevel--;
                }

                _settingPopup.isDetectButton = EditorGUILayout.Toggle(new GUIContent("Nhận diện nút theo tên", "Tên ảnh thành phần chứa từ khoá => thêm Button"), _settingPopup.isDetectButton);
                if (_settingPopup.isDetectButton)
                {
                    EditorGUI.indentLevel++;
                    _settingPopup.keywordsButton = EditorGUILayout.TextField(new GUIContent("Từ khoá nút", "Cách nhau bởi dấu phẩy. Ví dụ btn_play, BtnShop, button-ok"), _settingPopup.keywordsButton);
                    _settingPopup.keywordsClose = EditorGUILayout.TextField(new GUIContent("Từ khoá nút đóng", "Nút chứa từ này được gắn sẵn Close()"), _settingPopup.keywordsClose);
                    _settingPopup.isAddClickSound = EditorGUILayout.Toggle("Thêm âm thanh click", _settingPopup.isAddClickSound);
                    EditorGUI.indentLevel--;
                }

                _settingPopup.animType = (BoxAnimType)EditorGUILayout.EnumPopup("Hiệu ứng mở / đóng", _settingPopup.animType);
                _settingPopup.isPlaySoundOpenClose = EditorGUILayout.Toggle("Âm thanh mở / đóng", _settingPopup.isPlaySoundOpenClose);
                _settingPopup.isCloseByBackKey = EditorGUILayout.Toggle("Đóng bằng phím Back", _settingPopup.isCloseByBackKey);
                _settingPopup.isGenerateScript = EditorGUILayout.Toggle(new GUIContent("Sinh script", "Tạo {Tên}.cs kế thừa BaseBox, có Setup() + field nút + hàm xử lý rỗng"), _settingPopup.isGenerateScript);
                _settingPopup.isOpenPrefabAfterBuild = EditorGUILayout.Toggle("Mở prefab sau khi tạo", _settingPopup.isOpenPrefabAfterBuild);
                EditorGUI.indentLevel--;
                if (EditorGUI.EndChangeCheck())
                    _SavePrefs();

                string _error = _result != null ? PopupBuilderController.Validate(_result, _settingPopup) : "Hãy Scan trước.";
                if (!string.IsNullOrEmpty(_error))
                    EditorGUILayout.HelpBox(_error, MessageType.Info);
                GUI.enabled = string.IsNullOrEmpty(_error);
                if (GUILayout.Button("Build Popup", GUILayout.Height(28)))
                    _RunBuildPopup();
                GUI.enabled = true;
            }
            EditorGUILayout.Space(4);
        }

        private static string _DrawFolderField(string label, string pathFolder)
        {
            EditorGUILayout.BeginHorizontal();
            pathFolder = EditorGUILayout.TextField(label, pathFolder);
            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                string _selected = EditorUtility.OpenFolderPanel(label, string.IsNullOrEmpty(pathFolder) ? "Assets" : pathFolder, string.Empty);
                string _root = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/') + "/";
                _selected = _selected.Replace('\\', '/');
                if (!string.IsNullOrEmpty(_selected) && _selected.StartsWith(_root))
                {
                    pathFolder = _selected.Substring(_root.Length);
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();
            return pathFolder;
        }

        private void _RunBuildPopup()
        {
            _SavePrefs();
            // Chạy sau OnGUI để tránh lỗi layout khi hiện hộp thoại / biên dịch
            EditorApplication.delayCall += () => PopupBuilderController.Build(_result, _setting, _settingPopup);
        }

        private void _DrawResultList()
        {
            if (_result == null)
                return;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Kết quả", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Ảnh demo: {_result.widthDemo} x {_result.heightDemo}  |  {_result.numTemplateScanned} ảnh thành phần");
            EditorGUILayout.LabelField($"Tìm thấy: {_result.matches.Count} vị trí  |  {_result.timeScanSeconds:0.00}s  |  {(_result.isUsedGpu ? "GPU" : "CPU")}");
            if (_result.matches.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Tỉ lệ hiện tại (trung vị): x{_result.scaleDominant:0.###}", EditorStyles.boldLabel);
                if (GUILayout.Button(new GUIContent("Dùng làm tỉ lệ cố định", "Lần quét sau chỉ dùng tỉ lệ này (nhanh hơn)"), GUILayout.Width(150)))
                {
                    _setting.scaleMode = ImageScaleMode.Fixed;
                    _setting.scaleFixed = (float)System.Math.Round(_result.scaleDominant, 4);
                    _SavePrefs();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(_GetScaleDescription(_result.scaleDominant), EditorStyles.wordWrappedMiniLabel);
            }
            foreach (var _warning in _result.warnings)
                EditorGUILayout.HelpBox(_warning, MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Chọn tất cả")) _SetSelectAll(true);
            if (GUILayout.Button("Bỏ chọn tất cả")) _SetSelectAll(false);
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < _result.matches.Count; i++)
            {
                var _match = _result.matches[i];
                var _style = i == _indexHighlight ? EditorStyles.helpBox : GUIStyle.none;
                EditorGUILayout.BeginHorizontal(_style);
                GUILayout.Space(_match.depth * 12);
                _match.isSelected = EditorGUILayout.Toggle(_match.isSelected, GUILayout.Width(16));
                if (GUILayout.Button($"{_match.template.nameTemplate}", EditorStyles.label))
                {
                    _indexHighlight = i;
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(_match.template.pathAsset));
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label(new GUIContent($"x{_match.Scale:0.###}", $"Tỉ lệ thành phần / demo. Trong demo: {_match.rect.width}x{_match.rect.height}px, file gốc: {_match.template.Source.width}x{_match.template.Source.height}px"), GUILayout.Width(52));
                GUILayout.Label(new GUIContent($"{_match.score:0.000}", "Điểm khớp"), GUILayout.Width(42));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void _SetSelectAll(bool isSelect)
        {
            foreach (var _match in _result.matches)
                _match.isSelected = isSelect;
        }

        #endregion

        #region Preview

        private void _DrawPreview()
        {
            Rect _area = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(_area, new Color(0.15f, 0.15f, 0.15f));
            if (_textureDemo == null)
            {
                GUI.Label(_area, "Kéo ảnh demo vào để xem trước", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            int _widthDemo = _result != null ? _result.widthDemo : _textureDemo.width;
            int _heightDemo = _result != null ? _result.heightDemo : _textureDemo.height;
            float _scale = Mathf.Min((_area.width - 10) / _widthDemo, (_area.height - 10) / _heightDemo);
            var _rectImage = new Rect(0, 0, _widthDemo * _scale, _heightDemo * _scale);
            _rectImage.center = _area.center;
            GUI.DrawTexture(_rectImage, _textureDemo, ScaleMode.StretchToFill);

            if (_result == null)
                return;

            for (int i = 0; i < _result.matches.Count; i++)
            {
                var _match = _result.matches[i];
                // Đổi gốc dưới-trái (pixel) sang gốc trên-trái (GUI)
                var _rectGui = new Rect(
                    _rectImage.x + _match.rect.x * _scale,
                    _rectImage.y + (_heightDemo - _match.rect.y - _match.rect.height) * _scale,
                    _match.rect.width * _scale,
                    _match.rect.height * _scale);

                Color _color = i == _indexHighlight ? Color.yellow
                    : !_match.isSelected ? new Color(0.5f, 0.5f, 0.5f, 0.6f)
                    : Color.HSVToRGB((_match.template.nameTemplate.GetHashCode() & 0xFFFF) / 65535f, 0.8f, 1f);
                _DrawOutline(_rectGui, _color, i == _indexHighlight ? 3 : 1.5f);
                if (i == _indexHighlight)
                    _DrawLabelScale(_rectGui, _match);

                if (Event.current.type == EventType.MouseDown && _rectGui.Contains(Event.current.mousePosition))
                {
                    // Click chọn ảnh nhỏ nhất (vẽ sau) tại vị trí chuột
                    _indexHighlight = i;
                }
            }
            if (Event.current.type == EventType.MouseDown && _area.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                Repaint();
            }
        }

        /// <summary>Hiện tên + tỉ lệ + kích thước của thành phần đang chọn ngay trên khung xem trước.</summary>
        private static void _DrawLabelScale(Rect rectGui, ImageMatchData match)
        {
            string _text = $"{match.template.nameTemplate}  x{match.Scale:0.###}  ({match.rect.width}x{match.rect.height} / gốc {match.template.Source.width}x{match.template.Source.height})";
            var _content = new GUIContent(_text);
            Vector2 _size = EditorStyles.whiteMiniLabel.CalcSize(_content);
            var _rectLabel = new Rect(rectGui.x, rectGui.y - _size.y - 2, _size.x + 6, _size.y + 2);
            if (_rectLabel.y < 0)
                _rectLabel.y = rectGui.yMax + 2;
            EditorGUI.DrawRect(_rectLabel, new Color(0f, 0f, 0f, 0.75f));
            GUI.Label(new Rect(_rectLabel.x + 3, _rectLabel.y + 1, _size.x, _size.y), _content, EditorStyles.whiteMiniLabel);
        }

        private static void _DrawOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        #endregion

        #region Actions

        private void _RunScan()
        {
            string _pathDemo = AssetDatabase.GetAssetPath(_textureDemo);
            string _pathFolder = AssetDatabase.GetAssetPath(_folderTemplate);
            List<string> _lsPathTemplate = ImageLayoutScanController.FindTemplatePaths(_pathFolder, _setting.isIncludeSubFolder, _pathDemo);
            if (_lsPathTemplate.Count == 0)
            {
                EditorUtility.DisplayDialog("Image Layout Scanner", "Không tìm thấy ảnh thành phần nào trong thư mục.", "OK");
                return;
            }

            try
            {
                _result = ImageLayoutScanController.Scan(_pathDemo, _lsPathTemplate, _setting,
                    (message, progress) => EditorUtility.DisplayCancelableProgressBar("Image Layout Scanner", message, progress));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            _indexHighlight = -1;
            Debug.Log($"[ImageLayoutScanner] {Path.GetFileName(_pathDemo)}: tìm thấy {_result.matches.Count} vị trí trong {_result.timeScanSeconds:0.00}s ({(_result.isUsedGpu ? "GPU" : "CPU")}).");
            Repaint();
        }

        private void _RunBuild()
        {
            var _root = ImageLayoutScanController.BuildUI(_result, _setting, Selection.activeTransform);
            Debug.Log($"[ImageLayoutScanner] Đã tạo '{_root.name}'.", _root);
        }

        #endregion
    }
}
