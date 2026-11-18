using Buzzy.Core;
using Xunit;

/// <summary>M5: luật meta (thưởng, New Feature %, booster, lưu).</summary>
public class MetaTests
{
    [Fact]
    public void FeatureProgress_MatchesVideo()
    {
        var m = new MetaState(new MetaConfig());
        Assert.Equal(0.4, m.FeatureProgress(2), 3);     // V4: xong Lv.2 → 40 %
        Assert.Equal(0.8, m.FeatureProgress(4), 3);     // V4: xong Lv.4 → 80 %
        Assert.Equal("Hidden", m.NextFeatureName(2));
        Assert.Equal(1.0, m.FeatureProgress(5), 3);
        Assert.Equal("Frozen Pixel", m.NextFeatureName(6));
        Assert.Equal(0.25, m.FeatureProgress(6), 3);    // đoạn 6→10: 4 level
        Assert.Equal("", m.NextFeatureName(40));
    }

    [Fact]
    public void Slider_BouncesAndRewards()
    {
        var m = new MetaState(new MetaConfig { SliderStepSeconds = 1 });
        int[] want = { 2, 3, 4, 3, 2, 3, 4, 3, 2, 3, 4 };   // x2-x3-x4-x3-x2 chạy qua lại
        for (int t = 0; t < want.Length; t++) Assert.Equal(want[t], m.AdMultiplierAt(t + 0.5));
        Assert.Equal(40, m.CoinReward);
        Assert.Equal(160, m.AdReward(2.5));              // nút AD "160" khi ô x4 sáng
        Assert.Equal(80, m.AdReward(0.5));               // "80" khi ô x2
    }

    [Fact]
    public void Complete_Unlock_Buy_Save()
    {
        var m = new MetaState(new MetaConfig());
        Assert.Equal(600, m.Coins);
        m.CompleteLevel(40); Assert.Equal(640, m.Coins); Assert.Equal(2, m.Level);
        Assert.Equal(-1, m.PendingUnlock());
        m.CompleteLevel(40); m.CompleteLevel(40);                 // Level 4
        Assert.Equal(0, m.PendingUnlock());                       // VIP Bus mở ở 4, chưa nhận
        m.ClaimUnlock(0); Assert.Equal(1, m.Boosters[0]); Assert.Equal(-1, m.PendingUnlock());
        Assert.True(m.UseBooster(0)); Assert.False(m.UseBooster(0));
        Assert.True(m.BuyBooster(0)); Assert.Equal(3, m.Boosters[0]); Assert.Equal(720 - 500, m.Coins);
        Assert.False(m.BuyBooster(1));                            // 700 > 220
        m.Sound = false;
        var store = new MemorySaveStore(); m.Save(store);
        var m2 = new MetaState(new MetaConfig()); m2.Load(store);
        Assert.Equal(m.Level, m2.Level); Assert.Equal(m.Coins, m2.Coins); Assert.Equal(3, m2.Boosters[0]); Assert.False(m2.Sound); Assert.Equal(1, m2.UnlockClaimed);
    }
}
