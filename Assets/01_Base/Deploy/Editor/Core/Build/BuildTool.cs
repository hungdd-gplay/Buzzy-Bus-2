#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BaseProject.EditorTools.Build
{
    /// <summary>
    /// Logic build Android / iOS dùng BuildConfig. Menu "Build" trên thanh menu.
    ///  - Build .aab: version code tự tăng theo ngày yyyyMMddNN (chỉ lưu khi build thành công).
    ///  - Test build: thêm define TEST_BUILD, giữ nguyên các define khác (FEATURE_...).
    ///  - Switch Platform: tự áp dụng cấu hình nền tảng mới.
    /// </summary>
    public static class BuildTool
    {
        private const string PATH_MENU = "Build/";
        private const string DEFINE_TEST_BUILD = "TEST_BUILD";
        private const string PATH_FOLDER_RESOURCES_CONFIG = "Assets/Resources/Config";
        private const string PATH_BUILD_INFO = PATH_FOLDER_RESOURCES_CONFIG + "/BuildInfo.asset";

        public static string FolderBuild => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));

        // ================== Menu ==================

        [MenuItem(PATH_MENU + "Build Config...", false, 0)]
        private static void MenuOpenConfig() => BuildConfigWindow.Open();

        [MenuItem(PATH_MENU + "Áp dụng config vào Player Settings", false, 1)]
        private static void MenuApply()
        {
            ApplyToPlayerSettings(BuildConfig.LoadOrCreate(), EditorUserBuildSettings.activeBuildTarget);
        }

        [MenuItem(PATH_MENU + "Android - Build APK (Test)", false, 20)]
        private static void MenuBuildApkTest() => BuildAndroid(false, true);

        [MenuItem(PATH_MENU + "Android - Build APK (Release)", false, 21)]
        private static void MenuBuildApkRelease() => BuildAndroid(false, false);

        [MenuItem(PATH_MENU + "Android - Build AAB (Release)", false, 22)]
        private static void MenuBuildAab() => BuildAndroid(true, false);

        [MenuItem(PATH_MENU + "iOS - Build Xcode (Release)", false, 40)]
        private static void MenuBuildIOS() => BuildIOS(false);

        [MenuItem(PATH_MENU + "iOS - Build Xcode (Test)", false, 41)]
        private static void MenuBuildIOSTest() => BuildIOS(true);

        [MenuItem(PATH_MENU + "Switch Platform/Android", false, 60)]
        private static void MenuSwitchAndroid() => SwitchPlatform(BuildTarget.Android);

        [MenuItem(PATH_MENU + "Switch Platform/iOS", false, 61)]
        private static void MenuSwitchIOS() => SwitchPlatform(BuildTarget.iOS);

        [MenuItem(PATH_MENU + "Mở thư mục Builds", false, 80)]
        public static void OpenFolderBuild()
        {
            Directory.CreateDirectory(FolderBuild);
            EditorUtility.RevealInFinder(FolderBuild + Path.DirectorySeparatorChar);
        }

        // ================== Version code theo ngày ==================

        /// <summary>yyyyMMdd * 100 + số thứ tự trong ngày. Vd 13/11/2026: 2026111301, build lại cùng ngày: 2026111302.</summary>
        public static int ComputeNextDateVersionCode(int lastVersionCode, DateTime now)
        {
            int _codeBaseToday = int.Parse(now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)) * 100;
            if (lastVersionCode >= _codeBaseToday + 99)
            {
                Debug.LogWarning("[Build] Đã quá 99 bản trong ngày hoặc version code cũ lớn hơn ngày hiện tại, dùng last + 1.");
                return lastVersionCode + 1;
            }
            if (lastVersionCode >= _codeBaseToday)
                return lastVersionCode + 1;
            return _codeBaseToday + 1;
        }

        public static int GetNextAndroidVersionCode(BuildConfig config)
        {
            int _last = Math.Max(config.android.versionCode, PlayerSettings.Android.bundleVersionCode);
            return ComputeNextDateVersionCode(_last, DateTime.Now);
        }

        public static string GetNextIOSBuildNumber(BuildConfig config)
        {
            int.TryParse(config.ios.buildNumber, out int _last);
            int.TryParse(PlayerSettings.iOS.buildNumber, out int _lastPlayer);
            return ComputeNextDateVersionCode(Math.Max(_last, _lastPlayer), DateTime.Now).ToString();
        }

        // ================== Áp dụng ==================

        /// <summary>Ghi cấu hình vào PlayerSettings cho nền tảng target (không đổi version code).</summary>
        public static void ApplyToPlayerSettings(BuildConfig config, BuildTarget target)
        {
            PlayerSettings.productName = config.common.productName;
            PlayerSettings.bundleVersion = config.common.versionName;
            if (config.common.iconApp != null)
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { config.common.iconApp }, IconKind.Any);

            if (target == BuildTarget.Android)
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, config.android.packageName);
                PlayerSettings.Android.bundleVersionCode = config.android.versionCode;
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)config.android.minSdkVersion;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,
                    config.android.isUseIL2CPP ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);

                AndroidArchitecture _architecture = AndroidArchitecture.None;
                if (config.android.isArmV7) _architecture |= AndroidArchitecture.ARMv7;
                if (config.android.isArm64 && config.android.isUseIL2CPP) _architecture |= AndroidArchitecture.ARM64;
                if (_architecture == AndroidArchitecture.None) _architecture = AndroidArchitecture.ARMv7;
                PlayerSettings.Android.targetArchitectures = _architecture;

                bool _isHasKeystore = !string.IsNullOrEmpty(config.android.keystorePath) && File.Exists(_GetFullPath(config.android.keystorePath));
                PlayerSettings.Android.useCustomKeystore = _isHasKeystore;
                if (_isHasKeystore)
                {
                    PlayerSettings.Android.keystoreName = config.android.keystorePath;
                    PlayerSettings.Android.keystorePass = config.KeystorePass;
                    PlayerSettings.Android.keyaliasName = config.android.keyAliasName;
                    PlayerSettings.Android.keyaliasPass = config.KeyAliasPass;
                }
                else
                {
                    Debug.LogWarning("[Build] Không thấy keystore: " + config.android.keystorePath + " -> build bằng debug key.");
                }
            }
            else if (target == BuildTarget.iOS)
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, config.ios.bundleIdentifier);
                PlayerSettings.iOS.buildNumber = config.ios.buildNumber;
                PlayerSettings.iOS.appleDeveloperTeamID = config.ios.appleTeamId;
                PlayerSettings.iOS.appleEnableAutomaticSigning = config.ios.isAutomaticSigning;
                PlayerSettings.iOS.targetOSVersionString = config.ios.targetOSVersion;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            }

            AssetDatabase.SaveAssets();
            Debug.LogFormat("[Build] Đã áp dụng config {0}: package={1} | version={2} | code={3} | name={4}",
                target, PlayerSettings.applicationIdentifier, PlayerSettings.bundleVersion,
                target == BuildTarget.iOS ? PlayerSettings.iOS.buildNumber : PlayerSettings.Android.bundleVersionCode.ToString(),
                PlayerSettings.productName);
        }

        /// <summary>Ghi BuildInfo (Resources) để runtime đọc version code / tiền tố IAP.</summary>
        public static void WriteBuildInfo(BuildConfig config, BuildTarget target, bool isTestBuild)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(PATH_FOLDER_RESOURCES_CONFIG))
                AssetDatabase.CreateFolder("Assets/Resources", "Config");

            var _buildInfo = AssetDatabase.LoadAssetAtPath<BuildInfo>(PATH_BUILD_INFO);
            if (_buildInfo == null)
            {
                _buildInfo = ScriptableObject.CreateInstance<BuildInfo>();
                AssetDatabase.CreateAsset(_buildInfo, PATH_BUILD_INFO);
            }

            int _versionCode = config.android.versionCode;
            if (target == BuildTarget.iOS)
                int.TryParse(PlayerSettings.iOS.buildNumber, out _versionCode);
            else if (target == BuildTarget.Android)
                _versionCode = PlayerSettings.Android.bundleVersionCode;

            _buildInfo.versionCode = _versionCode;
            _buildInfo.versionName = PlayerSettings.bundleVersion;
            _buildInfo.iapProductPrefix = config.IapProductPrefix;
            _buildInfo.isTestBuild = isTestBuild;
            _buildInfo.buildTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            EditorUtility.SetDirty(_buildInfo);
            AssetDatabase.SaveAssets();
        }

        // ================== Build ==================

        public static void BuildAndroid(bool isAab, bool isTestBuild)
        {
            if (!_EnsurePlatform(BuildTarget.Android))
                return;

            var _config = BuildConfig.LoadOrCreate();
            ApplyToPlayerSettings(_config, BuildTarget.Android);

            int _versionCodeOld = PlayerSettings.Android.bundleVersionCode;
            bool _isAutoVersion = isAab ? _config.android.isAutoVersionCodeForAab : _config.android.isAutoVersionCodeForApk;
            int _versionCode = _isAutoVersion ? GetNextAndroidVersionCode(_config) : _config.android.versionCode;
            PlayerSettings.Android.bundleVersionCode = _versionCode;

            EditorUserBuildSettings.buildAppBundle = isAab;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
            _SetTestDefine(NamedBuildTarget.Android, isTestBuild);
            WriteBuildInfo(_config, BuildTarget.Android, isTestBuild);

            string _fileName = $"{_SafeFileName(_config.FileNameBuild)}_v{PlayerSettings.bundleVersion}_vc{_versionCode}_{DateTime.Now:yyMMdd_HHmm}{(isTestBuild ? "_TEST" : "")}.{(isAab ? "aab" : "apk")}";
            string _folder = Path.Combine(FolderBuild, "Android");
            Directory.CreateDirectory(_folder);
            string _path = Path.Combine(_folder, _fileName);

            var _report = BuildPipeline.BuildPlayer(_GetScenes(), _path, BuildTarget.Android, BuildOptions.None);
            if (_report.summary.result == BuildResult.Succeeded)
            {
                if (_isAutoVersion)
                {
                    Undo.RecordObject(_config, "Version code");
                    _config.android.versionCode = _versionCode;
                    EditorUtility.SetDirty(_config);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log($"[Build] Thành công: {_path} ({_report.summary.totalSize / 1048576f:0.0} MB)");
                EditorUtility.RevealInFinder(_path);
            }
            else
            {
                PlayerSettings.Android.bundleVersionCode = _versionCodeOld;
                Debug.LogError($"[Build] Thất bại ({_report.summary.result}), giữ version code {_versionCodeOld}.");
            }
        }

        public static void BuildIOS(bool isTestBuild)
        {
            if (!_EnsurePlatform(BuildTarget.iOS))
                return;

            var _config = BuildConfig.LoadOrCreate();
            ApplyToPlayerSettings(_config, BuildTarget.iOS);

            string _buildNumberOld = PlayerSettings.iOS.buildNumber;
            string _buildNumber = _config.ios.isAutoBuildNumber ? GetNextIOSBuildNumber(_config) : _config.ios.buildNumber;
            PlayerSettings.iOS.buildNumber = _buildNumber;
            _SetTestDefine(NamedBuildTarget.iOS, isTestBuild);
            WriteBuildInfo(_config, BuildTarget.iOS, isTestBuild);

            string _path = Path.Combine(FolderBuild, "iOS", _SafeFileName(_config.FileNameBuild) + (isTestBuild ? "_TEST" : ""));
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            var _report = BuildPipeline.BuildPlayer(_GetScenes(), _path, BuildTarget.iOS, BuildOptions.None);
            if (_report.summary.result == BuildResult.Succeeded)
            {
                if (_config.ios.isAutoBuildNumber)
                {
                    _config.ios.buildNumber = _buildNumber;
                    EditorUtility.SetDirty(_config);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log("[Build] Xcode project: " + _path);
                EditorUtility.RevealInFinder(_path);
            }
            else
            {
                PlayerSettings.iOS.buildNumber = _buildNumberOld;
                Debug.LogError($"[Build] Thất bại ({_report.summary.result}).");
            }
        }

        public static void SwitchPlatform(BuildTarget target)
        {
            if (EditorUserBuildSettings.activeBuildTarget == target)
            {
                ApplyToPlayerSettings(BuildConfig.LoadOrCreate(), target);
                return;
            }
            var _group = BuildPipeline.GetBuildTargetGroup(target);
            EditorUserBuildSettings.SwitchActiveBuildTarget(_group, target);
            // ActiveBuildTargetListener sẽ áp dụng config sau khi switch xong.
        }

        // ================== Private ==================

        private static bool _EnsurePlatform(BuildTarget target)
        {
            if (EditorUserBuildSettings.activeBuildTarget == target)
                return true;
            if (EditorUtility.DisplayDialog("Build", $"Platform hiện tại là {EditorUserBuildSettings.activeBuildTarget}. Chuyển sang {target}?\n(Sau khi chuyển xong, bấm build lại.)", "Chuyển", "Hủy"))
                SwitchPlatform(target);
            return false;
        }

        private static void _SetTestDefine(NamedBuildTarget target, bool isTestBuild)
        {
            var _lstDefine = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim()).Where(d => d.Length > 0 && d != DEFINE_TEST_BUILD).ToList();
            if (isTestBuild)
                _lstDefine.Add(DEFINE_TEST_BUILD);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", _lstDefine));
        }

        private static string[] _GetScenes()
        {
            return EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        }

        private static string _GetFullPath(string path)
        {
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }

        private static string _SafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_').Replace(':', '_');
        }
    }

    /// <summary>Mở project: tạo BuildConfig + BuildInfo nếu chưa có (để Play trong Editor đọc được version).</summary>
    [InitializeOnLoad]
    public static class BuildConfigBootstrap
    {
        static BuildConfigBootstrap()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                var _config = BuildConfig.LoadOrCreate();
                if (AssetDatabase.LoadAssetAtPath<BuildInfo>("Assets/Resources/Config/BuildInfo.asset") == null)
                    BuildTool.WriteBuildInfo(_config, EditorUserBuildSettings.activeBuildTarget, false);
            };
        }
    }

    /// <summary>Đổi platform -> tự áp dụng cấu hình của platform mới.</summary>
    public class ActiveBuildTargetListener : IActiveBuildTargetChanged
    {
        public int callbackOrder => 0;

        public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
        {
            if (newTarget == BuildTarget.Android || newTarget == BuildTarget.iOS)
                BuildTool.ApplyToPlayerSettings(BuildConfig.LoadOrCreate(), newTarget);
        }
    }

    /// <summary>Mọi bản build (kể cả bấm Build trong Build Profiles) đều ghi BuildInfo cho runtime.</summary>
    public class BuildInfoPreprocess : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            var _config = BuildConfig.LoadOrCreate();
            bool _isTest = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(report.summary.platformGroup)).Contains("TEST_BUILD");
            BuildTool.WriteBuildInfo(_config, report.summary.platform, _isTest);
        }
    }
}
#endif
