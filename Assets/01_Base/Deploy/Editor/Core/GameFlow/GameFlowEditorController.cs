using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BaseProject.EditorTools.GameFlow
{
    [Serializable]
    public class PageCreateSetting
    {
        public string namePage = "Event";
        public string folderScript = ProjectPaths.BASE_DEPLOY + "/View/Home/Pages";
        public bool isAddToScene = true;

        public string NameClass => namePage + "Page";
    }

    /// <summary>
    /// Controller editor của Game Flow: tạo / tìm GameFlowConfig, tạo page mới
    /// (thêm PageName, sinh script {Name}Page : PageBase, dựng object page + nút tab trong scene Home đang mở).
    /// </summary>
    [InitializeOnLoad]
    public static class GameFlowEditorController
    {
        public const string MARKER_ENUM = "[PAGE_GENERATOR]";
        private const string _PATH_CONFIG_DEFAULT = "Assets/Resources/Config/GameFlowConfig.asset";
        private const string _KEY_PENDING = "BaseProject.GameFlow.PendingPage";

        static GameFlowEditorController()
        {
            EditorApplication.delayCall += ProcessPending;
        }

        #region Config

        public static GameFlowConfig GetOrCreateConfig()
        {
            var _guid = AssetDatabase.FindAssets("t:GameFlowConfig").FirstOrDefault();
            if (!string.IsNullOrEmpty(_guid))
                return AssetDatabase.LoadAssetAtPath<GameFlowConfig>(AssetDatabase.GUIDToAssetPath(_guid));

            _CreateFolder(Path.GetDirectoryName(_PATH_CONFIG_DEFAULT)?.Replace('\\', '/'));
            var _config = ScriptableObject.CreateInstance<GameFlowConfig>();
            foreach (PageName _page in Enum.GetValues(typeof(PageName)))
                _config.pages.Add(new HomePageSetting { page = _page, isEnable = true });
            AssetDatabase.CreateAsset(_config, _PATH_CONFIG_DEFAULT);
            AssetDatabase.SaveAssets();
            return _config;
        }

        #endregion

        #region Scene

        public static PageController FindPageControllerInScene()
        {
            return Object.FindObjectsByType<PageController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        }

        public static PageBase FindPageInScene(PageName page)
        {
            return Object.FindObjectsByType<PageBase>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(p => p.idPage == page);
        }

        /// <summary>Class PageBase có dùng "PageName.{page}" trong code (đoán class của page).</summary>
        public static Type FindPageClass(PageName page)
        {
            var _inScene = FindPageInScene(page);
            if (_inScene != null)
                return _inScene.GetType();
            return TypeCache.GetTypesDerivedFrom<PageBase>().FirstOrDefault(t => t.Name == page + "Page");
        }

        #endregion

        #region Tạo page

        public static string Validate(PageCreateSetting setting)
        {
            if (string.IsNullOrEmpty(setting.namePage) || !Regex.IsMatch(setting.namePage, @"^[A-Z][A-Za-z0-9]*$"))
                return "Tên page phải dạng PascalCase (bắt đầu bằng chữ hoa, chỉ chữ và số). Ví dụ: Event, Collection.";
            if (Enum.GetNames(typeof(PageName)).Contains(setting.namePage))
                return $"PageName.{setting.namePage} đã tồn tại.";
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(setting.NameClass, false) != null))
                return $"Class {setting.NameClass} đã tồn tại.";
            if (!setting.folderScript.StartsWith("Assets"))
                return "Thư mục script phải nằm trong Assets.";
            if (string.IsNullOrEmpty(_GetPathScript(typeof(PageController))))
                return "Không tìm thấy PageController.cs.";
            return string.Empty;
        }

        public static bool CreatePage(PageCreateSetting setting)
        {
            string _error = Validate(setting);
            if (!string.IsNullOrEmpty(_error))
            {
                EditorUtility.DisplayDialog("Tạo page", _error, "OK");
                return false;
            }

            string _pathEnum = _GetPathScript(typeof(PageController));
            int _value = Enum.GetValues(typeof(PageName)).Cast<int>().Max() + 1;
            foreach (Match _match in Regex.Matches(File.ReadAllText(_pathEnum), @"=\s*(\d+)\s*,"))
                _value = Mathf.Max(_value, int.Parse(_match.Groups[1].Value) + 1);

            string _text = File.ReadAllText(_pathEnum);
            int _indexMarker = _text.IndexOf(MARKER_ENUM, StringComparison.Ordinal);
            if (_indexMarker < 0)
            {
                EditorUtility.DisplayDialog("Tạo page", $"Không thấy dòng '// {MARKER_ENUM}' trong enum PageName.", "OK");
                return false;
            }
            string _newLine = _text.Contains("\r\n") ? "\r\n" : "\n";
            int _indexLine = _text.LastIndexOf('\n', _indexMarker) + 1;
            _text = _text.Insert(_indexLine, $"    {setting.namePage} = {_value},{_newLine}");
            File.WriteAllText(_pathEnum, _text, new UTF8Encoding(true));

            string _pathScript = $"{setting.folderScript.TrimEnd('/')}/{setting.NameClass}.cs";
            _CreateFolder(setting.folderScript.TrimEnd('/'));
            File.WriteAllText(_pathScript, GenerateScript(setting), new UTF8Encoding(true));

            SessionState.SetString(_KEY_PENDING, JsonUtility.ToJson(setting));
            AssetDatabase.ImportAsset(_pathEnum, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(_pathScript, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[GameFlow] Đã thêm PageName.{setting.namePage} = {_value} và {_pathScript}. Chờ biên dịch...");
            return true;
        }

        public static string GenerateScript(PageCreateSetting setting)
        {
            var _code = new StringBuilder();
            _code.AppendLine("using UnityEngine;");
            _code.AppendLine();
            _code.AppendLine("/// <summary>");
            _code.AppendLine($"/// Page {setting.namePage} ở màn Home (PageName.{setting.namePage}). Bật / tắt: Tools > Base Project > Game Flow.");
            _code.AppendLine("/// </summary>");
            _code.AppendLine($"public class {setting.NameClass} : PageBase");
            _code.AppendLine("{");
            _code.AppendLine("    /// <summary>Khởi tạo 1 lần khi vào Home.</summary>");
            _code.AppendLine("    public override void Init(PageController controller)");
            _code.AppendLine("    {");
            _code.AppendLine("        base.Init(controller);");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    /// <summary>Làm mới dữ liệu (gọi khi Init và mỗi lần chọn page).</summary>");
            _code.AppendLine("    public override void InitState()");
            _code.AppendLine("    {");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    public override void OnChoicePage()");
            _code.AppendLine("    {");
            _code.AppendLine("        base.OnChoicePage();");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    public override void OnHidePage()");
            _code.AppendLine("    {");
            _code.AppendLine("        base.OnHidePage();");
            _code.AppendLine("    }");
            _code.AppendLine("}");
            return _code.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        public static void ProcessPending()
        {
            string _json = SessionState.GetString(_KEY_PENDING, string.Empty);
            if (string.IsNullOrEmpty(_json))
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ProcessPending;
                return;
            }

            var _setting = JsonUtility.FromJson<PageCreateSetting>(_json);
            var _type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(_setting.NameClass, false)).FirstOrDefault(t => t != null);
            if (_type == null || !Enum.TryParse(_setting.namePage, out PageName _page))
            {
                Debug.LogWarning($"[GameFlow] Chưa thấy {_setting.NameClass} / PageName.{_setting.namePage} (đang lỗi biên dịch?). Sửa lỗi xong sẽ tự chạy tiếp.");
                return;
            }
            SessionState.EraseString(_KEY_PENDING);

            var _config = GetOrCreateConfig();
            _config.SetPageEnable(_page, true);
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssetIfDirty(_config);

            if (!_setting.isAddToScene)
            {
                Debug.Log($"[GameFlow] Đã tạo page {_setting.NameClass}.");
                return;
            }
            var _pageObject = AddPageToScene(_type, _page);
            Debug.Log(_pageObject != null
                ? $"[GameFlow] Đã tạo page {_setting.NameClass} và nút tab trong scene (nhớ Save scene, đổi icon nút tab)."
                : $"[GameFlow] Đã tạo {_setting.NameClass}. Scene đang mở không có PageController: mở scene Home rồi bấm '+ Scene' trong Game Flow.");
        }

        /// <summary>Dựng object page trong Content của scroll + nhân bản 1 nút tab có sẵn, rồi thêm vào PageController.pages.</summary>
        public static GameObject AddPageToScene(Type typeClass, PageName page)
        {
            var _pageController = FindPageControllerInScene();
            if (_pageController == null || typeClass == null)
                return null;
            var _existing = FindPageInScene(page);
            if (_existing != null)
                return _existing.gameObject;

            var _serializedController = new SerializedObject(_pageController);
            var _scrollRect = _serializedController.FindProperty("scrollPage").objectReferenceValue as ScrollRect;
            var _propertyPages = _serializedController.FindProperty("pages");
            if (_scrollRect == null || _scrollRect.content == null)
            {
                Debug.LogError("[GameFlow] PageController chưa gán scrollPage.");
                return null;
            }

            // Page: copy kích thước / anchor từ page đầu tiên
            var _content = _scrollRect.content;
            var _sample = _content.childCount > 0 ? _content.GetChild(0) as RectTransform : null;
            var _go = new GameObject(typeClass.Name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(_go, "Add Page");
            var _rect = (RectTransform)_go.transform;
            _rect.SetParent(_content, false);
            if (_sample != null)
            {
                _go.layer = _sample.gameObject.layer;
                _rect.anchorMin = _sample.anchorMin;
                _rect.anchorMax = _sample.anchorMax;
                _rect.pivot = _sample.pivot;
                _rect.sizeDelta = _sample.sizeDelta;
                _rect.localScale = _sample.localScale;
            }
            var _pageNew = (PageBase)_go.AddComponent(typeClass);
            _pageNew.idPage = page;

            // Nút tab: nhân bản nút của 1 page có sẵn
            var _sampleTab = Enumerable.Range(0, _propertyPages.arraySize)
                .Select(i => _propertyPages.GetArrayElementAtIndex(i).objectReferenceValue as PageBase)
                .Where(p => p != null && p.choicePageUI != null)
                .Select(p => p.choicePageUI)
                .FirstOrDefault();
            if (_sampleTab != null)
            {
                var _tab = Object.Instantiate(_sampleTab.gameObject, _sampleTab.transform.parent);
                Undo.RegisterCreatedObjectUndo(_tab, "Add Page Tab");
                _tab.name = page + "Btn";
                _tab.transform.SetAsLastSibling();
                var _choice = _tab.GetComponent<ChoicePageUI>();
                var _serializedChoice = new SerializedObject(_choice);
                _serializedChoice.FindProperty("controller").objectReferenceValue = _pageNew;
                _serializedChoice.ApplyModifiedPropertiesWithoutUndo();
                _pageNew.choicePageUI = _choice;
            }
            else
            {
                Debug.LogWarning("[GameFlow] Không có nút tab mẫu, hãy tự tạo ChoicePageUI và gán vào page.");
            }

            _propertyPages.arraySize++;
            _propertyPages.GetArrayElementAtIndex(_propertyPages.arraySize - 1).objectReferenceValue = _pageNew;
            _serializedController.ApplyModifiedProperties();
            EditorUtility.SetDirty(_pageNew);
            EditorSceneManager.MarkSceneDirty(_go.scene);
            Selection.activeGameObject = _go;
            return _go;
        }

        #endregion

        #region Helper

        private static string _GetPathScript(Type type)
        {
            foreach (var _guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                var _script = AssetDatabase.LoadAssetAtPath<MonoScript>(_path);
                if (_script != null && _script.GetClass() == type)
                    return _path;
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
