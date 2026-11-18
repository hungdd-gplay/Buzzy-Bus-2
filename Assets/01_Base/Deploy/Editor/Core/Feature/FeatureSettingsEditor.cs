using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BaseProject.Core.Feature.EditorTools
{
    /// <summary>
    /// View: giao diện tick bật/tắt tính năng. Dùng chung cho Inspector của FeatureSettings.asset
    /// và cửa sổ Tools > Base Project > Feature Settings.
    /// </summary>
    public static class FeatureSettingsDrawer
    {
        public static void Draw(FeatureSettings settings)
        {
            if (settings == null)
                return;

            EditorGUILayout.HelpBox("Tick để bật/tắt tính năng, sau đó bấm \"Áp dụng\". Unity sẽ compile lại.\n" +
                "Thứ tự trong danh sách = thứ tự khởi tạo trong GameController.", MessageType.Info);

            bool _isSynced = settings.IsSyncedWithDefines();
            var _current = FeatureSettings.GetCurrentFeatureDefines();

            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < settings.toggles.Count; i++)
            {
                var _toggle = settings.toggles[i];
                var _info = FeatureCatalog.Get(_toggle.id);
                if (_info == null)
                    continue;

                bool _isApplied = _current.Contains(_info.defineSymbol);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                _toggle.isEnabled = EditorGUILayout.ToggleLeft($"{i + 1}. {_info.displayName}", _toggle.isEnabled, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                var _styleState = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = _toggle.isEnabled == _isApplied ? new Color(0.4f, 0.8f, 0.4f) : new Color(1f, 0.7f, 0.2f) }
                };
                GUILayout.Label(_toggle.isEnabled == _isApplied ? (_isApplied ? "đang bật" : "đang tắt") : "chưa áp dụng", _styleState);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(_info.defineSymbol + "  —  " + _info.description, EditorStyles.wordWrappedMiniLabel);
                if (_info.dependencies.Length > 0)
                    EditorGUILayout.LabelField("Cần: " + string.Join(", ", _info.dependencies.Select(d => FeatureCatalog.Get(d).displayName)), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(settings);

            foreach (var _warning in settings.GetDependencyWarnings())
                EditorGUILayout.HelpBox(_warning, MessageType.Warning);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Bật tất cả"))
            {
                settings.toggles.ForEach(t => t.isEnabled = true);
                EditorUtility.SetDirty(settings);
            }
            if (GUILayout.Button("Tắt tất cả"))
            {
                settings.toggles.ForEach(t => t.isEnabled = false);
                EditorUtility.SetDirty(settings);
            }
            if (GUILayout.Button("Hoàn tác (theo define hiện tại)"))
            {
                settings.toggles.ForEach(t => t.isEnabled = _current.Contains(FeatureCatalog.Get(t.id).defineSymbol));
                EditorUtility.SetDirty(settings);
            }
            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = _isSynced ? Color.white : new Color(1f, 0.8f, 0.3f);
            GUI.enabled = !_isSynced && !EditorApplication.isCompiling;
            if (GUILayout.Button(_isSynced ? "Đã áp dụng" : "Áp dụng (ghi Define Symbols)", GUILayout.Height(32)))
            {
                var _warnings = settings.GetDependencyWarnings();
                if (_warnings.Count == 0 || EditorUtility.DisplayDialog("Thiếu tính năng phụ thuộc",
                        string.Join("\n", _warnings) + "\n\nVẫn áp dụng?", "Áp dụng", "Huỷ"))
                {
                    settings.ApplyDefines();
                }
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;
            if (EditorApplication.isCompiling)
                EditorGUILayout.HelpBox("Đang compile...", MessageType.None);
        }
    }

    [CustomEditor(typeof(FeatureSettings))]
    public class FeatureSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var _settings = (FeatureSettings)target;
            if (_settings.SyncWithCatalog())
                EditorUtility.SetDirty(_settings);
            FeatureSettingsDrawer.Draw(_settings);
        }
    }

    public class FeatureSettingsWindow : EditorWindow
    {
        private FeatureSettings _settings;
        private Vector2 _scroll;

        [MenuItem("Tools/Base Project/Feature Settings")]
        public static void OpenWindow()
        {
            var _window = GetWindow<FeatureSettingsWindow>("Feature Settings");
            _window.minSize = new Vector2(420, 500);
        }

        private void OnEnable()
        {
            _settings = FeatureSettings.LoadOrCreate(out _);
        }

        private void OnGUI()
        {
            if (_settings == null)
                _settings = FeatureSettings.LoadOrCreate(out _);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField("File setting", _settings, typeof(FeatureSettings), false);
            EditorGUILayout.EndHorizontal();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            FeatureSettingsDrawer.Draw(_settings);
            EditorGUILayout.EndScrollView();
        }

        private void OnFocus()
        {
            Repaint();
        }
    }
}
