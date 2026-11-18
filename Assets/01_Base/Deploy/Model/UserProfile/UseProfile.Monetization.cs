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
/// UseProfile - Doanh thu & quảng cáo: remove ads, revenue D0/D1, số lần xem ads, tracking/GDPR, trạng thái gói mua.
/// </summary>
public partial class UseProfile
{

    public bool IsRemoveAds
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.REMOVE_ADS, 0) == 1;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.REMOVE_ADS, value ? 1 : 0);
            GPlayerPrefs.Save();
        }
    }
    public static bool LoggedRevenueCents
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.LOGGED_REVENUE_CENTS, 0) == 1;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.LOGGED_REVENUE_CENTS, value ? 1 : 0);
            GPlayerPrefs.Save();
        }
    }
    public static int PreviousRevenueD0_D1
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.PREVIOUS_REVENUE_D0_D1, 0);
        }
        set
        {
            if (value < 0)
                value = 0;

            GPlayerPrefs.SetInt(StringHelper.PREVIOUS_REVENUE_D0_D1, value);
            GPlayerPrefs.Save();
        }
    }

    public static float RevenueD0_D1
    {
        get
        {
            return PlayerPrefs.GetFloat(StringHelper.REVENUE_D0_D1, 0);
        }
        set
        {
            if (value < 0f)
                value = 0f;

            GPlayerPrefs.SetFloat(StringHelper.REVENUE_D0_D1, value);
            GPlayerPrefs.Save();
        }
    }
    public static int NumberOfDisplayedInterstitialD0_D1
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.NUMBER_OF_DISPLAYED_INTERSTITIAL_D0_D1, 0);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.NUMBER_OF_DISPLAYED_INTERSTITIAL_D0_D1, value);
            GPlayerPrefs.Save();
        }
    }


    public static int PreviousRevenueD1
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.PREVIOUS_REVENUE_D1, 0);
        }
        set
        {
            if (value < 0)
                value = 0;

            GPlayerPrefs.SetInt(StringHelper.PREVIOUS_REVENUE_D1, value);
            GPlayerPrefs.Save();
        }
    }

    public static float RevenueD1
    {
        get
        {
            return PlayerPrefs.GetFloat(StringHelper.REVENUE_D1, 0);
        }
        set
        {
            if (value < 0f)
                value = 0f;

            GPlayerPrefs.SetFloat(StringHelper.REVENUE_D1, value);
            GPlayerPrefs.Save();
        }
    }
    public static int NumberOfDisplayedInterstitialD1
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.NUMBER_OF_DISPLAYED_INTERSTITIAL_D1, 0);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.NUMBER_OF_DISPLAYED_INTERSTITIAL_D1, value);
            GPlayerPrefs.Save();
        }
    }

    public static int PayingType
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.PAYING_TYPE, 0);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.PAYING_TYPE, value);
            GPlayerPrefs.Save();
        }
    }

    public static int NumberOfAdsInPlay;
    public static int NumberOfAdsInDay
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.NUMBER_OF_ADS_IN_DAY, 0);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.NUMBER_OF_ADS_IN_DAY, value);
            GPlayerPrefs.Save();
        }
    }

    public static int NumberInterShowed
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.NUMBER_INTER_SHOWED, 0);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.NUMBER_INTER_SHOWED, value);
            GPlayerPrefs.Save();
        }
    }

    public static int NumberRewardShowed
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.NUMBER_REWARD_SHOWED, 0);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.NUMBER_REWARD_SHOWED, value);
            GPlayerPrefs.Save();
        }
    }

    public static bool IsTrackedPremission
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.IS_TRACKED_PREMISSION, 0) == 1;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.IS_TRACKED_PREMISSION, value ? 1 : 0);
            GPlayerPrefs.Save();
        }
    }

    public static bool IsAcceptTracker
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.IS_ACCEPT_TRACKED_PREMISSION, 0) == 1;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.IS_ACCEPT_TRACKED_PREMISSION, value ? 1 : 0);
            GPlayerPrefs.Save();
        }
    }


    public static bool GDPRContentValue;
    public static bool StarterPackIsCompleted
    {
        get { return GPlayerPrefs.GetInt(StringHelper.STARTER_PACK_IS_COMPLETED, 0) == 1; }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.STARTER_PACK_IS_COMPLETED, value ? 1 : 0);
            GPlayerPrefs.Save();
        }
    }
}
