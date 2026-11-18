using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cổng duy nhất để gọi quảng cáo. Bên dưới dùng "provider" (hiện tại: AdmobAds = AdMob + AppLovin MAX).
/// Đổi nhà cung cấp quảng cáo chỉ cần thay provider, code gọi giữ nguyên.
/// Mọi hàm static an toàn khi tính năng Ads bị tắt:
///  - Reward: nếu isRewardWhenAdsOff = true thì trả thưởng ngay (tiện test / game không quảng cáo), ngược lại báo "không có video".
///  - Interstitial: gọi luôn callback đóng.
///  - Banner / MREC: bỏ qua.
/// </summary>
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    [Tooltip("Provider quảng cáo (GameObject con).")]
    public AdmobAds provider;
    [Tooltip("Khi tắt Ads: bấm xem video thưởng vẫn nhận thưởng ngay.")]
    public bool isRewardWhenAdsOff = true;

    private bool _isInited;

    public static AdmobAds Provider => Instance != null ? Instance.provider : null;
    public static bool IsReady => Instance != null && Instance._isInited && Instance.provider != null;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    public void Init()
    {
        Instance = this;
        if (provider == null)
            provider = GetComponentInChildren<AdmobAds>(true);
        if (provider == null)
        {
            Debug.LogWarning("[AdsManager] Chưa gán provider quảng cáo.");
            return;
        }
        provider.Init();
        _isInited = true;
    }

    // ================== Reward ==================

    public static void ShowVideoReward(UnityAction actionReward, UnityAction actionNotLoadedVideo, UnityAction actionClose,
        ActionWatchVideo actionType = ActionWatchVideo.None, string level = null, string adWhere = "")
    {
        if (!IsReady)
        {
            bool _isGiveReward = Instance == null || Instance.isRewardWhenAdsOff;
            if (_isGiveReward)
                actionReward?.Invoke();
            else
                actionNotLoadedVideo?.Invoke();
            actionClose?.Invoke();
            return;
        }
        Provider.ShowVideoReward(actionReward, actionNotLoadedVideo, actionClose, actionType, level, adWhere);
    }

    public static bool IsLoadedVideoReward()
    {
        return IsReady && Provider.IsLoadedVideoReward();
    }

    // ================== Interstitial ==================

    public static bool ShowInterstitial(bool isShowImmediatly = false, string actionWatchLog = "other",
        UnityAction actionIniterClose = null, string level = null)
    {
        if (!IsReady)
        {
            actionIniterClose?.Invoke();
            return false;
        }
        return Provider.ShowInterstitial(isShowImmediatly, actionWatchLog, actionIniterClose, level);
    }

    // ================== Banner ==================

    public static void ShowBanner()
    {
        if (IsReady) Provider.ShowBanner();
    }

    public static void DestroyBanner()
    {
        if (IsReady) Provider.DestroyBanner();
    }

    public static void ShowBannerCollap()
    {
        if (IsReady) Provider.ShowBannerCollap();
    }

    public static void HideBannerCollap()
    {
        if (IsReady) Provider.HideBannerCollap();
    }

    // ================== MREC ==================

    public static void ShowMRec()
    {
        if (IsReady) Provider.ShowMRec();
    }

    public static void HideMRec()
    {
        if (IsReady) Provider.HideMRec();
    }

    public static bool IsCanShowMrec()
    {
        return IsReady && Provider.IsCanShowMrec();
    }
}
