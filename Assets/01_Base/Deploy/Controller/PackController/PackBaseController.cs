using System;
using UnityEngine;
using static SaleSegemntData;

/// <summary>
/// Controller chung cho mọi gói IAP. Đọc thông số từ SalePackDatabase (packModel) và lịch từ segment (schedule).
/// Tự tính: có xuất hiện không, lần xuất hiện bắt đầu / kết thúc khi nào, chờ interval, gợi ý popup, mở popup.
/// Gói có logic đặc biệt: kế thừa class này, override các hàm *Handle, đặt component làm con của PackController
/// với typePack tương ứng (PackController sẽ dùng nó thay vì tạo controller mặc định).
/// </summary>
public class PackBaseController : MonoBehaviour
{
    public TypePackIAP typePack;

    [HideInInspector] public SalePackDataModel packModel;
    protected PackController controller;

    /// <summary>Đang xuất hiện (sau lần RefreshState gần nhất).</summary>
    public bool IsActiveNow { get; private set; }
    public PackScheduleConfig Schedule => controller != null ? controller.GetSchedule(typePack) : null;
    public bool IsForever => packModel != null && packModel.CycleEnd == DateTime.MaxValue;

    #region Init

    public virtual void Init(PackController packController, SalePackDataModel model)
    {
        controller = packController;
        packModel = model;
        if (packModel != null)
            typePack = packModel.typePackIAP;
        OnInitHandle();
    }

    #endregion

    #region Trạng thái xuất hiện

    /// <summary>Tính lại trạng thái; bắt đầu lần xuất hiện mới nếu đủ điều kiện và đã qua interval.</summary>
    public void RefreshState(DateTime now)
    {
        IsActiveNow = _EvaluateActive(now);
    }

    /// <summary>Giữ tên cũ: gói đang xuất hiện.</summary>
    public virtual bool CheckActive()
    {
        RefreshState(UnbiasedTime.Instance.Now());
        return IsActiveNow;
    }

    private bool _EvaluateActive(DateTime now)
    {
        var _schedule = Schedule;
        if (packModel == null || _schedule == null)
            return false;
        if (!IsEligible(now, _schedule))
            return false;

        bool _isInCycle = packModel.IsHasCycle && now >= packModel.CycleStart && now < packModel.CycleEnd;
        if (!_isInCycle)
        {
            // Đang chờ interval sau lần xuất hiện trước
            if (packModel.IsHasCycle && packModel.CycleEnd != DateTime.MaxValue && now < _AddSecondsSafe(packModel.CycleEnd, _schedule.intervalReappearSeconds))
                return false;
            packModel.StartCycle(now, ComputeCycleEnd(now, _schedule));
            OnCycleStartHandle();
        }

        if (packModel.IsBoughtOutInAppear)
            return false;
        return IsActiveHandle(now);
    }

    /// <summary>Đủ điều kiện xuất hiện (chưa xét lần xuất hiện / interval).</summary>
    public virtual bool IsEligible(DateTime now, PackScheduleConfig schedule)
    {
        if (!schedule.isActive)
            return false;
        if (UseProfile.CurrentLevel < schedule.levelUnlock)
            return false;
        if (!schedule.IsDayAllowed(now) || !schedule.IsInTimeWindow(now))
            return false;
        if (packModel.IsBoughtOutTotal)
            return false;
        if (packModel.isHideWhenRemoveAds && GameController.Instance.useProfile.IsRemoveAds)
            return false;
        if (packModel.minLevelPlayInDay > 0 && UseProfile.LevelPlayInDay < packModel.minLevelPlayInDay)
            return false;

        switch (packModel.userType)
        {
            case PackUserType.NonPayer when IsBoughtAnyPack():
            case PackUserType.Payer when !IsBoughtAnyPack():
                return false;
        }

        foreach (var _require in packModel.requireBought)
            if (_require != null && CheckBought(_require.typePack) < _require.minBought)
                return false;
        foreach (var _typeHide in packModel.hideIfBought)
            if (CheckBought(_typeHide) > 0)
                return false;

        if (packModel.eventSale != EventSaleType.None && !_IsEventSaleRunning(now, out _))
            return false;
        if (!string.IsNullOrEmpty(packModel.groupExclusive) && controller != null && !controller.IsChosenInGroup(this))
            return false;

        return IsEligibleHandle(now);
    }

    /// <summary>Thời điểm kết thúc 1 lần xuất hiện bắt đầu lúc now.</summary>
    public virtual DateTime ComputeCycleEnd(DateTime now, PackScheduleConfig schedule)
    {
        DateTime _end;
        switch (schedule.durationType)
        {
            case PackDurationType.Duration:
                _end = _AddSecondsSafe(now, Math.Max(1, schedule.durationSeconds));
                break;
            case PackDurationType.UntilEndOfDay:
                _end = now.Date.AddDays(1);
                break;
            case PackDurationType.UntilEndOfWeek:
                _end = _GetNextMonday(now);
                break;
            case PackDurationType.UntilEndOfActiveDays:
                if (schedule.isAllWeek)
                {
                    _end = _GetNextMonday(now);
                    break;
                }
                _end = now.Date.AddDays(1);
                for (int i = 0; i < 7 && schedule.IsDayAllowed(_end); i++)
                    _end = _end.AddDays(1);
                break;
            default:
                _end = DateTime.MaxValue;
                break;
        }

        // Không vượt quá khoảng thời gian / event
        if (schedule.TryGetTimeEnd(out var _timeEnd) && _timeEnd < _end)
            _end = _timeEnd;
        if (packModel.eventSale != EventSaleType.None && _IsEventSaleRunning(now, out var _eventEnd) && _eventEnd < _end)
            _end = _eventEnd;
        return _end;
    }

    /// <summary>Số giây còn lại của lần xuất hiện. -1 nếu không mất.</summary>
    public virtual long GetTimeEnd()
    {
        if (packModel == null || IsForever)
            return -1;
        return Math.Max(0, (long)(packModel.CycleEnd - UnbiasedTime.Instance.Now()).TotalSeconds);
    }

    #endregion

    #region Mua / popup

    /// <summary>PackController gọi khi mua thành công bất kỳ gói nào.</summary>
    public virtual void LoadPack(TypePackIAP typePackBought)
    {
        if (packModel == null || typePackBought != typePack)
            return;
        packModel.OnPurchased();
        OnPurchasedHandle();
    }

    public virtual bool IsCanShowSugget()
    {
        var _schedule = Schedule;
        if (packModel == null || _schedule == null || !IsActiveNow)
            return false;
        if (!packModel.isCanShowPopup || packModel.isOnlyActiveOnShop)
            return false;

        bool _isUnlockLevel = UseProfile.CurrentLevel == _schedule.levelUnlock;
        if (!_isUnlockLevel && packModel.IsHasSuggestAdsCondition)
        {
            bool _isEnoughAds = (packModel.suggestMinAdsInPlay > 0 && UseProfile.NumberOfAdsInPlay >= packModel.suggestMinAdsInPlay)
                                || (packModel.suggestMinAdsInDay > 0 && UseProfile.NumberOfAdsInDay >= packModel.suggestMinAdsInDay);
            if (!_isEnoughAds)
                return false;
        }

        if (TimeManager.CaculateTime(packModel.LastTimeShowHome, UnbiasedTime.Instance.Now()) < _schedule.intervalSuggestSeconds)
            return false;
        return IsCanShowSuggetHandle();
    }

    public virtual void OnShowPopupSuggetHome()
    {
        packModel.SetTimeShowHomeNow();
        if (packModel.IsHasSuggestAdsCondition)
        {
            UseProfile.NumberOfAdsInPlay = 0;
            UseProfile.NumberOfAdsInDay = 0;
        }
    }

    public virtual void ShowPopup(bool isPostEventPopupClose)
    {
        if (packModel == null)
            return;
        if (packModel.clickAction == PackClickAction.BuyDirect)
        {
            IAPManager.BuyProduct(typePack);
            return;
        }
        if (string.IsNullOrEmpty(packModel.popupResourcePath))
        {
            Debug.LogWarning($"[Pack] {typePack} chưa gán popupResourcePath trong SalePackDatabase.");
            return;
        }
        BasePackBox.Open(packModel.popupResourcePath, typePack, isPostEventPopupClose);
    }

    public virtual void OnClickIconBtn()
    {
        ShowPopup(false);
    }

    public virtual void ResetDataNewDay()
    {
    }

    #endregion

    #region Hàm để trống cho gói đặc biệt

    protected virtual void OnInitHandle()
    {
    }

    /// <summary>Điều kiện riêng (ngoài các điều kiện trong SalePackDatabase).</summary>
    protected virtual bool IsEligibleHandle(DateTime now)
    {
        return true;
    }

    /// <summary>Điều kiện riêng khi đang trong 1 lần xuất hiện.</summary>
    protected virtual bool IsActiveHandle(DateTime now)
    {
        return true;
    }

    protected virtual void OnCycleStartHandle()
    {
    }

    protected virtual void OnPurchasedHandle()
    {
    }

    protected virtual bool IsCanShowSuggetHandle()
    {
        return true;
    }

    #endregion

    #region Helper

    public static int CheckBought(TypePackIAP typePackIAP)
    {
        var _pack = GameController.Instance.dataContain.salePackDatabase.GetPack(typePackIAP);
        return _pack != null ? _pack.boughtTimes : 0;
    }

    public static bool IsBoughtAnyPack()
    {
        var _database = GameController.Instance.dataContain.salePackDatabase;
        foreach (var _pack in _database.packs)
            if (_pack != null && _pack.boughtTimes > 0)
                return true;
        return false;
    }

    /// <summary>Event sale của gói (packModel.eventSale) đang chạy; end = lúc event kết thúc.</summary>
    private bool _IsEventSaleRunning(DateTime now, out DateTime end)
    {
        end = DateTime.MaxValue;
        var _endless = GameController.Instance.segmentController.GetCurrentSegment().saleData.GetEndLessActive();
        if (_endless == null || _endless.gameTheme != packModel.eventSale)
            return false;
        end = _endless.end;
        return now < _endless.end;
    }

    private static DateTime _GetNextMonday(DateTime now)
    {
        int _days = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        if (_days == 0)
            _days = 7;
        return now.Date.AddDays(_days);
    }

    private static DateTime _AddSecondsSafe(DateTime time, long seconds)
    {
        if (time == DateTime.MaxValue)
            return time;
        double _remain = (DateTime.MaxValue - time).TotalSeconds;
        return seconds >= _remain ? DateTime.MaxValue : time.AddSeconds(seconds);
    }

    #endregion
}
