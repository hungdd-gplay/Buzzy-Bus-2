using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class BasePackBox : BaseBox
{
    [SerializeField] protected TypePackIAP typePack;
    [SerializeField] protected Button buyBtn;
    [SerializeField] protected Text priceTxt;
    private Action<object> actionBuyPackSuccess;

    protected SalePackDataModel pack;
    private bool _isInited;

    private static readonly Dictionary<string, BasePackBox> _dicInstance = new Dictionary<string, BasePackBox>();

    /// <summary>
    /// Mở popup gói theo đường dẫn Resources (SalePackDataModel.popupResourcePath).
    /// Nhiều gói dùng chung 1 popup: typePack ghi đè gói hiển thị.
    /// </summary>
    public static BasePackBox Open(string resourcePath, TypePackIAP typePackShow, bool isPostEventPopupClose)
    {
        if (!_dicInstance.TryGetValue(resourcePath, out var _box) || _box == null)
        {
            var _prefab = Resources.Load<BasePackBox>(resourcePath);
            if (_prefab == null)
            {
                Debug.LogError("[BasePackBox] Không tìm thấy popup gói: Resources/" + resourcePath);
                return null;
            }
            _box = Instantiate(_prefab);
            _dicInstance[resourcePath] = _box;
        }
        if (typePackShow != TypePackIAP.None)
            _box.typePack = typePackShow;
        _box.Init();
        _box.InitState();
        _box.isPostEventPopupClose = isPostEventPopupClose;
        _box.Show();
        return _box;
    }

    public TypePackIAP TypePack => typePack;

    public void Init()
    {
        if (_isInited)
            return;
        _isInited = true;
        buyBtn.onClick.AddListener(OnClickBuy);
    }

    public virtual void InitState()
    {
        priceTxt.text = IAPManager.GetPrice(typePack);
        pack = GameController.Instance.dataContain.salePackDatabase.GetPack(typePack);

        if (actionBuyPackSuccess != null)
            EventDispatcher.EventDispatcher.Instance.RemoveListener(EventID.BUY_PRODUCT_SUCCESS, actionBuyPackSuccess);
        actionBuyPackSuccess = (sender) => OnBuyPackSuccessHandle((TypePackIAP)sender);
        EventDispatcher.EventDispatcher.Instance.RegisterListener(EventID.BUY_PRODUCT_SUCCESS, actionBuyPackSuccess);
    }

    protected virtual void OnClickBuy()
    {
        IAPManager.BuyProduct(typePack, buyBtn.gameObject);
    }
    private void OnBuyPackSuccessHandle(TypePackIAP typePack)
    {
        if (this.typePack != typePack)
            return;

        OnBuyPackSuccess();
    }

    protected virtual void OnBuyPackSuccess()
    {

    }

    protected override void OnDisable()
    {
        base.OnDisable();
        EventDispatcher.EventDispatcher.Instance.RemoveListener(EventID.BUY_PRODUCT_SUCCESS, actionBuyPackSuccess);
    }
}
