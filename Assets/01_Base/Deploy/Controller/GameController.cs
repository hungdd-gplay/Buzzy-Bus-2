using System;
using System.Collections.Generic;
using BaseProject.Core.Feature;
using UnityEngine;
using Falcon.FalconCore.Scripts;
using GPlay.GplayGoogleUMP;
using Falcon.FalconAnalytics.Scripts.Models.Messages.PreDefines;
#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif
using Gplay.Notifications;

/// <summary>
/// Điểm khởi tạo chung của game (sống xuyên scene). Cấu trúc GameObject con (tạo/sắp xếp bằng
/// Tools > Base Project > Setup GameController):
///   1. UserProfile      (UseProfile)          └─ Event (UserEventProfile)
///   2. DataContain      (DataContain)         └─ EventData (EventDataContain)
///   3. SegmentController(SegmentController)   : thông số game theo segment / remote config
///   4. SoundManager     (SoundManager)        └─ Music / Sound / SoundUI
///   5. AnalyticController(AnalyticsController)
///   6. AdsManager       (AdsManager)          └─ AdmobAds (provider)
///   7. IAPManager       (IAPManager)          └─ IapController (provider)
///   8. BoxController    (BoxController)       : quản lý popup
///   9. FXManager        (FXManager)           └─ FXController / MoneyEffectController
///   Features            (các tính năng tái sử dụng: Pack, VideoPack, Challenge, EventSale, Login, SkillUser, Notification...)
///   Loading             (LoadingPanel, StartLoading...)
/// QUY TẮC: core (1-3, 5, 8) luôn chạy; tính năng init trong _InitFeatures() theo thứ tự FeatureId,
/// bọc #if FEATURE_xxx và chạy qua _InitStep(). Bật/tắt: Tools > Base Project > Feature Settings.
/// </summary>
public class GameController : MonoBehaviour
{
    public static GameController Instance;

    [Header("1. User Profile")]
    public UseProfile useProfile;

    [Header("2. Data")]
    public DataContain dataContain;

    [Header("3. Segment (thông số game)")]
    public SegmentController segmentController;

    [Header("4. Sound")]
    public SoundManager soundManager;

    [Header("5. Analytic")]
    public AnalyticsController AnalyticsController;

    [Header("6. Ads")]
    public AdsManager adsManager;

    [Header("7. IAP")]
    public IAPManager iapManager;

    [Header("8. Popup")]
    public BoxController boxController;

    [Header("9. FX")]
    public FXManager fxManager;

    [Header("Tính năng khác (bật/tắt bằng Feature Settings)")]
    public PackController packController;
    public SaleVideoController videoPackController;
    public ChallengeController challengeController;
    public EventSaleController eventSaleController;
    public LoginController loginController;
    public SkillUserController skillUserController;
    public NotificationController notificationController;
    public EventPackController eventPackController;

    [Header("Khác")]
    public PoolController poolController;
    [SerializeField] private StartLoading startLoading;
    [SerializeField] private LoadingPanel loadingAction;

    [HideInInspector] public PlayerData playerData;
    [HideInInspector] public SceneType currentScene;
    public ModeGame modeGame;
    [HideInInspector] public bool openFeatureBox = false;
    public static bool isFirstLoadLevel = false;

    private readonly HashSet<FeatureId> _setFeatureInited = new HashSet<FeatureId>();

    // ===== Truy cập nhanh tới provider (giữ tương thích code cũ) =====
    public UserEventProfile userEventProfile => useProfile != null ? useProfile.userEventProfile : null;
    public AdmobAds admobAds => adsManager != null ? adsManager.provider : null;
    public IapController iapController => iapManager != null ? iapManager.provider : null;
    public FXController fXController => fxManager != null ? fxManager.fxController : null;
    public MoneyEffectController moneyEffectController => fxManager != null ? fxManager.moneyEffectController : null;

    /// <summary>Tính năng đã bật VÀ init thành công.</summary>
    public bool IsFeatureReady(FeatureId id)
    {
        return _setFeatureInited.Contains(id);
    }

    /// <summary>Bản static, an toàn khi GameController chưa tồn tại. Dùng trước khi gọi sang tính năng khác.</summary>
    public static bool IsFeatureOn(FeatureId id)
    {
        return Instance != null && Instance.IsFeatureReady(id);
    }

    protected void Start()
    {
        Instance = this;
        DontDestroyOnLoad(this);
        Init();
    }

    public void Init()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // ===== CORE 1-3: dữ liệu người chơi, data tĩnh, thông số segment (luôn chạy, trước mọi tính năng) =====
        playerData = new PlayerData();
        useProfile.CurrentLevelPlay = UseProfile.CurrentLevel;
        segmentController.Init();

        // ===== TÍNH NĂNG: theo thứ tự FeatureId =====
        _InitFeatures();

        // ===== CORE: reset dữ liệu theo ngày (sau khi các tính năng đã init) =====
        ResetDay();
        UseProfile.NumPlayedInDay++;

        SoundManager.PlayMusicHome();

        // Consent (UMP/ATT) -> Segment (cập nhật remote config) -> Ads -> Loading scene
        _InitConsentThenLoadScene();

#if FEATURE_NOTIFICATION
        _InitStep(FeatureId.Notification, () => notificationController.Initialize());
#endif
#if FEATURE_ANALYTICS_FALCON
        _InitStep(FeatureId.AnalyticsFalcon, () => FalconMain.Init());
#endif
    }

    /// <summary>Init các tính năng độc lập, đúng thứ tự FeatureId. Tắt tính năng = bỏ define tương ứng.</summary>
    private void _InitFeatures()
    {
#if FEATURE_SOUND
        _InitStep(FeatureId.Sound, () => soundManager.Init());
#endif
#if FEATURE_IAP
        _InitStep(FeatureId.Iap, () => iapManager.Init());
#endif
#if FEATURE_SALE_PACK
        _InitStep(FeatureId.SalePack, () => packController.Init());
#endif
#if FEATURE_VIDEO_PACK
        _InitStep(FeatureId.VideoPack, () => videoPackController.Init());
#endif
#if FEATURE_CHALLENGE
        _InitStep(FeatureId.Challenge, () => challengeController.Init());
#endif
#if FEATURE_EVENT_SALE
        _InitStep(FeatureId.EventSale, () => eventSaleController.Init());
#endif
#if FEATURE_LOGIN_ONLINE
        _InitStep(FeatureId.LoginOnline, () => loginController.Init());
#endif
#if FEATURE_EFFECT
        _InitStep(FeatureId.Effect, () => fxManager.Init());
#endif
#if FEATURE_SKILL_USER
        _InitStep(FeatureId.SkillUser, () => skillUserController.Init());
#endif
#if FEATURE_THEME
        _InitStep(FeatureId.Theme, () => BaseProject.Theme.ThemeManager.CreateAndInit());
#endif
    }

    /// <summary>Chạy 1 bước init, bắt lỗi để không ảnh hưởng các tính năng sau.</summary>
    private void _InitStep(FeatureId id, Action actionInit)
    {
        try
        {
            actionInit?.Invoke();
            _setFeatureInited.Add(id);
        }
        catch (Exception _exception)
        {
            Debug.LogError($"[GameController] Init tính năng {id} lỗi, bỏ qua: {_exception}");
        }
    }

    private void _InitConsentThenLoadScene()
    {
#if FEATURE_CONSENT
        try
        {
            GplayUMP.ShowConsentForm((consent) =>
            {
                UseProfile.GDPRContentValue = consent;
#if UNITY_IOS
                if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
                {
                    ATTrackingStatusBinding.RequestAuthorizationTracking();
                }
#endif
                _setFeatureInited.Add(FeatureId.Consent);
                InitLoadScene();
            });
            return;
        }
        catch (Exception _exception)
        {
            Debug.LogError("[GameController] Consent lỗi, tiếp tục load: " + _exception);
        }
#endif
        InitLoadScene();
    }

    public void InitLoadScene()
    {
        // Cập nhật lại thông số segment sau khi remote config đã có thể fetch xong
        segmentController.Init();
#if FEATURE_ADS
        _InitStep(FeatureId.Ads, () => adsManager.Init());
#endif
        if (startLoading != null)
        {
            startLoading.Init();
        }
    }

    public void LoadScene(string sceneName, bool showMrec = true)
    {
        // Game không có Home: lệnh về Home sẽ chuyển thành vào GamePlay (GameFlowConfig)
        sceneName = GameFlow.ResolveScene(sceneName);
        bool _isMrecLoading = IsFeatureReady(FeatureId.Ads)
            && segmentController.GetCurrentSegment().adsNewData.config.mrec.CheckActive(AdsNewSegmentData.BannerMrec.MrecType.Loading);
        if (_isMrecLoading)
            loadingAction.GotoScene(sceneName, showMrec);
        else
            Initiate.Fade(sceneName.ToString(), Color.black, 2f);
#if FEATURE_ANALYTICS_FALCON
        try
        {
            new FEventLog(FalconLog.OPEN_SCENE_ + sceneName).Send();
            Debug.Log(FalconLog.OPEN_SCENE_ + sceneName);
        }
        catch
        {
        }
#endif
    }

    public static void SetUserProperties()
    {
        if (UseProfile.IsFirstTimeInstall)
        {
            UseProfile.FirstTimeOpenGame = UnbiasedTime.Instance.Now();
            UseProfile.LastTimeOpenGame = UseProfile.FirstTimeOpenGame;
            UseProfile.IsFirstTimeInstall = false;
        }

        UseProfile.RetentionD = (UnbiasedTime.Instance.Now() - UseProfile.FirstTimeOpenGame).Days;

        var dayPlayerd = (TimeManager.ParseTimeStartDay(UnbiasedTime.Instance.Now()) - TimeManager.ParseTimeStartDay(UseProfile.LastTimeOpenGame)).Days;
        if (dayPlayerd >= 1)
        {
            UseProfile.LastTimeOpenGame = UnbiasedTime.Instance.Now();
            UseProfile.DaysPlayed++;
        }

        AnalyticsController.SetUserProperties();
    }

    public void ResetDay()
    {
        long time = TimeManager.CaculateTime(TimeManager.ParseTimeStartDay(UseProfile.LastTimeOnline), TimeManager.ParseTimeStartDay(UnbiasedTime.Instance.Now()));
        if (time < 86400)
            return;

        // ===== CORE =====
        UseProfile.LastTimeOnline = UnbiasedTime.Instance.Now();
        ShopItemVideoCoin.NumTurnVideoCoinInDay = 0;
        UseProfile.NumberLoseLevelInDay = 0;
        UseProfile.SetTurnSuggetBuyItemVideo(GiftType.SuggetItem_BirdSort, 0);
        UseProfile.SetTurnSuggetBuyItemVideo(GiftType.ReturnItem_BirdSort, 0);
        UseProfile.SetTurnSuggetBuyItemVideo(GiftType.ShuffleItem_BirdSort, 0);
        UseProfile.SetTurnSuggetBuyItemVideo(GiftType.IceItem_BirdSort, 0);
        UseProfile.SetTurnSuggetBuyItemVideo(GiftType.BreakTheRule_BirdSort, 0);
        UseProfile.SetTurnSuggetBuyItemVideo(GiftType.AddTimeItem_BirdSort, 0);
        UseProfile.SetTurnSuggetBuyItemVideo(GiftType.BlockBrand_BirdSort, 0);
        UseProfile.SetTurnSuggetBuyItemVideo(GiftType.AddBrandItem_BirdSort, 0);
        ReviveBox.CurrentTurnSuggetReviveVideo = 0;
        UseProfile.NumPlayedInDay = 0;
        UserEventProfile.ResetDayEvents();

        // ===== TÍNH NĂNG =====
#if FEATURE_SALE_PACK
        if (IsFeatureReady(FeatureId.SalePack))
            packController.ResetDataNewDay();
#endif
#if FEATURE_VIDEO_PACK
        if (IsFeatureReady(FeatureId.VideoPack))
            videoPackController.ResetDataNewDay();
#endif
#if FEATURE_CHALLENGE
        if (IsFeatureReady(FeatureId.Challenge))
            challengeController.ResetDay();
#endif
    }

    public bool CheckHomeIsHasReward()
    {
#if FEATURE_CHALLENGE
        if (IsFeatureReady(FeatureId.Challenge) && challengeController.IsHasChallengeReward())
            return true;
#endif
#if FEATURE_EVENT_SALE
        if (IsFeatureReady(FeatureId.EventSale))
        {
            if (eventSaleController.IsHasEventSaleReward())
                return true;
            foreach (var item in eventSaleController.lstEventSales)
            {
                if (UseProfile.CurrentLevel == item.LevelBackToHome())
                    return true;
            }
        }
#endif
        if (LevelChestController.IsHasChest())
            return true;
        if (StartChestController.IsHasChest())
            return true;
        return false;
    }

    public bool CheckEndLevelChallenge()
    {
        return false;
    }

    private void OnApplicationQuit()
    {
    }
}

public enum SceneType
{
    StartLoading = 0,
    MainHome = 1,
    GamePlay = 2
}
