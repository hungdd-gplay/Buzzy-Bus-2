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
/// Hồ sơ SỰ KIỆN ngoài lề core game (mini game, challenge mode, event theo mùa...).
/// Đặt trên GameObject con "Event" của UserProfile.
///  - Mỗi sự kiện mới: thêm 1 #region riêng, key PlayerPrefs bắt đầu bằng PREFIX_EVENT_KEY.
///  - Reset theo ngày: thêm vào ResetDayEvents().
///  - Dữ liệu dạng đơn giản có thể dùng GetEventInt/SetEventInt... không cần khai báo property.
/// </summary>
public class UserEventProfile : MonoBehaviour
{
    public const string PREFIX_EVENT_KEY = "EVT_";

    /// <summary>Reset dữ liệu sự kiện khi sang ngày mới (GameController.ResetDay gọi).</summary>
    public static void ResetDayEvents()
    {
        ResetDayMiniGame();
    }

    #region Generic (lưu nhanh theo key sự kiện)
    public static int GetEventInt(string eventKey, string field, int defaultValue = 0)
    {
        return PlayerPrefs.GetInt(PREFIX_EVENT_KEY + eventKey + "_" + field, defaultValue);
    }

    public static void SetEventInt(string eventKey, string field, int value)
    {
        GPlayerPrefs.SetInt(PREFIX_EVENT_KEY + eventKey + "_" + field, value);
        GPlayerPrefs.Save();
    }

    public static string GetEventString(string eventKey, string field, string defaultValue = "")
    {
        return PlayerPrefs.GetString(PREFIX_EVENT_KEY + eventKey + "_" + field, defaultValue);
    }

    public static void SetEventString(string eventKey, string field, string value)
    {
        PlayerPrefs.SetString(PREFIX_EVENT_KEY + eventKey + "_" + field, value);
        PlayerPrefs.Save();
    }

    public static DateTime GetEventTime(string eventKey, string field, DateTime defaultValue)
    {
        string _raw = GetEventString(eventKey, field, string.Empty);
        if (long.TryParse(_raw, out long _binary))
            return DateTime.FromBinary(_binary);
        return defaultValue;
    }

    public static void SetEventTime(string eventKey, string field, DateTime value)
    {
        SetEventString(eventKey, field, value.ToBinary().ToString());
    }
    #endregion

    #region Challenge mode

    public static bool IsChallengeModeEnable
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.IS_CHALLENGE_MODE_ENABLE, 0) == 1;
        }
        set
        {
            GPlayerPrefs.SetInt(StringHelper.IS_CHALLENGE_MODE_ENABLE, value ? 1 : 0);
            GPlayerPrefs.Save();
        }
    }
    #endregion

    #region MiniGame (Connect Bird - dữ liệu cũ, có thể xóa khi không dùng)

    public static DateTime LastTimeOpenMinigame
    {
        get
        {
            if (PlayerPrefs.HasKey(StringHelper.LAST_TIME_MINIGAME_SHOW_HOME))
            {
                var temp = Convert.ToInt64(PlayerPrefs.GetString(StringHelper.LAST_TIME_MINIGAME_SHOW_HOME));
                return DateTime.FromBinary(temp);
            }
            else
            {
                var newDateTime = UnbiasedTime.Instance.Now().AddMinutes(-35);
                PlayerPrefs.SetString(StringHelper.LAST_TIME_MINIGAME_SHOW_HOME, newDateTime.ToBinary().ToString());
                PlayerPrefs.Save();
                return newDateTime;
            }
        }
        set
        {
            PlayerPrefs.SetString(StringHelper.LAST_TIME_MINIGAME_SHOW_HOME, value.ToBinary().ToString());
            PlayerPrefs.Save();
        }
    
    }

    public static int ConnectBirdTurnBuy
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.CONNECT_BIRD_TURN_BUY, 0);
        }
        set
        {
            Debug.Log("Set turnBuy " + value);
            PlayerPrefs.SetInt(StringHelper.CONNECT_BIRD_TURN_BUY, value);
            PlayerPrefs.Save();
            // GameController.Instance.playerData.SaveData();
        }
    }
    public static int Version
    {

        get { return PlayerPrefs.GetInt(StringHelper.VERSION_MINI_GAME_MINIGAME, 0); }
        set { PlayerPrefs.SetInt(StringHelper.VERSION_MINI_GAME_MINIGAME, value); }
    }
    public static int PointConnectBirdMiniGame
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.POINT_MINIGAME_CONNECT_BIRD, 0);
        }
        set
        {
            PlayerPrefs.SetInt(StringHelper.POINT_MINIGAME_CONNECT_BIRD, value);
            // GameController.Instance.playerData.SaveData();
        }
    }
    public static string lsRewardConnectBirdMNGOfPlayer
    {
        get
        {
            return PlayerPrefs.GetString(StringHelper.LIST_REWARD_MINI_GAME_CONNECT_BIRD, "[]");
        }
        set
        {
            PlayerPrefs.SetString(StringHelper.LIST_REWARD_MINI_GAME_CONNECT_BIRD, value);
            // GameController.Instance.playerData.SaveData();
        }
    }
    public static int WasCommpleteConnectBirdMiniGame
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.WAS_COMMPLETE_MINI_GAME_CONNECT_BIRD, 0);
        }
        set
        {
            PlayerPrefs.SetInt(StringHelper.WAS_COMMPLETE_MINI_GAME_CONNECT_BIRD, value);
            PlayerPrefs.Save();
        }
    }
    public static int ConnectBirdTurnFree
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.CONNECT_BIRD_TURN_FREE, GameController.Instance.segmentController.GetCurrentSegment().minigameConnectSegmentData.config.num_turn_free);
        }
        set
        {
            PlayerPrefs.SetInt(StringHelper.CONNECT_BIRD_TURN_FREE, value);
            // GameController.Instance.playerData.SaveData();
        }
    }
    public static int LimitNumbWatchVideoInDay
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.LIMIT_NUMB_WATCH_VIDEO_IN_DAY, GameController.Instance.segmentController.GetCurrentSegment().minigameConnectSegmentData.config.num_watch_video_buy_egg);
        }
        set
        {
            PlayerPrefs.SetInt(StringHelper.LIMIT_NUMB_WATCH_VIDEO_IN_DAY, value);
        }
    }
    public static int FirstOpenConnectBirdMNGame
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.FIRST_OPEN_CONNECT_BIRD_MINI_GAME, 0);
        }
        set
        {

            PlayerPrefs.SetInt(StringHelper.FIRST_OPEN_CONNECT_BIRD_MINI_GAME, value);
            PlayerPrefs.Save();
            // GameController.Instance.playerData.SaveData();
        }
    }
    public static int FirstShowHand
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.FIRST_SHOW_HAND, 0);
        }
        set
        {
            PlayerPrefs.SetInt(StringHelper.FIRST_SHOW_HAND, value);
            PlayerPrefs.Save();
        }
    }
    public static int CurrentNumWatchVideoInDay
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.CURRENT_NUM_WATCH_VIDEO_IN_DAY_MINI_GAME_CONNECT_BIRD, 3);
        }
        set
        {
            Debug.Log("Curent num watch video " + value);
            PlayerPrefs.SetInt(StringHelper.CURRENT_NUM_WATCH_VIDEO_IN_DAY_MINI_GAME_CONNECT_BIRD, value);
        }
    }
    public static DateTime DataToResetMiniGame
    {
        get
        {
            if (!PlayerPrefs.HasKey(StringHelper.DATA_TO_RESET_MINI_GAME_CONNECT_BIRD))
            {

                var temp = JsonConvert.SerializeObject(UnbiasedTime.Instance.Now());
                PlayerPrefs.SetString(StringHelper.DATA_TO_RESET_MINI_GAME_CONNECT_BIRD, temp);
                PlayerPrefs.Save();
                return UnbiasedTime.Instance.Now();
            }
            else
            {
                var oldata = JsonConvert.DeserializeObject<DateTime>(PlayerPrefs.GetString(StringHelper.DATA_TO_RESET_MINI_GAME_CONNECT_BIRD));
                return oldata;
            }
        }
        set
        {

            var temp = JsonConvert.SerializeObject(value);
            PlayerPrefs.SetString(StringHelper.DATA_TO_RESET_MINI_GAME_CONNECT_BIRD, temp);
        }
    }
    public static int FirstWeek
    {
        get
        {
            return PlayerPrefs.GetInt(StringHelper.FIRST_WEEK, 0);
        }
        set
        {
            PlayerPrefs.SetInt(StringHelper.FIRST_WEEK, value);
        }
    }
    public static DateTime DataToResetShopMiniGame
    {
        get
        {
            if (!PlayerPrefs.HasKey(StringHelper.DATA_TO_RESET_MINI_GAME_SHOP_CONNECT_BIRD))
            {

                var temp = JsonConvert.SerializeObject(UnbiasedTime.Instance.Now());
                PlayerPrefs.SetString(StringHelper.DATA_TO_RESET_MINI_GAME_SHOP_CONNECT_BIRD, temp);
                PlayerPrefs.Save();
                return UnbiasedTime.Instance.Now();
            }
            else
            {
                var oldata = JsonConvert.DeserializeObject<DateTime>(PlayerPrefs.GetString(StringHelper.DATA_TO_RESET_MINI_GAME_SHOP_CONNECT_BIRD));
                return oldata;
            }
        }
        set
        {

            Debug.Log("Current num " + value );
            var temp = JsonConvert.SerializeObject(value);
            PlayerPrefs.SetString(StringHelper.DATA_TO_RESET_MINI_GAME_SHOP_CONNECT_BIRD, temp);
        }
    }
    public static void ResetDayMiniGame()
    {
        ConnectBirdTurnFree = GameController.Instance.segmentController.GetCurrentSegment().minigameConnectSegmentData
            .config.num_turn_free;
        CurrentNumWatchVideoInDay = LimitNumbWatchVideoInDay = GameController.Instance.segmentController.GetCurrentSegment()
            .minigameConnectSegmentData
            .config.num_watch_video_buy_egg;
        var nowTime = UnbiasedTime.Instance.Now().AddDays(1);
        var newDayStartTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, 0, 0, 0);
        DataToResetShopMiniGame = newDayStartTime;
    }
    #endregion
}
