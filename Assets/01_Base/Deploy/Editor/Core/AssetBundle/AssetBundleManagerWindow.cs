#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BaseProject.EditorTools.AssetBundles
{
    /// <summary>
    /// AssetBundle > Manager...
    ///  - Danh sách bundle: số asset, dung lượng lần build gần nhất, mở rộng xem asset (ping / bỏ khỏi bundle).
    ///  - Đổi tên / xóa tên bundle, kéo thả asset vào ô để gán bundle.
    ///  - Cấu hình build (nén, platform, output, copy StreamingAssets) + nút build.
    /// </summary>
    public class AssetBundleManagerWindow : EditorWindow
    {
        private AssetBundleBuildSettings _settings;
        private SerializedObject _serializedSettings;
        private Vector2 _scrollPosition;
        private string _filter = string.Empty;
        private string _nameNewBundle = string.Empty;
        private readonly HashSet<string> _setExpanded = new HashSet<string>();
        private readonly Dictionary<string, bool> _dictSelected = new Dictionary<string, bool>();
        private string _nameRenaming;
        private string _nameRenameValue;
        private bool _isShowSettings = true;

        public static void Open()
        {
            var _window = GetWindow<AssetBundleManagerWindow>("AssetBundle Manager");
            _window.minSize = new Vector2(520, 480);
        }

        private void OnEnable()
        {
            _settings = AssetBundleBuildSettings.LoadOrCreate();
            _serializedSettings = new SerializedObject(_settings);
        }

        private void OnGUI()
        {
            if (_settings == null || _serializedSettings == null || _serializedSettings.targetObject == null)
                OnEnable();

            _DrawSettings();
            EditorGUILayout.Space(6);
            _DrawDropArea();
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            _filter = EditorGUILayout.TextField("Lọc", _filter);
            if (GUILayout.Button("Dọn tên rỗng", GUILayout.Width(100)))
                AssetBundleBuilder.RemoveUnusedNames();
            EditorGUILayout.EndHorizontal();

            var _arrayName = AssetDatabase.GetAllAssetBundleNames()
                .Where(n => string.IsNullOrEmpty(_filter) || n.Contains(_filter.ToLowerInvariant())).ToArray();
            var _dictSize = _ReadSizesFromCatalog(EditorUserBuildSettings.activeBuildTarget);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            if (_arrayName.Length == 0)
                EditorGUILayout.HelpBox("Chưa có bundle. Kéo asset vào ô trên, hoặc chuột phải asset > AssetBundle > Gán bundle...", MessageType.Info);
            foreach (var _name in _arrayName)
                _DrawBundle(_name, _dictSize);
            EditorGUILayout.EndScrollView();

            _DrawBuildButtons(_arrayName);
        }

        // ================== Settings ==================

        private void _DrawSettings()
        {
            _isShowSettings = EditorGUILayout.Foldout(_isShowSettings, "Cấu hình build", true, EditorStyles.foldoutHeader);
            if (!_isShowSettings)
                return;
            _serializedSettings.Update();
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("folderOutput"), new GUIContent("Thư mục output"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("compression"), new GUIContent("Nén"));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Platform");
            var _propertyAndroid = _serializedSettings.FindProperty("isBuildAndroid");
            var _propertyIOS = _serializedSettings.FindProperty("isBuildIOS");
            var _propertyWindows = _serializedSettings.FindProperty("isBuildWindows");
            int _indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            _propertyAndroid.boolValue = EditorGUILayout.ToggleLeft("Android", _propertyAndroid.boolValue, GUILayout.Width(80));
            _propertyIOS.boolValue = EditorGUILayout.ToggleLeft("iOS", _propertyIOS.boolValue, GUILayout.Width(60));
            _propertyWindows.boolValue = EditorGUILayout.ToggleLeft("Windows", _propertyWindows.boolValue, GUILayout.Width(80));
            EditorGUI.indentLevel = _indent;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("isCopyToStreamingAssets"), new GUIContent("Copy vào StreamingAssets"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("isCleanOldFiles"), new GUIContent("Xóa file bundle cũ"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("isForceRebuild"), new GUIContent("Build lại toàn bộ"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("baseUrlServer"), new GUIContent("URL server"));
            EditorGUI.indentLevel--;
            _serializedSettings.ApplyModifiedProperties();
        }

        // ================== Kéo thả ==================

        private void _DrawDropArea()
        {
            EditorGUILayout.BeginHorizontal();
            _nameNewBundle = EditorGUILayout.TextField("Bundle đích", _nameNewBundle);
            EditorGUILayout.EndHorizontal();

            var _rect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            GUI.Box(_rect, string.IsNullOrWhiteSpace(_nameNewBundle)
                ? "Kéo asset / thư mục vào đây (nhập tên bundle đích trước, để trống = tên thư mục)"
                : $"Kéo asset / thư mục vào đây để gán vào \"{AssetBundleBuilder.NormalizeName(_nameNewBundle)}\"", EditorStyles.helpBox);

            var _event = Event.current;
            if (!_rect.Contains(_event.mousePosition))
                return;
            if (_event.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                _event.Use();
            }
            else if (_event.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var _path in DragAndDrop.paths.Where(p => p.StartsWith("Assets/")))
                {
                    string _name = _nameNewBundle;
                    if (string.IsNullOrWhiteSpace(_name))
                        _name = AssetDatabase.IsValidFolder(_path) ? Path.GetFileName(_path) : Path.GetFileName(Path.GetDirectoryName(_path));
                    AssetBundleBuilder.SetBundleName(_path, _name);
                }
                _event.Use();
            }
        }

        // ================== Danh sách bundle ==================

        private void _DrawBundle(string nameBundle, Dictionary<string, long> dictSize)
        {
            var _arrayPath = AssetDatabase.GetAssetPathsFromAssetBundle(nameBundle);
            if (!_dictSelected.ContainsKey(nameBundle))
                _dictSelected[nameBundle] = true;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            _dictSelected[nameBundle] = EditorGUILayout.Toggle(_dictSelected[nameBundle], GUILayout.Width(18));
            bool _isExpanded = _setExpanded.Contains(nameBundle);

            if (_nameRenaming == nameBundle)
            {
                _nameRenameValue = EditorGUILayout.TextField(_nameRenameValue);
                if (GUILayout.Button("OK", GUILayout.Width(40)))
                {
                    _RenameBundle(nameBundle, _nameRenameValue);
                    _nameRenaming = null;
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("Hủy", GUILayout.Width(40)))
                    _nameRenaming = null;
            }
            else
            {
                bool _isExpandedNew = EditorGUILayout.Foldout(_isExpanded, nameBundle, true);
                if (_isExpandedNew != _isExpanded)
                {
                    if (_isExpandedNew) _setExpanded.Add(nameBundle);
                    else _setExpanded.Remove(nameBundle);
                }
                GUILayout.FlexibleSpace();
                string _textSize = dictSize.TryGetValue(nameBundle, out long _size) ? EditorUtility.FormatBytes(_size) : "-";
                GUILayout.Label($"{_arrayPath.Length} asset | {_textSize}", EditorStyles.miniLabel, GUILayout.Width(140));
                if (GUILayout.Button("Đổi tên", EditorStyles.miniButtonLeft, GUILayout.Width(55)))
                {
                    _nameRenaming = nameBundle;
                    _nameRenameValue = nameBundle;
                }
                if (GUILayout.Button("Build", EditorStyles.miniButtonMid, GUILayout.Width(45)))
                {
                    var _lstName = new List<string> { nameBundle };
                    EditorApplication.delayCall += () => AssetBundleBuilder.BuildBundles(_lstName, new[] { EditorUserBuildSettings.activeBuildTarget });
                }
                if (GUILayout.Button("Xóa", EditorStyles.miniButtonRight, GUILayout.Width(40)))
                {
                    if (EditorUtility.DisplayDialog("Xóa bundle", $"Bỏ gán bundle \"{nameBundle}\" khỏi {_arrayPath.Length} asset?", "Xóa", "Hủy"))
                    {
                        AssetDatabase.RemoveAssetBundleName(nameBundle, true);
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (var _path in _arrayPath)
                {
                    EditorGUILayout.BeginHorizontal();
                    var _asset = AssetDatabase.LoadMainAssetAtPath(_path);
                    EditorGUILayout.ObjectField(_asset, typeof(Object), false);
                    if (GUILayout.Button("Bỏ", EditorStyles.miniButton, GUILayout.Width(36)))
                    {
                        _RemoveAssetFromBundle(_path);
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        private void _DrawBuildButtons(string[] arrayName)
        {
            var _lstSelected = arrayName.Where(n => _dictSelected.TryGetValue(n, out bool v) && v).ToList();
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Đã chọn {_lstSelected.Count}/{arrayName.Length} bundle | Platform hiện tại: {EditorUserBuildSettings.activeBuildTarget}", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Chọn hết")) foreach (var _name in arrayName) _dictSelected[_name] = true;
            if (GUILayout.Button("Bỏ chọn")) foreach (var _name in arrayName) _dictSelected[_name] = false;
            if (GUILayout.Button("Mở output")) AssetBundleBuilder.OpenFolderOutput();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_lstSelected.Count == 0))
            {
                if (GUILayout.Button("Build đã chọn\n(platform hiện tại)", GUILayout.Height(36)))
                    EditorApplication.delayCall += () => AssetBundleBuilder.BuildBundles(_lstSelected, new[] { EditorUserBuildSettings.activeBuildTarget });
                if (GUILayout.Button("Build đã chọn\n(các platform tích)", GUILayout.Height(36)))
                    EditorApplication.delayCall += () => AssetBundleBuilder.BuildBundles(_lstSelected, AssetBundleBuilder.GetTargetsFromSettings());
            }
            if (GUILayout.Button("Build tất cả\n(các platform tích)", GUILayout.Height(36)))
                EditorApplication.delayCall += () => AssetBundleBuilder.BuildBundles(null, AssetBundleBuilder.GetTargetsFromSettings());
            EditorGUILayout.EndHorizontal();
        }

        // ================== Helper ==================

        private static void _RenameBundle(string nameOld, string nameNew)
        {
            nameNew = AssetBundleBuilder.NormalizeName(nameNew);
            if (string.IsNullOrEmpty(nameNew) || nameNew == nameOld)
                return;
            foreach (var _path in AssetDatabase.GetAssetPathsFromAssetBundle(nameOld))
            {
                var _importer = AssetImporter.GetAtPath(_path);
                if (_importer != null && _importer.assetBundleName == nameOld)
                    _importer.SetAssetBundleNameAndVariant(nameNew, string.Empty);
                else
                    _ReassignFolderParent(_path, nameOld, nameNew);
            }
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }

        /// <summary>Asset nằm trong thư mục được gán bundle -> đổi tên ở thư mục cha.</summary>
        private static void _ReassignFolderParent(string assetPath, string nameOld, string nameNew)
        {
            string _folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(_folder) && _folder.StartsWith("Assets"))
            {
                var _importer = AssetImporter.GetAtPath(_folder);
                if (_importer != null && _importer.assetBundleName == nameOld)
                {
                    _importer.SetAssetBundleNameAndVariant(nameNew, string.Empty);
                    return;
                }
                _folder = Path.GetDirectoryName(_folder)?.Replace('\\', '/');
            }
        }

        private static void _RemoveAssetFromBundle(string assetPath)
        {
            var _importer = AssetImporter.GetAtPath(assetPath);
            if (_importer != null && !string.IsNullOrEmpty(_importer.assetBundleName))
                _importer.SetAssetBundleNameAndVariant(string.Empty, string.Empty);
            else
                Debug.LogWarning("[AssetBundle] Asset này được gán qua thư mục cha, hãy bỏ gán ở thư mục: " + Path.GetDirectoryName(assetPath));
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }

        /// <summary>Đọc dung lượng từng bundle từ file build gần nhất.</summary>
        private static Dictionary<string, long> _ReadSizesFromCatalog(BuildTarget target)
        {
            var _dict = new Dictionary<string, long>();
            string _folder = AssetBundleBuilder.GetFolderOutput(target);
            if (!Directory.Exists(_folder))
                return _dict;
            foreach (var _name in AssetDatabase.GetAllAssetBundleNames())
            {
                string _path = Path.Combine(_folder, _name);
                if (File.Exists(_path))
                    _dict[_name] = new FileInfo(_path).Length;
            }
            return _dict;
        }
    }
}
#endif
