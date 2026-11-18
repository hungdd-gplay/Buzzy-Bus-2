using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BaseProject.Theme.EditorTools
{
    /// <summary>
    /// Controller (Editor): tạo cấu trúc, đổi concept 1 nút, dựng ThemePack từ thư mục, tạo + build theme sự kiện.
    /// Cấu trúc:
    ///   Assets/_ThemeAssets/Default/{Loading, Home, Gameplay}/...        concept mặc định (bị thay khi đổi concept)
    ///   Assets/_ThemeAssets/Events/&lt;Sự kiện&gt;/{Loading, Home, Gameplay}/  theme sự kiện (không bị xoá, có thể đóng AssetBundle)
    ///   Assets/Resources/Theme/ThemeSystemConfig.asset                  cấu hình
    /// </summary>
    public static class ThemeEditorUtility
    {
        public const string PATH_ROOT = "Assets/_ThemeAssets";
        public const string PATH_DEFAULT = PATH_ROOT + "/Default";
        public const string PATH_EVENTS = PATH_ROOT + "/Events";
        public const string PATH_CONFIG = "Assets/Resources/Theme/ThemeSystemConfig.asset";
        public const string PATH_PACK_DEFAULT = PATH_DEFAULT + "/ThemePack_Default.asset";
        public const string FOLDER_OTHER = "_Other";
        public const string PREFIX_BUNDLE = "theme_";
        public const string PATH_BUNDLE_OUTPUT = "AssetBundles";
        public const string PATH_BACKUP = "ThemeBackup";

        private static readonly string[] _EXTENSIONS_IMAGE = { ".png", ".jpg", ".jpeg", ".tga", ".psd" };

        #region Setup

        /// <summary>Tạo thư mục, config, pack mặc định (bỏ qua phần đã có).</summary>
        public static ThemeSystemConfig SetupStructure()
        {
            _EnsureFolder(PATH_DEFAULT);
            foreach (ThemeCategory _category in Enum.GetValues(typeof(ThemeCategory)))
                _EnsureFolder(PATH_DEFAULT + "/" + _category);
            _EnsureFolder(PATH_EVENTS);
            _EnsureFolder(Path.GetDirectoryName(PATH_CONFIG).Replace('\\', '/'));

            var _packDefault = AssetDatabase.LoadAssetAtPath<ThemePack>(PATH_PACK_DEFAULT);
            if (_packDefault == null)
            {
                _packDefault = ScriptableObject.CreateInstance<ThemePack>();
                _packDefault.eventId = ThemeEventId.Default;
                _packDefault.displayName = "Default";
                AssetDatabase.CreateAsset(_packDefault, PATH_PACK_DEFAULT);
            }

            var _config = LoadConfig();
            if (_config == null)
            {
                _config = ScriptableObject.CreateInstance<ThemeSystemConfig>();
                AssetDatabase.CreateAsset(_config, PATH_CONFIG);
            }
            if (_config.packDefault == null)
            {
                _config.packDefault = _packDefault;
                EditorUtility.SetDirty(_config);
            }
            _WriteKeyGuide(PATH_ROOT + "/_HuongDanTenAnh.txt");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return _config;
        }

        public static ThemeSystemConfig LoadConfig()
        {
            return AssetDatabase.LoadAssetAtPath<ThemeSystemConfig>(PATH_CONFIG);
        }

        private static void _WriteKeyGuide(string path)
        {
            var _sb = new StringBuilder();
            _sb.AppendLine("TÊN FILE ẢNH THEME (không phân biệt hoa thường, đuôi .png/.jpg/.tga/.psd)");
            _sb.AppendLine("Ảnh trang trí con: thêm hậu tố, vd Home_ButtonPlay_Decor_1, Home_ButtonPlay_Decor_2");
            _sb.AppendLine("BG gameplay nhiều ảnh: Gameplay_BG_01, Gameplay_BG_02... (sắp theo tên)");
            _sb.AppendLine();
            foreach (ThemeCategory _category in Enum.GetValues(typeof(ThemeCategory)))
            {
                _sb.AppendLine("[" + _category + "]");
                foreach (ThemeSlotId _slot in Enum.GetValues(typeof(ThemeSlotId)))
                {
                    if (_slot != ThemeSlotId.None && ThemeKeyUtility.GetCategory(_slot) == _category)
                        _sb.AppendLine("  " + _slot);
                }
            }
            File.WriteAllText(path, _sb.ToString(), new UTF8Encoding(true));
        }

        #endregion

        #region Đổi concept

        public class ConceptReport
        {
            public int numReplaced;
            public int numAdded;
            public int numDeleted;
            public List<string> warnings = new List<string>();
            public string pathBackup;
        }

        /// <summary>
        /// Đổi concept: ảnh trong thư mục nguồn (tên file = key) được chép vào Default/&lt;nhóm&gt;.
        /// Ảnh cùng key -> ghi đè và GIỮ GUID (scene/prefab đang dùng tự nhận ảnh mới).
        /// isDeleteOld: ảnh cũ không có trong bộ mới bị xoá.
        /// </summary>
        public static ConceptReport ApplyConcept(string pathFolderSource, bool isDeleteOld, bool isBackup)
        {
            var _report = new ConceptReport();
            string _fullDefault = Path.GetFullPath(PATH_DEFAULT);
            string _fullSource = Path.GetFullPath(pathFolderSource);
            if (_fullSource.StartsWith(_fullDefault, StringComparison.OrdinalIgnoreCase))
            {
                _report.warnings.Add("Thư mục nguồn không được nằm trong " + PATH_DEFAULT);
                return _report;
            }

            SetupStructure();
            if (isBackup)
                _report.pathBackup = _BackupDefault();

            // Ảnh concept hiện tại: key -> đường dẫn asset
            var _dictOld = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var _path in _FindImages(PATH_DEFAULT))
                _dictOld[Path.GetFileNameWithoutExtension(_path)] = _path;

            // Ảnh concept mới
            var _setKeyNew = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var _filesSource = Directory.GetFiles(_fullSource, "*.*", SearchOption.AllDirectories)
                .Where(f => _EXTENSIONS_IMAGE.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var _fileSource in _filesSource)
            {
                string _key = Path.GetFileNameWithoutExtension(_fileSource);
                if (!_setKeyNew.Add(_key))
                {
                    _report.warnings.Add($"Trùng tên '{_key}', bỏ qua: {_fileSource}");
                    continue;
                }

                string _folderCategory = ThemeKeyUtility.TryParseKey(_key, out var _slot, out _)
                    ? ThemeKeyUtility.GetCategory(_slot).ToString()
                    : FOLDER_OTHER;
                if (_folderCategory == FOLDER_OTHER)
                    _report.warnings.Add($"'{_key}' không khớp slot nào trong ThemeSlotId -> để vào {FOLDER_OTHER}");

                string _folderTarget = PATH_DEFAULT + "/" + _folderCategory;
                Directory.CreateDirectory(_folderTarget);
                string _pathTarget = _folderTarget + "/" + _key + Path.GetExtension(_fileSource).ToLowerInvariant();

                if (_dictOld.TryGetValue(_key, out var _pathOld))
                {
                    _ReplaceKeepGuid(_pathOld, _fileSource, _pathTarget);
                    _report.numReplaced++;
                }
                else
                {
                    File.Copy(_fileSource, _pathTarget, true);
                    _report.numAdded++;
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            if (isDeleteOld)
            {
                foreach (var _pair in _dictOld)
                {
                    if (_setKeyNew.Contains(_pair.Key))
                        continue;
                    if (AssetDatabase.DeleteAsset(_pair.Value))
                        _report.numDeleted++;
                }
            }

            EnsureSpriteImport(PATH_DEFAULT);
            var _packDefault = AssetDatabase.LoadAssetAtPath<ThemePack>(PATH_PACK_DEFAULT);
            RebuildPack(_packDefault, PATH_DEFAULT);
            return _report;
        }

        /// <summary>Thay nội dung ảnh nhưng giữ nguyên GUID (kể cả khi đổi đuôi png -> jpg).</summary>
        private static void _ReplaceKeepGuid(string pathOld, string fileSource, string pathTarget)
        {
            if (string.Equals(pathOld, pathTarget, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(fileSource, pathOld, true);
                return;
            }
            string _metaOld = pathOld + ".meta";
            string _metaText = File.Exists(_metaOld) ? File.ReadAllText(_metaOld) : null;
            File.Delete(pathOld);
            if (File.Exists(_metaOld))
                File.Delete(_metaOld);
            File.Copy(fileSource, pathTarget, true);
            if (_metaText != null)
                File.WriteAllText(pathTarget + ".meta", _metaText);
        }

        private static string _BackupDefault()
        {
            string _pathBackup = Path.Combine(PATH_BACKUP, "Default_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            _CopyDirectory(PATH_DEFAULT, _pathBackup);
            return Path.GetFullPath(_pathBackup);
        }

        private static void _CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var _file in Directory.GetFiles(from))
                File.Copy(_file, Path.Combine(to, Path.GetFileName(_file)), true);
            foreach (var _dir in Directory.GetDirectories(from))
                _CopyDirectory(_dir, Path.Combine(to, Path.GetFileName(_dir)));
        }

        #endregion

        #region Pack

        /// <summary>Dựng lại ThemePack từ ảnh trong thư mục (tên file = key; Gameplay_BG_xx -> danh sách BG).</summary>
        public static void RebuildPack(ThemePack pack, string pathFolder)
        {
            if (pack == null)
                return;
            pack.entries.Clear();
            pack.gameplayBackgrounds.Clear();

            var _lsPath = _FindImages(pathFolder).OrderBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var _path in _lsPath)
            {
                var _sprite = AssetDatabase.LoadAllAssetsAtPath(_path).OfType<Sprite>().FirstOrDefault();
                if (_sprite == null)
                    continue;
                string _key = Path.GetFileNameWithoutExtension(_path);
                if (ThemeKeyUtility.IsGameplayBackgroundKey(_key))
                    pack.gameplayBackgrounds.Add(_sprite);
                pack.entries.Add(new ThemeSpriteEntry { key = _key, sprite = _sprite });
            }
            pack.ClearCache();
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Đặt Texture Type = Sprite cho mọi ảnh trong thư mục.</summary>
        public static void EnsureSpriteImport(string pathFolder)
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var _path in _FindImages(pathFolder))
                {
                    var _importer = AssetImporter.GetAtPath(_path) as TextureImporter;
                    if (_importer == null || _importer.textureType == TextureImporterType.Sprite)
                        continue;
                    _importer.textureType = TextureImporterType.Sprite;
                    _importer.spriteImportMode = SpriteImportMode.Single;
                    _importer.alphaIsTransparency = true;
                    _importer.mipmapEnabled = false;
                    _importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();
        }

        private static List<string> _FindImages(string pathFolder)
        {
            var _result = new List<string>();
            if (!AssetDatabase.IsValidFolder(pathFolder))
                return _result;
            foreach (var _guid in AssetDatabase.FindAssets("t:Texture2D", new[] { pathFolder }))
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                if (_EXTENSIONS_IMAGE.Contains(Path.GetExtension(_path).ToLowerInvariant()) && !_result.Contains(_path))
                    _result.Add(_path);
            }
            return _result;
        }

        #endregion

        #region Theme sự kiện

        public static string GetEventFolder(ThemeEventId eventId)
        {
            return PATH_EVENTS + "/" + eventId;
        }

        public static string GetEventPackPath(ThemeEventId eventId)
        {
            return GetEventFolder(eventId) + "/ThemePack_" + eventId + ".asset";
        }

        public static string GetBundleName(ThemeEventId eventId)
        {
            return PREFIX_BUNDLE + eventId.ToString().ToLowerInvariant();
        }

        /// <summary>Tạo thư mục sự kiện + ThemePack. isIncludeInBuild: thêm vào danh sách theme có sẵn trong build.</summary>
        public static ThemePack CreateEventTheme(ThemeEventId eventId, bool isIncludeInBuild)
        {
            var _config = SetupStructure();
            string _folder = GetEventFolder(eventId);
            _EnsureFolder(_folder);
            foreach (ThemeCategory _category in Enum.GetValues(typeof(ThemeCategory)))
                _EnsureFolder(_folder + "/" + _category);

            var _pack = AssetDatabase.LoadAssetAtPath<ThemePack>(GetEventPackPath(eventId));
            if (_pack == null)
            {
                _pack = ScriptableObject.CreateInstance<ThemePack>();
                _pack.eventId = eventId;
                _pack.displayName = eventId.ToString();
                AssetDatabase.CreateAsset(_pack, GetEventPackPath(eventId));
            }
            SetEventIncludeInBuild(eventId, isIncludeInBuild, _config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return _pack;
        }

        public static void SetEventIncludeInBuild(ThemeEventId eventId, bool isInclude, ThemeSystemConfig config = null)
        {
            config = config ?? LoadConfig();
            var _pack = AssetDatabase.LoadAssetAtPath<ThemePack>(GetEventPackPath(eventId));
            if (config == null || _pack == null)
                return;
            config.packsEventLocal.RemoveAll(p => p == null || p == _pack);
            if (isInclude)
                config.packsEventLocal.Add(_pack);
            EditorUtility.SetDirty(config);
        }

        public static bool IsEventIncludeInBuild(ThemeEventId eventId)
        {
            var _config = LoadConfig();
            var _pack = AssetDatabase.LoadAssetAtPath<ThemePack>(GetEventPackPath(eventId));
            return _config != null && _pack != null && _config.packsEventLocal.Contains(_pack);
        }

        /// <summary>Cập nhật ThemePack sự kiện từ ảnh trong thư mục sự kiện.</summary>
        public static ThemePack RefreshEventPack(ThemeEventId eventId)
        {
            string _folder = GetEventFolder(eventId);
            var _pack = AssetDatabase.LoadAssetAtPath<ThemePack>(GetEventPackPath(eventId));
            if (_pack == null)
                return null;
            EnsureSpriteImport(_folder);
            RebuildPack(_pack, _folder);
            return _pack;
        }

        /// <summary>Build AssetBundle cho 1 sự kiện (nền tảng đang chọn trong Build Settings). Trả về đường dẫn file.</summary>
        public static string BuildEventBundle(ThemeEventId eventId)
        {
            string _folder = GetEventFolder(eventId);
            if (!AssetDatabase.IsValidFolder(_folder))
                throw new Exception("Chưa có thư mục sự kiện: " + _folder);
            RefreshEventPack(eventId);

            var _assetNames = AssetDatabase.FindAssets(string.Empty, new[] { _folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .Distinct()
                .ToArray();

            var _target = EditorUserBuildSettings.activeBuildTarget;
            string _output = Path.Combine(PATH_BUNDLE_OUTPUT, _target.ToString());
            Directory.CreateDirectory(_output);
            var _build = new AssetBundleBuild
            {
                assetBundleName = GetBundleName(eventId),
                assetNames = _assetNames
            };
            var _manifest = BuildPipeline.BuildAssetBundles(_output, new[] { _build }, BuildAssetBundleOptions.ChunkBasedCompression, _target);
            if (_manifest == null)
                throw new Exception("Build AssetBundle thất bại, xem Console.");
            return Path.GetFullPath(Path.Combine(_output, _build.assetBundleName));
        }

        #endregion

        #region Preview trong Editor

        /// <summary>Gán ảnh của 1 pack lên mọi ThemeSlotImage / ThemeGameplayBackground trong scene đang mở (có Undo).</summary>
        public static int PreviewPackInScene(ThemePack pack, ThemePack packFallback)
        {
            int _count = 0;
            foreach (var _slotImage in UnityEngine.Object.FindObjectsByType<ThemeSlotImage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Sprite _sprite = pack != null ? pack.GetSprite(_slotImage.slot, _slotImage.subKey) : null;
                if (_sprite == null && packFallback != null)
                    _sprite = packFallback.GetSprite(_slotImage.slot, _slotImage.subKey);
                if (_sprite == null)
                    continue;
                _AssignSpriteWithUndo(_slotImage.gameObject, _sprite);
                _count++;
            }
            foreach (var _background in UnityEngine.Object.FindObjectsByType<ThemeGameplayBackground>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Sprite _sprite = pack != null && pack.HasGameplayBackground ? pack.GetGameplayBackground(1) : null;
                if (_sprite == null && packFallback != null)
                    _sprite = packFallback.GetGameplayBackground(1);
                if (_sprite == null)
                    continue;
                _AssignSpriteWithUndo(_background.gameObject, _sprite);
                _count++;
            }
            return _count;
        }

        private static void _AssignSpriteWithUndo(GameObject target, Sprite sprite)
        {
            var _image = target.GetComponent<UnityEngine.UI.Image>();
            if (_image != null)
            {
                Undo.RecordObject(_image, "Preview Theme");
                _image.sprite = sprite;
                EditorUtility.SetDirty(_image);
                return;
            }
            var _renderer = target.GetComponent<SpriteRenderer>();
            if (_renderer != null)
            {
                Undo.RecordObject(_renderer, "Preview Theme");
                _renderer.sprite = sprite;
                EditorUtility.SetDirty(_renderer);
            }
        }

        #endregion

        private static void _EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path))
                return;
            string _parent = Path.GetDirectoryName(path).Replace('\\', '/');
            _EnsureFolder(_parent);
            AssetDatabase.CreateFolder(_parent, Path.GetFileName(path));
        }
    }
}
