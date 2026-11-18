using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BaseProject.EditorTools.GameFlow
{
    /// <summary>
    /// View: cấu hình luồng màn hình của game (có Home không, Loading vào thẳng GamePlay),
    /// bật / tắt page ở Home và tạo page mới.
    /// Menu: Tools > Base Project > Game Flow (Home / Page)
    /// </summary>
    public class GameFlowWindow : EditorWindow
    {
        private const string _KEY_PREFS_PAGE = "BaseProject.GameFlow.PageSetting";

        private GameFlowConfig _config;
        private PageCreateSetting _settingPage = new PageCreateSetting();
        private string _errorPage = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Tools/Base Project/Game Flow (Home - Page)")]
        public static void OpenWindow()
        {
            var _window = GetWindow<GameFlowWindow>("Game Flow");
            _window.minSize = new Vector2(460, 420);
        }

        private void OnEnable()
        {
            string _json = EditorPrefs.GetString(_KEY_PREFS_PAGE, string.Empty);
            if (!string.IsNullOrEmpty(_json))
                JsonUtility.FromJsonOverwrite(_json, _settingPage);
            _config = GameFlowEditorController.GetOrCreateConfig();
            _errorPage = GameFlowEditorController.Validate(_settingPage);
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(_KEY_PREFS_PAGE, JsonUtility.ToJson(_settingPage));
        }

        private void OnFocus()
        {
            _errorPage = GameFlowEditorController.Validate(_settingPage);
        }

        private void OnGUI()
        {
            if (_config == null)
                _config = GameFlowEditorController.GetOrCreateConfig();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUI.BeginChangeCheck();
            _DrawFlow();
            EditorGUILayout.Space(8);
            _DrawPages();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssetIfDirty(_config);
            }
            EditorGUILayout.Space(8);
            _DrawCreatePage();
            EditorGUILayout.EndScrollView();
        }

        private void _DrawFlow()
        {
            EditorGUILayout.LabelField("Luồng màn hình", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("Config", _config, typeof(GameFlowConfig), false);
            _config.isUseHome = EditorGUILayout.Toggle(new GUIContent("Có màn Home", "Tắt: Loading -> GamePlay luôn, mọi lệnh về Home sẽ load lại GamePlay"), _config.isUseHome);
            GUI.enabled = _config.isUseHome;
            _config.levelPlayDirectFromLoading = EditorGUILayout.IntField(new GUIContent("Vào thẳng GamePlay đến level", "Level <= giá trị này: Loading vào thẳng GamePlay. Remote 'start_level_back_to_home' ghi đè"), _config.levelPlayDirectFromLoading);
            GUI.enabled = true;

            EditorGUILayout.HelpBox(_config.isUseHome
                    ? "Loading -> GamePlay nếu level <= ngưỡng hoặc segment loadGameConfig.typeSenceFirstOnOpenGame = 1, ngược lại -> Home.\nHết level: segment loadGameConfig.isComeHomeWhenEndLevel."
                    : "Game KHÔNG có Home: Loading -> GamePlay; nút Home / Quit / Next level sẽ load lại GamePlay.\nCác tính năng chạy ở Home (gợi ý popup, daily login, nhận quà challenge...) cần được gọi từ GamePlay nếu muốn dùng.",
                MessageType.None);
        }

        private void _DrawPages()
        {
            EditorGUILayout.LabelField("Page ở Home", EditorStyles.boldLabel);
            GUI.enabled = _config.isUseHome;

            var _lsEnable = new List<PageName>();
            var _pageController = GameFlowEditorController.FindPageControllerInScene();
            foreach (PageName _page in Enum.GetValues(typeof(PageName)))
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                bool _isEnable = _config.IsPageEnable(_page);
                bool _isEnableNew = EditorGUILayout.ToggleLeft(_page.ToString(), _isEnable, GUILayout.Width(140));
                if (_isEnableNew != _isEnable)
                    _config.SetPageEnable(_page, _isEnableNew);
                if (_isEnableNew)
                    _lsEnable.Add(_page);

                var _type = GameFlowEditorController.FindPageClass(_page);
                var _inScene = _pageController != null ? GameFlowEditorController.FindPageInScene(_page) : null;
                EditorGUILayout.LabelField($"Script: {(_type != null ? _type.Name : "—")}   " +
                                           (_pageController != null ? $"Scene: {(_inScene != null ? "✔" : "✘")}" : ""), EditorStyles.miniLabel);
                if (_pageController != null && _inScene == null && _type != null && GUILayout.Button("+ Scene", GUILayout.Width(64)))
                    GameFlowEditorController.AddPageToScene(_type, _page);
                if (_inScene != null && GUILayout.Button("Chọn", GUILayout.Width(50)))
                    Selection.activeGameObject = _inScene.gameObject;
                EditorGUILayout.EndHorizontal();
            }

            if (_lsEnable.Count > 0)
            {
                int _index = Mathf.Max(0, _lsEnable.IndexOf(_config.defaultPage));
                _config.defaultPage = _lsEnable[EditorGUILayout.Popup("Page mặc định", _index, _lsEnable.ConvertAll(p => p.ToString()).ToArray())];
            }
            if (!_config.IsPageEnable(PageName.Home))
                EditorGUILayout.HelpBox("Page Home đang tắt: nút Play, chest, khởi tạo challenge / event ở Home sẽ không chạy.", MessageType.Warning);
            if (_pageController == null)
                EditorGUILayout.HelpBox("Mở scene Home để xem / thêm page vào scene.", MessageType.Info);
            GUI.enabled = true;
        }

        private void _DrawCreatePage()
        {
            EditorGUILayout.LabelField("Tạo page mới", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _settingPage.namePage = EditorGUILayout.TextField(new GUIContent("Tên (PascalCase)", "Ví dụ: Event -> PageName.Event, class EventPage"), _settingPage.namePage);
            EditorGUILayout.LabelField(" ", $"PageName.{_settingPage.namePage}   |   class {_settingPage.NameClass}", EditorStyles.miniLabel);
            _settingPage.folderScript = EditorGUILayout.TextField("Thư mục script", _settingPage.folderScript);
            _settingPage.isAddToScene = EditorGUILayout.Toggle(new GUIContent("Thêm vào scene Home đang mở", "Tạo object page trong scroll + nhân bản 1 nút tab"), _settingPage.isAddToScene);
            if (EditorGUI.EndChangeCheck())
                _errorPage = GameFlowEditorController.Validate(_settingPage);

            if (!string.IsNullOrEmpty(_errorPage))
                EditorGUILayout.HelpBox(_errorPage, MessageType.Warning);
            GUI.enabled = string.IsNullOrEmpty(_errorPage);
            if (GUILayout.Button("Tạo page", GUILayout.Height(30)))
            {
                EditorPrefs.SetString(_KEY_PREFS_PAGE, JsonUtility.ToJson(_settingPage));
                var _settingCopy = JsonUtility.FromJson<PageCreateSetting>(JsonUtility.ToJson(_settingPage));
                EditorApplication.delayCall += () => GameFlowEditorController.CreatePage(_settingCopy);
            }
            GUI.enabled = true;
        }
    }
}
