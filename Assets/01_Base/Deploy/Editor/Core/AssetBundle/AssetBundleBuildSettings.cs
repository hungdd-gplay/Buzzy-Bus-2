#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BaseProject.EditorTools.AssetBundles
{
    public enum BundleCompression
    {
        [InspectorName("LZ4 (chunk) - load nhanh")] LZ4 = 0,
        [InspectorName("LZMA - nhỏ nhất")] LZMA = 1,
        [InspectorName("Không nén")] None = 2,
    }

    /// <summary>Cấu hình build AssetBundle (Editor). Assets/01_BaseCode/Settings/Editor/AssetBundleBuildSettings.asset</summary>
    public class AssetBundleBuildSettings : ScriptableObject
    {
        public const string PATH_ASSET = ProjectPaths.SETTINGS_EDITOR + "/AssetBundleBuildSettings.asset";

        [Tooltip("Thư mục xuất (tương đối từ thư mục project). Bên trong chia theo platform.")]
        public string folderOutput = "AssetBundles";
        public BundleCompression compression = BundleCompression.LZ4;
        public bool isBuildAndroid = true;
        public bool isBuildIOS = true;
        public bool isBuildWindows = false;
        [Tooltip("Copy bundle vào StreamingAssets/AssetBundles/{platform} để gói kèm bản build.")]
        public bool isCopyToStreamingAssets = false;
        [Tooltip("Xóa file bundle cũ không còn trong danh sách sau khi build.")]
        public bool isCleanOldFiles = true;
        [Tooltip("Ép build lại toàn bộ (bỏ cache).")]
        public bool isForceRebuild = false;
        [Tooltip("URL server (chỉ để ghi vào catalog.json, ví dụ https://cdn.game.com/bundles).")]
        public string baseUrlServer;

        public static AssetBundleBuildSettings LoadOrCreate()
        {
            var _settings = ProjectPaths.FindAsset<AssetBundleBuildSettings>(PATH_ASSET);
            if (_settings != null)
                return _settings;
            ProjectPaths.EnsureFolder(ProjectPaths.SETTINGS_EDITOR);
            _settings = CreateInstance<AssetBundleBuildSettings>();
            AssetDatabase.CreateAsset(_settings, PATH_ASSET);
            AssetDatabase.SaveAssets();
            return _settings;
        }
    }
}
#endif
