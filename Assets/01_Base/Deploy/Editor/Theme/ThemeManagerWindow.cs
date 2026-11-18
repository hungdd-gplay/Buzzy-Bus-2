using System.IO;
using UnityEditor;
using UnityEngine;

namespace BaseProject.Theme.EditorTools
{
    /// <summary>
    /// View (Editor): Tools > Base Project > Theme Manager
    /// 1. Cấu trúc + cấu hình   2. Đổi concept (1 nút)   3. Theme sự kiện + AssetBundle   4. Gắn slot / Preview / Test
    /// </summary>
    public class ThemeManagerWindow : EditorWindow
    {
        private const string _KEY_PREFS_SOURCE = "BaseProject.Theme.FolderConceptSource";

        private ThemeSystemConfig _config;
        private string _pathFolderConceptSource;
        private bool _isDeleteOld = true;
        private bool _isBackup = true;
        private ThemeEventId _eventIdSelected = ThemeEventId.Halloween;
        private bool _isEventIncludeInBuild;
        private ThemeSlotId _slotAssign = ThemeSlotId.Home_BG;
        private string _subKeyAssign = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Tools/Base Project/Theme Manager")]
        public static void OpenWindow()
        {
            var _window = GetWindow<ThemeManagerWindow>("Theme Manager");
            _window.minSize = new Vector2(420, 560);
        }

        private void OnEnable()
        {
            _config = ThemeEditorUtility.LoadConfig();
            _pathFolderConceptSource = EditorPrefs.GetString(_KEY_PREFS_SOURCE, string.Empty);
            _isEventIncludeInBuild = ThemeEditorUtility.IsEventIncludeInBuild(_eventIdSelected);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _DrawSetup();
            if (_config != null)
            {
                _DrawConcept();
                _DrawEvent();
                _DrawAssignSlot();
                _DrawPreviewAndTest();
            }
            EditorGUILayout.EndScrollView();
        }

        #region 1. Cấu trúc

        private void _DrawSetup()
        {
            EditorGUILayout.LabelField("1. Cấu trúc & cấu hình", EditorStyles.boldLabel);
            if (_config == null)
            {
                EditorGUILayout.HelpBox("Chưa có ThemeSystemConfig. Bấm để tạo thư mục _ThemeAssets, config và pack mặc định.", MessageType.Info);
                if (GUILayout.Button("Tạo cấu trúc Theme", GUILayout.Height(28)))
                    _config = ThemeEditorUtility.SetupStructure();
                return;
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField("Config", _config, typeof(ThemeSystemConfig), false);
            if (GUILayout.Button("Sửa", GUILayout.Width(50)))
                Selection.activeObject = _config;
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            _config.isEnableTheme = EditorGUILayout.ToggleLeft("Bật hệ thống Theme", _config.isEnableTheme);
            EditorGUI.BeginDisabledGroup(!_config.isEnableTheme);
            EditorGUILayout.BeginHorizontal();
            _config.isEnableLoading = EditorGUILayout.ToggleLeft("Loading", _config.isEnableLoading, GUILayout.Width(90));
            _config.isEnableHome = EditorGUILayout.ToggleLeft("Home", _config.isEnableHome, GUILayout.Width(80));
            _config.isEnableGameplay = EditorGUILayout.ToggleLeft("Gameplay", _config.isEnableGameplay, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();
            _config.isEnableEventTheme = EditorGUILayout.ToggleLeft("Theme sự kiện (RemoteConfig)", _config.isEnableEventTheme);
            _config.isEnableAssetBundle = EditorGUILayout.ToggleLeft("Tải theme sự kiện từ AssetBundle", _config.isEnableAssetBundle);
            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_config);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Mở thư mục ảnh mặc định"))
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(ThemeEditorUtility.PATH_DEFAULT));
            if (GUILayout.Button("Danh sách tên ảnh"))
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(ThemeEditorUtility.PATH_ROOT + "/_HuongDanTenAnh.txt"));
            EditorGUILayout.EndHorizontal();
            _DrawSeparator();
        }

        #endregion

        #region 2. Đổi concept

        private void _DrawConcept()
        {
            EditorGUILayout.LabelField("2. Đổi concept game (1 nút)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Chọn thư mục chứa bộ ảnh concept mới (tên file = tên slot, vd Home_BG.png, Home_ButtonPlay_Decor_1.png, Gameplay_BG_01.png).\n" +
                "Ảnh trùng tên được ghi đè và GIỮ liên kết trong scene/prefab. Ảnh cũ không có trong bộ mới sẽ bị xoá.",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("Thư mục concept mới", _pathFolderConceptSource);
            if (GUILayout.Button("Chọn", GUILayout.Width(60)))
            {
                string _path = EditorUtility.OpenFolderPanel("Chọn thư mục concept mới", _pathFolderConceptSource, string.Empty);
                if (!string.IsNullOrEmpty(_path))
                {
                    _pathFolderConceptSource = _path;
                    EditorPrefs.SetString(_KEY_PREFS_SOURCE, _path);
                }
            }
            EditorGUILayout.EndHorizontal();
            _isDeleteOld = EditorGUILayout.ToggleLeft("Xoá ảnh concept cũ không có trong bộ mới", _isDeleteOld);
            _isBackup = EditorGUILayout.ToggleLeft("Sao lưu concept cũ ra thư mục ThemeBackup/ (ngoài Assets)", _isBackup);

            GUI.enabled = Directory.Exists(_pathFolderConceptSource);
            if (GUILayout.Button("Áp dụng concept mới", GUILayout.Height(32)))
                _RunApplyConcept();
            GUI.enabled = true;

            if (GUILayout.Button("Cập nhật Pack mặc định từ thư mục (khi tự kéo ảnh vào)"))
            {
                ThemeEditorUtility.EnsureSpriteImport(ThemeEditorUtility.PATH_DEFAULT);
                ThemeEditorUtility.RebuildPack(_config.packDefault, ThemeEditorUtility.PATH_DEFAULT);
                ShowNotification(new GUIContent($"Pack mặc định: {_config.packDefault.entries.Count} ảnh"));
            }
            _DrawSeparator();
        }

        private void _RunApplyConcept()
        {
            string _message = "Ảnh concept trong " + ThemeEditorUtility.PATH_DEFAULT + " sẽ bị thay bằng bộ ảnh mới."
                + (_isDeleteOld ? "\nẢnh cũ không có trong bộ mới sẽ bị XOÁ." : string.Empty)
                + (_isBackup ? "\nBản sao lưu được lưu tại ThemeBackup/." : "\nKHÔNG sao lưu - không thể hoàn tác.");
            if (!EditorUtility.DisplayDialog("Đổi concept", _message, "Áp dụng", "Huỷ"))
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Theme", "Đang đổi concept...", 0.5f);
                var _report = ThemeEditorUtility.ApplyConcept(_pathFolderConceptSource, _isDeleteOld, _isBackup);
                EditorUtility.ClearProgressBar();
                string _summary = $"Thay: {_report.numReplaced}  |  Thêm: {_report.numAdded}  |  Xoá: {_report.numDeleted}"
                    + (string.IsNullOrEmpty(_report.pathBackup) ? string.Empty : "\nSao lưu: " + _report.pathBackup);
                foreach (var _warning in _report.warnings)
                    Debug.LogWarning("[Theme] " + _warning);
                if (_report.warnings.Count > 0)
                    _summary += $"\n{_report.warnings.Count} cảnh báo (xem Console).";
                EditorUtility.DisplayDialog("Đổi concept xong", _summary, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        #endregion

        #region 3. Theme sự kiện

        private void _DrawEvent()
        {
            EditorGUILayout.LabelField("3. Theme sự kiện", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _eventIdSelected = (ThemeEventId)EditorGUILayout.EnumPopup(new GUIContent("Sự kiện", "Thêm sự kiện mới trong enum ThemeEventId"), _eventIdSelected);
            if (EditorGUI.EndChangeCheck())
                _isEventIncludeInBuild = ThemeEditorUtility.IsEventIncludeInBuild(_eventIdSelected);
            if (_eventIdSelected == ThemeEventId.Default)
            {
                EditorGUILayout.HelpBox("Default là concept chính, dùng mục 2.", MessageType.None);
                _DrawSeparator();
                return;
            }

            string _folder = ThemeEditorUtility.GetEventFolder(_eventIdSelected);
            bool _isExist = AssetDatabase.IsValidFolder(_folder);
            EditorGUILayout.LabelField("Thư mục", _folder, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Id (RemoteConfig " + _config.keyRemoteEventId + ")", ((int)_eventIdSelected).ToString(), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Tên AssetBundle (" + _config.keyRemoteBundleName + ")", ThemeEditorUtility.GetBundleName(_eventIdSelected), EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            _isEventIncludeInBuild = EditorGUILayout.ToggleLeft(new GUIContent("Có sẵn trong build (không cần tải AssetBundle)",
                "Tắt: ảnh sự kiện không vào bản build, chỉ tải qua AssetBundle"), _isEventIncludeInBuild);
            if (EditorGUI.EndChangeCheck() && _isExist)
                ThemeEditorUtility.SetEventIncludeInBuild(_eventIdSelected, _isEventIncludeInBuild);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_isExist ? "Mở thư mục" : "Tạo thư mục sự kiện"))
            {
                if (!_isExist)
                    ThemeEditorUtility.CreateEventTheme(_eventIdSelected, _isEventIncludeInBuild);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(_folder));
            }
            GUI.enabled = _isExist;
            if (GUILayout.Button("Cập nhật Pack từ thư mục"))
            {
                var _pack = ThemeEditorUtility.RefreshEventPack(_eventIdSelected);
                if (_pack != null)
                    ShowNotification(new GUIContent($"{_eventIdSelected}: {_pack.entries.Count} ảnh"));
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button($"Build AssetBundle ({EditorUserBuildSettings.activeBuildTarget})", GUILayout.Height(26)))
            {
                try
                {
                    string _pathBundle = ThemeEditorUtility.BuildEventBundle(_eventIdSelected);
                    EditorUtility.RevealInFinder(_pathBundle);
                    EditorUtility.DisplayDialog("Build AssetBundle xong",
                        $"File: {_pathBundle}\n\nUpload lên server theo đường dẫn:\n{_config.urlAssetBundleFormat}\n\nRemoteConfig:\n{_config.keyRemoteEventId} = {(int)_eventIdSelected}\n{_config.keyRemoteBundleName} = {ThemeEditorUtility.GetBundleName(_eventIdSelected)}\n{_config.keyRemoteBundleVersion} = tăng mỗi lần upload bản mới",
                        "OK");
                }
                catch (System.Exception _exception)
                {
                    EditorUtility.DisplayDialog("Build AssetBundle lỗi", _exception.Message, "OK");
                }
            }
            GUI.enabled = true;
            _DrawSeparator();
        }

        #endregion

        #region 4. Gắn slot / Preview / Test

        private void _DrawAssignSlot()
        {
            EditorGUILayout.LabelField("4. Gắn slot cho object đang chọn", EditorStyles.boldLabel);
            _slotAssign = (ThemeSlotId)EditorGUILayout.EnumPopup("Slot", _slotAssign);
            _subKeyAssign = EditorGUILayout.TextField(new GUIContent("Sub key (decor)", "Vd Decor_1. Để trống = ảnh chính"), _subKeyAssign);
            var _selected = Selection.gameObjects;
            GUI.enabled = _selected.Length > 0;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"Gắn ThemeSlotImage ({_selected.Length})"))
            {
                foreach (var _go in _selected)
                {
                    // Không dùng ?? với UnityEngine.Object (null giả trong Editor)
                    var _slotImage = _go.GetComponent<ThemeSlotImage>();
                    if (_slotImage == null)
                        _slotImage = Undo.AddComponent<ThemeSlotImage>(_go);
                    Undo.RecordObject(_slotImage, "Assign Theme Slot");
                    _slotImage.slot = _slotAssign;
                    _slotImage.subKey = _subKeyAssign;
                    EditorUtility.SetDirty(_slotImage);
                }
            }
            if (GUILayout.Button($"Gắn BG Gameplay ({_selected.Length})"))
            {
                foreach (var _go in _selected)
                {
                    if (_go.GetComponent<ThemeGameplayBackground>() == null)
                        Undo.AddComponent<ThemeGameplayBackground>(_go);
                }
            }
            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;
            _DrawSeparator();
        }

        private void _DrawPreviewAndTest()
        {
            EditorGUILayout.LabelField("5. Xem trước / Test", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Preview Default trong scene"))
                    ShowNotification(new GUIContent($"Đã gán {ThemeEditorUtility.PreviewPackInScene(_config.packDefault, null)} ảnh"));
                var _packEvent = AssetDatabase.LoadAssetAtPath<ThemePack>(ThemeEditorUtility.GetEventPackPath(_eventIdSelected));
                GUI.enabled = _packEvent != null;
                if (GUILayout.Button($"Preview {_eventIdSelected} trong scene"))
                    ShowNotification(new GUIContent($"Đã gán {ThemeEditorUtility.PreviewPackInScene(_packEvent, _config.packDefault)} ảnh"));
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("Preview đổi ảnh trực tiếp trong scene (Ctrl+Z để hoàn tác). Nhớ preview lại Default trước khi lưu scene.", MessageType.None);

                EditorGUI.BeginChangeCheck();
                _config.isUseEditorTestEvent = EditorGUILayout.ToggleLeft("Khi Play: dùng sự kiện test (bỏ qua RemoteConfig)", _config.isUseEditorTestEvent);
                if (_config.isUseEditorTestEvent)
                    _config.eventIdEditorTest = (ThemeEventId)EditorGUILayout.EnumPopup("Sự kiện test", _config.eventIdEditorTest);
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(_config);
                return;
            }

            var _manager = ThemeManager.Instance;
            if (_manager == null)
            {
                EditorGUILayout.HelpBox("ThemeManager chưa chạy (Theme đang tắt hoặc thiếu config).", MessageType.Warning);
                return;
            }
            EditorGUILayout.LabelField("Đang dùng", _manager.EventIdCurrent.ToString());
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"Đổi sang {_eventIdSelected}"))
                _manager.SetEventTheme(_eventIdSelected, onDone: ok => Debug.Log($"[Theme] Đổi sang {_eventIdSelected}: {(ok ? "thành công" : "thất bại")}"));
            if (GUILayout.Button("Về Default"))
                _manager.ResetToDefault();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        private static void _DrawSeparator()
        {
            EditorGUILayout.Space(4);
            var _rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(_rect, new Color(0.5f, 0.5f, 0.5f, 0.4f));
            EditorGUILayout.Space(4);
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }
    }
}
