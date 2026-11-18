using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý tất cả item trong gameplay: khởi tạo, sinh nút UI, dùng item (kiểm tra + trừ số lượng),
/// tự kích hoạt item chọn trước level, cập nhật hồi chiêu / thời gian tác dụng, pool object Spawn.
/// Item là các component ItemBase (gán trong list hoặc đặt làm con của object này).
/// </summary>
public class ItemController : MonoBehaviour
{
    [SerializeField] private List<ItemBase> items = new List<ItemBase>();
    [Tooltip("Tự thêm các ItemBase nằm trong object con (không cần kéo vào list).")]
    [SerializeField] private bool isAutoFindItemInChildren = true;

    public ItemInGameView view;
    public SuggetUseItemInGame suggetUseItemInGame;
    [HideInInspector] public bool isUsedItem;

    public event Action<ItemBase> OnItemActivated;
    public event Action<ItemBase> OnItemEnded;

    private readonly Dictionary<GiftType, ItemBase> _dicItem = new Dictionary<GiftType, ItemBase>();
    private readonly Dictionary<GameObject, Stack<GameObject>> _dicPool = new Dictionary<GameObject, Stack<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> _dicSpawnedToPrefab = new Dictionary<GameObject, GameObject>();
    private Coroutine _coroutineActiveItemSellectLevel;

    public IReadOnlyList<ItemBase> Items => items;

    #region Init

    public void Init()
    {
        isUsedItem = false;
        _CollectItems();

        var _lsItemGamePlayUI = new List<ItemBase>();
        foreach (var _item in items)
        {
            _item.Setup(this);
            _item.OnLevelStart();
            if (_item.getPlace == ItemBase.GetPlace.GamePlayUI && _item.IsEnable)
                _lsItemGamePlayUI.Add(_item);
        }

        if (suggetUseItemInGame != null)
            suggetUseItemInGame.Init();
        if (GamePlayController.Instance != null && GamePlayController.Instance.gameScene != null)
            GamePlayController.Instance.gameScene.SpawnItemsUI(_lsItemGamePlayUI);
        if (view != null)
            view.Init();
    }

    private void _CollectItems()
    {
        if (items == null)
            items = new List<ItemBase>();
        if (isAutoFindItemInChildren)
        {
            foreach (var _child in GetComponentsInChildren<ItemBase>(true))
                if (!items.Contains(_child))
                    items.Add(_child);
        }
        items.RemoveAll(i => i == null);

        _dicItem.Clear();
        foreach (var _item in items)
        {
            if (_dicItem.ContainsKey(_item.typeItem))
            {
                Debug.LogWarning($"[ItemController] Trùng item {_item.typeItem} ({_item.name}), bỏ qua.", _item);
                continue;
            }
            _dicItem.Add(_item.typeItem, _item);
        }
        items.RemoveAll(i => !_dicItem.ContainsValue(i));
    }

    #endregion

    #region Dùng item

    /// <summary>
    /// Dùng item: kiểm tra còn item + điều kiện, kích hoạt tác dụng, trừ 1 (nếu isConsume và cấu hình cho phép).
    /// </summary>
    public bool UseItem(GiftType typeItem, bool isConsume = true)
    {
        var _item = GetItem(typeItem);
        if (_item == null)
            return false;
        if (isConsume && !_item.IsHasItem)
            return false;
        if (!_item.ActiveItem())
            return false;

        isUsedItem = true;
        if (isConsume && _item.IsConsumeOnActive && !_item.IsUnlimited)
            GameController.Instance.dataContain.giftDatabase.Claim(typeItem, -1, Reason.in_campain_level);

        var _itemUI = _item.GetItemUI();
        if (_itemUI != null)
            _itemUI.InitState();
        return true;
    }

    public bool IsCanUseItem(GiftType typeItem)
    {
        var _item = GetItem(typeItem);
        return _item != null && _item.IsCanUseItem();
    }

    public bool IsHasItem(GiftType typeItem)
    {
        return _dicItem.ContainsKey(typeItem);
    }

    /// <summary>Lấy item theo loại. Null nếu gameplay không có item này.</summary>
    public ItemBase GetItem(GiftType typeItem)
    {
        _dicItem.TryGetValue(typeItem, out var _item);
        return _item;
    }

    public T GetItem<T>() where T : ItemBase
    {
        foreach (var _item in items)
            if (_item is T _itemT)
                return _itemT;
        return null;
    }

    public List<ItemBase> GetItems(ItemBase.GetPlace place)
    {
        return items.FindAll(i => i.getPlace == place);
    }

    /// <summary>Kích hoạt các item người chơi đã chọn ở PlayCampainBox (getPlace = SellectLevel).</summary>
    public void ActiveItemSellectLevel()
    {
        if (_coroutineActiveItemSellectLevel != null)
            StopCoroutine(_coroutineActiveItemSellectLevel);
        _coroutineActiveItemSellectLevel = StartCoroutine(_ActiveItemSellectLevelHandle());
    }

    private IEnumerator _ActiveItemSellectLevelHandle()
    {
        yield return new WaitUntil(() => GamePlayController.Instance.IsObstaclesReady());
        if (!GamePlayController.Instance.IsHasTutorialInLevel())
        {
            foreach (var _item in items)
            {
                if (_item.getPlace == ItemBase.GetPlace.SellectLevel && UseProfile.IsUseItem(_item.typeItem))
                    UseItem(_item.typeItem);
            }
        }
        GamePlayController.Instance.SetLockReturn(false);
        _coroutineActiveItemSellectLevel = null;
    }

    public void InteractButton(bool isInteract)
    {
        foreach (var _item in items)
            if (_item.getPlace == ItemBase.GetPlace.GamePlayUI)
                _item.InteractButton(isInteract);
    }

    #endregion

    #region Level

    /// <summary>Gọi khi bắt đầu / chơi lại level: reset số lần dùng, hồi chiêu.</summary>
    public void OnLevelStart()
    {
        isUsedItem = false;
        foreach (var _item in items)
            _item.OnLevelStart();
    }

    /// <summary>Gọi khi thắng / thua / thoát: kết thúc mọi tác dụng đang chạy.</summary>
    public void OnLevelEnd()
    {
        foreach (var _item in items)
            _item.OnLevelEnd();
    }

    public void PauseItems(bool isPause)
    {
        foreach (var _item in items)
            _item.Pause(isPause);
    }

    private void Update()
    {
        float _deltaTime = Time.deltaTime;
        for (int i = 0; i < items.Count; i++)
            items[i].Tick(_deltaTime);
    }

    #endregion

    #region Sự kiện (ItemBase gọi)

    public void NotifyItemActivated(ItemBase item)
    {
        OnItemActivated?.Invoke(item);
        if (GamePlayController.Instance != null)
            GamePlayController.Instance.OnUseItemSuggetBuy(item.typeItem);
        if (suggetUseItemInGame != null)
            suggetUseItemInGame.ResetSugget();
    }

    public void NotifyItemEnded(ItemBase item)
    {
        OnItemEnded?.Invoke(item);
    }

    #endregion

    #region Pool

    public GameObject SpawnFromPool(GameObject prefab, Vector3 position, Transform parent = null)
    {
        if (!_dicPool.TryGetValue(prefab, out var _stack))
        {
            _stack = new Stack<GameObject>();
            _dicPool.Add(prefab, _stack);
        }

        GameObject _spawned = null;
        while (_stack.Count > 0 && _spawned == null)
            _spawned = _stack.Pop();
        if (_spawned == null)
        {
            _spawned = Instantiate(prefab, position, Quaternion.identity, parent != null ? parent : transform);
            _dicSpawnedToPrefab[_spawned] = prefab;
        }
        else
        {
            _spawned.transform.SetParent(parent != null ? parent : transform, true);
            _spawned.transform.position = position;
        }
        _spawned.SetActive(true);
        return _spawned;
    }

    public void DespawnToPool(GameObject spawned)
    {
        if (spawned == null)
            return;
        spawned.SetActive(false);
        if (_dicSpawnedToPrefab.TryGetValue(spawned, out var _prefab) && _dicPool.TryGetValue(_prefab, out var _stack))
            _stack.Push(spawned);
        else
            Destroy(spawned);
    }

    #endregion

    private void OnDestroy()
    {
        if (_coroutineActiveItemSellectLevel != null)
            StopCoroutine(_coroutineActiveItemSellectLevel);
    }
}
