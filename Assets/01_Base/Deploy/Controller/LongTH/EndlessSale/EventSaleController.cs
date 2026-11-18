using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventSaleController : MonoBehaviour
{
    public List<EventBase> lstEventSales;

    public void Init()
    {
        for (int i = 0; i < lstEventSales.Count; i++)
        {
            lstEventSales[i].Init();
        }
    }

    public void InitHome()
    {
        for (int i = 0; i < lstEventSales.Count; i++)
        {
            lstEventSales[i].InitHome();
        }
    }

    public void InitStateHome()
    {
        for (int i = 0; i < lstEventSales.Count; i++)
        {
            lstEventSales[i].InitStateHome();
        }
    }

    public void OnLeftHome()
    {
        for (int i = 0; i < lstEventSales.Count; i++)
        {
            lstEventSales[i].OnLeftHome();
        }
    }

    public EventBase GetEventSales(EventSaleType type)
    {
        for (int i = 0; i < lstEventSales.Count; i++)
        {
            if (lstEventSales[i].typeEvent == type)
            {
                return lstEventSales[i];
            }
        }

        return null;
    }

    public EventBase GetEventSaleActive()
    {
        for (int i = 0; i < lstEventSales.Count; i++)
        {
            if (lstEventSales[i].IsEventActive())
            {
                return lstEventSales[i];
            }
        }

        return null;
    }

    public virtual void AddItem(EventSaleType type, int amount)
    {
        for (int i = 0; i < lstEventSales.Count; i++)
        {
            if (lstEventSales[i].typeEvent == type)
            {
                lstEventSales[i].AddItem(amount);
                return;
            }
        }
    }

    public void ResetDay()
    {
        for (int i = 0; i < lstEventSales.Count; i++)
        {
            lstEventSales[i].ResetDay();
        }
    }

    public bool IsHasEventSaleReward()
    {
        for (int i = 0; i < lstEventSales.Count; i++)
        {
            if (lstEventSales[i].IsHasClaimReward())
                return true;
        }

        return false;
    }
}
