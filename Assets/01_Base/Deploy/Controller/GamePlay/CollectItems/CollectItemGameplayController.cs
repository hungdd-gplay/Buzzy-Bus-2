using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Quản lý item thu thập trong 1 level cho một nhóm nguồn (TKey = ChallengeType / EventSaleType...).
/// Có sẵn: spawn item lên các vị trí gameplay cung cấp, tính điểm, khung hiển thị, nhặt tất cả (nam châm),
/// cộng vào tiến độ khi thắng (và khi thua nếu cấu hình isKeepWhenLose), dọn khi chơi lại.
/// Lớp con chỉ cần cho biết danh sách nguồn và khung hiển thị.
/// </summary>
public abstract class CollectItemGameplayController<TKey> : SerializedMonoBehaviour, ICollectItemController
{
    [SerializeField] protected Transform parentItem;

    public Dictionary<TKey, int> scoreItems = new Dictionary<TKey, int>();
    public Dictionary<TKey, int> PredictItems = new Dictionary<TKey, int>();

    /// <summary>(item, số điểm) mỗi lần nhặt.</summary>
    public event Action<CollectItemBase, int> OnCollected;

    protected readonly Dictionary<TKey, List<CollectItemBase>> dicItems = new Dictionary<TKey, List<CollectItemBase>>();
    protected readonly Dictionary<TKey, ICollectItemSource> dicSource = new Dictionary<TKey, ICollectItemSource>();
    private readonly Dictionary<CollectItemBase, TKey> _dicItemToKey = new Dictionary<CollectItemBase, TKey>();
    private bool _isCommitted;

    #region Lớp con cung cấp

    /// <summary>Tất cả nguồn có thể rơi item (kể cả đang tắt, controller tự lọc IsCollectActive).</summary>
    protected abstract IEnumerable<ICollectItemSource> GetSources();

    protected abstract TKey GetKey(ICollectItemSource source);

    protected virtual void SetUpFrame(TKey key)
    {
    }

    protected virtual ItemShowFarme GetFrameByKey(TKey key)
    {
        return null;
    }

    /// <summary>Hệ số nhân khi cộng vào tiến độ.</summary>
    protected virtual float GetMultiplyCommit(TKey key, ICollectItemSource source)
    {
        return source.CollectConfig != null && source.CollectConfig.multiplyOnWin > 0f ? source.CollectConfig.multiplyOnWin : 1f;
    }

    /// <summary>Gọi thêm sau khi đã cộng tiến độ lúc thắng (ví dụ: challenge không cần item).</summary>
    protected virtual void OnWinLevelHandle()
    {
    }

    #endregion

    #region Spawn

    public virtual void Init()
    {
        ClearItems();
    }

    /// <summary>Tạo item cho mọi nguồn đang chạy và gắn lên các vị trí GamePlayController.GetTransformsSpawnItem().</summary>
    public virtual void SpawnItems()
    {
        ClearItems();
        var _lsTransformSpawn = GamePlayController.Instance != null
            ? new List<Transform>(GamePlayController.Instance.GetTransformsSpawnItem())
            : new List<Transform>();

        foreach (var _source in GetSources())
        {
            if (_source == null || !_source.IsCollectActive() || _source.PrefabCollectItem == null)
                continue;
            var _key = GetKey(_source);
            if (dicSource.ContainsKey(_key))
                continue;

            dicSource[_key] = _source;
            var _lsItem = new List<CollectItemBase>();
            int _numSpawn = _source.GetNumItemSpawnInLevel();
            for (int i = 0; i < _numSpawn; i++)
            {
                var _transformSpawn = _PopRandom(_lsTransformSpawn);
                if (_transformSpawn == null)
                    break;
                var _item = SpawnItem(_key, _source, _transformSpawn);
                if (_item != null)
                    _lsItem.Add(_item);
            }

            dicItems[_key] = _lsItem;
            PredictItems[_key] = _lsItem.Count;
            scoreItems[_key] = 0;
            SetUpFrame(_key);
        }
    }

    /// <summary>Tạo thêm 1 item của nguồn key tại vị trí bất kỳ (ví dụ: rơi ra khi phá object).</summary>
    public CollectItemBase SpawnItem(TKey key, Transform posSpawn)
    {
        return dicSource.TryGetValue(key, out var _source) ? SpawnItem(key, _source, posSpawn) : null;
    }

    protected virtual CollectItemBase SpawnItem(TKey key, ICollectItemSource source, Transform posSpawn)
    {
        var _item = Instantiate(source.PrefabCollectItem, parentItem != null ? parentItem : transform);
        _item.Setup(this, source);
        _item.Spawn(posSpawn);
        _dicItemToKey[_item] = key;
        if (dicItems.TryGetValue(key, out var _lsItem) && !_lsItem.Contains(_item))
        {
            _lsItem.Add(_item);
            PredictItems[key] = _lsItem.Count;
        }
        return _item;
    }

    public void ClearItems()
    {
        foreach (var _item in _dicItemToKey.Keys)
            if (_item != null)
                Destroy(_item.gameObject);
        _dicItemToKey.Clear();
        dicItems.Clear();
        dicSource.Clear();
        scoreItems.Clear();
        PredictItems.Clear();
        _isCommitted = false;
    }

    private static Transform _PopRandom(List<Transform> lsTransform)
    {
        lsTransform.RemoveAll(t => t == null);
        if (lsTransform.Count == 0)
            return null;
        int _index = UnityEngine.Random.Range(0, lsTransform.Count);
        var _target = lsTransform[_index];
        lsTransform.RemoveAt(_index);
        return _target;
    }

    #endregion

    #region Điểm

    public void AddScoreItem(TKey key, int value)
    {
        scoreItems.TryGetValue(key, out int _score);
        scoreItems[key] = _score + value;
    }

    public int GetScoreItem(TKey key)
    {
        return scoreItems.TryGetValue(key, out int _score) ? _score : 0;
    }

    public List<CollectItemBase> GetItems(TKey key)
    {
        return dicItems.TryGetValue(key, out var _lsItem) ? _lsItem : new List<CollectItemBase>();
    }

    /// <summary>Nhặt tất cả item còn lại (nam châm / hết level).</summary>
    public int CollectAll(bool isUseMagnet = true)
    {
        int _count = 0;
        foreach (var _item in new List<CollectItemBase>(_dicItemToKey.Keys))
            if (_item != null && _item.Collect(isUseMagnet))
                _count++;
        return _count;
    }

    #endregion

    #region Kết thúc level

    /// <summary>Thắng: cộng điểm đã nhặt (x hệ số) vào tiến độ event / challenge.</summary>
    public virtual void WinLevel()
    {
        _Commit(true);
        OnWinLevelHandle();
    }

    /// <summary>Thua: chỉ cộng các nguồn có isKeepWhenLose.</summary>
    public virtual void LoseLevel()
    {
        _Commit(false);
    }

    private void _Commit(bool isWin)
    {
        if (_isCommitted)
            return;
        _isCommitted = true;
        foreach (var _score in scoreItems)
        {
            if (_score.Value <= 0 || !dicSource.TryGetValue(_score.Key, out var _source) || _source == null)
                continue;
            if (!isWin && (_source.CollectConfig == null || !_source.CollectConfig.isKeepWhenLose))
                continue;
            int _amount = Mathf.RoundToInt(_score.Value * GetMultiplyCommit(_score.Key, _source));
            if (_amount > 0)
                _source.AddCollectedItem(_amount);
        }
    }

    #endregion

    #region ICollectItemController

    public void OnItemCollected(CollectItemBase item, int amount)
    {
        if (_dicItemToKey.TryGetValue(item, out var _key))
            AddScoreItem(_key, amount);
        OnCollected?.Invoke(item, amount);
    }

    public int GetScoreItem(CollectItemBase item)
    {
        return _dicItemToKey.TryGetValue(item, out var _key) ? GetScoreItem(_key) : 0;
    }

    public ItemShowFarme GetFrame(CollectItemBase item)
    {
        return _dicItemToKey.TryGetValue(item, out var _key) ? GetFrameByKey(_key) : null;
    }

    #endregion
}
