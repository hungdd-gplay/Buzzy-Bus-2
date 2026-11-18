#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BaseProject.EditorTools
{
    /// <summary>
    /// Tools > Base Project > SDK ID Manager
    /// Quản lý toàn bộ ID SDK tại 1 chỗ (SdkIdConfig) + toggle dùng / không dùng.
    /// Bấm OK:
    ///   - Lưu SdkIdConfig (code AdmobAds / AppOpenAdManager / CustomPostprocessScript đọc trực tiếp từ đây).
    ///   - Ghi sang settings của SDK: GoogleMobileAdsSettings, AppLovinSettings, FacebookSettings, AppsFlyerObject (prefab + scene đang mở).
    /// </summary>
    /// <summary>Mở project lần đầu: tự tạo SdkIdConfig (điền sẵn ID hiện tại) để quảng cáo không bị trống ID.</summary>
    [InitializeOnLoad]
    public static class SdkIdConfigBootstrap
    {
        static SdkIdConfigBootstrap()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    SdkIdManagerWindow.LoadOrCreateConfig();
            };
        }
    }

    public class SdkIdManagerWindow : EditorWindow
    {
        private const string PATH_FOLDER_CONFIG = "Assets/Resources/Config";
        private const string PATH_ASSET_CONFIG = PATH_FOLDER_CONFIG + "/SdkIdConfig.asset";

        private SdkIdConfig _config;
        private SerializedObject _serializedConfig;
        private Vector2 _scrollPosition;
        private string _messageResult = string.Empty;

        [MenuItem("Tools/Base Project/SDK ID Manager", false, 20)]
        public static void Open()
        {
            var _window = GetWindow<SdkIdManagerWindow>("SDK ID Manager");
            _window.minSize = new Vector2(560, 600);
        }

        private void OnEnable()
        {
            _config = LoadOrCreateConfig();
            _serializedConfig = new SerializedObject(_config);
        }

        public static SdkIdConfig LoadOrCreateConfig()
        {
            var _config = AssetDatabase.LoadAssetAtPath<SdkIdConfig>(PATH_ASSET_CONFIG);
            if (_config != null)
                return _config;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(PATH_FOLDER_CONFIG))
                AssetDatabase.CreateFolder("Assets/Resources", "Config");

            _config = CreateInstance<SdkIdConfig>();
            _FillCurrentProjectValues(_config);
            _ReadFromSdkSettings(_config);
            AssetDatabase.CreateAsset(_config, PATH_ASSET_CONFIG);
            AssetDatabase.SaveAssets();
            Debug.Log("[SDK ID Manager] Tạo " + PATH_ASSET_CONFIG + " (điền sẵn ID đang dùng trong project).");
            return _config;
        }

        private void OnGUI()
        {
            if (_config == null || _serializedConfig == null || _serializedConfig.targetObject == null)
                OnEnable();
            _serializedConfig.Update();

            EditorGUILayout.HelpBox("Điền ID rồi bấm OK. Toggle đầu mỗi SDK / mỗi đơn vị quảng cáo = có dùng hay không " +
                                    "(tắt thì code sẽ không init/không load đơn vị đó).", MessageType.Info);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            _DrawSdk("admob", "AdMob", (p) =>
            {
                EditorGUILayout.PropertyField(p.FindPropertyRelative("isUseTestId"), new GUIContent("Dùng ID test"));
                _DrawPlatform(p.FindPropertyRelative("appId"), "App ID");
                _DrawUnit(p.FindPropertyRelative("banner"), "Banner (adaptive)");
                _DrawUnit(p.FindPropertyRelative("bannerCollapse"), "Banner collapsible");
                _DrawUnit(p.FindPropertyRelative("bannerMrec"), "Banner MREC");
                _DrawUnit(p.FindPropertyRelative("interstitial"), "Interstitial");
                _DrawUnit(p.FindPropertyRelative("reward"), "Reward");
                _DrawUnit(p.FindPropertyRelative("appOpen"), "App Open");
            });

            _DrawSdk("max", "AppLovin MAX", (p) =>
            {
                EditorGUILayout.PropertyField(p.FindPropertyRelative("sdkKey"), new GUIContent("SDK Key"));
                _DrawPlatform(p.FindPropertyRelative("admobAdapterAppId"), "AdMob adapter App ID");
                _DrawUnit(p.FindPropertyRelative("banner"), "Banner");
                _DrawUnit(p.FindPropertyRelative("mrec"), "MREC");
                _DrawUnit(p.FindPropertyRelative("interstitial"), "Interstitial");
                _DrawUnit(p.FindPropertyRelative("reward"), "Reward");
            });

            _DrawSdk("facebook", "Facebook SDK", (p) =>
            {
                EditorGUILayout.PropertyField(p.FindPropertyRelative("appName"), new GUIContent("App Name"));
                EditorGUILayout.PropertyField(p.FindPropertyRelative("appId"), new GUIContent("App ID"));
                EditorGUILayout.PropertyField(p.FindPropertyRelative("clientToken"), new GUIContent("Client Token"));
            });

            _DrawSdk("appsFlyer", "AppsFlyer", (p) =>
            {
                EditorGUILayout.PropertyField(p.FindPropertyRelative("devKey"), new GUIContent("Dev Key"));
                EditorGUILayout.PropertyField(p.FindPropertyRelative("appIdIOS"), new GUIContent("App ID (iOS)"));
                EditorGUILayout.PropertyField(p.FindPropertyRelative("isDebug"), new GUIContent("Debug log"));
                EditorGUILayout.PropertyField(p.FindPropertyRelative("isGetConversionData"), new GUIContent("Get Conversion Data"));
            });

            EditorGUILayout.EndScrollView();
            _serializedConfig.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Đọc lại từ settings SDK"))
            {
                Undo.RecordObject(_config, "Read SDK settings");
                _ReadFromSdkSettings(_config);
                EditorUtility.SetDirty(_config);
                _serializedConfig.Update();
                _messageResult = "Đã đọc App ID / SDK key / Facebook / AppsFlyer từ settings hiện có.";
            }
            if (GUILayout.Button("OK - Áp dụng", GUILayout.Height(30)))
                _messageResult = ApplyToSdkSettings(_config);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_messageResult))
                EditorGUILayout.HelpBox(_messageResult, MessageType.None);
        }

        // ================== Vẽ ==================

        private void _DrawSdk(string nameProperty, string title, Action<SerializedProperty> actionDrawBody)
        {
            var _property = _serializedConfig.FindProperty(nameProperty);
            var _propertyEnable = _property.FindPropertyRelative("isEnable");

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            _propertyEnable.boolValue = EditorGUILayout.ToggleLeft(title, _propertyEnable.boolValue, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(!_propertyEnable.boolValue))
            {
                EditorGUI.indentLevel++;
                actionDrawBody(_property);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        private void _DrawPlatform(SerializedProperty property, string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(property.FindPropertyRelative("android"), new GUIContent("Android"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("ios"), new GUIContent("iOS"));
            EditorGUI.indentLevel--;
        }

        private void _DrawUnit(SerializedProperty property, string label)
        {
            var _propertyEnable = property.FindPropertyRelative("isEnable");
            _propertyEnable.boolValue = EditorGUILayout.ToggleLeft(label, _propertyEnable.boolValue, EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(!_propertyEnable.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(property.FindPropertyRelative("android"), new GUIContent("Android"));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("ios"), new GUIContent("iOS"));
                EditorGUI.indentLevel--;
            }
        }

        // ================== Áp dụng ==================

        /// <summary>Lưu config và ghi ID sang settings của từng SDK. Trả về báo cáo.</summary>
        public static string ApplyToSdkSettings(SdkIdConfig config)
        {
            EditorUtility.SetDirty(config);
            var _report = new System.Text.StringBuilder("Đã lưu SdkIdConfig.\n");

            if (config.admob.isEnable)
            {
                _report.AppendLine(_WriteSettings("GoogleMobileAdsSettings", s =>
                {
                    _SetString(s, "adMobAndroidAppId", config.admob.appId.android);
                    _SetString(s, "adMobIOSAppId", config.admob.appId.ios);
                }));
            }

            if (config.max.isEnable)
            {
                _report.AppendLine(_WriteSettings("AppLovinSettings", s =>
                {
                    _SetString(s, "sdkKey", config.max.sdkKey);
                    _SetString(s, "adMobAndroidAppId", config.max.admobAdapterAppId.android);
                    _SetString(s, "adMobIosAppId", config.max.admobAdapterAppId.ios);
                }));
            }

            if (config.facebook.isEnable)
            {
                _report.AppendLine(_WriteSettings("FacebookSettings", s =>
                {
                    _SetFirstArrayString(s, "appIds", config.facebook.appId);
                    _SetFirstArrayString(s, "appLabels", config.facebook.appName);
                    _SetFirstArrayString(s, "clientTokens", config.facebook.clientToken);
                }));
                _TryRegenerateFacebookManifest(_report);
            }

            if (config.appsFlyer.isEnable)
                _report.AppendLine(_WriteAppsFlyer(config.appsFlyer));

            AssetDatabase.SaveAssets();
            Debug.Log("[SDK ID Manager]\n" + _report);
            return _report.ToString();
        }

        private static string _WriteSettings(string nameType, Action<SerializedObject> actionWrite)
        {
            var _lstGuid = AssetDatabase.FindAssets("t:" + nameType);
            if (_lstGuid.Length == 0)
                return $"⚠ Không tìm thấy {nameType} (SDK chưa import?).";
            foreach (var _guid in _lstGuid)
            {
                var _asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(_guid));
                if (_asset == null)
                    continue;
                var _serialized = new SerializedObject(_asset);
                actionWrite(_serialized);
                _serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(_asset);
            }
            return $"✓ {nameType}";
        }

        private static string _WriteAppsFlyer(SdkIdConfig.AppsFlyerConfig config)
        {
            int _count = 0;
            // Prefab
            foreach (var _guid in AssetDatabase.FindAssets("t:Prefab AppsFlyerObject"))
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                var _root = PrefabUtility.LoadPrefabContents(_path);
                bool _isChanged = _WriteAppsFlyerComponents(_root, config, ref _count);
                if (_isChanged)
                    PrefabUtility.SaveAsPrefabAsset(_root, _path);
                PrefabUtility.UnloadPrefabContents(_root);
            }
            // Scene đang mở
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var _scene = EditorSceneManager.GetSceneAt(i);
                if (!_scene.isLoaded)
                    continue;
                bool _isChanged = false;
                foreach (var _root in _scene.GetRootGameObjects())
                    _isChanged |= _WriteAppsFlyerComponents(_root, config, ref _count);
                if (_isChanged)
                    EditorSceneManager.MarkSceneDirty(_scene);
            }
            return _count > 0 ? $"✓ AppsFlyerObjectScript ({_count} chỗ)" : "⚠ Không thấy AppsFlyerObjectScript (prefab AppsFlyerObject / scene đang mở).";
        }

        private static bool _WriteAppsFlyerComponents(GameObject root, SdkIdConfig.AppsFlyerConfig config, ref int count)
        {
            bool _isChanged = false;
            foreach (var _behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (_behaviour == null || _behaviour.GetType().Name != "AppsFlyerObjectScript")
                    continue;
                var _serialized = new SerializedObject(_behaviour);
                _SetString(_serialized, "devKey", config.devKey);
                _SetString(_serialized, "appID", config.appIdIOS);
                var _propertyDebug = _serialized.FindProperty("isDebug");
                if (_propertyDebug != null) _propertyDebug.boolValue = config.isDebug;
                var _propertyConversion = _serialized.FindProperty("getConversionData");
                if (_propertyConversion != null) _propertyConversion.boolValue = config.isGetConversionData;
                _serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(_behaviour);
                _isChanged = true;
                count++;
            }
            return _isChanged;
        }

        private static void _TryRegenerateFacebookManifest(System.Text.StringBuilder report)
        {
            try
            {
                var _type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("Facebook.Unity.Editor.ManifestMod"))
                    .FirstOrDefault(t => t != null);
                var _method = _type?.GetMethod("GenerateManifest", BindingFlags.Public | BindingFlags.Static);
                if (_method != null)
                {
                    _method.Invoke(null, null);
                    report.AppendLine("✓ Facebook AndroidManifest");
                }
            }
            catch (Exception _exception)
            {
                report.AppendLine("⚠ Facebook manifest: " + _exception.Message);
            }
        }

        // ================== Đọc ==================

        /// <summary>Đọc App ID / SDK key... từ settings SDK hiện có vào config.</summary>
        private static void _ReadFromSdkSettings(SdkIdConfig config)
        {
            _ReadSettings("GoogleMobileAdsSettings", s =>
            {
                config.admob.appId.android = _GetString(s, "adMobAndroidAppId", config.admob.appId.android);
                config.admob.appId.ios = _GetString(s, "adMobIOSAppId", config.admob.appId.ios);
            });
            _ReadSettings("AppLovinSettings", s =>
            {
                config.max.sdkKey = _GetString(s, "sdkKey", config.max.sdkKey);
                config.max.admobAdapterAppId.android = _GetString(s, "adMobAndroidAppId", config.max.admobAdapterAppId.android);
                config.max.admobAdapterAppId.ios = _GetString(s, "adMobIosAppId", config.max.admobAdapterAppId.ios);
            });
            _ReadSettings("FacebookSettings", s =>
            {
                config.facebook.appId = _GetFirstArrayString(s, "appIds", config.facebook.appId);
                config.facebook.appName = _GetFirstArrayString(s, "appLabels", config.facebook.appName);
                config.facebook.clientToken = _GetFirstArrayString(s, "clientTokens", config.facebook.clientToken);
            });
            foreach (var _guid in AssetDatabase.FindAssets("t:Prefab AppsFlyerObject"))
            {
                var _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(_guid));
                if (_prefab == null) continue;
                foreach (var _behaviour in _prefab.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (_behaviour == null || _behaviour.GetType().Name != "AppsFlyerObjectScript") continue;
                    var _serialized = new SerializedObject(_behaviour);
                    config.appsFlyer.devKey = _GetString(_serialized, "devKey", config.appsFlyer.devKey);
                    config.appsFlyer.appIdIOS = _GetString(_serialized, "appID", config.appsFlyer.appIdIOS);
                    var _propertyDebug = _serialized.FindProperty("isDebug");
                    if (_propertyDebug != null) config.appsFlyer.isDebug = _propertyDebug.boolValue;
                    var _propertyConversion = _serialized.FindProperty("getConversionData");
                    if (_propertyConversion != null) config.appsFlyer.isGetConversionData = _propertyConversion.boolValue;
                }
            }
        }

        private static void _ReadSettings(string nameType, Action<SerializedObject> actionRead)
        {
            foreach (var _guid in AssetDatabase.FindAssets("t:" + nameType))
            {
                var _asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(_guid));
                if (_asset != null)
                {
                    actionRead(new SerializedObject(_asset));
                    return;
                }
            }
        }

        /// <summary>Giá trị đang hard-code trong project trước khi chuyển sang SdkIdConfig.</summary>
        private static void _FillCurrentProjectValues(SdkIdConfig config)
        {
            config.admob.banner = new SdkIdConfig.AdUnitId(true, "ca-app-pub-9457878244675693/1908612182", "ca-app-pub-9457878244675693/6063245506");
            config.admob.bannerCollapse = new SdkIdConfig.AdUnitId(true, "ca-app-pub-9457878244675693/9348118506", "ca-app-pub-9457878244675693/9507666490");
            config.admob.bannerMrec = new SdkIdConfig.AdUnitId(true, "ca-app-pub-9457878244675693/1639161209", "ca-app-pub-9457878244675693/1639161209");
            config.admob.interstitial = new SdkIdConfig.AdUnitId(true, "ca-app-pub-9457878244675693/8113070216", "ca-app-pub-8467610367562059/5655666608");
            config.admob.reward = new SdkIdConfig.AdUnitId(false, "", "");
            config.admob.appOpen = new SdkIdConfig.AdUnitId(true, "ca-app-pub-9457878244675693/3614647346", "ca-app-pub-9457878244675693/5619876469");

            config.max.banner = new SdkIdConfig.AdUnitId(true, "5e65a04d1e14efd3", "ff665c0a75cadcc4");
            config.max.mrec = new SdkIdConfig.AdUnitId(true, "802c865561f1039d", "6fb959c7b9a69602");
            config.max.interstitial = new SdkIdConfig.AdUnitId(true, "4dec3a272f533458", "c8d31e48f08ed31e");
            config.max.reward = new SdkIdConfig.AdUnitId(true, "cb024cb6279767b8", "02932bb866cbb369");
        }

        // ================== SerializedObject helper ==================

        private static void _SetString(SerializedObject serialized, string nameProperty, string value)
        {
            var _property = serialized.FindProperty(nameProperty);
            if (_property != null && _property.propertyType == SerializedPropertyType.String)
                _property.stringValue = value ?? string.Empty;
        }

        private static string _GetString(SerializedObject serialized, string nameProperty, string fallback)
        {
            var _property = serialized.FindProperty(nameProperty);
            return _property != null && _property.propertyType == SerializedPropertyType.String && !string.IsNullOrEmpty(_property.stringValue)
                ? _property.stringValue
                : fallback;
        }

        private static void _SetFirstArrayString(SerializedObject serialized, string nameProperty, string value)
        {
            var _property = serialized.FindProperty(nameProperty);
            if (_property == null || !_property.isArray)
                return;
            if (_property.arraySize == 0)
                _property.arraySize = 1;
            _property.GetArrayElementAtIndex(0).stringValue = value ?? string.Empty;
        }

        private static string _GetFirstArrayString(SerializedObject serialized, string nameProperty, string fallback)
        {
            var _property = serialized.FindProperty(nameProperty);
            if (_property == null || !_property.isArray || _property.arraySize == 0)
                return fallback;
            string _value = _property.GetArrayElementAtIndex(0).stringValue;
            return string.IsNullOrEmpty(_value) ? fallback : _value;
        }
    }
}
#endif
