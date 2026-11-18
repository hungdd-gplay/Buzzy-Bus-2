#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BaseProject.EditorTools.Build
{
    /// <summary>
    /// Build > Build Config...
    /// Cấu hình trước khi build: tên game, version name, icon, keystore, version code (Android), build number (iOS).
    /// Tab Android / iOS riêng; có nút Switch Platform và các nút build.
    /// </summary>
    public class BuildConfigWindow : EditorWindow
    {
        private BuildConfig _config;
        private SerializedObject _serializedConfig;
        private int _indexTab;
        private Vector2 _scrollPosition;

        public static void Open()
        {
            var _window = GetWindow<BuildConfigWindow>("Build Config");
            _window.minSize = new Vector2(500, 560);
        }

        private void OnEnable()
        {
            _config = BuildConfig.LoadOrCreate();
            _serializedConfig = new SerializedObject(_config);
            _indexTab = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? 1 : 0;
        }

        private void OnGUI()
        {
            if (_config == null || _serializedConfig == null || _serializedConfig.targetObject == null)
                OnEnable();
            _serializedConfig.Update();

            var _target = EditorUserBuildSettings.activeBuildTarget;
            EditorGUILayout.HelpBox($"Platform đang chọn: {_target}", _target == BuildTarget.Android || _target == BuildTarget.iOS ? MessageType.Info : MessageType.Warning);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("Chung", EditorStyles.boldLabel);
            var _propertyCommon = _serializedConfig.FindProperty("common");
            EditorGUILayout.PropertyField(_propertyCommon.FindPropertyRelative("productName"), new GUIContent("Tên game"));
            EditorGUILayout.PropertyField(_propertyCommon.FindPropertyRelative("versionName"), new GUIContent("Version name"));
            EditorGUILayout.PropertyField(_propertyCommon.FindPropertyRelative("iconApp"), new GUIContent("Icon"));
            EditorGUILayout.PropertyField(_propertyCommon.FindPropertyRelative("iapProductPrefix"), new GUIContent("Tiền tố IAP"));
            EditorGUILayout.PropertyField(_propertyCommon.FindPropertyRelative("fileNameBuild"), new GUIContent("Tên file build"));

            EditorGUILayout.Space(8);
            _indexTab = GUILayout.Toolbar(_indexTab, new[] { "Android", "iOS" });
            EditorGUILayout.Space(4);
            if (_indexTab == 0) _DrawAndroid();
            else _DrawIOS();

            EditorGUILayout.EndScrollView();
            _serializedConfig.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            _DrawButtons(_target);
        }

        private void _DrawAndroid()
        {
            var _property = _serializedConfig.FindProperty("android");
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("packageName"), new GUIContent("Package name"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Version code", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("versionCode"), new GUIContent("Version code hiện tại"));
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("isAutoVersionCodeForAab"), new GUIContent("Tự tăng khi build .aab"));
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("isAutoVersionCodeForApk"), new GUIContent("Tự tăng khi build .apk"));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Version code bản build tiếp theo", BuildTool.GetNextAndroidVersionCode(_config).ToString(), EditorStyles.boldLabel);
            if (GUILayout.Button("Dùng ngay", GUILayout.Width(80)))
            {
                _serializedConfig.ApplyModifiedProperties();
                Undo.RecordObject(_config, "Version code");
                _config.android.versionCode = BuildTool.GetNextAndroidVersionCode(_config);
                EditorUtility.SetDirty(_config);
                _serializedConfig.Update();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Keystore", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            var _propertyPath = _property.FindPropertyRelative("keystorePath");
            EditorGUILayout.PropertyField(_propertyPath, new GUIContent("File keystore"));
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string _path = EditorUtility.OpenFilePanel("Chọn keystore", Path.GetDirectoryName(Application.dataPath), "keystore,jks");
                if (!string.IsNullOrEmpty(_path))
                    _propertyPath.stringValue = _ToProjectRelative(_path);
            }
            EditorGUILayout.EndHorizontal();
            string _fullPath = Path.IsPathRooted(_propertyPath.stringValue) ? _propertyPath.stringValue
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", _propertyPath.stringValue ?? ""));
            if (!File.Exists(_fullPath))
                EditorGUILayout.HelpBox("Không tìm thấy file keystore -> sẽ build bằng debug key.", MessageType.Warning);

            EditorGUILayout.PropertyField(_property.FindPropertyRelative("keyAliasName"), new GUIContent("Alias"));
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("isStorePasswordLocal"), new GUIContent("Lưu mật khẩu trên máy này"));
            _serializedConfig.ApplyModifiedProperties();

            EditorGUI.BeginChangeCheck();
            string _keystorePass = EditorGUILayout.PasswordField("Keystore password", _config.KeystorePass);
            string _aliasPass = EditorGUILayout.PasswordField("Alias password", _config.KeyAliasPass);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_config, "Password");
                _config.KeystorePass = _keystorePass;
                _config.KeyAliasPass = _aliasPass;
                EditorUtility.SetDirty(_config);
            }
            _serializedConfig.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("isUseIL2CPP"), new GUIContent("IL2CPP"));
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("isArmV7"), new GUIContent("ARMv7"));
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("isArm64"), new GUIContent("ARM64 (cần IL2CPP)"));
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("minSdkVersion"), new GUIContent("Min SDK"));
        }

        private void _DrawIOS()
        {
            var _property = _serializedConfig.FindProperty("ios");
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("bundleIdentifier"), new GUIContent("Bundle identifier"));
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("buildNumber"), new GUIContent("Build number hiện tại"));
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("isAutoBuildNumber"), new GUIContent("Tự tăng theo ngày"));
            EditorGUILayout.LabelField("Build number tiếp theo", BuildTool.GetNextIOSBuildNumber(_config), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("appleTeamId"), new GUIContent("Apple Team ID"));
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("isAutomaticSigning"), new GUIContent("Automatic signing"));
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("targetOSVersion"), new GUIContent("iOS tối thiểu"));
            EditorGUILayout.PropertyField(_property.FindPropertyRelative("appStoreId"), new GUIContent("App Store ID"));
        }

        private void _DrawButtons(BuildTarget target)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Lưu & áp dụng vào Player Settings", GUILayout.Height(26)))
            {
                AssetDatabase.SaveAssets();
                BuildTool.ApplyToPlayerSettings(_config, target);
            }
            if (GUILayout.Button("Mở thư mục Builds", GUILayout.Height(26)))
                BuildTool.OpenFolderBuild();
            EditorGUILayout.EndHorizontal();

            if (_indexTab == 0)
            {
                if (target != BuildTarget.Android)
                {
                    if (GUILayout.Button("Switch Platform -> Android", GUILayout.Height(28)))
                        BuildTool.SwitchPlatform(BuildTarget.Android);
                    return;
                }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Build APK Test", GUILayout.Height(30)))
                    _RunLater(() => BuildTool.BuildAndroid(false, true));
                if (GUILayout.Button("Build APK Release", GUILayout.Height(30)))
                    _RunLater(() => BuildTool.BuildAndroid(false, false));
                if (GUILayout.Button("Build AAB Release", GUILayout.Height(30)))
                    _RunLater(() => BuildTool.BuildAndroid(true, false));
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                if (target != BuildTarget.iOS)
                {
                    if (GUILayout.Button("Switch Platform -> iOS", GUILayout.Height(28)))
                        BuildTool.SwitchPlatform(BuildTarget.iOS);
                    return;
                }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Build Xcode Test", GUILayout.Height(30)))
                    _RunLater(() => BuildTool.BuildIOS(true));
                if (GUILayout.Button("Build Xcode Release", GUILayout.Height(30)))
                    _RunLater(() => BuildTool.BuildIOS(false));
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>Build ngoài OnGUI để tránh lỗi layout.</summary>
        private void _RunLater(Action action)
        {
            AssetDatabase.SaveAssets();
            EditorApplication.delayCall += () => action();
            GUIUtility.ExitGUI();
        }

        private static string _ToProjectRelative(string pathFull)
        {
            string _root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/') + "/";
            pathFull = pathFull.Replace('\\', '/');
            return pathFull.StartsWith(_root) ? pathFull.Substring(_root.Length) : pathFull;
        }
    }
}
#endif
