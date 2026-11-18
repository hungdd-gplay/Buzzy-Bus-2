using UnityEngine;

/// <summary>
/// Quản lý hiệu ứng. Cấu trúc:
///   FXManager
///     ├─ FXController          : pool particle/prefab hiệu ứng theo loại
///     └─ MoneyEffectController : hiệu ứng bay tiền/vật phẩm về UI
/// </summary>
public class FXManager : MonoBehaviour
{
    public static FXManager Instance { get; private set; }

    public FXController fxController;
    public MoneyEffectController moneyEffectController;

    private bool _isInited;

    public static bool IsReady => Instance != null && Instance._isInited;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        if (fxController == null)
            fxController = GetComponentInChildren<FXController>(true);
        if (moneyEffectController == null)
            moneyEffectController = GetComponentInChildren<MoneyEffectController>(true);
    }

    public void Init()
    {
        Instance = this;
        if (fxController == null)
            fxController = GetComponentInChildren<FXController>(true);
        if (moneyEffectController == null)
            moneyEffectController = GetComponentInChildren<MoneyEffectController>(true);
        if (fxController != null)
            fxController.Init();
        _isInited = true;
    }

    /// <summary>Lấy hiệu ứng từ pool. Trả về null nếu tính năng Effect đang tắt.</summary>
    public static GameObject GetFX(FXController.TypeFX type)
    {
        if (!IsReady || Instance.fxController == null)
            return null;
        return Instance.fxController.GetFX(type);
    }
}
