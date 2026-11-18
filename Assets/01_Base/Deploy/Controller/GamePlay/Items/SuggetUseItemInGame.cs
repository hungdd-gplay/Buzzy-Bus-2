using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gợi ý dùng item: người chơi không thao tác sau timeDelaySugget giây thì nháy nút item đầu tiên
/// (theo thứ tự trong danh sách) đang dùng được.
/// </summary>
public class SuggetUseItemInGame : MonoBehaviour
{
    [Serializable]
    public class SuggetItemData
    {
        public GiftType typeItem;
        [Tooltip("Ảnh viền sáng khi gợi ý. Để trống dùng ảnh mặc định của ItemUI.")]
        public Sprite highlight;
    }

    [SerializeField] private float timeDelaySugget = 15f;
    [Tooltip("Thứ tự ưu tiên gợi ý (trên cùng ưu tiên nhất).")]
    [SerializeField] private List<SuggetItemData> lsSuggetItem = new List<SuggetItemData>();

    private float _timer;
    private bool _isSuggeting;
    private Action<object> _onBuyItem;

    private Dictionary<GiftType, ItemUI> _ItemsUI =>
        GamePlayController.Instance != null && GamePlayController.Instance.gameScene != null
            ? GamePlayController.Instance.gameScene.itemsUI
            : null;

    public void Init()
    {
        _isSuggeting = false;
        _timer = -timeDelaySugget; // Lần đầu đợi gấp đôi
        if (_onBuyItem == null)
        {
            _onBuyItem = (s) => ResetSugget();
            EventDispatcher.EventDispatcher.Instance.RegisterListener(EventID.BUYED_ITEM, _onBuyItem);
        }
    }

    public void ActiveSugget(bool isActive)
    {
        var _itemsUI = _ItemsUI;
        if (_itemsUI == null)
            return;

        if (!isActive)
        {
            foreach (var _itemUI in _itemsUI.Values)
                if (_itemUI != null)
                    _itemUI.SuggetUseItem(false);
            return;
        }

        if (_isSuggeting || GamePlayController.Instance.itemController == null)
            return;

        var _itemController = GamePlayController.Instance.itemController;
        foreach (var _data in lsSuggetItem)
        {
            if (!_itemsUI.TryGetValue(_data.typeItem, out var _itemUI) || _itemUI == null)
                continue;
            if (!_itemController.IsCanUseItem(_data.typeItem))
                continue;
            if (_data.highlight != null)
                _itemUI.hightLightItem = _data.highlight;
            _itemUI.SuggetUseItem(true);
            _isSuggeting = true;
            return;
        }
    }

    /// <summary>Gọi khi người chơi thao tác / dùng item để đếm lại từ đầu.</summary>
    public void ResetSugget()
    {
        ActiveSugget(false);
        _timer = 0;
        _isSuggeting = false;
    }

    private void Update()
    {
        if (GamePlayController.Instance == null || GamePlayController.Instance.state != StateGame.Playing)
            return;
        _timer += Time.deltaTime;
        if (_timer >= timeDelaySugget)
        {
            ActiveSugget(true);
            _timer = 0;
        }
    }

    private void OnDestroy()
    {
        if (_onBuyItem != null && EventDispatcher.EventDispatcher.HasInstance())
            EventDispatcher.EventDispatcher.Instance.RemoveListener(EventID.BUYED_ITEM, _onBuyItem);
    }
}
