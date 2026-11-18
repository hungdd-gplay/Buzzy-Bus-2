using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Globalization;
using BestHTTP.Extensions;
//using MoreMountains.NiceVibrations;
using Newtonsoft.Json;
//using Facebook.Unity;
using PlayFab;
using PlayFab.ClientModels;
using EventDispatcher;
using static GameEvent;

/// <summary>
/// Hồ sơ người chơi: lưu các biến tổng quát và biến phát sinh trong quá trình chơi (PlayerPrefs).
/// Đã tách nhỏ theo nhóm (partial class) trong thư mục Controllers/UserProfile:
///   UseProfile.cs              : Level / tiến trình chơi (file gốc, giữ GUID component)
///   UseProfile.Setting.cs      : nhạc, âm thanh, rung, thông báo
///   UseProfile.Session.cs      : cài đặt, retention, ngày chơi
///   UseProfile.Monetization.cs : remove ads, revenue, ads count, tracking
///   UseProfile.Online.cs       : đăng nhập, đồng bộ
///   UseProfile.Currency.cs     : coin
///   UseProfile.Item.cs         : vật phẩm
///   UseProfile.Health.cs       : mạng
/// Dữ liệu sự kiện ngoài lề (mini game, challenge, event...) nằm ở UserEventProfile (GameObject con "Event").
/// </summary>
public partial class UseProfile : MonoBehaviour
{
    [Tooltip("Dữ liệu sự kiện ngoài lề core game (GameObject con).")]
    public UserEventProfile userEventProfile;

    public static int CurrentLevel
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.CURRENT_LEVEL, 1);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.CURRENT_LEVEL, value);
            GPlayerPrefs.Save();
        }
    }

    public int CurrentHardLevel
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.CURRENT_HARD_LEVEL, 1);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.CURRENT_HARD_LEVEL, value);
            GPlayerPrefs.Save();
        }
    }

    public int CurrentLevelPlay
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.CURRENT_LEVEL_PLAY, 1);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.CURRENT_LEVEL_PLAY, value);
            GPlayerPrefs.Save();
        }
    }

    public static int IQScore
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.IQ_SCORE, 0);
        }
        set
        {

            GPlayerPrefs.SetInt(StringHelper.IQ_SCORE, value);
            GPlayerPrefs.Save();
        }
    }

    public static int NumberLoseLevelInDay
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.NUM_LOSE_LEVEL_TO_DAY, 0);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.NUM_LOSE_LEVEL_TO_DAY, value);
            GPlayerPrefs.Save();
        }
    }

    public static bool IsClickTutPlayHome
    {
        get => PlayerPrefs.GetInt("IsClickTutPlayHome", 0) == 1;
        set
        {
            PlayerPrefs.SetInt("IsClickTutPlayHome", value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
    public static bool hadBuySuggest { get; set; }
    
    public string OwnedBranchSkin
    {
        get { return GPlayerPrefs.GetString(StringHelper.OWNED_BRANCH_SKIN, ""); }
        set
        {
            GPlayerPrefs.SetString(StringHelper.OWNED_BRANCH_SKIN, value);
            GPlayerPrefs.Save();
        }
    }
    public string OwnedThemeSkin
    {
        get { return GPlayerPrefs.GetString(StringHelper.OWNED_THEME, ""); }
        set
        {
            GPlayerPrefs.SetString(StringHelper.OWNED_THEME, value);
            GPlayerPrefs.Save();
        } 
    }


    public static int LevelPlayInDay
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.NUM_PLAYED_LEVEL_IN_DAY, 0);
        }
        set
        {
            PlayerPrefs.SetInt(StringHelper.NUM_PLAYED_LEVEL_IN_DAY, value);
            PlayerPrefs.Save();
        }
    }

    public static int RememberHardCurrentPlayLevelMax
    {
        get { return GPlayerPrefs.GetInt(StringHelper.REMEMBER_HARD_CURRENT_PLAY_LEVEL_MAX, -1); }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.REMEMBER_HARD_CURRENT_PLAY_LEVEL_MAX, value);
            GPlayerPrefs.Save();
        }
    }

    public static int RememberLevelCurrentLevelMax
    {
        get { return GPlayerPrefs.GetInt(StringHelper.REMEMBER_LEVEL_CURRENT_LEVEL_MAX, -1); }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.REMEMBER_LEVEL_CURRENT_LEVEL_MAX, value);
            GPlayerPrefs.Save();
        }
    }

    public static int RememberHardCurrentLevelMax
    {
        get { return GPlayerPrefs.GetInt(StringHelper.REMEMBER_HARD_CURRENT_LEVEL_MAX, -1); }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.REMEMBER_HARD_CURRENT_LEVEL_MAX, value);
            GPlayerPrefs.Save();
        }
    }

    public static int RememberLevelCurrentPlayLevelMax
    {
        get { return GPlayerPrefs.GetInt(StringHelper.REMEMBER_LEVEL_CURRENT_PLAY_LEVEL_MAX, 1501); }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.REMEMBER_LEVEL_CURRENT_PLAY_LEVEL_MAX, value);
            GPlayerPrefs.Save();
        }
    }
}
