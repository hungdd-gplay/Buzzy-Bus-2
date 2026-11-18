using System.Collections.Generic;
using UnityEngine;

namespace BaseProject.Theme
{
    /// <summary>
    /// Model: cấu hình hệ thống Theme. Đặt tại Resources/Theme/ThemeSystemConfig.asset
    /// (Tools > Base Project > Theme Manager > "Tạo cấu trúc" sẽ tự tạo).
    /// </summary>
    [CreateAssetMenu(fileName = "ThemeSystemConfig", menuName = "Base Project/Theme/Theme System Config")]
    public class ThemeSystemConfig : ScriptableObject
    {
        public const string PATH_RESOURCES = "Theme/ThemeSystemConfig";

        [Header("Bật / tắt tính năng")]
        public bool isEnableTheme = true;
        [Tooltip("Cho phép đổi theme theo sự kiện (RemoteConfig / gọi code)")]
        public bool isEnableEventTheme = true;
        [Tooltip("Cho phép tải theme sự kiện từ AssetBundle")]
        public bool isEnableAssetBundle = true;
        public bool isEnableLoading = true;
        public bool isEnableHome = true;
        public bool isEnableGameplay = true;

        [Header("Theme")]
        [Tooltip("Concept mặc định của game (bị thay khi đổi concept)")]
        public ThemePack packDefault;
        [Tooltip("Theme sự kiện có sẵn trong build (không cần tải)")]
        public List<ThemePack> packsEventLocal = new List<ThemePack>();

        [Header("RemoteConfig")]
        [Tooltip("Key int: id sự kiện đang chạy (ThemeEventId). 0 = mặc định")]
        public string keyRemoteEventId = "theme_event_id";
        [Tooltip("Key string: tên (mã định danh) AssetBundle của sự kiện")]
        public string keyRemoteBundleName = "theme_event_bundle";
        [Tooltip("Key int: version AssetBundle (tăng lên để bắt tải lại, cache theo version)")]
        public string keyRemoteBundleVersion = "theme_event_bundle_version";
        [Tooltip("Thời gian tối đa chờ RemoteConfig (giây) trước khi dùng theme mặc định")]
        public float timeoutWaitRemoteConfig = 8f;

        [Header("AssetBundle API")]
        [Tooltip("{bundle} = tên AssetBundle từ RemoteConfig, {platform} = Android / iOS / StandaloneWindows64...")]
        public string urlAssetBundleFormat = "https://your-cdn.example.com/assetbundles/{platform}/{bundle}";
        public int timeoutDownloadSeconds = 30;

        [Header("Test trong Editor")]
        public bool isUseEditorTestEvent = false;
        public ThemeEventId eventIdEditorTest = ThemeEventId.Default;

        public bool IsCategoryEnabled(ThemeCategory category)
        {
            switch (category)
            {
                case ThemeCategory.Loading: return isEnableLoading;
                case ThemeCategory.Home: return isEnableHome;
                case ThemeCategory.Gameplay: return isEnableGameplay;
                default: return true;
            }
        }

        public ThemePack GetPackEventLocal(ThemeEventId eventId)
        {
            if (eventId == ThemeEventId.Default)
                return packDefault;
            foreach (var _pack in packsEventLocal)
            {
                if (_pack != null && _pack.eventId == eventId)
                    return _pack;
            }
            return null;
        }
    }
}
