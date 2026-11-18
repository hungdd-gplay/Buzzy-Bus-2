#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace BaseProject.EditorTools.Build
{
    /// <summary>
    /// Cấu hình build (chỉ Editor, không vào bản build). Sửa qua Build > Build Config.
    /// Android và iOS cấu hình riêng; khi Switch Platform sẽ tự áp dụng cấu hình của nền tảng mới vào PlayerSettings.
    /// </summary>
    public class BuildConfig : ScriptableObject
    {
        public const string PATH_FOLDER = ProjectPaths.SETTINGS_EDITOR;
        public const string PATH_ASSET = PATH_FOLDER + "/BuildConfig.asset";

        [Serializable]
        public class CommonConfig
        {
            public string productName = "My Game";
            public string versionName = "1.0.0";
            [Tooltip("Icon mặc định (để trống = giữ icon hiện tại).")]
            public Texture2D iconApp;
            [Tooltip("Tiền tố product id IAP. Để trống = package Android.")]
            public string iapProductPrefix;
            [Tooltip("Tên file build (để trống = productName).")]
            public string fileNameBuild;
        }

        [Serializable]
        public class AndroidConfig
        {
            public string packageName = "com.company.game";
            public int versionCode = 1;
            [Tooltip("Build .aab: version code = yyyyMMddNN (vd 2026111301, cùng ngày build lại -> 2026111302).")]
            public bool isAutoVersionCodeForAab = true;
            [Tooltip("Build .apk test cũng tăng version code theo ngày.")]
            public bool isAutoVersionCodeForApk = false;

            [Header("Keystore")]
            [Tooltip("Đường dẫn tương đối từ thư mục project hoặc tuyệt đối.")]
            public string keystorePath = ProjectPaths.KEYSTORE + "/game.keystore";
            public string keystorePass;
            public string keyAliasName;
            public string keyAliasPass;
            [Tooltip("Lưu mật khẩu vào EditorPrefs máy này thay vì asset (asset có thể bị commit lên git).")]
            public bool isStorePasswordLocal = false;

            [Header("Build")]
            public bool isArm64 = true;
            public bool isArmV7 = true;
            public bool isUseIL2CPP = true;
            public int minSdkVersion = 23;
        }

        [Serializable]
        public class IOSConfig
        {
            public string bundleIdentifier = "com.company.game";
            [Tooltip("Build number (CFBundleVersion).")]
            public string buildNumber = "1";
            [Tooltip("Mỗi lần Build iOS: build number = yyyyMMddNN.")]
            public bool isAutoBuildNumber = true;
            public string appleTeamId;
            public bool isAutomaticSigning = true;
            public string targetOSVersion = "13.0";
            [Tooltip("App Store id (số) dùng cho link đánh giá.")]
            public string appStoreId;
        }

        public CommonConfig common = new CommonConfig();
        public AndroidConfig android = new AndroidConfig();
        public IOSConfig ios = new IOSConfig();

        private const string KEY_PREFS_PASS = "BaseProject.Build.";

        public string KeystorePass
        {
            get => android.isStorePasswordLocal ? EditorPrefs.GetString(KEY_PREFS_PASS + "keystorePass", "") : android.keystorePass;
            set
            {
                if (android.isStorePasswordLocal) EditorPrefs.SetString(KEY_PREFS_PASS + "keystorePass", value);
                else android.keystorePass = value;
            }
        }

        public string KeyAliasPass
        {
            get => android.isStorePasswordLocal ? EditorPrefs.GetString(KEY_PREFS_PASS + "keyAliasPass", "") : android.keyAliasPass;
            set
            {
                if (android.isStorePasswordLocal) EditorPrefs.SetString(KEY_PREFS_PASS + "keyAliasPass", value);
                else android.keyAliasPass = value;
            }
        }

        public string IapProductPrefix => string.IsNullOrEmpty(common.iapProductPrefix) ? android.packageName : common.iapProductPrefix;
        public string FileNameBuild => string.IsNullOrEmpty(common.fileNameBuild) ? common.productName : common.fileNameBuild;

        public static BuildConfig LoadOrCreate()
        {
            // Tìm theo kiểu để không phụ thuộc vị trí (sau khi tái cấu trúc thư mục)
            var _config = ProjectPaths.FindAsset<BuildConfig>(PATH_ASSET);
            if (_config != null)
                return _config;

            ProjectPaths.EnsureFolder(PATH_FOLDER);
            _config = CreateInstance<BuildConfig>();
            _config._FillFromCurrentProject();
            AssetDatabase.CreateAsset(_config, PATH_ASSET);
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] Tạo " + PATH_ASSET + " (lấy giá trị từ Config.cs cũ + PlayerSettings).");
            return _config;
        }

        /// <summary>Lần đầu: lấy giá trị đang dùng (Config.cs cũ của Bird Sort 2 + PlayerSettings hiện tại).</summary>
        private void _FillFromCurrentProject()
        {
            common.productName = string.IsNullOrEmpty(PlayerSettings.productName) ? "Bird Sort 2: Color Puzzle" : PlayerSettings.productName;
            common.versionName = string.IsNullOrEmpty(PlayerSettings.bundleVersion) ? "6.0.7" : PlayerSettings.bundleVersion;
            common.fileNameBuild = "Bird Sort 2: Color Puzzle";

            android.packageName = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            if (string.IsNullOrEmpty(android.packageName))
                android.packageName = "com.globalplay.birdsort2.color.puzzle";
            android.versionCode = Math.Max(PlayerSettings.Android.bundleVersionCode, 2026100801);
            android.keystorePath = ProjectPaths.KEYSTORE + "/birdsort2.keystore";
            android.keystorePass = "12345678two";
            android.keyAliasName = "birdsort2";
            android.keyAliasPass = "12345678two";
            android.minSdkVersion = Math.Max(23, (int)PlayerSettings.Android.minSdkVersion);

            ios.bundleIdentifier = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS);
            if (string.IsNullOrEmpty(ios.bundleIdentifier))
                ios.bundleIdentifier = android.packageName;
            ios.buildNumber = string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) ? "1" : PlayerSettings.iOS.buildNumber;
            ios.appleTeamId = PlayerSettings.iOS.appleDeveloperTeamID;
            ios.isAutomaticSigning = PlayerSettings.iOS.appleEnableAutomaticSigning;
            ios.targetOSVersion = string.IsNullOrEmpty(PlayerSettings.iOS.targetOSVersionString) ? "13.0" : PlayerSettings.iOS.targetOSVersionString;
            ios.appStoreId = "6689494714";
        }
    }
}
#endif
