using System;
using UnityEngine;

/// <summary>
/// Nơi DUY NHẤT chứa ID của các SDK (AdMob, AppLovin MAX, Facebook, AppsFlyer).
/// Sửa qua Tools > Base Project > SDK ID Manager (bấm OK sẽ ghi sang settings của từng SDK).
/// Code runtime đọc qua SdkIdConfig.Instance (asset nằm ở Resources/Config/SdkIdConfig).
/// </summary>
[CreateAssetMenu(fileName = "SdkIdConfig", menuName = "Base Project/SDK Id Config")]
public class SdkIdConfig : ScriptableObject
{
    public const string PATH_RESOURCES = "Config/SdkIdConfig";

    /// <summary>ID theo nền tảng.</summary>
    [Serializable]
    public class PlatformId
    {
        public string android;
        public string ios;

        public PlatformId() { }
        public PlatformId(string android, string ios) { this.android = android; this.ios = ios; }

        public string Get()
        {
#if UNITY_IOS
            return ios;
#else
            return android;
#endif
        }
    }

    /// <summary>1 đơn vị quảng cáo: có toggle dùng / không dùng.</summary>
    [Serializable]
    public class AdUnitId : PlatformId
    {
        public bool isEnable = true;

        public AdUnitId() { }
        public AdUnitId(bool isEnable, string android, string ios) : base(android, ios) { this.isEnable = isEnable; }
    }

    [Serializable]
    public class AdmobConfig
    {
        public bool isEnable = true;
        [Tooltip("Dùng ID test của Google (giống define TEST_BUILD).")]
        public bool isUseTestId = false;
        public PlatformId appId = new PlatformId();
        public AdUnitId banner = new AdUnitId();
        public AdUnitId bannerCollapse = new AdUnitId();
        [Tooltip("Banner kích thước MREC của AdMob (fallback khi MREC MAX lỗi).")]
        public AdUnitId bannerMrec = new AdUnitId();
        public AdUnitId interstitial = new AdUnitId();
        public AdUnitId reward = new AdUnitId(false, "", "");
        public AdUnitId appOpen = new AdUnitId();
    }

    [Serializable]
    public class MaxConfig
    {
        public bool isEnable = true;
        public string sdkKey;
        [Tooltip("AdMob App ID cho adapter Google trong MAX (ghi vào AppLovinSettings).")]
        public PlatformId admobAdapterAppId = new PlatformId();
        public AdUnitId banner = new AdUnitId();
        public AdUnitId mrec = new AdUnitId();
        public AdUnitId interstitial = new AdUnitId();
        public AdUnitId reward = new AdUnitId();
    }

    [Serializable]
    public class FacebookConfig
    {
        public bool isEnable = true;
        public string appName;
        public string appId;
        public string clientToken;
    }

    [Serializable]
    public class AppsFlyerConfig
    {
        public bool isEnable = true;
        public string devKey;
        [Tooltip("iOS App ID (số trên App Store, không có 'id'). Android không cần.")]
        public string appIdIOS;
        public bool isDebug;
        [Tooltip("Nhận dữ liệu conversion (install attribution / deferred deeplink) qua callback onConversionDataSuccess.")]
        public bool isGetConversionData;
    }

    public AdmobConfig admob = new AdmobConfig();
    public MaxConfig max = new MaxConfig();
    public FacebookConfig facebook = new FacebookConfig();
    public AppsFlyerConfig appsFlyer = new AppsFlyerConfig();

    // ===== ID test chính thức của Google =====
    public const string ADMOB_TEST_BANNER = "ca-app-pub-3940256099942544/8388050270";
    public const string ADMOB_TEST_BANNER_COLLAPSE = "ca-app-pub-3940256099942544/9214589741";
    public const string ADMOB_TEST_INTERSTITIAL = "ca-app-pub-3940256099942544/1033173712";
    public const string ADMOB_TEST_REWARD = "ca-app-pub-3940256099942544/5224354917";
    public const string ADMOB_TEST_APP_OPEN = "ca-app-pub-3940256099942544/9257395921";

    private static SdkIdConfig _instance;

    public static SdkIdConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<SdkIdConfig>(PATH_RESOURCES);
                if (_instance == null)
                {
                    Debug.LogError("[SdkIdConfig] Chưa có Resources/" + PATH_RESOURCES + ". Mở Tools > Base Project > SDK ID Manager để tạo.");
                    _instance = CreateInstance<SdkIdConfig>();
                }
            }
            return _instance;
        }
    }

    /// <summary>Đang dùng ID test AdMob (toggle hoặc define TEST_BUILD).</summary>
    public bool IsAdmobTest
    {
        get
        {
#if TEST_BUILD
            return true;
#else
            return admob.isUseTestId;
#endif
        }
    }

    /// <summary>Lấy ID AdMob: trả ID test nếu đang test, rỗng nếu unit bị tắt.</summary>
    public string GetAdmobUnit(AdUnitId unit, string testId)
    {
        if (unit == null || !unit.isEnable)
            return string.Empty;
        return IsAdmobTest ? testId : unit.Get();
    }

    /// <summary>Lấy ID MAX: rỗng nếu unit bị tắt.</summary>
    public string GetMaxUnit(AdUnitId unit)
    {
        if (unit == null || !unit.isEnable)
            return string.Empty;
        return unit.Get();
    }

    public bool IsAdmobUnitOn(AdUnitId unit) => admob.isEnable && unit != null && unit.isEnable;
    public bool IsMaxUnitOn(AdUnitId unit) => max.isEnable && unit != null && unit.isEnable;
}
