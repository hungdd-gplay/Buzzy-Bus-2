using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class ShopItemBase : MonoBehaviour
{
    public TypePackIAP typePack;
    [SerializeField] protected Button buyBtn;
    [SerializeField] protected Text priceTxt;

    protected Action<object> actionIAPInited;
    protected SalePackDataModel pack;

    public virtual void Init()
    {
        buyBtn.onClick.AddListener(Buy);

        priceTxt.text = IAPManager.GetPrice(typePack);
        pack = GameController.Instance.dataContain.salePackDatabase.GetPack(typePack);

        actionIAPInited = (s) => { priceTxt.text = IAPManager.GetPrice(typePack); };
        EventDispatcher.EventDispatcher.Instance.RegisterListener(EventID.IAP_INIT_DONE, actionIAPInited);
    }    

    public virtual void Buy()
    {
        IAPManager.BuyProduct(typePack, buyBtn.gameObject);
    }    

    protected virtual void OnDestroy()
    {
        EventDispatcher.EventDispatcher.Instance.RemoveListener(EventID.IAP_INIT_DONE, actionIAPInited);
    }    
}
