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
/// UseProfile - Mạng (Health): số mạng, hồi mạng, vô hạn theo thời gian.
/// </summary>
public partial class UseProfile
{

    public static int Health
    {
        get
        {
            //if (IsUnlimitedHealth)
            //    return GiftDatabase.DefaultItem[GiftType.Health];

            //var timeRefill = GameController.Instance.segmentController.GetCurrentSegment().coreGameData.config.healthConfig.timeReFillHealth;
            //int currentHealth = PlayerPrefs.GetInt(StringHelper.HEALTH, GiftDatabase.DefaultItem[GiftType.Health]);
            //if (currentHealth <= 0
            //    && TimeManager.CaculateTime(TimeLastOverHealth.AddSeconds(timeRefill), UnbiasedTime.Instance.Now()) >= timeRefill)
            //{
            //    GPlayerPrefs.SetInt(StringHelper.HEALTH, GiftDatabase.DefaultItem[GiftType.Health]);
            //    UseProfile.IsRefillHealth = false;
            //    return GiftDatabase.DefaultItem[GiftType.Health];
            //}

            //return currentHealth;

            return 100;
        }
        set
        {
            if (!IsUnlimitedHealth)
            {
                GPlayerPrefs.SetInt(StringHelper.HEALTH, value);
                evtCoinChange?.Invoke();
                GPlayerPrefs.Save();
            }
        }
    }

    public static DateTime TimeLastOverHealth
    {
        get
        {
            if (GPlayerPrefs.HasKey(StringHelper.TIME_LAST_OVER_HEALTH))
            {
                var temp = Convert.ToInt64(PlayerPrefs.GetString(StringHelper.TIME_LAST_OVER_HEALTH));
                return DateTime.FromBinary(temp);
            }
            else
            {
                var newDateTime = UnbiasedTime.Instance.Now().AddDays(-1);
                GPlayerPrefs.SetString(StringHelper.TIME_LAST_OVER_HEALTH, newDateTime.ToBinary().ToString());
                GPlayerPrefs.Save();
                return newDateTime;
            }
        }
        set
        {
            GPlayerPrefs.SetString(StringHelper.TIME_LAST_OVER_HEALTH, value.ToBinary().ToString());
            GPlayerPrefs.Save();
        }
    }

    public static bool IsRefillHealth
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.IS_REFILL_HEALTH, 0) == 1;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.IS_REFILL_HEALTH, value ? 1 : 0);
            GPlayerPrefs.Save();
        }
    }

    public static DateTime TimeEndUnlimitedHealth
    {
        get
        {
            if (GPlayerPrefs.HasKey(StringHelper.TIME_END_UNLIMITED_HEALTH))
            {
                var temp = Convert.ToInt64(PlayerPrefs.GetString(StringHelper.TIME_END_UNLIMITED_HEALTH));
                return DateTime.FromBinary(temp);
            }
            else
            {
                var newDateTime = UnbiasedTime.Instance.Now().AddDays(-1);
                GPlayerPrefs.SetString(StringHelper.TIME_END_UNLIMITED_HEALTH, newDateTime.ToBinary().ToString());
                GPlayerPrefs.Save();
                return newDateTime;
            }
        }
        set
        {
            GPlayerPrefs.SetString(StringHelper.TIME_END_UNLIMITED_HEALTH, value.ToBinary().ToString());
            GPlayerPrefs.Save();
        }
    }

    public static void AddTimeEndUnlimitedHealth(int seconds)
    {
        if (TimeEndUnlimitedHealth < UnbiasedTime.Instance.Now())
        {
            TimeEndUnlimitedHealth = UnbiasedTime.Instance.Now();
        }

        TimeEndUnlimitedHealth = TimeEndUnlimitedHealth.AddSeconds(seconds);
    }

    public static bool IsUnlimitedHealth
    {
        get
        {
            return UseProfile.TimeEndUnlimitedHealth > UnbiasedTime.Instance.Now();
        }
    }
}
