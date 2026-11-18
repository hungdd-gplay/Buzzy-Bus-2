namespace BaseProject.Core.Feature
{
    /// <summary>
    /// Cờ bật/tắt tính năng đọc từ Scripting Define Symbols (đặt trong Tools > Base Project > Feature Settings).
    /// Dùng ở code thường: if (FeatureFlags.IsAds) {...}
    /// Dùng khi code tham chiếu SDK có thể bị gỡ: #if FEATURE_ADS ... #endif
    /// (static readonly thay vì const để tránh cảnh báo "unreachable code")
    /// </summary>
    public static class FeatureFlags
    {
#if FEATURE_SOUND
        public static readonly bool IsSound = true;
#else
        public static readonly bool IsSound = false;
#endif

#if FEATURE_IAP
        public static readonly bool IsIap = true;
#else
        public static readonly bool IsIap = false;
#endif

#if FEATURE_SALE_PACK
        public static readonly bool IsSalePack = true;
#else
        public static readonly bool IsSalePack = false;
#endif

#if FEATURE_VIDEO_PACK
        public static readonly bool IsVideoPack = true;
#else
        public static readonly bool IsVideoPack = false;
#endif

#if FEATURE_CHALLENGE
        public static readonly bool IsChallenge = true;
#else
        public static readonly bool IsChallenge = false;
#endif

#if FEATURE_EVENT_SALE
        public static readonly bool IsEventSale = true;
#else
        public static readonly bool IsEventSale = false;
#endif

#if FEATURE_LOGIN_ONLINE
        public static readonly bool IsLoginOnline = true;
#else
        public static readonly bool IsLoginOnline = false;
#endif

#if FEATURE_EFFECT
        public static readonly bool IsEffect = true;
#else
        public static readonly bool IsEffect = false;
#endif

#if FEATURE_SKILL_USER
        public static readonly bool IsSkillUser = true;
#else
        public static readonly bool IsSkillUser = false;
#endif

#if FEATURE_THEME
        public static readonly bool IsTheme = true;
#else
        public static readonly bool IsTheme = false;
#endif

#if FEATURE_CONSENT
        public static readonly bool IsConsent = true;
#else
        public static readonly bool IsConsent = false;
#endif

#if FEATURE_ADS
        public static readonly bool IsAds = true;
#else
        public static readonly bool IsAds = false;
#endif

#if FEATURE_NOTIFICATION
        public static readonly bool IsNotification = true;
#else
        public static readonly bool IsNotification = false;
#endif

#if FEATURE_ANALYTICS_FALCON
        public static readonly bool IsAnalyticsFalcon = true;
#else
        public static readonly bool IsAnalyticsFalcon = false;
#endif

        public static bool IsEnabled(FeatureId id)
        {
            switch (id)
            {
                case FeatureId.Sound: return IsSound;
                case FeatureId.Iap: return IsIap;
                case FeatureId.SalePack: return IsSalePack;
                case FeatureId.VideoPack: return IsVideoPack;
                case FeatureId.Challenge: return IsChallenge;
                case FeatureId.EventSale: return IsEventSale;
                case FeatureId.LoginOnline: return IsLoginOnline;
                case FeatureId.Effect: return IsEffect;
                case FeatureId.SkillUser: return IsSkillUser;
                case FeatureId.Theme: return IsTheme;
                case FeatureId.Consent: return IsConsent;
                case FeatureId.Ads: return IsAds;
                case FeatureId.Notification: return IsNotification;
                case FeatureId.AnalyticsFalcon: return IsAnalyticsFalcon;
                default: return false;
            }
        }
    }
}
