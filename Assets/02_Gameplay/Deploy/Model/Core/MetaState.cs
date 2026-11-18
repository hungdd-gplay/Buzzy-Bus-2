using System;
using System.Collections.Generic;

namespace Buzzy.Core
{
    /// <summary>Kho lưu (PlayerPrefs ở Unity, bộ nhớ ở test).</summary>
    public interface ISaveStore
    {
        int GetInt(string key, int def);
        void SetInt(string key, int value);
        void Save();
    }

    public sealed class MemorySaveStore : ISaveStore
    {
        public readonly Dictionary<string, int> Data = new Dictionary<string, int>();
        public int GetInt(string key, int def) { int v; return Data.TryGetValue(key, out v) ? v : def; }
        public void SetInt(string key, int value) { Data[key] = value; }
        public void Save() { }
    }

    /// <summary>
    /// Số liệu meta `[thiết kế]` (design-decisions.md mục 7, 8, 14 + video V4: xu 640 → 720, thưởng 40, AD x2–x4). Dữ liệu, không hằng số trong code;
    /// Unity nạp từ BuzzyLayoutConfig.
    /// </summary>
    public sealed class MetaConfig
    {
        /// <summary>Xu ban đầu: video V4 có 640 xu khi xong Lv.2 → 600 + 40 (Lv.1) `[suy luận]`.</summary>
        public int StartCoins = 600;
        /// <summary>Thưởng gốc mỗi level (nút xu "40").</summary>
        public int WinCoins = 40;
        /// <summary>Thanh nhân thưởng quảng cáo chạy qua lại x2-x3-x4-x3-x2 (U2); thưởng AD = WinCoins x ô đang sáng.</summary>
        public int[] AdMultipliers = { 2, 3, 4, 3, 2 };
        /// <summary>Thời gian thanh nhân nhảy sang ô kế (s).</summary>
        public double SliderStepSeconds = 0.35;
        /// <summary>Cấp mở 4 booster (BoosterConfig: VIP Bus 4, Sky Grab 8, Magic Wand 13, Bus Claw 16).</summary>
        public int[] BoosterUnlock = { 4, 8, 13, 16 };
        /// <summary>Giá xu mua 1 lần (BoosterConfig CoinPrice) và số nhận được (CoinRewardQuantity 3, AdsRewardQuantity 1).</summary>
        public int[] BoosterPrice = { 500, 700, 300, 600 };
        public int BoosterBuyQty = 3, BoosterAdQty = 1;
        /// <summary>Số booster tặng khi mở khoá (popup Claim; chấm đỏ "1" ở Lv.19 / Lv.77).</summary>
        public int BoosterUnlockGift = 1;
        /// <summary>Mốc "New Feature" trên popup thắng (FeatureUnlockConfig: mục có _showVictoryProgress, trong phạm vi bản đầu).</summary>
        public int[] FeatureLevels = { 6, 10, 40 };
        public string[] FeatureNames = { "Hidden", "Frozen Pixel", "Locked Bus" };
        public int MaxLevel = 300;
    }

    /// <summary>Tiến trình người chơi: level hiện tại, xu, kho booster, cài đặt. Luật thưởng / mua ở đây để test không cần Unity.</summary>
    public sealed class MetaState
    {
        public readonly MetaConfig Cfg;
        public int Level = 1;
        public int Coins;
        public readonly int[] Boosters = new int[4];
        /// <summary>Bit i = đã nhận quà mở khoá booster i (popup Claim chỉ hiện 1 lần).</summary>
        public int UnlockClaimed;
        public bool Sound = true, Music = true, Haptic = true;
        public int LevelsWon;

        public MetaState(MetaConfig cfg) { Cfg = cfg ?? new MetaConfig(); Coins = Cfg.StartCoins; }

        const string P = "buzzy.";
        public void Load(ISaveStore s)
        {
            Level = Math.Max(1, s.GetInt(P + "level", 1)); Coins = s.GetInt(P + "coins", Cfg.StartCoins);
            for (int i = 0; i < 4; i++) Boosters[i] = s.GetInt(P + "booster" + i, 0);
            UnlockClaimed = s.GetInt(P + "unlockClaimed", 0); LevelsWon = s.GetInt(P + "won", 0);
            Sound = s.GetInt(P + "sound", 1) != 0; Music = s.GetInt(P + "music", 1) != 0; Haptic = s.GetInt(P + "haptic", 1) != 0;
        }

        public void Save(ISaveStore s)
        {
            s.SetInt(P + "level", Level); s.SetInt(P + "coins", Coins);
            for (int i = 0; i < 4; i++) s.SetInt(P + "booster" + i, Boosters[i]);
            s.SetInt(P + "unlockClaimed", UnlockClaimed); s.SetInt(P + "won", LevelsWon);
            s.SetInt(P + "sound", Sound ? 1 : 0); s.SetInt(P + "music", Music ? 1 : 0); s.SetInt(P + "haptic", Haptic ? 1 : 0);
            s.Save();
        }

        // ---------------------------------------------------------------- thưởng thắng
        /// <summary>Chỉ số ô thanh nhân đang sáng tại thời điểm t (chạy qua lại 0..n-1).</summary>
        public int SliderIndex(double t)
        {
            int n = Cfg.AdMultipliers.Length; if (n <= 1 || Cfg.SliderStepSeconds <= 0) return 0;
            int step = (int)Math.Floor(t / Cfg.SliderStepSeconds); int period = 2 * (n - 1);
            int k = step % period; return k < n ? k : period - k;
        }
        public int AdMultiplierAt(double t) { return Cfg.AdMultipliers[SliderIndex(t)]; }
        public int CoinReward { get { return Cfg.WinCoins; } }
        public int AdReward(double t) { return Cfg.WinCoins * AdMultiplierAt(t); }

        /// <summary>Xong level: cộng xu (đã nhân), sang level kế (không vượt MaxLevel).</summary>
        public void CompleteLevel(int coinsEarned)
        {
            Coins += coinsEarned; LevelsWon++;
            if (Level < Cfg.MaxLevel) Level++;
        }

        // ---------------------------------------------------------------- "New Feature"
        /// <summary>Mốc tính năng kế tiếp sau level vừa xong; -1 nếu hết.</summary>
        public int NextFeature(int completedLevel)
        {
            for (int i = 0; i < Cfg.FeatureLevels.Length; i++) if (Cfg.FeatureLevels[i] > completedLevel) return i;
            return -1;
        }
        /// <summary>% tiến tới tính năng kế: đoạn từ mốc trước (p) tới mốc kế (m) gồm m−p level; xong level k → (k−p+1)/(m−p). Video: Lv.2 → 40 %, Lv.4 → 80 % (mốc 6).</summary>
        public double FeatureProgress(int completedLevel)
        {
            int i = NextFeature(completedLevel); if (i < 0) return 1;
            int m = Cfg.FeatureLevels[i], p = i == 0 ? 1 : Cfg.FeatureLevels[i - 1];
            return Math.Min(1.0, (double)(completedLevel - p + 1) / (m - p));
        }
        public string NextFeatureName(int completedLevel) { int i = NextFeature(completedLevel); return i < 0 ? "" : Cfg.FeatureNames[i]; }

        // ---------------------------------------------------------------- booster
        public bool IsBoosterUnlocked(int i) { return Level >= Cfg.BoosterUnlock[i]; }
        /// <summary>Booster vừa mở ở level này mà chưa nhận quà → hiện popup Claim; -1 nếu không.</summary>
        public int PendingUnlock()
        {
            for (int i = 0; i < 4; i++) if (Level >= Cfg.BoosterUnlock[i] && (UnlockClaimed & (1 << i)) == 0) return i;
            return -1;
        }
        public void ClaimUnlock(int i) { UnlockClaimed |= 1 << i; Boosters[i] += Cfg.BoosterUnlockGift; }
        public bool CanBuyBooster(int i) { return Coins >= Cfg.BoosterPrice[i]; }
        public bool BuyBooster(int i)
        {
            if (!CanBuyBooster(i)) return false;
            Coins -= Cfg.BoosterPrice[i]; Boosters[i] += Cfg.BoosterBuyQty; return true;
        }
        public void AdBooster(int i) { Boosters[i] += Cfg.BoosterAdQty; }
        public bool UseBooster(int i) { if (Boosters[i] <= 0) return false; Boosters[i]--; return true; }
    }
}
