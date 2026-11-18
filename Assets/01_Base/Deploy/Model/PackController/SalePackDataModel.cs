using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

public enum TypeSale
{
    Starter = 0,
    FlashSale = 1,
    Weekday = 2,
    Weekend = 3,
    Config = 4,
    PriorityLow = 5
}

public enum TypeWeekday
{
    Even = 0,
    Odd = 1
}

public enum TimeType
{
    NoTimeLife = 0,
    Second = 1,
    Minute = 2,
    Hour = 3,
    Day = 4,
    Week = 5
}

/// <summary>Bấm icon gói ở Home thì làm gì.</summary>
public enum PackClickAction
{
    OpenPopup = 0,  // Mở popup (popupResourcePath)
    BuyDirect = 1   // Mua luôn (ví dụ Remove Ads)
}

/// <summary>Gói chỉ dành cho nhóm người chơi nào.</summary>
public enum PackUserType
{
    All = 0,
    NonPayer = 1,   // Chưa mua gói nào
    Payer = 2       // Đã mua ít nhất 1 gói
}

/// <summary>Điều kiện: phải mua gói khác đủ số lần.</summary>
[Serializable]
public class PackRequireBought
{
    public TypePackIAP typePack;
    public int minBought = 1;
}

/// <summary>
/// Thông số 1 gói IAP (SalePackDatabase): hiển thị, giảm giá, giới hạn mua, điều kiện xuất hiện, gợi ý popup.
/// Lịch xuất hiện (active, level, thời gian hiện, interval) nằm ở segment: SaleSegemntData.packSchedules.
/// Trạng thái người chơi (đã mua, lần xuất hiện hiện tại...) lưu PlayerPrefs theo typePackIAP.
/// </summary>
[Serializable]
public class SalePackDataModel : BaseDataButtonEvent
{
    [Header("========= GÓI ===========")]
    public TypePackIAP typePackIAP;
    public TypeSale typeSale;
    [Tooltip("Gói lớn hơn được ưu tiên hiện trước (icon Home, popup gợi ý).")]
    public int prioritySale;

    [Header("========= HIỂN THỊ ===========")]
    [Tooltip("Chỉ xuất hiện trong Shop.")]
    public bool isOnlyActiveOnShop;
    [Tooltip("Có icon ở Home + tự bật popup gợi ý.")]
    public bool isCanShowPopup;
    [ShowIf("isCanShowPopup", true)] public BaseButtonEvent buttonShow;
    [Tooltip("Hiện banner trong Shop.")]
    public bool isShowShop;
    [ShowIf("isShowShop", true)] public ShopItemBase shopBanner;
    [Tooltip("Hiện gói nhỏ dưới các popup (QuitLevel, BuyItem, WinLevel).")]
    public bool isShowInPopup;
    [ShowIf("isShowInPopup", true)] public PopupItemBase shopPopup;
    public PackClickAction clickAction = PackClickAction.OpenPopup;
    [Tooltip("Đường dẫn popup trong Resources (ví dụ UI/Popups/SalePacks/StarterPackBox). Nhiều gói có thể dùng chung 1 popup.")]
    public string popupResourcePath;
    [Tooltip("Phần trăm giảm giá hiển thị trên popup / banner.")]
    public float salePercent;

    [Header("========= GIỚI HẠN MUA ===========")]
    [Tooltip("Số lần mua tối đa trong 1 lần xuất hiện (mua đủ thì ẩn tới lần xuất hiện sau). 0 = không giới hạn.")]
    [FormerlySerializedAs("buyTimes")]
    public int maxBuyPerAppear = 1;
    [Tooltip("Tổng số lần mua tối đa (mua đủ thì không xuất hiện nữa). 0 = không giới hạn.")]
    public int maxBuyTotal;

    [Header("========= ĐIỀU KIỆN XUẤT HIỆN ===========")]
    public PackUserType userType = PackUserType.All;
    [Tooltip("Chỉ xuất hiện khi đã mua các gói này đủ số lần.")]
    public List<PackRequireBought> requireBought = new List<PackRequireBought>();
    [Tooltip("Ẩn khi đã mua 1 trong các gói này.")]
    public List<TypePackIAP> hideIfBought = new List<TypePackIAP>();
    [Tooltip("Số level phải chơi trong ngày. 0 = không cần.")]
    public int minLevelPlayInDay;
    [Tooltip("Ẩn khi người chơi đã Remove Ads.")]
    public bool isHideWhenRemoveAds;
    [Tooltip("Chỉ xuất hiện trong thời gian event sale này (hết event thì mất). None = không phụ thuộc.")]
    public EventSaleType eventSale = EventSaleType.None;
    [Tooltip("Các gói cùng nhóm: mỗi người chơi chỉ thấy 1 gói (gói đầu tiên đủ điều kiện), mua 1 gói thì cả nhóm ẩn. Để trống = không nhóm.")]
    public string groupExclusive;

    [Header("========= GỢI Ý POPUP Ở HOME ===========")]
    [Tooltip("Chỉ gợi ý khi đã xem >= số quảng cáo này trong lượt chơi. 0 = bỏ qua.")]
    public int suggestMinAdsInPlay;
    [Tooltip("Chỉ gợi ý khi đã xem >= số quảng cáo này trong ngày. 0 = bỏ qua.")]
    public int suggestMinAdsInDay;

    [Tooltip("Thông số tuỳ gói (gói đặc biệt kế thừa PackBaseController đọc thêm).")]
    public float[] workValue;

    public bool IsHasSuggestAdsCondition => suggestMinAdsInPlay > 0 || suggestMinAdsInDay > 0;

    public override bool Equals(object obj)
    {
        return obj is SalePackDataModel _other && _other.typePackIAP == typePackIAP;
    }

    public override int GetHashCode()
    {
        return (int)typePackIAP;
    }

    #region Trạng thái người chơi (PlayerPrefs)

    private const string _KEY_CYCLE_START = "PACK_CYCLE_START_";
    private const string _KEY_CYCLE_END = "PACK_CYCLE_END_";

    /// <summary>Số lần mua trong lần xuất hiện hiện tại.</summary>
    public int NumOfPurchased
    {
        get => PlayerPrefs.GetInt(StringHelper.NUM_OF_PURCHASED_ + typePackIAP, 0);
        set
        {
            GPlayerPrefs.SetInt(StringHelper.NUM_OF_PURCHASED_ + typePackIAP, value);
            GPlayerPrefs.Save();
        }
    }

    /// <summary>Tổng số lần đã mua.</summary>
    public int boughtTimes
    {
        get
        {
            int _bought = PlayerPrefs.GetInt(StringHelper.BOUGHT_TIMES_ + typePackIAP, 0);
            // Dữ liệu cũ: gói mua 1 lần chỉ lưu cờ IS_PACK_PURCHASED_
            if (_bought == 0 && PlayerPrefs.GetInt(StringHelper.IS_PACK_PURCHASED_ + typePackIAP, 1) == 0)
                _bought = 1;
            return _bought;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.BOUGHT_TIMES_ + typePackIAP, value);
            GPlayerPrefs.Save();
        }
    }

    public bool IsBoughtOutTotal => maxBuyTotal > 0 && boughtTimes >= maxBuyTotal;
    public bool IsBoughtOutInAppear => maxBuyPerAppear > 0 && NumOfPurchased >= maxBuyPerAppear;

    public bool IsHasCycle => PlayerPrefs.HasKey(_KEY_CYCLE_END + typePackIAP);

    /// <summary>Thời điểm bắt đầu lần xuất hiện hiện tại.</summary>
    public DateTime CycleStart
    {
        get => _GetTime(_KEY_CYCLE_START + typePackIAP, DateTime.MinValue);
        set => _SetTime(_KEY_CYCLE_START + typePackIAP, value);
    }

    /// <summary>Thời điểm kết thúc lần xuất hiện hiện tại (DateTime.MaxValue = không mất).</summary>
    public DateTime CycleEnd
    {
        get => _GetTime(_KEY_CYCLE_END + typePackIAP, DateTime.MinValue);
        set => _SetTime(_KEY_CYCLE_END + typePackIAP, value);
    }

    /// <summary>Giữ cho popup cũ (đếm ngược).</summary>
    public DateTime PackTimeEnd => CycleEnd;

    public DateTime LastTimeShowHome
    {
        get => _GetTime(StringHelper.LAST_TIME_PACK_SHOW_HOME_ + typePackIAP, DateTime.MinValue);
        set => _SetTime(StringHelper.LAST_TIME_PACK_SHOW_HOME_ + typePackIAP, value);
    }

    public void SetTimeShowHomeNow()
    {
        LastTimeShowHome = UnbiasedTime.Instance.Now();
    }

    /// <summary>Gọi khi mua thành công.</summary>
    public void OnPurchased()
    {
        NumOfPurchased++;
        boughtTimes++;
    }

    /// <summary>Giữ tên cũ.</summary>
    public void CheckPurchased()
    {
        OnPurchased();
    }

    public void StartCycle(DateTime start, DateTime end)
    {
        CycleStart = start;
        CycleEnd = end;
        NumOfPurchased = 0;
    }

    public string GetRemainTimeFormat(long time)
    {
        return TimeManager.ShowTime3(time);
    }

    private static DateTime _GetTime(string key, DateTime defaultValue)
    {
        if (!PlayerPrefs.HasKey(key))
            return defaultValue;
        return long.TryParse(PlayerPrefs.GetString(key), out long _binary) ? DateTime.FromBinary(_binary) : defaultValue;
    }

    private static void _SetTime(string key, DateTime value)
    {
        GPlayerPrefs.SetString(key, value.ToBinary().ToString());
        GPlayerPrefs.Save();
    }

    #endregion
}
