using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace BaseProject.Theme
{
    /// <summary>
    /// Controller: tải AssetBundle theme sự kiện qua API.
    /// URL = config.urlAssetBundleFormat với {bundle} = tên bundle (từ RemoteConfig), {platform} = nền tảng.
    /// Có cache theo version (tăng version trên RemoteConfig để bắt tải lại).
    /// </summary>
    public static class ThemeAssetBundleLoader
    {
        public static string BuildUrl(string urlFormat, string bundleName)
        {
            return urlFormat
                .Replace("{bundle}", UnityWebRequest.EscapeURL(bundleName))
                .Replace("{platform}", GetPlatformName());
        }

        public static string GetPlatformName()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android: return "Android";
                case RuntimePlatform.IPhonePlayer: return "iOS";
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor: return "StandaloneOSX";
                default:
#if UNITY_EDITOR
                    return UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString();
#else
                    return "StandaloneWindows64";
#endif
            }
        }

        /// <summary>
        /// Tải bundle và lấy ThemePack đầu tiên bên trong.
        /// onDone(pack, bundle, error): pack/bundle null nếu lỗi.
        /// </summary>
        public static IEnumerator LoadThemePack(string url, uint version, int timeoutSeconds, Action<ThemePack, AssetBundle, string> onDone)
        {
            using (var _request = version > 0
                ? UnityWebRequestAssetBundle.GetAssetBundle(url, version, 0)
                : UnityWebRequestAssetBundle.GetAssetBundle(url))
            {
                _request.timeout = Mathf.Max(1, timeoutSeconds);
                yield return _request.SendWebRequest();

                if (_request.result != UnityWebRequest.Result.Success)
                {
                    onDone?.Invoke(null, null, $"Tải AssetBundle lỗi ({url}): {_request.error}");
                    yield break;
                }

                AssetBundle _bundle = DownloadHandlerAssetBundle.GetContent(_request);
                if (_bundle == null)
                {
                    onDone?.Invoke(null, null, "AssetBundle rỗng hoặc sai nền tảng: " + url);
                    yield break;
                }

                var _requestAssets = _bundle.LoadAllAssetsAsync<ThemePack>();
                yield return _requestAssets;
                ThemePack _pack = _requestAssets.allAssets != null && _requestAssets.allAssets.Length > 0
                    ? _requestAssets.allAssets[0] as ThemePack
                    : null;
                if (_pack == null)
                {
                    _bundle.Unload(true);
                    onDone?.Invoke(null, null, "Không tìm thấy ThemePack trong AssetBundle: " + url);
                    yield break;
                }
                onDone?.Invoke(_pack, _bundle, null);
            }
        }
    }
}
