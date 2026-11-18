using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nơi lưu trữ toàn bộ dữ liệu tĩnh (ScriptableObject) của game.
///  - Core   : vật phẩm/quà, đăng nhập hằng ngày
///  - Shop   : gói bán (IAP, sale pack, video pack)
///  - Theme  : skin/theme
///  - Event  : dữ liệu các sự kiện ngoài lề nằm ở GameObject con "EventData" (EventDataContain)
/// Có thể tạo/cập nhật data từ CSV/XLSX: Tools > Base Project > Data Converter.
/// </summary>
public class DataContain : MonoBehaviour
{
    [Header("Core")]
    public GiftDatabase giftDatabase;
    public DailyLoginDatabase dailyLoginDatabase;

    [Header("Shop")]
    public IAPDatabase iapDatabase;
    public SalePackDatabase salePackDatabase;
    public PackVideoDatabase packVideoDatabase;

    [Header("Theme")]
    public ThemeSkinDatabase themeSkinDatabase;
    public BaseProject.Theme.ThemeSystemConfig themeSystemConfig;

    [Header("Sound")]
    public SoundDatabase soundDatabase;

    [Header("Event (GameObject con)")]
    public EventDataContain eventDataContain;

    [Header("Data khác (tạo từ Data Converter...)")]
    public List<ScriptableObject> lstDataExtra = new List<ScriptableObject>();

    /// <summary>Lấy data phụ theo kiểu (tìm trong lstDataExtra rồi tới EventDataContain).</summary>
    public T GetData<T>() where T : ScriptableObject
    {
        for (int i = 0; i < lstDataExtra.Count; i++)
        {
            if (lstDataExtra[i] is T _data)
                return _data;
        }
        return eventDataContain != null ? eventDataContain.GetEventData<T>() : null;
    }
}
