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
/// UseProfile - Tài khoản online: đăng nhập FB/GG/PlayFab, tên, avatar, đồng bộ dữ liệu.
/// </summary>
public partial class UseProfile
{

    public static bool IsLoggedInByFBorGG
    {
        get
        {
            return IsLoggedIn;
        }
    }

    public static DateTime LastTimeSyncName
    {
        get => PlayerPrefs.GetString(StringHelper.LastTimeSyncName, DateTime.MinValue.ToString(CultureInfo.InvariantCulture)).ToDateTime();
        set
        {
            GPlayerPrefs.SetString(StringHelper.LastTimeSyncName, value.ToString(CultureInfo.InvariantCulture));
            GPlayerPrefs.Save();
        }
    }

    public static DateTime LastTimePlayFab
    {
        get => DateTime.Parse(PlayerPrefs.GetString(StringHelper.LAST_TIME_PLAYFAB, new DateTime().ToString()));
        set
        {
            GPlayerPrefs.SetString(StringHelper.LAST_TIME_PLAYFAB, value.ToString());
            GPlayerPrefs.Save();
        }
    }

    public static string PieceData
    {
        get
        {
            return PlayerPrefs.GetString(StringHelper.PIECE_DATA, "");
        }
        set
        {
            GPlayerPrefs.SetString(StringHelper.PIECE_DATA, value);
            GPlayerPrefs.Save();
        }
    }
    public static int CoinSync
    {
        get
        {

            return PlayerPrefs.GetInt(StringHelper.COIN, GiftDatabase.DefaultItem[GiftType.Coin]);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.COIN, value);
            GPlayerPrefs.Save();
        }
    }
    public static long VersionData
    {
        get
        {
            //Debug.Log("========== VersionData gggggggg ");
            long value = -1;
            System.Int64.TryParse(PlayerPrefs.GetString(StringHelper.VERSION_DATA, "0"), out value);
            return value;
        }
        set
        {
           // Debug.Log("========== VersionData sssss " + value);
            PlayerPrefs.SetString(StringHelper.VERSION_DATA, value.ToString());
            PlayerPrefs.Save();
        }
    }

    public static string NamePlayer
    {
        get
        {
            return PlayerPrefs.GetString("FB_NAME_v2", "");
        }
        set
        {
            GPlayerPrefs.SetString("FB_NAME_v2", value);
            GPlayerPrefs.Save();
        }
    }
    public static string AvatarLink
    {
        get
        {
            return PlayerPrefs.GetString(StringHelper.AVATAR_LINK, "");
        }
        set
        {
            GPlayerPrefs.SetString(StringHelper.AVATAR_LINK, value);
            GPlayerPrefs.Save();
        }
    }
    public static string FlagLink
    {
        get
        {
            return PlayerPrefs.GetString(StringHelper.FLAG_LINK, "");
        }
        set
        {
            GPlayerPrefs.SetString(StringHelper.FLAG_LINK, value);
            GPlayerPrefs.Save();
        }
    }
    public static int AvatarIDLocal
    {
        get
        {
            return PlayerPrefs.GetInt("Avatar_ID");
        }
        set
        {

            GPlayerPrefs.SetInt("Avatar_ID", value);
            GPlayerPrefs.Save();

        }
    }

    public static int ScorePvP
    {
        get
        {
            return PlayerPrefs.GetInt("ScorePvP");
        }
        set
        {

            GPlayerPrefs.SetInt("ScorePvP", value);
            GPlayerPrefs.Save();
        }
    }

    public static string JsonDataPlay
    {
        get
        {
            return PlayerPrefs.GetString(StringHelper.JSON_DATA_PLAY, "");
        }
        set
        {
            GPlayerPrefs.SetString(StringHelper.JSON_DATA_PLAY, value);
            GPlayerPrefs.Save();
        }
    }
    public static string PlayFabId
    {
        get
        {
            return PlayerPrefs.GetString("PlayFabId", string.Empty);
        }
        set
        {

            GPlayerPrefs.SetString("PlayFabId", value);
            GPlayerPrefs.Save();
        }
    }
    public static bool IsLoggedIn
    {
        get
        {

            return PlayerPrefs.GetInt(StringHelper.IS_LOGGED_IN, 0) == 1;

        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.IS_LOGGED_IN, value ? 1 : 0);
            GPlayerPrefs.Save();

        }
    }

    public static string AuthCodeGG
    {
        get
        {
            return PlayerPrefs.GetString(StringHelper.AUTH_CODE_GG, "");
        }
        set
        {
            GPlayerPrefs.SetString(StringHelper.AUTH_CODE_GG, value);
            GPlayerPrefs.Save();
        }
    }

    public static long LastTimeSync
    {
        get
        {
            return long.TryParse(PlayerPrefs.GetString(StringHelper.LAST_TIME_SYNC), out long rt) ? rt : 0;
        }
    }
    public static string LinkedId
    {
        get
        {
            return PlayerPrefs.GetString(StringHelper.LINKED_ID, "");
        }
        set
        {
            Debug.Log("ERR: " + value);
            GPlayerPrefs.SetString(StringHelper.LINKED_ID, value);
            GPlayerPrefs.Save();
        }
    }
}
