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
/// UseProfile - Tiền tệ: Coin.
/// </summary>
public partial class UseProfile
{

    public static Action evtCoinChange;
    public static int Coin
    {
        get
        {

            return PlayerPrefs.GetInt(StringHelper.COIN, GiftDatabase.DefaultItem[GiftType.Coin]);
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.COIN, value);
            evtCoinChange?.Invoke();
            GameUtils.RaiseMessage(HomeMessages.GoldMessage.Instance);
            GPlayerPrefs.Save();
        }
    }
}
