using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tạo / tái sử dụng popup từ Resources, thay cho đoạn Setup() lặp lại ở mọi popup:
///     public static XBox Setup() => BoxFactory.Get&lt;XBox&gt;(PATH_RESOURCE);
/// Mỗi đường dẫn chỉ có 1 instance. Instance bị huỷ (đổi scene) sẽ tự tạo lại.
/// </summary>
public static class BoxFactory
{
    private static readonly Dictionary<string, BaseBox> _dicInstance = new Dictionary<string, BaseBox>();

    /// <summary>Lấy popup (tạo nếu chưa có) và bật lên (giống Setup() cũ).</summary>
    public static T Get<T>(string resourcePath, bool isActive = true) where T : BaseBox
    {
        var _box = GetInstance<T>(resourcePath);
        if (_box != null && isActive)
            _box.gameObject.SetActive(true);
        return _box;
    }

    /// <summary>Lấy popup nhưng không bật (dùng để set dữ liệu trước rồi mới Show()).</summary>
    public static T GetInstance<T>(string resourcePath) where T : BaseBox
    {
        if (_dicInstance.TryGetValue(resourcePath, out var _cached) && _cached != null)
            return _cached as T;

        var _prefab = Resources.Load<T>(resourcePath);
        if (_prefab == null)
        {
            Debug.LogError($"[BoxFactory] Không tìm thấy {typeof(T).Name} tại Resources/{resourcePath}");
            return null;
        }

        // Tạo dưới 1 object đang tắt => Awake / OnEnable chưa chạy, chỉ chạy khi thật sự mở.
        // (Không SetActive trực tiếp lên prefab vì trong Editor sẽ sửa luôn file prefab.)
        var _box = Object.Instantiate(_prefab, _GetHolderInactive());
        _box.gameObject.SetActive(false);
        _box.transform.SetParent(null, false);
        // Holder nằm trong scene DontDestroyOnLoad -> đưa popup về scene hiện tại (đổi scene thì huỷ như cũ)
        SceneManager.MoveGameObjectToScene(_box.gameObject, SceneManager.GetActiveScene());
        _box.name = _prefab.name;
        _dicInstance[resourcePath] = _box;
        return _box;
    }

    private static Transform _holderInactive;

    private static Transform _GetHolderInactive()
    {
        if (_holderInactive == null)
        {
            var _go = new GameObject("[BoxFactory] Holder");
            _go.SetActive(false);
            Object.DontDestroyOnLoad(_go);
            _holderInactive = _go.transform;
        }
        return _holderInactive;
    }

    /// <summary>Popup đang mở (đã tạo và đang active).</summary>
    public static bool IsShowing(string resourcePath)
    {
        return _dicInstance.TryGetValue(resourcePath, out var _box) && _box != null && _box.gameObject.activeSelf;
    }

    /// <summary>Huỷ instance đã cache (ví dụ để giải phóng bộ nhớ popup nặng).</summary>
    public static void Release(string resourcePath)
    {
        if (_dicInstance.TryGetValue(resourcePath, out var _box) && _box != null)
            Object.Destroy(_box.gameObject);
        _dicInstance.Remove(resourcePath);
    }
}
