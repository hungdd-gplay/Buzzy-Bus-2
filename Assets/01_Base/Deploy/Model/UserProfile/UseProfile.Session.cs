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
/// UseProfile - Phiên chơi: cài đặt lần đầu, retention, số ngày chơi, thời điểm online, đánh giá app.
/// </summary>
public partial class UseProfile
{

    public static bool IsFirstTimeInstall
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.FIRST_TIME_INSTALL, 1) == 1;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.FIRST_TIME_INSTALL, value ? 1 : 0);
            GPlayerPrefs.Save();
        }
    }

    public static bool IsReInstallSyncData
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.IS_REINSTALL_SYNC_DATA, 1) == 1;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.IS_REINSTALL_SYNC_DATA, value ? 1 : 0);
            GPlayerPrefs.Save();
        }
    }

    public static int RetentionD
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.RETENTION_D, 0);
        }
        set
        {
            if (value < 0)
                value = 0;

            GPlayerPrefs.SetInt(StringHelper.RETENTION_D, value);
            GPlayerPrefs.Save();
        }
    }

    public static int DaysPlayed
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.DAYS_PLAYED, 1);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.DAYS_PLAYED, value);
            GPlayerPrefs.Save();
        }
    }
    public static DateTime FirstTimeOpenGame
    {
        get
        {
            if (GPlayerPrefs.HasKey(StringHelper.FIRST_TIME_OPEN_GAME))
            {
                var temp = Convert.ToInt64(PlayerPrefs.GetString(StringHelper.FIRST_TIME_OPEN_GAME));
                return DateTime.FromBinary(temp);
            }
            else
            {
                var newDateTime = UnbiasedTime.Instance.Now().AddDays(-1);
                GPlayerPrefs.SetString(StringHelper.FIRST_TIME_OPEN_GAME, newDateTime.ToBinary().ToString());
                GPlayerPrefs.Save();
                return newDateTime;
            }
        }
        set
        {
            GPlayerPrefs.SetString(StringHelper.FIRST_TIME_OPEN_GAME, value.ToBinary().ToString());
            GPlayerPrefs.Save();
        }
    }

    public static DateTime LastTimeOpenGame
    {
        get
        {
            if (GPlayerPrefs.HasKey(StringHelper.LAST_TIME_OPEN_GAME))
            {
                var temp = Convert.ToInt64(PlayerPrefs.GetString(StringHelper.LAST_TIME_OPEN_GAME));
                return DateTime.FromBinary(temp);
            }
            else
            {
                var newDateTime = UnbiasedTime.Instance.Now().AddDays(-1);
                GPlayerPrefs.SetString(StringHelper.LAST_TIME_OPEN_GAME, newDateTime.ToBinary().ToString());
                GPlayerPrefs.Save();
                return newDateTime;
            }
        }
        set
        {
            GPlayerPrefs.SetString(StringHelper.LAST_TIME_OPEN_GAME, value.ToBinary().ToString());
            GPlayerPrefs.Save();
        }
    }


    public static bool CanShowRate
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.CAN_SHOW_RATE, 1) == 1;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.CAN_SHOW_RATE, value ? 1 : 0);
            GPlayerPrefs.Save();
        }
    }

    public static int LevelShowedRate
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.LEVEL_SHOWED_RATE, 0);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.LEVEL_SHOWED_RATE, value);
            GPlayerPrefs.Save();
        }
    }

    public static DateTime LastTimeOnline
    {
        get
        {
            if (GPlayerPrefs.HasKey(StringHelper.LAST_TIME_ONLINE))
            {
                var temp = Convert.ToInt64(PlayerPrefs.GetString(StringHelper.LAST_TIME_ONLINE));
                return DateTime.FromBinary(temp);
            }
            else
            {
                var newDateTime = UnbiasedTime.Instance.Now().AddDays(-1);
                GPlayerPrefs.SetString(StringHelper.LAST_TIME_ONLINE, newDateTime.ToBinary().ToString());
                GPlayerPrefs.Save();
                return newDateTime;
            }
        }
        set
        {
            GPlayerPrefs.SetString(StringHelper.LAST_TIME_ONLINE, value.ToBinary().ToString());
            GPlayerPrefs.Save();
        }
    }

    public static int NumPlayedInDay
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.NUM_PLAYED_IN_DAY, 0);
        }
        set
        {

            PlayerPrefs.SetInt(StringHelper.NUM_PLAYED_IN_DAY, value);
            PlayerPrefs.Save();
        }
    }
}
