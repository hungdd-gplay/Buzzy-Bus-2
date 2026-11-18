/// <summary>
/// Item thu thập của Event Sale. Logic chung (spawn, nhặt, bay về khung...) ở CollectItemBase.
/// Item event riêng: kế thừa class này và override các hàm *Handle.
/// </summary>
public class EventSaleItem : CollectItemBase
{
    public EventSaleType typeEvent;
}
