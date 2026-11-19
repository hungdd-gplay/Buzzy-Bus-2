using System;
using UnityEngine;
using Buzzy.Core;

namespace Buzzy.Game
{
    /// <summary>Lưu tiến trình bằng GPlayerPrefs của base (bọc PlayerPrefs).</summary>
    public sealed class BuzzyPrefsStore : ISaveStore
    {
        public int GetInt(string key, int def) { return GPlayerPrefs.GetInt(key, def); }
        public void SetInt(string key, int value) { GPlayerPrefs.SetInt(key, value); }
        public void Save() { GPlayerPrefs.Save(); }
    }

    /// <summary>
    /// Luồng màn chơi (M5): nạp tiến trình → level đã lưu → thắng (popup) → cộng xu, level kế; thua → chơi lại / revive.
    /// Không có màn Home (design-decisions mục 8): loading vào thẳng level.
    /// </summary>
    public class BuzzyBootstrap : MonoBehaviour
    {
        public BuzzyLevelDatabase database;
        public BuzzyLayoutConfig config;
        public BuzzyAssets assets;
        [Tooltip("0 = level đã lưu; >0 = ép level (test)")] public int level = 0;
        [Range(0.25f, 4f)] public float timeScale = 1f;
        [Tooltip("M7: bảng thử nghiệm (<, Lv, >, R) dưới badge, chỉ Editor / bản development")] public bool devPanel = true;

        Camera _cam; BuzzyGameView _view; MetaState _meta; ISaveStore _store; BuzzyAudio _audio; int _maxLevel;
        public BuzzyGameView View { get { return _view; } }
        public MetaState Meta { get { return _meta; } }

        void Start()
        {
            _store = new BuzzyPrefsStore();
            var mc = config.ToMetaConfig(); mc.MaxLevel = Mathf.Min(mc.MaxLevel, Mathf.Max(1, database.PlayableCount));   // M6: hết level trong phạm vi thì đứng ở level cuối
            _meta = new MetaState(mc); _meta.Load(_store); _maxLevel = mc.MaxLevel;
            if (level > 0) _meta.Level = Mathf.Clamp(level, 1, mc.MaxLevel);
            var camGo = new GameObject("Camera"); camGo.tag = "MainCamera";
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = config.backgroundTop;
            config.ApplyCamera(_cam, (float)Screen.width / Screen.height);
            camGo.AddComponent<AudioListener>(); _audio = camGo.AddComponent<BuzzyAudio>();   // M7: âm thanh sống cùng camera
            StartLevel();
        }

        void StartLevel()
        {
            if (_view != null) Destroy(_view.gameObject);
            var lv = database.GetPlayable(_meta.Level) ?? database.levels[0];   // M6: bỏ qua level ngoài phạm vi
            var go = new GameObject("BuzzyGame");
            _view = go.AddComponent<BuzzyGameView>();
            _view.Meta = _meta; _view.TimeScale = timeScale; _view.DisplayLevel = _meta.Level;
            _view.Audio = _audio; _view.DevMaxLevel = _maxLevel;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // T1: bảng thử nghiệm: đổi level không lưu (thắng level đó mới lưu như thường)
            _view.DevPanel = devPanel; _view.DevLevelRequested = n => { _meta.Level = Mathf.Clamp(n, 1, _maxLevel); StartLevel(); }; _view.DevRestartRequested = StartLevel;
#endif
            _view.Vibrate = () =>
            {
#if UNITY_ANDROID || UNITY_IOS
                if (_meta.Haptic) Handheld.Vibrate();
#endif
            };
            _view.ShowRewardAd = ShowRewardAd;
            _view.LevelCompleted += OnLevelCompleted;
            _view.RetryRequested += StartLevel;
            _view.MetaChanged += () => _meta.Save(_store);
            _view.Init(lv, config, assets, _cam, new Vector2Int(Screen.width, Screen.height));
            if (assets.music != null && assets.music.Length > 0) _audio.PlayMusic(assets.music[0], config.musicVolume);   // A2: nhạc nền lặp, không phát lại khi sang level
            else if (assets.sfxTap == null) Debug.LogWarning("[Buzzy] BuzzyAssets chưa có âm thanh: chạy Tools/Buzzy Bus/Refresh Assets");
        }

        void OnLevelCompleted(int coins)
        {
            _meta.CompleteLevel(coins); _meta.Save(_store);
            StartLevel();
        }

        /// <summary>Quảng cáo thưởng qua AdsManager của base; trong Editor không có SDK → thưởng ngay.</summary>
        bool ShowRewardAd(Action reward)
        {
#if UNITY_EDITOR
            reward(); return true;
#else
            try { AdsManager.ShowVideoReward(() => reward(), () => { }, () => { }); return true; }
            catch (Exception e) { Debug.LogWarning("AdsManager: " + e.Message); return false; }
#endif
        }
    }
}
