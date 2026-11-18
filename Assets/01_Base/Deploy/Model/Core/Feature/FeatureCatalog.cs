using System.Collections.Generic;

namespace BaseProject.Core.Feature
{
    /// <summary>
    /// Danh sách tính năng có thể bật/tắt. Giá trị enum = THỨ TỰ KHỞI TẠO trong GameController (nhỏ chạy trước).
    /// Thêm tính năng mới:
    ///   1. Thêm 1 dòng vào FeatureId (giá trị theo đúng vị trí cần init).
    ///   2. Thêm FeatureInfo vào FeatureCatalog.ALL_FEATURES (define, tên, mô tả, phụ thuộc).
    ///   3. Thêm cờ vào FeatureFlags + case trong FeatureFlags.IsEnabled.
    ///   4. Thêm bước init trong GameController._InitFeatures() bọc bằng #if DEFINE.
    /// </summary>
    public enum FeatureId
    {
        Sound = 10,
        Iap = 20,
        SalePack = 30,
        VideoPack = 40,
        Challenge = 50,
        EventSale = 60,
        LoginOnline = 70,
        Effect = 80,
        SkillUser = 90,
        Theme = 100,
        Consent = 110,
        Ads = 120,
        Notification = 130,
        AnalyticsFalcon = 140,
    }

    public class FeatureInfo
    {
        public FeatureId id;
        public string defineSymbol;
        public string displayName;
        public string description;
        public FeatureId[] dependencies;

        public FeatureInfo(FeatureId id, string defineSymbol, string displayName, string description, params FeatureId[] dependencies)
        {
            this.id = id;
            this.defineSymbol = defineSymbol;
            this.displayName = displayName;
            this.description = description;
            this.dependencies = dependencies ?? new FeatureId[0];
        }
    }

    public static class FeatureCatalog
    {
        /// <summary>Tiền tố chung của mọi define tính năng (để tool chỉ đụng tới define của mình).</summary>
        public const string PREFIX_DEFINE = "FEATURE_";

        public static readonly List<FeatureInfo> ALL_FEATURES = new List<FeatureInfo>
        {
            new FeatureInfo(FeatureId.Sound, "FEATURE_SOUND", "Âm thanh", "SoundManager: Music (nhạc nền), Sound (song song), SoundUI (click...)"),
            new FeatureInfo(FeatureId.Iap, "FEATURE_IAP", "In-App Purchase", "IAPManager + provider IapController: mua hàng trong app"),
            new FeatureInfo(FeatureId.SalePack, "FEATURE_SALE_PACK", "Gói bán hàng", "PackController: Starter, Weekly, BlackFriday, Noel...", FeatureId.Iap),
            new FeatureInfo(FeatureId.VideoPack, "FEATURE_VIDEO_PACK", "Gói xem video", "SaleVideoController: nhận quà bằng video", FeatureId.Ads),
            new FeatureInfo(FeatureId.Challenge, "FEATURE_CHALLENGE", "Challenge / LiveOps", "ChallengeController: Coin, Diamond, SpaceRace, RoyalPass, Weekly"),
            new FeatureInfo(FeatureId.EventSale, "FEATURE_EVENT_SALE", "Event Sale", "EventSaleController: Endless / Halloween / Noel"),
            new FeatureInfo(FeatureId.LoginOnline, "FEATURE_LOGIN_ONLINE", "Đăng nhập online", "LoginController: Facebook / PlayFab"),
            new FeatureInfo(FeatureId.Effect, "FEATURE_EFFECT", "Hiệu ứng FX", "FXManager: FXController (pool hiệu ứng) + MoneyEffectController"),
            new FeatureInfo(FeatureId.SkillUser, "FEATURE_SKILL_USER", "Độ khó động", "SkillUserController: điều chỉnh độ khó theo người chơi"),
            new FeatureInfo(FeatureId.Theme, "FEATURE_THEME", "Theme / Concept", "ThemeManager: đổi concept, theme sự kiện, AssetBundle"),
            new FeatureInfo(FeatureId.Consent, "FEATURE_CONSENT", "Consent (UMP / ATT)", "Form đồng ý GDPR + App Tracking iOS trước khi load quảng cáo"),
            new FeatureInfo(FeatureId.Ads, "FEATURE_ADS", "Quảng cáo", "AdsManager + provider AdmobAds/MAX: banner, inter, reward, app open, MREC"),
            new FeatureInfo(FeatureId.Notification, "FEATURE_NOTIFICATION", "Local Notification", "NotificationController"),
            new FeatureInfo(FeatureId.AnalyticsFalcon, "FEATURE_ANALYTICS_FALCON", "Falcon Analytics", "FalconMain: log sự kiện Falcon"),
        };

        /// <summary>Define cũ đã đổi tên -> tự chuyển sang define mới khi mở project.</summary>
        public static readonly Dictionary<string, FeatureId> LEGACY_DEFINES = new Dictionary<string, FeatureId>
        {
            { "FEATURE_MUSIC", FeatureId.Sound },
        };

        public static FeatureInfo Get(FeatureId id)
        {
            foreach (var _info in ALL_FEATURES)
            {
                if (_info.id == id)
                    return _info;
            }
            return null;
        }
    }
}
