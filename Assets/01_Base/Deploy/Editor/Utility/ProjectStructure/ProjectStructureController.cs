using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BaseProject.EditorTools.Build;
using UnityEditor;
using UnityEngine;

namespace BaseProject.EditorTools.ProjectStructure
{
    /// <summary>
    /// Controller tái cấu trúc thư mục:
    ///   1. BuildPlan: phân loại mọi thứ ở Assets/ theo luật -> danh sách MoveEntry (chưa di chuyển gì).
    ///   2. Quét asset không dùng (không nằm trong phụ thuộc của scene build / Resources / StreamingAssets /
    ///      AssetBundle / ProjectSettings / script / thư mục Editor) -> _ToDelete.
    ///   3. Apply: di chuyển bằng AssetDatabase.MoveAsset (giữ GUID, không mất tham chiếu), dọn thư mục rỗng,
    ///      sửa đường dẫn keystore, tạo khung 02_Gameplay, lưu lịch sử để hoàn tác.
    /// </summary>
    public static class ProjectStructureController
    {
        private const string _PATH_HISTORY = "ProjectStructureHistory.json"; // Ở thư mục gốc project (ngoài Assets)
        private const string _ASSETS = "Assets/";

        private static readonly HashSet<string> _EXT_SKIP_UNUSED = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".asmdef", ".asmref", ".rsp", ".shader", ".cginc", ".hlsl", ".compute", ".shadergraph", ".shadersubgraph",
            ".dll", ".aar", ".jar", ".so", ".a", ".bundle", ".framework", ".md", ".keystore", ".jks",
            ".m", ".mm", ".h", ".c", ".cpp", ".swift", ".java", ".kt", ".gradle", ".plist", ".androidlib", ".xcframework"
        };

        private static readonly HashSet<string> _NAME_FOLDER_STRIP_DEPLOY = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Scripts", "Script", "Controllers", "Controller", "Views", "View", "Models", "Model", "Datas", "Editor"
        };

        private static readonly HashSet<string> _NAME_FOLDER_TYPE_RES = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Prefabs", "Prefab", "Prefap", "Texture2D", "Textures", "Texture", "Sprites", "Sprite", "Images", "Audio", "Audios",
            "Sounds", "Sound", "Musics", "Music", "Animations", "Animation", "AnimationNew", "Anim", "Materials", "Material",
            "Fonts", "Font", "Scenes", "Scene", "Data", "Datas", "Models", "Meshes", "Shaders", "Videos"
        };

        #region Lập kế hoạch

        public static MovePlan BuildPlan(ProjectStructureRules rules, Func<string, float, bool> onProgress = null)
        {
            var _plan = new MovePlan { timeCreate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            var _keep = new HashSet<string>(rules.keepAtRoot, StringComparer.OrdinalIgnoreCase);
            var _thirdParty = new HashSet<string>(rules.thirdParty, StringComparer.OrdinalIgnoreCase);
            var _junk = new HashSet<string>(rules.junkAtRoot, StringComparer.OrdinalIgnoreCase);
            var _baseRoots = new List<string>();

            foreach (var _pathRoot in Directory.GetFileSystemEntries("Assets").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
            {
                if (_pathRoot.EndsWith(".meta") || Path.GetFileName(_pathRoot).StartsWith("."))
                    continue;
                string _name = Path.GetFileName(_pathRoot);
                bool _isFolder = Directory.Exists(_pathRoot);
                if (_keep.Contains(_name))
                    continue;

                if (_junk.Contains(_name))
                    _plan.entries.Add(_Entry(_pathRoot, $"{ProjectPaths.TO_DELETE}/{_name}", MoveCategory.Unused, "Rác ở thư mục gốc", _isFolder));
                else if (_thirdParty.Contains(_name))
                    _plan.entries.Add(_Entry(_pathRoot, $"{ProjectPaths.THIRD_PARTY}/{_name}", MoveCategory.ThirdParty, "Bên thứ 3", _isFolder));
                else if (!_isFolder && _IsEnvironmentFile(_name, rules))
                    _plan.entries.Add(_Entry(_pathRoot, $"{ProjectPaths.ENVIRONMENT}/{_GetEnvironmentSubFolder(_name)}/{_name}", MoveCategory.Environment, "File môi trường", false));
                else
                    _baseRoots.Add(_pathRoot);
            }

            // File của dự án base (từng file)
            var _lsFile = new List<string>();
            foreach (var _pathRoot in _baseRoots)
            {
                if (Directory.Exists(_pathRoot))
                    _lsFile.AddRange(Directory.GetFiles(_pathRoot, "*", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).Where(p => !p.EndsWith(".meta") && !Path.GetFileName(p).StartsWith(".")));
                else
                    _lsFile.Add(_pathRoot);
            }

            for (int i = 0; i < _lsFile.Count; i++)
            {
                if (onProgress != null && i % 50 == 0 && onProgress("Phân loại " + _lsFile[i], (float)i / _lsFile.Count * 0.6f))
                    return _plan;
                var _entry = _PlanBaseFile(_lsFile[i], rules, _plan);
                if (_entry != null)
                    _plan.entries.Add(_entry);
            }

            if (rules.isScanUnused)
                _MarkUnused(_plan, onProgress);
            _MarkConflicts(_plan);
            return _plan;
        }

        private static MoveEntry _PlanBaseFile(string path, ProjectStructureRules rules, MovePlan plan)
        {
            string _relative = path.Substring(_ASSETS.Length);
            var _segments = _relative.Split('/').ToList();
            string _fileName = _segments[_segments.Count - 1];
            var _dirs = _segments.Take(_segments.Count - 1).ToList();
            string _extension = Path.GetExtension(_fileName);

            foreach (var _keep in rules.keepPaths)
                if (!string.IsNullOrEmpty(_keep) && path.StartsWith(_keep.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
                    return null;

            // Thư mục đặc biệt
            int _indexResources = _dirs.FindLastIndex(d => d == "Resources");
            if (_indexResources >= 0 && _dirs.Take(_indexResources).Contains("Editor"))
            {
                // Resources của tool Editor: giữ là Editor-only (không lọt vào bản build)
                string _sub = string.Join("/", _segments.Skip(_indexResources + 1));
                return _Entry(path, $"{ProjectPaths.BASE_DEPLOY}/{ProjectPaths.FOLDER_EDITOR}/Resources/{_sub}", MoveCategory.Base, "Resources của Editor (giữ Editor-only)", false);
            }
            if (_indexResources >= 0)
            {
                string _sub = string.Join("/", _segments.Skip(_indexResources + 1));
                return _Entry(path, $"{ProjectPaths.RESOURCES}/{_sub}", MoveCategory.Resources, "Gộp vào Assets/Resources (giữ đường dẫn Resources.Load)", false);
            }
            int _indexStreaming = _dirs.FindIndex(d => d == "StreamingAssets");
            if (_indexStreaming >= 0)
                return _Entry(path, $"{ProjectPaths.STREAMING_ASSETS}/{string.Join("/", _segments.Skip(_indexStreaming + 1))}", MoveCategory.StreamingAssets, "Gộp vào StreamingAssets", false);
            if (_dirs.Contains("Editor Default Resources") || _dirs.Contains("Gizmos"))
                return null;
            if (_extension.Equals(".asmdef", StringComparison.OrdinalIgnoreCase) || _extension.Equals(".asmref", StringComparison.OrdinalIgnoreCase))
            {
                plan.warnings.Add($"Bỏ qua {path}: assembly definition, chuyển script ra ngoài sẽ đổi assembly. Hãy xử lý tay.");
                return null;
            }
            if (_IsEnvironmentFile(_fileName, rules))
                return _Entry(path, $"{ProjectPaths.ENVIRONMENT}/{_GetEnvironmentSubFolder(_fileName)}/{_fileName}", MoveCategory.Environment, "File môi trường", false);
            if (IsServiceAccountKey(path))
                return _Entry(path, $"{ProjectPaths.ENVIRONMENT}/Keys/{_fileName}", MoveCategory.Environment, "Khoá service account (không commit git)", false);
            int _indexPlugins = _dirs.IndexOf("Plugins");
            if (_indexPlugins >= 0)
                return _Entry(path, $"Assets/Plugins/{string.Join("/", _segments.Skip(_indexPlugins + 1))}", MoveCategory.Base, "Plugin native -> Assets/Plugins", false);

            // Tiền tố: thư mục gốc phụ (01_BirdSort, LongTH...) giữ tên để dễ truy lại
            string _root = _dirs.Count > 0 ? _dirs[0] : string.Empty;
            var _dirsSub = _dirs.Skip(1).ToList();
            if (_dirs.Count > 0 && !_root.Equals(rules.mainBaseFolder, StringComparison.OrdinalIgnoreCase))
                _dirsSub.Insert(0, _root);
            bool _isEditor = _dirs.Contains("Editor");

            if (_extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                string _category = _isEditor ? ProjectPaths.FOLDER_EDITOR : GetScriptCategory(path);
                var _sub = _dirsSub.Where(d => !_NAME_FOLDER_STRIP_DEPLOY.Contains(d)).ToList();
                return _Entry(path, _Join(ProjectPaths.BASE_DEPLOY, _category, _sub, _fileName), MoveCategory.Base, "Script -> " + _category, false);
            }
            if (_extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
            {
                var _sub = _dirsSub.Where(d => !_NAME_FOLDER_STRIP_DEPLOY.Contains(d)).ToList();
                return _Entry(path, _Join(ProjectPaths.BASE_DEPLOY, "Docs", _sub, _fileName), MoveCategory.Base, "Tài liệu", false);
            }

            string _typeFolder = GetResTypeFolder(_extension);
            var _subRes = new List<string>(_dirsSub);
            int _indexType = _subRes.FindIndex(d => _NAME_FOLDER_TYPE_RES.Contains(d));
            if (_indexType >= 0 && _indexType <= 1)
                _subRes.RemoveAt(_indexType);
            return _Entry(path, _Join(ProjectPaths.BASE_RES, _typeFolder, _subRes, _fileName), MoveCategory.Base, "Tài nguyên -> " + _typeFolder, false);
        }

        /// <summary>Thư mục trong Res theo đuôi file.</summary>
        public static string GetResTypeFolder(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".unity": return "Scenes";
                case ".prefab": return "Prefabs";
                case ".png": case ".jpg": case ".jpeg": case ".psd": case ".tga": case ".tif": case ".tiff":
                case ".exr": case ".hdr": case ".gif": case ".bmp": case ".svg": case ".spriteatlas": case ".spriteatlasv2":
                    return "Textures";
                case ".wav": case ".mp3": case ".ogg": case ".aif": case ".aiff": case ".mixer":
                    return "Audio";
                case ".anim": case ".controller": case ".overridecontroller": case ".playable": case ".mask":
                    return "Animations";
                case ".mat": case ".physicmaterial": case ".physicsmaterial2d":
                    return "Materials";
                case ".ttf": case ".otf": case ".fontsettings":
                    return "Fonts";
                case ".fbx": case ".obj": case ".blend": case ".mesh":
                    return "Models";
                case ".shader": case ".cginc": case ".hlsl": case ".compute": case ".shadergraph": case ".shadersubgraph":
                    return "Shaders";
                case ".mp4": case ".webm": case ".mov":
                    return "Videos";
                case ".dll": case ".aar": case ".jar": case ".so": case ".a": case ".bundle": case ".framework":
                    return "Plugins";
                case ".asset":
                    return "Data";
                case ".json": case ".txt": case ".bytes": case ".csv": case ".xml": case ".xlsx":
                    return "Data/Text";
                default:
                    return "Other";
            }
        }

        #endregion

        #region Phân loại script Model / View / Controller

        private static readonly string[] _SUFFIX_CONTROLLER = { "Controller", "Manager", "Service", "System", "Handler", "Factory", "Loader", "Builder" };
        private static readonly string[] _SUFFIX_VIEW = { "Box", "UI", "View", "Page", "Panel", "Popup", "Button", "Btn", "Item", "Icon", "Frame", "Farme", "Bar", "Cell", "Slot", "Text", "Anim", "Effect", "FX", "Element", "Elm", "Scroll", "Banner", "Tab" };
        private static readonly string[] _SUFFIX_MODEL = { "Data", "Database", "Config", "Configs", "Model", "Info", "Setting", "Settings", "Profile", "Enum", "Enums", "Const", "Constants", "Catalog", "Flags", "Type", "Types", "Id", "Ids", "Define", "Defines" };

        /// <summary>Model / View / Controller cho 1 script (dựa vào kiểu đã biên dịch + tên + đường dẫn).</summary>
        public static string GetScriptCategory(string path)
        {
            string _fileName = Path.GetFileNameWithoutExtension(path);
            string _baseName = _fileName.Split('.')[0]; // Partial class: UseProfile.Setting -> UseProfile
            var _script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Type _type = _script != null ? _script.GetClass() : null;
            if (_type == null)
                _type = _FindType(_baseName);

            if (_EndsWithAny(_baseName, _SUFFIX_CONTROLLER))
                return ProjectPaths.FOLDER_CONTROLLER;

            if (_type != null)
            {
                if (typeof(ScriptableObject).IsAssignableFrom(_type))
                    return ProjectPaths.FOLDER_MODEL;
                if (typeof(MonoBehaviour).IsAssignableFrom(_type))
                {
                    if (_IsView(_type, _baseName, path))
                        return ProjectPaths.FOLDER_VIEW;
                    return _EndsWithAny(_baseName, _SUFFIX_MODEL) || _baseName.Contains("Profile") ? ProjectPaths.FOLDER_MODEL : ProjectPaths.FOLDER_CONTROLLER;
                }
                if (_type.IsEnum || _EndsWithAny(_baseName, _SUFFIX_MODEL) || (_type.IsSerializable && !(_type.IsAbstract && _type.IsSealed)))
                    return ProjectPaths.FOLDER_MODEL;
                return ProjectPaths.FOLDER_CONTROLLER;
            }

            // Không tìm được kiểu: đọc chữ trong file
            string _text = _script != null ? _script.text : (File.Exists(path) ? File.ReadAllText(path) : string.Empty);
            if (Regex.IsMatch(_text, @"\benum\s+\w+") && !Regex.IsMatch(_text, @"\bclass\s+\w+"))
                return ProjectPaths.FOLDER_MODEL;
            var _matchBase = Regex.Match(_text, @"class\s+\w+\s*(?:<[^>]*>)?\s*:\s*([\w\.]+)");
            string _nameBase = _matchBase.Success ? _matchBase.Groups[1].Value : string.Empty;
            if (_nameBase.EndsWith("ScriptableObject"))
                return ProjectPaths.FOLDER_MODEL;
            if (_EndsWithAny(_nameBase, _SUFFIX_VIEW) || _EndsWithAny(_baseName, _SUFFIX_VIEW) || _IsViewPath(path))
                return ProjectPaths.FOLDER_VIEW;
            if (_EndsWithAny(_baseName, _SUFFIX_MODEL) || _baseName.Contains("Profile"))
                return ProjectPaths.FOLDER_MODEL;
            return ProjectPaths.FOLDER_CONTROLLER;
        }

        private static bool _IsView(Type type, string baseName, string path)
        {
            if (typeof(UnityEngine.EventSystems.UIBehaviour).IsAssignableFrom(type))
                return true;
            for (var _t = type; _t != null; _t = _t.BaseType)
                if (_t.Name == "BaseBox" || _t.Name == "PageBase" || _t.Name == "BaseButtonEvent")
                    return true;
            return _EndsWithAny(baseName, _SUFFIX_VIEW) || _IsViewPath(path);
        }

        private static bool _IsViewPath(string path)
        {
            return path.Contains("/UI/") || path.Contains("/Popups/") || path.Contains("/View/") || path.Contains("/Views/") || path.Contains("/Page/");
        }

        private static bool _EndsWithAny(string name, string[] suffixes)
        {
            foreach (var _suffix in suffixes)
                if (!string.IsNullOrEmpty(_suffix) && name.EndsWith(_suffix, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static readonly Dictionary<string, Type> _cacheType = new Dictionary<string, Type>();

        private static Type _FindType(string name)
        {
            if (_cacheType.TryGetValue(name, out var _cached))
                return _cached;
            Type _found = null;
            foreach (var _assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string _nameAssembly = _assembly.GetName().Name;
                if (!_nameAssembly.StartsWith("Assembly-CSharp"))
                    continue;
                _found = _assembly.GetTypes().FirstOrDefault(t => t.Name == name);
                if (_found != null)
                    break;
            }
            _cacheType[name] = _found;
            return _found;
        }

        #endregion

        #region Asset không dùng

        private static void _MarkUnused(MovePlan plan, Func<string, float, bool> onProgress)
        {
            onProgress?.Invoke("Tính phụ thuộc từ scene build, Resources, ProjectSettings...", 0.65f);
            var _roots = new HashSet<string>();
            foreach (var _scene in EditorBuildSettings.scenes)
                if (_scene.enabled && !string.IsNullOrEmpty(_scene.path))
                    _roots.Add(_scene.path);

            foreach (var _path in AssetDatabase.GetAllAssetPaths())
            {
                if (!_path.StartsWith(_ASSETS))
                    continue;
                if (_path.Contains("/Resources/") || _path.StartsWith(ProjectPaths.STREAMING_ASSETS + "/") || _path.Contains("/Editor/")
                    || _path.EndsWith(".cs"))
                    _roots.Add(_path);
            }
            foreach (var _bundle in AssetDatabase.GetAllAssetBundleNames())
                foreach (var _path in AssetDatabase.GetAssetPathsFromAssetBundle(_bundle))
                    _roots.Add(_path);

            // GUID nhắc tới trong ProjectSettings (icon, splash, shader luôn include...)
            foreach (var _file in Directory.GetFiles("ProjectSettings", "*.asset"))
            {
                foreach (Match _match in Regex.Matches(File.ReadAllText(_file), @"guid:\s*([0-9a-f]{32})"))
                {
                    string _path = AssetDatabase.GUIDToAssetPath(_match.Groups[1].Value);
                    if (!string.IsNullOrEmpty(_path))
                        _roots.Add(_path);
                }
            }

            onProgress?.Invoke("Tính phụ thuộc...", 0.75f);
            var _used = new HashSet<string>(AssetDatabase.GetDependencies(_roots.ToArray(), true));

            foreach (var _entry in plan.entries)
            {
                if (_entry.category != MoveCategory.Base || _entry.isFolder)
                    continue;
                if (_EXT_SKIP_UNUSED.Contains(Path.GetExtension(_entry.pathFrom)))
                    continue;
                if (_entry.pathFrom.Contains("/Editor/") || _entry.pathFrom.Contains("/Plugins/") || _used.Contains(_entry.pathFrom))
                    continue;
                if (_IsEditorOnlyAsset(_entry.pathFrom))
                    continue;
                _entry.category = MoveCategory.Unused;
                _entry.pathTo = ProjectPaths.TO_DELETE + "/" + _entry.pathFrom.Substring(_ASSETS.Length);
                _entry.reason = "Không được scene build / Resources / asset nào dùng";
            }
        }

        /// <summary>Asset có kiểu nằm trong assembly Editor (setting của tool) -> không bao giờ được scene tham chiếu nhưng vẫn đang dùng.</summary>
        private static bool _IsEditorOnlyAsset(string path)
        {
            if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return false;
            var _type = AssetDatabase.GetMainAssetTypeAtPath(path);
            string _nameAssembly = _type?.Assembly.GetName().Name ?? string.Empty;
            return _nameAssembly.Contains("Editor");
        }

        /// <summary>File .json là khoá service account Google (chứa private key).</summary>
        public static bool IsServiceAccountKey(string path)
        {
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                return false;
            try
            {
                using (var _reader = new StreamReader(path))
                {
                    var _buffer = new char[2048];
                    int _count = _reader.Read(_buffer, 0, _buffer.Length);
                    string _head = new string(_buffer, 0, _count);
                    return _head.Contains("\"service_account\"") && _head.Contains("private_key");
                }
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static void _MarkConflicts(MovePlan plan)
        {
            var _countTarget = plan.entries.GroupBy(e => e.pathTo, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key);
            var _setDuplicate = new HashSet<string>(_countTarget, StringComparer.OrdinalIgnoreCase);
            foreach (var _entry in plan.entries)
            {
                bool _isExisting = _entry.pathTo != _entry.pathFrom && (File.Exists(_entry.pathTo) || Directory.Exists(_entry.pathTo));
                if (!_setDuplicate.Contains(_entry.pathTo) && !_isExisting)
                    continue;
                _entry.reason = (_isExisting ? "Đích đã có file: " : "Trùng đích với file khác: ") + _entry.reason;
                _entry.category = MoveCategory.Conflict;
                _entry.isSelected = false;
            }
        }

        #endregion

        #region Thực hiện / hoàn tác

        [Serializable]
        private class MoveHistory
        {
            public string time;
            public List<MoveEntry> moved = new List<MoveEntry>();
        }

        /// <summary>Di chuyển các mục được chọn. Trả về (số thành công, danh sách lỗi).</summary>
        public static (int countDone, List<string> errors) Apply(MovePlan plan, Func<string, float, bool> onProgress = null)
        {
            var _errors = new List<string>();
            var _history = new MoveHistory { time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            var _selected = plan.entries.Where(e => e.isSelected && e.category != MoveCategory.Conflict && e.pathFrom != e.pathTo).ToList();
            var _sourceRoots = new HashSet<string>(_selected.Select(e => _GetRootFolder(e.pathFrom)).Where(r => !string.IsNullOrEmpty(r)));

            // Tạo thư mục đích trước (ngoài StartAssetEditing để MoveAsset thấy được thư mục)
            foreach (var _folder in _selected.Select(e => Path.GetDirectoryName(e.pathTo)?.Replace('\\', '/')).Distinct())
                ProjectPaths.EnsureFolder(_folder);

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < _selected.Count; i++)
                {
                    var _entry = _selected[i];
                    if (onProgress != null && i % 20 == 0 && onProgress("Chuyển " + _entry.pathFrom, (float)i / _selected.Count))
                        break;
                    string _error = AssetDatabase.MoveAsset(_entry.pathFrom, _entry.pathTo);
                    if (string.IsNullOrEmpty(_error))
                        _history.moved.Add(_entry);
                    else
                        _errors.Add($"{_entry.pathFrom} -> {_entry.pathTo}: {_error}");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (var _root in _sourceRoots)
                _DeleteEmptyFolders(_root);
            _FixKeystorePath(_history.moved);
            CreateFolderSkeleton();
            File.WriteAllText(_PATH_HISTORY, JsonUtility.ToJson(_history, true), new UTF8Encoding(false));
            _WriteToDeleteReadme(_history.moved);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return (_history.moved.Count, _errors);
        }

        public static bool IsHasHistory => File.Exists(_PATH_HISTORY);

        /// <summary>Chuyển ngược lại lần tái cấu trúc gần nhất.</summary>
        public static (int countDone, List<string> errors) RevertLast()
        {
            var _errors = new List<string>();
            if (!File.Exists(_PATH_HISTORY))
                return (0, _errors);
            var _history = JsonUtility.FromJson<MoveHistory>(File.ReadAllText(_PATH_HISTORY));
            foreach (var _folder in _history.moved.Select(e => Path.GetDirectoryName(e.pathFrom)?.Replace('\\', '/')).Distinct())
                ProjectPaths.EnsureFolder(_folder);

            int _count = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = _history.moved.Count - 1; i >= 0; i--)
                {
                    var _entry = _history.moved[i];
                    string _error = AssetDatabase.MoveAsset(_entry.pathTo, _entry.pathFrom);
                    if (string.IsNullOrEmpty(_error))
                        _count++;
                    else
                        _errors.Add($"{_entry.pathTo} -> {_entry.pathFrom}: {_error}");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            foreach (var _root in new[] { ProjectPaths.BASE, ProjectPaths.THIRD_PARTY, ProjectPaths.ENVIRONMENT, ProjectPaths.TO_DELETE })
                _DeleteEmptyFolders(_root);
            _FixKeystorePath(_history.moved.Select(e => new MoveEntry { pathFrom = e.pathTo, pathTo = e.pathFrom }).ToList());
            File.Move(_PATH_HISTORY, _PATH_HISTORY + ".reverted_" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            AssetDatabase.Refresh();
            return (_count, _errors);
        }

        /// <summary>
        /// Sửa các mục mà bản tái cấu trúc đầu tiên xếp sai (dựa vào lịch sử, cập nhật lại lịch sử để Hoàn tác vẫn đúng):
        ///   - Assets/Scripts/UnityPurchasing: trả về chỗ cũ (Unity IAP Obfuscator luôn sinh vào đó, để chỗ khác sẽ trùng class).
        ///   - "Asset refs in scenes": gom về 03_ThirdParty (cả Editor/Resources, trước đó lọt vào Assets/Resources của bản build).
        ///   - File trong thư mục Plugins (jar, .m, .h): về Assets/Plugins (file native iOS bị đánh dấu nhầm là không dùng).
        ///   - FeatureSettings.asset (setting Editor, bị coi là không dùng): về 01_Base/Res/Data/Settings.
        ///   - Asset có kiểu thuộc assembly Editor nằm trong _ToDelete (vd IAPDatabase dùng bởi Pack Manager): trả về 01_Base/Res.
        ///   - Khoá service account .json: chuyển vào 04_Environment/Keys.
        /// </summary>
        public static (int countDone, List<string> logs) FixAfterRestructure()
        {
            var _logs = new List<string>();
            if (!File.Exists(_PATH_HISTORY))
            {
                _logs.Add("Không có ProjectStructureHistory.json.");
                return (0, _logs);
            }
            var _history = JsonUtility.FromJson<MoveHistory>(File.ReadAllText(_PATH_HISTORY));
            var _lsFix = new List<(MoveEntry entry, string pathNew, string reason)>();
            var _setDone = new HashSet<string>();
            foreach (var _entry in _history.moved)
            {
                if (!_setDone.Add(_entry.pathFrom) || !File.Exists(_entry.pathTo))
                    continue;
                string _relative = _entry.pathFrom.Substring(_ASSETS.Length);
                string _fileName = Path.GetFileName(_entry.pathFrom);
                string _pathNew = null, _reason = null;
                int _indexPlugins = _entry.pathFrom.IndexOf("/Plugins/", StringComparison.Ordinal);

                if (_entry.pathFrom.StartsWith("Assets/Scripts/UnityPurchasing/"))
                    (_pathNew, _reason) = (_entry.pathFrom, "Unity IAP sinh file cố định ở đây");
                else if (_entry.pathFrom.StartsWith("Assets/Asset refs in scenes/"))
                    (_pathNew, _reason) = ($"{ProjectPaths.THIRD_PARTY}/{_relative}", "Tool bên thứ 3");
                else if (_indexPlugins >= 0 && _entry.category != MoveCategory.ThirdParty)
                    (_pathNew, _reason) = ("Assets/Plugins/" + _entry.pathFrom.Substring(_indexPlugins + "/Plugins/".Length), "Plugin native");
                else if (_fileName == "FeatureSettings.asset")
                    (_pathNew, _reason) = (FEATURE_SETTINGS_PATH, "Setting Editor");
                else if (IsServiceAccountKey(_entry.pathTo))
                    (_pathNew, _reason) = ($"{ProjectPaths.ENVIRONMENT}/Keys/{_fileName}", "Khoá service account");
                else if (_entry.pathTo.StartsWith(ProjectPaths.TO_DELETE + "/") && (_IsEditorOnlyAsset(_entry.pathTo) || _fileName == "IAPDatabase.asset"))
                    (_pathNew, _reason) = ($"{ProjectPaths.BASE_RES}/Data/{_fileName}", "Asset dùng bởi tool Editor");

                if (_pathNew == null || _pathNew == _entry.pathTo)
                    continue;
                _lsFix.Add((_entry, _pathNew, _reason));
            }

            foreach (var _folder in _lsFix.Select(f => Path.GetDirectoryName(f.pathNew)?.Replace('\\', '/')).Distinct())
                ProjectPaths.EnsureFolder(_folder);

            int _count = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var _fix in _lsFix)
                {
                    if (File.Exists(_fix.pathNew))
                    {
                        _logs.Add($"Bỏ qua (đích đã có file): {_fix.entry.pathTo} -> {_fix.pathNew}");
                        continue;
                    }
                    string _error = AssetDatabase.MoveAsset(_fix.entry.pathTo, _fix.pathNew);
                    if (!string.IsNullOrEmpty(_error))
                    {
                        _logs.Add($"Lỗi {_fix.entry.pathTo} -> {_fix.pathNew}: {_error}");
                        continue;
                    }
                    _logs.Add($"[{_fix.reason}] {_fix.entry.pathTo} -> {_fix.pathNew}");
                    foreach (var _same in _history.moved.Where(e => e.pathFrom == _fix.entry.pathFrom))
                    {
                        _same.pathTo = _fix.pathNew;
                        if (_same.category == MoveCategory.Unused)
                            _same.category = MoveCategory.Base;
                    }
                    _count++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (var _root in new[] { ProjectPaths.BASE, ProjectPaths.TO_DELETE, "Assets/Resources/UI" })
                _DeleteEmptyFolders(_root);
            File.WriteAllText(_PATH_HISTORY, JsonUtility.ToJson(_history, true), new UTF8Encoding(false));
            _WriteToDeleteReadme(_history.moved);
            AssetDatabase.Refresh();
            return (_count, _logs);
        }

        private const string FEATURE_SETTINGS_PATH = ProjectPaths.SETTINGS + "/FeatureSettings.asset";

        /// <summary>Tạo khung thư mục chuẩn (Res / Deploy MVC) cho 01_Base và 02_Gameplay.</summary>
        public static void CreateFolderSkeleton()
        {
            string[] _resFolders = { "Prefabs", "Textures", "Audio", "Animations", "Materials", "Fonts", "Scenes", "Data" };
            string[] _deployFolders = { ProjectPaths.FOLDER_MODEL, ProjectPaths.FOLDER_VIEW, ProjectPaths.FOLDER_CONTROLLER, ProjectPaths.FOLDER_EDITOR };
            foreach (var _root in new[] { (ProjectPaths.BASE_RES, ProjectPaths.BASE_DEPLOY), (ProjectPaths.GAMEPLAY_RES, ProjectPaths.GAMEPLAY_DEPLOY) })
            {
                foreach (var _folder in _resFolders)
                    _EnsureFolderKeep(_root.Item1 + "/" + _folder);
                foreach (var _folder in _deployFolders)
                    _EnsureFolderKeep(_root.Item2 + "/" + _folder);
            }
            _EnsureFolderKeep(ProjectPaths.THIRD_PARTY);
            _EnsureFolderKeep(ProjectPaths.ENVIRONMENT);

            string _readme = ProjectPaths.GAMEPLAY + "/README.md";
            if (!File.Exists(_readme))
            {
                File.WriteAllText(_readme,
                    "# 02_Gameplay\n\nCore gameplay của từng game (thêm sau, tách khỏi 01_Base).\n\n" +
                    "- `Res/`: tài nguyên (prefab, texture, audio, animation, scene...).\n" +
                    "- `Deploy/Model`: dữ liệu (level, cấu hình, ScriptableObject).\n" +
                    "- `Deploy/View`: hiển thị (MonoBehaviour gắn object, UI).\n" +
                    "- `Deploy/Controller`: logic gameplay (kế thừa GamePlayController, ItemBase, CollectItemBase...).\n" +
                    "- `Deploy/Editor`: tool editor riêng của gameplay.\n", new UTF8Encoding(true));
            }
        }

        /// <summary>Tạo thư mục + file .gitkeep (Unity bỏ qua file bắt đầu bằng dấu chấm) để git giữ thư mục rỗng.</summary>
        private static void _EnsureFolderKeep(string pathFolder)
        {
            ProjectPaths.EnsureFolder(pathFolder);
            string _keep = pathFolder + "/.gitkeep";
            if (Directory.GetFileSystemEntries(pathFolder).Length == 0 && !File.Exists(_keep))
                File.WriteAllText(_keep, string.Empty);
        }

        private static void _FixKeystorePath(List<MoveEntry> moved)
        {
            var _map = moved.Where(e => e.pathFrom.EndsWith(".keystore") || e.pathFrom.EndsWith(".jks")).ToDictionary(e => e.pathFrom, e => e.pathTo);
            if (_map.Count == 0)
                return;
            var _config = ProjectPaths.FindAsset<BuildConfig>(BuildConfig.PATH_ASSET);
            if (_config != null && _map.TryGetValue(_config.android.keystorePath, out var _newPath))
            {
                _config.android.keystorePath = _newPath;
                EditorUtility.SetDirty(_config);
            }
            string _keystorePlayer = PlayerSettings.Android.keystoreName?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(_keystorePlayer))
            {
                foreach (var _pair in _map)
                {
                    if (_keystorePlayer.EndsWith(_pair.Key))
                    {
                        PlayerSettings.Android.keystoreName = _keystorePlayer.Substring(0, _keystorePlayer.Length - _pair.Key.Length) + _pair.Value;
                        break;
                    }
                }
            }
        }

        private static void _WriteToDeleteReadme(List<MoveEntry> moved)
        {
            var _unused = moved.Where(e => e.category == MoveCategory.Unused).ToList();
            if (_unused.Count == 0 || !AssetDatabase.IsValidFolder(ProjectPaths.TO_DELETE))
                return;
            var _text = new StringBuilder();
            _text.AppendLine("# _ToDelete");
            _text.AppendLine();
            _text.AppendLine("Asset không được scene build / Resources / StreamingAssets / AssetBundle / ProjectSettings / script nào tham chiếu.");
            _text.AppendLine("Chạy thử game (cả build) rồi xoá thư mục này. Cần lấy lại: Tools > Base Project > Project Structure > Hoàn tác.");
            _text.AppendLine();
            foreach (var _entry in _unused)
                _text.AppendLine($"- {_entry.pathFrom}");
            File.WriteAllText(ProjectPaths.TO_DELETE + "/README.md", _text.ToString(), new UTF8Encoding(true));
        }

        private static void _DeleteEmptyFolders(string pathRoot)
        {
            if (string.IsNullOrEmpty(pathRoot) || !Directory.Exists(pathRoot))
                return;
            foreach (var _dir in Directory.GetDirectories(pathRoot))
                _DeleteEmptyFolders(_dir.Replace('\\', '/'));
            bool _isEmpty = Directory.GetFiles(pathRoot).All(f => f.EndsWith(".meta") || Path.GetFileName(f).StartsWith(".")) && Directory.GetDirectories(pathRoot).Length == 0;
            if (_isEmpty && pathRoot != "Assets")
            {
                bool _isSkeleton = pathRoot.StartsWith(ProjectPaths.BASE + "/") || pathRoot.StartsWith(ProjectPaths.GAMEPLAY + "/") || pathRoot == ProjectPaths.THIRD_PARTY || pathRoot == ProjectPaths.ENVIRONMENT;
                if (!_isSkeleton || !File.Exists(pathRoot + "/.gitkeep"))
                    AssetDatabase.DeleteAsset(pathRoot);
            }
        }

        #endregion

        #region CSV

        public static string ToCsv(MovePlan plan)
        {
            var _text = new StringBuilder("selected,category,from,to,reason\n");
            foreach (var _entry in plan.entries)
                _text.AppendLine($"{(_entry.isSelected ? 1 : 0)},{_entry.category},{_Csv(_entry.pathFrom)},{_Csv(_entry.pathTo)},{_Csv(_entry.reason)}");
            return _text.ToString();
        }

        /// <summary>Đọc lại CSV (đã sửa cột to / selected) vào kế hoạch hiện có (khớp theo cột from).</summary>
        public static int ApplyCsv(MovePlan plan, string csv)
        {
            var _dic = plan.entries.ToDictionary(e => e.pathFrom, e => e);
            int _count = 0;
            foreach (var _line in csv.Split('\n').Skip(1))
            {
                var _cells = _ParseCsvLine(_line.TrimEnd('\r'));
                if (_cells.Count < 4 || !_dic.TryGetValue(_cells[2], out var _entry))
                    continue;
                _entry.isSelected = _cells[0] == "1";
                _entry.pathTo = _cells[3];
                _count++;
            }
            _MarkConflicts(plan);
            return _count;
        }

        private static string _Csv(string value)
        {
            value = value ?? string.Empty;
            return value.Contains(",") || value.Contains("\"") ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        private static List<string> _ParseCsvLine(string line)
        {
            var _cells = new List<string>();
            var _cell = new StringBuilder();
            bool _isQuote = false;
            for (int i = 0; i < line.Length; i++)
            {
                char _c = line[i];
                if (_isQuote)
                {
                    if (_c == '"' && i + 1 < line.Length && line[i + 1] == '"') { _cell.Append('"'); i++; }
                    else if (_c == '"') _isQuote = false;
                    else _cell.Append(_c);
                }
                else if (_c == '"') _isQuote = true;
                else if (_c == ',') { _cells.Add(_cell.ToString()); _cell.Clear(); }
                else _cell.Append(_c);
            }
            _cells.Add(_cell.ToString());
            return _cells;
        }

        #endregion

        #region Helper

        private static MoveEntry _Entry(string from, string to, MoveCategory category, string reason, bool isFolder)
        {
            return new MoveEntry { pathFrom = from, pathTo = to, category = category, reason = reason, isFolder = isFolder, isSelected = true };
        }

        private static string _Join(string root, string category, List<string> sub, string fileName)
        {
            var _parts = new List<string> { root };
            if (!string.IsNullOrEmpty(category))
                _parts.Add(category);
            _parts.AddRange(sub.Where(s => !string.IsNullOrEmpty(s)));
            _parts.Add(fileName);
            return string.Join("/", _parts);
        }

        private static string _GetRootFolder(string path)
        {
            if (!path.StartsWith(_ASSETS))
                return null;
            var _segments = path.Substring(_ASSETS.Length).Split('/');
            return _segments.Length > 1 ? _ASSETS + _segments[0] : null;
        }

        private static bool _IsEnvironmentFile(string fileName, ProjectStructureRules rules)
        {
            foreach (var _pattern in rules.environmentPatterns)
            {
                if (string.IsNullOrEmpty(_pattern))
                    continue;
                if (_pattern.StartsWith(".") ? fileName.EndsWith(_pattern, StringComparison.OrdinalIgnoreCase) : fileName.StartsWith(_pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string _GetEnvironmentSubFolder(string fileName)
        {
            string _lower = fileName.ToLowerInvariant();
            if (_lower.EndsWith(".keystore") || _lower.EndsWith(".jks"))
                return "KeyStore";
            if (_lower.StartsWith("googleservice-info") || _lower.EndsWith(".p12") || _lower.EndsWith(".mobileprovision"))
                return "iOS";
            return "Android";
        }

        #endregion
    }
}
