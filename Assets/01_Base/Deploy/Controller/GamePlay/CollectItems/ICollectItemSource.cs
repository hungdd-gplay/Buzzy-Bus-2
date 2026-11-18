using UnityEngine;

/// <summary>
/// Nguồn sinh item thu thập trong level (một event hoặc một challenge).
/// ChallengeBase và EventBase đã cài sẵn; tính năng mới chỉ cần implement interface này.
/// </summary>
public interface ICollectItemSource
{
    /// <summary>Định danh dùng cho log / tool, ví dụ "Challenge.WeeklyChallenge".</summary>
    string IdCollect { get; }

    /// <summary>Nguồn đang chạy và được phép rơi item trong level hiện tại.</summary>
    bool IsCollectActive();

    /// <summary>Cấu hình item thu thập trong segment.</summary>
    CollectItemConfig CollectConfig { get; }

    CollectItemBase PrefabCollectItem { get; }

    Sprite IconCollect { get; }

    /// <summary>Số item sẽ rơi trong level này (đã tính level hướng dẫn và giới hạn).</summary>
    int GetNumItemSpawnInLevel();

    /// <summary>Cộng số item nhặt được vào tiến độ của event / challenge.</summary>
    void AddCollectedItem(int amount);
}

/// <summary>Bộ điều khiển item thu thập trong gameplay (item gọi ngược lại khi được nhặt).</summary>
public interface ICollectItemController
{
    void OnItemCollected(CollectItemBase item, int amount);
    int GetScoreItem(CollectItemBase item);
    ItemShowFarme GetFrame(CollectItemBase item);
}

public static class CollectItemHelper
{
    /// <summary>Số item rơi chung cho mọi nguồn: level mở khoá = numItemTutorial, còn lại theo bảng trọng số, rồi giới hạn max.</summary>
    public static int GetNumItemSpawn(CollectItemConfig config, int levelUnlock, System.Func<int> getNumByWeight)
    {
        if (config != null && !config.isEnable)
            return 0;
        int _num = config != null && UseProfile.CurrentLevel == levelUnlock
            ? config.numItemTutorial
            : (getNumByWeight != null ? getNumByWeight() : 0);
        if (config != null && config.maxItemPerLevel > 0)
            _num = Mathf.Min(_num, config.maxItemPerLevel);
        return Mathf.Max(0, _num);
    }
}
