using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BaseProject.EditorTools.ImageLayoutScanner
{
    /// <summary>
    /// Controller: từ kết quả quét ảnh demo tạo 1 Popup hoàn chỉnh theo chuẩn BaseBox:
    ///   {Name} (Canvas + CanvasScaler + GraphicRaycaster + CanvasGroup + {Name} : BaseBox)
    ///     ├─ Dim        (nền tối, bấm ra ngoài để đóng - tuỳ chọn)
    ///     └─ MainPanel  (toàn bộ ảnh thành phần, nút được nhận diện theo tên)
    /// + sinh script {Name}.cs (Setup() qua BoxFactory, field Button, hàm xử lý rỗng)
    /// + lưu prefab vào thư mục Resources và tự gắn script / field sau khi biên dịch.
    /// </summary>
    [InitializeOnLoad]
    public static class PopupBuilderController
    {
        public const string NAME_MAIN_PANEL = "MainPanel";
        public const string NAME_DIM = "Dim";
        private const string _NAME_DEMO_REFERENCE_PREFIX = "_DemoReference";
        private const string _KEY_PENDING = "BaseProject.PopupBuilder.Pending";
        private const string _FOLDER_RESOURCES = "/Resources/";

        static PopupBuilderController()
        {
            EditorApplication.delayCall += ProcessPendingWire;
        }

        #region Build

        /// <summary>Tạo popup. Trả về đường dẫn prefab, null nếu huỷ / lỗi.</summary>
        public static string Build(ImageLayoutScanResult result, ImageLayoutScanSetting settingScan, PopupBuildSetting setting)
        {
            string _error = Validate(result, setting);
            if (!string.IsNullOrEmpty(_error))
            {
                EditorUtility.DisplayDialog("Build Popup", _error, "OK");
                return null;
            }

            string _nameClass = setting.namePopup.Trim();
            string _pathPrefab = $"{setting.folderPrefab.TrimEnd('/')}/{_nameClass}.prefab";
            string _pathScript = $"{setting.folderScript.TrimEnd('/')}/{_nameClass}.cs";

            if (File.Exists(_pathPrefab) && !EditorUtility.DisplayDialog("Build Popup", $"Prefab đã tồn tại:\n{_pathPrefab}\n\nGhi đè?", "Ghi đè", "Huỷ"))
                return null;

            bool _isWriteScript = setting.isGenerateScript;
            if (_isWriteScript && File.Exists(_pathScript))
            {
                int _choice = EditorUtility.DisplayDialogComplex("Build Popup",
                    $"Script đã tồn tại:\n{_pathScript}\n\nGhi đè sẽ mất code đã viết trong đó.",
                    "Giữ script cũ", "Huỷ", "Ghi đè");
                if (_choice == 1)
                    return null;
                _isWriteScript = _choice == 2;
            }

            // 1) Dựng cây object
            Vector2 _sizeUI = ImageLayoutScanController.GetSizeUI(result, settingScan);
            var _root = _CreatePopupRoot(_nameClass, _sizeUI, setting);
            try
            {
                if (setting.isAddDim)
                    _CreateDim(_root.transform, setting);

                var _mainPanel = ImageLayoutScanController.BuildLayout(result, settingScan, _root.transform, NAME_MAIN_PANEL, settingScan.isAddDemoAsReference);
                _HideDemoReference(_mainPanel.transform);

                var _lsButton = setting.isDetectButton ? _CreateButtons(_mainPanel.transform, _root.transform, setting) : new List<PopupButtonData>();

                // Bấm vào khung popup không được xuyên xuống Dim
                if (setting.isAddDim && setting.isCloseOnClickDim)
                    _BlockRaycastTopLevel(_mainPanel.transform);

                // 2) Lưu prefab
                _CreateFolder(setting.folderPrefab);
                PrefabUtility.SaveAsPrefabAsset(_root, _pathPrefab, out bool _isSuccess);
                if (!_isSuccess)
                {
                    Debug.LogError("[PopupBuilder] Không lưu được prefab: " + _pathPrefab);
                    return null;
                }

                // 3) Script + gắn script
                var _pending = new PopupPendingWire
                {
                    pathPrefab = _pathPrefab,
                    nameClass = _nameClass,
                    animType = setting.animType,
                    isPlaySoundOpenClose = setting.isPlaySoundOpenClose,
                    isCloseOnClickDim = setting.isAddDim && setting.isCloseOnClickDim,
                    isCloseByBackKey = setting.isCloseByBackKey,
                    isOpenPrefabAfterBuild = setting.isOpenPrefabAfterBuild,
                    buttons = _lsButton
                };

                if (!setting.isGenerateScript)
                {
                    _FinishPrefab(_pathPrefab, setting.isOpenPrefabAfterBuild);
                    Debug.Log($"[PopupBuilder] Đã tạo prefab (không sinh script): {_pathPrefab}");
                    return _pathPrefab;
                }

                bool _isScriptChanged = false;
                if (_isWriteScript)
                {
                    string _code = GenerateScript(_nameClass, GetResourcePath(_pathPrefab), Path.GetFileName(result.pathDemo), _lsButton);
                    _isScriptChanged = _WriteIfChanged(_pathScript, _code);
                }

                SessionState.SetString(_KEY_PENDING, JsonUtility.ToJson(_pending));
                if (_isScriptChanged)
                {
                    // Unity biên dịch xong -> domain reload -> static ctor -> ProcessPendingWire
                    AssetDatabase.ImportAsset(_pathScript, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"[PopupBuilder] Đã tạo {_pathPrefab} + {_pathScript}. Chờ Unity biên dịch để gắn script...");
                }
                else
                {
                    ProcessPendingWire();
                }
                return _pathPrefab;
            }
            finally
            {
                Object.DestroyImmediate(_root);
            }
        }

        /// <summary>Kiểm tra đầu vào. Trả về thông báo lỗi, rỗng nếu hợp lệ.</summary>
        public static string Validate(ImageLayoutScanResult result, PopupBuildSetting setting)
        {
            if (result == null || result.matches.Count(m => m.isSelected) == 0)
                return "Chưa có kết quả quét (hoặc chưa chọn ảnh nào).";
            string _name = setting.namePopup?.Trim();
            if (string.IsNullOrEmpty(_name) || !Regex.IsMatch(_name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                return "Tên popup phải là tên class C# hợp lệ (chữ, số, _; không bắt đầu bằng số).";
            if (!setting.folderScript.StartsWith("Assets"))
                return "Thư mục script phải nằm trong Assets.";
            if (!setting.folderPrefab.StartsWith("Assets") || GetResourcePath(setting.folderPrefab.TrimEnd('/') + "/x.prefab") == null)
                return "Thư mục prefab phải nằm trong 1 thư mục 'Resources' (để BoxFactory load được).";
            if (setting.isGenerateScript && _IsTypeNameTakenByOther(_name, $"{setting.folderScript.TrimEnd('/')}/{_name}.cs"))
                return $"Đã có class '{_name}' ở file khác. Hãy đặt tên khác.";
            return string.Empty;
        }

        /// <summary>"Assets/Resources/UI/Popups/XBox.prefab" -> "UI/Popups/XBox". Null nếu không nằm trong Resources.</summary>
        public static string GetResourcePath(string pathAsset)
        {
            string _path = pathAsset.Replace('\\', '/');
            int _index = _path.LastIndexOf(_FOLDER_RESOURCES, StringComparison.Ordinal);
            if (_index < 0)
                return null;
            string _relative = _path.Substring(_index + _FOLDER_RESOURCES.Length);
            string _extension = Path.GetExtension(_relative);
            return string.IsNullOrEmpty(_extension) ? _relative : _relative.Substring(0, _relative.Length - _extension.Length);
        }

        private static GameObject _CreatePopupRoot(string name, Vector2 sizeUI, PopupBuildSetting setting)
        {
            var _root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            _root.layer = LayerMask.NameToLayer("UI");

            var _canvas = _root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera; // BaseBox gán camera khi mở
            _canvas.planeDistance = 5;
            if (SortingLayer.layers.Any(l => l.name == "Popup"))
                _canvas.sortingLayerName = "Popup";

            var _scaler = _root.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(Mathf.Round(sizeUI.x), Mathf.Round(sizeUI.y));
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = setting.matchWidthOrHeight;
            return _root;
        }

        private static void _CreateDim(Transform root, PopupBuildSetting setting)
        {
            var _dim = ImageLayoutScanController.CreateUIObject(NAME_DIM, root);
            var _rect = (RectTransform)_dim.transform;
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.one;
            _rect.sizeDelta = Vector2.zero;
            _rect.anchoredPosition = Vector2.zero;

            var _image = _dim.AddComponent<Image>();
            _image.color = setting.colorDim;
            _image.raycastTarget = true; // Chặn bấm xuyên xuống UI phía sau

            if (setting.isCloseOnClickDim)
            {
                var _button = _dim.AddComponent<Button>();
                _button.transition = Selectable.Transition.None;
                _button.targetGraphic = _image;
            }
        }

        private static void _HideDemoReference(Transform mainPanel)
        {
            foreach (Transform _child in mainPanel)
            {
                if (!_child.name.StartsWith(_NAME_DEMO_REFERENCE_PREFIX))
                    continue;
                _child.SetAsFirstSibling();
                _child.gameObject.SetActive(false); // Bật lên khi cần căn chỉnh, xoá trước khi release
            }
        }

        /// <summary>Nhận diện nút theo tên ảnh, thêm Button (+ âm thanh click). Trả về danh sách để sinh field.</summary>
        private static List<PopupButtonData> _CreateButtons(Transform mainPanel, Transform root, PopupBuildSetting setting)
        {
            var _keywordsButton = _ParseKeywords(setting.keywordsButton);
            var _keywordsClose = _ParseKeywords(setting.keywordsClose);
            var _lsButton = new List<PopupButtonData>();
            var _usedField = new HashSet<string>();
            bool _isHasClose = false;

            foreach (var _image in mainPanel.GetComponentsInChildren<Image>(true))
            {
                if (_image.name.StartsWith(_NAME_DEMO_REFERENCE_PREFIX))
                    continue;
                var _tokens = SplitNameToTokens(_image.name);
                bool _isClose = _tokens.Any(_keywordsClose.Contains);
                bool _isButton = _isClose || _tokens.Any(_keywordsButton.Contains);
                if (!_isButton)
                    continue;

                _image.raycastTarget = true;
                var _button = _image.gameObject.AddComponent<Button>();
                _button.targetGraphic = _image;
                if (setting.isAddClickSound)
                    _image.gameObject.AddComponent<UIButtonSound>();

                // Tên field: bỏ các từ khoá nút, ghép PascalCase. "btn_play_1" -> _btnPlay1
                var _tokensName = _tokens.Where(t => !_keywordsButton.Contains(t)).ToList();
                if (_isClose && !_isHasClose)
                    _tokensName = new List<string> { "close" };
                string _namePart = ToPascalCase(_tokensName);
                if (string.IsNullOrEmpty(_namePart))
                    _namePart = "Button";
                string _field = "_btn" + _namePart;
                for (int i = 2; _usedField.Contains(_field); i++)
                    _field = "_btn" + _namePart + i;
                _usedField.Add(_field);

                _lsButton.Add(new PopupButtonData
                {
                    fieldName = _field,
                    pathInPrefab = AnimationUtility.CalculateTransformPath(_image.transform, root),
                    isClose = _isClose,
                    nameMethod = _isClose ? string.Empty : "_OnClick" + _field.Substring(4)
                });
                _isHasClose |= _isClose;
            }
            return _lsButton;
        }

        private static void _BlockRaycastTopLevel(Transform mainPanel)
        {
            foreach (Transform _child in mainPanel)
            {
                var _image = _child.GetComponent<Image>();
                if (_image != null && _child.gameObject.activeSelf)
                    _image.raycastTarget = true;
            }
        }

        #endregion

        #region Script

        public static string GenerateScript(string nameClass, string pathResource, string nameDemo, List<PopupButtonData> lsButton)
        {
            var _code = new StringBuilder();
            _code.AppendLine("using UnityEngine;");
            _code.AppendLine("using UnityEngine.UI;");
            _code.AppendLine();
            _code.AppendLine("/// <summary>");
            _code.AppendLine($"/// Popup tạo từ ảnh demo '{nameDemo}' (Tools > Base Project > Image Layout Scanner > Build Popup).");
            _code.AppendLine($"/// Mở: {nameClass}.Setup().Show();");
            _code.AppendLine("/// </summary>");
            _code.AppendLine($"public class {nameClass} : BaseBox");
            _code.AppendLine("{");
            _code.AppendLine($"    public const string PATH_RESOURCE = \"{pathResource}\";");
            _code.AppendLine();
            _code.AppendLine($"    public static {nameClass} Setup()");
            _code.AppendLine("    {");
            _code.AppendLine($"        return BoxFactory.Get<{nameClass}>(PATH_RESOURCE);");
            _code.AppendLine("    }");

            if (lsButton.Count > 0)
            {
                _code.AppendLine();
                _code.AppendLine("    [Header(\"========= BUTTON ===========\")]");
                foreach (var _button in lsButton)
                    _code.AppendLine($"    [SerializeField] private Button {_button.fieldName};");
            }

            _code.AppendLine();
            _code.AppendLine("    protected override void OnAwake()");
            _code.AppendLine("    {");
            _code.AppendLine("        base.OnAwake();");
            foreach (var _button in lsButton)
            {
                string _action = _button.isClose ? "Close" : _button.nameMethod;
                _code.AppendLine($"        if ({_button.fieldName} != null)");
                _code.AppendLine($"            {_button.fieldName}.onClick.AddListener({_action});");
            }
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    protected override void OnStart()");
            _code.AppendLine("    {");
            _code.AppendLine("        base.OnStart();");
            _code.AppendLine("        // Đổ dữ liệu mỗi lần popup mở");
            _code.AppendLine("    }");

            foreach (var _button in lsButton.Where(b => !b.isClose))
            {
                _code.AppendLine();
                _code.AppendLine($"    private void {_button.nameMethod}()");
                _code.AppendLine("    {");
                _code.AppendLine($"        // TODO: xử lý khi bấm {_button.pathInPrefab}");
                _code.AppendLine("    }");
            }
            _code.AppendLine("}");
            // Chuẩn dự án: CRLF
            return _code.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        private static bool _WriteIfChanged(string pathScript, string code)
        {
            if (File.Exists(pathScript) && File.ReadAllText(pathScript) == code)
                return false;
            _CreateFolder(Path.GetDirectoryName(pathScript)?.Replace('\\', '/'));
            File.WriteAllText(pathScript, code, new UTF8Encoding(true));
            return true;
        }

        private static bool _IsTypeNameTakenByOther(string nameClass, string pathScript)
        {
            var _type = _FindType(nameClass);
            if (_type == null)
                return false;
            var _script = AssetDatabase.FindAssets(nameClass + " t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => AssetDatabase.LoadAssetAtPath<MonoScript>(p)?.GetClass() == _type);
            return _script != null && _script.Replace('\\', '/') != pathScript.Replace('\\', '/');
        }

        #endregion

        #region Gắn script sau khi biên dịch

        /// <summary>Gắn component + field vào prefab nếu có việc đang chờ và class đã biên dịch xong.</summary>
        public static void ProcessPendingWire()
        {
            string _json = SessionState.GetString(_KEY_PENDING, string.Empty);
            if (string.IsNullOrEmpty(_json))
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ProcessPendingWire;
                return;
            }

            var _pending = JsonUtility.FromJson<PopupPendingWire>(_json);
            var _type = _FindType(_pending.nameClass);
            if (_type == null)
            {
                // Lỗi biên dịch -> giữ lại, sửa xong Unity reload sẽ tự chạy tiếp
                Debug.LogWarning($"[PopupBuilder] Chưa tìm thấy class '{_pending.nameClass}' (đang có lỗi biên dịch?). Sửa lỗi xong sẽ tự gắn vào {_pending.pathPrefab}.");
                return;
            }
            SessionState.EraseString(_KEY_PENDING);

            if (!typeof(BaseBox).IsAssignableFrom(_type))
            {
                Debug.LogError($"[PopupBuilder] '{_pending.nameClass}' không kế thừa BaseBox.");
                return;
            }
            _WirePrefab(_pending, _type);
            _FinishPrefab(_pending.pathPrefab, _pending.isOpenPrefabAfterBuild);
        }

        private static void _WirePrefab(PopupPendingWire pending, Type type)
        {
            var _root = PrefabUtility.LoadPrefabContents(pending.pathPrefab);
            try
            {
                var _component = _root.GetComponent(type) ?? _root.AddComponent(type);
                var _serialized = new SerializedObject(_component);

                _SetObject(_serialized, "mainPanel", _root.transform.Find(NAME_MAIN_PANEL));
                _SetObject(_serialized, "canvasGruop", _root.GetComponent<CanvasGroup>());
                var _dim = _root.transform.Find(NAME_DIM);
                _SetObject(_serialized, "buttonDim", _dim != null ? _dim.GetComponent<Button>() : null);
                _SetBool(_serialized, "isPopup", true);
                _SetBool(_serialized, "isAnim", pending.animType != BoxAnimType.None);
                _SetBool(_serialized, "isCloseOnClickDim", pending.isCloseOnClickDim);
                _SetBool(_serialized, "isPlaySoundOpenClose", pending.isPlaySoundOpenClose);
                _SetBool(_serialized, "isCloseByBackKey", pending.isCloseByBackKey);
                var _propertyAnim = _serialized.FindProperty("animType");
                if (_propertyAnim != null)
                    _propertyAnim.intValue = (int)pending.animType;

                int _countWired = 0;
                foreach (var _button in pending.buttons)
                {
                    var _transform = _root.transform.Find(_button.pathInPrefab);
                    var _property = _serialized.FindProperty(_button.fieldName);
                    if (_transform == null || _property == null)
                        continue;
                    _property.objectReferenceValue = _transform.GetComponent<Button>();
                    _countWired++;
                }
                _serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(_root, pending.pathPrefab);
                Debug.Log($"[PopupBuilder] Đã gắn {pending.nameClass} vào {pending.pathPrefab} ({_countWired}/{pending.buttons.Count} nút). Mở: {pending.nameClass}.Setup().Show();");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(_root);
            }
        }

        private static void _FinishPrefab(string pathPrefab, bool isOpen)
        {
            var _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pathPrefab);
            if (_prefab == null)
                return;
            EditorGUIUtility.PingObject(_prefab);
            if (isOpen)
                AssetDatabase.OpenAsset(_prefab);
        }

        private static void _SetObject(SerializedObject serialized, string nameProperty, Object value)
        {
            var _property = serialized.FindProperty(nameProperty);
            if (_property != null)
                _property.objectReferenceValue = value;
        }

        private static void _SetBool(SerializedObject serialized, string nameProperty, bool value)
        {
            var _property = serialized.FindProperty(nameProperty);
            if (_property != null)
                _property.boolValue = value;
        }

        #endregion

        #region Helper

        /// <summary>"BtnPlay_1", "btn-play now" -> [btn, play, 1] / [btn, play, now] (chữ thường).</summary>
        public static List<string> SplitNameToTokens(string name)
        {
            string _spaced = Regex.Replace(name ?? string.Empty, "([a-z0-9])([A-Z])", "$1 $2");
            return Regex.Split(_spaced.ToLowerInvariant(), "[^a-z0-9]+").Where(t => t.Length > 0).ToList();
        }

        public static string ToPascalCase(IEnumerable<string> tokens)
        {
            var _builder = new StringBuilder();
            foreach (var _token in tokens)
                _builder.Append(char.ToUpperInvariant(_token[0])).Append(_token.Substring(1));
            return _builder.ToString();
        }

        private static HashSet<string> _ParseKeywords(string text)
        {
            return new HashSet<string>((text ?? string.Empty).Split(',', ';', ' ')
                .Select(k => k.Trim().ToLowerInvariant()).Where(k => k.Length > 0));
        }

        private static Type _FindType(string nameClass)
        {
            foreach (var _assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var _type = _assembly.GetType(nameClass, false);
                if (_type != null && typeof(MonoBehaviour).IsAssignableFrom(_type))
                    return _type;
            }
            return null;
        }

        private static void _CreateFolder(string pathFolder)
        {
            if (string.IsNullOrEmpty(pathFolder) || AssetDatabase.IsValidFolder(pathFolder))
                return;
            string _parent = Path.GetDirectoryName(pathFolder)?.Replace('\\', '/');
            _CreateFolder(_parent);
            AssetDatabase.CreateFolder(_parent, Path.GetFileName(pathFolder));
        }

        #endregion
    }
}
