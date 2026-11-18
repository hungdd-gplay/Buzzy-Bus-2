using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Khung hiển thị số item thu thập đã nhặt trong level (mỗi event / challenge 1 khung).
/// Khoá theo chuỗi "Kiểu.Giá trị" nên dùng được cho mọi loại nguồn (ChallengeType, EventSaleType, ...).
/// </summary>
public class ItemShowFarmeController : MonoBehaviour
{
    [SerializeField] private ItemShowFarme itemShowFarmePrefab;
    [SerializeField] private Transform parentItem;

    private readonly Dictionary<string, ItemShowFarme> _dicFrame = new Dictionary<string, ItemShowFarme>();

    public void Init()
    {
        foreach (var _frame in _dicFrame.Values)
            if (_frame != null)
                Destroy(_frame.gameObject);
        _dicFrame.Clear();
    }

    public void SetUpFrame(ChallengeType type) => SetUpFrame(_GetKey(type));
    public void SetUpFrame(EventSaleType type) => SetUpFrame(_GetKey(type));
    public ItemShowFarme GetFarme(ChallengeType type) => GetFarme(_GetKey(type));
    public ItemShowFarme GetFarme(EventSaleType type) => GetFarme(_GetKey(type));

    /// <summary>Tạo (hoặc làm mới) khung cho key. Gọi lại khi chơi lại level không bị trùng.</summary>
    public ItemShowFarme SetUpFrame(string key)
    {
        if (itemShowFarmePrefab == null)
            return null;
        if (!_dicFrame.TryGetValue(key, out var _frame) || _frame == null)
        {
            _frame = Instantiate(itemShowFarmePrefab, parentItem != null ? parentItem : transform);
            _dicFrame[key] = _frame;
        }
        _frame.Init();
        _frame.Close();
        return _frame;
    }

    public ItemShowFarme GetFarme(string key)
    {
        return _dicFrame.TryGetValue(key, out var _frame) ? _frame : null;
    }

    private static string _GetKey<TEnum>(TEnum type) where TEnum : struct
    {
        return typeof(TEnum).Name + "." + type;
    }
}
