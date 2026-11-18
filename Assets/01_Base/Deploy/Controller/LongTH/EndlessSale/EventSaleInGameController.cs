using System.Collections.Generic;
using BaseProject.Core.Feature;

/// <summary>
/// Item thu thập của các Event Sale trong level (Halloween, Noel, NewYear...).
/// Toàn bộ logic chung nằm ở CollectItemGameplayController.
/// </summary>
public class EventSaleInGameController : CollectItemGameplayController<EventSaleType>
{
    protected override IEnumerable<ICollectItemSource> GetSources()
    {
        if (!GameController.IsFeatureOn(FeatureId.EventSale) || GameController.Instance.eventSaleController == null)
            yield break;
        foreach (var _event in GameController.Instance.eventSaleController.lstEventSales)
            if (_event != null)
                yield return _event;
    }

    protected override EventSaleType GetKey(ICollectItemSource source)
    {
        return ((EventBase)source).typeEvent;
    }

    protected override void SetUpFrame(EventSaleType key)
    {
        if (GamePlayController.Instance.itemShowFarmeController != null)
            GamePlayController.Instance.itemShowFarmeController.SetUpFrame(key);
    }

    protected override ItemShowFarme GetFrameByKey(EventSaleType key)
    {
        return GamePlayController.Instance.itemShowFarmeController != null
            ? GamePlayController.Instance.itemShowFarmeController.GetFarme(key)
            : null;
    }

    protected override float GetMultiplyCommit(EventSaleType key, ICollectItemSource source)
    {
        float _multiply = base.GetMultiplyCommit(key, source);
#if TEST_BUILD
        // Bản test đã mua Remove Ads: x3 để test nhanh
        if (GameController.Instance.useProfile.IsRemoveAds)
            _multiply *= 3f;
#endif
        return _multiply;
    }
}
