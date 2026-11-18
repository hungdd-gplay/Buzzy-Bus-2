#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BaseProject.EditorTools.AssetBundles
{
    /// <summary>
    /// Logic gán tên bundle + build. Menu "AssetBundle" trên thanh menu và chuột phải trong Project (Assets/AssetBundle/...).
    /// Output: {folderOutput}/{Platform}/{bundle} + catalog.json (hash, crc, size) để upload server.
    /// </summary>
    public static class AssetBundleBuilder
    {
        private const string PATH_MENU = "AssetBundle/";
        private const string PATH_MENU_ASSETS = "Assets/AssetBundle/";

        // ================== Menu thanh trên ==================

        [MenuItem(PATH_MENU + "Manager...", false, 0)]
        private static void MenuOpenManager() => AssetBundleManagerWindow.Open();

        [MenuItem(PATH_MENU + "Build tất cả (platform hiện tại)", false, 20)]
        private static void MenuBuildActive()
        {
            BuildBundles(null, new[] { EditorUserBuildSettings.activeBuildTarget });
        }

        [MenuItem(PATH_MENU + "Build tất cả (các platform đã chọn)", false, 21)]
        private static void MenuBuildSelectedPlatforms()
        {
            BuildBundles(null, GetTargetsFromSettings());
        }

        [MenuItem(PATH_MENU + "Mở thư mục output", false, 40)]
        public static void OpenFolderOutput()
        {
            string _folder = GetFolderOutput(null);
            Directory.CreateDirectory(_folder);
            EditorUtility.RevealInFinder(_folder + Path.DirectorySeparatorChar);
        }

        [MenuItem(PATH_MENU + "Dọn tên bundle không dùng", false, 41)]
        public static void RemoveUnusedNames()
        {
            AssetDatabase.RemoveUnusedAssetBundleNames();
            Debug.Log("[AssetBundle] Đã xóa tên bundle không còn asset nào.");
        }

        // ================== Menu chuột phải (Project) ==================

        [MenuItem(PATH_MENU_ASSETS + "Gán bundle = tên thư mục chứa", false, 2000)]
        private static void MenuAssignByFolder()
        {
            foreach (var _path in _GetSelectedPaths())
            {
                string _folder = AssetDatabase.IsValidFolder(_path) ? _path : Path.GetDirectoryName(_path).Replace('\\', '/');
                SetBundleName(_path, Path.GetFileName(_folder));
            }
        }

        [MenuItem(PATH_MENU_ASSETS + "Gán bundle = tên từng file", false, 2001)]
        private static void MenuAssignByFile()
        {
            foreach (var _path in _GetSelectedPaths())
                SetBundleName(_path, Path.GetFileNameWithoutExtension(_path));
        }

        [MenuItem(PATH_MENU_ASSETS + "Gán bundle...", false, 2002)]
        private static void MenuAssignByName()
        {
            AssetBundleNamePopup.Open(_GetSelectedPaths().ToArray());
        }

        [MenuItem(PATH_MENU_ASSETS + "Bỏ gán bundle", false, 2003)]
        private static void MenuClear()
        {
            foreach (var _path in _GetSelectedPaths())
                SetBundleName(_path, string.Empty);
        }

        [MenuItem(PATH_MENU_ASSETS + "Gán bundle = tên thư mục chứa", true)]
        [MenuItem(PATH_MENU_ASSETS + "Gán bundle = tên từng file", true)]
        [MenuItem(PATH_MENU_ASSETS + "Gán bundle...", true)]
        [MenuItem(PATH_MENU_ASSETS + "Bỏ gán bundle", true)]
        private static bool ValidateSelection() => _GetSelectedPaths().Count > 0;

        // ================== API ==================

        /// <summary>Gán tên bundle (chữ thường, cho phép "/" để tạo thư mục con). Rỗng = bỏ gán.</summary>
        public static void SetBundleName(string assetPath, string bundleName)
        {
            var _importer = AssetImporter.GetAtPath(assetPath);
            if (_importer == null)
                return;
            _importer.SetAssetBundleNameAndVariant(NormalizeName(bundleName), string.Empty);
        }

        public static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            return name.Trim().ToLowerInvariant().Replace(' ', '_').Replace('\\', '/');
        }

        public static BuildTarget[] GetTargetsFromSettings()
        {
            var _settings = AssetBundleBuildSettings.LoadOrCreate();
            var _lst = new List<BuildTarget>();
            if (_settings.isBuildAndroid) _lst.Add(BuildTarget.Android);
            if (_settings.isBuildIOS) _lst.Add(BuildTarget.iOS);
            if (_settings.isBuildWindows) _lst.Add(BuildTarget.StandaloneWindows64);
            if (_lst.Count == 0) _lst.Add(EditorUserBuildSettings.activeBuildTarget);
            return _lst.ToArray();
        }

        public static string GetFolderOutput(BuildTarget? target)
        {
            var _settings = AssetBundleBuildSettings.LoadOrCreate();
            string _root = Path.IsPathRooted(_settings.folderOutput)
                ? _settings.folderOutput
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", _settings.folderOutput));
            return target.HasValue ? Path.Combine(_root, target.Value.ToString()) : _root;
        }

        /// <summary>Build bundle. lstBundleName null = tất cả bundle có tên.</summary>
        public static bool BuildBundles(IList<string> lstBundleName, IList<BuildTarget> lstTarget)
        {
            var _settings = AssetBundleBuildSettings.LoadOrCreate();
            var _lstName = (lstBundleName ?? AssetDatabase.GetAllAssetBundleNames())
                .Where(n => AssetDatabase.GetAssetPathsFromAssetBundle(n).Length > 0).ToList();
            if (_lstName.Count == 0)
            {
                EditorUtility.DisplayDialog("AssetBundle", "Chưa có bundle nào được gán asset.", "OK");
                return false;
            }

            var _arrayBuild = _lstName.Select(n => new AssetBundleBuild
            {
                assetBundleName = n,
                assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(n),
            }).ToArray();

            var _options = BuildAssetBundleOptions.None;
            if (_settings.compression == BundleCompression.LZ4) _options |= BuildAssetBundleOptions.ChunkBasedCompression;
            else if (_settings.compression == BundleCompression.None) _options |= BuildAssetBundleOptions.UncompressedAssetBundle;
            if (_settings.isForceRebuild) _options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;

            bool _isSuccess = true;
            foreach (var _target in lstTarget)
            {
                string _folder = GetFolderOutput(_target);
                Directory.CreateDirectory(_folder);
                EditorUtility.DisplayProgressBar("Build AssetBundle", $"{_target}: {_arrayBuild.Length} bundle", 0.5f);
                try
                {
                    var _manifest = BuildPipeline.BuildAssetBundles(_folder, _arrayBuild, _options, _target);
                    if (_manifest == null)
                    {
                        Debug.LogError("[AssetBundle] Build lỗi cho " + _target);
                        _isSuccess = false;
                        continue;
                    }
                    if (_settings.isCleanOldFiles && lstBundleName == null)
                        _CleanOldFiles(_folder, _manifest.GetAllAssetBundles(), _target.ToString());
                    _WriteCatalog(_folder, _manifest, _target, _settings);
                    if (_settings.isCopyToStreamingAssets)
                        _CopyToStreamingAssets(_folder, _manifest, _target);
                    Debug.Log($"[AssetBundle] Build xong {_target}: {_manifest.GetAllAssetBundles().Length} bundle -> {_folder}");
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            AssetDatabase.Refresh();
            return _isSuccess;
        }

        // ================== Private ==================

        private static List<string> _GetSelectedPaths()
        {
            return Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p) && p.StartsWith("Assets/")).ToList();
        }

        private static void _WriteCatalog(string folder, AssetBundleManifest manifest, BuildTarget target, AssetBundleBuildSettings settings)
        {
            var _builder = new StringBuilder();
            _builder.AppendLine("{");
            _builder.AppendLine($"  \"platform\": \"{target}\",");
            _builder.AppendLine($"  \"buildTime\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
            _builder.AppendLine($"  \"baseUrl\": \"{settings.baseUrlServer}\",");
            _builder.AppendLine("  \"bundles\": [");
            var _arrayName = manifest.GetAllAssetBundles();
            for (int i = 0; i < _arrayName.Length; i++)
            {
                string _name = _arrayName[i];
                string _path = Path.Combine(folder, _name);
                BuildPipeline.GetCRCForAssetBundle(_path, out uint _crc);
                long _size = File.Exists(_path) ? new FileInfo(_path).Length : 0;
                string _dependencies = string.Join(",", manifest.GetAllDependencies(_name).Select(d => $"\"{d}\""));
                _builder.Append($"    {{ \"name\": \"{_name}\", \"hash\": \"{manifest.GetAssetBundleHash(_name)}\", \"crc\": {_crc}, \"size\": {_size}, \"dependencies\": [{_dependencies}] }}");
                _builder.AppendLine(i < _arrayName.Length - 1 ? "," : "");
            }
            _builder.AppendLine("  ]");
            _builder.AppendLine("}");
            File.WriteAllText(Path.Combine(folder, "catalog.json"), _builder.ToString(), new UTF8Encoding(false));
        }

        private static void _CleanOldFiles(string folder, string[] arrayBundleName, string nameManifest)
        {
            var _setKeep = new HashSet<string>(arrayBundleName.Select(n => n.Replace('\\', '/')));
            foreach (var _file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string _relative = _file.Substring(folder.Length + 1).Replace('\\', '/');
                string _base = _relative.EndsWith(".manifest") ? _relative.Substring(0, _relative.Length - 9) : _relative;
                if (_base == nameManifest || _relative == "catalog.json" || _setKeep.Contains(_base))
                    continue;
                File.Delete(_file);
                Debug.Log("[AssetBundle] Xóa file cũ: " + _relative);
            }
        }

        private static void _CopyToStreamingAssets(string folder, AssetBundleManifest manifest, BuildTarget target)
        {
            string _destination = Path.Combine(Application.streamingAssetsPath, AssetBundleLoader.FOLDER_STREAMING, target.ToString());
            if (Directory.Exists(_destination))
                Directory.Delete(_destination, true);
            Directory.CreateDirectory(_destination);
            foreach (var _name in manifest.GetAllAssetBundles().Append(target.ToString()).Append("catalog.json"))
            {
                string _source = Path.Combine(folder, _name);
                if (!File.Exists(_source))
                    continue;
                string _dest = Path.Combine(_destination, _name);
                Directory.CreateDirectory(Path.GetDirectoryName(_dest));
                File.Copy(_source, _dest, true);
            }
        }
    }

    /// <summary>Popup nhập tên bundle cho các asset đang chọn.</summary>
    public class AssetBundleNamePopup : EditorWindow
    {
        private string[] _arrayPath;
        private string _nameBundle = string.Empty;
        private int _indexExisting = -1;

        public static void Open(string[] arrayPath)
        {
            var _window = CreateInstance<AssetBundleNamePopup>();
            _window._arrayPath = arrayPath;
            _window.titleContent = new GUIContent("Gán AssetBundle");
            _window.ShowUtility();
            _window.minSize = new Vector2(360, 120);
            _window.maxSize = new Vector2(600, 140);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField($"{_arrayPath?.Length ?? 0} asset/thư mục đang chọn");
            var _arrayExisting = AssetDatabase.GetAllAssetBundleNames();
            int _index = EditorGUILayout.Popup("Bundle có sẵn", _indexExisting, _arrayExisting);
            if (_index != _indexExisting)
            {
                _indexExisting = _index;
                if (_index >= 0) _nameBundle = _arrayExisting[_index];
            }
            _nameBundle = EditorGUILayout.TextField("Tên bundle", _nameBundle);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Hủy")) Close();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_nameBundle)))
            {
                if (GUILayout.Button("Gán"))
                {
                    foreach (var _path in _arrayPath)
                        AssetBundleBuilder.SetBundleName(_path, _nameBundle);
                    Close();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
