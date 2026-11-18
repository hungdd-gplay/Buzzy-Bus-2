using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Tải AssetBundle lúc runtime (dùng chung cho mọi tính năng).
///  - Từ StreamingAssets/AssetBundles/{platform}/{bundle} (gói kèm bản build).
///  - Từ server: {baseUrl}/{platform}/{bundle}, có cache theo version/hash.
///  - Giữ bundle đã tải trong bộ nhớ, tránh tải trùng; Unload khi không dùng.
/// Cấu trúc thư mục khớp với AssetBundle > Build (Editor).
/// </summary>
public static class AssetBundleLoader
{
    public const string FOLDER_STREAMING = "AssetBundles";

    private static readonly Dictionary<string, AssetBundle> _dictBundleLoaded = new Dictionary<string, AssetBundle>();

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

    public static string BuildUrl(string baseUrl, string bundleName)
    {
        return $"{baseUrl.TrimEnd('/')}/{GetPlatformName()}/{bundleName}";
    }

    public static string GetStreamingPath(string bundleName)
    {
        return Path.Combine(Application.streamingAssetsPath, FOLDER_STREAMING, GetPlatformName(), bundleName);
    }

    public static bool IsLoaded(string bundleName) => _dictBundleLoaded.ContainsKey(bundleName);

    public static AssetBundle GetLoaded(string bundleName)
    {
        _dictBundleLoaded.TryGetValue(bundleName, out var _bundle);
        return _bundle;
    }

    /// <summary>Tải bundle gói kèm trong StreamingAssets.</summary>
    public static IEnumerator LoadFromStreamingAssets(string bundleName, Action<AssetBundle, string> onDone)
    {
        string _path = GetStreamingPath(bundleName);
        if (!_path.Contains("://"))
            _path = "file://" + _path;
        yield return _Load(bundleName, _path, 0, 30, onDone);
    }

    /// <summary>Tải bundle từ server. version > 0: cache theo version (tăng version để buộc tải lại).</summary>
    public static IEnumerator LoadFromUrl(string bundleName, string url, uint version, int timeoutSeconds, Action<AssetBundle, string> onDone)
    {
        yield return _Load(bundleName, url, version, timeoutSeconds, onDone);
    }

    /// <summary>Tải 1 asset trong bundle đã tải.</summary>
    public static IEnumerator LoadAsset<T>(string bundleName, string assetName, Action<T> onDone) where T : UnityEngine.Object
    {
        var _bundle = GetLoaded(bundleName);
        if (_bundle == null)
        {
            Debug.LogWarning($"[AssetBundle] Chưa tải bundle {bundleName}");
            onDone?.Invoke(null);
            yield break;
        }
        var _request = _bundle.LoadAssetAsync<T>(assetName);
        yield return _request;
        onDone?.Invoke(_request.asset as T);
    }

    /// <summary>Tải toàn bộ asset kiểu T trong bundle.</summary>
    public static IEnumerator LoadAllAssets<T>(string bundleName, Action<T[]> onDone) where T : UnityEngine.Object
    {
        var _bundle = GetLoaded(bundleName);
        if (_bundle == null)
        {
            onDone?.Invoke(Array.Empty<T>());
            yield break;
        }
        var _request = _bundle.LoadAllAssetsAsync<T>();
        yield return _request;
        var _result = new List<T>();
        foreach (var _asset in _request.allAssets)
            if (_asset is T _typed) _result.Add(_typed);
        onDone?.Invoke(_result.ToArray());
    }

    public static void Unload(string bundleName, bool isUnloadAllObjects = false)
    {
        if (_dictBundleLoaded.TryGetValue(bundleName, out var _bundle))
        {
            if (_bundle != null)
                _bundle.Unload(isUnloadAllObjects);
            _dictBundleLoaded.Remove(bundleName);
        }
    }

    public static void UnloadAll(bool isUnloadAllObjects = false)
    {
        foreach (var _bundle in _dictBundleLoaded.Values)
            if (_bundle != null) _bundle.Unload(isUnloadAllObjects);
        _dictBundleLoaded.Clear();
    }

    private static IEnumerator _Load(string bundleName, string url, uint version, int timeoutSeconds, Action<AssetBundle, string> onDone)
    {
        if (_dictBundleLoaded.TryGetValue(bundleName, out var _bundleCached) && _bundleCached != null)
        {
            onDone?.Invoke(_bundleCached, null);
            yield break;
        }

        using (var _request = version > 0
                   ? UnityWebRequestAssetBundle.GetAssetBundle(url, version, 0)
                   : UnityWebRequestAssetBundle.GetAssetBundle(url))
        {
            _request.timeout = Mathf.Max(1, timeoutSeconds);
            yield return _request.SendWebRequest();

            if (_request.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(null, $"Tải AssetBundle lỗi ({url}): {_request.error}");
                yield break;
            }

            var _bundle = DownloadHandlerAssetBundle.GetContent(_request);
            if (_bundle == null)
            {
                onDone?.Invoke(null, "AssetBundle rỗng hoặc sai nền tảng: " + url);
                yield break;
            }
            _dictBundleLoaded[bundleName] = _bundle;
            onDone?.Invoke(_bundle, null);
        }
    }
}
