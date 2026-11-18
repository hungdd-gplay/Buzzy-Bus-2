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
/// UseProfile - Cài đặt người chơi: nhạc, âm thanh, rung, thông báo.
/// </summary>
public partial class UseProfile
{

    public bool OnVibration
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.ONOFF_VIBRATION, 1) == 1;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.ONOFF_VIBRATION, value ? 1 : 0);
            //MMVibrationManager.SetHapticsActive(value);
            GPlayerPrefs.Save();
        }
    }

    public bool OnSound
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.ONOFF_SOUND, 1) == 1;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.ONOFF_SOUND, value ? 1 : 0);
            SoundManager.ApplySetting();
            GPlayerPrefs.Save();
        }
    }

    public bool OnMusic
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.ONOFF_MUSIC, 1) == 1;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.ONOFF_MUSIC, value ? 1 : 0);
            SoundManager.ApplySetting();
            GPlayerPrefs.Save();
        }
    }

    public static bool OnNotification
    {
        get => PlayerPrefs.GetInt(StringHelper.ONOFF_NOTIFICATION, 1) == 1;
        set
        {
            GPlayerPrefs.SetInt(StringHelper.ONOFF_NOTIFICATION, value ? 1 : 0);
            GPlayerPrefs.Save();
        }
    }
}
