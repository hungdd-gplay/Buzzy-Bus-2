using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using BestHTTP.Extensions;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.ClientModels;
using EventDispatcher;
using static GameEvent;

/// <summary>
/// UseProfile - Vật phẩm: số lượng, dùng/không dùng, vô hạn theo thời gian, gợi ý mua bằng video.
/// </summary>
public partial class UseProfile
{

    public static int GetNumItem(GiftType typeItem, int defaultNum = 3)
    {
        return PlayerPrefs.GetInt(StringHelper.NUM_ITEM + typeItem.ToString(),
            RemoteConfigController.GetIntConfig(String.Format(FirebaseConfig.DEFAULT_NUM_ITEM, typeItem.ToString()),
         GiftDatabase.GetDefaultAmount(typeItem, defaultNum)));
    }

    public static void SetItem(GiftType typeItem, int amount)
    {
        GPlayerPrefs.SetInt(StringHelper.NUM_ITEM + typeItem.ToString(), amount);
    }

    public static void AddItem(GiftType typeItem, int amount)
    {
        if (amount < 0)
        {
            if (IsUnlimitedItem(typeItem))
                return;
        }
        int current = GetNumItem(typeItem);
        current += amount;

        GPlayerPrefs.SetInt(StringHelper.NUM_ITEM + typeItem.ToString(), current);
    }

    public static bool IsUseItem(GiftType typeItem)
    {
        if (!IsUnlimitedItem(typeItem) && GetNumItem(typeItem) <= 0)
        {
            UseItem(typeItem, false);
            return false;
        }

        return PlayerPrefs.GetInt(StringHelper.USE_ITEM + typeItem.ToString(), 0) == 0 ? false : true;
    }

    public static void UseItem(GiftType typeItem, bool isUse)
    {
        GPlayerPrefs.SetInt(StringHelper.USE_ITEM + typeItem.ToString(), isUse ? 1 : 0);
    }

    public static bool IsUnlimitedItem(GiftType typeItem)
    {
        if (typeItem == GiftType.SuggetItem_BirdSort || typeItem == GiftType.ReturnItem_BirdSort || typeItem == GiftType.ShuffleItem_BirdSort || typeItem == GiftType.IceItem_BirdSort || typeItem == GiftType.BreakTheRule_BirdSort)
            return false;

        return GetTimeEndUnlimitedItem(typeItem) > UnbiasedTime.Instance.Now();
    }

    public static System.DateTime GetTimeEndUnlimitedItem(GiftType typeItem)
    {
        if (GPlayerPrefs.HasKey(StringHelper.TIME_END_UNLIMITED_ITEM + typeItem))
        {
            var temp = Convert.ToInt64(PlayerPrefs.GetString(StringHelper.TIME_END_UNLIMITED_ITEM + typeItem));
            return DateTime.FromBinary(temp);
        }
        else
        {
            var newDateTime = UnbiasedTime.Instance.Now().AddDays(-1);
            GPlayerPrefs.SetString(StringHelper.TIME_END_UNLIMITED_ITEM + typeItem, newDateTime.ToBinary().ToString());
            GPlayerPrefs.Save();
            return newDateTime;
        }
    }

    public static void AddTimeEndUnlimitedItem(GiftType typeItem, int seccons)
    {
        var time = GetTimeEndUnlimitedItem(typeItem);
        if (time < UnbiasedTime.Instance.Now())
            time = UnbiasedTime.Instance.Now();
        time = time.AddSeconds(seccons);
        GPlayerPrefs.SetString(StringHelper.TIME_END_UNLIMITED_ITEM + typeItem, time.ToBinary().ToString());
        GPlayerPrefs.Save();
    }

    public static void SetTimeEndUnlimitedItem(GiftType typeItem, long timeEnd)
    {
        GPlayerPrefs.SetString(StringHelper.TIME_END_UNLIMITED_ITEM + typeItem, timeEnd.ToString());
        GPlayerPrefs.Save();
    }

    public static int GetCurrentTurnSuggetBuyItemVideo(GiftType typeItem)
    {
        return PlayerPrefs.GetInt(StringHelper.CURRENT_TURN_SUGGET_VIDEO_BUY_ITEM + typeItem, 0);
    }

    public static void AddTurnSuggetBuyItemVideo(GiftType typeItem, int value)
    {
        int current = GetCurrentTurnSuggetBuyItemVideo(typeItem);
        GPlayerPrefs.SetInt(StringHelper.CURRENT_TURN_SUGGET_VIDEO_BUY_ITEM + typeItem, current + value);
        GPlayerPrefs.Save();
    }

    public static void SetTurnSuggetBuyItemVideo(GiftType typeItem, int value)
    {
        GPlayerPrefs.SetInt(StringHelper.CURRENT_TURN_SUGGET_VIDEO_BUY_ITEM + typeItem, value);
        GPlayerPrefs.Save();
    }

    public static DateTime GetLastTimeOverSuggetVideoBuyItem(GiftType typeItem)
    {

        if (GPlayerPrefs.HasKey(StringHelper.LAST_TIME_OVER_SUGGET_VIDEO_BUY_ITEM + typeItem))
        {
            var temp = Convert.ToInt64(GPlayerPrefs.GetString(StringHelper.LAST_TIME_OVER_SUGGET_VIDEO_BUY_ITEM + typeItem));
            return DateTime.FromBinary(temp);
        }
        else
        {
            var newDateTime = UnbiasedTime.Instance.Now().AddDays(-1);
            GPlayerPrefs.SetString(StringHelper.LAST_TIME_OVER_SUGGET_VIDEO_BUY_ITEM + typeItem, newDateTime.ToBinary().ToString());
            GPlayerPrefs.Save();
            return newDateTime;
        }
    }

    public static void SetLastTimeOverSuggetVideoBuyItem(GiftType typeItem, DateTime time)
    {
        GPlayerPrefs.SetString(StringHelper.LAST_TIME_OVER_SUGGET_VIDEO_BUY_ITEM + typeItem, time.ToBinary().ToString());
        GPlayerPrefs.Save();
    }
}
