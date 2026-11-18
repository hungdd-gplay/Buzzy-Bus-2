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

        Camera _cam; BuzzyGameView _view; MetaState _meta; ISaveStore _store;
        public BuzzyGameView View { get { return _view; } }
        public MetaState Meta { get { return _meta; } }

        void Start()
        {
            _store = new BuzzyPrefsStore();
            _meta = new MetaState(config.ToMetaConfig()); _meta.Load(_store);
            if (level > 0) _meta.Level = Mathf.Clamp(level, 1, database.levels.Count);
            var camGo = new GameObject("Camera"); camGo.tag = "MainCamera";
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = config.backgroundTop;
            config.ApplyCamera(_cam, (float)Screen.width / Screen.height);
            StartLevel();
        }

        void StartLevel()
        {
            if (_view != null) Destroy(_view.gameObject);
            var lv = database.Get(_meta.Level) ?? database.levels[0];
            var go = new GameObject("BuzzyGame");
            _view = go.AddComponent<BuzzyGameView>();
            _view.Meta = _meta; _view.TimeScale = timeScale;
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
