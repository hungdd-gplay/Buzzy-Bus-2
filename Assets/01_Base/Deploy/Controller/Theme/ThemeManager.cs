using System;
using System.Collections;
using UnityEngine;

namespace BaseProject.Theme
{
    /// <summary>
    /// Controller: quản lý theme đang dùng.
    /// - Được GameController khởi tạo theo thứ tự (define FEATURE_THEME), sống xuyên scene.
    /// - Theme mặc định = concept của game. Theme sự kiện lấy từ RemoteConfig (id sự kiện + tên AssetBundle),
    ///   ưu tiên bản có sẵn trong build (packsEventLocal), không có thì tải AssetBundle.
    /// - Ảnh nào theme sự kiện không có -> tự lấy từ theme mặc định.
    /// View (ThemeSlotImage, ThemeGameplayBackground) lắng nghe OnThemeChanged để đổi ảnh.
    /// </summary>
    public class ThemeManager : MonoBehaviour
    {
        public static ThemeManager Instance { get; private set; }

        /// <summary>Gọi khi theme đổi (View tự cập nhật ảnh).</summary>
        public static event Action OnThemeChanged;

        public ThemeSystemConfig Config { get; private set; }
        public ThemePack PackCurrent { get; private set; }
        public ThemeEventId EventIdCurrent { get; private set; } = ThemeEventId.Default;
        public bool IsReady { get; private set; }

        private AssetBundle _bundleCurrent;
        private Coroutine _coroutineLoad;

        public bool IsEnabled => Config != null && Config.isEnableTheme;

        /// <summary>Tạo + khởi tạo ThemeManager (gọi từ GameController._InitFeatures). Trả về null nếu thiếu config hoặc theme bị tắt.</summary>
        public static ThemeManager CreateAndInit()
        {
            if (Instance != null)
                return Instance;
            var _config = Resources.Load<ThemeSystemConfig>(ThemeSystemConfig.PATH_RESOURCES);
            if (_config == null)
            {
                Debug.LogWarning("[Theme] Thiếu Resources/" + ThemeSystemConfig.PATH_RESOURCES + " (Tools > Base Project > Theme Manager > Tạo cấu trúc).");
                return null;
            }
            if (!_config.isEnableTheme)
                return null;
            var _go = new GameObject("[ThemeManager]");
            DontDestroyOnLoad(_go);
            var _manager = _go.AddComponent<ThemeManager>();
            _manager._Init(_config);
            return _manager;
        }

        private void _Init(ThemeSystemConfig config)
        {
            Instance = this;
            Config = config;
            PackCurrent = config.packDefault;
            EventIdCurrent = ThemeEventId.Default;
            IsReady = true;
            // Các View đã bật trước khi ThemeManager khởi tạo -> áp theme mặc định ngay
            OnThemeChanged?.Invoke();

            if (!config.isEnableEventTheme)
                return;
#if UNITY_EDITOR
            if (config.isUseEditorTestEvent)
            {
                SetEventTheme(config.eventIdEditorTest);
                return;
            }
#endif
            StartCoroutine(_WaitRemoteConfigAndApply());
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            _UnloadBundle();
        }

        #region API

        /// <summary>Lấy ảnh theo slot (theme hiện tại, thiếu thì lấy theme mặc định). null nếu nhóm bị tắt hoặc không có ảnh.</summary>
        public Sprite GetSprite(ThemeSlotId slot, string subKey = null)
        {
            if (!IsEnabled || !Config.IsCategoryEnabled(ThemeKeyUtility.GetCategory(slot)))
                return null;
            string _key = ThemeKeyUtility.GetKey(slot, subKey);
            Sprite _sprite = PackCurrent != null ? PackCurrent.GetSprite(_key) : null;
            if (_sprite == null && Config.packDefault != null && PackCurrent != Config.packDefault)
                _sprite = Config.packDefault.GetSprite(_key);
            return _sprite;
        }

        /// <summary>Lấy BG gameplay theo level (theme hiện tại có danh sách BG thì dùng, không thì lấy mặc định).</summary>
        public Sprite GetGameplayBackground(int level)
        {
            if (!IsEnabled || !Config.isEnableGameplay)
                return null;
            if (PackCurrent != null && PackCurrent.HasGameplayBackground)
                return PackCurrent.GetGameplayBackground(level);
            return Config.packDefault != null ? Config.packDefault.GetGameplayBackground(level) : null;
        }

        /// <summary>
        /// Đổi sang theme sự kiện. Có bản trong build thì dùng ngay, không có thì tải AssetBundle (bundleName rỗng -> lấy từ RemoteConfig).
        /// </summary>
        public void SetEventTheme(ThemeEventId eventId, string bundleName = null, uint bundleVersion = 0, Action<bool> onDone = null)
        {
            if (!IsEnabled)
            {
                onDone?.Invoke(false);
                return;
            }
            if (eventId == ThemeEventId.Default)
            {
                ResetToDefault();
                onDone?.Invoke(true);
                return;
            }

            var _packLocal = Config.GetPackEventLocal(eventId);
            if (_packLocal != null)
            {
                _ApplyPack(_packLocal, eventId, null);
                onDone?.Invoke(true);
                return;
            }

            if (!Config.isEnableAssetBundle)
            {
                Debug.LogWarning($"[Theme] Không có theme '{eventId}' trong build và AssetBundle đang tắt.");
                onDone?.Invoke(false);
                return;
            }

            if (string.IsNullOrEmpty(bundleName))
                bundleName = RemoteConfigController.GetStringConfig(Config.keyRemoteBundleName, string.Empty);
            if (string.IsNullOrEmpty(bundleName))
            {
                Debug.LogWarning($"[Theme] Chưa có tên AssetBundle cho sự kiện '{eventId}' (RemoteConfig: {Config.keyRemoteBundleName}).");
                onDone?.Invoke(false);
                return;
            }
            if (bundleVersion == 0)
                bundleVersion = (uint)Mathf.Max(0, RemoteConfigController.GetIntConfig(Config.keyRemoteBundleVersion, 0));

            if (_coroutineLoad != null)
                StopCoroutine(_coroutineLoad);
            _coroutineLoad = StartCoroutine(_LoadBundleAndApply(eventId, bundleName, bundleVersion, onDone));
        }

        /// <summary>Quay về theme mặc định (concept của game).</summary>
        public void ResetToDefault()
        {
            _ApplyPack(Config != null ? Config.packDefault : null, ThemeEventId.Default, null);
        }

        #endregion

        private IEnumerator _WaitRemoteConfigAndApply()
        {
            float _timeWait = 0;
            while (!RemoteConfigController.isFetchDone && _timeWait < Config.timeoutWaitRemoteConfig)
            {
                _timeWait += Time.unscaledDeltaTime;
                yield return null;
            }
            int _eventIdRemote = RemoteConfigController.GetIntConfig(Config.keyRemoteEventId, 0);
            if (_eventIdRemote == 0)
                yield break;
            if (!Enum.IsDefined(typeof(ThemeEventId), _eventIdRemote))
            {
                Debug.LogWarning($"[Theme] RemoteConfig {Config.keyRemoteEventId} = {_eventIdRemote} không có trong ThemeEventId.");
                yield break;
            }
            SetEventTheme((ThemeEventId)_eventIdRemote);
        }

        private IEnumerator _LoadBundleAndApply(ThemeEventId eventId, string bundleName, uint bundleVersion, Action<bool> onDone)
        {
            string _url = ThemeAssetBundleLoader.BuildUrl(Config.urlAssetBundleFormat, bundleName);
            ThemePack _packLoaded = null;
            AssetBundle _bundleLoaded = null;
            string _error = null;
            yield return ThemeAssetBundleLoader.LoadThemePack(_url, bundleVersion, Config.timeoutDownloadSeconds,
                (pack, bundle, error) => { _packLoaded = pack; _bundleLoaded = bundle; _error = error; });

            _coroutineLoad = null;
            if (_packLoaded == null)
            {
                Debug.LogWarning("[Theme] " + _error + " -> giữ theme hiện tại.");
                onDone?.Invoke(false);
                yield break;
            }
            _ApplyPack(_packLoaded, eventId, _bundleLoaded);
            onDone?.Invoke(true);
        }

        private void _ApplyPack(ThemePack pack, ThemeEventId eventId, AssetBundle bundle)
        {
            var _bundleOld = _bundleCurrent;
            PackCurrent = pack != null ? pack : Config.packDefault;
            EventIdCurrent = pack != null ? eventId : ThemeEventId.Default;
            _bundleCurrent = bundle;
            PackCurrent?.ClearCache();

            Debug.Log($"[Theme] Đang dùng theme: {EventIdCurrent}{(bundle != null ? " (AssetBundle)" : string.Empty)}");
            OnThemeChanged?.Invoke();

            // Ảnh đã được View đổi sang theme mới -> giải phóng bundle cũ
            if (_bundleOld != null && _bundleOld != bundle)
                _bundleOld.Unload(true);
        }

        private void _UnloadBundle()
        {
            if (_bundleCurrent != null)
            {
                _bundleCurrent.Unload(true);
                _bundleCurrent = null;
            }
        }
    }
}
