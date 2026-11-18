using DG.Tweening;
using MagneticScrollView;
using Org.BouncyCastle.Crypto.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BaseProject.Core.Feature;

public class SaleInPopup : MonoBehaviour
{
    [SerializeField] private MagneticScrollRect scrollRect;
    [SerializeField] protected RectTransform parentContent;
    [SerializeField] protected RectTransform otherContent;
    public Transform parentSale;
    public RectTransform newLocation;
    public RectTransform oldLocation;
    protected Dictionary<TypePackIAP, ShopItemBase> saleItems = new Dictionary<TypePackIAP, ShopItemBase>();

    protected Action<object> actionReload;

    private void OnEnable()
    {
        parentContent.sizeDelta = new Vector2(parentContent.parent.GetComponent<RectTransform>().sizeDelta.x, parentContent.sizeDelta.y);
        //if (!GameController.Instance.segmentController.GetCurrentSegment().adsNewData.config.mrec.CheckActive(AdsNewSegmentData.BannerMrec.MrecType.Win))
        //{
        Init();
        //}

    }

    public void Init()
    {
        InitState();

        actionReload = (sender) =>
        {
            InitState();
        };

        EventDispatcher.EventDispatcher.Instance.RegisterListener(EventID.RELOAD_SALE_POPUP, actionReload);
    }

    public virtual void InitState()
    {
        ResetPack();
        if (GameController.IsFeatureOn(FeatureId.SalePack) && GameController.Instance.packController.HasPackSale())
        {
            var packActiveSale = GameController.Instance.packController.activeSalePacks;
            List<TypePackIAP> lstDel = new List<TypePackIAP>();
            foreach (var item in saleItems.Keys)
            {
                int count = 0;

                if (packActiveSale.Find((s) => s.packModel.typePackIAP == item))
                {
                    count++;
                }

                if (count == 0)
                {
                    Destroy(saleItems[item].gameObject);
                    lstDel.Add(item);
                }
            }

            foreach (var item in lstDel)
            {
                saleItems.Remove(item);
            }

            for (int i = 0; i < packActiveSale.Count; i++)
            {
                int index = i;
                var pack = packActiveSale[index].packModel;
                if (saleItems.ContainsKey(pack.typePackIAP))
                {
                    if (packActiveSale[index].CheckActive())
                    {
                        saleItems[pack.typePackIAP].gameObject.SetActive(true);
                    }
                }
                else
                {
                    if (pack.typeSale != TypeSale.PriorityLow && pack.isShowInPopup)
                    {
                        // Điều kiện xuất hiện (ví dụ ProBundle cần mua Master / Mega) đã nằm trong SalePackDatabase.requireBought
                        var pa = Instantiate(pack.shopPopup, parentSale);
                        pa.Init();

                        saleItems.Add(pack.typePackIAP, pa);
                    }
                }
            }
        }

        if (saleItems.Count < 1)
        {
            CloseSale();
        }
        else
        {
            OpenSale();
        }
    }

    public void OpenSale()
    {
        parentContent.localScale = Vector3.zero;
        parentContent.gameObject.SetActive(true);
        scrollRect.StartAutoArranging();

        parentContent.DOScale(1f, 0.3f);
        otherContent.DOAnchorPos(newLocation.anchoredPosition, 0.3f);
    }

    public void CloseSale()
    {
        otherContent.DOAnchorPos(oldLocation.anchoredPosition, 0.3f);
        parentContent.DOScale(0f, 0.3f).OnComplete(() =>
        {
            scrollRect.StopAutoArranging();
            parentContent.gameObject.SetActive(false);
        });
    }

    public void ResetPack()
    {
        foreach (var item in saleItems)
        {
            item.Value.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        EventDispatcher.EventDispatcher.Instance.RemoveListener(EventID.RELOAD_SALE_POPUP, actionReload);
    }
}
