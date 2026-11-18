using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cổng duy nhất để mua bán sản phẩm (IAP). Bên dưới dùng provider IapController (Unity IAP).
/// Dữ liệu gói (IAPDatabase) luôn đọc được kể cả khi tắt tính năng IAP.
/// Khi tắt IAP: mua hàng sẽ gọi callback thất bại, giá trả về chuỗi rỗng.
/// </summary>
public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    [Tooltip("Provider IAP (GameObject con).")]
    public IapController provider;

    private bool _isInited;

    public static IapController Provider => Instance != null ? Instance.provider : null;
    public static bool IsReady => Instance != null && Instance._isInited && Instance.provider != null;

    /// <summary>Danh sách gói IAP (đọc từ provider, không phụ thuộc trạng thái bật/tắt).</summary>
    public static IAPDatabase InappDatabase
    {
        get
        {
            if (Provider != null && Provider.inappDatabase != null)
                return Provider.inappDatabase;
            var _gameController = GameController.Instance;
            if (_gameController != null && _gameController.dataContain != null)
                return _gameController.dataContain.iapDatabase;
            return null;
        }
    }

    private UnityAction _actionBuyFault;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    public void Init()
    {
        Instance = this;
        if (provider == null)
            provider = GetComponentInChildren<IapController>(true);
        if (provider == null)
        {
            Debug.LogWarning("[IAPManager] Chưa gán provider IAP.");
            return;
        }
        provider.Init();
        _isInited = true;
    }

    /// <summary>Callback khi mua thất bại (gán trước khi gọi BuyProduct).</summary>
    public static UnityAction ActBuyFault
    {
        set
        {
            if (Instance != null) Instance._actionBuyFault = value;
            if (Provider != null) Provider.ActBuyFault = value;
        }
    }

    public static void BuyProduct(TypePackIAP typePack, GameObject btnBuy = null)
    {
        if (!IsReady)
        {
            Debug.LogWarning("[IAPManager] IAP đang tắt, bỏ qua mua: " + typePack);
            _InvokeBuyFault();
            return;
        }
        Provider.BuyProduct(typePack, btnBuy);
    }

    public static void BuyProductNotInapp(TypePackIAP typePack)
    {
        if (!IsReady)
        {
            _InvokeBuyFault();
            return;
        }
        Provider.BuyProductNotInapp(typePack);
    }

    public static string GetPrice(string idIAPPack)
    {
        return IsReady ? Provider.GetPrice(idIAPPack) : string.Empty;
    }

    public static string GetPrice(TypePackIAP typePack)
    {
        return IsReady ? Provider.GetPrice(typePack) : string.Empty;
    }

    public static void RestorePurchases()
    {
        if (IsReady) Provider.RestorePurchases();
    }

    private static void _InvokeBuyFault()
    {
        if (Instance != null && Instance._actionBuyFault != null)
        {
            var _action = Instance._actionBuyFault;
            Instance._actionBuyFault = null;
            _action.Invoke();
        }
    }
}
